using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase60Tests
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
    public void Test_Phase60_NonlinearPoissonBoundaryMarketMaking()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var result = QuantCalculator.CalculateNonlinearPoissonBoundaryMarketMaking(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.GlobalOptimalSpreadBps > 0m, "全局最优做市价差应大于 0 bps");
        Assert.IsTrue(result.AverageInventoryDecayHours > 0m, "平均库存半衰期应大于 0 小时");
        Assert.IsTrue(result.AdverseSelectionBreachReductionPct > 0m, "逆向选择击穿压降率应大于 0%");
        Assert.IsTrue(result.TotalBoundaryMarketMakingAlphaBps > 0m, "做市优化总 Alpha 应大于 0 bps");
        Assert.AreEqual(components.Count, result.PoissonItems.Count);

        foreach (var item in result.PoissonItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetName));
            Assert.IsTrue(item.OptimalBidAskSpreadBps > 0m);
            Assert.IsTrue(item.InventoryHalfLifeHours > 0m);
            Assert.IsTrue(item.QueueExhaustionHazardPct >= 0m);
            Assert.IsTrue(item.BoundaryMarketMakingAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.PoissonRegimeBadge));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.PoissonAdvice));
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
        StringAssert.Contains(result.ExecutiveVerdict, "Citadel & Optiver 做市审定");
    }

    [TestMethod]
    public void Test_Phase60_KacMoodyGaugeTopologicalCharge()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var result = QuantCalculator.CalculateKacMoodyGaugeTopologicalCharge(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.AreNotEqual(0m, result.GlobalTopologicalChargeNumber, "瞬子拓扑荷数应非零");
        Assert.IsTrue(result.AverageGaugeCurvature >= 0m, "杨-米尔斯规范场曲率应为非负");
        Assert.IsTrue(result.FalseAlarmResonanceReductionPct > 0m, "流动性共振误判压降率应大于 0%");
        Assert.IsTrue(result.TotalTopologicalChargeAlphaBps > 0m, "拓扑规范不变总 Alpha 应大于 0 bps");
        Assert.AreEqual(components.Count, result.GaugeItems.Count);

        decimal totalWeight = 0m;
        foreach (var item in result.GaugeItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetName));
            Assert.IsTrue(item.KacMoodyRootProjectionNorm > 0m);
            Assert.IsTrue(item.YangMillsCurvatureMagnitude >= 0m);
            Assert.IsTrue(item.SymmetryBreakingOrderParameter >= 0m);
            Assert.IsTrue(item.GaugeInvariantTargetWeight > 0m);
            Assert.IsTrue(item.TopologicalChargeAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.KacMoodyRegimeBadge));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.KacMoodyAdvice));
            totalWeight += item.GaugeInvariantTargetWeight;
        }

        Assert.AreEqual(100.0, (double)totalWeight, 1.0, "规范不变目标权重之和应归一化为 100%");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
        StringAssert.Contains(result.ExecutiveVerdict, "Renaissance & D.E. Shaw 规范场审定");
    }

    [TestMethod]
    public void Test_Phase60_McKeanVlasovOptimalLiquidation()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var result = QuantCalculator.CalculateMcKeanVlasovOptimalLiquidation(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.GlobalCrowdingDragCoeff > 0m, "全局群体拥挤拖拽系数应大于 0");
        Assert.IsTrue(result.AggregateLiquidationSavingPct > 0m, "踩踏清算落差节省率应大于 0%");
        Assert.IsTrue(result.TotalMeanFieldEvasionAlphaBps > 0m, "平均场协同清算 Alpha 应大于 0 bps");
        Assert.AreEqual(components.Count, result.McKeanVlasovItems.Count);

        foreach (var item in result.McKeanVlasovItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetName));
            Assert.IsTrue(item.CollectiveCrowdingDragCoeff > 0m);
            Assert.IsTrue(item.OptimalDecrowdingSpeed > 0m);
            Assert.IsTrue(item.CrowdingShortfallSavingPct > 0m);
            Assert.IsTrue(item.MeanFieldEvasionAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.McKeanVlasovRegimeBadge));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.McKeanVlasovAdvice));
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
        StringAssert.Contains(result.ExecutiveVerdict, "Two Sigma & Jump 清算审定");
    }

    [TestMethod]
    public void Test_Phase60_VolterraNonMarkovianCreditParity()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var result = QuantCalculator.CalculateVolterraNonMarkovianCreditParity(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.GlobalVolterraMemoryHurst > 0m, "全局沃尔泰拉赫斯特指数应大于 0");
        Assert.IsTrue(result.ErgodicSpectralEntropy > 0m, "遍历谱能量信息熵应大于 0");
        Assert.IsTrue(result.DrawdownRecoveryCycleShortenPct > 0m, "回撤修复周期缩短比例应大于 0%");
        Assert.IsTrue(result.TotalVolterraCreditParityAlphaBps > 0m, "沃尔泰拉谱平价总 Alpha 应大于 0 bps");
        Assert.AreEqual(components.Count, result.VolterraItems.Count);

        decimal totalWeight = 0m;
        foreach (var item in result.VolterraItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetName));
            Assert.IsTrue(item.VolterraMemoryPersistenceIndex > 0m);
            Assert.IsTrue(item.ErgodicSpectralRiskContributionPct > 0m);
            Assert.IsTrue(item.MultiCycleDebtOverhangBeta > 0m);
            Assert.IsTrue(item.VolterraResilientTargetWeight > 0m);
            Assert.IsTrue(item.VolterraCreditParityAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.VolterraRegimeBadge));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.VolterraAdvice));
            totalWeight += item.VolterraResilientTargetWeight;
        }

        Assert.AreEqual(100.0, (double)totalWeight, 1.0, "分数阶全天候平价目标权重之和应归一化为 100%");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
        StringAssert.Contains(result.ExecutiveVerdict, "Bridgewater & Millennium 信用平价审定");
    }

    [TestMethod]
    public void Test_Phase60_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();
        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);

        // 验证 Phase 60 核心模型已全链路贯通
        Assert.IsNotNull(result.NonlinearPoissonBoundaryMarketMaking, "NonlinearPoissonBoundaryMarketMaking 应在回测流程中被执行并填充");
        Assert.IsNotNull(result.KacMoodyGaugeTopologicalCharge, "KacMoodyGaugeTopologicalCharge 应在回测流程中被执行并填充");
        Assert.IsNotNull(result.McKeanVlasovOptimalLiquidation, "McKeanVlasovOptimalLiquidation 应在回测流程中被执行并填充");
        Assert.IsNotNull(result.VolterraNonMarkovianCreditParity, "VolterraNonMarkovianCreditParity 应在回测流程中被执行并填充");

        // 验证 HTML 尽调研报导出与免责声明
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);
        Assert.IsFalse(string.IsNullOrWhiteSpace(html));

        Assert.IsTrue(html.Contains("一百四十二、Citadel Securities & Optiver: 非线性泊松跳跃边界反射与 Avellaneda-Stoikov 粘性滑移面做市控制 (Phase 60)"), "HTML should contain Chapter 142");
        Assert.IsTrue(html.Contains("一百四十三、Renaissance Technologies & D.E. Shaw: 仿射 Kac-Moody 无限维李代数根格与杨-米尔斯规范场拓扑荷度量 (Phase 60)"), "HTML should contain Chapter 143");
        Assert.IsTrue(html.Contains("一百四十四、Two Sigma & Jump Trading: McKean-Vlasov 前向-后向随机微分方程 (FBSDE) 非局部多主体平均场协同清算 (Phase 60)"), "HTML should contain Chapter 144");
        Assert.IsTrue(html.Contains("一百四十五、Bridgewater Associates & Millennium Macro: 分数阶伊藤-沃尔泰拉 (Ito-Volterra) 非马尔可夫信用周期记忆与遍历谱平价 (Phase 60)"), "HTML should contain Chapter 145");

        // 验证页脚免责声明包含 Phase 60 顶级机构量化模型
        Assert.IsTrue(html.Contains("Citadel Securities & Optiver 非线性泊松跳跃边界反射与 Avellaneda-Stoikov 粘性滑移面做市控制引擎"), "HTML should contain Citadel Optiver disclaimer");
        Assert.IsTrue(html.Contains("Renaissance Technologies & D.E. Shaw 仿射 Kac-Moody 无限维李代数根格与杨-米尔斯规范场拓扑荷度量引擎"), "HTML should contain Renaissance D.E. Shaw disclaimer");
        Assert.IsTrue(html.Contains("Two Sigma & Jump Trading McKean-Vlasov 前向-后向随机微分方程 (FBSDE) 非局部多主体平均场协同清算引擎"), "HTML should contain Two Sigma Jump disclaimer");
        Assert.IsTrue(html.Contains("Bridgewater Associates & Millennium Macro 分数阶伊藤-沃尔泰拉 (Ito-Volterra) 非马尔可夫信用周期记忆与遍历谱平价引擎"), "HTML should contain Bridgewater Millennium disclaimer");
    }
}
