using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase35Tests
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
            Name = "易方达中小盘混合 (高波高Beta成长龙头)",
            FundSize = "180.5亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.20, 0.26, seed: 111)
        };
        var fundB = new FundDetail
        {
            Code = "510300",
            Name = "华泰柏瑞沪深300ETF (核心基石宽基蓝筹)",
            FundSize = "850.0亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.08, 0.16, seed: 222)
        };
        var fundC = new FundDetail
        {
            Code = "000001",
            Name = "华夏纯债中短债A (稳健固收中枢)",
            FundSize = "65.2亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.045, 0.028, seed: 333)
        };
        var fundD = new FundDetail
        {
            Code = "000509",
            Name = "易方达现金增利货币A (极低波流动性底仓)",
            FundSize = "320.0亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.022, 0.006, seed: 444)
        };

        return new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 35m),
            (fundB, 30m),
            (fundC, 25m),
            (fundD, 10m)
        };
    }

    private static List<decimal> CreateSampleDailyReturns(int days = 250, double annRet = 0.10, double annVol = 0.18, int seed = 777)
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
    public void Test_Phase35_FrtbPlaAndTrafficLightBacktest()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.12, 0.16, 888);

        var result = QuantCalculator.CalculateFrtbPlaAndTrafficLightBacktest(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.SpearmanRankCorrelation >= -1.0m && result.SpearmanRankCorrelation <= 1.0m);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.SpearmanZoneStatus));
        Assert.IsTrue(result.KolmogorovSmirnovStatistic >= 0.0m && result.KolmogorovSmirnovStatistic <= 1.0m);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.KsZoneStatus));
        Assert.IsTrue(result.Rolling250DaysVaRExceedanceCount >= 0);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.BaselTrafficLightZone));
        Assert.IsTrue(result.RegulatoryCapitalMultiplierAddOn >= 0.0m);
        Assert.IsTrue(result.TotalCapitalMultiplier >= 3.0m);
        Assert.IsNotNull(result.RecentObservations);
        Assert.IsTrue(result.RecentObservations.Count > 0);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.OverallPlaComplianceStatus));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.PlaExecutiveVerdict));
    }

    [TestMethod]
    public void Test_Phase35_PodShopCapitalAllocationAndDerisking()
    {
        var components = CreateSampleComponents();
        decimal totalCap = 2000m;

        var result = QuantCalculator.CalculatePodShopCapitalAllocationAndDerisking(components, totalCap);

        Assert.IsNotNull(result);
        Assert.AreEqual(totalCap, result.TotalFundCapitalWan);
        Assert.IsTrue(result.ActiveWorkingCapitalWan > 0m);
        Assert.IsTrue(result.ActiveWorkingCapitalWan + result.CentralReservePoolWan == totalCap);
        Assert.AreEqual(components.Count, result.PodList.Count);
        Assert.AreEqual(components.Count, result.ActivePodsCount + result.DeriskedPodsCount + result.StoppedOutPodsCount);

        foreach (var pod in result.PodList)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(pod.PodId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(pod.PodName));
            Assert.IsTrue(pod.AllocatedCapitalWan > 0m);
            Assert.IsTrue(pod.HighWaterMarkNav >= 0.1m);
            Assert.IsTrue(pod.CurrentDrawdownPercent >= 0m);
            Assert.IsTrue(pod.PeakDrawdownPercent >= 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(pod.DeriskingStatus));
            Assert.IsTrue(pod.PostAdjustmentCapitalWan >= 0m);
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.PodGovernanceHealthBadge));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.PodExecutiveVerdict));
    }

    [TestMethod]
    public void Test_Phase35_FactorOrthogonalizationAndPureLoadings()
    {
        var components = CreateSampleComponents();

        var result = QuantCalculator.CalculateFactorOrthogonalizationAndPureLoadings(components);

        Assert.IsNotNull(result);
        Assert.AreEqual(5, result.FactorCount);
        Assert.AreEqual(5, result.FactorList.Count);
        Assert.IsTrue(result.AverageCrossCorrelationAfter < 0.001m, "Löwdin 对称正交化后跨因子相关性必须严格归零。");
        Assert.IsTrue(result.OrthogonalityAccuracy > 0.85m, "Löwdin 对称正交化需保证极高保真度。");
        Assert.AreEqual(components.Count, result.AssetLoadingList.Count);

        foreach (var al in result.AssetLoadingList)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(al.FundCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(al.FundName));
            Assert.IsTrue(al.ResidualSpecificRiskPercent >= 0m && al.ResidualSpecificRiskPercent <= 100m);
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.OrthogonalExecutiveVerdict));
    }

    [TestMethod]
    public void Test_Phase35_NonlinearDistanceAndMutualInformationAlpha()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.10, 0.15, 999);

        var result = QuantCalculator.CalculateNonlinearDistanceAndMutualInformationAlpha(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.PortfolioAverageDistanceCorr >= 0.0m && result.PortfolioAverageDistanceCorr <= 1.0m);
        Assert.IsTrue(result.PortfolioAverageMutualInformationBits >= 0.0m);
        Assert.IsTrue(result.NonlinearAlphaGainPercentage >= 0.0m);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TopNonlinearAlphaDriverCode));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TopNonlinearAlphaDriverName));
        Assert.AreEqual(components.Count, result.AssetFeatureList.Count);

        // 验证排序正确性 (Rank 1, 2, 3, ...)
        for (int i = 0; i < result.AssetFeatureList.Count; i++)
        {
            Assert.AreEqual(i + 1, result.AssetFeatureList[i].FeatureImportanceRank);
            Assert.IsTrue(result.AssetFeatureList[i].DistanceCorrelation >= 0m && result.AssetFeatureList[i].DistanceCorrelation <= 1m);
            Assert.IsTrue(result.AssetFeatureList[i].MutualInformationBits >= 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.AssetFeatureList[i].DependenceClassification));
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.NonlinearExecutiveVerdict));
    }

    [TestMethod]
    public void Test_Phase35_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        // 运行完整投资组合分析引擎
        var portResult = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(portResult);
        Assert.IsNotNull(portResult.FrtbPlaAndTrafficLight);
        Assert.IsNotNull(portResult.PodShopCapitalAllocation);
        Assert.IsNotNull(portResult.FactorOrthogonalization);
        Assert.IsNotNull(portResult.NonlinearDistanceMutualInfo);

        // 验证 HTML 研报导出中 Phase 35 章节
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(portResult, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("四十二、BIS / BCBS FRTB 内部模型法损益归因 (PLA)"));
        Assert.IsTrue(html.Contains("四十三、Millennium / Point72 Pod Shop 多策略单元动态资本分配"));
        Assert.IsTrue(html.Contains("四十四、MSCI Barra & Axioma 风格因子 Löwdin 对称正交化"));
        Assert.IsTrue(html.Contains("四十五、Two Sigma & Citadel 距离相关系数 (dCor) 与互信息 (MI)"));
    }
}
