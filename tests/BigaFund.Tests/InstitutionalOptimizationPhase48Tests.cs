using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase48Tests
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
    public void Test_Phase48_StochasticPontryaginControl()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateStochasticPontryaginControl(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(components.Count, res.AssetSpmpControls.Count);
        Assert.IsTrue(!double.IsNaN((double)res.PortfolioAverageCoStateP), "Portfolio average CoState P should be a valid number");
        Assert.IsTrue(res.PortfolioAverageOptimalControlRate > 0m, "Portfolio average optimal control rate should be positive");
        Assert.IsTrue(res.TotalRebalanceSavingsBps > 0m, "Total rebalance savings should be positive");
        Assert.IsTrue(res.HamiltonianSecondOrderConcavity < 0m, "Hamiltonian second-order concavity should be strictly negative for maximum");

        foreach (var asset in res.AssetSpmpControls)
        {
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(asset.AssetName));
            Assert.IsTrue(asset.Weight > 0m);
            Assert.IsTrue(!double.IsNaN((double)asset.CoStateP), "CoState P should be a valid number");
            Assert.IsTrue(!double.IsNaN((double)asset.CoStateQ), "CoState Q should be a valid number");
            Assert.IsTrue(asset.JumpIntensityLambda >= 0m, "Jump intensity should be non-negative");
            Assert.IsTrue(Math.Abs(asset.OptimalControlIntensity) > 0m, "Optimal control intensity magnitude should be positive");
            Assert.IsTrue(asset.RebalanceFrictionSavingBps > 0m, "Friction saving should be positive");
            Assert.IsFalse(string.IsNullOrEmpty(asset.ControlRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(asset.SpmpExecutionGuidance));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("庞特里亚金") || res.ExecutiveVerdict.Contains("SPMP"), "Verdict should mention SPMP or Pontryagin");
    }

    [TestMethod]
    public void Test_Phase48_ConsensusAdmmArbitration()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateConsensusAdmmArbitration(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.IsTrue(res.PodAllocations.Count > 0, "Pod allocations should be populated");
        Assert.AreEqual(res.TotalPodsCount, res.PodAllocations.Count);
        Assert.IsTrue(res.AdmmIterationsTaken >= 1, "ADMM iterations taken should be >= 1");
        Assert.IsTrue(res.FinalPrimalResidual >= 0m, "Final primal residual should be non-negative");
        Assert.IsTrue(res.FinalPrimalResidual < 0.05m, "Final primal residual should converge below tolerance threshold");
        Assert.IsTrue(res.TotalCapitalEfficiencyGainBps > 0m, "Total capital efficiency gain should be positive");

        decimal totalConsensusWeight = 0m;
        foreach (var pod in res.PodAllocations)
        {
            Assert.IsFalse(string.IsNullOrEmpty(pod.PodCode));
            Assert.IsFalse(string.IsNullOrEmpty(pod.PodStrategyName));
            Assert.IsTrue(pod.LocalTargetWeight > 0m);
            Assert.IsTrue(pod.ConsensusAdmmWeight > 0m);
            Assert.IsTrue(pod.PrimalResidualNorm >= 0m);
            Assert.IsTrue(pod.CapitalEfficiencyGainBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(pod.ConvergenceStatusBadge));
            Assert.IsFalse(string.IsNullOrEmpty(pod.CentralArbitrationAdvice));

            totalConsensusWeight += pod.ConsensusAdmmWeight;
        }

        Assert.AreEqual(100.0m, Math.Round(totalConsensusWeight, 0), "Sum of consensus weights should normalize to approximately 100%");
        Assert.IsTrue(res.ExecutiveVerdict.Contains("ADMM") || res.ExecutiveVerdict.Contains("共识"), "Verdict should mention ADMM or consensus");
    }

    [TestMethod]
    public void Test_Phase48_InfiniteHdpMacroClustering()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateInfiniteHdpMacroClustering(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.IsTrue(res.ActiveRegimesCount >= 2, "Active regimes count should be >= 2");
        Assert.AreEqual(res.ActiveRegimesCount, res.ActiveRegimes.Count);
        Assert.IsTrue(res.NewRegimeEmergenceProbability > 0m && res.NewRegimeEmergenceProbability <= 100m, "New regime emergence probability should be in (0, 100]");
        Assert.IsTrue(res.MacroSurpriseIndex >= 0m && res.MacroSurpriseIndex <= 100m, "Macro surprise index should be in [0, 100]");
        Assert.IsTrue(res.BayesianEvidenceRatio > 1.0m, "Bayesian evidence ratio over fixed HMM should be > 1.0");

        decimal totalPosterior = 0m;
        foreach (var regime in res.ActiveRegimes)
        {
            Assert.IsTrue(regime.RegimeId >= 1);
            Assert.IsFalse(string.IsNullOrEmpty(regime.RegimeName));
            Assert.IsTrue(regime.PosteriorProbability > 0m);
            Assert.IsTrue(regime.TransitionPersistence > 0m);
            Assert.IsTrue(regime.SurpriseAnomalyScore >= 0m);
            Assert.IsFalse(string.IsNullOrEmpty(regime.RegimeRoleBadge));
            Assert.IsFalse(string.IsNullOrEmpty(regime.AssetAllocationGuidance));

            totalPosterior += regime.PosteriorProbability;
        }

        Assert.AreEqual(100.0m, Math.Round(totalPosterior, 0), "Posterior probabilities should sum to approximately 100%");
        Assert.IsTrue(res.ExecutiveVerdict.Contains("HDP") || res.ExecutiveVerdict.Contains("非参数") || res.ExecutiveVerdict.Contains("狄利克雷"), "Verdict should mention HDP or Dirichlet process");
    }

    [TestMethod]
    public void Test_Phase48_CoxProcessAsymmetricMarketMaking()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateCoxProcessAsymmetricMarketMaking(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(components.Count, res.AssetMarketMakingItems.Count);
        Assert.IsTrue(res.PortfolioWeightedAverageSpreadBps > 0m, "Average spread should be positive");
        Assert.IsTrue(res.AverageAsymmetrySkewBps >= 0m, "Average asymmetry skew should be non-negative");
        Assert.IsTrue(res.TotalToxicDefenseGainBps > 0m, "Toxic selection defense gain should be positive");
        Assert.IsTrue(res.TotalExpectedNetMakingAlphaBps > 0m, "Net market making alpha should be positive");

        foreach (var item in res.AssetMarketMakingItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.BaselineArrivalRate > 0m);
            Assert.IsTrue(item.OptimalBidHalfSpreadBps > 0m);
            Assert.IsTrue(item.OptimalAskHalfSpreadBps > 0m);
            Assert.IsTrue(!double.IsNaN((double)item.SpreadAsymmetrySkewBps), "Spread asymmetry skew should be a valid number");
            Assert.IsTrue(item.ToxicSelectionDefenseGainBps > 0m);
            Assert.IsTrue(item.ExpectedMarketMakingAlphaBps > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.MarketMakingStrategyBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.HighFrequencyQuotingGuidance));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Cox") || res.ExecutiveVerdict.Contains("做市"), "Verdict should mention Cox process or market making");
    }

    [TestMethod]
    public void Test_Phase48_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.StochasticPontryaginControl, "StochasticPontryaginControl should be populated");
        Assert.IsNotNull(result.ConsensusAdmmArbitration, "ConsensusAdmmArbitration should be populated");
        Assert.IsNotNull(result.InfiniteHdpMacroClustering, "InfiniteHdpMacroClustering should be populated");
        Assert.IsNotNull(result.CoxProcessAsymmetricMarketMaking, "CoxProcessAsymmetricMarketMaking should be populated");

        // Verify HTML Export includes Phase 48 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("九十四、Renaissance Technologies & D.E. Shaw 跳跃扩散 Lévy 过程 Esscher 测度变换与随机庞特里亚金极大值原理 (SPMP) 伴随状态最优控制 (Phase 48)"), "HTML should contain Section 94");
        Assert.IsTrue(html.Contains("九十五、Citadel Global Strategies & Millennium Management 多 Pod 去中心化共识交替方向乘子法 (Consensus ADMM) 与增广拉格朗日多重约束动态资本仲裁 (Phase 48)"), "HTML should contain Section 95");
        Assert.IsTrue(html.Contains("九十六、Two Sigma & Bridgewater Associates 非参数分层狄利克雷过程隐马尔可夫模型 (HDP-HMM) 自适应宏观结构聚类与未见体制动态涌现 (Phase 48)"), "HTML should contain Section 96");
        Assert.IsTrue(html.Contains("九十七、Jane Street Capital & Jump Trading 双重随机 Cox 点过程与非齐次复合泊松跳跃做市最优非对称价差偏置控制 (Phase 48)"), "HTML should contain Section 97");
        Assert.IsTrue(html.Contains("一阶伴随协变量"), "HTML should contain Section 94 table/card keywords");
        Assert.IsTrue(html.Contains("ADMM 共识权重"), "HTML should contain Section 95 table keywords");
        Assert.IsTrue(html.Contains("未见体制动态涌现概率"), "HTML should contain Section 96 card keywords");
        Assert.IsTrue(html.Contains("做市有效全价差"), "HTML should contain Section 97 card keywords");
    }
}
