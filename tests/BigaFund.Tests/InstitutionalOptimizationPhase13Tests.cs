using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase13Tests
{
    [TestMethod]
    public void TestMacroScenarioStress_StandardScenarios_CalculatesAccurately()
    {
        // 验证宏观情景联合冲击与传导推演
        var allocations = new List<FundAssetAllocation>
        {
            new FundAssetAllocation
            {
                ReportDate = "2024-12-31",
                StockRatio = 80.0m,
                BondRatio = 15.0m,
                CashRatio = 5.0m
            }
        };

        var result = QuantCalculator.EvaluateMacroScenarioStress(
            fundBeta: 1.10m,
            fundSizeMln: 100.0m,
            sector: "宽基核心",
            currentVaR99: 4.2m,
            allocations: allocations);

        Assert.IsNotNull(result);
        Assert.AreEqual(4, result.Items.Count);

        var stagflation = result.Items.FirstOrDefault(i => i.ScenarioId == "MACRO_STAGFLATION");
        Assert.IsNotNull(stagflation);
        Assert.IsTrue(stagflation.ExpectedReturnPct < 0, "滞胀情景下股债双杀预期收益应为负");
        Assert.IsTrue(stagflation.ExpectedLossAmountMln < 0);
        Assert.IsTrue(stagflation.ShockedVaR99Pct > 4.2m, "波动率冲击后 VaR 应放大");
        Assert.IsTrue(stagflation.DeltaVaR99Pct > 0m);

        var easing = result.Items.FirstOrDefault(i => i.ScenarioId == "MACRO_MONETARY_EASING");
        Assert.IsNotNull(easing);
        Assert.IsTrue(easing.ExpectedReturnPct > 0, "宽货币强刺激情景下预期收益应为正");

        Assert.IsTrue(result.WorstCaseDrawdownPct <= result.AverageShockLossPct);
        Assert.IsTrue(result.ComprehensiveResilienceScore >= 0m && result.ComprehensiveResilienceScore <= 100m);
        Assert.IsFalse(string.IsNullOrEmpty(result.ResilienceGrade));
        Assert.IsFalse(string.IsNullOrEmpty(result.ExecutiveSummary));
    }

    [TestMethod]
    public void TestMultiFactorCrossSectionalRanking_MADWinsorizationAndZScore()
    {
        // 构建 12 只不同风格基金构成的样本池
        var funds = new List<FundDetail>();
        var rnd = new Random(42);

        for (int i = 1; i <= 12; i++)
        {
            string name = i % 3 == 0 ? $"红利高股息稳健_{i}号" : (i % 3 == 1 ? $"半导体芯片科技成长_{i}号" : $"消费医药蓝筹价值_{i}号");
            var f = new FundDetail
            {
                Code = $"{100000 + i:D6}",
                Name = name,
                Type = "偏股混合型",
                FundSize = $"{rnd.Next(5, 50)}亿元"
            };

            // 构造差异化的净值序列
            var navs = new List<NavRecord>();
            decimal baseNav = 1.0m;
            decimal drift = (decimal)(rnd.NextDouble() * 0.001 - 0.0003);
            for (int d = 0; d < 120; d++)
            {
                baseNav *= (1.0m + drift + (decimal)(rnd.NextDouble() * 0.02 - 0.01));
                navs.Add(new NavRecord
                {
                    Date = new DateTime(2024, 1, 1).AddDays(d),
                    UnitNav = Math.Round(baseNav, 4),
                    CumulativeNav = Math.Round(baseNav, 4)
                });
            }
            f.NavHistory = navs;
            f.QuantMetrics = QuantCalculator.CalculateMetrics(navs, "全历程");

            funds.Add(f);
        }

        var rankingResult = QuantCalculator.CalculateMultiFactorCrossSectionalRanking(funds);

        Assert.IsNotNull(rankingResult);
        Assert.AreEqual(12, rankingResult.TotalAnalyzedCount);
        Assert.AreEqual(12, rankingResult.AllScoredFunds.Count);

        // 验证每只基金的打分区间合规性
        foreach (var item in rankingResult.AllScoredFunds)
        {
            Assert.IsTrue(item.MomentumScore >= 0m && item.MomentumScore <= 100m);
            Assert.IsTrue(item.RiskAdjustedScore >= 0m && item.RiskAdjustedScore <= 100m);
            Assert.IsTrue(item.DownsideDefenseScore >= 0m && item.DownsideDefenseScore <= 100m);
            Assert.IsTrue(item.AlphaPurityScore >= 0m && item.AlphaPurityScore <= 100m);
            Assert.IsTrue(item.ConvexityScore >= 0m && item.ConvexityScore <= 100m);
            Assert.IsTrue(item.CompositeScore >= 0m && item.CompositeScore <= 100m);
            Assert.IsTrue(item.PercentileRank >= 0m && item.PercentileRank <= 100m);
            Assert.IsFalse(string.IsNullOrEmpty(item.RatingTag));
            Assert.IsFalse(string.IsNullOrEmpty(item.DiagnosticComment));

            // 验证已正确回填到 FundDetail
            var fundMatch = funds.First(f => f.Code == item.FundCode);
            Assert.IsNotNull(fundMatch.MultiFactorScore);
            Assert.AreEqual(item.CompositeScore, fundMatch.MultiFactorScore.CompositeScore);
        }

        // 验证 Top 列表非空
        Assert.IsTrue(rankingResult.TopCoreFunds.Count > 0);
        Assert.IsTrue(rankingResult.TopAlphaFunds.Count > 0);
        Assert.IsTrue(rankingResult.TopDefensiveFunds.Count > 0);
        Assert.IsTrue(rankingResult.TopCoreFunds[0].CompositeScore >= rankingResult.TopCoreFunds[^1].CompositeScore);
    }

    [TestMethod]
    public void TestBarraFactorReturnAttribution_ExactConservationGuarantee()
    {
        // 构造测试组合
        var fund1 = new FundDetail { Code = "000001", Name = "大盘科技成长先锋", Type = "股票型" };
        var fund2 = new FundDetail { Code = "000002", Name = "红利高息价值精选", Type = "偏股混合型" };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fund1, 60.0m),
            (fund2, 40.0m)
        };

        decimal portReturn = 18.50m;
        decimal bmReturn = 6.20m;

        var attribution = PortfolioEngine.CalculateBarraFactorReturnAttribution(components, portReturn, bmReturn);

        Assert.IsNotNull(attribution);
        Assert.AreEqual(12.30m, attribution.TotalActiveReturn);
        Assert.AreEqual(6, attribution.FactorContributions.Count);

        // 核心数学守恒验证: 风格因子收益 + 选股特质阿尔法 = 总主动超额收益 (误差 < 0.001%)
        decimal sumCalculated = attribution.TotalStyleFactorReturn + attribution.SpecificAlphaReturn;
        decimal residual = Math.Abs(sumCalculated - attribution.TotalActiveReturn);
        Assert.IsTrue(residual < 0.001m, $"归因存在残差泄漏: 风格收益 {attribution.TotalStyleFactorReturn} + 特质Alpha {attribution.SpecificAlphaReturn} != 主动超额 {attribution.TotalActiveReturn}");
        Assert.AreEqual(residual, attribution.ResidualGap);

        Assert.IsFalse(string.IsNullOrEmpty(attribution.DominantDriver));
        Assert.IsFalse(string.IsNullOrEmpty(attribution.AttributionSummary));

        // 验证各因子的主动暴露与符号关系
        foreach (var factor in attribution.FactorContributions)
        {
            Assert.AreEqual(Math.Round(factor.PortfolioExposure - factor.BenchmarkExposure, 3), factor.ActiveExposure);
            Assert.AreEqual(Math.Round(factor.ActiveExposure * factor.FactorPremiumPct, 4), factor.ReturnContributionPct);
        }
    }

    [TestMethod]
    public void TestPortfolioLiquidityHorizon_AlmgrenChrissImpact()
    {
        var fund1 = new FundDetail { Code = "000001", Name = "易方达蓝筹精选", FundSize = "350.5亿元", Type = "混合型" };
        var fund2 = new FundDetail { Code = "000002", Name = "万家微盘精选", FundSize = "8.2亿元", Type = "混合型" };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fund1, 70.0m),
            (fund2, 30.0m)
        };

        var liquidity = PortfolioEngine.CalculatePortfolioLiquidityHorizon(
            components,
            portfolioAumMln: 300.0m, // 3 亿元专户
            maxDailyParticipationPct: 10.0m);

        Assert.IsNotNull(liquidity);
        Assert.AreEqual(2, liquidity.Items.Count);

        var largeFund = liquidity.Items.First(i => i.FundCode == "000001");
        var smallFund = liquidity.Items.First(i => i.FundCode == "000002");

        // 大盘基金规模大，单日变现容量高，天数应为 1
        Assert.AreEqual(1, largeFund.DaysToLiquidate);
        Assert.IsTrue(largeFund.ImpactCostPct < smallFund.ImpactCostPct, "大规模基金冲击成本率应显著低于小规模微盘基金");

        Assert.IsTrue(liquidity.DaysToLiquidateTotal >= 1);
        Assert.IsTrue(liquidity.WeightedDaysToLiquidate >= 1.0m);
        Assert.IsTrue(liquidity.TotalEstimatedImpactCostPct > 0m);
        Assert.IsTrue(liquidity.TotalEstimatedImpactLossMln > 0m);
        Assert.IsFalse(string.IsNullOrEmpty(liquidity.LiquidityGrade));
        Assert.IsFalse(string.IsNullOrEmpty(liquidity.ExecutiveSummary));
    }

    [TestMethod]
    public void TestCompositeBenchmarkSynthesis_AndActiveShareCalculation()
    {
        // 1. 验证动态复合业绩基准合成 (60% 沪深300 + 40% 中债综合指数)
        var dates = new[] { new DateTime(2024, 1, 2), new DateTime(2024, 1, 3), new DateTime(2024, 1, 4) };
        var csi300Navs = new List<BenchmarkRecord>
        {
            new BenchmarkRecord { Date = dates[0], CumulativeReturnRate = 1.0m },
            new BenchmarkRecord { Date = dates[1], CumulativeReturnRate = 2.0m },
            new BenchmarkRecord { Date = dates[2], CumulativeReturnRate = -1.0m }
        };
        var bondNavs = new List<BenchmarkRecord>
        {
            new BenchmarkRecord { Date = dates[0], CumulativeReturnRate = 0.1m },
            new BenchmarkRecord { Date = dates[1], CumulativeReturnRate = 0.2m },
            new BenchmarkRecord { Date = dates[2], CumulativeReturnRate = 0.3m }
        };

        var components = new List<CompositeBenchmarkComponent>
        {
            new CompositeBenchmarkComponent { IndexCode = "CSI300", IndexName = "沪深300", WeightPercent = 60.0m, HistoricalNavs = csi300Navs },
            new CompositeBenchmarkComponent { IndexCode = "CBA", IndexName = "中债综合", WeightPercent = 40.0m, HistoricalNavs = bondNavs }
        };

        var synthesizedNav = QuantCalculator.SynthesizeCompositeBenchmarkNav(components);
        Assert.AreEqual(3, synthesizedNav.Count);

        // Day 1: 1.0 * 0.6 + 0.1 * 0.4 = 0.64
        Assert.AreEqual(0.64m, synthesizedNav[0].CumulativeReturnRate);
        // Day 2: 2.0 * 0.6 + 0.2 * 0.4 = 1.28
        Assert.AreEqual(1.28m, synthesizedNav[1].CumulativeReturnRate);
        // Day 3: -1.0 * 0.6 + 0.3 * 0.4 = -0.48
        Assert.AreEqual(-0.48m, synthesizedNav[2].CumulativeReturnRate);

        // 2. 验证 Cremers-Petajisto 主动份额 (Active Share) 计算
        var pWeights = new List<(string, decimal)>
        {
            ("科技", 50.0m),
            ("消费", 30.0m),
            ("医药", 20.0m)
        };
        var bWeights = new List<(string, decimal)>
        {
            ("金融", 25.0m),
            ("消费", 20.0m),
            ("科技", 15.0m),
            ("周期", 20.0m),
            ("医药", 20.0m)
        };

        var asResult = QuantCalculator.CalculateActiveShare(pWeights, bWeights, trackingError: 5.5m);
        Assert.IsNotNull(asResult);
        // Deltas: 科技 |50-15|=35, 消费 |30-20|=10, 医药 |20-20|=0, 金融 |0-25|=25, 周期 |0-20|=20
        // Sum = 35 + 10 + 0 + 25 + 20 = 90
        // Active Share = 90 / 2 = 45%
        Assert.AreEqual(45.0m, asResult.ActiveSharePercent);
        Assert.AreEqual(55.0m, asResult.OverlapWeightPercent);
        Assert.IsTrue(asResult.ActiveShareRating.Contains("主动偏离") || asResult.ActiveShareRating.Contains("Tilt"));
        Assert.IsFalse(string.IsNullOrEmpty(asResult.ManagementStyleDiagnosis));
    }

    [TestMethod]
    public void TestExecutivePitchDeck_HtmlContainsPhase13Sections()
    {
        var fund = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘先锋",
            ManagerName = "张经理",
            Type = "偏股混合型",
            FundSize = "180.0亿元"
        };

        var navs = new List<NavRecord>();
        decimal nav = 1.0m;
        for (int i = 0; i < 90; i++)
        {
            nav *= 1.0015m;
            navs.Add(new NavRecord { Date = new DateTime(2024, 1, 1).AddDays(i), UnitNav = nav, CumulativeNav = nav });
        }
        fund.NavHistory = navs;
        fund.QuantMetrics = QuantCalculator.CalculateMetrics(navs, "全历程");

        // 构造带有 Phase 13 结果的 PortfolioResult
        var portfolio = new PortfolioResult
        {
            TotalReturn = 22.50m,
            BenchmarkReturn = 8.30m,
            QuantMetrics = fund.QuantMetrics
        };
        portfolio.MacroStressResult = QuantCalculator.EvaluateMacroScenarioStress(fundBeta: 1.05m);
        portfolio.BarraReturnAttribution = PortfolioEngine.CalculateBarraFactorReturnAttribution(
            new List<(FundDetail, decimal)> { (fund, 100.0m) }, portfolio.TotalReturn, portfolio.BenchmarkReturn);
        portfolio.LiquidityHorizon = PortfolioEngine.CalculatePortfolioLiquidityHorizon(
            new List<(FundDetail, decimal)> { (fund, 100.0m) }, portfolioAumMln: 250.0m);

        string html = ExportService.GenerateExecutivePitchDeckHtml(fund, fund.QuantMetrics, portfolio);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("前瞻性多维宏观情景联合冲击压力测试"), "应包含宏观情景联合压力测试模块");
        Assert.IsTrue(html.Contains("组合流动性地平线与 Almgren-Chriss 冲击滑点测算"), "应包含流动性地平线测算模块");
        Assert.IsTrue(html.Contains("Barra 风格多因子超额收益归因"), "应包含 Barra 风格多因子归因模块");
        Assert.IsTrue(html.Contains("纯特质选股超额阿尔法 (Specific Alpha)"), "应包含纯选股特质阿尔法行");
        Assert.IsTrue(html.Contains("六大风格因子累计贡献"), "应包含六大风格因子累计贡献行");
    }
}
