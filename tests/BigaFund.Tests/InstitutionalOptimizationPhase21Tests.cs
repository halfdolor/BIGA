using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase21Tests
{
    private static List<NavRecord> GenerateNavSeries(DateTime start, int days, double dailyMean, double dailyStd)
    {
        var list = new List<NavRecord>();
        double nav = 1.0;
        var rng = new Random(54321);

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
            double ret = dailyMean + dailyStd * z;
            nav *= (1.0 + ret);
        }
        return list;
    }

    [TestMethod]
    public void Test_FactorRiskDecomposition_EulerIdentityAndVarianceDecomposition()
    {
        var fund1 = new FundDetail { Code = "000001", Name = "华夏大盘精选", Type = "偏股混合型" };
        var fund2 = new FundDetail { Code = "000002", Name = "易方达稳健收益", Type = "纯债债券型" };
        var fund3 = new FundDetail { Code = "000003", Name = "国泰大宗商品", Type = "商品黄金型" };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fund1, 50m),
            (fund2, 35m),
            (fund3, 15m)
        };

        var result = QuantCalculator.DecomposeFactorRisk(components);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.PortfolioTotalVolatility > 0m, "Portfolio volatility must be positive");
        Assert.IsTrue(result.FactorRiskItems.Count >= 7, "Must contain 6 systematic style factors + 1 specific risk item");

        decimal sumFpcr = result.FactorRiskItems.Sum(x => x.FactorPercentageContributionToRisk);
        Assert.AreEqual(100.0, (double)sumFpcr, 0.5, "Euler percentage contributions must sum to 100%");

        decimal sumSysAndSpec = result.SystematicRiskVariancePercent + result.SpecificRiskVariancePercent;
        Assert.AreEqual(100.0, (double)sumSysAndSpec, 0.5, "Systematic + Specific variance must sum to 100%");

        Assert.IsTrue(result.FactorRiskHerfindahlIndex > 0m, "HHI must be positive");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.FactorDiversificationGrade), "Factor diversification grade should be set");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveDiagnosis), "Executive diagnosis must be generated");
    }

    [TestMethod]
    public void Test_FactorRiskParity_WeightsSumTo100AndBounded()
    {
        var fund1 = new FundDetail { Code = "110011", Name = "易方达中小盘", Type = "偏股混合型" };
        var fund2 = new FundDetail { Code = "000100", Name = "富国纯债", Type = "纯债债券型" };
        var fund3 = new FundDetail { Code = "160216", Name = "国泰黄金ETF", Type = "商品型" };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fund1, 70m),
            (fund2, 20m),
            (fund3, 10m)
        };

        var optimalWeights = QuantCalculator.SolveFactorRiskParityWeights(components);

        Assert.IsNotNull(optimalWeights);
        Assert.AreEqual(3, optimalWeights.Count);

        decimal totalW = optimalWeights.Sum(x => x.OptimalFrpWeightPercent);
        Assert.AreEqual(100.0m, Math.Round(totalW, 1), "Optimal FRP weights must sum strictly to 100%");

        foreach (var item in optimalWeights)
        {
            Assert.IsTrue(item.OptimalFrpWeightPercent >= 5.0m, $"Weight for {item.FundCode} should be >= 5%");
            Assert.IsTrue(item.OptimalFrpWeightPercent <= 50.0m, $"Weight for {item.FundCode} should be <= 50%");
        }
    }

    [TestMethod]
    public void Test_LdiImmunization_CashFlowDiscountingAndFundingRatio()
    {
        var fundBond = new FundDetail { Code = "001001", Name = "招商产业债", Type = "纯债债券型" };
        var fundEq = new FundDetail { Code = "002002", Name = "汇添富蓝筹", Type = "股票型" };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fundBond, 80m),
            (fundEq, 20m)
        };

        decimal assetAum = 120.0m;
        var ldi = PortfolioEngine.EvaluateLdiImmunization(components, assetAum, baseDiscountRate: 2.50m);

        Assert.IsNotNull(ldi);
        Assert.AreEqual(assetAum, ldi.AssetTotalMarketValue);
        Assert.AreEqual(10, ldi.LiabilityStream.Count, "Should model 10-year liability cash flow stream");

        Assert.IsTrue(ldi.LiabilityTotalPresentValue > 0m, "Liability PV must be positive");
        Assert.IsTrue(ldi.LiabilityMacaulayDurationYears > 3.0m && ldi.LiabilityMacaulayDurationYears < 8.0m, "Liability duration should be realistic");

        Assert.IsTrue(ldi.AssetMacaulayDurationYears > 0m, "Asset duration should be positive");
        Assert.IsTrue(ldi.FundingRatio > 0m, "Funding ratio must be positive");

        decimal expectedFundingRatio = (assetAum / ldi.LiabilityTotalPresentValue) * 100m;
        Assert.AreEqual((double)expectedFundingRatio, (double)ldi.FundingRatio, 0.5);
    }

    [TestMethod]
    public void Test_LdiImmunization_RedingtonImmunizationCheck()
    {
        var fundLongBond = new FundDetail { Code = "003001", Name = "超长国债ETF联接", Type = "长期纯债" };
        var compImmunized = new List<(FundDetail Fund, decimal Weight)>
        {
            (fundLongBond, 100m)
        };

        var ldiImmunized = PortfolioEngine.EvaluateLdiImmunization(compImmunized, portfolioAumBillions: 120.0m);
        Assert.IsTrue(ldiImmunized.FundingRatio >= 100m);

        var fundCash = new FundDetail { Code = "004001", Name = "广发微金货币", Type = "货币型" };
        var compMismatch = new List<(FundDetail Fund, decimal Weight)>
        {
            (fundCash, 100m)
        };

        var ldiMismatch = PortfolioEngine.EvaluateLdiImmunization(compMismatch, portfolioAumBillions: 100.0m);
        Assert.IsFalse(ldiMismatch.IsRedingtonImmunized, "Cash portfolio should not satisfy Redington immunization against long liabilities");
        Assert.IsTrue(ldiMismatch.DurationMismatchYears < -2.0m, "Cash asset duration should be far lower than liability duration");
    }

    [TestMethod]
    public void Test_LdiImmunization_YieldCurveShocks_StressScenarios()
    {
        var fund = new FundDetail { Code = "005001", Name = "交银信用添利", Type = "一级债基" };
        var components = new List<(FundDetail Fund, decimal Weight)> { (fund, 100m) };

        var ldi = PortfolioEngine.EvaluateLdiImmunization(components, portfolioAumBillions: 105.0m);

        Assert.IsNotNull(ldi.StressShocks);
        Assert.AreEqual(5, ldi.StressShocks.Count, "Must evaluate 5 rate shock scenarios");

        var baseline = ldi.StressShocks.First(s => s.InterestRateShiftBps == 0m);
        Assert.AreEqual(0.0m, baseline.SolvencySurplusDelta, "Surplus delta at 0bps shift must be zero");

        var down100 = ldi.StressShocks.First(s => s.InterestRateShiftBps == -100m);
        var up100 = ldi.StressShocks.First(s => s.InterestRateShiftBps == 100m);

        Assert.IsTrue(down100.StressedAssetValue > ldi.AssetTotalMarketValue, "Asset value should increase when interest rate drops");
        Assert.IsTrue(up100.StressedAssetValue < ldi.AssetTotalMarketValue, "Asset value should decrease when interest rate rises");
    }

    [TestMethod]
    public void Test_TieredFeeDynamicRebalance_ToleranceBandFiltersAndHoldingAge()
    {
        var fund1 = new FundDetail { Code = "163402", Name = "兴全趋势投资", Type = "偏股混合型", QuantMetrics = new QuantMetrics { TotalReturn = 25m } };
        var fund2 = new FundDetail { Code = "000080", Name = "银华富裕主题", Type = "股票型", QuantMetrics = new QuantMetrics { TotalReturn = 2m } };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fund1, 50m),
            (fund2, 50m)
        };

        decimal toleranceBand = 5.0m;
        decimal capital = 1000m;
        var rebalance = PortfolioEngine.SimulateTieredFeeDynamicRebalance(components, toleranceBand, capital);

        Assert.IsNotNull(rebalance);
        Assert.AreEqual(2, rebalance.TotalComponentsCount);
        Assert.AreEqual(2, rebalance.Items.Count);

        Assert.IsTrue(rebalance.CalendarTurnoverVolume > 0m, "Calendar turnover should calculate all drifted volume");
        Assert.IsTrue(rebalance.TotalTurnoverVolume <= rebalance.CalendarTurnoverVolume, "Tolerance band rebalance must strictly reduce or equal turnover volume");
        Assert.IsTrue(rebalance.AvoidedPunitiveFeesTotal >= 0m, "Avoided punitive fees should be non-negative");

        foreach (var item in rebalance.Items)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.TradeDirection));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.ActionAdvice));
            if (item.IsBandBreached)
            {
                Assert.IsTrue(item.ExecutedTradeAmount > 0m, "Breached item should execute trades");
            }
            else
            {
                Assert.AreEqual(0m, item.ExecutedTradeAmount, "Unbreached item should have 0 executed trade amount");
            }
        }
    }

    [TestMethod]
    public void Test_CalculatePortfolio_EndToEndPhase21Integration()
    {
        var navDate = new DateTime(2023, 1, 1);
        var f1 = new FundDetail
        {
            Code = "000001",
            Name = "华夏成长",
            Type = "混合型",
            NavHistory = GenerateNavSeries(navDate, 300, 0.0004, 0.015)
        };
        var f2 = new FundDetail
        {
            Code = "000002",
            Name = "易方达中债",
            Type = "债券型",
            NavHistory = GenerateNavSeries(navDate, 300, 0.0001, 0.003)
        };
        var f3 = new FundDetail
        {
            Code = "000003",
            Name = "易方达标普500",
            Type = "QDII",
            NavHistory = GenerateNavSeries(navDate, 300, 0.0003, 0.012)
        };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (f1, 40m),
            (f2, 40m),
            (f3, 20m)
        };

        var result = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.FactorRiskParity, "PortfolioResult.FactorRiskParity must be populated");
        Assert.IsNotNull(result.LdiImmunization, "PortfolioResult.LdiImmunization must be populated");
        Assert.IsNotNull(result.TieredFeeRebalance, "PortfolioResult.TieredFeeRebalance must be populated");

        // Verify FRP items
        Assert.IsTrue(result.FactorRiskParity.FactorRiskItems.Count >= 7);
        Assert.IsTrue(result.FactorRiskParity.OptimalWeights.Count == 3);

        // Verify LDI
        Assert.AreEqual(10, result.LdiImmunization.LiabilityStream.Count);
        Assert.AreEqual(5, result.LdiImmunization.StressShocks.Count);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.LdiImmunization.ExecutiveAdvice));

        // Verify Tiered Fee Rebalance
        Assert.AreEqual(3, result.TieredFeeRebalance.Items.Count);
        Assert.IsTrue(result.TieredFeeRebalance.TurnoverReductionRatePercent >= 0m);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TieredFeeRebalance.FrictionOptimizationSummary));
    }
}
