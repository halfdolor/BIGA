using System;
using System.Collections.Generic;

namespace BigaFund.Models;

/// <summary>
/// 交易动作类型
/// </summary>
public enum SimulatedTradeAction
{
    AiInitialBuild,    // 🤖 AI 智能建仓
    ManualBuy,         // 🛒 手动买入
    ManualSell,        // 💰 手动卖出/赎回
    AiTrimProfit,      // 🎯 AI 逢高止盈
    AiStopLoss,        // 🚨 AI 破位止损
    AiRebalance,       // ⚖️ AI 动态再平衡
    DepositCash,       // 💵 资金注入
    ResetAccount       // 🔄 账户重置
}

/// <summary>
/// 模拟投资账户主模型 (默认 100,000 元现金)
/// </summary>
public class SimulatedAccount
{
    public string AccountId { get; set; } = "default_ai_account";
    public string AccountName { get; set; } = "AI 智能量化模拟盘 (10万实操金)";
    public decimal InitialCash { get; set; } = 100000m; // 初始本金 10 万元
    public decimal Cash { get; set; } = 100000m;        // 当前可用现金
    public decimal TotalAsset { get; set; } = 100000m;   // 当前总资产 (现金 + 持仓市值)
    public decimal TotalMarketValue { get; set; } = 0m; // 持仓总市值
    public decimal TotalProfit { get; set; } = 0m;      // 累计总盈亏金额 (总资产 - 初始现金)
    public decimal TotalProfitRate { get; set; } = 0m;  // 累计总收益率 (%)
    public decimal TodayProfit { get; set; } = 0m;      // 今日参考盈亏金额
    public decimal TodayProfitRate { get; set; } = 0m;  // 今日参考收益率 (%)
    public decimal PositionRatio { get; set; } = 0m;    // 仓位占比 (%)
    public decimal AnnualizedReturn { get; set; } = 0m; // 年化收益率 (%)
    public decimal MaxDrawdown { get; set; } = 0m;      // 最大回撤 (%)
    public decimal SharpeRatio { get; set; } = 0m;      // 夏普比率
    public decimal WinRate { get; set; } = 0m;          // 历史交易胜率 (%)
    public int ClosedTradeCount { get; set; } = 0;      // 已平仓交易总笔数
    public int ProfitableTradeCount { get; set; } = 0;  // 盈利平仓交易笔数
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public List<SimulatedPosition> Positions { get; set; } = new();
    public List<SimulatedTrade> Trades { get; set; } = new();
    public List<SimulatedDailySnapshot> DailySnapshots { get; set; } = new();
}

/// <summary>
/// 模拟持仓头寸明细
/// </summary>
public class SimulatedPosition
{
    public string AccountId { get; set; } = "default_ai_account";
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public string FundType { get; set; } = string.Empty;
    public decimal Shares { get; set; }                 // 持仓份额
    public decimal CostBasis { get; set; }              // 持仓均价 / 单位持仓成本
    public decimal TotalCost { get; set; }              // 累计投入总成本
    public decimal LatestNav { get; set; }              // 最新单位净值
    public string NavDate { get; set; } = string.Empty; // 净值日期
    public decimal MarketValue { get; set; }            // 持仓最新市值 (份额 * 最新净值)
    public decimal FloatingProfit { get; set; }         // 浮动盈亏金额 (市值 - 成本)
    public decimal FloatingProfitRate { get; set; }     // 浮动盈亏率 (%)
    public decimal WeightPercent { get; set; }          // 占总资产仓位比例 (%)
    public decimal TodayChangePercent { get; set; }     // 今日涨跌幅 (%)
    public decimal TodayProfit { get; set; }            // 今日盈亏金额 (元)

