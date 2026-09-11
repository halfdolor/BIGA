using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase29Tests
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
        var fundA = new FundDetail { Code = "110011", Name = "易方达中小盘混合 (偏股型)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.16, 0.22, seed: 111) };
        var fundB = new FundDetail { Code = "510300", Name = "华泰柏瑞沪深300ETF (宽基指数)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.08, 0.15, seed: 222) };
        var fundC = new FundDetail { Code = "000001", Name = "华夏纯债中短债A (固定收益)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.042, 0.032, seed: 333) };
        var fundD = new FundDetail { Code = "000509", Name = "易方达现金增利货币A (流动性货币)", NavHistory = GenerateSyntheticNav(baseDate, 365, 0.021, 0.005, seed: 444) };

        return new List<(FundDetail Fund, decimal Weight)>
        {
            (fundA, 35.0m),
            (fundB, 25.0m),
            (fundC, 25.0m),
            (fundD, 15.0m)
        };
    }

    [TestMethod]
    public void Test_RmtCovarianceCleaning_ReducesConditionNumber_AndIdentifiesNoise()
    {
        var components = CreateSampleComponents();

        var result = QuantCalculator.CalculateRmtCovarianceCleaning(components);

        Assert.IsNotNull(result, "RmtCovarianceCleaningResult should not be null");
        Assert.AreEqual(4, result.AssetCountN, "Asset count should match components count");
        Assert.IsTrue(result.SampleCountT > 10, "Sample count should be greater than 10");
        Assert.IsTrue(result.QualityRatioQ > 1.0m, "Quality ratio Q should be greater than 1.0");
        Assert.IsTrue(result.MarchenkoPasturUpperBound > 0m, "MP upper bound should be positive");
        Assert.IsTrue(result.EigenItems.Count == 4, "Eigen items count should match asset count");
        Assert.IsTrue(result.CleanedConditionNumber <= result.RawConditionNumber || result.ConditionNumberImprovementRatio >= 1.0m,
            "Cleaned condition number should be less than or equal to raw condition number");
        Assert.IsNotNull(result.CleanedCorrelationMatrix, "Cleaned correlation matrix should be generated");
        Assert.IsNotNull(result.CleanedCovarianceMatrix, "Cleaned covariance matrix should be generated");
        Assert.IsTrue(!string.IsNullOrWhiteSpace(result.RmtDenoisingVerdict), "Verdict should not be empty");

        // 验证去噪相关矩阵对角线元素为 1.0
        for (int i = 0; i < result.AssetCountN; i++)
        {
            Assert.AreEqual(1.0, result.CleanedCorrelationMatrix[i, i], 1e-4, $"Diagonal element {i} should be 1.0");
        }
    }

    [TestMethod]
    public void Test_NestedClusteredOptimization_WeightsSumToOneHundredPercent()
    {
        var components = CreateSampleComponents();
        var rmtResult = QuantCalculator.CalculateRmtCovarianceCleaning(components);

        var result = QuantCalculator.CalculateNestedClusteredOptimization(components, rmtResult);

        Assert.IsNotNull(result, "NestedClusteredOptimizationResult should not be null");
        Assert.IsTrue(result.TotalClusters >= 2, "Cluster count should be at least 2");
        Assert.IsTrue(result.ClusterWeightItems.Count == components.Count, "Cluster weight items count should match components");

        // 验证各资产权重和为 100%
        decimal totalNcoWeight = result.ClusterWeightItems.Sum(item => item.NcoFinalWeightPercent);
        Assert.AreEqual(100.0m, totalNcoWeight, 1.0m, "Sum of NCO weights should be approximately 100%");

        Assert.IsTrue(result.NcoExpectedReturnAnnualPercent > 0m, "Expected return should be positive");
        Assert.IsTrue(result.NcoAnnualizedVolatilityPercent > 0m, "Volatility should be positive");
        Assert.IsTrue(result.NcoSharpeRatio > 0m, "NCO Sharpe ratio should be positive");
        Assert.IsTrue(!string.IsNullOrWhiteSpace(result.NcoOptimizationVerdict), "Verdict should not be empty");
    }

    [TestMethod]
    public void Test_MicrostructureLiquidity_SlippageTiersMonotonicallyIncrease()
    {
        var components = CreateSampleComponents();

        var result = QuantCalculator.CalculateMicrostructureLiquidity(components);

        Assert.IsNotNull(result, "MicrostructureLiquidityResult should not be null");
        Assert.IsTrue(result.WeightedAmihudIlliquidity > 0m, "Weighted Amihud illiquidity should be positive");
        Assert.IsTrue(result.WeightedRollEffectiveSpreadBps > 0m, "Weighted Roll spread should be positive");
        Assert.IsTrue(result.TotalDailyAbsorbingCapacityWan > 0m, "Total daily absorbing capacity should be positive");
        Assert.AreEqual(4, result.AssetItemList.Count, "Asset item count should match component count");
        Assert.AreEqual(5, result.SlippageTiers.Count, "Should produce exactly 5 trade volume tiers");

        // 验证 5 档滑点点子与损耗金额严格单调递增
        for (int i = 1; i < result.SlippageTiers.Count; i++)
        {
            var prevTier = result.SlippageTiers[i - 1];
            var currTier = result.SlippageTiers[i];

            Assert.IsTrue(currTier.ExpectedSlippageBps >= prevTier.ExpectedSlippageBps,
                $"Slippage Bps at tier {currTier.TradeScaleLabel} should be >= previous tier");
            Assert.IsTrue(currTier.EstimatedFrictionAmountYuan > prevTier.EstimatedFrictionAmountYuan,
                $"Friction amount Yuan at tier {currTier.TradeScaleLabel} should be > previous tier");
        }

        Assert.IsTrue(!string.IsNullOrWhiteSpace(result.MicrostructureFrictionVerdict), "Verdict should not be empty");
    }

    [TestMethod]
    public void Test_PortfolioEntropyRegularization_EffectiveNumberOfBetsBoundedByNominal()
    {
        var components = CreateSampleComponents();
        var dailyNavs = components[0].Fund.NavHistory;
        var dailyReturns = new List<decimal>();
        for (int i = 1; i < dailyNavs.Count; i++)
        {
            dailyReturns.Add(dailyNavs[i].DailyReturn);
        }

        var result = QuantCalculator.CalculatePortfolioEntropyRegularization(components, dailyReturns);

        Assert.IsNotNull(result, "PortfolioEntropyRegularizationResult should not be null");
        Assert.IsTrue(result.EffectiveNumberOfAssets >= 1.0m, "ENA should be at least 1.0");
        Assert.IsTrue(result.EffectiveNumberOfBets >= 1.0m, "ENB should be at least 1.0");
        // 正交因子的有效下注数在数学上受限于名义有效资产数（考虑微小数值容差）
        Assert.IsTrue(result.EffectiveNumberOfBets <= result.EffectiveNumberOfAssets + 0.5m,
            $"ENB ({result.EffectiveNumberOfBets}) should be bounded by ENA ({result.EffectiveNumberOfAssets})");
        Assert.IsTrue(result.DiversificationDeficit >= 0m, "Diversification deficit should be non-negative");
        Assert.IsTrue(result.EntropyRegularizedDiversificationScore >= 0m && result.EntropyRegularizedDiversificationScore <= 100m,
            "Entropy diversification score should be between 0 and 100");
        Assert.AreEqual(4, result.PcaBetContributions.Count, "PCA Bet contributions count should match component count");
        Assert.IsTrue(!string.IsNullOrWhiteSpace(result.EntropyDiversificationVerdict), "Verdict should not be empty");
    }

    [TestMethod]
    public void Test_PortfolioEngine_Phase29_EndToEndIntegration()
    {
        var components = CreateSampleComponents();

        var portfolioResult = PortfolioEngine.CalculatePortfolio(components);

        Assert.IsNotNull(portfolioResult, "PortfolioResult should not be null");

        // 验证 Phase 29 四大模型已挂载至计算引擎管线
        Assert.IsNotNull(portfolioResult.RmtCovarianceCleaning, "RmtCovarianceCleaning should be calculated in pipeline");
        Assert.IsNotNull(portfolioResult.NestedClusteredOptimization, "NestedClusteredOptimization should be calculated in pipeline");
        Assert.IsNotNull(portfolioResult.MicrostructureLiquidity, "MicrostructureLiquidity should be calculated in pipeline");
        Assert.IsNotNull(portfolioResult.PortfolioEntropyRegularization, "PortfolioEntropyRegularization should be calculated in pipeline");

        // 验证尽调报告生成包含 Phase 29 四大章节
        string htmlReport = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(portfolioResult, components);
        Assert.IsTrue(htmlReport.Contains("十八、随机矩阵理论 (RMT) 与 Marchenko-Pastur 谱滤波去噪 (Phase 29)"), "Report must include Chapter 18");
        Assert.IsTrue(htmlReport.Contains("十九、嵌套聚类优化 (NCO) 层次化前沿与簇间-簇内双重配置 (Phase 29)"), "Report must include Chapter 19");
        Assert.IsTrue(htmlReport.Contains("二十、Amihud 冲击弹性与 Roll 隐性买卖价差微观流动性摩擦锥 (Phase 29)"), "Report must include Chapter 20");
        Assert.IsTrue(htmlReport.Contains("二十一、信息几何有效下注数 (ENB) 与香农熵分散度审定 (Phase 29)"), "Report must include Chapter 21");
    }
}
