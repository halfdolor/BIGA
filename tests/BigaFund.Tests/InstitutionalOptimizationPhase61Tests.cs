using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase61Tests
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
    public void Test_Phase61_RoughHawkesQueueLatencyArbitrage()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var result = QuantCalculator.CalculateRoughHawkesQueueLatencyArbitrage(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.GlobalOptimalQueueSpreadBps > 0m, "全局最优跳级挂单价差应大于 0 bps");
        Assert.IsTrue(result.AverageQueueSurvivalRatePct > 0m, "平均排队生存率应大于 0%");
        Assert.IsTrue(result.LatencyArbitrageSlippageReductionPct > 0m, "延迟套利滑点压降率应大于 0%");
        Assert.IsTrue(result.TotalRoughHawkesQueueAlphaBps > 0m, "粗糙排队总 Alpha 应大于 0 bps");
        Assert.AreEqual(components.Count, result.HawkesItems.Count);

        foreach (var item in result.HawkesItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetName));
            Assert.IsTrue(item.RoughHawkesBranchingRatio > 0m);
            Assert.IsTrue(item.QueueSurvivalProbabilityPct > 0m);
            Assert.IsTrue(item.LatencyArbitrageHazardPct >= 0m);
            Assert.IsTrue(item.OptimalQueueSpreadBps > 0m);
            Assert.IsTrue(item.RoughHawkesQueueAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.RoughHawkesRegimeBadge));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.RoughHawkesAdvice));
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
        StringAssert.Contains(result.ExecutiveVerdict, "Citadel & Jane Street 排队审定");
    }

    [TestMethod]
    public void Test_Phase61_SymplecticHamiltonianManifoldResonance()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var result = QuantCalculator.CalculateSymplecticHamiltonianManifoldResonance(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.GlobalSymplecticPhaseVolumeDrift >= 0m, "相空间辛体积漂移率应为非负");
        Assert.IsTrue(result.AverageLyapunovExponent > 0m, "平均庞加莱李雅普诺夫指数应大于 0");
        Assert.IsTrue(result.FalseResonanceAvoidancePct > 0m, "伪共振规避率应大于 0%");
        Assert.IsTrue(result.TotalSymplecticManifoldAlphaBps > 0m, "辛几何保结构总 Alpha 应大于 0 bps");
        Assert.AreEqual(components.Count, result.SymplecticItems.Count);

        decimal totalWeight = 0m;
        foreach (var item in result.SymplecticItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetName));
            Assert.IsTrue(item.SymplecticMomentumMagnitude > 0m);
            Assert.IsTrue(item.PoincareSectionLyapunovExponent > 0m);
            Assert.IsTrue(item.SymplecticEnergyConservationRatio > 0m);
            Assert.IsTrue(item.SymplecticPreservingTargetWeight > 0m);
            Assert.IsTrue(item.SymplecticManifoldAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.SymplecticRegimeBadge));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.SymplecticAdvice));
            totalWeight += item.SymplecticPreservingTargetWeight;
        }

        Assert.AreEqual(100.0, (double)totalWeight, 1.0, "辛几何保结构目标权重之和应归一化为 100%");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
        StringAssert.Contains(result.ExecutiveVerdict, "Renaissance & D.E. Shaw 辛几何审定");
    }

    [TestMethod]
    public void Test_Phase61_WassersteinBarycenterDynamicRebalancing()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var result = QuantCalculator.CalculateWassersteinBarycenterDynamicRebalancing(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.GlobalBarycenterEntropy > 0m, "全局测度重心信息熵应大于 0");
        Assert.IsTrue(result.AverageWassersteinDistance > 0m, "平均 Wasserstein 测度距离应大于 0");
        Assert.IsTrue(result.TransportFrictionSavingPct > 0m, "传输摩擦损耗节省率应大于 0%");
        Assert.IsTrue(result.TotalOptimalTransportAlphaBps > 0m, "最优传输重构总 Alpha 应大于 0 bps");
        Assert.AreEqual(components.Count, result.WassersteinItems.Count);

        decimal totalWeight = 0m;
        foreach (var item in result.WassersteinItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetName));
            Assert.IsTrue(item.WassersteinDistanceToBarycenter > 0m);
            Assert.IsTrue(item.SinkhornTransportCostTenThousand > 0m);
            Assert.IsTrue(item.RicciCurvatureDispersion > 0m);
            Assert.IsTrue(item.GeodesicOptimalTargetWeight > 0m);
            Assert.IsTrue(item.OptimalTransportAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.WassersteinRegimeBadge));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.WassersteinAdvice));
            totalWeight += item.GeodesicOptimalTargetWeight;
        }

        Assert.AreEqual(100.0, (double)totalWeight, 1.0, "测地线平滑重构目标权重之和应归一化为 100%");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
        StringAssert.Contains(result.ExecutiveVerdict, "Two Sigma & Point72 重心审定");
    }

    [TestMethod]
    public void Test_Phase61_QuantumSpectralChaosMacroParity()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var result = QuantCalculator.CalculateQuantumSpectralChaosMacroParity(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.GlobalGueSpectralRigidity > 0m, "全局 GUE 谱刚度应大于 0");
        Assert.IsTrue(result.BerryRobnikChaosEntropy > 0m, "Berry-Robnik 混沌熵应大于 0");
        Assert.IsTrue(result.MacroTailDrawdownMitigationPct > 0m, "宏观尾部滞胀回撤减免率应大于 0%");
        Assert.IsTrue(result.TotalQuantumSpectralParityAlphaBps > 0m, "量子谱平价总 Alpha 应大于 0 bps");
        Assert.AreEqual(components.Count, result.QuantumItems.Count);

        decimal totalWeight = 0m;
        foreach (var item in result.QuantumItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetName));
            Assert.IsTrue(item.DysonLevelSpacingRatio > 0m);
            Assert.IsTrue(item.QuantumSpectralRigidityIndex > 0m);
            Assert.IsTrue(item.QuantumTunnelingJumpProbabilityPct > 0m);
            Assert.IsTrue(item.QuantumChaosParityTargetWeight > 0m);
            Assert.IsTrue(item.QuantumSpectralParityAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.QuantumRegimeBadge));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.QuantumAdvice));
            totalWeight += item.QuantumChaosParityTargetWeight;
        }

        Assert.AreEqual(100.0, (double)totalWeight, 1.0, "量子混沌平价目标权重之和应归一化为 100%");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
        StringAssert.Contains(result.ExecutiveVerdict, "Bridgewater & AQR 量子混沌审定");
    }

    [TestMethod]
    public void Test_Phase61_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();
        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);

        // 验证 Phase 61 核心模型已全链路贯通
        Assert.IsNotNull(result.RoughHawkesQueueLatencyArbitrage, "RoughHawkesQueueLatencyArbitrage 应在回测流程中被执行并填充");
        Assert.IsNotNull(result.SymplecticHamiltonianManifoldResonance, "SymplecticHamiltonianManifoldResonance 应在回测流程中被执行并填充");
        Assert.IsNotNull(result.WassersteinBarycenterDynamicRebalancing, "WassersteinBarycenterDynamicRebalancing 应在回测流程中被执行并填充");
        Assert.IsNotNull(result.QuantumSpectralChaosMacroParity, "QuantumSpectralChaosMacroParity 应在回测流程中被执行并填充");

        // 验证 HTML 尽调研报导出与免责声明
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);
        Assert.IsFalse(string.IsNullOrWhiteSpace(html));

        Assert.IsTrue(html.Contains("一百四十六、Citadel Securities & Jane Street: 微观高阶粗糙霍克斯过程与订单簿队列隐性排队延迟博弈 (Phase 61)"), "HTML should contain Chapter 146");
        Assert.IsTrue(html.Contains("一百四十七、Renaissance Technologies & D.E. Shaw: 相空间辛几何哈密顿流形动力学与辛积分保结构降维共振 (Phase 61)"), "HTML should contain Chapter 147");
        Assert.IsTrue(html.Contains("一百四十八、Two Sigma & Point72: 最优传输 (Monge-Kantorovich) Wasserstein 重心测度流与截面非平衡协同重构 (Phase 61)"), "HTML should contain Chapter 148");
        Assert.IsTrue(html.Contains("一百四十九、Bridgewater Associates & AQR Capital: 连续谱量子混沌非对易算子多尺度周期与超全天候宏观体制平价 (Phase 61)"), "HTML should contain Chapter 149");

        // 验证页脚免责声明包含 Phase 61 顶级机构量化模型
        Assert.IsTrue(html.Contains("Citadel Securities & Jane Street 微观高阶粗糙霍克斯过程与订单簿队列隐性排队延迟博弈引擎"), "HTML should contain Citadel Jane Street disclaimer");
        Assert.IsTrue(html.Contains("Renaissance Technologies & D.E. Shaw 相空间辛几何哈密顿流形动力学与辛积分保结构降维共振引擎"), "HTML should contain Renaissance D.E. Shaw disclaimer");
        Assert.IsTrue(html.Contains("Two Sigma & Point72 最优传输 (Monge-Kantorovich) Wasserstein 重心测度流与截面非平衡协同重构引擎"), "HTML should contain Two Sigma Point72 disclaimer");
        Assert.IsTrue(html.Contains("Bridgewater Associates & AQR Capital 连续谱量子混沌非对易算子多尺度周期与超全天候宏观体制平价引擎"), "HTML should contain Bridgewater AQR disclaimer");
    }
}
