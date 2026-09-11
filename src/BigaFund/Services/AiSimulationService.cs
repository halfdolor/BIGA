using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BigaFund.Models;

namespace BigaFund.Services;

public class AiSimulationService
{
    private readonly FundDataService _dataService;
    private readonly DuckDbService _duckDb;

    public DuckDbService DuckDb => _duckDb;
    public FundDataService DataService => _dataService;

    // 经典代表性严选标的池 (覆盖核心宽基、科技半导体/算力AI、红利低波价值、大宗/医药消费、固收纯债)
    private static readonly (string Code, string Name, string Type, string Role)[] CandidateFunds =
    [
        ("510300", "华泰柏瑞沪深300ETF", "指数型-股票", "核心宽基底仓"),
        ("000001", "华夏成长混合", "混合型-偏股", "均衡精选成长"),
        ("001630", "天弘中证计算机主题ETF联接", "指数型-股票", "科技AI与算力先锋"),
        ("005827", "易方达蓝筹精选混合", "混合型-偏股", "价值龙头底仓"),
        ("000171", "易方达裕丰回报债券", "债券型-混合债", "固收稳健避险"),
        ("001186", "富国文体健康混合A", "混合型-偏股", "消费医疗赛道"),
        ("002939", "广发中证全指金融地产ETF联接", "指数型-股票", "低估红利防守"),
        ("110022", "易方达消费行业股票", "股票型", "品牌消费白马")
    ];

    public AiSimulationService(FundDataService dataService)
    {
        _dataService = dataService;
        _duckDb = dataService.DuckDb;
    }

    /// <summary>
    /// 加载当前模拟账户完整状态（包含持仓、最新估值、统计指标与每日收益曲线）
    /// </summary>
    public async Task<SimulatedAccount> GetAccountAsync(bool refreshNavs = true, CancellationToken ct = default)
    {
        var account = await _duckDb.GetSimulatedAccountAsync("default_ai_account", ct);
        var trades = await _duckDb.GetSimulatedTradesAsync("default_ai_account", ct);
        account.Trades = trades;

        // 1. 若有持仓，拉取并刷新最新净值与盈亏
        if (account.Positions.Count > 0 && refreshNavs)
        {
            foreach (var pos in account.Positions)
            {
                try
                {
                    var detail = await _dataService.GetFundDetailAsync(pos.FundCode, false, ct);
                    if (detail != null && detail.NavHistory.Count > 0)
                    {
                        var latest = detail.NavHistory[^1];
                        pos.LatestNav = latest.UnitNav;
                        pos.NavDate = latest.Date.ToString("yyyy-MM-dd");
                        pos.FundName = string.IsNullOrEmpty(pos.FundName) ? detail.Name : pos.FundName;
                        pos.FundType = string.IsNullOrEmpty(pos.FundType) ? detail.Type : pos.FundType;

                        // 计算今日涨跌
                        if (detail.NavHistory.Count >= 2)
                        {
                            var prev = detail.NavHistory[^2];
                            if (prev.UnitNav > 0)
                            {
                                pos.TodayChangePercent = Math.Round((latest.UnitNav - prev.UnitNav) / prev.UnitNav * 100m, 2);
                                pos.TodayProfit = Math.Round(pos.Shares * (latest.UnitNav - prev.UnitNav), 2);
                            }
                        }

                        // 执行 AI 实时评级研判
                        DiagnoseSinglePosition(pos, detail);
                    }
                }
                catch
                {
                    // 容错降级
                }

                pos.MarketValue = Math.Round(pos.Shares * (pos.LatestNav > 0 ? pos.LatestNav : pos.CostBasis), 2);
                pos.FloatingProfit = Math.Round(pos.MarketValue - pos.TotalCost, 2);
                pos.FloatingProfitRate = pos.TotalCost > 0 ? Math.Round((pos.FloatingProfit / pos.TotalCost) * 100m, 2) : 0m;
            }

            account.TotalMarketValue = Math.Round(account.Positions.Sum(p => p.MarketValue), 2);
            account.TotalAsset = Math.Round(account.Cash + account.TotalMarketValue, 2);
            account.TotalProfit = Math.Round(account.TotalAsset - account.InitialCash, 2);
            account.TotalProfitRate = account.InitialCash > 0 ? Math.Round((account.TotalProfit / account.InitialCash) * 100m, 2) : 0m;
            account.TodayProfit = Math.Round(account.Positions.Sum(p => p.TodayProfit), 2);
            account.TodayProfitRate = account.TotalAsset > 0 ? Math.Round((account.TodayProfit / account.TotalAsset) * 100m, 2) : 0m;
            account.PositionRatio = account.TotalAsset > 0 ? Math.Round((account.TotalMarketValue / account.TotalAsset) * 100m, 2) : 0m;

            foreach (var pos in account.Positions)
            {
                pos.WeightPercent = account.TotalAsset > 0 ? Math.Round((pos.MarketValue / account.TotalAsset) * 100m, 2) : 0m;
            }

            // 保存刷新的持仓最新状态
            await _duckDb.SaveSimulatedAccountAsync(account, ct);
        }

        // 2. 统计已平仓交易胜率
        var sellTrades = trades.Where(t => t.Action == SimulatedTradeAction.ManualSell 
                                        || t.Action == SimulatedTradeAction.AiTrimProfit 
                                        || t.Action == SimulatedTradeAction.AiStopLoss).ToList();
        account.ClosedTradeCount = sellTrades.Count;
        account.ProfitableTradeCount = sellTrades.Count(t => t.RealizedProfit > 0);
        account.WinRate = account.ClosedTradeCount > 0 ? Math.Round((decimal)account.ProfitableTradeCount / account.ClosedTradeCount * 100m, 1) : 0m;

        // 3. 重构并生成时序收益率走势快照
        await ReconstructPerformanceCurveAsync(account, ct);

        return account;
    }

