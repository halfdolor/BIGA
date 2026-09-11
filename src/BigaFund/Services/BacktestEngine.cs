using BigaFund.Models;

namespace BigaFund.Services;

public enum DcaFrequency
{
    WeeklyThursday, // 每周四
    WeeklyMonday,   // 每周一
    MonthlyFirstDay // 每月第一个交易日
}

public enum StrategyType
{
    RegularDca,             // 普通定期定额
    MaTimingDca,            // 均线动态择时定投 (MA60)
    TargetProfitDca,        // 目标收益止盈定投 (如+15%止盈)
    GridTrading,            // 动态价格网格交易 (3%间距)
    ValuationPercentileDca, // 估值百分位智能定投 (滚动250日分位)
    StockBondRebalance      // 股债 50:50 动态再平衡 (5%偏离度触发)
}

public static class BacktestEngine
{
    /// <summary>
    /// 统一策略回测入口
    /// </summary>
    public static BacktestResult RunBacktest(
        IReadOnlyList<NavRecord> navHistory,
        decimal periodicAmount = 1000m,
        DcaFrequency frequency = DcaFrequency.WeeklyThursday,
        StrategyType strategyType = StrategyType.RegularDca,
        int maPeriod = 60,
        decimal targetProfitRate = 0.15m,
        decimal gridSpacing = 0.03m,
        decimal rebalanceThreshold = 0.05m,
        int valuationWindow = 250,
        decimal subscriptionFeeRate = 0.001m)
    {
        return strategyType switch
        {
            StrategyType.MaTimingDca => RunMaTimingDcaBacktest(navHistory, periodicAmount, frequency, maPeriod, subscriptionFeeRate),
            StrategyType.TargetProfitDca => RunTargetProfitDcaBacktest(navHistory, periodicAmount, frequency, targetProfitRate, subscriptionFeeRate),
            StrategyType.GridTrading => RunGridTradingBacktest(navHistory, periodicAmount, gridSpacing, subscriptionFeeRate),
            StrategyType.ValuationPercentileDca => RunValuationPercentileDcaBacktest(navHistory, periodicAmount, frequency, valuationWindow, subscriptionFeeRate),
            StrategyType.StockBondRebalance => RunStockBondRebalanceBacktest(navHistory, periodicAmount, rebalanceThreshold, subscriptionFeeRate: subscriptionFeeRate),
            _ => RunDcaBacktest(navHistory, periodicAmount, frequency, subscriptionFeeRate)
        };
    }

    /// <summary>
    /// 普通定期定额策略回测执行器
    /// </summary>
    public static BacktestResult RunDcaBacktest(
        IReadOnlyList<NavRecord> navHistory,
        decimal periodicAmount = 1000m,
        DcaFrequency frequency = DcaFrequency.WeeklyThursday,
        decimal subscriptionFeeRate = 0.001m) // 默认申购费 0.1% (通常互联网打折后)
    {
        var result = new BacktestResult
        {
            PeriodicAmount = periodicAmount,
            StrategyName = frequency switch
            {
                DcaFrequency.WeeklyMonday => "每周一定投 (普通定额)",
                DcaFrequency.WeeklyThursday => "每周四定投 (普通定额)",
                DcaFrequency.MonthlyFirstDay => "每月初定投 (普通定额)",
                _ => "普通定投策略"
            },
            StrategyDescription = "在固定时间按固定金额买入，不考虑价格高低与市场估值。"
        };

        if (navHistory == null || navHistory.Count < 2 || periodicAmount <= 0)
        {
            return result;
        }

        result.StartDate = navHistory[0].Date;
        result.EndDate = navHistory[^1].Date;

        decimal totalInvested = 0m;
        decimal totalShares = 0m;
        int periods = 0;

        var cashFlows = new List<(DateTime Date, double Amount)>();
        var timeline = new List<DcaPoint>();
        int lastInvestedMonth = -1;

        for (int i = 0; i < navHistory.Count; i++)
        {
            var record = navHistory[i];
            bool shouldInvest = IsInvestmentDay(record.Date, frequency, ref lastInvestedMonth);

            if (shouldInvest && record.UnitNav > 0)
            {
                decimal netInvest = periodicAmount * (1m - subscriptionFeeRate);
                decimal sharesBought = netInvest / record.UnitNav;
                totalShares += sharesBought;
                totalInvested += periodicAmount;
                periods++;

                cashFlows.Add((record.Date, (double)-periodicAmount));
            }

            decimal curVal = totalShares * record.UnitNav;
            decimal curRet = totalInvested > 0 ? ((curVal - totalInvested) / totalInvested) * 100m : 0m;

            timeline.Add(new DcaPoint
            {
                Date = record.Date,
                TotalInvested = totalInvested,
                CurrentValue = curVal,
                ReturnRate = curRet
            });
        }

        FinalizeResult(result, navHistory, totalShares, totalInvested, periods, cashFlows, timeline);
        return result;
    }

