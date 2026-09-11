using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase50Tests
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
    public void Test_Phase50_VoiculescuFreeProbabilityQrg()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateVoiculescuFreeProbabilityQrg(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.AssetQrgItems.Count);
        Assert.IsTrue(res.VoiculescuFreeNoiseFractionPercent > 0m && res.VoiculescuFreeNoiseFractionPercent <= 100m,
            "Voiculescu free noise fraction percentage should be within (0, 100]");
        Assert.IsTrue(res.QrgEffectiveEnergyScaleLambda > 0m, "QRG energy scale lambda should be positive");
        Assert.IsTrue(res.ConditionNumberCompressionRatio >= 1.0m, "Condition number compression ratio should be >= 1.0");
        Assert.IsTrue(res.SpectralSingularityRepairGainBps > 0m, "Spectral singularity repair gain bps should be positive");

        foreach (var item in res.AssetQrgItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.RawEmpiricalEigenvalue > 0m);
            Assert.IsTrue(item.VoiculescuFreeCumulantR > 0m);
            Assert.IsTrue(item.QrgEnergyScaleLambda > 0m);
            Assert.IsTrue(item.RepairedCleanEigenvalue > 0m);
            Assert.IsTrue(item.ConditionNumberCompression >= 1.0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.SpectralPurityBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.SpectralFilteringAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Renaissance") || res.ExecutiveVerdict.Contains("D.E. Shaw") || res.ExecutiveVerdict.Contains("自由概率") || res.ExecutiveVerdict.Contains("QRG"),
            "Verdict should mention Renaissance, D.E. Shaw, Free Probability, or QRG");
    }

    [TestMethod]
    public void Test_Phase50_ThomCatastropheChaosDynamics()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateThomCatastropheChaosDynamics(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.CatastropheItems.Count);
        Assert.IsTrue(res.PortfolioMaxLyapunovExponent >= 0m, "Portfolio max Lyapunov exponent should be non-negative");
        Assert.IsTrue(res.LyapunovPredictionHorizonDays > 0m, "Lyapunov prediction horizon days should be positive");
        Assert.IsTrue(res.ThomCatastropheBifurcationDistance >= 0m, "Thom catastrophe bifurcation distance should be non-negative");
        Assert.IsTrue(res.SystemicAntiHysteresisBufferPct >= 0m, "Systemic anti-hysteresis buffer pct should be non-negative");

        foreach (var item in res.CatastropheItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.MaxLyapunovExponent >= 0m);
            Assert.IsTrue(item.DistanceToBifurcationSurface >= 0m);
            Assert.IsTrue(item.AntiHysteresisHedgingBufferPct >= 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.CatastropheRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.NonlinearDynamicHedgingAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Citadel") || res.ExecutiveVerdict.Contains("AQR") || res.ExecutiveVerdict.Contains("李雅普诺夫") || res.ExecutiveVerdict.Contains("尖点突变"),
            "Verdict should mention Citadel, AQR, Lyapunov, or Cusp Catastrophe");
    }

    [TestMethod]
    public void Test_Phase50_NeuralSchrodingerBridgeReflectedBsde()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateNeuralSchrodingerBridgeReflectedBsde(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.BridgeBsdeItems.Count);
        Assert.IsTrue(res.SchrodingerBridgeEntropicDistance > 0m, "Schrodinger bridge entropic distance should be positive");
        Assert.IsTrue(res.ReflectedBsdeBoundarySlackMargin >= 0m, "Boundary slack margin should be non-negative");
        Assert.IsTrue(res.SkorokhodReflectionLocalTimeIntensity >= 0m, "Local time intensity should be non-negative");
        Assert.IsTrue(res.OptimalTransitionCostSavingsBps > 0m, "Optimal transition cost savings should be positive");

        foreach (var item in res.BridgeBsdeItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.EntropicOptimalTransportVelocity > 0m);
            Assert.IsTrue(item.ReflectedBsdeBoundarySlack >= 0m);
            Assert.IsTrue(item.SkorokhodLocalTimeIntensity >= 0m);
            Assert.IsTrue(item.DynamicTransitionSavingsBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.BoundarySafetyBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.GenerativeTransitionAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Two Sigma") || res.ExecutiveVerdict.Contains("WorldQuant") || res.ExecutiveVerdict.Contains("薛定谔桥") || res.ExecutiveVerdict.Contains("BSDE"),
            "Verdict should mention Two Sigma, WorldQuant, Schrodinger Bridge, or BSDE");
    }

    [TestMethod]
    public void Test_Phase50_BoltzmannVlasovRelativisticExecution()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateBoltzmannVlasovRelativisticExecution(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.VlasovFieldItems.Count);
        Assert.IsTrue(res.VlasovSelfConsistentFieldPotential > 0m, "Vlasov self consistent field potential should be positive");
        Assert.IsTrue(res.MicrostructuralAcousticSpeed > 0m, "Microstructural acoustic speed should be positive");
        Assert.IsTrue(res.RelativisticCausalHorizonSafetyScore > 0m && res.RelativisticCausalHorizonSafetyScore <= 100m,
            "Relativistic causal horizon safety score should be in (0, 100]");
        Assert.IsTrue(res.AdverseSelectionAlphaProtectionBps > 0m, "Adverse selection alpha protection bps should be positive");

        foreach (var ch in res.VlasovFieldItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(ch.ChannelCode));
            Assert.IsFalse(string.IsNullOrEmpty(ch.ChannelName));
            Assert.IsTrue(ch.PhaseSpaceDensity > 0m);
            Assert.IsTrue(ch.MeanParticleVelocity > 0m);
            Assert.IsTrue(Math.Abs(ch.VlasovSelfConsistentFieldForce) > 0m);
            Assert.IsTrue(ch.AcousticShockwaveSpeed > 0m);
            Assert.IsTrue(ch.RelativisticCausalSafetyScore > 0m && ch.RelativisticCausalSafetyScore <= 100m);
            Assert.IsFalse(string.IsNullOrEmpty(ch.KineticRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(ch.RelativisticQuotingTactics));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Jane Street") || res.ExecutiveVerdict.Contains("Hudson River") || res.ExecutiveVerdict.Contains("HRT") || res.ExecutiveVerdict.Contains("玻尔兹曼") || res.ExecutiveVerdict.Contains("纳什"),
            "Verdict should mention Jane Street, HRT, Boltzmann, or Nash");
    }

    [TestMethod]
    public void Test_Phase50_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.VoiculescuFreeProbabilityQrg, "VoiculescuFreeProbabilityQrg should be populated in PortfolioResult");
        Assert.IsNotNull(result.ThomCatastropheChaosDynamics, "ThomCatastropheChaosDynamics should be populated in PortfolioResult");
        Assert.IsNotNull(result.NeuralSchrodingerBridgeReflectedBsde, "NeuralSchrodingerBridgeReflectedBsde should be populated in PortfolioResult");
        Assert.IsNotNull(result.BoltzmannVlasovRelativisticExecution, "BoltzmannVlasovRelativisticExecution should be populated in PortfolioResult");

        // Verify HTML Export includes Phase 50 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("一百零二、Renaissance Technologies & D.E. Shaw 非对易自由概率论 (Voiculescu Free Probability) 与量子重整化群 (QRG) 高维矩阵奇异谱修复 (Phase 50)"), "HTML should contain Section 102");
        Assert.IsTrue(html.Contains("一百零三、Citadel Global Strategies & AQR Capital Management 宏观随机动力学混沌奇异吸引子 (Lyapunov Exponent Spectrum) 与托姆尖点突变论 (Thom Cusp Catastrophe) 极值跳变防踩踏 (Phase 50)"), "HTML should contain Section 103");
        Assert.IsTrue(html.Contains("一百零四、Two Sigma & WorldQuant 神经薛定谔桥 (Neural Schrödinger Bridge) 熵正则化最优输运与反射倒向随机微分方程 (Reflected BSDE) 资本硬约束动态流动性生成式对冲 (Phase 50)"), "HTML should contain Section 104");
        Assert.IsTrue(html.Contains("一百零五、Jane Street Capital & Hudson River Trading 玻尔兹曼-弗拉索夫 (Boltzmann-Vlasov) 动理学流动性连续体场论与相对论性纳什执行博弈 (Phase 50)"), "HTML should contain Section 105");
        Assert.IsTrue(html.Contains("非对易白噪声滤除比例"), "HTML should contain Section 102 keywords");
        Assert.IsTrue(html.Contains("尖点突变分岔曲面安全距离"), "HTML should contain Section 103 keywords");
        Assert.IsTrue(html.Contains("薛定谔桥熵正则化距离"), "HTML should contain Section 104 keywords");
        Assert.IsTrue(html.Contains("相对论因果视界防抢跑得分"), "HTML should contain Section 105 keywords");
    }
}
