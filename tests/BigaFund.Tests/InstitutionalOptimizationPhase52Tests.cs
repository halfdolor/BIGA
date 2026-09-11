using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase52Tests
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
    public void Test_Phase52_BouchaudTransientImpactPropagator()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateBouchaudTransientImpactPropagator(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.ImpactItems.Count);
        Assert.IsTrue(res.GlobalTransientSlippageSavingsBps > 0m, "Global transient slippage savings should be positive");
        Assert.IsTrue(res.MeanPowerLawExponentGamma >= 0.40m && res.MeanPowerLawExponentGamma <= 0.60m,
            "Mean power law exponent gamma should be within [0.40, 0.60]");
        Assert.IsTrue(res.AverageOrderFlowConcealmentIndex > 0m && res.AverageOrderFlowConcealmentIndex <= 100m,
            "Order flow concealment index should be within (0, 100]");
        Assert.IsTrue(res.SelfImpactMitigationRatioPct > 0m && res.SelfImpactMitigationRatioPct <= 100m,
            "Self impact mitigation ratio should be within (0, 100]");

        foreach (var item in res.ImpactItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.CharacteristicRelaxationTimeSec > 0m);
            Assert.IsTrue(item.PowerLawDecayExponentGamma >= 0.40m && item.PowerLawDecayExponentGamma <= 0.60m);
            Assert.IsTrue(item.CumulativeSelfImpactBps > 0m);
            Assert.IsTrue(item.ReboundElasticityRatio > 0m);
            Assert.IsTrue(item.OptimalUProfileHeadSlicePct > 0m);
            Assert.IsTrue(item.OptimalUProfileTailSlicePct > 0m);
            Assert.IsTrue(item.TransientSlippageSavingsBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.ImpactRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.TransientExecutionAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Citadel") || res.ExecutiveVerdict.Contains("Jane Street") || res.ExecutiveVerdict.Contains("Bouchaud") || res.ExecutiveVerdict.Contains("瞬态"),
            "Verdict should mention Citadel, Jane Street, Bouchaud, or 瞬态");
    }

    [TestMethod]
    public void Test_Phase52_GraphLaplacianDiffusionWavelet()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateGraphLaplacianDiffusionWavelet(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.ClusterItems.Count);
        Assert.IsTrue(res.FiedlerAlgebraicConnectivity >= 0m, "Fiedler algebraic connectivity should be non-negative");
        Assert.IsTrue(res.ManifoldDiffusionDimension > 0m, "Manifold diffusion dimension should be positive");
        Assert.IsTrue(res.SpectralClusteringModularity != 0m, "Spectral clustering modularity should be non-zero");
        Assert.IsTrue(res.GlobalWaveletPurgeRatioPct > 0m && res.GlobalWaveletPurgeRatioPct <= 100m,
            "Global wavelet purge ratio should be within (0, 100]");

        foreach (var item in res.ClusterItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.SpectralClusterId >= 1);
            Assert.IsTrue(item.GraphDegreeCentrality > 0m);
            Assert.IsTrue(item.HeatKernelDiffusionRadius > 0m);
            Assert.IsTrue(item.DiffusionWaveletNoisePurgeBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.TopologyCommunityBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.ManifoldAllocationAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Two Sigma") || res.ExecutiveVerdict.Contains("D.E. Shaw") || res.ExecutiveVerdict.Contains("Laplacian") || res.ExecutiveVerdict.Contains("拉普拉斯") || res.ExecutiveVerdict.Contains("菲德勒"),
            "Verdict should mention Two Sigma, D.E. Shaw, Laplacian, or Fiedler");
    }

    [TestMethod]
    public void Test_Phase52_ViscoelasticRheologyCapitalStrain()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateViscoelasticRheologyCapitalStrain(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.RheologyItems.Count);
        Assert.IsTrue(res.GlobalSystemicLossTangent > 0m, "Global systemic loss tangent should be positive");
        Assert.IsTrue(res.MeanFractionalOrderAlpha > 0m && res.MeanFractionalOrderAlpha < 1.0m,
            "Mean fractional order alpha should be in (0, 1)");
        Assert.IsTrue(res.SystemicDynamicStorageModulus > 0m, "Systemic dynamic storage modulus should be positive");
        Assert.IsTrue(res.EarliestCreepRuptureHorizonDays > 0m, "Earliest creep rupture horizon days should be positive");

        foreach (var item in res.RheologyItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.FractionalRheologyOrderAlpha > 0m && item.FractionalRheologyOrderAlpha < 1.0m);
            Assert.IsTrue(item.DynamicStorageModulusEr > 0m);
            Assert.IsTrue(item.DynamicLossModulusEi > 0m);
            Assert.IsTrue(item.SystemicLossTangentTanDelta > 0m);
            Assert.IsTrue(item.MittagLefflerCreepCompliance > 0m);
            Assert.IsTrue(item.CreepRuptureHorizonDays > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.RheologyStateBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.ViscoelasticHedgingAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Bridgewater") || res.ExecutiveVerdict.Contains("AQR") || res.ExecutiveVerdict.Contains("粘弹性") || res.ExecutiveVerdict.Contains("流变") || res.ExecutiveVerdict.Contains("Mittag-Leffler"),
            "Verdict should mention Bridgewater, AQR, Viscoelastic, Rheology, or Mittag-Leffler");
    }

    [TestMethod]
    public void Test_Phase52_NonequilibriumLangevinVorticity()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateNonequilibriumLangevinVorticity(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(6, res.VorticityItems.Count); // 4 * 3 / 2 = 6 pairs
        Assert.IsTrue(res.GlobalLimitCyclePumpWorkBps > 0m, "Global limit cycle pump work bps should be positive");
        Assert.IsTrue(res.MeanBrokenDetailedBalanceDegree >= 0m && res.MeanBrokenDetailedBalanceDegree <= 1.0m,
            "Mean broken detailed balance degree should be in [0, 1]");
        Assert.IsTrue(res.MaximumProbabilityVorticity > 0m, "Maximum probability vorticity should be positive");
        Assert.IsTrue(res.NonEquilibriumStatArbEfficiencyPct > 0m && res.NonEquilibriumStatArbEfficiencyPct <= 100m,
            "Non-equilibrium stat-arb efficiency should be in (0, 100]");

        foreach (var item in res.VorticityItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.PairCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.PairName));
            Assert.IsTrue(item.StationaryDriftGradientForce >= 0m);
            Assert.IsTrue(item.NonConservativeRotationalForce > 0m);
            Assert.IsTrue(item.BrokenDetailedBalanceDegree >= 0m && item.BrokenDetailedBalanceDegree <= 1.0m);
            Assert.IsTrue(item.ProbabilityCurrentVorticity > 0m);
            Assert.IsTrue(item.IrreversibleEntropyProductionRate > 0m);
            Assert.IsTrue(item.LimitCyclePumpWorkBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.VorticityRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.LangevinArbitrageAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Renaissance") || res.ExecutiveVerdict.Contains("Hudson River") || res.ExecutiveVerdict.Contains("HRT") || res.ExecutiveVerdict.Contains("朗之万") || res.ExecutiveVerdict.Contains("旋度"),
            "Verdict should mention Renaissance, HRT, Langevin, or Vorticity");
    }

    [TestMethod]
    public void Test_Phase52_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.BouchaudTransientImpactPropagator, "BouchaudTransientImpactPropagator should be populated in PortfolioResult");
        Assert.IsNotNull(result.GraphLaplacianDiffusionWavelet, "GraphLaplacianDiffusionWavelet should be populated in PortfolioResult");
        Assert.IsNotNull(result.ViscoelasticRheologyCapitalStrain, "ViscoelasticRheologyCapitalStrain should be populated in PortfolioResult");
        Assert.IsNotNull(result.NonequilibriumLangevinVorticity, "NonequilibriumLangevinVorticity should be populated in PortfolioResult");

        // Verify HTML Export includes Phase 52 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("一百一十、Citadel Securities & Jane Street: 微观瞬态幂律记忆价格冲击核 (Bouchaud Propagator) 与动态隐匿拆单执行引擎 (Phase 52)"), "HTML should contain Section 110");
        Assert.IsTrue(html.Contains("一百一十一、Two Sigma & D.E. Shaw: 高维流形图拉普拉斯算子谱聚类与非局部热核扩散小波多尺度 Alpha 分解引擎 (Phase 52)"), "HTML should contain Section 111");
        Assert.IsTrue(html.Contains("一百一十二、Bridgewater Associates & AQR Capital: 分数阶粘弹性流变学宏观资产负荷动力学与系统性流动性蠕变松弛测算引擎 (Phase 52)"), "HTML should contain Section 112");
        Assert.IsTrue(html.Contains("一百一十三、Renaissance Technologies & Hudson River Trading: 非平衡态朗之万细致平衡破缺与概率流旋度相空间统计套利做功巡回引擎 (Phase 52)"), "HTML should contain Section 113");
        Assert.IsTrue(html.Contains("组合瞬态滑点挽回总增益"), "HTML should contain Section 110 keywords");
        Assert.IsTrue(html.Contains("菲德勒代数连通韧性 λ_2"), "HTML should contain Section 111 keywords");
        Assert.IsTrue(html.Contains("流动性流变损耗角正切 tan δ"), "HTML should contain Section 112 keywords");
        Assert.IsTrue(html.Contains("相空间极限环泵送总做功"), "HTML should contain Section 113 keywords");
    }
}