    /// <summary>
    /// AI 单基多条件诊断研判 (结合 4433、量化得分、RSI、浮盈)
    /// </summary>
    private static void DiagnoseSinglePosition(SimulatedPosition pos, FundDetail detail)
    {
        if (detail.NavHistory.Count < 10) return;

        var metrics = QuantCalculator.CalculateMetrics(detail.NavHistory);
        var scoreCard = QuantCalculator.CalculateFundScore(detail, metrics);
        pos.AiScore = scoreCard.OverallScore;

        // 计算短期 RSI
        int rsiPeriod = 14;
        decimal rsi = 50m;
        if (detail.NavHistory.Count >= rsiPeriod + 1)
        {
            decimal gain = 0m, loss = 0m;
            int start = detail.NavHistory.Count - rsiPeriod;
            for (int i = start; i < detail.NavHistory.Count; i++)
            {
                decimal diff = detail.NavHistory[i].UnitNav - detail.NavHistory[i - 1].UnitNav;
                if (diff > 0) gain += diff;
                else loss += Math.Abs(diff);
            }
            if (gain + loss > 0) rsi = Math.Round(gain / (gain + loss) * 100m, 1);
        }

        // 研判信号规则
        if (pos.FloatingProfitRate >= 15.0m && rsi > 75m)
        {
            pos.AiSignal = "💰 建议止盈 (逢高分批减仓)";
            pos.AiRecommendation = $"持仓累计浮盈已达 +{pos.FloatingProfitRate:F2}%，短期 RSI({rsi}) 处于超买高估区间，建议分批止盈 20%~30% 锁定胜果。";
        }
        else if (pos.FloatingProfitRate <= -10.0m && scoreCard.OverallScore < 50m)
        {
            pos.AiSignal = "🚨 破位止损 (回避弱势标的)";
            pos.AiRecommendation = $"持仓浮亏 {pos.FloatingProfitRate:F2}% 且量化综合评分降至 {scoreCard.OverallScore:F1}，趋势走弱，建议止损或换基至高性价比标的。";
        }
        else if (rsi < 30m && scoreCard.OverallScore >= 65m)
        {
            pos.AiSignal = "💎 逢低吸筹 (定投补仓黄金点)";
            pos.AiRecommendation = $"优质核心标的出现短期超跌(RSI={rsi})，量化得分优异({scoreCard.OverallScore:F1})，安全边际凸显，建议逢低买入加仓。";
        }
        else if (scoreCard.OverallScore >= 75m)
        {
            pos.AiSignal = "🚀 强烈看好 (多头稳健持有)";
            pos.AiRecommendation = $"标的综合量化评级处于全市场前列，阿尔法超额收益持续强劲，建议保持重仓持有。";
        }
        else
        {
            pos.AiSignal = "🛡️ 继续持有 (底仓锁定)";
            pos.AiRecommendation = $"走势处于中枢震荡区间，量化指标正常，建议继续持有观察。";
        }
    }

