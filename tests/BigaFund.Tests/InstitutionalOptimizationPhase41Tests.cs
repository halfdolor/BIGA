using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase41Tests
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
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.09, 0.17, seed: 222)
        };
        var fundC = new FundDetail
        {
            Code = "000001",
            Name = "华夏纯债中短债A (低波固收防御)",
            FundSize = "65.2亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.046, 0.029, seed: 333)
        };
        var fundD = new FundDetail
        {
            Code = "161715",
            Name = "招商中证白酒指数 (周期估值增强)",
            FundSize = "320.0亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.14, 0.26, seed: 444)
        };

        return new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 35m),
            (fundB, 30m),
            (fundC, 20m),
            (fundD, 15m)
        };
    }

    private static List<decimal> CreateSampleDailyReturns(int days = 250, double annRet = 0.11, double annVol = 0.17, int seed = 777)
    {
        var rng = new Random(seed);
        var list = new List<decimal>(days);
        double dailyMean = annRet / 252.0;
        double dailyStd = annVol / Math.Sqrt(252.0);

        for (int i = 0; i < days; i++)
        {
            double u1 = Math.Max(1e-8, rng.NextDouble());
            double u2 = Math.Max(1e-8, rng.NextDouble());
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            double ret = dailyMean + dailyStd * z;
            list.Add((decimal)ret);
        }
        return list;
    }

    [TestMethod]
    public void Test_Phase41_SymbolicGeneticAlphaMining()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.12, 0.16, 888);

        var result = QuantCalculator.CalculateSymbolicGeneticAlphaMining(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.TotalGenerationsEvolved >= 50, "Total generations should be at least 50");
        Assert.IsTrue(result.TotalFormulasEvaluated >= 1000, "Total formulas evaluated should be at least 1000");
        Assert.IsTrue(result.TopAlphaInformationRatio > 0m, "Top Alpha ICIR should be positive");
        Assert.IsTrue(result.AverageFormulaComplexity > 0m, "Average complexity should be positive");
        Assert.IsTrue(result.MultiAlphaEnsembleAnnualizedAlpha > 0m, "Multi-Alpha ensemble return should be positive");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict), "ExecutiveVerdict should not be empty");

        Assert.IsTrue(result.EvolvedAlphaFormulas.Count >= 5, "Should return at least 5 evolved Alpha formulas");
        foreach (var formula in result.EvolvedAlphaFormulas)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(formula.FormulaId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(formula.Expression));
            Assert.IsTrue(formula.InformationCoefficient > 0m);
            Assert.IsTrue(formula.RankInformationCoefficient > 0m);
            Assert.IsTrue(formula.IcInformationRatio > 0m);
            Assert.IsTrue(formula.LongShortAnnualizedSharpe > 0m);
            Assert.IsTrue(formula.ComplexityPenalty > 0m);
            Assert.IsTrue(formula.FitnessScore > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(formula.FormulaFamily));
        }
    }

    [TestMethod]
    public void Test_Phase41_CausalKnowledgeGraphSpillover()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.11, 0.15, 999);

        var result = QuantCalculator.CalculateCausalKnowledgeGraphSpillover(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.TotalSystemSpilloverIndexPercent > 0m && result.TotalSystemSpilloverIndexPercent <= 100m, "Total spillover should be in (0, 100]");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.DominantInformationSourceAsset), "Dominant source should not be empty");
        Assert.IsTrue(result.AverageLeadTimeDays > 0m, "Lead time days should be positive");
        Assert.IsTrue(result.CausalNetworkDensityPercent > 0m && result.CausalNetworkDensityPercent <= 100m, "Network density should be in (0, 100]");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict), "ExecutiveVerdict should not be empty");

        int expectedEdges = components.Count * (components.Count - 1);
        Assert.AreEqual(expectedEdges, result.SpilloverEdges.Count, "Should output N*(N-1) directed causal edges");

        foreach (var edge in result.SpilloverEdges)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(edge.SourceAsset));
            Assert.IsFalse(string.IsNullOrWhiteSpace(edge.TargetAsset));
            Assert.AreNotEqual(edge.SourceAsset, edge.TargetAsset, "Source and target cannot be identical");
            Assert.IsTrue(edge.GrangerFStatistic > 0m, "Granger F-statistic should be positive");
            Assert.IsTrue(edge.GrangerPValue >= 0m && edge.GrangerPValue <= 1.0m, "Granger p-value should be in [0, 1]");
            Assert.IsTrue(edge.TransferEntropyBits > 0m, "Transfer entropy bits should be positive");
            Assert.IsTrue(edge.DirectionalSpilloverPercent >= 0m, "Directional spillover should be non-negative");
            Assert.IsTrue(edge.LeadTimeDays > 0m, "Lead time should be positive");
            Assert.IsFalse(string.IsNullOrWhiteSpace(edge.CausalRoleBadge));
        }
    }

    [TestMethod]
    public void Test_Phase41_ContextualBanditPolicyRouting()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.13, 0.18, 123);

        var result = QuantCalculator.CalculateContextualBanditPolicyRouting(components, returns);

        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.CurrentContextRegime), "Current context regime should not be empty");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.OptimalStrategyName), "Optimal strategy name should not be empty");
        Assert.IsTrue(result.ExplorationVsExploitationRatio > 0m && result.ExplorationVsExploitationRatio < 1.0m, "Exploration ratio should be in (0, 1)");
        Assert.IsTrue(result.CumulativeMetaSharpeGainPercent > 0m, "Meta Sharpe gain should be positive");
        Assert.IsTrue(result.OverallPolicyConfidencePercent > 0m && result.OverallPolicyConfidencePercent <= 100m, "Policy confidence should be in (0, 100]");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict), "ExecutiveVerdict should not be empty");

        Assert.AreEqual(5, result.StrategyArms.Count, "Should have 5 strategy arms");

        decimal totalWeight = result.StrategyArms.Sum(a => a.DynamicMetaWeightPercent);
        Assert.IsTrue(Math.Abs(totalWeight - 100m) < 1.0m, $"Meta weights should sum to approx 100%, got {totalWeight}");

        foreach (var arm in result.StrategyArms)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(arm.StrategyCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(arm.StrategyName));
            Assert.IsTrue(arm.PriorAlpha > 0m);
            Assert.IsTrue(arm.PriorBeta > 0m);
            Assert.IsTrue(arm.PosteriorExpectedWinRatePercent > 0m && arm.PosteriorExpectedWinRatePercent <= 100m);
            Assert.IsTrue(arm.ThompsonSampledScore > 0m);
            Assert.IsTrue(arm.DynamicMetaWeightPercent > 0m);
            Assert.IsTrue(arm.CumulativeRegretReductionPercent >= 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(arm.BanditStatusBadge));
        }
    }

    [TestMethod]
    public void Test_Phase41_MicrostructureResiliencyAndSlippageSurface()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.10, 0.14, 456);

        var result = QuantCalculator.CalculateMicrostructureResiliencyAndSlippageSurface(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.HalfLifeRecoverySeconds > 0m, "Half life recovery seconds should be positive");
        Assert.IsTrue(result.OptimalExecutionUrgency >= 1 && result.OptimalExecutionUrgency <= 5, "Optimal urgency should be 1..5");
        Assert.IsTrue(result.AverageEffectiveSlippageBps > 0m, "Average effective slippage should be positive");
        Assert.IsTrue(result.AnnualizedTransactionCostSavingsPercent > 0m, "Annualized savings should be positive");
        Assert.IsTrue(result.ResiliencyAlphaRecoveryIndex >= 0m && result.ResiliencyAlphaRecoveryIndex <= 100m, "Resiliency recovery index should be in [0, 100]");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));

        Assert.AreEqual(30, result.SurfaceGridPoints.Count, "Should have 6 participation rates * 5 urgency levels = 30 grid points");

        foreach (var pt in result.SurfaceGridPoints)
        {
            Assert.IsTrue(pt.ParticipationRatePercent > 0m);
            Assert.IsTrue(pt.UrgencyLevel >= 1 && pt.UrgencyLevel <= 5);
            Assert.IsTrue(pt.InstantaneousImpactBps > 0m);
            Assert.IsTrue(pt.TransientResiliencyRecoveryPercent >= 0m && pt.TransientResiliencyRecoveryPercent <= 100m);
            Assert.IsTrue(pt.EffectiveSlippageBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(pt.OptimalAlgorithm));
        }
    }

    [TestMethod]
    public void Test_Phase41_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var engineResult = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(engineResult);
        Assert.IsNotNull(engineResult.SymbolicGeneticAlphaMining, "SymbolicGeneticAlphaMining should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.CausalKnowledgeGraphSpillover, "CausalKnowledgeGraphSpillover should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.ContextualBanditPolicyRouter, "ContextualBanditPolicyRouter should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.MicrostructureResiliencySlippageSurface, "MicrostructureResiliencySlippageSurface should be populated in PortfolioEngine");

        // Verify institutional due diligence HTML report generation
        string htmlReport = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(engineResult, components);
        Assert.IsFalse(string.IsNullOrWhiteSpace(htmlReport));
        Assert.IsTrue(htmlReport.Contains("六十六、D.E. Shaw & WorldQuant 符号基因规划自适应 Alpha 因子挖掘引擎 (Phase 41)"), "Report should contain Phase 41.1 section");
        Assert.IsTrue(htmlReport.Contains("六十七、Two Sigma & Man Group AHL 知识图谱跨资产因果时滞传递与宏观情绪溢出网络 (Phase 41)"), "Report should contain Phase 41.2 section");
        Assert.IsTrue(htmlReport.Contains("六十八、Citadel & Point72 上下文多臂老虎机 (Thompson Sampling) 自适应策略路由器 (Phase 41)"), "Report should contain Phase 41.3 section");
        Assert.IsTrue(htmlReport.Contains("六十九、Jump Trading & Optiver 深度强化微观流动性冲击弹性与非线性滑点曲面 (Phase 41)"), "Report should contain Phase 41.4 section");
    }
}
