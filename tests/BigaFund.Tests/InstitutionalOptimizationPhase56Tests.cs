using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase56Tests
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
        var rng = new Random(666);
        var returns = new List<decimal>();
        for (int i = 0; i < 250; i++)
        {
            returns.Add((decimal)(rng.NextDouble() * 0.04 - 0.018));
        }
        return returns;
    }

    [TestMethod]
    public void Test_Phase56_LobMicroPriceMartingaleVacuumPenetration()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateLobMicroPriceMartingaleVacuumPenetration(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.LobItems.Count);
        Assert.IsTrue(res.AverageVacuumPenetrationPct > 0m && res.AverageVacuumPenetrationPct <= 100m,
            "Average vacuum penetration pct should be within (0, 100]");
        Assert.IsTrue(res.PassiveExecutionSpreadSavingBps > 0m, "Passive spread saving should be positive");
        Assert.IsTrue(res.TotalMarketMakingAlphaBps > 0m, "Total market making alpha bps should be positive");

        foreach (var item in res.LobItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.SpreadBps > 0m);
            Assert.IsTrue(item.DepthImbalanceRatio >= -1m && item.DepthImbalanceRatio <= 1m);
            Assert.IsTrue(item.InstantaneousVacuumPenetrationPct > 0m && item.InstantaneousVacuumPenetrationPct <= 100m);
            Assert.IsTrue(item.OptimalQuoteDepthOffsetBps > 0m);
            Assert.IsTrue(item.MicrostructureExecutionAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.LobRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.LobExecutionAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Jane Street") || res.ExecutiveVerdict.Contains("Citadel") || res.ExecutiveVerdict.Contains("LOB") || res.ExecutiveVerdict.Contains("微观价格") || res.ExecutiveVerdict.Contains("真空"),
            "Verdict should mention Jane Street, Citadel, LOB, MicroPrice, or Vacuum");
    }

    [TestMethod]
    public void Test_Phase56_MultidimensionalLevyItoJumpDiffusion()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateMultidimensionalLevyItoJumpDiffusion(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.JumpItems.Count);
        Assert.IsTrue(res.GlobalCoJumpArrivalIntensity > 0m, "Global co-jump arrival intensity should be positive");
        Assert.IsTrue(res.GlobalJumpVariationRatioPct > 0m && res.GlobalJumpVariationRatioPct <= 100m,
            "Global jump variation ratio should be within (0, 100]");
        Assert.IsTrue(res.GlobalJumpTailDrawdownReductionPct > 0m && res.GlobalJumpTailDrawdownReductionPct <= 100m,
            "Global jump tail drawdown reduction should be within (0, 100]");
        Assert.IsTrue(res.GlobalLevyJumpAlphaBps > 0m, "Global levy jump alpha bps should be positive");

        foreach (var item in res.JumpItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.ContinuousDiffusionVolPct > 0m);
            Assert.IsTrue(item.JumpArrivalIntensityLambda > 0m);
            Assert.IsTrue(item.DownsideJumpAsymmetryRatio > 0m);
            Assert.IsTrue(item.JumpVariationRatioPct > 0m && item.JumpVariationRatioPct <= 100m);
            Assert.IsTrue(item.JumpCushionHedgeMultiplier >= 1.0m);
            Assert.IsTrue(item.DownsideJumpTailDefensePct > 0m);
            Assert.IsTrue(item.LevyJumpAntiTailAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.LevyJumpBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.LevyJumpAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("D.E. Shaw") || res.ExecutiveVerdict.Contains("Two Sigma") || res.ExecutiveVerdict.Contains("列维") || res.ExecutiveVerdict.Contains("跳跃") || res.ExecutiveVerdict.Contains("Levy"),
            "Verdict should mention D.E. Shaw, Two Sigma, Levy, Jump, or Diffusion");
    }

    [TestMethod]
    public void Test_Phase56_HypergraphSpinGlassFrustrationAnnealing()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateHypergraphSpinGlassFrustrationAnnealing(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.HypergraphItems.Count);
        Assert.IsTrue(res.GlobalFrustrationEnergyDensity >= 0m, "Global frustration energy density should be non-negative");
        Assert.IsTrue(res.MeanHyperedgeInteractionDegree >= 1.0m, "Mean hyperedge interaction degree should be >= 1");
        Assert.IsTrue(res.GlobalSystemicResonanceVulnerabilityPct > 0m && res.GlobalSystemicResonanceVulnerabilityPct <= 100m,
            "Resonance vulnerability reduction should be in (0, 100]");
        Assert.IsTrue(res.HypergraphAnnealingAlphaBps > 0m, "Hypergraph annealing alpha bps should be positive");

        decimal totalAnnealedWeight = 0m;
        foreach (var item in res.HypergraphItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.HyperedgeDegree >= 1);
            Assert.IsTrue(item.SpinGlassFrustrationDensity >= 0m);
            Assert.IsTrue(item.EdwardsAndersonOrderParameter >= 0m);
            Assert.IsTrue(item.AnnealedRobustOptimalWeight > 0m);
            Assert.IsTrue(item.SystemicResonanceReductionPct > 0m);
            Assert.IsTrue(item.HypergraphDeclusteringAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.HypergraphBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.HypergraphAnnealingAdvice));
            totalAnnealedWeight += item.AnnealedRobustOptimalWeight;
        }

        Assert.IsTrue(Math.Abs(totalAnnealedWeight - 100m) < 1.0m, $"Annealed weights should sum close to 100%, actual: {totalAnnealedWeight}");

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Renaissance") || res.ExecutiveVerdict.Contains("Millennium") || res.ExecutiveVerdict.Contains("超图") || res.ExecutiveVerdict.Contains("自旋玻璃") || res.ExecutiveVerdict.Contains("退火"),
            "Verdict should mention Renaissance, Millennium, Hypergraph, Spin Glass, or Annealing");
    }

    [TestMethod]
    public void Test_Phase56_SovereignDebtCycleDeleveragingImmunity()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateSovereignDebtCycleDeleveragingImmunity(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.DebtItems.Count);
        Assert.IsFalse(string.IsNullOrEmpty(res.CurrentLongTermDebtSupercyclePhase));
        Assert.IsTrue(res.GlobalDeleveragingVulnerabilityScore > 0m && res.GlobalDeleveragingVulnerabilityScore <= 100m,
            "Global DVI should be in (0, 100]");
        Assert.IsTrue(res.MarkovRegimeTransitionEntropy > 0m, "Markov regime transition entropy should be positive");
        Assert.IsTrue(res.SupercycleMacroImmunityAlphaBps > 0m, "Supercycle macro immunity alpha bps should be positive");

        decimal totalDalioWeight = 0m;
        foreach (var item in res.DebtItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.MacroDebtRegimeState));
            Assert.IsTrue(item.DeleveragingVulnerabilityIndex > 0m);
            Assert.IsTrue(item.SovereignMonetaryDebasementExposure > 0m);
            Assert.IsTrue(item.StagflationaryConvexityBufferPct > 0m);
            Assert.IsTrue(item.DalioImmunityTargetWeight > 0m);
            Assert.IsTrue(item.DebtCycleMacroAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.MacroDebtImmunityBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.SovereignDebtAdvice));
            totalDalioWeight += item.DalioImmunityTargetWeight;
        }

        Assert.IsTrue(Math.Abs(totalDalioWeight - 100m) < 1.0m, $"Dalio immunity target weights should sum close to 100%, actual: {totalDalioWeight}");

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Bridgewater") || res.ExecutiveVerdict.Contains("Aladdin") || res.ExecutiveVerdict.Contains("达利欧") || res.ExecutiveVerdict.Contains("主权债务") || res.ExecutiveVerdict.Contains("去杠杆"),
            "Verdict should mention Bridgewater, Aladdin, Dalio, Sovereign Debt, or Deleveraging");
    }

    [TestMethod]
    public void Test_Phase56_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.LobMicroPriceMartingaleVacuumPenetration, "LobMicroPriceMartingaleVacuumPenetration should be populated in PortfolioResult");
        Assert.IsNotNull(result.MultidimensionalLevyItoJumpDiffusion, "MultidimensionalLevyItoJumpDiffusion should be populated in PortfolioResult");
        Assert.IsNotNull(result.HypergraphSpinGlassFrustrationAnnealing, "HypergraphSpinGlassFrustrationAnnealing should be populated in PortfolioResult");
        Assert.IsNotNull(result.SovereignDebtCycleDeleveragingImmunity, "SovereignDebtCycleDeleveragingImmunity should be populated in PortfolioResult");

        // Verify HTML Export includes Phase 56 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("一百二十六、Jane Street & Citadel Securities: 全档限价订单簿 (LOB) 微观价格扩散鞅测度变换与即时流动性真空渗透控制引擎 (Phase 56)"), "HTML should contain Section 126");
        Assert.IsTrue(html.Contains("一百二十七、D.E. Shaw & Two Sigma: 连续时间多维非高斯列维-伊藤跳跃扩散与极值共跳曲率对冲引擎 (Phase 56)"), "HTML should contain Section 127");
        Assert.IsTrue(html.Contains("一百二十八、Renaissance Technologies & Millennium Management: 高阶拓扑超图场论与自旋玻璃阻挫能量退火解聚引擎 (Phase 56)"), "HTML should contain Section 128");
        Assert.IsTrue(html.Contains("一百二十九、Bridgewater Associates & BlackRock Aladdin: 达利欧长期主权债务大周期与超级去杠杆马尔可夫状态免疫引擎 (Phase 56)"), "HTML should contain Section 129");
        Assert.IsTrue(html.Contains("微观价格鞅漂移偏差"), "HTML should contain Section 126 keywords");
        Assert.IsTrue(html.Contains("多维共跳年化到达强度"), "HTML should contain Section 127 keywords");
        Assert.IsTrue(html.Contains("超图自旋玻璃阻挫能量"), "HTML should contain Section 128 keywords");
        Assert.IsTrue(html.Contains("主导长期债务超级周期"), "HTML should contain Section 129 keywords");
    }
}
