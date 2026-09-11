using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase55Tests
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
    public void Test_Phase55_RandomMatrixLocalSpectralShrinkage()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateRandomMatrixLocalSpectralShrinkage(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.SpectralItems.Count);
        Assert.IsTrue(res.GlobalRmtSignalToNoiseRatioGain > 0m, "Global RMT SNR gain should be positive");
        Assert.IsTrue(res.MarchenkoPasturUpperBoundRatio > 0m, "MP upper bound ratio should be positive");
        Assert.IsTrue(res.SpikeFactorCount >= 1, "Spike factor count should be at least 1");
        Assert.IsTrue(res.OptimalShrinkageIntensityPct > 0m && res.OptimalShrinkageIntensityPct <= 100m,
            "Optimal shrinkage intensity should be in (0, 100]");

        foreach (var item in res.SpectralItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.RawSampleVariance > 0m);
            Assert.IsTrue(item.DenoisedSpectralVariance > 0m);
            Assert.IsTrue(item.NoiseFilteringRatioPct > 0m && item.NoiseFilteringRatioPct <= 100m);
            Assert.IsTrue(item.SpikeFactorLoading > 0m);
            Assert.IsTrue(item.LedoitPeitShrinkageWeight > 0m && item.LedoitPeitShrinkageWeight <= 1m);
            Assert.IsTrue(item.SpectralDenoisedAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.RmtRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.RmtPortfolioAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Renaissance") || res.ExecutiveVerdict.Contains("Two Sigma") || res.ExecutiveVerdict.Contains("随机矩阵") || res.ExecutiveVerdict.Contains("RMT") || res.ExecutiveVerdict.Contains("谱去噪"),
            "Verdict should mention Renaissance, Two Sigma, Random Matrix, RMT, or Spectral Denoising");
    }

    [TestMethod]
    public void Test_Phase55_MultivariateHawkesToxicityCascade()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateMultivariateHawkesToxicityCascade(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.CascadeItems.Count);
        Assert.IsTrue(res.GlobalHawkesBranchingRatio > 0m, "Global Hawkes branching ratio should be positive");
        Assert.IsTrue(res.OrderFlowToxicityScore > 0m && res.OrderFlowToxicityScore <= 100m,
            "Order flow toxicity score should be in (0, 100]");
        Assert.IsTrue(res.FlashCrashCascadeVulnerabilityPct > 0m && res.FlashCrashCascadeVulnerabilityPct <= 100m,
            "Flash crash cascade vulnerability should be in (0, 100]");
        Assert.IsTrue(res.MicrostructureAntidoteAlphaBps > 0m, "Microstructure antidote alpha bps should be positive");

        foreach (var item in res.CascadeItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.SelfExcitationAlpha > 0m);
            Assert.IsTrue(item.CrossExcitationBeta >= 0m);
            Assert.IsTrue(item.BranchingSpectralRadius > 0m);
            Assert.IsTrue(item.MarkedToxicityIntensity > 0m);
            Assert.IsTrue(item.CascadeVulnerabilityPct > 0m);
            Assert.IsTrue(item.HawkesExecutionAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.HawkesStateBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.HawkesToxicityAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Citadel") || res.ExecutiveVerdict.Contains("Jump Trading") || res.ExecutiveVerdict.Contains("霍克斯") || res.ExecutiveVerdict.Contains("Hawkes") || res.ExecutiveVerdict.Contains("毒性"),
            "Verdict should mention Citadel, Jump Trading, Hawkes, or Toxicity");
    }

    [TestMethod]
    public void Test_Phase55_MultifractalHurstSurfaceDefense()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateMultifractalHurstSurfaceDefense(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.MultifractalItems.Count);
        Assert.IsTrue(res.LongRangeMemoryHurstExponent > 0m, "Hurst exponent should be positive");
        Assert.IsTrue(res.GlobalMultifractalSpectrumWidth > 0m, "Global multifractal spectrum width should be positive");
        Assert.IsTrue(res.FractalAsymmetryDegree != 0m, "Fractal asymmetry degree should be non-zero");
        Assert.IsTrue(res.DualExtremumDrawdownDefenseRatio > 0m && res.DualExtremumDrawdownDefenseRatio <= 100m,
            "Dual extremum drawdown defense ratio should be in (0, 100]");

        foreach (var item in res.MultifractalItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.HurstExponentH > 0m);
            Assert.IsTrue(item.MultifractalWidthDeltaAlpha > 0m);
            Assert.IsTrue(item.SingularityModeAlphaZero > 0m);
            Assert.IsTrue(item.PersistenceMemoryScore > 0m);
            Assert.IsTrue(item.DualDrawdownDefenseGainPct > 0m);
            Assert.IsTrue(item.FractalAntiFragileAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.MultifractalBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.MultifractalDefenseAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Bridgewater") || res.ExecutiveVerdict.Contains("AQR") || res.ExecutiveVerdict.Contains("赫斯特") || res.ExecutiveVerdict.Contains("分形") || res.ExecutiveVerdict.Contains("Hurst"),
            "Verdict should mention Bridgewater, AQR, Hurst, or Multifractal");
    }

    [TestMethod]
    public void Test_Phase55_MultiAgentAdversarialPolicyDistillation()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateMultiAgentAdversarialPolicyDistillation(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.DistillationItems.Count);
        Assert.IsTrue(res.GlobalAdversarialRobustnessScore > 0m && res.GlobalAdversarialRobustnessScore <= 100m,
            "Global adversarial robustness score should be in (0, 100]");
        Assert.IsTrue(res.NashEquilibriumConvergenceDegree > 0m && res.NashEquilibriumConvergenceDegree <= 100m,
            "Nash equilibrium convergence degree should be in (0, 100]");
        Assert.IsTrue(res.PolicyDistillationFidelityPct > 0m && res.PolicyDistillationFidelityPct <= 100m,
            "Policy distillation fidelity pct should be in (0, 100]");
        Assert.IsTrue(res.DistilledAntiSqueezeAlphaBps > 0m, "Distilled anti-squeeze alpha bps should be positive");

        foreach (var item in res.DistillationItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.AdversarialPerturbationRadius > 0m);
            Assert.IsTrue(item.DistillationKlDivergence > 0m);
            Assert.IsTrue(item.MultiAgentNashCooperationScore > 0m);
            Assert.IsTrue(item.AntiCrowdingResiliencePct > 0m);
            Assert.IsTrue(item.RobustOptimalWeightPct > 0m);
            Assert.IsTrue(item.DistilledAlphaGainBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.MarlRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.MarlDistillationAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Point72") || res.ExecutiveVerdict.Contains("Millennium") || res.ExecutiveVerdict.Contains("MARL") || res.ExecutiveVerdict.Contains("对抗") || res.ExecutiveVerdict.Contains("蒸馏"),
            "Verdict should mention Point72, Millennium, MARL, Adversarial, or Distillation");
    }

    [TestMethod]
    public void Test_Phase55_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.RandomMatrixLocalSpectralShrinkage, "RandomMatrixLocalSpectralShrinkage should be populated in PortfolioResult");
        Assert.IsNotNull(result.MultivariateHawkesToxicityCascade, "MultivariateHawkesToxicityCascade should be populated in PortfolioResult");
        Assert.IsNotNull(result.MultifractalHurstSurfaceDefense, "MultifractalHurstSurfaceDefense should be populated in PortfolioResult");
        Assert.IsNotNull(result.MultiAgentAdversarialPolicyDistillation, "MultiAgentAdversarialPolicyDistillation should be populated in PortfolioResult");

        // Verify HTML Export includes Phase 55 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("一百二十二、Renaissance Technologies & Two Sigma: 高维随机矩阵理论局部谱去噪与收缩协方差重构引擎 (Phase 55)"), "HTML should contain Section 122");
        Assert.IsTrue(html.Contains("一百二十三、Citadel Securities & Jump Trading: 微观分形霍克斯自激互激订单流毒性与闪崩级联预警引擎 (Phase 55)"), "HTML should contain Section 123");
        Assert.IsTrue(html.Contains("一百二十四、Bridgewater Associates & AQR Capital: 多资产非线性分形波动率记忆与赫斯特表面跨周期极值回撤对偶防御引擎 (Phase 55)"), "HTML should contain Section 124");
        Assert.IsTrue(html.Contains("一百二十五、Point72 & Millennium Management: 多代理强化学习非稳态微观策略博弈与对抗性鲁棒策略蒸馏引擎 (Phase 55)"), "HTML should contain Section 125");
        Assert.IsTrue(html.Contains("RMT 谱去噪信噪比增益"), "HTML should contain Section 122 keywords");
        Assert.IsTrue(html.Contains("霍克斯分支比率谱半径"), "HTML should contain Section 123 keywords");
        Assert.IsTrue(html.Contains("长程记忆赫斯特指数 H"), "HTML should contain Section 124 keywords");
        Assert.IsTrue(html.Contains("全局对抗鲁棒性评分"), "HTML should contain Section 125 keywords");
    }
}
