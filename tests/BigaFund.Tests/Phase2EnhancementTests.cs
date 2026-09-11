using System.IO;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class Phase2EnhancementTests
{
    [TestMethod]
    public void Test_SolveMomentumRiskBudget_WeightsProperties()
    {
        int n = 3;
        double[,] cov = new double[3, 3]
        {
            { 0.04, 0.005, 0.002 },
            { 0.005, 0.02, 0.001 },
            { 0.002, 0.001, 0.01 }
        };
        double[] meanReturns = [0.20, 0.10, 0.05];

        double[] weights = PortfolioEngine.SolveMomentumRiskBudget(cov, meanReturns, n);

        Assert.AreEqual(n, weights.Length);
        double sum = 0.0;
        foreach (var w in weights)
        {
            Assert.IsTrue(w > 0.0, $"每个资产的动量风险预算权重应严格大于 0, 实际: {w}");
            sum += w;
        }

        Assert.AreEqual(1.0, sum, 1e-4, "动量风险预算权重总和必须严格为 1.0");
    }

    [TestMethod]
    public void Test_PortfolioEngine_IncludesMomentumRiskBudgetScheme()
    {
        var fundA = new FundDetail
        {
            Code = "000001",
            Name = "成长先锋",
            Type = "混合型",
            NavHistory = new List<NavRecord>()
        };
        var fundB = new FundDetail
        {
            Code = "000002",
            Name = "稳健增利",
            Type = "债券型",
            NavHistory = new List<NavRecord>()
        };

        var start = new DateTime(2023, 1, 1);
        decimal navA = 1.0m;
        decimal navB = 1.0m;

        for (int i = 0; i < 120; i++)
        {
            var dt = start.AddDays(i);
            navA *= (1.0m + ((i % 3 == 0) ? 0.015m : -0.005m));
            navB *= (1.0m + 0.0003m);
            fundA.NavHistory.Add(new NavRecord { Date = dt, UnitNav = navA, CumulativeNav = navA });
            fundB.NavHistory.Add(new NavRecord { Date = dt, UnitNav = navB, CumulativeNav = navB });
        }

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (fundA, 50m),
            (fundB, 50m)
        };

        var result = PortfolioEngine.CalculatePortfolio(components, riskFreeRate: 2.0m, timeRange: "ALL");

        Assert.IsNotNull(result.MomentumRiskBudgetWeights, "MomentumRiskBudgetWeights 不应为空");
        Assert.IsTrue(result.MomentumRiskBudgetWeights.ContainsKey("000001"));
        Assert.IsTrue(result.MomentumRiskBudgetWeights.ContainsKey("000002"));

        decimal totalWeight = result.MomentumRiskBudgetWeights.Values.Sum();
        Assert.AreEqual(100.0m, totalWeight, 0.5m, "四舍五入后的权重百分比总和应约为 100%");

        var mrbScheme = result.Schemes.FirstOrDefault(s => s.SchemeName.Contains("动量风险预算") || s.SchemeName.Contains("MRB"));
        Assert.IsNotNull(mrbScheme, "Schemes 列表应包含动量风险预算 (MRB) 方案");
        Assert.IsTrue(mrbScheme.SharpeRatio > -10m, "MRB 方案应计算出有效夏普比率");
    }

    [TestMethod]
    public void Test_ExportService_ExportToHtml_GeneratesValidHtml()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"biga_fund_report_test_{Guid.NewGuid():N}.html");

        try
        {
            var fund = new FundDetail
            {
                Code = "110011",
                Name = "易方达中小盘混合",
                Type = "混合型-偏股",
                ManagerName = "张坤",
                ManagerTenure = "8.5年",
                FundSize = "180.50 亿元",
                NavHistory = new List<NavRecord>()
            };

            var start = new DateTime(2023, 1, 1);
            decimal nav = 1.0m;
            for (int i = 0; i < 90; i++)
            {
                var dt = start.AddDays(i);
                nav *= (1.0m + ((i % 2 == 0) ? 0.01m : -0.008m));
                fund.NavHistory.Add(new NavRecord { Date = dt, UnitNav = nav, CumulativeNav = nav });
            }

            var metrics = QuantCalculator.CalculateMetrics(fund.NavHistory, "ALL", null);
            // 显式加入情景压测结果验证 HTML 压测表格渲染
            metrics.StressTestScenarios.Add(new StressTestScenario
            {
                ScenarioName = "2024年年初微盘股流动性危机",
                StartDate = new DateTime(2024, 1, 2),
                EndDate = new DateTime(2024, 2, 8),
                FundReturnRate = -6.5m,
                BenchmarkReturnRate = -7.1m,
                MaxDrawdown = 8.2m,
                DefenseRating = "稳健防御",
                Description = "测试情景描述"
            });

            var backtest = new BacktestResult
            {
                StrategyName = "智能定投策略",
                TotalInvested = 10000m,
                FinalAssetValue = 11250m,
                AnnualizedIrr = 15.2m,
                Timeline = new List<DcaPoint>
                {
                    new() { Date = start, TotalInvested = 1000m, CurrentValue = 1000m, ReturnRate = 0m },
                    new() { Date = start.AddDays(90), TotalInvested = 10000m, CurrentValue = 11250m, ReturnRate = 12.5m }
                }
            };

            ExportService.ExportToHtml(tempFile, fund, metrics, backtest, fund.NavHistory);

            Assert.IsTrue(File.Exists(tempFile), "HTML 研报文件应存在");
            string content = File.ReadAllText(tempFile);
            Assert.IsTrue(content.Length > 1000, "HTML 研报内容长度应大于 1000 字符");
            Assert.IsTrue(content.Contains("<!DOCTYPE html>"), "应包含 DOCTYPE 声明");
            Assert.IsTrue(content.Contains("110011"), "应包含基金代码 110011");
            Assert.IsTrue(content.Contains("易方达中小盘混合"), "应包含基金名称");
            Assert.IsTrue(content.Contains("张坤"), "应包含基金经理姓名");
            Assert.IsTrue(content.Contains("@media print"), "应包含打印样式定义以便导出 PDF");
            Assert.IsTrue(content.Contains("历史经典黑天鹅极端市场情景压力测试"), "应包含极端情景压测表");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    [TestMethod]
    public void Test_ExportService_ExportPortfolioToHtml_GeneratesValidHtml()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"biga_portfolio_report_test_{Guid.NewGuid():N}.html");

        try
        {
            var fundA = new FundDetail
            {
                Code = "000001",
                Name = "华夏成长混合",
                Type = "混合型",
                NavHistory = new List<NavRecord>()
            };
            var fundB = new FundDetail
            {
                Code = "000171",
                Name = "易方达裕丰回报债券",
                Type = "债券型",
                NavHistory = new List<NavRecord>()
            };

            var start = new DateTime(2023, 1, 1);
            decimal navA = 1.0m, navB = 1.0m;
            for (int i = 0; i < 90; i++)
            {
                var dt = start.AddDays(i);
                navA *= (1.0m + ((i % 2 == 0) ? 0.012m : -0.009m));
                navB *= (1.0m + 0.0003m);
                fundA.NavHistory.Add(new NavRecord { Date = dt, UnitNav = navA, CumulativeNav = navA });
                fundB.NavHistory.Add(new NavRecord { Date = dt, UnitNav = navB, CumulativeNav = navB });
            }

            var components = new List<(FundDetail Fund, decimal WeightPercent)>
            {
                (fundA, 60m),
                (fundB, 40m)
            };

            var items = new List<PortfolioItem>
            {
                new() { Code = "000001", Name = "华夏成长混合", Type = "混合型", WeightPercent = 60m },
                new() { Code = "000171", Name = "易方达裕丰回报债券", Type = "债券型", WeightPercent = 40m }
            };

            var result = PortfolioEngine.CalculatePortfolio(components, riskFreeRate: 2.0m, timeRange: "ALL");

            ExportService.ExportPortfolioToHtml(tempFile, items, result);

            Assert.IsTrue(File.Exists(tempFile), "组合 HTML 研报文件应存在");
            string content = File.ReadAllText(tempFile);
            Assert.IsTrue(content.Length > 1000, "组合研报内容长度应大于 1000 字符");
            Assert.IsTrue(content.Contains("华夏成长混合"), "应包含成分基金名称");
            Assert.IsTrue(content.Contains("动量风险预算"), "应包含动量风险预算 (MRB) 方案展示");
            Assert.IsTrue(content.Contains("相关系数矩阵"), "应包含资产相关性矩阵");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    [TestMethod]
    public void Test_QuantCalculator_CalculateRollingMetrics_ComputesExpectedCount()
    {
        var navs = new List<NavRecord>();
        var start = new DateTime(2023, 1, 1);
        decimal nav = 1.0m;

        for (int i = 0; i < 100; i++)
        {
            var dt = start.AddDays(i);
            nav *= (1.0m + ((i % 2 == 0) ? 0.01m : -0.005m));
            navs.Add(new NavRecord { Date = dt, UnitNav = nav, CumulativeNav = nav });
        }

        var rolling = QuantCalculator.CalculateRollingMetrics(navs, window: 60, riskFreeRate: 2.0m);

        Assert.AreEqual(40, rolling.Count, "100 个净值点计算 60 日滚动窗口应产生 40 个滚动数据点 (100 - 1 - 60 + 1 = 40)");
        foreach (var r in rolling)
        {
            Assert.IsTrue(r.RollingAnnualizedVol > 0m, $"滚动年化波动率应大于 0, 实际: {r.RollingAnnualizedVol}");
            Assert.IsTrue(r.RollingSharpe > -100m && r.RollingSharpe < 100m, "滚动夏普比率应在合理范围内");
        }
    }
}
