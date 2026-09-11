using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase25Tests
{
    private static List<NavRecord> GenerateSyntheticNav(DateTime start, int days, double annualRet, double annualVol, int seed = 42)
    {
        var list = new List<NavRecord>();
        double nav = 1.0;
        double dailyMean = annualRet / 252.0;
        double dailyVol = annualVol / Math.Sqrt(252.0);
        var rng = new Random(seed);

        for (int i = 0; i < days; i++)
        {
            list.Add(new NavRecord
            {
                Date = start.AddDays(i),
                UnitNav = (decimal)nav,
                CumulativeNav = (decimal)nav,
                DailyReturn = 0m
            });

            double u1 = Math.Max(1e-8, rng.NextDouble());
            double u2 = Math.Max(1e-8, rng.NextDouble());
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            double ret = dailyMean + dailyVol * z;
            nav *= (1.0 + ret);
        }

        // 填充 DailyReturn
        for (int i = 1; i < list.Count; i++)
        {
            list[i].DailyReturn = Math.Round((list[i].UnitNav - list[i - 1].UnitNav) / list[i - 1].UnitNav * 100m, 4);
        }

        return list;
    }

    private static List<(FundDetail Fund, decimal Weight)> CreateSampleComponents()
    {
        var baseDate = new DateTime(2023, 1, 1);
        var fundA = new FundDetail { Code = "110011", Name = "易方达中小盘混合 (偏股型)", NavHistory = GenerateSyntheticNav(baseDate, 300, 0.16, 0.22, seed: 101) };
        var fundB = new FundDetail { Code = "510300", Name = "华泰柏瑞沪深300ETF (宽基指数)", NavHistory = GenerateSyntheticNav(baseDate, 300, 0.09, 0.17, seed: 202) };
        var fundC = new FundDetail { Code = "000001", Name = "华夏纯债中短债A (固定收益)", NavHistory = GenerateSyntheticNav(baseDate, 300, 0.045, 0.035, seed: 303) };
        var fundD = new FundDetail { Code = "000509", Name = "易方达现金增利货币A (流动性货币)", NavHistory = GenerateSyntheticNav(baseDate, 300, 0.022, 0.005, seed: 404) };

        return new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 35.0m),
            (fundB, 25.0m),
            (fundC, 25.0m),
            (fundD, 15.0m)
        };
    }

    [TestMethod]
    public void Test_CalculateCopulaTailDependence_KendallTauAndClaytonGumbel()
    {
        var components = CreateSampleComponents();
        var result = QuantCalculator.CalculateCopulaTailDependence(components);

        Assert.IsNotNull(result, "Copula result should not be null.");
        Assert.IsTrue(result.PairwiseCopulaList.Count > 0, "Pairwise Copula list should have items for 4 components (C(4,2) = 6 pairs).");
        Assert.AreEqual(6, result.PairwiseCopulaList.Count, "4 components should produce 6 pairs.");

        foreach (var pair in result.PairwiseCopulaList)
        {
            // 检验相关系数与秩相关
            Assert.IsTrue(pair.PearsonCorrelation >= -1.0m && pair.PearsonCorrelation <= 1.0m, "Pearson correlation must be in [-1, 1].");
            Assert.IsTrue(pair.KendallTau >= -1.0m && pair.KendallTau <= 1.0m, "Kendall tau must be in [-1, 1].");

            // 检验尾部依赖度 [0, 1]
            Assert.IsTrue(pair.LowerTailDependenceLambda >= 0m && pair.LowerTailDependenceLambda <= 1.0m, "Lower tail lambda must be in [0, 1].");
            Assert.IsTrue(pair.UpperTailDependenceLambda >= 0m && pair.UpperTailDependenceLambda <= 1.0m, "Upper tail lambda must be in [0, 1].");

            // 检验风控定级徽章
            Assert.IsFalse(string.IsNullOrWhiteSpace(pair.TailRiskBadge), "Tail risk badge should not be empty.");
        }

        // 检验组合层级加权尾部依赖与踩踏放大倍数
        Assert.IsTrue(result.PortfolioWeightedLowerTailDependence >= 0m, "Weighted lower tail lambda must be non-negative.");
        Assert.IsTrue(result.PortfolioWeightedUpperTailDependence >= 0m, "Weighted upper tail lambda must be non-negative.");
        Assert.IsTrue(result.SystemicCrashAmplificationFactor >= 1.0m, "Systemic crash amplification factor must be >= 1.0.");
        Assert.IsTrue(result.LinearVsCopulaVaRDifferencePercent >= 0m, "Copula VaR underestimation diff must be non-negative.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveTailDiagnosis), "Executive tail diagnosis should not be empty.");
    }

    [TestMethod]
    public void Test_SolveParetoMultiObjectiveFrontier_NSGA2()
    {
        var components = CreateSampleComponents();
        var result = QuantCalculator.SolveParetoMultiObjectiveFrontier(components, riskFreeRatePercent: 2.0m, populationSize: 60);

        Assert.IsNotNull(result, "Pareto result should not be null.");
        Assert.IsTrue(result.FrontierSolutions.Count > 0, "Frontier should contain non-dominated Pareto solutions.");

        // 验证各方案目标值合法性
        foreach (var sol in result.FrontierSolutions)
        {
            Assert.IsTrue(sol.Weights.Count == components.Count, "Each solution must have weights matching component count.");
            Assert.AreEqual(100.0m, sol.Weights.Values.Sum(), 1.0m, "Weights of solution must sum to 100%.");
            Assert.IsTrue(sol.ExpectedReturnPercent > -50m && sol.ExpectedReturnPercent < 100m, "Expected return must be in reasonable range.");
            Assert.IsTrue(sol.DownsideCvar95Percent >= 0m, "Downside CVaR must be non-negative.");
            Assert.IsTrue(sol.RebalanceTurnoverPercent >= 0m && sol.RebalanceTurnoverPercent <= 200m, "Turnover must be in [0%, 200%].");
        }

        // 验证超体积指标与膝点拐点妥协解
        Assert.IsTrue(result.HypervolumeIndicator >= 0m && result.HypervolumeIndicator <= 1.0m, "Hypervolume indicator must be in [0, 1].");
        Assert.IsNotNull(result.OptimalCompromiseSolution, "Knee point compromise solution must be identified.");
        Assert.IsTrue(result.OptimalCompromiseSolution.IsRecommended, "Knee point solution must be flagged as recommended.");

        // 验证保守与激进解
        Assert.IsNotNull(result.ConservativeSolution, "Conservative solution must exist.");
        Assert.IsNotNull(result.AggressiveSolution, "Aggressive solution must exist.");
        Assert.IsTrue(result.AggressiveSolution.ExpectedReturnPercent >= result.ConservativeSolution.ExpectedReturnPercent,
            "Aggressive solution expected return must be >= conservative solution return.");

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveParetoAdvice), "Executive Pareto advice must not be empty.");
    }

    [TestMethod]
    public void Test_FitGarch11VolatilityForecast_VarianceTargetingAndCone()
    {
        // 构造带有波动聚类特性的日收益率序列 (例如 250 天)
        var rng = new Random(123);
        var returns = new List<decimal>();
        double currentVol = 0.015;
        for (int i = 0; i < 250; i++)
        {
            // 简易 GARCH 波动模拟：高波动后伴随高波动
            double shock = (rng.NextDouble() - 0.5) * 2.0;
            currentVol = Math.Sqrt(0.00002 + 0.12 * Math.Pow(shock * currentVol, 2) + 0.85 * currentVol * currentVol);
            returns.Add((decimal)(shock * currentVol));
        }

        var result = QuantCalculator.FitGarch11VolatilityForecast(returns, forecastDays: 60);

        Assert.IsNotNull(result, "GARCH forecast result should not be null.");
        Assert.IsTrue(result.Alpha >= 0m, "Alpha must be non-negative.");
        Assert.IsTrue(result.Beta >= 0m, "Beta must be non-negative.");
        Assert.IsTrue(result.Persistence < 1.0m, "Persistence (alpha + beta) must be strictly < 1 for stationarity.");
        Assert.IsTrue(result.HalfLifeDays > 0m, "Half-life must be positive.");
        Assert.IsTrue(result.CurrentConditionalVolPercent > 0m, "Current conditional volatility must be positive.");
        Assert.IsTrue(result.LongTermUnconditionalVolPercent > 0m, "Long term volatility must be positive.");

        // 验证波动锥多期限结构 (5, 10, 20, 40, 60, 120, 252 天)
        Assert.IsTrue(result.ForecastPoints.Count >= 5, "Should have multi-horizon forecast points.");
        foreach (var pt in result.ForecastPoints)
        {
            Assert.IsTrue(pt.HorizonDays > 0);
            Assert.IsTrue(pt.ForecastAnnualizedVolPercent > 0m);

            // 验证波动锥分位数单调递增性
            Assert.IsTrue(pt.VolCone10Percentile <= pt.VolCone25Percentile, "Cone 10% <= Cone 25%");
            Assert.IsTrue(pt.VolCone25Percentile <= pt.VolCone50Median, "Cone 25% <= Cone 50%");
            Assert.IsTrue(pt.VolCone50Median <= pt.VolCone75Percentile, "Cone 50% <= Cone 75%");
            Assert.IsTrue(pt.VolCone75Percentile <= pt.VolCone90Percentile, "Cone 75% <= Cone 90%");
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.VolClusteringRegime), "Vol clustering regime must be assigned.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TacticalRiskBudgetAdvice), "Tactical risk budget advice must not be empty.");
    }

    [TestMethod]
    public void Test_PortfolioEngine_CalculatePortfolio_Phase25Integrated()
    {
        var components = CreateSampleComponents();
        var inputComponents = components.Select(c => (c.Fund, c.Weight)).ToList();

        var result = PortfolioEngine.CalculatePortfolio(inputComponents, riskFreeRate: 2.0m);

        Assert.IsNotNull(result, "PortfolioResult should not be null.");

        // 验证 Phase 25 挂载的三大引擎输出均已成功集成
        Assert.IsNotNull(result.CopulaTailDependence, "CopulaTailDependence should be populated in PortfolioResult.");
        Assert.IsTrue(result.CopulaTailDependence.PairwiseCopulaList.Count > 0);

        Assert.IsNotNull(result.ParetoMultiObjective, "ParetoMultiObjective should be populated in PortfolioResult.");
        Assert.IsTrue(result.ParetoMultiObjective.FrontierSolutions.Count > 0);

        Assert.IsNotNull(result.GarchVolatilityForecast, "GarchVolatilityForecast should be populated in PortfolioResult.");
        Assert.IsTrue(result.GarchVolatilityForecast.ForecastPoints.Count > 0);
    }

    [TestMethod]
    public void Test_ExportService_GenerateInstitutionalDueDiligenceFactSheetHtml()
    {
        var components = CreateSampleComponents();
        var inputComponents = components.Select(c => (c.Fund, c.Weight)).ToList();
        var portfolioResult = PortfolioEngine.CalculatePortfolio(inputComponents, riskFreeRate: 2.0m);

        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(portfolioResult, inputComponents);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html), "Generated HTML fact sheet must not be empty.");
        Assert.IsTrue(html.Contains("机构级投资组合尽职调查与多资产配置研报"), "Must contain main title.");
        Assert.IsTrue(html.Contains("极值 Copula 尾部联结模型"), "Must contain Copula section.");
        Assert.IsTrue(html.Contains("多目标帕累托前沿自适应进化优化"), "Must contain Pareto section.");
        Assert.IsTrue(html.Contains("GARCH(1,1) 前瞻条件异方差预测"), "Must contain GARCH volatility cone section.");
        Assert.IsTrue(html.Contains("免责声明"), "Must contain professional disclaimers.");
    }
}