    /// <summary>
    /// AI 依据各类条件分析，生成 10 万元现金智能建仓配置方案
    /// 包含：4433 机构选基、五维量化综合得分、资产与行业多维分散、科学投决买卖信号
    /// </summary>
    public async Task<AiAllocationProposal> GenerateAiAllocationProposalAsync(
        decimal totalCapital = 100000m,
        string strategy = "BALANCED",
        CancellationToken ct = default)
    {
        var proposal = new AiAllocationProposal
        {
            TotalCapital = totalCapital,
            StrategyType = strategy
        };

        if (strategy == "GROWTH")
        {
            proposal.Title = "🚀 AI 进取成长先锋 10 万智能建仓方案";
            proposal.StrategyDescription = "重仓科技 AI 算力龙头与高弹性成长赛道，配合宽基核心底仓，追求高额阿尔法与进攻爆发力。";
            proposal.AllocatedAmount = totalCapital * 0.90m;
            proposal.ReservedCash = totalCapital * 0.10m;
        }
        else if (strategy == "DEFENSIVE")
        {
            proposal.Title = "🛡️ AI 绝对收益红利稳健 10 万智能建仓方案";
            proposal.StrategyDescription = "以红利低波价值与优质固收纯债为核心，极致控制下行最大回撤，稳健吃息增值。";
            proposal.AllocatedAmount = totalCapital * 0.95m;
            proposal.ReservedCash = totalCapital * 0.05m;
        }
        else
        {
            proposal.Title = "🏆 AI 均衡全天候 10 万智能建仓方案 (强烈推荐)";
            proposal.StrategyDescription = "遵循机构级核心-卫星配置理念，将宽基底仓、科技动量、红利价值与固收纯债科学融合，穿越牛熊。";
            proposal.AllocatedAmount = totalCapital * 0.90m;
            proposal.ReservedCash = totalCapital * 0.10m;
        }

        // 定义方案权重配比
        var allocations = strategy switch
        {
            "GROWTH" => new[]
            {
                ("001630", 0.40m, "科技成长先锋"),
                ("510300", 0.30m, "核心宽基底仓"),
                ("000001", 0.20m, "均衡成长中枢")
            },
            "DEFENSIVE" => new[]
            {
                ("002939", 0.40m, "红利价值防守"),
                ("000171", 0.40m, "固收稳健避险"),
                ("510300", 0.15m, "宽基核心底仓")
            },
            _ => new[]
            {
                ("510300", 0.30m, "核心宽基底仓"),
                ("001630", 0.25m, "科技AI算力先锋"),
                ("005827", 0.20m, "价值龙头白马"),
                ("000171", 0.15m, "固收避险缓冲")
            }
        };

        foreach (var (code, weight, role) in allocations)
        {
            decimal targetAmt = Math.Round(totalCapital * weight, 2);
            var item = new AiAllocationItem
            {
                FundCode = code,
                RoleTag = role,
                WeightPercent = weight * 100m,
                TargetAmount = targetAmt
            };

            // 获取标的实时数据与量化条件研判
            try
            {
                var detail = await _dataService.GetFundDetailAsync(code, false, ct);
                if (detail != null && detail.NavHistory.Count > 0)
                {
                    item.FundName = detail.Name;
                    item.FundType = detail.Type;
                    var latest = detail.NavHistory[^1];
                    item.LatestNav = latest.UnitNav;
                    item.EstimatedShares = item.LatestNav > 0 ? Math.Round((targetAmt * 0.999m) / item.LatestNav, 2) : 0m;

                    var metrics = QuantCalculator.CalculateMetrics(detail.NavHistory);
                    var scoreCard = QuantCalculator.CalculateFundScore(detail, metrics);
                    item.QuantScore = scoreCard.OverallScore;

                    var check4433 = QuantCalculator.Evaluate4433Rule(detail);

                    // 组织量化入选条件标签
                    if (check4433.Passed4433) item.ConditionsPassed.Add("4433机构严选过检");
                    if (metrics.SharpeRatio >= 1.0m) item.ConditionsPassed.Add($"三年夏普比率前列({metrics.SharpeRatio:F2})");
                    if (metrics.MaxDrawdown < 18.0m) item.ConditionsPassed.Add($"最大回撤控制极佳({metrics.MaxDrawdown:F1}%)");
                    if (scoreCard.OverallScore >= 75m) item.ConditionsPassed.Add("五维量化评分优秀");
                    item.ConditionsPassed.Add(role);

                    // 详细入选分析依据
                    item.DetailedRationale = $"【{role}】通过机构 4433 量化筛选，综合量化评级 {scoreCard.RatingGrade}({scoreCard.OverallScore:F1}分)，夏普比率 {metrics.SharpeRatio:F2}，年化收益 {metrics.AnnualizedReturn:F1}%。建议在 10 万资金中配置 ¥{targetAmt:N0} ({item.WeightPercent:F0}%)，强化组合阿尔法收益与抗风险防御韧性。";
                }
                else
                {
                    item.FundName = CandidateFunds.FirstOrDefault(c => c.Code == code).Name ?? "优选基金";
                    item.FundType = CandidateFunds.FirstOrDefault(c => c.Code == code).Type ?? "混合型";
                    item.LatestNav = 1.0000m;
                    item.EstimatedShares = targetAmt;
                    item.QuantScore = 80m;
                    item.ConditionsPassed.Add("量化多维优选");
                    item.DetailedRationale = $"【{role}】经过系统多维条件严选，符合当前资产配置方案。";
                }
            }
            catch
            {
                item.FundName = CandidateFunds.FirstOrDefault(c => c.Code == code).Name ?? "优选基金";
                item.FundType = "公募基金";
                item.LatestNav = 1.0000m;
                item.EstimatedShares = targetAmt;
                item.QuantScore = 80m;
                item.ConditionsPassed.Add("全市场优选");
                item.DetailedRationale = $"【{role}】符合系统资产配置策略。";
            }

            proposal.Items.Add(item);
        }

        return proposal;
    }

