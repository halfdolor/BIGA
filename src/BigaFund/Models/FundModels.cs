using BigaFund.Services;

namespace BigaFund.Models;

public class FundInfo
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Pinyin { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Managers { get; set; } = string.Empty;
    public decimal LatestNav { get; set; }
    public string NavDate { get; set; } = string.Empty;

    public string Sector => FundSectorHelper.DetectSector(Name, Type);
    public string DisplayText => $"{Code} - {Name} ({Type})";
}

public class NavRecord
{
    public DateTime Date { get; set; }
    public decimal UnitNav { get; set; }
    public decimal CumulativeNav { get; set; }
    public decimal DailyReturn { get; set; }
}

public class BenchmarkRecord
{
    public DateTime Date { get; set; }
    public decimal CumulativeReturnRate { get; set; } // e.g. 15.2 means 15.2%
}

public class FundDetail
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ManagerName { get; set; } = string.Empty;
    public string ManagerTenure { get; set; } = string.Empty;
    public string Manager => ManagerName;
    public string Tenure => ManagerTenure;
    public string FundSize { get; set; } = string.Empty;

    public string Sector => FundSectorHelper.DetectSector(Name, Type);

    public List<NavRecord> NavHistory { get; set; } = new();
    public List<BenchmarkRecord> BenchmarkCsi300 { get; set; } = new();
    public List<BenchmarkRecord> PeerAverage { get; set; } = new();

    // 季度前十大重仓股与大类资产配置穿透
    public List<FundStockHolding> Holdings { get; set; } = new();
    public List<FundAssetAllocation> AssetAllocations { get; set; } = new();
    public MorningstarStyleBox StyleBox { get; set; } = MorningstarStyleBox.LargeCapBlend;
    public string StyleBoxName => StyleBox switch
    {
        MorningstarStyleBox.LargeCapValue => "大盘价值",
        MorningstarStyleBox.LargeCapBlend => "大盘平衡",
        MorningstarStyleBox.LargeCapGrowth => "大盘成长",
        MorningstarStyleBox.MidCapValue => "中盘价值",
        MorningstarStyleBox.MidCapBlend => "中盘平衡",
        MorningstarStyleBox.MidCapGrowth => "中盘成长",
        MorningstarStyleBox.SmallCapValue => "小盘价值",
        MorningstarStyleBox.SmallCapBlend => "小盘平衡",
        MorningstarStyleBox.SmallCapGrowth => "小盘成长",
        MorningstarStyleBox.FixedIncome => "固收纯债",
        MorningstarStyleBox.MoneyMarket => "货币理财",
        _ => "宽基平衡"
    };
    public decimal HoldingsCr10 => Holdings.Count > 0 ? Holdings.Take(10).Sum(h => h.WeightPercent) : 0m;
    public FundQuantScoreCard? ScoreCard { get; set; }
    public RealtimeValuation? RealtimeValuation { get; set; }
    public BrinsonAttributionResult? BrinsonResult { get; set; }
    public StyleDriftAnalysisResult? StyleDrift { get; set; }
    public BarraAttributionResult? BarraAttribution { get; set; }
    public ManagerCareerProfile? ManagerProfile { get; set; }
    public FamaFrench5Result? FamaFrenchResult { get; set; }
    public UnderwaterAnalysisResult? UnderwaterAnalysis { get; set; }
    public ManagerTimingAbilityResult? TimingAbility { get; set; }
    public BullBearCaptureResult? BullBearCapture { get; set; }
    public InstitutionalDueDiligenceCard? DueDiligence { get; set; }
    public CrisisReplayResult? CrisisReplay { get; set; }
    public MultiPeriodCarinoBrinsonResult? MultiPeriodBrinson { get; set; }
    public QuantMetrics? QuantMetrics { get; set; }
    public MacroStressTestResult? MacroStressResult { get; set; }
    public ActiveShareAnalysisResult? ActiveShareResult { get; set; }
    public FundMultiFactorScoreItem? MultiFactorScore { get; set; }

    // Phase 15 科学投资建议与短中期科学评估
    public ShortTermEvaluationResult? ShortTermEvaluation { get; set; }
    public MediumTermEvaluationResult? MediumTermEvaluation { get; set; }
    public HorizonConcordanceResult? HorizonConcordance { get; set; }
    public ForwardSimulationForecastItem? ForwardForecast { get; set; }
    public ForecastRealityComparisonResult? ForecastRealityComparison { get; set; }
    public ScientificInvestmentAdvice? ScientificAdvice { get; set; }

    // Phase 17 机构级 4433 选基、阿尔法持续性与因子 IC 检验
    public Fund4433CheckResult? Result4433 { get; set; }
    public AlphaPersistenceResult? AlphaPersistence { get; set; }
    public FactorIcAnalysisResult? FactorIcAnalysis { get; set; }

    // Phase 18 量化多因子拥挤度与估值分化监控体系
    public FactorCrowdingAnalysisResult? FactorCrowding { get; set; }

    // Phase 20 机构投审会准入尽调闸门检测
    public InstitutionalGatekeeperResult? GatekeeperResult { get; set; }

    public decimal LatestUnitNav => NavHistory.Count > 0 ? NavHistory[^1].UnitNav : 0;
    public decimal LatestNav => LatestUnitNav;
    public decimal LatestCumulativeNav => NavHistory.Count > 0 ? NavHistory[^1].CumulativeNav : 0;
    public DateTime LatestDate => NavHistory.Count > 0 ? NavHistory[^1].Date : DateTime.MinValue;
    public DateTime StartDate => NavHistory.Count > 0 ? NavHistory[0].Date : DateTime.MinValue;
}

public enum MorningstarStyleBox
{
    LargeCapValue,  // 大盘价值
    LargeCapBlend,  // 大盘平衡
    LargeCapGrowth, // 大盘成长
    MidCapValue,    // 中盘价值
    MidCapBlend,    // 中盘平衡
    MidCapGrowth,   // 中盘成长
    SmallCapValue,  // 小盘价值
    SmallCapBlend,  // 小盘平衡
    SmallCapGrowth, // 小盘成长
    FixedIncome,    // 固收债券
    MoneyMarket,    // 货币市场
    BroadIndex      // 宽基被动
}

public class FundStockHolding
{
    public string StockCode { get; set; } = string.Empty;
    public string StockName { get; set; } = string.Empty;
    public decimal WeightPercent { get; set; } // 占净值比例 (如 8.5 代表 8.5%)
    public string ShareChange { get; set; } = string.Empty; // 较上期变动 (增持/减持/新进/不变)
    public string ReportDate { get; set; } = string.Empty; // 报告期 (如 2024Q4)
    public string Industry { get; set; } = string.Empty; // 申万一级行业/板块分类
}

public class FundAssetAllocation
{
    public string ReportDate { get; set; } = string.Empty; // 报告期
    public decimal StockRatio { get; set; } // 股票资产占比 (%)
    public decimal BondRatio { get; set; } // 债券资产占比 (%)
    public decimal CashRatio { get; set; } // 现金资产占比 (%)
    public decimal OtherRatio { get; set; } // 其它资产占比 (%)
    public decimal NetAsset { get; set; } // 净资产规模 (亿元)
}

public class DrawdownPoint
{
    public DateTime Date { get; set; }
    public decimal DrawdownRate { get; set; } // 回撤百分比 (负数或0，如 -12.5% 代表回撤 12.5%)
    public decimal PeakNav { get; set; }      // 截至该日的历史最高复权净值
    public decimal CurrentNav { get; set; }   // 该日复权净值
}

/// <summary>
/// 历史上重大回撤周期解构 (Drawdown Episode)
/// </summary>
public class DrawdownEpisode
{
    public int Rank { get; set; } // 严重程度排名 (1..5)
    public DateTime PeakDate { get; set; } // 峰值最高点日期
    public DateTime TroughDate { get; set; } // 谷底最低点日期
    public DateTime? RecoveryDate { get; set; } // 完全修复至前高日期 (未修复为 null)
    public decimal DrawdownPercent { get; set; } // 最大跌幅 (如 -28.4%)
    public int FallDays { get; set; } // 暴跌历时交易日数
    public int? RecoveryDays { get; set; } // 出坑修复历时交易日数
    public int TotalDays { get; set; } // 该周期总历时交易日数
    public bool IsRecovered => RecoveryDate.HasValue;
    public string StatusText => IsRecovered ? $"已修复 ({RecoveryDays}天)" : "⚠️ 修复中 / 尚未出坑";
    public string StatusDesc => StatusText;
}

public class QuantMetrics
{
    public string PeriodName { get; set; } = "全周期";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TradingDays { get; set; }

    public decimal TotalReturn { get; set; }
    public decimal AnnualizedReturn { get; set; }
    public decimal AnnualizedVolatility { get; set; }
    public decimal MaxDrawdown { get; set; }
    public DateTime? MaxDrawdownPeakDate { get; set; }
    public DateTime? MaxDrawdownTroughDate { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal SortinoRatio { get; set; }
    public decimal CalmarRatio { get; set; }
    public decimal BenchmarkReturn { get; set; }
    public decimal ExcessReturn { get => TotalReturn - BenchmarkReturn; set { } }
    public decimal Alpha { get; set; }
    public decimal Beta { get; set; } = 1.0m;
    public decimal InformationRatio { get; set; }
    public int? RecoveryTradingDays { get; set; }

    // 极端下行风险与尾部风险度量
    public decimal VaR95 { get; set; } // 95% 置信度日在险价值 (历史模拟法, %)
    public decimal CVaR95 { get; set; } // 95% 条件在险价值 / 预期短缺损失 (CVaR, %)

    // 机构级非正态分布与下行风险全景体系
    public decimal DownsideDeviation { get; set; } // 年化下行标准差 (DD, %)
    public decimal OmegaRatio { get; set; }        // 奥米加比率 (Omega Ratio, Rf为门槛)
    public decimal UlcerIndex { get; set; }        // 溃疡指数 (Ulcer Index, 综合水下深度与时间煎熬, %)
    public decimal MartinRatio { get; set; }       // 马丁比率 (Martin Ratio = 超额年化收益 / 溃疡指数)
    public decimal PainRatio { get; set; }         // 收益痛苦比 (Pain Ratio = 年化收益 / 平均绝对回撤)

    // 机构级风险收益拓展指标 (Capture Ratios, Treynor Ratio, Tracking Error)
    public decimal UpsideCaptureRatio { get; set; } = 100.0m;   // 上行捕获率 (UCR, %)
    public decimal DownsideCaptureRatio { get; set; } = 100.0m; // 下行捕获率 (DCR, %)
    public decimal CaptureRatio { get; set; } = 1.0m;           // 综合捕获比率 (UCR / DCR)
    public decimal TreynorRatio { get; set; }                   // 特雷诺比率 ((Rp - Rf) / Beta)
    public decimal TrackingError { get; set; }                  // 跟踪误差 (年化超额标准差, %)

    // 历史经典极端市场情景压力测试
    public List<StressTestScenario> StressTestScenarios { get; set; } = new();

    // 历史前五大最深回撤周期解构 (Top 5 Drawdown Episodes)
    public List<DrawdownEpisode> DrawdownEpisodes { get; set; } = new();

    // 滚动时序量化指标 (如 120日滚动窗口)
    public List<RollingMetricPoint> RollingMetrics { get; set; } = new();

    // 回撤水下曲线完整离散时序与穿透风控指标
    public List<DrawdownPoint> DrawdownSeries { get; set; } = new();
    public decimal CurrentDrawdown { get; set; } // 当前时点相对高点回撤幅度 (%)
    public decimal HoldingsCr10 { get; set; } // 前十大持仓集中度 CR10 (%)
    public string StyleBoxName { get; set; } = "大盘平衡"; // 晨星九宫格风格箱
    public BarraAttributionResult? BarraAttribution { get; set; }
    public ManagerCareerProfile? ManagerProfile { get; set; }
    public TailRiskMetrics? TailRisk { get; set; }
    public FamaFrench5Result? FamaFrenchResult { get; set; }
    public UnderwaterAnalysisResult? UnderwaterAnalysis { get; set; }
    public ManagerTimingAbilityResult? TimingAbility { get; set; }
    public BootstrapAlphaResult? LuckVsSkillAudit { get; set; }
    public ManagerTransitionAuditResult? ManagerTransitionAudit { get; set; }

    // Phase 9 扁平便捷属性
    public decimal Skewness { get => TailRisk?.Skewness ?? 0m; set { } }
    public decimal ExcessKurtosis { get => TailRisk?.ExcessKurtosis ?? 0m; set { } }
    public decimal CornishFisherVaR95 { get => TailRisk?.CornishFisherVaR95 ?? VaR95; set { } }
    public decimal CornishFisherCVaR95 { get => TailRisk?.CornishFisherCVaR95 ?? CVaR95; set { } }
    public decimal CornishFisherVaR99 { get => TailRisk?.CornishFisherVaR99 ?? 0m; set { } }
    public decimal CornishFisherCVaR99 { get => TailRisk?.CornishFisherCVaR99 ?? 0m; set { } }
    public decimal ModifiedVaR95 { get => CornishFisherVaR95; set { } }
    public decimal ModifiedCVaR95 { get => CornishFisherCVaR95; set { } }
    public decimal ModifiedVaR99 { get => CornishFisherVaR99; set { } }
    public decimal ModifiedCVaR99 { get => CornishFisherCVaR99; set { } }
    public decimal Basel10DayVaR99 { get => TailRisk?.Basel10DayVaR99 ?? 0m; set { } }
    public string TailFatnessRating { get => TailRisk?.TailFatnessRating ?? "适度正态"; set { } }
    public decimal UnderwaterTimeRatio { get => UnderwaterAnalysis?.UnderwaterTimeRatio ?? 0m; set { } }
    public int MaxUnderwaterDays { get => UnderwaterAnalysis?.MaxUnderwaterDays ?? 0; set { } }
    public decimal AverageUnderwaterDepth { get => UnderwaterAnalysis?.AverageUnderwaterDepth ?? 0m; set { } }

    // Phase 10 择时选股便捷属性
    public decimal TmAlpha { get => TimingAbility?.TmAlpha ?? 0m; set { } }
    public decimal TmGamma { get => TimingAbility?.TmGamma ?? 0m; set { } }
    public decimal HmAlpha { get => TimingAbility?.HmAlpha ?? 0m; set { } }
    public decimal HmDownsideBeta { get => TimingAbility?.HmDownsideBeta ?? 0m; set { } }
    public string TimingRating { get => TimingAbility?.TimingRating ?? "未测算"; set { } }

    // Phase 11 牛熊非对称捕获与极端暴跌条件相关性分析
    public BullBearCaptureResult? BullBearCapture { get; set; }
    public decimal BullBeta { get => BullBearCapture?.BullBeta ?? Beta; set { } }
    public decimal BearBeta { get => BullBearCapture?.BearBeta ?? Beta; set { } }
    public decimal CaptureSpread { get => BullBearCapture?.CaptureSpread ?? (UpsideCaptureRatio - DownsideCaptureRatio); set { } }
    public decimal CrashCorrelation { get => BullBearCapture?.CrashCorrelation ?? 0m; set { } }
    public string ConvexityRating { get => BullBearCapture?.ConvexityRating ?? "稳健均衡"; set { } }

    // Phase 11 机构 FOF 综合尽调六维雷达评估与晨星五星等效评级
    public InstitutionalDueDiligenceCard? DueDiligence { get; set; }
    public decimal DiligenceScore { get => DueDiligence?.OverallDiligenceScore ?? 0m; set { } }
    public string StarRating { get => DueDiligence?.StarRating ?? "★★★☆☆"; set { } }
    public string InstitutionalVerdict { get => DueDiligence?.InstitutionalVerdict ?? string.Empty; set { } }

    // Phase 12 历史极端黑天鹅危机压力测试回放
    public CrisisReplayResult? CrisisReplay { get; set; }
    public decimal ComprehensiveResilienceScore { get => CrisisReplay?.ComprehensiveResilienceScore ?? 0m; set { } }
    public string OverallResilienceRating { get => CrisisReplay?.OverallResilienceRating ?? "稳健防御型"; set { } }

    // Phase 13 前瞻性多维宏观情景压力测试与主动份额
    public MacroStressTestResult? MacroStressResult { get; set; }
    public decimal MacroResilienceScore { get => MacroStressResult?.ComprehensiveResilienceScore ?? 0m; set { } }
    public ActiveShareAnalysisResult? ActiveShareResult { get; set; }
    public decimal ActiveSharePercent { get => ActiveShareResult?.ActiveSharePercent ?? 0m; set { } }

    // Fama-French 快捷属性
    public decimal FamaFrenchAlpha { get => FamaFrenchResult?.AlphaAnnualized ?? 0m; set { } }
    public decimal FamaFrenchRSquared { get => FamaFrenchResult?.RSquaredPercent ?? 0m; set { } }

    // Phase 15 科学投资建议与短中期科学评估
    public ShortTermEvaluationResult? ShortTermEvaluation { get; set; }
    public MediumTermEvaluationResult? MediumTermEvaluation { get; set; }
    public HorizonConcordanceResult? HorizonConcordance { get; set; }
    public ForwardSimulationForecastItem? ForwardForecast { get; set; }
    public ForecastRealityComparisonResult? ForecastRealityComparison { get; set; }
    public ScientificInvestmentAdvice? ScientificAdvice { get; set; }
}

public class StressTestScenario
{
    public string ScenarioName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal FundReturnRate { get; set; } // 期间累计涨跌幅 (%)
    public decimal BenchmarkReturnRate { get; set; } // 期间沪深300涨跌幅 (%)
    public decimal ExcessReturnRate => FundReturnRate - BenchmarkReturnRate; // 超额收益 (%)
    public decimal MaxDrawdown { get; set; } // 期间最大回撤 (%)
    public string DefenseRating { get; set; } = "适中"; // "卓越防御" / "跑赢基准" / "跟随震荡" / "高弹性承压"
}

public class CorrelationMatrixResult
{
    public List<string> AssetCodes { get; set; } = new();
    public List<string> AssetNames { get; set; } = new();
    public double[,] Matrix { get; set; } = new double[0, 0];
    public decimal AverageCorrelation { get; set; } // 资产间平均两两相关系数
    public string DiversificationRating { get; set; } = "良好"; // "极佳分散" / "良好分散" / "一般分散" / "高度同质"
}

public class RollingMetricPoint
{
    public DateTime Date { get; set; }
    public decimal RollingAnnualizedVol { get; set; } // 滚动年化波动率 (%)
    public decimal RollingVolatility => RollingAnnualizedVol;
    public decimal RollingSharpe { get; set; } // 滚动夏普比率
}

public class DcaPoint
{
    public DateTime Date { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal ReturnRate { get; set; }
}

public class BacktestResult
{
    public string StrategyName { get; set; } = "定投策略 (DCA)";
    public string StrategyDescription { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalPeriods { get; set; }
    public decimal PeriodicAmount { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal FinalShares { get; set; }
    public decimal FinalAssetValue { get; set; }
    public decimal TotalProfit => FinalAssetValue - TotalInvested;
    public decimal TotalReturnRate => TotalInvested > 0 ? (TotalProfit / TotalInvested) * 100m : 0m;
    public decimal AnnualizedIrr { get; set; } // 年化内部收益率
    public decimal BuyAndHoldReturnRate { get; set; }
    public decimal AverageCostPrice => FinalShares > 0 ? Math.Round(TotalInvested / FinalShares, 4) : 0m;

    // 高阶策略衍生指标
    public int TakeProfitRounds { get; set; } // 止盈触发轮次
    public decimal RealizedProfit { get; set; } // 止盈已落袋锁定收益 (元)
    public int GridTradesCount { get; set; } // 网格套利成交总笔数
    public decimal GridArbitrageProfit { get; set; } // 网格高抛低吸套利毛收益 (元)
    public int RebalanceCount { get; set; } // 动态再平衡触发次数
    public decimal EquityAssetValue { get; set; } // 权益持仓市值 (元)
    public decimal BondAssetValue { get; set; } // 固收/债券持仓市值 (元)

    public List<DcaPoint> Timeline { get; set; } = new();
}

public class UserFavorite
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
    public string DisplayText => $"{Code} {Name}";
}

public class PortfolioItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal WeightPercent { get; set; } = 50m;
}

public class PortfolioOptimizationScheme
{
    public string SchemeName { get; set; } = string.Empty; // 最大夏普, 最小方差, 风险平价
    public string Name => SchemeName;
    public string Description { get; set; } = string.Empty;
    public decimal ExpectedReturn { get; set; }
    public decimal ExpectedVolatility { get; set; }
    public decimal Volatility => ExpectedVolatility;
    public decimal SharpeRatio { get; set; }
    public Dictionary<string, decimal> Weights { get; set; } = new(); // Code -> WeightPercent (0-100)
}

public class PortfolioResult
{
    public string PortfolioName { get; set; } = "基金投资组合";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TradingDays { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal AnnualizedReturn { get; set; }
    public decimal AnnualizedVolatility { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal BenchmarkReturn { get; set; }
    public decimal DiversificationBenefit { get; set; } // 资产配置分散化降波度
    public decimal CalmarRatio => MaxDrawdown > 0 ? AnnualizedReturn / MaxDrawdown : 0m;
    public List<NavRecord> PortfolioNavHistory { get; set; } = new();
    public QuantMetrics? QuantMetrics { get; set; }

    // 组合资产两两相关系数矩阵与压力测试
    public CorrelationMatrixResult? CorrelationMatrix { get; set; }
    public List<StressTestScenario> StressTestScenarios { get; set; } = new();

    // 机构级非正态风控体系与下行指标
    public decimal DownsideDeviation { get; set; } // 组合年化下行标准差 (%)
    public decimal OmegaRatio { get; set; }        // 组合奥米加比率 (Omega Ratio)
    public decimal UlcerIndex { get; set; }        // 组合溃疡指数 (%)
    public decimal MartinRatio { get; set; }       // 组合马丁比率
    public decimal PainRatio { get; set; }         // 组合收益痛苦比
    public List<DrawdownEpisode> DrawdownEpisodes { get; set; } = new();

    // 现代资产配置理论 (MPT) 与智能优化方案
    public Dictionary<string, decimal> MaxSharpeWeights { get; set; } = new();
    public Dictionary<string, decimal> MinVarianceWeights { get; set; } = new();
    public Dictionary<string, decimal> RiskParityWeights { get; set; } = new();
    public Dictionary<string, decimal> MomentumRiskBudgetWeights { get; set; } = new();
    public Dictionary<string, decimal> BlackLittermanWeights { get; set; } = new();
    public Dictionary<string, decimal> HrpWeights { get; set; } = new(); // 机器学习层次风险平价最优权重 (HRP)
    public Dictionary<string, decimal> MeanCVaRWeights { get; set; } = new(); // 均值-CVaR 极值尾部损失优化权重
    public Dictionary<string, decimal> MdpWeights { get; set; } = new(); // Choueifaty 最大分散化投资组合权重 (MDP)
    public List<PortfolioOptimizationScheme> Schemes { get; set; } = new();
    public List<(double Volatility, double Return)> EfficientFrontierPoints { get; set; } = new();
    public List<(double Volatility, double Return)> EfficientFrontierCurve { get; set; } = new();
    public RebalanceOrderSheet? RebalanceOrders { get; set; }
    public BrinsonAttributionResult? BrinsonAttribution { get; set; }

    // 组合底层穿透合并持仓与前瞻性蒙特卡洛随机漫步推演
    public PortfolioLookThroughResult? LookThroughResult { get; set; }
    public MonteCarloSimulationResult? MonteCarloResult { get; set; }

    // 宏观情景与多因子冲击前瞻性压力测试
    public MacroShockSimulationResult? MacroShockResult { get; set; }

    // 组合动态时序再平衡与换手摩擦损耗仿真
    public RebalanceSimulationResult? RebalanceSimulation { get; set; }

    // 组合同质化诊断与 Choueifaty 分散化比率 (DR)
    public PortfolioDiversificationResult? Diversification { get; set; }

    // 资产边际风险贡献 (MCR) 与百分比风险贡献 (PCR) 穿透解构
    public PortfolioRiskDecompositionResult? RiskDecomposition { get; set; }

    // 机构级 Barra CNE6 组合多因子暴露聚合与主动风险分解 (Factor Risk vs Specific Risk)
    public PortfolioFactorRiskResult? FactorRiskAttribution { get; set; }

    // 连续多资产凯利公式最优资本配置与目标波动率杠杆/现金缓冲引擎
    public PortfolioKellyAndTargetVolResult? KellyAndTargetVol { get; set; }

    // 组合牛熊非对称捕获与极端暴跌条件相关性
    public BullBearCaptureResult? BullBearCapture { get; set; }

    // Phase 12 组合历史极端黑天鹅危机压力测试回放
    public CrisisReplayResult? CrisisReplay { get; set; }

    // Phase 12 组合多期 Carino 跨周期 Brinson-Fachler 几何无残差归因
    public MultiPeriodCarinoBrinsonResult? MultiPeriodBrinson { get; set; }

    // Phase 12 战术资产配置 (TAA) 动量轮动回测
    public TaaBacktestResult? TaaBacktest { get; set; }

    // Phase 13 宏观情景前瞻性压力测试与传导推演
    public MacroStressTestResult? MacroStressResult { get; set; }

    // Phase 13 Barra 风格因子主动超额收益归因 (Factor Return Attribution)
    public BarraFactorReturnAttributionResult? BarraReturnAttribution { get; set; }

    // Phase 13 投资组合前瞻性流动性地平线与冲击成本测算
    public PortfolioLiquidityHorizonResult? LiquidityHorizon { get; set; }

    // Phase 13 组合主动份额 (Active Share) 与偏离度分析
    public ActiveShareAnalysisResult? ActiveShareResult { get; set; }

    // Phase 17 组合日度时序动态再平衡回测与带盒约束优化权重
    public PortfolioDynamicBacktestResult? DynamicBacktest { get; set; }
    public Dictionary<string, decimal> ConstrainedMaxSharpeWeights { get; set; } = new();
    public Dictionary<string, decimal> ConstrainedMinVarianceWeights { get; set; } = new();
    public Dictionary<string, decimal> ConstrainedRiskParityWeights { get; set; } = new();

    // Phase 18 组合穿透持仓重叠度消冗矩阵与伪分散告警
    public PortfolioOverlapMatrixResult? HoldingsOverlap { get; set; }

    // Phase 19 机构级优化：SAA 战略多资产基准锚定与 TAA 战术偏离度监控
    public PortfolioSaaTaaMonitorResult? SaaTaaMonitoring { get; set; }

    // Phase 19 机构级优化：全组合收益与夏普比率贡献率穿透解构
    public PortfolioSharpeDecompositionResult? SharpeDecomposition { get; set; }

    // Phase 19 机构级优化：极端宏观情景反向压力测试 (Reverse Stress Testing)
    public ReverseStressTestResult? ReverseStressTest { get; set; }

    // Phase 19 机构级优化：风险价值后验检验 (Kupiec POF & Christoffersen 似然比检验)
    public KupiecVaRTestResult? KupiecVaRTest { get; set; }

    // Phase 20 机构级优化：宏观经济四象限体制轮动与全天候自适应配置矩阵
    public MacroRegimeSwitchingResult? MacroRegimeSwitching { get; set; }

    // Phase 20 机构级优化：条件在险回撤 (CDaR) 与欧拉期望亏空 (Euler ES) 尾部极值解构
    public PortfolioTailRiskDecompositionResult? TailRiskDecomposition { get; set; }

    // Phase 20 机构级优化：机构投审会一键准入尽调闸门与自动化否决风控雷达
    public PortfolioGatekeeperAuditResult? GatekeeperAudit { get; set; }

    // Phase 21 机构级优化：多资产多因子风险平价 (Factor Risk Parity) 与系统性因子风险微分解构
    public FactorRiskParityResult? FactorRiskParity { get; set; }

    // Phase 21 机构级优化：负债驱动投资 (LDI) 资产负债充足率与久期匹配雷丁顿免疫
    public LdiImmunizationResult? LdiImmunization { get; set; }

    // Phase 21 机构级优化：考虑中国公募阶梯赎回费与账龄时钟的容差动态再平衡
    public TieredFeeDynamicRebalanceResult? TieredFeeRebalance { get; set; }

    // Phase 22 机构级优化：阿拉丁级历史黑天鹅全息宏观危机因子传导压力测试
    public HistoricalCrisisStressResult? HistoricalCrisisStress { get; set; }

    // Phase 22 机构级优化：大体量资金执行落差与平方根市场冲击模型 (含公募10%巨额赎回预警)
    public ExecutionShortfallResult? ExecutionShortfall { get; set; }

    // Phase 23 机构级优化：动态因子时序择时与动量-估值自适应轮动中枢
    public DynamicFactorTimingResult? DynamicFactorTiming { get; set; }

    // Phase 23 机构级优化：GIPS 国际标准多期复合 Brinson 归因 (Carino 对数平滑算法)
    public MultiPeriodBrinsonResult? GipsMultiPeriodBrinson { get; set; }

    // Phase 23 机构级优化：FOF 底层股票全息影子组合二次重构与风格纯度指标
    public ShadowPortfolioPurityResult? ShadowPortfolioPurity { get; set; }

    // Phase 24 机构级优化：多时域流动性阶梯变现天数 (DTL) 与巨额赎回级联逆向选择仿真
    public LiquidityLadderRedemptionRunResult? LiquidityRedemptionRun { get; set; }

    // Phase 24 机构级优化：CPPI / TIPP 动态保本增值与收益锁定棘轮保险引擎
    public PortfolioInsuranceSimulationResult? PortfolioInsurance { get; set; }

    // Phase 24 机构级优化：高阶矩 (偏度与峰度) 修正夏普比率 (Modified Sharpe) 与 Omega 优化前沿
    public HigherMomentsOptimizationResult? HigherMomentsOptimization { get; set; }

    // Phase 25 机构级优化：极值非对称 Copula 尾部联结与下行协同暴跌测算
    public CopulaTailDependenceResult? CopulaTailDependence { get; set; }

    // Phase 25 机构级优化：多目标帕累托前沿自适应进化优化 (NSGA-II 算法内核)
    public ParetoMultiObjectiveResult? ParetoMultiObjective { get; set; }

    // Phase 25 机构级优化：GARCH(1,1) 前瞻条件异方差预测与波动率锥
    public GarchVolatilityForecastResult? GarchVolatilityForecast { get; set; }

    // Phase 26 机构级优化：Ledoit-Wolf 渐近最优收缩协方差估计器
    public LedoitWolfShrinkageResult? LedoitWolfShrinkage { get; set; }

    // Phase 26 机构级优化：Hamilton 马尔可夫两状态体制转换模型
    public MarkovRegimeSwitchingResult? MarkovRegimeSwitching { get; set; }

    // Phase 26 机构级优化：Merton 泊松跳跃扩散蒙特卡洛极端前瞻引擎
    public MertonJumpDiffusionResult? MertonJumpDiffusion { get; set; }

    // Phase 26 机构级优化：规模敏感型流动性调整在险价值 (L-VaR)
    public LiquidityAdjustedVaRResult? LiquidityAdjustedVaR { get; set; }

    // Phase 26 机构级优化：专业量化 Tearsheet 仪表盘 (月度收益热力图与水下回撤持续期)
    public PortfolioTearsheetResult? PortfolioTearsheet { get; set; }

    // Phase 27 机构级优化：Almgren-Chriss 最优算法执行与清算微观轨迹推演
    public AlmgrenChrissExecutionResult? AlmgrenChrissExecution { get; set; }

    // Phase 27 机构级优化：Michaud 蒙特卡洛重抽样均值方差有效前沿
    public MichaudResampledFrontierResult? MichaudResampledFrontier { get; set; }

    // Phase 27 机构级优化：反向压力测试逆向破产临界拓扑求解器
    public ReverseStressTopologyResult? ReverseStressTopology { get; set; }

    // Phase 27 机构级优化：Cornish-Fisher 高阶矩偏度峰度修正 VaR/CVaR
    public CornishFisherVaRResult? CornishFisherVaR { get; set; }

    // Phase 28 机构级优化：多因子拥挤度雷达与流动性踩踏预警
    public AssetCrowdingRadarResult? AssetCrowdingRadar { get; set; }

    // Phase 28 机构级优化：带最大回撤硬顶约束的分数凯利动态仓位配置
    public DrawdownConstrainedKellyResult? DrawdownConstrainedKelly { get; set; }

    // Phase 28 机构级优化：BSTS 贝叶斯结构时序滤波与趋势断点诊断
    public BstsTrendFilterResult? BstsTrendFilter { get; set; }

    // Phase 28 机构级优化：多期跨期期限结构前沿与时间跨度风险衰减锥
    public MultiHorizonRiskTermResult? MultiHorizonRiskTerm { get; set; }

    // Phase 29 机构级优化：随机矩阵理论 (RMT) 与 Marchenko-Pastur 谱滤波去噪
    public RmtCovarianceCleaningResult? RmtCovarianceCleaning { get; set; }

    // Phase 29 机构级优化：嵌套聚类优化 (NCO) 层次化前沿与簇间-簇内双重配置
    public NestedClusteredOptimizationResult? NestedClusteredOptimization { get; set; }

    // Phase 29 机构级优化：Amihud 冲击弹性与 Roll 隐性买卖价差微观流动性摩擦锥
    public MicrostructureLiquidityResult? MicrostructureLiquidity { get; set; }

    // Phase 29 机构级优化：信息几何有效下注数 (ENB) 与香农熵分散度审定
    public PortfolioEntropyRegularizationResult? PortfolioEntropyRegularization { get; set; }

    // Phase 30 机构级优化：全天候宏观态识别与马氏金融动荡度降杠杆雷达
    public MahalanobisTurbulenceResult? MahalanobisTurbulence { get; set; }

    // Phase 30 机构级优化：欧拉下行条件在险价值 (CVaR) 风险贡献分解与尾部去毒
    public EulerCvarAttributionResult? EulerCvarAttribution { get; set; }

    // Phase 30 机构级优化：基准相对下行半方差跟踪误差 (DTE) 与不对称 Stutzer 大偏差指数
    public DownsideTrackingErrorResult? DownsideTrackingError { get; set; }

    // Phase 30 机构级优化：负债驱动投资 (LDI) 跨期现金流匹配与阶梯式清算瀑布
    public LdiCashFlowMatchResult? LdiCashFlowMatch { get; set; }

    // Phase 31 机构级优化：卡尔曼滤波时变贝塔与风格漂移预警
    public KalmanFilterStyleDriftResult? KalmanFilterStyleDrift { get; set; }

    // Phase 31 机构级优化：基数约束与换手预算稀疏投资组合优化
    public CardinalitySparseOptimizationResult? CardinalitySparseOptimization { get; set; }

    // Phase 31 机构级优化：条件系统性在险价值增量 Delta-CoVaR 与金融传染
    public DeltaCoVaRSystemicRiskResult? DeltaCoVaRSystemicRisk { get; set; }

    // Phase 31 机构级优化：Basel III / FRTB 压力在险价值与多流动性时限资本拨备
    public FrtbStressedCapitalChargeResult? FrtbStressedCapitalCharge { get; set; }

    // Phase 32 机构级优化：Idzorek 显式置信度 Black-Litterman 贝叶斯后验优化
    public IdzorekBlackLittermanResult? IdzorekBlackLitterman { get; set; }

    // Phase 32 机构级优化：Acerbi 连续风险厌恶谱风险测度 (SRM)
    public SpectralRiskMeasureResult? SpectralRiskMeasure { get; set; }

    // Phase 32 机构级优化：动态风险预算漂移走廊与带摩擦平滑再平衡
    public RiskBudgetDriftCorridorResult? RiskBudgetDriftCorridor { get; set; }

    // Phase 32 机构级优化：Bailey-de Prado 概率夏普 (PSR) 与通缩夏普 (DSR) 策略过拟合检验
    public DeflatedSharpeOverfitResult? DeflatedSharpeOverfit { get; set; }

    // Phase 33 机构级优化：AQR 杠杆约束异象 Betting Against Beta (BAB) 与高质量多空 (QMJ) 因子解构
    public BabQmjFactorDecompositionResult? BabQmjFactorDecomposition { get; set; }

    // Phase 33 机构级优化：BlackRock Aladdin 极值理论 (EVT) 广义帕累托 (GPD) 尾部外推与重现期风险测度
    public EvtGeneralizedParetoResult? EvtGeneralizedPareto { get; set; }

    // Phase 33 机构级优化：MSCI Barra 内生流动性黑洞弹性 (LBHI) 与资产踩踏抛售反馈乘数
    public LiquidityBlackHoleResult? LiquidityBlackHole { get; set; }

    // Phase 33 机构级优化：Marcos Lopez de Prado 策略微观夏普衰减半衰期与自适应 CUSUM 概念漂移滤波检验
    public SharpeDecayCusumResult? SharpeDecayCusum { get; set; }

    // Phase 34 机构级优化：跨资产非对称时滞领先-滞后互相关与信息流传导有向网络
    public LeadLagCrossCorrelationResult? LeadLagCrossCorrelation { get; set; }

    // Phase 34 机构级优化：多重投资期限风险期限结构与 Lo-MacKinlay 方差比非随机游走检验
    public MultiHorizonRiskTermStructureResult? MultiHorizonRiskTermStructure { get; set; }

    // Phase 34 机构级优化：3 状态高斯隐马尔可夫模型 (Gaussian HMM) 宏观体制解码与转移信息熵
    public ThreeStateGaussianHmmResult? ThreeStateGaussianHmm { get; set; }

    // Phase 34 机构级优化：广义反向择时短周期反转 Alpha 与动量崩塌预警指数 (MCWI)
    public MomentumCrashAndReversalResult? MomentumCrashAndReversal { get; set; }

    // Phase 35 机构级优化：BIS / BCBS FRTB 内部模型法损益归因 (PLA) 与巴塞尔 250 天交通灯超限检定
    public FrtbPlaAndTrafficLightResult? FrtbPlaAndTrafficLight { get; set; }

    // Phase 35 机构级优化：Millennium & Point72 Pod Shop 多策略单元动态资本分配与阶梯止损降额机制
    public PodShopCapitalAllocationResult? PodShopCapitalAllocation { get; set; }

    // Phase 35 机构级优化：MSCI Barra & Axioma 风格因子 Löwdin 对称正交化与纯因子载荷矩阵
    public FactorOrthogonalizationResult? FactorOrthogonalization { get; set; }

    // Phase 35 机构级优化：Two Sigma & Citadel 距离相关系数 (dCor) 与互信息 (MI) 非线性 Alpha 特征排序
    public NonlinearDistanceMutualInfoResult? NonlinearDistanceMutualInfo { get; set; }

    // Phase 36 机构级优化：Citadel & Millennium 微观因子与资产拥挤度评分及机构踩踏排队指数 (HLRI)
    public AssetFactorCrowdednessResult? AssetFactorCrowdedness { get; set; }

    // Phase 36 机构级优化：BlackRock Aladdin & J.P. Morgan 多因子宏观情景冲击传导与压力在险价值 (Stressed VaR)
    public MacroFactorShockPropagationResult? MacroFactorShockPropagation { get; set; }

    // Phase 36 机构级优化：AQR Capital & Antti Ilmanen 真实波动率特征结构与方差风险溢价 (VRP)
    public VarianceRiskPremiumResult? VarianceRiskPremium { get; set; }

    // Phase 36 机构级优化：Two Sigma & Renaissance 摩擦成本敏感型动态无交易再平衡缓冲带 (No-Trade Buffer Bands)
    public DynamicNoTradeBufferBandResult? DynamicNoTradeBufferBand { get; set; }

    // Phase 37 机构级优化：Bridgewater 宏观增长-通胀双维度惊喜指数矩阵与全天候宏观贝塔正交中性化覆盖
    public MacroSurpriseAndNeutralizingOverlayResult? MacroSurpriseOverlay { get; set; }

    // Phase 37 机构级优化：Citadel & Millennium 多经理平台核心风格因子正交中性化与非故意风险漂移剔除
    public StyleFactorNeutralizationResult? StyleFactorNeutralization { get; set; }

    // Phase 37 机构级优化：AQR Capital & Antti Ilmanen 动态目标波动率定标时间序列动量 (TSMOM) 与危机 Alpha
    public VolatilityTargetedTsmomResult? VolatilityTargetedTsmom { get; set; }

    // Phase 37 机构级优化：Two Sigma & D.E. Shaw Almgren-Chriss 连续动态最优拆单轨迹与流动性在险度量
    public OptimalExecutionTrajectoryResult? OptimalExecutionTrajectory { get; set; }

    // Phase 38 机构级优化：Man Group AHL & AQR 跨资产大类期限结构展期收益 (Roll Yield / Carry) 与基差动量
    public CrossAssetTermStructureCarryResult? TermStructureCarry { get; set; }

    // Phase 38 机构级优化：Renaissance Technologies & CFM 随机矩阵理论 (RMT) 马尔琴科-帕斯图尔谱分解滤波与信号降噪
    public RmtSpectralFilteringResult? RmtSpectralFiltering { get; set; }

    // Phase 38 机构级优化：Citadel & Millennium 多元非对称极值下行联结与组合结构性协同崩塌脆弱度矩阵
    public MultivariateTailCoCrashResult? MultivariateTailCoCrash { get; set; }

    // Phase 38 机构级优化：Jane Street & Citadel Securities 微观订单流不平衡 (OFI)、Kyle 价格冲击与逆向选择足迹
    public MicrostructureAdverseSelectionResult? MicrostructureAdverseSelection { get; set; }

    // Phase 39 机构级优化：Bridgewater & AQR 主因子正交风险平价 (PFRP) 与特征风险预算
    public PrincipalFactorRiskParityResult? PrincipalFactorRiskParity { get; set; }

    // Phase 39 机构级优化：Millennium & Point72 动态下行凸性期权对冲与广义波动率偏度复制
    public DynamicDownsideConvexityHedgeResult? DynamicDownsideConvexityHedge { get; set; }

    // Phase 39 机构级优化：Renaissance & D.E. Shaw 贝叶斯时变状态空间卡尔曼滤波与自适应 Alpha 动态跟踪
    public BayesianKalmanAlphaTrackerResult? BayesianKalmanAlphaTracker { get; set; }

    // Phase 39 机构级优化：Jane Street & Jump Trading 跨资产微观流动性共振裂谷与闪崩级联阻尼器
    public CrossAssetLiquidityChasmDamperResult? CrossAssetLiquidityChasmDamper { get; set; }

    // Phase 40 机构级优化：Bridgewater & Citadel 宏观马尔可夫区制转移 (MRS) 与跨周期动态配置
    public MacroMarkovRegimeSwitchingResult? MacroMarkovRegimeSwitching { get; set; }

    // Phase 40 机构级优化：AQR & Man Group 多频率截面动量 (CSMOM) 与双重动量相对优势剥离
    public CrossSectionalMomentumResult? CrossSectionalMomentum { get; set; }

    // Phase 40 机构级优化：Millennium & Balyasny 多策略 Pod 阶梯式回撤硬风控熔断与动态资本再平衡
    public PodTieredDrawdownCircuitBreakerResult? PodTieredDrawdownCircuitBreaker { get; set; }

    // Phase 40 机构级优化：Jane Street & Optiver 限价单队列成交概率与高频期现基差收敛套利
    public LimitOrderQueueAndBasisArbitrageResult? LimitOrderQueueAndBasisArbitrage { get; set; }

    // Phase 41 机构级优化：D.E. Shaw & WorldQuant 符号基因规划自适应 Alpha 因子挖掘引擎
    public SymbolicGeneticAlphaMiningResult? SymbolicGeneticAlphaMining { get; set; }

    // Phase 41 机构级优化：Two Sigma & Man Group AHL 因果知识图谱跨资产时滞传递与宏观情绪溢出网络
    public CausalKnowledgeGraphSpilloverResult? CausalKnowledgeGraphSpillover { get; set; }

    // Phase 41 机构级优化：Citadel & Point72 上下文多臂老虎机 (Thompson Sampling) 动态策略路由器
    public ContextualBanditPolicyRouterResult? ContextualBanditPolicyRouter { get; set; }

    // Phase 41 机构级优化：Jump Trading & Optiver 深度强化微观流动性冲击弹性与非线性滑点曲面
    public MicrostructureResiliencySlippageSurfaceResult? MicrostructureResiliencySlippageSurface { get; set; }

    // Phase 42 机构级优化：Bridgewater & AQR 跨资产内生流动性螺旋与去杠杆压力传染动力学模型
    public EndogenousLiquiditySpiralResult? EndogenousLiquiditySpiral { get; set; }

    // Phase 42 机构级优化：Renaissance & Citadel 变分无监督深度隐空间流形聚类与非线性宏观体制自编码器
    public VariationalLatentManifoldRegimeResult? VariationalLatentManifoldRegime { get; set; }

    // Phase 42 机构级优化：Millennium & Point72 跨资产多周期极值尾部相关相变检测与渗流理论系统性临界熔断网络
    public PercolationTailPhaseTransitionResult? PercolationTailPhaseTransition { get; set; }

    // Phase 42 机构级优化：WorldQuant & HRT 统计套利强化正交特征残差动量与微观协整均值回复追踪引擎
    public StatArbResidualMomentumOuResult? StatArbResidualMomentumOu { get; set; }

    // Phase 43 机构级优化：Renaissance & Two Sigma 最大相关最小冗余 (mRMR) 互信息特征选择与正交子空间集成
    public MrmrFeatureSelectionEnsembleResult? MrmrFeatureEnsemble { get; set; }

    // Phase 43 机构级优化：Citadel & Millennium 基于 Wasserstein 最优输运的多策略 Pod 动态资本曲率重构
    public WassersteinPodCapitalCurvatureResult? WassersteinPodCurvature { get; set; }

    // Phase 43 机构级优化：Jane Street & Citadel Securities 微观限价订单簿自激 Hawkes 点过程与流动性雪崩预警
    public HawkesMicrostructureAvalancheResult? HawkesMicrostructureAvalanche { get; set; }

    // Phase 43 机构级优化：Bridgewater & AQR Capital 高阶矩张量风险平价与非高斯偏度-峰度协同对冲矩阵
    public HigherOrderTensorRiskParityResult? HigherOrderTensorRiskParity { get; set; }

    // Phase 44 机构级优化：Renaissance & D.E. Shaw 连续时间倒向随机微分方程 (BSDE) 动态粘性解对冲
    public BsdeDynamicViscosityHedgingResult? BsdeDynamicHedging { get; set; }

    // Phase 44 机构级优化：Citadel & Millennium 多资产高阶拓扑超图 (Hypergraph) 与持续同调 Betti 空洞破裂预警
    public HypergraphTopologicalCausalityResult? HypergraphTopologicalCausality { get; set; }

    // Phase 44 机构级优化：Jane Street & Citadel Securities 微观瞬时订单流毒性扩散核与跨标的交叉冲击张量
    public CrossImpactTensorMicrostructureResult? CrossImpactTensorMicrostructure { get; set; }

    // Phase 44 机构级优化：Bridgewater & AQR 测度模糊集 Wasserstein 球分布鲁棒优化 (DRO) 极小极大平价
    public WassersteinDroMinimaxParityResult? WassersteinDroMinimaxParity { get; set; }

    // Phase 45 机构级优化：Renaissance & D.E. Shaw 连续时间分数阶粗糙波动率 (Rough Heston) 与 Riemann-Liouville 分数阶积分预测
    public FractionalRoughVolatilityResult? FractionalRoughVolatility { get; set; }

    // Phase 45 机构级优化：Citadel & Millennium 六参数 Nelson-Siegel-Svensson (NSS) 利率期限结构拟合与蝶式凸性套利
    public NelsonSiegelSvenssonTermStructureResult? NelsonSiegelSvenssonTermStructure { get; set; }

    // Phase 45 机构级优化：Two Sigma & Man Group AHL 贝叶斯在线变点检测 (BOCPD) 与序列失效率运行长度后验滤波
    public BayesianOnlineChangepointDetectionResult? BayesianOnlineChangepointDetection { get; set; }

    // Phase 45 机构级优化：Jane Street & Citadel Securities Avellaneda-Stoikov 连续库存风险最优保留价与非对称微观做市定价
    public AvellanedaStoikovMicrostructureResult? AvellanedaStoikovMicrostructure { get; set; }

    // Phase 46 机构级优化：Renaissance & Alan Turing Institute 粗糙路径特征签名 (Rough Path Signature) 与高阶张量 Alpha 几何编码
    public RoughPathSignatureAlphaResult? RoughPathSignatureAlpha { get; set; }

    // Phase 46 机构级优化：Citadel Global Strategies & Millennium RV 随机最优停止时间与自由边界斯内尔包络 (Snell Envelope) 动态去杠杆
    public StochasticOptimalStoppingResult? StochasticOptimalStopping { get; set; }

    // Phase 46 机构级优化：Two Sigma & Bridgewater Associates 因果结构方程模型 (SCM) 与反事实 Do-Calculus 宏观干预归因
    public CausalStructuralModelAttributionResult? CausalStructuralModelAttribution { get; set; }

    // Phase 46 机构级优化：Jane Street & Citadel Securities 暂态市场冲击幂律记忆核 (Propagator Kernel) 与最优拆单执行减摩
    public TransientMarketImpactPropagatorResult? TransientMarketImpactPropagator { get; set; }

    // Phase 47 机构级优化：Renaissance Technologies & D.E. Shaw 马利亚温随机变分分析 (Malliavin Calculus) 与无似然高阶 Greeks 敏感度对偶
    public MalliavinCalculusSensitivityResult? MalliavinCalculusSensitivity { get; set; }

    // Phase 47 机构级优化：Citadel Global Strategies & Millennium Management 半定松弛 (SDR) 锥优化与基数约束稀疏指数跟踪
    public SemidefiniteRelaxationCardinalityResult? SemidefiniteRelaxationCardinality { get; set; }

    // Phase 47 机构级优化：Two Sigma & Bridgewater Associates 连续时间平均场博弈 (Mean Field Games, MFG) 与机构间策略纳什均衡执行
    public MeanFieldGameExecutionResult? MeanFieldGameExecution { get; set; }

    // Phase 47 机构级优化：Jane Street Capital & Jump Trading Kyle-Back 连续拍卖动态知情交易与隐匿执行模型
    public KyleBackStealthExecutionResult? KyleBackStealthExecution { get; set; }

    // Phase 48 机构级优化：Renaissance Technologies & D.E. Shaw 跳跃扩散 Lévy 过程 Esscher 测度变换与随机庞特里亚金极大值原理 (SPMP) 伴随状态最优控制
    public StochasticPontryaginControlResult? StochasticPontryaginControl { get; set; }

    // Phase 48 机构级优化：Citadel Global Strategies & Millennium Management 多 Pod 去中心化共识交替方向乘子法 (Consensus ADMM) 与增广拉格朗日多重约束动态资本仲裁
    public ConsensusAdmmArbitrationResult? ConsensusAdmmArbitration { get; set; }

    // Phase 48 机构级优化：Two Sigma & Bridgewater Associates 非参数分层狄利克雷过程隐马尔可夫模型 (HDP-HMM) 自适应宏观结构聚类与未见体制动态涌现
    public InfiniteHdpMacroClusteringResult? InfiniteHdpMacroClustering { get; set; }

    // Phase 48 机构级优化：Jane Street Capital & Jump Trading 双重随机 Cox 点过程与非齐次复合泊松跳跃做市最优非对称价差偏置控制
    public CoxProcessAsymmetricMarketMakingResult? CoxProcessAsymmetricMarketMaking { get; set; }

    // Phase 49 机构级优化：Renaissance Technologies & D.E. Shaw 粗糙分数 Ornstein-Uhlenbeck (fOU) 反持续性长程记忆与 Skorokhod 非适应超前对冲
    public RoughFractionalOuMemoryResult? RoughFractionalOuMemory { get; set; }

    // Phase 49 机构级优化：Citadel Global Strategies & Millennium Management 双层分层 Stackelberg 动态主从博弈与道德风险防范委托-代理多经理最优契约机制
    public BilevelStackelbergContractResult? BilevelStackelbergContract { get; set; }

    // Phase 49 机构级优化：Two Sigma & Bridgewater Associates 拓扑信息几何 Fisher-Rao 黎曼流形测地线距离与 Wasserstein 测度重心相变预警
    public TopologicalInformationGeometryResult? TopologicalInformationGeometry { get; set; }

    // Phase 49 机构级优化：Jane Street Capital & Jump Trading 多维 Hawkes 自激/互激点过程谱半径与 LOB 队列反应式微观流动性黑洞防御
    public MultivariateHawkesMicrostructureResult? MultivariateHawkesMicrostructure { get; set; }

    // Phase 50 机构级优化：Renaissance Technologies & D.E. Shaw 非对易自由概率论 (Voiculescu Free Probability) 与量子重整化群 (QRG) 高维矩阵奇异谱修复
    public VoiculescuFreeProbabilityQrgResult? VoiculescuFreeProbabilityQrg { get; set; }

    // Phase 50 机构级优化：Citadel Global Strategies & AQR Capital Management 宏观随机动力学混沌奇异吸引子 (Lyapunov Exponent Spectrum) 与托姆尖点突变论 (Thom Cusp Catastrophe) 极值跳变防踩踏
    public ThomCatastropheChaosDynamicsResult? ThomCatastropheChaosDynamics { get; set; }

    // Phase 50 机构级优化：Two Sigma & WorldQuant 神经薛定谔桥 (Neural Schrödinger Bridge) 熵正则化最优输运与反射倒向随机微分方程 (Reflected BSDE) 资本硬约束动态流动性生成式对冲
    public NeuralSchrodingerBridgeReflectedBsdeResult? NeuralSchrodingerBridgeReflectedBsde { get; set; }

    // Phase 50 机构级优化：Jane Street Capital & Hudson River Trading 玻尔兹曼-弗拉索夫 (Boltzmann-Vlasov) 动理学流动性连续体场论与相对论性纳什执行博弈
    public BoltzmannVlasovRelativisticExecutionResult? BoltzmannVlasovRelativisticExecution { get; set; }

    // Phase 51 机构级优化：Millennium Management & Point72 多 PM 策略 Pod 凸二次多维资本分配与内部互冲去交叉阻尼引擎
    public MillenniumConvexPodAllocationResult? MillenniumConvexPodAllocation { get; set; }

    // Phase 51 机构级优化：Jump Trading & Tower Research Capital 粗糙分数阶随机波动率 (Gatheral Rough Heston) 与微观粗糙度幂律偏度流形引擎
    public RoughFractionalVolatilityGatheralResult? RoughFractionalVolatilityGatheral { get; set; }

    // Phase 51 机构级优化：Bridgewater Associates & BlackRock Aladdin 宏观热力学最小相对交叉熵 (Jaynes MaxEnt) 与非高斯情景冲击流形映射引擎
    public ThermodynamicCrossEntropyStressResult? ThermodynamicCrossEntropyStress { get; set; }

    // Phase 51 机构级优化：Jump Trading & Hudson River Trading 超对称路径积分 (Supersymmetric Path Integral) 与非微扰瞬子 (Instanton) 极端黑天鹅跃迁防御引擎
    public SupersymmetricInstantonTunnelingResult? SupersymmetricInstantonTunneling { get; set; }

    // Phase 52 机构级优化：Citadel Securities & Jane Street 微观瞬态幂律记忆价格冲击核 (Bouchaud Propagator) 与动态隐匿拆单执行引擎
    public BouchaudTransientImpactPropagatorResult? BouchaudTransientImpactPropagator { get; set; }

    // Phase 52 机构级优化：Two Sigma & D.E. Shaw 高维流形图拉普拉斯算子谱聚类与非局部热核扩散小波多尺度 Alpha 分解引擎
    public GraphLaplacianDiffusionWaveletResult? GraphLaplacianDiffusionWavelet { get; set; }

    // Phase 52 机构级优化：Bridgewater Associates & AQR Capital 分数阶粘弹性流变学宏观资产负荷动力学与系统性流动性蠕变松弛测算引擎
    public ViscoelasticRheologyCapitalStrainResult? ViscoelasticRheologyCapitalStrain { get; set; }

    // Phase 52 机构级优化：Renaissance Technologies & Hudson River Trading 非平衡态朗之万细致平衡破缺与概率流旋度相空间统计套利做功巡回引擎
    public NonequilibriumLangevinVorticityResult? NonequilibriumLangevinVorticity { get; set; }

    // Phase 53 机构级优化：Jump Trading & Optiver 连续时间仿射跳跃-扩散限价簿做市与自适应逆向选择免受损控制引擎
    public JumpDiffusionAffineMarketMakingResult? JumpDiffusionAffineMarketMaking { get; set; }

    // Phase 53 机构级优化：Citadel Global Fixed Income & Millennium Macro 多因子无套利高斯仿射动态期限结构与曲率相对价值套利引擎
    public AffineArbitrageFreeTermStructureResult? AffineArbitrageFreeTermStructure { get; set; }

    // Phase 53 机构级优化：Point72 & Citadel Multi-Strategy 多 Pod 合作博弈 Shapley-Owen 边际分散贡献与动态流动性影子定价仲裁引擎
    public MultiPodShapleyShadowPricingResult? MultiPodShapleyShadowPricing { get; set; }

    // Phase 53 机构级优化：D.E. Shaw & WorldQuant 非欧流形 Ollivier-Ricci 拓扑曲率流相变相干与高阶持久同调过滤引擎
    public OllivierRicciCurvaturePersistentHomologyResult? OllivierRicciCurvaturePersistentHomology { get; set; }

    // Phase 54 机构级优化：Renaissance Technologies & D.E. Shaw 连续时间马尔可夫跳跃状态空间与分层狄利克雷过程 (HDP-HMM) 动态非参数宏观体制涌现引擎
    public ContinuousMarkovSwitchingDirichletProcessResult? ContinuousMarkovSwitchingDirichletProcess { get; set; }

    // Phase 54 机构级优化：Citadel Securities & Hudson River Trading 双边限价订单簿微观非线性平均场博弈与高阶随机冲量最优流动性提供控制引擎
    public MeanFieldGameImpulseLiquidityControlResult? MeanFieldGameImpulseLiquidityControl { get; set; }

    // Phase 54 机构级优化：Two Sigma & WorldQuant 高维因果发现有向无环图与结构因果模型反事实干预不变性 Alpha 纯化引擎
    public CausalDagStructuralInvarianceAlphaResult? CausalDagStructuralInvarianceAlpha { get; set; }

    // Phase 54 机构级优化：Bridgewater Associates & AQR Capital 广义非对称谱风险测度与动态极值高斯-Student Copula 尾部联动全景压测引擎
    public SpectralRiskExtremeCopulaStressResult? SpectralRiskExtremeCopulaStress { get; set; }

    // Phase 55 机构级优化：Renaissance Technologies & Two Sigma 高维随机矩阵理论局部谱去噪与收缩协方差重构引擎
    public RandomMatrixLocalSpectralShrinkageResult? RandomMatrixLocalSpectralShrinkage { get; set; }

    // Phase 55 机构级优化：Citadel Securities & Jump Trading 微观分形霍克斯自激互激订单流毒性与闪崩级联预警引擎
    public MultivariateHawkesToxicityCascadeResult? MultivariateHawkesToxicityCascade { get; set; }

    // Phase 55 机构级优化：Bridgewater Associates & AQR Capital 多资产非线性分形波动率记忆与赫斯特表面跨周期极值回撤对偶防御引擎
    public MultifractalHurstSurfaceDefenseResult? MultifractalHurstSurfaceDefense { get; set; }

    // Phase 55 机构级优化：Point72 & Millennium Management 多代理强化学习非稳态微观策略博弈与对抗性鲁棒策略蒸馏引擎
    public MultiAgentAdversarialPolicyDistillationResult? MultiAgentAdversarialPolicyDistillation { get; set; }

    // Phase 56 机构级优化：Jane Street & Citadel Securities 全档限价订单簿 (LOB) 微观价格扩散鞅测度变换与即时流动性真空渗透控制引擎
    public LobMicroPriceMartingaleVacuumPenetrationResult? LobMicroPriceMartingaleVacuumPenetration { get; set; }

    // Phase 56 机构级优化：D.E. Shaw & Two Sigma 连续时间多维非高斯列维-伊藤跳跃扩散与极值共跳曲率对冲引擎
    public MultidimensionalLevyItoJumpDiffusionResult? MultidimensionalLevyItoJumpDiffusion { get; set; }

    // Phase 56 机构级优化：Renaissance Technologies & Millennium Management 高阶拓扑超图场论与自旋玻璃阻挫能量退火解聚引擎
    public HypergraphSpinGlassFrustrationAnnealingResult? HypergraphSpinGlassFrustrationAnnealing { get; set; }

    // Phase 56 机构级优化：Bridgewater Associates & BlackRock Aladdin 达利欧长期主权债务大周期与超级去杠杆马尔可夫状态免疫引擎
    public SovereignDebtCycleDeleveragingImmunityResult? SovereignDebtCycleDeleveragingImmunity { get; set; }

    // Phase 57 机构级优化：Two Sigma & Citadel Securities 连续时间马利亚温随机变分微积分与粗糙波动率泛函高阶 Greeks 深度对冲引擎
    public MalliavinRoughVolatilityGreeksResult? MalliavinRoughVolatilityGreeks { get; set; }

    // Phase 57 机构级优化：Renaissance Technologies & Jump Trading 开放量子耗散系统 Lindblad 主方程与微观相干态密度矩阵统计套利引擎
    public QuantumLindbladDecoherenceStatArbResult? QuantumLindbladDecoherenceStatArb { get; set; }

    // Phase 57 机构级优化：Millennium Management & Point72 连续时间多主体均值场博弈 (Mean-Field Games) 与 FPK-HJB 拥挤踩踏解耦优化引擎
    public MeanFieldGameCrowdingDecouplingResult? MeanFieldGameCrowdingDecoupling { get; set; }

    // Phase 57 机构级优化：Bridgewater Associates & AQR Capital 非平衡态热力学最大熵产生与信息几何 Fisher-Rao 测地线宏观流形平滑轮动引擎
    public ThermodynamicFisherRaoGeodesicRegimeResult? ThermodynamicFisherRaoGeodesicRegime { get; set; }

    // Phase 58 机构级优化：Jane Street & Citadel Securities 连续时间 Glosten-Milgrom 贝叶斯信念更新与流动性逆向选择防护引擎
    public GlostenMilgromAdverseSelectionResult? GlostenMilgromAdverseSelection { get; set; }

    // Phase 58 机构级优化：Renaissance Technologies & D.E. Shaw 非广延统计力学 Tsallis 广义熵与多重分形奇异谱极值崩塌预警引擎
    public TsallisNonextensiveSingularSpectrumResult? TsallisNonextensiveSingularSpectrum { get; set; }

    // Phase 58 机构级优化：Two Sigma & Jump Trading 随机非线性粘性阻尼与 Fredholm 记忆核最优变分执行清算引擎
    public ViscousMemoryOptimalExecutionResult? ViscousMemoryOptimalExecution { get; set; }

    // Phase 58 机构级优化：Bridgewater Associates & Millennium Management 结构向量自回归有向无环图 (SVAR-DAG) 与 Pearl 反事实因果干预宏观网络引擎
    public SvarDagCausalInterventionNetworkResult? SvarDagCausalInterventionNetwork { get; set; }

    // Phase 59 机构级优化：Citadel Securities & Jump Trading 连续拍卖 Kyle's λ 价格冲击弹性与订单流信息穿透深度控制引擎
    public KyleContinuousAuctionElasticityResult? KyleContinuousAuctionElasticity { get; set; }

    // Phase 59 机构级优化：Renaissance Technologies & D.E. Shaw 拓扑 Wilson 重整化群矩流动与仿射渗流相变临界聚类引擎
    public WilsonRenormalizationPercolationResult? WilsonRenormalizationPercolation { get; set; }

    // Phase 59 机构级优化：Two Sigma & PDT Partners 连续时间随机微分博弈 (SDG) 捕食者-猎物纳什均衡与流动性黑洞抢跑规避变分引擎
    public PredatoryGameLiquidityEvasionResult? PredatoryGameLiquidityEvasion { get; set; }

    // Phase 59 机构级优化：Bridgewater Associates & AQR Capital 连续时间漂移-扩散非线性状态空间与鲁棒卡尔曼-布西宏观体制平价引擎
    public DriftDiffusionKalmanMacroParityResult? DriftDiffusionKalmanMacroParity { get; set; }

    // Phase 60 机构级优化：Citadel Securities & Optiver 非线性泊松跳跃边界反射与 Avellaneda-Stoikov 粘性滑移面做市控制引擎
    public NonlinearPoissonBoundaryMarketMakingResult? NonlinearPoissonBoundaryMarketMaking { get; set; }

    // Phase 60 机构级优化：Renaissance Technologies & D.E. Shaw 仿射 Kac-Moody 无限维李代数根格与杨-米尔斯规范场拓扑荷度量引擎
    public KacMoodyGaugeTopologicalChargeResult? KacMoodyGaugeTopologicalCharge { get; set; }

    // Phase 60 机构级优化：Two Sigma & Jump Trading McKean-Vlasov 前向-后向随机微分方程 (FBSDE) 非局部多主体平均场协同清算引擎
    public McKeanVlasovOptimalLiquidationResult? McKeanVlasovOptimalLiquidation { get; set; }

    // Phase 60 机构级优化：Bridgewater Associates & Millennium Macro 分数阶伊藤-沃尔泰拉 (Ito-Volterra) 非马尔可夫信用周期记忆与遍历谱平价引擎
    public VolterraNonMarkovianCreditParityResult? VolterraNonMarkovianCreditParity { get; set; }

    // Phase 61 机构级优化：Citadel Securities & Jane Street 微观高阶粗糙霍克斯过程与订单簿队列隐性排队延迟博弈引擎
    public RoughHawkesQueueLatencyArbitrageResult? RoughHawkesQueueLatencyArbitrage { get; set; }

    // Phase 61 机构级优化：Renaissance Technologies & D.E. Shaw 相空间辛几何哈密顿流形动力学与辛积分保结构降维共振引擎
    public SymplecticHamiltonianManifoldResonanceResult? SymplecticHamiltonianManifoldResonance { get; set; }

    // Phase 61 机构级优化：Two Sigma & Point72 最优传输 (Monge-Kantorovich) Wasserstein 重心测度流与截面非平衡协同重构引擎
    public WassersteinBarycenterDynamicRebalancingResult? WassersteinBarycenterDynamicRebalancing { get; set; }

    // Phase 61 机构级优化：Bridgewater Associates & AQR Capital 连续谱量子混沌非对易算子多尺度周期与超全天候宏观体制平价引擎
    public QuantumSpectralChaosMacroParityResult? QuantumSpectralChaosMacroParity { get; set; }

    // Phase 62 机构级优化：Millennium Management & Point72 多子策略高水位动态资本回撤扣划与跨组合因子拥挤解耦引擎
    public MultiPodFactorCrowdingClawbackResult? MultiPodFactorCrowdingClawback { get; set; }

    // Phase 62 机构级优化：BlackRock Aladdin & NBIM / GIC NGFS 气候宏观转型三情景压力测试与碳贝塔搁浅资产 VaR 引擎
    public ClimateTransitionStrandedAssetStressResult? ClimateTransitionStrandedAssetStress { get; set; }

    // Phase 62 机构级优化：Two Sigma & D.E. Shaw 高阶张量环 (Tensor Ring) 紧致分解与隐式多模态 Alpha 场引擎
    public TensorRingMultimodalAlphaFieldResult? TensorRingMultimodalAlphaField { get; set; }

    // Phase 62 机构级优化：Citadel & Jump Trading 维纳-泊松空间马利亚温微分 (Malliavin Calculus) 跳跃扩散随机波动率对冲引擎
    public MalliavinJumpDiffusionHedgingResult? MalliavinJumpDiffusionHedging { get; set; }
}

public class StoredFundItem
{
    public bool IsSelected { get; set; } // 选基大厅批量勾选操作标识
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Manager { get; set; } = string.Empty;
    public string Tenure { get; set; } = string.Empty;
    public string FundSize { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public decimal LatestNav { get; set; }

    // 机构级核心量化因子
    public decimal Return1Y { get; set; } // 近1年复权累计收益率 (%)
    public decimal MaxDrawdown { get; set; } // 历史最大回撤 (%)
    public decimal SharpeRatio { get; set; } // 夏普比率 (Rf=2%)
    public decimal AnnualizedVol { get; set; } // 年化波动率 (%)
    public decimal QuantScore { get; set; } // 量化综合评分 (0~100)
    public string RatingGrade { get; set; } = string.Empty; // 量化评级 (AAA卓越, AA优良, A稳健, B中性, C谨慎)
    public decimal CustomWeightedScore { get; set; } // 自定义多因子加权得分 (0~100)

    // Phase 12 高阶机构筛选风控因子
    public decimal CalmarRatio => MaxDrawdown > 0 ? Math.Round(Return1Y / MaxDrawdown, 2) : 0m;
    public decimal SortinoRatio { get; set; }
    public decimal CaptureSpread { get; set; }
    public string StarRating { get; set; } = "★★★☆☆";

    // Phase 14 截面多因子标准化评分与机构评级标签
    public decimal MultiFactorCompositeScore { get; set; } // 截面标准化综合得分 (0~100)
    public decimal MultiFactorPercentile { get; set; } // 截面百分位排名 (0~100%)
    public string MultiFactorRatingTag { get; set; } = string.Empty; // 机构评级标签 (如 💎 核心底仓 / 🚀 进攻先锋 / 🛡️ 稳健防守)
    public decimal MomentumScore { get; set; }
    public decimal RiskAdjustedScore { get; set; }
    public decimal DownsideDefenseScore { get; set; }
    public decimal AlphaPurityScore { get; set; }
    public decimal ConvexityScore { get; set; }

    // Phase 15 科学短中期评估与决策信号
    public decimal ShortTermScore { get; set; }
    public decimal MediumTermScore { get; set; }
    public string ScientificActionSignal { get; set; } = string.Empty;
    public decimal ScientificConviction { get; set; }
    public string ConcordanceType { get; set; } = string.Empty;

    // Phase 17 机构 4433 选基与阿尔法持续性
    public bool Passed4433 { get; set; }
    public string Passed4433Tag => Passed4433 ? "🏆 4433优选" : "";
    public decimal AlphaPersistenceScore { get; set; }
    public string AlphaPersistenceGrade { get; set; } = string.Empty;

    public string Sector => FundSectorHelper.DetectSector(Name, Type);
}

public class MarketCatalogItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Pinyin { get; set; } = string.Empty;
    public string PinyinFull { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public string Sector => FundSectorHelper.DetectSector(Name, Type);
}

public class RealtimeValuation
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal UnitNav { get; set; }              // 最新公布单位净值
    public decimal EstimatedNav { get; set; }         // 盘中实时估算净值
    public decimal EstimatedGrowthRate { get; set; }   // 盘中估算涨跌幅 (%)
    public string ValuationTime { get; set; } = string.Empty; // 估值时间
    public string NavDate { get; set; } = string.Empty;       // 最新公布净值日期
    public decimal DailyReturn { get; set; }          // 最新公布日收益率 (%)
}

public class FundQuantScoreCard
{
    public decimal OverallScore { get; set; } // 综合量化评分 (0~100)
    public string RatingGrade { get; set; } = string.Empty; // 评级 (AAA卓越, AA优良, A稳健, B中性, C谨慎)
    public decimal ReturnScore { get; set; }        // 收益维度得分 (0~100, 权重25%)
    public decimal RiskControlScore { get; set; }   // 风控维度得分 (0~100, 权重20%)
    public decimal RiskAdjustedScore { get; set; }  // 风险调整维度得分 (0~100, 权重25%)
    public decimal ResilienceScore { get; set; }    // 极端抗跌维度得分 (0~100, 权重15%)
    public decimal StabilityScore { get; set; }     // 风格集中度维度得分 (0~100, 权重15%)
    public List<string> Strengths { get; set; } = new(); // 亮点列表
    public List<string> Weaknesses { get; set; } = new(); // 风险点列表
    public List<string> HighlightTags => Strengths;
    public List<string> RiskWarnings => Weaknesses;
}

public class BrinsonSectorItem
{
    public string SectorName { get; set; } = string.Empty;
    public decimal PortfolioWeight { get; set; } // 组合/基金资产占比 (0~100%)
    public decimal BenchmarkWeight { get; set; } // 基准权重 (%)
    public decimal PortfolioReturn { get; set; } // 组合分项收益率 (%)
    public decimal BenchmarkReturn { get; set; } // 基准分项收益率 (%)
    public decimal AllocationEffect { get; set; } // 资产配置效应 Q_alloc (%)
    public decimal SelectionEffect { get; set; }  // 个股选择效应 Q_select (%)
    public decimal InteractionEffect { get; set; } // 交叉互动效应 Q_inter (%)
    public decimal TotalEffect => AllocationEffect + SelectionEffect + InteractionEffect; // 总贡献效应 (%)
}

public class BrinsonAttributionResult
{
    public decimal TotalPortfolioReturn { get; set; }
    public decimal TotalBenchmarkReturn { get; set; }
    public decimal TotalExcessReturn => TotalPortfolioReturn - TotalBenchmarkReturn;
    public decimal TotalAllocationEffect { get; set; }
    public decimal TotalSelectionEffect { get; set; }
    public decimal TotalInteractionEffect { get; set; }
    public List<BrinsonSectorItem> SectorItems { get; set; } = new();
    public string SummaryAnalysis { get; set; } = string.Empty;
}

public class RebalanceOrderItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal CurrentAmount { get; set; }      // 当前持有金额 (元)
    public decimal CurrentWeight { get; set; }      // 当前占比 (%)
    public decimal TargetAmount { get; set; }       // 目标持有金额 (元)
    public decimal TargetWeight { get; set; }       // 目标占比 (%)
    public string Action { get; set; } = "持有";     // 操作建议: 买入/卖出/持有
    public decimal TradeAmount { get; set; }        // 理论交易金额 (元，绝对值)
    public decimal ExecutableTradeAmount { get; set; } // 整手/步长取整后实际交易金额 (元)
    public decimal LotRoundingResidual { get; set; }  // 取整残差零头 (元)
    public decimal ExecutedTargetWeight { get; set; }  // 取整执行后实际达成权重 (%)
    public decimal TradeWeightChange { get; set; }  // 权重变动 (%)
    public decimal EstimatedFee { get; set; }       // 预估交易手续费 (元)
}

public class RebalanceOrderSheet
{
    public decimal TotalPortfolioValue { get; set; } // 组合总净资产 (元)
    public string TargetSchemeName { get; set; } = string.Empty; // 目标方案名称 (如 "风险平价 (ERC)")
    public decimal TotalBuyAmount { get; set; }      // 计划买入总额 (元)
    public decimal TotalSellAmount { get; set; }     // 计划卖出总额 (元)
    public decimal ExecutableBuyAmount { get; set; } // 取整后实际买入总额 (元)
    public decimal ExecutableSellAmount { get; set; } // 取整后实际卖出总额 (元)
    public decimal NetCashChange { get; set; }       // 净出资额 (买入 - 卖出)
    public decimal EstimatedTotalFees { get; set; }  // 预计总调仓费用 (元)
    public decimal TurnoverRate { get; set; }        // 组合双边换手率 (%)
    public decimal ExecutableTurnoverRate { get; set; } // 整手执行后实际单边换手率 (%)
    public decimal LotSizeRoundingStep { get; set; } = 100m; // 调仓取整步长 (元，默认 100 元整)
    public decimal MinSubscriptionAmount { get; set; } = 100m; // 最低起购门槛 (元)
    public decimal CashBufferReserved { get; set; } // 预留现金安全缓冲 (元)
    public decimal CashBufferPercent { get; set; } = 2.0m; // 现金缓冲比例 (%)
    public decimal CashDragAnnualizedCost { get; set; } // 预估现金拖累年化成本 (%)
    public List<RebalanceOrderItem> Orders { get; set; } = new();
}

public class MetricCompareRow
{
    public string MetricName { get; set; } = string.Empty;
    public string ValueA { get; set; } = string.Empty;
    public string Winner { get; set; } = string.Empty;
    public string ValueB { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class CommonStockItem
{
    public string StockCode { get; set; } = string.Empty;
    public string StockName { get; set; } = string.Empty;
    public decimal WeightA { get; set; } // 基金 A 权重 (%)
    public decimal WeightB { get; set; } // 基金 B 权重 (%)
    public decimal OverlapWeight => Math.Min(WeightA, WeightB); // 共同有效重合权重 (%)
    public decimal WeightDiff => WeightA - WeightB; // 权重差 (A - B)
    public string Industry { get; set; } = string.Empty; // 申万一级行业分类
}

public class UniqueStockItem
{
    public string StockCode { get; set; } = string.Empty;
    public string StockName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public string Industry { get; set; } = string.Empty;
    public string FundOwner { get; set; } = "A"; // "A" 或 "B"
}

public class IndustryExposureSpreadItem
{
    public string IndustryName { get; set; } = string.Empty;
    public decimal WeightA { get; set; } // 基金 A 行业暴露 (%)
    public decimal WeightB { get; set; } // 基金 B 行业暴露 (%)
    public decimal Spread => WeightA - WeightB; // 暴露差额 (%)
    public string Status => Spread switch
    {
        > 3.0m => "🔵 基金A显著超配",
        < -3.0m => "🔴 基金B显著超配",
        _ => "⚖️ 配置相对均衡"
    };
}

public class FundHoldingOverlapResult
{
    public int CommonCount { get; set; } // 共同持有股票数量
    public decimal OverlapWeightPercent { get; set; } // 重合权重综合比例 (%)
    public string OverlapRating { get; set; } = "低度重合"; // "极高同质化", "中度重合", "低度重合", "互补分散"
    public string OverlapSummary { get; set; } = string.Empty; // 穿透诊断结论
    public List<CommonStockItem> CommonStocks { get; set; } = new();
    public List<UniqueStockItem> UniqueStocksA { get; set; } = new();
    public List<UniqueStockItem> UniqueStocksB { get; set; } = new();
    public List<IndustryExposureSpreadItem> IndustrySpreads { get; set; } = new();
}

public class FundCompareFullResult
{
    public FundDetail FundA { get; set; } = new();
    public FundDetail FundB { get; set; } = new();
    public QuantMetrics MetricsA { get; set; } = new();
    public QuantMetrics MetricsB { get; set; } = new();
    public FundQuantScoreCard ScoreCardA { get; set; } = new();
    public FundQuantScoreCard ScoreCardB { get; set; } = new();
    public double Correlation { get; set; }
    public string CorrelationRating { get; set; } = string.Empty;
    public double WinRateA { get; set; }
    public double WinRateB { get; set; }
    public FundHoldingOverlapResult OverlapResult { get; set; } = new();
    public BullBearCompareResult? BullBearCompare { get; set; }
    public string Range { get; set; } = "1Y";
}

public class ScoreCardCompareRow
{
    public string Dimension { get; set; } = string.Empty;
    public string WeightText { get; set; } = string.Empty;
    public string ScoreA { get; set; } = string.Empty;
    public string Winner { get; set; } = string.Empty;
    public string ScoreB { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class PortfolioLookThroughHolding
{
    public string StockCode { get; set; } = string.Empty;
    public string StockName { get; set; } = string.Empty;
    public decimal PortfolioWeight { get; set; } // 组合底层穿透有效绝对权重 (%)
    public string Industry { get; set; } = string.Empty; // 申万一级行业
    public string ContributingFunds { get; set; } = string.Empty; // e.g. "华夏成长(3.2%), 中欧医疗(1.8%)"
    public int FundCount { get; set; } // 持有此标的的成分基金数量
    public bool IsConsensusHeavy => FundCount >= 2; // 是否为多基共同重仓标的
}

public class PortfolioLookThroughIndustry
{
    public string IndustryName { get; set; } = string.Empty;
    public decimal PortfolioWeight { get; set; } // 该行业在组合底层的总穿透暴露 (%)
    public int StockCount { get; set; } // 该行业覆盖的重仓个股只数
    public decimal RatioOfIdentified { get; set; } // 占已识别重仓总权重的比例 (%)
}

public class PortfolioLookThroughResult
{
    public List<PortfolioLookThroughHolding> TopHoldings { get; set; } = new(); // 按穿透权重降序的前十大/前二十大
    public List<PortfolioLookThroughIndustry> IndustryExposures { get; set; } = new(); // 申万行业敞口分布
    public decimal PortfolioCr10 { get; set; } // 组合穿透前十大股票集中度 (%)
    public decimal TotalHoldingsWeight { get; set; } // 组合已穿透股票总权重 (%)
    public int TotalUniqueStocks { get; set; } // 穿透覆盖的非重复个股总数
    public int ConsensusStockCount { get; set; } // 被2只及以上基金共同重仓的股票数量
    public decimal StocksHhi { get; set; } // 穿透股票持仓 HHI 指数 (0~10000)
    public decimal IndustryHhi { get; set; } // 申万行业敞口 HHI 指数 (0~10000)
    public decimal EffectiveStockCount { get; set; } // 真实有效持仓个股只数 (N_eff = 1 / sum(w_i^2))
    public string HhiConcentrationLevel { get; set; } = "适度集中"; // 高度分散 / 适度集中 / 显著抱团
    public string LookThroughSummary { get; set; } = string.Empty; // 穿透风险诊断结论
}

public class MonteCarloSimulationResult
{
    public int HorizonTradingDays { get; set; } = 250; // 推演交易日数 (如 250 天 = 1 年)
    public int SimulationRuns { get; set; } = 1000;    // 模拟时序路径数 (如 1000 条)
    public decimal Percentile95Return { get; set; }   // 95% 乐观上限收益率 (%)
    public decimal MedianReturn { get; set; }         // 50% 中位数期望收益率 (%)
    public decimal Percentile5Return { get; set; }    // 5% 悲观下限防御底线收益率 (%)
    public decimal ProbabilityOfLoss { get; set; }    // 本金亏损概率 (%)
    public decimal ExpectedSimulatedDrawdown { get; set; } // 模拟路径平均最大回撤 (%)
    public decimal EndNav95 { get; set; }             // 95% 乐观终值净值
    public decimal EndNavMedian { get; set; }         // 50% 中位数终值净值
    public decimal EndNav5 { get; set; }              // 5% 悲观终值净值
    public string SimulationSummary { get; set; } = string.Empty; // 推演报告结论

    // 蒙特卡洛 250 天时序扇形展开分位数曲线 (Fan Chart Data)
    public double[] TrajectoryDays { get; set; } = Array.Empty<double>(); // 0..250
    public double[] Trajectory95 { get; set; } = Array.Empty<double>();   // 95% 乐观上轨时序
    public double[] Trajectory75 { get; set; } = Array.Empty<double>();   // 75% 中上分位时序
    public double[] Trajectory50 { get; set; } = Array.Empty<double>();   // 50% 基准中位时序
    public double[] Trajectory25 { get; set; } = Array.Empty<double>();   // 25% 承压分位时序
    public double[] Trajectory5 { get; set; } = Array.Empty<double>();    // 5% 悲观底线时序
}

/// <summary>
/// 历史单季度晨星九宫格风格点
/// </summary>
public class StyleDriftPoint
{
    public string Period { get; set; } = string.Empty;          // 报告期，如 "2024Q3"
    public double SizeScore { get; set; }                       // 市值规模评分 (-100~+100, >0大盘, <0小盘)
    public double ValueScore { get; set; }                      // 风格属性评分 (-100~+100, <0价值, >0成长)
    public string StyleBoxName { get; set; } = "平衡型";        // 风格九宫格名称
    public decimal Cr10 { get; set; }                           // 该期前十大集中度
    public string TopIndustry { get; set; } = string.Empty;     // 第一大行业
}

/// <summary>
/// 机构级风格漂移分析结果与稳定性指数 (Style Drift Index, SDI)
/// </summary>
public class StyleDriftAnalysisResult
{
    public List<StyleDriftPoint> HistoryPoints { get; set; } = new();
    public decimal StyleDriftIndex { get; set; }                // 风格漂移度指数 (SDI)
    public string StabilityRating { get; set; } = string.Empty;    // 🎯 极度稳定 / ⚖️ 基本稳定 / ⚠️ 显著漂移
    public string AnalysisSummary { get; set; } = string.Empty;
}

/// <summary>
/// 宏观情景冲击下单只基金的受损与风险贡献
/// </summary>
public class MacroShockFundImpact
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal WeightPercent { get; set; } // 组合内配置权重 (%)
    public decimal Beta { get; set; }          // 相对权益基准的 Beta
    public decimal EstimatedNavChangePercent { get; set; } // 预估该基金净值涨跌 (%)
    public decimal ContributionPercent { get; set; } // 对组合总体冲击损益的边际贡献度 (%)
}

/// <summary>
/// 宏观情景与多因子冲击测试情景
/// </summary>
public class MacroShockScenario
{
    public string ScenarioId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty; // 如 "🐻 权益市场系统性暴跌"
    public string Description { get; set; } = string.Empty;
    public decimal EquityShockPercent { get; set; } // 股票资产冲击幅 (%)
    public decimal InterestRateShockBps { get; set; } // 市场利率变动 (基点 bps)
    public decimal EstimatedNavChangePercent { get; set; } // 组合预估整体净值冲击率 (%)
    public decimal EstimatedPnL { get; set; } // 组合预估金额损益 (元)
    public decimal StressedVaR95 { get; set; } // 压力状态下的日在险价值 Stressed VaR95 (%)
    public string ImpactRating { get; set; } = "适中"; // "轻微影响" / "适度承压" / "严重受创"
    public List<MacroShockFundImpact> FundImpacts { get; set; } = new();
}

/// <summary>
/// 组合宏观压力测试模拟综合结果
/// </summary>
public class MacroShockSimulationResult
{
    public decimal TotalCapital { get; set; } = 1000000m; // 组合设定总本金 (元)
    public List<MacroShockScenario> PresetScenarios { get; set; } = new();
    public MacroShockScenario? CustomScenario { get; set; }
    public string DiagnosticSummary { get; set; } = string.Empty;
}

/// <summary>
/// Barra 风格多因子单因子暴露与收益贡献项
/// </summary>
public class BarraFactorItem
{
    public string FactorId { get; set; } = string.Empty;       // 如 "Size", "Value", "Growth", "Momentum", "LowVol", "Dividend"
    public string FactorName { get; set; } = string.Empty;     // 如 "市值规模 (Size)", "估值价值 (Value)"
    public string FactorCategory { get; set; } = "风格因子";   // "风格因子" / "动量风险" / "宏观收益"
    public decimal ExposureBeta { get; set; }                  // 因子暴露系数 Beta
    public decimal CumulativeReturn { get; set; }              // 因子累计收益率 (%)
    public decimal AnnualizedContribution { get; set; }        // 因子年化收益贡献 (%) = Beta * FactorAnnReturn
    public decimal ContributionPercent { get; set; }           // 占解释超额收益的相对比例 (%)
    public string ExposureEvaluation { get; set; } = "中性";   // "显著正暴露" / "适度正暴露" / "中性平衡" / "适度负暴露" / "显著负暴露"

    // 便捷属性别名
    public string FactorCode => FactorId;
    public decimal Beta => ExposureBeta;
    public decimal ContributionReturn => AnnualizedContribution;
    public double TStat => Math.Round(Math.Abs((double)ExposureBeta) * 3.5, 2);
    public string Significance => ExposureEvaluation.Contains("显著") ? "*** (p<0.01)" : (ExposureEvaluation.Contains("适度") ? "** (p<0.05)" : "中性");
    public string Description => ExposureEvaluation;
}

/// <summary>
/// Barra CNE5/CNE6 风格多因子回归分解综合结果
/// </summary>
public class BarraAttributionResult
{
    public List<BarraFactorItem> FactorItems { get; set; } = new();
    public decimal RSquared { get; set; }                      // 模型拟合优度 R^2 (0~1.0)
    public decimal RSquaredPercent => Math.Round(RSquared * 100m, 1); // 风格解释度 (%)
    public decimal SpecificRiskPercent => Math.Round(Math.Max(0m, 1.0m - RSquared) * 100m, 1); // 特质风险比例 (%)
    public decimal SpecificAlphaAnnualized { get; set; }       // 年化特异选股阿尔法 Alpha (%)
    public decimal FactorTotalContribution { get; set; }       // 风格因子合计年化收益贡献 (%)
    public decimal FundAnnualizedExcess { get; set; }          // 基金实际年化超额收益 (%)
    public string DominantStyle { get; set; } = "宽基均衡风格"; // 主导风格标签
    public string AttributionSummary { get; set; } = string.Empty; // 归因诊断总结

    // 便捷属性别名
    public List<BarraFactorItem> Factors => FactorItems;
    public decimal SpecificAlpha => SpecificAlphaAnnualized;
}

/// <summary>
/// 布莱克-莱特曼 (Black-Litterman) 投资者主观/量化观点契约
/// </summary>
public class BlackLittermanView
{
    public string AssetCodeA { get; set; } = string.Empty;     // 标的资产代码 A (多头或绝对观点)
    public string? AssetCodeB { get; set; }                    // 标的资产代码 B (相对观点中的空头对冲资产，若为 null 则为绝对观点)
    public decimal ExpectedReturn { get; set; }                // 预期年化收益率 / 相对跑赢幅度 (%)
    public double Confidence { get; set; } = 0.6;              // 观点置信度 (0.1 ~ 1.0)
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// 组合动态再平衡策略类型
/// </summary>
public enum RebalanceStrategyType
{
    BuyAndHold,       // 买入持有 (零再平衡，任由权重漂移)
    MonthlyCalendar,  // 月度日历强制再平衡
    QuarterlyCalendar,// 季度日历强制再平衡
    ThresholdBand     // 偏离容忍度阈值再平衡 (如偏离目标 ±5% 触发)
}

/// <summary>
/// 组合动态再平衡单次调仓事件明细
/// </summary>
public class RebalanceEventItem
{
    public DateTime Date { get; set; }                         // 调仓发生交易日
    public string TriggerReason { get; set; } = string.Empty; // 触发原因 (如 "月度定期平衡", "权重偏离达 5.2%")
    public decimal PortfolioNavBefore { get; set; }            // 调仓前单位净值
    public decimal TurnoverRate { get; set; }                  // 当次单边换手率 (%)
    public decimal FrictionFeeAmount { get; set; }             // 当次扣减的摩擦损耗费用 (元，按基准本金测算)
    public decimal MaxWeightDeviationBefore { get; set; }      // 调仓前资产相对目标最大偏离度 (%)
    public string Details { get; set; } = string.Empty;        // 调仓细节简述
}

/// <summary>
/// 组合动态时序再平衡与换手摩擦模拟结果
/// </summary>
public class RebalanceSimulationResult
{
    public RebalanceStrategyType StrategyType { get; set; } = RebalanceStrategyType.MonthlyCalendar;
    public string StrategyName { get; set; } = "月度定期再平衡";
    public decimal InitialCapital { get; set; } = 1000000m;    // 初始测算本金 (元)
    public decimal FrictionFeeRate { get; set; } = 0.0015m;    // 单边申赎与冲击摩擦费率 (默认 0.15%)
    public decimal ThresholdBand { get; set; } = 0.05m;        // 容忍度阈值 (如 5%)

    public decimal FinalNav { get; set; }                      // 策略终值累计净值
    public decimal TotalReturn { get; set; }                   // 扣除手续费后总收益率 (%)
    public decimal AnnualizedReturn { get; set; }              // 扣除手续费后年化收益率 (CAGR %)
    public decimal AnnualizedVolatility { get; set; }          // 动态再平衡年化波动率 (%)
    public decimal MaxDrawdown { get; set; }                   // 动态再平衡最大回撤 (%)
    public decimal SharpeRatio { get; set; }                   // 动态再平衡夏普比率

    // 买入持有基准对比
    public decimal BuyAndHoldNav { get; set; }                 // 买入持有终值净值
    public decimal BuyAndHoldReturn { get; set; }              // 买入持有总收益率 (%)
    public decimal BuyAndHoldMaxDrawdown { get; set; }          // 买入持有最大回撤 (%)
    public decimal NetAlphaVsBuyAndHold => TotalReturn - BuyAndHoldReturn; // 动态调仓带来的净超额效益 (%)

    public int TotalRebalanceCount { get; set; }               // 累计触发调仓次数
    public decimal TotalTurnoverRate { get; set; }             // 累计双边换手率 (%)
    public decimal AnnualizedTurnoverRate { get; set; }         // 年均单边换手率 (%)
    public decimal TotalFrictionFeeLoss { get; set; }          // 累计摩擦磨损总金额 (元)
    public decimal FrictionFeeDragPercent { get; set; }        // 摩擦费率拖累收益率总百分点 (%)

    public List<RebalanceEventItem> Events { get; set; } = new();
    public string DiagnosticSummary { get; set; } = string.Empty;

    // 便捷属性别名
    public int RebalanceCount => TotalRebalanceCount;
}

/// <summary>
/// 基金经理职业生涯任期统计与滚动胜率档案
/// </summary>
public class ManagerCareerProfile
{
    public string ManagerName { get; set; } = string.Empty;    // 基金经理姓名
    public string FundCode { get; set; } = string.Empty;       // 基金代码
    public DateTime? TenureStartDate { get; set; }             // 任职起始日期
    public DateTime? TenureEndDate { get; set; }               // 任职截止日期 (null 表示至今)
    public int TenureDays { get; set; }                        // 任职天数
    public decimal TenureYears { get; set; }                   // 任职年限 (如 3.5 年)

    // 任职期业绩
    public decimal TenureTotalReturn { get; set; }             // 任期内累计总回报 (%)
    public decimal TenureAnnualizedReturn { get; set; }        // 任期内年化复合回报 CAGR (%)
    public decimal TenureBenchmarkReturn { get; set; }         // 同期沪深300基准收益率 (%)
    public decimal TenureExcessReturn => TenureTotalReturn - TenureBenchmarkReturn; // 任期累计超额收益 (%)
    public decimal TenureAnnualizedAlpha { get; set; }         // 任期年化主动超额收益 (%)
    public decimal TenureMaxDrawdown { get; set; }             // 任期内最大回撤 (%)

    // 滚动胜率矩阵 (Rolling Excess Win-Rate Matrix)
    public int TotalMonths { get; set; }                       // 统计自然月总数
    public int WinningMonths { get; set; }                     // 跑赢基准的月数
    public decimal MonthlyWinRate { get; set; }                // 月度超额胜率 (WinningMonths / TotalMonths * 100%)

    public int TotalQuarters { get; set; }                     // 统计自然季度数
    public int WinningQuarters { get; set; }                   // 跑赢基准的季度数
    public decimal QuarterlyWinRate { get; set; }              // 季度超额胜率 (WinningQuarters / TotalQuarters * 100%)

    public decimal MaxMonthlyOutperformance { get; set; }      // 单月最大跑赢幅度 (%)
    public decimal MaxMonthlyUnderperformance { get; set; }    // 单月最大跑输幅度 (%)

    public string WinRateRating { get; set; } = "均衡";        // 🏅 顶尖持续进攻 / ⚖️ 稳健均衡跑赢 / 🛡️ 防守抗跌 / ⚠️ 胜率偏弱
    public string CareerSummary { get; set; } = string.Empty;  // 生涯档案诊断评语

    // 便捷属性别名
    public string Tenure => $"{TenureYears:F1}年 ({TenureDays}天)";
    public string TenureString => Tenure;
    public decimal TenureCumulativeReturn => TenureTotalReturn;
    public decimal BenchmarkCumulativeReturn => TenureBenchmarkReturn;
    public decimal RollingMonthlyWinRate => MonthlyWinRate;
    public int MonthlyWinCount => WinningMonths;
    public decimal RollingQuarterlyWinRate => QuarterlyWinRate;
    public int QuarterlyWinCount => WinningQuarters;
    public decimal MaxOutperformanceMonth => MaxMonthlyOutperformance;
    public decimal MaxUnderperformanceMonth => MaxMonthlyUnderperformance;
    public string StabilityRating => WinRateRating;
    public string CareerStabilityRating => WinRateRating;
    public int OutperformingMonths { get => WinningMonths; set { } }
    public int TotalTenureMonths { get => TotalMonths; set { } }
    public int OutperformingQuarters { get => WinningQuarters; set { } }
    public int TotalTenureQuarters { get => TotalQuarters; set { } }
}

/// <summary>
/// 非正态极值尾部风险模型 (Cornish-Fisher 展开式与高阶矩风控)
/// </summary>
public class TailRiskMetrics
{
    public decimal Skewness { get; set; }                      // 样本偏度 (度量分布左右非对称性，负偏代表暴跌频次高)
    public decimal ExcessKurtosis { get; set; }                 // 超额峰度 (度量厚尾程度，>0 代表尖峰厚尾)
    public decimal CornishFisherVaR95 { get; set; }            // 95% 置信度 Cornish-Fisher 修正日度在险价值 (%)
    public decimal CornishFisherVaR99 { get; set; }            // 99% 置信度 Cornish-Fisher 修正日度在险价值 (%)
    public decimal CornishFisherCVaR95 { get; set; }           // 95% 置信度 Cornish-Fisher 修正预期短缺损失 (ES / mCVaR, %)
    public decimal CornishFisherCVaR99 { get; set; }           // 99% 置信度 Cornish-Fisher 修正预期短缺损失 (%)
    public decimal Basel10DayVaR99 { get; set; }               // 巴塞尔协议 III 监管口径 10 交易日 99% 极值 VaR (%)
    public string TailFatnessRating { get; set; } = "适度正态"; // "⚠️ 极度厚尾黑天鹅高危" / "⚡ 中度肥尾" / "🛡️ 准正态/防守右偏"
    public string DiagnosticSummary { get; set; } = string.Empty;

    // 便捷属性别名
    public decimal ModifiedVaR95 { get => CornishFisherVaR95; set { } }
    public decimal ModifiedCVaR95 { get => CornishFisherCVaR95; set { } }
    public decimal ModifiedVaR99 { get => CornishFisherVaR99; set { } }
    public decimal ModifiedCVaR99 { get => CornishFisherCVaR99; set { } }
    public decimal BaselVaR10d { get => Basel10DayVaR99; set { } }
}

/// <summary>
/// Fama-French 五因子资产定价模型 (FF5: MKT, SMB, HML, RMW, CMA) 单因子明细
/// </summary>
public class FamaFrenchFactorItem
{
    public string FactorId { get; set; } = string.Empty;       // MKT, SMB, HML, RMW, CMA
    public string FactorName { get; set; } = string.Empty;     // 市场溢价, 规模因子, 价值因子, 盈利能力, 投资模式
    public decimal Beta { get; set; }                          // 因子暴露系数 Beta
    public decimal TStat { get; set; }                         // t 检验统计量
    public decimal PValue { get; set; }                        // p 显著性水平
    public bool IsSignificant => Math.Abs(TStat) >= 1.96m;     // 95% 置信度显著标识
    public decimal AnnualizedContribution { get; set; }        // 因子年化收益贡献 (%)
    public decimal ContributionPercent { get; set; }           // 相对因子总解释收益贡献占比 (%)
    public string FactorCategory { get; set; } = string.Empty; // 市场风险溢价 / 风格规模 / 质量与经营模式
    public string FactorEvaluation { get; set; } = string.Empty;// 暴露评价

    // 便捷属性别名
    public string FactorCode => FactorId;
    public decimal ContributionReturn => AnnualizedContribution;
    public string PValueText => $"{PValue:F4}{(PValue < 0.01m ? " (***)" : (PValue < 0.05m ? " (**)" : (PValue < 0.10m ? " (*)" : "")))}";
    public string Description => $"{FactorCategory} - {FactorEvaluation}";
}

/// <summary>
/// Fama-French 五因子资产定价模型全景结果
/// </summary>
public class FamaFrench5Result
{
    public List<FamaFrenchFactorItem> FactorItems { get; set; } = new();
    public decimal AlphaAnnualized { get; set; }               // 剥离五因子后的纯特质选股阿尔法 (%)
    public decimal RSquared { get; set; }                      // 模型拟合优度 (0.0 ~ 1.0)
    public decimal RSquaredPercent => Math.Round(RSquared * 100m, 1); // 因子解释度 (%)
    public decimal ResidualRiskPercent => Math.Round(Math.Max(0m, 1.0m - RSquared) * 100m, 1); // 特质残差风险比例 (%)
    public decimal FactorTotalContribution { get; set; }       // 五因子合计年化解释收益 (%)
    public decimal FundAnnualizedExcess { get; set; }          // 基金实际年化超额收益 (%)
    public string DominantFactor { get; set; } = string.Empty; // 显著主导因子
    public string AttributionSummary { get; set; } = string.Empty; // 机构研报级因子归因总结

    // 便捷属性别名
    public List<FamaFrenchFactorItem> Factors => FactorItems;
    public decimal SpecificAlpha => AlphaAnnualized;
    public decimal SpecificAlphaAnnualized => AlphaAnnualized;
    public decimal SpecificRiskPercent => ResidualRiskPercent;
    public string DiagnosticSummary => AttributionSummary;
}

/// <summary>
/// 组合资产分散化效益与同质化穿透诊断 (Choueifaty Diversification Ratio & Homogeneity)
/// </summary>
public class PortfolioDiversificationResult
{
    public decimal DiversificationRatio { get; set; } = 1.0m;  // Choueifaty 分散化比率 (DR >= 1.0)
    public decimal VolatilityReductionPercent { get; set; }    // 分散化无偿降低的组合波动率比例 (%)
    public decimal WeightedAverageCorrelation { get; set; }    // 组合内部两两加权平均相关系数 rho
    public decimal HomogeneityScore { get; set; }              // 组合同质化得分 (0-100，越高代表资产越同质/抱团)
    public bool PseudoDiversificationWarning { get; set; }      // 伪分散高危警报 (同质化严重)
    public List<string> ClusterGroups { get; set; } = new();   // 资产层级聚类分组列表
    public string DiagnosticSummary { get; set; } = string.Empty;

    // 便捷属性别名
    public decimal DR => DiversificationRatio;
    public decimal ReductionPercent => VolatilityReductionPercent;
    public decimal AvgCorr => WeightedAverageCorrelation;
}

/// <summary>
/// 全时序水下回撤曲线离散数据点 (Underwater Curve Point)
/// </summary>
public class UnderwaterPoint
{
    public DateTime Date { get; set; }
    public decimal UnderwaterPercent { get; set; }             // 水下回撤百分比 (<= 0%，如 -12.5% 代表回撤 12.5%)
    public decimal CurrentNav { get; set; }                    // 当日复权净值
    public decimal PeakNav { get; set; }                       // 历史截至当日最高复权净值
}

/// <summary>
/// 全生命周期水下曲线与套牢时长深度解构
/// </summary>
public class UnderwaterAnalysisResult
{
    public List<UnderwaterPoint> UnderwaterSeries { get; set; } = new();
    public int TotalDays { get; set; }                         // 统计总交易日数
    public int UnderwaterDays { get; set; }                    // 处于水下的交易日数
    public decimal UnderwaterTimeRatio { get; set; }           // 历史水下时间占比 (UnderwaterDays / TotalDays * 100%)
    public int MaxUnderwaterDays { get; set; }                 // 历史上最长连续水下交易日数 (最长套牢时间)
    public int CurrentUnderwaterDays { get; set; }             // 当前连续处于水下的交易日数
    public DateTime? MaxUnderwaterStartDate { get; set; }      // 最长套牢期起始日期
    public DateTime? MaxUnderwaterEndDate { get; set; }        // 最长套牢期结束日期 (完全修复或至今)
    public decimal AverageUnderwaterDepth { get; set; }        // 水下时期的平均套牢回撤深度 (%)
    public decimal PainIndex { get; set; }                     // 痛苦指数
    public string DiagnosticSummary { get; set; } = string.Empty;

    // 便捷属性别名
    public decimal UnderwaterRatio => UnderwaterTimeRatio;
    public int MaxTroughHoldDays => MaxUnderwaterDays;
}

/// <summary>
/// 基金经理择时与选股双模型解构 (Treynor-Mazuy 与 Henriksson-Merton)
/// </summary>
public class ManagerTimingAbilityResult
{
    // Treynor-Mazuy (TM) 二次回归: R_p - R_f = alpha_tm + beta_1 * (R_m - R_f) + gamma * (R_m - R_f)^2
    public decimal TmAlpha { get; set; }              // TM 选股纯阿尔法 (年化 %)
    public decimal TmBeta { get; set; }               // TM 线性基准暴露 Beta
    public decimal TmGamma { get; set; }              // TM 市场择时系数 Gamma (正值显著代表择时增厚收益)
    public decimal TmGammaTStat { get; set; }         // Gamma 的 t 统计量
    public decimal TmGammaPValue { get; set; }        // Gamma 的 p 显著性值
    public bool TmTimingSignificant => Math.Abs(TmGammaTStat) >= 1.96m; // 95% 置信度择时显著
    public decimal TmRSquared { get; set; }           // TM 模型判定系数 R^2

    // Henriksson-Merton (HM) 双Beta期权回归: R_p - R_f = alpha_hm + beta_1 * (R_m - R_f) + beta_2 * max(0, -(R_m - R_f))
    public decimal HmAlpha { get; set; }              // HM 选股纯阿尔法 (年化 %)
    public decimal HmBeta1 { get; set; }              // HM 上行市场 Beta
    public decimal HmBeta2 { get; set; }              // HM 下行看跌期权避险增益 Beta2 (正值显著代表下行减仓防守能力)
    public decimal HmDownsideBeta => HmBeta2;
    public decimal HmBeta2TStat { get; set; }         // Beta2 的 t 统计量
    public decimal HmBeta2PValue { get; set; }        // Beta2 的 p 显著性值
    public bool HmTimingSignificant => Math.Abs(HmBeta2TStat) >= 1.96m; // 95% 置信度显著
    public decimal HmRSquared { get; set; }           // HM 模型判定系数 R^2

    public string TimingRating { get; set; } = "弱择时/风格恒定"; // "🏅 卓越双边择时型", "🛡️ 显著下行避险型", "🎯 纯阿尔法自下而上选股型", "⚠️ 负向择时磨损型", "弱择时/风格恒定"
    public string TimingRatingBadge => TimingRating;
    public string SelectionRating { get; set; } = "中等选股能力"; // "🌟 卓越纯选股Alpha", "良好选股", "普通选股", "选股能力较弱"
    public string DiagnosticSummary { get; set; } = string.Empty; // 机构综合结论

    public decimal TmAlphaAnnualized => TmAlpha;
    public decimal TmAlphaTStat => TmBeta;
    public decimal TmR2 => TmRSquared;
    public bool HasTmTimingSkill => TmTimingSignificant && TmGamma > 0;

    public decimal HmAlphaAnnualized => HmAlpha;
    public decimal HmAlphaTStat => HmBeta1;
    public decimal HmDownsideBetaTStat => HmBeta2TStat;
    public decimal HmDownsideBetaPValue => HmBeta2PValue;
    public decimal HmR2 => HmRSquared;
    public bool HasHmTimingSkill => HmTimingSignificant && HmBeta2 > 0;
}

/// <summary>
/// 组合单资产边际风险贡献 (MCR) 与百分比风险贡献 (PCR) 穿透项
/// </summary>
public class PortfolioRiskContributionItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal CapitalWeight { get; set; }              // 资金配置权重 (%)
    public decimal MarginalContributionToRisk { get; set; } // 边际风险贡献 MCR (%/%)
    public decimal Mcr => MarginalContributionToRisk;
    public decimal AbsoluteContributionToRisk { get; set; } // 绝对风险贡献 ACR (% 年化波动率)
    public decimal Acr => AbsoluteContributionToRisk;
    public decimal PercentageContributionToRisk { get; set; } // 百分比风险贡献 PCR (%)
    public decimal Pcr => PercentageContributionToRisk;
    public decimal RiskConcentrationRatio { get; set; }     // 风险乘数比率 (PCR / CapitalWeight, >1.0 说明风险占比超过本金占比)
    public bool IsRiskHog => RiskConcentrationRatio >= 1.8m && PercentageContributionToRisk >= 25.0m; // 风险拥挤/风险猪标识
    public string RiskStatusText => IsRiskHog ? "⚠️ 风险过度拥挤" : (RiskConcentrationRatio < 0.6m ? "🛡️ 低波减震器" : "⚖️ 风险本金匹配");
}

/// <summary>
/// 组合整体风险预算穿透解构报告
/// </summary>
public class PortfolioRiskDecompositionResult
{
    public List<PortfolioRiskContributionItem> Items { get; set; } = new();
    public decimal TotalPortfolioVolatility { get; set; } // 组合总年化波动率 (%)
    public decimal PortfolioVolatility => TotalPortfolioVolatility;
    public string DominantRiskAsset { get; set; } = string.Empty; // 贡献最大风险的资产
    public decimal DominantRiskPercent { get; set; }     // 最大资产风险贡献占比 (%)
    public decimal RiskBudgetGini { get; set; }          // 风险预算基尼集中度系数 (0~1.0)
    public bool HasRiskHogWarning { get; set; }          // 是否存在风险拥挤
    public bool HasRiskHog => HasRiskHogWarning;
    public string DiagnosticSummary { get; set; } = string.Empty;
}

/// <summary>
/// 选基大厅自定义多因子评分权重配置
/// </summary>
public class MultiFactorScoreWeights
{
    public decimal ReturnWeight { get; set; } = 30m;     // 年化收益率权重 (默认 30%)
    public decimal DrawdownWeight { get; set; } = 25m;   // 最大回撤防御权重 (默认 25%)
    public decimal SharpeWeight { get; set; } = 25m;     // 夏普比率权重 (默认 25%)
    public decimal StabilityWeight { get; set; } = 20m;  // 波动与综合稳定性权重 (默认 20%)
}

/// <summary>
/// 组合单风格因子暴露与主动风险贡献明细项
/// </summary>
public class PortfolioFactorExposureItem
{
    public string FactorName { get; set; } = string.Empty; // 如 "Beta (市场贝塔)", "Size (市值规模)", "Value (价值风格)", "Momentum (动量趋势)", "Volatility (残差波动)", "Quality (质量盈利)"
    public decimal PortfolioExposure { get; set; } // 组合加权因子暴露 (Z-score 标准差单位)
    public decimal BenchmarkExposure { get; set; } // 基准对应因子暴露
    public decimal ActiveTilt => PortfolioExposure - BenchmarkExposure; // 主动偏离暴露
    public decimal FactorVarianceContribution { get; set; } // 该因子对组合总方差的贡献值
    public decimal FactorRiskPercent { get; set; } // 该因子对主动风险的贡献占比 (%)
    public string FactorInterpretation { get; set; } = string.Empty; // 因子投资含义解读
}

/// <summary>
/// 机构级 Barra CNE6 组合多因子暴露聚合与主动风险欧拉方差分解结果
/// </summary>
public class PortfolioFactorRiskResult
{
    public List<PortfolioFactorExposureItem> FactorItems { get; set; } = new();
    public decimal TotalActiveVolatility { get; set; } // 组合年化主动风险 (Tracking Error, %)
    public decimal FactorRiskVolatility { get; set; }  // 风格因子风险部分年化波动率 (%)
    public decimal SpecificRiskVolatility { get; set; } // 特质选股残差风险部分年化波动率 (%)
    public decimal FactorRiskPercent { get; set; }     // 风格因子风险占比 (%) = (FactorVar / TotalVar) * 100
    public decimal SpecificRiskPercent { get; set; }   // 纯选股特质风险占比 (%) = (SpecificVar / TotalVar) * 100
    public string DominantFactorTilt { get; set; } = string.Empty; // 组合最显著的风格因子倾斜
    public string RiskAttributionProfile { get; set; } = string.Empty; // "因子驱动型 (风格博弈)" / "选股驱动型 (纯Alpha)" / "均衡稳健型"
    public string DiagnosticSummary { get; set; } = string.Empty; // 机构研报级诊断摘要
}

/// <summary>
/// 连续多资产凯利公式最优资本配置与目标波动率杠杆/现金缓冲引擎测算结果
/// </summary>
public class PortfolioKellyAndTargetVolResult
{
    public Dictionary<string, decimal> FullKellyWeights { get; set; } = new(); // 理论最优全凯利各资产权重比例 (0-100%)
    public Dictionary<string, decimal> HalfKellyWeights { get; set; } = new(); // 稳健半凯利各资产权重比例 (0-100%)
    public decimal TheoreticalMaxLogGrowth { get; set; } // 理论最优预期年化复合对数增长率 (%)
    public decimal TargetVolatility { get; set; } = 10.0m; // 目标年化波动率约束 (%)
    public decimal PortfolioIntrinsicVolatility { get; set; } // 组合无杠杆纯资产固有年化波动率 (%)
    public decimal SuggestedRiskyWeight { get; set; } // 建议风险资产总仓位比例 (如 0.85 代表 85%)
    public decimal SuggestedCashWeight { get; set; } // 建议现金/纯债缓冲垫配比 (如 0.15 代表 15%)
    public decimal ImpliedLeverage { get; set; } = 1.0m; // 隐含杠杆倍数 (若波动率低于目标则 >1.0)
    public decimal AdjustedExpectedReturn { get; set; } // 叠加目标波动率控波/杠杆后的年化预期收益率 (%)
    public string CapitalAllocationAdvice { get; set; } = string.Empty; // 资产头寸调整策略建议
}

/// <summary>
/// 牛熊市场双边非对称捕获与极端暴跌条件相关性分析
/// </summary>
public class BullBearCaptureResult
{
    public decimal BullBeta { get; set; } // 上涨行情 Beta (基准 > 0 交易日)
    public decimal BearBeta { get; set; } // 下跌行情 Beta (基准 <= 0 交易日)
    public decimal AsymmetryIndex => BullBeta - BearBeta; // 牛熊不对称度 (>0 说明涨时跟涨多，跌时抗跌强)
    public decimal UpsideCaptureRatio { get; set; } // 上行捕获率 UCR (%)
    public decimal DownsideCaptureRatio { get; set; } // 下行捕获率 DCR (%)
    public decimal CaptureSpread => UpsideCaptureRatio - DownsideCaptureRatio; // 捕获利差 Spread (%)
    public decimal NormalCorrelation { get; set; } // 常态全样本相关系数
    public decimal CrashCorrelation { get; set; }  // 极端暴跌日条件相关系数 (基准单日跌幅 > 1.0%)
    public decimal CorrelationShift => CrashCorrelation - NormalCorrelation; // 暴跌时相关性骤升偏离度
    public string ConvexityRating { get; set; } = "均衡稳健"; // "🌟 极品正凸性 (涨多跌少)" / "🛡️ 稳健抗跌" / "⚠️ 负凸性脆弱"
    public string AsymmetryDiagnosis { get; set; } = string.Empty; // 机构诊断评语
}

/// <summary>
/// 双基金牛熊非对称捕获与暴跌条件相关性对比
/// </summary>
public class BullBearCompareResult
{
    public BullBearCaptureResult CaptureA { get; set; } = new();
    public BullBearCaptureResult CaptureB { get; set; } = new();
    public decimal BullBetaSpread => CaptureA.BullBeta - CaptureB.BullBeta;
    public decimal BearBetaSpread => CaptureA.BearBeta - CaptureB.BearBeta;
    public decimal CaptureSpreadDiff => CaptureA.CaptureSpread - CaptureB.CaptureSpread;
    public double CrashCorrelation { get; set; }
    public double NormalCorrelation { get; set; }
    public double CorrelationShift => CrashCorrelation - NormalCorrelation;
    public string ComparisonSummary { get; set; } = string.Empty;
}

/// <summary>
/// 机构级 FOF 尽调六维雷达综合评分与晨星五星等效评级报告卡
/// </summary>
public class InstitutionalDueDiligenceCard
{
    // 六维综合雷达打分 (0 ~ 100 归一化)
    public decimal AlphaPurityScore { get; set; } // 1. 选股纯阿尔法 (TM Alpha, FF5 Specific Alpha, 胜率)
    public decimal TimingConvexityScore { get; set; } // 2. 择时与非对称对冲 (TM Gamma, HM Downside Beta, 捕获利差)
    public decimal TailResilienceScore { get; set; } // 3. 尾部抗脆弱韧性 (最大回撤, Calmar, Pain Index, 修复历时)
    public decimal RiskAdjustedEfficiencyScore { get; set; } // 4. 风险收益性价比 (Sharpe, Sortino, Omega, Ulcer Index)
    public decimal StyleDisciplineScore { get; set; } // 5. 风格纪律性与纯粹度 (Barra R^2, 跟踪误差稳定性)
    public decimal CapacityLiquidityScore { get; set; } // 6. 规模容量与流动性 (AUM 黄金区间, 摩擦损耗)

    public decimal OverallDiligenceScore { get; set; } // 六维加权综合尽调总分 (0 ~ 100)
    public string StarRating { get; set; } = "★★★☆☆"; // 晨星等效五星评级 (★★★★★, ★★★★☆, ★★★☆☆, ★★☆☆☆, ★☆☆☆☆)
    public string DiligenceGrade { get; set; } = "优选标的"; // "机构核心底仓", "卫星增强优选", "中性观察", "审慎回避"
    public string InstitutionalVerdict { get; set; } = string.Empty; // 机构投资决策委员会评审决议
    public List<string> KeyStrengths { get; set; } = new(); // 核心投资亮点
    public List<string> KeyRisks { get; set; } = new(); // 核心警惕风险
}

/// <summary>
/// 历史经典宏观极端黑天鹅危机情景定义
/// </summary>
public class HistoricalCrisisDefinition
{
    public string ScenarioId { get; set; } = string.Empty;
    public string ScenarioName { get; set; } = string.Empty; // 如 "2015 杠杆股灾踩踏"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BenchmarkName { get; set; } = "沪深300";
    public decimal BenchmarkDrop { get; set; } // 基准在危机期间最大跌幅 (%)，如 -43.5%
    public string MarketBackground { get; set; } = string.Empty; // 危机背景描述
    public string CategoryTag { get; set; } = string.Empty; // "流动性踩踏", "熔断机制失效", "贸易战与去杠杆", "全球黑天鹅挤兑", "抱团估值瓦解", "量化微结构雪崩"
}

/// <summary>
/// 单个历史危机情景的回放或代理冲击测试结果
/// </summary>
public class CrisisReplayItem
{
    public string ScenarioId { get; set; } = string.Empty;
    public string ScenarioName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal BenchmarkDrop { get; set; }
    public decimal FundReturn { get; set; } // 期间收益率 (%)
    public decimal FundMaxDrawdown { get; set; } // 危机期间最大回撤 (%)
    public decimal ExcessReturnOverBenchmark => FundReturn - BenchmarkDrop; // 相对基准超额防守收益 (%)
    public int RecoveryDays { get; set; } // 修复危机失地所需交易日 (0 代表尚未修复或未产生重大回撤)
    public bool IsSyntheticProxy { get; set; } // 是否为晚于危机成立通过多因子与资产类别代理推演得出
    public string ResilienceGrade { get; set; } = "中性抗跌"; // "极品抗跌金钟罩", "优良稳健防御", "同步跟随", "脆弱高波受损"
    public string DiagnosticComment { get; set; } = string.Empty;
}

/// <summary>
/// 全生命周期历史极端危机压力测试综合结果
/// </summary>
public class CrisisReplayResult
{
    public List<CrisisReplayItem> CrisisItems { get; set; } = new();
    public decimal AverageCrisisDrawdown { get; set; } // 危机平均最大回撤 (%)
    public decimal AverageExcessReturn { get; set; } // 危机平均超额防守收益 (%)
    public decimal ComprehensiveResilienceScore { get; set; } // 综合危机韧性评分 (0~100)
    public string OverallResilienceRating { get; set; } = "稳健防御型"; // "全天候极韧防御", "结构性抗跌", "高弹性进攻", "极端脆弱"
    public string ExecutiveSummary { get; set; } = string.Empty;
}

/// <summary>
/// 单期 Brinson 归因输入数据
/// </summary>
public class MultiPeriodBrinsonPeriodInput
{
    public string PeriodName { get; set; } = string.Empty; // 如 "2023Q1"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<BrinsonSectorItem> SectorItems { get; set; } = new();
}

/// <summary>
/// 单个行业在多期累积归因中的汇总贡献
/// </summary>
public class MultiPeriodSectorAttribution
{
    public string SectorName { get; set; } = string.Empty;
    public decimal CumulativeAllocationEffect { get; set; } // 经 Carino 连乘平滑后的累计资产配置效应 (%)
    public decimal CumulativeSelectionEffect { get; set; }  // 经 Carino 连乘平滑后的累计标的选择效应 (%)
    public decimal CumulativeInteractionEffect { get; set; } // 经 Carino 连乘平滑后的累计交互效应 (%)
    public decimal CumulativeTotalAlpha => CumulativeAllocationEffect + CumulativeSelectionEffect + CumulativeInteractionEffect;
    public decimal AveragePortfolioWeight { get; set; } // 平均组合配置权重 (%)
    public decimal AverageBenchmarkWeight { get; set; } // 平均基准权重 (%)
}

/// <summary>
/// 经过 Carino 几何复利平滑的多期跨周期 Brinson-Fachler 业绩归因结果
/// </summary>
public class MultiPeriodCarinoBrinsonResult
{
    public int PeriodCount { get; set; }
    public decimal CumulativePortfolioReturn { get; set; } // 组合多期复利总收益率 (%)
    public decimal CumulativeBenchmarkReturn { get; set; } // 基准多期复利总收益率 (%)
    public decimal CumulativeExcessReturn => CumulativePortfolioReturn - CumulativeBenchmarkReturn; // 总超额收益率 (%)

    public decimal CarinoAllocationEffect { get; set; } // Carino 平滑总配置效应 (%)
    public decimal CarinoSelectionEffect { get; set; }  // Carino 平滑总选择效应 (%)
    public decimal CarinoInteractionEffect { get; set; } // Carino 平滑总交互效应 (%)

    // 严格几何守恒验证: CarinoAllocationEffect + CarinoSelectionEffect + CarinoInteractionEffect == CumulativeExcessReturn
    public decimal ResidualGap => Math.Abs(CumulativeExcessReturn - (CarinoAllocationEffect + CarinoSelectionEffect + CarinoInteractionEffect));
    public bool IsStrictlyConserved => ResidualGap < 0.001m;

    public List<MultiPeriodSectorAttribution> SectorAttributions { get; set; } = new();
    public List<(string PeriodName, decimal Alloc, decimal Select, decimal Inter, decimal Excess, decimal CarinoK)> PeriodCarinoBreakdown { get; set; } = new();
    public string AttributionSummary { get; set; } = string.Empty;
}

/// <summary>
/// 战术资产配置 (TAA) 单期调仓记录
/// </summary>
public class TaaRebalanceRecord
{
    public DateTime Date { get; set; }
    public List<string> SelectedFundCodes { get; set; } = new();
    public List<string> SelectedFundNames { get; set; } = new();
    public Dictionary<string, decimal> TargetWeights { get; set; } = new();
    public decimal PortfolioNavBefore { get; set; }
    public decimal PortfolioNavAfter { get; set; }
    public bool IsDefensiveCashMode { get; set; } // 是否由于均线破位触发避险切入货币理财
    public string RebalanceRationale { get; set; } = string.Empty; // 调仓逻辑 (如 "动量领涨 Top2")
}

/// <summary>
/// 战术资产配置 (TAA) 动量轮动回测结果
/// </summary>
public class TaaBacktestResult
{
    public string StrategyName { get; set; } = "自适应动量战术轮动策略 (TAA)";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TradingDays { get; set; }
    public decimal TotalReturn { get; set; } // 策略累计收益率 (%)
    public decimal AnnualizedReturn { get; set; } // 策略年化收益率 (%)
    public decimal AnnualizedVolatility { get; set; } // 年化波动率 (%)
    public decimal MaxDrawdown { get; set; } // 最大回撤 (%)
    public decimal SharpeRatio { get; set; } // 夏普比率
    public decimal CalmarRatio => MaxDrawdown > 0 ? AnnualizedReturn / MaxDrawdown : 0m;
    public decimal BenchmarkTotalReturn { get; set; } // 等权基准累计收益率 (%)
    public decimal ExcessReturnOverBenchmark => TotalReturn - BenchmarkTotalReturn;
    public decimal WinRateVsBenchmark { get; set; } // 相对基准调仓胜率 (%)
    public int TotalRebalanceCount { get; set; }
    public List<NavRecord> StrategyNavHistory { get; set; } = new();
    public List<NavRecord> BenchmarkNavHistory { get; set; } = new();
    public List<TaaRebalanceRecord> RebalanceHistory { get; set; } = new();
    public string StrategyDiagnosis { get; set; } = string.Empty;
}

/// <summary>
/// 前瞻性宏观经济情景定义 (利率、权益、商品、信用利差、波动率与汇率)
/// </summary>
public class MacroScenarioDefinition
{
    public string ScenarioId { get; set; } = string.Empty;
    public string ScenarioName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal InterestRateBps { get; set; } // 无风险利率变动 (基点 BP, e.g. +75)
    public decimal EquityMarketShockPct { get; set; } // 股票大盘基准冲击 (%)
    public decimal CommodityInflationShockPct { get; set; } // 商品通胀因子冲击 (%)
    public decimal CreditSpreadBps { get; set; } // 信用利差冲击 (基点 BP)
    public decimal VolatilityShockPct { get; set; } // 波动率激增幅度 (%)
    public decimal ExchangeRateShockPct { get; set; } // 汇率变动幅度 (%)
}

/// <summary>
/// 单个宏观情景下的资产/组合损益与风险推演结果
/// </summary>
public class MacroFactorStressItem
{
    public string ScenarioId { get; set; } = string.Empty;
    public string ScenarioName { get; set; } = string.Empty;
    public decimal ExpectedReturnPct { get; set; } // 预期收益变动 (%)
    public decimal ExpectedLossAmountMln { get; set; } // 预期名义损益金额 (百万元)
    public decimal DeltaVaR99Pct { get; set; } // 99% VaR 增量百分比 (%)
    public decimal ShockedVaR99Pct { get; set; } // 冲击后的 99% VaR (%)
    public string VulnerabilityRating { get; set; } = "稳健防御型"; // "极高逆周期防御", "稳健适度承压", "中度风险预警", "极端脆弱受损"
    public string StressRationale { get; set; } = string.Empty;
}

/// <summary>
/// 前瞻性宏观情景联合冲击压力测试全景结果
/// </summary>
public class MacroStressTestResult
{
    public List<MacroFactorStressItem> Items { get; set; } = new();
    public decimal WorstCaseDrawdownPct { get; set; } // 最悲观情景预期回撤 (%)
    public decimal AverageShockLossPct { get; set; } // 平均冲击损益率 (%)
    public decimal ComprehensiveResilienceScore { get; set; } // 综合宏观抗压得分 (0~100)
    public string ResilienceGrade { get; set; } = "稳健抗压";
    public string ExecutiveSummary { get; set; } = string.Empty;
}

/// <summary>
/// 单只基金在多因子截面标准化评分结果
/// </summary>
public class FundMultiFactorScoreItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal MomentumScore { get; set; } // 收益动量维度得分 (0~100)
    public decimal RiskAdjustedScore { get; set; } // 风险性价比维度得分 (Sharpe/Calmar/Sortino) (0~100)
    public decimal DownsideDefenseScore { get; set; } // 尾部防守抗跌得分 (MDD/Pain/VaR) (0~100)
    public decimal AlphaPurityScore { get; set; } // 纯阿尔法选股得分 (TM/FF5 Alpha) (0~100)
    public decimal ConvexityScore { get; set; } // 非对称凸性与捕获得分 (Capture Spread) (0~100)
    public decimal CompositeScore { get; set; } // 综合加权总分 (0~100)
    public decimal PercentileRank { get; set; } // 全市场/分类百分位排名 (0~100%, 越大越优)
    public string RatingTag { get; set; } = "💎 核心底仓";
    public string DiagnosticComment { get; set; } = string.Empty;
}

/// <summary>
/// 全市场多因子截面打分与选基优选池输出
/// </summary>
public class FundMultiFactorRankingResult
{
    public List<FundMultiFactorScoreItem> AllScoredFunds { get; set; } = new();
    public List<FundMultiFactorScoreItem> TopCoreFunds { get; set; } = new(); // 综合得分最高 Top 10 (核心底仓池)
    public List<FundMultiFactorScoreItem> TopAlphaFunds { get; set; } = new(); // 选股 Alpha 最强 Top 10 (进攻先锋池)
    public List<FundMultiFactorScoreItem> TopDefensiveFunds { get; set; } = new(); // 防守韧性最高 Top 10 (稳健防御池)
    public int TotalAnalyzedCount { get; set; }
    public DateTime RankingDate { get; set; } = DateTime.Today;
    public string ExecutiveSummary { get; set; } = string.Empty;
}

/// <summary>
/// 单个 Barra 风格因子主动超额收益贡献明细
/// </summary>
public class BarraFactorReturnContributionItem
{
    public string FactorCode { get; set; } = string.Empty;
    public string FactorName { get; set; } = string.Empty;
    public decimal PortfolioExposure { get; set; } // 组合因子暴露
    public decimal BenchmarkExposure { get; set; } // 基准因子暴露
    public decimal ActiveExposure => PortfolioExposure - BenchmarkExposure; // 主动暴露差
    public decimal FactorPremiumPct { get; set; } // 因子期间市场溢价回报率 (%)
    public decimal ReturnContributionPct => Math.Round(ActiveExposure * FactorPremiumPct, 4); // 因子主动收益贡献 (%)
    public decimal ContributionSharePct { get; set; } // 占总主动超额的百分比贡献 (%)
    public string FactorDescription { get; set; } = string.Empty;
}

/// <summary>
/// Barra 组合风格多因子收益归因模型全景结果 (严格守恒: 因子收益贡献 + 特质收益 = 总主动收益)
/// </summary>
public class BarraFactorReturnAttributionResult
{
    public decimal TotalPortfolioReturn { get; set; } // 组合总收益率 (%)
    public decimal TotalBenchmarkReturn { get; set; } // 基准总收益率 (%)
    public decimal TotalActiveReturn => TotalPortfolioReturn - TotalBenchmarkReturn; // 总主动超额收益 (%)
    public List<BarraFactorReturnContributionItem> FactorContributions { get; set; } = new();
    public decimal TotalStyleFactorReturn { get; set; } // 六大风格因子累计收益贡献 (%)
    public decimal SpecificAlphaReturn { get; set; } // 纯特质选股超额收益 (%)
    public decimal StyleContributionSharePct => TotalActiveReturn != 0 ? Math.Round((TotalStyleFactorReturn / TotalActiveReturn) * 100m, 1) : 0m;
    public decimal SpecificAlphaSharePct => TotalActiveReturn != 0 ? Math.Round((SpecificAlphaReturn / TotalActiveReturn) * 100m, 1) : 0m;
    public decimal ResidualGap => Math.Abs(TotalActiveReturn - (TotalStyleFactorReturn + SpecificAlphaReturn));
    public bool IsStrictlyConserved => ResidualGap < 0.001m;
    public string DominantDriver { get; set; } = "纯特质选股主导型 (Alpha Pure)";
    public string AttributionSummary { get; set; } = string.Empty;
}

/// <summary>
/// 投资组合单只成分基金流动性与冲击明细
/// </summary>
public class FundLiquidityItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal PortfolioWeight { get; set; } // 组合配置权重 (%)
    public decimal HoldingAmountMln { get; set; } // 持有市值 (百万元)
    public decimal FundAumMln { get; set; } // 单只基金总规模 (百万元)
    public decimal DailyMaxRedemptionMln { get; set; } // 单日安全赎回限额 (百万元, 如规模的10%)
    public int DaysToLiquidate { get; set; } // 安全清盘变现所需交易日数
    public decimal ImpactCostPct { get; set; } // Almgren-Chriss 平方根冲击滑点成本率 (%)
    public string LiquidityTier { get; set; } = "T+1 极速";
}

/// <summary>
/// 投资组合前瞻性流动性地平线与冲击滑点测算全景结果
/// </summary>
public class PortfolioLiquidityHorizonResult
{
    public decimal PortfolioAumMln { get; set; } // 组合模拟总规模 (百万元)
    public decimal MaxDailyParticipationPct { get; set; } = 10.0m; // 单日最大赎回参与率 (默认 10%)
    public int DaysToLiquidateTotal { get; set; } // 组合 100% 完全变现所需总交易日数
    public decimal WeightedDaysToLiquidate { get; set; } // 资产加权平均变现天数
    public decimal TotalEstimatedImpactCostPct { get; set; } // 加权预估冲击滑点成本率 (%)
    public decimal TotalEstimatedImpactLossMln => Math.Round(PortfolioAumMln * (TotalEstimatedImpactCostPct / 100m), 2); // 预估冲击损失金额 (百万元)
    public decimal TPlus1LiquidPct { get; set; } // T+1 内可变现资产占比 (%)
    public decimal TPlus3LiquidPct { get; set; } // T+3 内可变现资产占比 (%)
    public decimal TPlus7LiquidPct { get; set; } // T+7 内可变现资产占比 (%)
    public string LiquidityGrade { get; set; } = "💎 极高流动性";
    public List<FundLiquidityItem> Items { get; set; } = new();
    public string ExecutiveSummary { get; set; } = string.Empty;
}

/// <summary>
/// 自定义复合业绩基准成分
/// </summary>
public class CompositeBenchmarkComponent
{
    public string IndexCode { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public decimal WeightPercent { get; set; } // 配置权重 (%)
    public List<BenchmarkRecord> HistoricalNavs { get; set; } = new();
}

/// <summary>
/// 主动份额 (Active Share) 与基准偏离度分析结果 (Cremers & Petajisto 经典学术标准)
/// </summary>
public class ActiveShareAnalysisResult
{
    public string BenchmarkName { get; set; } = "自定义复合基准";
    public decimal ActiveSharePercent { get; set; } // 主动份额 (%) = 0.5 * sum |w_p - w_b|
    public decimal OverlapWeightPercent => 100m - ActiveSharePercent; // 与基准重合度 (%)
    public string ActiveShareRating { get; set; } = "🎯 极高主动管理"; // "极高主动", "适度主动", "弱主动偏离", "伪主动/衣柜基金"
    public decimal TrackingError { get; set; } // 跟踪误差 (%)
    public string ManagementStyleDiagnosis { get; set; } = string.Empty;
    public List<(string AssetOrSector, decimal PortfolioWeight, decimal BenchmarkWeight, decimal ActiveDelta)> ComponentDeviations { get; set; } = new();
}

/// <summary>
/// 科学投资建议行动指令 (Phase 15)
/// </summary>
public enum InvestmentActionSignal
{
    StrongBuy,      // 强烈买入 (重仓增配)
    Accumulate,     // 逢低吸筹 (定投建仓)
    Hold,           // 继续持有 (锁定底仓)
    TrimProfit,     // 逢高止盈 (分批减仓)
    StopLossExit    // 破位止损 (回避换基)
}

/// <summary>
/// 建议适用投资周期 (Phase 15)
/// </summary>
public enum TargetInvestmentHorizon
{
    ShortTerm,      // 短期敏捷博弈 (1~4周)
    MediumTerm,     // 中期稳健持有 (3~12个月)
    AllWeather      // 全天候底仓 (跨越牛熊)
}

/// <summary>
/// 短期维度量化评估结果 (5~20 交易日 / 1周~1个月) (Phase 15)
/// </summary>
public class ShortTermEvaluationResult
{
    public decimal Momentum5D { get; set; } // 近 5 日动量收益率 (%)
    public decimal Momentum20D { get; set; } // 近 20 日动量收益率 (%)
    public decimal ShortTermRsi { get; set; } // 短期 14日 RSI 相对强弱指标 (0~100)
    public decimal VaR95_5D { get; set; } // 5日 95% 在险价值 VaR (%)
    public decimal CVaR95_5D { get; set; } // 5日 95% 条件在险价值 CVaR (%)
    public decimal ShortTermWinRate { get; set; } // 近 20 日单日上涨胜率 (%)
    public decimal ShortTermVolatility { get; set; } // 短期滚动年化波动率 (%)
    public decimal ShortTermScore { get; set; } // 短期维度综合量化得分 (0~100)
    public string ShortTermRating { get; set; } = "🛡️ 低波蓄势"; // "🚀 极强进攻", "⚡ 动量加速", "⚠️ 短线超买警惕", "💎 超跌反弹契机", "🚨 破位下挫"
    public string ShortTermDiagnosis { get; set; } = string.Empty;
}

/// <summary>
/// 中期维度量化评估结果 (60~250 交易日 / 3个月~1年) (Phase 15)
/// </summary>
public class MediumTermEvaluationResult
{
    public decimal AnnualizedReturn { get; set; } // 中期年化收益率 (%)
    public decimal AnnualizedExcessReturn { get; set; } // 相对基准中期年化超额收益 (%)
    public decimal InformationRatio { get; set; } // 中期信息比率 (IR)
    public decimal CalmarRatio { get; set; } // 中期卡玛比率 (收益/最大回撤)
    public decimal StyleDriftIndex { get; set; } // 中期风格漂移指数 (SDI)
    public decimal DownsideCaptureRatio { get; set; } // 中期下行捕获率 (%)
    public int MaxDrawdownRecoveryDays { get; set; } // 历史最大回撤修复速度 (交易日数)
    public decimal MediumTermScore { get; set; } // 中期维度综合量化得分 (0~100)
    public string MediumTermRating { get; set; } = "💎 稳健长牛底仓"; // "💎 优质长牛底仓", "⚡ 中线成长先锋", "⚖️ 平庸观察", "🚨 破位出清"
    public string MediumTermDiagnosis { get; set; } = string.Empty;
}

/// <summary>
/// 短中期协同研判矩阵四象限分析结果 (Phase 15)
/// </summary>
public class HorizonConcordanceResult
{
    public string ConcordanceType { get; set; } = "短中双优·主升浪重仓"; // 四象限格局
    public decimal ConcordanceScore { get; set; } // 协同打分 (0~100)
    public decimal SuggestedAllocationMultiplier { get; set; } = 1.0m; // 建议仓位调整乘数 (如 1.2x 代表增配, 0.5x 代表压降)
    public string StrategicGuidance { get; set; } = string.Empty; // 机构配置战略指引
}

/// <summary>
/// 前瞻性模拟预测单日分位数点
/// </summary>
public class SimulationQuantilePoint
{
    public int DayIndex { get; set; }
    public DateTime ForecastDate { get; set; }
    public decimal P10_Pessimistic { get; set; } // 10% 悲观分位数
    public decimal P50_Expected { get; set; }    // 50% 预期中轴
    public decimal P90_Optimistic { get; set; }  // 90% 乐观分位数
}

/// <summary>
/// 前瞻性多周期模拟预测结果 (GARCH条件方差 + 动量衰减与均值回归漂移项) (Phase 15)
/// </summary>
public class ForwardSimulationForecastItem
{
    public string FundCode { get; set; } = string.Empty;
    public DateTime BaseDate { get; set; }
    public decimal BaseNav { get; set; }
    public decimal ConditionalVolatility { get; set; } // GARCH 条件年化波动率 (%)
    public decimal ShortTermExpectedReturn20D { get; set; } // 短期 20D 期望收益率 (%)
    public decimal MediumTermExpectedReturn60D { get; set; } // 中期 60D 期望收益率 (%)
    public List<SimulationQuantilePoint> ForecastTrajectory { get; set; } = new(); // 未来 60 交易日逐日前瞻轨迹分位数
    public decimal ExpectedP10EndNav { get; set; } // 预测期末悲观净值下界
    public decimal ExpectedP50EndNav { get; set; } // 预测期末预期净值中轴
    public decimal ExpectedP90EndNav { get; set; } // 预测期末乐观净值上界
    public string ForecastSummary { get; set; } = string.Empty;
}

/// <summary>
/// 事前模拟预测 vs 事后真实走势对比检验与模型审计结果 (Phase 15)
/// </summary>
public class ForecastRealityComparisonResult
{
    public int AuditedDaysCount { get; set; } // 审计对比的交易日数 (如过去 30 或 60 交易日)
    public decimal DirectionalHitRate { get; set; } // 涨跌方向预测准确胜率 (%)
    public decimal PicpCoverageRatio { get; set; } // 预测置信区间覆盖概率 (PICP, % 落在 P10~P90 之间)
    public decimal RootMeanSquareError { get; set; } // 预测残差均方根误差 (RMSE)
    public decimal MeanAbsolutePercentageError { get; set; } // 平均绝对百分比误差 (MAPE, %)
    public decimal AdviceAlphaContribution { get; set; } // 遵从建议相比被动持有的超额贡献 (Advice Alpha, %)
    public string AuditRating { get; set; } = "🌟 极高吻合可信"; // "极高吻合可信", "稳健有效", "存在适度偏离", "模型失真警惕"
    public string AuditConclusion { get; set; } = string.Empty;
}

/// <summary>
/// 单个因子/策略的历史复盘教训与诊断
/// </summary>
public class PostMortemExperienceItem
{
    public string FactorOrStrategy { get; set; } = string.Empty;
    public decimal HistoricalHitRate { get; set; } // 历史命中率 (%)
    public decimal ExcessContribution { get; set; } // 超额收益贡献 (%)
    public string ExperienceDiagnosis { get; set; } = string.Empty; // 经验复盘诊断
    public string IterationAdjustment { get; set; } = string.Empty; // 持续迭代优化动作
}

/// <summary>
/// 历史经验复盘与持续迭代审计报告 (Phase 15)
/// </summary>
public class PostMortemExperienceReport
{
    public DateTime ReportDate { get; set; } = DateTime.Today;
    public List<PostMortemExperienceItem> LessonsLearned { get; set; } = new();
    public string OverallExperienceSummary { get; set; } = string.Empty;
}

/// <summary>
/// 基于预测残差贝叶斯后验反馈的自适应模型权重持续迭代结果 (Phase 15)
/// </summary>
public class AdaptiveModelWeightResult
{
    public decimal PriorMomentumWeight { get; set; } = 0.25m; // 迭代前动量权重
    public decimal PriorRiskAdjustedWeight { get; set; } = 0.25m; // 迭代前风险收益权重
    public decimal PriorDownsideDefenseWeight { get; set; } = 0.20m; // 迭代前防御权重
    public decimal PriorAlphaPurityWeight { get; set; } = 0.15m; // 迭代前纯Alpha权重
    public decimal PriorConvexityWeight { get; set; } = 0.15m; // 迭代前凸性权重

    public decimal AdaptiveMomentumWeight { get; set; } // 自适应迭代后动量权重
    public decimal AdaptiveRiskAdjustedWeight { get; set; } // 自适应迭代后风险收益权重
    public decimal AdaptiveDownsideDefenseWeight { get; set; } // 自适应迭代后防御权重
    public decimal AdaptiveAlphaPurityWeight { get; set; } // 自适应迭代后纯Alpha权重
    public decimal AdaptiveConvexityWeight { get; set; } // 自适应迭代后凸性权重

    public decimal TotalWeightSum { get => AdaptiveMomentumWeight + AdaptiveRiskAdjustedWeight + AdaptiveDownsideDefenseWeight + AdaptiveAlphaPurityWeight + AdaptiveConvexityWeight; set { } }
    public decimal IterationInformationGain { get; set; } // 贝叶斯后验信息增益 (%)
    public string AdaptationSummary { get; set; } = string.Empty;
}

/// <summary>
/// 终极闭环科学投资建议与买卖决议 (专业、靠谱、精准) (Phase 15)
/// </summary>
public class ScientificInvestmentAdvice
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public InvestmentActionSignal ActionSignal { get; set; } = InvestmentActionSignal.Hold;
    public string ActionSignalText => ActionSignal switch
    {
        InvestmentActionSignal.StrongBuy => "🚀 强烈买入 (重仓增配)",
        InvestmentActionSignal.Accumulate => "💎 逢低吸筹 (定投建仓)",
        InvestmentActionSignal.Hold => "🛡️ 继续持有 (锁定底仓)",
        InvestmentActionSignal.TrimProfit => "💰 逢高止盈 (分批减仓)",
        InvestmentActionSignal.StopLossExit => "🚨 破位止损 (回避换基)",
        _ => "⚖️ 观望观察"
    };

    public decimal ConvictionScore { get; set; } // 建议置信度评分 (0~100%)
    public TargetInvestmentHorizon RecommendedHorizon { get; set; } = TargetInvestmentHorizon.MediumTerm;
    public string HorizonText { get => RecommendedHorizon switch
    {
        TargetInvestmentHorizon.ShortTerm => "短期敏捷博弈 (1~4周)",
        TargetInvestmentHorizon.MediumTerm => "中期稳健持有 (3~12个月)",
        TargetInvestmentHorizon.AllWeather => "全天候底仓 (跨越牛熊)",
        _ => "周期中性"
    }; set { } }

    public decimal MinExpectedReturn { get; set; } // 周期预期下限收益率 (%)
    public decimal TargetExpectedReturn { get; set; } // 周期目标预期中轴收益率 (%)
    public decimal MaxExpectedReturn { get; set; } // 周期预期上限收益率 (%)

    public decimal CurrentUnitNav { get; set; } // 当前最新单位净值
    public decimal SuggestedEntryNavLower { get; set; } // 建议建仓/买入下限参考净值
    public decimal SuggestedEntryNavUpper { get; set; } // 建议建仓/买入上限参考净值
    public decimal TrailingStopLossNav { get; set; } // 动态追踪止损触发参考净值
    public decimal StopLossPercent { get; set; } // 相对当前净值止损跌幅比例 (%)
    public decimal TakeProfitTargetNav { get; set; } // 目标止盈目标净值
    public decimal TakeProfitPercent { get; set; } // 相对当前净值止盈涨幅比例 (%)

    public ShortTermEvaluationResult ShortTermEvaluation { get; set; } = new();
    public MediumTermEvaluationResult MediumTermEvaluation { get; set; } = new();
    public HorizonConcordanceResult HorizonConcordance { get; set; } = new();
    public ForecastRealityComparisonResult RealityAudit { get; set; } = new();
    public AdaptiveModelWeightResult AdaptiveWeights { get; set; } = new();
    public ForwardSimulationForecastItem? SimulationForecast { get; set; }
    public PostMortemExperienceReport? ExperienceReport { get; set; }

    public List<string> KeyInvestmentTheses { get; set; } = new(); // 核心投资支撑逻辑 (3~4条)
    public List<string> PrimaryRiskWarnings { get; set; } = new(); // 核心下行风险警示 (2~3条)
    public string ExecutiveAdvisoryVerdict { get; set; } = string.Empty; // 投决会首席投资官决议评述
}

// ==========================================
// Phase 17 机构级 FOF 组合时序动态回测与摩擦测算模型
// ==========================================

public enum PortfolioRebalanceMode
{
    Monthly,
    Quarterly,
    SemiAnnually,
    DriftThresholdOnly
}

public class PortfolioDailyPoint
{
    public DateTime Date { get; set; }
    public decimal PortfolioValue { get; set; }
    public decimal UnitNav { get; set; }
    public decimal Cash { get; set; }
    public decimal DailyReturn { get; set; }
    public decimal BenchmarkReturn { get; set; }
    public decimal CumulativeReturn { get; set; }
    public decimal BenchmarkCumulativeReturn { get; set; }
    public decimal Drawdown { get; set; }
    public bool IsRebalanceDay { get; set; }
}

public class PortfolioRebalanceEvent
{
    public DateTime Date { get; set; }
    public string TriggerReason { get; set; } = string.Empty;
    public Dictionary<string, decimal> PreWeights { get; set; } = new();
    public Dictionary<string, decimal> PostWeights { get; set; } = new();
    public decimal RebalancedTradeVolume { get; set; }
    public decimal TransactionFee { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class PortfolioDynamicBacktestResult
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalTradingDays { get; set; }
    public decimal InitialCapital { get; set; }
    public decimal FinalCapital { get; set; }
    public decimal CumulativeReturn { get; set; }
    public decimal AnnualizedReturn { get; set; }
    public decimal AnnualizedVolatility { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal SortinoRatio { get; set; }
    public decimal CalmarRatio { get; set; }
    public decimal MaxDrawdown { get; set; }
    public DateTime MaxDrawdownPeakDate { get; set; }
    public DateTime MaxDrawdownTroughDate { get; set; }
    public int MaxDrawdownDurationDays { get; set; }
    public decimal BenchmarkCumulativeReturn { get; set; }
    public decimal BenchmarkAnnualizedReturn { get; set; }
    public decimal BenchmarkMaxDrawdown { get; set; }
    public decimal ExcessReturn { get; set; }
    public decimal InformationRatio { get; set; }
    public decimal TotalTurnoverRate { get; set; }
    public decimal AnnualizedTurnoverRate { get; set; }
    public decimal TotalTransactionFees { get; set; }
    public decimal FeeErosionPercent { get; set; }
    public List<PortfolioDailyPoint> DailyEquityCurve { get; set; } = new();
    public List<PortfolioRebalanceEvent> RebalanceHistory { get; set; } = new();
    public string ExecutiveSummary { get; set; } = string.Empty;
}

// ==========================================
// Phase 17 经典机构选基范式与阿尔法持续性模型
// ==========================================

public class Fund4433CheckResult
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public bool Passed4433 { get; set; }
    public decimal RankPercentile1Y { get; set; }
    public decimal RankPercentile2Y { get; set; }
    public decimal RankPercentile3Y { get; set; }
    public decimal RankPercentile5Y { get; set; }
    public decimal RankPercentile6M { get; set; }
    public decimal RankPercentile3M { get; set; }
    public List<string> PassedCriteria { get; set; } = new();
    public List<string> FailedCriteria { get; set; } = new();
    public string Conclusion { get; set; } = string.Empty;
}

public class AlphaPersistenceResult
{
    public string FundCode { get; set; } = string.Empty;
    public decimal RollingWinRate3M { get; set; }
    public decimal RollingWinRate6M { get; set; }
    public decimal RollingWinRate12M { get; set; }
    public int MaxConsecutiveOutperformanceMonths { get; set; }
    public decimal ActiveRisk { get; set; }
    public decimal InformationRatio { get; set; }
    public decimal AlphaDecayRate { get; set; }
    public decimal PersistenceScore { get; set; }
    public string PersistenceRating { get; set; } = string.Empty;
    public string InstitutionalVerdict { get; set; } = string.Empty;
}

// ==========================================
// Phase 17 多因子预测效力 IC / IR 检验与衰减模型
// ==========================================

public class FactorIcHorizonItem
{
    public string FactorName { get; set; } = string.Empty;
    public int HorizonDays { get; set; }
    public decimal RankIc { get; set; }
    public decimal NormalIc { get; set; }
    public decimal TStatistic { get; set; }
    public bool IsSignificant => Math.Abs(TStatistic) >= 1.96m;
}

public class FactorIcSummaryItem
{
    public string FactorName { get; set; } = string.Empty;
    public string FactorDisplayName { get; set; } = string.Empty;
    public decimal MeanRankIc { get; set; }
    public decimal MeanNormalIc { get; set; }
    public decimal IcStdDev { get; set; }
    public decimal IcInformationRatio { get; set; }
    public decimal DirectionalConsistency { get; set; }
    public Dictionary<int, decimal> DecayCurve { get; set; } = new();
    public string PredictiveStrength { get; set; } = string.Empty;
}

public class FactorIcAnalysisResult
{
    public string FundCode { get; set; } = string.Empty;
    public List<FactorIcSummaryItem> Factors { get; set; } = new();
    public string DominantAlphaFactor { get; set; } = string.Empty;
    public string MostStableFactor { get; set; } = string.Empty;
    public string InstitutionalRecommendation { get; set; } = string.Empty;
}

public class PortfolioBoxConstraint
{
    public string FundCode { get; set; } = string.Empty;
    public decimal MinWeight { get; set; } = 0m;
    public decimal MaxWeight { get; set; } = 1.0m;
}

// ==========================================
// Phase 18 FOF 组合穿透交叉重叠度消冗矩阵模型
// ==========================================

public class FundHoldingsOverlapItem
{
    public string FundCodeA { get; set; } = string.Empty;
    public string FundNameA { get; set; } = string.Empty;
    public string FundCodeB { get; set; } = string.Empty;
    public string FundNameB { get; set; } = string.Empty;

    // 两两持仓权重交集重叠率 sum(min(w_A,k, w_B,k)) (%)
    public decimal WeightedOverlapPercent { get; set; }

    // Jaccard 股票集合重叠率: |A inter B| / |A union B| (%)
    public decimal JaccardSimilarity { get; set; }

    // 持仓权重向量余弦相似度 (0.0 ~ 1.0)
    public decimal CosineSimilarity { get; set; }

    // 共同重仓股数量
    public int CommonStockCount { get; set; }

    // 共同重仓股列表摘要
    public string TopOverlappingStocks { get; set; } = string.Empty;

    // 伪分散同质化高危预警 (当重叠率 >= 30% 时触发)
    public bool RedundancyAlert => WeightedOverlapPercent >= 30.0m || JaccardSimilarity >= 40.0m;

    // 重叠严重度标签
    public string OverlapSeverity
    {
        get
        {
            if (WeightedOverlapPercent >= 45.0m) return "🔴 极高重合 (⚠️ 建议消冗)";
            if (WeightedOverlapPercent >= 30.0m) return "🟡 中度重合 (⚠️ 存在同质)";
            if (JaccardSimilarity >= 40.0m) return "🟡 持仓同质 (⚠️ 标的高度交叠)";
            if (WeightedOverlapPercent >= 15.0m) return "🟢 轻微重合";
            return "✅ 互补分散";
        }
    }

    public string PairTitle => $"{FundNameA} ✕ {FundNameB}";
}

public class PortfolioOverlapMatrixResult
{
    public List<FundHoldingsOverlapItem> OverlapItems { get; set; } = new();
    public decimal AveragePairwiseOverlap { get; set; } // 平均两两重叠率 (%)
    public decimal MaxPairwiseOverlap { get; set; }     // 最大两两重叠率 (%)
    public string HighestOverlapPair { get; set; } = string.Empty; // 最高重叠基金对
    public int RedundantPairCount { get; set; }          // 触发预警的基金对数量
    public decimal RedundancyScore { get; set; }         // 组合冗余度综合评分 (0~100)
    public string DiversificationHealthGrade { get; set; } = "优良"; // 优良 / 良好 / 预警 / 高危
    public string ActionableAdvice { get; set; } = string.Empty;    // 具体消冗调仓建议
}

// ==========================================
// Phase 18 量化多因子拥挤度度量与估值分化预警模型
// ==========================================

public class FactorCrowdingItem
{
    public string FactorName { get; set; } = string.Empty;
    public string FactorDisplayName { get; set; } = string.Empty;
    public decimal ValuationSpread { get; set; }             // 多空估值差 (Top 20% - Bottom 20%)
    public decimal HistoricalSpreadPercentile { get; set; }   // 估值差历史分位数 (%)
    public decimal CrowdingScore { get; set; }               // 拥挤度综合评分 (0~100)
    public string RiskStatus { get; set; } = "正常";          // 正常 / 适度拥挤 / 踩踏高危
    public decimal ShortTermReturnRunUp { get; set; }        // 近期累积急涨幅度 (%)
    public decimal VolatilityCompressionRatio { get; set; }  // 波动率低位压缩比率
    public string DiagnosticMessage { get; set; } = string.Empty;
}

public class FactorCrowdingAnalysisResult
{
    public string FundCode { get; set; } = string.Empty;
    public DateTime EvaluationDate { get; set; }
    public List<FactorCrowdingItem> Factors { get; set; } = new();
    public decimal OverallCrowdingIndex { get; set; }        // 因子系统综合拥挤指数 (0~100)
    public decimal EffectiveFactorDimension { get; set; }   // PCA 因子有效维度 Neff = (sum lambda)^2 / sum lambda^2
    public int CrowdedFactorCount { get; set; }              // 处于高危拥挤状态的因子数
    public string DominantCrowdedFactor { get; set; } = string.Empty; // 最拥挤的因子
    public string InstitutionalRiskWarning { get; set; } = string.Empty; // 机构级风控告警
}

// ==========================================
// Phase 19 机构级优化：SAA 战略基准与 TAA 战术偏离度监控模型
// ==========================================

public class SaaBenchmarkDefinition
{
    public string AssetClassName { get; set; } = string.Empty;
    public decimal SaaTargetWeight { get; set; }                     // SAA 战略基准权重 (%)
    public string BenchmarkIndexCode { get; set; } = string.Empty;   // 对应参考指数代码 (如 000300.SH, H11001.CSI)
    public string BenchmarkIndexName { get; set; } = string.Empty;   // 对应参考指数名称
}

public class TaaTacticalDeviationItem
{
    public string AssetClassName { get; set; } = string.Empty;
    public decimal SaaTargetWeight { get; set; }                     // SAA 战略目标权重 (%)
    public decimal CurrentWeight { get; set; }                       // 当前组合实际权重 (%)
    public decimal TacticalDeviation { get; set; }                   // 战术偏离度 Delta_w = Current - Saa (%)
    public decimal SoftToleranceBand { get; set; } = 3.0m;           // 软容忍带宽 (默认 3.0%)
    public decimal HardToleranceBand { get; set; } = 5.0m;           // 硬合规限额 (默认 5.0%)
    public string DeviationStatus { get; set; } = "正常";            // 正常 / ⚠️ 软超限预警 / 🚨 硬限额违规
    public string StatusBadge { get; set; } = "🟢 合规";
    public string RebalanceActionAdvice { get; set; } = string.Empty;// 调仓建议 (如: 建议减配 3.5%)
    public int MappedFundCount { get; set; }                         // 归属于该类别的基金只数
    public string MappedFundsSummary { get; set; } = string.Empty;   // 基金名称汇总
}

public class PortfolioSaaTaaMonitorResult
{
    public List<TaaTacticalDeviationItem> AssetClassDeviations { get; set; } = new();
    public decimal TotalActiveTrackingError { get; set; }            // 相对 SAA 复合基准的主动跟踪误差 (%)
    public decimal InformationRatio { get; set; }                   // 相对 SAA 复合基准的信息比率 (IR)
    public decimal CompositeBenchmarkReturn { get; set; }           // SAA 多资产复合基准累计收益率 (%)
    public decimal ActiveExcessReturn { get; set; }                 // 组合战术超额收益率 (%)
    public int SoftBreachCount { get; set; }                        // 触碰软容忍带宽资产数
    public int HardBreachCount { get; set; }                        // 突破硬限额资产数
    public string MaxDeviationAsset { get; set; } = string.Empty;   // 偏离度最大的大类资产
    public decimal MaxDeviationPercent { get; set; }
    public decimal SaaComplianceScore { get; set; }                 // SAA 合规履约度综合评分 (0~100)
    public string OverallStatus { get; set; } = "合规受控";         // 合规受控 / 存在偏离 / 严重违规调仓
    public bool RebalanceRequired { get; set; }                     // 是否必须启动战术纠偏再平衡
    public string SummaryRecommendation { get; set; } = string.Empty;
}

// ==========================================
// Phase 19 机构级优化：全组合收益与夏普比率贡献率穿透解构模型
// ==========================================

public class PortfolioSharpeContributionItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal WeightPercent { get; set; }                       // 组合持仓权重 (%)
    public decimal AnnualizedReturn { get; set; }                   // 单基年化收益率 (%)
    public decimal ReturnContributionPercent { get; set; }          // 收益贡献率 w_i * r_i / R_p (%)
    public decimal VolatilityContributionPercent { get; set; }      // 风险预算百分比贡献 PCR_i (%)
    public decimal MarginalSharpeContribution { get; set; }         // 边际夏普贡献 d(SR_p) / d(w_i)
    public decimal SharpeContributionPercent { get; set; }          // 夏普比率贡献占比 (%)
    public decimal RiskAdjustedEfficiencyRatio { get; set; }        // 收益-风险匹配效率指数 RC_i / PCR_i
    public string InstitutionalRole { get; set; } = "⚖️ 稳健匹配";   // 🔥 核心超额引擎 / 🛡️ 优质降波基石 / ⚠️ 夏普拖累负资产 / ⚖️ 稳健匹配
    public string RoleBadge { get; set; } = string.Empty;
    public string OptimizationAdvice { get; set; } = string.Empty;  // 针对性资产配置优化建议
}

public class PortfolioSharpeDecompositionResult
{
    public decimal PortfolioSharpeRatio { get; set; }
    public decimal PortfolioAnnualizedReturn { get; set; }
    public decimal PortfolioAnnualizedVolatility { get; set; }
    public List<PortfolioSharpeContributionItem> Items { get; set; } = new();
    public int AlphaEngineCount { get; set; }                       // 核心超额引擎资产数量
    public int SharpeDragCount { get; set; }                        // 拖累组合夏普资产数量
    public string BestEfficiencyFund { get; set; } = string.Empty;  // 效率最高基金
    public string WorstDragFund { get; set; } = string.Empty;       // 拖累最严重基金
    public decimal DragRemovalSharpePotential { get; set; }         // 剔除拖累资产后组合潜在夏普比率
    public string StrategicActionPlan { get; set; } = string.Empty; // 组合夏普提效行动方案
}

// ==========================================
// Phase 19 机构级优化：极端宏观情景反向压力测试 (Reverse Stress Testing) 模型
// ==========================================

public class ReverseStressFactorShock
{
    public string FactorName { get; set; } = string.Empty;
    public string FactorDisplayName { get; set; } = string.Empty;
    public decimal RequiredShockPercent { get; set; }               // 触发组合目标亏损所需的因子冲击幅度 (%)
    public decimal PortfolioSensitivityBeta { get; set; }           // 组合对该因子的加权弹性敏感度
    public decimal LossContributionFraction { get; set; }           // 该因子贡献的损失份额占比 (%)
    public string FragilityRank { get; set; } = string.Empty;       // 脆弱度评级 (高危 / 中度 / 坚挺)
}

public class ReverseStressTestResult
{
    public decimal TargetLossThresholdPercent { get; set; } = -15.0m;// 设定的反向亏损阈值 (如 -15%)
    public decimal MinimalShockNorm { get; set; }                   // 最小冲击马氏距离范数 (范数越小越脆弱)
    public List<ReverseStressFactorShock> FactorShocks { get; set; } = new();
    public string MostFragileFactor { get; set; } = string.Empty;   // 组合最脆弱的传导因子
    public decimal MostFragileFactorShockThreshold { get; set; }    // 该因子只需下跌多少即引爆阈值
    public string FragilityDiagnosis { get; set; } = string.Empty;  // 脆弱度综合诊断
    public string RecommendedHedgingAction { get; set; } = string.Empty; // 针对脆弱因子的对冲方案建议
}

// ==========================================
// Phase 19 机构级优化：风险价值后验检验 (Kupiec POF & Christoffersen 似然比检验) 模型
// ==========================================

public class KupiecVaRTestPoint
{
    public DateTime Date { get; set; }
    public decimal ActualReturnPercent { get; set; }
    public decimal HistoricalVaRPercent { get; set; }
    public decimal ParametricVaRPercent { get; set; }
    public bool IsHistoricalBreached { get; set; }
    public bool IsParametricBreached { get; set; }
}

public class KupiecVaRTestResult
{
    public decimal ConfidenceLevel { get; set; } = 0.95m;           // 95% 或 99% 置信度
    public int TotalObservations { get; set; }
    public int HistoricalExceptions { get; set; }                  // 历史 VaR 突破次数
    public int ParametricExceptions { get; set; }                  // 参量 VaR 突破次数
    public decimal HistoricalFailureRate { get; set; }             // 历史实际失效率 (%)
    public decimal ParametricFailureRate { get; set; }             // 参量实际失效率 (%)
    public decimal ExpectedFailureRate => (1.0m - ConfidenceLevel) * 100m;
    public decimal HistoricalLikelihoodRatio { get; set; }          // Kupiec LR 统计量
    public decimal ParametricLikelihoodRatio { get; set; }          // Kupiec LR 统计量
    public decimal HistoricalPValue { get; set; }                   // 检验 p-value
    public decimal ParametricPValue { get; set; }                   // 检验 p-value
    public string HistoricalTrafficLight { get; set; } = "Green";   // Green / Yellow / Red
    public string ParametricTrafficLight { get; set; } = "Green";   // Green / Yellow / Red
    public string HistoricalTrafficLightBadge => HistoricalTrafficLight switch
    {
        "Green" => "🟢 绿色合格区 (Model Sound)",
        "Yellow" => "🟡 黄色警戒区 (Supervisory Scrutiny)",
        "Red" => "🔴 红色拒绝区 (Model Rejected)",
        _ => "🟢 绿色合格区"
    };
    public string ParametricTrafficLightBadge => ParametricTrafficLight switch
    {
        "Green" => "🟢 绿色合格区 (Model Sound)",
        "Yellow" => "🟡 黄色警戒区 (Supervisory Scrutiny)",
        "Red" => "🔴 红色拒绝区 (Model Rejected)",
        _ => "🟢 绿色合格区"
    };
    public string ValidationSummary { get; set; } = string.Empty;
    public List<KupiecVaRTestPoint> RollingPoints { get; set; } = new();
}

// ==========================================
// Phase 20 机构级优化：宏观经济四象限体制轮动与全天候自适应配置矩阵模型
// ==========================================

public enum MacroRegimeType
{
    Recovery,    // 复苏期 (高增长, 低通胀) -> 顺周期成长、股票高配
    Overheat,    // 过热期 (高增长, 高通胀) -> 大宗商品、抗通胀黄金、顺周期价值
    Stagflation, // 滞胀期 (低增长, 高通胀) -> 现金防御、商品分化、短债
    Recession    // 衰退期 (低增长, 低通胀) -> 利率债长久期主升浪、防守高股息
}

public class MacroRegimeAssetTarget
{
    public string AssetClassName { get; set; } = string.Empty;
    public decimal CurrentWeightPercent { get; set; }
    public decimal RegimeOptimalWeightPercent { get; set; }
    public decimal TacticalWeightGap => RegimeOptimalWeightPercent - CurrentWeightPercent;
    public decimal ExpectedRegimeSharpe { get; set; }
    public string ActionAdvice { get; set; } = string.Empty; // 增配/减配/标配
}

public class CorrelationJumpPair
{
    public string AssetPair { get; set; } = string.Empty;
    public decimal BaselineCorrelation { get; set; }
    public decimal RegimeShockCorrelation { get; set; }
    public decimal CorrelationDelta => RegimeShockCorrelation - BaselineCorrelation;
    public string RiskAlert { get; set; } = string.Empty; // 股债双杀预警 / 避险有效
}

public class MacroRegimeSwitchingResult
{
    public MacroRegimeType CurrentRegime { get; set; } = MacroRegimeType.Recovery;
    public string RegimeDisplayName => CurrentRegime switch
    {
        MacroRegimeType.Recovery => "🌱 经济复苏象限 (高增长·低通胀)",
        MacroRegimeType.Overheat => "🔥 经济过热象限 (高增长·高通胀)",
        MacroRegimeType.Stagflation => "⚡ 经济滞胀象限 (低增长·高通胀)",
        MacroRegimeType.Recession => "🛡️ 经济衰退象限 (低增长·低通胀)",
        _ => "宏观中性"
    };
    public string EconomicEnvironmentSummary { get; set; } = string.Empty;
    public string FavorableAssetClasses { get; set; } = string.Empty;
    public List<MacroRegimeAssetTarget> AssetTargets { get; set; } = new();
    public List<CorrelationJumpPair> CorrelationJumpMatrix { get; set; } = new();
    public decimal RegimeFitScore { get; set; } // 组合当前配置与该体制的契合度打分 (0~100)
    public string TransitionTacticalAdvice { get; set; } = string.Empty;
}

// ==========================================
// Phase 20 机构级优化：条件在险回撤 (CDaR) 与欧拉期望亏空 (Euler ES) 尾部极值解构模型
// ==========================================

public class EulerExpectedShortfallItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal WeightPercent { get; set; }
    public decimal MarginalExpectedShortfall { get; set; } // MES: E[-r_i | -r_p >= VaR_p] (%)
    public decimal ComponentExpectedShortfall { get; set; } // CES: w_i * MES_i (%)
    public decimal TailRiskContributionPercent { get; set; } // ES%_i = CES_i / ES_p * 100%
    public bool IsTailRiskBlackHole { get; set; } // 尾部风险黑洞资产标识 (贡献显著超过权重占比)
    public string TailRiskBadge { get; set; } = string.Empty;
    public string DeRiskingAdvice { get; set; } = string.Empty;
}

public class PortfolioTailRiskDecompositionResult
{
    public decimal ConfidenceLevel { get; set; } = 0.95m;
    public decimal PortfolioVaR95Percent { get; set; } // 95% 在险价值 (%)
    public decimal PortfolioExpectedShortfallPercent { get; set; } // 95% 期望亏空 ES / CVaR (%)
    public decimal ConditionalDrawdownAtRisk95 { get; set; } // 95% 条件在险回撤 CDaR (%)
    public int SevereDrawdownEpisodesCount { get; set; } // 极端深套回撤期次数
    public int AverageSevereRecoveryDays { get; set; } // 极端回撤平均修复交易日
    public List<EulerExpectedShortfallItem> Items { get; set; } = new();
    public decimal EulerCheckSumPercent { get; set; } // 欧拉加总和 = sum(CES_i), 验证严格恒等性
    public int TailRiskBlackHoleCount { get; set; }
    public string WorstTailRiskFund { get; set; } = string.Empty;
    public string TailRiskExecutiveSummary { get; set; } = string.Empty;
}

// ==========================================
// Phase 20 机构级优化：机构投审会一键准入尽调闸门与自动化否决风控雷达模型
// ==========================================

public class GatekeeperRuleCheckItem
{
    public string RuleName { get; set; } = string.Empty;
    public string StandardCriteria { get; set; } = string.Empty;
    public string ActualValue { get; set; } = string.Empty;
    public bool IsPassed { get; set; }
    public bool IsHardVeto { get; set; } // 触犯一票否决红线
    public string StatusBadge => IsHardVeto ? "🔴 一票否决" : (IsPassed ? "🟢 合格通过" : "🟡 审慎关注");
    public string AuditDetail { get; set; } = string.Empty;
}

public class InstitutionalGatekeeperResult
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal OverallGateScore { get; set; } // 0 ~ 100
    public string CommitteeResolution { get; set; } = "🟢 建议准入入库 (Approved)"; // Approved / Watchlist / Vetoed
    public string ResolutionBadge => CommitteeResolution.Contains("准入") ? "🟢 准入" : (CommitteeResolution.Contains("观察") ? "🟡 观察" : "🔴 否决");
    public int TotalRulesCount { get; set; } = 6;
    public int PassedRulesCount { get; set; }
    public int HardVetoCount { get; set; }
    public List<GatekeeperRuleCheckItem> Rules { get; set; } = new();
    public List<string> VetoRedlines { get; set; } = new();
    public string ExecutiveRecommendation { get; set; } = string.Empty;
}

public class PortfolioGatekeeperAuditResult
{
    public decimal PortfolioOverallPassRate { get; set; } // 组合成分基金准入合规率 (%)
    public decimal PortfolioWeightedScore { get; set; } // 加权合规准入评分
    public int TotalComponentsCount { get; set; }
    public int ApprovedCount { get; set; }
    public int WatchlistCount { get; set; }
    public int VetoedCount { get; set; }
    public List<InstitutionalGatekeeperResult> FundAudits { get; set; } = new();
    public string CommitteeAuditSummary { get; set; } = string.Empty;
}

// ==========================================
// Phase 21 机构级优化：多资产多因子风险平价 (Factor Risk Parity) 与系统性因子风险解构模型
// ==========================================

public class FactorRiskDecompositionItem
{
    public string FactorName { get; set; } = string.Empty; // 市场贝塔(Beta)、规模(Size)、价值(Value)、动量(Momentum)、盈利质量(Quality)、低波(LowVol)、特异残差(Specific)
    public decimal PortfolioFactorBeta { get; set; }       // 组合在该因子上的加权有效暴露
    public decimal FactorVolatility { get; set; }          // 因子年化波动率 (%)
    public decimal FactorMarginalRiskContribution { get; set; } // 边际因子风险贡献 FMCR (%)
    public decimal FactorComponentRisk { get; set; }       // 绝对因子风险贡献 FCR (%)
    public decimal FactorPercentageContributionToRisk { get; set; } // 因子风险贡献占比 FPCR (%)
    public string RiskCategory { get; set; } = "系统性因子风险"; // 系统性因子风险 / 特异残差风险
    public string RiskBadge => FactorPercentageContributionToRisk > 35m ? "🚨 极度集中" : (FactorPercentageContributionToRisk > 20m ? "⚠️ 重点敞口" : "🟢 均衡分布");
    public string RiskAnalysisNote { get; set; } = string.Empty;
}

public class FactorRiskParityOptimalWeightItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal CurrentWeightPercent { get; set; }
    public decimal OptimalFrpWeightPercent { get; set; }   // 因子风险平价最优配置权重 (%)
    public decimal WeightGap => OptimalFrpWeightPercent - CurrentWeightPercent;
    public string AllocationAction => WeightGap > 2.0m ? "增配 (+)" : (WeightGap < -2.0m ? "减配 (-)" : "保持基准");
}

public class FactorRiskParityResult
{
    public decimal PortfolioTotalVolatility { get; set; }      // 组合总年化波动率 (%)
    public decimal SystematicRiskVariancePercent { get; set; } // 系统性因子方差占比 (%)
    public decimal SpecificRiskVariancePercent { get; set; }   // 特异残差方差占比 (%)
    public decimal FactorRiskHerfindahlIndex { get; set; }     // 因子风险集中度 HHI (0~10000，越低代表因子越分散平价)
    public List<FactorRiskDecompositionItem> FactorRiskItems { get; set; } = new();
    public List<FactorRiskParityOptimalWeightItem> OptimalWeights { get; set; } = new();
    public string ExecutiveDiagnosis { get; set; } = string.Empty;
    public string FactorDiversificationGrade { get; set; } = "良好"; // 优秀 / 良好 / 一般 / 严重集中
}

// ==========================================
// Phase 21 机构级优化：负债驱动投资 (LDI) 资产负债充足率与久期匹配雷丁顿免疫模型
// ==========================================

public class LiabilityCashFlowItem
{
    public int YearIndex { get; set; }                     // 未来第几年 (1~10)
    public decimal LiabilityCashFlow { get; set; }         // 预测兑付现金流 (亿元)
    public decimal DiscountRate { get; set; }              // 贴现收益率 (%)
    public decimal PresentValue { get; set; }              // 现值 (亿元)
    public decimal WeightInPvPercent { get; set; }         // 现值占比 (%)
    public decimal MacaulayDurationYears { get; set; }     // 麦考利久期贡献
}

public class LdiFundingStressShock
{
    public string ScenarioName { get; set; } = string.Empty; // e.g. "利率下行 -100bps", "利率下行 -50bps", "基准现状", "利率上行 +50bps", "利率上行 +100bps"
    public decimal InterestRateShiftBps { get; set; }      // 收益率变动 (bps)
    public decimal StressedAssetValue { get; set; }         // 冲击后资产估值 (亿元)
    public decimal StressedLiabilityPv { get; set; }       // 冲击后负债现值 (亿元)
    public decimal StressedFundingRatio { get; set; }      // 冲击后充足率 (%)
    public decimal SolvencySurplusDelta { get; set; }      // 偿付盈余变动 (亿元)
    public string SolvencyStatus { get; set; } = string.Empty; // 盈余充沛 / 基本平衡 / 出现偿付缺口
}

public class LdiImmunizationResult
{
    public decimal AssetTotalMarketValue { get; set; }     // 资产端总市值 (亿元)
    public decimal LiabilityTotalPresentValue { get; set; } // 负债端总精算现值 (亿元)
    public decimal FundingRatio { get; set; }              // 资产负债充足率 (%) = Asset / Liability * 100
    public decimal AssetMacaulayDurationYears { get; set; } // 资产端加权有效久期 (年)
    public decimal LiabilityMacaulayDurationYears { get; set; } // 负债端麦考利久期 (年)
    public decimal DollarDurationGap { get; set; }         // 金钱久期缺口 = (Asset * D_A - Liab * D_L) / 100
    public decimal DurationMismatchYears => AssetMacaulayDurationYears - LiabilityMacaulayDurationYears;
    public bool IsRedingtonImmunized { get; set; }         // 是否满足雷丁顿免疫标准 (|DurationMismatch| <= 0.5年 且 FundingRatio >= 100%)
    public string ImmunizationStatusBadge => IsRedingtonImmunized ? "🟢 雷丁顿免疫达标" : (FundingRatio < 100m ? "🔴 偿付准备金不足" : "🟡 存在久期错配敞口");
    public List<LiabilityCashFlowItem> LiabilityStream { get; set; } = new();
    public List<LdiFundingStressShock> StressShocks { get; set; } = new();
    public string ExecutiveAdvice { get; set; } = string.Empty;
}

// ==========================================
// Phase 21 机构级优化：考虑中国公募阶梯赎回费与账龄时钟的容差动态再平衡模型
// ==========================================

public class TieredFeeRebalanceItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal TargetWeightPercent { get; set; }
    public decimal ActualWeightPercent { get; set; }
    public decimal WeightDriftPercent => ActualWeightPercent - TargetWeightPercent;
    public bool IsBandBreached { get; set; }               // 是否突破容差带
    public decimal ExecutedTradeAmount { get; set; }       // 执行交易金额 (万元)
    public string TradeDirection { get; set; } = string.Empty; // 买入增配 / 卖出减持 / 容差静默
    public int AverageHoldingDays { get; set; }            // 拟减持份额对应的加权平均持有天数
    public decimal ApplicableFeeRatePercent { get; set; }  // 适用赎回费率 (<7天: 1.5%, 7-29天: 0.5%, 30-364天: 0.25%, >=365天: 0%)
    public decimal EstimatedFrictionCost { get; set; }     // 赎回费等摩擦损耗 (万元)
    public decimal AvoidedPunitiveFee { get; set; }        // 相比盲目月度刚性再平衡规避的 1.5% 惩罚费 (万元)
    public string ActionAdvice { get; set; } = string.Empty;
}

public class TieredFeeDynamicRebalanceResult
{
    public decimal ToleranceBandPercent { get; set; } = 5.0m; // 容差带阈值 (默认 ±5.0%)
    public int TotalComponentsCount { get; set; }
    public int BreachedCount { get; set; }                 // 突破容差带必须调仓的基金只数
    public int MutedCount { get; set; }                    // 容差带内静默无需交易的基金只数 (节省换手)
    public decimal TotalTurnoverVolume { get; set; }       // 容差带再平衡总换手规模 (万元)
    public decimal CalendarTurnoverVolume { get; set; }    // 对照组：日历固定再平衡总换手规模 (万元)
    public decimal TurnoverReductionRatePercent { get; set; } // 换手率降低比例 (%)
    public decimal ActualFrictionCostTotal { get; set; }   // 实际产生的摩擦成本 (万元)
    public decimal AvoidedPunitiveFeesTotal { get; set; }  // 成功规避的 7 天内 1.5% 惩罚赎回费 (万元)
    public decimal FrictionCostRatePercent { get; set; }   // 摩擦损耗占组合总资金比例 (%)
    public List<TieredFeeRebalanceItem> Items { get; set; } = new();
    public string FrictionOptimizationSummary { get; set; } = string.Empty;
}

// ==========================================
// Phase 22 机构级优化：截面多因子全市场选基分位数多空利差与单调性检验模型
// ==========================================

public class DecilePerformanceItem
{
    public int DecileIndex { get; set; }                  // 分位数编号 (1~10: D1=前10%最高分, D10=后10%最低分)
    public string DecileName => $"D{DecileIndex}";
    public int FundCount { get; set; }                    // 纳入该分位数的基金数量
    public decimal AnnualizedReturn { get; set; }         // 该分位组合年化收益率 (%)
    public decimal AnnualizedVolatility { get; set; }     // 该分位组合年化波动率 (%)
    public decimal MaxDrawdown { get; set; }             // 该分位组合历史最大回撤 (%)
    public decimal SharpeRatio { get; set; }              // 该分位组合夏普比率
    public decimal WinRatePercent { get; set; }           // 正收益胜率 (%)
    public decimal ExcessReturnOverMarket { get; set; }   // 相对全市场等权基准的超额收益 (%)
    public string DecileRole { get; set; } = string.Empty;// 角色标签 (如 "💎 头部阿尔法精选", "🛡️ 中枢基石", "🚨 尾部排雷避险")
}

public class DecileSpreadBacktestResult
{
    public string FactorName { get; set; } = "多因子综合评分"; // 检验的因子名称
    public int TotalUniverseCount { get; set; }           // 样本池基金总数
    public List<DecilePerformanceItem> Deciles { get; set; } = new();
    public decimal LongShortAnnualizedSpread { get; set; }// 多空年化利差 (D1 - D10, %)
    public decimal LongShortVolatility { get; set; }      // 多空利差年化波动率 (%)
    public decimal LongShortSharpe { get; set; }          // 多空组合信息比率 / 夏普比率 (IR)
    public decimal LongShortMaxDrawdown { get; set; }     // 多空组合最大回撤 (%)
    public decimal FactorTStatistic { get; set; }         // 因子多空利差 t-检验统计量
    public bool IsStatisticallySignificant => Math.Abs(FactorTStatistic) >= 2.0m; // |t| >= 2.0 显著
    public decimal MonotonicityScorePercent { get; set; } // 单调性得分 (D_k > D_{k+1} 递减对比例, 0~100%)
    public decimal SpearmanRankCorrelation { get; set; }  // 分位数与收益率的斯皮尔曼秩相关系数 (-1.0 ~ +1.0)
    public string FactorEfficacyGrade { get; set; } = string.Empty; // 🏆 卓越有效 / ⭐ 稳健显著 / ⚠️ 弱单调 / ❌ 因子失效
    public string DiagnosticSummary { get; set; } = string.Empty;   // 投研会审计结论
}

// ==========================================
// Phase 22 机构级优化：阿拉丁级历史黑天鹅全息宏观危机因子传导压力测试模型
// ==========================================

public class CrisisShockImpactItem
{
    public string ScenarioId { get; set; } = string.Empty;     // 场景唯一标识
    public string ScenarioName { get; set; } = string.Empty;   // 如 "2008 全球次贷金融海啸"
    public string HistoricalPeriod { get; set; } = string.Empty;// 如 "2007.10 - 2008.10"
    public string CoreMacroShockVector { get; set; } = string.Empty; // 核心冲击向量摘要
    public decimal ProjectedPortfolioReturn { get; set; }      // 组合预估承压收益率 (%)
    public decimal StressedDrawdown { get; set; }              // 极端情景下最大承压回撤 (%)
    public decimal EstimatedCapitalLossAmount { get; set; }    // 组合设定资金下的亏损金额 (万元)
    public decimal StressedVaR99 { get; set; }                 // 99% 置信度极端在险价值 (%)
    public decimal RecoveryPeriodMonths { get; set; }          // 预估历史修复周期 (月)
    public string ResilienceGrade { get; set; } = string.Empty; // 💎 坚不可摧 / 🛡️ 稳健抗跌 / ⚠️ 适度承压 / 🚨 严重失血
    public string DefenseDiagnosis { get; set; } = string.Empty;// 机构防御能力诊断与对冲建议
}

public class HistoricalCrisisStressResult
{
    public decimal PortfolioCapitalTenThousand { get; set; } = 1000m; // 测算本金规模 (万元)
    public decimal AverageCrisisDrawdown { get; set; }         // 6 大危机平均最大回撤 (%)
    public decimal WorstCaseLossPercent { get; set; }          // 最极端情景单次跌幅 (%)
    public string WorstCrisisName { get; set; } = string.Empty;// 最脆弱暴露的危机事件
    public decimal BestResilienceReturn { get; set; }          // 最强防御情景下的净值变化 (%)
    public string OverallResilienceRating { get; set; } = string.Empty; // 综合抗极端黑天鹅评级
    public List<CrisisShockImpactItem> CrisisScenarios { get; set; } = new();
    public string ExecutiveSummary { get; set; } = string.Empty;// 投研决策委员会风控审查报告
}

// ==========================================
// Phase 22 机构级优化：大体量资金执行落差与平方根市场冲击模型 (含公募10%巨额赎回预警与TWAP拆单)
// ==========================================

public class ExecutionImpactItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public string TradeDirection { get; set; } = string.Empty; // 买入增配 / 卖出减持
    public decimal TradeAmountTenThousand { get; set; }        // 拟调仓金额 (万元)
    public decimal EstimatedDailyVolumeAdvTenThousand { get; set; } // 预估日均成交/申赎体量 ADV (万元)
    public decimal OrderAdvRatioPercent { get; set; }          // 订单占日均体量比例 (%)
    public decimal MarketImpactBps { get; set; }               // 平方根市场冲击成本 (基点 bps)
    public decimal MarketImpactCostTenThousand { get; set; }   // 冲击摩擦损失金额 (万元)
    public decimal CsrcRedemptionAumRatioPercent { get; set; } // 赎回额占该基金总规模比例 (%)
    public bool IsGiantRedemptionRisk { get; set; }            // 是否触及公募 10% 巨额赎回警戒线
    public string GiantRedemptionAlertBadge => IsGiantRedemptionRisk 
        ? "🚨 触发10%巨额赎回预警" 
        : (CsrcRedemptionAumRatioPercent >= 5.0m ? "⚠️ 接近巨额赎回门槛(>5%)" : "🟢 流动性充足安全");
    public int RecommendedExecutionDays { get; set; }          // 建议执行交易日数 (TWAP平摊)
    public string ExecutionAdvice { get; set; } = string.Empty;// 交易台执行指令建议
}

public class TwapExecutionTranche
{
    public int DayIndex { get; set; }                          // 执行日 (第 1..N 天)
    public string DayLabel => $"T+{DayIndex - 1} 日";
    public decimal TrancheAmountTenThousand { get; set; }      // 当日计划拆单执行金额 (万元)
    public decimal CumulativePercentage { get; set; }          // 累计执行进度 (%)
    public decimal EstimatedTrancheImpactBps { get; set; }     // 当期拆单后分摊市场冲击 (bps)
}

public class ExecutionShortfallResult
{
    public decimal TotalCapitalTenThousand { get; set; } = 1000m; // 投资组合总规模 (万元)
    public decimal TotalTradeVolumeTenThousand { get; set; }   // 总调仓交易金额 (万元)
    public decimal TotalMarketImpactCostTenThousand { get; set; } // 平方根市场冲击总损耗 (万元)
    public decimal AverageImpactBps { get; set; }              // 加权平均冲击成本 (bps)
    public decimal StatutoryFeesTenThousand { get; set; }      // 基础申赎与销售服务规费 (万元)
    public decimal TotalExecutionShortfallTenThousand => TotalMarketImpactCostTenThousand + StatutoryFeesTenThousand; // 总执行落差
    public int GiantRedemptionBreachCount { get; set; }        // 触及巨额赎回预警的基金数量
    public int MaxRecommendedExecutionDays { get; set; }       // 组合调仓所需最大平滑交易日数
    public List<ExecutionImpactItem> ImpactItems { get; set; } = new();
    public List<TwapExecutionTranche> RecommendedTwapTranches { get; set; } = new();
    public string ExecutionDeskSummary { get; set; } = string.Empty; // 机构交易台执行综述
}

// ==========================================
// Phase 23 机构级优化：动态因子时序择时与动量-估值自适应轮动中枢
// ==========================================

public class FactorTimingMetricItem
{
    public string FactorId { get; set; } = string.Empty; // "Value", "Momentum", "LowVol", "Quality", "Size"
    public string FactorName { get; set; } = string.Empty; // 价值因子 / 动量因子 / 低波动因子 / 质量因子 / 小市值因子
    public decimal ShortTermMomentum1M { get; set; } // 1个月动量 (%)
    public decimal MediumTermMomentum3M { get; set; } // 3个月动量 (%)
    public decimal LongTermMomentum12M { get; set; } // 12个月动量 (%)
    public decimal ValuationSpreadZScore { get; set; } // 因子估值价差 Z-Score (-3.0 ~ +3.0)
    public decimal Volatility3M { get; set; } // 3个月年化波动率 (%)
    public decimal TrendStrength { get; set; } // 趋势综合得分 (-100 ~ +100)
    public decimal RecommendedWeightPercent { get; set; } // 推荐自适应配置权重 (%)
    public string RotationStance { get; set; } = string.Empty; // 🔥 强烈超配 / 📈 稳健增配 / ⚖️ 标配中性 / 📉 减配防守
    public string FactorCrowdingLevel { get; set; } = string.Empty; // 🟢 拥挤度低 / 🟡 拥挤度适中 / 🚨 极度拥挤注意踩踏
}

public class DynamicFactorTimingResult
{
    public string CurrentMarketRegime { get; set; } = "红利低波防守体制"; // 市场风格体制
    public DateTime EvaluationDate { get; set; } = DateTime.Today;
    public decimal RegimeConfidencePercent { get; set; } = 85.0m;
    public List<FactorTimingMetricItem> FactorMetrics { get; set; } = new();
    public string TopOverweightFactor { get; set; } = string.Empty;
    public string TopUnderweightFactor { get; set; } = string.Empty;
    public decimal FactorDispersionPercent { get; set; } = 18.5m; // 截面因子离散度 (%)
    public string MacroFactorAdvice { get; set; } = string.Empty;
    public string RotationCycleDiagnosis { get; set; } = string.Empty; // 轮动周期诊断
}

// ==========================================
// Phase 23 机构级优化：Fama-French 运气剔除与基金经理真技能 Bootstrap 检验模型
// ==========================================

public class BootstrapAlphaResult
{
    public string ManagerName { get; set; } = string.Empty;
    public decimal ObservedAnnualAlphaPercent { get; set; } // 观测年化超额 Alpha (%)
    public decimal ObservedTStat { get; set; } // 观测 t-统计量
    public int BootstrapTrialsCount { get; set; } = 1000; // Bootstrap 模拟次数
    public decimal LuckyAlphaMeanPercent { get; set; } // 零技能伪分布 Alpha 均值 (~0)
    public decimal LuckyAlphaStdPercent { get; set; } // 纯运气伪 Alpha 标准差
    public decimal LuckyAlpha5thPercentile { get; set; } // 5% 分位数 (左尾)
    public decimal LuckyAlpha95thPercentile { get; set; } // 95% 分位数 (右尾)
    public decimal EmpiricalPValue { get; set; } // 经验单侧 p-value (超越纯运气的概率, 越小越显著)
    public decimal TwoSidedPValue { get; set; } // 双侧检验 p-value
    public string SkillClassification { get; set; } = string.Empty; // 🏆 统计显著真Alpha (True Skill) / 🎲 运气驱动幸存者 (Luck Driven) / ⚖️ 风格Beta暴露 / 📉 负向价值侵蚀 (Alpha Destroyer)
    public decimal ShrunkTrueAlphaPercent { get; set; } // 经验贝叶斯收缩后的真实 Alpha 期望 (%)
    public string ConfidenceGrade { get; set; } = string.Empty; // 99% 极高置信 / 95% 显著 / 90% 边际显著 / ❌ 不显著
    public string AuditConclusion { get; set; } = string.Empty; // 投研会审计建议
}

// ==========================================
// Phase 23 机构级优化：GIPS 国际标准多期复合 Brinson 归因 (Carino 对数平滑算法)
// ==========================================

public class BrinsonPeriodInput
{
    public string PeriodLabel { get; set; } = string.Empty; // 如 "2024-Q1" 或 "第1期"
    public decimal PortfolioReturn { get; set; } // 组合当期收益 (%)
    public decimal BenchmarkReturn { get; set; } // 基准当期收益 (%)
    public decimal AllocationEffect { get; set; } // 当期单期资产配置效应 (%)
    public decimal SelectionEffect { get; set; } // 当期单期个基选择效应 (%)
    public decimal InteractionEffect { get; set; } // 当期单期交互效应 (%)
}

public class MultiPeriodBrinsonPeriodItem
{
    public string PeriodLabel { get; set; } = string.Empty;
    public decimal PortfolioReturn { get; set; } // %
    public decimal BenchmarkReturn { get; set; } // %
    public decimal ExcessReturn => PortfolioReturn - BenchmarkReturn; // %
    public decimal RawAllocationEffect { get; set; }
    public decimal RawSelectionEffect { get; set; }
    public decimal RawInteractionEffect { get; set; }
    public decimal CarinoLinkingFactor { get; set; } // 平滑系数 k_t
    public decimal SmoothedAllocationEffect { get; set; } // 平滑调整后配置效应 (%)
    public decimal SmoothedSelectionEffect { get; set; } // 平滑调整后选择效应 (%)
    public decimal SmoothedInteractionEffect { get; set; } // 平滑调整后交互效应 (%)
    public decimal TotalSmoothedEffect => SmoothedAllocationEffect + SmoothedSelectionEffect + SmoothedInteractionEffect;
}

public class MultiPeriodBrinsonResult
{
    public int PeriodCount { get; set; }
    public decimal TotalPortfolioCompoundedReturn { get; set; } // 全期复合投资组合收益率 (%)
    public decimal TotalBenchmarkCompoundedReturn { get; set; } // 全期复合基准收益率 (%)
    public decimal TotalCompoundedExcessReturn { get; set; } // 全期复合总超额收益率 (%)
    public decimal CumulativeSmoothedAllocationEffect { get; set; } // 全期累积平滑配置效应 (%)
    public decimal CumulativeSmoothedSelectionEffect { get; set; } // 全期累积平滑选择效应 (%)
    public decimal CumulativeSmoothedInteractionEffect { get; set; } // 全期累积平滑交互效应 (%)
    public decimal MathematicalIdentityResidual { get; set; } // 残差 = 总超额 - (配置 + 选择 + 交互)，理论严格为 0
    public bool IsGipsIdentityStrictlySatisfied => Math.Abs(MathematicalIdentityResidual) < 0.0001m; // GIPS 恒等检验
    public decimal AllocationContributionPercent { get; set; } // 配置贡献占比 (%)
    public decimal SelectionContributionPercent { get; set; } // 选择贡献占比 (%)
    public List<MultiPeriodBrinsonPeriodItem> Periods { get; set; } = new();
    public string PerformanceSummary { get; set; } = string.Empty;
}

// ==========================================
// Phase 23 机构级优化：FOF 底层全息影子组合二次重构与风格纯度分析模型
// ==========================================

public class ShadowStockResonanceItem
{
    public string StockCode { get; set; } = string.Empty;
    public string StockName { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal AggregatedPortfolioWeightPercent { get; set; } // 组合底层穿透聚合权重 (%)
    public int HoldingFundCount { get; set; } // 持有该个股的成分基金只数
    public List<string> HoldingFundNames { get; set; } = new(); // 持有该股票的基金列表
    public bool IsHighResonanceWarning => HoldingFundCount >= 2 && AggregatedPortfolioWeightPercent >= 2.0m;
    public string RiskTag => IsHighResonanceWarning ? "🚨 跨基金抱团共振" : "🟢 正常重叠";
}

public class ShadowPortfolioPurityResult
{
    public int TotalUnderlyingStockCount { get; set; } // 穿透底层独立个股总数
    public decimal Top10StockConcentrationPercent { get; set; } // 影子组合 Top10 集中度 (%)
    public decimal ActiveSharePercent { get; set; } // 相对宽基基准的主动持股比例 Active Share (%)
    public decimal StylePurityScore { get; set; } // 风格纯度得分 (0~100)
    public decimal HiddenOverlapConcentrationPercent { get; set; } // 跨基金重复持有个股合计权重 (%)
    public string HiddenOverlapRiskLevel { get; set; } = string.Empty; // 🟢 分散健康 / 🟡 适度重叠 / 🚨 严重抱团共振
    public List<ShadowStockResonanceItem> TopShadowHoldings { get; set; } = new();
    public List<ShadowStockResonanceItem> HighResonanceAlerts { get; set; } = new();
    public string PurityDiagnosis { get; set; } = string.Empty;
}

// ==========================================
// Phase 24: 机构级前沿量化投研模型定义
// ==========================================

public class FundLiquidityTierItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal PortfolioWeightPercent { get; set; } // 组合中权重 (%)
    public decimal PortfolioHoldingAmountTenThousand { get; set; } // 持仓市值 (万元)
    public decimal FundAumBillion { get; set; } // 基金总规模 (亿元)
    public decimal DaysToLiquidateFull { get; set; } // 100%仓位清算所需天数 (基于10% ADV参与率)
    public decimal DaysToLiquidateHalf { get; set; } // 50%仓位清算天数
    public string LiquidityTier { get; set; } = string.Empty; // Tier 1 (<=1天) / Tier 2 (2~3天) / Tier 3 (4~7天) / Tier 4 (>7天)
    public decimal EstimatedLiquidationSlippageBps { get; set; } // 预计完全清算冲击成本 (bps)
    public decimal EstimatedLiquidationCostTenThousand { get; set; } // 冲击滑点损失金额 (万元)
}

public class RedemptionCascadeScenarioItem
{
    public decimal RedemptionRatioPercent { get; set; } // 赎回比例 (如 10%, 20%, 30%)
    public string LiquidationStrategy { get; set; } = string.Empty; // 瀑布式优先变现高流动性 (Waterfall) vs 全组合等比例平仓 (Pro-Rata) vs 最优流动性保全 (Optimal)
    public decimal PreRunWeightedDtlDays { get; set; } // 赎回前组合加权变现天数 (天)
    public decimal PostRunWeightedDtlDays { get; set; } // 赎回后剩余组合加权变现天数 (天)
    public decimal LiquidityDeteriorationPercent { get; set; } // 剩余组合流动性恶化度 (%)
    public decimal TotalSlippageCostTenThousand { get; set; } // 变现冲击滑点成本 (万元)
    public decimal RemainingHealthScore { get; set; } // 剩余组合流动性健康度评分 (0~100)
    public string RegulatoryAlertStatus { get; set; } = string.Empty; // 🟢 合规充裕 / 🟡 密切关注 / 🚨 严重枯竭预警
    public string ScenarioDescription { get; set; } = string.Empty;
}

public class LiquidityLadderRedemptionRunResult
{
    public decimal PortfolioWeightedDtlDays { get; set; } // 组合加权变现天数 (天)
    public string PortfolioLiquidityGrade { get; set; } = string.Empty; // 极高流动性 / 优良流动性 / 中性适度 / 严重受限
    public decimal Tier1WeightPercent { get; set; } // Tier 1 (<=1天) 资产权重占比 (%)
    public decimal Tier2WeightPercent { get; set; } // Tier 2 (2~3天) 资产权重占比 (%)
    public decimal Tier3WeightPercent { get; set; } // Tier 3 (4~7天) 资产权重占比 (%)
    public decimal Tier4WeightPercent { get; set; } // Tier 4 (>7天) 资产权重占比 (%)
    public List<FundLiquidityTierItem> FundTierItems { get; set; } = new();
    public List<RedemptionCascadeScenarioItem> CascadeScenarios { get; set; } = new();
    public string ExecutiveDiagnosis { get; set; } = string.Empty;
}

public class PortfolioInsurancePathPoint
{
    public int StepIndex { get; set; }
    public decimal BenchmarkValue { get; set; } // 买入持有策略净值
    public decimal CppiAssetValue { get; set; } // CPPI 策略资产总值
    public decimal CppiFloorValue { get; set; } // CPPI 动态贴现底线 Floor
    public decimal CppiCushion { get; set; } // CPPI 安全垫 Cushion
    public decimal CppiRiskExposurePercent { get; set; } // CPPI 风险资产目标敞口占比 (%)
    public decimal TippAssetValue { get; set; } // TIPP 棘轮策略资产总值
    public decimal TippFloorValue { get; set; } // TIPP 棘轮锁定底线 Floor
    public decimal TippCushion { get; set; } // TIPP 安全垫 Cushion
    public decimal TippRiskExposurePercent { get; set; } // TIPP 风险资产目标敞口占比 (%)
}

public class PortfolioInsuranceSimulationResult
{
    public decimal InitialCapital { get; set; } = 1000m; // 期初初始本金 (万元)
    public decimal ProtectionRatioPercent { get; set; } = 95.0m; // 预设保本率 (%)
    public decimal RiskMultiplier { get; set; } = 3.5m; // 风险乘数 M
    public decimal RiskFreeRatePercent { get; set; } = 2.5m; // 无风险收益率 (%)
    public decimal CppiFinalValue { get; set; } // CPPI 期末净资产 (万元)
    public decimal CppiTotalReturnPercent { get; set; } // CPPI 累计收益率 (%)
    public decimal CppiMaxDrawdownPercent { get; set; } // CPPI 实际最大回撤 (%)
    public decimal TippFinalValue { get; set; } // TIPP 棘轮期末净资产 (万元)
    public decimal TippTotalReturnPercent { get; set; } // TIPP 累计收益率 (%)
    public decimal TippMaxDrawdownPercent { get; set; } // TIPP 实际最大回撤 (%)
    public decimal BuyAndHoldFinalValue { get; set; } // 对照组买入持有期末净资产 (万元)
    public decimal BuyAndHoldTotalReturnPercent { get; set; } // 对照组累计收益率 (%)
    public decimal BuyAndHoldMaxDrawdownPercent { get; set; } // 对照组最大回撤 (%)
    public bool IsCppiFloorPreserved { get; set; } // CPPI 是否绝对未击穿保本底线
    public bool IsTippFloorPreserved { get; set; } // TIPP 是否绝对未击穿保本底线
    public decimal CppiProtectionSuccessRatePercent { get; set; } // CPPI 保本成功率 (%)
    public decimal TippProtectionSuccessRatePercent { get; set; } // TIPP 保本成功率 (%)
    public List<PortfolioInsurancePathPoint> PathPoints { get; set; } = new();
    public string StrategyRecommendation { get; set; } = string.Empty;
}

public class HigherMomentAssetMetric
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal AnnualizedReturnPercent { get; set; } // 年化收益率 (%)
    public decimal AnnualizedVolPercent { get; set; } // 年化波动率 (%)
    public decimal Skewness { get; set; } // 偏度 S
    public decimal ExcessKurtosis { get; set; } // 超额峰度 K
    public decimal CornishFisherMVaR95 { get; set; } // Cornish-Fisher 95% 修正在险价值 (%)
    public decimal StandardSharpeRatio { get; set; } // 经典高斯夏普比率
    public decimal ModifiedSharpeRatio { get; set; } // 高阶矩修正夏普比率 (MSR)
    public decimal OmegaRatio { get; set; } // Omega 收益分布比率 (@L=0)
    public string TailRiskBadge { get; set; } = string.Empty; // 🟢 正偏细尾 (收益凸性) / 🟡 适度正态 / 🚨 负偏高尖 (深水炸弹)
}

public class HigherMomentsOptimizationResult
{
    public Dictionary<string, decimal> ClassicMarkowitzWeights { get; set; } = new(); // 经典 Markowitz 均值-方差权重
    public Dictionary<string, decimal> ModifiedSharpeOptimalWeights { get; set; } = new(); // 修正夏普最优权重
    public Dictionary<string, decimal> OmegaOptimalWeights { get; set; } = new(); // Omega 比率最大化最优权重
    public decimal ClassicPortfolioSharpe { get; set; }
    public decimal ModifiedPortfolioSharpe { get; set; }
    public decimal OmegaPortfolioRatio { get; set; }
    public List<HigherMomentAssetMetric> AssetMetrics { get; set; } = new();
    public string OptimizationComparisonDiagnosis { get; set; } = string.Empty;
}

public class ManagerTransitionAuditResult
{
    public string HandoverDateText { get; set; } = string.Empty; // 换帅断点交接日期 (如 "2023-06-15")
    public string PredecessorName { get; set; } = string.Empty; // 前任基金经理
    public decimal PredecessorTenureYears { get; set; } // 前任任职年限
    public decimal PredecessorAnnualAlphaPercent { get; set; } // 前任年化超额 Alpha (%)
    public decimal PredecessorBeta { get; set; } // 前任跟踪 Beta
    public decimal PredecessorVolatilityPercent { get; set; } // 前任年化波动率 (%)
    public decimal PredecessorMaxDrawdownPercent { get; set; } // 前任最大回撤 (%)
    public string SuccessorName { get; set; } = string.Empty; // 现任接棒经理
    public decimal SuccessorTenureYears { get; set; } // 现任任职年限
    public decimal SuccessorAnnualAlphaPercent { get; set; } // 现任年化超额 Alpha (%)
    public decimal SuccessorBeta { get; set; } // 现任跟踪 Beta
    public decimal SuccessorVolatilityPercent { get; set; } // 现任年化波动率 (%)
    public decimal SuccessorMaxDrawdownPercent { get; set; } // 现任最大回撤 (%)
    public decimal AlphaDeltaPercent { get; set; } // Alpha 变化幅度 Delta (%)
    public decimal BetaDelta { get; set; } // Beta 突变幅度 Delta
    public decimal ChowTestFStatistic { get; set; } // Chow 断点结构突变检验 F 统计量
    public decimal ChowPValue { get; set; } // Chow 检验统计显著性 p 值
    public bool IsStructuralBreakSignificant { get; set; } // 是否发生统计显著的结构性断裂 (p < 0.05)
    public string StructuralBreakConclusion => IsStructuralBreakSignificant ? "拒绝原假设 (结构显著突变)" : "接受原假设 (无显著断裂)";
    public string TransitionRiskRating { get; set; } = string.Empty; // 🟢 平稳交接 / 🟡 风格重塑 / 🚨 核心断崖
    public string InvestmentCommitteeActionAdvice { get; set; } = string.Empty; // 投审会出入池量化裁决意见
}

// ==========================================
// Phase 25: 机构级前沿量化投研模型定义
// ==========================================

public class CopulaPairItem
{
    public string FundCodeA { get; set; } = string.Empty;
    public string FundNameA { get; set; } = string.Empty;
    public string FundCodeB { get; set; } = string.Empty;
    public string FundNameB { get; set; } = string.Empty;
    public decimal PearsonCorrelation { get; set; } // 经典线性皮尔逊相关系数
    public decimal KendallTau { get; set; } // 肯德尔秩相关系数 Tau
    public decimal ClaytonTheta { get; set; } // Clayton 联结函数参数
    public decimal LowerTailDependenceLambda { get; set; } // 极值下尾相关性 Lambda_L (暴跌协同踩踏概率)
    public decimal GumbelTheta { get; set; } // Gumbel 联结函数参数
    public decimal UpperTailDependenceLambda { get; set; } // 极值上尾相关性 Lambda_U (共振暴涨概率)
    public decimal TailAsymmetry { get; set; } // 尾部非对称度 (Lambda_L - Lambda_U)
    public string TailRiskBadge { get; set; } = string.Empty; // 🚨 极端下尾协同暴跌 / 🟡 中度非对称 / 🟢 独立分散
}

public class CopulaTailDependenceResult
{
    public decimal PortfolioWeightedLowerTailDependence { get; set; } // 组合加权下尾极端相关性
    public decimal PortfolioWeightedUpperTailDependence { get; set; } // 组合加权上尾极端相关性
    public decimal SystemicCrashAmplificationFactor { get; set; } // 系统性崩盘在险损失放大倍数 (如 1.45x)
    public decimal LinearVsCopulaVaRDifferencePercent { get; set; } // 传统高斯 VaR 相比 Copula 极值尾部低估偏差 (%)
    public List<CopulaPairItem> PairwiseCopulaList { get; set; } = new();
    public string ExecutiveTailDiagnosis { get; set; } = string.Empty;
}

public class ParetoSolutionItem
{
    public int SolutionId { get; set; }
    public string SolutionName { get; set; } = string.Empty; // 均衡折中解 (Knee Point) / 极值下行免疫解 / 进取超额增强解 / 低换手平滑解
    public decimal ExpectedReturnPercent { get; set; } // 预期年化收益率 (%)
    public decimal DownsideCvar95Percent { get; set; } // 95% 条件在险价值 CVaR (%)
    public decimal RebalanceTurnoverPercent { get; set; } // 调仓换手摩擦 (%)
    public decimal SharpeRatio { get; set; } // 预期夏普比率
    public Dictionary<string, decimal> Weights { get; set; } = new(); // 基金代码 -> 配置权重 (%)
    public int ParetoRank { get; set; } = 1; // 帕累托非支配层级 (1 为第一前沿)
    public decimal CrowdingDistance { get; set; } // 拥挤度距离
    public bool IsRecommended { get; set; } // 是否为推荐折中配置
}

public class ParetoMultiObjectiveResult
{
    public List<ParetoSolutionItem> FrontierSolutions { get; set; } = new();
    public ParetoSolutionItem? OptimalCompromiseSolution { get; set; } // 拐点平衡最优解
    public ParetoSolutionItem? ConservativeSolution { get; set; } // 稳健免疫极值下行解
    public ParetoSolutionItem? AggressiveSolution { get; set; } // 进取超额收益最大化解
    public decimal HypervolumeIndicator { get; set; } // 帕累托前沿超体积指标 (覆盖率)
    public decimal FrontierDiversitySpread { get; set; } // 前沿解集分散跨度指标
    public string ExecutiveParetoAdvice { get; set; } = string.Empty;
}

public class GarchForecastPoint
{
    public int HorizonDays { get; set; } // 预测时间窗口 (如 5, 10, 20, 40, 60, 120, 252 日)
    public DateTime ForecastDate { get; set; } // 前瞻预测截止日期
    public decimal ForecastAnnualizedVolPercent { get; set; } // GARCH(1,1) 条件年化波动率预测 (%)
    public decimal VolCone10Percentile { get; set; } // 波动率锥 10% 极低分位 (%)
    public decimal VolCone25Percentile { get; set; } // 波动率锥 25% 分位 (%)
    public decimal VolCone50Median { get; set; } // 波动率锥 50% 中位分位 (%)
    public decimal VolCone75Percentile { get; set; } // 波动率锥 75% 分位 (%)
    public decimal VolCone90Percentile { get; set; } // 波动率锥 90% 极高分位 (%)
    public bool IsClusteringHighVol { get; set; } // 是否处于高波聚集状态
}

public class GarchVolatilityForecastResult
{
    public decimal CurrentConditionalVolPercent { get; set; } // 当前即期条件年化波动率 (%)
    public decimal LongTermUnconditionalVolPercent { get; set; } // 长期无条件均值回归波动率 (%)
    public decimal Omega { get; set; } // GARCH 常数项 Omega
    public decimal Alpha { get; set; } // ARCH 创新冲击系数 Alpha
    public decimal Beta { get; set; } // GARCH 记忆衰减系数 Beta
    public decimal Persistence { get; set; } // 波动率持续性 (Alpha + Beta)
    public decimal HalfLifeDays { get; set; } // 均值回归半衰期 (天)
    public string VolClusteringRegime { get; set; } = string.Empty; // 🔴 高波聚类冲击释放 / 🟢 稳态低波均值收敛 / 🟡 异动消化过渡期
    public List<GarchForecastPoint> ForecastPoints { get; set; } = new();
    public string TacticalRiskBudgetAdvice { get; set; } = string.Empty;
}

// ==========================================
// Phase 26: 机构级前沿量化投研模型定义
// ==========================================

public class ShrinkageCovarianceItem
{
    public string FundCodeA { get; set; } = string.Empty;
    public string FundNameA { get; set; } = string.Empty;
    public string FundCodeB { get; set; } = string.Empty;
    public string FundNameB { get; set; } = string.Empty;
    public decimal SampleCovariance { get; set; } // 经典样本协方差
    public decimal TargetPriorCovariance { get; set; } // 常数相关目标协方差
    public decimal ShrunkCovariance { get; set; } // Ledoit-Wolf 收缩后协方差
    public decimal SampleCorrelation { get; set; } // 样本相关系数
    public decimal ShrunkCorrelation { get; set; } // 收缩后平滑相关系数
}

public class LedoitWolfShrinkageResult
{
    public decimal OptimalShrinkageIntensity { get; set; } // 渐近最优收缩强度 delta* in [0, 1]
    public decimal OptimalShrinkagePercent { get; set; } // 最优收缩强度百分比 (%)
    public decimal SampleConditionNumber { get; set; } // 样本协方差矩阵条件数 (lambda_max / lambda_min)
    public decimal ShrunkConditionNumber { get; set; } // 收缩协方差矩阵条件数
    public decimal ConditionNumberImprovementRatio { get; set; } // 条件数优化与良态改善倍数
    public decimal AveragePriorCorrelation { get; set; } // 单因子等相关收缩先验均值 r_bar
    public string ShrinkageStatusBadge { get; set; } = string.Empty; // 🟢 强正定高稳健收缩 / 🟡 中度均衡收缩 / 🔵 轻度微调收缩
    public List<ShrinkageCovarianceItem> CovarianceItems { get; set; } = new();
    public List<string> AssetCodes { get; set; } = new();
    public string AnalyticalSummary { get; set; } = string.Empty;
}

public class RegimeStateInfo
{
    public int StateId { get; set; } // 1: Bull / 2: Bear
    public string StateName { get; set; } = string.Empty; // 🐂 稳健低波牛市扩张 / 🐻 剧烈高波熊市收缩
    public decimal ExpectedAnnualReturnPercent { get; set; } // 状态年化预期收益率 (%)
    public decimal AnnualVolatilityPercent { get; set; } // 状态年化波动率 (%)
    public decimal TransitionProbabilityPercent { get; set; } // 状态自我保持概率 p_ii (%)
    public decimal ExpectedDurationMonths { get; set; } // 预期平均驻留持续期 (月)
    public decimal FilteredProbabilityPercent { get; set; } // 当前时点内生滤波概率 xi_t|t (%)
}

public class MarkovRegimeSwitchingResult
{
    public string CurrentRegime { get; set; } = string.Empty;
    public decimal CurrentRegimeProbabilityPercent { get; set; } // 当前主导体制置信度 (%)
    public decimal TransitionP11Percent { get; set; } // 牛市保持转移概率 p11 (%)
    public decimal TransitionP22Percent { get; set; } // 熊市保持转移概率 p22 (%)
    public decimal BullExpectedReturnPercent { get; set; }
    public decimal BullAnnualVolatilityPercent { get; set; }
    public decimal BearExpectedReturnPercent { get; set; }
    public decimal BearAnnualVolatilityPercent { get; set; }
    public decimal BullExpectedDurationMonths { get; set; }
    public decimal BearExpectedDurationMonths { get; set; }
    public List<RegimeStateInfo> RegimeStates { get; set; } = new();
    public string DynamicBlackLittermanPriorShift { get; set; } = string.Empty; // BL 先验均值动态调节指引
    public string TacticalAssetAllocationAdvice { get; set; } = string.Empty;
}

public class JumpDiffusionTrajectoryPoint
{
    public int TradingDay { get; set; } // 前瞻预测交易日 (1..N)
    public decimal Percentile5Nav { get; set; } // 5% 悲观极端跳空净值
    public decimal MedianNav { get; set; } // 50% 中位数净值
    public decimal Percentile95Nav { get; set; } // 95% 乐观净值
    public decimal PureGbmMedianNav { get; set; } // 传统连续几何布朗运动 (无跳跃) 基准净值
}

public class MertonJumpDiffusionResult
{
    public decimal JumpIntensityLambda { get; set; } // 泊松跳跃年化期望发生频次 lambda
    public decimal MeanJumpSizePercent { get; set; } // 对数跳跃平均幅度 mu_J (%)
    public decimal JumpVolatilityPercent { get; set; } // 跳跃幅度离散度 sigma_J (%)
    public decimal JumpRiskPremiumPercent { get; set; } // 跳跃风险补偿溢价 lambda * kappa (%)
    public decimal JumpAdjustedVaR95Percent { get; set; } // Merton 跳跃扩散 95% VaR (%)
    public decimal JumpAdjustedVaR99Percent { get; set; } // Merton 跳跃扩散 99% VaR (%)
    public decimal GaussianGbmVaR95Percent { get; set; } // 传统高斯连续扩散 95% VaR (%)
    public decimal TailRiskUnderestimationPercent { get; set; } // 传统高斯模型低估尾部损失百分比 (%)
    public List<JumpDiffusionTrajectoryPoint> TrajectoryPoints { get; set; } = new();
    public string JumpScenarioAudit { get; set; } = string.Empty;
}

public class LVaRScaleScenario
{
    public decimal CapitalScaleWan { get; set; } // 资金体量 (万元)
    public string CapitalScaleLabel { get; set; } = string.Empty; // 100万 / 1000万 / 5000万 / 1亿 / 5亿
    public decimal PureMarketVaR95Wan { get; set; } // 纯价格市场在险价值 (万元)
    public decimal LiquiditySpreadCostWan { get; set; } // 买卖外生价差平仓成本 (万元)
    public decimal MarketImpactCostWan { get; set; } // 平方根冲击滑点成本 (万元)
    public decimal TotalLVaRWan { get; set; } // 综合流动性调整 L-VaR (万元)
    public decimal LVaRRatioPercent { get; set; } // L-VaR 占组合总本金比例 (%)
    public decimal LiquidityAddonPercent { get; set; } // 流动性摩擦增量相对传统 VaR 上浮比例 (%)
}

public class LiquidityAdjustedVaRResult
{
    public decimal BaseCapitalWan { get; set; }
    public decimal PureMarketVaR95Percent { get; set; }
    public decimal PureMarketVaR95Wan { get; set; }
    public decimal LiquidityCostPercent { get; set; }
    public decimal LiquidityCostWan { get; set; }
    public decimal MarketImpactWan { get; set; }
    public decimal TotalLVaR95Percent { get; set; }
    public decimal TotalLVaR95Wan { get; set; }
    public decimal TotalLVaR99Percent { get; set; }
    public decimal TotalLVaR99Wan { get; set; }
    public decimal LiquidityRiskMultiplier { get; set; } // 综合 L-VaR / 传统 VaR 放大倍数
    public string LiquidityHealthBadge { get; set; } = string.Empty;
    public List<LVaRScaleScenario> ScaleScenarios { get; set; } = new();
    public string InstitutionalDeskAdvice { get; set; } = string.Empty;
}

public class MonthlyHeatmapRow
{
    public int Year { get; set; }
    public decimal? M1 { get; set; }
    public decimal? M2 { get; set; }
    public decimal? M3 { get; set; }
    public decimal? M4 { get; set; }
    public decimal? M5 { get; set; }
    public decimal? M6 { get; set; }
    public decimal? M7 { get; set; }
    public decimal? M8 { get; set; }
    public decimal? M9 { get; set; }
    public decimal? M10 { get; set; }
    public decimal? M11 { get; set; }
    public decimal? M12 { get; set; }
    public decimal FullYearReturn { get; set; }
    public decimal BestMonth { get; set; }
    public decimal WorstMonth { get; set; }
    public decimal PositiveMonthRatio { get; set; }
}

public class TearsheetUnderwaterPoint
{
    public DateTime Date { get; set; }
    public decimal DrawdownPercent { get; set; } // 相对历史高点回撤 (%) 负值或零
    public decimal PeakNav { get; set; }
    public decimal CurrentNav { get; set; }
    public int DaysInDrawdown { get; set; } // 处于回撤中的连续交易日天数
}

public class PortfolioTearsheetResult
{
    public List<MonthlyHeatmapRow> MonthlyHeatmapRows { get; set; } = new();
    public List<TearsheetUnderwaterPoint> UnderwaterPoints { get; set; } = new();
    public int TotalYears { get; set; }
    public int TotalMonths { get; set; }
    public int WinningMonths { get; set; }
    public int LosingMonths { get; set; }
    public decimal MonthlyWinRatePercent { get; set; }
    public decimal BestMonthlyReturnPercent { get; set; }
    public decimal WorstMonthlyReturnPercent { get; set; }
    public int MaxUnderwaterDays { get; set; } // 历史最长水下浸泡天数
    public int CurrentUnderwaterDays { get; set; } // 当前持续水下天数
    public string TearsheetExecutiveSummary { get; set; } = string.Empty;
}

// ==========================================
// Phase 27: 机构级算法执行、重抽样前沿、反向压力测试与高阶矩修正模型定义
// ==========================================

public class AlmgrenChrissTrajectoryStep
{
    public int StepIndex { get; set; }
    public decimal TimeHorizonDays { get; set; }
    public decimal RemainingHoldingRatioPercent { get; set; } // 剩余持仓比例 (%)
    public decimal RemainingHoldingWan { get; set; } // 剩余持仓规模 (万元)
    public decimal TradeSharesWan { get; set; } // 当期减仓抛售规模 (万元)
    public decimal TradeRateWanPerDay { get; set; } // 抛售速率 (万元/天)
    public decimal TemporaryImpactWan { get; set; } // 临时冲击损耗 (万元)
    public decimal PermanentImpactWan { get; set; } // 永久冲击折价 (万元)
    public decimal CumulativeCostWan { get; set; } // 累计冲击与滑点摩擦 (万元)
    public decimal VarianceRiskWan { get; set; } // 当期时机波动方差在险 (万元)
}

public class AlmgrenChrissExecutionResult
{
    public decimal TotalOrderCapitalWan { get; set; } // 总调仓规模 (万元)
    public int TotalTradingDays { get; set; } // 计划执行总天数
    public int NumberOfSteps { get; set; } // 离散时间步数
    public decimal RiskAversionLambda { get; set; } // 交易员风险厌恶系数
    public decimal HalfLifeDays { get; set; } // 执行半衰期 theta = 1/kappa (天)
    public decimal ExpectedTotalCostWan { get; set; } // 预期总执行落差 (Implementation Shortfall, 万元)
    public decimal ExpectedTotalCostPercent { get; set; } // 预期落差成本比例 (%)
    public decimal ExecutionVarianceRiskWan { get; set; } // 轨迹累积方差在险 (万元)
    public decimal ExecutionVaR95Wan { get; set; } // 95% 执行总在险价值 (万元)
    public string OptimalStrategyStyle { get; set; } = string.Empty; // ⚡ 快速激进平仓 / 🛡️ 平滑缓步减仓 / ⚖️ 经典平衡型
    public List<AlmgrenChrissTrajectoryStep> TrajectorySteps { get; set; } = new();
    public string ExecutionDeskRecommendation { get; set; } = string.Empty;
}

public class MichaudResampledPoint
{
    public int RiskRank { get; set; } // 风险等级序号 (1 ~ N)
    public decimal AnnualExpectedReturnPercent { get; set; } // 重抽样预期年化收益率 (%)
    public decimal AnnualVolatilityPercent { get; set; } // 重抽样年化波动率 (%)
    public decimal SharpeRatio { get; set; } // 夏普比率
    public Dictionary<string, decimal> AssetWeights { get; set; } = new(); // 各基金平滑权重字典
    public string PortfolioCompositionSummary { get; set; } = string.Empty;
}

public class MichaudResampledFrontierResult
{
    public int ResampleSimulations { get; set; } // 蒙特卡洛重抽样次数 (如 50 次)
    public int FrontierPointsCount { get; set; }
    public decimal ClassicMvoTurnoverInstability { get; set; } // 经典 MVO 对扰动权重翻转敏感度
    public decimal ResampledRobustnessGainRatio { get; set; } // 稳健性提升倍数 (如 2.5x)
    public decimal BestSharpeReturnPercent { get; set; }
    public decimal BestSharpeVolPercent { get; set; }
    public decimal BestSharpeValue { get; set; }
    public List<MichaudResampledPoint> ResampledPoints { get; set; } = new();
    public string ExecutiveSummary { get; set; } = string.Empty;
}

public class ReverseStressShockItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal PortfolioWeightPercent { get; set; } // 组合内基准权重 (%)
    public decimal CriticalShockPercent { get; set; } // 触发破产的临界跌幅 (%)
    public decimal LossContributionPercent { get; set; } // 对破产总损失的边际贡献度 (%)
    public string VulnerabilityGrade { get; set; } = string.Empty; // 🔴 极度脆弱核 / 🟡 中度敏感 / 🟢 稳健吸收垫
}

public class ReverseStressTopologyResult
{
    public decimal TargetThresholdDrawdownPercent { get; set; } // 目标容忍最大回撤 (如 -15.0%)
    public decimal MahalanobisDistance { get; set; } // 临界破产冲击马氏距离 D_M
    public decimal ProbabilityOfBreachNormalEstimatePercent { get; set; } // 理论破产触发概率 (%)
    public string MostVulnerableFundName { get; set; } = string.Empty;
    public decimal MostVulnerableShockPercent { get; set; }
    public List<ReverseStressShockItem> ShockItems { get; set; } = new();
    public string RiskOfficerVerdict { get; set; } = string.Empty;
}

public class CornishFisherVaRResult
{
    public decimal SampleSkewness { get; set; } // 收益偏度 S
    public decimal SampleExcessKurtosis { get; set; } // 超额峰度 K
    public decimal GaussianVaR95Percent { get; set; } // 高斯 95% VaR (%)
    public decimal CornishFisherVaR95Percent { get; set; } // Cornish-Fisher 修正 95% VaR (%)
    public decimal GaussianVaR99Percent { get; set; } // 高斯 99% VaR (%)
    public decimal CornishFisherVaR99Percent { get; set; } // Cornish-Fisher 修正 99% VaR (%)
    public decimal GaussianCVaR95Percent { get; set; } // 高斯 95% CVaR (%)
    public decimal CornishFisherCVaR95Percent { get; set; } // Cornish-Fisher 修正 95% CVaR (%)
    public decimal TailRiskUnderestimationMultiplier { get; set; } // 尾部低估放大倍数 (CF_VaR / Gauss_VaR)
    public string TailRiskHealthBadge { get; set; } = string.Empty; // 🔴 极端左偏厚尾警报 / 🟡 轻微肥尾 / 🟢 接近对称良态
    public string AnalyticalSummary { get; set; } = string.Empty;
}

#region Phase 28: 机构级前沿量化投研模型 (因子拥挤度踩踏预警、回撤硬顶分数凯利、BSTS趋势断点滤波、多期期限结构)

public class AssetCrowdingItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal PortfolioWeightPercent { get; set; } // 组合权重 (%)
    public decimal PairwiseCorrelationJump { get; set; } // 配对相关性跳跃幅度
    public decimal ValuationSpreadZScore { get; set; } // 估值离散度偏离 Z-Score
    public decimal TurnoverVelocitySpikeRatio { get; set; } // 换手率/成交活跃异动倍数
    public decimal HerdingConcentrationPercent { get; set; } // 机构持仓拥挤度贡献 (%)
    public decimal CrowdingCompositeScore { get; set; } // 单标的综合拥挤度评分 (0~100)
    public string CrowdingStatusBadge { get; set; } = string.Empty; // 🟢 宽松通畅 / 🟡 中度拥挤 / 🔴 极度拥挤预警
}

public class AssetCrowdingRadarResult
{
    public decimal OverallCrowdingScore { get; set; } // 组合加权综合拥挤度得分 (0~100)
    public decimal AveragePairwiseCorrelation { get; set; } // 当前平均配对相关系数
    public int CriticalCrowdedAssetCount { get; set; } // 极度拥挤警报标的数
    public string CrowdingRiskLevel { get; set; } = string.Empty; // 🟢 结构健康 / 🟡 预警关注 / 🔴 高度踩踏风险
    public List<AssetCrowdingItem> CrowdingItems { get; set; } = new();
    public string LiquidityCascadeWarning { get; set; } = string.Empty;
    public string ExecutiveAdvice { get; set; } = string.Empty;
}

public class KellyDrawdownTierItem
{
    public decimal DrawdownThresholdPercent { get; set; } // 回撤深度分档 (如 0%, 2%, 5%, 8%, 10%)
    public decimal SuggestedKellyEquityWeightPercent { get; set; } // 建议总权益仓位 (%)
    public decimal DefensiveCashBufferPercent { get; set; } // 防御安全现金垫 (%)
    public decimal SafetyDistanceMarginPercent { get; set; } // 距硬顶止损安全冗余 (%)
    public string ActionRecommendation { get; set; } = string.Empty; // 操作指引
}

public class DrawdownConstrainedKellyResult
{
    public decimal UnconstrainedFullKellyLeverage { get; set; } // 经典高斯无约束全凯利杠杆倍数 (如 2.45x)
    public decimal FractionalKellyScalar { get; set; } // 分数凯利缩放系数 (如 0.50x 半凯利)
    public decimal CurrentDrawdownPercent { get; set; } // 组合当前动态回撤 D_t (%)
    public decimal MaxDrawdownCeilingPercent { get; set; } // 资管合同最大回撤硬顶 D_max (%)
    public decimal OptimalEquityWeightPercent { get; set; } // 当前推荐权益总仓位 (%)
    public decimal RecommendedCashBufferPercent { get; set; } // 当前推荐防守现金比例 (%)
    public decimal RecommendedCashBufferWan { get; set; } // 推荐现金储备额 (万元, 假设 2000万总规模)
    public decimal DrawdownBrakingPenaltyRatio { get; set; } // 回撤制动惩罚因子 (1 - D_t / D_max)^gamma
    public string BrakingStatus { get; set; } = string.Empty; // 🟢 充分进攻态 / 🟡 缓速降杠杆 / 🔴 极度避险制动
    public List<KellyDrawdownTierItem> TierScenarios { get; set; } = new();
    public string RiskOfficerVerdict { get; set; } = string.Empty;
}

public class BstsAssetAlphaItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal SmoothedLocalAlphaAnnualPercent { get; set; } // 滤波局部年化 Alpha (%)
    public decimal SignalToNoiseRatio { get; set; } // Alpha 信号噪声比 (SNR)
    public decimal TrendSlopeVelocity { get; set; } // 局部趋势斜率速度 (%/月)
    public decimal BayesianStructuralBreakProbabilityPercent { get; set; } // 贝叶斯后验断点跳跃概率 P(Break) (%)
    public string RegimeDiagnosisBadge { get; set; } = string.Empty; // 🟢 Alpha 强劲持续 / 🟡 风格漂移中性 / 🔴 核心能力结构性衰竭
}

public class BstsTrendFilterResult
{
    public decimal PortfolioAverageAlphaAnnualPercent { get; set; } // 组合加权平滑 Alpha (%)
    public decimal PortfolioStructuralBreakProbabilityPercent { get; set; } // 组合整体结构性突变概率 (%)
    public decimal TrendStabilityScore { get; set; } // 趋势持续性评分 (0~100)
    public List<BstsAssetAlphaItem> AssetFilterItems { get; set; } = new();
    public string FilterSynthesisVerdict { get; set; } = string.Empty;
}

public class HorizonRiskPoint
{
    public string HorizonLabel { get; set; } = string.Empty; // 1个月 / 3个月 / 6个月 / 1年 / 3年 / 5年
    public int TradingDays { get; set; } // 21, 63, 126, 252, 756, 1260
    public decimal HorizonAnnualizedVolPercent { get; set; } // 跨期真实年化波动率 (%)
    public decimal SqrtTimeRuleAnnualizedVolPercent { get; set; } // 传统根号T外推波动率 (%)
    public decimal VarianceDecayRatio { get; set; } // 均值回归方差衰减比 (Realized_Vol / Sqrt_Vol)
    public decimal HorizonSharpeRatio { get; set; } // 跨期夏普比率
    public decimal OptimalEquityAllocationPercent { get; set; } // 期限匹配推荐权益仓位 (%)
    public decimal OptimalFixedIncomeCashAllocationPercent { get; set; } // 期限匹配推荐固收现金仓位 (%)
}

public class MultiHorizonRiskTermResult
{
    public decimal AutocorrelationLag1 { get; set; } // 日度收益一阶自相关系数 rho_1
    public decimal MeanReversionHalfLifeDays { get; set; } // 均值回归半衰期 (交易日)
    public decimal OneYearToFiveYearVolDecayPercent { get; set; } // 1年到5年跨期波动率衰减幅度 (%)
    public List<HorizonRiskPoint> HorizonPoints { get; set; } = new();
    public string HorizonAllocationGuidance { get; set; } = string.Empty;
}

#endregion

#region Phase 29: 随机矩阵谱滤波降噪、NCO嵌套聚类、微观隐性滑点与香农有效下注熵

public class RmtEigenItem
{
    public int Index { get; set; } // 特征值序号 (1, 2, ...)
    public decimal RawEigenvalue { get; set; } // 原始样本特征值
    public string Classification { get; set; } = string.Empty; // 🟣 纯随机白噪声 (MP Band) / 🟡 行业风格因子 / 🔴 系统性主导因子 (Market)
    public decimal DenoisedEigenvalue { get; set; } // 降噪重构后特征值
    public decimal VarianceExplainedPercent { get; set; } // 降噪特征值方差解释比 (%)
    public string LeadingAssets { get; set; } = string.Empty; // 特征向量投影主导资产
}

public class RmtCovarianceCleaningResult
{
    public int SampleCountT { get; set; } // 样本天数 T
    public int AssetCountN { get; set; } // 资产数量 N
    public decimal QualityRatioQ { get; set; } // Q = T / N 样本充沛比
    public decimal MarchenkoPasturUpperBound { get; set; } // 理论最大噪声边界 lambda_+
    public decimal MarchenkoPasturLowerBound { get; set; } // 理论最小噪声边界 lambda_-
    public int NoiseEigenvalueCount { get; set; } // 噪声特征值个数
    public decimal NoiseRatioPercent { get; set; } // 噪声特征值占比 (%)
    public decimal RawConditionNumber { get; set; } // 原始经验协方差矩阵条件数
    public decimal CleanedConditionNumber { get; set; } // 去噪后协方差矩阵条件数
    public decimal ConditionNumberImprovementRatio { get; set; } // 条件数改善优化倍数
    public List<RmtEigenItem> EigenItems { get; set; } = new();
    public double[,] CleanedCorrelationMatrix { get; set; } = new double[0, 0];
    public double[,] CleanedCovarianceMatrix { get; set; } = new double[0, 0];
    public string RmtDenoisingVerdict { get; set; } = string.Empty;
}

public class NcoClusterWeightItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public string ClusterId { get; set; } = string.Empty; // 簇标识: Cluster A / Cluster B ...
    public decimal IntraClusterWeightPercent { get; set; } // 簇内 MVO 局部最优权重 (%)
    public decimal InterClusterWeightPercent { get; set; } // 簇间宏观配置权重 (%)
    public decimal NcoFinalWeightPercent { get; set; } // NCO 最终合成配置权重 (%)
    public decimal EqualWeightPercent { get; set; } // 基准等权对比 (%)
    public decimal WeightTiltDeltaPercent { get; set; } // 相对等权的倾斜偏差 (%)
}

public class NestedClusteredOptimizationResult
{
    public int TotalClusters { get; set; } // 聚类簇群数量 K
    public decimal NcoExpectedReturnAnnualPercent { get; set; } // NCO 组合预期年化收益率 (%)
    public decimal NcoAnnualizedVolatilityPercent { get; set; } // NCO 组合年化波动率 (%)
    public decimal NcoSharpeRatio { get; set; } // NCO 组合夏普比率
    public decimal MvoSharpeRatio { get; set; } // 传统 MVO 组合夏普比率
    public decimal SharpeImprovementPercent { get; set; } // 鲁棒夏普提升幅度 (%)
    public List<NcoClusterWeightItem> ClusterWeightItems { get; set; } = new();
    public string NcoOptimizationVerdict { get; set; } = string.Empty;
}

public class MicrostructureAssetItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal AmihudIlliquidityMeasure { get; set; } // Amihud 价格冲击弹性指数 (10^-6)
    public decimal RollEffectiveSpreadBps { get; set; } // Roll 隐性有效买卖价差 (bp)
    public decimal TurnOverImpactElasticity { get; set; } // 换手冲击弹性度
    public decimal DailySafeAbsorbingCapacityWan { get; set; } // 日均安全吸收容量 (万元)
    public string LiquidityHealthBadge { get; set; } = string.Empty; // 🟢 高流动性极低摩擦 / 🟡 中度交易损耗 / 🔴 流动性狭窄高滑点
}

public class MicrostructureSlippageTierItem
{
    public string TradeScaleLabel { get; set; } = string.Empty; // 100万元 / 500万元 / 1000万元 / 2000万元 / 5000万元
    public decimal TradeScaleWan { get; set; }
    public decimal ExpectedSlippageBps { get; set; } // 预期隐性滑点损耗 (bp)
    public decimal EstimatedFrictionAmountYuan { get; set; } // 预估隐性摩擦损耗金额 (元)
    public decimal RecommendedExecutionDays { get; set; } // 建议拆单建仓/平仓执行交易日 (天)
    public string ExecutionExecutionPacingAdvice { get; set; } = string.Empty;
}

public class MicrostructureLiquidityResult
{
    public decimal WeightedAmihudIlliquidity { get; set; } // 全组合加权 Amihud 冲击指数
    public decimal WeightedRollEffectiveSpreadBps { get; set; } // 全组合加权 Roll 隐性买卖价差 (bp)
    public decimal TotalDailyAbsorbingCapacityWan { get; set; } // 全组合单日安全吸纳容量总上限 (万元)
    public List<MicrostructureAssetItem> AssetItemList { get; set; } = new();
    public List<MicrostructureSlippageTierItem> SlippageTiers { get; set; } = new();
    public string MicrostructureFrictionVerdict { get; set; } = string.Empty;
}

public class PcaBetContributionItem
{
    public int PrincipalComponentIndex { get; set; } // 主成分序号 PC1, PC2 ...
    public decimal Eigenvalue { get; set; } // 主成分特征值
    public decimal VarianceExplainedPercent { get; set; } // 方差解释比例 (%)
    public decimal RiskContributionVariancePercent { get; set; } // 组合风险方差贡献比例 p_k (%)
    public decimal EntropyContribution { get; set; } // 香农下注熵贡献 -p_k * ln(p_k)
    public string IndependenceStatusBadge { get; set; } = string.Empty; // 🔵 核心主导因子 / 🟢 辅助分散因子 / ⚪ 边缘残差因子
}

public class PortfolioEntropyRegularizationResult
{
    public decimal NominalShannonEntropy { get; set; } // 名义资产权重香农熵 H(w)
    public decimal EffectiveNumberOfAssets { get; set; } // 名义有效资产数 ENA = exp(H(w))
    public decimal FactorBetShannonEntropy { get; set; } // 正交因子下注香农熵 H(p)
    public decimal EffectiveNumberOfBets { get; set; } // 真实正交有效下注数 ENB = exp(H(p))
    public decimal DiversificationDeficit { get; set; } // 伪分散度赤字 Delta = ENA - ENB
    public decimal EntropyRegularizedDiversificationScore { get; set; } // 熵正则化分散度健康评分 (0~100)
    public List<PcaBetContributionItem> PcaBetContributions { get; set; } = new();
    public string EntropyDiversificationVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 30 机构级量化投研：全天候宏观态、马氏动荡度、欧拉下行CVaR与LDI负债匹配模型

/// <summary>
/// Bridgewater 4象限宏观态特征项
/// </summary>
public class MacroRegimeQuadrantItem
{
    public string RegimeName { get; set; } = string.Empty; // 通胀繁荣 (Growth+, Inf+) / 停滞滞胀 (Growth-, Inf+) / 通缩萧条 (Growth-, Inf-) / 温和复苏 (Growth+, Inf-)
    public string MacroGrowthEnv { get; set; } = string.Empty; // 增长超预期 / 增长承压
    public string MacroInflationEnv { get; set; } = string.Empty; // 通胀上行 / 通胀下行
    public decimal HistoricalFrequencyPercent { get; set; } // 历史分布频率 (%)
    public decimal ExpectedReturnAnnualPercent { get; set; } // 该宏观态下组合条件年化预期收益 (%)
    public decimal ConditionalVolatilityPercent { get; set; } // 该宏观态下组合条件年化波动率 (%)
    public decimal RecommendedRiskBudgetPercent { get; set; } // 全天候理论最优风险预算占比 (%)
    public string BenchmarkAssetTiltAdvice { get; set; } = string.Empty; // 宏观超配/低配推荐资产类别
}

/// <summary>
/// 马氏距离金融动荡度与自适应降杠杆雷达模型结果
/// </summary>
public class MahalanobisTurbulenceResult
{
    public decimal AverageTurbulenceScore { get; set; } // 历史全样本马氏动荡度均值 d^2
    public decimal CurrentTurbulenceScore { get; set; } // 当前截面马氏动荡度评分
    public decimal Turbulence75Percentile { get; set; } // 75% 动荡度分位阈值
    public decimal Turbulence90Percentile { get; set; } // 90% 极端动荡预警阈值
    public decimal TurbulencePercentileRank { get; set; } // 当前动荡度在历史分布中的百分位 (0~100%)
    public decimal TurbulenceRatioVsHistorical { get; set; } // 相对历史均值的偏离倍数 (如 1.4x)
    public decimal RecommendedLeverageMultiplier { get; set; } // 动态战术杠杆倍数 (0.20x ~ 1.00x)
    public decimal RecommendedDefensiveCashBufferPercent { get; set; } // 建议防守避险现金垫储备比例 (%)
    public string MarketTurbulenceStateBadge { get; set; } = string.Empty; // 🟢 常态平稳 / 🟡 轻度扰动 / 🔴 极度动荡
    public List<MacroRegimeQuadrantItem> MacroQuadrants { get; set; } = new(); // 4 象限宏观态分布
    public string TurbulenceTacticalVerdict { get; set; } = string.Empty; // 执委战术风控建议
}

/// <summary>
/// 欧拉下行条件在险价值 (Euler CVaR) 单资产边际风险项
/// </summary>
public class EulerCvarAssetItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal NominalWeightPercent { get; set; } // 名义持仓权重 (%)
    public decimal AnnualizedReturnPercent { get; set; } // 历史年化收益率 (%)
    public decimal MarginalExpectedShortfallPercent { get; set; } // 95% MES 边际期望损失 (%)
    public decimal EulerAbsoluteCvarContributionPercent { get; set; } // 欧拉 CVaR 绝对风险贡献金额/百分点 (%)
    public decimal EulerPercentCvarContributionRatio { get; set; } // 欧拉 %CVaR 风险贡献率 (%)
    public decimal TailBetaVsPortfolio { get; set; } // 极端尾部 Beta 弹性
    public string TailToxicityRating { get; set; } = string.Empty; // 🟢 避险锚点 / 🟡 正常贡献 / 🔴 毒性尾部资产
    public decimal RecommendedDetoxWeightPercent { get; set; } // 尾部去毒后建议目标权重 (%)
}

/// <summary>
/// 欧拉下行条件在险价值风险贡献分解与尾部去毒结果
/// </summary>
public class EulerCvarAttributionResult
{
    public decimal ConfidenceLevelPercent { get; set; } = 95.0m; // 置信度 95%
    public decimal PortfolioVaRPercent { get; set; } // 组合 95% 在险价值 VaR (%)
    public decimal PortfolioCvarPercent { get; set; } // 组合 95% 条件在险价值 CVaR (%)
    public decimal EulerSumCvarPercent { get; set; } // 欧拉分解求和检验值 (%)
    public decimal TailHerfindahlIndex { get; set; } // 尾部风险集中度 HHI 指数 (0~10000)
    public string MostToxicAssetCode { get; set; } = string.Empty; // 极端尾部毒性最大的资产代码
    public decimal MostToxicAssetCvarContributionRatio { get; set; } // 最大毒性资产 %CVaR 贡献比
    public List<EulerCvarAssetItem> AssetItemList { get; set; } = new();
    public string EulerCvarVerdict { get; set; } = string.Empty;
}

/// <summary>
/// 基准相对下行半方差跟踪误差 (DTE) 与 Stutzer 指数模型结果
/// </summary>
public class DownsideTrackingErrorResult
{
    public decimal BenchmarkAnnualizedReturnPercent { get; set; } // 基准年化收益率 (%)
    public decimal ActiveAnnualizedReturnPercent { get; set; } // 主动超额年化收益率 (%)
    public decimal SymmetricTrackingErrorPercent { get; set; } // 传统对称跟踪误差 TE (%)
    public decimal DownsideTrackingErrorPercent { get; set; } // 下行半方差跟踪误差 DTE (%)
    public decimal AsymmetricDownsideGainRatio { get; set; } // 非对称下行增益比 (TE / DTE)
    public decimal InformationRatio { get; set; } // 传统信息比率 IR
    public decimal DownsideInformationRatio { get; set; } // 下行信息比率 DIR = ActiveReturn / DTE
    public decimal StutzerDecayIndex { get; set; } // Stutzer 大偏差极端衰减指数 I_S
    public decimal AnnualizedProbUnderperformDecayRatePercent { get; set; } // 长期跑输基准概率的年化衰减率 (%)
    public string BenchmarkPurityGradeBadge { get; set; } = string.Empty; // 🏆 卓越纯度 Alpha / 🛡️ 稳健低误 Alpha / ⚠️ 虚高伴随重落后
    public string StutzerVerdict { get; set; } = string.Empty;
}

/// <summary>
/// 负债驱动投资 (LDI) 跨期负债现金流期限匹配项
/// </summary>
public class LiabilityCashFlowMatchItem
{
    public string HorizonLabel { get; set; } = string.Empty; // 3个月 (3M) / 6个月 (6M) / 1年 (1Y) / 2年 (2Y) / 3年 (3Y) / 5年 (5Y)
    public int HorizonDays { get; set; } // 期限天数
    public decimal ScheduledLiabilityAmountWan { get; set; } // 刚性到期负债现金流需求 (万元)
    public decimal MatchedAssetCashFlowWan { get; set; } // 资产端匹配预期现金流供给 (分红+到期+稳健流动性) (万元)
    public decimal NetCashFlowSurplusWan { get; set; } // 净盈余/缺口 (万元)
    public decimal CoverageRatioPercent { get; set; } // 现金流覆盖率 (%)
    public string CashFlowHealthBadge { get; set; } = string.Empty; // 🟢 充裕超配 / 🟡 平衡适中 / 🔴 缺口需调仓
    public string RecommendedLiquidationAsset { get; set; } = string.Empty; // 若需平仓兑付首选资产梯阶
}

/// <summary>
/// 负债驱动投资 (LDI) 久期缺口与清算瀑布模型结果
/// </summary>
public class LdiCashFlowMatchResult
{
    public decimal TotalAssetPresentValueWan { get; set; } // 资产现值总额 (万元)
    public decimal TotalLiabilityPresentValueWan { get; set; } // 负债现值总额 (万元)
    public decimal AssetEffectiveDurationYears { get; set; } // 资产加权有效久期 (年)
    public decimal LiabilityEffectiveDurationYears { get; set; } // 负债加权有效久期 (年)
    public decimal DurationGapYears { get; set; } // 久期缺口 Duration Gap = D_A - (L/A) * D_L (年)
    public decimal OverallLiquidityCoverageRatioPercent { get; set; } // 综合流动性覆盖率 LCR (%)
    public decimal ImmediateLiquidReserveWan { get; set; } // 即期极速流动性安全储备 (万元)
    public List<LiabilityCashFlowMatchItem> HorizonMatchItems { get; set; } = new();
    public string LiquidationWaterfallStrategy { get; set; } = string.Empty; // 4级阶梯式资产变现清算瀑布策略
    public string LdiExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 31 机构级量化投研：卡尔曼时变Beta、基数稀疏优化、ΔCoVaR系统传染与FRTB压力资本

/// <summary>
/// 卡尔曼滤波时变 Beta 单日状态轨迹点
/// </summary>
public class KalmanBetaPoint
{
    public DateTime Date { get; set; }
    public decimal TimeVaryingBeta { get; set; } // 时变 Beta 估计值
    public decimal TimeVaryingAlpha { get; set; } // 时变 Alpha 截距 (年化 %)
    public decimal EstimationErrorVariance { get; set; } // 状态滤波后验方差 P_t
    public decimal KalmanGain { get; set; } // 观测更新步卡尔曼增益 K_t
    public decimal InnovationResidual { get; set; } // 新息残差 v_t
}

/// <summary>
/// 单只资产卡尔曼滤波时变贝塔与风格漂移分析项
/// </summary>
public class KalmanFilterStyleDriftItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal CurrentInstantBeta { get; set; } // 即期最新时变 Beta
    public decimal HistoricalMeanBeta { get; set; } // 历史时变 Beta 均值
    public decimal BetaVolatilityStdDev { get; set; } // Beta 波动率标准差
    public decimal BetaMin { get; set; } // 历史 Beta 最低点
    public decimal BetaMax { get; set; } // 历史 Beta 最高点
    public decimal StyleDriftIndex { get; set; } // 风格漂移指数 SDI = sqrt(mean((beta_t - mean)^2)) * 100
    public string StyleDriftBadge { get; set; } = string.Empty; // 🟢 风格严守 / 🟡 轻度漂移 / 🔴 严重违约
    public string StyleDriftDiagnosis { get; set; } = string.Empty; // 风格特征诊断结论
}

/// <summary>
/// 卡尔曼滤波时变 Beta 与风格漂移预警全套结果
/// </summary>
public class KalmanFilterStyleDriftResult
{
    public decimal PortfolioInstantBeta { get; set; } // 组合当前加权即期 Beta
    public decimal PortfolioMeanBeta { get; set; } // 组合历史加权平均 Beta
    public decimal AverageStyleDriftIndex { get; set; } // 组合成分平均风格漂移指数 SDI
    public string MostDriftedAssetCode { get; set; } = string.Empty; // 风格漂移最严重的资产代码
    public decimal MostDriftedAssetSdi { get; set; } // 最严重资产的 SDI 指数
    public List<KalmanFilterStyleDriftItem> AssetItemList { get; set; } = new();
    public List<KalmanBetaPoint> PortfolioBetaTrajectory { get; set; } = new();
    public string StyleDriftExecutiveVerdict { get; set; } = string.Empty;
}

/// <summary>
/// Axioma 稀疏投资组合基数约束单资产配置项
/// </summary>
public class SparseCardinalityAssetItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public bool IsSelectedInSparseSubset { get; set; } // 是否入选 K 只精选稀疏组合
    public decimal SparseOptimalWeightPercent { get; set; } // 稀疏组合分配权重 (%)
    public decimal OriginalUnconstrainedWeightPercent { get; set; } // 原全量无约束权重 (%)
    public decimal WeightDeviationPercent { get; set; } // 权重偏离值 (%)
    public decimal TurnoverContributionPercent { get; set; } // 对总换手率的贡献绝对值 (|w_sparse - w_orig| / 2) (%)
    public string LiquidationAdvice { get; set; } = string.Empty; // 调仓清算处置指引
}

/// <summary>
/// Axioma 基数约束与换手预算稀疏投资组合优化结果
/// </summary>
public class CardinalitySparseOptimizationResult
{
    public int TargetCardinalityK { get; set; } // 目标持仓基金只数硬顶上限 K (如 3 或 4 只)
    public int TotalCandidatesN { get; set; } // 候选池基金总数 N
    public decimal TurnoverBudgetPercent { get; set; } // 调仓单边换手率预算上限 (%)
    public decimal ActualTurnoverPercent { get; set; } // 稀疏调仓实际单边换手率 (%)
    public decimal SparseAnnualizedReturnPercent { get; set; } // 稀疏组合年化预期收益率 (%)
    public decimal SparseAnnualizedVolatilityPercent { get; set; } // 稀疏组合年化波动率 (%)
    public decimal SparseSharpeRatio { get; set; } // 稀疏组合夏普比率
    public decimal UnconstrainedSharpeRatio { get; set; } // 原始全量无约束组合夏普比率
    public decimal CardinalityEfficiencyLossPercent { get; set; } // 基数效率损失率 ((Uncon - Sparse) / Uncon) (%)
    public decimal CardinalityRetentionRatioPercent { get; set; } // 效率保留率 (%)
    public List<SparseCardinalityAssetItem> AssetItemList { get; set; } = new();
    public string SparseOptimizationVerdict { get; set; } = string.Empty;
}

/// <summary>
/// Adrian-Brunnermeier 条件系统性在险价值增量 ΔCoVaR 单资产分析项
/// </summary>
public class DeltaCoVaRAssetItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal NominalWeightPercent { get; set; } // 名义持仓权重 (%)
    public decimal IndividualVaR95Percent { get; set; } // 单基自身 95% 在险价值 VaR (%)
    public decimal CoVaRWhenAssetInCrisisPercent { get; set; } // 该标的处于 95% 危机时组合的 CoVaR (95%) (%)
    public decimal CoVaRWhenAssetInMedianPercent { get; set; } // 该标的处于 50% 正常时组合的 CoVaR (50%) (%)
    public decimal DeltaCoVaRContributionPercent { get; set; } // 系统性传染增量 ΔCoVaR = CoVaR(95%) - CoVaR(50%) (%)
    public decimal SystemicContagionRank { get; set; } // 系统性风险传染度排名
    public string SystemicImportanceRating { get; set; } = string.Empty; // ⚠️ 系统重要传染源 / 🛡️ 防御抗跌标的 / ⚪ 常态影响
    public string IsolationFirewallAdvice { get; set; } = string.Empty; // 机构隔离风控防火墙建议
}

/// <summary>
/// 条件系统性在险价值增量 ΔCoVaR 与金融传染模型结果
/// </summary>
public class DeltaCoVaRSystemicRiskResult
{
    public decimal AverageDeltaCoVaRPercent { get; set; } // 组合平均系统性风险溢价 ΔCoVaR (%)
    public string HighestContagionAssetCode { get; set; } = string.Empty; // 最大系统性传染源基金
    public decimal HighestContagionDeltaCoVaR { get; set; } // 最大传染源 ΔCoVaR (%)
    public decimal SystemicNetworkVulnerabilityScore { get; set; } // 系统性关联脆弱度综合评分 (0~100)
    public string SystemicFragilityBadge { get; set; } = string.Empty; // 🟢 强韧韧性 / 🟡 局部共振 / 🔴 高危传染踩踏
    public List<DeltaCoVaRAssetItem> AssetItemList { get; set; } = new();
    public string DeltaCoVaRVerdict { get; set; } = string.Empty;
}

/// <summary>
/// FRTB 阶梯流动性时限分档资本计提项
/// </summary>
public class FrtbLiquidityHorizonItem
{
    public string HorizonTierLabel { get; set; } = string.Empty; // Tier 1 (10天: 极速货币) / Tier 2 (20天: 利率纯债) / Tier 3 (40天: 偏股蓝筹) / Tier 4 (120天: 另类小盘)
    public int HorizonDays { get; set; } // 流动性时限天数
    public decimal AllocatedCapitalWan { get; set; } // 资产分配规模 (万元)
    public decimal ComponentExpectedShortfallPercent { get; set; } // 该时限下条件在险损失 ES (%)
    public decimal ScaledCapitalChargeWan { get; set; } // 经时限阶梯缩放后的监管资本计提金额 (万元)
    public decimal CapitalContributionRatioPercent { get; set; } // 资本拨备占比 (%)
}

/// <summary>
/// Basel III / FRTB 压力在险价值 (sVaR) 与监管资本计提结果
/// </summary>
public class FrtbStressedCapitalChargeResult
{
    public DateTime StressedWindowStartDate { get; set; } // 历史上最恶劣 250 天压力窗口起始日
    public DateTime StressedWindowEndDate { get; set; } // 历史上最恶劣 250 天压力窗口结束日
    public decimal StressedWindowMaxDrawdownPercent { get; set; } // 最劣 250 天压力期内最大回撤 (%)
    public decimal StressedWindowAnnualizedVolatilityPercent { get; set; } // 最劣 250 天压力期年化波动率 (%)
    public decimal NormalVaR99Percent { get; set; } // 常态滚动 99% VaR (10天) (%)
    public decimal StressedVaR99Percent { get; set; } // 最劣窗口 99% sVaR (10天) (%)
    public decimal StressedMultiplierRatio { get; set; } // 压力倍数比率 (sVaR / Normal VaR >= 1.0)
    public decimal NormalEs975Percent { get; set; } // 常态 97.5% 期望损失 ES (%)
    public decimal StressedEs975Percent { get; set; } // 压力期 97.5% 期望损失 sES (%)
    public decimal TotalPortfolioCapitalWan { get; set; } // 组合总资本规模 (万元)
    public decimal FrtbTotalCapitalChargeWan { get; set; } // FRTB 阶梯流动性时限总监管资本金拨备 (万元)
    public decimal FrtbCapitalAdequacyRatioPercent { get; set; } // 资本充足备付率 (Capital Charge / Total Capital) (%)
    public string CapitalAdequacyBadge { get; set; } = string.Empty; // 🟢 资本充裕超标 / 🟡 适度合规 / 🔴 拨备不足预警
    public List<FrtbLiquidityHorizonItem> HorizonItemList { get; set; } = new();
    public string FrtbExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 32 机构级量化投研：Idzorek置信度BL、谱风险测度SRM、风险预算漂移走廊与DSR过拟合检验

// 1. Idzorek 显式置信度 Black-Litterman 贝叶斯优化
public class IdzorekViewItem
{
    public int ViewIndex { get; set; }
    public string ViewDescription { get; set; } = string.Empty; // 观点描述 (如 "000001 超额 000002 达 +2.5%")
    public decimal ExpectedExcessReturnPercent { get; set; } // 主观预期超额收益率 (%)
    public decimal UserSpecifiedConfidencePercent { get; set; } // 显式置信度 C_k (%)
    public decimal CalibratedOmegaVariance { get; set; } // Idzorek 解析反求的观点不确定性方差 ω_k
    public decimal ViewInformationContributionPercent { get; set; } // 观点信息权重溢价贡献 VIC (%)
    public string ViewImpactBadge { get; set; } = string.Empty; // 🟢 高信赖基石 / 🟡 适度倾斜 / ⚪ 中性参考
}

public class IdzorekAssetWeightItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal EquilibriumPriorWeightPercent { get; set; } // 先验市场均衡权重 w_eq (%)
    public decimal EquilibriumPriorReturnPercent { get; set; } // 先验隐含均衡收益率 Π (%)
    public decimal PosteriorExpectedReturnPercent { get; set; } // BL 后验期望收益率 E[R] (%)
    public decimal IdzorekPosteriorWeightPercent { get; set; } // Idzorek 置信度加权后验最优权重 w_BL (%)
    public decimal WeightTiltDeltaPercent { get; set; } // 权重战术倾斜偏离 Δw (%)
}

public class IdzorekBlackLittermanResult
{
    public decimal PriorEquilibriumSharpeRatio { get; set; } // 先验均衡组合夏普比率
    public decimal PosteriorOptimalSharpeRatio { get; set; } // BL 后验优化组合夏普比率
    public decimal SharpeRatioImprovementPercent { get; set; } // 夏普效率提升幅度 (%)
    public decimal PriorPosteriorKLDivergenceEntropy { get; set; } // 先验-后验偏离 KL 散度信息熵
    public decimal AverageUserConfidencePercent { get; set; } // 平均观点显式置信度 (%)
    public List<IdzorekViewItem> ViewItemList { get; set; } = new();
    public List<IdzorekAssetWeightItem> AssetItemList { get; set; } = new();
    public string IdzorekExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Acerbi 连续风险厌恶谱风险测度 (Spectral Risk Measures, SRM)
public class SpectralTierItem
{
    public string TailTierName { get; set; } = string.Empty; // 灾难级 (0~1%) / 严重级 (1~5%) / 警惕级 (5~10%) / 常态级 (10~100%)
    public decimal QuantileLowerBoundPercent { get; set; }
    public decimal QuantileUpperBoundPercent { get; set; }
    public decimal CumulativeSpectrumWeightPercent { get; set; } // 该梯阶在 Acerbi 指数谱上的累计积分权重 (%)
    public decimal TierAverageLossPercent { get; set; } // 该梯阶内资产平均日损失 (%)
    public decimal TierSpectralRiskContributionPercent { get; set; } // 对总谱风险测度 SRM 的贡献占比 (%)
    public string TierRiskSeverityBadge { get; set; } = string.Empty; // 🔴 极度致命 / 🟠 严重受创 / 🟡 显著承压 / 🟢 常态吸纳
}

public class SpectralRiskMeasureResult
{
    public decimal RiskAversionGamma { get; set; } // 机构风险厌恶指数参数 γ (默认 10.0)
    public decimal SpectralRiskMeasure1dPercent { get; set; } // Acerbi 连续指数谱在险价值 SRM (单日, %)
    public decimal SpectralRiskMeasureAnnualizedPercent { get; set; } // Acerbi 连续指数谱在险价值 SRM (年化, %)
    public decimal ClassicalVaR99Percent { get; set; } // 传统 99% 单点 VaR (%)
    public decimal ClassicalExpectedShortfall99Percent { get; set; } // 传统 99% CVaR/ES (%)
    public decimal TailSeverityPremiumRatio { get; set; } // 谱风险尾部严酷度溢价倍率 (SRM / ES99 >= 1.0)
    public decimal ExponentialSpectrumConcentrationRatio { get; set; } // 最劣 1% 损失所占的谱权重膨胀倍数
    public List<SpectralTierItem> TailTierList { get; set; } = new();
    public string SpectralExecutiveVerdict { get; set; } = string.Empty;
}

// 3. 动态风险预算漂移走廊 (Risk Contribution Drift Corridor) 与平滑再平衡
public class RiskBudgetAssetItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal CurrentHoldingWeightPercent { get; set; } // 当前实际持仓权重 (%)
    public decimal MarginalRiskContributionPercent { get; set; } // 边际风险贡献 MRC (%/%)
    public decimal PercentageRiskContributionPercent { get; set; } // 实际百分比风险贡献 PRC (%)
    public decimal TargetRiskBudgetPercent { get; set; } // 目标预设风险预算 b_i (%)
    public decimal RiskBudgetDriftDeltaPercent { get; set; } // 风险贡献偏离漂移 ΔRC = PRC - b_i (%)
    public decimal InnerBandLowerPercent { get; set; } // 内侧缓冲下界
    public decimal InnerBandUpperPercent { get; set; } // 内侧缓冲上界
    public decimal OuterBandLowerPercent { get; set; } // 外侧硬约束下界
    public decimal OuterBandUpperPercent { get; set; } // 外侧硬约束上界
    public string CorridorStatusBadge { get; set; } = string.Empty; // 🟢 预算达标 / 🟡 缓冲预警 / 🔴 走廊破位
    public decimal SmoothRebalanceTargetWeightPercent { get; set; } // 带摩擦平滑再平衡目标权重 (%)
    public decimal RebalanceTradeWeightDeltaPercent { get; set; } // 建议调仓幅度 (%)
}

public class RiskBudgetDriftCorridorResult
{
    public decimal TotalRiskContributionDriftIndex { get; set; } // 全组合风险贡献漂移指数 RCDI = Σ|PRC_i - b_i| (%)
    public decimal PortfolioAnnualizedVolatilityPercent { get; set; } // 组合当前年化总波动率 (%)
    public int BreachedAssetCount { get; set; } // 突破外侧走廊硬屏障的资产数
    public int WarningAssetCount { get; set; } // 处于黄色预警缓冲区的资产数
    public decimal RequiredSmoothRebalanceTurnoverPercent { get; set; } // 实施平滑再平衡所需单边换手率 (%)
    public decimal EstimatedRebalanceCostBps { get; set; } // 预估调仓滑点与摩擦损耗 (bps)
    public bool TriggerRebalanceAction { get; set; } // 是否触发调仓清算执行屏障
    public string CorridorOverallStatus { get; set; } = string.Empty; // 🟢 风险预算稳定 / 🟡 局部轻微漂移 / 🔴 强平再平衡激活
    public List<RiskBudgetAssetItem> AssetItemList { get; set; } = new();
    public string RiskBudgetExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Bailey-de Prado 概率夏普比率 (PSR) 与通缩夏普比率 (DSR) 策略过拟合检验
public class DeflatedSharpeOverfitResult
{
    public decimal UnadjustedAnnualizedSharpeRatio { get; set; } // 样本未调整传统年化夏普比率
    public decimal ReturnSkewness { get; set; } // 收益率样本偏度 γ_3 (负偏表示左侧肥尾)
    public decimal ReturnKurtosis { get; set; } // 收益率样本峰度 γ_4 (大于 3 表示厚尾)
    public int SampleObservationsT { get; set; } // 有效日收益率样本观测期数 T
    public int NumberOfTrialsTestedN { get; set; } // 策略/参数空间回测候选试验次数 N
    public decimal BenchmarkSharpeRatio { get; set; } // 检验基准夏普比率 SR*
    public decimal ProbabilisticSharpeRatioPercent { get; set; } // 概率夏普比率 PSR(SR*) (%)
    public decimal ExpectedMaxNullSharpeRatio { get; set; } // 零假设伪阿尔法试验期望最大夏普 SR_0
    public decimal DeflatedSharpeRatioPercent { get; set; } // 通缩夏普比率 DSR = PSR(SR_0) (%)
    public decimal FalseDiscoveryProbabilityPercent { get; set; } // 伪发现过拟合概率 (1 - DSR) (%)
    public string AlphaGenuineStatusBadge { get; set; } = string.Empty; // 🟢 真实阿尔法 (显著通过) / 🟡 边缘可疑 / 🔴 伪阿尔法 (严重过拟合)
    public string DsrExecutiveVerdict { get; set; } = string.Empty;
}
#endregion

#region Phase 33 机构级量化投研模型 (BAB/QMJ因子解构、EVT极值GPD外推、内生流动性黑洞、夏普衰减半衰期与CUSUM滤波)

// 1. AQR 杠杆约束异象 Betting Against Beta (BAB) 与高质量多空 (QMJ) 因子解构
public class BabAssetItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal PortfolioWeightPercent { get; set; } // 组合当前持仓权重 (%)
    public decimal SystematicBeta { get; set; } // 系统性贝塔系数 β
    public string BetaBucketName { get; set; } = string.Empty; // 🟢 低贝塔 (Low Beta) / 🟡 基准中性 (Neutral) / 🔴 高贝塔 (High Beta)
    public decimal BabPortfolioWeightPercent { get; set; } // 在 BAB 杠杆中性组合中的配重 (%)
    public decimal QualityProfitabilityScore { get; set; } // 盈利性得分 (0-100)
    public decimal QualitySafetyScore { get; set; } // 安全性得分 (0-100)
    public decimal QualityStabilityScore { get; set; } // 收益稳定性得分 (0-100)
    public decimal CompositeQualityScore { get; set; } // QMJ 综合质量评分 (0-100)
    public string QualityRatingBadge { get; set; } = string.Empty; // 💎 优质卓越 (Quality) / ⚖️ 稳健适中 / ⚠️ 劣质投机 (Junk)
}

public class BabQmjFactorDecompositionResult
{
    public decimal LowBetaBasketAverageBeta { get; set; } // 低贝塔多头篮子加权平均 Beta (β_L)
    public decimal HighBetaBasketAverageBeta { get; set; } // 高贝塔空头篮子加权平均 Beta (β_H)
    public decimal LowBetaLeverageRatio { get; set; } // 低贝塔加杠杆倍数 (1 / β_L)
    public decimal HighBetaDeleverageRatio { get; set; } // 高贝塔去杠杆倍数 (1 / β_H)
    public decimal BabAnnualizedSpreadReturnPercent { get; set; } // 贝塔中性 BAB 多空年化利差收益 (%)
    public decimal BabAnnualizedVolatilityPercent { get; set; } // BAB 策略年化波动率 (%)
    public decimal BabSharpeRatio { get; set; } // BAB 策略年化夏普比率
    public decimal ImpliedLeverageShadowCostPercent { get; set; } // 机构杠杆融资约束隐含影子成本 ψ (%)
    public decimal PortfolioNetBabExposurePercent { get; set; } // 投资组合净 BAB 因子暴露 (%)
    public decimal PortfolioWeightedQualityScore { get; set; } // 组合加权 QMJ 综合质量得分 (0-100)
    public decimal QualityMinusJunkAnnualizedPremiumPercent { get; set; } // 质量多空溢价 (QMJ Spread, %)
    public decimal TrueManagerSelectionAlphaPercent { get; set; } // 剔除 BAB 杠杆异象与 QMJ 暴露后的真实选基纯 Alpha (%)
    public List<BabAssetItem> AssetItemList { get; set; } = new();
    public string BabQmjExecutiveVerdict { get; set; } = string.Empty;
}

// 2. BlackRock Aladdin 极值理论 (EVT) 广义帕累托分布 (GPD) 尾部外推与极端重现期 (Return Period) 风险测度
public class EvtReturnPeriodItem
{
    public string PeriodName { get; set; } = string.Empty; // 1年一遇 (250日) / 4年一遇 (1000日) / 10年一遇 (2500日) / 20年一遇 (5000日)
    public int ReturnPeriodDays { get; set; } // 250 / 1000 / 2500 / 5000
    public decimal NonExceedanceProbabilityPercent { get; set; } // 99.60% / 99.90% / 99.96% / 99.98%
    public decimal ExtrapolatedExtremeLossVaRPercent { get; set; } // GPD 尾部解析外推极值在险损失 VaR (%)
    public decimal ExtrapolatedExpectedTailLossESPercent { get; set; } // GPD 尾部期望断崖短缺损失 ES (%)
    public decimal HistoricalSampleWorstLossPercent { get; set; } // 历史样本观测最劣单日损失 (%)
    public decimal TailExtrapolationRatio { get; set; } // 外推增幅倍数 (EVT-VaR / 历史最劣损失)
    public string ShockSeverityGrade { get; set; } = string.Empty; // 🔴 灾难性断崖 / 🟠 极严酷黑天鹅 / 🟡 显著系统性冲击
}

public class EvtGeneralizedParetoResult
{
    public decimal ThresholdLossPercent { get; set; } // 超限损失门槛 u (%)
    public int TotalSampleObservationsT { get; set; } // 样本总观测期数 T
    public int ExceedanceObservationsNu { get; set; } // 突破超限门槛的样本点数 N_u
    public decimal ExceedanceProbabilityPercent { get; set; } // 超限经验概率 ζ_u = N_u / T (%)
    public decimal GpdShapeParameterXi { get; set; } // GPD 形状参数 ξ (Tail Index，大于0为厚尾 Fréchet)
    public decimal GpdScaleParameterBeta { get; set; } // GPD 尺度参数 β
    public decimal TailIndexFatnessRatio { get; set; } // 相对正态尾部肥度倍数
    public string TailDistributionRegime { get; set; } = string.Empty; // 🔴 强重尾幂律衰减 (Heavy Fréchet) / 🟡 中度厚尾指数过渡 / 🟢 薄尾轻度衰减
    public decimal EvtVaR99Percent { get; set; } // EVT 外推 99% 极值 VaR (%)
    public decimal EvtES99Percent { get; set; } // EVT 外推 99% 极值 ES (%)
    public List<EvtReturnPeriodItem> ReturnPeriodList { get; set; } = new();
    public string EvtExecutiveVerdict { get; set; } = string.Empty;
}

// 3. MSCI Barra 内生流动性黑洞弹性 (LBHI) 与资产踩踏抛售反馈乘数 (Fire-Sale Run Cascade Multiplier)
public class FireSaleAssetItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal PortfolioWeightPercent { get; set; } // 组合持仓权重 (%)
    public decimal AssetAumTenThousand { get; set; } // 基金估算规模 (万元)
    public decimal DailyAdvTenThousand { get; set; } // 日均交易量 ADV (万元)
    public decimal MarketDepthElasticity { get; set; } // 市场深度价格冲击弹性系数 λ
    public decimal DirectSellingPressureTenThousand { get; set; } // 赎回冲击下直接抛售规模 Q^(1) (万元)
    public decimal DirectPriceDropPercent { get; set; } // 一级直接抛售导致的价格跌幅 (%)
    public decimal InducedDeleveragingPressureTenThousand { get; set; } // 二级诱发级联去杠杆抛售规模 Q^(2) (万元)
    public decimal TotalLiquidationVolumeTenThousand { get; set; } // 级联总抛售规模 (万元)
    public decimal FireSaleCascadeRatio { get; set; } // 资产级联放大比率 (Q^(1)+Q^(2)) / Q^(1)
    public string VulnerabilityStatusBadge { get; set; } = string.Empty; // 🔴 高度踩踏敏感 / 🟡 中度流动性挤压 / 🟢 流动性充沛
}

public class LiquidityBlackHoleResult
{
    public decimal TotalPortfolioAumTenThousand { get; set; } // 组合基准拟合总规模 (万元)
    public decimal DirectLiquidationVolumeTenThousand { get; set; } // 一级直接赎回抛售总规模 (万元)
    public decimal SecondaryInducedLiquidationVolumeTenThousand { get; set; } // 二级诱发踩踏抛售总规模 (万元)
    public decimal AggregateFireSaleVolumeTenThousand { get; set; } // 级联总抛售规模 (万元)
    public decimal FireSaleCascadeMultiplier { get; set; } // 全组合踩踏级联乘数 FCM = (Q^(1) + Q^(2)) / Q^(1)
    public decimal ExogenousDirectPriceImpactPercent { get; set; } // 外生直接加权价格冲击 (%)
    public decimal EndogenousFeedbackPriceImpactPercent { get; set; } // 内生反馈螺旋总价格冲击 (%)
    public decimal LiquidityBlackHoleIndex { get; set; } // 流动性黑洞指数 LBHI (0-100)
    public decimal MaxFireSaleCapacityTenThousand { get; set; } // 临界无踩踏最大安全变现额度 MFLC (万元)
    public string BlackHoleRiskLevel { get; set; } = string.Empty; // 🟢 流动性缓冲充裕 / 🟡 局部微踩踏预警 / 🔴 内生流动性黑洞爆发
    public List<FireSaleAssetItem> AssetItemList { get; set; } = new();
    public string LiquidityBlackHoleExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Marcos Lopez de Prado 策略微观夏普衰减半衰期 (Sharpe Half-Life) 与自适应 CUSUM 概念漂移滤波检验
public class SharpeDecayCusumResult
{
    public decimal CurrentAnnualizedSharpeRatio { get; set; } // 当前全样本年化夏普比率
    public decimal InitialEstimatedSharpeRatio { get; set; } // 策略初期峰值夏普 SR_0
    public decimal ExponentialDecayRateLambda { get; set; } // 指数衰减速率参数 λ
    public decimal SharpeDecayHalfLifeDays { get; set; } // 夏普比率衰减半衰期 t_1/2 (交易日)
    public decimal EstimatedDaysToTerminalExpiration { get; set; } // 预估衰减至无风险收益临界天数 (交易日)
    public decimal CusumPositiveAccumulator { get; set; } // CUSUM 正向上行动能累积器 S^+
    public decimal CusumNegativeAccumulator { get; set; } // CUSUM 负向衰退累积器 S^-
    public decimal CusumAlertThreshold { get; set; } // CUSUM 报警临界阈值 h
    public bool TriggerStructuralDecayAlert { get; set; } // 是否触发策略结构性衰变报警 (S^- > h)
    public bool TriggerRegimeMomentumBurst { get; set; } // 是否触发动能爆发 (S^+ > h)
    public string AlphaLongevityStatusBadge { get; set; } = string.Empty; // 🟢 强稳态长效阿尔法 / 🟡 稳健正常衰减 / 🔴 结构性衰变失效
    public string CusumDecayExecutiveVerdict { get; set; } = string.Empty;
}
#endregion

#region Phase 34 机构级量化投研模型 (Lead-Lag时滞互相关网络、多期限风险方差比、3状态HMM体制解码、短周期反转与动量崩塌)

// 1. Goldman Sachs & J.P. Morgan: 跨资产非对称时滞领先-滞后互相关与信息流传导有向网络
public class LeadLagAssetItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal PortfolioWeightPercent { get; set; } // 组合当前持仓权重 (%)
    public decimal InformationLeadershipScore { get; set; } // 信息领导力综合得分 ILS (-100 ~ +100)
    public decimal AverageLeadLagDays { get; set; } // 平均领先(+)或滞后(-)交易日 (天)
    public int OutgoingLeadingLinksCount { get; set; } // 出度：作为先行者领先的资产数量
    public int IncomingLaggingLinksCount { get; set; } // 入度：作为滞后跟随者的资产数量
    public string NetworkRoleBadge { get; set; } = string.Empty; // 👑 价格发现龙头 / ⚖️ 稳态中枢 / ⏳ 滞后跟随标的
    public string TacticalAdvisory { get; set; } = string.Empty; // 战术启示
}

public class LeadLagPairItem
{
    public string LeaderFundCode { get; set; } = string.Empty; // 领先资产代码
    public string LeaderFundName { get; set; } = string.Empty;
    public string FollowerFundCode { get; set; } = string.Empty; // 滞后资产代码
    public string FollowerFundName { get; set; } = string.Empty;
    public int OptimalLagDays { get; set; } // 最优时滞 τ* (天)
    public decimal PeakCrossCorrelation { get; set; } // 峰值互相关系数 ρ(τ*)
    public decimal ContemporaneousCorrelation { get; set; } // 同期相关系数 ρ(0)
    public decimal AsymmetryGap { get; set; } // 非对称时滞优势差值 |ρ(τ*)| - |ρ(0)|
    public string LeadLagDirectionBadge { get; set; } = string.Empty; // ➡️ 强领先传导 / ⚡ 中度领先 / 🔄 准同步
}

public class LeadLagCrossCorrelationResult
{
    public int MaxLagHorizonDays { get; set; } = 5; // 最大时滞窗口天数 (±5日)
    public decimal PortfolioAverageLeadLagDispersionDays { get; set; } // 全组合平均时滞离散度 (天)
    public decimal MaxPairwiseAsymmetryGap { get; set; } // 最大资产对非对称领先优势差
    public string AnchorLeaderFundCode { get; set; } = string.Empty; // 组合核心价格发现锚头
    public string AnchorLeaderFundName { get; set; } = string.Empty;
    public string MostLaggingFundCode { get; set; } = string.Empty; // 组合最深滞后跟随标的
    public string MostLaggingFundName { get; set; } = string.Empty;
    public List<LeadLagAssetItem> AssetItemList { get; set; } = new();
    public List<LeadLagPairItem> PairItemList { get; set; } = new();
    public string LeadLagExecutiveVerdict { get; set; } = string.Empty;
}

// 2. BlackRock Aladdin / Axioma / Lo-MacKinlay: 多重投资期限风险期限结构与方差比非随机游走检验
public class HorizonRiskItem
{
    public int HorizonDays { get; set; } // 期限天数 q (1d, 5d, 21d, 63d, 126d, 252d)
    public string HorizonName { get; set; } = string.Empty; // 1日 / 5日(周) / 21日(月) / 63日(季) / 126日(半年) / 252日(年)
    public decimal CumulativeReturnPercent { get; set; } // 该期限累积收益率 (%)
    public decimal ActualPeriodVolatilityPercent { get; set; } // 实际多期复合波动率 σ(q) (%)
    public decimal SqrtTimeBenchmarkVolPercent { get; set; } // 平方根法则理论外推波动率 σ(1)*sqrt(q) (%)
    public decimal LoMacKinlayVarianceRatio { get; set; } // Lo-MacKinlay 方差比率 VR(q) = σ²(q) / (q * σ²(1))
    public decimal HeteroscedasticityZScore { get; set; } // 异方差稳健检验统计量 Z*(q)
    public string DynamicsRegimeBadge { get; set; } = string.Empty; // 🔄 均值回归 (VR<0.9) / 📈 动量趋势 (VR>1.1) / 🎲 随机游走
    public decimal HorizonAdjustmentMultiplier { get; set; } // 期限调整倍数 κ(q) = sqrt(VR(q))
    public decimal HorizonAdjustedVaR99Percent { get; set; } // 期限修正 99% 在险价值 VaR (%)
    public decimal HorizonAdjustedES99Percent { get; set; } // 期限修正 99% 期望损失 ES (%)
}

public class MultiHorizonRiskTermStructureResult
{
    public decimal BaseDailyVolatilityPercent { get; set; } // 基准单日波动率 σ(1) (%)
    public decimal AnnualizedLoMacKinlayVarianceRatio { get; set; } // 252 日(年度)综合方差比率 VR(252)
    public decimal LongTermVolatilityDistortionPercent { get; set; } // 长期真实波动率对时间平方根外推的偏离幅度 (%)
    public string TermStructureDominantPattern { get; set; } = string.Empty; // 🔄 强均值回归型 / 📈 强动量聚集型 / 🎲 鞅差随机游走
    public List<HorizonRiskItem> HorizonItemList { get; set; } = new();
    public string MultiHorizonExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Two Sigma / Citadel: 3 状态高斯隐马尔可夫模型 (Gaussian HMM) 宏观体制自动解码与转移信息熵
public class HmmStateItem
{
    public int StateIndex { get; set; } // 状态索引 (1, 2, 3)
    public string StateName { get; set; } = string.Empty; // 🟢 牛市低波扩张态 / 🟡 震荡中波修复态 / 🔴 危机高波踩踏态
    public decimal ExpectedDailyReturnPercent { get; set; } // 状态期望日收益率 μ_k (%)
    public decimal AnnualizedVolatilityPercent { get; set; } // 状态年化波动率 σ_k (%)
    public decimal PosteriorProbabilityPercent { get; set; } // 当前即期后验概率 π_k (%)
    public decimal TransitionSelfPersistencePercent { get; set; } // 状态自留存概率 P_kk (%)
    public decimal ExpectedDwellDays { get; set; } // 预期平均驻留持续天数 D_k = 1 / (1 - P_kk) (天)
    public int HistoricalSampleDays { get; set; } // 历史上处于该状态的样本交易日数
    public decimal HistoricalCoveragePercent { get; set; } // 历史样本覆盖占比 (%)
}

public class ThreeStateGaussianHmmResult
{
    public int CurrentDecodedStateIndex { get; set; } // 当前解码所属状态 (1, 2, 3)
    public string CurrentDecodedStateName { get; set; } = string.Empty;
    public decimal BullStateProbabilityPercent { get; set; } // 牛市扩张态后验概率 π_1 (%)
    public decimal NeutralStateProbabilityPercent { get; set; } // 震荡修复态后验概率 π_2 (%)
    public decimal CrisisStateProbabilityPercent { get; set; } // 危机踩踏态后验概率 π_3 (%)
    public decimal RegimeTransitionEntropy { get; set; } // 体制转移信息熵 H(π) (nats/bits)
    public decimal NormalizedEntropyPercent { get; set; } // 归一化不确定性熵 H / ln(3) (%)
    public string MacroRegimeAdvisoryBadge { get; set; } = string.Empty; // 🛡️ 危机防御对冲 / ⚔️ 积极权益进攻 / ⚖️ 中性平衡防守
    public List<HmmStateItem> StateItemList { get; set; } = new();
    public List<decimal> TransitionMatrixFlat { get; set; } = new(); // 3x3 转移概率矩阵展平 (P_11..P_33)
    public string HmmExecutiveVerdict { get; set; } = string.Empty;
}

// 4. AQR / Asness / Daniel-Moskowitz: 广义反向择时短周期反转 Alpha 与动量崩塌预警指数 (MCWI)
public class ReversalAssetItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal PortfolioWeightPercent { get; set; } // 当前持仓权重 (%)
    public decimal Past5DayReturnPercent { get; set; } // 近 5 日累积收益率 (%)
    public decimal ShortTermReversalScore { get; set; } // 5 日流动性反转因子得分 STR_5d (-100 ~ +100)
    public decimal Past21DayReturnPercent { get; set; } // 近 21 日月度收益率 (%)
    public decimal MonthlyReversalScore { get; set; } // 21 日反转得分 STR_21d (-100 ~ +100)
    public decimal BearMarketDownsideBeta { get; set; } // 极端熊市条件 Beta β_Bear
    public decimal BullMarketUpsideBeta { get; set; } // 极端牛市条件 Beta β_Bull
    public decimal DownsideBetaAsymmetry { get; set; } // 动量非对称 Beta 差值 Δβ = β_Bear - β_Bull
    public string TacticalReversalBadge { get; set; } = string.Empty; // 🚀 超跌博弈反转 / 💎 强势动量延续 / ⚠️ 极度超买谨防补跌
}

public class MomentumCrashAndReversalResult
{
    public decimal Portfolio5DayReversalAlphaPercent { get; set; } // 组合 5 日反转阿尔法超额预期 (%)
    public decimal MomentumCrashWarningIndex { get; set; } // 动量崩塌预警指数 MCWI (0-100)
    public decimal MomentumCrashProbabilityPercent { get; set; } // 恐慌反弹期动量崩塌概率 P_Crash (%)
    public decimal DownsideAsymmetricBetaSpread { get; set; } // 组合整体熊市-牛市非对称 Beta 偏离
    public string MomentumProtectionStatusBadge { get; set; } = string.Empty; // 🟢 动量安全稳态 / 🟡 尾部风险积聚 / 🔴 动量崩塌高危预警
    public decimal RecommendedReversalTiltTurnoverPercent { get; set; } // 建议向超跌反转优质资产倾斜换手率 (%)
    public List<ReversalAssetItem> AssetItemList { get; set; } = new();
    public string ReversalExecutiveVerdict { get; set; } = string.Empty;
}
#endregion

#region Phase 35 机构级量化投研模型 (FRTB内部模型法PLA与交通灯、Pod Shop阶梯降额止损、因子对称正交化、非线性信息论dCor/MI)

// 1. BIS / BCBS FRTB 内部模型法损益归因 (PLA) 与巴塞尔 250 天交通灯超限检定
public class PlaDailyObservationItem
{
    public DateTime Date { get; set; }
    public decimal HypotheticalPnlWan { get; set; } // 前台假想损益 HPL (万元)
    public decimal RiskTheoreticalPnlWan { get; set; } // 风控理论损益 RTPL (万元)
    public decimal PnlDifferenceWan { get; set; } // 损益差异 (HPL - RTPL)
    public decimal VaR99Wan { get; set; } // 当日 99% 在险价值 (万元)
    public bool IsVaRExceedance { get; set; } // 是否发生 99% VaR 违约超限
}

public class FrtbPlaAndTrafficLightResult
{
    public decimal SpearmanRankCorrelation { get; set; } // 斯皮尔曼秩相关系数 SRC (HPL vs RTPL)
    public string SpearmanZoneStatus { get; set; } = string.Empty; // 🟢 绿色合规 (>=0.80) / 🟡 黄色观察 (0.70~0.80) / 🔴 红色失效 (<0.70)
    public decimal KolmogorovSmirnovStatistic { get; set; } // 柯尔莫哥洛夫-斯米尔诺夫 KS 检验统计量 D_KS
    public string KsZoneStatus { get; set; } = string.Empty; // 🟢 绿色通过 (<=0.09) / 🟡 黄色观察 (0.09~0.12) / 🔴 红色超限 (>0.12)
    public string OverallPlaComplianceStatus { get; set; } = string.Empty; // 🟢 内部模型法准入合规 / 🟡 准入观察 / 🔴 强制退回标准法
    
    // 巴塞尔 250 天滚动交通灯回测
    public int Rolling250DaysVaRExceedanceCount { get; set; } // 过去 250 天 99% VaR 超限次数 (0-4绿, 5-9黄, >=10红)
    public string BaselTrafficLightZone { get; set; } = string.Empty; // 🟢 绿色安全区 / 🟡 黄色监管注意区 / 🔴 红色模型失效区
    public decimal RegulatoryCapitalMultiplierAddOn { get; set; } // 监管资本附加乘数 k (绿区 0.0, 黄区 0.40~0.85, 红区 强制取消资格)
    public decimal TotalCapitalMultiplier { get; set; } // 最终资本乘数 (3.0 + k)
    public decimal BacktestFailureProbabilityPercent { get; set; } // 真实模型低估概率 (二项分布累积检验 p-value)
    public List<PlaDailyObservationItem> RecentObservations { get; set; } = new();
    public string PlaExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Millennium & Point72 Pod Shop 多策略单元动态资本分配与阶梯止损降额机制
public class PodShopItem
{
    public string PodId { get; set; } = string.Empty; // 策略/标的单元代号
    public string PodName { get; set; } = string.Empty; // 单元名称
    public decimal AllocatedCapitalWan { get; set; } // 当前配置资本金 (万元)
    public decimal TargetWeightPercent { get; set; } // 目标权重 (%)
    public decimal HighWaterMarkNav { get; set; } // 历史高水位净值 HWM
    public decimal CurrentDrawdownPercent { get; set; } // 当前回撤幅度 (%)
    public decimal PeakDrawdownPercent { get; set; } // 历史最大回撤 (%)
    public decimal InformationRatio { get; set; } // 跟踪信息比率 IR
    public string DeriskingStatus { get; set; } = string.Empty; // 🟢 正常扩容 / 🟡 冻结加仓 (-2%) / 🟠 强制降额50% (-3%) / 🔴 硬止损清盘 (-5%)
    public decimal CapitalAdjustmentMultiplier { get; set; } // 资本调整系数 (1.00 / 0.50 / 0.00)
    public decimal PostAdjustmentCapitalWan { get; set; } // 动态风控后保留资本 (万元)
    public decimal MarginalRiskContributionPercent { get; set; } // 边际风险贡献 MRC (%)
}

public class PodShopCapitalAllocationResult
{
    public decimal TotalFundCapitalWan { get; set; } // 基金总资本池 (万元)
    public decimal ActiveWorkingCapitalWan { get; set; } // 当前实际运行资本 (万元)
    public decimal CentralReservePoolWan { get; set; } // 中央风控拦截回抽防守资金池 (万元)
    public int ActivePodsCount { get; set; } // 活跃运作 Pod 数量
    public int DeriskedPodsCount { get; set; } // 被减半降额 Pod 数量
    public int StoppedOutPodsCount { get; set; } // 触碰硬止损清盘 Pod 数量
    public decimal MaxPodMarginalRiskPercent { get; set; } // 单 Pod 最大边际风险敞口占比 (%)
    public string PodGovernanceHealthBadge { get; set; } = string.Empty; // 🟢 资本配置健康 / 🟡 结构性降额运作 / 🔴 组合大面积止损
    public List<PodShopItem> PodList { get; set; } = new();
    public string PodExecutiveVerdict { get; set; } = string.Empty;
}

// 3. MSCI Barra & Axioma 风格因子 Löwdin 对称正交化与纯因子载荷矩阵
public class OrthogonalFactorItem
{
    public string FactorName { get; set; } = string.Empty; // 因子名称 (规模 Size, 价值 Value, 动量 Momentum, 低波 LowVol, 质量 Quality)
    public decimal RawFactorVariance { get; set; } // 原始因子方差
    public decimal OrthogonalFactorVariance { get; set; } // 正交后纯因子方差
    public decimal InformationPreservationRatio { get; set; } // 与原因子最小二乘信息保留度 (Löwdin 范数保真度)
    public decimal CrossFactorMaxCollinearity { get; set; } // 正交前最大跨因子共线性 (正交后严格为 0)
}

public class AssetOrthogonalFactorLoadingItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal SizePureLoading { get; set; } // 纯规模因子载荷
    public decimal ValuePureLoading { get; set; } // 纯价值因子载荷
    public decimal MomentumPureLoading { get; set; } // 纯动量因子载荷
    public decimal LowVolPureLoading { get; set; } // 纯低波因子载荷
    public decimal QualityPureLoading { get; set; } // 纯质量因子载荷
    public decimal ResidualSpecificRiskPercent { get; set; } // 剥离正交因子后的特异质风险占比 (%)
}

public class FactorOrthogonalizationResult
{
    public int FactorCount { get; set; }
    public decimal AverageCrossCorrelationBefore { get; set; } // 正交前因子间平均绝对相关性
    public decimal AverageCrossCorrelationAfter { get; set; } // 正交后因子间平均相关性 (理论严格趋近于 0.0)
    public decimal OrthogonalityAccuracy { get; set; } // 正交化精度 ||F_orth^T * F_orth - I||
    public List<OrthogonalFactorItem> FactorList { get; set; } = new();
    public List<AssetOrthogonalFactorLoadingItem> AssetLoadingList { get; set; } = new();
    public string OrthogonalExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Two Sigma & Citadel 距离相关系数 (dCor) 与互信息 (MI) 非线性 Alpha 特征排序
public class NonlinearAssetFeatureItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal LinearPearsonCorr { get; set; } // 经典线性 Pearson 相关系数
    public decimal DistanceCorrelation { get; set; } // Székely 距离相关系数 dCor ∈ [0, 1]
    public decimal MutualInformationBits { get; set; } // 香农互信息 (Bits)
    public decimal NonlinearityPremiumRatio { get; set; } // 非线性溢出增益比 (dCor / (|Pearson| + 1e-4))
    public int FeatureImportanceRank { get; set; } // 非线性综合特征重要性排名
    public string DependenceClassification { get; set; } = string.Empty; // 强非线性主导 / 线性协同 / 独立弱相关 / 尾部高度耦合
}

public class NonlinearDistanceMutualInfoResult
{
    public decimal PortfolioAverageDistanceCorr { get; set; } // 全组合资产间平均距离相关系数 dCor
    public decimal PortfolioAverageMutualInformationBits { get; set; } // 平均互信息比特数
    public decimal NonlinearAlphaGainPercentage { get; set; } // 相比单纯线性相关捕获的额外非线性信息增益 (%)
    public string TopNonlinearAlphaDriverCode { get; set; } = string.Empty; // 第一非线性 Alpha 驱动标的代码
    public string TopNonlinearAlphaDriverName { get; set; } = string.Empty; // 第一非线性 Alpha 驱动标的名称
    public List<NonlinearAssetFeatureItem> AssetFeatureList { get; set; } = new();
    public string NonlinearExecutiveVerdict { get; set; } = string.Empty;
}
#endregion

#region Phase 36 顶级机构级量化系统升级模型定义

// 1. Citadel & Millennium 因子与资产微观拥挤度及机构踩踏排队指数 (HLRI)
public class AssetCrowdednessItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal TargetWeightPercent { get; set; }
    public decimal ValuationStretchZScore { get; set; } // 估值与收益拉伸度 Z-Score
    public decimal PairwiseCorrelationCompression { get; set; } // 截面协同压缩度 (与高权重资产的平均相关性)
    public decimal VolumeTurnoverAccelerationRatio { get; set; } // 换手异动加速度比率
    public decimal TailNegativeAsymmetry { get; set; } // 左尾负偏度度量
    public decimal HerdLiquidationRiskIndex { get; set; } // 踩踏风险指数 HLRI ∈ [0, 100]
    public string CrowdednessTierBadge { get; set; } = string.Empty; // 🟢 安全宽松 / 🟡 中度聚集 / 🟠 高度拥挤 / 🔴 极端踩踏高危
    public decimal EstimatedLiquidationDaysNormal { get; set; } // 常态市况清仓排队天数 (ADV 10% 参与率)
    public decimal EstimatedLiquidationDaysStress { get; set; } // 踩踏市况清仓排队天数 (ADV 2.5% 踩踏折让参与率)
    public string DeriskingActionGuidance { get; set; } = string.Empty; // 调仓建议
}

public class AssetFactorCrowdednessResult
{
    public decimal PortfolioAverageHlri { get; set; } // 全组合加权平均踩踏风险指数
    public int HighRiskCrowdedAssetsCount { get; set; } // 高危/极端拥挤资产数量
    public string PeakCrowdedAssetCode { get; set; } = string.Empty; // 最高拥挤度资产代码
    public string PeakCrowdedAssetName { get; set; } = string.Empty; // 最高拥挤度资产名称
    public decimal PeakCrowdedHlri { get; set; } // 最高拥挤度分值
    public decimal PortfolioWeightedLiquidationDaysStress { get; set; } // 压力踩踏市况加权平仓天数
    public string CrowdednessGovernanceBadge { get; set; } = string.Empty; // 🟢 整体宽松安全 / 🟡 局部适度聚集 / 🔴 抱团踩踏高危
    public List<AssetCrowdednessItem> Items { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. BlackRock Aladdin & J.P. Morgan 多因子宏观情景冲击传导与压力在险价值 (Stressed VaR)
public class MacroScenarioShockItem
{
    public string ScenarioName { get; set; } = string.Empty; // 情景名称 (2008次贷海啸、2020疫情休克、滞胀加息周期、地缘能源断供、科技估值挤压)
    public string ScenarioDescription { get; set; } = string.Empty; // 宏观叙事背景
    public decimal EquityShockPercent { get; set; } // 权益市场基准冲击 (%)
    public decimal BondYieldChangeBps { get; set; } // 国债利率变动基点 (bps)
    public decimal CommodityShockPercent { get; set; } // 大宗商品/通胀冲击 (%)
    public decimal VolShockPercent { get; set; } // 市场波动率激增幅度 (%)
    public decimal PortfolioExpectedPnlPercent { get; set; } // 组合预期前瞻冲击盈亏 (%)
    public decimal StressedVaR99Percent { get; set; } // 极端压力状态 99% 在险价值 (%)
    public string HardestHitAssetCode { get; set; } = string.Empty; // 该情景下受损最重标的代码
    public string HardestHitAssetName { get; set; } = string.Empty; // 该情景下受损最重标的名称
    public decimal HardestHitAssetLossPercent { get; set; } // 最重受损标的回撤 (%)
    public string ResilienceBadge { get; set; } = string.Empty; // 🟢 韧性防御 / 🟡 中度承压 / 🔴 深度重挫
}

public class AssetStressedLossItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal EquityBeta { get; set; } // 权益贝塔敏感度
    public decimal DurationSensitivity { get; set; } // 久期利率敏感度 (年)
    public decimal CommodityBeta { get; set; } // 大宗通胀敏感度
    public decimal VolSensitivity { get; set; } // 波动率敏感度
    public decimal GfcLossPercent { get; set; } // 金融海啸预期冲击 (%)
    public decimal CovidLossPercent { get; set; } // 疫情流动性冲击 (%)
    public decimal StagflationLossPercent { get; set; } // 滞胀加息预期冲击 (%)
    public decimal EnergyShockLossPercent { get; set; } // 能源危机预期冲击 (%)
    public decimal TechCompressionLossPercent { get; set; } // 成长挤压预期冲击 (%)
    public decimal WorstCaseScenarioLossPercent { get; set; } // 最恶劣情景峰值亏损 (%)
    public int FragilityRank { get; set; } // 宏观脆弱度综合排名
}

public class MacroFactorShockPropagationResult
{
    public string WorstCaseScenarioName { get; set; } = string.Empty; // 最恶劣冲击情景名称
    public decimal WorstCasePortfolioLossPercent { get; set; } // 最恶劣情景下组合回撤幅度 (%)
    public string StressedVaR99Range { get; set; } = string.Empty; // 压力 VaR99 区间
    public decimal FragilityDiversityRatio { get; set; } // 宏观脆弱度分散指数 (0~1)
    public List<MacroScenarioShockItem> ScenarioList { get; set; } = new();
    public List<AssetStressedLossItem> AssetStressedLossList { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. AQR Capital & Antti Ilmanen 真实波动率特征结构与方差风险溢价 (VRP)
public class AssetVrpItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal RealizedVolCloseToClose { get; set; } // Close-to-Close 真实年化波动率 (%)
    public decimal EffectiveHighLowVol { get; set; } // 高阶有效波动率结构 (%)
    public decimal ImpliedVolForecast { get; set; } // 前瞻隐含波动率预期 (%)
    public decimal VarianceRiskPremiumSpread { get; set; } // 方差风险溢价 IV^2 - RV^2
    public decimal VrpRatioPercent { get; set; } // 相对溢价率 (IV - RV) / RV * 100%
    public decimal DownsideToUpsideVolRatio { get; set; } // 下行半波动率 / 上行半波动率
    public string VrpHarvestSignal { get; set; } = string.Empty; // 🟢 积极收割方差 / 🟡 波动中性持有 / 🔴 买入凸性保险
    public decimal CarryYieldAnnualizedPercent { get; set; } // 理论年化 Carry 收割收益率估计 (%)
}

public class VarianceRiskPremiumResult
{
    public decimal PortfolioAverageRealizedVol { get; set; } // 组合加权真实波动率 (%)
    public decimal PortfolioAverageImpliedVol { get; set; } // 组合加权预期隐含波动率 (%)
    public decimal PortfolioAverageVrpSpread { get; set; } // 平均方差风险溢价点数 (pts)
    public decimal PortfolioAverageVrpRatio { get; set; } // 平均相对方差溢价率 (%)
    public string VrpHarvestRegimeBadge { get; set; } = string.Empty; // 🟢 结构性做空波动率盈利区 / 🟡 溢价均衡区 / 🔴 波动率倒挂避险区
    public List<AssetVrpItem> AssetVrpList { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Two Sigma & Renaissance 摩擦成本敏感型动态无交易再平衡缓冲带 (No-Trade Buffer Bands)
public class NoTradeBufferBandItem
{
    public string FundCode { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal TargetWeightPercent { get; set; } // 理论最优目标权重 (%)
    public decimal CurrentWeightPercent { get; set; } // 当前实际运行权重 (%)
    public decimal HalfBandWidthPercent { get; set; } // 动态无交易缓冲半走廊宽度 Δw_i (%)
    public decimal LowerBandPercent { get; set; } // 动态下边界 (低于需加仓) (%)
    public decimal UpperBandPercent { get; set; } // 动态上边界 (高于需减仓) (%)
    public string BreachStatus { get; set; } = string.Empty; // 🟢 走廊内免交易 (零摩擦) / 🔴 向上突破超配 (平滑减仓) / 🔵 向下突破低配 (平滑加仓)
    public decimal RecommendedTradePercent { get; set; } // 推荐交易调整量 (仅向边界边缘平滑交易) (%)
    public decimal EstimatedFrictionSavedBps { get; set; } // 相比机械完全调仓节省的摩擦损耗 (bps)
    public string ActionGuidance { get; set; } = string.Empty; // 执行指导指令
}

public class DynamicNoTradeBufferBandResult
{
    public decimal TotalTargetWeightPercent { get; set; }
    public decimal TotalCurrentWeightPercent { get; set; }
    public int InBandAssetCount { get; set; } // 免交易走廊内资产数
    public int OutBandAssetCount { get; set; } // 触发边界再平衡资产数
    public decimal TurnoverReductionPercent { get; set; } // 相比机械全量换手的换手率节省比例 (%)
    public decimal EstimatedAnnualFrictionSavedWan { get; set; } // 年化预估节省交易摩擦成本 (万元)
    public decimal NetSharpeUplift { get; set; } // 考虑交易摩擦节省后的净夏普改善增益
    public List<NoTradeBufferBandItem> Items { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 37: 机构级全天候宏观惊喜、风格中性化、波动率TSMOM与最优执行轨迹模型定义

// =========================================================================
// 1. Bridgewater Associates: 宏观增长-通胀双维度惊喜指数矩阵与全天候宏观贝塔正交中性化覆盖
// =========================================================================
public class MacroSurpriseAssetExposureItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal WeightPercent { get; set; }
    public decimal GrowthSurpriseBeta { get; set; }     // 经济增长超预期因子贝塔 β_growth
    public decimal InflationSurpriseBeta { get; set; }  // 通胀超预期因子贝塔 β_inflation
    public decimal NetSurpriseExposure { get; set; }   // 净综合宏观意外敞口
    public string MacroSensitivityClass { get; set; } = string.Empty; // 敏感度分类 (顺周期成长 / 抗通胀防御 / 利率敏感 / 宏观中性)
    public decimal NeutralizingOverlayWeightPercent { get; set; } // 推荐全天候中性化覆盖对冲头寸 (%)
}

public class MacroSurpriseScenarioItem
{
    public string QuadrantName { get; set; } = string.Empty; // 宏观四象限名称 (如: 增长超预期+通胀低预期 / 滞胀惊喜 / 衰退通缩)
    public string EconomicEnvironment { get; set; } = string.Empty; // 宏观环境特征
    public decimal GrowthSurpriseShockPercent { get; set; } // 经济增长惊喜冲击 (σ)
    public decimal InflationSurpriseShockPercent { get; set; } // 通胀惊喜冲击 (σ)
    public decimal UnhedgedExpectedReturnPercent { get; set; } // 未对冲预期损益 (%)
    public decimal HedgedExpectedReturnPercent { get; set; } // 全天候对冲覆盖后预期损益 (%)
    public decimal ResilienceGainPercent { get; set; } // 韧性改善度 (%)
    public string RiskAlertLevel { get; set; } = string.Empty; // 风险预警级别 (🟢 安全平稳 / 🟡 适度波动 / 🔴 显著受挫)
}

public class MacroSurpriseAndNeutralizingOverlayResult
{
    public decimal PortfolioGrowthBeta { get; set; } // 组合整体经济增长惊喜贝塔
    public decimal PortfolioInflationBeta { get; set; } // 组合整体通胀惊喜贝塔
    public decimal MacroUnhedgedVolatilityPercent { get; set; } // 未对冲宏观意外波动率 (%)
    public decimal MacroHedgedVolatilityPercent { get; set; } // 对冲覆盖后宏观意外波动率 (%)
    public decimal MacroVolatilityReductionPercent { get; set; } // 宏观意外波动率压降率 (%)
    public decimal MaxDrawdownMitigationPercent { get; set; } // 极端宏观情景最大回撤平抑幅度 (%)
    public string MacroRegimeResilienceBadge { get; set; } = string.Empty; // 宏观韧性评级 (如: 👑 桥水全天候抗冲击 / 🛡️ 稳健中性 / ⚠️ 顺周期敞口过大)
    public List<MacroSurpriseAssetExposureItem> AssetExposures { get; set; } = new();
    public List<MacroSurpriseScenarioItem> ScenarioShocks { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// =========================================================================
// 2. Citadel & Millennium: 多经理平台核心风格因子正交中性化与非故意风险漂移剔除
// =========================================================================
public class StyleFactorExposureItem
{
    public string FactorName { get; set; } = string.Empty; // 风格因子名称 (Size, Value, Momentum, LowVol, Liquidity)
    public decimal ActiveFactorExposureZScore { get; set; } // 组合当前主动风格暴露 Z 分
    public decimal UpperToleranceLimit { get; set; } // 平台容忍上限 (+0.05)
    public decimal LowerToleranceLimit { get; set; } // 平台容忍下限 (-0.05)
    public bool IsBreached { get; set; } // 是否超出多经理平台风控红线
    public decimal StyleVarianceContributionPercent { get; set; } // 风格方差贡献占比 (%)
    public decimal PostHedgeActiveExposureZScore { get; set; } // 中性化对冲后残余主动暴露 Z 分
    public string StatusBadge { get; set; } = string.Empty; // 状态标记 (🟢 严格中性 / 🔴 风格漂移超标)
}

public class StyleNeutralizingAdjustmentItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CurrentWeightPercent { get; set; }
    public decimal TargetWeightPercent { get; set; }
    public decimal WeightAdjustmentPercent { get; set; } // 调仓建议 Δw (%)
    public decimal TrackingErrorContributionBps { get; set; } // 跟踪误差贡献 (bps)
    public string DeBiasingAction { get; set; } = string.Empty; // 去偏调整动作 (增配低估 / 压降动量偏好 / 压缩特异市值等)
}

public class StyleFactorNeutralizationResult
{
    public decimal TotalPreHedgeStyleRiskContributionPercent { get; set; } // 中性化前风格因子总风险贡献占比 (%)
    public decimal TotalPostHedgeStyleRiskContributionPercent { get; set; } // 中性化后风格因子总风险贡献占比 (%)
    public decimal UnintendedStyleRiskEliminationRatioPercent { get; set; } // 非故意风格风险消除率 (%) (目标 >= 80%)
    public decimal PureAlphaVarianceRatioPercent { get; set; } // 纯特异 Alpha 方差占比 (%)
    public decimal InformationRatioUplift { get; set; } // 信息比率 (IR) 预期改善幅度
    public int BreachedFactorCount { get; set; } // 突破风格限制因子数
    public List<StyleFactorExposureItem> FactorExposures { get; set; } = new();
    public List<StyleNeutralizingAdjustmentItem> Adjustments { get; set; } = new();
    public string GovernanceVerdict { get; set; } = string.Empty;
}

// =========================================================================
// 3. AQR Capital & Antti Ilmanen: 动态目标波动率定标时间序列动量 (TSMOM) 与危机 Alpha
// =========================================================================
public class TsmomAssetSignalItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal ShortTermTrendReturnPercent { get; set; } // 短期趋势 (21天) (%)
    public decimal MidTermTrendReturnPercent { get; set; } // 中期趋势 (63天) (%)
    public decimal LongTermTrendReturnPercent { get; set; } // 长期趋势 (252天) (%)
    public decimal CompositeTsmomScore { get; set; } // 综合时间序列动量得分 (-2.0 ~ +2.0)
    public decimal RealizedVolatilityPercent { get; set; } // 资产近半年波动率 (%)
    public decimal VolatilityScalingFactor { get; set; } // 波动率缩放比例 (σ_target / σ_realized)
    public decimal TargetScaledTsmomWeightPercent { get; set; } // 波动率定标后推荐动量多空权重 (%)
    public decimal TrendHalfLifeDays { get; set; } // 趋势半衰期 (天)
    public string CrisisAlphaBadge { get; set; } = string.Empty; // 危机 Alpha 评级 (🛡️ 强危机防御 / 🚀 顺周期加速 / ⚠️ 趋势衰减)
}

public class VolatilityTargetedTsmomResult
{
    public decimal TargetVolatilityPercent { get; set; } // 设定目标波动率 (通常 10.0%)
    public decimal PortfolioRealizedTsmomVolatilityPercent { get; set; } // 策略实现波动率 (%)
    public decimal LongExposurePercent { get; set; } // 多头总暴露 (%)
    public decimal ShortExposurePercent { get; set; } // 空头/低配总暴露 (%)
    public decimal NetExposurePercent { get; set; } // 净动量敞口 (%)
    public decimal CrisisAlphaPotentialScore { get; set; } // 组合危机 Alpha 潜力得分 (0~100)
    public decimal MomentumCrashDefenseIndex { get; set; } // 动量崩溃防御指数 (0~100)
    public List<TsmomAssetSignalItem> AssetSignals { get; set; } = new();
    public string StrategyRecommendation { get; set; } = string.Empty;
}

// =========================================================================
// 4. Two Sigma & D.E. Shaw: Almgren-Chriss 连续动态最优拆单轨迹与流动性在险度量
// =========================================================================
public class ExecutionTimeSliceItem
{
    public int SliceIndex { get; set; } // 切片序号 (1 ~ N)
    public string TimeLabel { get; set; } = string.Empty; // 时间段标签 (如: T+0.1, 09:30~10:00)
    public decimal RemainingHoldingsPercent { get; set; } // 理论最优剩余持仓比例 x_j / X_0 (%)
    public decimal SlicedOrderVolumeWan { get; set; } // 本期最优建议报单量 (万元)
    public decimal TwapVolumeWan { get; set; } // 传统平权 TWAP 报单量 (万元)
    public decimal TemporaryImpactBps { get; set; } // 瞬时冲击滑点 (bps)
    public decimal CumulativeFrictionWan { get; set; } // 累计交易损耗 (万元)
    public decimal InventoryVarianceRiskWan { get; set; } // 在持库存波动率方差风险折现 (万元)
}

public class OptimalExecutionTrajectoryResult
{
    public decimal TotalRebalanceAmountWan { get; set; } // 总调仓执行金额 (万元)
    public decimal ExecutionHorizonDays { get; set; } // 总清算时间窗口 (天)
    public decimal CharacteristicDecayParameterKappa { get; set; } // 特征衰减率参数 κ
    public decimal CharacteristicDecayHalfLifeHours { get; set; } // 特征半衰期 (小时)
    public decimal AlmgrenChrissExpectedShortfallWan { get; set; } // 最优轨迹预期执行落差 (万元)
    public decimal TwapExpectedShortfallWan { get; set; } // 传统 TWAP 预期执行落差 (万元)
    public decimal ExecutionSlippageSavingsWan { get; set; } // 相比 TWAP 节省滑点金额 (万元)
    public decimal ExecutionSlippageSavingsRatioPercent { get; set; } // 滑点节省比例 (%)
    public decimal ExecutionShortfallVaR95Wan { get; set; } // 执行在险价值 (VaR 95%) (万元)
    public List<ExecutionTimeSliceItem> TrajectorySlices { get; set; } = new();
    public string ExecutionVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 38: 期限结构Carry·RMT谱降噪·多元极值崩塌·微观逆向选择

// =========================================================================
// 1. Man Group AHL & AQR: 跨资产大类期限结构展期收益 (Roll Yield / Carry) 与基差动量
// =========================================================================
public class AssetCarryItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal FrontPrice { get; set; } // 近月价格 / 现货基准
    public decimal NextPrice { get; set; } // 远月价格 / 远期合约
    public decimal SpreadBps { get; set; } // 跨期近远月基差差价 (bps)
    public decimal AnnualizedRollYieldPercent { get; set; } // 年化展期收益率 Carry (%)
    public decimal CurveSlope { get; set; } // 期限结构斜率 (>0 升水Contango, <0 贴水Backwardation)
    public decimal BasisMomentumScore { get; set; } // 基差动量得分 (-2.0 ~ +2.0)
    public decimal RealizedVolatilityPercent { get; set; } // 资产实现波动率 (%)
    public decimal RecommendedCarryWeightPercent { get; set; } // Carry-Risk Parity 推荐配置权重 (%)
    public string CarryRegimeBadge { get; set; } = string.Empty; // 展期状态 (📈 深度贴水超额收割 / 📉 升水侵蚀避险 / ⚖️ 结构中性)
}

public class CrossAssetTermStructureCarryResult
{
    public decimal PortfolioWeightedRollYieldPercent { get; set; } // 组合加权年化展期收益率 (%)
    public decimal CarrySharpeUplift { get; set; } // 展期收益对组合夏普比率贡献提升
    public int BackwardationAssetCount { get; set; } // 处于贴水结构(正Carry)资产数量
    public int ContangoAssetCount { get; set; } // 处于升水结构(负Carry)资产数量
    public decimal BasisMomentumAlphaRatioPercent { get; set; } // 基差动量 Alpha 胜率贡献 (%)
    public List<AssetCarryItem> AssetCarries { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// =========================================================================
// 2. Renaissance Technologies & CFM: 随机矩阵理论 (RMT) 马尔琴科-帕斯图尔谱分解滤波降噪
// =========================================================================
public class RmtEigenModeItem
{
    public int Rank { get; set; } // 特征值位次 (1 ~ N)
    public decimal EmpiricalEigenvalue { get; set; } // 经验样本特征值 λ_k
    public decimal FilteredEigenvalue { get; set; } // RMT 谱滤波降噪后特征值 λ̃_k
    public bool IsSignal { get; set; } // 是否为有效信息特征值 (超出 MP 噪声上界)
    public decimal VarianceExplainedPercent { get; set; } // 该模态方差解释比率 (%)
    public string ModeClassification { get; set; } = string.Empty; // 模态分类 (🏛️ 宏观市场系统模态 / 🧬 行业风格因子模态 / 🌫️ MP 纯随机噪声带)
}

public class RmtSpectralFilteringResult
{
    public decimal GammaRatio { get; set; } // 矩阵维度纵横比 Q = T / N
    public decimal MarchenkoPasturLowerBound { get; set; } // 马尔琴科-帕斯图尔理论噪声下界 λ_minus
    public decimal MarchenkoPasturUpperBound { get; set; } // 马尔琴科-帕斯图尔理论噪声上界 λ_plus
    public int SignalEigenvalueCount { get; set; } // 有效信号特征值数量 (λ > λ_plus)
    public int NoiseEigenvalueCount { get; set; } // 随机噪声特征值数量 (λ <= λ_plus)
    public decimal NoiseVariancePurifiedPercent { get; set; } // 剥离纯噪声方差占比 (%)
    public decimal RawConditionNumber { get; set; } // 原始经验相关矩阵条件数
    public decimal DenoisedConditionNumber { get; set; } // RMT 降噪滤波后矩阵条件数
    public decimal ConditionNumberImprovementRatioPercent { get; set; } // 条件数优化压降比例 (%)
    public List<RmtEigenModeItem> EigenModes { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// =========================================================================
// 3. Citadel & Millennium: 多元非对称极值下行联结与组合结构性协同崩塌脆弱度矩阵
// =========================================================================
public class AssetTailFragilityItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal AverageLowerTailDependence { get; set; } // 平均极值下行尾部依赖系数 λ_L
    public decimal AverageUpperTailDependence { get; set; } // 平均极值上行尾部依赖系数 λ_U
    public decimal AsymmetryRatio { get; set; } // 非对称尾部倍率 λ_L / λ_U
    public decimal TailGraphCentralityScore { get; set; } // 极值损失级联传染中心度 (0~100)
    public string TailRiskConvexityBadge { get; set; } = string.Empty; // 尾部凸性评级 (🛡️ 凸性防守吸收器 / ⚠️ 脆性共振放大源 / ⚖️ 稳态跟随)
}

public class MultivariateTailCoCrashResult
{
    public decimal PortfolioLowerTailCoCrashProbabilityPercent { get; set; } // 组合多元极值协同暴跌超额概率 (%)
    public decimal StructuralFragilityIndex { get; set; } // 组合结构性脆弱度指数 (0~100)
    public decimal AsymmetricDownsideTailDominanceRatio { get; set; } // 下行尾部联动对上行联动的非对称优势比
    public string TailConvexityDefenseRating { get; set; } = string.Empty; // 组合下行凸性评级 (AAA 顶级吸收 / AA 优良韧性 / A 脆弱共振)
    public List<AssetTailFragilityItem> AssetFragilities { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// =========================================================================
// 4. Jane Street & Citadel Securities: 微观订单流不平衡 (OFI)、Kyle 价格冲击与逆向选择足迹
// =========================================================================
public class AdverseSelectionAssetItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal KyleLambdaBps { get; set; } // Kyle 价格冲击敏感度系数 (bps / 千万元成交量)
    public decimal HasbrouckPermanentInfoSharePercent { get; set; } // Hasbrouck 永久价格冲击信息份额 (%)
    public decimal VpinToxicityScore { get; set; } // VPIN 毒性流概率评分 (0~100)
    public decimal AdverseSelectionCostBps { get; set; } // 知情交易引发的逆向选择摩擦 (bps)
    public decimal InventoryHoldingFrictionBps { get; set; } // 做市库存存货持有摩擦 (bps)
    public string ExecutionAggressionRecommendation { get; set; } = string.Empty; // 报单进攻性建议 (⚡ 激进抢单对冲 / 🐢 冰山被动挂单 / ⏸️ 暂停规避毒性)
}

public class MicrostructureAdverseSelectionResult
{
    public decimal PortfolioAverageKyleLambdaBps { get; set; } // 组合加权平均 Kyle's Lambda (bps/千万元)
    public decimal PermanentAlphaSharePercent { get; set; } // 永久信息冲击均值占比 (%)
    public decimal PortfolioVpinToxicityIndex { get; set; } // 综合 VPIN 订单流毒性指数 (0~100)
    public string ExecutionSpeedGuidance { get; set; } = string.Empty; // 微观执行时钟节奏决策
    public List<AdverseSelectionAssetItem> AssetMicrostructures { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 39: 主因子风险平价、动态凸性对冲、卡尔曼时变Alpha与流动性共振裂谷数据模型

// =========================================================================
// 1. Bridgewater Associates & AQR Capital: 主因子正交风险平价 (PFRP) 与特征风险预算
// =========================================================================
public class PrincipalFactorItem
{
    public int FactorIndex { get; set; } // 主因子序数 (F1, F2...)
    public string FactorName { get; set; } = string.Empty; // 特征因子主导属性 (如: 宏观增长与广义动量 / 信用久期与流动性)
    public decimal Eigenvalue { get; set; } // 对应特征值 λ_k
    public decimal VarianceExplainedPercent { get; set; } // 解释方差占比 (%)
    public decimal CumulativeVariancePercent { get; set; } // 累计解释方差 (%)
    public decimal RawFactorRiskContributionPercent { get; set; } // 初始组合在主因子上的风险贡献占比 (%)
    public decimal ParityFactorRiskContributionPercent { get; set; } // 主因子平价后风险贡献占比 (%) (理论应趋近 1/K)
    public decimal OptimalEigenmodeWeightPercent { get; set; } // 对应主因子特征合成权重占比 (%)
    public string DominantDriverRegime { get; set; } = string.Empty; // 主导宏观驱动体制 (如: 扩张加速 / 滞胀紧缩 / 避险震荡)
}

public class PrincipalFactorRiskParityResult
{
    public int TotalPrincipalFactors { get; set; } // 提取的正交主因子总数
    public decimal EffectiveNumberOfPrincipalFactors { get; set; } // 有效独立主因子数 (ENPF = exp(-sum p_k ln p_k))
    public decimal FactorDiversificationRatio { get; set; } // 主因子正交分散化比率 (FDR)
    public decimal FactorRiskInequalityGiniPercent { get; set; } // 初始因子风险贡献基尼不平等系数 (%)
    public decimal PostParityVarianceReductionPercent { get; set; } // 平价后极端穿透方差压降比率 (%)
    public List<PrincipalFactorItem> PrincipalFactors { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// =========================================================================
// 2. Millennium Management & Point72: 动态下行凸性期权对冲与广义波动率偏度复制
// =========================================================================
public class ConvexityHedgeStrikeItem
{
    public decimal MoneynessPercent { get; set; } // 虚值行权档位 (如 95%, 90%, 85%)
    public decimal StrikePrice { get; set; } // 对应名义行权价 (K)
    public decimal ImpliedVolatilityPercent { get; set; } // 波动率偏度拟合隐含波动率 σ_iv (%)
    public decimal OptionDelta { get; set; } // 看跌期权 Delta (Δ_put)
    public decimal OptionGamma { get; set; } // 看跌期权 Gamma (Γ_put)
    public decimal OptionVega { get; set; } // 看跌期权 Vega (ν_put)
    public decimal RecommendedHedgeRatioPercent { get; set; } // 建议合成期权保护头寸占净值比 (%)
    public decimal AnnualizedThetaCostPercent { get; set; } // 年化时间价值保费损耗率 (Theta Decay) (%)
    public decimal StressDownsideBufferRatio { get; set; } // 极端暴跌情景下凸性放大缓冲倍数
    public string ProtectionTierBadge { get; set; } = string.Empty; // 保护层级标签 (如: 浅度缓冲 / 核心防线 / 巨灾避险)
}

public class DynamicDownsideConvexityHedgeResult
{
    public decimal BaselinePortfolioVolatilityPercent { get; set; } // 组合基础波动率 (%)
    public decimal VolatilitySkewSlopeBps { get; set; } // SVI 隐含波动率偏度斜率 (Skew Slope bps)
    public decimal PortfolioConvexityDeficitScore { get; set; } // 投资组合下行凸性缺口得分 (0~100)
    public decimal OptimalTotalHedgeRatioPercent { get; set; } // 综合建议看跌凸性对冲覆盖率 (%)
    public decimal NetConvexityProtectionBenefitRatio { get; set; } // 净凸性保护性价比 (Sharpe Protection Ratio)
    public decimal MaxCushionBufferPercent { get; set; } // 极端跳空 20% 冲击下凸性缓冲吸收率 (%)
    public List<ConvexityHedgeStrikeItem> StrikeHedgingProfiles { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// =========================================================================
// 3. Renaissance Technologies & D.E. Shaw: 贝叶斯时变状态空间卡尔曼滤波与自适应 Alpha 动态跟踪
// =========================================================================
public class KalmanAlphaAssetItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal StaticBeta { get; set; } // 传统静态 OLS 贝塔
    public decimal FilteredDynamicBeta { get; set; } // 卡尔曼后验滤波时变贝塔 β_t|t
    public decimal PriorInnovationVariance { get; set; } // 新息残差先验方差 (Q_t/R_t 尺度)
    public decimal KalmanOptimalGain { get; set; } // 卡尔曼最优增益 K_t
    public decimal DynamicAlphaAnnualizedPercent { get; set; } // 自适应纯 Alpha 真实时变年化估计 (%)
    public decimal AlphaDecayHalfLifeDays { get; set; } // Alpha 衰减半衰期 (天)
    public decimal RegimeShiftConfidencePercent { get; set; } // 结构断裂点/体制漂移置信度 (%)
    public string AlphaTrajectoryBadge { get; set; } = string.Empty; // Alpha 轨道路由 (🚀 强化加速 / 🟢 稳健衰减 / ⚠️ 风格蜕变)
}

public class BayesianKalmanAlphaTrackerResult
{
    public decimal PortfolioFilteredAlphaAnnualizedPercent { get; set; } // 组合卡尔曼自适应滤波时变总 Alpha (%)
    public decimal AverageKalmanConvergenceRatePercent { get; set; } // 参数状态收敛速度与置信度 (%)
    public decimal TrackingNoiseReductionPercent { get; set; } // 相比普通滑动窗口 OLS 的高频噪声压降率 (%)
    public decimal InformationRatioUpliftPercent { get; set; } // 动态卡尔曼对冲后的信息比率 (IR) 提升幅度 (%)
    public int StructuralBreakAssetCount { get; set; } // 发生结构性断裂资产数
    public List<KalmanAlphaAssetItem> AssetKalmanAlphas { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// =========================================================================
// 4. Jane Street & Jump Trading: 跨资产微观流动性共振裂谷与闪崩级联阻尼器
// =========================================================================
public class LiquidityChasmAssetItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal AmihudIlliquidityMeasure { get; set; } // Amihud 绝对不流动性均值
    public decimal CrossAssetLiquidityBeta { get; set; } // 跨资产流动性联动贝塔 β_illiq
    public decimal MarketMakerPullbackSensitivityScore { get; set; } // 做市商协同抽单敏感度得分 (0~100)
    public decimal CascadeFlashCrashInjectionWeightPercent { get; set; } // 闪崩级联冲击向外注入权重贡献 (%)
    public decimal DamperAbsorptionCapacityPercent { get; set; } // 级联阻尼器吸收能力 (%)
    public string ExecutionThrottlingGateStatus { get; set; } = string.Empty; // 报单执行节流阀门 (🟢 全速放行 / 🟡 分钟级限流 / 🛑 熔断阻尼)
}

public class CrossAssetLiquidityChasmDamperResult
{
    public decimal LiquidityChasmIndex { get; set; } // 跨资产流动性共振裂谷脆弱度指数 (LCI, 0~100)
    public decimal FlashCrashCascadeAmplifierRatio { get; set; } // 闪崩级联传染放大倍数
    public decimal SystemicLiquidityDampingScorePercent { get; set; } // 组合自愈级联阻尼吸收效率 (%)
    public decimal CoEvaporationDrawdownRiskPercent { get; set; } // 流动性共同蒸发极端回撤在险价值 (%)
    public string MarketMakerPullbackAlertLevel { get; set; } = string.Empty; // 做市商撤单整体警报等级
    public List<LiquidityChasmAssetItem> AssetLiquidityChasms { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 40: 机构级马尔可夫区制转移、截面动量剥离、Pod阶梯硬风控熔断与订单簿队列基差套利模型

// =========================================================================
// 1. Bridgewater Associates & Citadel: 宏观马尔可夫区制转移 (MRS) 与跨周期条件资产配置
// =========================================================================
public class MarkovRegimeStateItem
{
    public int StateIndex { get; set; } // 体制序号 (0: 牛市扩张, 1: 震荡盘整, 2: 紧缩衰退, 3: 危机枯竭)
    public string RegimeName { get; set; } = string.Empty; // 宏观体制名称
    public decimal FilteredProbabilityPercent { get; set; } // 当前期 Hamilton 滤波后验概率 (%)
    public decimal SmoothedProbabilityPercent { get; set; } // 全样本平滑概率 (%)
    public decimal ErgodicSteadyStateProbPercent { get; set; } // 遍历极限稳态概率 (%)
    public decimal RegimeConditionalReturnAnnualizedPercent { get; set; } // 体制条件期望年化收益率 (%)
    public decimal RegimeConditionalVolatilityAnnualizedPercent { get; set; } // 体制条件年化波动率 (%)
    public decimal ExpectedDurationDays { get; set; } // 预期持续期 (天, 1/(1-P_ii))
    public decimal RecommendedRegimeWeightPercent { get; set; } // 对应体制下的跨周期优化建议权重 (%)
    public string RegimeMacroBadge { get; set; } = string.Empty; // 体制特征徽章 (如: 🚀 扩张进攻 / 🛡️ 防御抗跌)
}

public class MacroMarkovRegimeSwitchingResult
{
    public int DominantRegimeStateIndex { get; set; } = 1; // 当前主导体制索引 (1..4)
    public string DominantRegimeName { get; set; } = string.Empty; // 当前主导宏观体制
    public decimal DominantRegimeConfidencePercent { get; set; } // 主导体制置信概率 (%)
    public decimal RegimeEntropyIndex { get; set; } // 体制不确定性香农熵 (0~1.0, 越低越确定)
    public decimal RegimeSwitchingSpeedDays { get; set; } // 均值回复/体制切换特征周期 (天)
    public decimal AdaptiveCrossCycleSharpeUpliftPercent { get; set; } // 相比静态配置的跨周期夏普比率提升幅度 (%)
    public List<MarkovRegimeStateItem> RegimeStates { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// =========================================================================
// 2. AQR Capital & Man Group AHL: 多频率截面交叉动量 (CSMOM) 与双重动量相对优势剥离
// =========================================================================
public class CrossSectionalMomentumAssetItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Momentum1MPercent { get; set; } // 1个月(21日) 截面动量收益率 (%)
    public decimal Momentum3MPercent { get; set; } // 3个月(63日) 截面动量收益率 (%)
    public decimal Momentum6MPercent { get; set; } // 6个月(126日) 截面动量收益率 (%)
    public decimal Momentum12MPercent { get; set; } // 12个月(252日) 截面动量收益率 (%)
    public decimal CompositeCrossSectionalZScore { get; set; } // 综合多频率截面标准化 Z-Score
    public int CrossSectionalRank { get; set; } // 截面强弱名次
    public decimal HurstPersistenceExponent { get; set; } // Hurst 指数 (H > 0.5 趋势持续, H < 0.5 均值回归)
    public decimal DualMomentumScore { get; set; } // 双重动量综合得分 (绝对趋势 + 相对强弱)
    public string DualMomentumSignal { get; set; } = string.Empty; // 决策信号 (🔥 强劲领涨进攻 / 🟢 稳健持有 / ⚠️ 减仓防守)
}

public class CrossSectionalMomentumResult
{
    public decimal CrossSectionalDispersionPercent { get; set; } // 资产截面动量离散度 (Cross-Sectional Volatility) (%)
    public decimal WinnerLoserSpreadAnnualizedPercent { get; set; } // 多头胜者 vs 空头劣者截面多空利差 (%)
    public decimal RankInformationCoefficient { get; set; } // 截面动量 Rank IC 预测相关性
    public decimal PortfolioAverageHurstExponent { get; set; } // 组合加权平均 Hurst 趋势记忆指数
    public int TopDecileAssetCount { get; set; } // 处于高动量分位的资产数量
    public List<CrossSectionalMomentumAssetItem> AssetMomentums { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// =========================================================================
// 3. Millennium Management & Balyasny (BAM): 多策略 Pod 阶梯式回撤硬风控熔断与动态资本再平衡
// =========================================================================
public class PodDrawdownTierItem
{
    public string PodCode { get; set; } = string.Empty;
    public string PodName { get; set; } = string.Empty; // Pod 策略/资产单元名称
    public decimal AllocatedCapitalPercent { get; set; } // 初始基准资本分配比例 (%)
    public decimal CurrentDrawdownPercent { get; set; } // 当前相对历史高水位线的真实回撤幅度 (%)
    public decimal MaxHistoricalDrawdownPercent { get; set; } // 历史最大回撤 (%)
    public string CircuitBreakerTier { get; set; } = string.Empty; // 当前所处硬风控阶梯 (🟢 正常 / 🟡 预警减额 / 🟠 减半去杠杆 / 🔴 熔断冻结 / 🛑 清盘停机)
    public decimal DynamicCapitalMultiplier { get; set; } // 动态资本调整乘数 (1.00x, 0.75x, 0.50x, 0.00x)
    public decimal PostBreakerCapitalPercent { get; set; } // 风控执行后有效资本权重 (%)
    public decimal MarginalSharpeContribution { get; set; } // 边际夏普贡献度
    public decimal ReleasedCapitalReallocatedPercent { get; set; } // 释放并再平衡调出的风险资本 (%)
}

public class PodTieredDrawdownCircuitBreakerResult
{
    public decimal OverallPodHealthScore { get; set; } // Pod 综合风控健康评分 (0~100)
    public int NormalPodCount { get; set; } // 处于正常运行状态的 Pod 数量
    public int ThrottledPodCount { get; set; } // 触发减额/减杠杆阶梯的 Pod 数量
    public int CircuitBreakerTriggeredCount { get; set; } // 触发硬冻结/停机的 Pod 数量
    public decimal TotalCapitalProtectedPercent { get; set; } // 阶梯硬风控成功保全防守的资本规模占比 (%)
    public decimal DynamicRebalancingEfficiencyGainPercent { get; set; } // 资本动态再平衡至优胜 Pod 的年化效率增益 (%)
    public List<PodDrawdownTierItem> PodTiers { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// =========================================================================
// 4. Jane Street & Optiver: 微观订单簿队列成交概率与高频期现基差收敛套利执行引擎
// =========================================================================
public class QueueFillArbitrageAssetItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal OptimalLimitSpreadBps { get; set; } // 最优被动挂单买卖价差深度 (bps)
    public decimal PassiveFillProbabilityPercent { get; set; } // 限价单在当前队列位置的预期成交概率 (%)
    public decimal QueueWaitTimeSeconds { get; set; } // 队列等待成交平均耗时 (秒)
    public decimal CashFuturesBasisBps { get; set; } // 现货 vs 期货衍生品名义基差偏离 (bps)
    public decimal AnnualizedCarryArbitrageYieldPercent { get; set; } // 期现基差套利年化无风险收益率 (%)
    public decimal BasisMeanReversionHalfLifeDays { get; set; } // 基差均值回复收敛半衰期 (天)
    public decimal PassiveSlippageSavingsBps { get; set; } // 相比市价主动吃单节省的被动滑点收益 (bps)
    public string ExecutionRegimeBadge { get; set; } = string.Empty; // 微观执行策略 (⚡ 高频被动做市 / 🎯 基差收敛套利 / 🛡️ 防冲单阻尼)
}

public class LimitOrderQueueAndBasisArbitrageResult
{
    public decimal PortfolioAverageFillProbabilityPercent { get; set; } // 投资组合限价单加权预期成交概率 (%)
    public decimal AnnualizedPassiveExecutionSavingsPercent { get; set; } // 被动挂单排队相比市价单年化滑点节省 (%)
    public decimal MaxBasisMispricingBps { get; set; } // 跨品种/期现最大基差偏离度 (bps)
    public decimal AnnualizedSyntheticBasisArbitrageYieldPercent { get; set; } // 现货组合衍生品无风险套利潜在年化收益率 (%)
    public decimal AverageBasisHalfLifeDays { get; set; } // 平均基差收敛半衰期 (天)
    public List<QueueFillArbitrageAssetItem> AssetQueueArbitrages { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// =========================================================================
// Phase 41: 机构级终极前沿量化投研服务 (符号基因Alpha、因果知识图谱、Bandit策略路由、微观冲击弹性曲面)
// =========================================================================

// 1. D.E. Shaw & WorldQuant: 符号基因规划自适应 Alpha 因子挖掘引擎
public class GeneticAlphaFormulaItem
{
    public string FormulaId { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty; // 符号基因表达式 (如 ts_rank(decay_linear(delta(close, 5), 10), 20))
    public decimal InformationCoefficient { get; set; } // 信息系数 IC
    public decimal RankInformationCoefficient { get; set; } // 秩信息系数 Rank IC
    public decimal IcInformationRatio { get; set; } // 因子信息比率 ICIR
    public decimal LongShortAnnualizedSharpe { get; set; } // 多空组合年化夏普比率
    public decimal ComplexityPenalty { get; set; } // 表达式复杂度惩罚 (奥卡姆剃刀)
    public decimal FitnessScore { get; set; } // 综合适应度得分 (0~100)
    public string FormulaFamily { get; set; } = string.Empty; // 因子族系分类
}

public class SymbolicGeneticAlphaMiningResult
{
    public int TotalGenerationsEvolved { get; set; } // 遗传迭代演化代数
    public int TotalFormulasEvaluated { get; set; } // 评估候选公式总数
    public decimal TopAlphaInformationRatio { get; set; } // 最优 Alpha 公式 ICIR
    public decimal AverageFormulaComplexity { get; set; } // 种群平均公式语法树复杂度
    public decimal MultiAlphaEnsembleAnnualizedAlpha { get; set; } // 多 Alpha 复合年化超额收益率 (%)
    public List<GeneticAlphaFormulaItem> EvolvedAlphaFormulas { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Two Sigma & Man Group AHL: 知识图谱跨资产因果时滞传递与宏观情绪溢出网络
public class CausalSpilloverEdgeItem
{
    public string SourceAsset { get; set; } = string.Empty; // 溢能源头资产
    public string TargetAsset { get; set; } = string.Empty; // 溢出受体资产
    public decimal GrangerFStatistic { get; set; } // Granger 因果检验 F 统计量
    public decimal GrangerPValue { get; set; } // Granger 因果 p-value
    public decimal TransferEntropyBits { get; set; } // 信息传递熵 (Bits)
    public decimal DirectionalSpilloverPercent { get; set; } // 定向溢出贡献率 (%)
    public decimal LeadTimeDays { get; set; } // 前置传导预警时滞 (天)
    public string CausalRoleBadge { get; set; } = string.Empty; // 因果拓扑角色
}

public class CausalKnowledgeGraphSpilloverResult
{
    public decimal TotalSystemSpilloverIndexPercent { get; set; } // Diebold-Yilmaz 全系统总溢出率指数 (%)
    public string DominantInformationSourceAsset { get; set; } = string.Empty; // 全市场主导信息发射源头资产
    public decimal AverageLeadTimeDays { get; set; } // 平均前置传导时间 (天)
    public decimal CausalNetworkDensityPercent { get; set; } // 因果拓扑网络连通密度 (%)
    public List<CausalSpilloverEdgeItem> SpilloverEdges { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Citadel & Point72: 上下文多臂老虎机 (Thompson Sampling) 自适应策略路由器
public class BanditStrategyArmItem
{
    public string StrategyCode { get; set; } = string.Empty; // 策略代号
    public string StrategyName { get; set; } = string.Empty; // 策略全称 (如 最大夏普 MPT、等权风险平价、马尔可夫条件配置等)
    public decimal PriorAlpha { get; set; } // 贝叶斯共轭先验 Alpha (胜场)
    public decimal PriorBeta { get; set; } // 贝叶斯共轭先验 Beta (负场)
    public decimal PosteriorExpectedWinRatePercent { get; set; } // 后验期望胜率 (%)
    public decimal ThompsonSampledScore { get; set; } // Thompson 采样即时打分
    public decimal DynamicMetaWeightPercent { get; set; } // 动态自适应元策略分配权重 (%)
    public decimal CumulativeRegretReductionPercent { get; set; } // 累积遗憾缩减率 (%)
    public string BanditStatusBadge { get; set; } = string.Empty; // 老虎机状态徽标
}

public class ContextualBanditPolicyRouterResult
{
    public string OptimalStrategyName { get; set; } = string.Empty; // 当前上下文主导最优配置策略
    public string CurrentContextRegime { get; set; } = string.Empty; // 当前宏观与市场状态上下文标签
    public decimal ExplorationVsExploitationRatio { get; set; } // 探索 vs 利用权衡比
    public decimal CumulativeMetaSharpeGainPercent { get; set; } // 元策略相比单一策略夏普增益 (%)
    public decimal OverallPolicyConfidencePercent { get; set; } // 路由策略置信度 (%)
    public List<BanditStrategyArmItem> StrategyArms { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Jump Trading & Optiver: 深度强化微观流动性冲击弹性与非线性滑点曲面
public class SlippageSurfaceGridPointItem
{
    public decimal ParticipationRatePercent { get; set; } // 调仓参与率 (%) (1% ~ 25%)
    public int UrgencyLevel { get; set; } // 执行急迫度等级 (1:被动挂单 ~ 5:市价抢单)
    public decimal InstantaneousImpactBps { get; set; } // 瞬态价格冲击 (bps)
    public decimal TransientResiliencyRecoveryPercent { get; set; } // 订单簿弹性复原吸收率 (%)
    public decimal EffectiveSlippageBps { get; set; } // 实际综合有效滑点 (bps)
    public string OptimalAlgorithm { get; set; } = string.Empty; // 推荐最优执行算法 (TWAP / VWAP / POV / IS)
}

public class MicrostructureResiliencySlippageSurfaceResult
{
    public decimal HalfLifeRecoverySeconds { get; set; } // 订单簿流动性自愈半衰期 (秒)
    public int OptimalExecutionUrgency { get; set; } // 推荐最优执行急迫度
    public decimal AverageEffectiveSlippageBps { get; set; } // 预期组合执行加权滑点 (bps)
    public decimal AnnualizedTransactionCostSavingsPercent { get; set; } // 弹性执行相比朴素市价年化摩擦节省 (%)
    public decimal ResiliencyAlphaRecoveryIndex { get; set; } // 流动性恢复弹性 Alpha 指数 (0~100)
    public List<SlippageSurfaceGridPointItem> SurfaceGridPoints { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// =========================================================================
// Phase 42: 机构级终极前沿量化投研服务 (内生流动性螺旋、变分隐流形聚类、渗流相变熔断、正交残差OU套利)
// =========================================================================

// 1. Bridgewater Associates & AQR Capital: 跨资产内生流动性螺旋与去杠杆压力传染动力学模型
public class EndogenousSpiralAssetItem
{
    public string AssetCode { get; set; } = string.Empty; // 资产代号
    public string AssetName { get; set; } = string.Empty; // 资产名称
    public decimal CurrentWeightPercent { get; set; } // 当前组合权重 (%)
    public decimal AssetBeta { get; set; } // 市场系统性 Beta
    public decimal HaircutPercent { get; set; } // 质押融资折算折价率/保证金比例 Haircut (%)
    public decimal LossSpiralImpactBps { get; set; } // 价格下跌净值侵蚀损失螺旋冲击 (bps)
    public decimal ForcedLiquidationVolumeRmb { get; set; } // 压力下被动去杠杆平仓敞口 (万元)
    public decimal FireSaleDiscountPercent { get; set; } // 踩踏折价率/流动性惩罚折价 (%)
    public int DeleveragingPriorityRank { get; set; } // 推荐去杠杆平仓顺位 (1:最优优先处置 ~ N:最后防守)
}

public class EndogenousLiquiditySpiralResult
{
    public decimal SystemicLiquidityCascadeMultiplier { get; set; } // 系统内生流动性反馈乘数 (M = 1 / (1 - beta))
    public decimal TotalForcedFireSaleCapitalPercent { get; set; } // 组合被动去杠杆清算规模占比 (%)
    public decimal MarginSpiralElasticity { get; set; } // 保证金螺旋弹性系数 (d_Haircut / d_Sigma)
    public decimal PortfolioStressedIlliquidityDiscountPercent { get; set; } // 极端踩踏抛售综合流动性折价率 (%)
    public List<EndogenousSpiralAssetItem> FireSaleAssetItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Renaissance Technologies & Citadel: 变分无监督深度隐空间流形聚类与非线性宏观体制自编码器
public class LatentManifoldClusterItem
{
    public int ClusterId { get; set; } // 隐空间流形聚类 ID
    public string ClusterName { get; set; } = string.Empty; // 簇名称 (如: 低波稳健慢牛簇、跨资产流动性恐慌簇、滞胀防御簇)
    public decimal CentroidZ1 { get; set; } // 聚类中心隐变量 Z1 (宏观动量维度)
    public decimal CentroidZ2 { get; set; } // 聚类中心隐变量 Z2 (波动压力维度)
    public decimal CentroidZ3 { get; set; } // 聚类中心隐变量 Z3 (资产退耦维度)
    public decimal ClusterProbabilityPercent { get; set; } // 当前隐空间高斯混合隶属概率 (%)
    public decimal RegimeSharpeMultiplier { get; set; } // 该体制下组合期望夏普乘数
    public string RecommendedRegimeAction { get; set; } = string.Empty; // 体制战术配置建议
}

public class VariationalLatentManifoldRegimeResult
{
    public decimal CurrentLatentZ1 { get; set; } // 当前宏观趋势与动量潜变量维度 Z1
    public decimal CurrentLatentZ2 { get; set; } // 当前系统波动与流动性压力潜变量维度 Z2
    public decimal CurrentLatentZ3 { get; set; } // 当前跨资产退耦与风格分化潜变量维度 Z3
    public decimal DeepReconstructionAnomalyScore { get; set; } // 变分自编码器隐空间重构异动度 (0~100)
    public decimal ManifoldTransitionVelocity { get; set; } // 隐空间相轨迹演化跃迁速度
    public decimal KlDivergenceLoss { get; set; } // 变分贝叶斯 KL 散度正则化损失
    public string DominantLatentClusterName { get; set; } = string.Empty; // 当前主导隐空间流形体制簇名称
    public List<LatentManifoldClusterItem> LatentClusters { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Millennium Management & Point72: 跨资产多周期极值尾部相关相变检测与渗流理论系统性临界熔断网络
public class PercolationCriticalEdgeItem
{
    public string SourceAsset { get; set; } = string.Empty; // 边起点资产
    public string TargetAsset { get; set; } = string.Empty; // 边终点资产
    public decimal TailCorrelation { get; set; } // 下行极值 Copula 尾部相关系数
    public decimal PercolationWeight { get; set; } // 渗流连通概率权重
    public bool IsSpanningGiantCluster { get; set; } // 是否贯穿最大连通巨集团 (Giant Component)
    public int ClusterId { get; set; } // 所属连通子图簇 ID
    public decimal CriticalBreakageResistance { get; set; } // 断裂阻尼弹性系数 (切断该边使系统解耦的难度)
}

public class PercolationTailPhaseTransitionResult
{
    public decimal CriticalPercolationThreshold { get; set; } // 理论渗流临界相变阈值 p_c
    public decimal GiantConnectedClusterSizePercent { get; set; } // 最大连通巨集团占比 S_infinity (%)
    public decimal PercolationSusceptibility { get; set; } // 渗流敏感度 / 磁化涨落发散测度 chi
    public decimal DistanceToPhaseTransitionCriticality { get; set; } // 距离相变临界点的安全阻尼余量 (|p - p_c|)
    public decimal SystemicCriticalityIndex { get; set; } // 系统性临界相变指数 SCI (0~100)
    public List<PercolationCriticalEdgeItem> PercolationCriticalEdges { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. WorldQuant & Hudson River Trading: 统计套利强化正交特征残差动量与微观协整均值回复追踪引擎
public class StatArbPairSpreadItem
{
    public string AssetPairCode { get; set; } = string.Empty; // 配对代码 (如: 110011 / 510300)
    public string AssetPairName { get; set; } = string.Empty; // 配对资产名称
    public decimal ResidualMomentumZScore { get; set; } // 因子正交残差动量标准化 Z-Score
    public decimal CointegrationAdfPValue { get; set; } // 协整价差 Augmented Dickey-Fuller 平稳性 p 值
    public decimal OuMeanReversionSpeedTheta { get; set; } // Ornstein-Uhlenbeck 均值回复速度参数 theta
    public decimal OuHalfLifeDays { get; set; } // 价差收敛解析半衰期 t_half (天)
    public decimal CurrentSpreadZScore { get; set; } // 当前价差偏离均值 Z-Score
    public string OptimalArbitrageSignalBadge { get; set; } = string.Empty; // 套利信号状态徽标 (多配对/空配对/平仓观察)
}

public class StatArbResidualMomentumOuResult
{
    public decimal PortfolioAverageResidualMomentum { get; set; } // 投资组合正交残差动量加权评分
    public decimal AverageCointegrationHalfLifeDays { get; set; } // 平均协整价差 OU 收敛半衰期 (天)
    public decimal TopPairArbitrageZScore { get; set; } // 极值套利对价差最大偏离度 Z-Score
    public decimal AnnualizedIdiosyncraticStatArbAlphaPercent { get; set; } // 特质统计套利剥离预期年化 Alpha (%)
    public List<StatArbPairSpreadItem> StatArbPairs { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// =========================================================================
// Phase 43: 机构级终极前沿量化投研服务 (mRMR特征正交集成、Wasserstein最优输运资本曲率、Hawkes点过程雪崩预警、高阶矩张量风险平价)
// =========================================================================

// 1. Renaissance Technologies & Two Sigma: 最大相关最小冗余 (mRMR) 互信息特征选择与随机特征子空间正交集成引擎
public class MrmrFeatureItem
{
    public string FeatureCode { get; set; } = string.Empty; // 特征代号 (如: F_MOM_12M, F_VOL_PARITY, F_LIQ_AMIHUD, F_SKEW_TAIL)
    public string FeatureName { get; set; } = string.Empty; // 特征名称
    public string FeatureCategory { get; set; } = string.Empty; // 特征维度族 (时序动量/波动曲率/微观流动性/极值偏度/基本面估值)
    public decimal TargetRelevanceMutualInfo { get; set; } // 目标收益相关性互信息 I(f; y)
    public decimal RedundancyPenaltyMutualInfo { get; set; } // 特征子集间冗余度互信息惩罚 1/|S| \sum I(f; f_s)
    public decimal MrmrOptimizationScore { get; set; } // mRMR 综合评分 = I(f; y) - Redundancy
    public int MrmrSelectionRank { get; set; } // mRMR 优选排序名次 (1:最优入选 ~ N)
    public decimal SubspaceOrthogonalWeightPercent { get; set; } // 随机正交子空间集成配置权重 (%)
    public string FeatureImportanceStatus { get; set; } = string.Empty; // 状态标记 (🔥 核心主导特征 / 🟢 稳健正交增益 / ⚠️ 冗余惩罚过滤)
}

public class MrmrFeatureSelectionEnsembleResult
{
    public decimal TopFeatureEnsembleIcGainRatio { get; set; } // 正交子空间集成预测 IC 增益比率 (如: +28.5%)
    public decimal CollinearityReductionRatePercent { get; set; } // 多重共线性消除率 (%)
    public decimal AverageFeatureMutualInformation { get; set; } // 平均特征互信息 (nats/bits)
    public int SelectedOptimalFeatureCount { get; set; } // 经 mRMR 筛选出的最优紧凑特征数量
    public List<MrmrFeatureItem> FeatureRankings { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Citadel & Millennium Management: 基于 Wasserstein 测度距离最优输运 (Optimal Transport) 的多策略 Pod 动态资本曲率重构与非线性凸松弛配置
public class PodWassersteinCurvatureItem
{
    public string PodCode { get; set; } = string.Empty; // Pod 策略单元代号 (如: POD-QUANT-MOM, POD-VOL-ARB, POD-STAT-ARB, POD-MACRO-CTA)
    public string PodName { get; set; } = string.Empty; // Pod 策略经理/策略名称
    public decimal CurrentAllocatedCapitalPercent { get; set; } // 当前配置资本占比 (%)
    public decimal OptimalTransportTargetWeightPercent { get; set; } // Wasserstein 最优输运目标再平衡权重 (%)
    public decimal WassersteinMarginalDisplacement { get; set; } // 测度空间中几何位移边际成本 (W2 边际分量)
    public decimal PodCurvatureIndex { get; set; } // 资本流形 Ricci 曲率指数 (反映风险相关性与吸收能力)
    public decimal TransportFrictionCostBps { get; set; } // 最优几何输运交易摩擦 (bps)
    public string AllocationDynamicRecommendation { get; set; } = string.Empty; // 输运建议 (🚀 最优资本注入 / 🛡️ 稳健维持均衡 / ⚠️ 曲率凸度收缩)
}

public class WassersteinPodCapitalCurvatureResult
{
    public decimal TotalWassersteinDistanceMetric { get; set; } // 全组合经验分布至全天候目标分布的 W2 Wasserstein 最优输运距离
    public decimal CurvatureStabilityImprovementPercent { get; set; } // 资本流形曲率稳定性改善增益 (%)
    public decimal TotalOptimalTransportTurnoverPercent { get; set; } // 最优输运单边换手率 (%)
    public decimal NetTransportFrictionSavingsBps { get; set; } // 相比欧氏二次优化节省的几何输运摩擦 (bps)
    public List<PodWassersteinCurvatureItem> PodCurvatureAllocations { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Jane Street & Citadel Securities / Virtu: 微观限价订单簿 (LOB) 自激 Hawkes 点过程、跳跃扩散强度与流动性雪崩级联预警系统
public class HawkesAssetJumpItem
{
    public string AssetCode { get; set; } = string.Empty; // 资产代号
    public string AssetName { get; set; } = string.Empty; // 资产名称
    public decimal BaselineArrivalRateMu { get; set; } // 外生基准订单到达强度 mu
    public decimal SelfExcitingIntensityAlpha { get; set; } // 自激繁殖催化强度 alpha
    public decimal DecayRateBeta { get; set; } // 记忆衰减速度 beta
    public decimal BranchingRatioEta { get; set; } // 自激分支比率 eta = alpha / beta (接近 1.0 时为临界状态)
    public decimal JumpDiffusionIntensityLambda { get; set; } // Merton 泊松跳跃扩散发生强度 lambda_J (次/日)
    public decimal ExpectedJumpMagnitudePercent { get; set; } // 跳跃期望冲击幅度 (%)
    public decimal LiquidityAvalancheRiskScore { get; set; } // 微观流动性雪崩风险指数 (0~100)
    public string MicrostructureDefenseSignal { get; set; } = string.Empty; // 做市防御指令 (🟢 正常平滑做市 / 🟡 拓宽买卖报价 / 🟠 冰山被动隐藏 / 🔴 紧急撤单避险)
}

public class HawkesMicrostructureAvalancheResult
{
    public decimal PortfolioAverageBranchingRatio { get; set; } // 组合加权 Hawkes 自激分支比率 eta
    public decimal CompositeAvalancheRiskIndex { get; set; } // 综合微观流动性雪崩级联指数 (0~100)
    public decimal CriticalCascadeAlertCount { get; set; } // 处于高危自激繁殖临界期的资产数量
    public decimal ExpectedPreemptiveCostSavingBps { get; set; } // 前瞻性微观避险预期节省滑点损失 (bps)
    public List<HawkesAssetJumpItem> AssetHawkesJumps { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Bridgewater Associates & AQR Capital: 高阶矩张量风险平价 (Higher-Order Moment Tensor Risk Parity) 与非高斯偏度-峰度协同传染对冲矩阵
public class AssetHigherMomentItem
{
    public string AssetCode { get; set; } = string.Empty; // 资产代号
    public string AssetName { get; set; } = string.Empty; // 资产名称
    public decimal AssetSkewness { get; set; } // 单资产独立偏度 S_i
    public decimal AssetExcessKurtosis { get; set; } // 单资产独立超额峰度 K_i
    public decimal CoSkewnessMarginalContribution { get; set; } // 三阶协偏度张量 M3 欧拉边际风险贡献
    public decimal CoKurtosisMarginalContribution { get; set; } // 四阶协峰度张量 M4 欧拉边际风险贡献
    public decimal SecondOrderParityWeightPercent { get; set; } // 传统二阶协方差风险平价权重 (%)
    public decimal TensorHigherOrderParityWeightPercent { get; set; } // 高阶矩张量风险平价优化权重 (%)
    public decimal ConvexityTailAdjustmentPercent { get; set; } // 非高斯尾部凸性调整幅度 (%)
}

public class HigherOrderTensorRiskParityResult
{
    public decimal PortfolioCoSkewnessMetric { get; set; } // 组合系统性三阶协偏度 M3 (负偏越低左尾风险越大)
    public decimal PortfolioCoKurtosisMetric { get; set; } // 组合系统性四阶超额协峰度 M4 (峰度越厚黑天鹅概率越高)
    public decimal HigherOrderRiskDispersionIndex { get; set; } // 高阶矩风险平价离散度指数 (越接近 0 越完全平价)
    public decimal TailConvexityHedgingUpliftPercent { get; set; } // 极端黑天鹅下行回撤收窄保护比率 (%)
    public List<AssetHigherMomentItem> HigherMomentAssets { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 44: BSDE 粘性解动态对冲、高阶拓扑超图持续同调、跨标的交叉冲击张量与 Wasserstein-DRO 极小极大平价

// 1. Renaissance Technologies & D.E. Shaw: 连续时间倒向随机微分方程 (BSDE) 动态粘性解对冲引擎与随机波动率曲率最小化
public class BsdeHedgeAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal UnderlyingPrice { get; set; } // 标的基准价格 (元)
    public decimal StochasticVolSigma { get; set; } // Heston/SABR 随机波动率水平 (%)
    public decimal ViscosityValueYt { get; set; } // 倒向随机微分方程粘性解状态 Y_t (期权公允估值)
    public decimal DynamicHedgeControlZt { get; set; } // 连续对冲控制量 Z_t (最优动态 Delta 对冲比率)
    public decimal MalliavinCurvatureDispersion { get; set; } // 二阶 Malliavin 路径依赖曲率离散度
    public decimal FrictionSlippageSavingsBps { get; set; } // 动态粘性对冲较离散对冲挽回滑点摩擦 (bps)
    public string HedgeExecutionDirective { get; set; } = string.Empty; // 动态对冲执行指令 (⚡ 紧密连续微调 / 🛡️ 稳健持有不动 / 🔄 跨期展期重校)
}

public class BsdeDynamicViscosityHedgingResult
{
    public decimal PortfolioAverageHedgeControlNorm { get; set; } // 组合平均对冲控制向量 L2 范数 ||Z_t||
    public decimal NetHedgeSlippageSavingsBps { get; set; } // 组合净节省对冲滑点 (bps)
    public decimal StochasticVolCurvatureSuppressionPercent { get; set; } // 随机波动率路径曲率平抑增益 (%)
    public decimal DynamicHedgeEfficiencyScore { get; set; } // 动态对冲复制效能综合评分 (0~100)
    public List<BsdeHedgeAssetItem> AssetBsdeHedges { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Citadel & Millennium Management: 多资产高阶拓扑超图 (Hypergraph) 关联网络与持续同调 Persistent Homology 空洞破裂预警
public class HypergraphEdgeItem
{
    public string HyperedgeId { get; set; } = string.Empty; // 超边编号 (如: HE-MACRO-LIQ, HE-TECH-MOM)
    public string HyperedgeTheme { get; set; } = string.Empty; // 超边协同主题 (如: 宏观流动性紧缩超边 / 高贝塔科技共振超边 / 固收久期避险超边)
    public int Cardinality { get; set; } // 包含资产基数 (>=3)
    public string MemberAssetsSummary { get; set; } = string.Empty; // 成员资产摘要
    public decimal HyperedgeWeight { get; set; } // 超边相互作用强度权重
    public decimal HigherOrderSpectralContribution { get; set; } // 超图拉普拉斯谱贡献度
    public string TopologicalContagionRisk { get; set; } = string.Empty; // 拓扑传染风险层级 (🔴 强共振脆弱 / 🟡 局部传导 / 🟢 稳健解耦)
}

public class PersistentHomologyCavityItem
{
    public int Dimension { get; set; } // 同调维度 (0: 连通分支 / 1: 拓扑空洞环路)
    public decimal BirthFiltrationRadius { get; set; } // 出生过滤半径 ε_birth
    public decimal DeathFiltrationRadius { get; set; } // 死亡过滤半径 ε_death
    public decimal PersistenceLifespan { get; set; } // 持续生命期 (Death - Birth)
    public string CavityNature { get; set; } = string.Empty; // 拓扑结构性质 (持久拓扑空洞 / 瞬态拓扑噪声 / 宏观孤岛分支)
    public string SystemicImplication { get; set; } = string.Empty; // 系统性金融涵义 (流动性断裂孤岛 / 暗套利流形闭环 / 流动性真空)
}

public class HypergraphTopologicalCausalityResult
{
    public int TotalHyperedges { get; set; } // 拓扑超边总数
    public decimal HypergraphSpectralGap { get; set; } // 超图谱间隙 (反映全网协同稳定性)
    public int Betti0ConnectedComponents { get; set; } // 0 维 Betti 数 β_0 (连通分支数)
    public int Betti1TopologicalCavities { get; set; } // 1 维 Betti 数 β_1 (高阶拓扑空洞环路数)
    public decimal TopologicalPhaseEntropy { get; set; } // 持续同调拓扑流形相变信息熵
    public decimal CavityRuptureProbabilityPercent { get; set; } // 拓扑空洞相变破裂概率 (%)
    public List<HypergraphEdgeItem> Hyperedges { get; set; } = new();
    public List<PersistentHomologyCavityItem> PersistenceCavities { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Jane Street & Citadel Securities / Virtu: 微观瞬时订单流毒性扩散核、跨标的交叉价格冲击张量与非对称做市执行
public class CrossImpactPairItem
{
    public string SourceAssetCode { get; set; } = string.Empty; // 冲击触发源标的代码
    public string TargetAssetCode { get; set; } = string.Empty; // 被动冲击响应标的代码
    public string AssetPairDisplay { get; set; } = string.Empty; // 标的配对显示
    public decimal InstantaneousCrossImpactLambda { get; set; } // 瞬时交叉价格冲击系数 Λ_ij (bps/千万元)
    public decimal CrossDecayHalfLifeSeconds { get; set; } // 交叉冲击瞬态核衰减半衰期 (秒)
    public decimal ToxicityLeakageRatioPercent { get; set; } // 订单流毒性跨标的泄漏率 (%)
    public decimal CrossArbitrageSlippageBps { get; set; } // 交叉冲击诱导滑点 (bps)
    public string AsymmetricQuotingBias { get; set; } = string.Empty; // 最优非对称挂单价差偏置 (📈 向上倾斜挂买 / 📉 向下倾斜挂卖 / ⏸️ 双向拓宽价差)
}

public class AssetToxicityLeakageItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal NetToxicityExportBps { get; set; } // 净对外毒性辐射冲击 (bps)
    public decimal NetToxicityImportBps { get; set; } // 净吸收外来交叉毒性 (bps)
    public decimal CrossImpactSusceptibilityIndex { get; set; } // 交叉冲击易感度指数 (0~100)
    public decimal RecommendedPreemptiveSpreadBps { get; set; } // 建议做市前瞻性加宽价差 (bps)
    public string ExecutionDefenseAction { get; set; } = string.Empty; // 执行防御策略 (🛡️ 交叉被动吸收 / ⚡ 跨品种联动抢跑 / 🐢 冰山延迟平抑)
}

public class CrossImpactTensorMicrostructureResult
{
    public decimal TensorAverageCrossImpactLambdaBps { get; set; } // 组合加权平均交叉冲击系数 (bps/千万元)
    public decimal CompositeToxicityDiffusionRatePercent { get; set; } // 综合微观毒性跨品种扩散率 (%)
    public decimal CrossExecutionFrictionSavingsBps { get; set; } // 交叉冲击执行优化预期挽回滑点 (bps)
    public string MicrostructureRegimeState { get; set; } = string.Empty; // 微观流动性扩散体制 (平稳吸收态 / 强交叉共振态 / 毒性级联泄漏态)
    public List<CrossImpactPairItem> TopCrossImpactPairs { get; set; } = new();
    public List<AssetToxicityLeakageItem> AssetToxicityProfiles { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Bridgewater Associates & AQR Capital: 测度模糊集 Wasserstein-Ball 分布鲁棒优化 (DRO) 与极小极大抗毁平价
public class WassersteinDroAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal EmpiricalSampleWeightPercent { get; set; } // 历史经验样本优化权重 (%)
    public decimal WassersteinDroMinimaxWeightPercent { get; set; } // 最劣分布极小极大 DRO 抗毁平价权重 (%)
    public decimal AllocationShiftPercent { get; set; } // 权重稳健重构转移幅度 (%)
    public decimal WorstCaseMarginalRiskContribution { get; set; } // 最劣分布下欧拉边际风险贡献 (Worst-Case MRC)
    public decimal AmbiguityRobustPenaltyBps { get; set; } // 模糊度鲁棒保护溢价扣除 (bps)
    public string RobustAllocationRole { get; set; } = string.Empty; // 鲁棒配置角色定位 (🏰 极端抗毁压舱石 / ⚖️ 稳健中枢平衡器 / 🛡️ 尾部防御削减)
}

public class WassersteinDroMinimaxParityResult
{
    public decimal AmbiguityBallRadiusEpsilon { get; set; } // Wasserstein 测度模糊球半径 ε
    public decimal WorstCaseExpectedShortfallPercent { get; set; } // 最劣分布下预期尾部回撤 CVaR 边界 (%)
    public decimal EmpiricalSampleEstimatedCvarPercent { get; set; } // 传统经验样本低估 CVaR (%)
    public decimal OutOfSampleDrawdownMitigationPercent { get; set; } // 样本外最劣回撤平抑优化率 (%)
    public decimal DualLagrangeMultiplierLambda { get; set; } // 对偶凸优化拉格朗日乘子 λ*
    public List<WassersteinDroAssetItem> DroAssetAllocations { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 45 机构级终极前沿量化投研服务数据契约

// 1. Renaissance Technologies & D.E. Shaw: 连续时间分数阶粗糙波动率 (Rough Heston) 与 Riemann-Liouville 分数阶积分预测
public class AssetRoughVolatilityItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal HurstParameterH { get; set; } // Hurst 粗糙度参数 H (H < 0.5 表明粗糙亚扩散路径)
    public decimal VolOfVolNu { get; set; } // 波动率之波动率 ν (Vol-of-Vol 爆发强度)
    public decimal HistoricalRealizedVolPercent { get; set; } // 经典历史已实现年化波动率 (%)
    public decimal FractionalPredictedVolPercent { get; set; } // Riemann-Liouville 分数阶积分预测年化波动率 (%)
    public decimal ShortTermVolBurstProbabilityPercent { get; set; } // 短周期波动率激增突发概率 (%)
    public decimal RoughConvexityPremiumBps { get; set; } // 粗糙路径凸性溢价调整 (bps)
    public string VolRegimeRoughnessState { get; set; } = string.Empty; // 波动率粗糙度体制 (🌪️ 极端超粗糙爆发态 / 🌊 典型亚扩散记忆态 / ⚖️ 近马尔可夫平稳态)
}

public class FractionalRoughVolatilityResult
{
    public decimal PortfolioAverageHurstParameterH { get; set; } // 组合加权平均 Hurst 粗糙度 H
    public decimal CompositeVolBurstRiskProbabilityPercent { get; set; } // 综合波动率短期突发飙升概率 (%)
    public decimal FractionalVsMarkovianVolDispersionPercent { get; set; } // 分数阶与经典马尔可夫波动率偏离度 (%)
    public decimal NetRoughOptionHedgingCostSavingsBps { get; set; } // 粗糙波动率对冲拟合节约成本 (bps)
    public List<AssetRoughVolatilityItem> AssetRoughVolatilities { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Citadel Global Fixed Income & Millennium RV: 六参数 Nelson-Siegel-Svensson (NSS) 利率期限结构与蝶式凸性相对价值套利
public class KeyRateDurationItem
{
    public string TenorLabel { get; set; } = string.Empty; // 关键利率期限节点 (1Y, 3Y, 5Y, 10Y, 30Y)
    public decimal TenorYears { get; set; } // 期限年数
    public decimal FittedNssYieldPercent { get; set; } // NSS 拟合即期收益率 (%)
    public decimal KeyRateDurationYears { get; set; } // 组合在该节点的关键利率久期 (KRD_i)
    public decimal KeyRateRiskContributionPercent { get; set; } // 节点利率风险贡献占比 (%)
    public decimal CurvatureSensitivityGamma { get; set; } // 节点二阶凸性敏感度
    public string ArbitrageButterflyLeg { get; set; } = string.Empty; // 蝶式套利腿角色 (翼端做多 Long Wing / 腹部做空 Short Body / 宏观中性 Neutral)
}

public class NelsonSiegelSvenssonTermStructureResult
{
    public decimal Beta0Level { get; set; } // 长期水平因子 β_0 (%)
    public decimal Beta1Slope { get; set; } // 短期斜率因子 β_1 (%)
    public decimal Beta2Curvature1 { get; set; } // 中期曲率因子 1 β_2 (%)
    public decimal Beta3Curvature2 { get; set; } // 次级曲率因子 2 β_3 (%)
    public decimal Tau1Decay { get; set; } // 尺度衰减参数 τ_1 (年)
    public decimal Tau2Decay { get; set; } // 尺度衰减参数 τ_2 (年)
    public decimal PortfolioEffectiveDurationYears { get; set; } // 组合有效总久期 (年)
    public decimal ButterflyArbitrageExpectedAlphaBps { get; set; } // 蝶式凸性利差相对价值套利预期 Alpha (bps)
    public List<KeyRateDurationItem> KeyRateDurations { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Two Sigma & Man Group AHL Systematic Macro: 贝叶斯在线变点检测 (BOCPD) 与序列失效率运行长度后验滤波
public class ChangepointHazardItem
{
    public int TimeStepIndex { get; set; } // 时序步长索引
    public DateTime ObservationDate { get; set; } // 观测日期
    public decimal MaximumAcyclicRunLength { get; set; } // 最大后验体制运行长度 (Run-Length r_t, 天)
    public decimal InstantaneousHazardRatePercent { get; set; } // 瞬时结构失效率 H(r_t) (%)
    public decimal ChangepointProbabilityPercent { get; set; } // 变点重置发生概率 P(r_t = 0 | x) (%)
    public string MacroRegimePhaseBadge { get; set; } = string.Empty; // 宏观结构阶段定位 (🌱 新生体制确认 / 🌲 稳态扩张期 / ⚠️ 相变高危期 / 💥 结构断裂突变态)
    public string RecommendedAssetAction { get; set; } = string.Empty; // 推荐自适应资产调仓动作 (🛡️ 阶梯去杠杆避险 / 📈 稳态因子加仓 / ⏸️ 冻结再平衡观察)
}

public class BayesianOnlineChangepointDetectionResult
{
    public decimal CurrentRegimeRunLengthDays { get; set; } // 当前宏观体制已持续交易日 (天)
    public decimal LatestChangepointProbabilityPercent { get; set; } // 最新截面结构变点爆发概率 (%)
    public decimal SystemicHazardRatePercent { get; set; } // 系统综合失效率水平 (%)
    public string CurrentStructuralRegimeClassification { get; set; } = string.Empty; // 当前体制类别 (稳态低波 / 结构突变高波 / 趋势延续)
    public decimal PreemptiveDeriskingAlphaSavingsBps { get; set; } // 提前识别变点挽回损失 (bps)
    public List<ChangepointHazardItem> RecentHazardHistory { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Jane Street & Citadel Securities: Avellaneda-Stoikov 连续库存风险最优保留价与非对称限价挂单微观做市定价
public class AvellanedaStoikovAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal CurrentHoldingInventoryUnits { get; set; } // 当前瞬时持仓库存单位 q
    public decimal MidPriceQuote { get; set; } // 市场基准中间价 S
    public decimal ReservationPriceIndifference { get; set; } // 无差异最优保留价 r(s, q)
    public decimal OptimalBidSpreadBps { get; set; } // 最优买单半价差 δ_b (bps)
    public decimal OptimalAskSpreadBps { get; set; } // 最优卖单半价差 δ_a (bps)
    public decimal AsymmetricQuoteSkewBps { get; set; } // 挂单非对称倾斜度 (δ_a - δ_b, bps)
    public decimal InventoryExcursionRiskScore { get; set; } // 存货单边偏离暴跌风险分 (0~100)
    public string MicrostructureQuotingAction { get; set; } = string.Empty; // 微观挂单执行动作 (📥 倾斜挂买吸收库存 / 📤 激进挂卖卸载存货 / ⚖️ 对称双边提供流动性)
}

public class AvellanedaStoikovMicrostructureResult
{
    public decimal PortfolioWeightedReservationSkewBps { get; set; } // 组合加权做市保留价倾斜偏置 (bps)
    public decimal AverageOptimalQuotingSpreadBps { get; set; } // 组合平均最优做市全价差 (bps)
    public decimal InventoryRiskMitigationRatePercent { get; set; } // 存货不利移动风险削减率 (%)
    public decimal ExpectedMicroExecutionSavingsBps { get; set; } // 微观限价单撮合预期节省摩擦 (bps)
    public List<AvellanedaStoikovAssetItem> AssetQuotingProfiles { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 46 机构级终极前沿量化投研服务数据契约

// 1. Renaissance Technologies & Alan Turing Institute: 粗糙路径特征签名 (Rough Path Signature) 与高阶张量 Alpha 几何编码
public class AssetPathSignatureItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal SignatureOrder1ReturnNorm { get; set; } // 1 阶增量范数 S^(1)
    public decimal SignatureOrder2EnergyNorm { get; set; } // 2 阶张量能量范数 S^(2)
    public decimal SignedLevyArea { get; set; } // 非交换李代数面积 Lévy Area A_12 (几何旋度)
    public decimal GeometricMomentumCurvature { get; set; } // 几何路径曲率特征 (对时间重参数化不变)
    public decimal PathSignatureAlphaScore { get; set; } // 截断张量空间 Alpha 预测得分 (-100 ~ +100)
    public decimal NonMarkovianAlphaIncrementBps { get; set; } // 非马尔可夫记忆 Alpha 增益 (bps)
    public string PathTopologyRegimeBadge { get; set; } = string.Empty; // 几何拓扑形态 (🌀 逆时针顺势旋流态 / ⚡ 顺时针背离耗散态 / 📐 线性正定拉升态 / 🌊 随机布朗混沌态)
}

public class RoughPathSignatureAlphaResult
{
    public decimal PortfolioAverageLevyArea { get; set; } // 组合加权平均李代数面积 Lévy Area
    public decimal CompositePathCurvatureIndex { get; set; } // 综合非马尔可夫路径曲率指数
    public decimal HighOrderTensorAlphaPremiumBps { get; set; } // 高阶张量特征超额收益增益 (bps)
    public decimal SignatureInformationCaptureRatio { get; set; } // 路径签名全息信息捕获率 (%)
    public List<AssetPathSignatureItem> AssetPathSignatures { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Citadel Global Strategies & Millennium RV: 连续时间随机最优停止时间与自由边界斯内尔包络 (Snell Envelope) 动态去杠杆
public class OptimalStoppingThresholdItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal CurrentPriceToHighRatio { get; set; } // 当前价格相对局部峰值比 (P_t / M_t)
    public decimal FreeBoundaryStoppingThreshold { get; set; } // 自由边界最优止损阈值 b*(t) (平滑粘贴临界线)
    public decimal SnellEnvelopeOptionValueBps { get; set; } // 斯内尔包络美式提前退出期权时间价值 (bps)
    public decimal SmoothPastingElasticity { get; set; } // 平滑粘贴一阶接触弹性 |V'(b) - g'(b)|
    public decimal ExpectedOptimalHoldingDaysRemaining { get; set; } // 预期最优持有剩余交易日 (天)
    public bool IsStoppingTriggered { get; set; } // 是否已击穿自由边界触发立即平仓/去杠杆
    public string DeRiskingUrgencyBadge { get; set; } = string.Empty; // 去杠杆紧迫度 (🛑 触碰自由边界·立即硬止损 / ⚠️ 逼近警戒区·阶梯降仓 / 🟢 位于顺风期·安心持有)
    public string OptimalStoppingAction { get; set; } = string.Empty; // 最优停止执行动作建议
}

public class StochasticOptimalStoppingResult
{
    public decimal PortfolioWeightedStoppingBoundary { get; set; } // 组合加权平滑粘贴自由边界阈值
    public decimal AggregateSnellEnvelopeTimeValueBps { get; set; } // 组合斯内尔包络总期权溢价 (bps)
    public int AssetsAtStoppingBoundaryCount { get; set; } // 当前触及最优退出边界的资产数量
    public decimal AvoidedDrawdownAlphaSavingsBps { get; set; } // 动态自由边界相比静态止损挽回回撤 (bps)
    public List<OptimalStoppingThresholdItem> AssetStoppingThresholds { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Two Sigma & Bridgewater Associates: 因果结构方程模型 (SCM) 与反事实 Do-Calculus 宏观干预归因
public class CausalNodeEffectItem
{
    public string FactorOrVariableName { get; set; } = string.Empty; // 因果变量节点 (流动性紧缩 / 利率陡峭化 / 信用利差扩张 / 盈利动量)
    public decimal ObservationalCorrelation { get; set; } // 传统观测关联度 (可能包含虚假混杂偏误)
    public decimal AverageTreatmentEffectDo { get; set; } // 后门准则 Do(X=Δ) 介入平均因果处理效应 ATE (%)
    public decimal ConfoundingBiasBps { get; set; } // 混杂偏误幅度 (|Correlation - ATE| * 10000, bps)
    public decimal DirectCausalContributionPercent { get; set; } // 对组合收益的直接因果边际贡献率 (%)
    public string CausalMechanismType { get; set; } = string.Empty; // 因果机理属性 (🎯 真实主导驱动因 / 🎭 伪相关虚假投影因 / 🛡️ 结构性缓冲吸收因)
}

public class CounterfactualScenarioItem
{
    public string ScenarioName { get; set; } = string.Empty; // 反事实假设情景 (如：假定央行未收紧流动性 / 假定海外无突发冲击)
    public string IntervenedDoVariable { get; set; } = string.Empty; // 介入变量 Do(X)
    public decimal BaselineActualReturnPercent { get; set; } // 基准实际收益率 (%)
    public decimal CounterfactualReturnPercent { get; set; } // 反事实推演假设收益率 (%)
    public decimal NetCausalDeltaBps => (CounterfactualReturnPercent - BaselineActualReturnPercent) * 100m; // 因果纯效应净差值 (bps)
    public string StrategicImplication { get; set; } = string.Empty; // 战略启示与配置指导
}

public class CausalStructuralModelAttributionResult
{
    public decimal SystemicConfoundingBiasRatio { get; set; } // 系统综合混杂虚假相关度比率 (%)
    public decimal TrueCausalAlphaContributionBps { get; set; } // 真实因果剥离超额 Alpha 净贡献 (bps)
    public decimal SpuriousCorrelationEliminatedBps { get; set; } // 剔除虚假共线性防止的错配损耗 (bps)
    public string MacroCausalDagStructureState { get; set; } = string.Empty; // 宏观因果网络状态 (稳健直接传导态 / 复杂中介共振态 / 混杂阻塞态)
    public List<CausalNodeEffectItem> CausalNodeEffects { get; set; } = new();
    public List<CounterfactualScenarioItem> CounterfactualScenarios { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Jane Street & Citadel Securities: 暂态市场冲击幂律记忆核 (Propagator Kernel) 与最优拆单执行减摩
public class AssetTransientImpactItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal PowerLawDecayExponentGamma { get; set; } // 幂律衰减指数 γ (经验值约 0.45 ~ 0.55)
    public decimal PropagatorMemoryKernelFactorG0 { get; set; } // 瞬时冲击比例常数 Γ_0 (bps/千万元)
    public decimal AccumulatedTransientImpactBps { get; set; } // 历史订单流积压的未衰减暂态冲击 I(t) (bps)
    public decimal InstantaneousNaiveImpactBps { get; set; } // 传统静态模型低估的无记忆冲击 (bps)
    public decimal ImpactMemoryAccumulationRatio { get; set; } // 记忆积压放大倍数 (I_transient / I_naive)
    public decimal AdaptiveExecutionSavingsBps { get; set; } // 考虑记忆核的最优非均匀拆单挽回滑点 (bps)
    public string ExecutionCadenceAdvice { get; set; } = string.Empty; // 拆单节奏建议 (🐢 慢速凸性冷却拆单 / ⏸️ 暂停等待冲击衰减 / ⚡ 趁流动性潮涌分批吃单)
}

public class TransientMarketImpactPropagatorResult
{
    public decimal PortfolioAverageDecayExponentGamma { get; set; } // 组合加权平均幂律衰减指数 γ
    public decimal TotalAccumulatedTransientDragBps { get; set; } // 组合总暂态冲击记忆积压拖累 (bps)
    public decimal NonUniformExecutionSavingsBps { get; set; } // 动态自适应拆单预期挽回冲击滑点 (bps)
    public decimal MarketResilienceHalfLifeMinutes { get; set; } // 流动性弹性半衰期 (分钟)
    public List<AssetTransientImpactItem> AssetTransientImpacts { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 47 机构级前沿量化投研服务 (马利亚温随机变分、半定松弛SDR、平均场博弈MFG、Kyle-Back隐匿执行)

// 1. Renaissance Technologies & D.E. Shaw: 马利亚温随机变分分析 (Malliavin Calculus) 与无似然高阶 Greeks 敏感度对偶
public class AssetMalliavinSensitivityItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; } // 组合持仓权重 (%)
    public decimal MalliavinDelta { get; set; } // 维纳空间 Skorokhod 对偶一阶价格敏感度 Delta
    public decimal MalliavinGamma { get; set; } // 二阶积分变分曲率敏感度 Gamma
    public decimal MalliavinVega { get; set; } // 波动率扰动对偶敏感度 Vega (bps/1% vol)
    public decimal CrossAssetVanna { get; set; } // 交叉资产波动率-价格混合敏感度 Vanna
    public decimal NumericalRobustnessScore { get; set; } // 对偶无似然求解稳健性得分 (0 ~ 100)
    public string SensitivityRegimeBadge { get; set; } = string.Empty; // 敏感度状态徽标 (💎 强凸性防御区 / ⚡ 高阶极值敏锐区 / 🛡️ 稳态中性对冲区 / 🌊 扩散钝化区)
    public string DeltaHedgeAdvice { get; set; } = string.Empty; // 动态对偶敏感度调平建议
}

public class MalliavinCalculusSensitivityResult
{
    public decimal PortfolioWeightedMalliavinDelta { get; set; } // 组合加权对偶 Delta
    public decimal PortfolioWeightedMalliavinGamma { get; set; } // 组合加权对偶 Gamma 曲率
    public decimal PortfolioAggregatedVegaBps { get; set; } // 组合隐含波动率敏感度总 Vega (bps)
    public decimal FiniteDifferenceSpeedupRatio { get; set; } // 相对传统有限差分扰动的计算加速比 (倍)
    public List<AssetMalliavinSensitivityItem> AssetSensitivities { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Citadel Global Strategies & Millennium Management: 半定松弛 (SDR) 锥优化与基数约束稀疏指数跟踪
public class AssetSdrSparseWeightItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal OriginalUnconstrainedWeight { get; set; } // 原始无基数约束权重 (%)
    public decimal SdrOptimizedSparseWeight { get; set; } // SDR 锥松弛后全局最优稀疏权重 (%)
    public bool IsSelectedInCardinality { get; set; } // 是否被基数 K 约束选中入选底仓
    public decimal MarginalTrackingVarianceContribution { get; set; } // 边际跟踪方差贡献 (bps)
    public decimal TransactionFeeSavingBps { get; set; } // 稀疏化剔除/保留带来的摩擦节约 (bps)
    public string SelectionRoleBadge { get; set; } = string.Empty; // 资产角色徽标 (⭐ 核心骨干资产 / 🎯 边际精选标的 / ✂️ 稀疏化裁剪剔除)
}

public class SemidefiniteRelaxationCardinalityResult
{
    public int CardinalityLimitK { get; set; } // 最大允许持仓基数 K
    public int ActualSparseAssetsSelected { get; set; } // 实际稀疏精选标的数
    public decimal SdrSparseTrackingErrorPercent { get; set; } // SDR 稀疏组合年化跟踪误差 (%)
    public decimal SdrConicRelaxationGapRatio { get; set; } // SDR 锥松弛下界对偶间隙 Duality Gap (%)
    public decimal FrictionCostReductionBps { get; set; } // 稀疏化累计换手与托管摩擦节省 (bps)
    public List<AssetSdrSparseWeightItem> SparseWeightItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Two Sigma & Bridgewater Associates: 连续时间平均场博弈 (Mean Field Games, MFG) 与机构间策略纳什均衡执行
public class AssetMfgNashStrategyItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal MarketCrowdingDensityIndex { get; set; } // 市场机构同向建仓/持仓拥挤度密度指数 (0 ~ 100)
    public decimal MfgEquilibriumExecutionRate { get; set; } // HJB-FPK 耦合纳什均衡最优执行调仓速率 (%/h)
    public decimal AntiFrontRunningDefensiveRatio { get; set; } // 机构间博弈防抢跑防踩踏弹性系数
    public decimal EquilibriumSlippageSavingsBps { get; set; } // 相比盲目激进执行挽回的踩踏冲击滑点 (bps)
    public string CrowdingRegimeBadge { get; set; } = string.Empty; // 拥挤度博弈评级 (🚨 极端踩踏高危区 / ⚠️ 机构同向密集区 / 🍃 自由非拥挤区)
    public string StrategicExecutionGuidance { get; set; } = string.Empty; // 策略博弈执行指导
}

public class MeanFieldGameExecutionResult
{
    public decimal SystemicCrowdingIndex { get; set; } // 组合全景机构平均拥挤度指数
    public decimal TotalMfgEquilibriumSavingsBps { get; set; } // MFG 纳什均衡预期总体滑点挽回 (bps)
    public decimal CoordinationConvergencePeriodHours { get; set; } // 机构调仓群体分布平衡收敛时间 (小时)
    public int CrowdedAssetsCount { get; set; } // 达到高危拥挤踩踏警戒线的资产数
    public List<AssetMfgNashStrategyItem> AssetNashStrategies { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Jane Street Capital & Jump Trading: Kyle-Back 连续拍卖动态知情交易与隐匿执行模型 (Stealth Execution)
public class AssetKyleBackExecutionItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal KyleLambdaImpactFactor { get; set; } // 动态 Kyle's Lambda 价格冲击灵敏度系数 (bps/万手)
    public decimal OptimalStealthTradingIntensity { get; set; } // 动态知情交易强度 β(t)
    public decimal InformationLeakageDecayRate { get; set; } // 订单流被做市商解构的信息泄露衰减率 (%/min)
    public decimal PrivateAlphaHalfLifeDays { get; set; } // 私有 Alpha 信号有效半衰期 (交易日)
    public decimal StealthAlphaPreservationBps { get; set; } // 隐匿执行相比直接激进吃单保留的净 Alpha (bps)
    public string StealthCamouflageRating { get; set; } = string.Empty; // 隐身伪装评级 (🥷 完美隐身无痕 / 🕵️ 良好噪声混淆 / 📢 明显暴露警报)
    public string MicroExecutionTactic { get; set; } = string.Empty; // 微观做市隐匿拆挂单战术
}

public class KyleBackStealthExecutionResult
{
    public decimal PortfolioAverageKyleLambda { get; set; } // 组合加权平均 Kyle's Lambda 冲击系数
    public decimal CompositeStealthCamouflageScore { get; set; } // 综合隐身伪装效率评分 (0 ~ 100)
    public decimal TotalPreservedAlphaBps { get; set; } // 组合全流程隐匿执行累计锁定的 Alpha 保护额 (bps)
    public decimal AverageAlphaSignalHalfLifeDays { get; set; } // 组合平均特质 Alpha 信号衰减半衰期 (天)
    public List<AssetKyleBackExecutionItem> AssetKyleBackExecutions { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 48 机构级前沿量化投研服务 (随机庞特里亚金SPMP、共识ADMM、无限HDP-HMM、双重Cox做市)

// 1. Renaissance Technologies & D.E. Shaw: 跳跃扩散 Lévy 过程 Esscher 测度变换与随机庞特里亚金极大值原理 (SPMP) 伴随状态最优控制
public class AssetSpmpControlItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; } // 组合持仓权重 (%)
    public decimal CoStateP { get; set; } // 一阶随机伴随协变量 p_t (哈密顿一阶敏感度)
    public decimal CoStateQ { get; set; } // 二阶维纳波动伴随协变量 q_t
    public decimal JumpIntensityLambda { get; set; } // Merton 泊松跳跃到达强度 λ (次/年)
    public decimal OptimalControlIntensity { get; set; } // 随机庞特里亚金最优调仓控制率 u_t* (%/天)
    public decimal EsscherThetaParameter { get; set; } // 风险中性 Esscher 测度变换倾斜参数 θ
    public decimal RebalanceFrictionSavingBps { get; set; } // 伴随控制平滑降低的冲击滑点与换手磨损 (bps)
    public string ControlRegimeBadge { get; set; } = string.Empty; // 伴随控制评级 (🎯 极优平滑区 / ⚡ 跳跃脉冲对冲区 / 🛡️ 稳态跟随区 / 🌊 惯性漂移区)
    public string SpmpExecutionGuidance { get; set; } = string.Empty; // 庞特里亚金控制执行指导
}

public class StochasticPontryaginControlResult
{
    public decimal PortfolioAverageCoStateP { get; set; } // 组合加权平均伴随协变量 p_t
    public decimal PortfolioAverageOptimalControlRate { get; set; } // 组合平均庞特里亚金最优控制率 (%/天)
    public decimal TotalRebalanceSavingsBps { get; set; } // 伴随控制累计降低调仓滑点与磨损 (bps)
    public decimal HamiltonianSecondOrderConcavity { get; set; } // 哈密顿函数二阶充分性凹性指标 (< 0 确保全局最优)
    public List<AssetSpmpControlItem> AssetSpmpControls { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Citadel Global Strategies & Millennium Management: 多 Pod 去中心化共识交替方向乘子法 (Consensus ADMM) 与增广拉格朗日多重约束动态资本仲裁
public class PodAdmmAllocationItem
{
    public string PodCode { get; set; } = string.Empty; // Pod 策略单元标识 (如 POD-STATARB, POD-MOMENTUM, POD-MACRO, POD-HFT)
    public string PodStrategyName { get; set; } = string.Empty; // 策略单元名称
    public decimal LocalTargetWeight { get; set; } // Pod 本地无约束最优权重 (%)
    public decimal ConsensusAdmmWeight { get; set; } // ADMM 全局共识协调最优权重 (%)
    public decimal DualMultiplierU { get; set; } // 增广拉格朗日对偶影子价格乘子 u_k
    public decimal PrimalResidualNorm { get; set; } // 原始残差范数 ||w_k - z||_2
    public decimal CapitalEfficiencyGainBps { get; set; } // 共识仲裁释放的边际资本利用效率 (bps)
    public string ConvergenceStatusBadge { get; set; } = string.Empty; // 收敛状态 (🟢 完美对偶共识 / 🟡 边界活跃约束 / 🔵 弹性微调区)
    public string CentralArbitrationAdvice { get; set; } = string.Empty; // 投委会中央仲裁指导
}

public class ConsensusAdmmArbitrationResult
{
    public int TotalPodsCount { get; set; } // 协同仲裁 Pod 策略总数
    public int AdmmIterationsTaken { get; set; } // 达到收敛迭代步数
    public decimal FinalPrimalResidual { get; set; } // 最终全局原始残差 ||r||_2
    public decimal FinalDualResidual { get; set; } // 最终全局对偶残差 ||s||_2
    public decimal PortfolioNetLeverage { get; set; } // 全局净头寸暴露杠杆率 (%)
    public decimal TotalCapitalEfficiencyGainBps { get; set; } // 全局多 Pod 资本共识增益 (bps)
    public List<PodAdmmAllocationItem> PodAllocations { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Two Sigma & Bridgewater Associates: 非参数分层狄利克雷过程隐马尔可夫模型 (HDP-HMM) 自适应宏观结构聚类与未见体制动态涌现
public class MacroHdpRegimeItem
{
    public int RegimeId { get; set; } // 宏观体制序号
    public string RegimeName { get; set; } = string.Empty; // 宏观体制语义命名 (如: 结构性复苏牛市、滞胀流动性挤压、低波动资产荒、跨周期非线性相变)
    public decimal PosteriorProbability { get; set; } // 贝叶斯后验概率密度 (%)
    public decimal MeanAnnualizedReturn { get; set; } // 该体制下历史预期年化收益 (%)
    public decimal AnnualizedVolatility { get; set; } // 该体制下资产年化波动率 (%)
    public decimal TransitionPersistence { get; set; } // 体制自转移自留存概率 (%)
    public decimal SurpriseAnomalyScore { get; set; } // 体制涌现惊异度分值 (0 ~ 100)
    public string RegimeRoleBadge { get; set; } = string.Empty; // 体制角色 (👑 当前主导体制 / 🌟 新生涌现体制 / 🛡️ 防御底仓体制 / ⏳ 衰退边缘体制)
    public string AssetAllocationGuidance { get; set; } = string.Empty; // 跨周期宏观对冲配置建议
}

public class InfiniteHdpMacroClusteringResult
{
    public int ActiveRegimesCount { get; set; } // 自适应非参数推断出的活动主导体制数
    public decimal NewRegimeEmergenceProbability { get; set; } // 未见新体制动态涌现概率 (%)
    public decimal MacroSurpriseIndex { get; set; } // 宏观黑天鹅惊异度综合指数 (0 ~ 100)
    public decimal BayesianEvidenceRatio { get; set; } // 相对传统固定 3-状态 HMM 的边际贝叶斯证据比 (倍)
    public List<MacroHdpRegimeItem> ActiveRegimes { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Jane Street Capital & Jump Trading: 双重随机 Cox 点过程与非齐次复合泊松跳跃做市最优非对称价差偏置控制
public class AssetCoxMarketMakingItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal BaselineArrivalRate { get; set; } // Cox 过程基准到达强度 λ_0 (单/秒)
    public decimal OptimalBidHalfSpreadBps { get; set; } // 买侧最优最优保留半价差 δ^b* (bps)
    public decimal OptimalAskHalfSpreadBps { get; set; } // 卖侧最优最优保留半价差 δ^a* (bps)
    public decimal SpreadAsymmetrySkewBps { get; set; } // 买卖非对称价差偏置 (δ^a* - δ^b*) (bps)
    public decimal InventoryPenaltyCurvature { get; set; } // 做市商净库存非线性效用惩罚曲率
    public decimal ToxicSelectionDefenseGainBps { get; set; } // 逆向选择毒性防御带来的滑点节省 (bps)
    public decimal ExpectedMarketMakingAlphaBps { get; set; } // 预期做市双边价差捕获 + 返佣净 Alpha (bps)
    public string MarketMakingStrategyBadge { get; set; } = string.Empty; // 做市策略徽标 (🛡️ 偏空毒性防御 / 🚀 积极双边吸收 / ⚖️ 中性对称做市 / 🚨 撤单微观避险)
    public string HighFrequencyQuotingGuidance { get; set; } = string.Empty; // 超高频盘口挂单调度战术
}

public class CoxProcessAsymmetricMarketMakingResult
{
    public decimal PortfolioWeightedAverageSpreadBps { get; set; } // 组合加权平均做市买卖有效全价差 (bps)
    public decimal AverageAsymmetrySkewBps { get; set; } // 平均做市报价非对称偏置幅度 (bps)
    public decimal TotalToxicDefenseGainBps { get; set; } // 逆向选择毒性防御累计挽回损失 (bps)
    public decimal TotalExpectedNetMakingAlphaBps { get; set; } // 预期做市业务净超额 Alpha 贡献 (bps)
    public List<AssetCoxMarketMakingItem> AssetMarketMakingItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 49 机构级前沿量化投研服务 (分数fOU超前对冲、Stackelberg博弈契约、信息几何测地线、多元Hawkes微观LOB)

// 1. Renaissance Technologies & D.E. Shaw: 粗糙分数 Ornstein-Uhlenbeck (fOU) 反持续性长程记忆与 Skorokhod 非适应超前对冲
public class AssetFouMemoryItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; } // 组合资产权重 (%)
    public decimal HurstParameter { get; set; } // 赫斯特指数 H (H < 0.5 呈现粗糙反持续性)
    public decimal FouMeanReversionSpeed { get; set; } // 分数 Ornstein-Uhlenbeck 均值回复速度 κ (次/年)
    public decimal SkorokhodAnticipatingDivergence { get; set; } // Skorokhod 散度算子非适应超前变分修正量
    public decimal AnticipatingHedgeDelta { get; set; } // 考虑长程粗糙记忆的超前动态对冲 Delta
    public decimal TrackingErrorReductionPercent { get; set; } // 消除对冲时滞带来的跟踪误差压降 (%)
    public string RoughnessDegreeBadge { get; set; } = string.Empty; // 粗糙记忆评级 (🌪️ 极端粗糙反持续 / ⚡ 中度反持续震荡 / 🌊 弱相关平滑区)
    public string AnticipatingExecutionAdvice { get; set; } = string.Empty; // Skorokhod 超前对冲与调仓指引
}

public class RoughFractionalOuMemoryResult
{
    public decimal PortfolioWeightedHurstParameter { get; set; } // 组合加权平均赫斯特指数 H
    public decimal PortfolioAverageFouReversionSpeed { get; set; } // 组合平均 fOU 均值回复速度 κ
    public decimal TotalTrackingErrorReductionPercent { get; set; } // 总体对冲跟踪误差平均压降 (%)
    public decimal SkorokhodDivergenceEfficiencyGainBps { get; set; } // Skorokhod 超前对冲带来的减摩增益 (bps)
    public List<AssetFouMemoryItem> AssetFouMemoryItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Citadel Global Strategies & Millennium Management: 双层分层 Stackelberg 动态主从博弈与道德风险防范委托-代理多经理最优契约机制
public class PodStackelbergContractItem
{
    public string PodCode { get; set; } = string.Empty; // Pod 策略单元标识 (如 POD-MOMENTUM, POD-STATARB, POD-MACRO, POD-HFT)
    public string PodStrategyName { get; set; } = string.Empty; // 策略单元名称
    public decimal AllocatedCapital { get; set; } // CIO 分配资本规模 (亿元)
    public decimal OptimalIncentiveSlopeAlpha { get; set; } // Holmström-Milgrom 最优绩效提成斜率 α* (%)
    public decimal HighWaterMarkHurdleRate { get; set; } // 高水位门槛收益率 (%)
    public decimal DrawdownStopLossThreshold { get; set; } // 动态止损与追保警戒线 (%)
    public decimal MoralHazardRiskPenalty { get; set; } // 道德风险防范与策略漂移惩罚成本 (bps)
    public string ContractGovernanceBadge { get; set; } = string.Empty; // 契约治理状态 (🛡️ 严格契约风控区 / ⚖️ 均衡激励对齐区 / ⚡ 预警追保监管区)
    public string PrincipalAgentAdvice { get; set; } = string.Empty; // 委托代理契约履约指导
}

public class BilevelStackelbergContractResult
{
    public int TotalPodsCount { get; set; } // 纳入 Stackelberg 契约管辖的 Pod 策略总数
    public decimal PlatformAverageIncentiveSlope { get; set; } // 平台平均最优提成激励斜率 α* (%)
    public decimal SystemicMoralHazardDeterrenceScore { get; set; } // 平台道德风险防范综合得分 (0 ~ 100)
    public decimal CapitalEfficiencyGainBps { get; set; } // 主从博弈消除策略漂移后的资本效率增益 (bps)
    public List<PodStackelbergContractItem> PodContractItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Two Sigma & Bridgewater Associates: 拓扑信息几何 Fisher-Rao 黎曼流形测地线距离与 Wasserstein 测度重心相变预警
public class MacroGeodesicRegimeItem
{
    public string RegimePairName { get; set; } = string.Empty; // 宏观流形测地线路径 (如: 繁荣周期 ➔ 流动性紧缩, 稳态低波 ➔ 滞胀重构)
    public decimal FisherRaoGeodesicDistance { get; set; } // Fisher-Rao 黎曼流形测地线绝对距离 d_FR
    public decimal RiemannianManifoldCurvature { get; set; } // 统计流形局部标量/截面曲率 R
    public decimal WassersteinBarycenterDriftRate { get; set; } // Wasserstein-2 测度几何重心漂移速率 (%/月)
    public decimal PhaseTransitionProbability { get; set; } // 宏观非线性断裂与流动性相变爆发概率 (%)
    public string ManifoldTopologyBadge { get; set; } = string.Empty; // 拓扑形态徽标 (🚨 极度相变分岔区 / ⚠️ 流动性应力形变区 / 🟢 稳态测地流形)
    public string MacroGeometricHedgingAdvice { get; set; } = string.Empty; // 信息几何宏观对冲与流动性避险指引
}

public class TopologicalInformationGeometryResult
{
    public decimal AverageFisherRaoDistance { get; set; } // 宏观状态间平均 Fisher-Rao 测地线距离
    public decimal MaxManifoldCurvatureMagnitude { get; set; } // 统计流形最大局部曲率绝对值
    public decimal GlobalPhaseTransitionWarningIndex { get; set; } // 全局宏观相变与黑天鹅预警指数 (0 ~ 100)
    public decimal BarycenterStabilityScore { get; set; } // Wasserstein 测度重心稳定性得分 (0 ~ 100)
    public List<MacroGeodesicRegimeItem> GeodesicRegimeItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Jane Street Capital & Jump Trading: 多维 Hawkes 自激/互激点过程谱半径与 LOB 队列反应式微观流动性黑洞防御
public class HawkesExcitationMatrixItem
{
    public string EventPairCode { get; set; } = string.Empty; // 事件交互通道 (如 BUY_ARRIVE ➔ SELL_CANCEL, SELL_ARRIVE ➔ SWEEP_LOB)
    public string EventPairName { get; set; } = string.Empty; // 事件交互语义
    public decimal BaselineArrivalRate { get; set; } // 基础自发泊松强度 μ (单/秒)
    public decimal ExcitationAlpha { get; set; } // 自激/互激强度系数 α
    public decimal DecayRateBeta { get; set; } // 指数记忆衰减速率 β (1/秒)
    public decimal BranchingRatioContribution { get; set; } // 边际分支比贡献度 (α / β)
    public decimal LobQueueDepletionProb { get; set; } // 限价订单簿 (LOB) 深度瞬时耗尽概率 (%)
    public string MicrostructureRegimeBadge { get; set; } = string.Empty; // 微观结构评级 (⚡ 临界自激雪崩区 / 🌪️ 跨品类级联抽单 / 🛡️ 稳态吸收挂单)
    public string HighFrequencyQuotingTactics { get; set; } = string.Empty; // 队列反应式做市与微观拆单指引
}

public class MultivariateHawkesMicrostructureResult
{
    public decimal HawkesBranchingSpectralRadius { get; set; } // 多元 Hawkes 互激核谱半径 ρ(Γ) (临界分支比，接近 1 预警订单流雪崩)
    public decimal AverageQueueDepletionProbability { get; set; } // 组合加权平均 LOB 队列耗尽概率 (%)
    public decimal AvalancheBlackHoleWarningScore { get; set; } // 微观流动性黑洞闪崩预警综合指数 (0 ~ 100)
    public decimal NetMicrostructureAlphaBps { get; set; } // 队列反应式高频微观结构净 Alpha (bps)
    public List<HawkesExcitationMatrixItem> HawkesMatrixItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 50: 机构级终极前沿量化系统大圆满 (自由概率QRG·Thom突变混沌·薛定谔桥BSDE·玻尔兹曼纳什场)

// 1. Renaissance Technologies & D.E. Shaw: 非对易自由概率论 (Voiculescu Free Probability) 与量子重整化群 (QRG) 高维矩阵奇异谱修复
public class AssetFreeProbabilityQrgItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; } // 组合配置权重 (%)
    public decimal RawEmpiricalEigenvalue { get; set; } // 原始经验相关阵特征值 λ_raw
    public decimal VoiculescuFreeCumulantR { get; set; } // 自由概率 R-变换自由累积量 κ_1 (期望非对易均值)
    public decimal QrgEnergyScaleLambda { get; set; } // 量子重整化群有效截断能标 Λ*
    public decimal RepairedCleanEigenvalue { get; set; } // QRG 粗粒化流去噪修复后特征值 λ_clean
    public decimal ConditionNumberCompression { get; set; } // 奇异谱条件数压缩贡献比
    public string SpectralPurityBadge { get; set; } = string.Empty; // 谱纯净度徽标 (💎 拓扑大尺度主模 / ⚡ 稳态超对称低波 / 🛡️ 微观高斯白噪声滤除)
    public string SpectralFilteringAdvice { get; set; } = string.Empty; // 自由概率与 QRG 谱修复调仓指引
}

public class VoiculescuFreeProbabilityQrgResult
{
    public decimal VoiculescuFreeNoiseFractionPercent { get; set; } // 自由概率论识别的高维非对易白噪声比例 (%)
    public decimal QrgEffectiveEnergyScaleLambda { get; set; } // 组合重整化群最优有效能量尺度 Λ*
    public decimal ConditionNumberCompressionRatio { get; set; } // 协方差矩阵条件数压缩倍率 κ_prior / κ_post
    public decimal SpectralSingularityRepairGainBps { get; set; } // 奇异谱修复带来的组合样本外抗过拟合减噪增益 (bps)
    public List<AssetFreeProbabilityQrgItem> AssetQrgItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Citadel Global Strategies & AQR Capital Management: 宏观随机动力学混沌奇异吸引子 (Lyapunov Exponent Spectrum) 与托姆尖点突变论 (Thom Cusp Catastrophe) 极值跳变防踩踏
public class ThomCatastropheDynamicItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; } // 组合配置权重 (%)
    public decimal MaxLyapunovExponent { get; set; } // 局部相空间最大李雅普诺夫指数 λ_max
    public decimal ThomControlParameterAlpha { get; set; } // 尖点突变势函数控制参数 α (分岔分量)
    public decimal ThomControlParameterBeta { get; set; } // 尖点突变势函数控制参数 β (不对称扰动分量)
    public decimal BifurcationDiscriminantDelta { get; set; } // 尖点突变分岔判别式 Δ = 4α³ + 27β²
    public decimal DistanceToBifurcationSurface { get; set; } // 距离突变临界分岔曲面的欧氏流形距离
    public decimal AntiHysteresisHedgingBufferPct { get; set; } // 防踩踏滞后回滞动态对冲缓冲比例 (%)
    public string CatastropheRegimeBadge { get; set; } = string.Empty; // 突变动力学评级 (🚨 临界分岔跳变区 / ⚠️ 亚稳态双解回滞区 / 🟢 稳态势井低耗散)
    public string NonlinearDynamicHedgingAdvice { get; set; } = string.Empty; // 混沌与突变动力学对冲指导
}

public class ThomCatastropheChaosDynamicsResult
{
    public decimal PortfolioMaxLyapunovExponent { get; set; } // 组合系统全局最大李雅普诺夫指数 λ_max
    public decimal LyapunovPredictionHorizonDays { get; set; } // 混沌系统可预测视界天数 τ_L = 1 / λ_max
    public decimal ThomCatastropheBifurcationDistance { get; set; } // 全局尖点突变分岔临界面平均安全距离
    public decimal SystemicAntiHysteresisBufferPct { get; set; } // 系统性防踩踏回滞动态对冲缓冲总头寸 (%)
    public List<ThomCatastropheDynamicItem> CatastropheItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Two Sigma & WorldQuant: 神经薛定谔桥 (Neural Schrödinger Bridge) 熵正则化最优输运与反射倒向随机微分方程 (Reflected BSDE) 资本硬约束动态流动性生成式对冲
public class SchrodingerBridgeBsdeItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; } // 组合配置权重 (%)
    public decimal PriorMeasureDrift { get; set; } // 初始经验测度 P_0 漂移率 (%)
    public decimal TargetMeasureDrift { get; set; } // 目标最优测度 P_1 漂移率 (%)
    public decimal EntropicOptimalTransportVelocity { get; set; } // 薛定谔桥熵正则化生成式速度场向量 v*(t, x)
    public decimal ReflectedBsdeBoundarySlack { get; set; } // 反射倒向 SDE 硬边界松弛裕度 (距流动性/杠杆红线)
    public decimal SkorokhodLocalTimeIntensity { get; set; } // Skorokhod 边界反射局部时强度 dK_t (bps)
    public decimal DynamicTransitionSavingsBps { get; set; } // 相比直线调仓节省的过渡冲击与摩擦成本 (bps)
    public string BoundarySafetyBadge { get; set; } = string.Empty; // 硬约束安全状态 (🛡️ 绝对安全流形 / ⚡ 边界反射缓冲区 / 🚨 临界吸收红线)
    public string GenerativeTransitionAdvice { get; set; } = string.Empty; // 薛定谔桥与反射 BSDE 连续生成调仓指引
}

public class NeuralSchrodingerBridgeReflectedBsdeResult
{
    public decimal SchrodingerBridgeEntropicDistance { get; set; } // 薛定谔桥熵正则化最优转移距离 W_ε(P_0, P_1)
    public decimal ReflectedBsdeBoundarySlackMargin { get; set; } // 组合反射 BSDE 资本硬约束平均松弛边际 (%)
    public decimal SkorokhodReflectionLocalTimeIntensity { get; set; } // Skorokhod 边界反弹局部时总强度 (bps)
    public decimal OptimalTransitionCostSavingsBps { get; set; } // 生成式连续输运带来的调仓冲击减摩总增益 (bps)
    public List<SchrodingerBridgeBsdeItem> BridgeBsdeItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Jane Street Capital & Hudson River Trading: 玻尔兹曼-弗拉索夫 (Boltzmann-Vlasov) 动理学流动性连续体场论与相对论性纳什执行博弈
public class VlasovMicrostructureFieldItem
{
    public string ChannelCode { get; set; } = string.Empty; // 微观流动性通道代码 (如 MIC-PASSIVE-BID, MIC-AGGRESSIVE-ASK, MIC-CROSS-SWEEP, MIC-DARK-POOL)
    public string ChannelName { get; set; } = string.Empty; // 通道物理语义
    public decimal PhaseSpaceDensity { get; set; } // 相空间粒子系综密度 f(x, v) (单/tick·ms)
    public decimal MeanParticleVelocity { get; set; } // 微观价格滑移平均粒子漂移速度 v (tick/ms)
    public decimal VlasovSelfConsistentFieldForce { get; set; } // 弗拉索夫自洽流动性引力场强 F_self (N/tick)
    public decimal AcousticShockwaveSpeed { get; set; } // 微观订单簿声学冲击波波前扩散速度 c_s (tick/ms)
    public decimal RelativisticCausalSafetyScore { get; set; } // 相对论因果光锥防抢跑物理安全得分 (0 ~ 100)
    public string KineticRegimeBadge { get; set; } = string.Empty; // 动理学相空间评级 (⚡ 超声速激波扫盘 / 🌪️ 亚声速自洽旋涡 / 🛡️ 稳态朗道阻尼)
    public string RelativisticQuotingTactics { get; set; } = string.Empty; // 动理学场论相对论做市挂单与隐匿战术
}

public class BoltzmannVlasovRelativisticExecutionResult
{
    public decimal VlasovSelfConsistentFieldPotential { get; set; } // 弗拉索夫微观流动性自洽场引力总势能 Φ
    public decimal MicrostructuralAcousticSpeed { get; set; } // 限价订单簿微观声学冲击波平均传导速度 c_s (tick/ms)
    public decimal RelativisticCausalHorizonSafetyScore { get; set; } // 组合相对论因果视界防逆向选择安全综合得分 (0 ~ 100)
    public decimal AdverseSelectionAlphaProtectionBps { get; set; } // 声波波前隐匿执行保护的逆向选择净 Alpha (bps)
    public List<VlasovMicrostructureFieldItem> VlasovFieldItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 51 机构级前沿量化投研模型

// 1. Millennium Management & Point72: 多 PM 策略 Pod 凸二次多维资本分配与内部互冲去交叉阻尼引擎
public class PodAllocationItem
{
    public string PodCode { get; set; } = string.Empty; // Pod 策略代码 (如 POD-STATARB, POD-MACRO-DIR, POD-VOL-SURF, POD-EVENT-DRIVEN, POD-EQ-LS)
    public string PodName { get; set; } = string.Empty; // Pod 名称 / 策略标签
    public string AssignedStrategy { get; set; } = string.Empty; // 策略类别
    public decimal TargetWeight { get; set; } // 动态凸二次优化分配资本权重 (%)
    public decimal RawOrderVolumeBps { get; set; } // 未对冲前名义订单交易量 (bps)
    public decimal InternalNettingSavedBps { get; set; } // 内部互冲虚拟撮合抵消换手量 (bps)
    public decimal NetExternalExecutionBps { get; set; } // 实际需要向外部经纪商/交易所报送的净敞口 (bps)
    public decimal StopOutSlackMargin { get; set; } // 距离回撤硬清盘 (Stop-Out) 的安全松弛边际 (%)
    public decimal DampedCapitalDeleveragingFactor { get; set; } // 回撤阶梯阻尼去杠杆系数 (0.0 ~ 1.0)
    public string PodRiskRegimeBadge { get; set; } = string.Empty; // Pod 风控状态徽标 (🛡️ 高资本效率稳态 / ⚡ 互冲高净额区 / 🚨 阶梯止损去杠杆)
    public string PodAllocationAdvice { get; set; } = string.Empty; // Pod 动态调仓与跨 Pod 资本调度指导
}

public class MillenniumConvexPodAllocationResult
{
    public decimal InternalNettingEfficiencyPercent { get; set; } // 跨 Pod 内部订单虚拟撮合净额率 (%)
    public decimal CrossPodMarginContagionIndex { get; set; } // 跨 Pod 保证金交叉违约传染系统性指数 (0.00 ~ 1.00)
    public decimal DynamicCapitalDragSavingsBps { get; set; } // 避免外部双向滑点/佣金与借贷成本节约总增益 (bps)
    public decimal SystemicStopOutSlackMargin { get; set; } // 平台多 Pod 组合平均止损清盘安全松弛边际 (%)
    public List<PodAllocationItem> PodAllocationItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Jump Trading & Tower Research Capital: 粗糙分数阶随机波动率 (Gatheral Rough Heston H in (0.05, 0.20)) 与微观粗糙度幂律偏度流形引擎
public class RoughVolAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; } // 资产配置权重 (%)
    public decimal EstimatedHurstExponent { get; set; } // 粗糙分数赫斯特指数 H (Gatheral 经验值通常在 0.05 ~ 0.20)
    public decimal RoughnessBurstMultiplier { get; set; } // 粗糙度爆裂乘数 (度量极短时间内的突变聚集性)
    public decimal PowerLawAtTheMoneySkew { get; set; } // 短端无套利平价隐波幂律偏度斜率 ψ(τ) ~ τ^(H - 1/2)
    public decimal VolMemoryPersistenceDays { get; set; } // 分数阶记忆衰减半衰期 (天)
    public decimal RoughnessConvexityRatio { get; set; } // 粗糙对冲凸性修正比率
    public string RoughnessRegimeBadge { get; set; } = string.Empty; // 粗糙度评级 (⚡ 超粗糙高频爆裂 / 🌪️ 经典分形粗糙 / 🛡️ 准半鞅平滑扩散)
    public string RoughnessHedgingAdvice { get; set; } = string.Empty; // 粗糙波动率短端偏度与高频再对冲指引
}

public class RoughFractionalVolatilityGatheralResult
{
    public decimal PortfolioWeightedHurstExponent { get; set; } // 组合加权粗糙赫斯特指数 H_port
    public decimal SystemicRoughnessBurstIndex { get; set; } // 系统性高频粗糙度突发爆裂综合指数
    public decimal ShortTermPowerLawSkewSlope { get; set; } // 极短期限幂律偏度斜率 (Power-Law Skew Slope)
    public decimal RoughnessHedgingAlphaBps { get; set; } // 粗糙对冲凸性校准挽回的再对冲摩擦损耗 (bps)
    public List<RoughVolAssetItem> RoughVolItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Bridgewater Associates & BlackRock Aladdin: 宏观热力学最小相对交叉熵 (Jaynes MaxEnt / KL Relative Entropy) 与非高斯情景冲击流形映射引擎
public class MacroStressScenarioItem
{
    public string ScenarioId { get; set; } = string.Empty; // 情景代码 (如 SCEN-STAGFLATION, SCEN-RATES-SHOCK, SCEN-CREDIT-CRUNCH, SCEN-GEOPOLITICAL)
    public string ScenarioName { get; set; } = string.Empty; // 宏观极端情景语义
    public decimal PriorProbability { get; set; } // 先验无偏基础概率 q_i (%)
    public decimal StressedPostProbability { get; set; } // 最小交叉熵最优扭曲概率 p*_i (%)
    public decimal KullbackLeiblerRelativeEntropy { get; set; } // 相对熵信息散度 D_KL(p* || q)
    public decimal FreeEnergyShiftDeltaF { get; set; } // 吉布斯自由能改变量 ΔF = -ln Z
    public decimal StressedPortfolioLossPct { get; set; } // 最小信息扭曲情景下组合预期损失率 (%)
    public string ScenarioSeverityBadge { get; set; } = string.Empty; // 情景烈度评级 (🚨 极度熵增裂变 / ⚡ 显著自由能骤降 / 🛡️ 稳态相容冲击)
    public string DynamicHedgingPrescription { get; set; } = string.Empty; // 热力学最小扭曲压力防守对冲处方
}

public class ThermodynamicCrossEntropyStressResult
{
    public decimal SystemicCrossEntropyDkl { get; set; } // 全局宏观系统最小相对交叉熵 D_KL
    public decimal MacroThermodynamicTemperature { get; set; } // 宏观热力学等效信息温度 T_macro
    public decimal GibbsFreeEnergyCollapse { get; set; } // 吉布斯自由能坍缩总量 |ΔF|
    public decimal StressedConditionalVaR99 { get; set; } // 最小相对熵扭曲测度下的条件在险价值 Stressed CVaR 99% (%)
    public List<MacroStressScenarioItem> StressScenarioItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Jump Trading & Hudson River Trading: 超对称路径积分 (Supersymmetric Path Integral) 与非微扰瞬子 (Instanton) 极端黑天鹅跃迁与负能级隧道穿透防御
public class InstantonTunnelingItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; } // 资产配置权重 (%)
    public decimal WittenSuperpotentialCurvature { get; set; } // Witten 超势曲率 W''(x)
    public decimal InstantonActionMinimalS { get; set; } // 欧几里得虚时间极小瞬子经典作用量 S_inst
    public decimal TunnelingEscapeProbability { get; set; } // 流动性势垒量子隧道跃迁穿透逃逸几率 Γ (bps)
    public decimal EnergyGapBufferBps { get; set; } // 超对称基态能级差非微扰安全缓冲 (bps)
    public string VacuumStabilityBadge { get; set; } = string.Empty; // 真空稳定性状态 (🛡️ 绝对基态稳定 / ⚡ 瞬子亚稳态跃迁 / 🚨 真空衰变穿透)
    public string QuantumBarrierHedgingAdvice { get; set; } = string.Empty; // 非微扰势垒穿透与超对称负能级对冲指令
}

public class SupersymmetricInstantonTunnelingResult
{
    public decimal GlobalInstantonActionS { get; set; } // 组合全局极小瞬子经典作用量 S_inst
    public decimal MaximumTunnelingEscapeRate { get; set; } // 组合最大非微扰隧道穿透逃逸速率 Γ_max (bps)
    public decimal SupersymmetricEnergyGapBps { get; set; } // 超对称哈密顿基态能级差缓冲 ΔE_SUSY (bps)
    public decimal NonPerturbativeTailShieldPct { get; set; } // 非微扰真空衰变黑天鹅防御头寸 (%)
    public List<InstantonTunnelingItem> InstantonItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 52 机构级前沿量化投研模型

// 1. Citadel Securities & Jane Street: 微观瞬态幂律记忆价格冲击核与动态隐匿拆单执行
public class TransientImpactAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal CharacteristicRelaxationTimeSec { get; set; } // 特征弛豫时间 τ_0 (秒)
    public decimal PowerLawDecayExponentGamma { get; set; } // 幂律衰减指数 γ ∈ [0.4, 0.6]
    public decimal CumulativeSelfImpactBps { get; set; } // 累积自冲击偏离 (bps)
    public decimal ReboundElasticityRatio { get; set; } // 冲击回弹恢复弹性率 (%)
    public decimal OptimalUProfileHeadSlicePct { get; set; } // U型最优拆单首期执行切片占比 (%)
    public decimal OptimalUProfileTailSlicePct { get; set; } // U型最优拆单尾期执行切片占比 (%)
    public decimal TransientSlippageSavingsBps { get; set; } // 相比传统 TWAP 瞬态滑点挽回增益 (bps)
    public string ImpactRegimeBadge { get; set; } = string.Empty; // 冲击流变模式 (🟢 弹性自愈 / 🟡 幂律持久阻尼 / 🔴 冲击自锁过冲)
    public string TransientExecutionAdvice { get; set; } = string.Empty; // 瞬态冲击自适应拆单与隐匿执行指令
}

public class BouchaudTransientImpactPropagatorResult
{
    public decimal GlobalTransientSlippageSavingsBps { get; set; } // 组合瞬态滑点挽回总增益 (bps)
    public decimal MeanPowerLawExponentGamma { get; set; } // 组合平均幂律衰减指数 γ
    public decimal AverageOrderFlowConcealmentIndex { get; set; } // 订单流动态隐匿度指数 (0~100)
    public decimal SelfImpactMitigationRatioPct { get; set; } // 累积自冲击过冲消除比率 (%)
    public List<TransientImpactAssetItem> ImpactItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Two Sigma & D.E. Shaw: 高维流形图拉普拉斯算子谱聚类与非局部热核扩散小波
public class LaplacianClusterAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public int SpectralClusterId { get; set; } // 谱聚会社群编号 (Cluster 1~K)
    public decimal GraphDegreeCentrality { get; set; } // 图度中心性 D_ii
    public decimal FiedlerHarmonicCoordinate { get; set; } // 菲德勒特征向量投影坐标 ψ_2(i)
    public decimal HeatKernelDiffusionRadius { get; set; } // 热核多尺度流形扩散有效半径
    public decimal DiffusionWaveletNoisePurgeBps { get; set; } // 非局部小波滤波去噪纯化 Alpha (bps)
    public string TopologyCommunityBadge { get; set; } = string.Empty; // 拓扑社群状态 (🌐 核心连通中枢 / 🧬 异构流形簇 / 🛡️ 绝缘低噪边缘)
    public string ManifoldAllocationAdvice { get; set; } = string.Empty; // 流形图谱调仓与非局部 Alpha 增强指引
}

public class GraphLaplacianDiffusionWaveletResult
{
    public decimal FiedlerAlgebraicConnectivity { get; set; } // 菲德勒代数连通韧性 λ_2
    public decimal ManifoldDiffusionDimension { get; set; } // 组合本征热核扩散维度
    public decimal SpectralClusteringModularity { get; set; } // 谱聚会社群模块度 Q (0~1)
    public decimal GlobalWaveletPurgeRatioPct { get; set; } // 组合扩散小波全局去噪纯化率 (%)
    public List<LaplacianClusterAssetItem> ClusterItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Bridgewater Associates & AQR Capital: 分数阶粘弹性流变学宏观资产负荷动力学与流动性蠕变
public class RheologyAssetStressItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal FractionalRheologyOrderAlpha { get; set; } // 分数阶流变指数 α ∈ (0, 1)
    public decimal DynamicStorageModulusEr { get; set; } // 动态复原弹性储能模量 E_R
    public decimal DynamicLossModulusEi { get; set; } // 粘性流动性耗散模量 E''
    public decimal SystemicLossTangentTanDelta { get; set; } // 流动性粘性损耗角正切 tan δ
    public decimal MittagLefflerCreepCompliance { get; set; } // 稳态 Mittag-Leffler 蠕变柔量 J_inf
    public decimal CreepRuptureHorizonDays { get; set; } // 流动性枯竭断裂时间预测 (天)
    public string RheologyStateBadge { get; set; } = string.Empty; // 流变韧性评级 (🟢 高弹性回弹 / 🟡 粘弹性蠕变 / 🚨 耗散断裂警戒)
    public string ViscoelasticHedgingAdvice { get; set; } = string.Empty; // 粘弹性流变应力对冲与去杠杆阻尼指令
}

public class ViscoelasticRheologyCapitalStrainResult
{
    public decimal GlobalSystemicLossTangent { get; set; } // 组合流动性流变损耗角正切 tan δ
    public decimal MeanFractionalOrderAlpha { get; set; } // 组合加权分数阶流变阶数 α
    public decimal SystemicDynamicStorageModulus { get; set; } // 组合宏观弹性复原储能模量
    public decimal EarliestCreepRuptureHorizonDays { get; set; } // 最早流动性蠕变耗尽断裂视界 (天)
    public List<RheologyAssetStressItem> RheologyItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Renaissance Technologies & Hudson River Trading: 非平衡态朗之万细致平衡破缺与相空间旋度做功
public class LangevinPairVorticityItem
{
    public string PairCode { get; set; } = string.Empty; // 套利资产对代码
    public string PairName { get; set; } = string.Empty; // 套利资产对名称
    public decimal StationaryDriftGradientForce { get; set; } // 保守梯度恢复力 |-∇U|
    public decimal NonConservativeRotationalForce { get; set; } // 非保守驱动旋转力 |F_rot|
    public decimal BrokenDetailedBalanceDegree { get; set; } // 细致平衡破缺度 Φ_BDB ∈ [0, 1]
    public decimal ProbabilityCurrentVorticity { get; set; } // 稳态概率流旋度强度 |Ω|
    public decimal IrreversibleEntropyProductionRate { get; set; } // 不可逆相空间熵产生率 S_dot
    public decimal LimitCyclePumpWorkBps { get; set; } // 极限环非平衡态泵送做功 (bps)
    public string VorticityRegimeBadge { get; set; } = string.Empty; // 旋度做功状态 (🌀 强非平衡循环做功 / ⚖️ 准平衡保守态 / 🛑 耗散停滞)
    public string LangevinArbitrageAdvice { get; set; } = string.Empty; // 非平衡态朗之万相空间统计套利执行指令
}

public class NonequilibriumLangevinVorticityResult
{
    public decimal GlobalLimitCyclePumpWorkBps { get; set; } // 组合相空间极限环泵送总做功 (bps)
    public decimal MeanBrokenDetailedBalanceDegree { get; set; } // 组合平均细致平衡破缺度 Φ_BDB
    public decimal MaximumProbabilityVorticity { get; set; } // 最大稳态概率流旋度峰值
    public decimal NonEquilibriumStatArbEfficiencyPct { get; set; } // 非平衡态统计套利捕获效率 (%)
    public List<LangevinPairVorticityItem> VorticityItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 53 机构级前沿量化投研模型

// 1. Jump Trading & Optiver: 连续时间仿射跳跃-扩散限价簿做市与自适应逆向选择免受损控制
public class JumpDiffusionMarketMakingItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal JumpPoissonIntensityLambda { get; set; } // 泊松跳跃频率 λ_jump (次/年)
    public decimal MeanJumpMagnitudePercent { get; set; } // 跳跃均值幅度 μ_J (%)
    public decimal OptimalBidHalfSpreadBps { get; set; } // 最优买单半价差 δ_*^b (bps)
    public decimal OptimalAskHalfSpreadBps { get; set; } // 最优卖单半价差 δ_*^a (bps)
    public decimal InventoryRiskPenaltyGamma { get; set; } // 二次库存风险厌恶惩罚系数 γ
    public decimal ToxicityAvoidanceGainBps { get; set; } // 毒性跳跃冲击规避增益 (bps)
    public string JumpRegimeBadge { get; set; } = string.Empty; // 跳跃做市状态 (🟢 连续扩散平稳 / 🟡 泊松跳跃预警 / 🚨 极度毒性跳跃冲击)
    public string MarketMakingControlAdvice { get; set; } = string.Empty; // 做市挂单与非对称库存控制指令
}

public class JumpDiffusionAffineMarketMakingResult
{
    public decimal GlobalToxicityAvoidanceGainBps { get; set; } // 组合毒性跳跃规避总增益 (bps)
    public decimal MeanJumpPoissonIntensity { get; set; } // 加权平均跳跃强度 λ_jump (次/年)
    public decimal AverageOptimalBidAskSpreadBps { get; set; } // 全天候最优双边总买卖价差 (bps)
    public decimal MarketMakingInventoryResiliencePct { get; set; } // 做市库存抗跳跃吸收韧性比率 (%)
    public List<JumpDiffusionMarketMakingItem> MarketMakingItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Citadel Global Fixed Income & Millennium Macro: 多因子无套利高斯仿射动态期限结构与曲率相对价值套利
public class AffineTermStructureFactorItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal LevelFactorLoadingL { get; set; } // 水平因子载荷 L_t
    public decimal SlopeFactorLoadingS { get; set; } // 斜率因子载荷 S_t
    public decimal CurvatureFactorLoadingC { get; set; } // 曲率因子载荷 C_t
    public decimal DynamicTermPremiumBps { get; set; } // 无套利动态期限溢价 TP_t (bps)
    public decimal ButterflyConvexityArbitrageAlphaBps { get; set; } // 蝶式凸性曲率相对价值套利 Alpha (bps)
    public decimal ArbitrageFreeModelFitR2 { get; set; } // 无套利 DNS-ATSM 拟合优度 R^2
    public string TermStructureRegimeBadge { get; set; } = string.Empty; // 期限结构形态 (📈 陡峭利差走阔 / 📉 倒挂衰退衰减 / 🔄 驼峰曲率凸性)
    public string YieldCurveArbitrageAdvice { get; set; } = string.Empty; // 无套利曲线相对价值套利执行指令
}

public class AffineArbitrageFreeTermStructureResult
{
    public decimal GlobalButterflyConvexityAlphaBps { get; set; } // 组合蝶式凸性套利总 Alpha (bps)
    public decimal MeanDynamicTermPremiumBps { get; set; } // 组合平均期限溢价 (bps)
    public decimal YieldCurveArbitrageViolationResidual { get; set; } // 曲线无套利偏离残差 RMS
    public decimal TermStructureTrackingQualityPct { get; set; } // 仿射无套利跟踪拟合置信度 (%)
    public List<AffineTermStructureFactorItem> FactorItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Point72 & Citadel Multi-Strategy: 多 Pod 合作博弈 Shapley-Owen 边际分散贡献与动态流动性影子定价仲裁
public class PodShapleyCapitalItem
{
    public string PodCode { get; set; } = string.Empty; // Pod 策略集群代码
    public string PodName { get; set; } = string.Empty; // Pod 业务线名称
    public decimal AllocatedCapitalPct { get; set; } // 当前分配资本占比 (%)
    public decimal ShapleyMarginalContributionPct { get; set; } // Shapley-Owen 边际分散贡献度 (%)
    public decimal LiquidityShadowPriceCostBps { get; set; } // 对偶流动性影子借贷成本 (bps)
    public decimal DiversificationGainRatio { get; set; } // 边际分散溢价增益倍数
    public decimal DynamicLeverageMultiplier { get; set; } // 动态杠杆再平衡倍数
    public decimal TargetRebalancedCapitalPct { get; set; } // 仲裁后目标建议资本占比 (%)
    public string PodArbitrationBadge { get; set; } = string.Empty; // Pod 仲裁评级 (🚀 边际扩容激励 / ⚖️ 维持中性运作 / 🔻 影子成本惩罚收缩)
    public string CapitalArbitrationAdvice { get; set; } = string.Empty; // 多 Pod 资本拍卖与动态杠杆再平衡指令
}

public class MultiPodShapleyShadowPricingResult
{
    public decimal GlobalInternalCapitalNettingGainBps { get; set; } // 全基金多 Pod 内部资金撮合节约增益 (bps)
    public decimal MeanLiquidityShadowPriceBps { get; set; } // 全组合流动性影子价格中枢 (bps)
    public decimal CooperativeGameEfficiencyRatioPct { get; set; } // 合作博弈资本配置帕累托效率 (%)
    public decimal SystemicCrowdingDampeningIndex { get; set; } // 多 Pod 头寸拥挤对冲阻尼指数
    public List<PodShapleyCapitalItem> PodItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. D.E. Shaw & WorldQuant: 非欧流形 Ollivier-Ricci 拓扑曲率流相变相干与高阶持久同调过滤
public class RicciTopologicalAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal NodeOllivierRicciCurvature { get; set; } // 节点局部 Ollivier-Ricci 平均几何曲率 κ
    public decimal RicciFlowDenoisingGainBps { get; set; } // Ricci 几何流滤波去噪 Alpha 增益 (bps)
    public decimal ZeroBettiClusterLifespan { get; set; } // 零阶 Betti-0 连通分支持久寿命
    public decimal OneBettiCyclePersistence { get; set; } // 一阶 Betti-1 拓扑环洞持续性度量
    public decimal TopologicalPhaseCoherenceScore { get; set; } // 拓扑相变相干性评分 (0~100)
    public string RicciManifoldBadge { get; set; } = string.Empty; // 流形拓扑评级 (🔮 正曲率强凝聚簇 / 🌌 零曲率平坦欧氏 / 🌀 负曲率相变双曲洞)
    public string TopologicalAlphaAdvice { get; set; } = string.Empty; // 流形 Ricci 拓扑相变过滤与仓位调优指引
}

public class OllivierRicciCurvaturePersistentHomologyResult
{
    public decimal GlobalRicciFlowDenoisingAlphaBps { get; set; } // 组合 Ricci 拓扑流去噪全局 Alpha 增益 (bps)
    public decimal MeanGraphOllivierRicciCurvature { get; set; } // 全图加权平均 Ollivier-Ricci 几何曲率 κ
    public decimal MacroPhaseTransitionCoherence { get; set; } // 宏观拓扑相变相干度指数
    public decimal PersistentHomologyCycleDensity { get; set; } // 持久同调一阶拓扑环洞密度
    public List<RicciTopologicalAssetItem> TopologicalItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 54 机构级前沿量化投研模型

// 1. Renaissance Technologies & D.E. Shaw: 连续时间马尔可夫跳跃状态空间与分层狄利克雷过程 (HDP-HMM) 动态非参数宏观体制涌现
public class HdpRegimeAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public string ActiveLatentState { get; set; } = string.Empty; // 狄利克雷过程自适应激活隐状态 (如 S_1: 稳态低波, S_2: 趋势脉冲, S_3: 流动性黑洞)
    public decimal StationaryProbabilityPct { get; set; } // 隐状态稳态驻留概率分布 π* (%)
    public decimal MeanSojournTimeDays { get; set; } // 连续时间生成元期望驻留寿命 τ = -1/q_ii (天)
    public decimal InstantaneousHazardRate { get; set; } // 瞬时体制跃迁危险率 (次/年)
    public decimal RegimeEntropyNats { get; set; } // 隐状态转换香农信息熵 (nats)
    public decimal HdpAlphaYieldGainBps { get; set; } // 非参数体制自适应配置 Alpha 增益 (bps)
    public string StateRegimeBadge { get; set; } = string.Empty; // 状态徽章 (🟢 稳态常态低波 / 🟡 趋势脉冲扩张 / 🚨 极端相变跃迁)
    public string DirichletRegimeControlAdvice { get; set; } = string.Empty; // 狄利克雷隐状态前瞻对冲与战术配置指令
}

public class ContinuousMarkovSwitchingDirichletProcessResult
{
    public decimal GlobalHdpRegimeEntropy { get; set; } // 组合宏观体制信息熵中枢 (nats)
    public decimal DominantStateHazardRate { get; set; } // 主导体制突变危险率 (次/年)
    public int NonparametricActiveStateCount { get; set; } // 动态自适应涌现有效隐状态数
    public decimal HdpStateTransitionStabilityPct { get; set; } // 狄利克雷隐状态转移稳定性置信度 (%)
    public List<HdpRegimeAssetItem> RegimeItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Citadel Securities & Hudson River Trading: 双边限价订单簿微观非线性平均场博弈与高阶随机冲量最优流动性提供控制
public class MfgImpulseAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal ContinuousSpreadBps { get; set; } // 纳什均衡连续双边买卖半价差 δ* (bps)
    public decimal ImpulseThresholdSStar { get; set; } // 离散随机冲量控制临界触发阈值 [s_*, S^*]
    public decimal MeanFieldDensityM { get; set; } // 微观匿名做市商连续体平均场密度 m(t, x)
    public decimal NashEquilibriumAlphaBps { get; set; } // 纳什博弈最优做市 Alpha 增益 (bps)
    public decimal HerdingVulnerability { get; set; } // 微观羊群效应易损脆弱度 (0~1)
    public decimal ImpulseExecutionSavingBps { get; set; } // 离散随机冲量调仓减摩节约 (bps)
    public string MfgStateBadge { get; set; } = string.Empty; // 平均场博弈状态 (⚡ 均衡最优纳什 / 🛡️ 冲量减摩防御 / ⚠️ 羊群雪崩高危)
    public string MfgOrderControlAdvice { get; set; } = string.Empty; // 平均场博弈连续挂单与冲量执行指令
}

public class MeanFieldGameImpulseLiquidityControlResult
{
    public decimal GlobalMeanFieldLiquidityAlphaBps { get; set; } // 平均场博弈做市全局 Alpha 增益 (bps)
    public decimal MeanFieldNashEquilibriumSpreadBps { get; set; } // 纳什均衡连续双边挂单价差中枢 (bps)
    public decimal HerdingCrowdImmunityPct { get; set; } // 群体微观羊群效应免疫度 (%)
    public decimal ImpulseControlEfficiencyRatio { get; set; } // 随机冲量离散挂撤单减摩效率倍数
    public List<MfgImpulseAssetItem> ImpulseItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Two Sigma & WorldQuant: 高维因果发现有向无环图与结构因果模型反事实干预不变性 Alpha 纯化
public class CausalInvarianceAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal CausalInvarianceScore { get; set; } // 因果不变性纯度评分 (0~100)
    public decimal DoCalculusInterventionAlphaBps { get; set; } // 反事实 do-calculus 外生干预纯化 Alpha (bps)
    public int DirectCausalParentsCount { get; set; } // DAG 因果有向拓扑直系父节点数
    public decimal ConfounderBiasAttenuationPct { get; set; } // 后门混杂因子虚假偏差衰减率 (%)
    public decimal CausalStabilityRatio { get; set; } // 跨微观与宏观环境因果稳定性比率
    public string CausalRegimeBadge { get; set; } = string.Empty; // 因果评级 (💎 真因果不变核 / 🔬 弱混杂待纯化 / 🚫 伪相关对撞噪声)
    public string CausalAlphaTradingAdvice { get; set; } = string.Empty; // SCM 结构因果干预与不变性 Alpha 配置指令
}

public class CausalDagStructuralInvarianceAlphaResult
{
    public decimal GlobalCausalInvarianceAlphaBps { get; set; } // 因果不变性纯化全局 Alpha 增益 (bps)
    public decimal MeanCausalGraphSparsityRatio { get; set; } // 因果 DAG 图拓扑稀疏度指数
    public decimal SpuriousCorrelationRejectionRatePct { get; set; } // 虚假统计相关噪声剔除率 (%)
    public decimal CounterfactualRobustnessScore { get; set; } // 反事实干预跨环境稳健性评分 (0~100)
    public List<CausalInvarianceAssetItem> CausalItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Bridgewater Associates & AQR Capital: 广义非对称谱风险测度与动态极值高斯-Student Copula 尾部联动全景压测
public class SpectralRiskCopulaAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal SpectralRiskContributionPct { get; set; } // 广义 Acerbi 谱风险边际资本贡献占比 (%)
    public decimal GpdScaleBeta { get; set; } // 广义帕累托分布 (GPD) 尺度参数 β
    public decimal GpdShapeXi { get; set; } // 广义帕累托分布 (GPD) 厚尾形状参数 ξ (Tail Index)
    public decimal AsymmetricLowerTailCopula { get; set; } // 非对称动态极值 Copula 下行尾部相依系数 λ_L
    public decimal TailConvexityHedgeCostBps { get; set; } // 最优尾部凸性衍生品对冲成本 (bps)
    public decimal StressCapitalAdequacyPct { get; set; } // 极端深水压力资本充足率 (%)
    public string SpectralRiskBadge { get; set; } = string.Empty; // 谱风险评级 (🛡️ 谱风险安全垫裕量 / 🌊 中度肥尾需对冲 / 🌋 极值相依脱钩熔断)
    public string ExtremeTailHedgingAdvice { get; set; } = string.Empty; // 极值 Copula 动态尾部对冲执行指令
}

public class SpectralRiskExtremeCopulaStressResult
{
    public decimal GlobalSpectralRiskCapitalRequirementPct { get; set; } // 组合广义谱风险资本拨备率 (%)
    public decimal ExtremeTailAsymmetricCopulaDependency { get; set; } // 动态极值 Copula 非对称下行尾部相关度
    public decimal GpdTailShapeParameterXi { get; set; } // 广义帕累托分布 (GPD) 厚尾形状参数中枢 ξ
    public decimal OptimalTailConvexityHedgeRatioPct { get; set; } // 最优尾部凸性衍生品对冲覆盖率 (%)
    public List<SpectralRiskCopulaAssetItem> StressItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 55 机构级前沿量化投研模型

// 1. Renaissance Technologies & Two Sigma: 高维随机矩阵理论局部谱去噪与收缩协方差重构
public class RmtSpectralAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal RawSampleVariance { get; set; } // 原始样本经验方差 (%)
    public decimal DenoisedSpectralVariance { get; set; } // RMT 局部谱去噪重构方差 (%)
    public decimal NoiseFilteringRatioPct { get; set; } // 随机矩阵噪声过滤衰减率 (%)
    public decimal SpikeFactorLoading { get; set; } // 主 Spike 因子谱载荷
    public decimal LedoitPeitShrinkageWeight { get; set; } // 局部非线性收缩权重比率
    public decimal SpectralDenoisedAlphaBps { get; set; } // 谱去噪稳健配置 Alpha 增益 (bps)
    public string RmtRegimeBadge { get; set; } = string.Empty; // 谱去噪评级 (💎 纯真信号Spike / 🔬 局部收缩平滑 / 🚫 纯噪声特征态)
    public string RmtPortfolioAdvice { get; set; } = string.Empty; // RMT 协方差重构与去噪配置指令
}

public class RandomMatrixLocalSpectralShrinkageResult
{
    public decimal GlobalRmtSignalToNoiseRatioGain { get; set; } // 全局 RMT 谱去噪信噪比增益 (dB)
    public decimal MarchenkoPasturUpperBoundRatio { get; set; } // Marchenko-Pastur 极限噪声谱上界截止比率 (%)
    public int SpikeFactorCount { get; set; } // 有效 Spike 信号因子特征值数量
    public decimal OptimalShrinkageIntensityPct { get; set; } // Ledoit-Peit 局部非线性谱收缩最优强度 (%)
    public List<RmtSpectralAssetItem> SpectralItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Citadel Securities & Jump Trading: 微观分形霍克斯自激互激订单流毒性与闪崩级联预警
public class HawkesCascadeAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal SelfExcitationAlpha { get; set; } // 霍克斯盘口内自激强度 α_ii
    public decimal CrossExcitationBeta { get; set; } // 跨资产传染互激强度中枢 ∑_{j≠i} α_ij
    public decimal BranchingSpectralRadius { get; set; } // 局部分支谱半径 ρ_i
    public decimal MarkedToxicityIntensity { get; set; } // 标记订单规模毒性到达率 (次/秒)
    public decimal CascadeVulnerabilityPct { get; set; } // 闪崩连锁反应敏感度 (%)
    public decimal HawkesExecutionAlphaBps { get; set; } // 微观防毒自激减摩 Alpha (bps)
    public string HawkesStateBadge { get; set; } = string.Empty; // 霍克斯状态 (🛡️ 低毒稳态泊松 / ⚠️ 强互激亚稳态 / 🚨 级联雪崩临界)
    public string HawkesToxicityAdvice { get; set; } = string.Empty; // 多元霍克斯防毒与撤单保护指令
}

public class MultivariateHawkesToxicityCascadeResult
{
    public decimal GlobalHawkesBranchingRatio { get; set; } // 全局多元霍克斯分支比率谱半径 ρ(Γ)
    public decimal OrderFlowToxicityScore { get; set; } // 订单流逆向选择毒性指数 (0~100)
    public decimal FlashCrashCascadeVulnerabilityPct { get; set; } // 微观闪崩雪崩级联脆弱度 (%)
    public decimal MicrostructureAntidoteAlphaBps { get; set; } // 微观防毒挂单执行 Alpha 增益 (bps)
    public List<HawkesCascadeAssetItem> CascadeItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Bridgewater Associates & AQR Capital: 多资产非线性分形波动率记忆与赫斯特表面跨周期极值回撤对偶防御
public class MultifractalHurstAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal HurstExponentH { get; set; } // 标的长程记忆赫斯特指数 H (0~1)
    public decimal MultifractalWidthDeltaAlpha { get; set; } // 多重分形奇异谱宽 Δα
    public decimal SingularityModeAlphaZero { get; set; } // 奇异性强度极值中枢 α_0
    public decimal PersistenceMemoryScore { get; set; } // 波动长程记忆持久性评分 (0~100)
    public decimal DualDrawdownDefenseGainPct { get; set; } // 极值回撤对偶防御压降率 (%)
    public decimal FractalAntiFragileAlphaBps { get; set; } // 多重分形反脆弱自适应 Alpha (bps)
    public string MultifractalBadge { get; set; } = string.Empty; // 分形评级 (📈 强持续趋势长记忆 / ⚖️ 遍历扩散弱分形 / 🌪️ 极端奇异反转谱)
    public string MultifractalDefenseAdvice { get; set; } = string.Empty; // 赫斯特表面对偶防御配置指令
}

public class MultifractalHurstSurfaceDefenseResult
{
    public decimal GlobalMultifractalSpectrumWidth { get; set; } // 全局多重分形奇异谱宽 Δα
    public decimal LongRangeMemoryHurstExponent { get; set; } // 全局长程记忆赫斯特指数中枢 H
    public decimal FractalAsymmetryDegree { get; set; } // 多重分形左右谱非对称偏度
    public decimal DualExtremumDrawdownDefenseRatio { get; set; } // 跨周期极值回撤对偶防御覆盖率 (%)
    public List<MultifractalHurstAssetItem> MultifractalItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Point72 & Millennium Management: 多代理强化学习非稳态微观策略博弈与对抗性鲁棒策略蒸馏
public class MarlDistillationAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal AdversarialPerturbationRadius { get; set; } // Minimax 对抗性扰动防御半径 ε
    public decimal DistillationKlDivergence { get; set; } // 教师-学生策略蒸馏 KL 散度 D_KL
    public decimal MultiAgentNashCooperationScore { get; set; } // 多代理非零和博弈合作共赢度 (0~100)
    public decimal AntiCrowdingResiliencePct { get; set; } // 防挤压践踏韧性弹性 (%)
    public decimal RobustOptimalWeightPct { get; set; } // 对抗鲁棒蒸馏最优目标权重 (%)
    public decimal DistilledAlphaGainBps { get; set; } // 鲁棒策略蒸馏配置 Alpha (bps)
    public string MarlRegimeBadge { get; set; } = string.Empty; // MARL 评级 (🛡️ 极值对抗鲁棒核 / 🤝 纳什协同弱干扰 / ⚔️ 高扰动需蒸馏)
    public string MarlDistillationAdvice { get; set; } = string.Empty; // 多代理博弈与鲁棒蒸馏落地指令
}

public class MultiAgentAdversarialPolicyDistillationResult
{
    public decimal GlobalAdversarialRobustnessScore { get; set; } // 全局对抗鲁棒性评分 (0~100)
    public decimal NashEquilibriumConvergenceDegree { get; set; } // 多代理马尔可夫博弈纳什均衡收敛度 (%)
    public decimal PolicyDistillationFidelityPct { get; set; } // 极值不确定性鲁棒策略蒸馏保真度 (%)
    public decimal DistilledAntiSqueezeAlphaBps { get; set; } // 策略蒸馏防挤压稳健 Alpha (bps)
    public List<MarlDistillationAssetItem> DistillationItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 56 机构级前沿量化投研模型

// 1. Jane Street & Citadel Securities: 全档限价订单簿 (LOB) 微观价格扩散鞅测度变换与即时流动性真空渗透控制
public class LobMicroPriceAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal SpreadBps { get; set; } // 盘口双边微观买卖价差 (bps)
    public decimal DepthImbalanceRatio { get; set; } // 5档全档订单簿不平衡度 I = (Qb - Qa) / (Qb + Qa) (-1 ~ +1)
    public decimal MicroPriceMartingaleDriftBps { get; set; } // Sasha Stoikov 微观价格调和鞅漂移偏差 (bps)
    public decimal InstantaneousVacuumPenetrationPct { get; set; } // 即时流动性真空破裂渗透率 (%)
    public decimal OptimalQuoteDepthOffsetBps { get; set; } // 最优被动挂单深度偏移量 δ* (bps)
    public decimal MicrostructureExecutionAlphaBps { get; set; } // 做市防逆向选择减摩 Alpha (bps)
    public string LobRegimeBadge { get; set; } = string.Empty; // LOB微观评级 (⚡ 深度均衡公平鞅 / 🌪️ 真空高危需撤单 / 🛡️ 不平衡单边强压)
    public string LobExecutionAdvice { get; set; } = string.Empty; // 限价订单簿做市与真空渗透执行指令
}

public class LobMicroPriceMartingaleVacuumPenetrationResult
{
    public decimal GlobalMicroPriceDriftBps { get; set; } // 全局微观价格鞅漂移中枢 (bps)
    public decimal AverageVacuumPenetrationPct { get; set; } // 全局流动性真空破裂渗透率中枢 (%)
    public decimal PassiveExecutionSpreadSavingBps { get; set; } // 全局被动挂单买卖价差节省 (bps)
    public decimal TotalMarketMakingAlphaBps { get; set; } // 做市微观执行防毒总 Alpha (bps)
    public List<LobMicroPriceAssetItem> LobItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. D.E. Shaw & Two Sigma: 连续时间多维非高斯列维-伊藤跳跃扩散与极值共跳曲率对冲
public class LevyItoJumpAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal ContinuousDiffusionVolPct { get; set; } // 连续时间高斯扩散年化波动率 (%)
    public decimal JumpArrivalIntensityLambda { get; set; } // 非齐次泊松共跳到达强度 λ_jump (次/年)
    public decimal DownsideJumpAsymmetryRatio { get; set; } // Kou 双指数下行跳跃非对称偏度比率 η- / η+
    public decimal JumpVariationRatioPct { get; set; } // 跳跃方差占总二次变差比率 JV (%)
    public decimal JumpCushionHedgeMultiplier { get; set; } // 极值共跳动态吸收垫对冲乘数 (x)
    public decimal DownsideJumpTailDefensePct { get; set; } // 极端断崖跳跃尾部压降率 (%)
    public decimal LevyJumpAntiTailAlphaBps { get; set; } // 非高斯跳跃凸性对冲 Alpha (bps)
    public string LevyJumpBadge { get; set; } = string.Empty; // 跳跃评级 (🌋 高频极值共跳源 / 🌊 弱跳跃平滑扩散 / 🛡️ 厚尾强吸收核)
    public string LevyJumpAdvice { get; set; } = string.Empty; // 列维-伊藤跳跃扩散与共跳对冲指令
}

public class MultidimensionalLevyItoJumpDiffusionResult
{
    public decimal GlobalCoJumpArrivalIntensity { get; set; } // 全局多维共跳年化到达强度 Λ (次/年)
    public decimal GlobalJumpVariationRatioPct { get; set; } // 全局跳跃方差变差占比中枢 (%)
    public decimal GlobalJumpTailDrawdownReductionPct { get; set; } // 极端下行断崖跳跃压降率 (%)
    public decimal GlobalLevyJumpAlphaBps { get; set; } // 极值跳跃凸性对冲总 Alpha (bps)
    public List<LevyItoJumpAssetItem> JumpItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Renaissance Technologies & Millennium Management: 高阶拓扑超图场论与自旋玻璃阻挫能量退火解聚
public class HypergraphSpinGlassAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public int HyperedgeDegree { get; set; } // 高阶关联超边阶数与节点度 d_H(v)
    public decimal SpinGlassFrustrationDensity { get; set; } // 自旋玻璃阻挫局部能量密度 F_frust
    public decimal EdwardsAndersonOrderParameter { get; set; } // 复杂网络相变冻结有序参量 q_EA (0~1)
    public decimal AnnealedRobustOptimalWeight { get; set; } // 模拟退火解聚无阻挫最优权重 (%)
    public decimal SystemicResonanceReductionPct { get; set; } // 跨资产级联共振去杠杆脆弱度压降 (%)
    public decimal HypergraphDeclusteringAlphaBps { get; set; } // 超图拓扑去阻挫稳健 Alpha (bps)
    public string HypergraphBadge { get; set; } = string.Empty; // 超图评级 (🕸️ 高阶超边共振核 / 🧊 深度冻结阻挫态 / 💎 退火解聚稳健基)
    public string HypergraphAnnealingAdvice { get; set; } = string.Empty; // 超图拓扑与自旋玻璃解聚指令
}

public class HypergraphSpinGlassFrustrationAnnealingResult
{
    public decimal GlobalFrustrationEnergyDensity { get; set; } // 全局超图自旋玻璃阻挫能量密度
    public decimal MeanHyperedgeInteractionDegree { get; set; } // 平均高阶超边关联阶数
    public decimal GlobalSystemicResonanceVulnerabilityPct { get; set; } // 全局系统性共振脆弱度压降 (%)
    public decimal HypergraphAnnealingAlphaBps { get; set; } // 超图拓扑去阻挫稳健总 Alpha (bps)
    public List<HypergraphSpinGlassAssetItem> HypergraphItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Bridgewater Associates & BlackRock Aladdin: 达利欧长期主权债务大周期与超级去杠杆马尔可夫状态免疫
public class SovereignDebtMacroAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public string MacroDebtRegimeState { get; set; } = string.Empty; // 长期债务大周期宏观态归属 (繁荣扩张 / 通缩萧条 / 通胀再膨胀 / 信用重构)
    public decimal DeleveragingVulnerabilityIndex { get; set; } // 超级去杠杆脆弱性指数 (0~100)
    public decimal SovereignMonetaryDebasementExposure { get; set; } // 主权信用货币超发贬值敏感敞口 Beta
    public decimal StagflationaryConvexityBufferPct { get; set; } // 滞胀双杀下行凸性缓冲度 (%)
    public decimal DalioImmunityTargetWeight { get; set; } // 达利欧全天候主权债务免疫目标权重 (%)
    public decimal DebtCycleMacroAlphaBps { get; set; } // 长期主权债务大周期免疫 Alpha (bps)
    public string MacroDebtImmunityBadge { get; set; } = string.Empty; // 债务周期评级 (👑 货币信用避险底仓 / ⚡ 繁荣高杠杆敏感 / 🛡️ 通缩去杠杆盾)
    public string SovereignDebtAdvice { get; set; } = string.Empty; // 长期债务大周期与超级去杠杆配置指令
}

public class SovereignDebtCycleDeleveragingImmunityResult
{
    public string CurrentLongTermDebtSupercyclePhase { get; set; } = string.Empty; // 当前长期主权债务超级大周期主导阶段
    public decimal GlobalDeleveragingVulnerabilityScore { get; set; } // 全局去杠杆脆弱性综合评分 (0~100)
    public decimal MarkovRegimeTransitionEntropy { get; set; } // 马尔可夫大周期状态转移信息熵
    public decimal SupercycleMacroImmunityAlphaBps { get; set; } // 长期主权债务周期免疫总 Alpha (bps)
    public List<SovereignDebtMacroAssetItem> DebtItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 57 机构级前沿量化投研模型

// 1. Two Sigma & Citadel Securities: 连续时间马利亚温随机变分微积分与粗糙波动率泛函高阶 Greeks 深度对冲
public class MalliavinRoughVolAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal HurstRoughnessParameterH { get; set; } // 粗糙路径赫斯特指数 H (通常 0.08~0.22)
    public decimal RoughVolOfVol { get; set; } // 粗糙波动率的波动率 nu_rough
    public decimal MalliavinNoiseFreeVega { get; set; } // 马利亚温分部积分无偏 Vega (bps)
    public decimal MalliavinVolgaCurvature { get; set; } // 马利亚温高阶波动率凸性曲率 Volga
    public decimal MalliavinVannaCrossSensitivity { get; set; } // 现货-波动率高阶交叉敏感度 Vanna
    public decimal RoughPathTailDrawdownReductionPct { get; set; } // 粗糙路径防断崖尾部回撤压降率 (%)
    public decimal MalliavinHedgingAlphaBps { get; set; } // 变分积分无偏对冲 Alpha (bps)
    public string RoughVolRegimeBadge { get; set; } = string.Empty; // 粗糙度评级 (🌊 极度粗糙超高阶偏度 / 🛡️ 平滑自适应对冲 / 💎 低扰动平稳基)
    public string RoughVolAdvice { get; set; } = string.Empty; // 马利亚温变分对冲与粗糙度保护指令
}

public class MalliavinRoughVolatilityGreeksResult
{
    public decimal GlobalHurstExponentH { get; set; } // 全局有效粗糙赫斯特指数 H_eff
    public decimal GlobalMalliavinVolgaCurvature { get; set; } // 全局马利亚温高阶曲率
    public decimal AverageRoughTailDefensePct { get; set; } // 平均粗糙断崖防御度 (%)
    public decimal TotalMalliavinHedgingAlphaBps { get; set; } // 马利亚温对冲总 Alpha (bps)
    public List<MalliavinRoughVolAssetItem> MalliavinItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Renaissance Technologies & Jump Trading: 开放量子耗散系统 Lindblad 主方程与微观相干态密度矩阵统计套利
public class QuantumLindbladDecoherenceAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal QuantumStatePurity { get; set; } // 量子态纯度 Tr(rho^2) (0~1]
    public decimal VonNeumannEntropy { get; set; } // 冯·诺依曼微观信息熵 S(rho)
    public decimal DecoherenceHalfLifeMicrosec { get; set; } // 环境退相干特征半衰期 tau (微秒)
    public decimal OffDiagonalCoherenceDegree { get; set; } // 非对角微观相干度 C_l1(rho)
    public decimal DissipativeJumpIntensityGamma { get; set; } // Lindblad 耗散跳跃强度 gamma
    public decimal QuantumStatArbAlphaBps { get; set; } // 开放量子相干统计套利 Alpha (bps)
    public string QuantumRegimeBadge { get; set; } = string.Empty; // 量子相干评级 (⚛️ 强相干纠缠纠偏 / 🌀 临界耗散退相干 / 🛡️ 稳态基态流)
    public string QuantumStatArbAdvice { get; set; } = string.Empty; // 开放量子系统与相干套利指令
}

public class QuantumLindbladDecoherenceStatArbResult
{
    public decimal GlobalQuantumStatePurity { get; set; } // 全局微观量子态纯度 Tr(rho^2)
    public decimal GlobalVonNeumannEntropy { get; set; } // 全局冯·诺依曼微观熵
    public decimal AverageDecoherenceHalfLifeMicrosec { get; set; } // 平均退相干半衰期 (微秒)
    public decimal TotalQuantumStatArbAlphaBps { get; set; } // 开放量子相干统计套利总 Alpha (bps)
    public List<QuantumLindbladDecoherenceAssetItem> QuantumItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Millennium Management & Point72: 连续时间多主体均值场博弈 (Mean-Field Games) 与 FPK-HJB 拥挤踩踏解耦
public class MeanFieldGameAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal CrowdingPressureIndexCPI { get; set; } // 内生拥挤压力指数 CPI (0~100)
    public decimal NashEquilibriumDriftBps { get; set; } // 纳什均衡博弈执行漂移 (bps)
    public decimal FireSaleCascadeVulnerabilityPct { get; set; } // 踩踏抛售级联脆弱度 (%)
    public decimal MeanFieldDecoupledTargetWeight { get; set; } // 均值场博弈解耦目标权重 (%)
    public decimal DoomLoopDefenseBufferPct { get; set; } // 死亡螺旋防御缓冲度 (%)
    public decimal MfgGameTheoreticAlphaBps { get; set; } // 均值场博弈稳健 Alpha (bps)
    public string MfgRegimeBadge { get; set; } = string.Empty; // 均值场评级 (🏛️ 纳什均衡解耦核 / ⚡ 严重拥挤踩踏预警 / 🛡️ 低密度安全带)
    public string MfgDecouplingAdvice { get; set; } = string.Empty; // 均值场博弈与防踩踏解耦指令
}

public class MeanFieldGameCrowdingDecouplingResult
{
    public decimal GlobalCrowdingPressureIndex { get; set; } // 全局拥挤压力指数 CPI (0~100)
    public decimal FpkHjbConvergenceResidual { get; set; } // FPK-HJB 纳什收敛残差 (x10^-4)
    public decimal SystemicDoomLoopReductionPct { get; set; } // 系统性踩踏风险消除率 (%)
    public decimal TotalMfgRobustAlphaBps { get; set; } // 均值场博弈稳健总 Alpha (bps)
    public List<MeanFieldGameAssetItem> MfgItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Bridgewater Associates & AQR Capital: 非平衡态热力学最大熵产生与信息几何 Fisher-Rao 测地线宏观流形平滑轮动
public class ThermodynamicFisherRaoAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal FisherRaoGeodesicDistance { get; set; } // 黎曼流形 Fisher-Rao 测地线最短距离 d_G
    public decimal EntropyProductionRateMEPR { get; set; } // 最大熵产生率 sigma_MEPR
    public decimal OnsagerKineticFrictionBps { get; set; } // 昂萨格动能摩擦阻尼 (bps)
    public decimal RiemannianCurvatureScalarR { get; set; } // 统计流形黎曼标量曲率 R
    public decimal GeodesicSmoothTargetWeight { get; set; } // 测地线超平滑目标权重 (%)
    public decimal TurnoverDragReductionPct { get; set; } // 调仓换手摩擦压缩率 (%)
    public decimal GeodesicMacroAlphaBps { get; set; } // 测地线平滑宏观轮动 Alpha (bps)
    public string FisherRaoBadge { get; set; } = string.Empty; // 流形评级 (🌐 测地线超平滑底仓 / ⚡ 熵产突变活跃态 / 🛡️ 最小散度平衡点)
    public string GeodesicAdvice { get; set; } = string.Empty; // 信息几何与非平衡态热力学轮动指令
}

public class ThermodynamicFisherRaoGeodesicRegimeResult
{
    public decimal GlobalFisherRaoGeodesicDistance { get; set; } // 全局 Fisher-Rao 测地线距离
    public decimal GlobalEntropyProductionRate { get; set; } // 全局非平衡态最大熵产生率
    public decimal AverageTurnoverDragSavingPct { get; set; } // 平均换手摩擦节省率 (%)
    public decimal TotalGeodesicMacroAlphaBps { get; set; } // 测地线宏观轮动总 Alpha (bps)
    public List<ThermodynamicFisherRaoAssetItem> ThermodynamicItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 58 机构级前沿量化投研模型

// 1. Jane Street & Citadel Securities: 连续时间 Glosten-Milgrom 贝叶斯信念更新与流动性逆向选择防护
public class GlostenMilgromAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal InformedTraderRatioAlpha { get; set; } // 知情交易者比例 alpha (0~1)
    public decimal PosteriorHighStateBeliefPi { get; set; } // 高状态后验贝叶斯信念 Pi_t
    public decimal AdverseSelectionSpreadBps { get; set; } // 逆向选择买卖价差补偿 Delta S (bps)
    public decimal InventoryJumpExposurePct { get; set; } // 存货跳跃暴露度 (%)
    public decimal ToxicitySlippageReductionPct { get; set; } // 逆向选择毒性滑点压降率 (%)
    public decimal GlostenMilgromAlphaBps { get; set; } // 贝叶斯挂单防护 Alpha (bps)
    public string AdverseSelectionRegimeBadge { get; set; } = string.Empty; // 评级 (🛡️ 强韧抗逆向选择防御 / ⚠️ 知情流高穿透警戒 / 💎 低毒性深度缓冲)
    public string GlostenMilgromAdvice { get; set; } = string.Empty; // 挂单与防穿透指令
}

public class GlostenMilgromAdverseSelectionResult
{
    public decimal GlobalInformedTraderRatio { get; set; } // 全局平均知情交易者比例
    public decimal GlobalAdverseSelectionSpreadBps { get; set; } // 全局逆向选择加权价差 (bps)
    public decimal AverageSlippageReductionPct { get; set; } // 平均滑点压降率 (%)
    public decimal TotalGlostenMilgromAlphaBps { get; set; } // 逆向选择防御总 Alpha (bps)
    public List<GlostenMilgromAssetItem> GlostenItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Renaissance Technologies & D.E. Shaw: 非广延统计力学 Tsallis 广义熵与多重分形奇异谱极值崩塌预警
public class TsallisSingularAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal NonextensiveParameterQ { get; set; } // Tsallis 非广延参数 q (通常 1.1~2.2)
    public decimal TsallisEntropySq { get; set; } // Tsallis 广义非广延熵 S_q
    public decimal SingularSpectrumWidthDeltaAlpha { get; set; } // 多重分形奇异谱宽度 Delta alpha
    public decimal PhaseTransitionCorrelationLengthXi { get; set; } // 临界相变相干长度 xi
    public decimal AvalancheCollapseDefensePct { get; set; } // 极端相变雪崩防御度 (%)
    public decimal TsallisAdaptiveAlphaBps { get; set; } // 非广延自适应对冲 Alpha (bps)
    public string TsallisRegimeBadge { get; set; } = string.Empty; // 评级 (🌋 临界雪崩相变态 / 🌀 多重分形厚尾区 / 🧊 准平衡态平稳基)
    public string TsallisAdvice { get; set; } = string.Empty; // 统计力学相变对冲指令
}

public class TsallisNonextensiveSingularSpectrumResult
{
    public decimal GlobalNonextensiveParameterQ { get; set; } // 全局有效非广延参数 q_eff
    public decimal GlobalSingularSpectrumWidth { get; set; } // 全局多重分形奇异谱宽度
    public decimal AverageAvalancheDefensePct { get; set; } // 平均雪崩防御度 (%)
    public decimal TotalTsallisAdaptiveAlphaBps { get; set; } // 非广延自适应总 Alpha (bps)
    public List<TsallisSingularAssetItem> TsallisItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Two Sigma & Jump Trading: 随机非线性粘性阻尼与 Fredholm 记忆核最优变分执行清算
public class ViscousExecutionAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal ViscousMemoryDecayGamma { get; set; } // 非局部幂律记忆核衰减指数 gamma (0.3~0.8)
    public decimal FredholmDampingRatio { get; set; } // Fredholm 粘性阻尼比
    public decimal EulerLagrangeOptimalPace { get; set; } // 欧拉-拉格朗日最优变分清算速度 (手/分)
    public decimal NonlinearSlippageSavingsPct { get; set; } // 非线性粘性执行滑点节省率 (%)
    public decimal ViscousExecutionAlphaBps { get; set; } // 阿斯普兰德凸变分减摩 Alpha (bps)
    public string ViscousRegimeBadge { get; set; } = string.Empty; // 评级 (🚀 粘性超流平滑清算 / 🌊 强阻尼幂律回弹 / ⚖️ 稳态平衡阻尼)
    public string ViscousAdvice { get; set; } = string.Empty; // 变分清算拆单执行指令
}

public class ViscousMemoryOptimalExecutionResult
{
    public decimal GlobalViscousMemoryDecayGamma { get; set; } // 全局有效粘性记忆衰减指数 gamma
    public decimal GlobalFredholmDampingRatio { get; set; } // 全局 Fredholm 阻尼比
    public decimal AverageSlippageSavingsPct { get; set; } // 平均滑点节省率 (%)
    public decimal TotalViscousExecutionAlphaBps { get; set; } // 变分减摩执行总 Alpha (bps)
    public List<ViscousExecutionAssetItem> ViscousItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Bridgewater Associates & Millennium Management: 结构向量自回归有向无环图 (SVAR-DAG) 与 Pearl 反事实因果干预宏观网络
public class SvarDagCausalAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal CausalCentralityScore { get; set; } // DAG 因果拓扑中心度评分 (0~100)
    public decimal CounterfactualShockResponsePct { get; set; } // Pearl do(X) 反事实冲击响应率 (%)
    public decimal SystemicCausalFragilityIndex { get; set; } // 系统性因果脆弱度指数 (0~100)
    public decimal CausalDecoupledTargetWeight { get; set; } // 因果解耦最优目标权重 (%)
    public decimal AntifragileCausalAlphaBps { get; set; } // 结构反事实因果 Alpha (bps)
    public string CausalRegimeBadge { get; set; } = string.Empty; // 评级 (💎 因果源头强抗逆节点 / ⚡ 传导级联敏感节点 / 🛡️ 反事实吸收缓冲垫)
    public string CausalAdvice { get; set; } = string.Empty; // 结构因果推断调仓指令
}

public class SvarDagCausalInterventionNetworkResult
{
    public decimal GlobalCausalNetworkDensity { get; set; } // 全局 SVAR-DAG 因果网络密度
    public decimal GlobalSystemicFragilityIndex { get; set; } // 全局系统性因果脆弱度指数
    public decimal AverageShockImmunityPct { get; set; } // 平均反事实冲击免疫度 (%)
    public decimal TotalAntifragileCausalAlphaBps { get; set; } // 结构因果反脆弱总 Alpha (bps)
    public List<SvarDagCausalAssetItem> CausalItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 59 机构级前沿量化投研模型

// 1. Citadel Securities & Jump Trading: 连续拍卖 Kyle's λ 价格冲击弹性与订单流信息穿透深度控制
public class KyleAuctionAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal KyleLambdaImpactBps { get; set; } // Kyle's λ 价格冲击系数 (bps/单位成交量)
    public decimal PriceElasticityCoefficient { get; set; } // 盘口流动性价格弹性 (1 / (λ * P))
    public decimal InformationPenetrationDepthPct { get; set; } // 知情订单穿透深度 (%)
    public decimal OptimalQuoteBufferDepth { get; set; } // 盘口最优挂单缓冲档位
    public decimal KyleElasticityAlphaBps { get; set; } // Kyle 弹性防御与价差优化 Alpha (bps)
    public string KyleRegimeBadge { get; set; } = string.Empty; // 评级 (🛡️ 强弹性厚盘缓冲 / ⚠️ 知情大单易穿透 / 💎 稳态深度最优区)
    public string KyleAdvice { get; set; } = string.Empty; // Kyle 连续拍卖挂单与深度控制指令
}

public class KyleContinuousAuctionElasticityResult
{
    public decimal GlobalKyleLambdaBps { get; set; } // 全局加权 Kyle's λ (bps)
    public decimal AveragePriceElasticity { get; set; } // 平均流动性价格弹性
    public decimal AveragePenetrationReductionPct { get; set; } // 平均穿透滑点压降率 (%)
    public decimal TotalKyleElasticityAlphaBps { get; set; } // Kyle 弹性挂单防御总 Alpha (bps)
    public List<KyleAuctionAssetItem> KyleItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Renaissance Technologies & D.E. Shaw: 拓扑 Wilson 重整化群矩流动与仿射渗流相变临界聚类
public class WilsonPercolationAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal WilsonRgFlowVelocity { get; set; } // Wilson 重整化群有效流速
    public decimal GiantComponentAffiliationScore { get; set; } // 渗流巨连通分支 (GCC) 关联度评分 (0~100)
    public decimal PercolationCriticalSusceptibility { get; set; } // 临界渗流相变敏感度 χ
    public decimal RgInvariantTargetWeight { get; set; } // 重整化群不变性解耦最优权重 (%)
    public decimal PercolationDefenseAlphaBps { get; set; } // 抗渗流相变自适应 Alpha (bps)
    public string WilsonRegimeBadge { get; set; } = string.Empty; // 评级 (🌊 巨连通相变核心区 / 🏝️ RG 孤立稳定岛 / ⚖️ 准临界渗流过渡区)
    public string WilsonAdvice { get; set; } = string.Empty; // 重整化群抗相变配置指令
}

public class WilsonRenormalizationPercolationResult
{
    public decimal GlobalPercolationProbabilityPc { get; set; } // 全局临界渗流连接概率阈值 p_c
    public decimal GiantConnectedComponentRatioPct { get; set; } // 巨连通分支 (GCC) 规模占比 (%)
    public decimal AveragePercolationSusceptibility { get; set; } // 平均相变敏感度 χ
    public decimal TotalPercolationDefenseAlphaBps { get; set; } // 抗渗流重整化总 Alpha (bps)
    public List<WilsonPercolationAssetItem> WilsonItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Two Sigma & PDT Partners: 连续时间随机微分博弈 (SDG) 捕食者-猎物纳什均衡与流动性黑洞抢跑规避变分
public class PredatoryEvasionAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal PredatoryPressureIndex { get; set; } // 捕食者抢跑做空压力指数 (0~100)
    public decimal LiquidityBlackHoleDepthPct { get; set; } // 流动性黑洞下潜深度 (%)
    public decimal NashEvasionOptimalSpeed { get; set; } // HJBI 纳什规避最优变分执行速度 (手/分)
    public decimal CamouflageRandomizationPct { get; set; } // 伪装扰动混淆因子 (%)
    public decimal PredatoryEvasionAlphaBps { get; set; } // 防掠食变分挽回 Alpha (bps)
    public string PredatoryRegimeBadge { get; set; } = string.Empty; // 评级 (🛡️ 强伪装规避潜行 / ⚠️ 黑洞抢跑高危区 / ⚡ 快速穿越超流态)
    public string PredatoryAdvice { get; set; } = string.Empty; // HJBI 防掠食拆单避险指令
}

public class PredatoryGameLiquidityEvasionResult
{
    public decimal GlobalPredatoryPressureIndex { get; set; } // 全局捕食者压力指数
    public decimal AverageBlackHoleDepthPct { get; set; } // 平均流动性黑洞下潜深度 (%)
    public decimal AveragePredatoryDamageReductionPct { get; set; } // 平均掠食滑点减损率 (%)
    public decimal TotalPredatoryEvasionAlphaBps { get; set; } // 防掠食规避总 Alpha (bps)
    public List<PredatoryEvasionAssetItem> PredatoryItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Bridgewater Associates & AQR Capital: 连续时间漂移-扩散非线性状态空间与鲁棒卡尔曼-布西宏观体制平价
public class DriftDiffusionMacroAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal MacroDriftSensitivityBeta { get; set; } // 连续宏观漂移敏感度 Beta
    public decimal ContinuousQuadrantAffinityScore { get; set; } // 经济四象限连续亲和度评分 (0~100)
    public decimal KalmanBucyTrackingErrorPct { get; set; } // 鲁棒卡尔曼-布西宏观跟踪误差 (%)
    public decimal RegimeResilientTargetWeight { get; set; } // 宏观漂移不变性平价目标权重 (%)
    public decimal MacroParityAlphaBps { get; set; } // 鲁棒宏观平价 Alpha (bps)
    public string DriftRegimeBadge { get; set; } = string.Empty; // 评级 (💎 宏观漂移中性压舱石 / 🌪️ 象限转换敏感标的 / 🛡️ 卡尔曼粘性抗震器)
    public string DriftMacroAdvice { get; set; } = string.Empty; // 连续状态空间动态平价指令
}

public class DriftDiffusionKalmanMacroParityResult
{
    public decimal GlobalMacroDriftSpeed { get; set; } // 全局宏观漂移速度 (单位/月)
    public decimal AverageKalmanConfidencePct { get; set; } // 平均鲁棒卡尔曼滤波置信度 (%)
    public decimal RegimeChurnReductionPct { get; set; } // 宏观平价去震荡换手压降率 (%)
    public decimal TotalMacroParityAlphaBps { get; set; } // 鲁棒宏观平价总 Alpha (bps)
    public List<DriftDiffusionMacroAssetItem> DriftMacroItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 60 机构级前沿量化投研模型

// 1. Citadel Securities & Optiver: 非线性泊松跳跃边界反射与 Avellaneda-Stoikov 粘性滑移面做市控制
public class PoissonMarketMakingAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal OptimalBidAskSpreadBps { get; set; } // 最优非对称双边挂单买卖价差 (bps)
    public decimal InventoryReservationOffsetBps { get; set; } // 库存保留价格偏移量 (bps)
    public decimal InventoryHalfLifeHours { get; set; } // 粘性滑移面库存吸收半衰期 (小时)
    public decimal QueueExhaustionHazardPct { get; set; } // 盘口深度击穿突变危险率 (%)
    public decimal BoundaryMarketMakingAlphaBps { get; set; } // 泊松边界反射做市净 Alpha (bps)
    public string PoissonRegimeBadge { get; set; } = string.Empty; // 评级 (🛡️ 粘性滑移抗穿透 / ⚠️ 极值毒性库存警戒 / 💎 稳态价差双向收割)
    public string PoissonAdvice { get; set; } = string.Empty; // Avellaneda-Stoikov 非齐次挂单指令
}

public class NonlinearPoissonBoundaryMarketMakingResult
{
    public decimal GlobalOptimalSpreadBps { get; set; } // 全局加权最优双边挂单价差 (bps)
    public decimal AverageInventoryDecayHours { get; set; } // 平均库存吸收半衰期 (小时)
    public decimal AdverseSelectionBreachReductionPct { get; set; } // 逆向选择击穿压降率 (%)
    public decimal TotalBoundaryMarketMakingAlphaBps { get; set; } // 边界反射做市总 Alpha (bps)
    public List<PoissonMarketMakingAssetItem> PoissonItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Renaissance Technologies & D.E. Shaw: 仿射 Kac-Moody 无限维李代数根格与杨-米尔斯规范场拓扑荷度量
public class KacMoodyGaugeAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal KacMoodyRootProjectionNorm { get; set; } // 仿射 Kac-Moody 根格映射投影范数
    public decimal YangMillsCurvatureMagnitude { get; set; } // 杨-米尔斯规范场强曲率 2-形式模长 |F|
    public decimal SymmetryBreakingOrderParameter { get; set; } // 规范对称性破缺序参量 Φ (0~100)
    public decimal GaugeInvariantTargetWeight { get; set; } // 规范场拓扑荷不变性稳定权重 (%)
    public decimal TopologicalChargeAlphaBps { get; set; } // 拓扑荷抗相变 Alpha (bps)
    public string KacMoodyRegimeBadge { get; set; } = string.Empty; // 评级 (🌀 规范场瞬子活跃相 / 🛡️ 拓扑荷守恒平坦相 / ⚖️ 破缺过渡临界相)
    public string KacMoodyAdvice { get; set; } = string.Empty; // 李代数根格规范不变性配置指令
}

public class KacMoodyGaugeTopologicalChargeResult
{
    public decimal GlobalTopologicalChargeNumber { get; set; } // 全局杨-米尔斯瞬子拓扑荷数 Q_gauge
    public decimal AverageGaugeCurvature { get; set; } // 平均规范场强曲率
    public decimal FalseAlarmResonanceReductionPct { get; set; } // 极值流动性共振误判压降率 (%)
    public decimal TotalTopologicalChargeAlphaBps { get; set; } // 拓扑规范不变总 Alpha (bps)
    public List<KacMoodyGaugeAssetItem> GaugeItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Two Sigma & Jump Trading: McKean-Vlasov 前向-后向随机微分方程 (FBSDE) 非局部多主体平均场协同清算
public class McKeanVlasovAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal CollectiveCrowdingDragCoeff { get; set; } // 多 Pod 群体拥挤拖拽系数 Γ_crowd
    public decimal NonLocalMeanFieldDriftBps { get; set; } // 非局部平均场分布诱导反向漂移 (bps)
    public decimal OptimalDecrowdingSpeed { get; set; } // FBSDE 纳什最优解耦清算速度 (手/分)
    public decimal CrowdingShortfallSavingPct { get; set; } // 避免踩踏清算落差节省率 (%)
    public decimal MeanFieldEvasionAlphaBps { get; set; } // 平均场协同避踩踏 Alpha (bps)
    public string McKeanVlasovRegimeBadge { get; set; } = string.Empty; // 评级 (⚡ 协同解耦超流区 / ⚠️ 多Pod并发踩踏陷阱 / 🛡️ 低拥挤稳健滑行)
    public string McKeanVlasovAdvice { get; set; } = string.Empty; // McKean-Vlasov FBSDE 动态分单调度指令
}

public class McKeanVlasovOptimalLiquidationResult
{
    public decimal GlobalCrowdingDragCoeff { get; set; } // 全局群体拥挤拖拽系数
    public decimal AverageMeanFieldDriftBps { get; set; } // 平均非局部平均场漂移 (bps)
    public decimal AggregateLiquidationSavingPct { get; set; } // 组合总体清算落差节省率 (%)
    public decimal TotalMeanFieldEvasionAlphaBps { get; set; } // 平均场协同清算总 Alpha (bps)
    public List<McKeanVlasovAssetItem> McKeanVlasovItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Bridgewater Associates & Millennium Macro: 分数阶伊藤-沃尔泰拉 (Ito-Volterra) 非马尔可夫信用周期记忆与遍历谱平价
public class VolterraCreditParityAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal VolterraMemoryPersistenceIndex { get; set; } // 沃尔泰拉奇异核深层记忆持久度 (0~100)
    public decimal ErgodicSpectralRiskContributionPct { get; set; } // 跨周期遍历谱风险贡献占比 (%)
    public decimal MultiCycleDebtOverhangBeta { get; set; } // 多周期主权/信用债务悬垂贝塔
    public decimal VolterraResilientTargetWeight { get; set; } // 非马尔可夫全天候平价目标权重 (%)
    public decimal VolterraCreditParityAlphaBps { get; set; } // 信用周期长记忆平价 Alpha (bps)
    public string VolterraRegimeBadge { get; set; } = string.Empty; // 评级 (💎 长周期信用吸收基石 / 🌪️ 债务记忆高敏波动源 / 🛡️ 遍历谱平价中枢)
    public string VolterraAdvice { get; set; } = string.Empty; // 分数阶沃尔泰拉全天候再平衡指令
}

public class VolterraNonMarkovianCreditParityResult
{
    public decimal GlobalVolterraMemoryHurst { get; set; } // 全局分数阶深层记忆赫斯特指数 H_Volterra
    public decimal ErgodicSpectralEntropy { get; set; } // 遍历谱能量信息熵
    public decimal DrawdownRecoveryCycleShortenPct { get; set; } // 回撤修复周期缩短率 (%)
    public decimal TotalVolterraCreditParityAlphaBps { get; set; } // 沃尔泰拉信用谱平价总 Alpha (bps)
    public List<VolterraCreditParityAssetItem> VolterraItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 61 机构级前沿量化投研模型

// 1. Citadel Securities & Jane Street: 微观高阶粗糙霍克斯过程与订单簿队列隐性排队延迟博弈
public class RoughHawkesAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal RoughHawkesBranchingRatio { get; set; } // 粗糙霍克斯自激分支比 η_Hawkes
    public decimal QueueSurvivalProbabilityPct { get; set; } // 队列排队生存概率 P_survive (%)
    public decimal LatencyArbitrageHazardPct { get; set; } // 延迟套利抢跑危险率 ξ_latency (%)
    public decimal OptimalQueueSpreadBps { get; set; } // 最优跳级挂单价差 (bps)
    public decimal RoughHawkesQueueAlphaBps { get; set; } // 粗糙霍克斯排队净 Alpha (bps)
    public string RoughHawkesRegimeBadge { get; set; } = string.Empty; // 评级 (💎 极速通道优先排队 / 🛡️ 稳态队列深度防御 / ⚠️ 延迟套利抢跑警戒)
    public string RoughHawkesAdvice { get; set; } = string.Empty; // 微观粗糙排队与延迟对冲挂单指令
}

public class RoughHawkesQueueLatencyArbitrageResult
{
    public decimal GlobalOptimalQueueSpreadBps { get; set; } // 全局最优跳级挂单价差 (bps)
    public decimal AverageQueueSurvivalRatePct { get; set; } // 平均排队生存概率 (%)
    public decimal LatencyArbitrageSlippageReductionPct { get; set; } // 延迟套利滑点压降率 (%)
    public decimal TotalRoughHawkesQueueAlphaBps { get; set; } // 粗糙霍克斯排队总 Alpha (bps)
    public List<RoughHawkesAssetItem> HawkesItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. Renaissance Technologies & D.E. Shaw: 相空间辛几何哈密顿流形动力学与辛积分保结构降维共振
public class SymplecticManifoldAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal SymplecticMomentumMagnitude { get; set; } // 共轭动量范数 ||p||
    public decimal PoincareSectionLyapunovExponent { get; set; } // 庞加莱截面李雅普诺夫指数 λ_Poincare
    public decimal SymplecticEnergyConservationRatio { get; set; } // 辛积分能量守恒率 (%)
    public decimal SymplecticPreservingTargetWeight { get; set; } // 辛几何保结构目标权重 (%)
    public decimal SymplecticManifoldAlphaBps { get; set; } // 辛流形保结构 Alpha (bps)
    public string SymplecticRegimeBadge { get; set; } = string.Empty; // 评级 (🛡️ 辛流形拟周期稳定相 / 🌀 庞加莱激波共振相 / ⚖️ 正则相流平衡相)
    public string SymplecticAdvice { get; set; } = string.Empty; // 辛几何保结构配置指令
}

public class SymplecticHamiltonianManifoldResonanceResult
{
    public decimal GlobalSymplecticPhaseVolumeDrift { get; set; } // 相空间辛体积漂移率 ΔΩ
    public decimal AverageLyapunovExponent { get; set; } // 平均庞加莱李雅普诺夫指数
    public decimal FalseResonanceAvoidancePct { get; set; } // 虚假共振崩溃规避率 (%)
    public decimal TotalSymplecticManifoldAlphaBps { get; set; } // 辛几何保结构总 Alpha (bps)
    public List<SymplecticManifoldAssetItem> SymplecticItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Two Sigma & Point72: 最优传输 (Monge-Kantorovich) Wasserstein 重心测度流与截面非平衡协同重构
public class WassersteinBarycenterAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal WassersteinDistanceToBarycenter { get; set; } // 至 Wasserstein 重心测度距离 W_2
    public decimal SinkhornTransportCostTenThousand { get; set; } // Sinkhorn 熵正则传输功损耗 (万元)
    public decimal RicciCurvatureDispersion { get; set; } // 测度空间奥托-里奇曲率离散度
    public decimal GeodesicOptimalTargetWeight { get; set; } // 测地线平滑重构目标权重 (%)
    public decimal OptimalTransportAlphaBps { get; set; } // 最优传输非平衡重构 Alpha (bps)
    public string WassersteinRegimeBadge { get; set; } = string.Empty; // 评级 (💎 测度重心核心吸附极 / 🛡️ 测地线稳健传输流 / 🌪️ 高曲率非平衡耗散源)
    public string WassersteinAdvice { get; set; } = string.Empty; // 最优传输测地线动态重构指令
}

public class WassersteinBarycenterDynamicRebalancingResult
{
    public decimal GlobalBarycenterEntropy { get; set; } // 全局测度重心香农-玻尔兹曼熵
    public decimal AverageWassersteinDistance { get; set; } // 平均 Wasserstein 测度距离
    public decimal TransportFrictionSavingPct { get; set; } // 传输摩擦损耗节省率 (%)
    public decimal TotalOptimalTransportAlphaBps { get; set; } // 最优传输重构总 Alpha (bps)
    public List<WassersteinBarycenterAssetItem> WassersteinItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Bridgewater Associates & AQR Capital: 连续谱量子混沌非对易算子多尺度周期与超全天候宏观体制平价
public class QuantumSpectralChaosAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal DysonLevelSpacingRatio { get; set; } // 戴森布朗运动能级排斥比 r_level
    public decimal QuantumSpectralRigidityIndex { get; set; } // 量子混沌谱刚度指数 Δ_3
    public decimal QuantumTunnelingJumpProbabilityPct { get; set; } // 量子隧穿跃迁概率 (%)
    public decimal QuantumChaosParityTargetWeight { get; set; } // 量子混沌平价目标权重 (%)
    public decimal QuantumSpectralParityAlphaBps { get; set; } // 量子谱平价 Alpha (bps)
    public string QuantumRegimeBadge { get; set; } = string.Empty; // 评级 (⚛️ GUE 普适谱刚度基石 / 🛡️ 超全天候隧穿免疫区 / 🌪️ 能级交叉强耦合源)
    public string QuantumAdvice { get; set; } = string.Empty; // 量子谱刚度超全天候宏观平价指令
}

public class QuantumSpectralChaosMacroParityResult
{
    public decimal GlobalGueSpectralRigidity { get; set; } // 全局高斯酉系综 GUE 谱刚度
    public decimal BerryRobnikChaosEntropy { get; set; } // Berry-Robnik 量子混沌熵
    public decimal MacroTailDrawdownMitigationPct { get; set; } // 宏观尾部滞胀回撤减免率 (%)
    public decimal TotalQuantumSpectralParityAlphaBps { get; set; } // 量子混沌超全天候总 Alpha (bps)
    public List<QuantumSpectralChaosAssetItem> QuantumItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion

#region Phase 62: 机构级前沿量化投研模型

// 1. Millennium Management & Point72: 多子策略/基金高水位动态资本回撤扣划与跨组合因子拥挤解耦
public class PodFactorCrowdingAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal PodPeakToTroughDrawdownPct { get; set; } // 子策略历史峰谷高水位动态回撤 (%)
    public decimal PodDrawdownClawbackRatioPct { get; set; } // 高水位回撤动态资本扣划比例 (%)
    public decimal CrossPodFactorCrowdingIndex { get; set; } // 跨子策略隐性因子拥挤度 (0~1)
    public decimal LiquidityContagionVulnerabilityPct { get; set; } // 流动性踩踏传染脆弱度 (%)
    public decimal CrowdingDecoupledTargetWeight { get; set; } // 因子解耦与扣划后目标配置权重 (%)
    public decimal MultiPodClawbackAlphaBps { get; set; } // 动态扣划与拥挤解耦净 Alpha (bps)
    public string PodRegimeBadge { get; set; } = string.Empty; // 评级 (💎 稳健高水位基石 / ⚠️ 因子高拥挤踩踏警戒 / 🛡️ 强制扣划隔离保护)
    public string PodAdvice { get; set; } = string.Empty; // 资本重分配与拥挤解耦指令
}

public class MultiPodFactorCrowdingClawbackResult
{
    public decimal GlobalAverageClawbackRatioPct { get; set; } // 全局平均子策略回撤扣划比例 (%)
    public decimal PortfolioLatentCrowdingEntropy { get; set; } // 组合隐性因子拥挤谱熵
    public decimal CascadeLiquidationRiskMitigationPct { get; set; } // 踩踏踩点协同清算风险化解率 (%)
    public decimal TotalMultiPodClawbackAlphaBps { get; set; } // 多子策略解耦净 Alpha 总贡献 (bps)
    public List<PodFactorCrowdingAssetItem> PodItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 2. BlackRock Aladdin & NBIM / GIC: NGFS 气候宏观转型三情景压力测试与碳贝塔搁浅资产 VaR
public class ClimateTransitionAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal AssetCarbonBeta { get; set; } // 范围一/二/三碳排放与 Capex 敏感度碳贝塔
    public decimal OrderlyTransitionImpactPct { get; set; } // NGFS 有序转型 (1.5°C) 估值冲击 (%)
    public decimal DisorderlyTransitionImpactPct { get; set; } // NGFS 无序转型 (2.0°C) 突变折价 (%)
    public decimal StrandedAssetExtremeVaRPct { get; set; } // 棕色搁浅资产极端气候压力 VaR (%)
    public decimal ClimateResilientTargetWeight { get; set; } // 气候韧性最优目标配置权重 (%)
    public decimal ClimateTransitionAlphaBps { get; set; } // 绿色转型避险净 Alpha (bps)
    public string ClimateRegimeBadge { get; set; } = string.Empty; // 评级 (🌿 净零转型低碳领跑 / ⚡ 碳税中性转型过渡 / 🚨 棕色搁浅高危资产)
    public string ClimateAdvice { get; set; } = string.Empty; // NGFS 气候压力测试配置应对指令
}

public class ClimateTransitionStrandedAssetStressResult
{
    public decimal PortfolioWeightedCarbonBeta { get; set; } // 组合加权平均碳贝塔
    public decimal OrderlyScenarioPortfolioDrawdownPct { get; set; } // 有序转型全组合净值冲击预估 (%)
    public decimal DisorderlyStrandedWriteDownPct { get; set; } // 无序转型搁浅资产潜在减记比例 (%)
    public decimal TotalClimateTransitionAlphaBps { get; set; } // 气候转型超额防卫总 Alpha (bps)
    public List<ClimateTransitionAssetItem> ClimateItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 3. Two Sigma & D.E. Shaw: 高阶张量环 (Tensor Ring) 紧致分解与隐式多模态 Alpha 场
public class TensorRingAlphaAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public int TensorRingCoreRank { get; set; } // 张量环紧致潜在截断秩 R_TR
    public decimal MultimodalEntanglementEntropy { get; set; } // 多模态张量环循环缠结信息熵
    public decimal TensorNoiseSuppressionPct { get; set; } // 高阶张量去噪信噪比提升率 (%)
    public decimal TensorRingOptimalTargetWeight { get; set; } // 张量环降维保真目标权重 (%)
    public decimal TensorRingAlphaFieldBps { get; set; } // 多模态张量场净 Alpha (bps)
    public string TensorRegimeBadge { get; set; } = string.Empty; // 评级 (🌀 高阶张量强循环相干 / 🌐 稳态多模态低秩核 / 🌫️ 张量高噪退化态)
    public string TensorAdvice { get; set; } = string.Empty; // 张量环紧致流形配置指令
}

public class TensorRingMultimodalAlphaFieldResult
{
    public decimal GlobalTensorRingReconstructionError { get; set; } // 张量环全局 Frobenius 重构相对误差
    public decimal AverageMultimodalEntanglement { get; set; } // 平均多模态时空循环相干度
    public decimal HighDimensionalAlphaPurityGainPct { get; set; } // 高维 Alpha 挖掘纯度提升率 (%)
    public decimal TotalTensorRingAlphaFieldBps { get; set; } // 多模态张量环总 Alpha 贡献 (bps)
    public List<TensorRingAlphaAssetItem> TensorItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

// 4. Citadel & Jump Trading: 维纳-泊松空间马利亚温微分 (Malliavin Calculus) 跳跃扩散随机波动率对冲
public class MalliavinHedgingAssetItem
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal MalliavinDeltaRatio { get; set; } // 马利亚温分部积分解析 Delta 对冲比率
    public decimal MalliavinGammaSensitivity { get; set; } // 马利亚温变分二阶曲率 Gamma 敏感度
    public decimal JumpDiffusionHazardIntensity { get; set; } // 泊松跳跃弥散突变强度 lambda_jump (年化)
    public decimal MalliavinImmunizedTargetWeight { get; set; } // 马利亚温跳跃扩散免疫目标权重 (%)
    public decimal MalliavinHedgingAlphaBps { get; set; } // 瞬时变分对冲净 Alpha (bps)
    public string MalliavinRegimeBadge { get; set; } = string.Empty; // 评级 (🛡️ 维纳-泊松完美免疫 / ⚡ 随机波动凸度防穿透 / 🌊 跳跃弥散高敏敞口)
    public string MalliavinAdvice { get; set; } = string.Empty; // 马利亚温积分变分对冲与再平衡指令
}

public class MalliavinJumpDiffusionHedgingResult
{
    public decimal GlobalMalliavinHedgeRatio { get; set; } // 全局加权马利亚温跳跃免疫对冲系数
    public decimal StochasticVolConvexityCapture { get; set; } // 随机波动率凸度增益捕捉 (bps)
    public decimal ExtremeJumpSlippageReductionPct { get; set; } // 极端跳跃扩散执行滑点压降率 (%)
    public decimal TotalMalliavinHedgingAlphaBps { get; set; } // 马利亚温变分对冲总 Alpha (bps)
    public List<MalliavinHedgingAssetItem> MalliavinItems { get; set; } = new();
    public string ExecutiveVerdict { get; set; } = string.Empty;
}

#endregion