    /// <summary>
    /// 均线动态择时定投策略回测执行器 (Moving Average Timing DCA)
    /// 当价格低于均线（低估/高安全边际）时加倍买入；当价格高于均线（超买/高估）时少买或防御。
    /// </summary>
    public static BacktestResult RunMaTimingDcaBacktest(
        IReadOnlyList<NavRecord> navHistory,
        decimal periodicAmount = 1000m,
        DcaFrequency frequency = DcaFrequency.WeeklyThursday,
        int maPeriod = 60,
        decimal subscriptionFeeRate = 0.001m)
    {
        var result = new BacktestResult
        {
            PeriodicAmount = periodicAmount,
            StrategyName = $"均线动态择时定投 (MA{maPeriod})",
            StrategyDescription = $"基于 MA{maPeriod} 动态偏离度自适应加减仓(0.5x~2.0x)，破均线逢低加大布局，涨幅过大逢高防御。"
        };

        if (navHistory == null || navHistory.Count < 2 || periodicAmount <= 0)
        {
            return result;
        }

        result.StartDate = navHistory[0].Date;
        result.EndDate = navHistory[^1].Date;

        // 1. 预计算均线时序
        decimal[] maValues = new decimal[navHistory.Count];
        decimal rollingSum = 0m;
        for (int i = 0; i < navHistory.Count; i++)
        {
            rollingSum += navHistory[i].UnitNav;
            if (i >= maPeriod)
            {
                rollingSum -= navHistory[i - maPeriod].UnitNav;
                maValues[i] = rollingSum / maPeriod;
            }
            else
            {
                maValues[i] = rollingSum / (i + 1);
            }
        }

        decimal totalInvested = 0m;
        decimal totalShares = 0m;
        int periods = 0;

        var cashFlows = new List<(DateTime Date, double Amount)>();
        var timeline = new List<DcaPoint>();
        int lastInvestedMonth = -1;

        for (int i = 0; i < navHistory.Count; i++)
        {
            var record = navHistory[i];
            bool shouldInvest = IsInvestmentDay(record.Date, frequency, ref lastInvestedMonth);

            if (shouldInvest && record.UnitNav > 0)
            {
                decimal ma = maValues[i];
                decimal multiplier = 1.0m;

                if (ma > 0)
                {
                    decimal dev = (record.UnitNav - ma) / ma;
                    if (dev <= -0.10m)
                    {
                        multiplier = 2.0m; // 严重破位深度低估：2倍定投
                    }
                    else if (dev <= -0.05m)
                    {
                        multiplier = 1.5m; // 破位适度低估：1.5倍定投
                    }
                    else if (dev <= 0m)
                    {
                        multiplier = 1.2m; // 均线下方轻度低估：1.2倍定投
                    }
                    else if (dev <= 0.10m)
                    {
                        multiplier = 0.8m; // 均线上方小幅超买：0.8倍定投
                    }
                    else
                    {
                        multiplier = 0.5m; // 远离均线大幅超买：0.5倍防御
                    }
                }

                decimal currentInvest = periodicAmount * multiplier;
                decimal netInvest = currentInvest * (1m - subscriptionFeeRate);
                decimal sharesBought = netInvest / record.UnitNav;

                totalShares += sharesBought;
                totalInvested += currentInvest;
                periods++;

                cashFlows.Add((record.Date, (double)-currentInvest));
            }

            decimal curVal = totalShares * record.UnitNav;
            decimal curRet = totalInvested > 0 ? ((curVal - totalInvested) / totalInvested) * 100m : 0m;

            timeline.Add(new DcaPoint
            {
                Date = record.Date,
                TotalInvested = totalInvested,
                CurrentValue = curVal,
                ReturnRate = curRet
            });
        }

        FinalizeResult(result, navHistory, totalShares, totalInvested, periods, cashFlows, timeline, cashReserve: 0m);
        return result;
    }

