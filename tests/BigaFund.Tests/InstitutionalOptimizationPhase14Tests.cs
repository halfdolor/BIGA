using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase14Tests
{
    [TestMethod]
    public void TestStoredFundItem_MultiFactorCrossSectionalRanking_Enrichment()
    {
        // 构造包含 8 只不同风格的本地投研池 StoredFundItem
        var storedFunds = new List<StoredFundItem>
        {
            new StoredFundItem { Code = "000001", Name = "华夏成长混合", Type = "混合型-偏股", Return1Y = 28.5m, SharpeRatio = 1.45m, SortinoRatio = 1.62m, MaxDrawdown = 14.2m, QuantScore = 88.0m, CaptureSpread = 15.2m },
            new StoredFundItem { Code = "005827", Name = "易方达蓝筹精选", Type = "混合型-偏股", Return1Y = 12.0m, SharpeRatio = 0.85m, SortinoRatio = 0.92m, MaxDrawdown = 22.5m, QuantScore = 72.0m, CaptureSpread = 5.0m },
            new StoredFundItem { Code = "161725", Name = "招商中证白酒指数", Type = "指数型-股票", Return1Y = -8.2m, SharpeRatio = 0.12m, SortinoRatio = 0.15m, MaxDrawdown = 34.0m, QuantScore = 55.0m, CaptureSpread = -12.0m },
            new StoredFundItem { Code = "000171", Name = "易方达裕丰回报债券", Type = "债券型-混合债", Return1Y = 5.6m, SharpeRatio = 2.10m, SortinoRatio = 2.45m, MaxDrawdown = 2.8m, QuantScore = 82.0m, CaptureSpread = 8.5m },
            new StoredFundItem { Code = "161005", Name = "富国天惠成长混合", Type = "混合型-偏股", Return1Y = 19.8m, SharpeRatio = 1.15m, SortinoRatio = 1.28m, MaxDrawdown = 18.0m, QuantScore = 78.5m, CaptureSpread = 9.8m },
            new StoredFundItem { Code = "510300", Name = "华泰柏瑞沪深300ETF", Type = "指数型-股票", Return1Y = 14.2m, SharpeRatio = 0.95m, SortinoRatio = 1.05m, MaxDrawdown = 16.5m, QuantScore = 70.0m, CaptureSpread = 2.0m },
            new StoredFundItem { Code = "510500", Name = "南方中证500ETF", Type = "指数型-股票", Return1Y = 22.4m, SharpeRatio = 1.25m, SortinoRatio = 1.40m, MaxDrawdown = 19.5m, QuantScore = 79.0m, CaptureSpread = 11.0m },
            new StoredFundItem { Code = "159915", Name = "易方达创业板ETF", Type = "指数型-股票", Return1Y = 32.0m, SharpeRatio = 1.30m, SortinoRatio = 1.42m, MaxDrawdown = 26.0m, QuantScore = 80.5m, CaptureSpread = 16.5m },
        };

        var rankingResult = QuantCalculator.CalculateMultiFactorCrossSectionalRanking(storedFunds);

        Assert.IsNotNull(rankingResult);
        Assert.AreEqual(8, rankingResult.TotalAnalyzedCount);
        Assert.AreEqual(8, rankingResult.AllScoredFunds.Count);

        // 验证每只基金的横截面标准化打分已成功回填至 StoredFundItem 实体对象
        foreach (var f in storedFunds)
        {
            Assert.IsTrue(f.MultiFactorCompositeScore > 0m, $"{f.Code} 综合总分应大于 0");
            Assert.IsTrue(f.MultiFactorPercentile > 0m && f.MultiFactorPercentile <= 100m, $"{f.Code} 分位数排名应在 0~100 之间");
            Assert.IsFalse(string.IsNullOrEmpty(f.MultiFactorRatingTag), $"{f.Code} 机构评级标签不应为空");
            Assert.IsTrue(f.MomentumScore > 0m, $"{f.Code} 动量得分应大于 0");
            Assert.IsTrue(f.RiskAdjustedScore > 0m, $"{f.Code} 风险性价比得分应大于 0");
            Assert.IsTrue(f.DownsideDefenseScore > 0m, $"{f.Code} 尾部防御得分应大于 0");
            Assert.IsTrue(f.AlphaPurityScore > 0m, $"{f.Code} 纯Alpha得分应大于 0");
            Assert.IsTrue(f.ConvexityScore > 0m, $"{f.Code} 凸性得分应大于 0");
        }

        // 验证第一名的打分高于末尾基金
        var topFund = rankingResult.TopCoreFunds.First();
        var bottomFund = rankingResult.AllScoredFunds.Last();
        Assert.IsTrue(topFund.CompositeScore >= bottomFund.CompositeScore);
        Assert.IsTrue(topFund.PercentileRank >= bottomFund.PercentileRank);
    }

    [TestMethod]
    public void TestBarraAttribution_StrictConservationAndDominantDriver()
    {
        // 构造包含科技/成长和稳健防守的组合组件
        var compGrowth = new FundDetail
        {
            Code = "005827",
            Name = "易方达蓝筹精选",
            Type = "混合型-偏股",
            QuantMetrics = new QuantMetrics
            {
                Beta = 1.25m,
                AnnualizedReturn = 18.5m,
                AnnualizedVolatility = 22.0m,
                Alpha = 4.5m
            }
        };

        var compBond = new FundDetail
        {
            Code = "000171",
            Name = "易方达裕丰回报债券",
            Type = "债券型-混合债",
            QuantMetrics = new QuantMetrics
            {
                Beta = 0.20m,
                AnnualizedReturn = 6.5m,
                AnnualizedVolatility = 3.5m,
                Alpha = 2.0m
            }
        };

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (compGrowth, 70m),
            (compBond, 30m)
        };

        decimal portfolioReturn = 14.90m; // 组合总收益
        decimal benchmarkReturn = 8.20m;  // 基准总收益
        decimal activeReturn = portfolioReturn - benchmarkReturn; // 6.70%

        var attribution = PortfolioEngine.CalculateBarraFactorReturnAttribution(components, portfolioReturn, benchmarkReturn);

        Assert.IsNotNull(attribution);
        Assert.AreEqual(activeReturn, attribution.TotalActiveReturn);
        Assert.AreEqual(6, attribution.FactorContributions.Count);

        // 核心机构级数学公理验证：主动超额严格守恒 = 风格贡献 + 特质选股 Alpha (零残差)
        Assert.IsTrue(attribution.IsStrictlyConserved, "Barra主动超额分解必须零残差守恒");
        Assert.AreEqual(0.000m, attribution.ResidualGap, 0.001m);
        Assert.AreEqual(attribution.TotalActiveReturn, attribution.TotalStyleFactorReturn + attribution.SpecificAlphaReturn, 0.001m);

        // 验证风格因子代码全面覆盖
        var factorCodes = attribution.FactorContributions.Select(f => f.FactorCode).ToList();
        CollectionAssert.Contains(factorCodes, "BETA");
        CollectionAssert.Contains(factorCodes, "MOMENTUM");
        CollectionAssert.Contains(factorCodes, "SIZE");
        CollectionAssert.Contains(factorCodes, "VALUE");
        CollectionAssert.Contains(factorCodes, "QUALITY");
        CollectionAssert.Contains(factorCodes, "VOLATILITY");

        Assert.IsFalse(string.IsNullOrEmpty(attribution.DominantDriver));
        Assert.IsFalse(string.IsNullOrEmpty(attribution.AttributionSummary));
    }

    [TestMethod]
    public void TestLiquidityHorizon_AlmgrenChrissModel_AumScalability()
    {
        // 构造两个组合成分：一只大盘基金 (规模 120 亿)，一只小盘精选 (规模 8 亿)
        var compLarge = new FundDetail
        {
            Code = "510300",
            Name = "华泰柏瑞沪深300ETF",
            FundSize = "120.5亿元",
            QuantMetrics = new QuantMetrics { AnnualizedVolatility = 18.0m }
        };

        var compSmall = new FundDetail
        {
            Code = "009999",
            Name = "小盘量化增强",
            FundSize = "8.0亿元",
            QuantMetrics = new QuantMetrics { AnnualizedVolatility = 28.0m }
        };

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (compLarge, 60m),
            (compSmall, 40m)
        };

        // 1. 中等规模模拟 (3 亿元 AUM，10% 参与率)
        var horizon300M = PortfolioEngine.CalculatePortfolioLiquidityHorizon(components, portfolioAumMln: 300m, maxDailyParticipationPct: 10.0m);
        Assert.IsNotNull(horizon300M);
        Assert.AreEqual(2, horizon300M.Items.Count);
        Assert.IsTrue(horizon300M.DaysToLiquidateTotal >= 1);
        Assert.IsTrue(horizon300M.TotalEstimatedImpactCostPct > 0m);
        Assert.IsTrue(horizon300M.TotalEstimatedImpactLossMln > 0m);
        Assert.IsTrue(horizon300M.TPlus1LiquidPct > 0m);

        // 2. 超大规模机构专户模拟 (30 亿元 AUM，5% 严格参与率)
        var horizon3000M = PortfolioEngine.CalculatePortfolioLiquidityHorizon(components, portfolioAumMln: 3000m, maxDailyParticipationPct: 5.0m);
        Assert.IsNotNull(horizon3000M);

        // 机构级流动性公理：组合管理规模越大或单日参与率越严，清算天数越长且 Almgren-Chriss 暂态冲击滑点成本越高
        Assert.IsTrue(horizon3000M.DaysToLiquidateTotal >= horizon300M.DaysToLiquidateTotal);
        Assert.IsTrue(horizon3000M.TotalEstimatedImpactCostPct >= horizon300M.TotalEstimatedImpactCostPct);
        Assert.IsTrue(horizon3000M.TotalEstimatedImpactLossMln > horizon300M.TotalEstimatedImpactLossMln);
        Assert.IsFalse(string.IsNullOrEmpty(horizon3000M.LiquidityGrade));
        Assert.IsFalse(string.IsNullOrEmpty(horizon3000M.ExecutiveSummary));
    }

    [TestMethod]
    public void TestExportExecutivePitchDeck_HtmlReportGeneratesCompleteSections()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"BIGA_Test_PitchDeck_{Guid.NewGuid():N}.html");
        try
        {
            var fund = new FundDetail
            {
                Code = "005827",
                Name = "易方达蓝筹精选混合",
                Type = "混合型-偏股",
                ManagerName = "张坤",
                FundSize = "450亿元"
            };

            var metrics = new QuantMetrics
            {
                AnnualizedReturn = 16.8m,
                AnnualizedVolatility = 21.5m,
                MaxDrawdown = 18.6m,
                SharpeRatio = 0.95m,
                SortinoRatio = 1.15m,
                CalmarRatio = 0.90m,
                Alpha = 4.8m,
                Beta = 1.12m
            };

            var portfolio = new PortfolioResult
            {
                TotalReturn = 24.5m,
                AnnualizedReturn = 12.8m,
                MaxDrawdown = 14.2m,
                SharpeRatio = 1.35m,
                AnnualizedVolatility = 11.5m,
                BarraReturnAttribution = new BarraFactorReturnAttributionResult
                {
                    TotalPortfolioReturn = 18.5m,
                    TotalBenchmarkReturn = 10.0m,
                    TotalStyleFactorReturn = 5.2m,
                    SpecificAlphaReturn = 3.3m,
                    DominantDriver = "风格轮动暴露",
                    AttributionSummary = "由 Beta 与动量因子主导"
                },
                LiquidityHorizon = new PortfolioLiquidityHorizonResult
                {
                    PortfolioAumMln = 500m,
                    DaysToLiquidateTotal = 4,
                    WeightedDaysToLiquidate = 2.1m,
                    TotalEstimatedImpactCostPct = 0.085m,
                    LiquidityGrade = "💎 AAA (极佳流动性)",
                    ExecutiveSummary = "可在 4 个交易日内完全变现"
                }
            };

            ExportService.ExportExecutivePitchDeckToHtml(tempFile, fund, metrics, portfolio);

            Assert.IsTrue(File.Exists(tempFile), "研报 HTML 文件应成功创建");
            string htmlContent = File.ReadAllText(tempFile);

            // 验证高管路演画册所有核心模块
            StringAssert.Contains(htmlContent, "Executive Pitch Deck");
            StringAssert.Contains(htmlContent, "投资决策委员会");
            StringAssert.Contains(htmlContent, "005827");
            StringAssert.Contains(htmlContent, "易方达蓝筹精选混合");
            StringAssert.Contains(htmlContent, "Barra 风格多因子超额收益归因");
            StringAssert.Contains(htmlContent, "六大风格因子累计贡献");
            StringAssert.Contains(htmlContent, "纯特质选股超额阿尔法 (Specific Alpha)");
            StringAssert.Contains(htmlContent, "组合流动性地平线与 Almgren-Chriss 冲击滑点测算");
            StringAssert.Contains(htmlContent, "极佳流动性");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
