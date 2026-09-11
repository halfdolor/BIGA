using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase39Tests
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
            Name = "易方达中小盘混合 (高贝塔进攻)",
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
            Name = "华夏纯债中短债A (低波固收防御)",
            FundSize = "65.2亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.046, 0.029, seed: 333)
        };
        var fundD = new FundDetail
        {
            Code = "161715",
            Name = "招商中证白酒指数 (周期估值增强)",
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
    public void Test_Phase39_PrincipalFactorRiskParity()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.12, 0.16, 888);

        var result = QuantCalculator.CalculatePrincipalFactorRiskParity(components, returns);

        Assert.IsNotNull(result);
        Assert.AreEqual(components.Count, result.TotalPrincipalFactors, "Total principal factors should equal component count");
        Assert.IsTrue(result.EffectiveNumberOfPrincipalFactors >= 1.0m, "ENPF should be >= 1.0");
        Assert.IsTrue(result.EffectiveNumberOfPrincipalFactors <= result.TotalPrincipalFactors + 0.1m, "ENPF should not exceed total factors");
        Assert.IsTrue(result.FactorDiversificationRatio >= 1.0m, "Factor Diversification Ratio (FDR) should be >= 1.0");
        Assert.IsTrue(result.PostParityVarianceReductionPercent >= 0m, "Post parity variance reduction should be non-negative");
        Assert.IsTrue(result.FactorRiskInequalityGiniPercent >= 0m && result.FactorRiskInequalityGiniPercent <= 100m, "Factor Gini percent should be in [0, 100]");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict), "ExecutiveVerdict should not be empty");

        Assert.AreEqual(components.Count, result.PrincipalFactors.Count);
        decimal totalCumulativeVariance = result.PrincipalFactors.Last().CumulativeVariancePercent;
        Assert.IsTrue(Math.Abs(totalCumulativeVariance - 100m) < 1.0m, $"Cumulative variance of all factors should reach approx 100%, got {totalCumulativeVariance}");

        decimal totalOptimalWeight = result.PrincipalFactors.Sum(f => f.OptimalEigenmodeWeightPercent);
        Assert.IsTrue(Math.Abs(totalOptimalWeight - 100m) < 1.0m, $"Optimal eigenmode weights should sum to approx 100%, got {totalOptimalWeight}");

        decimal totalParityContribution = result.PrincipalFactors.Sum(f => f.ParityFactorRiskContributionPercent);
        Assert.IsTrue(Math.Abs(totalParityContribution - 100m) < 1.0m, $"Parity factor risk contributions should sum to approx 100%, got {totalParityContribution}");

        foreach (var pf in result.PrincipalFactors)
        {
            Assert.IsTrue(pf.FactorIndex >= 1);
            Assert.IsFalse(string.IsNullOrWhiteSpace(pf.FactorName));
            Assert.IsTrue(pf.Eigenvalue > 0m, "Eigenvalue should be positive");
            Assert.IsTrue(pf.VarianceExplainedPercent > 0m, "Variance explained should be positive");
            Assert.IsTrue(pf.RawFactorRiskContributionPercent >= 0m);
            Assert.IsTrue(pf.ParityFactorRiskContributionPercent > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(pf.DominantDriverRegime));
        }
    }

    [TestMethod]
    public void Test_Phase39_DynamicDownsideConvexityHedge()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.11, 0.15, 999);

        var result = QuantCalculator.CalculateDynamicDownsideConvexityHedge(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.BaselinePortfolioVolatilityPercent > 0m, "Baseline volatility should be positive");
        Assert.IsTrue(result.VolatilitySkewSlopeBps != 0m, "Volatility skew slope should not be zero");
        Assert.IsTrue(result.PortfolioConvexityDeficitScore >= 0m && result.PortfolioConvexityDeficitScore <= 100m, "Convexity deficit score should be in [0, 100]");
        Assert.IsTrue(result.OptimalTotalHedgeRatioPercent >= 0m && result.OptimalTotalHedgeRatioPercent <= 100m, "Optimal total hedge ratio should be in [0, 100]");
        Assert.IsTrue(result.MaxCushionBufferPercent > 0m, "Max cushion buffer should be positive");
        Assert.IsTrue(result.NetConvexityProtectionBenefitRatio > 0m, "Net convexity protection benefit ratio should be positive");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));

        Assert.AreEqual(3, result.StrikeHedgingProfiles.Count, "Should produce 3 OTM strike tiers (95%, 90%, 85%)");

        foreach (var sp in result.StrikeHedgingProfiles)
        {
            Assert.IsTrue(sp.MoneynessPercent > 0m && sp.MoneynessPercent < 100m, "Moneyness should be OTM (< 100%)");
            Assert.IsTrue(sp.StrikePrice > 0m, "Strike price should be positive");
            Assert.IsTrue(sp.ImpliedVolatilityPercent > 0m, "IV should be positive");
            Assert.IsTrue(sp.OptionDelta < 0m, "Put delta must be negative");
            Assert.IsTrue(sp.OptionGamma >= 0m, "Put gamma must be non-negative");
            Assert.IsTrue(sp.OptionVega >= 0m, "Put vega must be non-negative");
            Assert.IsTrue(sp.RecommendedHedgeRatioPercent >= 0m, "Hedge ratio should be non-negative");
            Assert.IsTrue(sp.StressDownsideBufferRatio > 0m, "Downside buffer ratio should be positive");
            Assert.IsFalse(string.IsNullOrWhiteSpace(sp.ProtectionTierBadge));
        }
    }

    [TestMethod]
    public void Test_Phase39_BayesianKalmanAlphaTracking()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.13, 0.18, 123);

        var result = QuantCalculator.CalculateBayesianKalmanAlphaTracking(components, returns);

        Assert.IsNotNull(result);
        Assert.AreEqual(components.Count, result.AssetKalmanAlphas.Count, "Should track Kalman Alpha for each component");
        Assert.IsTrue(result.TrackingNoiseReductionPercent > 0m, "Tracking noise reduction should be positive");
        Assert.IsTrue(result.InformationRatioUpliftPercent >= 0m, "IR uplift should be non-negative");
        Assert.IsTrue(result.StructuralBreakAssetCount >= 0 && result.StructuralBreakAssetCount <= components.Count, "Structural break asset count should be bounded");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));

        foreach (var ka in result.AssetKalmanAlphas)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(ka.Code));
            Assert.IsFalse(string.IsNullOrWhiteSpace(ka.Name));
            Assert.IsTrue(ka.PriorInnovationVariance > 0m, "Innovation variance should be positive");
            Assert.IsTrue(ka.KalmanOptimalGain >= 0m && ka.KalmanOptimalGain <= 1.0m, "Kalman optimal gain should be in [0, 1]");
            Assert.IsTrue(ka.AlphaDecayHalfLifeDays > 0m, "Alpha half-life should be positive");
            Assert.IsTrue(ka.RegimeShiftConfidencePercent >= 0m && ka.RegimeShiftConfidencePercent <= 100m, "Regime shift confidence should be in [0, 100]");
            Assert.IsFalse(string.IsNullOrWhiteSpace(ka.AlphaTrajectoryBadge));
        }
    }

    [TestMethod]
    public void Test_Phase39_CrossAssetLiquidityChasmAndDamper()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.10, 0.14, 456);

        var result = QuantCalculator.CalculateCrossAssetLiquidityChasmAndDamper(components, returns);

        Assert.IsNotNull(result);
        Assert.AreEqual(components.Count, result.AssetLiquidityChasms.Count, "Should calculate liquidity chasm for all components");
        Assert.IsTrue(result.LiquidityChasmIndex >= 0m && result.LiquidityChasmIndex <= 100m, "LCI should be in [0, 100]");
        Assert.IsTrue(result.FlashCrashCascadeAmplifierRatio >= 1.0m, "Flash crash cascade amplifier ratio should be >= 1.0x");
        Assert.IsTrue(result.SystemicLiquidityDampingScorePercent >= 0m && result.SystemicLiquidityDampingScorePercent <= 100m, "Damping score should be in [0, 100]");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.MarketMakerPullbackAlertLevel));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));

        decimal totalInjectionWeight = result.AssetLiquidityChasms.Sum(lc => lc.CascadeFlashCrashInjectionWeightPercent);
        Assert.IsTrue(Math.Abs(totalInjectionWeight - 100m) < 1.0m, $"Cascade injection weights should sum to approx 100%, got {totalInjectionWeight}");

        foreach (var lc in result.AssetLiquidityChasms)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(lc.Code));
            Assert.IsFalse(string.IsNullOrWhiteSpace(lc.Name));
            Assert.IsTrue(lc.AmihudIlliquidityMeasure > 0m, "Amihud illiquidity should be positive");
            Assert.IsTrue(lc.CrossAssetLiquidityBeta > 0m, "Cross asset liquidity beta should be positive");
            Assert.IsTrue(lc.MarketMakerPullbackSensitivityScore >= 0m && lc.MarketMakerPullbackSensitivityScore <= 100m);
            Assert.IsTrue(lc.CascadeFlashCrashInjectionWeightPercent >= 0m);
            Assert.IsTrue(lc.DamperAbsorptionCapacityPercent >= 0m && lc.DamperAbsorptionCapacityPercent <= 100m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(lc.ExecutionThrottlingGateStatus));
        }
    }

    [TestMethod]
    public void Test_Phase39_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var engineResult = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(engineResult);
        Assert.IsNotNull(engineResult.PrincipalFactorRiskParity, "PrincipalFactorRiskParity should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.DynamicDownsideConvexityHedge, "DynamicDownsideConvexityHedge should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.BayesianKalmanAlphaTracker, "BayesianKalmanAlphaTracker should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.CrossAssetLiquidityChasmDamper, "CrossAssetLiquidityChasmDamper should be populated in PortfolioEngine");

        // Verify institutional due diligence HTML report generation
        string htmlReport = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(engineResult, components);
        Assert.IsFalse(string.IsNullOrWhiteSpace(htmlReport));
        Assert.IsTrue(htmlReport.Contains("五十八、Bridgewater Associates & AQR Capital 主因子正交风险平价与特征风险预算配置引擎 (Phase 39)"), "Report should contain Phase 39.1 section");
        Assert.IsTrue(htmlReport.Contains("五十九、Millennium Management & Point72 动态下行凸性期权对冲与广义波动率偏度复制引擎 (Phase 39)"), "Report should contain Phase 39.2 section");
        Assert.IsTrue(htmlReport.Contains("六十、Renaissance Technologies & D.E. Shaw 贝叶斯时变状态空间卡尔曼滤波与自适应 Alpha 动态跟踪 (Phase 39)"), "Report should contain Phase 39.3 section");
        Assert.IsTrue(htmlReport.Contains("六十一、Jane Street & Jump Trading 跨资产微观流动性共振裂谷与闪崩级联阻尼器 (Phase 39)"), "Report should contain Phase 39.4 section");
    }
}