    /// <summary>
    /// 目标收益止盈定投策略回测执行器 (Target Profit-Taking DCA)
    /// 当持仓浮动收益率达成目标时（如 +15%），自动赎回全部持仓锁定利润进入现金池，并继续按原节奏开启新一轮定投。
    /// </summary>
    public static BacktestResult RunTargetProfitDcaBacktest(
        IReadOnlyList<NavRecord> navHistory,
        decimal periodicAmount = 1000m,
        DcaFrequency frequency = DcaFrequency.WeeklyThursday,
        decimal targetProfitRate = 0.15m,
        decimal subscriptionFeeRate = 0.001m)
    {
        var result = new BacktestResult
        {
            PeriodicAmount = periodicAmount,
            StrategyName = $"目标止盈定投 (+{targetProfitRate * 100:0.#}%)",
            StrategyDescription = $"当轮次定投持仓浮动收益率达到 +{targetProfitRate * 100:0.#}% 时自动赎回锁定利润进入现金池，并立即开启新一轮定投。有效锁定收益、杜绝坐过山车。"
        };

        if (navHistory == null || navHistory.Count < 2 || periodicAmount <= 0)
        {
            return result;
        }

        result.StartDate = navHistory[0].Date;
        result.EndDate = navHistory[^1].Date;

        decimal totalInvested = 0m;
        decimal totalShares = 0m;
        decimal currentCycleInvested = 0m;
        decimal cashReserve = 0m;
        decimal realizedProfit = 0m;
        int periods = 0;
        int takeProfitRounds = 0;

        var cashFlows = new List<(DateTime Date, double Amount)>();
        var timeline = new List<DcaPoint>();
        int lastInvestedMonth = -1;

        for (int i = 0; i < navHistory.Count; i++)
        {
            var record = navHistory[i];
            bool shouldInvest = IsInvestmentDay(record.Date, frequency, ref lastInvestedMonth);

            // 1. 定期扣款买入
            if (shouldInvest && record.UnitNav > 0)
            {
                decimal netInvest = periodicAmount * (1m - subscriptionFeeRate);
                decimal sharesBought = netInvest / record.UnitNav;

                totalShares += sharesBought;
                totalInvested += periodicAmount;
                currentCycleInvested += periodicAmount;
                periods++;

                cashFlows.Add((record.Date, (double)-periodicAmount));
            }

            // 2. 盘中/收盘监控浮动收益率，判定是否达成目标止盈线
            decimal holdingVal = totalShares * record.UnitNav;
            if (totalShares > 0 && currentCycleInvested > 0)
            {
                decimal cycleReturn = (holdingVal - currentCycleInvested) / currentCycleInvested;
                if (cycleReturn >= targetProfitRate)
                {
                    // 触发止盈赎回！
                    decimal redemptionProceeds = holdingVal * (1m - subscriptionFeeRate);
                    decimal cycleProfit = redemptionProceeds - currentCycleInvested;

                    cashReserve += redemptionProceeds;
                    realizedProfit += cycleProfit;
                    takeProfitRounds++;

                    // 记录赎回现金流入
                    cashFlows.Add((record.Date, (double)redemptionProceeds));

                    // 清空本轮持仓份额，开启新一轮定投
                    totalShares = 0m;
                    currentCycleInvested = 0m;
                    holdingVal = 0m;
                }
            }

            // 3. 计算当日总资产（持仓市值 + 已落袋现金池）
            decimal totalAsset = holdingVal + cashReserve;
            decimal curRet = totalInvested > 0 ? ((totalAsset - totalInvested) / totalInvested) * 100m : 0m;

            timeline.Add(new DcaPoint
            {
                Date = record.Date,
                TotalInvested = totalInvested,
                CurrentValue = totalAsset,
                ReturnRate = curRet
            });
        }

        result.TakeProfitRounds = takeProfitRounds;
        result.RealizedProfit = realizedProfit;

        FinalizeResult(result, navHistory, totalShares, totalInvested, periods, cashFlows, timeline, cashReserve);
        return result;
    }

