using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase46Tests
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
    public void Test_Phase46_RoughPathSignatureAlpha()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateRoughPathSignatureAlpha(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(components.Count, res.AssetPathSignatures.Count);
        Assert.IsTrue(res.PortfolioAverageLevyArea != 0m, "Portfolio average Levy area should be calculated");
        Assert.IsTrue(res.CompositePathCurvatureIndex > 0m, "Composite path curvature should be positive");
        Assert.IsTrue(res.HighOrderTensorAlphaPremiumBps > 0m, "High order tensor alpha premium should be positive");
        Assert.IsTrue(res.SignatureInformationCaptureRatio > 0m && res.SignatureInformationCaptureRatio <= 100m, "Signature information capture ratio should be in (0, 100]");

        foreach (var asset in res.AssetPathSignatures)
        {
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetName));
            Assert.IsTrue(asset.SignatureOrder1ReturnNorm > 0m, "Order 1 return norm should be positive");
            Assert.IsTrue(asset.SignatureOrder2EnergyNorm > 0m, "Order 2 energy norm should be positive");
            Assert.IsTrue(asset.GeometricMomentumCurvature > 0m, "Geometric momentum curvature should be positive");
            Assert.IsTrue(asset.PathSignatureAlphaScore > 0m, "Alpha score should be positive");
            Assert.IsTrue(asset.NonMarkovianAlphaIncrementBps > 0m, "Non-Markovian alpha increment should be positive");
            Assert.IsFalse(string.IsNullOrEmpty(asset.PathTopologyRegimeBadge), "Path topology badge should be populated");
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("粗糙路径签名") || res.ExecutiveVerdict.Contains("Rough"), "Verdict should mention signature");
    }

    [TestMethod]
    public void Test_Phase46_StochasticOptimalStopping()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateStochasticOptimalStopping(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(components.Count, res.AssetStoppingThresholds.Count);
        Assert.IsTrue(res.PortfolioWeightedStoppingBoundary > 0m && res.PortfolioWeightedStoppingBoundary <= 1.0m, "Portfolio weighted stopping boundary should be in (0, 1]");
        Assert.IsTrue(res.AggregateSnellEnvelopeTimeValueBps > 0m, "Snell envelope time value should be positive");
        Assert.IsTrue(res.AvoidedDrawdownAlphaSavingsBps > 0m, "Avoided drawdown alpha savings should be positive");

        foreach (var asset in res.AssetStoppingThresholds)
        {
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetName));
            Assert.IsTrue(asset.CurrentPriceToHighRatio > 0m, "Current price to high ratio should be positive");
            Assert.IsTrue(asset.FreeBoundaryStoppingThreshold > 0m && asset.FreeBoundaryStoppingThreshold <= 1.0m, "Stopping threshold should be in (0, 1]");
            Assert.IsTrue(asset.SnellEnvelopeOptionValueBps >= 0m, "Snell envelope option value should be non-negative");
            Assert.IsTrue(asset.SmoothPastingElasticity > 0m, "Smooth pasting elasticity should be positive");
            Assert.IsTrue(asset.ExpectedOptimalHoldingDaysRemaining > 0m, "Expected holding days remaining should be positive");
            Assert.IsFalse(string.IsNullOrEmpty(asset.DeRiskingUrgencyBadge), "Urgency badge should be populated");
            Assert.IsFalse(string.IsNullOrEmpty(asset.OptimalStoppingAction), "Stopping action should be populated");
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("随机最优停止") || res.ExecutiveVerdict.Contains("斯内尔包络"), "Verdict should mention optimal stopping or Snell envelope");
    }

    [TestMethod]
    public void Test_Phase46_CausalStructuralModelAttribution()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateCausalStructuralModelAttribution(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.CausalNodeEffects.Count, "Should identify 4 macro causal nodes");
        Assert.AreEqual(3, res.CounterfactualScenarios.Count, "Should simulate 3 counterfactual scenarios");
        Assert.IsTrue(res.SystemicConfoundingBiasRatio > 0m && res.SystemicConfoundingBiasRatio <= 100m, "Systemic confounding bias ratio should be in (0, 100]");
        Assert.IsTrue(res.TrueCausalAlphaContributionBps > 0m, "True causal alpha contribution should be positive");
        Assert.IsTrue(res.SpuriousCorrelationEliminatedBps > 0m, "Spurious correlation eliminated should be positive");

        foreach (var node in res.CausalNodeEffects)
        {
            Assert.IsFalse(string.IsNullOrEmpty(node.FactorOrVariableName));
            Assert.IsTrue(node.DirectCausalContributionPercent > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(node.CausalMechanismType));
        }

        foreach (var cf in res.CounterfactualScenarios)
        {
            Assert.IsFalse(string.IsNullOrEmpty(cf.ScenarioName));
            Assert.IsFalse(string.IsNullOrEmpty(cf.IntervenedDoVariable));
            Assert.IsFalse(string.IsNullOrEmpty(cf.StrategicImplication));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("因果结构方程") || res.ExecutiveVerdict.Contains("Do-Calculus"), "Verdict should mention SCM or Do-Calculus");
    }

    [TestMethod]
    public void Test_Phase46_TransientMarketImpactPropagator()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateTransientMarketImpactPropagator(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(components.Count, res.AssetTransientImpacts.Count);
        Assert.IsTrue(res.PortfolioAverageDecayExponentGamma > 0m, "Power law decay gamma should be positive");
        Assert.IsTrue(res.TotalAccumulatedTransientDragBps > 0m, "Total accumulated transient drag should be positive");
        Assert.IsTrue(res.NonUniformExecutionSavingsBps > 0m, "Non-uniform execution savings should be positive");
        Assert.IsTrue(res.MarketResilienceHalfLifeMinutes > 0m, "Resilience half-life should be positive");

        foreach (var asset in res.AssetTransientImpacts)
        {
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetName));
            Assert.IsTrue(asset.PowerLawDecayExponentGamma > 0m);
            Assert.IsTrue(asset.PropagatorMemoryKernelFactorG0 > 0m);
            Assert.IsTrue(asset.AccumulatedTransientImpactBps > 0m);
            Assert.IsTrue(asset.InstantaneousNaiveImpactBps > 0m);
            Assert.IsTrue(asset.ImpactMemoryAccumulationRatio > 0m);
            Assert.IsTrue(asset.AdaptiveExecutionSavingsBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(asset.ExecutionCadenceAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("暂态冲击传播子") || res.ExecutiveVerdict.Contains("暂态"), "Verdict should mention transient impact propagator");
    }

    [TestMethod]
    public void Test_Phase46_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.RoughPathSignatureAlpha, "RoughPathSignatureAlpha should be populated");
        Assert.IsNotNull(result.StochasticOptimalStopping, "StochasticOptimalStopping should be populated");
        Assert.IsNotNull(result.CausalStructuralModelAttribution, "CausalStructuralModelAttribution should be populated");
        Assert.IsNotNull(result.TransientMarketImpactPropagator, "TransientMarketImpactPropagator should be populated");

        // Verify HTML Export includes Phase 46 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("八十六、Renaissance Technologies & Alan Turing Institute 粗糙路径特征签名 (Rough Path Signature)"), "HTML should contain Section 86");
        Assert.IsTrue(html.Contains("八十七、Citadel Global Strategies & Millennium RV 随机最优停止时间与自由边界斯内尔包络"), "HTML should contain Section 87");
        Assert.IsTrue(html.Contains("八十八、Two Sigma & Bridgewater Associates 因果结构方程模型 (SCM) 与反事实 Do-Calculus"), "HTML should contain Section 88");
        Assert.IsTrue(html.Contains("八十九、Jane Street & Citadel Securities 暂态市场冲击幂律记忆核 (Propagator Kernel)"), "HTML should contain Section 89");
        Assert.IsTrue(html.Contains("Rough Path Signature"), "HTML should contain Section 86 table title");
        Assert.IsTrue(html.Contains("Snell Envelope"), "HTML should contain Section 87 table title");
        Assert.IsTrue(html.Contains("Pearl SCM"), "HTML should contain Section 88 table title");
        Assert.IsTrue(html.Contains("Propagator Model"), "HTML should contain Section 89 table title");
    }
}
