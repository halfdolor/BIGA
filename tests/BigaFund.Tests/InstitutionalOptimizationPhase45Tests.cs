using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase45Tests
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
            double ret = dailyMean + dailyStd * z;
            list.Add((decimal)ret);
        }

        return list;
    }

    [TestMethod]
    public void Test_Phase45_FractionalRoughVolatility()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateFractionalRoughVolatility(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(components.Count, res.AssetRoughVolatilities.Count);
        Assert.IsTrue(res.PortfolioAverageHurstParameterH > 0m && res.PortfolioAverageHurstParameterH < 0.5m, "Hurst parameter H should be in (0, 0.5) indicating rough path");
        Assert.IsTrue(res.CompositeVolBurstRiskProbabilityPercent >= 0m && res.CompositeVolBurstRiskProbabilityPercent <= 100m, "Vol burst probability should be in [0, 100]");
        Assert.IsTrue(res.FractionalVsMarkovianVolDispersionPercent > 0m, "Rough dispersion should be positive");
        Assert.IsTrue(res.NetRoughOptionHedgingCostSavingsBps > 0m, "Option hedging cost savings should be positive");

        foreach (var asset in res.AssetRoughVolatilities)
        {
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetName));
            Assert.IsTrue(asset.HurstParameterH > 0m && asset.HurstParameterH < 0.5m, "Asset Hurst parameter should be in rough regime (0, 0.5)");
            Assert.IsTrue(asset.VolOfVolNu > 0m, "Vol-of-Vol nu should be positive");
            Assert.IsTrue(asset.HistoricalRealizedVolPercent > 0m, "Historical realized vol should be positive");
            Assert.IsTrue(asset.FractionalPredictedVolPercent > 0m, "Fractional predicted vol should be positive");
            Assert.IsTrue(asset.RoughConvexityPremiumBps > 0m, "Rough convexity premium should be positive");
            Assert.IsFalse(string.IsNullOrEmpty(asset.VolRegimeRoughnessState), "Roughness state description should be populated");
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("粗糙波动率") || res.ExecutiveVerdict.Contains("Rough"), "Verdict should mention rough volatility");
    }

    [TestMethod]
    public void Test_Phase45_NelsonSiegelSvenssonTermStructure()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateNelsonSiegelSvenssonTermStructure(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.IsTrue(res.Beta0Level > 0m, "Beta0 level should be positive");
        Assert.IsTrue(res.PortfolioEffectiveDurationYears > 0m, "Portfolio effective duration should be positive");
        Assert.IsTrue(res.ButterflyArbitrageExpectedAlphaBps > 0m, "Butterfly arbitrage alpha should be positive");
        Assert.AreEqual(5, res.KeyRateDurations.Count, "Should calculate 5 key rate duration tenors (1Y, 3Y, 5Y, 10Y, 30Y)");

        decimal totalRiskContribution = res.KeyRateDurations.Sum(k => k.KeyRateRiskContributionPercent);
        Assert.IsTrue(totalRiskContribution >= 95m && totalRiskContribution <= 105m, $"KRD risk contributions should sum to ~100%, got {totalRiskContribution}%");

        foreach (var krd in res.KeyRateDurations)
        {
            Assert.IsFalse(string.IsNullOrEmpty(krd.TenorLabel));
            Assert.IsTrue(krd.TenorYears > 0m);
            Assert.IsTrue(krd.FittedNssYieldPercent > 0m);
            Assert.IsTrue(krd.KeyRateDurationYears >= 0m);
            Assert.IsFalse(string.IsNullOrEmpty(krd.ArbitrageButterflyLeg));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Nelson-Siegel-Svensson") || res.ExecutiveVerdict.Contains("NSS"), "Verdict should mention NSS term structure");
    }

    [TestMethod]
    public void Test_Phase45_BayesianOnlineChangepointDetection()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateBayesianOnlineChangepointDetection(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.IsTrue(res.CurrentRegimeRunLengthDays > 0m, "Current run-length should be positive");
        Assert.IsTrue(res.LatestChangepointProbabilityPercent >= 0m && res.LatestChangepointProbabilityPercent <= 100m, "Changepoint probability in [0, 100]");
        Assert.IsTrue(res.SystemicHazardRatePercent > 0m, "Systemic hazard rate should be positive");
        Assert.IsTrue(res.PreemptiveDeriskingAlphaSavingsBps > 0m, "Preemptive derisking alpha savings should be positive");
        Assert.IsTrue(res.RecentHazardHistory.Count > 0, "Should contain hazard history steps");

        foreach (var step in res.RecentHazardHistory)
        {
            Assert.IsTrue(step.TimeStepIndex > 0);
            Assert.IsTrue(step.MaximumAcyclicRunLength > 0m);
            Assert.IsTrue(step.InstantaneousHazardRatePercent >= 0m);
            Assert.IsFalse(string.IsNullOrEmpty(step.MacroRegimePhaseBadge));
            Assert.IsFalse(string.IsNullOrEmpty(step.RecommendedAssetAction));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("变点") || res.ExecutiveVerdict.Contains("BOCPD"), "Verdict should mention BOCPD");
    }

    [TestMethod]
    public void Test_Phase45_AvellanedaStoikovMicrostructure()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateAvellanedaStoikovMicrostructure(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.IsTrue(res.AverageOptimalQuotingSpreadBps > 0m, "Average quoting spread should be positive");
        Assert.IsTrue(res.InventoryRiskMitigationRatePercent > 0m, "Inventory mitigation rate should be positive");
        Assert.IsTrue(res.ExpectedMicroExecutionSavingsBps > 0m, "Expected execution savings should be positive");
        Assert.AreEqual(components.Count, res.AssetQuotingProfiles.Count);

        foreach (var profile in res.AssetQuotingProfiles)
        {
            Assert.IsFalse(string.IsNullOrEmpty(profile.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(profile.AssetName));
            Assert.IsTrue(profile.MidPriceQuote > 0m);
            Assert.IsTrue(profile.ReservationPriceIndifference > 0m);
            Assert.IsTrue(profile.OptimalBidSpreadBps > 0m);
            Assert.IsTrue(profile.OptimalAskSpreadBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(profile.MicrostructureQuotingAction));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Avellaneda-Stoikov") || res.ExecutiveVerdict.Contains("保留价"), "Verdict should mention Avellaneda-Stoikov");
    }

    [TestMethod]
    public void Test_Phase45_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.FractionalRoughVolatility, "FractionalRoughVolatility should be populated");
        Assert.IsNotNull(result.NelsonSiegelSvenssonTermStructure, "NelsonSiegelSvenssonTermStructure should be populated");
        Assert.IsNotNull(result.BayesianOnlineChangepointDetection, "BayesianOnlineChangepointDetection should be populated");
        Assert.IsNotNull(result.AvellanedaStoikovMicrostructure, "AvellanedaStoikovMicrostructure should be populated");

        // Verify HTML Export includes Phase 45 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("八十二、Renaissance Technologies & D.E. Shaw 连续时间分数阶粗糙波动率 (Rough Heston)"), "HTML should contain Section 82");
        Assert.IsTrue(html.Contains("八十三、Citadel Global Fixed Income & Millennium RV 六参数 Nelson-Siegel-Svensson (NSS)"), "HTML should contain Section 83");
        Assert.IsTrue(html.Contains("八十四、Two Sigma & Man Group AHL 贝叶斯在线变点检测 (BOCPD)"), "HTML should contain Section 84");
        Assert.IsTrue(html.Contains("八十五、Jane Street & Citadel Securities Avellaneda-Stoikov 连续库存风险最优保留价"), "HTML should contain Section 85");
        Assert.IsTrue(html.Contains("Rough Volatility"), "HTML should contain Section 82 table title");
        Assert.IsTrue(html.Contains("Nelson-Siegel-Svensson"), "HTML should contain Section 83 table title");
        Assert.IsTrue(html.Contains("BOCPD Hazard History"), "HTML should contain Section 84 table title");
        Assert.IsTrue(html.Contains("Avellaneda-Stoikov"), "HTML should contain Section 85 table title");
    }
}
