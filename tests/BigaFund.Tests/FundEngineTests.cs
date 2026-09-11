using System.IO;
using BigaFund.Models;
using BigaFund.Services;

namespace BigaFund.Tests;

[TestClass]
public class FundEngineTests
{
    [TestMethod]
    public void QuantCalculator_MaxDrawdown_IsAccurate()
    {
        // 构造测试净值序列：
        // 1.0 -> 1.20 (高点) -> 0.90 (低点，回撤 = (1.2 - 0.9)/1.2 = 25%) -> 1.50
        var dates = new[]
        {
            new DateTime(2025, 1, 1),
            new DateTime(2025, 1, 2),
            new DateTime(2025, 1, 3),
            new DateTime(2025, 1, 4)
        };

        var navs = new List<NavRecord>
        {
            new() { Date = dates[0], UnitNav = 1.0m, CumulativeNav = 1.0m },
            new() { Date = dates[1], UnitNav = 1.20m, CumulativeNav = 1.20m },
            new() { Date = dates[2], UnitNav = 0.90m, CumulativeNav = 0.90m },
            new() { Date = dates[3], UnitNav = 1.50m, CumulativeNav = 1.50m }
        };

        var metrics = QuantCalculator.CalculateMetrics(navs, "测试区间");

        // 总收益率 = (1.5 - 1.0)/1.0 = 50%
        Assert.AreEqual(50.0m, Math.Round(metrics.TotalReturn, 2));

        // 最大回撤 = 25.0%
        Assert.AreEqual(25.0m, Math.Round(metrics.MaxDrawdown, 2));
        Assert.AreEqual(dates[1], metrics.MaxDrawdownPeakDate);
        Assert.AreEqual(dates[2], metrics.MaxDrawdownTroughDate);
    }

    [TestMethod]
    public void BacktestEngine_DcaCalculation_MatchesExpectedSharesAndValue()
    {
        // 构造包含 2 个周四的交易序列
        // 周四 1 (2025-01-02): 净值 1.00，定投 1000 元，净买入 = 1000 * (1 - 0.001) = 999 元 -> 999 份
        // 周五 (2025-01-03): 净值 1.10，不定投
        // 周四 2 (2025-01-09): 净值 2.00，定投 1000 元，净买入 = 999 元 -> 499.5 份
        // 期末净值 2.00
        // 总份额 = 999 + 499.5 = 1498.5 份
        // 期末市值 = 1498.5 * 2.00 = 2997 元
        // 总投入本金 = 2000 元
        // 总净利润 = 2997 - 2000 = 997 元

        var navs = new List<NavRecord>
        {
            new() { Date = new DateTime(2025, 1, 2), UnitNav = 1.00m, CumulativeNav = 1.00m }, // 星期四
            new() { Date = new DateTime(2025, 1, 3), UnitNav = 1.10m, CumulativeNav = 1.10m }, // 星期五
            new() { Date = new DateTime(2025, 1, 9), UnitNav = 2.00m, CumulativeNav = 2.00m }  // 星期四
        };

        var result = BacktestEngine.RunDcaBacktest(navs, periodicAmount: 1000m, DcaFrequency.WeeklyThursday, subscriptionFeeRate: 0.001m);

        Assert.AreEqual(2, result.TotalPeriods);
        Assert.AreEqual(2000m, result.TotalInvested);
        Assert.AreEqual(1498.5m, result.FinalShares);
        Assert.AreEqual(2997.0m, result.FinalAssetValue);
        Assert.AreEqual(997.0m, result.TotalProfit);
        Assert.IsTrue(result.TotalReturnRate > 49.0m && result.TotalReturnRate < 51.0m);
        Assert.IsTrue(result.AnnualizedIrr > 0);
    }

    [TestMethod]
    public async Task FundDataService_LiveSearch_ReturnsRealFunds()
    {
        var service = new FundDataService();
        var results = await service.SearchFundsAsync("000001");

        Assert.IsNotNull(results);
        Assert.IsTrue(results.Count > 0, "应能搜索到 000001 华夏成长混合");
        var first = results.FirstOrDefault(f => f.Code == "000001");
        Assert.IsNotNull(first);
        Assert.IsTrue(first.Name.Contains("华夏"));
    }

