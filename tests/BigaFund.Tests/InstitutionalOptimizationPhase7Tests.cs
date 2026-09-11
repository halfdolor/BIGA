using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase7Tests
{
    [TestMethod]
    public void Test_QuantCalculator_DownsideDeviation_Calculation()
    {
        // 1. 全正收益序列，下行半离散度应为 0%
        var positiveReturns = new List<double> { 0.01, 0.02, 0.015, 0.008, 0.012 };
        var ddPos = QuantCalculator.CalculateDownsideDeviation(positiveReturns, 2.0m);
        Assert.AreEqual(0m, ddPos, "All returns above Rf threshold should yield 0 downside deviation");

        // 2. 含有下行亏损的序列
        var mixedReturns = new List<double> { 0.02, -0.01, -0.02, 0.01, -0.015 };
        var ddMixed = QuantCalculator.CalculateDownsideDeviation(mixedReturns, 2.0m);
        Assert.IsTrue(ddMixed > 0m, "Mixed returns with losses should yield positive downside deviation");
    }

    [TestMethod]
    public void Test_QuantCalculator_OmegaRatio_Calculation()
    {
        // 1. 期望收益显著为正且无下行亏损时，Omega 比率应返回上限 99.99
        var positiveReturns = new List<double> { 0.01, 0.02, 0.015 };
        var omegaPos = QuantCalculator.CalculateOmegaRatio(positiveReturns, 2.0m);
        Assert.AreEqual(99.99m, omegaPos, "Omega ratio should cap at 99.99 when downside is zero");

        // 2. 强牛市序列：上涨日收益总和显著大于下跌日损失总和，Omega 应 > 1.0
        var strongReturns = new List<double> { 0.03, 0.02, -0.005, 0.015, -0.002 };
        var omegaStrong = QuantCalculator.CalculateOmegaRatio(strongReturns, 2.0m);
        Assert.IsTrue(omegaStrong > 1.0m, "Omega ratio should be > 1.0 when gains outweigh losses");

        // 3. 熊市序列：下跌亏损总和大于收益，Omega 应 < 1.0
        var weakReturns = new List<double> { -0.03, -0.02, 0.005, -0.015, 0.002 };
        var omegaWeak = QuantCalculator.CalculateOmegaRatio(weakReturns, 2.0m);
        Assert.IsTrue(omegaWeak < 1.0m, "Omega ratio should be < 1.0 in bearish loss-dominated series");
    }

    [TestMethod]
    public void Test_QuantCalculator_UlcerIndexAndMartinRatio_Calculation()
    {
        var baseDate = new DateTime(2023, 1, 1);

        // 1. 单调平稳上升的净值序列，无回撤，溃疡指数 UI 应为 0，马丁比率应为 99.99
        var monotonicNavs = new List<NavRecord>();
        for (int i = 0; i < 50; i++)
        {
            monotonicNavs.Add(new NavRecord
            {
                Date = baseDate.AddDays(i),
                UnitNav = 1.0m + i * 0.01m,
                CumulativeNav = 1.0m + i * 0.01m,
                DailyReturn = 0.01m
            });
        }
        var (uiMono, martinMono) = QuantCalculator.CalculateUlcerIndexAndMartin(monotonicNavs, 15m, 2m);
        Assert.AreEqual(0m, uiMono, "Monotonic NAV should have 0 ulcer index");
        Assert.AreEqual(99.99m, martinMono, "Monotonic NAV should have capped Martin ratio");

        // 2. 经历深幅回撤再修复的序列，Ulcer Index 应为正数，Martin Ratio 相应计算
        var volatileNavs = new List<NavRecord>();
        decimal[] navValues = new decimal[] { 1.0m, 1.2m, 1.0m, 0.8m, 0.9m, 1.1m, 1.3m };
        for (int i = 0; i < navValues.Length; i++)
        {
            volatileNavs.Add(new NavRecord
            {
                Date = baseDate.AddDays(i),
                UnitNav = navValues[i],
                CumulativeNav = navValues[i],
                DailyReturn = i > 0 ? (navValues[i] - navValues[i - 1]) / navValues[i - 1] : 0m
            });
        }
        var (uiVol, martinVol) = QuantCalculator.CalculateUlcerIndexAndMartin(volatileNavs, 10m, 2m);
        Assert.IsTrue(uiVol > 0m, "Volatile NAV should have positive ulcer index");
        Assert.IsTrue(martinVol > 0m, "Martin ratio should be positive for positive excess return");
    }

    [TestMethod]
    public void Test_QuantCalculator_PainRatio_Calculation()
    {
        var baseDate = new DateTime(2023, 1, 1);

        // 1. 经历回撤的水下序列
        var navs = new List<NavRecord>
        {
            new() { Date = baseDate, UnitNav = 1.0m },
            new() { Date = baseDate.AddDays(1), UnitNav = 1.2m },
            new() { Date = baseDate.AddDays(2), UnitNav = 0.9m }, // 跌 25%
            new() { Date = baseDate.AddDays(3), UnitNav = 1.0m }, // 仍在水下
            new() { Date = baseDate.AddDays(4), UnitNav = 1.25m } // 创新高
        };

        decimal painRatio = QuantCalculator.CalculatePainRatio(navs, 12m);
        Assert.IsTrue(painRatio > 0m, "Pain ratio should be positive");
    }

    [TestMethod]
    public void Test_QuantCalculator_Top5DrawdownEpisodes_Detection()
    {
        var baseDate = new DateTime(2023, 1, 1);
        var navs = new List<NavRecord>();

        // 构造两次明确的回撤周期：
        // 周期 A (天 0 到 30)：峰值 1.0 -> 谷底 0.70 (-30%) -> 修复到 1.05
        // 周期 B (天 31 到 60)：峰值 1.20 -> 谷底 1.02 (-15%) -> 修复到 1.25
        decimal currentNav = 1.0m;

        // Peak 1 at Day 5: 1.0
        for (int i = 0; i <= 5; i++)
        {
            currentNav = 0.95m + i * 0.01m;
            navs.Add(new NavRecord { Date = baseDate.AddDays(i), UnitNav = currentNav, CumulativeNav = currentNav });
        }
        DateTime peak1Date = baseDate.AddDays(5); // UnitNav = 1.00

        // Fall to trough at Day 15: 0.70 (-30%)
        for (int i = 6; i <= 15; i++)
        {
            currentNav = 1.00m - (i - 5) * 0.03m;
            navs.Add(new NavRecord { Date = baseDate.AddDays(i), UnitNav = currentNav, CumulativeNav = currentNav });
        }
        DateTime trough1Date = baseDate.AddDays(15); // UnitNav = 0.70

        // Recover to 1.05 at Day 25
        for (int i = 16; i <= 25; i++)
        {
            currentNav = 0.70m + (i - 15) * 0.035m;
            navs.Add(new NavRecord { Date = baseDate.AddDays(i), UnitNav = currentNav, CumulativeNav = currentNav });
        }
        // At Day 25, currentNav = 1.05 > 1.00, so recovered

        // Peak 2 at Day 35: 1.20
        for (int i = 26; i <= 35; i++)
        {
            currentNav = 1.05m + (i - 25) * 0.015m;
            navs.Add(new NavRecord { Date = baseDate.AddDays(i), UnitNav = currentNav, CumulativeNav = currentNav });
        }
        DateTime peak2Date = baseDate.AddDays(35); // UnitNav = 1.20

        // Fall to trough at Day 45: 1.02 (-15%)
        for (int i = 36; i <= 45; i++)
        {
            currentNav = 1.20m - (i - 35) * 0.018m;
            navs.Add(new NavRecord { Date = baseDate.AddDays(i), UnitNav = currentNav, CumulativeNav = currentNav });
        }

        // Recover to 1.25 at Day 55
        for (int i = 46; i <= 55; i++)
        {
            currentNav = 1.02m + (i - 45) * 0.023m;
            navs.Add(new NavRecord { Date = baseDate.AddDays(i), UnitNav = currentNav, CumulativeNav = currentNav });
        }

        var episodes = QuantCalculator.CalculateDrawdownEpisodes(navs);

        // 验证识别出的回撤周期
        Assert.IsTrue(episodes.Count >= 2, $"Expected at least 2 drawdown episodes, got {episodes.Count}");

        // 第一深的回撤应该是 -30% 左右
        var ep1 = episodes[0];
        Assert.AreEqual(1, ep1.Rank);
        Assert.IsTrue(ep1.DrawdownPercent <= -25m, $"Episode 1 drawdown should be <= -25%, got {ep1.DrawdownPercent}%");
        Assert.AreEqual(peak1Date, ep1.PeakDate);
        Assert.AreEqual(trough1Date, ep1.TroughDate);
        Assert.IsTrue(ep1.IsRecovered, "Episode 1 should be recovered");
        Assert.IsTrue(ep1.FallDays > 0, "Episode 1 FallDays should be > 0");
        Assert.IsTrue(ep1.RecoveryDays > 0, "Episode 1 RecoveryDays should be > 0");
        Assert.AreEqual(ep1.FallDays + ep1.RecoveryDays.Value, ep1.TotalDays);

        // 第二深的回撤应该是 -15% 左右
        var ep2 = episodes[1];
        Assert.AreEqual(2, ep2.Rank);
        Assert.IsTrue(ep2.DrawdownPercent <= -10m && ep2.DrawdownPercent > ep1.DrawdownPercent);
        Assert.AreEqual(peak2Date, ep2.PeakDate);
    }

    [TestMethod]
    public void Test_PortfolioLookThrough_HHI_And_EffectiveStockCount_Calculation()
    {
        // 构造两只测试基金
        // Fund 1: 持有 股票A (50%), 股票B (50%)
        // Fund 2: 持有 股票A (50%), 股票C (50%)
        var f1 = new FundDetail
        {
            Code = "F1",
            Name = "Fund One",
            Type = "混合型",
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 10m, Industry = "食品饮料" },
                new() { StockCode = "300750", StockName = "宁德时代", WeightPercent = 10m, Industry = "电力设备" }
            }
        };

        var f2 = new FundDetail
        {
            Code = "F2",
            Name = "Fund Two",
            Type = "股票型",
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 10m, Industry = "食品饮料" },
                new() { StockCode = "000858", StockName = "五粮液", WeightPercent = 10m, Industry = "食品饮料" }
            }
        };

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (f1, 50m),
            (f2, 50m)
        };

        var lookThrough = PortfolioEngine.CalculateLookThroughHoldings(components);

        Assert.IsNotNull(lookThrough);
        Assert.AreEqual(3, lookThrough.TotalUniqueStocks); // 贵州茅台、宁德时代、五粮液
        Assert.IsTrue(lookThrough.StocksHhi > 0m, "Stocks HHI should be positive");
        Assert.IsTrue(lookThrough.EffectiveStockCount > 0m, "Effective stock count should be positive");
        Assert.IsTrue(lookThrough.IndustryHhi > 0m, "Industry HHI should be positive");
        Assert.IsFalse(string.IsNullOrEmpty(lookThrough.HhiConcentrationLevel), "HHI concentration level should not be empty");

        // 贵州茅台在两只基金中均有 10% * 50% = 5% + 5% = 10% 穿透权重
        var moutai = lookThrough.TopHoldings.FirstOrDefault(h => h.StockCode == "600519");
        Assert.IsNotNull(moutai);
        Assert.AreEqual(10.0m, moutai.PortfolioWeight);
        Assert.AreEqual(2, moutai.FundCount);
    }

    [TestMethod]
    public void Test_MacroScenarioShockSimulator_Scenarios_And_CustomShock()
    {
        // 构造测试基金列表
        var f1 = new FundDetail
        {
            Code = "000001",
            Name = "成长先锋",
            Type = "偏股混合型",
            NavHistory = new List<NavRecord>()
        };

        var f2 = new FundDetail
        {
            Code = "000002",
            Name = "纯债丰利",
            Type = "纯债型",
            NavHistory = new List<NavRecord>()
        };

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (f1, 60m),
            (f2, 40m)
        };

        var portfolioResult = new PortfolioResult
        {
            PortfolioName = "机构股债平衡组合",
            AnnualizedReturn = 8.5m,
            AnnualizedVolatility = 12.0m,
            SharpeRatio = 0.85m,
            MaxDrawdown = 14.5m
        };

        var shockResult = PortfolioEngine.RunMacroScenarioShock(components);

        Assert.IsNotNull(shockResult);
        Assert.AreEqual(6, shockResult.PresetScenarios.Count, "Should generate exactly 6 standard institutional macro shock scenarios");

        // 验证 S1：流动性危机 / 黑天鹅暴跌 (权益 -20%, 利率 -20bps)
        var s1 = shockResult.PresetScenarios.FirstOrDefault(s => s.ScenarioId == "S1");
        Assert.IsNotNull(s1);
        Assert.AreEqual(-20m, s1.EquityShockPercent);
        Assert.AreEqual(-20m, s1.InterestRateShockBps);
        Assert.IsTrue(s1.EstimatedNavChangePercent < 0m, "Equity heavy portfolio should drop under S1");
        Assert.IsTrue(s1.StressedVaR95 > 0m, "Stressed VaR95 should be positive");

        // 验证自定义连续冲击仿真：权益下跌 10%，利率上升 30 bps
        var custom = PortfolioEngine.SimulateScenario(components, "CUSTOM", "自定义冲击", "测试描述", -10m, 30m);
        Assert.IsNotNull(custom);
        Assert.AreEqual(-10m, custom.EquityShockPercent);
        Assert.AreEqual(30m, custom.InterestRateShockBps);
        Assert.IsTrue(custom.EstimatedNavChangePercent < 0m);
        Assert.AreEqual(2, custom.FundImpacts.Count);
    }

    [TestMethod]
    public void Test_ExportService_Phase7_Csv_And_Html_Export()
    {
        var baseDate = new DateTime(2023, 1, 1);
        var navs = new List<NavRecord>();
        var benchmarks = new List<BenchmarkRecord>();
        decimal nav = 1.0m;

        for (int i = 0; i < 60; i++)
        {
            nav *= 1.002m;
            var dt = baseDate.AddDays(i);
            navs.Add(new NavRecord { Date = dt, UnitNav = nav, CumulativeNav = nav, DailyReturn = 0.002m });
            benchmarks.Add(new BenchmarkRecord { Date = dt, CumulativeReturnRate = (nav - 1m) * 100m });
        }

        var fund = new FundDetail
        {
            Code = "999001",
            Name = "测试全能基金",
            Type = "混合型",
            NavHistory = navs,
            BenchmarkCsi300 = benchmarks
        };

        var metrics = QuantCalculator.CalculateMetrics(navs, "1年", benchmarks);

        // 验证 Phase 7 核心下行非正态指标已解算
        Assert.IsTrue(metrics.OmegaRatio > 0m);
        Assert.IsTrue(metrics.MartinRatio >= 0m);

        // 导出单基金 CSV
        string csvPath = Path.Combine(Path.GetTempPath(), $"biga_fund_test_{Guid.NewGuid():N}.csv");
        try
        {
            ExportService.ExportToCsv(csvPath, fund, metrics, new BacktestResult(), navs);
            Assert.IsTrue(File.Exists(csvPath));
            string csvContent = File.ReadAllText(csvPath);
            Assert.IsTrue(csvContent.Contains("奥米加比率 (Omega Ratio)"));
            Assert.IsTrue(csvContent.Contains("溃疡指数 (Ulcer Index)"));
            Assert.IsTrue(csvContent.Contains("马丁比率 (Martin Ratio)"));
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }

        // 导出组合 CSV 与 HTML
        var portfolio = new PortfolioResult
        {
            PortfolioName = "测试多资产组合",
            StartDate = baseDate,
            EndDate = baseDate.AddDays(59),
            TradingDays = 60,
            TotalReturn = 12.0m,
            AnnualizedReturn = 12.5m,
            AnnualizedVolatility = 8.0m,
            SharpeRatio = 1.3m,
            MaxDrawdown = 5.0m,
            OmegaRatio = 1.85m,
            UlcerIndex = 1.2m,
            MartinRatio = 8.75m,
            PainRatio = 6.4m,
            DownsideDeviation = 3.5m,
            PortfolioNavHistory = navs
        };

        portfolio.MacroShockResult = PortfolioEngine.RunMacroScenarioShock(new List<(FundDetail Fund, decimal WeightPercent)> { (fund, 100m) });

        string portCsvPath = Path.Combine(Path.GetTempPath(), $"biga_port_test_{Guid.NewGuid():N}.csv");
        string portHtmlPath = Path.Combine(Path.GetTempPath(), $"biga_port_test_{Guid.NewGuid():N}.html");
        try
        {
            ExportService.ExportPortfolioToCsv(portCsvPath, new List<PortfolioItem> { new() { Code = fund.Code, Name = fund.Name, WeightPercent = 100m } }, portfolio);
            Assert.IsTrue(File.Exists(portCsvPath));
            string portCsvContent = File.ReadAllText(portCsvPath);
            Assert.IsTrue(portCsvContent.Contains("奥米加比率 (Omega Ratio)"));
            Assert.IsTrue(portCsvContent.Contains("溃疡指数 (Ulcer Index)"));
            Assert.IsTrue(portCsvContent.Contains("宏观情景冲击与多因子压力测试"));

            ExportService.ExportPortfolioToHtml(portHtmlPath, new List<PortfolioItem> { new() { Code = fund.Code, Name = fund.Name, WeightPercent = 100m } }, portfolio);
            Assert.IsTrue(File.Exists(portHtmlPath));
            string portHtmlContent = File.ReadAllText(portHtmlPath);
            Assert.IsTrue(portHtmlContent.Contains("奥米加比率 (Omega)"));
            Assert.IsTrue(portHtmlContent.Contains("溃疡指数 (Ulcer UI)"));
            Assert.IsTrue(portHtmlContent.Contains("宏观情景冲击与多因子压力测试"));
        }
        finally
        {
            if (File.Exists(portCsvPath)) File.Delete(portCsvPath);
            if (File.Exists(portHtmlPath)) File.Delete(portHtmlPath);
        }
    }
}
