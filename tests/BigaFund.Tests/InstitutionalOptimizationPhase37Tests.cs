using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase37Tests
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
        var fundA = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘混合 (顺周期高成长)",
            FundSize = "180.5亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.22, 0.25, seed: 111)
        };
        var fundB = new FundDetail
        {
            Code = "510300",
            Name = "华泰柏瑞沪深300ETF (核心宽基蓝筹)",
            FundSize = "850.0亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.09, 0.17, seed: 222)
        };
        var fundC = new FundDetail
        {
            Code = "000001",
            Name = "华夏纯债中短债A (纯债久期防御)",
            FundSize = "65.2亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.046, 0.029, seed: 333)
        };
        var fundD = new FundDetail
        {
            Code = "161715",
            Name = "招商中证白酒指数 (消费估值大宗)",
            FundSize = "320.0亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.14, 0.26, seed: 444)
        };

        return new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 35m),
            (fundB, 30m),
            (fundC, 20m),
            (fundD, 15m)
        };
    }

    private static List<decimal> CreateSampleDailyReturns(int days = 250, double annRet = 0.11, double annVol = 0.17, int seed = 777)
    {
        var rng = new Random(seed);
        var list = new List<decimal>(days);
        double dailyMean = annRet / 252.0;
        double dailyStd = annVol / Math.Sqrt(252.0);

        for (int i = 0; i < days; i++)
        {
            double u1 = Math.Max(1e-8, rng.NextDouble());
            double u2 = Math.Max(1e-8, rng.NextDouble());
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            double ret = (dailyMean + dailyStd * z) * 100.0;
            list.Add(Math.Round((decimal)ret, 4));
        }
        return list;
    }

    [TestMethod]
    public void Test_Phase37_MacroSurpriseAndNeutralizingOverlay()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.12, 0.16, 888);

        var result = QuantCalculator.CalculateMacroSurpriseAndNeutralizingOverlay(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.PortfolioGrowthBeta != 0m, "PortfolioGrowthBeta should not be zero");
        Assert.IsTrue(result.PortfolioInflationBeta != 0m, "PortfolioInflationBeta should not be zero");
        Assert.IsTrue(result.MacroUnhedgedVolatilityPercent > 0m);
        Assert.IsTrue(result.MacroHedgedVolatilityPercent > 0m);
        Assert.IsTrue(result.MacroHedgedVolatilityPercent < result.MacroUnhedgedVolatilityPercent, "Hedged vol should be lower than unhedged vol");
        Assert.IsTrue(result.MacroVolatilityReductionPercent >= 40.0m, "Macro Volatility Reduction should be significant");
        Assert.IsTrue(result.MaxDrawdownMitigationPercent > 0m);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.MacroRegimeResilienceBadge));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));

        Assert.AreEqual(components.Count, result.AssetExposures.Count);
        Assert.AreEqual(4, result.ScenarioShocks.Count, "Should contain 4 Bridgewater regime quadrants");

        foreach (var sc in result.ScenarioShocks)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(sc.QuadrantName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(sc.EconomicEnvironment));
            Assert.IsFalse(string.IsNullOrWhiteSpace(sc.RiskAlertLevel));
        }
    }

    [TestMethod]
    public void Test_Phase37_StyleFactorNeutralization()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.11, 0.15, 999);

        var result = QuantCalculator.CalculateStyleFactorNeutralization(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.BreachedFactorCount >= 0);
        Assert.IsTrue(result.UnintendedStyleRiskEliminationRatioPercent >= 80.0m, "Elimination ratio must be >= 80%");
        Assert.IsTrue(result.TotalPreHedgeStyleRiskContributionPercent > result.TotalPostHedgeStyleRiskContributionPercent);
        Assert.IsTrue(result.PureAlphaVarianceRatioPercent > 70.0m);
        Assert.IsTrue(result.InformationRatioUplift > 0m);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.GovernanceVerdict));

        Assert.AreEqual(5, result.FactorExposures.Count, "Should assess 5 core style factors: Size, Value, Momentum, LowVol, Liquidity");
        foreach (var f in result.FactorExposures)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(f.FactorName));
            Assert.IsTrue(Math.Abs(f.PostHedgeActiveExposureZScore) <= Math.Abs(f.ActiveFactorExposureZScore));
        }

        Assert.AreEqual(components.Count, result.Adjustments.Count);
        decimal sumAdjustment = result.Adjustments.Sum(a => a.WeightAdjustmentPercent);
        Assert.IsTrue(Math.Abs(sumAdjustment) <= 0.05m, $"Sum of adjustments should be zero-sum net neutral, got {sumAdjustment}");
    }

    [TestMethod]
    public void Test_Phase37_VolatilityTargetedTsmom()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.13, 0.18, 123);

        var result = QuantCalculator.CalculateVolatilityTargetedTsmom(components, returns, targetVolatilityPercent: 12.0m);

        Assert.IsNotNull(result);
        Assert.AreEqual(12.0m, result.TargetVolatilityPercent);
        Assert.IsTrue(result.PortfolioRealizedTsmomVolatilityPercent > 0m);
        Assert.IsTrue(result.LongExposurePercent >= 0m);
        Assert.IsTrue(result.ShortExposurePercent >= 0m);
        Assert.IsTrue(result.CrisisAlphaPotentialScore >= 50.0m);
        Assert.IsTrue(result.MomentumCrashDefenseIndex >= 70.0m);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.StrategyRecommendation));

        Assert.AreEqual(components.Count, result.AssetSignals.Count);
        foreach (var s in result.AssetSignals)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(s.Code));
            Assert.IsFalse(string.IsNullOrWhiteSpace(s.Name));
            Assert.IsTrue(s.VolatilityScalingFactor > 0m);
            Assert.IsTrue(s.TrendHalfLifeDays > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(s.CrisisAlphaBadge));
        }
    }

    [TestMethod]
    public void Test_Phase37_OptimalExecutionTrajectory()
    {
        var components = CreateSampleComponents();
        decimal rebalanceAmount = 8000m;

        var result = QuantCalculator.CalculateOptimalExecutionTrajectory(components, totalRebalanceAmountWan: rebalanceAmount, executionHorizonDays: 1.0m);

        Assert.IsNotNull(result);
        Assert.AreEqual(rebalanceAmount, result.TotalRebalanceAmountWan);
        Assert.IsTrue(result.CharacteristicDecayParameterKappa > 0m);
        Assert.IsTrue(result.CharacteristicDecayHalfLifeHours > 0m);
        Assert.IsTrue(result.AlmgrenChrissExpectedShortfallWan > 0m);
        Assert.IsTrue(result.TwapExpectedShortfallWan > 0m);
        Assert.IsTrue(result.ExecutionSlippageSavingsWan > 0m);
        Assert.IsTrue(result.ExecutionSlippageSavingsRatioPercent > 0m);
        Assert.IsTrue(result.ExecutionShortfallVaR95Wan > result.AlmgrenChrissExpectedShortfallWan);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutionVerdict));

        Assert.AreEqual(10, result.TrajectorySlices.Count, "Should generate 10 execution time slices");
        Assert.IsTrue(result.TrajectorySlices.First().RemainingHoldingsPercent > 50.0m);
        Assert.AreEqual(0.0m, result.TrajectorySlices.Last().RemainingHoldingsPercent, "Last slice remaining holding should be 0%");
    }

    [TestMethod]
    public void Test_Phase37_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var engineResult = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(engineResult);
        Assert.IsNotNull(engineResult.MacroSurpriseOverlay, "MacroSurpriseOverlay should be computed in PortfolioEngine");
        Assert.IsNotNull(engineResult.StyleFactorNeutralization, "StyleFactorNeutralization should be computed in PortfolioEngine");
        Assert.IsNotNull(engineResult.VolatilityTargetedTsmom, "VolatilityTargetedTsmom should be computed in PortfolioEngine");
        Assert.IsNotNull(engineResult.OptimalExecutionTrajectory, "OptimalExecutionTrajectory should be computed in PortfolioEngine");

        // Verify institutional due diligence HTML report generation
        string htmlReport = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(engineResult, components);
        Assert.IsFalse(string.IsNullOrWhiteSpace(htmlReport));
        Assert.IsTrue(htmlReport.Contains("五十、Bridgewater Associates 宏观增长-通胀双维度惊喜指数矩阵与全天候宏观贝塔正交中性化覆盖"), "Report should contain Phase 37.1 section");
        Assert.IsTrue(htmlReport.Contains("五十一、Citadel & Millennium 多经理平台核心风格因子正交中性化与非故意风险漂移剔除"), "Report should contain Phase 37.2 section");
        Assert.IsTrue(htmlReport.Contains("五十二、AQR Capital & Antti Ilmanen 动态目标波动率定标时间序列动量 (TSMOM) 与危机 Alpha 防御"), "Report should contain Phase 37.3 section");
        Assert.IsTrue(htmlReport.Contains("五十三、Two Sigma & D.E. Shaw Almgren-Chriss 连续动态最优拆单轨迹与流动性在险度量"), "Report should contain Phase 37.4 section");
    }
}
