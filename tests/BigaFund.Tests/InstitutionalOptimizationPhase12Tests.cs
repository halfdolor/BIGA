using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase12Tests
{
    [TestMethod]
    public void TestHistoricalCrisisReplay_RealHistory_MatchesCrisisPeriod()
    {
        // 构造覆盖 2015 股灾踩踏与 2016 熔断时期的真实基金净值序列 (2015-05-01 ~ 2016-03-17, 共320天)
        var navs = new List<NavRecord>();
        var curDate = new DateTime(2015, 5, 1);
        decimal nav = 1.0m;

        for (int i = 0; i < 320; i++)
        {
            var date = curDate.AddDays(i);
            // 2015-06-12 至 2015-08-26 股灾期间设计暴跌
            if (date >= new DateTime(2015, 6, 12) && date <= new DateTime(2015, 8, 26))
            {
                nav *= 0.985m;
            }
            // 2016-01-01 至 2016-01-29 熔断期间再跌
            else if (date >= new DateTime(2016, 1, 1) && date <= new DateTime(2016, 1, 29))
            {
                nav *= 0.99m;
            }
            else
            {
                nav *= 1.002m;
            }

            navs.Add(new NavRecord
            {
                Date = date,
                UnitNav = Math.Round(nav, 4),
                CumulativeNav = Math.Round(nav, 4)
            });
        }

        var result = QuantCalculator.ReplayHistoricalCrisisScenarios(navs, fundBeta: 0.95m);

        Assert.IsNotNull(result);
        Assert.AreEqual(6, result.CrisisItems.Count);

        // 2015 股灾和 2016 熔断应为真实时序回放 (IsSyntheticProxy == false)
        var c2015 = result.CrisisItems.FirstOrDefault(c => c.ScenarioId == "CRISIS_2015");
        Assert.IsNotNull(c2015);
        Assert.IsFalse(c2015.IsSyntheticProxy);
        Assert.IsTrue(c2015.FundMaxDrawdown > 10m, $"2015 最大回撤应大于 10%，实际为 {c2015.FundMaxDrawdown}%");

        var c2016 = result.CrisisItems.FirstOrDefault(c => c.ScenarioId == "CRISIS_2016");
        Assert.IsNotNull(c2016);
        Assert.IsFalse(c2016.IsSyntheticProxy);

        // 2024 微盘股灾基金尚未存续，应触发代理合成 (IsSyntheticProxy == true)
        var c2024 = result.CrisisItems.FirstOrDefault(c => c.ScenarioId == "CRISIS_2024");
        Assert.IsNotNull(c2024);
        Assert.IsTrue(c2024.IsSyntheticProxy);
        Assert.IsTrue(c2024.FundMaxDrawdown > 0);

        Assert.IsTrue(result.ComprehensiveResilienceScore >= 0 && result.ComprehensiveResilienceScore <= 100);
        Assert.IsFalse(string.IsNullOrEmpty(result.OverallResilienceRating));
        Assert.IsFalse(string.IsNullOrEmpty(result.ExecutiveSummary));
    }

    [TestMethod]
    public void TestHistoricalCrisisReplay_SyntheticProxy_PostCrisisFund()
    {
        // 构造仅从 2023 年开始的新基金
        var navs = new List<NavRecord>();
        var curDate = new DateTime(2023, 1, 1);
        decimal nav = 1.0m;

        for (int i = 0; i < 200; i++)
        {
            nav *= 1.0005m;
            navs.Add(new NavRecord
            {
                Date = curDate.AddDays(i),
                UnitNav = Math.Round(nav, 4),
                CumulativeNav = Math.Round(nav, 4)
            });
        }

        var result = QuantCalculator.ReplayHistoricalCrisisScenarios(navs, fundBeta: 1.15m);

        Assert.IsNotNull(result);
        Assert.AreEqual(6, result.CrisisItems.Count);
        // 所有早期历史危机情景都应由代理合成估算填补，绝不留白
        Assert.IsTrue(result.CrisisItems.All(c => c.IsSyntheticProxy));
        foreach (var item in result.CrisisItems)
        {
            Assert.IsTrue(item.FundMaxDrawdown > 0);
            Assert.IsFalse(string.IsNullOrEmpty(item.ResilienceGrade));
            Assert.IsTrue(item.DiagnosticComment.Contains("代理合成"));
        }
    }

    [TestMethod]
    public void TestMultiPeriodCarinoBrinson_StrictlyZeroResidual()
    {
        // 构造 3 期不同宏观周期的板块配置输入 (T1, T2, T3)
        var periods = new List<MultiPeriodBrinsonPeriodInput>();

        // T1: 科技领涨周期
        periods.Add(new MultiPeriodBrinsonPeriodInput
        {
            PeriodName = "2024-Q1",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 3, 31),
            SectorItems = new List<BrinsonSectorItem>
            {
                new() { SectorName = "信息技术", PortfolioWeight = 40m, BenchmarkWeight = 20m, PortfolioReturn = 15.0m, BenchmarkReturn = 10.0m },
                new() { SectorName = "医药生物", PortfolioWeight = 20m, BenchmarkWeight = 25m, PortfolioReturn = 2.0m, BenchmarkReturn = 0.5m },
                new() { SectorName = "金融地产", PortfolioWeight = 20m, BenchmarkWeight = 30m, PortfolioReturn = 1.0m, BenchmarkReturn = 1.5m },
                new() { SectorName = "日常消费", PortfolioWeight = 20m, BenchmarkWeight = 25m, PortfolioReturn = 4.0m, BenchmarkReturn = 2.0m }
            }
        });

        // T2: 震荡调整周期
        periods.Add(new MultiPeriodBrinsonPeriodInput
        {
            PeriodName = "2024-Q2",
            StartDate = new DateTime(2024, 4, 1),
            EndDate = new DateTime(2024, 6, 30),
            SectorItems = new List<BrinsonSectorItem>
            {
                new() { SectorName = "信息技术", PortfolioWeight = 35m, BenchmarkWeight = 20m, PortfolioReturn = -3.0m, BenchmarkReturn = -6.0m },
                new() { SectorName = "医药生物", PortfolioWeight = 25m, BenchmarkWeight = 25m, PortfolioReturn = -1.0m, BenchmarkReturn = -4.0m },
                new() { SectorName = "金融地产", PortfolioWeight = 25m, BenchmarkWeight = 30m, PortfolioReturn = 0.5m, BenchmarkReturn = -2.0m },
                new() { SectorName = "日常消费", PortfolioWeight = 15m, BenchmarkWeight = 25m, PortfolioReturn = -4.0m, BenchmarkReturn = -5.0m }
            }
        });

        // T3: 价值修复周期
        periods.Add(new MultiPeriodBrinsonPeriodInput
        {
            PeriodName = "2024-Q3",
            StartDate = new DateTime(2024, 7, 1),
            EndDate = new DateTime(2024, 9, 30),
            SectorItems = new List<BrinsonSectorItem>
            {
                new() { SectorName = "信息技术", PortfolioWeight = 30m, BenchmarkWeight = 20m, PortfolioReturn = 14.0m, BenchmarkReturn = 9.0m },
                new() { SectorName = "医药生物", PortfolioWeight = 20m, BenchmarkWeight = 25m, PortfolioReturn = 8.0m, BenchmarkReturn = 6.0m },
                new() { SectorName = "金融地产", PortfolioWeight = 30m, BenchmarkWeight = 30m, PortfolioReturn = 13.0m, BenchmarkReturn = 8.0m },
                new() { SectorName = "日常消费", PortfolioWeight = 20m, BenchmarkWeight = 25m, PortfolioReturn = 9.0m, BenchmarkReturn = 4.0m }
            }
        });

        var result = BrinsonAttributionEngine.CalculateMultiPeriodCarinoBrinson(periods);

        Assert.IsNotNull(result);
        // 验证数学严格守恒：平滑效应之和与多期复利几何总超额残差严格小于 0.001%
        Assert.IsTrue(result.IsStrictlyConserved, $"Carino 算法未能保持严格几何守恒，残差为: {result.ResidualGap}%");
        Assert.IsTrue(Math.Abs(result.ResidualGap) < 0.001m);

        decimal sumEffects = result.CarinoAllocationEffect + result.CarinoSelectionEffect + result.CarinoInteractionEffect;
        Assert.AreEqual((double)result.CumulativeExcessReturn, (double)sumEffects, 0.001, "分项效应和与几何总超额不符");

        // 验证板块分解
        Assert.AreEqual(4, result.SectorAttributions.Count);
        decimal sumSectorAlpha = result.SectorAttributions.Sum(s => s.CumulativeTotalAlpha);
        Assert.AreEqual((double)result.CumulativeExcessReturn, (double)sumSectorAlpha, 0.05, "行业板块加总超额不符");
    }

    [TestMethod]
    public void TestTacticalAssetAllocation_MomentumRotation_ExecutesAndProtects()
    {
        // 构造包含 3 只不同走势基金的候选池
        var universe = new List<FundDetail>();
        var baseDate = new DateTime(2024, 1, 1);
        int totalDays = 100;
        int lookback = 15;

        // 基金 A: 强劲上升动量
        var fA = new FundDetail { Code = "000001", Name = "动量科技先锋", Type = "股票型" };
        decimal navA = 1.0m;
        for (int i = 0; i < totalDays; i++)
        {
            navA *= 1.004m;
            fA.NavHistory.Add(new NavRecord { Date = baseDate.AddDays(i), UnitNav = navA, CumulativeNav = navA });
        }
        universe.Add(fA);

        // 基金 B: 持续单边下跌
        var fB = new FundDetail { Code = "000002", Name = "弱势周期衰退", Type = "混合型" };
        decimal navB = 1.0m;
        for (int i = 0; i < totalDays; i++)
        {
            navB *= 0.997m;
            fB.NavHistory.Add(new NavRecord { Date = baseDate.AddDays(i), UnitNav = navB, CumulativeNav = navB });
        }
        universe.Add(fB);

        // 基金 C: 稳健低波小涨
        var fC = new FundDetail { Code = "000003", Name = "稳健价值红利", Type = "股票型" };
        decimal navC = 1.0m;
        for (int i = 0; i < totalDays; i++)
        {
            navC *= 1.001m;
            fC.NavHistory.Add(new NavRecord { Date = baseDate.AddDays(i), UnitNav = navC, CumulativeNav = navC });
        }
        universe.Add(fC);

        // 执行 TAA 动量轮动回测
        var result = PortfolioEngine.RunTacticalAssetAllocationBacktest(
            universe,
            lookbackDays: lookback,
            rebalanceIntervalDays: 15,
            topN: 1,
            useInverseVolWeight: true,
            defensiveCashRule: true);

        Assert.IsNotNull(result);
        Assert.AreEqual(totalDays - lookback, result.StrategyNavHistory.Count);
        Assert.AreEqual(1.0m, result.StrategyNavHistory[0].UnitNav);
        Assert.IsTrue(result.TotalReturn > 0, $"动量策略理应获取正收益，实际: {result.TotalReturn}%");
        Assert.IsTrue(result.RebalanceHistory.Count > 0);
        Assert.IsFalse(string.IsNullOrEmpty(result.StrategyDiagnosis));

        // 验证调仓日志记录有效
        foreach (var rec in result.RebalanceHistory)
        {
            Assert.IsNotNull(rec.TargetWeights);
            Assert.IsTrue(rec.TargetWeights.Count > 0);
        }
    }

    [TestMethod]
    public void TestExecutivePitchDeck_GenerationAndExport()
    {
        var fund = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘核心优选",
            Type = "混合型",
            ManagerName = "张坤",
            ManagerTenure = "12.5年",
            FundSize = "280.5亿元"
        };

        var navs = new List<NavRecord>();
        var curDate = new DateTime(2023, 1, 1);
        decimal nav = 1.0m;
        for (int i = 0; i < 260; i++)
        {
            nav *= (i % 2 == 0) ? 1.003m : 0.999m;
            navs.Add(new NavRecord { Date = curDate.AddDays(i), UnitNav = nav, CumulativeNav = nav });
        }
        fund.NavHistory = navs;

        var metrics = QuantCalculator.CalculateMetrics(navs, "全历程");
        fund.QuantMetrics = metrics;

        // 生成 Pitch Deck HTML
        string html = ExportService.GenerateExecutivePitchDeckHtml(fund, metrics);

        Assert.IsFalse(string.IsNullOrEmpty(html));
        StringAssert.Contains(html, "机构投资决策委员会·核心配置评审画册");
        StringAssert.Contains(html, "易方达中小盘核心优选");
        StringAssert.Contains(html, "极端宏观黑天鹅历史危机情景全息回放");
        StringAssert.Contains(html, "投资决策委员会配置决策签批表");

        // 导出至临时文件验证物理持久化
        string tempPath = Path.Combine(Path.GetTempPath(), $"biga_pitch_deck_{Guid.NewGuid():N}.html");
        try
        {
            ExportService.ExportExecutivePitchDeckToHtml(tempPath, fund, metrics);
            Assert.IsTrue(File.Exists(tempPath));
            var fileInfo = new FileInfo(tempPath);
            Assert.IsTrue(fileInfo.Length > 1000);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
