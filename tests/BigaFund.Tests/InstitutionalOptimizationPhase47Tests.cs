using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase47Tests
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
    public void Test_Phase47_MalliavinCalculusSensitivity()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateMalliavinCalculusSensitivity(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(components.Count, res.AssetSensitivities.Count);
        Assert.IsTrue(res.PortfolioWeightedMalliavinDelta > 0m, "Portfolio weighted Malliavin Delta should be positive");
        Assert.IsTrue(res.PortfolioWeightedMalliavinGamma > 0m, "Portfolio weighted Malliavin Gamma should be positive");
        Assert.IsTrue(res.PortfolioAggregatedVegaBps > 0m, "Portfolio aggregated Vega should be positive");
        Assert.IsTrue(res.FiniteDifferenceSpeedupRatio > 1.0m, "Finite difference speedup ratio should be > 1.0");

        foreach (var asset in res.AssetSensitivities)
        {
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetName));
            Assert.IsTrue(asset.Weight > 0m);
            Assert.IsTrue(asset.MalliavinDelta > 0m, "Malliavin Delta should be positive");
            Assert.IsTrue(asset.MalliavinGamma > 0m, "Malliavin Gamma should be positive");
            Assert.IsTrue(asset.MalliavinVega > 0m, "Malliavin Vega should be positive");
            Assert.IsTrue(asset.CrossAssetVanna > 0m, "Cross asset Vanna should be positive");
            Assert.IsTrue(asset.NumericalRobustnessScore >= 0m && asset.NumericalRobustnessScore <= 100m, "Robustness score should be in [0, 100]");
            Assert.IsFalse(string.IsNullOrEmpty(asset.SensitivityRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(asset.DeltaHedgeAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("马利亚温") || res.ExecutiveVerdict.Contains("Malliavin"), "Verdict should mention Malliavin");
    }

    [TestMethod]
    public void Test_Phase47_SemidefiniteRelaxationCardinality()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateSemidefiniteRelaxationCardinality(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(components.Count, res.SparseWeightItems.Count);
        Assert.IsTrue(res.CardinalityLimitK >= 1, "Cardinality limit K should be >= 1");
        Assert.IsTrue(res.ActualSparseAssetsSelected <= res.CardinalityLimitK, "Actual selected should not exceed limit K");
        Assert.IsTrue(res.SdrSparseTrackingErrorPercent > 0m, "SDR tracking error should be positive");
        Assert.IsTrue(res.SdrConicRelaxationGapRatio >= 0m, "Duality gap should be non-negative");
        Assert.IsTrue(res.FrictionCostReductionBps > 0m, "Friction cost reduction should be positive");

        int selectedCount = 0;
        decimal totalSparseWeight = 0m;

        foreach (var asset in res.SparseWeightItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetName));
            Assert.IsTrue(asset.OriginalUnconstrainedWeight > 0m);
            Assert.IsTrue(asset.MarginalTrackingVarianceContribution > 0m);
            Assert.IsTrue(asset.TransactionFeeSavingBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(asset.SelectionRoleBadge));

            if (asset.IsSelectedInCardinality)
            {
                selectedCount++;
                Assert.IsTrue(asset.SdrOptimizedSparseWeight > 0m);
                totalSparseWeight += asset.SdrOptimizedSparseWeight;
            }
            else
            {
                Assert.AreEqual(0m, asset.SdrOptimizedSparseWeight);
            }
        }

        Assert.AreEqual(res.ActualSparseAssetsSelected, selectedCount);
        Assert.AreEqual(100.0m, Math.Round(totalSparseWeight, 1), "Sum of sparse weights should be 100%");
        Assert.IsTrue(res.ExecutiveVerdict.Contains("半定松弛") || res.ExecutiveVerdict.Contains("SDR"), "Verdict should mention SDR");
    }

    [TestMethod]
    public void Test_Phase47_MeanFieldGameExecution()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateMeanFieldGameExecution(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(components.Count, res.AssetNashStrategies.Count);
        Assert.IsTrue(res.SystemicCrowdingIndex > 0m && res.SystemicCrowdingIndex <= 100m, "Systemic crowding index should be in (0, 100]");
        Assert.IsTrue(res.TotalMfgEquilibriumSavingsBps > 0m, "Total MFG equilibrium savings should be positive");
        Assert.IsTrue(res.CoordinationConvergencePeriodHours > 0m, "Coordination convergence period should be positive");
        Assert.IsTrue(res.CrowdedAssetsCount >= 0, "Crowded assets count should be non-negative");

        foreach (var asset in res.AssetNashStrategies)
        {
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetName));
            Assert.IsTrue(asset.MarketCrowdingDensityIndex >= 0m && asset.MarketCrowdingDensityIndex <= 100m);
            Assert.IsTrue(asset.MfgEquilibriumExecutionRate > 0m);
            Assert.IsTrue(asset.AntiFrontRunningDefensiveRatio > 0m && asset.AntiFrontRunningDefensiveRatio <= 1.0m);
            Assert.IsTrue(asset.EquilibriumSlippageSavingsBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(asset.CrowdingRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(asset.StrategicExecutionGuidance));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("平均场博弈") || res.ExecutiveVerdict.Contains("MFG"), "Verdict should mention MFG");
    }

    [TestMethod]
    public void Test_Phase47_KyleBackStealthExecution()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateKyleBackStealthExecution(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(components.Count, res.AssetKyleBackExecutions.Count);
        Assert.IsTrue(res.PortfolioAverageKyleLambda > 0m, "Average Kyle Lambda should be positive");
        Assert.IsTrue(res.CompositeStealthCamouflageScore > 0m && res.CompositeStealthCamouflageScore <= 100m, "Stealth score should be in (0, 100]");
        Assert.IsTrue(res.TotalPreservedAlphaBps > 0m, "Total preserved alpha should be positive");
        Assert.IsTrue(res.AverageAlphaSignalHalfLifeDays > 0m, "Signal half-life should be positive");

        foreach (var asset in res.AssetKyleBackExecutions)
        {
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetName));
            Assert.IsTrue(asset.KyleLambdaImpactFactor > 0m);
            Assert.IsTrue(asset.OptimalStealthTradingIntensity > 0m);
            Assert.IsTrue(asset.InformationLeakageDecayRate > 0m);
            Assert.IsTrue(asset.PrivateAlphaHalfLifeDays > 0m);
            Assert.IsTrue(asset.StealthAlphaPreservationBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(asset.StealthCamouflageRating));
            Assert.IsFalse(string.IsNullOrEmpty(asset.MicroExecutionTactic));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Kyle") || res.ExecutiveVerdict.Contains("隐匿执行"), "Verdict should mention Kyle or stealth execution");
    }

    [TestMethod]
    public void Test_Phase47_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.MalliavinCalculusSensitivity, "MalliavinCalculusSensitivity should be populated");
        Assert.IsNotNull(result.SemidefiniteRelaxationCardinality, "SemidefiniteRelaxationCardinality should be populated");
        Assert.IsNotNull(result.MeanFieldGameExecution, "MeanFieldGameExecution should be populated");
        Assert.IsNotNull(result.KyleBackStealthExecution, "KyleBackStealthExecution should be populated");

        // Verify HTML Export includes Phase 47 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("九十、Renaissance Technologies & D.E. Shaw 马利亚温随机变分分析 (Malliavin Calculus)"), "HTML should contain Section 90");
        Assert.IsTrue(html.Contains("九十一、Citadel Global Strategies & Millennium Management 半定松弛 (SDR) 锥优化与基数约束稀疏指数跟踪"), "HTML should contain Section 91");
        Assert.IsTrue(html.Contains("九十二、Two Sigma & Bridgewater Associates 连续时间平均场博弈 (Mean Field Games, MFG) 与机构间策略纳什均衡执行"), "HTML should contain Section 92");
        Assert.IsTrue(html.Contains("九十三、Jane Street Capital & Jump Trading Kyle-Back 连续拍卖动态知情交易与隐匿执行模型"), "HTML should contain Section 93");
        Assert.IsTrue(html.Contains("Malliavin Delta"), "HTML should contain Section 90 table title");
        Assert.IsTrue(html.Contains("SDR 稀疏权重"), "HTML should contain Section 91 table title");
        Assert.IsTrue(html.Contains("MFG 均衡执行速率"), "HTML should contain Section 92 table title");
        Assert.IsTrue(html.Contains("Kyle Lambda"), "HTML should contain Section 93 table title");
    }
}
