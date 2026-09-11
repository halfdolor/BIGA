using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase18Tests
{
    [TestMethod]
    public void Test_HoldingsOverlapMatrix_HighOverlap_DetectsRedundancy()
    {
        // 构造两只底层持仓高度相似的基金 (如均重仓茅台、宁德、平安)
        var fundA = new FundDetail
        {
            Code = "000001",
            Name = "蓝筹价值A",
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 9.5m },
                new() { StockCode = "300750", StockName = "宁德时代", WeightPercent = 8.0m },
                new() { StockCode = "601318", StockName = "中国平安", WeightPercent = 7.0m },
                new() { StockCode = "000858", StockName = "五粮液", WeightPercent = 6.0m }
            }
        };

        var fundB = new FundDetail
        {
            Code = "000002",
            Name = "蓝筹精选B",
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 9.0m },
                new() { StockCode = "300750", StockName = "宁德时代", WeightPercent = 7.5m },
                new() { StockCode = "601318", StockName = "中国平安", WeightPercent = 6.5m },
                new() { StockCode = "600036", StockName = "招商银行", WeightPercent = 5.0m }
            }
        };

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (fundA, 50m),
            (fundB, 50m)
        };

        var overlapResult = PortfolioEngine.CalculateHoldingsOverlapMatrix(components);

        Assert.IsNotNull(overlapResult);
        Assert.AreEqual(1, overlapResult.OverlapItems.Count);

        var item = overlapResult.OverlapItems[0];
        Assert.AreEqual("000001", item.FundCodeA);
        Assert.AreEqual("000002", item.FundCodeB);

        // 重叠持仓应为 3 只 (茅台、宁德、平安)
        Assert.AreEqual(3, item.CommonStockCount);

        // min(9.5, 9.0) + min(8.0, 7.5) + min(7.0, 6.5) = 9.0 + 7.5 + 6.5 = 23.0%
        Assert.AreEqual(23.0m, item.WeightedOverlapPercent);

        // Jaccard: 3 交集 / 5 并集 = 60.0%
        Assert.AreEqual(60.0m, item.JaccardSimilarity);

        // 余弦相似度应较高 (> 0.7)
        Assert.IsTrue(item.CosineSimilarity > 0.7m);

        // 高重叠预警判定 (> 20%)
        Assert.IsTrue(item.RedundancyAlert);
        Assert.IsTrue(item.OverlapSeverity.Contains("⚠️"));
        Assert.IsTrue(overlapResult.RedundantPairCount >= 1);
        Assert.IsTrue(overlapResult.RedundancyScore > 0m);
    }

    [TestMethod]
    public void Test_HoldingsOverlapMatrix_ZeroOverlap_ExcellentDiversification()
    {
        // 构造两只完全不同行业的基金 (如消费 vs 芯片)
        var fundA = new FundDetail
        {
            Code = "000001",
            Name = "白酒消费A",
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 10m },
                new() { StockCode = "000858", StockName = "五粮液", WeightPercent = 9m }
            }
        };

        var fundB = new FundDetail
        {
            Code = "000002",
            Name = "半导体芯片B",
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "688981", StockName = "中芯国际", WeightPercent = 10m },
                new() { StockCode = "002371", StockName = "北方华创", WeightPercent = 9m }
            }
        };

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (fundA, 50m),
            (fundB, 50m)
        };

        var overlapResult = PortfolioEngine.CalculateHoldingsOverlapMatrix(components);

        Assert.IsNotNull(overlapResult);
        Assert.AreEqual(1, overlapResult.OverlapItems.Count);

        var item = overlapResult.OverlapItems[0];
        Assert.AreEqual(0, item.CommonStockCount);
        Assert.AreEqual(0m, item.WeightedOverlapPercent);
        Assert.AreEqual(0m, item.JaccardSimilarity);
        Assert.AreEqual(0m, item.CosineSimilarity);
        Assert.IsFalse(item.RedundancyAlert);

        Assert.AreEqual(0, overlapResult.RedundantPairCount);
        Assert.IsTrue(overlapResult.DiversificationHealthGrade.Contains("优良分散"));
    }

    [TestMethod]
    public void Test_RebalanceOrders_LotRoundingAndCashBuffer()
    {
        var fundA = new FundDetail { Code = "000001", Name = "基金A" };
        var fundB = new FundDetail { Code = "000002", Name = "基金B" };

        decimal capital = 1_000_000m;
        // 当前各持有 50 万 (50%)
        var currentHoldings = new List<(FundDetail Fund, decimal CurrentAmount)>
        {
            (fundA, 500_000m),
            (fundB, 500_000m)
        };

        // 目标权重：A 降至 30%，B 升至 70%
        var targetWeights = new Dictionary<string, decimal>
        {
            { "000001", 30m },
            { "000002", 70m }
        };

        decimal lotStep = 100m;
        decimal minSub = 100m;
        decimal cashBufferPercent = 2.0m; // 预留 2% 现金缓冲 (20,000 元)

        var sheet = PortfolioEngine.GenerateRebalanceOrders(
            capital,
            currentHoldings,
            targetWeights,
            "最大夏普比率配置",
            0.12m,
            0.50m,
            lotStep,
            minSub,
            cashBufferPercent);

        Assert.IsNotNull(sheet);
        Assert.AreEqual(capital, sheet.TotalPortfolioValue);
        Assert.AreEqual(20_000m, sheet.CashBufferReserved);
        Assert.AreEqual(2.0m, sheet.CashBufferPercent);
        Assert.AreEqual(lotStep, sheet.LotSizeRoundingStep);

        // 验证每笔交易整手执行额均为 lotStep (100) 的整数倍
        foreach (var order in sheet.Orders)
        {
            Assert.IsTrue(order.ExecutableTradeAmount % lotStep == 0m, $"Order for {order.FundCode} is not rounded to {lotStep}");
            Assert.IsTrue(order.LotRoundingResidual >= 0m && order.LotRoundingResidual < lotStep);
        }

        // 卖出 基金A: 目标净投资市值 980,000 * 30% = 294,000，卖出 206,000 元
        var orderA = sheet.Orders.First(o => o.FundCode == "000001");
        Assert.AreEqual("卖出", orderA.Action);
        Assert.AreEqual(206_000m, orderA.ExecutableTradeAmount);

        // 买入 基金B: 目标净投资市值 980,000 * 70% = 686,000，买入 186,000 元 (净预留 20,000 元现金缓冲)
        var orderB = sheet.Orders.First(o => o.FundCode == "000002");
        Assert.AreEqual("买入", orderB.Action);
        Assert.AreEqual(186_000m, orderB.ExecutableTradeAmount);

        // 换手率应被正确计算
        Assert.IsTrue(sheet.TurnoverRate > 0m);
        Assert.IsTrue(sheet.ExecutableTurnoverRate > 0m);
        Assert.IsTrue(sheet.CashDragAnnualizedCost > 0m);
    }

    [TestMethod]
    public void Test_ExportRebalanceOrdersToCsv_FormatAndIntegrity()
    {
        var fundA = new FundDetail { Code = "000001", Name = "易方达蓝筹" };
        var fundB = new FundDetail { Code = "000002", Name = "华夏成长" };

        var currentHoldings = new List<(FundDetail Fund, decimal CurrentAmount)>
        {
            (fundA, 600_000m),
            (fundB, 400_000m)
        };

        var targetWeights = new Dictionary<string, decimal>
        {
            { "000001", 50m },
            { "000002", 50m }
        };

        var sheet = PortfolioEngine.GenerateRebalanceOrders(
            1_000_000m,
            currentHoldings,
            targetWeights,
            "风险平价配置",
            0.12m,
            0.50m,
            100m,
            100m,
            1.5m);

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_rebalance_{Guid.NewGuid():N}.csv");
        try
        {
            string csv = ExportService.ExportRebalanceOrdersToCsv(sheet, tempPath);

            Assert.IsTrue(File.Exists(tempPath));
            Assert.IsTrue(csv.Contains("=== BIGA 机构级投资组合调仓交易决策单 ==="));
            Assert.IsTrue(csv.Contains("调仓目标方案,风险平价配置"));
            Assert.IsTrue(csv.Contains("预留现金安全缓冲 (元)"));
            Assert.IsTrue(csv.Contains("整手调仓步长 (元)"));
            Assert.IsTrue(csv.Contains("000001,\"易方达蓝筹\""));
            Assert.IsTrue(csv.Contains("000002,\"华夏成长\""));
            Assert.IsTrue(csv.Contains("整手实际交易额(元)"));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [TestMethod]
    public void Test_FactorCrowdingAnalysis_ComputesDispersionAndDimension()
    {
        // 创建历史净值记录
        var navs = new List<NavRecord>();
        var baseDate = new DateTime(2023, 1, 1);
        double nav = 1.0;
        var rand = new Random(42);

        for (int i = 0; i < 300; i++)
        {
            double ret = (rand.NextDouble() - 0.48) * 0.02; // 轻微上升
            nav *= (1.0 + ret);
            navs.Add(new NavRecord
            {
                Date = baseDate.AddDays(i),
                UnitNav = (decimal)nav,
                CumulativeNav = (decimal)nav,
                DailyReturn = (decimal)ret * 100m
            });
        }

        var fund = new FundDetail
        {
            Code = "000001",
            Name = "量化精选A",
            NavHistory = navs
        };

        var crowding = QuantCalculator.AnalyzeFactorCrowdingAndDispersion(fund, navs);

        Assert.IsNotNull(crowding);
        Assert.IsTrue(crowding.Factors.Count > 0);
        Assert.AreEqual(6, crowding.Factors.Count);

        // 验证有效因子维度 Neff 处于 [1, 6] 区间
        Assert.IsTrue(crowding.EffectiveFactorDimension >= 1.0m && crowding.EffectiveFactorDimension <= 6.0m,
            $"Neff={crowding.EffectiveFactorDimension} out of expected [1, 6] range");

        // 验证每个因子的估值差、分位数、拥挤度指数
        foreach (var factor in crowding.Factors)
        {
            Assert.IsTrue(factor.HistoricalSpreadPercentile >= 0m && factor.HistoricalSpreadPercentile <= 100m);
            Assert.IsTrue(factor.CrowdingScore >= 0m && factor.CrowdingScore <= 100m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(factor.RiskStatus));
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(crowding.InstitutionalRiskWarning));
    }
}
