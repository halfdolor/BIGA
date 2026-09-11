using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase49Tests
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
    public void Test_Phase49_RoughFractionalOuMemory()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateRoughFractionalOuMemory(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(components.Count, res.AssetFouMemoryItems.Count);
        Assert.IsTrue(res.PortfolioWeightedHurstParameter > 0m && res.PortfolioWeightedHurstParameter < 0.5m,
            "Rough Hurst parameter should be strictly < 0.5 for anti-persistent sub-diffusion");
        Assert.IsTrue(res.PortfolioAverageFouReversionSpeed > 0m, "Portfolio average fOU reversion speed should be positive");
        Assert.IsTrue(res.TotalTrackingErrorReductionPercent > 0m, "Total tracking error reduction percent should be positive");
        Assert.IsTrue(res.SkorokhodDivergenceEfficiencyGainBps > 0m, "Skorokhod efficiency gain should be positive");

        foreach (var item in res.AssetFouMemoryItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.AssetName));
            Assert.IsTrue(item.Weight > 0m);
            Assert.IsTrue(item.HurstParameter > 0m && item.HurstParameter < 0.5m, "Hurst parameter should be < 0.5");
            Assert.IsTrue(item.FouMeanReversionSpeed > 0m);
            Assert.IsTrue(item.SkorokhodAnticipatingDivergence >= 0m);
            Assert.IsTrue(item.AnticipatingHedgeDelta > 0m);
            Assert.IsTrue(item.TrackingErrorReductionPercent > 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.RoughnessDegreeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.AnticipatingExecutionAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Renaissance") || res.ExecutiveVerdict.Contains("fOU") || res.ExecutiveVerdict.Contains("Skorokhod"),
            "Verdict should mention Renaissance, fOU, or Skorokhod");
    }

    [TestMethod]
    public void Test_Phase49_BilevelStackelbergContract()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateBilevelStackelbergContract(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.TotalPodsCount);
        Assert.AreEqual(4, res.PodContractItems.Count);
        Assert.IsTrue(res.PlatformAverageIncentiveSlope >= 15.0m && res.PlatformAverageIncentiveSlope <= 30.0m,
            "Platform average incentive slope should be in institutional range [15%, 30%]");
        Assert.IsTrue(res.SystemicMoralHazardDeterrenceScore >= 80.0m, "Moral hazard deterrence score should be high");
        Assert.IsTrue(res.CapitalEfficiencyGainBps > 0m, "Capital efficiency gain should be positive");

        foreach (var pod in res.PodContractItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(pod.PodCode));
            Assert.IsFalse(string.IsNullOrEmpty(pod.PodStrategyName));
            Assert.IsTrue(pod.AllocatedCapital > 0m);
            Assert.IsTrue(pod.OptimalIncentiveSlopeAlpha > 0m);
            Assert.IsTrue(pod.HighWaterMarkHurdleRate > 0m);
            Assert.IsTrue(pod.DrawdownStopLossThreshold > 0m);
            Assert.IsTrue(pod.MoralHazardRiskPenalty >= 0m);
            Assert.IsFalse(string.IsNullOrEmpty(pod.ContractGovernanceBadge));
            Assert.IsFalse(string.IsNullOrEmpty(pod.PrincipalAgentAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Stackelberg") || res.ExecutiveVerdict.Contains("Citadel") || res.ExecutiveVerdict.Contains("Millennium"),
            "Verdict should mention Stackelberg, Citadel, or Millennium");
    }

    [TestMethod]
    public void Test_Phase49_TopologicalInformationGeometry()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateTopologicalInformationGeometry(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.GeodesicRegimeItems.Count);
        Assert.IsTrue(res.AverageFisherRaoDistance > 0m, "Average Fisher-Rao distance should be positive");
        Assert.IsTrue(res.MaxManifoldCurvatureMagnitude > 0m, "Max manifold curvature magnitude should be positive");
        Assert.IsTrue(res.GlobalPhaseTransitionWarningIndex >= 0m && res.GlobalPhaseTransitionWarningIndex <= 100m,
            "Phase transition warning index should be in [0, 100]");
        Assert.IsTrue(res.BarycenterStabilityScore >= 0m && res.BarycenterStabilityScore <= 100m,
            "Barycenter stability score should be in [0, 100]");

        foreach (var item in res.GeodesicRegimeItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.RegimePairName));
            Assert.IsTrue(item.FisherRaoGeodesicDistance > 0m);
            Assert.IsTrue(item.RiemannianManifoldCurvature < 0m, "Statistical manifold curvature should be negative (hyperbolic)");
            Assert.IsTrue(item.WassersteinBarycenterDriftRate > 0m);
            Assert.IsTrue(item.PhaseTransitionProbability >= 0m && item.PhaseTransitionProbability <= 100m);
            Assert.IsFalse(string.IsNullOrEmpty(item.ManifoldTopologyBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.MacroGeometricHedgingAdvice));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Two Sigma") || res.ExecutiveVerdict.Contains("Bridgewater") || res.ExecutiveVerdict.Contains("Fisher-Rao") || res.ExecutiveVerdict.Contains("信息几何"),
            "Verdict should mention Two Sigma, Bridgewater, or Information Geometry");
    }

    [TestMethod]
    public void Test_Phase49_MultivariateHawkesMicrostructure()
    {
        var components = CreateSampleComponents();
        var dailyReturns = CreateSampleDailyReturns();

        var res = QuantCalculator.CalculateMultivariateHawkesMicrostructure(components, dailyReturns);

        Assert.IsNotNull(res);
        Assert.AreEqual(4, res.HawkesMatrixItems.Count);
        Assert.IsTrue(res.HawkesBranchingSpectralRadius > 0m && res.HawkesBranchingSpectralRadius < 1.0m,
            "Hawkes branching matrix spectral radius rho(Gamma) must be strictly < 1.0 to guarantee sub-critical stationarity");
        Assert.IsTrue(res.AverageQueueDepletionProbability > 0m && res.AverageQueueDepletionProbability <= 100m,
            "Average queue depletion probability should be in (0, 100]");
        Assert.IsTrue(res.AvalancheBlackHoleWarningScore > 0m, "Avalanche black hole warning score should be positive");
        Assert.IsTrue(res.NetMicrostructureAlphaBps > 0m, "Net microstructure alpha should be positive");

        foreach (var ch in res.HawkesMatrixItems)
        {
            Assert.IsFalse(string.IsNullOrEmpty(ch.EventPairCode));
            Assert.IsFalse(string.IsNullOrEmpty(ch.EventPairName));
            Assert.IsTrue(ch.BaselineArrivalRate > 0m);
            Assert.IsTrue(ch.ExcitationAlpha > 0m);
            Assert.IsTrue(ch.DecayRateBeta > 0m);
            Assert.IsTrue(ch.BranchingRatioContribution > 0m && ch.BranchingRatioContribution < 1.0m);
            Assert.IsTrue(ch.LobQueueDepletionProb > 0m && ch.LobQueueDepletionProb <= 100m);
            Assert.IsFalse(string.IsNullOrEmpty(ch.MicrostructureRegimeBadge));
            Assert.IsFalse(string.IsNullOrEmpty(ch.HighFrequencyQuotingTactics));
        }

        Assert.IsTrue(res.ExecutiveVerdict.Contains("Jane Street") || res.ExecutiveVerdict.Contains("Jump Trading") || res.ExecutiveVerdict.Contains("Hawkes"),
            "Verdict should mention Jane Street, Jump Trading, or Hawkes");
    }

    [TestMethod]
    public void Test_Phase49_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.RoughFractionalOuMemory, "RoughFractionalOuMemory should be populated in PortfolioResult");
        Assert.IsNotNull(result.BilevelStackelbergContract, "BilevelStackelbergContract should be populated in PortfolioResult");
        Assert.IsNotNull(result.TopologicalInformationGeometry, "TopologicalInformationGeometry should be populated in PortfolioResult");
        Assert.IsNotNull(result.MultivariateHawkesMicrostructure, "MultivariateHawkesMicrostructure should be populated in PortfolioResult");

        // Verify HTML Export includes Phase 49 sections
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);

        Assert.IsFalse(string.IsNullOrWhiteSpace(html));
        Assert.IsTrue(html.Contains("九十八、Renaissance Technologies & D.E. Shaw 粗糙分数 Ornstein-Uhlenbeck (fOU) 反持续性长程记忆与 Skorokhod 非适应超前对冲 (Phase 49)"), "HTML should contain Section 98");
        Assert.IsTrue(html.Contains("九十九、Citadel Global Strategies & Millennium Management 双层分层 Stackelberg 动态主从博弈与道德风险防范委托-代理多经理最优契约机制 (Phase 49)"), "HTML should contain Section 99");
        Assert.IsTrue(html.Contains("一百、Two Sigma & Bridgewater Associates 拓扑信息几何 Fisher-Rao 黎曼流形测地线距离与 Wasserstein 测度重心相变预警 (Phase 49)"), "HTML should contain Section 100");
        Assert.IsTrue(html.Contains("一百零一、Jane Street Capital & Jump Trading 多维 Hawkes 自激/互激点过程谱半径与 LOB 队列反应式微观流动性黑洞防御 (Phase 49)"), "HTML should contain Section 101");
        Assert.IsTrue(html.Contains("反持续粗糙亚扩散"), "HTML should contain Section 98 keywords");
        Assert.IsTrue(html.Contains("最优提成斜率"), "HTML should contain Section 99 keywords");
        Assert.IsTrue(html.Contains("Fisher-Rao 测地距离"), "HTML should contain Section 100 keywords");
        Assert.IsTrue(html.Contains("Hawkes 互激核谱半径"), "HTML should contain Section 101 keywords");
    }
}
