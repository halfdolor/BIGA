using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase11Tests
{
    private (List<NavRecord> navs, List<BenchmarkRecord> bmks) CreateSyntheticNavAndBenchmark(
        int days = 260,
        bool positiveConvexity = true)
    {
        var navs = new List<NavRecord>();
        var bmks = new List<BenchmarkRecord>();
        var baseDate = new DateTime(2023, 1, 1);

        decimal currentNav = 1.0m;
        double cumBmkRet = 0.0;

        for (int i = 0; i < days; i++)
        {
            var date = baseDate.AddDays(i);
            // 基准走势 (包含正常波动与偶发极端暴跌日)
            double bmkRet = 0.012 * Math.Sin(i * 0.15) + ((i % 15 == 0) ? -0.022 : 0.002);
            cumBmkRet += bmkRet * 100.0;

            double fundRet;
            if (positiveConvexity)
            {
                // 正凸性：基准上涨时弹性大 (1.25x)，基准下跌时防御回撤小 (0.6x)
                if (bmkRet >= 0)
                {
                    fundRet = 1.25 * bmkRet + 0.0005;
                }
                else
                {
                    fundRet = 0.60 * bmkRet + 0.0003;
                }
            }
            else
            {
                // 负凸性：涨得少，跌得多
                if (bmkRet >= 0)
                {
                    fundRet = 0.60 * bmkRet;
                }
                else
                {
                    fundRet = 1.40 * bmkRet;
                }
            }

            currentNav *= (decimal)(1.0 + fundRet);

            navs.Add(new NavRecord
            {
                Date = date,
                UnitNav = Math.Round(currentNav, 4),
                CumulativeNav = Math.Round(currentNav, 4),
                DailyReturn = Math.Round((decimal)fundRet * 100m, 2)
            });

            bmks.Add(new BenchmarkRecord
            {
                Date = date,
                CumulativeReturnRate = Math.Round((decimal)cumBmkRet, 2)
            });
        }

        return (navs, bmks);
    }

    [TestMethod]
    public void TestBullBearCapture_PositiveConvexity()
    {
        var (navs, bmks) = CreateSyntheticNavAndBenchmark(260, positiveConvexity: true);

        var capture = QuantCalculator.CalculateBullBearCapture(navs, bmks);

        Assert.IsNotNull(capture);
        Assert.IsTrue(capture.BullBeta > capture.BearBeta, $"Bull Beta ({capture.BullBeta}) should exceed Bear Beta ({capture.BearBeta}) for positive convexity.");
        Assert.IsTrue(capture.AsymmetryIndex > 0m, $"Asymmetry Index should be positive, got {capture.AsymmetryIndex}");
        Assert.IsTrue(capture.UpsideCaptureRatio > capture.DownsideCaptureRatio, $"Upside capture ({capture.UpsideCaptureRatio}%) should exceed Downside capture ({capture.DownsideCaptureRatio}%)");
        Assert.IsTrue(capture.CaptureSpread > 0m, $"Capture spread should be positive, got {capture.CaptureSpread}%");
        Assert.IsTrue(capture.ConvexityRating.Contains("极品正凸性") || capture.ConvexityRating.Contains("稳健抗跌"), $"Expected positive rating, got {capture.ConvexityRating}");
        Assert.IsFalse(string.IsNullOrEmpty(capture.AsymmetryDiagnosis));
    }

    [TestMethod]
    public void TestBullBearCapture_NegativeConvexity()
    {
        var (navs, bmks) = CreateSyntheticNavAndBenchmark(260, positiveConvexity: false);

        var capture = QuantCalculator.CalculateBullBearCapture(navs, bmks);

        Assert.IsNotNull(capture);
        Assert.IsTrue(capture.BearBeta > capture.BullBeta, $"Bear Beta ({capture.BearBeta}) should exceed Bull Beta ({capture.BullBeta}) for negative convexity.");
        Assert.IsTrue(capture.AsymmetryIndex < 0m, $"Asymmetry Index should be negative, got {capture.AsymmetryIndex}");
        Assert.IsTrue(capture.CaptureSpread < 0m, $"Capture spread should be negative, got {capture.CaptureSpread}%");
        Assert.IsTrue(capture.ConvexityRating.Contains("负凸性脆弱") || capture.ConvexityRating.Contains("承压"), $"Expected negative rating, got {capture.ConvexityRating}");
    }

    [TestMethod]
    public void TestInstitutionalDueDiligenceCard_HighQuality()
    {
        var (navs, bmks) = CreateSyntheticNavAndBenchmark(260, positiveConvexity: true);
        var metrics = QuantCalculator.CalculateMetrics(navs, "1年", bmks);

        var card = QuantCalculator.CalculateDueDiligenceCard(metrics, navs, fundSizeNum: 45.0m);

        Assert.IsNotNull(card);
        Assert.IsTrue(card.AlphaPurityScore >= 0m && card.AlphaPurityScore <= 100m);
        Assert.IsTrue(card.TimingConvexityScore >= 0m && card.TimingConvexityScore <= 100m);
        Assert.IsTrue(card.TailResilienceScore >= 0m && card.TailResilienceScore <= 100m);
        Assert.IsTrue(card.RiskAdjustedEfficiencyScore >= 0m && card.RiskAdjustedEfficiencyScore <= 100m);
        Assert.IsTrue(card.StyleDisciplineScore >= 0m && card.StyleDisciplineScore <= 100m);
        Assert.IsTrue(card.CapacityLiquidityScore >= 0m && card.CapacityLiquidityScore <= 100m);

        Assert.IsTrue(card.OverallDiligenceScore >= 0m && card.OverallDiligenceScore <= 100m);
        Assert.IsTrue(card.StarRating.Length >= 5, "Star rating should have 5 star symbols (e.g. ★★★★★)");
        Assert.IsFalse(string.IsNullOrEmpty(card.DiligenceGrade));
        Assert.IsFalse(string.IsNullOrEmpty(card.InstitutionalVerdict));
        Assert.IsTrue(card.KeyStrengths.Count > 0);
    }

    [TestMethod]
    public void TestBarraCNE6PortfolioFactorRiskAttribution_EulerVarianceAdditivity()
    {
        var fundA = new FundDetail
        {
            Code = "000001",
            Name = "华夏成长",
            BarraAttribution = new BarraAttributionResult
            {
                FactorItems = new List<BarraFactorItem>
                {
                    new() { FactorId = "Beta", FactorName = "市场贝塔", ExposureBeta = 1.10m },
                    new() { FactorId = "Size", FactorName = "市值规模", ExposureBeta = 0.40m },
                    new() { FactorId = "Value", FactorName = "价值风格", ExposureBeta = -0.20m },
                    new() { FactorId = "Momentum", FactorName = "动量效应", ExposureBeta = 0.30m },
                    new() { FactorId = "Volatility", FactorName = "高低波动", ExposureBeta = 0.25m },
                    new() { FactorId = "Quality", FactorName = "盈利质量", ExposureBeta = 0.50m }
                }
            }
        };

        var fundB = new FundDetail
        {
            Code = "000002",
            Name = "易方达稳健",
            BarraAttribution = new BarraAttributionResult
            {
                FactorItems = new List<BarraFactorItem>
                {
                    new() { FactorId = "Beta", FactorName = "市场贝塔", ExposureBeta = 0.80m },
                    new() { FactorId = "Size", FactorName = "市值规模", ExposureBeta = -0.10m },
                    new() { FactorId = "Value", FactorName = "价值风格", ExposureBeta = 0.60m },
                    new() { FactorId = "Momentum", FactorName = "动量效应", ExposureBeta = 0.10m },
                    new() { FactorId = "Volatility", FactorName = "高低波动", ExposureBeta = -0.30m },
                    new() { FactorId = "Quality", FactorName = "盈利质量", ExposureBeta = 0.20m }
                }
            }
        };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 60m),
            (fundB, 40m)
        };

        var individualDailyReturns = new Dictionary<string, List<double>>
        {
            ["000001"] = Enumerable.Range(0, 100).Select(i => 0.01 * Math.Sin(i)).ToList(),
            ["000002"] = Enumerable.Range(0, 100).Select(i => 0.008 * Math.Cos(i)).ToList()
        };

        double totalPortfolioVol = 15.5;
        var result = PortfolioEngine.CalculatePortfolioFactorRiskAttribution(components, individualDailyReturns, totalPortfolioVol);

        Assert.IsNotNull(result);
        Assert.AreEqual(6, result.FactorItems.Count);
        Assert.IsTrue(result.TotalActiveVolatility > 0m);

        // 验证欧拉方差分解守恒率: FactorRiskPercent + SpecificRiskPercent == 100%
        decimal percentSum = result.FactorRiskPercent + result.SpecificRiskPercent;
        Assert.AreEqual(100.0m, Math.Round(percentSum, 1), "FactorRiskPercent + SpecificRiskPercent must sum to 100%");

        Assert.IsFalse(string.IsNullOrEmpty(result.DominantFactorTilt));
        Assert.IsFalse(string.IsNullOrEmpty(result.RiskAttributionProfile));
        Assert.IsFalse(string.IsNullOrEmpty(result.DiagnosticSummary));
    }

    [TestMethod]
    public void TestKellyAndTargetVolatility_ContinuousAllocation()
    {
        var compA = (Fund: new FundDetail { Code = "A", Name = "基金A" }, Weight: 50m);
        var compB = (Fund: new FundDetail { Code = "B", Name = "基金B" }, Weight: 50m);
        var components = new List<(FundDetail Fund, decimal Weight)> { compA, compB };

        // 构造两个资产的收益率
        var rnd = new Random(42);
        var individualDailyReturns = new Dictionary<string, List<double>>
        {
            ["A"] = Enumerable.Range(0, 120).Select(_ => rnd.NextDouble() * 0.03 - 0.01).ToList(),
            ["B"] = Enumerable.Range(0, 120).Select(_ => rnd.NextDouble() * 0.02 - 0.008).ToList()
        };

        double portfolioIntrinsicVol = 0.18; // 18% 年化波动率比率
        decimal targetVol = 10.0m; // 低于固有波动率 -> 应降低风险仓位，配置现金缓冲垫

        var result = PortfolioEngine.CalculateKellyAndTargetVolatility(
            components, individualDailyReturns, portfolioIntrinsicVol, targetVol);

        Assert.IsNotNull(result);
        Assert.AreEqual(targetVol, result.TargetVolatility);
        Assert.AreEqual(18.0m, result.PortfolioIntrinsicVolatility);

        // 当 targetVol < portfolioIntrinsicVol，建议风险仓位应 < 100%，现金缓冲垫 > 0%
        Assert.IsTrue(result.SuggestedRiskyWeight < 100.0m, $"Suggested risky weight should be < 100%, got {result.SuggestedRiskyWeight}");
        Assert.IsTrue(result.SuggestedCashWeight > 0.0m, $"Suggested cash weight should be > 0%, got {result.SuggestedCashWeight}");
        Assert.AreEqual(100.0m, Math.Round(result.SuggestedRiskyWeight + result.SuggestedCashWeight, 1));
        Assert.AreEqual(1.0m, result.ImpliedLeverage, "When de-leveraging into cash buffer, implied leverage is 1.0x.");
        Assert.IsFalse(string.IsNullOrEmpty(result.CapitalAllocationAdvice));

        // 测试固有波动率低于目标波动率时的加杠杆空间 (targetVol = 25.0m > 18.0m)
        var leverResult = PortfolioEngine.CalculateKellyAndTargetVolatility(
            components, individualDailyReturns, portfolioIntrinsicVol, targetVol: 25.0m);
        Assert.IsTrue(leverResult.ImpliedLeverage > 1.0m, "Implied leverage should exceed 1.0 when target vol exceeds intrinsic vol.");
        Assert.AreEqual(100.0m, leverResult.SuggestedRiskyWeight);
        Assert.AreEqual(0.0m, leverResult.SuggestedCashWeight);

        // 连续凯利权重验证
        Assert.AreEqual(2, result.FullKellyWeights.Count);
        decimal sumKelly = result.FullKellyWeights.Values.Sum();
        Assert.AreEqual(100.0m, Math.Round(sumKelly, 1), "Full Kelly weights should sum to 100%");
    }

    [TestMethod]
    public void TestBullBearCrossFundComparison()
    {
        var (navsA, bmks) = CreateSyntheticNavAndBenchmark(260, positiveConvexity: true);
        var (navsB, _) = CreateSyntheticNavAndBenchmark(260, positiveConvexity: false);

        var fundA = new FundDetail { Code = "A", Name = "正凸性优选", NavHistory = navsA };
        var fundB = new FundDetail { Code = "B", Name = "负凸性脆弱", NavHistory = navsB };

        var compare = FundCompareEngine.CalculateBullBearCompare(fundA, fundB, bmks);

        Assert.IsNotNull(compare);
        Assert.IsNotNull(compare.CaptureA);
        Assert.IsNotNull(compare.CaptureB);
        Assert.IsTrue(compare.CaptureSpreadDiff > 0m, $"CaptureSpreadDiff should be positive (A > B), got {compare.CaptureSpreadDiff}");
        Assert.IsTrue(compare.ComparisonSummary.Contains("正凸性优选") || compare.ComparisonSummary.Contains("凸性更强"));
        Assert.IsTrue(compare.NormalCorrelation >= -1.0 && compare.NormalCorrelation <= 1.0);
    }

    [TestMethod]
    public void TestExportService_Phase11Sections_Execution()
    {
        var (navs, bmks) = CreateSyntheticNavAndBenchmark(260, positiveConvexity: true);
        var metrics = QuantCalculator.CalculateMetrics(navs, "1年", bmks);

        var fundA = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘",
            NavHistory = navs,
            BullBearCapture = metrics.BullBearCapture,
            DueDiligence = metrics.DueDiligence
        };

        var backtest = new BacktestResult
        {
            StrategyName = "定投测试",
            StrategyDescription = "每周定投",
            TotalPeriods = 52,
            TotalInvested = 52000m,
            FinalAssetValue = 61200m,
            AnnualizedIrr = 16.5m,
            BuyAndHoldReturnRate = 12.3m
        };

        string tempCsvPath = Path.Combine(Path.GetTempPath(), $"biga_phase11_test_{Guid.NewGuid()}.csv");
        string tempHtmlPath = Path.Combine(Path.GetTempPath(), $"biga_phase11_test_{Guid.NewGuid()}.html");

        try
        {
            // 1. 测试单基金 CSV 导出包含 Phase 11 模块
            ExportService.ExportToCsv(tempCsvPath, fundA, metrics, backtest, navs);
            string csvContent = File.ReadAllText(tempCsvPath);
            StringAssert.Contains(csvContent, "牛熊双边非对称捕获与暴跌日条件相关性分析");
            StringAssert.Contains(csvContent, "机构级 FOF 尽调六维雷达评分与晨星等效五星评级");

            // 2. 测试单基金 HTML 导出包含 Phase 11 模块
            ExportService.ExportToHtml(tempHtmlPath, fundA, metrics, backtest, navs);
            string htmlContent = File.ReadAllText(tempHtmlPath);
            StringAssert.Contains(htmlContent, "牛熊双边非对称捕获与暴跌条件相关性");
            StringAssert.Contains(htmlContent, "机构级 FOF 尽调六维雷达综合评分与晨星等效评级");
        }
        finally
        {
            if (File.Exists(tempCsvPath)) File.Delete(tempCsvPath);
            if (File.Exists(tempHtmlPath)) File.Delete(tempHtmlPath);
        }
    }
}
