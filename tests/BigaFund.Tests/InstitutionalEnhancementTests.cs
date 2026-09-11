using System.IO;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalEnhancementTests
{
    private string _tempDbPath = string.Empty;
    private DuckDbService _duckDb = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_inst_{Guid.NewGuid():N}.duckdb");
        _duckDb = new DuckDbService(_tempDbPath);
        _duckDb.Initialize();
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
            string wal = $"{_tempDbPath}.wal";
            if (File.Exists(wal)) File.Delete(wal);
        }
        catch { }
    }

    [TestMethod]
    public void Test_CalculateDrawdownSeries_TracksUnderwaterPointsAccurately()
    {
        var navHistory = new List<NavRecord>
        {
            new() { Date = new DateTime(2024, 1, 1), UnitNav = 1.0m, CumulativeNav = 1.0m },
            new() { Date = new DateTime(2024, 1, 2), UnitNav = 1.2m, CumulativeNav = 1.2m }, // 新峰值
            new() { Date = new DateTime(2024, 1, 3), UnitNav = 0.9m, CumulativeNav = 0.9m }, // (0.9-1.2)/1.2 = -25%
            new() { Date = new DateTime(2024, 1, 4), UnitNav = 0.6m, CumulativeNav = 0.6m }, // (0.6-1.2)/1.2 = -50%
            new() { Date = new DateTime(2024, 1, 5), UnitNav = 1.2m, CumulativeNav = 1.2m }, // 回本 0%
            new() { Date = new DateTime(2024, 1, 6), UnitNav = 1.5m, CumulativeNav = 1.5m }, // 新峰值 0%
        };

        var series = QuantCalculator.CalculateDrawdownSeries(navHistory);

        Assert.AreEqual(6, series.Count);
        Assert.AreEqual(0m, series[0].DrawdownRate);
        Assert.AreEqual(0m, series[1].DrawdownRate);
        Assert.AreEqual(-25.0m, series[2].DrawdownRate);
        Assert.AreEqual(-50.0m, series[3].DrawdownRate);
        Assert.AreEqual(0.0m, series[4].DrawdownRate);
        Assert.AreEqual(0.0m, series[5].DrawdownRate);

        // 验证所有时点回撤率均小于等于0
        Assert.IsTrue(series.All(p => p.DrawdownRate <= 0m));
    }

    [TestMethod]
    public void Test_EvaluateMorningstarStyle_ClassifiesVariousFundsCorrectly()
    {
        // 1. 固收纯债
        var bondFund = new FundDetail { Code = "000171", Name = "易方达裕丰回报债券", Type = "债券型-混合债" };
        Assert.AreEqual(MorningstarStyleBox.FixedIncome, QuantCalculator.EvaluateMorningstarStyle(bondFund));

        // 2. 货币理财
        var moneyFund = new FundDetail { Code = "000198", Name = "天弘余额宝货币", Type = "货币型" };
        Assert.AreEqual(MorningstarStyleBox.MoneyMarket, QuantCalculator.EvaluateMorningstarStyle(moneyFund));

        // 3. 大盘价值
        var valueFund = new FundDetail { Code = "000002", Name = "华夏沪深300红利价值ETF联接", Type = "股票型" };
        Assert.AreEqual(MorningstarStyleBox.LargeCapValue, QuantCalculator.EvaluateMorningstarStyle(valueFund));

        // 4. 大盘成长
        var growthFund = new FundDetail { Code = "005827", Name = "易方达蓝筹科技成长混合", Type = "混合型-偏股" };
        Assert.AreEqual(MorningstarStyleBox.LargeCapGrowth, QuantCalculator.EvaluateMorningstarStyle(growthFund));

        // 5. 中盘成长
        var midGrowthFund = new FundDetail { Code = "160127", Name = "南方中证500芯片半导体成长", Type = "股票型" };
        Assert.AreEqual(MorningstarStyleBox.MidCapGrowth, QuantCalculator.EvaluateMorningstarStyle(midGrowthFund));

        // 6. 小盘价值
        var smallValueFund = new FundDetail { Code = "501000", Name = "国证2000低波价值优选", Type = "股票型" };
        Assert.AreEqual(MorningstarStyleBox.SmallCapValue, QuantCalculator.EvaluateMorningstarStyle(smallValueFund));
    }

    [TestMethod]
    public void Test_CalculateHoldingsConcentration_ComputesTop10SumCorrectly()
    {
        var holdings = new List<FundStockHolding>();
        for (int i = 1; i <= 15; i++)
        {
            holdings.Add(new FundStockHolding
            {
                StockCode = $"60000{i}",
                StockName = $"测试股票{i}",
                WeightPercent = 5.0m
            });
        }

        decimal cr10 = QuantCalculator.CalculateHoldingsConcentration(holdings);
        // 前十大股票各 5.0%，总和应为 50.0%
        Assert.AreEqual(50.0m, cr10);
    }

    [TestMethod]
    public async Task Test_DuckDbService_HoldingsAndAssetAllocations_PersistAndRetrieveAccurately()
    {
        string fundCode = "005827";
        var holdings = new List<FundStockHolding>
        {
            new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 9.85m, ShareChange = "增持", ReportDate = "2024Q4" },
            new() { StockCode = "000858", StockName = "五粮液", WeightPercent = 8.60m, ShareChange = "持平", ReportDate = "2024Q4" },
            new() { StockCode = "00700", StockName = "腾讯控股", WeightPercent = 7.42m, ShareChange = "新进", ReportDate = "2024Q4" }
        };

        var allocations = new List<FundAssetAllocation>
        {
            new() { ReportDate = "2024-12-31", StockRatio = 91.5m, BondRatio = 0.0m, CashRatio = 8.1m, OtherRatio = 0.4m, NetAsset = 420.5m },
            new() { ReportDate = "2024-09-30", StockRatio = 88.2m, BondRatio = 0.0m, CashRatio = 11.2m, OtherRatio = 0.6m, NetAsset = 405.0m }
        };

        // 独立保存与读取持仓
        await _duckDb.SaveFundHoldingsAsync(fundCode, holdings);
        var readHoldings = await _duckDb.GetFundHoldingsAsync(fundCode);
        Assert.AreEqual(3, readHoldings.Count);
        Assert.AreEqual("贵州茅台", readHoldings[0].StockName);
        Assert.AreEqual(9.85m, readHoldings[0].WeightPercent);

        // 独立保存与读取资产配置
        await _duckDb.SaveFundAssetAllocationsAsync(fundCode, allocations);
        var readAlloc = await _duckDb.GetFundAssetAllocationsAsync(fundCode);
        Assert.AreEqual(2, readAlloc.Count);
        Assert.AreEqual(91.5m, readAlloc[0].StockRatio);
        Assert.AreEqual(420.5m, readAlloc[0].NetAsset);

        // 级联通过 FundDetail 读取
        var fundDetail = new FundDetail
        {
            Code = fundCode,
            Name = "易方达蓝筹精选混合",
            Type = "混合型-偏股",
            Holdings = holdings,
            AssetAllocations = allocations
        };
        fundDetail.NavHistory.Add(new NavRecord { Date = DateTime.Today, UnitNav = 2.15m, CumulativeNav = 2.15m });

        await _duckDb.SaveFundDetailAsync(fundDetail);
        var (cached, _) = await _duckDb.GetFundDetailAsync(fundCode);

        Assert.IsNotNull(cached);
        Assert.AreEqual(3, cached.Holdings.Count);
        Assert.AreEqual(2, cached.AssetAllocations.Count);
        Assert.AreEqual(25.87m, cached.HoldingsCr10); // 9.85 + 8.60 + 7.42
    }

    [TestMethod]
    public void Test_ExportService_ExportPortfolioToHtml_GeneratesInstitutionalReport()
    {
        var tempHtml = Path.Combine(Path.GetTempPath(), $"portfolio_report_{Guid.NewGuid():N}.html");
        try
        {
            var f1 = new FundDetail { Code = "000001", Name = "华夏成长混合", Type = "混合型-偏股" };
            var f2 = new FundDetail { Code = "000171", Name = "易方达裕丰回报债券", Type = "债券型" };

            var components = new List<(FundDetail Fund, decimal WeightPercent)>
            {
                (f1, 60m),
                (f2, 40m)
            };

            var portfolio = new PortfolioResult
            {
                PortfolioName = "机构多资产股债稳健平衡组合",
                StartDate = new DateTime(2023, 1, 1),
                EndDate = new DateTime(2024, 1, 1),
                TradingDays = 244,
                TotalReturn = 18.5m,
                AnnualizedReturn = 18.5m,
                AnnualizedVolatility = 12.3m,
                MaxDrawdown = 8.6m,
                SharpeRatio = 1.34m,
                DiversificationBenefit = 3.2m,
                CorrelationMatrix = new CorrelationMatrixResult
                {
                    AssetCodes = new List<string> { "000001", "000171" },
                    AssetNames = new List<string> { "华夏成长混合", "易方达裕丰回报债券" },
                    Matrix = new double[2, 2] { { 1.0, 0.15 }, { 0.15, 1.0 } },
                    AverageCorrelation = 0.15m,
                    DiversificationRating = "优秀"
                },
                Schemes = new List<PortfolioOptimizationScheme>
                {
                    new()
                    {
                        SchemeName = "最大夏普比率配置 (切点组合)",
                        Description = "在同等波动风险下获取最高超额收益",
                        ExpectedReturn = 19.8m,
                        ExpectedVolatility = 13.1m,
                        SharpeRatio = 1.36m,
                        Weights = new Dictionary<string, decimal> { ["华夏成长混合"] = 65m, ["易方达裕丰回报债券"] = 35m }
                    }
                }
            };

            ExportService.ExportPortfolioToHtml(tempHtml, portfolio, components);

            Assert.IsTrue(File.Exists(tempHtml));
            string htmlContent = File.ReadAllText(tempHtml);

            StringAssert.Contains(htmlContent, "BIGA PORTFOLIO");
            StringAssert.Contains(htmlContent, "机构多资产股债稳健平衡组合");
            StringAssert.Contains(htmlContent, "华夏成长混合");
            StringAssert.Contains(htmlContent, "资产相关性矩阵");
            StringAssert.Contains(htmlContent, "最大夏普比率配置 (切点组合)");
        }
        finally
        {
            if (File.Exists(tempHtml)) File.Delete(tempHtml);
        }
    }
}
