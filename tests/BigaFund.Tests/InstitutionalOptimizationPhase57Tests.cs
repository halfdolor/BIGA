using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase57Tests
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
        var rng = new Random(777);
        var returns = new List<decimal>();
        for (int i = 0; i < 250; i++)
        {
            returns.Add((decimal)(rng.NextDouble() * 0.04 - 0.018));
        }
        return returns;
    }

    [TestMethod]
    public void Test_Phase57_MalliavinRoughVolatilityGreeks()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateMalliavinRoughVolatilityGreeks(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.MalliavinItems.Count);
        Assert.IsTrue(res.GlobalHurstExponentH > 0m && res.GlobalHurstExponentH < 1.0m,
            "Global Hurst exponent should be within (0, 1)");
        Assert.IsTrue(res.GlobalMalliavinVolgaCurvature > 0m, "Volga curvature should be positive");
        Assert.IsTrue(res.AverageRoughTailDefensePct > 0m && res.AverageRoughTailDefensePct <= 100m,
            "Average rough tail defense pct should be within (0, 100]");
        Assert.IsTrue(res.TotalMalliavinHedgingAlphaBps > 0m, "Total Malliavin hedging alpha should be positive");

        foreach (var item in res.MalliavinItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.HurstRoughnessParameterH > 0m && item.HurstRoughnessParameterH < 1.0m);
            Assert.IsTrue(item.RoughVolOfVol > 0m);
            Assert.IsTrue(item.MalliavinNoiseFreeVega > 0m);
            Assert.IsTrue(item.MalliavinVolgaCurvature > 0m);
            Assert.IsTrue(item.RoughPathTailDrawdownReductionPct > 0m && item.RoughPathTailDrawdownReductionPct <= 100m);
            Assert.IsTrue(item.MalliavinHedgingAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.RoughVolRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.RoughVolAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Two Sigma") || res.ExecutiveVerdict.Contains("Citadel") || res.ExecutiveVerdict.Contains("马利亚温") || res.ExecutiveVerdict.Contains("粗糙波动率"),
            "Verdict should mention Two Sigma, Citadel, Malliavin, or Rough Volatility");
    }

    [TestMethod]
    public void Test_Phase57_QuantumLindbladDecoherenceStatArb()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateQuantumLindbladDecoherenceStatArb(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.QuantumItems.Count);
        Assert.IsTrue(res.GlobalQuantumStatePurity > 0m && res.GlobalQuantumStatePurity <= 1.0m,
            "Quantum state purity should be within (0, 1]");
        Assert.IsTrue(res.GlobalVonNeumannEntropy >= 0m, "Von Neumann entropy should be non-negative");
        Assert.IsTrue(res.AverageDecoherenceHalfLifeMicrosec > 0m, "Decoherence half-life should be positive");
        Assert.IsTrue(res.TotalQuantumStatArbAlphaBps > 0m, "Total quantum stat arb alpha should be positive");

        foreach (var item in res.QuantumItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.QuantumStatePurity > 0m && item.QuantumStatePurity <= 1.0m);
            Assert.IsTrue(item.VonNeumannEntropy >= 0m);
            Assert.IsTrue(item.DecoherenceHalfLifeMicrosec > 0m);
            Assert.IsTrue(item.OffDiagonalCoherenceDegree >= 0m);
            Assert.IsTrue(item.DissipativeJumpIntensityGamma > 0m);
            Assert.IsTrue(item.QuantumStatArbAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.QuantumRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.QuantumStatArbAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Renaissance") || res.ExecutiveVerdict.Contains("Jump") || res.ExecutiveVerdict.Contains("Lindblad") || res.ExecutiveVerdict.Contains("量子") || res.ExecutiveVerdict.Contains("相干"),
            "Verdict should mention Renaissance, Jump, Lindblad, Quantum, or Coherence");
    }

    [TestMethod]
    public void Test_Phase57_MeanFieldGameCrowdingDecoupling()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateMeanFieldGameCrowdingDecoupling(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.MfgItems.Count);
        Assert.IsTrue(res.GlobalCrowdingPressureIndex > 0m && res.GlobalCrowdingPressureIndex <= 100m,
            "Global CPI should be within (0, 100]");
        Assert.IsTrue(res.FpkHjbConvergenceResidual > 0m, "FPK-HJB residual should be positive");
        Assert.IsTrue(res.SystemicDoomLoopReductionPct > 0m && res.SystemicDoomLoopReductionPct <= 100m,
            "Systemic doom loop reduction pct should be within (0, 100]");
        Assert.IsTrue(res.TotalMfgRobustAlphaBps > 0m, "Total MFG robust alpha should be positive");

        decimal totalMfgWeight = 0m;
        foreach (var item in res.MfgItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.CrowdingPressureIndexCPI > 0m);
            Assert.IsTrue(item.NashEquilibriumDriftBps < 0m);
            Assert.IsTrue(item.FireSaleCascadeVulnerabilityPct > 0m);
            Assert.IsTrue(item.MeanFieldDecoupledTargetWeight > 0m);
            Assert.IsTrue(item.DoomLoopDefenseBufferPct > 0m);
            Assert.IsTrue(item.MfgGameTheoreticAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.MfgRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.MfgDecouplingAdvice));
            totalMfgWeight += item.MeanFieldDecoupledTargetWeight;
        }

        Assert.IsTrue(Math.Abs(totalMfgWeight - 100m) < 1.0m, $"Decoupled target weights should sum close to 100%, actual: {totalMfgWeight}");

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Millennium") || res.ExecutiveVerdict.Contains("Point72") || res.ExecutiveVerdict.Contains("均值场") || res.ExecutiveVerdict.Contains("博弈") || res.ExecutiveVerdict.Contains("踩踏"),
            "Verdict should mention Millennium, Point72, Mean-Field, Game, or Crowding");
    }

    [TestMethod]
    public void Test_Phase57_ThermodynamicFisherRaoGeodesicRegime()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateThermodynamicFisherRaoGeodesicRegime(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.ThermodynamicItems.Count);
        Assert.IsTrue(res.GlobalFisherRaoGeodesicDistance > 0m, "Fisher-Rao distance should be positive");
        Assert.IsTrue(res.GlobalEntropyProductionRate > 0m, "Entropy production rate should be positive");
        Assert.IsTrue(res.AverageTurnoverDragSavingPct > 0m && res.AverageTurnoverDragSavingPct <= 100m,
            "Turnover drag saving should be within (0, 100]");
        Assert.IsTrue(res.TotalGeodesicMacroAlphaBps > 0m, "Geodesic macro alpha should be positive");

        decimal totalGeodesicWeight = 0m;
        foreach (var item in res.ThermodynamicItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.FisherRaoGeodesicDistance > 0m);
            Assert.IsTrue(item.EntropyProductionRateMEPR > 0m);
            Assert.IsTrue(item.OnsagerKineticFrictionBps > 0m);
            Assert.IsTrue(item.RiemannianCurvatureScalarR < 0m);
            Assert.IsTrue(item.GeodesicSmoothTargetWeight > 0m);
            Assert.IsTrue(item.TurnoverDragReductionPct > 0m);
            Assert.IsTrue(item.GeodesicMacroAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.FisherRaoBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.GeodesicAdvice));
            totalGeodesicWeight += item.GeodesicSmoothTargetWeight;
        }

        Assert.IsTrue(Math.Abs(totalGeodesicWeight - 100m) < 1.0m, $"Geodesic smooth target weights should sum close to 100%, actual: {totalGeodesicWeight}");

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Bridgewater") || res.ExecutiveVerdict.Contains("AQR") || res.ExecutiveVerdict.Contains("Fisher-Rao") || res.ExecutiveVerdict.Contains("测地线") || res.ExecutiveVerdict.Contains("热力学"),
            "Verdict should mention Bridgewater, AQR, Fisher-Rao, Geodesic, or Thermodynamics");
    }

    [TestMethod]
    public void Test_Phase57_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.MalliavinRoughVolatilityGreeks, "MalliavinRoughVolatilityGreeks should be populated in PortfolioResult");
        Assert.IsNotNull(result.QuantumLindbladDecoherenceStatArb, "QuantumLindbladDecoherenceStatArb should be populated in PortfolioResult");
        Assert.IsNotNull(result.MeanFieldGameCrowdingDecoupling, "MeanFieldGameCrowdingDecoupling should be populated in PortfolioResult");
        Assert.IsNotNull(result.ThermodynamicFisherRaoGeodesicRegime, "ThermodynamicFisherRaoGeodesicRegime should be populated in PortfolioResult");

        // Verify HTML Export includes Phase 57 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("一百三十、Two Sigma & Citadel Securities: 连续时间马利亚温随机变分微积分与粗糙波动率泛函高阶 Greeks 深度对冲引擎 (Phase 57)"), "HTML should contain Section 130");
        Assert.IsTrue(html.Contains("一百三十一、Renaissance Technologies & Jump Trading: 开放量子耗散系统 Lindblad 主方程与微观相干态密度矩阵统计套利引擎 (Phase 57)"), "HTML should contain Section 131");
        Assert.IsTrue(html.Contains("一百三十二、Millennium Management & Point72: 连续时间多主体均值场博弈 (Mean-Field Games) 与 FPK-HJB 拥挤踩踏解耦优化引擎 (Phase 57)"), "HTML should contain Section 132");
        Assert.IsTrue(html.Contains("一百三十三、Bridgewater Associates & AQR Capital: 非平衡态热力学最大熵产生与信息几何 Fisher-Rao 测地线宏观流形平滑轮动引擎 (Phase 57)"), "HTML should contain Section 133");
        Assert.IsTrue(html.Contains("全局粗糙赫斯特指数"), "HTML should contain Section 130 keywords");
        Assert.IsTrue(html.Contains("全局微观量子态纯度"), "HTML should contain Section 131 keywords");
        Assert.IsTrue(html.Contains("全局内生拥挤压力指数"), "HTML should contain Section 132 keywords");
        Assert.IsTrue(html.Contains("Fisher-Rao 测地线距离"), "HTML should contain Section 133 keywords");
    }
}
