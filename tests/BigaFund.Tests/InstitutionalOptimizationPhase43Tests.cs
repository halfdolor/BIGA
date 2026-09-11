using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase43Tests
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
    public void Test_Phase43_MrmrFeatureSelectionAndOrthogonalEnsemble()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateMrmrFeatureSelectionAndOrthogonalEnsemble(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(5, res.SelectedOptimalFeatureCount);
        Assert.AreEqual(8, res.FeatureRankings.Count);
        Assert.IsTrue(res.TopFeatureEnsembleIcGainRatio > 0m, "IC gain ratio should be positive");
        Assert.IsTrue(res.CollinearityReductionRatePercent > 50m, "Collinearity reduction rate should be > 50%");
        Assert.IsTrue(res.AverageFeatureMutualInformation > 0m, "Average mutual information should be positive");

        // First ranking feature should have rank 1 and highest score
        Assert.AreEqual(1, res.FeatureRankings[0].MrmrSelectionRank);
        Assert.IsTrue(res.FeatureRankings[0].MrmrOptimizationScore >= res.FeatureRankings[1].MrmrOptimizationScore);
        Assert.IsTrue(res.FeatureRankings.Sum(f => f.SubspaceOrthogonalWeightPercent) > 99m, "Weights sum to ~100%");
        Assert.IsTrue(res.ExecutiveVerdict.Contains("mRMR"));
    }

    [TestMethod]
    public void Test_Phase43_WassersteinPodCapitalCurvatureAndConvexRebalancing()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateWassersteinPodCapitalCurvatureAndConvexRebalancing(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(5, res.PodCurvatureAllocations.Count);
        Assert.IsTrue(res.TotalWassersteinDistanceMetric > 0m, "Wasserstein metric should be positive");
        Assert.IsTrue(res.TotalOptimalTransportTurnoverPercent > 0m, "Turnover percent should be positive");
        Assert.IsTrue(res.CurvatureStabilityImprovementPercent > 0m, "Stability improvement should be positive");
        Assert.IsTrue(res.NetTransportFrictionSavingsBps > 0m, "Friction savings should be positive");

        decimal totalTargetWeight = res.PodCurvatureAllocations.Sum(p => p.OptimalTransportTargetWeightPercent);
        Assert.IsTrue(totalTargetWeight >= 99m && totalTargetWeight <= 101m, $"Target weights sum to {totalTargetWeight}%");
        Assert.IsTrue(res.ExecutiveVerdict.Contains("Wasserstein"));
    }

    [TestMethod]
    public void Test_Phase43_HawkesMicrostructureJumpAndAvalanche()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateHawkesMicrostructureJumpAndAvalanche(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(components.Count, res.AssetHawkesJumps.Count);
        Assert.IsTrue(res.PortfolioAverageBranchingRatio > 0m && res.PortfolioAverageBranchingRatio < 1.0m, "Branching ratio should be between 0 and 1");
        Assert.IsTrue(res.CompositeAvalancheRiskIndex > 0m && res.CompositeAvalancheRiskIndex <= 100m, "Avalanche risk should be in [0, 100]");
        Assert.IsTrue(res.ExpectedPreemptiveCostSavingBps > 0m, "Expected savings should be positive");

        foreach (var asset in res.AssetHawkesJumps)
        {
            Assert.IsFalse(string.IsNullOrEmpty(asset.MicrostructureDefenseSignal));
            Assert.IsTrue(asset.BranchingRatioEta > 0m && asset.BranchingRatioEta <= 1.0m);
            Assert.IsTrue(asset.JumpDiffusionIntensityLambda > 0m);
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Hawkes"));
    }

    [TestMethod]
    public void Test_Phase43_HigherOrderMomentTensorRiskParity()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateHigherOrderMomentTensorRiskParity(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(components.Count, res.HigherMomentAssets.Count);
        Assert.IsTrue(res.HigherOrderRiskDispersionIndex > 0m, "Risk dispersion index should be positive");
        Assert.IsTrue(res.TailConvexityHedgingUpliftPercent > 0m, "Convexity uplift should be positive");

        decimal sumTensorWeight = res.HigherMomentAssets.Sum(a => a.TensorHigherOrderParityWeightPercent);
        Assert.IsTrue(sumTensorWeight >= 99m && sumTensorWeight <= 101m, $"Tensor weights sum to {sumTensorWeight}%");

        decimal sumSecondWeight = res.HigherMomentAssets.Sum(a => a.SecondOrderParityWeightPercent);
        Assert.IsTrue(sumSecondWeight >= 99m && sumSecondWeight <= 101m, $"Second order weights sum to {sumSecondWeight}%");

        Assert.IsTrue(res.ExecutiveVerdict.Contains("高阶矩张量"));
    }

    [TestMethod]
    public void Test_Phase43_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.MrmrFeatureEnsemble, "MrmrFeatureEnsemble should be populated");
        Assert.IsNotNull(result.WassersteinPodCurvature, "WassersteinPodCurvature should be populated");
        Assert.IsNotNull(result.HawkesMicrostructureAvalanche, "HawkesMicrostructureAvalanche should be populated");
        Assert.IsNotNull(result.HigherOrderTensorRiskParity, "HigherOrderTensorRiskParity should be populated");

        // Verify HTML Export includes Phase 43 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("七十四、Renaissance Technologies & Two Sigma 最大相关最小冗余 (mRMR)"), "HTML should contain Section 74");
        Assert.IsTrue(html.Contains("七十五、Citadel & Millennium 基于 Wasserstein 最优输运的多策略 Pod 动态资本曲率重构"), "HTML should contain Section 75");
        Assert.IsTrue(html.Contains("七十六、Jane Street & Citadel Securities 微观限价订单簿自激 Hawkes 点过程"), "HTML should contain Section 76");
        Assert.IsTrue(html.Contains("七十七、Bridgewater Associates & AQR Capital 高阶矩张量风险平价"), "HTML should contain Section 77");
        Assert.IsTrue(html.Contains("mRMR Feature Space"), "HTML should contain Section 74 table title");
        Assert.IsTrue(html.Contains("Wasserstein Pod Transport"), "HTML should contain Section 75 table title");
        Assert.IsTrue(html.Contains("Hawkes Point Process"), "HTML should contain Section 76 table title");
        Assert.IsTrue(html.Contains("Higher-Order Tensor Parity"), "HTML should contain Section 77 table title");
    }
}
