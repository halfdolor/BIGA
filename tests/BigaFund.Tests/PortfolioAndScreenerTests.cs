using System.IO;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class PortfolioAndScreenerTests
{
    private List<NavRecord> CreateSimulatedNavHistory(int days = 500, decimal startNav = 1.0m, decimal trend = 0.0004m, decimal vol = 0.015m)
    {
        var list = new List<NavRecord>();
        var date = new DateTime(2023, 1, 1);
        decimal curNav = startNav;

        var rnd = new Random(42);

        for (int i = 0; i < days; i++)
        {
            // 跳过周末
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                date = date.AddDays(1);
                continue;
            }

            decimal shock = (decimal)((rnd.NextDouble() - 0.48) * (double)vol);
            curNav *= (1m + trend + shock);
            if (curNav <= 0.1m) curNav = 0.1m;

            list.Add(new NavRecord
            {
                Date = date,
                UnitNav = Math.Round(curNav, 4),
                CumulativeNav = Math.Round(curNav, 4)
            });

            date = date.AddDays(1);
        }

        return list;
    }

    [TestMethod]
    public void TestValuationPercentileDca_Execution()
    {
        var navs = CreateSimulatedNavHistory(300);
        var result = BacktestEngine.RunValuationPercentileDcaBacktest(
            navs,
            periodicAmount: 1000m,
            frequency: DcaFrequency.WeeklyThursday,
            window: 100,
            subscriptionFeeRate: 0.001m);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.TotalPeriods > 0);
        Assert.IsTrue(result.TotalInvested > 0);
        Assert.IsTrue(result.FinalAssetValue > 0);
        Assert.IsTrue(result.StrategyName.Contains("估值百分位智能定投"));
        Assert.IsNotNull(result.Timeline);
        Assert.AreEqual(navs.Count, result.Timeline.Count);
    }

    [TestMethod]
    public void TestStockBondRebalance_Execution()
    {
        var navs = CreateSimulatedNavHistory(400, trend: 0.0008m, vol: 0.02m);
        var result = BacktestEngine.RunStockBondRebalanceBacktest(
            navs,
            periodicAmount: 1000m,
            rebalanceThreshold: 0.05m,
            bondAnnualRate: 0.035m,
            subscriptionFeeRate: 0.001m);

        Assert.IsNotNull(result);
        Assert.AreEqual(10000m, result.TotalInvested);
        Assert.IsTrue(result.FinalAssetValue > 0);
        Assert.IsTrue(result.RebalanceCount >= 0);
        Assert.IsTrue(result.EquityAssetValue > 0);
        Assert.IsTrue(result.BondAssetValue > 0);
        Assert.IsTrue(result.StrategyName.Contains("50:50 动态再平衡"));
    }

    [TestMethod]
    public void TestPortfolioEngine_MultiAssetCombination()
    {
        var navs1 = CreateSimulatedNavHistory(300, startNav: 1.2m, trend: 0.0006m, vol: 0.02m);
        var navs2 = CreateSimulatedNavHistory(300, startNav: 1.0m, trend: 0.0001m, vol: 0.003m);

        var fund1 = new FundDetail
        {
            Code = "000001",
            Name = "华夏成长",
            Type = "混合型",
            NavHistory = navs1
        };

        var fund2 = new FundDetail
        {
            Code = "000171",
            Name = "易方达裕丰",
            Type = "债券型",
            NavHistory = navs2
        };

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (fund1, 60m),
            (fund2, 40m)
        };

        var result = PortfolioEngine.CalculatePortfolio(components, riskFreeRate: 2.0m);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.TradingDays > 100);
        Assert.IsTrue(result.PortfolioNavHistory.Count > 100);
        Assert.IsTrue(result.AnnualizedVolatility > 0);
        Assert.IsTrue(result.MaxDrawdown >= 0);
        Assert.IsNotNull(result.SharpeRatio);
        // 验证马科维茨现代投资组合理论(MPT)：资产分散化能够降低总体波动率
        Assert.IsTrue(result.DiversificationBenefit >= 0m, "组合分散化增益应大于等于 0");
    }

    [TestMethod]
    public async Task TestDuckDb_GetAllStoredFunds()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"biga_test_screener_{Guid.NewGuid():N}.duckdb");
        var duckDb = new DuckDbService(tempDbPath);

        try
        {
            var detail = new FundDetail
            {
                Code = "999001",
                Name = "测试选基混合基金",
                Type = "混合型-偏股",
                ManagerName = "张量化",
                ManagerTenure = "8年",
                FundSize = "50.00亿元",
                NavHistory = new List<NavRecord>
                {
                    new() { Date = DateTime.Today.AddDays(-1), UnitNav = 1.5000m },
                    new() { Date = DateTime.Today, UnitNav = 1.5230m }
                }
            };

            await duckDb.SaveFundDetailAsync(detail);

            var list = await duckDb.GetAllStoredFundsAsync();
            Assert.IsNotNull(list);
            Assert.IsTrue(list.Count >= 1);

            var found = list.FirstOrDefault(f => f.Code == "999001");
            Assert.IsNotNull(found);
            Assert.AreEqual("测试选基混合基金", found.Name);
            Assert.AreEqual("混合型-偏股", found.Type);
            Assert.AreEqual(1.5230m, found.LatestNav);
            // 验证量化因子字段已计算
            Assert.IsNotNull(found.Return1Y);
            Assert.IsNotNull(found.MaxDrawdown);
            Assert.IsNotNull(found.SharpeRatio);
            Assert.IsNotNull(found.AnnualizedVol);
        }
        finally
        {
            try
            {
                if (File.Exists(tempDbPath)) File.Delete(tempDbPath);
            }
            catch { }
        }
    }

    [TestMethod]
    public void TestPortfolioEngine_TimeRangeFiltering()
    {
        // 构造近 3 年 (约 750 个交易日) 的历史净值
        var navs1 = CreateSimulatedNavHistory(750, startNav: 1.0m, trend: 0.0004m, vol: 0.015m);
        var navs2 = CreateSimulatedNavHistory(750, startNav: 1.0m, trend: 0.0002m, vol: 0.005m);

        var fund1 = new FundDetail { Code = "000001", Name = "股票配置", NavHistory = navs1 };
        var fund2 = new FundDetail { Code = "000171", Name = "债券配置", NavHistory = navs2 };
        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (fund1, 50m),
            (fund2, 50m)
        };

        var resAll = PortfolioEngine.CalculatePortfolio(components, 2.0m, "ALL");
        var res1Y = PortfolioEngine.CalculatePortfolio(components, 2.0m, "1Y");
        var res3M = PortfolioEngine.CalculatePortfolio(components, 2.0m, "3M");

        Assert.IsNotNull(resAll);
        Assert.IsNotNull(res1Y);
        Assert.IsNotNull(res3M);

        Assert.IsTrue(resAll.TradingDays > res1Y.TradingDays, "ALL 周期天数应大于 1Y");
        Assert.IsTrue(res1Y.TradingDays > res3M.TradingDays, "1Y 周期天数应大于 3M");
        Assert.IsTrue(res3M.TradingDays >= 50 && res3M.TradingDays <= 75, "3M 交易日应在 60 天左右");
    }

    [TestMethod]
    public void TestExportService_PortfolioCsvExport()
    {
        var navs1 = CreateSimulatedNavHistory(200, startNav: 1.0m, trend: 0.0005m, vol: 0.01m);
        var navs2 = CreateSimulatedNavHistory(200, startNav: 1.0m, trend: 0.0001m, vol: 0.003m);

        var fund1 = new FundDetail { Code = "000001", Name = "测试基金A", Type = "混合型", NavHistory = navs1 };
        var fund2 = new FundDetail { Code = "000171", Name = "测试基金B", Type = "债券型", NavHistory = navs2 };

        var components = new List<PortfolioItem>
        {
            new() { Code = "000001", Name = "测试基金A", Type = "混合型", WeightPercent = 60m },
            new() { Code = "000171", Name = "测试基金B", Type = "债券型", WeightPercent = 40m }
        };

        var portCalcList = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (fund1, 60m),
            (fund2, 40m)
        };

        var portResult = PortfolioEngine.CalculatePortfolio(portCalcList, 2.0m, "ALL");

        string exportPath = Path.Combine(Path.GetTempPath(), $"biga_port_export_{Guid.NewGuid():N}.csv");
        try
        {
            ExportService.ExportPortfolioToCsv(exportPath, components, portResult);

            Assert.IsTrue(File.Exists(exportPath));
            string content = File.ReadAllText(exportPath);

            Assert.IsTrue(content.Contains("BIGA 基金组合资产配置与量化回测研报"));
            Assert.IsTrue(content.Contains("000001"));
            Assert.IsTrue(content.Contains("000171"));
            Assert.IsTrue(content.Contains("年化波动率"));
            Assert.IsTrue(content.Contains("资产分散化降波增益"));
            Assert.IsTrue(content.Contains("合成单位净值"));

            // 验证 UTF-8 BOM 标识 (EF BB BF)
            byte[] bytes = File.ReadAllBytes(exportPath);
            Assert.IsTrue(bytes.Length >= 3);
            Assert.AreEqual((byte)0xEF, bytes[0]);
            Assert.AreEqual((byte)0xBB, bytes[1]);
            Assert.AreEqual((byte)0xBF, bytes[2]);
        }
        finally
        {
            try
            {
                if (File.Exists(exportPath)) File.Delete(exportPath);
            }
            catch { }
        }
    }
}
