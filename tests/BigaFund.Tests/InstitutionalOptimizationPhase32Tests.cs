using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase32Tests
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
        var fundA = new FundDetail { Code = "110011", Name = "易方达中小盘混合 (偏股成长)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.16, 0.22, seed: 111) };
        var fundB = new FundDetail { Code = "510300", Name = "华泰柏瑞沪深300ETF (宽基蓝筹)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.08, 0.15, seed: 222) };
        var fundC = new FundDetail { Code = "000001", Name = "华夏纯债中短债A (固定收益债券)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.042, 0.032, seed: 333) };
        var fundD = new FundDetail { Code = "000509", Name = "易方达现金增利货币A (流动性现金)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.021, 0.005, seed: 444) };

        return new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 35.0m),
            (fundB, 25.0m),
            (fundC, 25.0m),
            (fundD, 15.0m)
        };
    }

    [TestMethod]
    public void Test_IdzorekBlackLitterman_CalibratesOmegaAndTiltWeights()
    {
        var components = CreateSampleComponents();
        var dailyNavs = components[0].Fund.NavHistory;
        var dailyReturns = new List<decimal>();
        for (int i = 1; i < dailyNavs.Count; i++)
        {
            decimal pRet = 0m;
            decimal totalW = components.Sum(c => c.Weight);
            foreach (var (fund, weight) in components)
            {
                pRet += (weight / totalW) * fund.NavHistory[i].DailyReturn;
            }
            dailyReturns.Add(pRet);
        }

        var result = QuantCalculator.CalculateIdzorekBlackLitterman(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.PriorEquilibriumSharpeRatio > 0m);
        Assert.IsTrue(result.PosteriorOptimalSharpeRatio > 0m);
        Assert.IsTrue(result.AverageUserConfidencePercent > 50.0m);
        Assert.IsTrue(result.PriorPosteriorKLDivergenceEntropy >= 0m);

        // 验证观点列表
        Assert.IsTrue(result.ViewItemList.Count >= 1);
        foreach (var view in result.ViewItemList)
        {
            Assert.IsTrue(view.UserSpecifiedConfidencePercent > 0m);
            Assert.IsTrue(view.CalibratedOmegaVariance > 0m);
            Assert.IsTrue(view.ViewInformationContributionPercent >= 0m);
            Assert.IsFalse(string.IsNullOrEmpty(view.ViewImpactBadge));
        }

        // 验证资产列表
        Assert.AreEqual(components.Count, result.AssetItemList.Count);
        decimal sumPostWeights = result.AssetItemList.Sum(a => a.IdzorekPosteriorWeightPercent);
        Assert.IsTrue(Math.Abs(sumPostWeights - 100.0m) < 1.0m, $"Posterior weights sum {sumPostWeights} should be close to 100%");

        foreach (var asset in result.AssetItemList)
        {
            Assert.IsFalse(string.IsNullOrEmpty(asset.FundCode));
            Assert.IsFalse(string.IsNullOrEmpty(asset.FundName));
            Assert.IsTrue(asset.IdzorekPosteriorWeightPercent >= 0m);
        }

        Assert.IsTrue(result.IdzorekExecutiveVerdict.Contains("Idzorek 显式置信度 BL 优化审定"));
    }

    [TestMethod]
    public void Test_SpectralRiskMeasure_CalculatesContinuousAcerbiAversionAndTiers()
    {
        var components = CreateSampleComponents();
        var dailyNavs = components[0].Fund.NavHistory;
        var dailyReturns = new List<decimal>();
        for (int i = 1; i < dailyNavs.Count; i++)
        {
            decimal pRet = 0m;
            decimal totalW = components.Sum(c => c.Weight);
            foreach (var (fund, weight) in components)
            {
                pRet += (weight / totalW) * fund.NavHistory[i].DailyReturn;
            }
            dailyReturns.Add(pRet);
        }

        double riskAversionGamma = 10.0;
        var result = QuantCalculator.CalculateSpectralRiskMeasures(dailyReturns, riskAversionGamma);

        Assert.IsNotNull(result);
        Assert.AreEqual((decimal)riskAversionGamma, result.RiskAversionGamma);
        Assert.IsTrue(result.SpectralRiskMeasure1dPercent > 0m);
        Assert.IsTrue(result.SpectralRiskMeasureAnnualizedPercent > 0m);
        Assert.IsTrue(result.ClassicalVaR99Percent > 0m);
        Assert.IsTrue(result.ClassicalExpectedShortfall99Percent > 0m);
        Assert.IsTrue(result.TailSeverityPremiumRatio > 0m);
        Assert.IsTrue(result.ExponentialSpectrumConcentrationRatio > 1.0m);

        // 验证 4 级尾部极值阶梯
        Assert.AreEqual(4, result.TailTierList.Count);
        decimal sumSpectrumWeights = result.TailTierList.Sum(t => t.CumulativeSpectrumWeightPercent);
        Assert.IsTrue(Math.Abs(sumSpectrumWeights - 100.0m) < 0.5m, $"Spectrum weights sum {sumSpectrumWeights} should be ~100%");

        foreach (var tier in result.TailTierList)
        {
            Assert.IsFalse(string.IsNullOrEmpty(tier.TailTierName));
            Assert.IsTrue(tier.TierSpectralRiskContributionPercent >= 0m);
            Assert.IsFalse(string.IsNullOrEmpty(tier.TierRiskSeverityBadge));
        }

        Assert.IsTrue(result.SpectralExecutiveVerdict.Contains("Acerbi 指数谱在险测度审定"));
    }

    [TestMethod]
    public void Test_RiskBudgetDriftCorridor_ComputesDriftAndSmoothRebalanceTurnover()
    {
        var components = CreateSampleComponents();

        var result = QuantCalculator.CalculateRiskBudgetDriftAndRebalance(components);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.TotalRiskContributionDriftIndex >= 0m);
        Assert.IsFalse(string.IsNullOrEmpty(result.CorridorOverallStatus));
        Assert.IsTrue(result.BreachedAssetCount >= 0);
        Assert.IsTrue(result.WarningAssetCount >= 0);
        Assert.IsTrue(result.RequiredSmoothRebalanceTurnoverPercent >= 0m);
        Assert.IsTrue(result.EstimatedRebalanceCostBps >= 0m);

        // 验证各标的资产
        Assert.AreEqual(components.Count, result.AssetItemList.Count);
        decimal sumPrc = result.AssetItemList.Sum(a => a.PercentageRiskContributionPercent);
        Assert.IsTrue(Math.Abs(sumPrc - 100.0m) < 1.0m, $"PRC sum {sumPrc} should be ~100%");

        decimal sumSmoothWeights = result.AssetItemList.Sum(a => a.SmoothRebalanceTargetWeightPercent);
        Assert.IsTrue(Math.Abs(sumSmoothWeights - 100.0m) < 1.0m, $"Smooth target weights sum {sumSmoothWeights} should be ~100%");

        foreach (var item in result.AssetItemList)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.FundCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.CorridorStatusBadge));
            Assert.IsTrue(item.InnerBandLowerPercent < item.InnerBandUpperPercent);
            Assert.IsTrue(item.OuterBandLowerPercent < item.OuterBandUpperPercent);
        }

        Assert.IsTrue(result.RiskBudgetExecutiveVerdict.Contains("风险预算漂移走廊审定"));
    }

    [TestMethod]
    public void Test_DeflatedSharpeOverfit_ComputesPsrAndDsrWithSampleStatistics()
    {
        var components = CreateSampleComponents();
        var dailyNavs = components[0].Fund.NavHistory;
        var dailyReturns = new List<decimal>();
        for (int i = 1; i < dailyNavs.Count; i++)
        {
            decimal pRet = 0m;
            decimal totalW = components.Sum(c => c.Weight);
            foreach (var (fund, weight) in components)
            {
                pRet += (weight / totalW) * fund.NavHistory[i].DailyReturn;
            }
            dailyReturns.Add(pRet);
        }

        int trialsN = 100;
        var result = QuantCalculator.CalculateProbabilisticAndDeflatedSharpe(dailyReturns, trialsN, 0.0);

        Assert.IsNotNull(result);
        Assert.AreEqual(trialsN, result.NumberOfTrialsTestedN);
        Assert.AreEqual(dailyReturns.Count, result.SampleObservationsT);
        Assert.IsTrue(result.ProbabilisticSharpeRatioPercent >= 0m && result.ProbabilisticSharpeRatioPercent <= 100m);
        Assert.IsTrue(result.DeflatedSharpeRatioPercent >= 0m && result.DeflatedSharpeRatioPercent <= 100m);
        Assert.IsTrue(result.FalseDiscoveryProbabilityPercent >= 0m && result.FalseDiscoveryProbabilityPercent <= 100m);

        // 验证 FDP + DSR 互补接近 100%
        decimal sumDsrFdp = result.DeflatedSharpeRatioPercent + result.FalseDiscoveryProbabilityPercent;
        Assert.IsTrue(Math.Abs(sumDsrFdp - 100.0m) < 0.2m, $"DSR + FDP ({sumDsrFdp}) should sum to 100%");

        Assert.IsFalse(string.IsNullOrEmpty(result.AlphaGenuineStatusBadge));
        Assert.IsTrue(result.DsrExecutiveVerdict.Contains("Bailey-de Prado 概率与通缩夏普审定"));
    }

    [TestMethod]
    public void Test_PortfolioEngine_Phase32PipelineIntegration_PopulatesAllPhase32Results()
    {
        var components = CreateSampleComponents();

        // 运行完整投资组合分析引擎
        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.IdzorekBlackLitterman, "Phase 32 IdzorekBlackLitterman must be populated");
        Assert.IsNotNull(result.SpectralRiskMeasure, "Phase 32 SpectralRiskMeasure must be populated");
        Assert.IsNotNull(result.RiskBudgetDriftCorridor, "Phase 32 RiskBudgetDriftCorridor must be populated");
        Assert.IsNotNull(result.DeflatedSharpeOverfit, "Phase 32 DeflatedSharpeOverfit must be populated");

        // 验证 HTML 研报导出包含 Phase 32 四大核心模块
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);
        Assert.IsFalse(string.IsNullOrEmpty(html));
        Assert.IsTrue(html.Contains("三十、Goldman Sachs GSAM & Idzorek (2005) 显式置信度百分比校准 Black-Litterman 优化 (Phase 32)"), "HTML report must contain Idzorek Black-Litterman section");
        Assert.IsTrue(html.Contains("三十一、BCBS & Carlo Acerbi 一致性连续指数风险厌恶谱风险测度 (Spectral Risk Measures, SRM) (Phase 32)"), "HTML report must contain Spectral Risk Measure section");
        Assert.IsTrue(html.Contains("三十二、Bridgewater All-Weather 动态风险预算漂移走廊 (RCDI) 与平滑再平衡引擎 (Phase 32)"), "HTML report must contain Risk Budget Drift Corridor section");
        Assert.IsTrue(html.Contains("三十三、Marcos Lopez de Prado & David Bailey 概率夏普 (PSR) 与多重回测试验通缩夏普 (DSR) 策略过拟合检验 (Phase 32)"), "HTML report must contain Deflated Sharpe Overfit section");
    }
}
