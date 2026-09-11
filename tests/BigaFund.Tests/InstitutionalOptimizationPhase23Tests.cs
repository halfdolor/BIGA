using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase23Tests
{
    private static List<NavRecord> GenerateSyntheticNav(DateTime start, int days, double annualRet, double annualVol)
    {
        var list = new List<NavRecord>();
        double nav = 1.0;
        double dailyMean = annualRet / 252.0;
        double dailyVol = annualVol / Math.Sqrt(252.0);
        var rng = new Random(12345);

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
            double ret = dailyMean + dailyVol * z;
            nav *= (1.0 + ret);
        }
        return list;
    }

    [TestMethod]
    public void Test_DynamicFactorTiming_GeneratesNormalizedWeightsAndRegime()
    {
        var funds = new List<StoredFundItem>
        {
            new() { Code = "000001", Name = "稳健红利低波", Return1Y = 12.5m, MaxDrawdown = 8.2m, SharpeRatio = 1.6m },
            new() { Code = "000002", Name = "高质量白马", Return1Y = 15.0m, MaxDrawdown = 12.0m, SharpeRatio = 1.4m },
            new() { Code = "000003", Name = "科技动量先锋", Return1Y = 22.0m, MaxDrawdown = 25.0m, SharpeRatio = 1.1m }
        };

        var result = QuantCalculator.EvaluateDynamicFactorTiming(funds);

        Assert.IsNotNull(result);
        Assert.AreEqual(5, result.FactorMetrics.Count, "Must evaluate 5 core style factors");
        
        // 权重和应严格归一化为 100%
        decimal sumWeight = result.FactorMetrics.Sum(m => m.RecommendedWeightPercent);
        Assert.AreEqual(100.0m, sumWeight, 0.01m, "Recommended weights must sum to 100%");

        // 每个因子权重应落在合理约束区间 [5%, 40%]
        foreach (var fm in result.FactorMetrics)
        {
            Assert.IsTrue(fm.RecommendedWeightPercent >= 4.9m && fm.RecommendedWeightPercent <= 40.1m,
                $"Factor {fm.FactorName} weight {fm.RecommendedWeightPercent}% out of bounds");
            Assert.IsFalse(string.IsNullOrWhiteSpace(fm.RotationStance));
            Assert.IsFalse(string.IsNullOrWhiteSpace(fm.FactorCrowdingLevel));
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.CurrentMarketRegime));
        Assert.IsTrue(result.RegimeConfidencePercent >= 70.0m);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TopOverweightFactor));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.MacroFactorAdvice));
    }

    [TestMethod]
    public void Test_BootstrapLuckVsSkill_TrueAlphaDetection()
    {
        // 高年化超额 (15.0%)，构建 300 天超额收益，测试 p-value 显著性
        var rng = new Random(777);
        var excessReturns = new List<decimal>();
        for (int i = 0; i < 300; i++)
        {
            double daily = (0.15 / 252.0) + (0.05 / Math.Sqrt(252.0)) * (rng.NextDouble() * 2.0 - 1.0);
            excessReturns.Add((decimal)daily);
        }

        var result = QuantCalculator.EvaluateManagerLuckVsSkillBootstrap(excessReturns, 15.0m, 1000);

        Assert.IsNotNull(result);
        Assert.AreEqual(1000, result.BootstrapTrialsCount);
        Assert.AreEqual(15.0m, result.ObservedAnnualAlphaPercent);

        // 零技能分布均值应接近 0% (误差通常 < 1.0%)
        Assert.IsTrue(Math.Abs(result.LuckyAlphaMeanPercent) < 1.0m, "H0 pseudo luck alpha mean must be close to 0");
        Assert.IsTrue(result.LuckyAlphaStdPercent > 0m, "Luck distribution must have positive dispersion");

        // 15% 强 Alpha 下，经验 p 值应高度显著 (<0.05)
        Assert.IsTrue(result.EmpiricalPValue < 0.05m, $"Expected empirical p-value < 0.05, got {result.EmpiricalPValue}");
        Assert.IsTrue(result.SkillClassification.Contains("真Alpha") || result.SkillClassification.Contains("True Skill"));
        Assert.IsTrue(result.ShrunkTrueAlphaPercent > 0m, "Shrunk alpha must be positive");
    }

    [TestMethod]
    public void Test_BootstrapLuckVsSkill_AlphaDestroyerDetection()
    {
        // 深度负超额 (-10.0%)，测试负向价值侵蚀判定
        var result = QuantCalculator.EvaluateManagerLuckVsSkillBootstrap(null, -10.0m, 500);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.SkillClassification.Contains("负向价值侵蚀") || result.SkillClassification.Contains("Alpha Destroyer"));
        Assert.IsTrue(result.AuditConclusion.Contains("一票否决") || result.AuditConclusion.Contains("侵蚀"));
    }

    [TestMethod]
    public void Test_MultiPeriodCompoundedBrinson_CarinoSmoothingMathematicalIdentity()
    {
        // 4 期季度数据，包含不同涨跌态势
        var quarters = new List<BrinsonPeriodInput>
        {
            new() { PeriodLabel = "Q1", PortfolioReturn = 8.5m, BenchmarkReturn = 4.0m, AllocationEffect = 2.5m, SelectionEffect = 1.5m, InteractionEffect = 0.5m },
            new() { PeriodLabel = "Q2", PortfolioReturn = -3.2m, BenchmarkReturn = -5.0m, AllocationEffect = 0.8m, SelectionEffect = 0.8m, InteractionEffect = 0.2m },
            new() { PeriodLabel = "Q3", PortfolioReturn = 12.0m, BenchmarkReturn = 7.5m, AllocationEffect = 2.2m, SelectionEffect = 1.8m, InteractionEffect = 0.5m },
            new() { PeriodLabel = "Q4", PortfolioReturn = 5.0m, BenchmarkReturn = 3.0m, AllocationEffect = 1.1m, SelectionEffect = 0.7m, InteractionEffect = 0.2m }
        };

        var result = QuantCalculator.CalculateMultiPeriodCompoundedBrinson(quarters);

        Assert.IsNotNull(result);
        Assert.AreEqual(4, result.PeriodCount);

        // 验证全期复利收益率计算
        // 组合复利: (1.085)*(0.968)*(1.12)*(1.05) - 1
        double expectedPortRet = ((1.0 + 0.085) * (1.0 - 0.032) * (1.0 + 0.12) * (1.0 + 0.05) - 1.0) * 100.0;
        Assert.AreEqual((decimal)expectedPortRet, result.TotalPortfolioCompoundedReturn, 0.01m);

        // 核心数学恒等式验证：累积平滑配置 + 累积平滑选择 + 累积平滑交互 ≡ 全期复利总超额
        decimal sumEffects = result.CumulativeSmoothedAllocationEffect + result.CumulativeSmoothedSelectionEffect + result.CumulativeSmoothedInteractionEffect;
        Assert.AreEqual(result.TotalCompoundedExcessReturn, sumEffects, 0.0001m, "GIPS Carino exact identity must be strictly satisfied");
        Assert.IsTrue(result.IsGipsIdentityStrictlySatisfied);
        Assert.IsTrue(Math.Abs(result.MathematicalIdentityResidual) < 0.0001m);

        // 验证每一期的 Carino 平滑因子 k_t 均有效且 > 0
        foreach (var p in result.Periods)
        {
            Assert.IsTrue(p.CarinoLinkingFactor > 0m, "Carino linking factor must be positive");
            Assert.AreEqual(p.TotalSmoothedEffect, p.SmoothedAllocationEffect + p.SmoothedSelectionEffect + p.SmoothedInteractionEffect, 0.0001m);
        }
    }

    [TestMethod]
    public void Test_MultiPeriodCompoundedBrinson_ZeroExcessEdgeCase()
    {
        // 组合与基准收益完全相同 (零超额极值测试)
        var quarters = new List<BrinsonPeriodInput>
        {
            new() { PeriodLabel = "Q1", PortfolioReturn = 5.0m, BenchmarkReturn = 5.0m, AllocationEffect = 0m, SelectionEffect = 0m, InteractionEffect = 0m },
            new() { PeriodLabel = "Q2", PortfolioReturn = -2.0m, BenchmarkReturn = -2.0m, AllocationEffect = 0m, SelectionEffect = 0m, InteractionEffect = 0m }
        };

        var result = QuantCalculator.CalculateMultiPeriodCompoundedBrinson(quarters);

        Assert.IsNotNull(result);
        Assert.AreEqual(0m, result.TotalCompoundedExcessReturn, 0.0001m);
        Assert.AreEqual(0m, result.CumulativeSmoothedAllocationEffect, 0.0001m);
        Assert.IsTrue(result.IsGipsIdentityStrictlySatisfied);
    }

    [TestMethod]
    public void Test_ShadowPortfolioPurity_AggregationAndActiveShare()
    {
        // 构造两只基金，底层持有部分重叠股票
        var fund1 = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘",
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "600519", StockName = "贵州茅台", Industry = "食品饮料", WeightPercent = 9.5m },
                new() { StockCode = "000858", StockName = "五粮液", Industry = "食品饮料", WeightPercent = 8.0m },
                new() { StockCode = "600036", StockName = "招商银行", Industry = "银行", WeightPercent = 7.0m }
            }
        };

        var fund2 = new FundDetail
        {
            Code = "163402",
            Name = "兴全趋势投资",
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "600519", StockName = "贵州茅台", Industry = "食品饮料", WeightPercent = 8.5m },
                new() { StockCode = "300750", StockName = "宁德时代", Industry = "电力设备", WeightPercent = 9.0m },
                new() { StockCode = "601318", StockName = "中国平安", Industry = "非银金融", WeightPercent = 6.5m }
            }
        };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fund1, 50m),
            (fund2, 50m)
        };

        var result = QuantCalculator.EvaluateShadowPortfolioPurity(components);

        Assert.IsNotNull(result);
        Assert.AreEqual(5, result.TotalUnderlyingStockCount, "Should aggregate to 5 unique stocks");
        
        // 茅台在两只基金中均被重仓: 9.5*0.5 + 8.5*0.5 = 9.0%
        var moutai = result.TopShadowHoldings.FirstOrDefault(s => s.StockCode == "600519");
        Assert.IsNotNull(moutai);
        Assert.AreEqual(9.0m, moutai.AggregatedPortfolioWeightPercent, 0.05m);
        Assert.AreEqual(2, moutai.HoldingFundCount);
        Assert.IsTrue(moutai.IsHighResonanceWarning, "Moutai held by 2 funds with 9% weight must trigger high resonance warning");

        // 验证集中度与 Active Share 指标区间
        Assert.IsTrue(result.Top10StockConcentrationPercent > 0m);
        Assert.IsTrue(result.ActiveSharePercent >= 45m && result.ActiveSharePercent <= 95m);
        Assert.IsTrue(result.StylePurityScore >= 30m && result.StylePurityScore <= 98m);
        Assert.IsTrue(result.HighResonanceAlerts.Count >= 1);
    }

    [TestMethod]
    public void Test_PortfolioEngine_IntegratesPhase23Results()
    {
        var fundA = new FundDetail
        {
            Code = "000001",
            Name = "蓝筹价值基金",
            NavHistory = GenerateSyntheticNav(new DateTime(2023, 1, 1), 250, 0.12, 0.15),
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "600519", StockName = "贵州茅台", Industry = "食品饮料", WeightPercent = 8.0m },
                new() { StockCode = "601318", StockName = "中国平安", Industry = "非银金融", WeightPercent = 7.0m }
            }
        };
        fundA.QuantMetrics = QuantCalculator.CalculateMetrics(fundA.NavHistory);

        var fundB = new FundDetail
        {
            Code = "000002",
            Name = "成长先锋基金",
            NavHistory = GenerateSyntheticNav(new DateTime(2023, 1, 1), 250, 0.18, 0.22),
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "300750", StockName = "宁德时代", Industry = "电力设备", WeightPercent = 9.0m },
                new() { StockCode = "600519", StockName = "贵州茅台", Industry = "食品饮料", WeightPercent = 6.0m }
            }
        };
        fundB.QuantMetrics = QuantCalculator.CalculateMetrics(fundB.NavHistory);

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 60m),
            (fundB, 40m)
        };

        var portfolioResult = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(portfolioResult);

        // 验证 Phase 23 挂载的三大成果对象
        Assert.IsNotNull(portfolioResult.DynamicFactorTiming, "DynamicFactorTiming must be populated");
        Assert.AreEqual(5, portfolioResult.DynamicFactorTiming.FactorMetrics.Count);

        Assert.IsNotNull(portfolioResult.GipsMultiPeriodBrinson, "GipsMultiPeriodBrinson must be populated");
        Assert.IsTrue(portfolioResult.GipsMultiPeriodBrinson.IsGipsIdentityStrictlySatisfied);

        Assert.IsNotNull(portfolioResult.ShadowPortfolioPurity, "ShadowPortfolioPurity must be populated");
        Assert.IsTrue(portfolioResult.ShadowPortfolioPurity.TotalUnderlyingStockCount > 0);
    }

    [TestMethod]
    public void Test_QuantMetrics_LuckVsSkillAuditPopulation()
    {
        var navHistory = GenerateSyntheticNav(new DateTime(2023, 1, 1), 250, 0.20, 0.14);
        var metrics = QuantCalculator.CalculateMetrics(navHistory);

        Assert.IsNotNull(metrics);
        Assert.IsNotNull(metrics.LuckVsSkillAudit, "LuckVsSkillAudit must be automatically generated");
        Assert.AreEqual(1000, metrics.LuckVsSkillAudit.BootstrapTrialsCount);
        Assert.IsFalse(string.IsNullOrWhiteSpace(metrics.LuckVsSkillAudit.SkillClassification));
        Assert.IsFalse(string.IsNullOrWhiteSpace(metrics.LuckVsSkillAudit.ConfidenceGrade));
    }
}
