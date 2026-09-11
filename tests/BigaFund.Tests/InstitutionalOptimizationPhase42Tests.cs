using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase42Tests
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
    public void Test_Phase42_EndogenousLiquiditySpiral()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.12, 0.18, 888);

        var result = QuantCalculator.CalculateEndogenousLiquiditySpiralAndFireSaleDeleveraging(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.SystemicLiquidityCascadeMultiplier >= 1.0m, "Systemic liquidity multiplier should be >= 1.0");
        Assert.IsTrue(result.TotalForcedFireSaleCapitalPercent > 0m && result.TotalForcedFireSaleCapitalPercent <= 100m, "Forced fire sale percent should be in (0, 100]");
        Assert.IsTrue(result.MarginSpiralElasticity > 0m, "Margin spiral elasticity should be positive");
        Assert.IsTrue(result.PortfolioStressedIlliquidityDiscountPercent > 0m, "Illiquidity discount percent should be positive");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict), "ExecutiveVerdict should not be empty");

        Assert.AreEqual(components.Count, result.FireSaleAssetItems.Count, "Should output fire sale items for each asset");
        for (int i = 0; i < result.FireSaleAssetItems.Count; i++)
        {
            var item = result.FireSaleAssetItems[i];
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetName));
            Assert.AreEqual(i + 1, item.DeleveragingPriorityRank, "Items should be sorted in sequential priority rank");
            Assert.IsTrue(item.AssetBeta > 0m);
            Assert.IsTrue(item.HaircutPercent > 0m);
            Assert.IsTrue(item.LossSpiralImpactBps > 0m);
            Assert.IsTrue(item.ForcedLiquidationVolumeRmb > 0m);
            Assert.IsTrue(item.FireSaleDiscountPercent > 0m);
        }
    }

    [TestMethod]
    public void Test_Phase42_VariationalLatentManifoldRegime()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.10, 0.15, 999);

        var result = QuantCalculator.CalculateVariationalLatentManifoldRegimeClustering(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.KlDivergenceLoss > 0m, "KL divergence loss should be positive");
        Assert.IsTrue(result.DeepReconstructionAnomalyScore >= 0m && result.DeepReconstructionAnomalyScore <= 100m, "Anomaly score should be in [0, 100]");
        Assert.IsTrue(result.ManifoldTransitionVelocity > 0m, "Manifold transition velocity should be positive");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.DominantLatentClusterName), "Dominant cluster name should not be empty");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict), "ExecutiveVerdict should not be empty");

        Assert.AreEqual(4, result.LatentClusters.Count, "Should output 4 latent manifold clusters");
        decimal sumProb = result.LatentClusters.Sum(c => c.ClusterProbabilityPercent);
        Assert.IsTrue(Math.Abs(sumProb - 100m) < 1.0m, $"Cluster probabilities should sum to ~100%, got {sumProb}");

        foreach (var c in result.LatentClusters)
        {
            Assert.IsTrue(c.ClusterId >= 1 && c.ClusterId <= 4);
            Assert.IsFalse(string.IsNullOrWhiteSpace(c.ClusterName));
            Assert.IsTrue(c.ClusterProbabilityPercent > 0m);
            Assert.IsTrue(c.RegimeSharpeMultiplier > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(c.RecommendedRegimeAction));
        }
    }

    [TestMethod]
    public void Test_Phase42_PercolationTailPhaseTransition()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.09, 0.14, 123);

        var result = QuantCalculator.CalculatePercolationTailPhaseTransitionNetwork(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.CriticalPercolationThreshold > 0m && result.CriticalPercolationThreshold <= 1.0m, "Critical percolation threshold should be in (0, 1]");
        Assert.IsTrue(result.GiantConnectedClusterSizePercent > 0m && result.GiantConnectedClusterSizePercent <= 100m, "Giant cluster size should be in (0, 100]");
        Assert.IsTrue(result.PercolationSusceptibility > 0m, "Percolation susceptibility should be positive");
        Assert.IsTrue(result.DistanceToPhaseTransitionCriticality >= 0m, "Distance to criticality should be non-negative");
        Assert.IsTrue(result.SystemicCriticalityIndex >= 0m && result.SystemicCriticalityIndex <= 100m, "Systemic criticality index should be in [0, 100]");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict), "ExecutiveVerdict should not be empty");

        int expectedEdges = components.Count * (components.Count - 1) / 2;
        Assert.AreEqual(expectedEdges, result.PercolationCriticalEdges.Count, "Should output N*(N-1)/2 undirected percolation edges");

        foreach (var edge in result.PercolationCriticalEdges)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(edge.SourceAsset));
            Assert.IsFalse(string.IsNullOrWhiteSpace(edge.TargetAsset));
            Assert.AreNotEqual(edge.SourceAsset, edge.TargetAsset);
            Assert.IsTrue(edge.TailCorrelation > 0m && edge.TailCorrelation <= 1.0m);
            Assert.IsTrue(edge.PercolationWeight > 0m && edge.PercolationWeight <= 1.0m);
            Assert.IsTrue(edge.CriticalBreakageResistance > 0m);
        }
    }

    [TestMethod]
    public void Test_Phase42_StatArbResidualMomentumOu()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.13, 0.19, 456);

        var result = QuantCalculator.CalculateStatArbResidualMomentumAndOrnsteinUhlenbeck(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.AverageCointegrationHalfLifeDays > 0m, "Cointegration half life should be positive");
        Assert.IsTrue(result.TopPairArbitrageZScore >= 0m, "Top pair arbitrage Z-score should be non-negative");
        Assert.IsTrue(result.AnnualizedIdiosyncraticStatArbAlphaPercent > 0m, "Annualized stat arb alpha should be positive");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict), "ExecutiveVerdict should not be empty");

        int expectedPairs = components.Count * (components.Count - 1) / 2;
        Assert.AreEqual(expectedPairs, result.StatArbPairs.Count, "Should output N*(N-1)/2 stat arb pairs");

        foreach (var pair in result.StatArbPairs)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(pair.AssetPairCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(pair.AssetPairName));
            Assert.IsTrue(pair.CointegrationAdfPValue >= 0m && pair.CointegrationAdfPValue <= 1.0m);
            Assert.IsTrue(pair.OuMeanReversionSpeedTheta > 0m);
            Assert.IsTrue(pair.OuHalfLifeDays > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(pair.OptimalArbitrageSignalBadge));
        }
    }

    [TestMethod]
    public void Test_Phase42_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var engineResult = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(engineResult);
        Assert.IsNotNull(engineResult.EndogenousLiquiditySpiral, "EndogenousLiquiditySpiral should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.VariationalLatentManifoldRegime, "VariationalLatentManifoldRegime should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.PercolationTailPhaseTransition, "PercolationTailPhaseTransition should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.StatArbResidualMomentumOu, "StatArbResidualMomentumOu should be populated in PortfolioEngine");

        // Verify institutional due diligence HTML report generation
        string htmlReport = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(engineResult, components);
        Assert.IsFalse(string.IsNullOrWhiteSpace(htmlReport));
        Assert.IsTrue(htmlReport.Contains("七十、Bridgewater Associates & AQR Capital 跨资产内生流动性螺旋与去杠杆压力传染动力学模型 (Phase 42)"), "Report should contain Phase 42.1 section");
        Assert.IsTrue(htmlReport.Contains("七十一、Renaissance Technologies & Citadel 变分无监督深度隐空间流形聚类与非线性宏观体制自编码器 (Phase 42)"), "Report should contain Phase 42.2 section");
        Assert.IsTrue(htmlReport.Contains("七十二、Millennium Management & Point72 跨资产多周期极值尾部相关相变检测与渗流理论系统性临界熔断网络 (Phase 42)"), "Report should contain Phase 42.3 section");
        Assert.IsTrue(htmlReport.Contains("七十三、WorldQuant & Hudson River Trading 统计套利强化正交特征残差动量与微观协整均值回复追踪引擎 (Phase 42)"), "Report should contain Phase 42.4 section");
    }
}
