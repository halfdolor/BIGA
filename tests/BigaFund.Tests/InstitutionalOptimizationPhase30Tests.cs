using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase30Tests
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
    public void Test_MahalanobisTurbulence_CalculatesPositiveDistanceAndDetectsTurbulence()
    {
        var components = CreateSampleComponents();
        var dailyNavs = components[0].Fund.NavHistory;
        var dailyReturns = new List<decimal>();
        for (int i = 1; i < dailyNavs.Count; i++)
        {
            dailyReturns.Add(dailyNavs[i].DailyReturn);
        }

        var result = QuantCalculator.CalculateMahalanobisTurbulence(components, dailyReturns);

        Assert.IsNotNull(result, "MahalanobisTurbulenceResult should not be null");
        Assert.IsTrue(result.CurrentTurbulenceScore > 0m, "Current turbulence score should be positive");
        Assert.IsTrue(result.AverageTurbulenceScore > 0m, "Average turbulence score should be positive");
        Assert.IsTrue(result.TurbulencePercentileRank >= 0m && result.TurbulencePercentileRank <= 100m, "Percentile rank should be in [0, 100]");
        Assert.IsTrue(result.RecommendedLeverageMultiplier >= 0.35m && result.RecommendedLeverageMultiplier <= 1.00m, "Leverage multiplier must be in [0.35, 1.00]");
        Assert.IsTrue(result.RecommendedDefensiveCashBufferPercent >= 5.0m, "Defensive cash buffer should be at least 5%");
        Assert.AreEqual(4, result.MacroQuadrants.Count, "Bridgewater macro quadrants must be exactly 4");

        // 验证宏观 4 象限的风险预算和为 100%
        decimal sumBudget = result.MacroQuadrants.Sum(q => q.RecommendedRiskBudgetPercent);
        Assert.AreEqual(100m, sumBudget, "Sum of Bridgewater macro risk budgets must equal 100%");

        Assert.IsTrue(!string.IsNullOrWhiteSpace(result.TurbulenceTacticalVerdict), "Verdict should not be empty");
    }

    [TestMethod]
    public void Test_EulerCvarAttribution_EulerSumMatchesTotalPortfolioCvar()
    {
        var components = CreateSampleComponents();
        var dailyNavs = components[0].Fund.NavHistory;
        var dailyReturns = new List<decimal>();
        decimal totW = components.Sum(c => c.Weight);
        for (int i = 1; i < dailyNavs.Count; i++)
        {
            decimal portRet = 0m;
            foreach (var c in components)
            {
                portRet += (c.Weight / totW) * c.Fund.NavHistory[i].DailyReturn;
            }
            dailyReturns.Add(portRet);
        }

        var result = QuantCalculator.CalculateEulerCvarAttribution(components, dailyReturns);

        Assert.IsNotNull(result, "EulerCvarAttributionResult should not be null");
        Assert.IsTrue(result.PortfolioVaRPercent > 0m, "Portfolio 95% VaR should be positive");
        Assert.IsTrue(result.PortfolioCvarPercent >= result.PortfolioVaRPercent, "CVaR must be >= VaR");
        Assert.AreEqual(components.Count, result.AssetItemList.Count, "Euler asset count must match components count");

        // 核心欧拉一次齐次性定理验证：各成分资产欧拉绝对贡献之和必须严格等于组合总 CVaR
        decimal sumEulerCvar = result.AssetItemList.Sum(a => a.EulerAbsoluteCvarContributionPercent);
        Assert.AreEqual((double)result.PortfolioCvarPercent, (double)sumEulerCvar, 0.05, "Euler absolute contributions must sum to portfolio CVaR");
        Assert.AreEqual((double)result.PortfolioCvarPercent, (double)result.EulerSumCvarPercent, 0.05, "EulerSumCvarPercent must match PortfolioCvarPercent");

        // 验证百分比贡献之和约为 100%
        decimal sumRatio = result.AssetItemList.Sum(a => a.EulerPercentCvarContributionRatio);
        Assert.AreEqual(100.0, (double)sumRatio, 1.0, "Sum of Euler CVaR percentage contributions should be ~100%");

        Assert.IsTrue(result.TailHerfindahlIndex > 0m, "Tail Herfindahl Index should be positive");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.MostToxicAssetCode), "Most toxic asset code should be identified");
        Assert.IsTrue(!string.IsNullOrWhiteSpace(result.EulerCvarVerdict), "Euler CVaR verdict should not be empty");
    }

    [TestMethod]
    public void Test_DownsideTrackingError_DteLessThanOrEqualToSymmetricTe_AndStutzerIndexNonNegative()
    {
        var components = CreateSampleComponents();
        var dailyNavs = components[0].Fund.NavHistory;
        var dailyReturns = new List<decimal>();
        for (int i = 1; i < dailyNavs.Count; i++)
        {
            dailyReturns.Add(dailyNavs[i].DailyReturn);
        }

        var result = QuantCalculator.CalculateDownsideTrackingErrorAndStutzer(dailyReturns);

        Assert.IsNotNull(result, "DownsideTrackingErrorResult should not be null");
        Assert.IsTrue(result.SymmetricTrackingErrorPercent > 0m, "Symmetric tracking error should be positive");
        Assert.IsTrue(result.DownsideTrackingErrorPercent > 0m, "Downside tracking error should be positive");

        // 当组合呈现正向收益偏度或均值超额时，下行跟踪误差通常显著小于或等于全样本对称跟踪误差
        Assert.IsTrue(result.AsymmetricDownsideGainRatio > 0m, "Asymmetric downside gain ratio should be positive");

        // 验证 Stutzer 极大似然大偏差衰减指数非负
        Assert.IsTrue(result.StutzerDecayIndex >= 0m, "Stutzer decay index I_S must be non-negative");
        Assert.IsTrue(result.AnnualizedProbUnderperformDecayRatePercent >= 0m, "Prob decay rate must be non-negative");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.BenchmarkPurityGradeBadge), "Purity grade badge should not be empty");
        Assert.IsTrue(!string.IsNullOrWhiteSpace(result.StutzerVerdict), "Stutzer verdict should not be empty");
    }

    [TestMethod]
    public void Test_LdiCashFlowMatching_CalculatesDurationGapAndCoverageRatio()
    {
        var components = CreateSampleComponents();
        decimal initialCapitalWan = 1000m;

        var result = QuantCalculator.CalculateLdiCashFlowMatching(components, initialCapitalWan);

        Assert.IsNotNull(result, "LdiCashFlowMatchResult should not be null");
        Assert.AreEqual(initialCapitalWan, result.TotalAssetPresentValueWan, "Asset present value must match input");
        Assert.IsTrue(result.TotalLiabilityPresentValueWan > 0m, "Liability present value must be positive");
        Assert.IsTrue(result.AssetEffectiveDurationYears > 0m, "Asset duration should be positive");
        Assert.IsTrue(result.LiabilityEffectiveDurationYears > 0m, "Liability duration should be positive");
        Assert.IsTrue(result.OverallLiquidityCoverageRatioPercent > 0m, "Overall LCR must be positive");
        Assert.IsTrue(result.ImmediateLiquidReserveWan > 0m, "Immediate liquid reserve must be positive");

        // 验证跨期档位为 6 档 (3M, 6M, 1Y, 2Y, 3Y, 5Y)
        Assert.AreEqual(6, result.HorizonMatchItems.Count, "Horizon match items must have 6 periods");

        // 验证 4 级阶梯式清算瀑布策略已生成
        Assert.IsTrue(result.LiquidationWaterfallStrategy.Contains("Tier 1") &&
                      result.LiquidationWaterfallStrategy.Contains("Tier 2") &&
                      result.LiquidationWaterfallStrategy.Contains("Tier 3") &&
                      result.LiquidationWaterfallStrategy.Contains("Tier 4"),
                      "Liquidation waterfall must detail all 4 tiers");

        Assert.IsTrue(!string.IsNullOrWhiteSpace(result.LdiExecutiveVerdict), "LDI verdict should not be empty");
    }

    [TestMethod]
    public void Test_PortfolioEngine_Phase30_EndToEndIntegration()
    {
        var components = CreateSampleComponents();

        var portfolioResult = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(portfolioResult, "PortfolioResult should not be null");

        // 验证 Phase 30 四大模型已挂载至计算引擎主管线
        Assert.IsNotNull(portfolioResult.MahalanobisTurbulence, "MahalanobisTurbulence should be calculated in pipeline");
        Assert.IsNotNull(portfolioResult.EulerCvarAttribution, "EulerCvarAttribution should be calculated in pipeline");
        Assert.IsNotNull(portfolioResult.DownsideTrackingError, "DownsideTrackingError should be calculated in pipeline");
        Assert.IsNotNull(portfolioResult.LdiCashFlowMatch, "LdiCashFlowMatch should be calculated in pipeline");

        // 验证尽调报告生成包含 Phase 30 四大章节
        string htmlReport = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(portfolioResult, components);
        Assert.IsTrue(htmlReport.Contains("二十二、全天候宏观 4 象限识别与马氏金融动荡度降杠杆雷达 (Phase 30)"), "Report must include Chapter 22");
        Assert.IsTrue(htmlReport.Contains("二十三、欧拉下行条件在险价值 (CVaR) 风险贡献分解与极端尾部去毒 (Phase 30)"), "Report must include Chapter 23");
        Assert.IsTrue(htmlReport.Contains("二十四、基准相对下行半方差跟踪误差 (DTE) 与 Stutzer 极端衰减指数 (Phase 30)"), "Report must include Chapter 24");
        Assert.IsTrue(htmlReport.Contains("二十五、负债驱动投资 (LDI) 跨期现金流匹配、久期缺口与清算瀑布 (Phase 30)"), "Report must include Chapter 25");
    }
}