    /// <summary>
    /// 动态价格网格交易策略回测执行器 (Dynamic Grid Trading Strategy)
    /// 针对震荡市，建立底仓后按固定百分比间距分批挂单：逢低跌穿买入 1 格，逢高涨穿卖出 1 格，持续高抛低吸套利。
    /// </summary>
    public static BacktestResult RunGridTradingBacktest(
        IReadOnlyList<NavRecord> navHistory,
        decimal periodicAmount = 1000m,
        decimal gridSpacing = 0.03m,
        decimal subscriptionFeeRate = 0.001m)
    {
        var result = new BacktestResult
        {
            PeriodicAmount = periodicAmount,
            StrategyName = $"动态网格交易策略 ({gridSpacing * 100:0.#}%间距)",
            StrategyDescription = $"建立半仓底仓后，净值每下跌 {gridSpacing * 100:0.#}% 逢低买入 1 格，每反弹上涨 {gridSpacing * 100:0.#}% 分批卖出 1 格，在区间震荡中持续捕获高抛低吸价差收益。"
        };

        if (navHistory == null || navHistory.Count < 2 || periodicAmount <= 0)
        {
            return result;
        }

        result.StartDate = navHistory[0].Date;
        result.EndDate = navHistory[^1].Date;

        // 初始网格配置：总本金 = 每格金额 * 10 (例如 10,000 元)
        decimal totalCapital = periodicAmount * 10m;
        decimal basePosition = totalCapital * 0.5m; // 50% 底仓
        decimal cashPool = totalCapital - basePosition; // 50% 现金池用于低吸

        decimal anchorPrice = navHistory[0].UnitNav;
        decimal totalShares = (basePosition * (1m - subscriptionFeeRate)) / anchorPrice;
        decimal totalInvested = totalCapital;
        int gridTrades = 0;
        decimal gridProfit = 0m;

        var cashFlows = new List<(DateTime Date, double Amount)>
        {
            (navHistory[0].Date, (double)-totalCapital)
        };
        var timeline = new List<DcaPoint>();

        for (int i = 0; i < navHistory.Count; i++)
        {
            var record = navHistory[i];
            if (record.UnitNav <= 0) continue;

            decimal priceDev = anchorPrice > 0 ? (record.UnitNav - anchorPrice) / anchorPrice : 0m;

            // 下跌触碰网格买入线
            if (priceDev <= -gridSpacing && cashPool >= periodicAmount)
            {
                decimal buyCash = periodicAmount;
                cashPool -= buyCash;
                decimal boughtShares = (buyCash * (1m - subscriptionFeeRate)) / record.UnitNav;
                totalShares += boughtShares;
                anchorPrice = record.UnitNav; // 更新当前基准网格锚点
                gridTrades++;
            }
            // 上涨触碰网格卖出线
            else if (priceDev >= gridSpacing && totalShares > 0)
            {
                decimal sharesToSell = periodicAmount / record.UnitNav;
                if (sharesToSell > totalShares) sharesToSell = totalShares;

                if (sharesToSell > 0)
                {
                    totalShares -= sharesToSell;
                    decimal proceeds = sharesToSell * record.UnitNav * (1m - subscriptionFeeRate);
                    cashPool += proceeds;
                    gridProfit += periodicAmount * gridSpacing; // 单格套利毛利润
                    anchorPrice = record.UnitNav; // 更新当前基准网格锚点
                    gridTrades++;
                }
            }

            decimal currentAsset = totalShares * record.UnitNav + cashPool;
            decimal curRet = totalInvested > 0 ? ((currentAsset - totalInvested) / totalInvested) * 100m : 0m;

            timeline.Add(new DcaPoint
            {
                Date = record.Date,
                TotalInvested = totalInvested,
                CurrentValue = currentAsset,
                ReturnRate = curRet
            });
        }

        result.GridTradesCount = gridTrades;
        result.GridArbitrageProfit = gridProfit;

        FinalizeResult(result, navHistory, totalShares, totalInvested, gridTrades, cashFlows, timeline, cashPool);
        return result;
    }

