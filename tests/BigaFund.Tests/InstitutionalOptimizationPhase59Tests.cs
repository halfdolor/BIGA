using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase59Tests
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

        for (int i = 1; i < list.Count; i++)
        {
            list[i].DailyReturn = Math.Round((list[i].UnitNav - list[i - 1].UnitNav) / list[i - 1].UnitNav * 100m, 4);
        }

        return list;
    }

    private static List<(FundDetail Fund, decimal Weight)> CreateSampleComponents()
    {
        var baseDate = new DateTime(2023, 1, 1);
        var fundA = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘混合 (高贝塔进攻)",
            FundSize = "180.5亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.22, 0.25, seed: 111)
        };
        var fundB = new FundDetail
        {
            Code = "510300",
            Name = "华泰柏瑞沪深300ETF (核心宽基蓝筹)",
            FundSize = "850.0亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.12, 0.18, seed: 222)
        };
        var fundC = new FundDetail
        {
            Code = "000001",
            Name = "华夏成长混合 (均衡成长对冲)",
            FundSize = "95.2亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.15, 0.20, seed: 333)
        };
        var fundD = new FundDetail
        {
            Code = "511010",
            Name = "国债ETF (宏观无风险流动性压舱石)",
            FundSize = "320.0亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.04, 0.05, seed: 444)
        };

        return new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 30m),
            (fundB, 35m),
            (fundC, 20m),
            (fundD, 15m)
        };
    }

    private static List<decimal> CreateSampleDailyReturns()
    {
        var rng = new Random(888);
        var returns = new List<decimal>();
        for (int i = 0; i < 250; i++)
        {
            returns.Add((decimal)(rng.NextDouble() * 0.04 - 0.018));
        }
        return returns;
    }

    [TestMethod]
    public void Test_Phase59_KyleContinuousAuctionElasticity()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateKyleContinuousAuctionElasticity(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.KyleItems.Count);
        Assert.IsTrue(res.GlobalKyleLambdaBps > 0m, "Global Kyle's lambda should be positive");
        Assert.IsTrue(res.AveragePriceElasticity > 0m, "Average price elasticity should be positive");
        Assert.IsTrue(res.AveragePenetrationReductionPct > 0m && res.AveragePenetrationReductionPct <= 100m,
            "Average penetration reduction should be within (0, 100]");
        Assert.IsTrue(res.TotalKyleElasticityAlphaBps > 0m, "Total Kyle elasticity alpha should be positive");

        foreach (var item in res.KyleItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.KyleLambdaImpactBps > 0m);
            Assert.IsTrue(item.PriceElasticityCoefficient > 0m);
            Assert.IsTrue(item.InformationPenetrationDepthPct > 0m && item.InformationPenetrationDepthPct <= 100m);
            Assert.IsTrue(item.OptimalQuoteBufferDepth > 0m);
            Assert.IsTrue(item.KyleElasticityAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.KyleRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.KyleAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Citadel") || res.ExecutiveVerdict.Contains("Jump") || res.ExecutiveVerdict.Contains("Kyle") || res.ExecutiveVerdict.Contains("连续拍卖") || res.ExecutiveVerdict.Contains("弹性"),
            "Verdict should mention Citadel, Jump, Kyle, Continuous Auction, or Elasticity");
    }

    [TestMethod]
    public void Test_Phase59_WilsonRenormalizationPercolation()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateWilsonRenormalizationPercolation(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.WilsonItems.Count);
        Assert.IsTrue(res.GlobalPercolationProbabilityPc > 0m && res.GlobalPercolationProbabilityPc <= 1.0m,
            "Global percolation probability pc should be within (0, 1]");
        Assert.IsTrue(res.GiantConnectedComponentRatioPct > 0m && res.GiantConnectedComponentRatioPct <= 100m,
            "GCC ratio should be within (0, 100]");
        Assert.IsTrue(res.AveragePercolationSusceptibility > 0m, "Average percolation susceptibility should be positive");
        Assert.IsTrue(res.TotalPercolationDefenseAlphaBps > 0m, "Total percolation defense alpha should be positive");

        decimal totalWeight = 0m;
        foreach (var item in res.WilsonItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.WilsonRgFlowVelocity > 0m);
            Assert.IsTrue(item.GiantComponentAffiliationScore > 0m);
            Assert.IsTrue(item.PercolationCriticalSusceptibility > 0m);
            Assert.IsTrue(item.RgInvariantTargetWeight > 0m);
            Assert.IsTrue(item.PercolationDefenseAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.WilsonRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.WilsonAdvice));
            totalWeight += item.RgInvariantTargetWeight;
        }

        Assert.IsTrue(Math.Abs(totalWeight - 100m) < 1.0m, $"RG invariant target weights should sum close to 100%, actual: {totalWeight}");

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Renaissance") || res.ExecutiveVerdict.Contains("D.E. Shaw") || res.ExecutiveVerdict.Contains("Wilson") || res.ExecutiveVerdict.Contains("重整化") || res.ExecutiveVerdict.Contains("渗流"),
            "Verdict should mention Renaissance, D.E. Shaw, Wilson, Renormalization, or Percolation");
    }

    [TestMethod]
    public void Test_Phase59_PredatoryGameLiquidityEvasion()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculatePredatoryGameLiquidityEvasion(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.PredatoryItems.Count);
        Assert.IsTrue(res.GlobalPredatoryPressureIndex > 0m, "Global predatory pressure index should be positive");
        Assert.IsTrue(res.AverageBlackHoleDepthPct > 0m && res.AverageBlackHoleDepthPct <= 100m,
            "Average black hole depth should be within (0, 100]");
        Assert.IsTrue(res.AveragePredatoryDamageReductionPct > 0m && res.AveragePredatoryDamageReductionPct <= 100m,
            "Average predatory damage reduction should be within (0, 100]");
        Assert.IsTrue(res.TotalPredatoryEvasionAlphaBps > 0m, "Total predatory evasion alpha should be positive");

        foreach (var item in res.PredatoryItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.PredatoryPressureIndex > 0m);
            Assert.IsTrue(item.LiquidityBlackHoleDepthPct > 0m);
            Assert.IsTrue(item.NashEvasionOptimalSpeed > 0m);
            Assert.IsTrue(item.CamouflageRandomizationPct > 0m && item.CamouflageRandomizationPct <= 100m);
            Assert.IsTrue(item.PredatoryEvasionAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.PredatoryRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.PredatoryAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Two Sigma") || res.ExecutiveVerdict.Contains("PDT") || res.ExecutiveVerdict.Contains("HJBI") || res.ExecutiveVerdict.Contains("捕食") || res.ExecutiveVerdict.Contains("博弈"),
            "Verdict should mention Two Sigma, PDT, HJBI, Predatory, or Game");
    }

    [TestMethod]
    public void Test_Phase59_DriftDiffusionKalmanMacroParity()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateDriftDiffusionKalmanMacroParity(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.DriftMacroItems.Count);
        Assert.IsTrue(res.GlobalMacroDriftSpeed > 0m, "Global macro drift speed should be positive");
        Assert.IsTrue(res.AverageKalmanConfidencePct > 0m && res.AverageKalmanConfidencePct <= 100m,
            "Average Kalman confidence pct should be within (0, 100]");
        Assert.IsTrue(res.RegimeChurnReductionPct > 0m && res.RegimeChurnReductionPct <= 100m,
            "Regime churn reduction pct should be within (0, 100]");
        Assert.IsTrue(res.TotalMacroParityAlphaBps > 0m, "Total macro parity alpha should be positive");

        decimal totalWeight = 0m;
        foreach (var item in res.DriftMacroItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.ContinuousQuadrantAffinityScore > 0m);
            Assert.IsTrue(item.KalmanBucyTrackingErrorPct > 0m);
            Assert.IsTrue(item.RegimeResilientTargetWeight > 0m);
            Assert.IsTrue(item.MacroParityAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.DriftRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.DriftMacroAdvice));
            totalWeight += item.RegimeResilientTargetWeight;
        }

        Assert.IsTrue(Math.Abs(totalWeight - 100m) < 1.0m, $"Regime resilient target weights should sum close to 100%, actual: {totalWeight}");

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Bridgewater") || res.ExecutiveVerdict.Contains("AQR") || res.ExecutiveVerdict.Contains("漂移") || res.ExecutiveVerdict.Contains("卡尔曼") || res.ExecutiveVerdict.Contains("宏观"),
            "Verdict should mention Bridgewater, AQR, Drift, Kalman, or Macro Parity");
    }

    [TestMethod]
    public void Test_Phase59_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.KyleContinuousAuctionElasticity, "KyleContinuousAuctionElasticity should be populated in PortfolioResult");
        Assert.IsNotNull(result.WilsonRenormalizationPercolation, "WilsonRenormalizationPercolation should be populated in PortfolioResult");
        Assert.IsNotNull(result.PredatoryGameLiquidityEvasion, "PredatoryGameLiquidityEvasion should be populated in PortfolioResult");
        Assert.IsNotNull(result.DriftDiffusionKalmanMacroParity, "DriftDiffusionKalmanMacroParity should be populated in PortfolioResult");

        // Verify HTML Export includes Phase 59 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("一百三十八、Citadel Securities & Jump Trading: 连续拍卖 Kyle's λ 价格冲击弹性与订单流信息穿透深度控制 (Phase 59)"), "HTML should contain Section 138");
        Assert.IsTrue(html.Contains("一百三十九、Renaissance Technologies & D.E. Shaw: 拓扑 Wilson 重整化群矩流动与仿射渗流相变临界聚类 (Phase 59)"), "HTML should contain Section 139");
        Assert.IsTrue(html.Contains("一百四十、Two Sigma & PDT Partners: 连续时间随机微分博弈 (SDG) 捕食者-猎物纳什均衡与流动性黑洞抢跑规避变分 (Phase 59)"), "HTML should contain Section 140");
        Assert.IsTrue(html.Contains("一百四十一、Bridgewater Associates & AQR Capital: 连续时间漂移-扩散非线性状态空间与鲁棒卡尔曼-布西宏观体制平价 (Phase 59)"), "HTML should contain Section 141");
        Assert.IsTrue(html.Contains("全局加权 Kyle's λ 价格冲击"), "HTML should contain Section 138 keywords");
        Assert.IsTrue(html.Contains("全局临界渗流阈值 p_c"), "HTML should contain Section 139 keywords");
        Assert.IsTrue(html.Contains("全局捕食者做空压力指数"), "HTML should contain Section 140 keywords");
        Assert.IsTrue(html.Contains("全局宏观漂移速度"), "HTML should contain Section 141 keywords");
    }
}
