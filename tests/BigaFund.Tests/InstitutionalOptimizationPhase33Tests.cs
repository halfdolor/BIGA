using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase33Tests
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
            Name = "易方达中小盘混合 (高贝塔成长)",
            FundSize = "180.5亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.18, 0.24, seed: 101)
        };
        var fundB = new FundDetail
        {
            Code = "510300",
            Name = "华泰柏瑞沪深300ETF (核心基石蓝筹)",
            FundSize = "850.0亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.09, 0.16, seed: 202)
        };
        var fundC = new FundDetail
        {
            Code = "000001",
            Name = "华夏纯债中短债A (低贝塔稳健固收)",
            FundSize = "65.2亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.045, 0.028, seed: 303)
        };
        var fundD = new FundDetail
        {
            Code = "000509",
            Name = "易方达现金增利货币A (极低贝塔现金)",
            FundSize = "320.0亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.022, 0.006, seed: 404)
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
    public void Test_BabQmjFactorDecomposition_CalculatesSpreadAndShadowCostAndQualityScores()
    {
        var components = CreateSampleComponents();
        var dailyReturns = ComputePortfolioReturns(components);

        var result = QuantCalculator.CalculateBabQmjFactorDecomposition(components, dailyReturns, 2.0m);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.LowBetaBasketAverageBeta < result.HighBetaBasketAverageBeta, "Low beta basket beta should be lower than high beta basket");
        Assert.IsTrue(result.LowBetaLeverageRatio >= 1.0m, "Low beta leverage ratio should be >= 1.0");
        Assert.IsTrue(result.HighBetaDeleverageRatio <= 1.0m, "High beta deleverage ratio should be <= 1.0");
        Assert.IsTrue(result.LowBetaLeverageRatio > result.HighBetaDeleverageRatio, "Low beta leverage should exceed high beta deleverage");

        Assert.IsTrue(result.ImpliedLeverageShadowCostPercent >= 0m, "Leverage shadow cost psi should be non-negative");
        Assert.IsTrue(result.PortfolioWeightedQualityScore >= 0m && result.PortfolioWeightedQualityScore <= 100m, "Portfolio quality score should be between 0 and 100");
        Assert.IsTrue(result.BabAnnualizedVolatilityPercent > 0m, "BAB volatility should be positive");

        // 验证标的列表
        Assert.AreEqual(components.Count, result.AssetItemList.Count);
        foreach (var item in result.AssetItemList)
        {
            Assert.IsFalse(string.IsNullOrEmpty(item.FundCode));
            Assert.IsFalse(string.IsNullOrEmpty(item.BetaBucketName));
            Assert.IsTrue(item.CompositeQualityScore >= 0m && item.CompositeQualityScore <= 100m);
            Assert.IsFalse(string.IsNullOrEmpty(item.QualityRatingBadge));
        }

        Assert.IsTrue(result.BabQmjExecutiveVerdict.Contains("AQR 杠杆异象与 QMJ 质量因子解构"));
    }

    [TestMethod]
    public void Test_EvtGeneralizedParetoExtrapolation_FitsPwmAndExtrapolatesReturnPeriods()
    {
        var components = CreateSampleComponents();
        var dailyReturns = ComputePortfolioReturns(components);

        var result = QuantCalculator.CalculateEvtGeneralizedParetoExtrapolation(dailyReturns, 0.90);

        Assert.IsNotNull(result);
        Assert.AreEqual(dailyReturns.Count, result.TotalSampleObservationsT);
        Assert.IsTrue(result.ExceedanceObservationsNu > 0, "Exceedances count must be positive");
        Assert.IsTrue(result.ThresholdLossPercent > 0m, "Threshold loss u should be positive");
        Assert.IsTrue(result.GpdScaleParameterBeta > 0m, "GPD scale beta must be positive");
        Assert.IsTrue(result.TailIndexFatnessRatio >= 0.5m, "Fatness ratio should be realistic");

        Assert.IsTrue(result.EvtVaR99Percent > 0m, "EVT 99% VaR must be positive");
        Assert.IsTrue(result.EvtES99Percent >= result.EvtVaR99Percent, "Expected Shortfall must be >= VaR");
        Assert.IsFalse(string.IsNullOrEmpty(result.TailDistributionRegime));

        // 验证 4 大极端重现期 (250/1000/2500/5000日)
        Assert.AreEqual(4, result.ReturnPeriodList.Count);
        for (int i = 0; i < result.ReturnPeriodList.Count; i++)
        {
            var rp = result.ReturnPeriodList[i];
            Assert.IsTrue(rp.ReturnPeriodDays > 0);
            Assert.IsTrue(rp.NonExceedanceProbabilityPercent > 99.0m);
            Assert.IsTrue(rp.ExtrapolatedExtremeLossVaRPercent > 0m);
            Assert.IsTrue(rp.ExtrapolatedExpectedTailLossESPercent >= rp.ExtrapolatedExtremeLossVaRPercent);
            Assert.IsFalse(string.IsNullOrEmpty(rp.ShockSeverityGrade));

            if (i > 0)
            {
                var prev = result.ReturnPeriodList[i - 1];
                Assert.IsTrue(rp.ExtrapolatedExtremeLossVaRPercent >= prev.ExtrapolatedExtremeLossVaRPercent,
                    $"Longer return period {rp.ReturnPeriodDays}d VaR should be >= {prev.ReturnPeriodDays}d VaR");
            }
        }

        Assert.IsTrue(result.EvtExecutiveVerdict.Contains("BlackRock Aladdin 极值理论 GPD 尾部外推"));
    }

    [TestMethod]
    public void Test_LiquidityBlackHoleAndFireSale_SimulatesFeedbackLoopAndFcm()
    {
        var components = CreateSampleComponents();

        decimal portfolioAum = 5000m;
        decimal redemptionShockPercent = 15.0m;

        var result = QuantCalculator.CalculateLiquidityBlackHoleAndFireSale(components, portfolioAum, redemptionShockPercent);

        Assert.IsNotNull(result);
        decimal expectedDirect = Math.Round(portfolioAum * (redemptionShockPercent / 100m), 2);
        Assert.AreEqual(expectedDirect, result.DirectLiquidationVolumeTenThousand);
        Assert.IsTrue(result.SecondaryInducedLiquidationVolumeTenThousand >= 0m, "Secondary liquidation volume should be non-negative");
        Assert.IsTrue(result.AggregateFireSaleVolumeTenThousand >= result.DirectLiquidationVolumeTenThousand, "Total liquidation must be >= direct");
        Assert.IsTrue(result.FireSaleCascadeMultiplier >= 1.0m, "FCM cascade multiplier must be >= 1.0");

        Assert.IsTrue(result.ExogenousDirectPriceImpactPercent >= 0m);
        Assert.IsTrue(result.EndogenousFeedbackPriceImpactPercent >= result.ExogenousDirectPriceImpactPercent, "Endogenous impact should >= exogenous");
        Assert.IsTrue(result.LiquidityBlackHoleIndex >= 0m && result.LiquidityBlackHoleIndex <= 100m, "LBHI should be within [0, 100]");
        Assert.IsTrue(result.MaxFireSaleCapacityTenThousand > 0m, "MFLC capacity should be positive");
        Assert.IsFalse(string.IsNullOrEmpty(result.BlackHoleRiskLevel));

        // 验证单资产穿透
        Assert.AreEqual(components.Count, result.AssetItemList.Count);
        foreach (var asset in result.AssetItemList)
        {
            Assert.IsFalse(string.IsNullOrEmpty(asset.FundCode));
            Assert.IsTrue(asset.MarketDepthElasticity > 0m);
            Assert.IsTrue(asset.DirectSellingPressureTenThousand >= 0m);
            Assert.IsTrue(asset.TotalLiquidationVolumeTenThousand >= asset.DirectSellingPressureTenThousand);
            Assert.IsTrue(asset.FireSaleCascadeRatio >= 1.0m);
            Assert.IsFalse(string.IsNullOrEmpty(asset.VulnerabilityStatusBadge));
        }

        Assert.IsTrue(result.LiquidityBlackHoleExecutiveVerdict.Contains("MSCI Barra 内生流动性黑洞弹性审定"));
    }

    [TestMethod]
    public void Test_SharpeDecayAndCusumDrift_FitsExponentialHalfLifeAndTracksCusum()
    {
        var components = CreateSampleComponents();
        var dailyReturns = ComputePortfolioReturns(components);

        var result = QuantCalculator.CalculateSharpeDecayAndCusumDrift(dailyReturns, 2.0m);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.ExponentialDecayRateLambda >= 0m, "Decay rate lambda should be non-negative");
        Assert.IsTrue(result.SharpeDecayHalfLifeDays > 0m, "Half-life days must be positive");
        Assert.IsTrue(result.EstimatedDaysToTerminalExpiration > 0m, "Days to expiration must be positive");
        Assert.IsTrue(result.CusumAlertThreshold > 0m, "CUSUM threshold h must be positive");
        Assert.IsTrue(result.CusumPositiveAccumulator >= 0m, "CUSUM S+ accumulator must be non-negative");
        Assert.IsTrue(result.CusumNegativeAccumulator >= 0m, "CUSUM S- accumulator must be non-negative");
        Assert.IsFalse(string.IsNullOrEmpty(result.AlphaLongevityStatusBadge));

        Assert.IsTrue(result.CusumDecayExecutiveVerdict.Contains("Lopez de Prado 策略夏普衰减与 CUSUM"));
    }

    [TestMethod]
    public void Test_PortfolioEngine_Phase33PipelineIntegration_PopulatesAllPhase33Results()
    {
        var components = CreateSampleComponents();

        // 运行完整投资组合分析引擎
        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.BabQmjFactorDecomposition, "Phase 33 BabQmjFactorDecomposition must be populated");
        Assert.IsNotNull(result.EvtGeneralizedPareto, "Phase 33 EvtGeneralizedPareto must be populated");
        Assert.IsNotNull(result.LiquidityBlackHole, "Phase 33 LiquidityBlackHole must be populated");
        Assert.IsNotNull(result.SharpeDecayCusum, "Phase 33 SharpeDecayCusum must be populated");

        // 验证 HTML 研报导出包含 Phase 33 四大核心章节
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, components);
        Assert.IsFalse(string.IsNullOrEmpty(html));
        Assert.IsTrue(html.Contains("三十四、AQR (Asness, Frazzini & Pedersen) 杠杆约束异象 Betting Against Beta (BAB) 与高质量多空 (QMJ) 因子解构 (Phase 33)"), "HTML report must contain AQR BAB/QMJ section");
        Assert.IsTrue(html.Contains("三十五、BlackRock Aladdin 极值理论 POT 广义帕累托 (GPD) 尾部外推与极端重现期风险测度 (Phase 33)"), "HTML report must contain Aladdin EVT GPD section");
        Assert.IsTrue(html.Contains("三十六、MSCI Barra & Brunnermeier-Pedersen 内生流动性黑洞弹性 (LBHI) 与资产踩踏抛售反馈乘数 (Phase 33)"), "HTML report must contain MSCI Barra Liquidity Black Hole section");
        Assert.IsTrue(html.Contains("三十七、Marcos Lopez de Prado 策略微观夏普衰减半衰期 (Sharpe Half-Life) 与 CUSUM 概念漂移滤波检验 (Phase 33)"), "HTML report must contain Lopez de Prado Sharpe Half-Life section");
    }
}