    /// <summary>
    /// 估值百分位智能定投策略回测执行器 (Valuation Percentile DCA)
    /// 基于滚动 lookbackWindow (默认 250 交易日) 的净值历史计算当前净值所处的分位数 Percentile Rank。
    /// 分位数越低表示处于历史估值洼地（高安全边际），倍投买入；分位数越高表示处于过热阶段，减额防御。
    /// </summary>
    public static BacktestResult RunValuationPercentileDcaBacktest(
        IReadOnlyList<NavRecord> navHistory,
        decimal periodicAmount = 1000m,
        DcaFrequency frequency = DcaFrequency.WeeklyThursday,
        int window = 250,
        decimal subscriptionFeeRate = 0.001m)
    {
        var result = new BacktestResult
        {
            PeriodicAmount = periodicAmount,
            StrategyName = $"估值百分位智能定投 (滚动{window}日分位)",
            StrategyDescription = $"基于过去 {window} 个交易日净值滚动分位数自适应买入：分位≤20% 极度低估 2.0x 贪婪加仓；20%~40% 中度低估 1.5x；40%~60% 合理 1.0x；60%~80% 偏高 0.7x；>80% 极度高估 0.4x 避险。"
        };

        if (navHistory == null || navHistory.Count < 2 || periodicAmount <= 0)
        {
            return result;
        }

        result.StartDate = navHistory[0].Date;
        result.EndDate = navHistory[^1].Date;

        decimal totalInvested = 0m;
        decimal totalShares = 0m;
        int periods = 0;

        var cashFlows = new List<(DateTime Date, double Amount)>();
        var timeline = new List<DcaPoint>();
        int lastInvestedMonth = -1;

        for (int i = 0; i < navHistory.Count; i++)
        {
            var record = navHistory[i];
            bool shouldInvest = IsInvestmentDay(record.Date, frequency, ref lastInvestedMonth);

            if (shouldInvest && record.UnitNav > 0)
            {
                // 计算当前净值在历史滚动窗口中的分位数
                int startIdx = Math.Max(0, i - window + 1);
                int count = i - startIdx + 1;
                int lowerOrEqual = 0;
                for (int k = startIdx; k <= i; k++)
                {
                    if (navHistory[k].UnitNav <= record.UnitNav)
                    {
                        lowerOrEqual++;
                    }
                }

                decimal percentile = count > 0 ? (decimal)lowerOrEqual / count : 0.5m;
                decimal multiplier = 1.0m;

                if (percentile <= 0.20m)
                {
                    multiplier = 2.0m; // 估值极低，贪婪倍投
                }
                else if (percentile <= 0.40m)
                {
                    multiplier = 1.5m; // 估值偏低，加大布局
                }
                else if (percentile <= 0.60m)
                {
                    multiplier = 1.0m; // 估值适中，正常定投
                }
                else if (percentile <= 0.80m)
                {
                    multiplier = 0.7m; // 估值偏高，适度防守
                }
                else
                {
                    multiplier = 0.4m; // 估值过热，极度防守
                }

                decimal currentInvest = periodicAmount * multiplier;
                decimal netInvest = currentInvest * (1m - subscriptionFeeRate);
                decimal sharesBought = netInvest / record.UnitNav;

                totalShares += sharesBought;
                totalInvested += currentInvest;
                periods++;

                cashFlows.Add((record.Date, (double)-currentInvest));
            }

            decimal curVal = totalShares * record.UnitNav;
            decimal curRet = totalInvested > 0 ? ((curVal - totalInvested) / totalInvested) * 100m : 0m;

            timeline.Add(new DcaPoint
            {
                Date = record.Date,
                TotalInvested = totalInvested,
                CurrentValue = curVal,
                ReturnRate = curRet
            });
        }

        FinalizeResult(result, navHistory, totalShares, totalInvested, periods, cashFlows, timeline, cashReserve: 0m);
        return result;
    }

