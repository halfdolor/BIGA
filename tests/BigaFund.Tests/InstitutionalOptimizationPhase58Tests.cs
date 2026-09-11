using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase58Tests
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
        var rng = new Random(888);
        var returns = new List<decimal>();
        for (int i = 0; i < 250; i++)
        {
            returns.Add((decimal)(rng.NextDouble() * 0.04 - 0.018));
        }
        return returns;
    }

    [TestMethod]
    public void Test_Phase58_GlostenMilgromAdverseSelection()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateGlostenMilgromAdverseSelection(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.GlostenItems.Count);
        Assert.IsTrue(res.GlobalInformedTraderRatio > 0m && res.GlobalInformedTraderRatio <= 1.0m,
            "Global informed trader ratio should be within (0, 1]");
        Assert.IsTrue(res.GlobalAdverseSelectionSpreadBps > 0m, "Global adverse selection spread should be positive");
        Assert.IsTrue(res.AverageSlippageReductionPct > 0m && res.AverageSlippageReductionPct <= 100m,
            "Average slippage reduction should be within (0, 100]");
        Assert.IsTrue(res.TotalGlostenMilgromAlphaBps > 0m, "Total Glosten-Milgrom alpha should be positive");

        foreach (var item in res.GlostenItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.InformedTraderRatioAlpha > 0m && item.InformedTraderRatioAlpha <= 1.0m);
            Assert.IsTrue(item.PosteriorHighStateBeliefPi > 0m && item.PosteriorHighStateBeliefPi <= 1.0m);
            Assert.IsTrue(item.AdverseSelectionSpreadBps > 0m);
            Assert.IsTrue(item.InventoryJumpExposurePct > 0m);
            Assert.IsTrue(item.ToxicitySlippageReductionPct > 0m && item.ToxicitySlippageReductionPct <= 100m);
            Assert.IsTrue(item.GlostenMilgromAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.AdverseSelectionRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.GlostenMilgromAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Jane Street") || res.ExecutiveVerdict.Contains("Citadel") || res.ExecutiveVerdict.Contains("Glosten") || res.ExecutiveVerdict.Contains("逆向选择") || res.ExecutiveVerdict.Contains("贝叶斯"),
            "Verdict should mention Jane Street, Citadel, Glosten, Adverse Selection, or Bayesian");
    }

    [TestMethod]
    public void Test_Phase58_TsallisNonextensiveSingularSpectrum()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateTsallisNonextensiveSingularSpectrum(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.TsallisItems.Count);
        Assert.IsTrue(res.GlobalNonextensiveParameterQ > 0m, "Global nonextensive parameter q should be positive");
        Assert.IsTrue(res.GlobalSingularSpectrumWidth > 0m, "Global singular spectrum width should be positive");
        Assert.IsTrue(res.AverageAvalancheDefensePct > 0m && res.AverageAvalancheDefensePct <= 100m,
            "Average avalanche defense pct should be within (0, 100]");
        Assert.IsTrue(res.TotalTsallisAdaptiveAlphaBps > 0m, "Total Tsallis adaptive alpha should be positive");

        foreach (var item in res.TsallisItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.NonextensiveParameterQ > 0m);
            Assert.IsTrue(item.TsallisEntropySq > 0m);
            Assert.IsTrue(item.SingularSpectrumWidthDeltaAlpha > 0m);
            Assert.IsTrue(item.PhaseTransitionCorrelationLengthXi > 0m);
            Assert.IsTrue(item.AvalancheCollapseDefensePct > 0m && item.AvalancheCollapseDefensePct <= 100m);
            Assert.IsTrue(item.TsallisAdaptiveAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.TsallisRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.TsallisAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Renaissance") || res.ExecutiveVerdict.Contains("D.E. Shaw") || res.ExecutiveVerdict.Contains("Tsallis") || res.ExecutiveVerdict.Contains("奇异谱") || res.ExecutiveVerdict.Contains("非广延"),
            "Verdict should mention Renaissance, D.E. Shaw, Tsallis, Singular Spectrum, or Nonextensive");
    }

    [TestMethod]
    public void Test_Phase58_ViscousMemoryOptimalExecution()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateViscousMemoryOptimalExecution(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.ViscousItems.Count);
        Assert.IsTrue(res.GlobalViscousMemoryDecayGamma > 0m, "Global viscous memory decay gamma should be positive");
        Assert.IsTrue(res.GlobalFredholmDampingRatio > 0m, "Global Fredholm damping ratio should be positive");
        Assert.IsTrue(res.AverageSlippageSavingsPct > 0m && res.AverageSlippageSavingsPct <= 100m,
            "Average slippage savings should be within (0, 100]");
        Assert.IsTrue(res.TotalViscousExecutionAlphaBps > 0m, "Total viscous execution alpha should be positive");

        foreach (var item in res.ViscousItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.ViscousMemoryDecayGamma > 0m);
            Assert.IsTrue(item.FredholmDampingRatio > 0m);
            Assert.IsTrue(item.EulerLagrangeOptimalPace > 0m);
            Assert.IsTrue(item.NonlinearSlippageSavingsPct > 0m && item.NonlinearSlippageSavingsPct <= 100m);
            Assert.IsTrue(item.ViscousExecutionAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.ViscousRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.ViscousAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Two Sigma") || res.ExecutiveVerdict.Contains("Jump") || res.ExecutiveVerdict.Contains("Fredholm") || res.ExecutiveVerdict.Contains("粘性") || res.ExecutiveVerdict.Contains("变分"),
            "Verdict should mention Two Sigma, Jump, Fredholm, Viscous, or Variational");
    }

    [TestMethod]
    public void Test_Phase58_SvarDagCausalInterventionNetwork()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateSvarDagCausalInterventionNetwork(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.CausalItems.Count);
        Assert.IsTrue(res.GlobalCausalNetworkDensity > 0m && res.GlobalCausalNetworkDensity <= 1.0m,
            "Global causal network density should be within (0, 1]");
        Assert.IsTrue(res.GlobalSystemicFragilityIndex > 0m, "Global systemic fragility index should be positive");
        Assert.IsTrue(res.AverageShockImmunityPct > 0m && res.AverageShockImmunityPct <= 100m,
            "Average shock immunity pct should be within (0, 100]");
        Assert.IsTrue(res.TotalAntifragileCausalAlphaBps > 0m, "Total antifragile causal alpha should be positive");

        decimal totalWeight = 0m;
        foreach (var item in res.CausalItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.CausalCentralityScore > 0m);
            Assert.IsTrue(item.CounterfactualShockResponsePct > 0m);
            Assert.IsTrue(item.SystemicCausalFragilityIndex > 0m);
            Assert.IsTrue(item.CausalDecoupledTargetWeight > 0m);
            Assert.IsTrue(item.AntifragileCausalAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.CausalRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.CausalAdvice));
            totalWeight += item.CausalDecoupledTargetWeight;
        }

        Assert.IsTrue(Math.Abs(totalWeight - 100m) < 1.0m, $"Causal decoupled target weights should sum close to 100%, actual: {totalWeight}");

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Bridgewater") || res.ExecutiveVerdict.Contains("Millennium") || res.ExecutiveVerdict.Contains("SVAR") || res.ExecutiveVerdict.Contains("DAG") || res.ExecutiveVerdict.Contains("因果") || res.ExecutiveVerdict.Contains("Pearl"),
            "Verdict should mention Bridgewater, Millennium, SVAR, DAG, Causal, or Pearl");
    }

    [TestMethod]
    public void Test_Phase58_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.GlostenMilgromAdverseSelection, "GlostenMilgromAdverseSelection should be populated in PortfolioResult");
        Assert.IsNotNull(result.TsallisNonextensiveSingularSpectrum, "TsallisNonextensiveSingularSpectrum should be populated in PortfolioResult");
        Assert.IsNotNull(result.ViscousMemoryOptimalExecution, "ViscousMemoryOptimalExecution should be populated in PortfolioResult");
        Assert.IsNotNull(result.SvarDagCausalInterventionNetwork, "SvarDagCausalInterventionNetwork should be populated in PortfolioResult");

        // Verify HTML Export includes Phase 58 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("一百三十四、Jane Street & Citadel Securities: 连续时间 Glosten-Milgrom 贝叶斯信念更新与流动性逆向选择防护 (Phase 58)"), "HTML should contain Section 134");
        Assert.IsTrue(html.Contains("一百三十五、Renaissance Technologies & D.E. Shaw: 非广延统计力学 Tsallis 广义熵与多重分形奇异谱极值崩塌预警 (Phase 58)"), "HTML should contain Section 135");
        Assert.IsTrue(html.Contains("一百三十六、Two Sigma & Jump Trading: 随机非线性粘性阻尼与 Fredholm 记忆核最优变分执行清算 (Phase 58)"), "HTML should contain Section 136");
        Assert.IsTrue(html.Contains("一百三十七、Bridgewater Associates & Millennium Management: 结构向量自回归有向无环图 (SVAR-DAG) 与 Pearl 反事实因果干预宏观网络 (Phase 58)"), "HTML should contain Section 137");
        Assert.IsTrue(html.Contains("全局加权知情交易者比例 α"), "HTML should contain Section 134 keywords");
        Assert.IsTrue(html.Contains("全局有效非广延参数 q_eff"), "HTML should contain Section 135 keywords");
        Assert.IsTrue(html.Contains("全局有效粘性记忆衰减 γ"), "HTML should contain Section 136 keywords");
        Assert.IsTrue(html.Contains("全局 SVAR-DAG 因果网络密度"), "HTML should contain Section 137 keywords");
    }
}
