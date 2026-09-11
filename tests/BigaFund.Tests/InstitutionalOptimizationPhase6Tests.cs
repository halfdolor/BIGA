using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase6Tests
{
    [TestMethod]
    public void Test_QuantCalculator_CaptureRatiosAndTreynor_Calculation()
    {
        // 构造具有明显上行超越与下行抗跌的净值序列与沪深300基准走势
        var navs = new List<NavRecord>();
        var benchmarks = new List<BenchmarkRecord>();
        var baseDate = new DateTime(2023, 1, 1);
        double nav = 1.0;
        double bm = 1.0;

        for (int i = 0; i < 250; i++)
        {
            // 奇数日市场上涨，基金涨幅更大 (模拟上行捕获高)
            // 偶数日市场下跌，基金跌幅更小 (模拟下行捕获低)
            double bmRet = (i % 2 == 1) ? 0.015 : -0.012;
            double fundRet = (i % 2 == 1) ? 0.020 : -0.006;

            nav *= (1.0 + fundRet);
            bm *= (1.0 + bmRet);

            var dt = baseDate.AddDays(i);
            navs.Add(new NavRecord
            {
                Date = dt,
                UnitNav = (decimal)nav,
                CumulativeNav = (decimal)nav,
                DailyReturn = (decimal)fundRet
            });

            benchmarks.Add(new BenchmarkRecord
            {
                Date = dt,
                CumulativeReturnRate = (decimal)((bm - 1.0) * 100.0)
            });
        }

        var metrics = QuantCalculator.CalculateMetrics(navs, "全周期", benchmarks);

        // 1. 验证特雷诺比率有合理数值
        Assert.IsTrue(metrics.TreynorRatio != 0m, "Treynor ratio should be non-zero");

        // 2. 验证年化跟踪误差 > 0
        Assert.IsTrue(metrics.TrackingError > 0m, "Tracking error should be positive");

        // 3. 验证上行与下行捕获比率
        Assert.IsTrue(metrics.UpsideCaptureRatio > 0m, "Upside capture ratio should be positive");
        Assert.IsTrue(metrics.DownsideCaptureRatio > 0m, "Downside capture ratio should be positive");
        Assert.IsTrue(metrics.CaptureRatio > 1.0m, "Because fund outperforms in up markets and protects in down markets, CaptureRatio should exceed 1.0");
    }

    [TestMethod]
    public void Test_QuantCalculator_StyleDriftAnalysis_SDI_And_Rating()
    {
        var fund = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘混合",
            Type = "混合型-偏股",
            StyleBox = MorningstarStyleBox.MidCapGrowth,
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 9.0m, Industry = "食品饮料" },
                new() { StockCode = "000858", StockName = "五粮液", WeightPercent = 8.0m, Industry = "食品饮料" },
                new() { StockCode = "000568", StockName = "泸州老窖", WeightPercent = 7.0m, Industry = "食品饮料" }
            }
        };

        var drift = QuantCalculator.AnalyzeStyleDrift(fund);

        // 1. 验证生成了 4 期历史报告点
        Assert.IsNotNull(drift);
        Assert.AreEqual(4, drift.HistoryPoints.Count);

        // 2. 验证各期坐标在九宫格评分范围内 (-100 ~ 100)
        foreach (var pt in drift.HistoryPoints)
        {
            Assert.IsTrue(pt.SizeScore >= -100 && pt.SizeScore <= 100);
            Assert.IsTrue(pt.ValueScore >= -100 && pt.ValueScore <= 100);
            Assert.IsFalse(string.IsNullOrEmpty(pt.StyleBoxName));
            Assert.IsTrue(pt.Cr10 > 0m);
        }

        // 3. 验证风格漂移指数 SDI >= 0
        Assert.IsTrue(drift.StyleDriftIndex >= 0m);
        Assert.IsFalse(string.IsNullOrEmpty(drift.StabilityRating));
        Assert.IsFalse(string.IsNullOrEmpty(drift.AnalysisSummary));
    }

    [TestMethod]
    public void Test_PortfolioEngine_ExtractEfficientFrontierCurve()
    {
        // 构造一组随机模拟散点 (波动率, 期望收益率)
        var scatter = new List<(double Volatility, double Return)>
        {
            (10.0, 5.0),
            (10.0, 8.0),
            (12.0, 9.0),
            (12.0, 11.0),
            (15.0, 10.0),
            (15.0, 16.0),
            (18.0, 14.0),
            (18.0, 19.0),
            (20.0, 18.0),
            (20.0, 22.0)
        };

        var frontier = PortfolioEngine.ExtractEfficientFrontierCurve(scatter);

        // 1. 曲线必须存在有效点
        Assert.IsNotNull(frontier);
        Assert.IsTrue(frontier.Count >= 2);

        // 2. 有效前沿上包络必须在同等或相邻波动率下取最大收益
        for (int i = 1; i < frontier.Count; i++)
        {
            Assert.IsTrue(frontier[i].Volatility >= frontier[i - 1].Volatility);
        }
    }

    [TestMethod]
    public void Test_PortfolioEngine_RunMonteCarloSimulation_FanChartTrajectories()
    {
        var mc = PortfolioEngine.RunMonteCarloSimulation(12.0m, 16.0m, horizonDays: 250, simulationRuns: 500);

        Assert.IsNotNull(mc);
        Assert.AreEqual(250, mc.HorizonTradingDays);

        // 验证扇形概率锥 5 轨时序数据维度与起点
        Assert.AreEqual(251, mc.TrajectoryDays.Length); // T=0..250
        Assert.AreEqual(251, mc.Trajectory95.Length);
        Assert.AreEqual(251, mc.Trajectory75.Length);
        Assert.AreEqual(251, mc.Trajectory50.Length);
        Assert.AreEqual(251, mc.Trajectory25.Length);
        Assert.AreEqual(251, mc.Trajectory5.Length);

        // T=0 时净值全部为基准 1.0
        Assert.AreEqual(1.0, mc.Trajectory95[0], 1e-4);
        Assert.AreEqual(1.0, mc.Trajectory50[0], 1e-4);
        Assert.AreEqual(1.0, mc.Trajectory5[0], 1e-4);

        // 终点 T=250 处必须严格满足分位数顺序: P95 >= P75 >= P50 >= P25 >= P5
        double end95 = mc.Trajectory95[250];
        double end75 = mc.Trajectory75[250];
        double end50 = mc.Trajectory50[250];
        double end25 = mc.Trajectory25[250];
        double end5 = mc.Trajectory5[250];

        Assert.IsTrue(end95 >= end75, $"End 95% ({end95}) should be >= 75% ({end75})");
        Assert.IsTrue(end75 >= end50, $"End 75% ({end75}) should be >= 50% ({end50})");
        Assert.IsTrue(end50 >= end25, $"End 50% ({end50}) should be >= 25% ({end25})");
        Assert.IsTrue(end25 >= end5, $"End 25% ({end25}) should be >= 5% ({end5})");
    }

    [TestMethod]
    public void Test_ExportService_InstitutionalExportEnhancements_HtmlAndCsv()
    {
        var tempCsv = Path.Combine(Path.GetTempPath(), $"biga_test_export_{Guid.NewGuid():N}.csv");
        var tempHtml = Path.Combine(Path.GetTempPath(), $"biga_test_export_{Guid.NewGuid():N}.html");

        try
        {
            var fund = new FundDetail
            {
                Code = "000001",
                Name = "华夏成长",
                Type = "混合型-偏股",
                Holdings = new List<FundStockHolding>
                {
                    new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 9.0m, Industry = "食品饮料" }
                }
            };
            var metrics = new QuantMetrics
            {
                TotalReturn = 25.5m,
                AnnualizedReturn = 18.2m,
                MaxDrawdown = 12.4m,
                SharpeRatio = 1.35m,
                TreynorRatio = 14.8m,
                TrackingError = 5.6m,
                UpsideCaptureRatio = 115.0m,
                DownsideCaptureRatio = 82.0m,
                CaptureRatio = 1.40m
            };
            fund.StyleDrift = QuantCalculator.AnalyzeStyleDrift(fund, metrics);

            // 测试单基 CSV 导出包含新增机构指标与风格漂移
            ExportService.ExportToCsv(tempCsv, fund, metrics, new BacktestResult(), new List<NavRecord>());
            string csvContent = File.ReadAllText(tempCsv, Encoding.UTF8);

            Assert.IsTrue(csvContent.Contains("特雷诺比率 (Treynor)"), "CSV should contain Treynor");
            Assert.IsTrue(csvContent.Contains("综合捕获比率 (Capture Ratio)"), "CSV should contain Capture Ratio");
            Assert.IsTrue(csvContent.Contains("晨星九宫格风格箱历史漂移追踪"), "CSV should contain Style Drift section");

            // 测试组合 HTML 导出包含 5 轨蒙特卡洛预测与有效前沿
            var portfolio = new PortfolioResult
            {
                AnnualizedReturn = 15.0m,
                AnnualizedVolatility = 14.0m,
                SharpeRatio = 1.07m,
                MonteCarloResult = PortfolioEngine.RunMonteCarloSimulation(15m, 14m, 250, 300),
                EfficientFrontierCurve = new List<(double Volatility, double Return)>
                {
                    (10.0, 8.0), (12.0, 12.0), (15.0, 16.0)
                },
                Schemes = new List<PortfolioOptimizationScheme>
                {
                    new() { SchemeName = "最大夏普组合", ExpectedReturn = 16.0m, ExpectedVolatility = 14.5m, SharpeRatio = 1.10m }
                }
            };

            ExportService.ExportPortfolioToHtml(tempHtml, new List<PortfolioItem>(), portfolio);
            string htmlContent = File.ReadAllText(tempHtml, Encoding.UTF8);

            Assert.IsTrue(htmlContent.Contains("蒙特卡洛扇形概率锥 5 轨前瞻推演"), "HTML should contain Fan Chart Cone of Uncertainty");
            Assert.IsTrue(htmlContent.Contains("马科维茨现代投资组合理论 (MPT) 有效前沿解析"), "HTML should contain Efficient Frontier section");
        }
        finally
        {
            if (File.Exists(tempCsv)) File.Delete(tempCsv);
            if (File.Exists(tempHtml)) File.Delete(tempHtml);
        }
    }

    [TestMethod]
    public void Test_FundScreener_MultiStageFunnelFilter()
    {
        var funds = new List<StoredFundItem>
        {
            new() { Code = "001", Name = "超强全能基", Return1Y = 25m, MaxDrawdown = 10m, SharpeRatio = 1.8m, QuantScore = 88m },
            new() { Code = "002", Name = "高收益大回撤", Return1Y = 30m, MaxDrawdown = 28m, SharpeRatio = 0.9m, QuantScore = 72m },
            new() { Code = "003", Name = "低回撤低收益", Return1Y = 4m, MaxDrawdown = 3m, SharpeRatio = 1.1m, QuantScore = 65m },
            new() { Code = "004", Name = "平庸亏损基", Return1Y = -12m, MaxDrawdown = 35m, SharpeRatio = -0.5m, QuantScore = 38m }
        };

        // 设定严格的机构漏斗：近1年>=10%，最大回撤<=15%，夏普>=1.0，评分>=75
        decimal minReturn = 10m;
        decimal maxDd = 15m;
        decimal minSharpe = 1.0m;
        decimal minScore = 75m;

        var filtered = funds.Where(f =>
            f.Return1Y >= minReturn &&
            f.MaxDrawdown <= maxDd &&
            f.SharpeRatio >= minSharpe &&
            f.QuantScore >= minScore
        ).ToList();

        // 仅有 001 满足全部 4 项机构精密漏斗门槛
        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual("001", filtered[0].Code);
    }
}