    /// <summary>
    /// 股债 50:50 动态再平衡策略回测执行器 (Dynamic Stock-Bond Rebalancing Strategy)
    /// 模拟经典的资产配置：期初投入资金中 50% 配置权益基金，50% 配置稳健固收（假定基准年化 3.5%）。
    /// 当两类资产市值因市场涨跌使得权益占比偏离 50% 超过 rebalanceThreshold（默认 5%）时触发再平衡，被动高抛低吸。
    /// </summary>
    public static BacktestResult RunStockBondRebalanceBacktest(
        IReadOnlyList<NavRecord> navHistory,
        decimal periodicAmount = 1000m,
        decimal rebalanceThreshold = 0.05m,
        decimal bondAnnualRate = 0.035m,
        decimal subscriptionFeeRate = 0.001m)
    {
        var result = new BacktestResult
        {
            PeriodicAmount = periodicAmount,
            StrategyName = $"股债 50:50 动态再平衡 ({rebalanceThreshold * 100:0.#}%偏离度)",
            StrategyDescription = $"期初配置 50% 权益基金 + 50% 固收资产(年化3.5%)。当权益市值比例偏离 50% 超过 ±{rebalanceThreshold * 100:0.#}% (即>55%或<45%) 时，自动再平衡至 50:50，实现机械化纪律高抛低吸。"
        };

        if (navHistory == null || navHistory.Count < 2 || periodicAmount <= 0)
        {
            return result;
        }

        result.StartDate = navHistory[0].Date;
        result.EndDate = navHistory[^1].Date;

        decimal totalCapital = periodicAmount * 10m; // 默认以 10 倍定投金额为初始资产包 (例如 10,000 元)
        decimal equityCapital = totalCapital * 0.5m;
        decimal bondCapital = totalCapital * 0.5m;

        decimal totalShares = (equityCapital * (1m - subscriptionFeeRate)) / navHistory[0].UnitNav;
        decimal totalInvested = totalCapital;
        int rebalanceCount = 0;

        var cashFlows = new List<(DateTime Date, double Amount)>
        {
            (navHistory[0].Date, (double)-totalCapital)
        };
        var timeline = new List<DcaPoint>();

        // 每天固收资产增长率 (按 250 交易日计)
        decimal dailyBondFactor = (decimal)Math.Pow(1.0 + (double)bondAnnualRate, 1.0 / 250.0);

        for (int i = 0; i < navHistory.Count; i++)
        {
            var record = navHistory[i];
            if (record.UnitNav <= 0) continue;

            if (i > 0)
            {
                // 固收收益累计
                bondCapital *= dailyBondFactor;
            }

            decimal equityVal = totalShares * record.UnitNav;
            decimal totalAsset = equityVal + bondCapital;
            decimal equityRatio = totalAsset > 0 ? equityVal / totalAsset : 0.5m;

            // 检查偏离度阈值触发再平衡
            if (Math.Abs(equityRatio - 0.50m) >= rebalanceThreshold && totalAsset > 0)
            {
                decimal targetVal = totalAsset * 0.50m;

                if (equityVal > targetVal)
                {
                    // 权益超配：减持权益份额，资金划转至固收 (锁定落袋利润)
                    decimal sellAmt = equityVal - targetVal;
                    decimal sharesToSell = sellAmt / record.UnitNav;
                    if (sharesToSell > totalShares) sharesToSell = totalShares;

                    totalShares -= sharesToSell;
                    bondCapital += sharesToSell * record.UnitNav * (1m - subscriptionFeeRate);
                }
                else
                {
                    // 权益低配：从固收调配资金抄底权益 (逢低买入)
                    decimal buyAmt = targetVal - equityVal;
                    if (bondCapital >= buyAmt)
                    {
                        bondCapital -= buyAmt;
                        totalShares += (buyAmt * (1m - subscriptionFeeRate)) / record.UnitNav;
                    }
                }

                rebalanceCount++;
            }

            decimal currentTotalAsset = totalShares * record.UnitNav + bondCapital;
            decimal curRet = totalInvested > 0 ? ((currentTotalAsset - totalInvested) / totalInvested) * 100m : 0m;

            timeline.Add(new DcaPoint
            {
                Date = record.Date,
                TotalInvested = totalInvested,
                CurrentValue = currentTotalAsset,
                ReturnRate = curRet
            });
        }

        result.RebalanceCount = rebalanceCount;
        result.EquityAssetValue = totalShares * navHistory[^1].UnitNav;
        result.BondAssetValue = bondCapital;

        FinalizeResult(result, navHistory, totalShares, totalInvested, rebalanceCount, cashFlows, timeline, bondCapital);
        return result;
    }

