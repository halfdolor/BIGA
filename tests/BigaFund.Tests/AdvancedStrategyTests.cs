using System.IO;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class AdvancedStrategyTests
{
    [TestMethod]
    public void TargetProfitDca_ExecutesTakeProfitAndAccumulatesCash()
    {
        // 构造触发止盈的序列：
        // 2025-01-02 (周四): 净值 1.00，定投 1000 元，净买入 999 份
        // 2025-01-03 (周五): 净值 1.20 (涨幅 +20% >= 目标 +15%)，触发止盈，赎回 999 * 1.20 * 0.999 元，转入现金池
        // 2025-01-09 (周四): 净值 1.10，再次定投 1000 元，开启第二轮
        var navs = new List<NavRecord>
        {
            new() { Date = new DateTime(2025, 1, 2), UnitNav = 1.00m, CumulativeNav = 1.00m },
            new() { Date = new DateTime(2025, 1, 3), UnitNav = 1.20m, CumulativeNav = 1.20m },
            new() { Date = new DateTime(2025, 1, 9), UnitNav = 1.10m, CumulativeNav = 1.10m }
        };

        var result = BacktestEngine.RunTargetProfitDcaBacktest(
            navs,
            periodicAmount: 1000m,
            frequency: DcaFrequency.WeeklyThursday,
            targetProfitRate: 0.15m,
            subscriptionFeeRate: 0.001m);

        Assert.AreEqual(1, result.TakeProfitRounds);
        Assert.IsTrue(result.RealizedProfit > 190m, "落袋毛利润应超过 190 元");
        Assert.IsTrue(result.FinalAssetValue > result.TotalInvested, "包含现金储备的期末资产应产生盈利");
        Assert.AreEqual(2000m, result.TotalInvested);
    }

    [TestMethod]
    public void GridTrading_ExecutesBuyAndSellGrids()
    {
        // 构造区间震荡序列：
        // Day 0: 净值 1.00，建底仓 (总本金 10,000，底仓 5,000，现金 5,000)
        // Day 1: 净值 0.96 (偏离 -4% <= -3%)，触发加仓买入 1 格 (1,000 元)
        // Day 2: 净值 1.01 (反弹偏离 >= +3%)，触发分批卖出 1 格 (1,000 元)
        var navs = new List<NavRecord>
        {
            new() { Date = new DateTime(2025, 1, 1), UnitNav = 1.00m, CumulativeNav = 1.00m },
            new() { Date = new DateTime(2025, 1, 2), UnitNav = 0.96m, CumulativeNav = 0.96m },
            new() { Date = new DateTime(2025, 1, 3), UnitNav = 1.01m, CumulativeNav = 1.01m }
        };

        var result = BacktestEngine.RunGridTradingBacktest(
            navs,
            periodicAmount: 1000m,
            gridSpacing: 0.03m,
            subscriptionFeeRate: 0.001m);

        Assert.AreEqual(2, result.GridTradesCount, "应当成功执行 1 笔跌穿买入与 1 笔反弹卖出");
        Assert.IsTrue(result.GridArbitrageProfit > 0m, "网格应当累计套利毛利");
        Assert.AreEqual(10000m, result.TotalInvested);
    }

    [TestMethod]
    public void QuantCalculator_RiskFreeRate_InfluencesSharpeRatio()
    {
        var navs = new List<NavRecord>();
        DateTime start = new DateTime(2024, 1, 1);
        decimal nav = 1.0m;
        for (int i = 0; i < 260; i++)
        {
            nav *= 1.001m; // 持续微涨
            navs.Add(new NavRecord { Date = start.AddDays(i), UnitNav = nav, CumulativeNav = nav });
        }

        var metricsLowRf = QuantCalculator.CalculateMetrics(navs, "测试低Rf", riskFreeRate: 1.0m);
        var metricsHighRf = QuantCalculator.CalculateMetrics(navs, "测试高Rf", riskFreeRate: 5.0m);

        Assert.IsTrue(metricsLowRf.SharpeRatio > metricsHighRf.SharpeRatio, "无风险利率越低，算出的夏普比率应越高");
    }

    [TestMethod]
    public async Task DuckDbService_Settings_SavesAndRetrieves()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"biga_settings_test_{Guid.NewGuid():N}.duckdb");
        var duckDb = new DuckDbService(testDbPath);

        try
        {
            await duckDb.SaveSettingAsync("RiskFreeRate", "2.8");
            await duckDb.SaveSettingAsync("GridSpacing", "4.5");

            string rf = await duckDb.GetSettingAsync("RiskFreeRate", "2.0");
            string grid = await duckDb.GetSettingAsync("GridSpacing", "3.0");
            string notFound = await duckDb.GetSettingAsync("NonExistentKey", "DefaultVal");

            Assert.AreEqual("2.8", rf);
            Assert.AreEqual("4.5", grid);
            Assert.AreEqual("DefaultVal", notFound);
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(testDbPath))
            {
                try { File.Delete(testDbPath); } catch { }
            }
        }
    }
}