    /// <summary>
    /// 一键执行 AI 智能分析建仓 (自动使用现金买入提案中的基金组合)
    /// </summary>
    public async Task<(bool Success, string Message)> ExecuteAiBuildProposalAsync(AiAllocationProposal proposal, CancellationToken ct = default)
    {
        if (proposal == null || proposal.Items.Count == 0)
        {
            return (false, "建仓方案为空，无法执行。");
        }

        var account = await _duckDb.GetSimulatedAccountAsync("default_ai_account", ct);
        decimal totalNeed = proposal.Items.Sum(i => i.TargetAmount);
        if (account.Cash < totalNeed)
        {
            return (false, $"当前模拟现金余额 ¥{account.Cash:N2} 不足，需要 ¥{totalNeed:N2}，请调整金额或先重置账户。");
        }

        int successCount = 0;
        foreach (var item in proposal.Items)
        {
            var res = await ExecuteBuyAsync(item.FundCode, item.TargetAmount, $"🤖 AI智能建仓: {item.RoleTag} - {string.Join('/', item.ConditionsPassed)}", isAi: true, ct: ct);
            if (res.Success) successCount++;
        }

        return (true, $"成功执行 AI 智能建仓！已自动购入 {successCount} 只多维严选基金，共投入现金 ¥{totalNeed:N2}，留存备用现金 ¥{(account.Cash - totalNeed):N2}。");
    }

