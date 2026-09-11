using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase17Tests
{
    private static List<NavRecord> CreateMockNavSequence(int days = 250, decimal startNav = 1.0000m, decimal dailyDrift = 0.0005m, decimal dailyVol = 0.010m)
    {
        var list = new List<NavRecord>();
        var baseDate = new DateTime(2024, 1, 1);
        decimal nav = startNav;

        for (int i = 0; i < days; i++)
        {
            double cycle = Math.Sin(i * 0.12) * (double)dailyVol;
            double ret = (double)dailyDrift + cycle;
            nav = Math.Max(0.1m, nav * (1.0m + (decimal)ret));

            list.Add(new NavRecord
            {
                Date = baseDate.AddDays(i),
                UnitNav = Math.Round(nav, 4),
                CumulativeNav = Math.Round(nav, 4),
                DailyReturn = Math.Round((decimal)ret * 100m, 2)
            });
        }
        return list;
    }

    private static List<BenchmarkRecord> CreateMockBenchmarks(List<NavRecord> navs, decimal annualReturn = 0.08m)
    {
        var list = new List<BenchmarkRecord>();
        if (navs.Count == 0) return list;

        DateTime start = navs[0].Date;
        for (int i = 0; i < navs.Count; i++)
        {
            double tYears = (navs[i].Date - start).TotalDays / 365.25;
            double cumRate = (Math.Pow(1.0 + (double)annualReturn, tYears) - 1.0) * 100.0;
            list.Add(new BenchmarkRecord
            {
                Date = navs[i].Date,
                CumulativeReturnRate = (decimal)cumRate
            });
        }
        return list;
    }

    [TestMethod]
    public void TestProjectToBoxSimplex_StrictBoundariesAndSumToOne()
    {
        // 4 资产合规盒约束测试: L = [0.05, 0.10, 0.05, 0.20], U = [0.40, 0.45, 0.30, 0.50]
        double[] minBounds = [0.05, 0.10, 0.05, 0.20];
        double[] maxBounds = [0.40, 0.45, 0.30, 0.50];
        double[] rawVector = [0.85, -0.30, 0.40, 0.15];

        double[] projected = PortfolioEngine.ProjectToBoxSimplex(rawVector, minBounds, maxBounds);

        Assert.AreEqual(4, projected.Length, "输出权重维度应与输入一致");

        double sum = 0;
        for (int i = 0; i < projected.Length; i++)
        {
            sum += projected[i];
            Assert.IsTrue(projected[i] >= minBounds[i] - 1e-5, $"资产 {i} 权重 {projected[i]} 低于下界 {minBounds[i]}");
            Assert.IsTrue(projected[i] <= maxBounds[i] + 1e-5, $"资产 {i} 权重 {projected[i]} 高于上界 {maxBounds[i]}");
        }

        Assert.AreEqual(1.0, sum, 1e-5, "合规盒约束投影视权重总和必须严格等于 1.0");
    }

    [TestMethod]
    public void TestSolveConstrainedMaxSharpeAndMinVariance()
    {
        // 3 资产协方差与预期收益
        double[] expReturns = [0.18, 0.10, 0.06];
        double[,] cov = new double[,]
        {
            { 0.040, 0.008, 0.002 },
            { 0.008, 0.025, 0.004 },
            { 0.002, 0.004, 0.010 }
        };

        double[] minBounds = [0.10, 0.15, 0.10];
        double[] maxBounds = [0.50, 0.60, 0.40];

        // 1. 测试盒约束最大夏普求解器
        double[] msWeights = PortfolioEngine.SolveConstrainedMaxSharpe(cov, expReturns, 3, 0.02, minBounds, maxBounds);
        Assert.AreEqual(3, msWeights.Length);
        Assert.AreEqual(1.0, msWeights.Sum(), 1e-5, "最大夏普权重总和应为 1.0");
        for (int i = 0; i < msWeights.Length; i++)
        {
            Assert.IsTrue(msWeights[i] >= minBounds[i] - 1e-5, $"MS 资产 {i} 权重 {msWeights[i]} 低于下界");
            Assert.IsTrue(msWeights[i] <= maxBounds[i] + 1e-5, $"MS 资产 {i} 权重 {msWeights[i]} 高于上界");
        }

        // 2. 测试盒约束最小方差求解器
        double[] mvWeights = PortfolioEngine.SolveConstrainedMinVariance(cov, 3, minBounds, maxBounds);
        Assert.AreEqual(3, mvWeights.Length);
        Assert.AreEqual(1.0, mvWeights.Sum(), 1e-5, "最小方差权重总和应为 1.0");
        for (int i = 0; i < mvWeights.Length; i++)
        {
            Assert.IsTrue(mvWeights[i] >= minBounds[i] - 1e-5, $"MV 资产 {i} 权重 {mvWeights[i]} 低于下界");
            Assert.IsTrue(mvWeights[i] <= maxBounds[i] + 1e-5, $"MV 资产 {i} 权重 {mvWeights[i]} 高于上界");
        }
    }

    [TestMethod]
    public void TestRunPortfolioDynamicBacktest_CashAndFrictionCalculation()
    {
        var navs1 = CreateMockNavSequence(250, 1.0000m, 0.0006m, 0.012m);
        var navs2 = CreateMockNavSequence(250, 1.2000m, 0.0004m, 0.008m);

        var f1 = new FundDetail { Code = "001001", Name = "成长先锋", NavHistory = navs1 };
        var f2 = new FundDetail { Code = "001002", Name = "稳健红利", NavHistory = navs2 };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (f1, 0.60m),
            (f2, 0.40m)
        };

        var bm = CreateMockBenchmarks(navs1, 0.06m);

        var backtest = PortfolioEngine.RunPortfolioDynamicBacktest(
            components,
            PortfolioRebalanceMode.Monthly,
            initialCapital: 1000000m,
            subscriptionFeeRate: 0.0012m,
            redemptionFeeRate: 0.0050m,
            driftThreshold: 0.05m,
            benchmarkRecords: bm);

        Assert.IsNotNull(backtest, "动态回测结果不应为空");
        Assert.IsTrue(backtest.TotalTradingDays >= 200, "交易日数量应超过 200");
        Assert.AreEqual(1000000m, backtest.InitialCapital, "初始本金应为 1,000,000");
        Assert.IsTrue(backtest.FinalCapital > 0, "期末净资产应大于 0");
        Assert.IsTrue(backtest.DailyEquityCurve.Count == backtest.TotalTradingDays, "每日资产净值曲线采样点数应等于交易日数");
        Assert.IsTrue(backtest.RebalanceHistory.Count > 0, "月度定期再平衡应产生调仓事件记录");
        Assert.IsTrue(backtest.TotalTransactionFees >= 0, "摩擦费用应非负");
        Assert.IsTrue(backtest.AnnualizedTurnoverRate >= 0, "换手率应非负");
        Assert.IsFalse(string.IsNullOrWhiteSpace(backtest.ExecutiveSummary), "应生成高管级投研诊断摘要");
    }

    [TestMethod]
    public void TestEvaluate4433Rule_InstitutionalSelectionCriteria()
    {
        var navs = CreateMockNavSequence(300, 1.0000m, 0.0010m, 0.010m);
        var fund = new FundDetail
        {
            Code = "002001",
            Name = "全天候优质成长",
            Type = "混合型",
            FundSize = "35.20 亿元",
            ManagerName = "资深经理",
            ManagerTenure = "5年120天",
            NavHistory = navs
        };

        var check = QuantCalculator.Evaluate4433Rule(fund);

        Assert.IsNotNull(check, "4433 严选规则结果不应为空");
        Assert.AreEqual("002001", check.FundCode);
        Assert.IsTrue(check.RankPercentile1Y >= 0 && check.RankPercentile1Y <= 100, "1年期百分位应在 0~100 之间");
        Assert.IsTrue(check.RankPercentile6M >= 0 && check.RankPercentile6M <= 100, "6个月百分位应在 0~100 之间");
        Assert.IsTrue(check.PassedCriteria.Count + check.FailedCriteria.Count == 4, "应核查 1Y, 2/3/5Y, 6M, 3M 四维核心标准");
        Assert.IsFalse(string.IsNullOrWhiteSpace(check.Conclusion), "应输出 4433 机构投研评语");
    }

    [TestMethod]
    public void TestCalculateAlphaPersistence_RollingWinRateAndDecay()
    {
        var navs = CreateMockNavSequence(350, 1.0000m, 0.0008m, 0.010m);
        var bm = CreateMockBenchmarks(navs, 0.04m);

        var fund = new FundDetail
        {
            Code = "003001",
            Name = "量化对冲精选",
            NavHistory = navs
        };

        var persistence = QuantCalculator.CalculateAlphaPersistence(fund, navs, bm);

        Assert.IsNotNull(persistence, "阿尔法持续性测算不应为空");
        Assert.AreEqual("003001", persistence.FundCode);
        Assert.IsTrue(persistence.RollingWinRate3M >= 0 && persistence.RollingWinRate3M <= 100, "3M 胜率应在 0~100%");
        Assert.IsTrue(persistence.RollingWinRate6M >= 0 && persistence.RollingWinRate6M <= 100, "6M 胜率应在 0~100%");
        Assert.IsTrue(persistence.RollingWinRate12M >= 0 && persistence.RollingWinRate12M <= 100, "12M 胜率应在 0~100%");
        Assert.IsTrue(persistence.PersistenceScore >= 0 && persistence.PersistenceScore <= 100, "持续性综合得分应在 0~100 分");
        Assert.IsFalse(string.IsNullOrWhiteSpace(persistence.PersistenceRating), "应具备阿尔法持续性评级 (如 Robust / Moderate)");
        Assert.IsFalse(string.IsNullOrWhiteSpace(persistence.InstitutionalVerdict), "应生成机构投研持续性裁定");
    }

    [TestMethod]
    public void TestAnalyzeFactorIcAndDecay_SpearmanAndDecayHorizons()
    {
        var navs = CreateMockNavSequence(300, 1.2000m, 0.0006m, 0.014m);
        var bm = CreateMockBenchmarks(navs, 0.05m);

        var fund = new FundDetail
        {
            Code = "004001",
            Name = "多因子增强",
            NavHistory = navs
        };

        var icResult = QuantCalculator.AnalyzeFactorIcAndDecay(fund, navs, bm);

        Assert.IsNotNull(icResult, "因子 IC 检验结果不应为空");
        Assert.IsTrue(icResult.Factors.Count >= 4, "应包含动量、低波、夏普、回撤等至少 4 类主流 Alpha 因子");

        foreach (var factor in icResult.Factors)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(factor.FactorName));
            Assert.IsTrue(factor.DecayCurve.Count == 4, "应包含 5D, 20D, 60D, 120D 四个预测时效窗口");
            Assert.IsTrue(factor.DirectionalConsistency >= 0 && factor.DirectionalConsistency <= 100, "方向一致性比例应在 0~100%");
            Assert.IsFalse(string.IsNullOrWhiteSpace(factor.PredictiveStrength), "应输出因子效力研判");
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(icResult.DominantAlphaFactor), "应明确主导 Alpha 因子画像");
        Assert.IsFalse(string.IsNullOrWhiteSpace(icResult.InstitutionalRecommendation), "应生成因子时效投研建议");
    }
}
