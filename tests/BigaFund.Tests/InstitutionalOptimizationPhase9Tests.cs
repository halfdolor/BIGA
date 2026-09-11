using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase9Tests
{
    private (List<NavRecord> navs, List<BenchmarkRecord> bmks) CreateSynthesizedFundAndBenchmark(int days = 300, double dailyDrift = 0.0005, double dailyVol = 0.015)
    {
        var navs = new List<NavRecord>();
        var bmks = new List<BenchmarkRecord>();
        var baseDate = new DateTime(2023, 1, 1);

        decimal currentNav = 1.0m;
        double cumBmkRet = 0.0;

        for (int i = 0; i < days; i++)
        {
            var date = baseDate.AddDays(i);
            // 构造具备非对称与厚尾特性的收益率序列
            double fatTailShock = (i % 37 == 0) ? -0.045 : ((i % 43 == 0) ? 0.035 : 0.0);
            double fundRet = dailyDrift + dailyVol * Math.Sin(i * 0.2) * 0.7 + fatTailShock;
            double bmkRet = 0.0002 + 0.010 * Math.Sin(i * 0.15) * 0.5 + (i % 11 == 0 ? -0.015 : 0.001);
            cumBmkRet += bmkRet * 100.0;

            currentNav *= (decimal)(1.0 + fundRet);

            navs.Add(new NavRecord
            {
                Date = date,
                UnitNav = Math.Round(currentNav, 4),
                CumulativeNav = Math.Round(currentNav, 4),
                DailyReturn = Math.Round((decimal)fundRet * 100m, 2)
            });

            bmks.Add(new BenchmarkRecord
            {
                Date = date,
                CumulativeReturnRate = Math.Round((decimal)cumBmkRet, 2)
            });
        }

        return (navs, bmks);
    }

    [TestMethod]
    public void Test_QuantCalculator_TailRiskMetrics_CornishFisher()
    {
        var (navs, bmks) = CreateSynthesizedFundAndBenchmark(260);

        var tailRisk = QuantCalculator.CalculateTailRiskMetrics(navs);

        Assert.IsNotNull(tailRisk, "Tail risk metrics should not be null");
        Assert.IsTrue(tailRisk.CornishFisherVaR95 > 0m, $"95% mVaR should be positive, got {tailRisk.CornishFisherVaR95}");
        Assert.IsTrue(tailRisk.CornishFisherCVaR95 >= tailRisk.CornishFisherVaR95,
            $"95% mCVaR ({tailRisk.CornishFisherCVaR95}) must be >= 95% mVaR ({tailRisk.CornishFisherVaR95})");

        Assert.IsTrue(tailRisk.CornishFisherVaR99 >= tailRisk.CornishFisherVaR95,
            $"99% mVaR ({tailRisk.CornishFisherVaR99}) must be >= 95% mVaR ({tailRisk.CornishFisherVaR95})");
        Assert.IsTrue(tailRisk.CornishFisherCVaR99 >= tailRisk.CornishFisherCVaR95,
            $"99% mCVaR ({tailRisk.CornishFisherCVaR99}) must be >= 95% mCVaR ({tailRisk.CornishFisherCVaR95})");

        Assert.IsTrue(tailRisk.Basel10DayVaR99 > tailRisk.CornishFisherVaR95,
            $"Basel III 10-day 99% VaR ({tailRisk.Basel10DayVaR99}) should be substantially higher than 1-day 95% mVaR ({tailRisk.CornishFisherVaR95})");

        Assert.IsFalse(string.IsNullOrWhiteSpace(tailRisk.TailFatnessRating), "Tail fatness rating should be assigned");
        Assert.IsFalse(string.IsNullOrWhiteSpace(tailRisk.DiagnosticSummary), "Tail risk diagnostic summary should be generated");
    }

    [TestMethod]
    public void Test_QuantCalculator_UnderwaterAnalysis()
    {
        var (navs, _) = CreateSynthesizedFundAndBenchmark(200);

        var underwater = QuantCalculator.CalculateUnderwaterAnalysis(navs);

        Assert.IsNotNull(underwater, "Underwater analysis result should not be null");
        Assert.AreEqual(200, underwater.TotalDays, "Total days should match input count");
        Assert.AreEqual(200, underwater.UnderwaterSeries.Count, "Underwater series points should match input count");

        Assert.IsTrue(underwater.UnderwaterTimeRatio >= 0m && underwater.UnderwaterTimeRatio <= 100m,
            $"Underwater time ratio should be between 0% and 100%, got {underwater.UnderwaterTimeRatio}");
        Assert.IsTrue(underwater.UnderwaterDays >= 0 && underwater.UnderwaterDays <= underwater.TotalDays);
        Assert.IsTrue(underwater.MaxUnderwaterDays >= 0, "Max underwater days should be non-negative");
        Assert.IsTrue(underwater.CurrentUnderwaterDays >= 0, "Current underwater days should be non-negative");
        Assert.IsTrue(underwater.MaxUnderwaterDays >= underwater.CurrentUnderwaterDays, "Max underwater streak must be >= current streak");
        Assert.IsTrue(underwater.AverageUnderwaterDepth >= 0m, "Average underwater depth must be >= 0");
        Assert.IsTrue(underwater.PainIndex >= 0m, "Pain index must be >= 0");
        Assert.IsFalse(string.IsNullOrWhiteSpace(underwater.DiagnosticSummary), "Underwater summary should be non-empty");

        // 验证离散时序点数据
        foreach (var pt in underwater.UnderwaterSeries)
        {
            Assert.IsTrue(pt.UnderwaterPercent <= 0.001m, "Underwater drawdown must be non-positive");
            Assert.IsTrue(pt.CurrentNav <= pt.PeakNav + 0.0001m, "Current NAV cannot exceed peak NAV");
        }
    }

    [TestMethod]
    public void Test_QuantCalculator_FamaFrench5Attribution()
    {
        var (navs, bmks) = CreateSynthesizedFundAndBenchmark(260);

        var fund = new FundDetail
        {
            Code = "000001",
            Name = "华夏成长混合",
            Type = "混合型-偏股",
            NavHistory = navs
        };

        var ff5 = QuantCalculator.CalculateFamaFrench5Attribution(fund, bmks, 2.0m);

        Assert.IsNotNull(ff5, "FF5 result should not be null");
        Assert.AreEqual(5, ff5.FactorItems.Count, "FF5 should produce 5 factor items (MKT, SMB, HML, RMW, CMA)");

        var factorIds = ff5.FactorItems.Select(f => f.FactorId).ToList();
        CollectionAssert.Contains(factorIds, "MKT");
        CollectionAssert.Contains(factorIds, "SMB");
        CollectionAssert.Contains(factorIds, "HML");
        CollectionAssert.Contains(factorIds, "RMW");
        CollectionAssert.Contains(factorIds, "CMA");

        foreach (var factor in ff5.FactorItems)
        {
            Assert.IsTrue(factor.PValue >= 0m && factor.PValue <= 1.0m, $"p-value for {factor.FactorId} should be in [0, 1], got {factor.PValue}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(factor.FactorEvaluation));
            Assert.IsFalse(string.IsNullOrWhiteSpace(factor.FactorCategory));
        }

        Assert.IsTrue(ff5.RSquared >= 0m && ff5.RSquared <= 1.0m, $"RSquared should be between 0 and 1, got {ff5.RSquared}");
        Assert.IsTrue(ff5.ResidualRiskPercent >= 0m && ff5.ResidualRiskPercent <= 100m);
        Assert.IsFalse(string.IsNullOrWhiteSpace(ff5.DominantFactor), "Dominant factor should be identified");
        Assert.IsFalse(string.IsNullOrWhiteSpace(ff5.AttributionSummary), "Attribution summary should be generated");
    }

    [TestMethod]
    public void Test_PortfolioEngine_HierarchicalRiskParity_And_Diversification()
    {
        var fundA = new FundDetail { Code = "BOND01", Name = "稳健债券A", Type = "债券型-纯债" };
        var fundB = new FundDetail { Code = "TECH02", Name = "科技先锋B", Type = "股票型" };
        var fundC = new FundDetail { Code = "VALUE03", Name = "红利价值C", Type = "混合型-偏股" };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 33.3m),
            (fundB, 33.3m),
            (fundC, 33.4m)
        };

        // 构造 3 个资产的 100 天收益序列
        var rng = new Random(42);
        var retsA = new List<double>();
        var retsB = new List<double>();
        var retsC = new List<double>();

        for (int i = 0; i < 100; i++)
        {
            retsA.Add(0.0001 + 0.002 * (rng.NextDouble() - 0.5)); // 低波动
            retsB.Add(0.0005 + 0.020 * (rng.NextDouble() - 0.5)); // 高波动
            retsC.Add(0.0003 + 0.009 * (rng.NextDouble() - 0.5)); // 中波动
        }

        var individualReturns = new Dictionary<string, List<double>>
        {
            ["BOND01"] = retsA,
            ["TECH02"] = retsB,
            ["VALUE03"] = retsC
        };

        // 1. 测试直接调用 HRP 求解器
        double[,] covMatrix = new double[3, 3]
        {
            { 0.001, 0.0002, 0.0001 },
            { 0.0002, 0.040, 0.008 },
            { 0.0001, 0.008, 0.015 }
        };
        var hrpWeights = PortfolioEngine.SolveHierarchicalRiskParity(covMatrix, 3, new List<string> { "BOND01", "TECH02", "VALUE03" });
        Assert.IsNotNull(hrpWeights);
        Assert.AreEqual(3, hrpWeights.Length);
        double sumWeights = hrpWeights.Sum();
        Assert.IsTrue(Math.Abs(sumWeights - 1.0) < 0.01, $"HRP weights should sum to 1.0, got {sumWeights}");
        // 低波动债券权重应显著高于高波动资产
        Assert.IsTrue(hrpWeights[0] > hrpWeights[1], "Low-vol asset should receive higher weight in HRP than high-vol asset");

        // 2. 测试 CalculateOptimizationSchemes 中包含第 6 套 HRP 方案
        var portResult = new PortfolioResult();
        PortfolioEngine.CalculateOptimizationSchemes(portResult, components, individualReturns, 2.0);

        Assert.IsNotNull(portResult.Schemes);
        Assert.IsTrue(portResult.Schemes.Count >= 6, "Should calculate at least 6 optimization schemes including HRP");

        var hrpScheme = portResult.Schemes.FirstOrDefault(s => s.SchemeName.Contains("HRP"));
        Assert.IsNotNull(hrpScheme, "HRP scheme should exist in Schemes list");
        Assert.IsTrue(hrpScheme.Weights.Count == 3);

        // 3. 测试 Choueifaty Diversification Ratio & 同质化诊断
        var divResult = PortfolioEngine.CalculatePortfolioDiversification(components, individualReturns, 0.12);
        Assert.IsNotNull(divResult);
        Assert.IsTrue(divResult.DiversificationRatio >= 1.0m, $"Diversification ratio should be >= 1.0, got {divResult.DiversificationRatio}");
        Assert.IsTrue(divResult.VolatilityReductionPercent >= 0m);
        Assert.IsTrue(divResult.HomogeneityScore >= 0m && divResult.HomogeneityScore <= 100m);
        Assert.IsFalse(string.IsNullOrWhiteSpace(divResult.DiagnosticSummary));
    }

    [TestMethod]
    public void Test_ExportService_Phase9_CSV_And_HTML()
    {
        var (navs, bmks) = CreateSynthesizedFundAndBenchmark(200);

        var fund = new FundDetail
        {
            Code = "163402",
            Name = "兴全趋势投资混合",
            Type = "混合型-偏股",
            ManagerName = "董承非",
            NavHistory = navs
        };

        var metrics = QuantCalculator.CalculateMetrics(navs, "全周期", bmks, 2.0m);
        metrics.FamaFrenchResult = QuantCalculator.CalculateFamaFrench5Attribution(fund, bmks, 2.0m);
        metrics.UnderwaterAnalysis = QuantCalculator.CalculateUnderwaterAnalysis(navs);
        metrics.TailRisk = QuantCalculator.CalculateTailRiskMetrics(navs);

        fund.FamaFrenchResult = metrics.FamaFrenchResult;
        fund.UnderwaterAnalysis = metrics.UnderwaterAnalysis;

        var backtest = new BacktestResult
        {
            StrategyName = "普通定投",
            TotalPeriods = 20,
            PeriodicAmount = 1000m,
            TotalInvested = 20000m,
            FinalAssetValue = 22500m,
            AnnualizedIrr = 8.2m
        };

        string tempCsv = Path.Combine(Path.GetTempPath(), $"biga_test_phase9_{Guid.NewGuid():N}.csv");
        string tempHtml = Path.Combine(Path.GetTempPath(), $"biga_test_phase9_{Guid.NewGuid():N}.html");

        try
        {
            // 验证 CSV 导出包含 Phase 9 字段
            ExportService.ExportToCsv(tempCsv, fund, metrics, backtest, navs);
            string csvContent = File.ReadAllText(tempCsv);
            StringAssert.Contains(csvContent, "Cornish-Fisher");
            StringAssert.Contains(csvContent, "mVaR 95%");
            StringAssert.Contains(csvContent, "水下套牢时间占比");
            StringAssert.Contains(csvContent, "Fama-French 五因子");

            // 验证 HTML 导出包含 Phase 9 标签
            ExportService.ExportToHtml(tempHtml, fund, metrics, backtest, navs);
            string htmlContent = File.ReadAllText(tempHtml);
            StringAssert.Contains(htmlContent, "Cornish-Fisher 展开式");
            StringAssert.Contains(htmlContent, "全历史水下套牢深度解构");
            StringAssert.Contains(htmlContent, "Fama-French 五因子资产定价模型");

            // 验证组合 HTML 导出
            var portfolioResult = new PortfolioResult
            {
                TotalReturn = 18.5m,
                AnnualizedReturn = 12.0m,
                AnnualizedVolatility = 14.2m,
                MaxDrawdown = -11.5m,
                SharpeRatio = 1.35m,
                Diversification = new PortfolioDiversificationResult
                {
                    DiversificationRatio = 1.45m,
                    VolatilityReductionPercent = 31.0m,
                    WeightedAverageCorrelation = 0.32m,
                    HomogeneityScore = 32m,
                    PseudoDiversificationWarning = false,
                    ClusterGroups = new List<string> { "簇 1: [000001, 110011]", "簇 2: [163402]" }
                }
            };

            string tempPortHtml = Path.Combine(Path.GetTempPath(), $"biga_test_port_phase9_{Guid.NewGuid():N}.html");
            try
            {
                var components = new List<(FundDetail Fund, decimal WeightPercent)>
                {
                    (fund, 100m)
                };
                ExportService.ExportPortfolioToHtml(tempPortHtml, portfolioResult, components);
                string portHtmlContent = File.ReadAllText(tempPortHtml);
                StringAssert.Contains(portHtmlContent, "Choueifaty DR");
                StringAssert.Contains(portHtmlContent, "同质化得分");
                StringAssert.Contains(portHtmlContent, "HRP Clusters");
            }
            finally
            {
                if (File.Exists(tempPortHtml)) File.Delete(tempPortHtml);
            }
        }
        finally
        {
            if (File.Exists(tempCsv)) File.Delete(tempCsv);
            if (File.Exists(tempHtml)) File.Delete(tempHtml);
        }
    }

    [TestMethod]
    public void Test_QuantMetrics_WpfBindingWritableCompatibility()
    {
        var metricsType = typeof(QuantMetrics);
        string[] propertiesToCheck = new[]
        {
            nameof(QuantMetrics.CornishFisherVaR95),
            nameof(QuantMetrics.CornishFisherCVaR95),
            nameof(QuantMetrics.Skewness),
            nameof(QuantMetrics.ExcessKurtosis),
            nameof(QuantMetrics.CornishFisherVaR99),
            nameof(QuantMetrics.CornishFisherCVaR99),
            nameof(QuantMetrics.ModifiedVaR95),
            nameof(QuantMetrics.ModifiedCVaR95),
            nameof(QuantMetrics.ExcessReturn),
            nameof(QuantMetrics.UnderwaterTimeRatio),
            nameof(QuantMetrics.MaxUnderwaterDays),
            nameof(QuantMetrics.AverageUnderwaterDepth),
            nameof(QuantMetrics.TmAlpha),
            nameof(QuantMetrics.TmGamma),
            nameof(QuantMetrics.HmAlpha),
            nameof(QuantMetrics.HmDownsideBeta),
            nameof(QuantMetrics.TimingRating),
            nameof(QuantMetrics.BullBeta),
            nameof(QuantMetrics.BearBeta),
            nameof(QuantMetrics.CaptureSpread),
            nameof(QuantMetrics.CrashCorrelation),
            nameof(QuantMetrics.ConvexityRating),
            nameof(QuantMetrics.DiligenceScore),
            nameof(QuantMetrics.StarRating),
            nameof(QuantMetrics.ComprehensiveResilienceScore),
            nameof(QuantMetrics.OverallResilienceRating),
            nameof(QuantMetrics.MacroResilienceScore),
            nameof(QuantMetrics.ActiveSharePercent),
            nameof(QuantMetrics.FamaFrenchAlpha),
            nameof(QuantMetrics.FamaFrenchRSquared)
        };

        foreach (var propName in propertiesToCheck)
        {
            var prop = metricsType.GetProperty(propName);
            Assert.IsNotNull(prop, $"Property {propName} should exist on QuantMetrics");
            Assert.IsTrue(prop.CanWrite, $"Property {propName} must have a setter to prevent WPF TwoWay binding CheckReadOnly crashes");
        }

        var metrics = new QuantMetrics();
        // Test that setting values does not throw
        var propVar = metricsType.GetProperty(nameof(QuantMetrics.CornishFisherVaR95));
        propVar!.SetValue(metrics, 12.34m);
    }
}