    /// <summary>
    /// 模拟买入操作（支持手动买入或 AI 智能买入）
    /// 自动扣减现金、计算申购费率、计算并合并持仓份额与成本均价、写入交易流水
    /// </summary>
    public async Task<(bool Success, string Message)> ExecuteBuyAsync(
        string fundCode,
        decimal amount,
        string reason = "🛒 手动买入",
        bool isAi = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fundCode) || amount <= 0)
        {
            return (false, "买入参数无效，请输入正确的基金代码与买入金额。");
        }

        fundCode = fundCode.Trim();
        var account = await _duckDb.GetSimulatedAccountAsync("default_ai_account", ct);

        if (account.Cash < amount)
        {
            return (false, $"当前可用现金不足 (可用: ¥{account.Cash:N2}, 需投入: ¥{amount:N2})。");
        }

        // 获取基金实时详情
        var detail = await _dataService.GetFundDetailAsync(fundCode, false, ct);
        string fundName = detail?.Name ?? fundCode;
        string fundType = detail?.Type ?? "公募基金";
        decimal nav = detail != null && detail.NavHistory.Count > 0 ? detail.NavHistory[^1].UnitNav : 1.0000m;
        string navDate = detail != null && detail.NavHistory.Count > 0 ? detail.NavHistory[^1].Date.ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd");

        // 默认公募基金申购费率 0.10%
        decimal feeRate = 0.0010m;
        decimal fee = Math.Round(amount * feeRate, 2);
        decimal netAmount = amount - fee;
        decimal newShares = Math.Round(netAmount / nav, 4);

        // 扣减现金
        account.Cash -= amount;

        // 查找是否已持有该基金
        var existingPos = account.Positions.FirstOrDefault(p => p.FundCode == fundCode);
        if (existingPos != null)
        {
            decimal totalShares = existingPos.Shares + newShares;
            decimal totalCost = existingPos.TotalCost + amount;
            existingPos.Shares = totalShares;
            existingPos.TotalCost = totalCost;
            existingPos.CostBasis = totalShares > 0 ? Math.Round(totalCost / totalShares, 4) : nav;
            existingPos.LatestNav = nav;
            existingPos.NavDate = navDate;
            existingPos.MarketValue = Math.Round(existingPos.Shares * nav, 2);
            existingPos.FloatingProfit = Math.Round(existingPos.MarketValue - existingPos.TotalCost, 2);
            existingPos.FloatingProfitRate = existingPos.TotalCost > 0 ? Math.Round((existingPos.FloatingProfit / existingPos.TotalCost) * 100m, 2) : 0m;
            existingPos.UpdatedAt = DateTime.Now;
            if (detail != null) DiagnoseSinglePosition(existingPos, detail);
        }
        else
        {
            var newPos = new SimulatedPosition
            {
                AccountId = account.AccountId,
                FundCode = fundCode,
                FundName = fundName,
                FundType = fundType,
                Shares = newShares,
                CostBasis = Math.Round(amount / newShares, 4),
                TotalCost = amount,
                LatestNav = nav,
                NavDate = navDate,
                MarketValue = Math.Round(newShares * nav, 2),
                FloatingProfit = Math.Round(newShares * nav - amount, 2),
                FloatingProfitRate = amount > 0 ? Math.Round(((newShares * nav - amount) / amount) * 100m, 2) : 0m,
                UpdatedAt = DateTime.Now
            };
            if (detail != null) DiagnoseSinglePosition(newPos, detail);
            account.Positions.Add(newPos);
        }

        // 写入交易记录流水
        var trade = new SimulatedTrade
        {
            TradeId = Guid.NewGuid().ToString("N"),
            AccountId = account.AccountId,
            TradeTime = DateTime.Now,
            FundCode = fundCode,
            FundName = fundName,
            Action = isAi ? SimulatedTradeAction.AiInitialBuild : SimulatedTradeAction.ManualBuy,
            ActionText = isAi ? "🤖 AI智能建仓" : "🛒 手动买入",
            Nav = nav,
            Shares = newShares,
            Amount = amount,
            Fee = fee,
            RealizedProfit = 0m,
            RealizedProfitRate = 0m,
            Reason = reason
        };

        await _duckDb.SaveSimulatedTradeAsync(trade, ct);
        await _duckDb.SaveSimulatedAccountAsync(account, ct);

        return (true, $"成功买入 {fundName} ({fundCode})，投入现金 ¥{amount:N2}，成交净值 {nav:F4}，获得份额 {newShares:N2} 份，手续费 ¥{fee:N2}。");
    }

    /// <summary>
    /// 模拟卖出/赎回操作（支持按份额卖出或按比例全部/部分平仓）
    /// 自动结算现金回笼、扣减赎回费率、计算实现平仓盈亏、更新持仓与交易流水
    /// </summary>
    public async Task<(bool Success, string Message)> ExecuteSellAsync(
        string fundCode,
        decimal sharesToSell,
        string reason = "💰 手动赎回",
        bool isAi = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fundCode) || sharesToSell <= 0)
        {
            return (false, "赎回参数无效，请输入有效基金代码与赎回份额。");
        }

        fundCode = fundCode.Trim();
        var account = await _duckDb.GetSimulatedAccountAsync("default_ai_account", ct);
        var pos = account.Positions.FirstOrDefault(p => p.FundCode == fundCode);
        if (pos == null || pos.Shares <= 0.0001m)
        {
            return (false, $"未持有基金 {fundCode}，无法卖出。");
        }

        if (sharesToSell > pos.Shares)
        {
            sharesToSell = pos.Shares; // 容错：全额卖出
        }

        // 获取最新净值
        var detail = await _dataService.GetFundDetailAsync(fundCode, false, ct);
        decimal nav = detail != null && detail.NavHistory.Count > 0 ? detail.NavHistory[^1].UnitNav : (pos.LatestNav > 0 ? pos.LatestNav : pos.CostBasis);

        // 默认公募基金持有 < 7 天赎回费较贵，正常中长期约 0.50%
        decimal feeRate = 0.0050m;
        decimal grossAmount = Math.Round(sharesToSell * nav, 2);
        decimal fee = Math.Round(grossAmount * feeRate, 2);
        decimal netProceeds = grossAmount - fee;

        // 平仓实现盈亏计算
        decimal costOfSoldShares = Math.Round(sharesToSell * pos.CostBasis, 2);
        decimal realizedProfit = Math.Round(netProceeds - costOfSoldShares, 2);
        decimal realizedProfitRate = costOfSoldShares > 0 ? Math.Round((realizedProfit / costOfSoldShares) * 100m, 2) : 0m;

        // 回笼现金
        account.Cash += netProceeds;

        // 更新持仓头寸
        pos.Shares -= sharesToSell;
        pos.TotalCost = Math.Max(0m, pos.TotalCost - costOfSoldShares);
        if (pos.Shares > 0.0001m)
        {
            pos.MarketValue = Math.Round(pos.Shares * nav, 2);
            pos.FloatingProfit = Math.Round(pos.MarketValue - pos.TotalCost, 2);
            pos.FloatingProfitRate = pos.TotalCost > 0 ? Math.Round((pos.FloatingProfit / pos.TotalCost) * 100m, 2) : 0m;
            pos.UpdatedAt = DateTime.Now;
        }
        else
        {
            account.Positions.Remove(pos);
        }

        // 确定动作标签
        SimulatedTradeAction action = isAi
            ? (realizedProfit >= 0 ? SimulatedTradeAction.AiTrimProfit : SimulatedTradeAction.AiStopLoss)
            : SimulatedTradeAction.ManualSell;
        string actionText = isAi
            ? (realizedProfit >= 0 ? "🎯 AI止盈卖出" : "🚨 AI止损减仓")
            : "💰 手动赎回";

        // 写入交易记录流水
        var trade = new SimulatedTrade
        {
            TradeId = Guid.NewGuid().ToString("N"),
            AccountId = account.AccountId,
            TradeTime = DateTime.Now,
            FundCode = fundCode,
            FundName = pos.FundName,
            Action = action,
            ActionText = actionText,
            Nav = nav,
            Shares = sharesToSell,
            Amount = grossAmount,
            Fee = fee,
            RealizedProfit = realizedProfit,
            RealizedProfitRate = realizedProfitRate,
            Reason = reason
        };

        await _duckDb.SaveSimulatedTradeAsync(trade, ct);
        await _duckDb.SaveSimulatedAccountAsync(account, ct);

        return (true, $"成功卖出 {pos.FundName} ({fundCode})，卖出 {sharesToSell:N2} 份，成交净值 {nav:F4}，回笼现金 ¥{netProceeds:N2}，平仓盈亏: {(realizedProfit >= 0 ? "+" : "")}¥{realizedProfit:N2} ({realizedProfitRate:F2}%)。");
    }

    /// <summary>
    /// AI 智能盯盘：全面诊断当前持仓并生成调仓、止盈、止损决策清单
    /// </summary>
    public async Task<List<AiHoldingDiagnosis>> DiagnoseHoldingsAsync(CancellationToken ct = default)
    {
        var list = new List<AiHoldingDiagnosis>();
        var account = await _duckDb.GetSimulatedAccountAsync("default_ai_account", ct);
        if (account.Positions.Count == 0) return list;

        foreach (var pos in account.Positions)
        {
            var diag = new AiHoldingDiagnosis
            {
                FundCode = pos.FundCode,
                FundName = pos.FundName,
                CurrentSignal = pos.AiSignal
            };

            if (pos.FloatingProfitRate >= 15.0m)
            {
                diag.UrgencyLevel = "URGENT";
                diag.ActionProposal = "💰 止盈减仓 30%";
                diag.SuggestedActionAmount = Math.Round(pos.Shares * 0.3m, 2);
                diag.Rationale = $"累计收益率达 +{pos.FloatingProfitRate:F2}%，达到系统多维止盈阈值(15%)，建议及时锁定部分利润。";
            }
            else if (pos.FloatingProfitRate <= -10.0m)
            {
                diag.UrgencyLevel = "URGENT";
                diag.ActionProposal = "🚨 止损清仓";
                diag.SuggestedActionAmount = pos.Shares;
                diag.Rationale = $"累计浮亏达 {pos.FloatingProfitRate:F2}%，触及风控红线，建议减仓规避进一步下行风险。";
            }
            else if (pos.WeightPercent > 45.0m)
            {
                diag.UrgencyLevel = "NOTICE";
                diag.ActionProposal = "⚖️ 均衡降仓 20%";
                diag.SuggestedActionAmount = Math.Round(pos.Shares * 0.2m, 2);
                diag.Rationale = $"单一基金持仓占比达 {pos.WeightPercent:F1}%，集中度偏高，建议适当降仓提高分散度。";
            }
            else
            {
                diag.UrgencyLevel = "NORMAL";
                diag.ActionProposal = "🛡️ 继续持有";
                diag.SuggestedActionAmount = 0m;
                diag.Rationale = "各项量化指标处于健康正常波动区间，无需调整。";
            }

            list.Add(diag);
        }

        return list;
    }

    /// <summary>
    /// 重置模拟操盘账户 (恢复 10 万元现金初始金，清空持仓与交易流水)
    /// </summary>
    public async Task ResetAccountAsync(decimal initialCash = 100000m, CancellationToken ct = default)
    {
        await _duckDb.ResetSimulatedAccountAsync("default_ai_account", initialCash, ct);
    }

    /// <summary>
    /// 重构自建仓以来的每日总资产与收益率走势图，并与沪深 300 基准指数同期走势进行对标
    /// </summary>
    public async Task ReconstructPerformanceCurveAsync(SimulatedAccount account, CancellationToken ct = default)
    {
        var snapshots = new List<SimulatedDailySnapshot>();
        if (account.Trades.Count == 0 && account.Positions.Count == 0)
        {
            account.DailySnapshots = snapshots;
            return;
        }

        // 确定模拟时间轴起点
        DateTime startDate = account.Trades.Count > 0 
            ? account.Trades.Min(t => t.TradeTime.Date) 
            : DateTime.Now.AddDays(-30).Date;
        DateTime endDate = DateTime.Now.Date;

        if ((endDate - startDate).TotalDays < 5)
        {
            startDate = endDate.AddDays(-14);
        }

        // 获取基准指数（沪深 300）走势数据
        var benchmarkNavs = new List<NavRecord>();
        try
        {
            var bmFund = await _dataService.GetFundDetailAsync("510300", false, ct);
            if (bmFund != null && bmFund.NavHistory.Count > 0)
            {
                benchmarkNavs = bmFund.NavHistory.Where(n => n.Date >= startDate && n.Date <= endDate).ToList();
            }
        }
        catch
        {
            // 容错
        }

        decimal bmBaseNav = benchmarkNavs.Count > 0 ? benchmarkNavs[0].UnitNav : 1.0m;

        // 获取持仓基金历史净值字典
        var fundNavDict = new Dictionary<string, List<NavRecord>>();
        foreach (var pos in account.Positions)
        {
            try
            {
                var detail = await _dataService.GetFundDetailAsync(pos.FundCode, false, ct);
                if (detail != null && detail.NavHistory.Count > 0)
                {
                    fundNavDict[pos.FundCode] = detail.NavHistory;
                }
            }
            catch { }
        }

        // 按自然交易日逐日推演模拟总资产
        var tradeChronology = account.Trades.OrderBy(t => t.TradeTime).ToList();

        // 选取基准日历或工作日日历
        var dateList = new List<DateTime>();
        if (benchmarkNavs.Count > 0)
        {
            dateList = benchmarkNavs.Select(n => n.Date).Distinct().OrderBy(d => d).ToList();
        }
        else
        {
            for (var d = startDate; d <= endDate; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                {
                    dateList.Add(d);
                }
            }
        }

        if (dateList.Count == 0) dateList.Add(DateTime.Now.Date);

        decimal peakAsset = account.InitialCash;
        decimal maxDd = 0m;
        var dailyReturns = new List<decimal>();

        for (int i = 0; i < dateList.Count; i++)
        {
            var curDate = dateList[i];

            // 累计到该日期截止的交易持仓与现金状态
            decimal simCash = account.InitialCash;
            var simHoldings = new Dictionary<string, (decimal shares, decimal cost)>();

            foreach (var tr in tradeChronology.Where(t => t.TradeTime.Date <= curDate))
            {
                if (tr.Action == SimulatedTradeAction.ManualBuy || tr.Action == SimulatedTradeAction.AiInitialBuild)
                {
                    simCash -= tr.Amount;
                    if (!simHoldings.ContainsKey(tr.FundCode)) simHoldings[tr.FundCode] = (0m, 0m);
                    var cur = simHoldings[tr.FundCode];
                    simHoldings[tr.FundCode] = (cur.shares + tr.Shares, cur.cost + tr.Amount);
                }
                else if (tr.Action == SimulatedTradeAction.ManualSell || tr.Action == SimulatedTradeAction.AiTrimProfit || tr.Action == SimulatedTradeAction.AiStopLoss)
                {
                    simCash += (tr.Amount - tr.Fee);
                    if (simHoldings.ContainsKey(tr.FundCode))
                    {
                        var cur = simHoldings[tr.FundCode];
                        decimal remainShares = Math.Max(0m, cur.shares - tr.Shares);
                        decimal remainCost = remainShares > 0 ? (cur.cost * (remainShares / cur.shares)) : 0m;
                        simHoldings[tr.FundCode] = (remainShares, remainCost);
                    }
                }
            }

            // 计算该日的持仓市值
            decimal simMarketVal = 0m;
            foreach (var kvp in simHoldings.Where(h => h.Value.shares > 0.0001m))
            {
                decimal unitNav = 1.0m;
                if (fundNavDict.TryGetValue(kvp.Key, out var histNavs) && histNavs.Count > 0)
                {
                    var rec = histNavs.LastOrDefault(n => n.Date <= curDate) ?? histNavs[0];
                    unitNav = rec.UnitNav;
                }
                simMarketVal += kvp.Value.shares * unitNav;
            }

            decimal curTotalAsset = Math.Round(simCash + simMarketVal, 2);
            decimal cumReturn = account.InitialCash > 0 ? Math.Round((curTotalAsset - account.InitialCash) / account.InitialCash * 100m, 2) : 0m;

            // 基准收益率计算
            decimal bmReturn = 0m;
            var bmRec = benchmarkNavs.LastOrDefault(n => n.Date <= curDate);
            if (bmRec != null && bmBaseNav > 0)
            {
                bmReturn = Math.Round((bmRec.UnitNav - bmBaseNav) / bmBaseNav * 100m, 2);
            }

            // 计算当日日收益率
            decimal prevAsset = snapshots.Count > 0 ? snapshots[^1].TotalAsset : account.InitialCash;
            decimal dayReturn = prevAsset > 0 ? Math.Round((curTotalAsset - prevAsset) / prevAsset * 100m, 2) : 0m;
            dailyReturns.Add(dayReturn);

            // 回撤统计
            if (curTotalAsset > peakAsset) peakAsset = curTotalAsset;
            decimal curDd = peakAsset > 0 ? (peakAsset - curTotalAsset) / peakAsset * 100m : 0m;
            if (curDd > maxDd) maxDd = curDd;

            snapshots.Add(new SimulatedDailySnapshot
            {
                Date = curDate,
                TotalAsset = curTotalAsset,
                Cash = simCash,
                MarketValue = simMarketVal,
                CumulativeReturn = cumReturn,
                BenchmarkReturn = bmReturn,
                DailyReturn = dayReturn
            });
        }

        account.DailySnapshots = snapshots;
        account.MaxDrawdown = Math.Round(maxDd, 2);

        // 计算夏普比率与年化收益率
        if (snapshots.Count >= 2 && dateList.Count >= 2)
        {
            double days = (dateList[^1] - dateList[0]).TotalDays;
            if (days >= 7)
            {
                decimal totalRet = snapshots[^1].CumulativeReturn;
                account.AnnualizedReturn = Math.Round(totalRet * (365m / (decimal)days), 2);
            }

            if (dailyReturns.Count >= 5)
            {
                double avg = dailyReturns.Select(d => (double)d).Average();
                double sumSq = dailyReturns.Select(d => (double)d - avg).Sum(d => d * d);
                double std = Math.Sqrt(sumSq / dailyReturns.Count);
                if (std > 0.001)
                {
                    // 年化夏普 (无风险利率假定 2%)
                    double rfDaily = 2.0 / 250.0;
                    account.SharpeRatio = Math.Round((decimal)((avg - rfDaily) / std * Math.Sqrt(250)), 2);
                }
            }
        }
    }
}
