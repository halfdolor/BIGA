using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase8Tests
{
    private (List<NavRecord> navs, List<BenchmarkRecord> bmks) CreateSynthesizedFundAndBenchmark(int days = 300, double dailyDrift = 0.0006, double dailyVol = 0.012)
    {
        var navs = new List<NavRecord>();
        var bmks = new List<BenchmarkRecord>();
        var baseDate = new DateTime(2023, 1, 1);

        decimal currentNav = 1.0m;
        double cumBmkRet = 0.0;

        for (int i = 0; i < days; i++)
        {
            var date = baseDate.AddDays(i);
            double fundRet = dailyDrift + dailyVol * Math.Sin(i * 0.15) * 0.6 + (i % 7 == 0 ? 0.004 : -0.002);
            double bmkRet = 0.0002 + 0.010 * Math.Sin(i * 0.12) * 0.5 + (i % 5 == 0 ? 0.003 : -0.002);
            cumBmkRet += bmkRet * 100.0;

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
    public void Test_QuantCalculator_BarraAttribution_Calculation()
    {
        var (navs, bmks) = CreateSynthesizedFundAndBenchmark(250);

        var fund = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘混合",
            Type = "混合型-偏股",
            ManagerName = "张坤"
        };

        var barra = QuantCalculator.CalculateBarraAttribution(fund, navs, bmks, 2.0m);

        Assert.IsNotNull(barra, "Barra attribution result should not be null");
        Assert.AreEqual(6, barra.FactorItems.Count, "Should calculate 6 Barra style factors");

        var factorCodes = barra.FactorItems.Select(f => f.FactorId).ToList();
        CollectionAssert.Contains(factorCodes, "Size");
        CollectionAssert.Contains(factorCodes, "Value");
        CollectionAssert.Contains(factorCodes, "Growth");
        CollectionAssert.Contains(factorCodes, "Momentum");
        CollectionAssert.Contains(factorCodes, "LowVol");
        CollectionAssert.Contains(factorCodes, "Dividend");

        Assert.IsTrue(barra.RSquared >= 0m && barra.RSquared <= 1.0m, $"RSquared should be between 0 and 1, got {barra.RSquared}");
        Assert.IsTrue(barra.RSquaredPercent >= 0m && barra.RSquaredPercent <= 100m);
        Assert.IsFalse(string.IsNullOrWhiteSpace(barra.DominantStyle), "Dominant style should be classified");
        Assert.IsFalse(string.IsNullOrWhiteSpace(barra.AttributionSummary), "Attribution summary should be generated");
        Assert.IsTrue(barra.SpecificAlphaAnnualized != 0m, "Specific alpha should be computed");
        foreach (var factor in barra.FactorItems)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(factor.FactorName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(factor.ExposureEvaluation));
            Assert.IsTrue(factor.TStat >= 0, "TStat should be formatted non-negative");
        }
    }

    [TestMethod]
    public void Test_QuantCalculator_ManagerProfileAndWinRate_Calculation()
    {
        var (navs, bmks) = CreateSynthesizedFundAndBenchmark(365, dailyDrift: 0.0008);

        var profile = QuantCalculator.CalculateManagerProfileAndWinRate(navs, bmks);

        Assert.IsNotNull(profile, "Manager profile should not be null");
        Assert.IsTrue(profile.TenureDays > 0, "Tenure days should be > 0");
        Assert.IsTrue(profile.TenureYears > 0m, "Tenure years should be > 0");
        Assert.IsTrue(profile.TotalMonths > 0, "Total months should be > 0");
        Assert.IsTrue(profile.MonthlyWinRate >= 0m && profile.MonthlyWinRate <= 100m, "Monthly win rate should be 0-100%");
        Assert.IsTrue(profile.QuarterlyWinRate >= 0m && profile.QuarterlyWinRate <= 100m, "Quarterly win rate should be 0-100%");
        Assert.IsTrue(profile.TenureTotalReturn != 0m, "Tenure total return should be non-zero");
        Assert.IsTrue(profile.TenureBenchmarkReturn != 0m, "Benchmark return should be non-zero");
        Assert.AreEqual(profile.TenureTotalReturn - profile.TenureBenchmarkReturn, profile.TenureExcessReturn, "Excess return must equal return difference");
        Assert.IsTrue(profile.MaxMonthlyOutperformance >= 0m, "Max outperformance should be non-negative");
        Assert.IsTrue(profile.MaxMonthlyUnderperformance <= 0m, "Max underperformance should be non-positive");
        Assert.IsFalse(string.IsNullOrWhiteSpace(profile.WinRateRating), "Win rate rating should not be empty");
        Assert.IsFalse(string.IsNullOrWhiteSpace(profile.CareerSummary), "Career summary should not be empty");
    }

    [TestMethod]
    public void Test_PortfolioEngine_InvertMatrix_GaussJordan()
    {
        double[,] a2 = new double[,]
        {
            { 4.0, 7.0 },
            { 2.0, 6.0 }
        };

        var inv2 = QuantCalculator.InvertMatrix(a2, 2);
        Assert.IsNotNull(inv2);
        Assert.AreEqual(0.6, inv2[0, 0], 1e-4);
        Assert.AreEqual(-0.7, inv2[0, 1], 1e-4);
        Assert.AreEqual(-0.2, inv2[1, 0], 1e-4);
        Assert.AreEqual(0.4, inv2[1, 1], 1e-4);

        double[,] a3 = new double[,]
        {
            { 2.0, 1.0, 0.0 },
            { 1.0, 3.0, 1.0 },
            { 0.0, 1.0, 2.0 }
        };
        var inv3 = QuantCalculator.InvertMatrix(a3, 3);
        Assert.IsNotNull(inv3);

        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                double sum = 0.0;
                for (int k = 0; k < 3; k++)
                {
                    sum += a3[r, k] * inv3[k, c];
                }
                double expected = (r == c) ? 1.0 : 0.0;
                Assert.AreEqual(expected, sum, 1e-4, $"Matrix product at ({r}, {c}) should equal {expected}");
            }
        }
    }

    [TestMethod]
    public void Test_PortfolioEngine_BlackLitterman_Optimization()
    {
        int assetCount = 3;
        var assetCodes = new List<string> { "110011", "161725", "000001" };
        var histAnnReturns = new double[] { 0.12, 0.18, 0.06 };
        var covMatrix = new double[,]
        {
            { 0.040, 0.015, 0.005 },
            { 0.015, 0.060, 0.008 },
            { 0.005, 0.008, 0.020 }
        };

        var views = new List<BlackLittermanView>
        {
            new BlackLittermanView
            {
                AssetCodeA = "161725",
                AssetCodeB = "110011",
                ExpectedReturn = 5.0m,
                Confidence = 0.75,
                Description = "看好白酒跑赢中小盘 5%"
            }
        };

        var blWeights = PortfolioEngine.SolveBlackLitterman(
            covMatrix,
            histAnnReturns,
            assetCount,
            views,
            fundCodes: assetCodes,
            tau: 0.05,
            deltaRiskAversion: 2.8);

        Assert.IsNotNull(blWeights);
        Assert.AreEqual(assetCount, blWeights.Length);

        double sumWeight = blWeights.Sum();
        Assert.AreEqual(1.0, sumWeight, 1e-2, "Black-Litterman weights should sum to 1.0");

        for (int i = 0; i < assetCount; i++)
        {
            Assert.IsTrue(blWeights[i] >= 0.0, $"Weight for index {i} must be non-negative, got {blWeights[i]}");
            Assert.IsTrue(blWeights[i] <= 1.0, $"Weight for index {i} must be <= 1.0, got {blWeights[i]}");
        }

        // 验证积极看好的资产 161725 (index 1) 获得正权重
        Assert.IsTrue(blWeights[1] > 0.0, "Bullish asset 161725 should receive positive weight");
    }

    [TestMethod]
    public void Test_PortfolioEngine_SimulateDynamicRebalancing_MonthlyAndBands()
    {
        var baseDate = new DateTime(2023, 1, 1);
        int days = 250;

        var navHistoryA = new List<NavRecord>();
        var navHistoryB = new List<NavRecord>();
        decimal navA = 1.0m;
        decimal navB = 1.0m;

        for (int i = 0; i < days; i++)
        {
            var date = baseDate.AddDays(i);
            navA *= 1.0015m;
            navB *= 0.9995m;

            navHistoryA.Add(new NavRecord { Date = date, UnitNav = navA, CumulativeNav = navA, DailyReturn = 0.15m });
            navHistoryB.Add(new NavRecord { Date = date, UnitNav = navB, CumulativeNav = navB, DailyReturn = -0.05m });
        }

        var fundA = new FundDetail { Code = "A01", Name = "高成长基金", NavHistory = navHistoryA };
        var fundB = new FundDetail { Code = "B02", Name = "防守型基金", NavHistory = navHistoryB };
        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 0.5m),
            (fundB, 0.5m)
        };

        // 1. 月度定期再平衡仿真
        var simMonthly = PortfolioEngine.SimulateDynamicRebalancing(
            components,
            strategyType: RebalanceStrategyType.MonthlyCalendar,
            initialCapital: 1000000m,
            frictionFeeRate: 0.0015m);

        Assert.IsNotNull(simMonthly);
        Assert.IsTrue(simMonthly.TotalRebalanceCount > 0, "Monthly calendar should trigger rebalance events");
        Assert.IsTrue(simMonthly.TotalFrictionFeeLoss > 0m, "Rebalancing should incur friction fees");
        Assert.IsTrue(simMonthly.Events.Count > 0, "Rebalance events list should be populated");
        Assert.IsTrue(simMonthly.AnnualizedTurnoverRate > 0m, "Turnover rate should be positive");
        Assert.IsFalse(string.IsNullOrWhiteSpace(simMonthly.DiagnosticSummary), "Diagnostic summary should be generated");

        // 2. 偏离容忍度 (ThresholdBand 5%) 再平衡仿真
        var simBand = PortfolioEngine.SimulateDynamicRebalancing(
            components,
            strategyType: RebalanceStrategyType.ThresholdBand,
            initialCapital: 1000000m,
            frictionFeeRate: 0.0015m,
            thresholdBand: 0.05m);

        Assert.IsNotNull(simBand);
        Assert.IsTrue(simBand.TotalRebalanceCount > 0, "Threshold band should trigger rebalances when weights drift > 5%");
        foreach (var ev in simBand.Events)
        {
            Assert.IsTrue(ev.TurnoverRate > 0m, "Event turnover should be positive");
            Assert.IsTrue(ev.FrictionFeeAmount > 0m, "Event friction fee should be positive");
            Assert.IsTrue(ev.MaxWeightDeviationBefore >= 4.9m, "Triggered event should have weight deviation near or above threshold");
        }

        // 3. 买入持有 (Buy & Hold) 仿真：调仓次数应为 0，磨损为 0
        var simBnH = PortfolioEngine.SimulateDynamicRebalancing(
            components,
            strategyType: RebalanceStrategyType.BuyAndHold,
            initialCapital: 1000000m,
            frictionFeeRate: 0.0015m);

        Assert.AreEqual(0, simBnH.TotalRebalanceCount, "Buy & Hold should have 0 rebalances");
        Assert.AreEqual(0m, simBnH.TotalFrictionFeeLoss, "Buy & Hold should have 0 friction fees");
        Assert.AreEqual(0m, simBnH.TotalTurnoverRate, "Buy & Hold should have 0 turnover");
    }

    [TestMethod]
    public void Test_ExportService_SingleFund_CsvAndHtml_IncludesPhase8Models()
    {
        var (navs, bmks) = CreateSynthesizedFundAndBenchmark(200);

        var fund = new FundDetail
        {
            Code = "000001",
            Name = "华夏成长混合",
            Type = "混合型-偏股",
            ManagerName = "王亚伟",
            ManagerTenure = "5年120天",
            FundSize = "85.20 亿元",
            NavHistory = navs,
            BenchmarkCsi300 = bmks
        };

        var metrics = QuantCalculator.CalculateMetrics(navs, "全周期", bmks);

        // 1. 验证 CSV 导出
        string tempCsvPath = Path.Combine(Path.GetTempPath(), $"test_phase8_single_{Guid.NewGuid():N}.csv");
        try
        {
            ExportService.ExportToCsv(tempCsvPath, fund, metrics, new BacktestResult(), navs);
            Assert.IsTrue(File.Exists(tempCsvPath), "CSV export file should exist");
            string csvContent = File.ReadAllText(tempCsvPath);

            StringAssert.Contains(csvContent, "Barra CNE5/CNE6 风格多因子归因与收益解构");
            StringAssert.Contains(csvContent, "特质阿尔法 (Specific Alpha)");
            StringAssert.Contains(csvContent, "基金经理任期全景与滚动超额胜率矩阵");
            StringAssert.Contains(csvContent, "滚动月度胜率 (vs 沪深300)");
        }
        finally
        {
            if (File.Exists(tempCsvPath)) File.Delete(tempCsvPath);
        }

        // 2. 验证 HTML 导出
        string tempHtmlPath = Path.Combine(Path.GetTempPath(), $"test_phase8_single_{Guid.NewGuid():N}.html");
        try
        {
            ExportService.ExportToHtml(tempHtmlPath, fund, metrics, new BacktestResult(), navs);
            Assert.IsTrue(File.Exists(tempHtmlPath), "HTML export file should exist");
            string htmlContent = File.ReadAllText(tempHtmlPath);

            StringAssert.Contains(htmlContent, "Barra CNE5/CNE6 风格多因子归因与收益解构");
            StringAssert.Contains(htmlContent, "特质Alpha");
            StringAssert.Contains(htmlContent, "基金经理生涯任期全景与滚动胜率矩阵");
            StringAssert.Contains(htmlContent, "滚动月度胜率");
        }
        finally
        {
            if (File.Exists(tempHtmlPath)) File.Delete(tempHtmlPath);
        }
    }

    [TestMethod]
    public void Test_ExportService_Portfolio_CsvAndHtml_IncludesDynamicRebalance()
    {
        var (navsA, bmks) = CreateSynthesizedFundAndBenchmark(200, dailyDrift: 0.001);
        var (navsB, _) = CreateSynthesizedFundAndBenchmark(200, dailyDrift: 0.0003);

        var fundA = new FundDetail { Code = "F01", Name = "基金A", NavHistory = navsA, BenchmarkCsi300 = bmks };
        var fundB = new FundDetail { Code = "F02", Name = "基金B", NavHistory = navsB, BenchmarkCsi300 = bmks };
        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (fundA, 60m),
            (fundB, 40m)
        };

        var portfolioResult = PortfolioEngine.CalculatePortfolio(components, 2.0m, "全周期");

        Assert.IsNotNull(portfolioResult.RebalanceSimulation, "Rebalance simulation should be automatically attached to PortfolioResult");

        var portfolioItems = new List<PortfolioItem>
        {
            new PortfolioItem { Code = "F01", Name = "基金A", WeightPercent = 60m },
            new PortfolioItem { Code = "F02", Name = "基金B", WeightPercent = 40m }
        };

        // 1. 验证组合 CSV 导出
        string tempCsvPath = Path.Combine(Path.GetTempPath(), $"test_phase8_portfolio_{Guid.NewGuid():N}.csv");
        try
        {
            ExportService.ExportPortfolioToCsv(tempCsvPath, portfolioItems, portfolioResult);
            Assert.IsTrue(File.Exists(tempCsvPath), "Portfolio CSV should exist");
            string csvContent = File.ReadAllText(tempCsvPath);

            StringAssert.Contains(csvContent, "组合全时序动态再平衡仿真与换手磨损损耗");
            StringAssert.Contains(csvContent, "再平衡净阿尔法");
            StringAssert.Contains(csvContent, "摩擦磨损手续费总额");
        }
        finally
        {
            if (File.Exists(tempCsvPath)) File.Delete(tempCsvPath);
        }

        // 2. 验证组合 HTML 研报导出
        string tempHtmlPath = Path.Combine(Path.GetTempPath(), $"test_phase8_portfolio_{Guid.NewGuid():N}.html");
        try
        {
            ExportService.ExportPortfolioToHtml(tempHtmlPath, portfolioItems, portfolioResult);
            Assert.IsTrue(File.Exists(tempHtmlPath), "Portfolio HTML should exist");
            string htmlContent = File.ReadAllText(tempHtmlPath);

            StringAssert.Contains(htmlContent, "全时序动态再平衡仿真与换手磨损损耗");
            StringAssert.Contains(htmlContent, "Black-Litterman 贝叶斯均衡");
            StringAssert.Contains(htmlContent, "净阿尔法");
        }
        finally
        {
            if (File.Exists(tempHtmlPath)) File.Delete(tempHtmlPath);
        }
    }
}
