using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase53Tests
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
    public void Test_Phase53_JumpDiffusionAffineMarketMaking()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateJumpDiffusionAffineMarketMaking(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.MarketMakingItems.Count);
        Assert.IsTrue(res.GlobalToxicityAvoidanceGainBps > 0m, "Global toxicity avoidance gain bps should be positive");
        Assert.IsTrue(res.MeanJumpPoissonIntensity > 0m, "Mean jump intensity should be positive");
        Assert.IsTrue(res.AverageOptimalBidAskSpreadBps > 0m, "Average optimal spread should be positive");
        Assert.IsTrue(res.MarketMakingInventoryResiliencePct > 0m && res.MarketMakingInventoryResiliencePct <= 100m,
            "Market making resilience pct should be in (0, 100]");

        foreach (var item in res.MarketMakingItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.JumpPoissonIntensityLambda > 0m);
            Assert.IsTrue(item.MeanJumpMagnitudePercent > 0m);
            Assert.IsTrue(item.OptimalBidHalfSpreadBps > 0m);
            Assert.IsTrue(item.OptimalAskHalfSpreadBps > 0m);
            Assert.IsTrue(item.InventoryRiskPenaltyGamma > 0m);
            Assert.IsTrue(item.ToxicityAvoidanceGainBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.JumpRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.MarketMakingControlAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Jump Trading") || res.ExecutiveVerdict.Contains("Optiver") || res.ExecutiveVerdict.Contains("跳跃") || res.ExecutiveVerdict.Contains("做市"),
            "Verdict should mention Jump Trading, Optiver, or jump market making");
    }

    [TestMethod]
    public void Test_Phase53_AffineArbitrageFreeTermStructure()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateAffineArbitrageFreeTermStructure(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.FactorItems.Count);
        Assert.IsTrue(res.GlobalButterflyConvexityAlphaBps > 0m, "Global butterfly convexity alpha bps should be positive");
        Assert.IsTrue(res.MeanDynamicTermPremiumBps > 0m, "Mean dynamic term premium bps should be positive");
        Assert.IsTrue(res.YieldCurveArbitrageViolationResidual >= 0m, "Yield curve arbitrage violation residual should be non-negative");
        Assert.IsTrue(res.TermStructureTrackingQualityPct > 90m && res.TermStructureTrackingQualityPct <= 100m,
            "Term structure tracking quality should be > 90%");

        foreach (var item in res.FactorItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.LevelFactorLoadingL > 0m);
            Assert.IsTrue(item.SlopeFactorLoadingS > 0m);
            Assert.IsTrue(item.CurvatureFactorLoadingC > 0m);
            Assert.IsTrue(item.DynamicTermPremiumBps > 0m);
            Assert.IsTrue(item.ButterflyConvexityArbitrageAlphaBps > 0m);
            Assert.IsTrue(item.ArbitrageFreeModelFitR2 > 0.95m);
            Assert.IsFalse(string.IsNullOrEmpty(item.TermStructureRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.YieldCurveArbitrageAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Citadel") || res.ExecutiveVerdict.Contains("Millennium") || res.ExecutiveVerdict.Contains("期限结构") || res.ExecutiveVerdict.Contains("DNS-ATSM") || res.ExecutiveVerdict.Contains("蝶式凸性"),
            "Verdict should mention Citadel, Millennium, term structure, or butterfly convexity");
    }

    [TestMethod]
    public void Test_Phase53_MultiPodShapleyShadowPricing()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateMultiPodShapleyShadowPricing(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.PodItems.Count); // 4 Pod business lines
        Assert.IsTrue(res.GlobalInternalCapitalNettingGainBps > 0m, "Global internal capital netting gain bps should be positive");
        Assert.IsTrue(res.MeanLiquidityShadowPriceBps > 0m, "Mean liquidity shadow price bps should be positive");
        Assert.IsTrue(res.CooperativeGameEfficiencyRatioPct > 0m && res.CooperativeGameEfficiencyRatioPct <= 100m,
            "Cooperative game efficiency ratio pct should be in (0, 100]");
        Assert.IsTrue(res.SystemicCrowdingDampeningIndex > 0m, "Systemic crowding dampening index should be positive");

        decimal totalRebalanced = res.PodItems.Sum(p => p.TargetRebalancedCapitalPct);
        Assert.IsTrue(Math.Abs(totalRebalanced - 100.0m) <= 1.0m, "Target rebalanced capital should sum to approximately 100%");

        foreach (var item in res.PodItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.PodCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.PodName));
            Assert.IsTrue(item.AllocatedCapitalPct > 0m);
            Assert.IsTrue(item.ShapleyMarginalContributionPct > 0m);
            Assert.IsTrue(item.LiquidityShadowPriceCostBps > 0m);
            Assert.IsTrue(item.DiversificationGainRatio > 0m);
            Assert.IsTrue(item.DynamicLeverageMultiplier > 0m);
            Assert.IsTrue(item.TargetRebalancedCapitalPct > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.PodArbitrationBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.CapitalArbitrationAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Point72") || res.ExecutiveVerdict.Contains("Citadel") || res.ExecutiveVerdict.Contains("Shapley") || res.ExecutiveVerdict.Contains("影子") || res.ExecutiveVerdict.Contains("Pod"),
            "Verdict should mention Point72, Citadel, Shapley, shadow pricing, or Pod");
    }

    [TestMethod]
    public void Test_Phase53_OllivierRicciCurvaturePersistentHomology()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateOllivierRicciCurvaturePersistentHomology(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.TopologicalItems.Count);
        Assert.IsTrue(res.GlobalRicciFlowDenoisingAlphaBps > 0m, "Global Ricci flow denoising alpha bps should be positive");
        Assert.IsTrue(res.MeanGraphOllivierRicciCurvature >= -1.0m && res.MeanGraphOllivierRicciCurvature <= 1.0m,
            "Mean graph Ollivier-Ricci curvature should be in [-1, 1]");
        Assert.IsTrue(res.MacroPhaseTransitionCoherence > 0m && res.MacroPhaseTransitionCoherence <= 100m,
            "Macro phase transition coherence should be in (0, 100]");
        Assert.IsTrue(res.PersistentHomologyCycleDensity > 0m, "Persistent homology cycle density should be positive");

        foreach (var item in res.TopologicalItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.NodeOllivierRicciCurvature >= -1.0m && item.NodeOllivierRicciCurvature <= 1.0m);
            Assert.IsTrue(item.RicciFlowDenoisingGainBps > 0m);
            Assert.IsTrue(item.ZeroBettiClusterLifespan > 0m);
            Assert.IsTrue(item.OneBettiCyclePersistence > 0m);
            Assert.IsTrue(item.TopologicalPhaseCoherenceScore > 0m && item.TopologicalPhaseCoherenceScore <= 100m);
            Assert.IsFalse(string.IsNullOrEmpty(item.RicciManifoldBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.TopologicalAlphaAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("D.E. Shaw") || res.ExecutiveVerdict.Contains("WorldQuant") || res.ExecutiveVerdict.Contains("Ricci") || res.ExecutiveVerdict.Contains("流形") || res.ExecutiveVerdict.Contains("同调"),
            "Verdict should mention D.E. Shaw, WorldQuant, Ricci, Manifold, or Homology");
    }

    [TestMethod]
    public void Test_Phase53_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.JumpDiffusionAffineMarketMaking, "JumpDiffusionAffineMarketMaking should be populated in PortfolioResult");
        Assert.IsNotNull(result.AffineArbitrageFreeTermStructure, "AffineArbitrageFreeTermStructure should be populated in PortfolioResult");
        Assert.IsNotNull(result.MultiPodShapleyShadowPricing, "MultiPodShapleyShadowPricing should be populated in PortfolioResult");
        Assert.IsNotNull(result.OllivierRicciCurvaturePersistentHomology, "OllivierRicciCurvaturePersistentHomology should be populated in PortfolioResult");

        // Verify HTML Export includes Phase 53 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("一百一十四、Jump Trading & Optiver: 连续时间仿射跳跃-扩散限价簿做市与自适应逆向选择免受损控制引擎 (Phase 53)"), "HTML should contain Section 114");
        Assert.IsTrue(html.Contains("一百一十五、Citadel Global Fixed Income & Millennium Macro: 多因子无套利高斯仿射动态期限结构与曲率相对价值套利引擎 (Phase 53)"), "HTML should contain Section 115");
        Assert.IsTrue(html.Contains("一百一十六、Point72 & Citadel Multi-Strategy: 多 Pod 合作博弈 Shapley-Owen 边际分散贡献与动态流动性影子定价仲裁引擎 (Phase 53)"), "HTML should contain Section 116");
        Assert.IsTrue(html.Contains("一百一十七、D.E. Shaw & WorldQuant: 非欧流形 Ollivier-Ricci 拓扑曲率流相变相干与高阶持久同调过滤引擎 (Phase 53)"), "HTML should contain Section 117");
        Assert.IsTrue(html.Contains("毒性跳跃规避总增益"), "HTML should contain Section 114 keywords");
        Assert.IsTrue(html.Contains("蝶式凸性套利总 Alpha"), "HTML should contain Section 115 keywords");
        Assert.IsTrue(html.Contains("内部资金撮合节约增益"), "HTML should contain Section 116 keywords");
        Assert.IsTrue(html.Contains("Ricci 流去噪全局 Alpha"), "HTML should contain Section 117 keywords");
    }
}
