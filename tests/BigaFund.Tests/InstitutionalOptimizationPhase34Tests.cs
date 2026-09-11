using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase34Tests
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
            Name = "易方达中小盘混合 (高波高Beta成长龙头)",
            FundSize = "180.5亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.20, 0.26, seed: 111)
        };
        var fundB = new FundDetail
        {
            Code = "510300",
            Name = "华泰柏瑞沪深300ETF (核心基石宽基蓝筹)",
            FundSize = "850.0亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.08, 0.16, seed: 222)
        };
        var fundC = new FundDetail
        {
            Code = "000001",
            Name = "华夏纯债中短债A (稳健固收中枢)",
            FundSize = "65.2亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.045, 0.028, seed: 333)
        };
        var fundD = new FundDetail
        {
            Code = "000509",
            Name = "易方达现金增利货币A (极低波流动性底仓)",
            FundSize = "320.0亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.022, 0.006, seed: 444)
        };

        return new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 35.0m),
            (fundB, 25.0m),
            (fundC, 25.0m),
            (fundD, 15.0m)
        };
    }

    private static List<decimal> ComputePortfolioReturns(List<(FundDetail Fund, decimal Weight)> components)
    {
        var dailyReturns = new List<decimal>();
        int days = components[0].Fund.NavHistory.Count;
        decimal totalW = components.Sum(c => c.Weight);

        for (int i = 1; i < days; i++)
        {
            decimal pRet = 0m;
            foreach (var (fund, weight) in components)
            {
                pRet += (weight / totalW) * fund.NavHistory[i].DailyReturn;
            }
            dailyReturns.Add(pRet);
        }

        return dailyReturns;
    }

    [TestMethod]
    public void Test_LeadLagCrossCorrelation_IdentifiesLeadingAssetAndBuildsNetwork()
    {
        var components = CreateSampleComponents();

        var result = QuantCalculator.CalculateLeadLagCrossCorrelationGraph(components, 5);

        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrEmpty(result.AnchorLeaderFundCode), "Anchor leader fund code must not be empty");
        Assert.IsFalse(string.IsNullOrEmpty(result.AnchorLeaderFundName), "Anchor leader fund name must not be empty");
        Assert.IsFalse(string.IsNullOrEmpty(result.MostLaggingFundCode), "Most lagging fund code must not be empty");
        Assert.IsTrue(result.PortfolioAverageLeadLagDispersionDays >= 0m, "Lead-lag dispersion must be non-negative");
        Assert.IsTrue(result.MaxPairwiseAsymmetryGap >= 0m, "Asymmetry gap must be non-negative");
        Assert.AreEqual(5, result.MaxLagHorizonDays);

        // 验证单资产列表
        Assert.AreEqual(components.Count, result.AssetItemList.Count);
        foreach (var item in result.AssetItemList)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.FundCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.NetworkRoleBadge));
            Assert.IsFalse(string.IsNullOrEmpty(item.TacticalAdvisory));
            Assert.IsTrue(item.OutgoingLeadingLinksCount >= 0);
            Assert.IsTrue(item.IncomingLaggingLinksCount >= 0);
        }

        // 验证有向资产对拓扑
        Assert.IsTrue(result.PairItemList.Count > 0, "Pair topology must have entries");
        foreach (var pair in result.PairItemList)
        {
            Assert.IsFalse(string.IsNullOrEmpty(pair.LeaderFundCode));
            Assert.IsFalse(string.IsNullOrEmpty(pair.FollowerFundCode));
            Assert.IsTrue(pair.OptimalLagDays >= 0 && pair.OptimalLagDays <= 5);
            Assert.IsTrue(pair.PeakCrossCorrelation >= -1.0m && pair.PeakCrossCorrelation <= 1.0m);
            Assert.IsTrue(pair.ContemporaneousCorrelation >= -1.0m && pair.ContemporaneousCorrelation <= 1.0m);
            Assert.IsTrue(pair.AsymmetryGap >= 0m);
            Assert.IsFalse(string.IsNullOrEmpty(pair.LeadLagDirectionBadge));
        }

        Assert.IsTrue(result.LeadLagExecutiveVerdict.Contains("Lead-Lag 信息流拓扑审定"));
    }

    [TestMethod]
    public void Test_MultiHorizonRiskTermStructure_CalculatesLoMacKinlayVarianceRatios()
    {
        var components = CreateSampleComponents();
        var dailyReturns = ComputePortfolioReturns(components);

        var result = QuantCalculator.CalculateMultiHorizonVarianceRatioRiskStructure(dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.BaseDailyVolatilityPercent > 0m, "Base daily volatility must be positive");
        Assert.IsTrue(result.AnnualizedLoMacKinlayVarianceRatio > 0m, "Annualized VR(252) must be positive");
        Assert.IsFalse(string.IsNullOrEmpty(result.TermStructureDominantPattern));

        // 验证 6 大投资期限 (1d, 5d, 21d, 63d, 126d, 252d)
        Assert.AreEqual(6, result.HorizonItemList.Count);
        
        // 1d 基准 VR 应为 1.0
        var h1 = result.HorizonItemList[0];
        Assert.AreEqual(1, h1.HorizonDays);
        Assert.AreEqual(1.000m, h1.LoMacKinlayVarianceRatio);
        Assert.AreEqual(0.00m, h1.HeteroscedasticityZScore);
        Assert.AreEqual(1.000m, h1.HorizonAdjustmentMultiplier);

        // 检验后续期限
        foreach (var h in result.HorizonItemList)
        {
            Assert.IsTrue(h.HorizonDays > 0);
            Assert.IsTrue(h.ActualPeriodVolatilityPercent > 0m);
            Assert.IsTrue(h.SqrtTimeBenchmarkVolPercent > 0m);
            Assert.IsTrue(h.LoMacKinlayVarianceRatio > 0m);
            Assert.IsTrue(h.HorizonAdjustmentMultiplier > 0m);
            Assert.IsTrue(h.HorizonAdjustedVaR99Percent > 0m);
            Assert.IsTrue(h.HorizonAdjustedES99Percent >= h.HorizonAdjustedVaR99Percent);
            Assert.IsFalse(string.IsNullOrEmpty(h.DynamicsRegimeBadge));
        }

        Assert.IsTrue(result.MultiHorizonExecutiveVerdict.Contains("Lo-MacKinlay 风险期限结构审定"));
    }

    [TestMethod]
    public void Test_ThreeStateGaussianHmm_DecodesRegimesAndComputesEntropy()
    {
        var components = CreateSampleComponents();
        var dailyReturns = ComputePortfolioReturns(components);

        var result = QuantCalculator.CalculateThreeStateGaussianHmm(dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.BullStateProbabilityPercent >= 0m && result.BullStateProbabilityPercent <= 100m);
        Assert.IsTrue(result.NeutralStateProbabilityPercent >= 0m && result.NeutralStateProbabilityPercent <= 100m);
        Assert.IsTrue(result.CrisisStateProbabilityPercent >= 0m && result.CrisisStateProbabilityPercent <= 100m);

        decimal sumProb = result.BullStateProbabilityPercent + result.NeutralStateProbabilityPercent + result.CrisisStateProbabilityPercent;
        Assert.AreEqual(100.0m, Math.Round(sumProb, 1), "Probabilities must sum to 100%");

        Assert.IsTrue(result.RegimeTransitionEntropy >= 0m, "Regime transition entropy must be non-negative");
        Assert.IsTrue(result.NormalizedEntropyPercent >= 0m && result.NormalizedEntropyPercent <= 100m);
        Assert.IsTrue(result.CurrentDecodedStateIndex >= 1 && result.CurrentDecodedStateIndex <= 3);
        Assert.IsFalse(string.IsNullOrEmpty(result.CurrentDecodedStateName));
        Assert.IsFalse(string.IsNullOrEmpty(result.MacroRegimeAdvisoryBadge));

        // 验证 3 状态细分列表
        Assert.AreEqual(3, result.StateItemList.Count);
        var bull = result.StateItemList[0];
        var neutral = result.StateItemList[1];
        var crisis = result.StateItemList[2];

        Assert.AreEqual("🟢 牛市低波扩张态", bull.StateName);
        Assert.AreEqual("🟡 震荡中波修复态", neutral.StateName);
        Assert.AreEqual("🔴 危机高波踩踏态", crisis.StateName);

        // 收益率排序验证: 牛市期望收益 >= 震荡期望收益 >= 危机期望收益
        Assert.IsTrue(bull.ExpectedDailyReturnPercent >= neutral.ExpectedDailyReturnPercent, "Bull expected return should >= neutral");
        Assert.IsTrue(neutral.ExpectedDailyReturnPercent >= crisis.ExpectedDailyReturnPercent, "Neutral expected return should >= crisis");

        // 波动率验证: 各宏观体制波动率均为正数且处在合理区间
        Assert.IsTrue(bull.AnnualizedVolatilityPercent > 0m);
        Assert.IsTrue(neutral.AnnualizedVolatilityPercent > 0m);
        Assert.IsTrue(crisis.AnnualizedVolatilityPercent > 0m);

        foreach (var s in result.StateItemList)
        {
            Assert.IsTrue(s.ExpectedDwellDays >= 1.0m, "Expected dwell days must be at least 1 day");
            Assert.IsTrue(s.TransitionSelfPersistencePercent >= 0m && s.TransitionSelfPersistencePercent <= 100m);
            Assert.IsTrue(s.HistoricalSampleDays >= 0);
        }

        Assert.IsTrue(result.HmmExecutiveVerdict.Contains("3状态高斯 HMM 体制审定"));
    }

    [TestMethod]
    public void Test_MomentumCrashAndReversalAlpha_CalculatesStrAndCrashWarningIndex()
    {
        var components = CreateSampleComponents();
        var dailyReturns = ComputePortfolioReturns(components);

        var result = QuantCalculator.CalculateMomentumCrashAndReversalAlpha(components, dailyReturns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.MomentumCrashWarningIndex >= 0m && result.MomentumCrashWarningIndex <= 100m, "MCWI should be within [0, 100]");
        Assert.IsTrue(result.MomentumCrashProbabilityPercent >= 0m && result.MomentumCrashProbabilityPercent <= 100m);
        Assert.IsTrue(result.RecommendedReversalTiltTurnoverPercent >= 0m && result.RecommendedReversalTiltTurnoverPercent <= 100m);
        Assert.IsFalse(string.IsNullOrEmpty(result.MomentumProtectionStatusBadge));

        // 验证单资产穿透
        Assert.AreEqual(components.Count, result.AssetItemList.Count);
        foreach (var item in result.AssetItemList)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.FundCode));
            Assert.IsTrue(item.ShortTermReversalScore >= -100m && item.ShortTermReversalScore <= 100m);
            Assert.IsTrue(item.MonthlyReversalScore >= -100m && item.MonthlyReversalScore <= 100m);
            Assert.IsTrue(item.BearMarketDownsideBeta >= 0m);
            Assert.IsTrue(item.BullMarketUpsideBeta >= 0m);
            Assert.IsFalse(string.IsNullOrEmpty(item.TacticalReversalBadge));
        }

        Assert.IsTrue(result.ReversalExecutiveVerdict.Contains("短周期反转与动量崩塌审定"));
    }

    [TestMethod]
    public void Test_PortfolioEngine_Phase34PipelineIntegration_PopulatesAllPhase34Results()
    {
        var components = CreateSampleComponents();

        // 运行完整投资组合分析引擎
        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.LeadLagCrossCorrelation, "Phase 34 LeadLagCrossCorrelation must be populated");
        Assert.IsNotNull(result.MultiHorizonRiskTermStructure, "Phase 34 MultiHorizonRiskTermStructure must be populated");
        Assert.IsNotNull(result.ThreeStateGaussianHmm, "Phase 34 ThreeStateGaussianHmm must be populated");
        Assert.IsNotNull(result.MomentumCrashAndReversal, "Phase 34 MomentumCrashAndReversal must be populated");

        // 验证 HTML 研报导出包含 Phase 34 四大核心章节
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);
        Assert.IsFalse(string.IsNullOrEmpty(html));
        Assert.IsTrue(html.Contains("三十八、Goldman Sachs & J.P. Morgan 跨资产非对称时滞领先-滞后互相关与信息流传导有向网络 (Phase 34)"), "HTML report must contain Goldman Sachs/J.P. Morgan Lead-Lag section");
        Assert.IsTrue(html.Contains("三十九、BlackRock Aladdin / Axioma / Lo-MacKinlay 多重投资期限风险期限结构与方差比非随机游走检验 (Phase 34)"), "HTML report must contain Aladdin Multi-Horizon Risk section");
        Assert.IsTrue(html.Contains("四十、Two Sigma & Citadel 3 状态高斯隐马尔可夫模型 (Gaussian HMM) 宏观体制自动解码与转移信息熵 (Phase 34)"), "HTML report must contain Two Sigma/Citadel Gaussian HMM section");
        Assert.IsTrue(html.Contains("四十一、AQR Capital & Daniel-Moskowitz 广义反向择时短周期反转 Alpha 与动量崩塌预警指数 (MCWI) (Phase 34)"), "HTML report must contain AQR Momentum Crash & Reversal section");
    }
}