    private static bool IsInvestmentDay(DateTime date, DcaFrequency frequency, ref int lastInvestedMonth)
    {
        switch (frequency)
        {
            case DcaFrequency.WeeklyThursday:
                return date.DayOfWeek == DayOfWeek.Thursday;
            case DcaFrequency.WeeklyMonday:
                return date.DayOfWeek == DayOfWeek.Monday;
            case DcaFrequency.MonthlyFirstDay:
                if (date.Month != lastInvestedMonth)
                {
                    lastInvestedMonth = date.Month;
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static void FinalizeResult(
        BacktestResult result,
        IReadOnlyList<NavRecord> navHistory,
        decimal totalShares,
        decimal totalInvested,
        int periods,
        List<(DateTime Date, double Amount)> cashFlows,
        List<DcaPoint> timeline,
        decimal cashReserve = 0m)
    {
        result.TotalPeriods = periods;
        result.TotalInvested = totalInvested;
        result.FinalShares = totalShares;
        result.FinalAssetValue = (totalShares * navHistory[^1].UnitNav) + cashReserve;
        result.Timeline = timeline;

        // 计算一次性全仓买入收益率（作为对照组）
        if (navHistory[0].UnitNav > 0)
        {
            decimal buyHoldGrowth = (navHistory[^1].UnitNav - navHistory[0].UnitNav) / navHistory[0].UnitNav;
            result.BuyAndHoldReturnRate = buyHoldGrowth * 100m;
        }

        // 计算 XIRR (年化内部收益率)
        if (cashFlows.Count > 0 && result.FinalAssetValue > 0)
        {
            cashFlows.Add((navHistory[^1].Date, (double)result.FinalAssetValue));
            result.AnnualizedIrr = (decimal)(CalculateXirr(cashFlows) * 100.0);
        }
    }

    /// <summary>
    /// 基于牛顿-拉弗森迭代法求解 XIRR（年化内部收益率）
    /// </summary>
    private static double CalculateXirr(List<(DateTime Date, double Amount)> cashFlows)
    {
        if (cashFlows.Count < 2) return 0.0;

        DateTime d0 = cashFlows[0].Date;
        double rate = 0.1; // 初始猜测年化 10%
        const int maxIter = 100;
        const double tolerance = 1e-6;

        for (int iter = 0; iter < maxIter; iter++)
        {
            double npv = 0.0;
            double dNpv = 0.0;

            foreach (var cf in cashFlows)
            {
                double years = (cf.Date - d0).TotalDays / 365.0;
                double factor = Math.Pow(1.0 + rate, years);
                if (double.IsInfinity(factor) || double.IsNaN(factor) || Math.Abs(factor) < 1e-12)
                {
                    factor = 1e-12;
                }

                npv += cf.Amount / factor;
                dNpv -= years * cf.Amount / (factor * (1.0 + rate));
            }

            if (Math.Abs(npv) < tolerance)
            {
                return rate;
            }

            if (Math.Abs(dNpv) < 1e-12) break;

            double newRate = rate - npv / dNpv;
            // 限制每次迭代范围，避免发散
            if (newRate <= -0.999) newRate = -0.999;
            if (newRate > 10.0) newRate = 10.0;

            if (Math.Abs(newRate - rate) < tolerance)
            {
                return newRate;
            }

            rate = newRate;
        }

        return rate;
    }
}