    // AI 量化研判信号与操作诊断
    public string AiSignal { get; set; } = "🛡️ 继续持有"; // 强烈看好 / 逢低吸筹 / 继续持有 / 建议止盈 / 破位止损
    public decimal AiScore { get; set; } = 75m;         // 量化综合评分 (0~100)
    public string AiRecommendation { get; set; } = "多维量化指标运行平稳，建议维持现有权重配置。";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 模拟交易订单流水明细
/// </summary>
public class SimulatedTrade
{
    public string TradeId { get; set; } = Guid.NewGuid().ToString("N");
    public string AccountId { get; set; } = "default_ai_account";
    public DateTime TradeTime { get; set; } = DateTime.Now;
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public SimulatedTradeAction Action { get; set; } = SimulatedTradeAction.ManualBuy;
    public string ActionText { get; set; } = "🛒 手动买入";
    public decimal Nav { get; set; }                    // 成交单位净值
    public decimal Shares { get; set; }                 // 成交份额
    public decimal Amount { get; set; }                 // 成交总金额 (元)
    public decimal Fee { get; set; }                    // 手续费 (元)
    public decimal RealizedProfit { get; set; }         // 平仓实现盈亏 (仅卖出时有效)
    public decimal RealizedProfitRate { get; set; }     // 平仓实现收益率 (%)
    public string Reason { get; set; } = string.Empty;  // 量化条件依据 / 操作研判原因
}

/// <summary>
/// 模拟账户时序总资产与收益率快照 (用于绘制历史走势与基准对比)
/// </summary>
public class SimulatedDailySnapshot
{
    public DateTime Date { get; set; }
    public decimal TotalAsset { get; set; }             // 当日总资产
    public decimal Cash { get; set; }                   // 当日现金余额
    public decimal MarketValue { get; set; }            // 当日持仓总市值
    public decimal CumulativeReturn { get; set; }       // 累计收益率 (%)
    public decimal BenchmarkReturn { get; set; }        // 沪深300同期基准收益率 (%)
    public decimal DailyReturn { get; set; }            // 当日收益率 (%)
}

/// <summary>
/// AI 智能资产配置提案 (10万模拟金)
/// </summary>
public class AiAllocationProposal
{
    public string Title { get; set; } = "AI 多维量化 10 万智能建仓方案 (均衡全天候型)";
    public string StrategyType { get; set; } = "BALANCED"; // BALANCED, GROWTH, DEFENSIVE
    public string StrategyDescription { get; set; } = string.Empty;
    public decimal TotalCapital { get; set; } = 100000m;
    public decimal AllocatedAmount { get; set; } = 90000m;
    public decimal ReservedCash { get; set; } = 10000m;
    public List<AiAllocationItem> Items { get; set; } = new();
}

/// <summary>
/// AI 资产配置建议单基明细
/// </summary>
public class AiAllocationItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public string FundType { get; set; } = string.Empty;
    public string RoleTag { get; set; } = string.Empty; // 核心宽基底仓 / 科技成长先锋 / 红利价值防御 / 固收避险缓冲
    public decimal TargetAmount { get; set; }           // 建议买入金额 (元)
    public decimal WeightPercent { get; set; }          // 建议权重占比 (%)
    public decimal LatestNav { get; set; }              // 最新参考净值
    public decimal EstimatedShares { get; set; }        // 预估购入份额
    public decimal QuantScore { get; set; }             // 量化得分 (0~100)
    public List<string> ConditionsPassed { get; set; } = new(); // 通过的量化条件标签
    public string DetailedRationale { get; set; } = string.Empty; // 详细入选分析依据
}

/// <summary>
/// 持仓 AI 诊断与调仓建议
/// </summary>
public class AiHoldingDiagnosis
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public string CurrentSignal { get; set; } = string.Empty;
    public string UrgencyLevel { get; set; } = "NORMAL"; // URGENT, NOTICE, NORMAL
    public string ActionProposal { get; set; } = string.Empty;
    public decimal SuggestedActionAmount { get; set; }
    public string Rationale { get; set; } = string.Empty;
}
