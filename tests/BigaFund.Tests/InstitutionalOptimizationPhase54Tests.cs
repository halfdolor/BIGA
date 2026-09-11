using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase54Tests
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
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.12, 0.18, seed: 222)
        };
        var fundC = new FundDetail
        {
            Code = "000001",
            Name = "华夏成长混合 (均衡成长对冲)",
            FundSize = "95.2亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.15, 0.20, seed: 333)
        };
        var fundD = new FundDetail
        {
            Code = "511010",
            Name = "国债ETF (宏观无风险流动性压舱石)",
            FundSize = "320.0亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.04, 0.05, seed: 444)
        };

        return new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 30m),
            (fundB, 35m),
            (fundC, 20m),
            (fundD, 15m)
        };
    }

    private static List<decimal> CreateSampleDailyReturns()
    {
        var rng = new Random(555);
        var returns = new List<decimal>();
        for (int i = 0; i < 250; i++)
        {
            returns.Add((decimal)(rng.NextDouble() * 0.04 - 0.018));
        }
        return returns;
    }

    [TestMethod]
    public void Test_Phase54_ContinuousMarkovSwitchingDirichletProcess()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateContinuousMarkovSwitchingDirichletProcess(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.RegimeItems.Count);
        Assert.IsTrue(res.GlobalHdpRegimeEntropy > 0m, "Global HDP regime entropy should be positive");
        Assert.IsTrue(res.DominantStateHazardRate > 0m, "Dominant state hazard rate should be positive");
        Assert.IsTrue(res.NonparametricActiveStateCount >= 2, "Active state count should be at least 2");
        Assert.IsTrue(res.HdpStateTransitionStabilityPct > 0m && res.HdpStateTransitionStabilityPct <= 100m,
            "HDP state transition stability should be in (0, 100]");

        foreach (var item in res.RegimeItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.ActiveLatentState));
            Assert.IsTrue(item.StationaryProbabilityPct > 0m && item.StationaryProbabilityPct <= 100m);
            Assert.IsTrue(item.MeanSojournTimeDays > 0m);
            Assert.IsTrue(item.InstantaneousHazardRate > 0m);
            Assert.IsTrue(item.RegimeEntropyNats > 0m);
            Assert.IsTrue(item.HdpAlphaYieldGainBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.StateRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.DirichletRegimeControlAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Renaissance") || res.ExecutiveVerdict.Contains("D.E. Shaw") || res.ExecutiveVerdict.Contains("狄利克雷") || res.ExecutiveVerdict.Contains("HDP-HMM") || res.ExecutiveVerdict.Contains("马尔可夫"),
            "Verdict should mention Renaissance, D.E. Shaw, Dirichlet, HDP-HMM, or Markov");
    }

    [TestMethod]
    public void Test_Phase54_MeanFieldGameImpulseLiquidityControl()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateMeanFieldGameImpulseLiquidityControl(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.ImpulseItems.Count);
        Assert.IsTrue(res.GlobalMeanFieldLiquidityAlphaBps > 0m, "Global mean field liquidity alpha bps should be positive");
        Assert.IsTrue(res.MeanFieldNashEquilibriumSpreadBps > 0m, "Mean field Nash equilibrium spread bps should be positive");
        Assert.IsTrue(res.HerdingCrowdImmunityPct > 0m && res.HerdingCrowdImmunityPct <= 100m,
            "Herding crowd immunity pct should be in (0, 100]");
        Assert.IsTrue(res.ImpulseControlEfficiencyRatio > 0m, "Impulse control efficiency ratio should be positive");

        foreach (var item in res.ImpulseItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.ContinuousSpreadBps > 0m);
            Assert.IsTrue(item.ImpulseThresholdSStar > 0m);
            Assert.IsTrue(item.MeanFieldDensityM > 0m);
            Assert.IsTrue(item.NashEquilibriumAlphaBps > 0m);
            Assert.IsTrue(item.HerdingVulnerability >= 0m);
            Assert.IsTrue(item.ImpulseExecutionSavingBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.MfgStateBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.MfgOrderControlAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Citadel") || res.ExecutiveVerdict.Contains("Hudson River") || res.ExecutiveVerdict.Contains("平均场") || res.ExecutiveVerdict.Contains("MFG") || res.ExecutiveVerdict.Contains("冲量"),
            "Verdict should mention Citadel, Hudson River, Mean-Field, MFG, or Impulse");
    }

    [TestMethod]
    public void Test_Phase54_CausalDagStructuralInvarianceAlpha()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateCausalDagStructuralInvarianceAlpha(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.CausalItems.Count);
        Assert.IsTrue(res.GlobalCausalInvarianceAlphaBps > 0m, "Global causal invariance alpha bps should be positive");
        Assert.IsTrue(res.MeanCausalGraphSparsityRatio > 0m && res.MeanCausalGraphSparsityRatio <= 1m,
            "Mean causal graph sparsity ratio should be in (0, 1]");
        Assert.IsTrue(res.SpuriousCorrelationRejectionRatePct > 0m && res.SpuriousCorrelationRejectionRatePct <= 100m,
            "Spurious correlation rejection rate pct should be in (0, 100]");
        Assert.IsTrue(res.CounterfactualRobustnessScore > 0m && res.CounterfactualRobustnessScore <= 100m,
            "Counterfactual robustness score should be in (0, 100]");

        foreach (var item in res.CausalItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.CausalInvarianceScore > 0m);
            Assert.IsTrue(item.DoCalculusInterventionAlphaBps > 0m);
            Assert.IsTrue(item.DirectCausalParentsCount >= 0);
            Assert.IsTrue(item.ConfounderBiasAttenuationPct > 0m);
            Assert.IsTrue(item.CausalStabilityRatio > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.CausalRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.CausalAlphaTradingAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Two Sigma") || res.ExecutiveVerdict.Contains("WorldQuant") || res.ExecutiveVerdict.Contains("因果") || res.ExecutiveVerdict.Contains("DAG") || res.ExecutiveVerdict.Contains("SCM"),
            "Verdict should mention Two Sigma, WorldQuant, Causal, DAG, or SCM");
    }

    [TestMethod]
    public void Test_Phase54_SpectralRiskExtremeCopulaStress()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateSpectralRiskExtremeCopulaStress(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.StressItems.Count);
        Assert.IsTrue(res.GlobalSpectralRiskCapitalRequirementPct > 0m && res.GlobalSpectralRiskCapitalRequirementPct <= 100m,
            "Global spectral risk capital requirement pct should be in (0, 100]");
        Assert.IsTrue(res.ExtremeTailAsymmetricCopulaDependency >= 0m && res.ExtremeTailAsymmetricCopulaDependency <= 1m,
            "Extreme tail asymmetric copula dependency should be in [0, 1]");
        Assert.IsTrue(res.GpdTailShapeParameterXi > 0m, "GPD tail shape parameter xi should be positive");
        Assert.IsTrue(res.OptimalTailConvexityHedgeRatioPct > 0m && res.OptimalTailConvexityHedgeRatioPct <= 100m,
            "Optimal tail convexity hedge ratio pct should be in (0, 100]");

        foreach (var item in res.StressItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.SpectralRiskContributionPct > 0m);
            Assert.IsTrue(item.GpdScaleBeta > 0m);
            Assert.IsTrue(item.GpdShapeXi > 0m);
            Assert.IsTrue(item.AsymmetricLowerTailCopula >= 0m && item.AsymmetricLowerTailCopula <= 1m);
            Assert.IsTrue(item.TailConvexityHedgeCostBps > 0m);
            Assert.IsTrue(item.StressCapitalAdequacyPct > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.SpectralRiskBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.ExtremeTailHedgingAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Bridgewater") || res.ExecutiveVerdict.Contains("AQR") || res.ExecutiveVerdict.Contains("谱风险") || res.ExecutiveVerdict.Contains("Copula") || res.ExecutiveVerdict.Contains("极端"),
            "Verdict should mention Bridgewater, AQR, Spectral Risk, Copula, or Extreme");
    }

    [TestMethod]
    public void Test_Phase54_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.ContinuousMarkovSwitchingDirichletProcess, "ContinuousMarkovSwitchingDirichletProcess should be populated in PortfolioResult");
        Assert.IsNotNull(result.MeanFieldGameImpulseLiquidityControl, "MeanFieldGameImpulseLiquidityControl should be populated in PortfolioResult");
        Assert.IsNotNull(result.CausalDagStructuralInvarianceAlpha, "CausalDagStructuralInvarianceAlpha should be populated in PortfolioResult");
        Assert.IsNotNull(result.SpectralRiskExtremeCopulaStress, "SpectralRiskExtremeCopulaStress should be populated in PortfolioResult");

        // Verify HTML Export includes Phase 54 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("一百一十八、Renaissance Technologies & D.E. Shaw: 连续时间马尔可夫跳跃状态空间与分层狄利克雷过程宏观体制涌现引擎 (Phase 54)"), "HTML should contain Section 118");
        Assert.IsTrue(html.Contains("一百一十九、Citadel Securities & Hudson River Trading: 双边限价订单簿微观非线性平均场博弈与高阶随机冲量最优流动性提供控制引擎 (Phase 54)"), "HTML should contain Section 119");
        Assert.IsTrue(html.Contains("一百二十、Two Sigma & WorldQuant: 高维因果发现有向无环图与结构因果模型反事实干预不变性 Alpha 纯化引擎 (Phase 54)"), "HTML should contain Section 120");
        Assert.IsTrue(html.Contains("一百二十一、Bridgewater Associates & AQR Capital: 广义非对称谱风险测度与动态极值高斯-Student Copula 尾部联动全景压测引擎 (Phase 54)"), "HTML should contain Section 121");
        Assert.IsTrue(html.Contains("全组合宏观体制信息熵"), "HTML should contain Section 118 keywords");
        Assert.IsTrue(html.Contains("平均场博弈做市 Alpha"), "HTML should contain Section 119 keywords");
        Assert.IsTrue(html.Contains("因果不变纯化全局 Alpha"), "HTML should contain Section 120 keywords");
        Assert.IsTrue(html.Contains("广义谱风险资本拨备率"), "HTML should contain Section 121 keywords");
    }
}
