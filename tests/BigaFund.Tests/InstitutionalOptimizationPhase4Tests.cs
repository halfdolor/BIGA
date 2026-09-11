using System.IO;
using System.Text;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase4Tests
{
    [TestMethod]
    public void Test_FundCompareEngine_HoldingOverlap_EmptyAndIdentical()
    {
        // 1. 双方持仓为空测试
        var emptyResult = FundCompareEngine.CalculateHoldingOverlap(new(), new());
        Assert.AreEqual(0, emptyResult.CommonCount);
        Assert.AreEqual(0m, emptyResult.OverlapWeightPercent);
        Assert.AreEqual("互补分散", emptyResult.OverlapRating);
        Assert.IsTrue(emptyResult.OverlapSummary.Contains("无交集"));

        // 2. 完全相同持仓测试 (100% 同质化)
        var holdingsA = new List<FundStockHolding>
        {
            new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 10m, Industry = "食品饮料" },
            new() { StockCode = "300750", StockName = "宁德时代", WeightPercent = 10m, Industry = "电力设备" },
            new() { StockCode = "000858", StockName = "五粮液", WeightPercent = 10m, Industry = "食品饮料" }
        };
        var holdingsB = new List<FundStockHolding>
        {
            new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 10m, Industry = "食品饮料" },
            new() { StockCode = "300750", StockName = "宁德时代", WeightPercent = 10m, Industry = "电力设备" },
            new() { StockCode = "000858", StockName = "五粮液", WeightPercent = 10m, Industry = "食品饮料" }
        };

        var identicalResult = FundCompareEngine.CalculateHoldingOverlap(holdingsA, holdingsB);
        Assert.AreEqual(3, identicalResult.CommonCount);
        Assert.AreEqual(30m, identicalResult.OverlapWeightPercent);
        Assert.AreEqual("中度重合", identicalResult.OverlapRating); // 30% 在 20~50 区间为中度重合
        Assert.AreEqual(0, identicalResult.UniqueStocksA.Count);
        Assert.AreEqual(0, identicalResult.UniqueStocksB.Count);
    }

    [TestMethod]
    public void Test_FundCompareEngine_HoldingOverlap_PartialIntersection()
    {
        var holdingsA = new List<FundStockHolding>
        {
            new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 9.0m, Industry = "食品饮料" },
            new() { StockCode = "300750", StockName = "宁德时代", WeightPercent = 7.0m, Industry = "电力设备" },
            new() { StockCode = "000858", StockName = "五粮液", WeightPercent = 5.0m, Industry = "食品饮料" },
            new() { StockCode = "002594", StockName = "比亚迪", WeightPercent = 4.0m, Industry = "汽车" }
        };

        var holdingsB = new List<FundStockHolding>
        {
            new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 6.0m, Industry = "食品饮料" }, // 重叠 6.0%, A-B=+3%
            new() { StockCode = "000858", StockName = "五粮液", WeightPercent = 8.0m, Industry = "食品饮料" },   // 重叠 5.0%, A-B=-3%
            new() { StockCode = "601318", StockName = "中国平安", WeightPercent = 6.0m, Industry = "非银金融" },
            new() { StockCode = "600036", StockName = "招商银行", WeightPercent = 5.0m, Industry = "银行" }
        };

        var result = FundCompareEngine.CalculateHoldingOverlap(holdingsA, holdingsB);

        // 重叠总比例 = min(9,6) + min(5,8) = 6 + 5 = 11%
        Assert.AreEqual(2, result.CommonCount);
        Assert.AreEqual(11.0m, result.OverlapWeightPercent);
        Assert.AreEqual("低度重合", result.OverlapRating);

        // 验证共同重仓股排序与差额
        Assert.AreEqual("600519", result.CommonStocks[0].StockCode);
        Assert.AreEqual(6.0m, result.CommonStocks[0].OverlapWeight);
        Assert.AreEqual(3.0m, result.CommonStocks[0].WeightDiff);

        Assert.AreEqual("000858", result.CommonStocks[1].StockCode);
        Assert.AreEqual(5.0m, result.CommonStocks[1].OverlapWeight);
        Assert.AreEqual(-3.0m, result.CommonStocks[1].WeightDiff);

        // 独有持仓
        Assert.AreEqual(2, result.UniqueStocksA.Count);
        Assert.IsTrue(result.UniqueStocksA.Any(s => s.StockCode == "300750"));
        Assert.IsTrue(result.UniqueStocksA.Any(s => s.StockCode == "002594"));

        Assert.AreEqual(2, result.UniqueStocksB.Count);
        Assert.IsTrue(result.UniqueStocksB.Any(s => s.StockCode == "601318"));
        Assert.IsTrue(result.UniqueStocksB.Any(s => s.StockCode == "600036"));
    }

    [TestMethod]
    public void Test_FundCompareEngine_CalculateIndustrySpreads()
    {
        var holdingsA = new List<FundStockHolding>
        {
            new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 8.0m, Industry = "食品饮料" },
            new() { StockCode = "000858", StockName = "五粮液", WeightPercent = 5.0m, Industry = "食品饮料" },
            new() { StockCode = "300750", StockName = "宁德时代", WeightPercent = 6.0m, Industry = "电力设备" }
        };

        var holdingsB = new List<FundStockHolding>
        {
            new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 4.0m, Industry = "食品饮料" },
            new() { StockCode = "601318", StockName = "中国平安", WeightPercent = 7.0m, Industry = "非银金融" }
        };

        var spreads = FundCompareEngine.CalculateIndustrySpreads(holdingsA, holdingsB);

        // A 食品饮料 = 13%, B 食品饮料 = 4% -> Spread = +9%
        var foodSpread = spreads.FirstOrDefault(s => s.IndustryName == "食品饮料");
        Assert.IsNotNull(foodSpread);
        Assert.AreEqual(13.0m, foodSpread.WeightA);
        Assert.AreEqual(4.0m, foodSpread.WeightB);
        Assert.AreEqual(9.0m, foodSpread.Spread);
        Assert.AreEqual("🔵 基金A显著超配", foodSpread.Status);

        // A 非银金融 = 0%, B 非银金融 = 7% -> Spread = -7%
        var finSpread = spreads.FirstOrDefault(s => s.IndustryName == "非银金融");
        Assert.IsNotNull(finSpread);
        Assert.AreEqual(0.0m, finSpread.WeightA);
        Assert.AreEqual(7.0m, finSpread.WeightB);
        Assert.AreEqual(-7.0m, finSpread.Spread);
        Assert.AreEqual("🔴 基金B显著超配", finSpread.Status);
    }

    [TestMethod]
    public void Test_FundCompareEngine_CorrelationAndWinRates()
    {
        // 1. 完全正相关收益率时序测试
        double[] retsA = { 0.01, -0.02, 0.015, -0.005, 0.03 };
        double[] retsB = { 0.02, -0.04, 0.030, -0.010, 0.06 };

        var (corr, rating) = FundCompareEngine.CalculateCorrelation(retsA, retsB);
        Assert.IsTrue(corr > 0.99);
        Assert.AreEqual("高度同质化（强正相关）", rating);

        // 2. 负相关测试
        double[] retsNeg = { -0.01, 0.02, -0.015, 0.005, -0.03 };
        var (corrNeg, ratingNeg) = FundCompareEngine.CalculateCorrelation(retsA, retsNeg);
        Assert.IsTrue(corrNeg < -0.99);
        Assert.AreEqual("负相关（对冲避险效应）", ratingNeg);

        // 3. 胜率计算测试: A 相对 B
        // Day 0: 0.01 < 0.02 (B wins)
        // Day 1: -0.02 > -0.04 (A wins)
        // Day 2: 0.015 < 0.030 (B wins)
        // Day 3: -0.005 > -0.010 (A wins)
        // Day 4: 0.03 < 0.06 (B wins)
        // A wins: 2, B wins: 3, Total: 5. WinRateA = 40%, WinRateB = 60%.
        var (winA, winB, countA, countB, total) = FundCompareEngine.CalculateWinRates(retsA, retsB);
        Assert.AreEqual(5, total);
        Assert.AreEqual(2, countA);
        Assert.AreEqual(3, countB);
        Assert.AreEqual(40.0, winA);
        Assert.AreEqual(60.0, winB);
    }

    [TestMethod]
    public void Test_ExportService_ExportComparisonToHtml_Generation()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"Test_FundCompare_{Guid.NewGuid():N}.html");
        try
        {
            var fundA = new FundDetail
            {
                Code = "000001",
                Name = "华夏成长混合",
                Type = "混合型",
                ManagerName = "王经理",
                ManagerTenure = "5.2年",
                FundSize = "35.2亿",
                Holdings = new List<FundStockHolding>
                {
                    new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 8.5m, Industry = "食品饮料" },
                    new() { StockCode = "300750", StockName = "宁德时代", WeightPercent = 6.2m, Industry = "电力设备" }
                }
            };

            var fundB = new FundDetail
            {
                Code = "005827",
                Name = "易方达蓝筹精选混合",
                Type = "混合型",
                ManagerName = "张经理",
                ManagerTenure = "8.1年",
                FundSize = "420.5亿",
                Holdings = new List<FundStockHolding>
                {
                    new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 9.8m, Industry = "食品饮料" },
                    new() { StockCode = "000858", StockName = "五粮液", WeightPercent = 8.1m, Industry = "食品饮料" }
                }
            };

            var metricsA = new QuantMetrics
            {
                TotalReturn = 28.5m,
                AnnualizedReturn = 12.4m,
                MaxDrawdown = 15.2m,
                SharpeRatio = 1.35m,
                CalmarRatio = 0.82m,
                AnnualizedVolatility = 16.8m,
                VaR95 = 2.1m,
                CVaR95 = 3.2m
            };

            var metricsB = new QuantMetrics
            {
                TotalReturn = 18.2m,
                AnnualizedReturn = 8.5m,
                MaxDrawdown = 22.1m,
                SharpeRatio = 0.88m,
                CalmarRatio = 0.38m,
                AnnualizedVolatility = 19.5m,
                VaR95 = 2.8m,
                CVaR95 = 4.1m
            };

            fundA.ScoreCard = QuantCalculator.CalculateFundScore(fundA, metricsA);
            fundB.ScoreCard = QuantCalculator.CalculateFundScore(fundB, metricsB);

            var overlap = FundCompareEngine.CalculateHoldingOverlap(fundA.Holdings, fundB.Holdings);
            var metricRows = new List<MetricCompareRow>
            {
                new() { MetricName = "区间累计收益率", ValueA = "+28.50%", ValueB = "+18.20%", Winner = "🏆 A 胜出 (+10.30%)", Description = "累计收益率" },
                new() { MetricName = "夏普比率", ValueA = "1.35", ValueB = "0.88", Winner = "🏆 基金 A 优", Description = "单位风险超额回报" }
            };

            ExportService.ExportComparisonToHtml(
                tempFile,
                fundA,
                metricsA,
                fundB,
                metricsB,
                overlap,
                0.78,
                "高度趋同 (风格类似)",
                56.5,
                43.5,
                metricRows,
                "近1年");

            Assert.IsTrue(File.Exists(tempFile));
            string htmlContent = File.ReadAllText(tempFile, Encoding.UTF8);

            Assert.IsTrue(htmlContent.Contains("华夏成长混合"));
            Assert.IsTrue(htmlContent.Contains("易方达蓝筹精选混合"));
            Assert.IsTrue(htmlContent.Contains("000001"));
            Assert.IsTrue(htmlContent.Contains("005827"));
            Assert.IsTrue(htmlContent.Contains("日收益相关度"));
            Assert.IsTrue(htmlContent.Contains("单日收益胜率"));
            Assert.IsTrue(htmlContent.Contains("持仓穿透重合度"));
            Assert.IsTrue(htmlContent.Contains("机构级五维量化打分全景对决"));
            Assert.IsTrue(htmlContent.Contains("前十大重仓股票穿透与重合度诊断"));
            Assert.IsTrue(htmlContent.Contains("申万一级行业配置偏离度"));
            Assert.IsTrue(htmlContent.Contains("贵州茅台"));
            Assert.IsTrue(htmlContent.Contains("机构免责声明"));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public void Test_ExportService_ExportScreenerToCsv_Generation()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"Test_Screener_{Guid.NewGuid():N}.csv");
        try
        {
            var funds = new List<StoredFundItem>
            {
                new()
                {
                    Code = "000001",
                    Name = "华夏成长混合",
                    Type = "混合型",
                    Manager = "王经理",
                    Tenure = "5.2年",
                    FundSize = "35.2亿",
                    QuantScore = 86.5m,
                    RatingGrade = "AAA",
                    Return1Y = 24.5m,
                    SharpeRatio = 1.45m,
                    MaxDrawdown = 12.3m,
                    AnnualizedVol = 15.6m,
                    LatestNav = 1.6520m,
                    UpdatedAt = DateTime.Today
                },
                new()
                {
                    Code = "005827",
                    Name = "易方达蓝筹精选混合",
                    Type = "混合型",
                    Manager = "张经理",
                    Tenure = "8.1年",
                    FundSize = "420.5亿",
                    QuantScore = 78.2m,
                    RatingGrade = "AA",
                    Return1Y = 15.2m,
                    SharpeRatio = 1.05m,
                    MaxDrawdown = 18.5m,
                    AnnualizedVol = 18.2m,
                    LatestNav = 1.9540m,
                    UpdatedAt = DateTime.Today
                }
            };

            ExportService.ExportScreenerToCsv(tempFile, funds);

            Assert.IsTrue(File.Exists(tempFile));
            string csvContent = File.ReadAllText(tempFile, Encoding.UTF8);

            Assert.IsTrue(csvContent.Contains("BIGA 基金多维量化筛选结果清单"));
            Assert.IsTrue(csvContent.Contains("标的数量,2 只"));
            Assert.IsTrue(csvContent.Contains("华夏成长混合"));
            Assert.IsTrue(csvContent.Contains("AAA"));
            Assert.IsTrue(csvContent.Contains("易方达蓝筹精选混合"));
            Assert.IsTrue(csvContent.Contains("AA"));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public void Test_Screener_GradeFilter_Logic()
    {
        var testFunds = new List<StoredFundItem>
        {
            new() { Code = "001", Name = "基金1", RatingGrade = "AAA", QuantScore = 88m },
            new() { Code = "002", Name = "基金2", RatingGrade = "AA", QuantScore = 75m },
            new() { Code = "003", Name = "基金3", RatingGrade = "A", QuantScore = 65m },
            new() { Code = "004", Name = "基金4", RatingGrade = "B", QuantScore = 55m },
            new() { Code = "005", Name = "基金5", RatingGrade = "C", QuantScore = 45m }
        };

        // 1. 全部评级
        var all = testFunds.Where(f => FilterByGrade(f, "ALL")).ToList();
        Assert.AreEqual(5, all.Count);

        // 2. 仅看 AAA
        var aaaOnly = testFunds.Where(f => FilterByGrade(f, "AAA")).ToList();
        Assert.AreEqual(1, aaaOnly.Count);
        Assert.AreEqual("001", aaaOnly[0].Code);

        // 3. AA 及以上 (AAA + AA)
        var aaPlus = testFunds.Where(f => FilterByGrade(f, "AA+")).ToList();
        Assert.AreEqual(2, aaPlus.Count);
        Assert.IsTrue(aaPlus.Any(f => f.Code == "001"));
        Assert.IsTrue(aaPlus.Any(f => f.Code == "002"));

        // 4. A 及以上 (AAA + AA + A)
        var aPlus = testFunds.Where(f => FilterByGrade(f, "A+")).ToList();
        Assert.AreEqual(3, aPlus.Count);
        Assert.IsTrue(aPlus.Any(f => f.Code == "001"));
        Assert.IsTrue(aPlus.Any(f => f.Code == "002"));
        Assert.IsTrue(aPlus.Any(f => f.Code == "003"));
    }

    private static bool FilterByGrade(StoredFundItem f, string grade)
    {
        return grade switch
        {
            "AAA" => f.RatingGrade == "AAA" || f.QuantScore >= 80m,
            "AA+" => f.RatingGrade == "AAA" || f.RatingGrade == "AA" || f.QuantScore >= 70m,
            "A+" => f.RatingGrade == "AAA" || f.RatingGrade == "AA" || f.RatingGrade == "A" || f.QuantScore >= 60m,
            _ => true
        };
    }
}
