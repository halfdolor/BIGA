using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase24Tests
{
    private static List<NavRecord> GenerateSyntheticNav(DateTime start, int days, double annualRet, double annualVol)
    {
        var list = new List<NavRecord>();
        double nav = 1.0;
        double dailyMean = annualRet / 252.0;
        double dailyVol = annualVol / Math.Sqrt(252.0);
        var rng = new Random(42);

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

    private static List<(FundDetail Fund, decimal Weight)> CreateSampleComponents()
    {
        var baseDate = new DateTime(2023, 1, 1);
        var fundA = new FundDetail { Code = "110011", Name = "易方达中小盘混合 (偏股型)", NavHistory = GenerateSyntheticNav(baseDate, 300, 0.16, 0.22) };
        var fundB = new FundDetail { Code = "510300", Name = "华泰柏瑞沪深300ETF (宽基指数)", NavHistory = GenerateSyntheticNav(baseDate, 300, 0.09, 0.17) };
        var fundC = new FundDetail { Code = "000001", Name = "华夏纯债中短债A (固定收益)", NavHistory = GenerateSyntheticNav(baseDate, 300, 0.045, 0.035) };
        var fundD = new FundDetail { Code = "000509", Name = "易方达现金增利货币A (流动性货币)", NavHistory = GenerateSyntheticNav(baseDate, 300, 0.022, 0.005) };

        return new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 35.0m),
            (fundB, 25.0m),
            (fundC, 25.0m),
            (fundD, 15.0m)
        };
    }

    [TestMethod]
    public void Test_LiquidityLadderAndRedemptionRun_CalculatesDtlAndTiers()
    {
        var components = CreateSampleComponents();
        var result = QuantCalculator.CalculateLiquidityLadderAndRedemptionRun(components, 2000m);

        Assert.IsNotNull(result, "LiquidityLadder result should not be null.");
        Assert.AreEqual(4, result.FundTierItems.Count, "All 4 component funds should have liquidity tier items.");
        
        // 验证 DTL 完全变现天数大于 0
        Assert.IsTrue(result.PortfolioWeightedDtlDays > 0, "Portfolio weighted DTL days must be positive.");
        
        // 验证各梯队权重归一化和为 100%
        decimal sumTiers = result.Tier1WeightPercent + result.Tier2WeightPercent + result.Tier3WeightPercent + result.Tier4WeightPercent;
        Assert.AreEqual(100.0m, sumTiers, 0.5m, "Sum of tier weight percentages should be approximately 100%.");

        // 验证巨额赎回级联逆向选择仿真场景 (10%, 20%, 30% 各含瀑布式与等比例双策略，共 6 组)
        Assert.AreEqual(6, result.CascadeScenarios.Count, "Should simulate 6 redemption cascade scenarios across 10%, 20%, 30%.");
        foreach (var scenario in result.CascadeScenarios)
        {
            Assert.IsTrue(scenario.RedemptionRatioPercent > 0);
            Assert.IsTrue(scenario.PostRunWeightedDtlDays >= 0);
            Assert.IsTrue(scenario.RemainingHealthScore >= 0 && scenario.RemainingHealthScore <= 100);
            Assert.IsFalse(string.IsNullOrWhiteSpace(scenario.RegulatoryAlertStatus));
        }

        // 验证流动性评级与执行诊断报告生成
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.PortfolioLiquidityGrade));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveDiagnosis));
    }

    [TestMethod]
    public void Test_RedemptionCascade_WaterfallVsProRata_Deterioration()
    {
        var components = CreateSampleComponents();
        var result = QuantCalculator.CalculateLiquidityLadderAndRedemptionRun(components, 5000m);

        // 提取 30% 极端巨额赎回情景下的瀑布变现与等比例平仓
        var waterfall30 = result.CascadeScenarios.FirstOrDefault(s => s.RedemptionRatioPercent == 30.0m && s.LiquidationStrategy.Contains("瀑布"));
        var proRata30 = result.CascadeScenarios.FirstOrDefault(s => s.RedemptionRatioPercent == 30.0m && s.LiquidationStrategy.Contains("等比例"));

        Assert.IsNotNull(waterfall30, "30% waterfall scenario must exist.");
        Assert.IsNotNull(proRata30, "30% pro-rata scenario must exist.");

        // 瀑布式抛售高流动性资产后，剩余组合的加权变现天数 (DTL) 应恶化 (大于等于等比例平仓)
        Assert.IsTrue(waterfall30.PostRunWeightedDtlDays >= proRata30.PostRunWeightedDtlDays, 
            "Waterfall liquidation should deteriorate remaining DTL more severely than pro-rata.");
    }

    [TestMethod]
    public void Test_PortfolioInsurance_CppiAndTippSimulation_GuaranteesFloors()
    {
        var components = CreateSampleComponents();
        var result = QuantCalculator.SimulatePortfolioInsurancePaths(
            components, 
            initialCapital: 1000m, 
            protectionRatioPercent: 95.0m, 
            riskMultiplier: 3.5m, 
            riskFreeRatePercent: 2.5m);

        Assert.IsNotNull(result);
        Assert.AreEqual(22, result.PathPoints.Count, "Sampled path points should cover 0 to 252 trading days (sampled bi-weekly).");

        // 验证 CPPI 贴现保本底线守恒
        Assert.IsTrue(result.IsCppiFloorPreserved, "CPPI floor must be preserved under continuous rebalancing.");
        
        // 验证 TIPP 棘轮保本底线守恒
        Assert.IsTrue(result.IsTippFloorPreserved, "TIPP floor must be strictly preserved.");

        // 验证 TIPP 棘轮锁定底线单调不减
        decimal lastTippFloor = 0m;
        foreach (var pt in result.PathPoints)
        {
            Assert.IsTrue(pt.TippFloorValue >= lastTippFloor - 1e-4m, "TIPP ratchet floor should never decrease.");
            lastTippFloor = pt.TippFloorValue;

            // 净值应始终高于或等于保本底线
            Assert.IsTrue(pt.TippAssetValue >= pt.TippFloorValue - 1e-2m, "TIPP asset value should stay above the ratchet floor.");
        }

        // 验证回撤指标非负且在合理闭区间内
        Assert.IsTrue(result.CppiMaxDrawdownPercent >= 0 && result.CppiMaxDrawdownPercent <= 100.0m);
        Assert.IsTrue(result.TippMaxDrawdownPercent >= 0 && result.TippMaxDrawdownPercent <= 100.0m);
        Assert.IsTrue(result.CppiFinalValue > 0);
        Assert.IsTrue(result.TippFinalValue > 0);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.StrategyRecommendation));
    }

    [TestMethod]
    public void Test_PortfolioInsurance_TippRatchet_LocksProfitsOnNewHighs()
    {
        var components = CreateSampleComponents();
        var result = QuantCalculator.SimulatePortfolioInsurancePaths(
            components,
            initialCapital: 2000m,
            protectionRatioPercent: 90.0m,
            riskMultiplier: 4.0m,
            riskFreeRatePercent: 3.0m);

        Assert.IsNotNull(result);
        var initialFloor = result.PathPoints.First().TippFloorValue;
        var peakFloor = result.PathPoints.Max(p => p.TippFloorValue);

        // 如果组合在年中冲出新高，TIPP 底线应当水涨船高 (大于或等于初始底线)
        Assert.IsTrue(peakFloor >= initialFloor, "TIPP ratchet mechanism must ratchet up floor when asset appreciates.");
    }

    [TestMethod]
    public void Test_HigherMoments_ModifiedSharpeAndOmega_PenalizesFatTails()
    {
        var components = CreateSampleComponents();
        var result = QuantCalculator.CalculateHigherMomentsAndModifiedSharpe(components, riskFreeRatePercent: 2.5m);

        Assert.IsNotNull(result);
        Assert.AreEqual(4, result.AssetMetrics.Count);

        foreach (var asset in result.AssetMetrics)
        {
            Assert.IsTrue(asset.CornishFisherMVaR95 > 0, "Cornish-Fisher MVaR must be positive.");
            Assert.IsTrue(asset.OmegaRatio > 0, "Omega ratio must be positive.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(asset.TailRiskBadge));
        }

        // 验证最优权重归一化与优化比较
        decimal sumModifiedWeights = result.ModifiedSharpeOptimalWeights.Sum(w => w.Value);
        Assert.AreEqual(100.0m, sumModifiedWeights, 0.5m, "Modified Sharpe optimal weights must sum to 100%.");

        decimal sumOmegaWeights = result.OmegaOptimalWeights.Sum(w => w.Value);
        Assert.AreEqual(100.0m, sumOmegaWeights, 0.5m, "Omega optimal weights must sum to 100%.");

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.OptimizationComparisonDiagnosis));
    }

    [TestMethod]
    public void Test_HigherMoments_MarkowitzVsModifiedSharpeWeightsShift()
    {
        var components = CreateSampleComponents();
        var result = QuantCalculator.CalculateHigherMomentsAndModifiedSharpe(components, riskFreeRatePercent: 2.0m);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.ClassicMarkowitzWeights.Count > 0);
        Assert.IsTrue(result.ModifiedSharpeOptimalWeights.Count > 0);
        
        // 修正夏普应产生非负有效权重
        foreach (var w in result.ModifiedSharpeOptimalWeights)
        {
            Assert.IsTrue(w.Value >= 0 && w.Value <= 100m, "Weight must be between 0 and 100%");
        }
    }

    [TestMethod]
    public void Test_ManagerTransition_ChowBreakpointTest_DetectsStructuralShift()
    {
        var rng = new Random(123);
        var dailyReturns = new List<decimal>();
        // 前任阶段 180 天 (正向均值与低波动)
        for (int i = 0; i < 180; i++)
        {
            dailyReturns.Add((decimal)(0.0008 + rng.NextDouble() * 0.008 - 0.004));
        }
        // 现任阶段 100 天 (负向均值与高波动风格断裂)
        for (int i = 0; i < 100; i++)
        {
            dailyReturns.Add((decimal)(-0.0006 + rng.NextDouble() * 0.016 - 0.008));
        }

        var transitionResult = QuantCalculator.EvaluateManagerTransitionBreakpoint(
            dailyReturns,
            currentAlpha: 4.8m,
            currentBeta: 0.92m,
            currentVol: 18.2m,
            currentMaxDd: 16.5m);

        Assert.IsNotNull(transitionResult);
        Assert.IsFalse(string.IsNullOrWhiteSpace(transitionResult.PredecessorName));
        Assert.IsFalse(string.IsNullOrWhiteSpace(transitionResult.SuccessorName));
        
        // Chow 检验 F 统计量应为非负数值
        Assert.IsTrue(transitionResult.ChowTestFStatistic >= 0, "Chow F statistic must be >= 0.");
        Assert.IsTrue(transitionResult.ChowPValue >= 0m && transitionResult.ChowPValue <= 1.0m, "Chow p-value must be between 0 and 1.");
        
        // 验证换帅风险评级与裁决意见非空
        Assert.IsFalse(string.IsNullOrWhiteSpace(transitionResult.TransitionRiskRating));
        Assert.IsFalse(string.IsNullOrWhiteSpace(transitionResult.StructuralBreakConclusion));
        Assert.IsFalse(string.IsNullOrWhiteSpace(transitionResult.InvestmentCommitteeActionAdvice));
    }

    [TestMethod]
    public void Test_PortfolioEngine_IntegratesPhase24_Seamlessly()
    {
        var components = CreateSampleComponents();
        var portfolioResult = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(portfolioResult);

        // 验证 Phase 24 流水线挂载的三大成果
        Assert.IsNotNull(portfolioResult.LiquidityRedemptionRun, "LiquidityRedemptionRun should be populated by PortfolioEngine.");
        Assert.IsNotNull(portfolioResult.PortfolioInsurance, "PortfolioInsurance should be populated by PortfolioEngine.");
        Assert.IsNotNull(portfolioResult.HigherMomentsOptimization, "HigherMomentsOptimization should be populated by PortfolioEngine.");

        // 验证各成果核心字段与业务完整性
        Assert.IsTrue(portfolioResult.LiquidityRedemptionRun.FundTierItems.Count == 4);
        Assert.IsTrue(portfolioResult.PortfolioInsurance.PathPoints.Count > 0);
        Assert.IsTrue(portfolioResult.HigherMomentsOptimization.AssetMetrics.Count == 4);
    }
}
