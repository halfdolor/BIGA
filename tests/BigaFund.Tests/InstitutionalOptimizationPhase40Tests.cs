using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase40Tests
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
            double ret = (dailyMean + dailyStd * z) * 100.0;
            list.Add(Math.Round((decimal)ret, 4));
        }
        return list;
    }

    [TestMethod]
    public void Test_Phase40_MarkovRegimeSwitchingAndConditionalAllocation()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.12, 0.16, 888);

        var result = QuantCalculator.CalculateMarkovRegimeSwitchingAndConditionalAllocation(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.DominantRegimeStateIndex >= 1 && result.DominantRegimeStateIndex <= 4, "State index should be between 1 and 4");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.DominantRegimeName), "Dominant regime name should not be empty");
        Assert.IsTrue(result.DominantRegimeConfidencePercent > 0m && result.DominantRegimeConfidencePercent <= 100m, "Regime confidence should be in (0, 100]");
        Assert.IsTrue(result.RegimeEntropyIndex >= 0m && result.RegimeEntropyIndex <= 1.0m, "Shannon entropy should be in [0, 1]");
        Assert.IsTrue(result.AdaptiveCrossCycleSharpeUpliftPercent >= 0m, "Cross-cycle Sharpe uplift should be non-negative");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict), "ExecutiveVerdict should not be empty");

        Assert.AreEqual(4, result.RegimeStates.Count, "Should output exactly 4 macro regime states");

        decimal totalFilteredProb = result.RegimeStates.Sum(s => s.FilteredProbabilityPercent);
        Assert.IsTrue(Math.Abs(totalFilteredProb - 100m) < 1.0m, $"Filtered probabilities should sum to approx 100%, got {totalFilteredProb}");

        decimal totalSmoothedProb = result.RegimeStates.Sum(s => s.SmoothedProbabilityPercent);
        Assert.IsTrue(Math.Abs(totalSmoothedProb - 100m) < 1.0m, $"Smoothed probabilities should sum to approx 100%, got {totalSmoothedProb}");

        decimal totalErgodicProb = result.RegimeStates.Sum(s => s.ErgodicSteadyStateProbPercent);
        Assert.IsTrue(Math.Abs(totalErgodicProb - 100m) < 1.0m, $"Ergodic probabilities should sum to approx 100%, got {totalErgodicProb}");

        decimal totalWeight = result.RegimeStates.Sum(s => s.RecommendedRegimeWeightPercent);
        Assert.IsTrue(Math.Abs(totalWeight - 100m) < 1.0m, $"Recommended weights should sum to approx 100%, got {totalWeight}");

        foreach (var st in result.RegimeStates)
        {
            Assert.IsTrue(st.StateIndex >= 1 && st.StateIndex <= 4);
            Assert.IsFalse(string.IsNullOrWhiteSpace(st.RegimeName));
            Assert.IsTrue(st.RegimeConditionalVolatilityAnnualizedPercent > 0m, "Volatility should be positive");
            Assert.IsTrue(st.ExpectedDurationDays > 0m, "Duration days should be positive");
            Assert.IsFalse(string.IsNullOrWhiteSpace(st.RegimeMacroBadge));
        }
    }

    [TestMethod]
    public void Test_Phase40_CrossSectionalMultiHorizonMomentum()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.11, 0.15, 999);

        var result = QuantCalculator.CalculateCrossSectionalMultiHorizonMomentum(components, returns);

        Assert.IsNotNull(result);
        Assert.AreEqual(components.Count, result.AssetMomentums.Count, "Should calculate momentum for all components");
        Assert.IsTrue(result.CrossSectionalDispersionPercent >= 0m, "Dispersion should be non-negative");
        Assert.IsTrue(result.WinnerLoserSpreadAnnualizedPercent != 0m, "Winner-loser spread should not be zero");
        Assert.IsTrue(result.PortfolioAverageHurstExponent > 0m, "Portfolio Hurst exponent should be positive");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));

        // Verify ranks form a permutation of 1 to components.Count
        var ranks = result.AssetMomentums.Select(a => a.CrossSectionalRank).OrderBy(r => r).ToList();
        for (int i = 0; i < components.Count; i++)
        {
            Assert.AreEqual(i + 1, ranks[i], "Ranks should be sequential 1..N");
        }

        foreach (var m in result.AssetMomentums)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(m.Code));
            Assert.IsFalse(string.IsNullOrWhiteSpace(m.Name));
            Assert.IsTrue(m.HurstPersistenceExponent > 0m, "Hurst exponent should be positive");
            Assert.IsTrue(m.DualMomentumScore >= 0m && m.DualMomentumScore <= 100m, "Dual momentum score should be in [0, 100]");
            Assert.IsFalse(string.IsNullOrWhiteSpace(m.DualMomentumSignal));
        }
    }

    [TestMethod]
    public void Test_Phase40_PodTieredDrawdownCircuitBreakerAndCapitalRebalancing()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.13, 0.18, 123);

        var result = QuantCalculator.CalculatePodTieredDrawdownCircuitBreakerAndCapitalRebalancing(components, returns);

        Assert.IsNotNull(result);
        Assert.AreEqual(components.Count, result.PodTiers.Count, "Should calculate Pod tiers for all components");
        Assert.IsTrue(result.OverallPodHealthScore >= 0m && result.OverallPodHealthScore <= 100m, "Health score should be in [0, 100]");
        Assert.AreEqual(components.Count, result.NormalPodCount + result.ThrottledPodCount + result.CircuitBreakerTriggeredCount, "Pod counts must sum to total pods");
        Assert.IsTrue(result.TotalCapitalProtectedPercent >= 0m && result.TotalCapitalProtectedPercent <= 100m, "Protected capital should be in [0, 100]");
        Assert.IsTrue(result.DynamicRebalancingEfficiencyGainPercent >= 0m, "Efficiency gain should be non-negative");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));

        decimal totalPostBreakerCapital = result.PodTiers.Sum(p => p.PostBreakerCapitalPercent);
        Assert.IsTrue(Math.Abs(totalPostBreakerCapital - 100m) < 1.0m, $"Post breaker capital should sum to approx 100%, got {totalPostBreakerCapital}");

        foreach (var pod in result.PodTiers)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(pod.PodCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(pod.PodName));
            Assert.IsTrue(pod.AllocatedCapitalPercent > 0m);
            Assert.IsTrue(pod.CurrentDrawdownPercent >= 0m);
            Assert.IsTrue(pod.MaxHistoricalDrawdownPercent >= 0m);
            Assert.IsTrue(pod.DynamicCapitalMultiplier >= 0m && pod.DynamicCapitalMultiplier <= 1.0m);
            Assert.IsTrue(pod.PostBreakerCapitalPercent >= 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(pod.CircuitBreakerTier));
        }
    }

    [TestMethod]
    public void Test_Phase40_LimitOrderQueueFillProbabilityAndBasisArbitrage()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.10, 0.14, 456);

        var result = QuantCalculator.CalculateLimitOrderQueueFillProbabilityAndBasisArbitrage(components, returns);

        Assert.IsNotNull(result);
        Assert.AreEqual(components.Count, result.AssetQueueArbitrages.Count, "Should calculate queue arbitrage for all components");
        Assert.IsTrue(result.PortfolioAverageFillProbabilityPercent >= 0m && result.PortfolioAverageFillProbabilityPercent <= 100m, "Fill probability should be in [0, 100]");
        Assert.IsTrue(result.AnnualizedPassiveExecutionSavingsPercent >= 0m, "Passive execution savings should be non-negative");
        Assert.IsTrue(result.MaxBasisMispricingBps >= 0m, "Max basis mispricing should be non-negative");
        Assert.IsTrue(result.AnnualizedSyntheticBasisArbitrageYieldPercent >= 0m, "Synthetic basis yield should be non-negative");
        Assert.IsTrue(result.AverageBasisHalfLifeDays > 0m, "Basis half-life should be positive");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));

        foreach (var qa in result.AssetQueueArbitrages)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(qa.Code));
            Assert.IsFalse(string.IsNullOrWhiteSpace(qa.Name));
            Assert.IsTrue(qa.OptimalLimitSpreadBps > 0m, "Limit spread bps should be positive");
            Assert.IsTrue(qa.PassiveFillProbabilityPercent >= 0m && qa.PassiveFillProbabilityPercent <= 100m);
            Assert.IsTrue(qa.QueueWaitTimeSeconds > 0m, "Queue wait time should be positive");
            Assert.IsTrue(qa.BasisMeanReversionHalfLifeDays > 0m, "Half life should be positive");
            Assert.IsFalse(string.IsNullOrWhiteSpace(qa.ExecutionRegimeBadge));
        }
    }

    [TestMethod]
    public void Test_Phase40_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var engineResult = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(engineResult);
        Assert.IsNotNull(engineResult.MacroMarkovRegimeSwitching, "MacroMarkovRegimeSwitching should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.CrossSectionalMomentum, "CrossSectionalMomentum should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.PodTieredDrawdownCircuitBreaker, "PodTieredDrawdownCircuitBreaker should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.LimitOrderQueueAndBasisArbitrage, "LimitOrderQueueAndBasisArbitrage should be populated in PortfolioEngine");

        // Verify institutional due diligence HTML report generation
        string htmlReport = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(engineResult, components);
        Assert.IsFalse(string.IsNullOrWhiteSpace(htmlReport));
        Assert.IsTrue(htmlReport.Contains("六十二、Bridgewater Associates & Citadel 宏观马尔可夫区制转移概率模型与跨周期条件资产配置引擎 (Phase 40)"), "Report should contain Phase 40.1 section");
        Assert.IsTrue(htmlReport.Contains("六十三、AQR Capital & Man Group AHL 多频率截面交叉动量与双重相对优势动量剥离引擎 (Phase 40)"), "Report should contain Phase 40.2 section");
        Assert.IsTrue(htmlReport.Contains("六十四、Millennium Management & Balyasny (BAM) 多策略 Pod Shop 阶梯式硬风控回撤熔断与动态资本再平衡矩阵 (Phase 40)"), "Report should contain Phase 40.3 section");
        Assert.IsTrue(htmlReport.Contains("六十五、Jane Street & Optiver 微观限价单队列成交概率与高频期现基差收敛套利执行引擎 (Phase 40)"), "Report should contain Phase 40.4 section");
    }
}