    [TestMethod]
    public async Task FundDataService_LiveDetail_ParsesFullHistoryAndBenchmark()
    {
        var service = new FundDataService();
        var detail = await service.GetFundDetailAsync("000001");

        Assert.IsNotNull(detail, "获取基金详情不应为空");
        Assert.AreEqual("000001", detail.Code);
        Assert.IsTrue(detail.Name.Contains("华夏"));
        Assert.IsTrue(detail.NavHistory.Count > 3000, $"华夏成长历史净值应超过3000条，实际为 {detail.NavHistory.Count}");
        Assert.IsTrue(detail.BenchmarkCsi300.Count > 0, "应成功提取沪深300基准走势");

        // 验证基于真实数据的量化指标运算
        var metrics = QuantCalculator.CalculateMetrics(detail.NavHistory, "成立以来", detail.BenchmarkCsi300);
        Assert.IsTrue(metrics.TotalReturn > 0);
        Assert.IsTrue(metrics.AnnualizedVolatility > 0);
        Assert.IsTrue(metrics.MaxDrawdown > 0);
        Assert.IsTrue(metrics.Beta > 0, "Beta系数应有效计算");
    }

    [TestMethod]
    public void QuantCalculator_RecoveryTradingDays_CalculatesCorrectly()
    {
        // 构造净值：
        // 索引 0: 1.00
        // 索引 1: 2.00 (高点)
        // 索引 2: 1.00 (低点，回撤 50%)
        // 索引 3: 1.50 (反弹中未修复)
        // 索引 4: 2.10 (突破前期高点 2.00，完成修复)
        // 从低点 (索引2) 到修复点 (索引4)，修复用时 4 - 2 = 2 个交易日
        var baseDate = new DateTime(2025, 1, 1);
        var navs = new List<NavRecord>
        {
            new() { Date = baseDate, UnitNav = 1.00m, CumulativeNav = 1.00m },
            new() { Date = baseDate.AddDays(1), UnitNav = 2.00m, CumulativeNav = 2.00m },
            new() { Date = baseDate.AddDays(2), UnitNav = 1.00m, CumulativeNav = 1.00m },
            new() { Date = baseDate.AddDays(3), UnitNav = 1.50m, CumulativeNav = 1.50m },
            new() { Date = baseDate.AddDays(4), UnitNav = 2.10m, CumulativeNav = 2.10m }
        };

        var metrics = QuantCalculator.CalculateMetrics(navs, "测试区间");
        Assert.AreEqual(50.0m, Math.Round(metrics.MaxDrawdown, 2));
        Assert.AreEqual(2, metrics.RecoveryTradingDays);
    }

    [TestMethod]
    public void QuantCalculator_CapmMetrics_CalculatesAlphaBetaAndIR()
    {
        var baseDate = new DateTime(2024, 1, 1);
        var navs = new List<NavRecord>();
        var benchs = new List<BenchmarkRecord>();

        // 构造 60 个交易日的基金与基准同步上涨/波动序列
        decimal fundNav = 1.00m;
        decimal benchReturn = 0.0m;

        for (int i = 0; i < 60; i++)
        {
            var date = baseDate.AddDays(i);
            decimal dailyFluct = (i % 2 == 0 ? 0.015m : -0.005m); // 稳定超额收益
            fundNav *= (1.0m + dailyFluct);
            benchReturn += (i % 2 == 0 ? 0.008m : -0.003m) * 100m;

            navs.Add(new NavRecord
            {
                Date = date,
                UnitNav = fundNav,
                CumulativeNav = fundNav
            });

            benchs.Add(new BenchmarkRecord
            {
                Date = date,
                CumulativeReturnRate = benchReturn
            });
        }

        var metrics = QuantCalculator.CalculateMetrics(navs, "CAPM测试", benchs);

        Assert.IsTrue(metrics.Beta > 0.5m, $"Beta 应大于 0.5，实际为 {metrics.Beta}");
        Assert.IsTrue(metrics.Alpha > 0m, $"基金超额收益应为正 Alpha，实际为 {metrics.Alpha}");
        Assert.IsTrue(metrics.InformationRatio != 0m, "信息比率不应为0");
    }

