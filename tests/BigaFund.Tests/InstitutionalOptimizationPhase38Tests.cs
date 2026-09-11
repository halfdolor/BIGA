using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase38Tests
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
            Name = "易方达中小盘混合 (顺周期高成长)",
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
            Name = "华夏纯债中短债A (纯债久期防御)",
            FundSize = "65.2亿元",
            NavHistory = GenerateSyntheticNav(baseDate, 500, 0.046, 0.029, seed: 333)
        };
        var fundD = new FundDetail
        {
            Code = "161715",
            Name = "招商中证白酒指数 (消费估值大宗)",
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
    public void Test_Phase38_CrossAssetTermStructureCarry()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.12, 0.16, 888);

        var result = QuantCalculator.CalculateCrossAssetTermStructureCarry(components, returns);

        Assert.IsNotNull(result);
        Assert.AreEqual(components.Count, result.AssetCarries.Count, "Should calculate Carry for all components");
        Assert.AreEqual(components.Count, result.BackwardationAssetCount + result.ContangoAssetCount, "Backwardation + Contango should equal total assets");
        Assert.IsTrue(result.CarrySharpeUplift >= 0m, "Carry Sharpe Uplift should be non-negative");
        Assert.IsTrue(result.BasisMomentumAlphaRatioPercent >= 0m, "Basis Momentum Alpha ratio should be non-negative");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict), "ExecutiveVerdict should not be empty");

        decimal totalCarryWeight = result.AssetCarries.Sum(a => a.RecommendedCarryWeightPercent);
        Assert.IsTrue(Math.Abs(totalCarryWeight - 100m) < 1.0m, $"Recommended Carry weights should sum to approx 100%, got {totalCarryWeight}");

        foreach (var ac in result.AssetCarries)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(ac.Code));
            Assert.IsFalse(string.IsNullOrWhiteSpace(ac.Name));
            Assert.IsTrue(ac.FrontPrice > 0m, "Front price should be positive");
            Assert.IsTrue(ac.NextPrice > 0m, "Next price should be positive");
            Assert.IsTrue(ac.RealizedVolatilityPercent > 0m, "Realized volatility should be positive");
            Assert.IsTrue(ac.RecommendedCarryWeightPercent >= 0m, "Recommended weight should be non-negative");
            Assert.IsFalse(string.IsNullOrWhiteSpace(ac.CarryRegimeBadge));
        }
    }

    [TestMethod]
    public void Test_Phase38_RmtSpectralFilteringAndNoiseCleaning()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.11, 0.15, 999);

        var result = QuantCalculator.CalculateRmtSpectralFilteringAndNoiseCleaning(components, returns);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.GammaRatio > 1.0m, $"Q (T/N) ratio should be > 1.0, got {result.GammaRatio}");
        Assert.IsTrue(result.MarchenkoPasturUpperBound > result.MarchenkoPasturLowerBound, "MP upper bound should exceed lower bound");
        Assert.IsTrue(result.MarchenkoPasturLowerBound >= 0m, "MP lower bound should be non-negative");
        Assert.AreEqual(result.EigenModes.Count, result.SignalEigenvalueCount + result.NoiseEigenvalueCount, "Total modes must equal Signal + Noise count");
        Assert.IsTrue(result.NoiseVariancePurifiedPercent > 0m, "Noise variance purified percent should be positive");
        Assert.IsTrue(result.ConditionNumberImprovementRatioPercent > 0m, "Condition number improvement ratio should be positive");
        Assert.IsTrue(result.DenoisedConditionNumber <= result.RawConditionNumber, "Denoised condition number must not exceed raw condition number");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));

        foreach (var em in result.EigenModes)
        {
            Assert.IsTrue(em.Rank >= 1);
            Assert.IsTrue(em.EmpiricalEigenvalue > 0m);
            Assert.IsTrue(em.FilteredEigenvalue > 0m);
            Assert.IsTrue(em.VarianceExplainedPercent > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(em.ModeClassification));
        }
    }

    [TestMethod]
    public void Test_Phase38_MultivariateTailCoCrashAndFragilityNetwork()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.13, 0.18, 123);

        var result = QuantCalculator.CalculateMultivariateTailCoCrashAndFragilityNetwork(components, returns);

        Assert.IsNotNull(result);
        Assert.AreEqual(components.Count, result.AssetFragilities.Count, "Should calculate fragility for each component");
        Assert.IsTrue(result.PortfolioLowerTailCoCrashProbabilityPercent >= 0m, "Tail co-crash probability should be non-negative");
        Assert.IsTrue(result.AsymmetricDownsideTailDominanceRatio > 0m, "Asymmetry ratio should be positive");
        Assert.IsTrue(result.StructuralFragilityIndex >= 0m && result.StructuralFragilityIndex <= 100m, "SFI must be within [0, 100]");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TailConvexityDefenseRating));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));

        foreach (var af in result.AssetFragilities)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(af.Code));
            Assert.IsFalse(string.IsNullOrWhiteSpace(af.Name));
            Assert.IsTrue(af.AverageLowerTailDependence >= 0m && af.AverageLowerTailDependence <= 1m);
            Assert.IsTrue(af.AverageUpperTailDependence >= 0m && af.AverageUpperTailDependence <= 1m);
            Assert.IsTrue(af.AsymmetryRatio > 0m);
            Assert.IsTrue(af.TailGraphCentralityScore >= 0m && af.TailGraphCentralityScore <= 100m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(af.TailRiskConvexityBadge));
        }
    }

    [TestMethod]
    public void Test_Phase38_MicrostructureOfiAndAdverseSelection()
    {
        var components = CreateSampleComponents();
        var returns = CreateSampleDailyReturns(250, 0.10, 0.14, 456);

        var result = QuantCalculator.CalculateMicrostructureOfiAndAdverseSelection(components, returns);

        Assert.IsNotNull(result);
        Assert.AreEqual(components.Count, result.AssetMicrostructures.Count, "Should calculate adverse selection for all components");
        Assert.IsTrue(result.PortfolioAverageKyleLambdaBps > 0m, "Kyle lambda should be positive");
        Assert.IsTrue(result.PermanentAlphaSharePercent > 0m && result.PermanentAlphaSharePercent <= 100m, "Hasbrouck info share must be in (0, 100]");
        Assert.IsTrue(result.PortfolioVpinToxicityIndex >= 0m && result.PortfolioVpinToxicityIndex <= 100m, "VPIN index must be in [0, 100]");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutionSpeedGuidance));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveVerdict));

        foreach (var ma in result.AssetMicrostructures)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(ma.Code));
            Assert.IsFalse(string.IsNullOrWhiteSpace(ma.Name));
            Assert.IsTrue(ma.KyleLambdaBps > 0m);
            Assert.IsTrue(ma.HasbrouckPermanentInfoSharePercent > 0m && ma.HasbrouckPermanentInfoSharePercent <= 100m);
            Assert.IsTrue(ma.VpinToxicityScore >= 0m && ma.VpinToxicityScore <= 100m);
            Assert.IsTrue(ma.AdverseSelectionCostBps > 0m);
            Assert.IsTrue(ma.InventoryHoldingFrictionBps > 0m);
            Assert.IsFalse(string.IsNullOrWhiteSpace(ma.ExecutionAggressionRecommendation));
        }
    }

    [TestMethod]
    public void Test_Phase38_PortfolioEngine_Integration_And_Export()
    {
        var components = CreateSampleComponents();

        var engineResult = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(engineResult);
        Assert.IsNotNull(engineResult.TermStructureCarry, "TermStructureCarry should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.RmtSpectralFiltering, "RmtSpectralFiltering should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.MultivariateTailCoCrash, "MultivariateTailCoCrash should be populated in PortfolioEngine");
        Assert.IsNotNull(engineResult.MicrostructureAdverseSelection, "MicrostructureAdverseSelection should be populated in PortfolioEngine");

        // Verify institutional due diligence HTML report generation
        string htmlReport = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(engineResult, components);
        Assert.IsFalse(string.IsNullOrWhiteSpace(htmlReport));
        Assert.IsTrue(htmlReport.Contains("五十四、Man Group AHL & AQR 跨资产大类期限结构展期收益 (Roll Yield / Carry) 与基差动量收割 (Phase 38)"), "Report should contain Phase 38.1 section");
        Assert.IsTrue(htmlReport.Contains("五十五、Renaissance Technologies & CFM 随机矩阵理论 (RMT) 马尔琴科-帕斯图尔谱分解滤波降噪 (Phase 38)"), "Report should contain Phase 38.2 section");
        Assert.IsTrue(htmlReport.Contains("五十六、Citadel & Millennium 多元非对称极值下行联结与组合结构性协同崩塌脆弱度矩阵 (Phase 38)"), "Report should contain Phase 38.3 section");
        Assert.IsTrue(htmlReport.Contains("五十七、Jane Street & Citadel Securities 微观订单流不平衡 (OFI)、Kyle 价格冲击信息份额与逆向选择足迹 (Phase 38)"), "Report should contain Phase 38.4 section");
    }
}
