using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase31Tests
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
    public void Test_KalmanFilterStyleDrift_CalculatesDynamicBetaAndDetectsDrift()
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

        var result = QuantCalculator.CalculateKalmanFilterStyleDrift(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.AreEqual(components.Count, result.AssetItemList.Count);
        Assert.IsTrue(result.PortfolioInstantBeta >= 0m, "Portfolio Instant Beta should be non-negative");
        Assert.IsTrue(result.PortfolioMeanBeta >= 0m, "Portfolio Mean Beta should be non-negative");
        Assert.IsFalse(string.IsNullOrEmpty(result.MostDriftedAssetCode));
        Assert.IsTrue(result.AverageStyleDriftIndex >= 0m);

        foreach (var item in result.AssetItemList)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.FundCode));
            Assert.IsTrue(item.BetaVolatilityStdDev >= 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.StyleDriftBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.StyleDriftDiagnosis));
        }

        Assert.IsTrue(result.StyleDriftExecutiveVerdict.Contains("卡尔曼时变 Beta"));
    }

    [TestMethod]
    public void Test_CardinalitySparseOptimization_SelectsKAssetsAndRespectsTurnoverBudget()
    {
        var components = CreateSampleComponents();
        int targetK = 3;
        decimal turnoverBudget = 35.0m;

        var result = QuantCalculator.SolveCardinalitySparseOptimization(components, targetK, turnoverBudget);

        Assert.IsNotNull(result);
        Assert.AreEqual(components.Count, result.TotalCandidatesN);
        Assert.AreEqual(targetK, result.TargetCardinalityK);
        Assert.AreEqual(turnoverBudget, result.TurnoverBudgetPercent);

        // 验证精选入选数量不多于 K
        int selectedCount = result.AssetItemList.Count(a => a.IsSelectedInSparseSubset);
        Assert.IsTrue(selectedCount <= targetK, $"Selected count {selectedCount} should be <= {targetK}");

        // 验证换手率在预算之内
        Assert.IsTrue(result.ActualTurnoverPercent <= turnoverBudget + 0.1m, "Actual turnover should not exceed budget");

        // 验证稀疏权重严格归一化为 100%
        decimal sumWeight = result.AssetItemList.Sum(a => a.SparseOptimalWeightPercent);
        Assert.IsTrue(Math.Abs(sumWeight - 100.0m) < 0.5m, $"Sum of sparse weights {sumWeight} should be ~100%");

        // 验证保留率处于合理区间
        Assert.IsTrue(result.CardinalityRetentionRatioPercent >= 60.0m);
        Assert.IsTrue(result.SparseOptimizationVerdict.Contains("Axioma 基数与换手约束优化审定"));
    }

    [TestMethod]
    public void Test_DeltaCoVaRSystemicRisk_CalculatesContagionRankAndVulnerability()
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

        var result = QuantCalculator.CalculateDeltaCoVaRSystemicRisk(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.AreEqual(components.Count, result.AssetItemList.Count);
        Assert.IsTrue(result.AverageDeltaCoVaRPercent >= 0m);
        Assert.IsFalse(string.IsNullOrEmpty(result.HighestContagionAssetCode));
        Assert.IsTrue(result.HighestContagionDeltaCoVaR >= 0m);
        Assert.IsTrue(result.SystemicNetworkVulnerabilityScore >= 0m && result.SystemicNetworkVulnerabilityScore <= 100m);
        Assert.IsFalse(string.IsNullOrEmpty(result.SystemicFragilityBadge));

        // 检查各标的传染指标与排名连续性
        var ranks = result.AssetItemList.Select(a => a.SystemicContagionRank).OrderBy(r => r).ToList();
        for (int i = 0; i < ranks.Count; i++)
        {
            Assert.AreEqual(i + 1, ranks[i]);
        }

        foreach (var item in result.AssetItemList)
        {
            Assert.IsTrue(item.DeltaCoVaRContributionPercent >= 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.SystemicImportanceRating));
            Assert.IsFalse(string.IsNullOrEmpty(item.IsolationFirewallAdvice));
        }

        Assert.IsTrue(result.DeltaCoVaRVerdict.Contains("Adrian-Brunnermeier ΔCoVaR"));
    }

    [TestMethod]
    public void Test_FrtbStressedCapitalCharge_FindsStressedWindowAndScalesLiquidityHorizons()
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

        decimal capitalWan = 2000m;
        var result = QuantCalculator.CalculateFrtbStressedCapitalCharge(components, dailyReturns, capitalWan);

        Assert.IsNotNull(result);
        Assert.AreEqual(capitalWan, result.TotalPortfolioCapitalWan);
        Assert.IsTrue(result.StressedWindowStartDate <= result.StressedWindowEndDate);
        Assert.IsTrue(result.StressedWindowMaxDrawdownPercent >= 0m);
        Assert.IsTrue(result.StressedWindowAnnualizedVolatilityPercent >= 0m);

        // 验证压力 sVaR 高于或等于常态 VaR
        Assert.IsTrue(result.StressedMultiplierRatio >= 1.0m, "Stressed multiplier ratio should be >= 1.0");
        Assert.IsTrue(result.StressedVaR99Percent >= result.NormalVaR99Percent * 0.99m);

        // 验证流动性阶梯配置
        Assert.AreEqual(4, result.HorizonItemList.Count);
        decimal sumScaleCharges = result.HorizonItemList.Sum(h => h.ScaledCapitalChargeWan);
        Assert.AreEqual(result.FrtbTotalCapitalChargeWan, sumScaleCharges);
        Assert.IsTrue(result.FrtbCapitalAdequacyRatioPercent > 0m);
        Assert.IsFalse(string.IsNullOrEmpty(result.CapitalAdequacyBadge));
        Assert.IsTrue(result.FrtbExecutiveVerdict.Contains("Basel III / FRTB"));
    }

    [TestMethod]
    public void Test_PortfolioEngine_Phase31PipelineIntegration_PopulatesAllPhase31Results()
    {
        var components = CreateSampleComponents();

        // 运行完整投资组合分析引擎
        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.KalmanFilterStyleDrift, "Phase 31 KalmanFilterStyleDrift must be populated");
        Assert.IsNotNull(result.CardinalitySparseOptimization, "Phase 31 CardinalitySparseOptimization must be populated");
        Assert.IsNotNull(result.DeltaCoVaRSystemicRisk, "Phase 31 DeltaCoVaRSystemicRisk must be populated");
        Assert.IsNotNull(result.FrtbStressedCapitalCharge, "Phase 31 FrtbStressedCapitalCharge must be populated");

        // 验证 HTML 研报导出包含 Phase 31 四大核心模块
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);
        Assert.IsFalse(string.IsNullOrEmpty(html));
        Assert.IsTrue(html.Contains("二十六、卡尔曼滤波状态空间时变 Beta 与风格漂移追踪预警 (Phase 31)"), "HTML report must contain Kalman filter section");
        Assert.IsTrue(html.Contains("二十七、Axioma 基数硬约束与换手预算稀疏组合精选推荐 (Phase 31)"), "HTML report must contain Cardinality sparse optimization section");
        Assert.IsTrue(html.Contains("二十八、Adrian-Brunnermeier 条件系统性在险价值增量 ΔCoVaR 与金融传染 (Phase 31)"), "HTML report must contain Delta-CoVaR section");
        Assert.IsTrue(html.Contains("二十九、Basel III / FRTB 历史最劣 250 天压力在险价值 (sVaR) 与监管资本拨备 (Phase 31)"), "HTML report must contain FRTB Stressed Capital Charge section");
    }
}
