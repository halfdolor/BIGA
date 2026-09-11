using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase62Tests
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
            Name = "易方达中小盘混合 (高贝塔多策略Pod A)",
            FundSize = "180.5亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.22, 0.25, seed: 111)
        };
        var fundB = new FundDetail
        {
            Code = "510300",
            Name = "华泰柏瑞沪深300ETF (核心宽基流动性Pod B)",
            FundSize = "850.0亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.12, 0.18, seed: 222)
        };
        var fundC = new FundDetail
        {
            Code = "000001",
            Name = "华夏成长混合 (均衡成长对冲Pod C)",
            FundSize = "95.2亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.15, 0.20, seed: 333)
        };
        var fundD = new FundDetail
        {
            Code = "511010",
            Name = "国债ETF (低碳安全垫稳健Pod D)",
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
        var rng = new Random(999);
        var returns = new List<decimal>();
        for (int i = 0; i < 250; i++)
        {
            returns.Add((decimal)(rng.NextDouble() * 0.04 - 0.018));
        }
        return returns;
    }

    [TestMethod]
    public void Test_Phase62_MultiPodFactorCrowdingClawback()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var result = QuantCalculator.CalculateMultiPodFactorCrowdingClawback(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.GlobalAverageClawbackRatioPct >= 0m, "全局高水位扣划比例应大于等于 0%");
        Assert.IsTrue(result.PortfolioLatentCrowdingEntropy > 0m, "跨组合隐性因子拥挤谱熵应大于 0");
        Assert.IsTrue(result.CascadeLiquidationRiskMitigationPct > 0m, "协同清算踩踏化解率应大于 0%");
        Assert.IsTrue(result.TotalMultiPodClawbackAlphaBps > 0m, "多Pod解耦总 Alpha 应大于 0 bps");
        Assert.AreEqual(components.Count, result.PodItems.Count);

        decimal totalTargetWeight = 0m;
        foreach (var item in result.PodItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetName));
            Assert.IsTrue(item.PodPeakToTroughDrawdownPct >= 0m);
            Assert.IsTrue(item.PodDrawdownClawbackRatioPct >= 0m);
            Assert.IsTrue(item.CrossPodFactorCrowdingIndex >= 0m);
            Assert.IsTrue(item.LiquidityContagionVulnerabilityPct >= 0m);
            Assert.IsTrue(item.CrowdingDecoupledTargetWeight > 0m);
            Assert.IsTrue(item.MultiPodClawbackAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.PodRegimeBadge));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.PodAdvice));
            totalTargetWeight += item.CrowdingDecoupledTargetWeight;
        }

        Assert.AreEqual(100.0, (double)totalTargetWeight, 1.0, "解耦目标权重之和应归一化为 100%");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
        StringAssert.Contains(result.ExecutiveVerdict, "Millennium & Point72 扣划审定");
    }

    [TestMethod]
    public void Test_Phase62_ClimateTransitionStrandedAssetStress()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var result = QuantCalculator.CalculateClimateTransitionStrandedAssetStress(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.PortfolioWeightedCarbonBeta > 0m, "组合加权平均碳贝塔应大于 0");
        Assert.IsTrue(result.OrderlyScenarioPortfolioDrawdownPct > 0m, "有序转型情景折算冲击应大于 0%");
        Assert.IsTrue(result.DisorderlyStrandedWriteDownPct > 0m, "无序转型搁浅减记比例应大于 0%");
        Assert.IsTrue(result.TotalClimateTransitionAlphaBps > 0m, "气候防卫总 Alpha 应大于 0 bps");
        Assert.AreEqual(components.Count, result.ClimateItems.Count);

        decimal totalTargetWeight = 0m;
        foreach (var item in result.ClimateItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetName));
            Assert.IsTrue(item.AssetCarbonBeta > 0m);
            Assert.IsTrue(item.OrderlyTransitionImpactPct < 0m, "有序转型估值冲击应为负值折价");
            Assert.IsTrue(item.DisorderlyTransitionImpactPct < 0m, "无序转型估值冲击应为负值折价");
            Assert.IsTrue(item.StrandedAssetExtremeVaRPct > 0m, "搁浅资产极端压力 VaR 应大于 0");
            Assert.IsTrue(item.ClimateResilientTargetWeight > 0m);
            Assert.IsTrue(item.ClimateTransitionAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.ClimateRegimeBadge));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.ClimateAdvice));
            totalTargetWeight += item.ClimateResilientTargetWeight;
        }

        Assert.AreEqual(100.0, (double)totalTargetWeight, 1.0, "气候韧性目标权重之和应归一化为 100%");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
        StringAssert.Contains(result.ExecutiveVerdict, "Aladdin & NBIM 气候审定");
    }

    [TestMethod]
    public void Test_Phase62_TensorRingMultimodalAlphaField()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var result = QuantCalculator.CalculateTensorRingMultimodalAlphaField(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.GlobalTensorRingReconstructionError > 0m, "张量环重构误差应大于 0");
        Assert.IsTrue(result.AverageMultimodalEntanglement > 0m, "平均循环相干缠结熵应大于 0");
        Assert.IsTrue(result.HighDimensionalAlphaPurityGainPct > 0m, "高维纯度提升率应大于 0%");
        Assert.IsTrue(result.TotalTensorRingAlphaFieldBps > 0m, "多模态张量总 Alpha 应大于 0 bps");
        Assert.AreEqual(components.Count, result.TensorItems.Count);

        decimal totalTargetWeight = 0m;
        foreach (var item in result.TensorItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetName));
            Assert.IsTrue(item.TensorRingCoreRank >= 2);
            Assert.IsTrue(item.MultimodalEntanglementEntropy > 0m);
            Assert.IsTrue(item.TensorNoiseSuppressionPct > 0m);
            Assert.IsTrue(item.TensorRingOptimalTargetWeight > 0m);
            Assert.IsTrue(item.TensorRingAlphaFieldBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.TensorRegimeBadge));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.TensorAdvice));
            totalTargetWeight += item.TensorRingOptimalTargetWeight;
        }

        Assert.AreEqual(100.0, (double)totalTargetWeight, 1.0, "张量环最优目标权重之和应归一化为 100%");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
        StringAssert.Contains(result.ExecutiveVerdict, "Two Sigma & D.E. Shaw 张量审定");
    }

    [TestMethod]
    public void Test_Phase62_MalliavinJumpDiffusionHedging()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var result = QuantCalculator.CalculateMalliavinJumpDiffusionHedging(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.GlobalMalliavinHedgeRatio > 0m, "加权对冲系数应大于 0");
        Assert.IsTrue(result.StochasticVolConvexityCapture > 0m, "波动率凸度增益应大于 0 bps");
        Assert.IsTrue(result.ExtremeJumpSlippageReductionPct > 0m, "极端跳跃滑点压降率应大于 0%");
        Assert.IsTrue(result.TotalMalliavinHedgingAlphaBps > 0m, "马利亚温变分总 Alpha 应大于 0 bps");
        Assert.AreEqual(components.Count, result.MalliavinItems.Count);

        decimal totalTargetWeight = 0m;
        foreach (var item in result.MalliavinItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.AssetName));
            Assert.IsTrue(item.MalliavinDeltaRatio > 0m);
            Assert.IsTrue(item.MalliavinGammaSensitivity > 0m);
            Assert.IsTrue(item.JumpDiffusionHazardIntensity > 0m);
            Assert.IsTrue(item.MalliavinImmunizedTargetWeight > 0m);
            Assert.IsTrue(item.MalliavinHedgingAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.MalliavinRegimeBadge));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.MalliavinAdvice));
            totalTargetWeight += item.MalliavinImmunizedTargetWeight;
        }

        Assert.AreEqual(100.0, (double)totalTargetWeight, 1.0, "马利亚温免疫目标权重之和应归一化为 100%");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
        StringAssert.Contains(result.ExecutiveVerdict, "Citadel & Jump 对冲审定");
    }

    [TestMethod]
    public void Test_Phase62_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();
        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);

        // 验证 Phase 62 核心模型已全链路贯通
        Assert.IsNotNull(result.MultiPodFactorCrowdingClawback, "MultiPodFactorCrowdingClawback 应在回测流程中被执行并填充");
        Assert.IsNotNull(result.ClimateTransitionStrandedAssetStress, "ClimateTransitionStrandedAssetStress 应在回测流程中被执行并填充");
        Assert.IsNotNull(result.TensorRingMultimodalAlphaField, "TensorRingMultimodalAlphaField 应在回测流程中被执行并填充");
        Assert.IsNotNull(result.MalliavinJumpDiffusionHedging, "MalliavinJumpDiffusionHedging 应在回测流程中被执行并填充");

        // 验证 HTML 尽调研报导出与免责声明
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);
        Assert.IsFalse(string.IsNullOrWhiteSpace(html));

        Assert.IsTrue(html.Contains("一百五十、Millennium Management & Point72: 多子策略/基金高水位动态资本回撤扣划与跨组合因子拥挤解耦 (Phase 62)"), "HTML should contain Chapter 150");
        Assert.IsTrue(html.Contains("一百五十一、BlackRock Aladdin & NBIM / GIC: NGFS 气候宏观转型三情景压力测试与碳贝塔搁浅资产 VaR (Phase 62)"), "HTML should contain Chapter 151");
        Assert.IsTrue(html.Contains("一百五十二、Two Sigma & D.E. Shaw: 高阶张量环 (Tensor Ring) 紧致分解与隐式多模态 Alpha 场 (Phase 62)"), "HTML should contain Chapter 152");
        Assert.IsTrue(html.Contains("一百五十三、Citadel & Jump Trading: 维纳-泊松空间马利亚温微分 (Malliavin Calculus) 跳跃扩散随机波动率对冲 (Phase 62)"), "HTML should contain Chapter 153");

        // 验证页脚免责声明包含 Phase 62 顶级机构量化模型
        Assert.IsTrue(html.Contains("Millennium Management & Point72 多子策略高水位动态资本回撤扣划与跨组合因子拥挤解耦引擎"), "HTML should contain Millennium Point72 disclaimer");
        Assert.IsTrue(html.Contains("BlackRock Aladdin & NBIM / GIC NGFS 气候宏观转型三情景压力测试与碳贝塔搁浅资产 VaR 引擎"), "HTML should contain BlackRock NBIM disclaimer");
        Assert.IsTrue(html.Contains("Two Sigma & D.E. Shaw 高阶张量环 (Tensor Ring) 紧致分解与隐式多模态 Alpha 场引擎"), "HTML should contain Two Sigma D.E. Shaw disclaimer");
        Assert.IsTrue(html.Contains("Citadel & Jump Trading 维纳-泊松空间马利亚温微分 (Malliavin Calculus) 跳跃扩散随机波动率对冲引擎"), "HTML should contain Citadel Jump Trading disclaimer");
    }
}
