using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase26Tests
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
        var fundA = new FundDetail { Code = "110011", Name = "易方达中小盘混合 (偏股型)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.15, 0.22, seed: 111) };
        var fundB = new FundDetail { Code = "510300", Name = "华泰柏瑞沪深300ETF (宽基指数)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.08, 0.16, seed: 222) };
        var fundC = new FundDetail { Code = "000001", Name = "华夏纯债中短债A (固定收益)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.042, 0.032, seed: 333) };
        var fundD = new FundDetail { Code = "000509", Name = "易方达现金增利货币A (流动性货币)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.021, 0.004, seed: 444) };

        return new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 35.0m),
            (fundB, 25.0m),
            (fundC, 25.0m),
            (fundD, 15.0m)
        };
    }

    [TestMethod]
    public void Test_CalculateLedoitWolfShrinkageCovariance_OptimalIntensityAndConditionNumber()
    {
        var components = CreateSampleComponents();
        var result = QuantCalculator.CalculateLedoitWolfShrinkageCovariance(components);

        Assert.IsNotNull(result, "Ledoit-Wolf shrinkage result should not be null.");
        Assert.IsTrue(result.OptimalShrinkageIntensity >= 0.0m && result.OptimalShrinkageIntensity <= 1.0m, "Optimal shrinkage intensity delta* must be in [0, 1].");
        Assert.IsTrue(result.OptimalShrinkagePercent >= 0.0m && result.OptimalShrinkagePercent <= 100.0m, "Optimal shrinkage percentage must be in [0%, 100%].");
        Assert.AreEqual(6, result.CovarianceItems.Count, "4 components should produce C(4,2) = 6 pairwise covariance items.");
        Assert.IsTrue(result.SampleConditionNumber > 0m, "Sample condition number must be positive.");
        Assert.IsTrue(result.ShrunkConditionNumber > 0m, "Shrunk condition number must be positive.");
        Assert.IsTrue(result.ConditionNumberImprovementRatio >= 1.0m, "Shrinkage must improve (or maintain) the condition number.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.AnalyticalSummary), "Analytical summary must be present.");
    }

    [TestMethod]
    public void Test_FitHamiltonMarkovRegimeSwitching_StatesAndFilteredProbabilities()
    {
        var components = CreateSampleComponents();
        var navs = components[0].Fund.NavHistory.Select(n => (decimal)n.DailyReturn).ToList();
        var result = QuantCalculator.FitHamiltonMarkovRegimeSwitching(navs);

        Assert.IsNotNull(result, "Hamilton Markov regime switching result should not be null.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.CurrentRegime), "Current regime should be identified.");
        Assert.IsTrue(result.CurrentRegimeProbabilityPercent >= 50.0m && result.CurrentRegimeProbabilityPercent <= 100.0m, "Current regime probability must be >= 50%.");
        Assert.IsTrue(result.RegimeStates.Count == 2, "Should identify two states: Bull and Bear.");
        Assert.IsTrue(result.BullExpectedDurationMonths > 0m, "Bull duration should be positive.");
        Assert.IsTrue(result.BearExpectedDurationMonths > 0m, "Bear duration should be positive.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.DynamicBlackLittermanPriorShift), "BL prior shift must be non-empty.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TacticalAssetAllocationAdvice), "Tactical advice must be non-empty.");
    }

    [TestMethod]
    public void Test_SimulateMertonJumpDiffusionPaths_PoissonJumpsAndFatTailVaR()
    {
        var components = CreateSampleComponents();
        var result = QuantCalculator.SimulateMertonJumpDiffusionPaths(components, horizonDays: 60, numPaths: 500);

        Assert.IsNotNull(result, "Merton jump diffusion result should not be null.");
        Assert.IsTrue(result.JumpIntensityLambda >= 0m, "Jump intensity lambda should be non-negative.");
        Assert.IsTrue(result.TrajectoryPoints.Count >= 5, "Trajectory points should cover milestone forecast horizons.");
        Assert.AreEqual(60, result.TrajectoryPoints.Last().TradingDay, "Last checkpoint must be horizonDays.");

        // 验证轨迹分位数单调性: 5%分位数 <= 50%中位数 <= 95%分位数
        foreach (var pt in result.TrajectoryPoints)
        {
            Assert.IsTrue(pt.Percentile5Nav <= pt.MedianNav, "5% Percentile Nav <= Median Nav");
            Assert.IsTrue(pt.MedianNav <= pt.Percentile95Nav, "Median Nav <= 95% Percentile Nav");
        }

        Assert.IsTrue(result.JumpAdjustedVaR95Percent > 0m, "Jump-adjusted 95% VaR must be positive.");
        Assert.IsTrue(result.TailRiskUnderestimationPercent >= 0m, "Fat-tail underestimation should be >= 0%.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.JumpScenarioAudit), "Audit description must be non-empty.");
    }

    [TestMethod]
    public void Test_CalculateLiquidityAdjustedVaR_ScaleSensitivity()
    {
        var components = CreateSampleComponents();
        var result = QuantCalculator.CalculateLiquidityAdjustedVaR(components, totalCapitalTenThousand: 2000m);

        Assert.IsNotNull(result, "L-VaR result should not be null.");
        Assert.IsTrue(result.PureMarketVaR95Wan > 0m, "Pure market VaR must be positive.");
        Assert.IsTrue(result.TotalLVaR95Wan >= result.PureMarketVaR95Wan, "Total L-VaR must be >= pure market VaR.");
        Assert.IsTrue(result.LiquidityRiskMultiplier >= 1.0m, "Liquidity multiplier must be >= 1.0x.");
        Assert.IsTrue(result.ScaleScenarios.Count >= 5, "Scale scenarios should cover multiple capital tiers.");

        // 验证跨规模递增严格单调性
        for (int i = 1; i < result.ScaleScenarios.Count; i++)
        {
            var prev = result.ScaleScenarios[i - 1];
            var curr = result.ScaleScenarios[i];
            Assert.IsTrue(curr.CapitalScaleWan > prev.CapitalScaleWan, "Capital scales must be strictly increasing.");
            Assert.IsTrue(curr.PureMarketVaR95Wan > prev.PureMarketVaR95Wan, "Pure market VaR must scale with capital.");
            Assert.IsTrue(curr.MarketImpactCostWan >= prev.MarketImpactCostWan, "Market impact cost must scale with capital.");
            Assert.IsTrue(curr.TotalLVaRWan > prev.TotalLVaRWan, "Total L-VaR must scale with capital.");
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.InstitutionalDeskAdvice), "Desk advice must not be empty.");
    }

    [TestMethod]
    public void Test_PortfolioTearsheet_MonthlyHeatmapAndUnderwater()
    {
        var components = CreateSampleComponents();
        var navList = components[0].Fund.NavHistory;
        var result = QuantCalculator.GeneratePortfolioTearsheetData(navList);

        Assert.IsNotNull(result, "Tearsheet result should not be null.");
        Assert.IsTrue(result.MonthlyHeatmapRows.Count > 0, "Should generate monthly heatmap rows.");
        Assert.IsTrue(result.TotalMonths > 0, "Total months should be > 0.");
        Assert.IsTrue(result.WinningMonths <= result.TotalMonths, "Winning months must not exceed total months.");
        Assert.IsTrue(result.MonthlyWinRatePercent >= 0m && result.MonthlyWinRatePercent <= 100m, "Win rate must be in [0%, 100%].");
        Assert.IsTrue(result.UnderwaterPoints.Count > 0, "Underwater points should be populated.");
        Assert.IsTrue(result.MaxUnderwaterDays >= 0, "Max underwater days must be >= 0.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TearsheetExecutiveSummary), "Executive summary must be present.");
    }

    [TestMethod]
    public void Test_PortfolioEngine_Phase26EndToEnd()
    {
        var components = CreateSampleComponents();
        var inputComponents = components.Select(c => (c.Fund, c.Weight)).ToList();

        var result = PortfolioEngine.CalculatePortfolio(inputComponents, riskFreeRate: 2.0m);

        Assert.IsNotNull(result, "PortfolioResult should not be null.");

        // 验证 Phase 26 挂载的 5 大终极引擎全部成功运转
        Assert.IsNotNull(result.LedoitWolfShrinkage, "LedoitWolfShrinkage should be populated.");
        Assert.AreEqual(6, result.LedoitWolfShrinkage.CovarianceItems.Count);

        Assert.IsNotNull(result.MarkovRegimeSwitching, "MarkovRegimeSwitching should be populated.");
        Assert.IsTrue(result.MarkovRegimeSwitching.RegimeStates.Count == 2);

        Assert.IsNotNull(result.MertonJumpDiffusion, "MertonJumpDiffusion should be populated.");
        Assert.IsTrue(result.MertonJumpDiffusion.TrajectoryPoints.Count > 0);

        Assert.IsNotNull(result.LiquidityAdjustedVaR, "LiquidityAdjustedVaR should be populated.");
        Assert.IsTrue(result.LiquidityAdjustedVaR.ScaleScenarios.Count > 0);

        Assert.IsNotNull(result.PortfolioTearsheet, "PortfolioTearsheet should be populated.");
        Assert.IsTrue(result.PortfolioTearsheet.MonthlyHeatmapRows.Count > 0);

        // 验证研报生成与 Phase 26 章节
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, inputComponents);
        Assert.IsTrue(html.Contains("Ledoit-Wolf 渐近最优收缩协方差与矩阵良态优化"), "HTML must contain Ledoit-Wolf section.");
        Assert.IsTrue(html.Contains("Hamilton 隐藏马尔可夫双状态"), "HTML must contain Markov regime section.");
        Assert.IsTrue(html.Contains("Merton 泊松跳跃扩散极端前瞻推演"), "HTML must contain Merton jump section.");
        Assert.IsTrue(html.Contains("规模敏感型流动性调整在险价值 (L-VaR)"), "HTML must contain L-VaR section.");
        Assert.IsTrue(html.Contains("专业量化 Tearsheet 仪表盘"), "HTML must contain Tearsheet section.");
    }
}
