using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase22Tests
{
    private static List<NavRecord> GenerateNavSeries(DateTime start, int days, double dailyMean, double dailyStd)
    {
        var list = new List<NavRecord>();
        double nav = 1.0;
        var rng = new Random(65432);

        for (int i = 0; i < days; i++)
        {
            list.Add(new NavRecord
            {
                Date = start.AddDays(i),
                UnitNav = (decimal)nav,
                CumulativeNav = (decimal)nav
            });

            double u1 = Math.Max(1e-8, rng.NextDouble());
            double u2 = Math.Max(1e-8, rng.NextDouble());
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            double ret = dailyMean + dailyStd * z;
            nav *= (1.0 + ret);
        }
        return list;
    }

    [TestMethod]
    public void Test_DecileSpreadBacktest_TenDecilePartitionAndCoverage()
    {
        // 1. 构造 50 只具备连续分值分布的选基样本池
        var funds = new List<StoredFundItem>();
        for (int i = 1; i <= 50; i++)
        {
            funds.Add(new StoredFundItem
            {
                Code = $"{i:D6}",
                Name = $"测试基金_{i}",
                Type = "偏股混合型",
                MultiFactorCompositeScore = 100m - i * 1.5m, // 从 98.5 降至 25.0
                Return1Y = 35.0m - i * 0.8m,
                MaxDrawdown = 10.0m + i * 0.3m,
                SharpeRatio = 1.8m - i * 0.03m
            });
        }

        var result = QuantCalculator.CalculateDecileSpreadBacktest(funds, "Composite");

        Assert.IsNotNull(result);
        Assert.AreEqual(50, result.TotalUniverseCount);
        Assert.AreEqual(10, result.Deciles.Count, "Must divide universe into exactly 10 deciles D1..D10");

        // 验证全覆盖
        int totalCovered = result.Deciles.Sum(d => d.FundCount);
        Assert.AreEqual(50, totalCovered, "All 50 funds must be accounted for across 10 deciles");

        // 验证 D1 与 D10 命名与排序
        Assert.AreEqual("D1", result.Deciles[0].DecileName);
        Assert.AreEqual("D10", result.Deciles[9].DecileName);
        Assert.IsTrue(result.Deciles[0].DecileRole.Contains("D1"));
        Assert.IsTrue(result.Deciles[9].DecileRole.Contains("D10"));
    }

    [TestMethod]
    public void Test_DecileSpreadBacktest_LongShortSpreadAndTStatistic()
    {
        // 构造强单调有效因子样本池 (D1 收益率显著高于 D10)
        var funds = new List<StoredFundItem>();
        for (int i = 1; i <= 40; i++)
        {
            funds.Add(new StoredFundItem
            {
                Code = $"{i:D6}",
                Name = $"单调基金_{i}",
                Type = "股票型",
                MomentumScore = 100m - i * 2.0m,
                Return1Y = 40.0m - i * 1.0m, // 严格单调递减
                MaxDrawdown = 12.0m + i * 0.2m
            });
        }

        var result = QuantCalculator.CalculateDecileSpreadBacktest(funds, "Momentum");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.LongShortAnnualizedSpread > 15.0m, "D1 - D10 spread must be significantly positive");
        Assert.IsTrue(result.LongShortSharpe > 0.5m, "Long-short spread Sharpe must be robust");
        Assert.IsTrue(result.FactorTStatistic > 2.0m, "t-statistic must exceed 2.0 critical value");
        Assert.IsTrue(result.IsStatisticallySignificant, "Factor must be statistically significant");
        Assert.IsTrue(result.MonotonicityScorePercent >= 70.0m, "Monotonicity score must be high for monotonically decreasing returns");
        Assert.IsTrue(result.FactorEfficacyGrade.Contains("卓越") || result.FactorEfficacyGrade.Contains("显著"));
    }

    [TestMethod]
    public void Test_DecileSpreadBacktest_SpearmanRankCorrelationAndDegradedFactor()
    {
        // 构造无显著区分度的噪声因子样本池
        var funds = new List<StoredFundItem>();
        var rng = new Random(12345);
        for (int i = 1; i <= 30; i++)
        {
            funds.Add(new StoredFundItem
            {
                Code = $"{i:D6}",
                Name = $"随机基金_{i}",
                Type = "灵活配置型",
                AlphaPurityScore = (decimal)(rng.NextDouble() * 50 + 25),
                Return1Y = (decimal)(rng.NextDouble() * 20 - 10), // 杂乱无序收益
                MaxDrawdown = 15.0m
            });
        }

        var result = QuantCalculator.CalculateDecileSpreadBacktest(funds, "AlphaPurity");

        Assert.IsNotNull(result);
        Assert.AreEqual(10, result.Deciles.Count);
        Assert.IsTrue(result.SpearmanRankCorrelation <= 1.0m && result.SpearmanRankCorrelation >= -1.0m);
        Assert.IsFalse(string.IsNullOrEmpty(result.DiagnosticSummary));
    }

    [TestMethod]
    public void Test_HolographicCrisisStress_SixHistoricalScenariosProjected()
    {
        var fundEq = new FundDetail { Code = "000001", Name = "易方达科讯", Type = "偏股混合型", StyleBox = MorningstarStyleBox.LargeCapGrowth };
        var fundBond = new FundDetail { Code = "000002", Name = "招商产业债", Type = "纯债债券型", StyleBox = MorningstarStyleBox.FixedIncome };
        var fundGold = new FundDetail { Code = "000003", Name = "华安黄金ETF", Type = "商品黄金型" };
        var fundCash = new FundDetail { Code = "000004", Name = "华宝添益", Type = "货币型", StyleBox = MorningstarStyleBox.MoneyMarket };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fundEq, 40m),
            (fundBond, 40m),
            (fundGold, 10m),
            (fundCash, 10m)
        };

        var result = QuantCalculator.EvaluateHolographicCrisisStress(components, 1000m);

        Assert.IsNotNull(result);
        Assert.AreEqual(6, result.CrisisScenarios.Count, "Must evaluate exactly 6 historical crises");

        var scenarioIds = result.CrisisScenarios.Select(s => s.ScenarioId).ToList();
        CollectionAssert.Contains(scenarioIds, "GFC_2008");
        CollectionAssert.Contains(scenarioIds, "CRASH_2015");
        CollectionAssert.Contains(scenarioIds, "TRADE_2018");
        CollectionAssert.Contains(scenarioIds, "COVID_2020");
        CollectionAssert.Contains(scenarioIds, "FED_2022");
        CollectionAssert.Contains(scenarioIds, "STAMPEDE_2024");

        Assert.IsTrue(result.WorstCaseLossPercent < 0m, "Worst case must reflect drawdown");
        Assert.IsFalse(string.IsNullOrEmpty(result.WorstCrisisName));
        Assert.IsFalse(string.IsNullOrEmpty(result.OverallResilienceRating));
        Assert.IsTrue(result.CrisisScenarios.All(s => s.StressedVaR99 > 0m));
        Assert.IsTrue(result.CrisisScenarios.All(s => !string.IsNullOrEmpty(s.ResilienceGrade)));
    }

    [TestMethod]
    public void Test_HolographicCrisisStress_AssetSensitivityDivergence()
    {
        // 对比纯权益组合 vs 纯债避险组合在 2008 GFC 冲击下的分化表现
        var eqFund = new FundDetail { Code = "110011", Name = "易方达中小盘", Type = "股票型", StyleBox = MorningstarStyleBox.MidCapGrowth };
        var bondFund = new FundDetail { Code = "001001", Name = "工银纯债", Type = "债券型", StyleBox = MorningstarStyleBox.FixedIncome };

        var eqPortfolio = new List<(FundDetail Fund, decimal Weight)> { (eqFund, 100m) };
        var bondPortfolio = new List<(FundDetail Fund, decimal Weight)> { (bondFund, 100m) };

        var eqStress = QuantCalculator.EvaluateHolographicCrisisStress(eqPortfolio, 1000m);
        var bondStress = QuantCalculator.EvaluateHolographicCrisisStress(bondPortfolio, 1000m);

        var eqGfc = eqStress.CrisisScenarios.First(s => s.ScenarioId == "GFC_2008");
        var bondGfc = bondStress.CrisisScenarios.First(s => s.ScenarioId == "GFC_2008");

        Assert.IsTrue(eqGfc.ProjectedPortfolioReturn <= -50.0m, "Pure equity portfolio must suffer severe drawdown in 2008 GFC");
        Assert.IsTrue(bondGfc.ProjectedPortfolioReturn > 5.0m, "Pure bond portfolio must experience flight-to-safety positive returns in 2008 GFC");
        Assert.IsTrue(eqGfc.EstimatedCapitalLossAmount > bondGfc.EstimatedCapitalLossAmount);
    }

    [TestMethod]
    public void Test_ExecutionShortfall_SquareRootMarketImpactScaling()
    {
        var fund1 = new FundDetail { Code = "000001", Name = "汇添富均衡", Type = "混合型", FundSize = "50亿元" };
        var fund2 = new FundDetail { Code = "000002", Name = "广发稳健", Type = "债券型", FundSize = "80亿元" };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fund1, 60m),
            (fund2, 40m)
        };

        // 测算 1000 万元组合 vs 5000 万元大体量组合的冲击成本
        var smallCapitalResult = PortfolioEngine.SimulateExecutionShortfall(components, null, 1000m);
        var largeCapitalResult = PortfolioEngine.SimulateExecutionShortfall(components, null, 5000m);

        Assert.IsNotNull(smallCapitalResult);
        Assert.IsNotNull(largeCapitalResult);
        Assert.IsTrue(smallCapitalResult.TotalTradeVolumeTenThousand > 0m);
        Assert.IsTrue(largeCapitalResult.TotalTradeVolumeTenThousand > smallCapitalResult.TotalTradeVolumeTenThousand);

        // Almgren-Chriss 平方根冲击定律：交易规模越大，单位冲击基点 (bps) 越高
        Assert.IsTrue(largeCapitalResult.AverageImpactBps >= smallCapitalResult.AverageImpactBps,
            "Average impact bps must increase with trade volume under square root market impact law");
        Assert.IsTrue(largeCapitalResult.TotalMarketImpactCostTenThousand > smallCapitalResult.TotalMarketImpactCostTenThousand);
    }

    [TestMethod]
    public void Test_ExecutionShortfall_CsrcTenPercentGiantRedemptionBreach()
    {
        // 构造微型基金 (AUM = 1000万元)，单次赎回 200万元，赎回比例达 20% (超 10% 警戒线)
        var microFund = new FundDetail
        {
            Code = "009999",
            Name = "小微灵活配置基金",
            Type = "混合型",
            FundSize = "1000万元"
        };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (microFund, 100m)
        };

        var orderSheet = new RebalanceOrderSheet
        {
            TotalSellAmount = 2000000m,
            TotalPortfolioValue = 10000000m,
            Orders = new List<RebalanceOrderItem>
            {
                new()
                {
                    FundCode = "009999",
                    FundName = "小微灵活配置基金",
                    Action = "SELL",
                    CurrentAmount = 5000000m,
                    TargetAmount = 3000000m // 拟赎回 200万元
                }
            }
        };

        var result = PortfolioEngine.SimulateExecutionShortfall(components, orderSheet, 1000m);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.GiantRedemptionBreachCount, "Must detect exactly 1 giant redemption breach");
        var impactItem = result.ImpactItems[0];
        Assert.IsTrue(impactItem.IsGiantRedemptionRisk, "Flag IsGiantRedemptionRisk must be true");
        Assert.IsTrue(impactItem.CsrcRedemptionAumRatioPercent >= 10.0m, "Redemption ratio must exceed 10%");
        Assert.IsTrue(impactItem.GiantRedemptionAlertBadge.Contains("🚨"));
        Assert.IsTrue(result.MaxRecommendedExecutionDays > 1, "Must recommend multi-day execution for giant redemption");
        Assert.IsTrue(result.RecommendedTwapTranches.Count > 1, "Must generate multi-day TWAP tranches");
    }

    [TestMethod]
    public void Test_PortfolioEngine_EndToEnd_Phase22Integration()
    {
        var start = new DateTime(2023, 1, 1);
        var fund1 = new FundDetail
        {
            Code = "163402",
            Name = "兴全趋势投资",
            Type = "混合型",
            FundSize = "150亿元",
            NavHistory = GenerateNavSeries(start, 250, 0.0006, 0.015)
        };

        var fund2 = new FundDetail
        {
            Code = "000083",
            Name = "添富逆向策略",
            Type = "股票型",
            FundSize = "80亿元",
            NavHistory = GenerateNavSeries(start, 250, 0.0007, 0.018)
        };

        var fund3 = new FundDetail
        {
            Code = "001182",
            Name = "易方达高等级信用债",
            Type = "债券型",
            FundSize = "60亿元",
            NavHistory = GenerateNavSeries(start, 250, 0.00015, 0.002)
        };

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (fund1, 45m),
            (fund2, 35m),
            (fund3, 20m)
        };

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);

        // 验证全息宏观危机传导压力测试
        Assert.IsNotNull(result.HistoricalCrisisStress, "HistoricalCrisisStress must be populated in CalculatePortfolio");
        Assert.AreEqual(6, result.HistoricalCrisisStress.CrisisScenarios.Count);
        Assert.IsFalse(string.IsNullOrEmpty(result.HistoricalCrisisStress.OverallResilienceRating));

        // 验证大体量执行落差与平方根市场冲击模型
        Assert.IsNotNull(result.ExecutionShortfall, "ExecutionShortfall must be populated in CalculatePortfolio");
        Assert.AreEqual(3, result.ExecutionShortfall.ImpactItems.Count);
        Assert.IsTrue(result.ExecutionShortfall.TotalTradeVolumeTenThousand > 0m);
        Assert.IsTrue(result.ExecutionShortfall.AverageImpactBps > 0m);
        Assert.IsTrue(result.ExecutionShortfall.RecommendedTwapTranches.Count >= 1);
    }
}
