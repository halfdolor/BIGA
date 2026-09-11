using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase36Tests
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
            Name = "易方达中小盘混合 (成长动量龙头)",
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
            Name = "华夏纯债中短债A (稳健中枢固收)",
            FundSize = "65.2亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.046, 0.029, seed: 333)
        };
        var fundD = new FundDetail
        {
            Code = "000509",
            Name = "易方达现金增利货币A (流动性缓冲底仓)",
            FundSize = "320.0亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.023, 0.007, seed: 444)
        };

        return new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 35m),
            (fundB, 30m),
            (fundC, 25m),
            (fundD, 10m)
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
            double ret = (dailyMean + dailyStd * z) * 100.0;
            list.Add(Math.Round((decimal)ret, 4));
        }
        return list;
    }

    [TestMethod]
    public void Test_Phase36_AssetFactorCrowdednessAndHerdLiquidation()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.12, 0.16, 888);

        var result = QuantCalculator.CalculateAssetFactorCrowdednessAndHerdLiquidation(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.PortfolioAverageHlri >= 0m && result.PortfolioAverageHlri <= 100m);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.CrowdednessGovernanceBadge));
        Assert.IsTrue(result.PortfolioWeightedLiquidationDaysStress > 0m);
        Assert.IsTrue(result.HighRiskCrowdedAssetsCount >= 0);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.PeakCrowdedAssetCode));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.PeakCrowdedAssetName));
        Assert.AreEqual(components.Count, result.Items.Count);

        foreach (var item in result.Items)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.FundCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.FundName));
            Assert.IsTrue(item.PairwiseCorrelationCompression >= 0m && item.PairwiseCorrelationCompression <= 1m);
            Assert.IsTrue(item.VolumeTurnoverAccelerationRatio >= 0.5m);
            Assert.IsTrue(item.HerdLiquidationRiskIndex >= 0m && item.HerdLiquidationRiskIndex <= 100m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.CrowdednessTierBadge));
            Assert.IsTrue(item.EstimatedLiquidationDaysStress >= item.EstimatedLiquidationDaysNormal);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.DeriskingActionGuidance));
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
    }

    [TestMethod]
    public void Test_Phase36_MacroFactorShockPropagation()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.10, 0.18, 999);

        var result = QuantCalculator.CalculateMacroFactorShockPropagation(components, returns);

        Assert.IsNotNull(result);
        Assert.AreEqual(5, result.ScenarioList.Count, "应涵盖 5 大经典历史与前瞻宏观极端冲击情景。");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.WorstCaseScenarioName));
        Assert.IsTrue(result.WorstCasePortfolioLossPercent <= 0m, "最差宏观冲击预期损益通常为负向回撤。");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.StressedVaR99Range));
        Assert.IsTrue(result.FragilityDiversityRatio > 0m);
        Assert.AreEqual(components.Count, result.AssetStressedLossList.Count);

        foreach (var sc in result.ScenarioList)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(sc.ScenarioName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(sc.ScenarioDescription));
            Assert.IsTrue(sc.StressedVaR99Percent >= 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(sc.HardestHitAssetName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(sc.ResilienceBadge));
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
    }

    [TestMethod]
    public void Test_Phase36_VarianceRiskPremiumAndVolatilityStructure()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.12, 0.15, 666);

        var result = QuantCalculator.CalculateVarianceRiskPremiumAndVolatilityStructure(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.PortfolioAverageRealizedVol > 0m);
        Assert.IsTrue(result.PortfolioAverageImpliedVol > 0m);
        Assert.IsTrue(result.PortfolioAverageVrpSpread >= 0m, "正常市场环境下期权隐含波动率通常存在正向方差溢价。");
        Assert.IsTrue(result.PortfolioAverageVrpRatio >= 0m);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.VrpHarvestRegimeBadge));
        Assert.AreEqual(components.Count, result.AssetVrpList.Count);

        foreach (var vrp in result.AssetVrpList)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(vrp.FundCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(vrp.FundName));
            Assert.IsTrue(vrp.RealizedVolCloseToClose > 0m);
            Assert.IsTrue(vrp.ImpliedVolForecast > 0m);
            Assert.IsTrue(vrp.DownsideToUpsideVolRatio > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(vrp.VrpHarvestSignal));
            Assert.IsTrue(vrp.CarryYieldAnnualizedPercent >= 0m);
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
    }

    [TestMethod]
    public void Test_Phase36_DynamicNoTradeBufferBands()
    {
        var components = CreateSampleComponents();

        var result = QuantCalculator.CalculateDynamicNoTradeBufferBands(components);

        Assert.IsNotNull(result);
        Assert.AreEqual(components.Count, result.Items.Count);
        Assert.AreEqual(components.Count, result.InBandAssetCount + result.OutBandAssetCount);
        Assert.IsTrue(result.TurnoverReductionPercent >= 30m, "Leland-Atkinson 动态走廊应降低至少 30% 以上的无谓再平衡换手。");
        Assert.IsTrue(result.EstimatedAnnualFrictionSavedWan > 0m);
        Assert.IsTrue(result.NetSharpeUplift > 0m);

        foreach (var item in result.Items)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.FundCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.FundName));
            Assert.IsTrue(item.TargetWeightPercent > 0m);
            Assert.IsTrue(item.CurrentWeightPercent > 0m);
            Assert.IsTrue(item.HalfBandWidthPercent > 0m);
            Assert.IsTrue(item.LowerBandPercent < item.UpperBandPercent);
            Assert.IsTrue(item.LowerBandPercent <= item.TargetWeightPercent && item.TargetWeightPercent <= item.UpperBandPercent);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.BreachStatus));
            Assert.IsTrue(item.EstimatedFrictionSavedBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.ActionGuidance));
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));
    }

    [TestMethod]
    public void Test_Phase36_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        // 运行完整投资组合分析引擎
        var portResult = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(portResult);
        Assert.IsNotNull(portResult.AssetFactorCrowdedness);
        Assert.IsNotNull(portResult.MacroFactorShockPropagation);
        Assert.IsNotNull(portResult.VarianceRiskPremium);
        Assert.IsNotNull(portResult.DynamicNoTradeBufferBand);

        // 验证 HTML 研报导出中 Phase 36 各章节
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(portResult, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("四十六、Citadel & Millennium 微观因子与资产拥挤度评分及机构踩踏排队指数 (HLRI)"));
        Assert.IsTrue(html.Contains("四十七、BlackRock Aladdin & J.P. Morgan 多因子宏观情景冲击传导与压力在险价值 (Stressed VaR)"));
        Assert.IsTrue(html.Contains("四十八、AQR Capital & Antti Ilmanen 真实波动率特征结构与方差风险溢价 (VRP)"));
        Assert.IsTrue(html.Contains("四十九、Two Sigma & Renaissance 摩擦成本敏感型动态无交易再平衡缓冲带 (No-Trade Buffer Bands)"));
    }
}
