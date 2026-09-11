using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase28Tests
{
    private static List<NavRecord> GenerateSyntheticNav(DateTime start, int days, double annualRet, double annualVol, int seed = 42)
    {
        var list = new List<NavRecord>();
        double nav = 1.0;
        double dailyMean = annualRet / 252.0;
        double dailyVol = annualVol / Math.Sqrt(252.0);
        var rng = new Random(seed);

        for (int i = 0; i < days; i++)
        {
            list.Add(new NavRecord
            {
                Date = start.AddDays(i),
                UnitNav = (decimal)nav,
                CumulativeNav = (decimal)nav,
                DailyReturn = 0m
            });

            double u1 = Math.Max(1e-8, rng.NextDouble());
            double u2 = Math.Max(1e-8, rng.NextDouble());
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            double ret = dailyMean + dailyVol * z;
            nav *= (1.0 + ret);
        }

        for (int i = 1; i < list.Count; i++)
        {
            list[i].DailyReturn = Math.Round((list[i].UnitNav - list[i - 1].UnitNav) / list[i - 1].UnitNav * 100m, 4);
        }

        return list;
    }

    private static List<(FundDetail Fund, decimal Weight)> CreateSampleComponents()
    {
        var baseDate = new DateTime(2023, 1, 1);
        var fundA = new FundDetail { Code = "110011", Name = "易方达中小盘混合 (偏股型)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.16, 0.22, seed: 111) };
        var fundB = new FundDetail { Code = "510300", Name = "华泰柏瑞沪深300ETF (宽基指数)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.08, 0.15, seed: 222) };
        var fundC = new FundDetail { Code = "000001", Name = "华夏纯债中短债A (固定收益)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.042, 0.032, seed: 333) };
        var fundD = new FundDetail { Code = "000509", Name = "易方达现金增利货币A (流动性货币)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.021, 0.005, seed: 444) };

        return new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 35.0m),
            (fundB, 25.0m),
            (fundC, 25.0m),
            (fundD, 15.0m)
        };
    }

    [TestMethod]
    public void Test_AssetCrowdingRadar_ScoreAndAlertLevels()
    {
        var components = CreateSampleComponents();

        var result = QuantCalculator.CalculateAssetCrowdingRadar(components);

        Assert.IsNotNull(result, "Asset crowding radar result must not be null.");
        Assert.AreEqual(4, result.CrowdingItems.Count, "Should analyze all 4 components.");
        Assert.IsTrue(result.OverallCrowdingScore >= 0m && result.OverallCrowdingScore <= 100m,
            $"Overall crowding score ({result.OverallCrowdingScore}) should be within [0, 100].");
        Assert.IsTrue(result.AveragePairwiseCorrelation >= -1.0m && result.AveragePairwiseCorrelation <= 1.0m,
            "Pairwise correlation must be between -1.0 and 1.0.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.CrowdingRiskLevel), "Crowding risk level must not be empty.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.LiquidityCascadeWarning), "Cascade warning must not be empty.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveAdvice), "Executive advice must not be empty.");

        foreach (var item in result.CrowdingItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.FundCode), "Fund code must be populated.");
            Assert.IsTrue(item.PortfolioWeightPercent > 0m, "Weight must be positive.");
            Assert.IsTrue(item.CrowdingCompositeScore >= 0m && item.CrowdingCompositeScore <= 100m,
                "Item composite score must be within [0, 100].");
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.CrowdingStatusBadge), "Status badge must be present.");
        }
    }

    [TestMethod]
    public void Test_DrawdownConstrainedKelly_MonotonicityAndDrawdownCeiling()
    {
        var baseDate = new DateTime(2023, 1, 1);
        var navs = GenerateSyntheticNav(baseDate, 365, 0.12, 0.18, seed: 555);
        var dailyReturns = navs.Select(n => n.DailyReturn).ToList();

        decimal totalCapitalWan = 2500m;
        decimal maxDdCeiling = 10.0m;
        decimal fractionalScalar = 0.50m;

        var result = QuantCalculator.CalculateDrawdownConstrainedKelly(
            dailyReturns, totalCapitalWan, maxDdCeiling, fractionalScalar);

        Assert.IsNotNull(result, "Drawdown constrained Kelly result must not be null.");
        Assert.AreEqual(maxDdCeiling, result.MaxDrawdownCeilingPercent, "Ceiling must match configuration.");
        Assert.AreEqual(fractionalScalar, result.FractionalKellyScalar, "Fractional scalar must match.");
        Assert.IsTrue(result.UnconstrainedFullKellyLeverage > 0m, "Full Kelly leverage must be positive.");
        Assert.IsTrue(result.OptimalEquityWeightPercent >= 0m && result.OptimalEquityWeightPercent <= 100m,
            "Optimal equity weight must be bounded in [0, 100]%.");
        Assert.AreEqual(100.0, (double)(result.OptimalEquityWeightPercent + result.RecommendedCashBufferPercent), 0.1,
            "Equity weight + cash buffer must sum to 100%.");
        Assert.AreEqual((double)(totalCapitalWan * (result.RecommendedCashBufferPercent / 100m)),
            (double)result.RecommendedCashBufferWan, 0.5, "Cash buffer in Wan must match calculated percent.");

        // 验证回撤场景阶梯严格单调性 (随回撤增加，建议仓位单调下降直至 0)
        Assert.AreEqual(5, result.TierScenarios.Count, "Should contain 5 drawdown tiers.");
        for (int i = 1; i < result.TierScenarios.Count; i++)
        {
            var prev = result.TierScenarios[i - 1];
            var curr = result.TierScenarios[i];
            Assert.IsTrue(curr.SuggestedKellyEquityWeightPercent <= prev.SuggestedKellyEquityWeightPercent,
                $"Higher drawdown tier ({curr.DrawdownThresholdPercent}%) must have <= equity weight than ({prev.DrawdownThresholdPercent}%).");
            Assert.IsTrue(curr.DefensiveCashBufferPercent >= prev.DefensiveCashBufferPercent,
                "Higher drawdown tier must have >= defensive cash buffer.");
        }

        // 验证在触及 10% 止损硬顶时，仓位强制清零为 0%
        var maxTier = result.TierScenarios.Last();
        Assert.AreEqual(0.0, (double)maxTier.SuggestedKellyEquityWeightPercent, 0.01,
            "At 100% of drawdown ceiling, equity weight must be 0% (full defensive cash).");
    }

    [TestMethod]
    public void Test_BstsBayesianTrendFilter_AlphaAndBreakProbability()
    {
        var components = CreateSampleComponents();
        var baseDate = new DateTime(2023, 1, 1);
        var navs = GenerateSyntheticNav(baseDate, 365, 0.10, 0.16, seed: 888);
        var portDailyReturns = navs.Select(n => n.DailyReturn).ToList();

        var result = QuantCalculator.RunBstsBayesianTrendFilter(components, portDailyReturns);

        Assert.IsNotNull(result, "BSTS result must not be null.");
        Assert.AreEqual(4, result.AssetFilterItems.Count, "4 assets should be evaluated.");
        Assert.IsTrue(result.PortfolioStructuralBreakProbabilityPercent >= 0m && result.PortfolioStructuralBreakProbabilityPercent <= 100m,
            "Break probability must be between 0% and 100%.");
        Assert.IsTrue(result.TrendStabilityScore >= 0m && result.TrendStabilityScore <= 100m,
            "Trend stability score must be between 0 and 100.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.FilterSynthesisVerdict), "Filter synthesis verdict must be populated.");

        foreach (var item in result.AssetFilterItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.FundCode), "Fund code must be populated.");
            Assert.IsTrue(item.SignalToNoiseRatio >= 0m, "SNR must be >= 0.");
            Assert.IsTrue(item.BayesianStructuralBreakProbabilityPercent >= 0m && item.BayesianStructuralBreakProbabilityPercent <= 100m,
                "Break probability must be in [0, 100].");
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.RegimeDiagnosisBadge), "Diagnosis badge must be present.");
        }
    }

    [TestMethod]
    public void Test_MultiHorizonRiskTermStructure_MeanReversionVarianceDecay()
    {
        var baseDate = new DateTime(2023, 1, 1);
        // 生成具有均值回归粘性的收益序列
        var navs = GenerateSyntheticNav(baseDate, 500, 0.11, 0.17, seed: 999);
        var portDailyReturns = navs.Select(n => n.DailyReturn).ToList();

        var result = QuantCalculator.CalculateMultiHorizonRiskTermStructure(portDailyReturns);

        Assert.IsNotNull(result, "Multi horizon risk term result must not be null.");
        Assert.AreEqual(6, result.HorizonPoints.Count, "Must produce exactly 6 horizon points (1M to 5Y).");
        Assert.IsTrue(result.MeanReversionHalfLifeDays > 0m, "Mean reversion half life must be positive.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.HorizonAllocationGuidance), "Allocation guidance must be populated.");

        // 验证期限结构各点的属性与约束
        for (int i = 0; i < result.HorizonPoints.Count; i++)
        {
            var pt = result.HorizonPoints[i];
            Assert.IsTrue(pt.TradingDays > 0, "Trading days must be positive.");
            Assert.IsTrue(pt.HorizonAnnualizedVolPercent > 0m, "Annualized vol must be positive.");
            Assert.AreEqual(100.0, (double)(pt.OptimalEquityAllocationPercent + pt.OptimalFixedIncomeCashAllocationPercent), 0.1,
                "Equity + Cash must sum to 100% across all horizons.");

            // 跨期越长，均值回归效应使得长期投资者的权益承受力越高
            if (i > 0)
            {
                var prev = result.HorizonPoints[i - 1];
                Assert.IsTrue(pt.OptimalEquityAllocationPercent >= prev.OptimalEquityAllocationPercent,
                    $"Horizon {pt.HorizonLabel} should recommend >= equity than {prev.HorizonLabel}.");
            }
        }
    }

    [TestMethod]
    public void Test_PortfolioEngine_Phase28EndToEnd()
    {
        var components = CreateSampleComponents();
        var inputComponents = components.Select(c => (c.Fund, c.Weight)).ToList();

        var result = PortfolioEngine.CalculatePortfolio(inputComponents, riskFreeRate: 2.0m);

        Assert.IsNotNull(result, "PortfolioResult should not be null.");

        // 验证 Phase 28 挂载的 4 大顶层算法模块全部无缝运转
        Assert.IsNotNull(result.AssetCrowdingRadar, "AssetCrowdingRadar should be populated.");
        Assert.AreEqual(4, result.AssetCrowdingRadar.CrowdingItems.Count, "4 crowding items expected.");

        Assert.IsNotNull(result.DrawdownConstrainedKelly, "DrawdownConstrainedKelly should be populated.");
        Assert.AreEqual(5, result.DrawdownConstrainedKelly.TierScenarios.Count, "5 Kelly tier scenarios expected.");

        Assert.IsNotNull(result.BstsTrendFilter, "BstsTrendFilter should be populated.");
        Assert.AreEqual(4, result.BstsTrendFilter.AssetFilterItems.Count, "4 BSTS asset items expected.");

        Assert.IsNotNull(result.MultiHorizonRiskTerm, "MultiHorizonRiskTerm should be populated.");
        Assert.AreEqual(6, result.MultiHorizonRiskTerm.HorizonPoints.Count, "6 horizon points expected.");

        // 验证尽调研报 HTML 导出包含 Phase 28 的 4 大核心章节
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, inputComponents);
        Assert.IsTrue(html.Contains("资产多因子拥挤度雷达与流动性踩踏预警 (Phase 28)"),
            "HTML must contain Asset Crowding Radar section.");
        Assert.IsTrue(html.Contains("带最大回撤硬顶约束的分数凯利动态仓位配置 (Phase 28)"),
            "HTML must contain Drawdown Constrained Kelly section.");
        Assert.IsTrue(html.Contains("BSTS 贝叶斯结构时序滤波与趋势断点诊断 (Phase 28)"),
            "HTML must contain BSTS Trend Filter section.");
        Assert.IsTrue(html.Contains("多期跨期期限结构前沿与时间跨度风险衰减锥 (Phase 28)"),
            "HTML must contain Multi-Horizon Risk Term section.");
    }
}
