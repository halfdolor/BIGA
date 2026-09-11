using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using BigaFund.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase10Tests
{
    private (List<NavRecord> navs, List<BenchmarkRecord> bmks) CreateTimingFundAndBenchmark(int days = 260, bool addTimingSkill = false)
    {
        var navs = new List<NavRecord>();
        var bmks = new List<BenchmarkRecord>();
        var baseDate = new DateTime(2023, 1, 1);

        decimal currentNav = 1.0m;
        double cumBmkRet = 0.0;

        for (int i = 0; i < days; i++)
        {
            var date = baseDate.AddDays(i);
            // 构造基准收益率 (具有上行和下行震荡)
            double bmkRet = 0.012 * Math.Sin(i * 0.1) + (i % 7 == 0 ? -0.015 : 0.003);
            cumBmkRet += bmkRet * 100.0;

            // 若具有择时能力: 当基准大幅变动时提供二次凸性或看跌保护
            double timingBoost = 0.0;
            if (addTimingSkill)
            {
                timingBoost = 0.5 * (bmkRet * bmkRet) + (bmkRet < 0 ? -0.3 * bmkRet : 0);
            }

            double fundRet = 0.0003 + 0.85 * bmkRet + timingBoost;
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
    public void Test_QuantCalculator_CalculateTimingAndSelectionAbility_Execution()
    {
        var (navs, bmks) = CreateTimingFundAndBenchmark(260, addTimingSkill: true);

        var result = QuantCalculator.CalculateTimingAndSelectionAbility(navs, bmks);

        Assert.IsNotNull(result, "Timing ability result should not be null.");
        Assert.IsTrue(result.TmRSquared >= 0m && result.TmRSquared <= 100.0m, $"TM R2 should be in [0, 100], got {result.TmRSquared}");
        Assert.IsTrue(result.HmRSquared >= 0m && result.HmRSquared <= 100.0m, $"HM R2 should be in [0, 100], got {result.HmRSquared}");
        Assert.IsFalse(string.IsNullOrEmpty(result.TimingRatingBadge), "TimingRatingBadge should be populated.");
        Assert.IsFalse(string.IsNullOrEmpty(result.SelectionRating), "SelectionRating should be populated.");
        Assert.IsFalse(string.IsNullOrEmpty(result.DiagnosticSummary), "DiagnosticSummary should be populated.");

        // 验证置信度属性
        Assert.AreEqual(Math.Abs(result.TmGammaTStat) >= 1.96m, result.TmTimingSignificant);
        Assert.AreEqual(Math.Abs(result.HmBeta2TStat) >= 1.96m, result.HmTimingSignificant);
    }

    [TestMethod]
    public void Test_PortfolioEngine_SolveMeanCVaR_SimplexConvergence()
    {
        int n = 3;
        int t = 200;
        var rnd = new Random(42);
        var individualDailyReturns = new Dictionary<string, List<double>>();
        var codes = new List<string> { "000001", "110011", "161725" };

        individualDailyReturns["000001"] = new List<double>();
        individualDailyReturns["110011"] = new List<double>();
        individualDailyReturns["161725"] = new List<double>();

        for (int i = 0; i < t; i++)
        {
            individualDailyReturns["000001"].Add(rnd.NextDouble() * 0.04 - 0.018); // 较稳健
            individualDailyReturns["110011"].Add(rnd.NextDouble() * 0.06 - 0.035); // 高波动且有肥尾下行
            individualDailyReturns["161725"].Add(rnd.NextDouble() * 0.02 - 0.009); // 低波
        }

        double[] weights = PortfolioEngine.SolveMeanCVaR(individualDailyReturns, codes, n, beta: 0.95);

        Assert.IsNotNull(weights);
        Assert.AreEqual(n, weights.Length);

        // 验证权重大于等于 0 且和为 1
        double sum = weights.Sum();
        Assert.AreEqual(1.0, sum, 0.01, $"Weights must sum to 1.0, got {sum}");
        foreach (var w in weights)
        {
            Assert.IsTrue(w >= -1e-5, $"Weight must be non-negative, got {w}");
        }

        // CVaR 防御求解器对低尾部亏损资产应有较好配置
        Assert.IsTrue(weights[2] > weights[1], "Mean-CVaR solver should prefer lower tail-risk assets over fat-tailed drawdown assets.");
    }

    [TestMethod]
    public void Test_PortfolioEngine_SolveMaximumDiversification_SimplexConvergence()
    {
        int n = 3;
        // 构造正定协方差矩阵 (低相关性)
        double[,] cov = new double[3, 3]
        {
            { 0.15 * 0.15, 0.15 * 0.25 * 0.1, 0.15 * 0.10 * -0.2 },
            { 0.15 * 0.25 * 0.1, 0.25 * 0.25, 0.25 * 0.10 * 0.05 },
            { 0.15 * 0.10 * -0.2, 0.25 * 0.10 * 0.05, 0.10 * 0.10 }
        };

        double[] weights = PortfolioEngine.SolveMaximumDiversification(cov, n);

        Assert.IsNotNull(weights);
        Assert.AreEqual(n, weights.Length);

        double sum = weights.Sum();
        Assert.AreEqual(1.0, sum, 0.01, $"MDP weights must sum to 1.0, got {sum}");
        foreach (var w in weights)
        {
            Assert.IsTrue(w >= -1e-5, $"MDP weight must be non-negative, got {w}");
        }
    }

    [TestMethod]
    public void Test_PortfolioEngine_CalculateRiskBudgetDecomposition_EulerIdentity()
    {
        // 3 资产组合
        var fundA = new FundDetail { Code = "000001", Name = "华夏成长" };
        var fundB = new FundDetail { Code = "110011", Name = "易方达中小盘" };
        var fundC = new FundDetail { Code = "161725", Name = "招商白酒" };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 50m),
            (fundB, 30m),
            (fundC, 20m)
        };

        var individualReturns = new Dictionary<string, List<double>>();
        int days = 200;
        var rnd = new Random(123);
        individualReturns["000001"] = new List<double>();
        individualReturns["110011"] = new List<double>();
        individualReturns["161725"] = new List<double>();

        for (int i = 0; i < days; i++)
        {
            individualReturns["000001"].Add(rnd.NextDouble() * 0.02 - 0.01);
            individualReturns["110011"].Add(rnd.NextDouble() * 0.015 - 0.007);
            individualReturns["161725"].Add(rnd.NextDouble() * 0.04 - 0.02);
        }

        var result = PortfolioEngine.CalculateRiskBudgetDecomposition(components, individualReturns, 0.15);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Items.Count);
        Assert.IsTrue(result.TotalPortfolioVolatility > 0m, "Total portfolio volatility must be positive.");

        // 1. 验证 Euler 定理恒等式: 绝对风险贡献之和 = 组合总波动率 (sum(ACR) == sigma_p)
        decimal sumAcr = result.Items.Sum(it => it.AbsoluteContributionToRisk);
        Assert.AreEqual((double)result.TotalPortfolioVolatility, (double)sumAcr, 0.05, "Euler identity: sum(ACR) must equal TotalPortfolioVolatility.");

        // 2. 验证百分比风险贡献之和 = 100% (sum(PCR) == 100%)
        decimal sumPcr = result.Items.Sum(it => it.PercentageContributionToRisk);
        Assert.AreEqual(100.0, (double)sumPcr, 0.5, "Sum of PCR must equal 100%.");

        // 3. 验证主导风险资产
        Assert.IsFalse(string.IsNullOrEmpty(result.DominantRiskAsset));

        // 4. 验证基尼系数在 [0, 1] 区间
        Assert.IsTrue(result.RiskBudgetGini >= 0m && result.RiskBudgetGini <= 1.0m, $"Gini must be between 0 and 1, got {result.RiskBudgetGini}");
    }

    [TestMethod]
    public void Test_FundScreenerControl_CalculateCustomScore()
    {
        var fundA = new StoredFundItem
        {
            Code = "000001",
            Name = "高收益高波动基金",
            Return1Y = 35.0m,
            MaxDrawdown = 28.0m,
            SharpeRatio = 1.1m,
            QuantScore = 75.0m
        };

        var fundB = new StoredFundItem
        {
            Code = "000002",
            Name = "稳健低回撤固收加",
            Return1Y = 8.0m,
            MaxDrawdown = 4.5m,
            SharpeRatio = 1.8m,
            QuantScore = 82.0m
        };

        // 1. 收益优先配置 (收益权重 70%, 抗跌 10%, 夏普 10%, 底色 10%)
        decimal scoreRetA = FundScreenerControl.CalculateCustomScore(fundA, 70m, 10m, 10m, 10m);
        decimal scoreRetB = FundScreenerControl.CalculateCustomScore(fundB, 70m, 10m, 10m, 10m);
        Assert.IsTrue(scoreRetA > scoreRetB, $"Fund A should score higher under return-focused weighting: A={scoreRetA}, B={scoreRetB}");

        // 2. 抗跌稳健优先配置 (收益权重 10%, 抗跌 60%, 夏普 20%, 底色 10%)
        decimal scoreDdA = FundScreenerControl.CalculateCustomScore(fundA, 10m, 60m, 20m, 10m);
        decimal scoreDdB = FundScreenerControl.CalculateCustomScore(fundB, 10m, 60m, 20m, 10m);
        Assert.IsTrue(scoreDdB > scoreDdA, $"Fund B should score higher under defensive weighting: A={scoreDdA}, B={scoreDdB}");
    }

    [TestMethod]
    public void Test_ExportService_Phase10_CsvAndHtmlContentIntegration()
    {
        var (navs, bmks) = CreateTimingFundAndBenchmark(260, addTimingSkill: true);
        var timing = QuantCalculator.CalculateTimingAndSelectionAbility(navs, bmks);

        var metrics = new QuantMetrics
        {
            TotalReturn = 20.5m,
            AnnualizedReturn = 18.2m,
            AnnualizedVolatility = 15.4m,
            MaxDrawdown = 12.3m,
            SharpeRatio = 1.15m,
            TimingAbility = timing
        };

        var fund = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘混合",
            Type = "混合型"
        };

        var backtest = new BacktestResult
        {
            StrategyName = "定期定额定投策略",
            TotalInvested = 10000m,
            FinalAssetValue = 12500m,
            AnnualizedIrr = 12.5m
        };

        // 1. 测试基金 CSV 导出包含 TM/HM 字段
        string tempCsv = Path.Combine(Path.GetTempPath(), $"test_fund_phase10_{Guid.NewGuid():N}.csv");
        try
        {
            ExportService.ExportToCsv(tempCsv, fund, metrics, backtest, navs);
            string content = File.ReadAllText(tempCsv);
            Assert.IsTrue(content.Contains("Treynor-Mazuy"), "CSV export must contain Treynor-Mazuy header.");
            Assert.IsTrue(content.Contains("Henriksson-Merton"), "CSV export must contain Henriksson-Merton header.");
            Assert.IsTrue(content.Contains("择时系数"), "CSV export must contain timing coefficient row.");
        }
        finally
        {
            if (File.Exists(tempCsv)) File.Delete(tempCsv);
        }

        // 2. 测试基金 HTML 导出包含 TM/HM 诊断卡片
        string tempHtml = Path.Combine(Path.GetTempPath(), $"test_fund_phase10_{Guid.NewGuid():N}.html");
        try
        {
            ExportService.ExportToHtml(tempHtml, fund, metrics, backtest, navs);
            string content = File.ReadAllText(tempHtml);
            Assert.IsTrue(content.Contains("Treynor-Mazuy"), "HTML export must contain Treynor-Mazuy section.");
            Assert.IsTrue(content.Contains("Henriksson-Merton"), "HTML export must contain Henriksson-Merton section.");
            Assert.IsTrue(content.Contains("综合评级"), "HTML export must contain timing rating badge.");
        }
        finally
        {
            if (File.Exists(tempHtml)) File.Delete(tempHtml);
        }

        // 3. 测试组合导出包含 Euler 风险预算与 8 套方案
        var components = new List<PortfolioItem>
        {
            new() { Code = "000001", Name = "华夏成长", WeightPercent = 60m },
            new() { Code = "110011", Name = "易方达中小盘", WeightPercent = 40m }
        };

        var portfolioResult = new PortfolioResult
        {
            StartDate = new DateTime(2023, 1, 1),
            EndDate = new DateTime(2023, 12, 31),
            TradingDays = 240,
            TotalReturn = 15.0m,
            AnnualizedReturn = 14.5m,
            AnnualizedVolatility = 12.0m,
            MaxDrawdown = 8.5m,
            SharpeRatio = 1.05m,
            Schemes = new List<PortfolioOptimizationScheme>
            {
                new() { SchemeName = "最大夏普比率", ExpectedReturn = 16m, ExpectedVolatility = 11m, SharpeRatio = 1.25m },
                new() { SchemeName = "均值-CVaR (尾部极值防御)", ExpectedReturn = 13m, ExpectedVolatility = 9m, SharpeRatio = 1.22m },
                new() { SchemeName = "最大分散化组合 (MDP)", ExpectedReturn = 14m, ExpectedVolatility = 9.5m, SharpeRatio = 1.26m }
            },
            RiskDecomposition = new PortfolioRiskDecompositionResult
            {
                TotalPortfolioVolatility = 12.0m,
                DominantRiskAsset = "华夏成长",
                RiskBudgetGini = 0.25m,
                Items = new List<PortfolioRiskContributionItem>
                {
                    new() { FundCode = "000001", FundName = "华夏成长", CapitalWeight = 60m, MarginalContributionToRisk = 0.14m, AbsoluteContributionToRisk = 0.084m, PercentageContributionToRisk = 70m, RiskConcentrationRatio = 1.17m },
                    new() { FundCode = "110011", FundName = "易方达中小盘", CapitalWeight = 40m, MarginalContributionToRisk = 0.09m, AbsoluteContributionToRisk = 0.036m, PercentageContributionToRisk = 30m, RiskConcentrationRatio = 0.75m }
                }
            }
        };

        string tempPortHtml = Path.Combine(Path.GetTempPath(), $"test_port_phase10_{Guid.NewGuid():N}.html");
        try
        {
            ExportService.ExportPortfolioToHtml(tempPortHtml, components, portfolioResult);
            string content = File.ReadAllText(tempPortHtml);
            Assert.IsTrue(content.Contains("均值-CVaR"), "Portfolio HTML export must contain Mean-CVaR scheme.");
            Assert.IsTrue(content.Contains("最大分散化组合 (MDP)"), "Portfolio HTML export must contain MDP scheme.");
            Assert.IsTrue(content.Contains("Euler 风险预算穿透解构"), "Portfolio HTML export must contain Euler risk decomposition.");
            Assert.IsTrue(content.Contains("边际风险贡献 (MCR)"), "Portfolio HTML export must contain MCR table column.");
        }
        finally
        {
            if (File.Exists(tempPortHtml)) File.Delete(tempPortHtml);
        }
    }
}
