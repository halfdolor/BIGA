using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase19Tests
{
    private static List<NavRecord> GenerateNavSeries(DateTime start, int days, double dailyMean, double dailyStd)
    {
        var list = new List<NavRecord>();
        double nav = 1.0;
        var rng = new Random(42);

        for (int i = 0; i < days; i++)
        {
            list.Add(new NavRecord
            {
                Date = start.AddDays(i),
                UnitNav = (decimal)nav,
                CumulativeNav = (decimal)nav
            });

            // Box-Muller normal random
            double u1 = Math.Max(1e-8, rng.NextDouble());
            double u2 = Math.Max(1e-8, rng.NextDouble());
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            double ret = dailyMean + dailyStd * z;
            nav *= (1.0 + ret);
        }
        return list;
    }

    [TestMethod]
    public void Test_SaaTaaMonitoring_NormalAllocation_PassesCompliance()
    {
        // 构造四类资产：权益 50%, 纯债 30%, 海外 10%, 商品 10% (完美匹配标准 SAA 战略配置)
        var equityFund = new FundDetail { Code = "000001", Name = "沪深300指数增强", Type = "偏股混合型", NavHistory = GenerateNavSeries(DateTime.Today.AddDays(-60), 60, 0.0004, 0.012) };
        var bondFund = new FundDetail { Code = "000002", Name = "中短债纯债基金", Type = "中短债纯债型", NavHistory = GenerateNavSeries(DateTime.Today.AddDays(-60), 60, 0.00015, 0.002) };
        var qdiiFund = new FundDetail { Code = "000003", Name = "标普500海外互联", Type = "QDII全球海外", NavHistory = GenerateNavSeries(DateTime.Today.AddDays(-60), 60, 0.0003, 0.010) };
        var goldFund = new FundDetail { Code = "000004", Name = "黄金大宗商品ETF", Type = "商品黄金大宗", NavHistory = GenerateNavSeries(DateTime.Today.AddDays(-60), 60, 0.0002, 0.008) };

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (equityFund, 50.0m),
            (bondFund, 30.0m),
            (qdiiFund, 10.0m),
            (goldFund, 10.0m)
        };

        var result = PortfolioEngine.CalculateSaaTaaMonitoring(components, equityFund.NavHistory);

        Assert.IsNotNull(result);
        Assert.AreEqual(4, result.AssetClassDeviations.Count);
        Assert.AreEqual(0, result.HardBreachCount);
        Assert.AreEqual(0, result.SoftBreachCount);
        Assert.AreEqual(100.0m, result.SaaComplianceScore);
        Assert.IsFalse(result.RebalanceRequired);
        Assert.IsTrue(result.OverallStatus.Contains("合规"));

        foreach (var dev in result.AssetClassDeviations)
        {
            Assert.AreEqual(0.0m, dev.TacticalDeviation);
            Assert.IsTrue(dev.StatusBadge.Contains("合规"));
        }
    }

    [TestMethod]
    public void Test_SaaTaaMonitoring_TacticalBreach_TriggersRebalanceAndAdvice()
    {
        // 构造极度战术偏离配置：权益资产高达 80% (超配 30%), 纯债 10% (欠配 20%), 海外 5%, 商品 5%
        var equityFund = new FundDetail { Code = "000001", Name = "偏股主板精选", Type = "混合偏股型", NavHistory = GenerateNavSeries(DateTime.Today.AddDays(-60), 60, 0.0004, 0.012) };
        var bondFund = new FundDetail { Code = "000002", Name = "纯债稳健底仓", Type = "纯债固收", NavHistory = GenerateNavSeries(DateTime.Today.AddDays(-60), 60, 0.00015, 0.002) };
        var qdiiFund = new FundDetail { Code = "000003", Name = "纳斯达克互联", Type = "海外QDII", NavHistory = GenerateNavSeries(DateTime.Today.AddDays(-60), 60, 0.0003, 0.010) };
        var goldFund = new FundDetail { Code = "000004", Name = "黄金ETF", Type = "黄金商品", NavHistory = GenerateNavSeries(DateTime.Today.AddDays(-60), 60, 0.0002, 0.008) };

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (equityFund, 80.0m),
            (bondFund, 10.0m),
            (qdiiFund, 5.0m),
            (goldFund, 5.0m)
        };

        var result = PortfolioEngine.CalculateSaaTaaMonitoring(components, equityFund.NavHistory);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.HardBreachCount >= 2, "权益与纯债偏离均应突破 ±5% 硬限额");
        Assert.IsTrue(result.RebalanceRequired, "硬违规必须触发再平衡");
        Assert.IsTrue(result.SaaComplianceScore < 60.0m, "履约评分应显著扣减");
        Assert.IsTrue(result.OverallStatus.Contains("硬违规"));

        var eqDev = result.AssetClassDeviations.First(d => d.AssetClassName.Contains("权益"));
        Assert.AreEqual(30.0m, eqDev.TacticalDeviation);
        Assert.IsTrue(eqDev.StatusBadge.Contains("违规"));
        Assert.IsTrue(eqDev.RebalanceActionAdvice.Contains("减配"));

        var bondDev = result.AssetClassDeviations.First(d => d.AssetClassName.Contains("纯债"));
        Assert.AreEqual(-20.0m, bondDev.TacticalDeviation);
        Assert.IsTrue(bondDev.StatusBadge.Contains("违规"));
        Assert.IsTrue(bondDev.RebalanceActionAdvice.Contains("增配"));
    }

    [TestMethod]
    public void Test_SharpeDecomposition_MencheroDavis_DecomposesContributions()
    {
        // 构造三只具有不同性价比特征的成分基金
        var fundA = new FundDetail { Code = "0001", Name = "高胜率Alpha基金" };
        var fundB = new FundDetail { Code = "0002", Name = "纯债低波压舱石" };
        var fundC = new FundDetail { Code = "0003", Name = "高波低效拖累基金" };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 0.40m),
            (fundB, 0.40m),
            (fundC, 0.20m)
        };

        // 构造日收益率序列 (100 天)
        var rng = new Random(101);
        var rets = new Dictionary<string, List<double>>
        {
            { "0001", new List<double>() },
            { "0002", new List<double>() },
            { "0003", new List<double>() }
        };

        for (int i = 0; i < 100; i++)
        {
            rets["0001"].Add(0.0010 + 0.008 * (rng.NextDouble() - 0.5)); // 年化约 25%, 夏普极高
            rets["0002"].Add(0.0002 + 0.002 * (rng.NextDouble() - 0.5)); // 稳健年化约 5%, 低波
            rets["0003"].Add(-0.0005 + 0.015 * (rng.NextDouble() - 0.5)); // 年化亏损 -12%, 高波高拖累
        }

        var result = PortfolioEngine.CalculatePortfolioSharpeDecomposition(components, rets, 0.12, 0.02);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Items.Count);
        Assert.IsTrue(result.AlphaEngineCount >= 1, "应识别出高胜率 Alpha 引擎");
        Assert.IsTrue(result.SharpeDragCount >= 1, "应识别出高波负收益拖累基金");

        var alphaItem = result.Items.First(x => x.FundCode == "0001");
        Assert.IsTrue(alphaItem.RiskAdjustedEfficiencyRatio > 1.25m, "Alpha 引擎效率比应大于 1.25");
        Assert.IsTrue(alphaItem.InstitutionalRole.Contains("核心超额引擎"));

        var dragItem = result.Items.First(x => x.FundCode == "0003");
        Assert.IsTrue(dragItem.InstitutionalRole.Contains("夏普拖累"));
        Assert.IsTrue(dragItem.MarginalSharpeContribution < 0m || dragItem.RiskAdjustedEfficiencyRatio < 0.75m);

        // 潜在夏普比率应大于当前夏普比率
        Assert.IsTrue(result.DragRemovalSharpePotential > result.PortfolioSharpeRatio);
    }

    [TestMethod]
    public void Test_ReverseStressTesting_AnalyticalMinimalShock_MatchesTargetLoss()
    {
        // 5 个宏观因子的敏感度 Beta 向量
        // [股票市场Beta, 利率Beta, 信用利差Beta, 汇率Beta, 大宗商品Beta]
        double[] betas = new double[] { 0.85, -0.40, -0.30, 0.15, 0.25 };

        // 因子波动率矩阵 (年化标准差)
        double[] factorVols = new double[] { 0.20, 0.05, 0.08, 0.06, 0.15 };
        int k = betas.Length;
        double[,] factorCov = new double[k, k];
        for (int i = 0; i < k; i++)
        {
            factorCov[i, i] = factorVols[i] * factorVols[i];
            for (int j = 0; j < k; j++)
            {
                if (i != j) factorCov[i, j] = 0.20 * factorVols[i] * factorVols[j]; // 弱正相关
            }
        }

        decimal targetLoss = -15.0m;
        var result = QuantCalculator.SolveReverseStressTest(betas, null, targetLoss, factorCov);

        Assert.IsNotNull(result);
        Assert.AreEqual(targetLoss, result.TargetLossThresholdPercent);
        Assert.IsTrue(result.MinimalShockNorm > 0m, "马氏冲击范数必须严格为正");
        Assert.AreEqual(k, result.FactorShocks.Count);

        // 验证解析解反向投影精确性: sum(b_i * f^*_i) 必须精确等于 targetLoss (-15%)
        double totalSimulatedLoss = 0.0;
        for (int i = 0; i < k; i++)
        {
            double b = betas[i];
            double f = (double)result.FactorShocks[i].RequiredShockPercent;
            totalSimulatedLoss += b * f;
        }

        Assert.AreEqual((double)targetLoss, totalSimulatedLoss, 0.05, "逆向冲击所引爆的组合损失必须与目标阈值严格吻合");

        // 验证组合最脆弱因子输出
        Assert.IsFalse(string.IsNullOrEmpty(result.MostFragileFactor));
        Assert.IsTrue(result.FactorShocks.Any(f => f.LossContributionFraction > 0m));
        Assert.IsTrue(!string.IsNullOrEmpty(result.RecommendedHedgingAction));
    }

    [TestMethod]
    public void Test_KupiecVaRBacktest_LikelihoodRatioAndTrafficLight()
    {
        // 构造 300 天真实日净值序列
        var navList = GenerateNavSeries(DateTime.Today.AddDays(-300), 300, 0.0003, 0.015);

        // 运行 95% 置信度、250 日滚动 Kupiec POF 回测
        var result = QuantCalculator.PerformKupiecVaRBacktest(navList, 0.95, 250);

        Assert.IsNotNull(result);
        Assert.AreEqual(0.95m, result.ConfidenceLevel);
        Assert.AreEqual(5.0m, result.ExpectedFailureRate);
        Assert.IsTrue(result.TotalObservations >= 100);
        Assert.IsTrue(result.HistoricalExceptions >= 0);
        Assert.IsTrue(result.HistoricalFailureRate >= 0m);
        Assert.IsTrue(result.HistoricalLikelihoodRatio >= 0m);
        Assert.IsTrue(result.HistoricalPValue >= 0m && result.HistoricalPValue <= 1.0m);

        // 巴塞尔交通灯必须属于 Green / Yellow / Red 之一
        Assert.IsTrue(result.HistoricalTrafficLight is "Green" or "Yellow" or "Red");
        Assert.IsTrue(result.HistoricalTrafficLightBadge.Length > 0);
    }

    [TestMethod]
    public void Test_PortfolioEngine_CalculatePortfolio_Phase19_EndToEndIntegration()
    {
        // 构造具备完整净值时序的多资产组合
        var fundA = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘A",
            Type = "偏股混合型",
            NavHistory = GenerateNavSeries(DateTime.Today.AddDays(-100), 100, 0.0005, 0.015)
        };
        var fundB = new FundDetail
        {
            Code = "001001",
            Name = "华夏纯债固收A",
            Type = "纯债固收型",
            NavHistory = GenerateNavSeries(DateTime.Today.AddDays(-100), 100, 0.00015, 0.003)
        };

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (fundA, 60.0m),
            (fundB, 40.0m)
        };

        var result = PortfolioEngine.CalculatePortfolio(components, riskFreeRate: 2.0m, timeRange: "ALL");

        Assert.IsNotNull(result);

        // 验证 Phase 19 各项全生命周期诊断输出均已自动化挂载
        Assert.IsNotNull(result.SaaTaaMonitoring, "SaaTaaMonitoring 应已自动实例化并计算");
        Assert.IsTrue(result.SaaTaaMonitoring.AssetClassDeviations.Count > 0);

        Assert.IsNotNull(result.SharpeDecomposition, "SharpeDecomposition 应已完成微分分解");
        Assert.AreEqual(2, result.SharpeDecomposition.Items.Count);

        Assert.IsNotNull(result.ReverseStressTest, "ReverseStressTest 反向压力测试应已求解");
        Assert.IsTrue(result.ReverseStressTest.FactorShocks.Count > 0);

        Assert.IsNotNull(result.KupiecVaRTest, "KupiecVaRTest 后验检验应已执行");
        Assert.IsTrue(result.KupiecVaRTest.TotalObservations > 0);
    }
}
