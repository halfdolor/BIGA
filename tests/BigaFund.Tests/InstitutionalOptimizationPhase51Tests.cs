using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase51Tests
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
    public void Test_Phase51_MillenniumConvexPodAllocation()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateMillenniumConvexPodAllocation(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(5, res.PodAllocationItems.Count);
        Assert.IsTrue(res.InternalNettingEfficiencyPercent > 0m && res.InternalNettingEfficiencyPercent <= 100m,
            "Internal netting efficiency should be within (0, 100]");
        Assert.IsTrue(res.CrossPodMarginContagionIndex >= 0m, "Cross Pod margin contagion index should be non-negative");
        Assert.IsTrue(res.DynamicCapitalDragSavingsBps > 0m, "Dynamic capital drag savings bps should be positive");
        Assert.IsTrue(res.SystemicStopOutSlackMargin > 0m, "Systemic stop-out slack margin should be positive");

        foreach (var item in res.PodAllocationItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.PodCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.PodName));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssignedStrategy));
            Assert.IsTrue(item.TargetWeight > 0m);
            Assert.IsTrue(item.RawOrderVolumeBps > 0m);
            Assert.IsTrue(item.InternalNettingSavedBps >= 0m);
            Assert.IsTrue(item.NetExternalExecutionBps >= 0m);
            Assert.IsTrue(item.StopOutSlackMargin > 0m);
            Assert.IsTrue(item.DampedCapitalDeleveragingFactor > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.PodRiskRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.PodAllocationAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Millennium") || res.ExecutiveVerdict.Contains("Point72") || res.ExecutiveVerdict.Contains("Pod") || res.ExecutiveVerdict.Contains("撮合"),
            "Verdict should mention Millennium, Point72, Pod, or Netting");
    }

    [TestMethod]
    public void Test_Phase51_RoughFractionalVolatilityGatheral()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateRoughFractionalVolatilityGatheral(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.RoughVolItems.Count);
        Assert.IsTrue(res.PortfolioWeightedHurstExponent > 0m && res.PortfolioWeightedHurstExponent < 0.5m,
            "Portfolio Hurst exponent should be rough (H < 0.5)");
        Assert.IsTrue(res.SystemicRoughnessBurstIndex >= 1.0m, "Systemic roughness burst index should be >= 1.0");
        Assert.IsTrue(res.ShortTermPowerLawSkewSlope != 0m, "Short term power law skew slope should be non-zero");
        Assert.IsTrue(res.RoughnessHedgingAlphaBps > 0m, "Roughness hedging alpha bps should be positive");

        foreach (var item in res.RoughVolItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.EstimatedHurstExponent > 0m && item.EstimatedHurstExponent < 0.5m);
            Assert.IsTrue(item.RoughnessBurstMultiplier >= 1.0m);
            Assert.IsTrue(item.PowerLawAtTheMoneySkew != 0m);
            Assert.IsTrue(item.VolMemoryPersistenceDays > 0m);
            Assert.IsTrue(item.RoughnessConvexityRatio > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.RoughnessRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.RoughnessHedgingAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Jump") || res.ExecutiveVerdict.Contains("Tower") || res.ExecutiveVerdict.Contains("Rough") || res.ExecutiveVerdict.Contains("Hurst") || res.ExecutiveVerdict.Contains("粗糙"),
            "Verdict should mention Jump, Tower, Rough, Hurst, or 粗糙");
    }

    [TestMethod]
    public void Test_Phase51_ThermodynamicCrossEntropyStress()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateThermodynamicCrossEntropyStress(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.StressScenarioItems.Count);
        Assert.IsTrue(res.SystemicCrossEntropyDkl > 0m, "Systemic cross entropy D_KL should be positive");
        Assert.IsTrue(res.MacroThermodynamicTemperature > 0m, "Macro thermodynamic temperature should be positive");
        Assert.IsTrue(res.GibbsFreeEnergyCollapse > 0m, "Gibbs free energy collapse should be positive");
        Assert.IsTrue(res.StressedConditionalVaR99 > 0m, "Stressed CVaR 99% should be positive");

        foreach (var item in res.StressScenarioItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.ScenarioId));
            Assert.IsFalse(string.IsNullOrEmpty(item.ScenarioName));
            Assert.IsTrue(item.PriorProbability > 0m);
            Assert.IsTrue(item.StressedPostProbability > 0m);
            Assert.IsTrue(item.KullbackLeiblerRelativeEntropy != 0m);
            Assert.IsTrue(item.FreeEnergyShiftDeltaF != 0m);
            Assert.IsTrue(item.StressedPortfolioLossPct > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.ScenarioSeverityBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.DynamicHedgingPrescription));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Bridgewater") || res.ExecutiveVerdict.Contains("Aladdin") || res.ExecutiveVerdict.Contains("相对熵") || res.ExecutiveVerdict.Contains("热力学"),
            "Verdict should mention Bridgewater, Aladdin, Relative Entropy, or Thermodynamics");
    }

    [TestMethod]
    public void Test_Phase51_SupersymmetricInstantonTunneling()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateSupersymmetricInstantonTunneling(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.InstantonItems.Count);
        Assert.IsTrue(res.GlobalInstantonActionS > 0m, "Global instanton action S should be positive");
        Assert.IsTrue(res.MaximumTunnelingEscapeRate >= 0m, "Maximum tunneling escape rate should be non-negative");
        Assert.IsTrue(res.SupersymmetricEnergyGapBps > 0m, "Supersymmetric energy gap buffer should be positive");
        Assert.IsTrue(res.NonPerturbativeTailShieldPct > 0m, "Non-perturbative tail shield pct should be positive");

        foreach (var item in res.InstantonItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.WittenSuperpotentialCurvature > 0m);
            Assert.IsTrue(item.InstantonActionMinimalS > 0m);
            Assert.IsTrue(item.TunnelingEscapeProbability >= 0m);
            Assert.IsTrue(item.EnergyGapBufferBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.VacuumStabilityBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.QuantumBarrierHedgingAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Jump") || res.ExecutiveVerdict.Contains("Hudson River") || res.ExecutiveVerdict.Contains("HRT") || res.ExecutiveVerdict.Contains("超对称") || res.ExecutiveVerdict.Contains("瞬子"),
            "Verdict should mention Jump, HRT, Supersymmetric, or Instanton");
    }

    [TestMethod]
    public void Test_Phase51_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.MillenniumConvexPodAllocation, "MillenniumConvexPodAllocation should be populated in PortfolioResult");
        Assert.IsNotNull(result.RoughFractionalVolatilityGatheral, "RoughFractionalVolatilityGatheral should be populated in PortfolioResult");
        Assert.IsNotNull(result.ThermodynamicCrossEntropyStress, "ThermodynamicCrossEntropyStress should be populated in PortfolioResult");
        Assert.IsNotNull(result.SupersymmetricInstantonTunneling, "SupersymmetricInstantonTunneling should be populated in PortfolioResult");

        // Verify HTML Export includes Phase 51 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("一百零六、Millennium Management & Point72 多 PM 策略 Pod 凸二次多维资本分配与内部互冲去交叉阻尼引擎 (Phase 51)"), "HTML should contain Section 106");
        Assert.IsTrue(html.Contains("一百零七、Jump Trading & Tower Research Capital 粗糙分数阶随机波动率 (Gatheral Rough Heston) 与微观粗糙度幂律偏度流形 (Phase 51)"), "HTML should contain Section 107");
        Assert.IsTrue(html.Contains("一百零八、Bridgewater Associates & BlackRock Aladdin 宏观热力学最小相对交叉熵 (Jaynes MaxEnt) 与非高斯情景冲击流形映射 (Phase 51)"), "HTML should contain Section 108");
        Assert.IsTrue(html.Contains("一百零九、Jump Trading & Hudson River Trading 超对称路径积分 (Supersymmetric Path Integral) 与非微扰瞬子 (Instanton) 极端黑天鹅跃迁防御 (Phase 51)"), "HTML should contain Section 109");
        Assert.IsTrue(html.Contains("跨 Pod 内部撮合净额率"), "HTML should contain Section 106 keywords");
        Assert.IsTrue(html.Contains("组合加权粗糙赫斯特指数"), "HTML should contain Section 107 keywords");
        Assert.IsTrue(html.Contains("系统最小相对交叉熵"), "HTML should contain Section 108 keywords");
        Assert.IsTrue(html.Contains("全局极小瞬子经典作用量"), "HTML should contain Section 109 keywords");
    }
}