    [TestMethod]
    public void BacktestEngine_MaTimingDca_AdaptsMultipliers()
    {
        // 构造包含 90 天的序列（足以计算 60 日均线）
        // 前 60 天价格平稳在 1.00
        // 后 30 天跌至 0.70（低估 30%，偏离度 < -15%，触发 2.0x 倍率加仓）
        var baseDate = new DateTime(2024, 1, 1);
        var navs = new List<NavRecord>();

        for (int i = 0; i < 90; i++)
        {
            var date = baseDate.AddDays(i);
            decimal nav = (i < 60) ? 1.00m : 0.70m;
            navs.Add(new NavRecord
            {
                Date = date,
                UnitNav = nav,
                CumulativeNav = nav
            });
        }

        var regularResult = BacktestEngine.RunBacktest(navs, 1000m, DcaFrequency.WeeklyThursday, StrategyType.RegularDca);
        var maResult = BacktestEngine.RunBacktest(navs, 1000m, DcaFrequency.WeeklyThursday, StrategyType.MaTimingDca);

        Assert.IsTrue(regularResult.StrategyName.Contains("普通"));
        Assert.IsTrue(maResult.StrategyName.Contains("MA60"));
        Assert.IsTrue(maResult.TotalInvested > regularResult.TotalInvested, "在下跌低估期，MA择时定投应自适应扩大买入乘数(2.0x)");
        Assert.IsTrue(maResult.AverageCostPrice < regularResult.AverageCostPrice, "智能择时定投应实现更优（更低）的持仓均价");
        Assert.IsTrue(maResult.FinalShares > regularResult.FinalShares, "智能择时定投在低谷期买入了更多份额");
    }

    [TestMethod]
    public void ExportService_GeneratesValidCsvWithBom()
    {
        string tempCsv = Path.Combine(Path.GetTempPath(), $"biga_export_test_{Guid.NewGuid():N}.csv");
        try
        {
            var fund = new FundDetail
            {
                Code = "000001",
                Name = "华夏成长混合",
                Type = "混合型-偏股",
                ManagerName = "王经理",
                ManagerTenure = "3年",
                FundSize = "50亿元"
            };

            var navs = new List<NavRecord>
            {
                new() { Date = new DateTime(2025, 1, 1), UnitNav = 1.0m, CumulativeNav = 1.0m },
                new() { Date = new DateTime(2025, 1, 2), UnitNav = 1.2m, CumulativeNav = 1.2m }
            };

            var metrics = QuantCalculator.CalculateMetrics(navs, "测试");
            var backtest = BacktestEngine.RunDcaBacktest(navs, 1000m, DcaFrequency.WeeklyThursday);

            ExportService.ExportToCsv(tempCsv, fund, metrics, backtest, navs);

            Assert.IsTrue(File.Exists(tempCsv), "导出的 CSV 文件应存在");

            // 验证 UTF-8 BOM 标识: 0xEF, 0xBB, 0xBF (确保在 Windows Excel 中完全不乱码)
            byte[] fileBytes = File.ReadAllBytes(tempCsv);
            Assert.IsTrue(fileBytes.Length >= 3);
            Assert.AreEqual(0xEF, fileBytes[0]);
            Assert.AreEqual(0xBB, fileBytes[1]);
            Assert.AreEqual(0xBF, fileBytes[2]);

            // 验证包含核心内容
            string content = File.ReadAllText(tempCsv, System.Text.Encoding.UTF8);
            Assert.IsTrue(content.Contains("000001"));
            Assert.IsTrue(content.Contains("华夏成长混合"));
            Assert.IsTrue(content.Contains("年化复合收益率 (CAGR)"));
            Assert.IsTrue(content.Contains("持仓均价"));
            Assert.IsTrue(content.Contains("2025-01-01"));
        }
        finally
        {
            if (File.Exists(tempCsv))
            {
                try { File.Delete(tempCsv); } catch { }
            }
        }
    }
}
