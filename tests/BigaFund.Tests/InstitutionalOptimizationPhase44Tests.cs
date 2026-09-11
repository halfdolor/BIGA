using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase44Tests
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
    public void Test_Phase44_BsdeDynamicViscosityHedging()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateBsdeDynamicViscosityHedging(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(components.Count, res.AssetBsdeHedges.Count);
        Assert.IsTrue(res.PortfolioAverageHedgeControlNorm > 0m, "BSDE control norm ||Z_t|| should be positive");
        Assert.IsTrue(res.NetHedgeSlippageSavingsBps > 0m, "Hedge slippage savings should be positive");
        Assert.IsTrue(res.StochasticVolCurvatureSuppressionPercent > 0m, "Curvature suppression should be positive");
        Assert.IsTrue(res.DynamicHedgeEfficiencyScore > 0m && res.DynamicHedgeEfficiencyScore <= 100m, "Efficiency score should be in [0, 100]");

        foreach (var asset in res.AssetBsdeHedges)
        {
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(asset.HedgeExecutionDirective));
            Assert.IsTrue(asset.DynamicHedgeControlZt > 0m, "Hedge control Z_t should be positive");
            Assert.IsTrue(asset.ViscosityValueYt > 0m, "Viscosity value Y_t should be positive");
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("BSDE"), "Executive verdict should mention BSDE");
    }

    [TestMethod]
    public void Test_Phase44_HypergraphTopologicalCausality()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateHypergraphTopologicalCausality(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.IsTrue(res.TotalHyperedges > 0, "Should detect at least 1 hyperedge");
        Assert.IsTrue(res.HypergraphSpectralGap > 0m, "Spectral gap should be positive");
        Assert.IsTrue(res.Betti0ConnectedComponents >= 1, "Betti-0 components should be >= 1");
        Assert.IsTrue(res.Betti1TopologicalCavities >= 0, "Betti-1 cavities should be >= 0");
        Assert.IsTrue(res.CavityRuptureProbabilityPercent >= 0m && res.CavityRuptureProbabilityPercent <= 100m);
        Assert.IsTrue(res.TopologicalPhaseEntropy > 0m, "Phase entropy should be positive");
        Assert.IsTrue(res.Hyperedges.Count > 0, "Hyperedges collection should not be empty");
        Assert.IsTrue(res.PersistenceCavities.Count > 0, "Persistent cavities should not be empty");

        foreach (var he in res.Hyperedges)
        {
            Assert.IsFalse(string.IsNullOrEmpty(he.HyperedgeTheme));
            Assert.IsTrue(he.Cardinality >= 2);
            Assert.IsTrue(he.HigherOrderSpectralContribution > 0m);
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("持续同调"), "Verdict should mention persistent homology");
    }

    [TestMethod]
    public void Test_Phase44_CrossImpactTensorMicrostructure()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateCrossImpactTensorMicrostructure(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.IsTrue(res.TensorAverageCrossImpactLambdaBps > 0m, "Cross impact lambda should be positive");
        Assert.IsTrue(res.CompositeToxicityDiffusionRatePercent > 0m, "Toxicity diffusion rate should be positive");
        Assert.IsTrue(res.CrossExecutionFrictionSavingsBps > 0m, "Execution savings should be positive");
        Assert.IsFalse(string.IsNullOrEmpty(res.MicrostructureRegimeState));
        Assert.IsTrue(res.TopCrossImpactPairs.Count > 0, "Should have cross impact pairs");
        Assert.AreEqual(components.Count, res.AssetToxicityProfiles.Count);

        foreach (var pair in res.TopCrossImpactPairs)
        {
            Assert.IsTrue(pair.InstantaneousCrossImpactLambda > 0m);
            Assert.IsTrue(pair.CrossDecayHalfLifeSeconds > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(pair.AsymmetricQuotingBias));
        }

        foreach (var profile in res.AssetToxicityProfiles)
        {
            Assert.IsFalse(string.IsNullOrEmpty(profile.ExecutionDefenseAction));
            Assert.IsTrue(profile.CrossImpactSusceptibilityIndex > 0m);
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("交叉价格冲击张量"), "Verdict should mention cross impact tensor");
    }

    [TestMethod]
    public void Test_Phase44_WassersteinDroMinimaxParity()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateWassersteinDroMinimaxParity(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.IsTrue(res.AmbiguityBallRadiusEpsilon > 0m, "Ball radius epsilon should be positive");
        Assert.IsTrue(res.WorstCaseExpectedShortfallPercent > 0m, "Worst case ES should be positive");
        Assert.IsTrue(res.OutOfSampleDrawdownMitigationPercent > 0m, "Drawdown mitigation should be positive");
        Assert.IsTrue(res.DualLagrangeMultiplierLambda > 0m, "Dual multiplier should be positive");
        Assert.AreEqual(components.Count, res.DroAssetAllocations.Count);

        decimal sumDroWeights = res.DroAssetAllocations.Sum(a => a.WassersteinDroMinimaxWeightPercent);
        Assert.IsTrue(sumDroWeights >= 99m && sumDroWeights <= 101m, $"DRO weights sum to {sumDroWeights}%");

        foreach (var item in res.DroAssetAllocations)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.RobustAllocationRole));
            Assert.IsTrue(item.WorstCaseMarginalRiskContribution > 0m);
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Wasserstein"), "Verdict should mention Wasserstein");
    }

    [TestMethod]
    public void Test_Phase44_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.BsdeDynamicHedging, "BsdeDynamicHedging should be populated");
        Assert.IsNotNull(result.HypergraphTopologicalCausality, "HypergraphTopologicalCausality should be populated");
        Assert.IsNotNull(result.CrossImpactTensorMicrostructure, "CrossImpactTensorMicrostructure should be populated");
        Assert.IsNotNull(result.WassersteinDroMinimaxParity, "WassersteinDroMinimaxParity should be populated");

        // Verify HTML Export includes Phase 44 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("七十八、Renaissance Technologies & D.E. Shaw 连续时间倒向随机微分方程 (BSDE)"), "HTML should contain Section 78");
        Assert.IsTrue(html.Contains("七十九、Citadel & Millennium 多资产高阶拓扑超图 (Hypergraph)"), "HTML should contain Section 79");
        Assert.IsTrue(html.Contains("八十、Jane Street & Citadel Securities 微观瞬时订单流毒性扩散核"), "HTML should contain Section 80");
        Assert.IsTrue(html.Contains("八十一、Bridgewater Associates & AQR 测度模糊集 Wasserstein-Ball 分布鲁棒优化 (DRO)"), "HTML should contain Section 81");
        Assert.IsTrue(html.Contains("BSDE Viscosity Hedging"), "HTML should contain Section 78 table title");
        Assert.IsTrue(html.Contains("Topological Hyperedges"), "HTML should contain Section 79 table title");
        Assert.IsTrue(html.Contains("Cross-Impact Pairs"), "HTML should contain Section 80 table title");
        Assert.IsTrue(html.Contains("Wasserstein DRO"), "HTML should contain Section 81 table title");
    }
}
