using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase27Tests
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

    private static List<NavRecord> GenerateAsymmetricFatTailNav(DateTime start, int days, int seed = 777)
    {
        var list = new List<NavRecord>();
        double nav = 1.0;
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

            double r = (rng.NextDouble() - 0.48) * 0.01;
            // 引入偶尔的极端负跳跃 (黑天鹅左偏暴跌与肥尾)
            if (rng.NextDouble() < 0.05)
            {
                r -= 0.045 + rng.NextDouble() * 0.035;
            }

            nav *= (1.0 + r);
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
    public void Test_AlmgrenChrissOptimalExecution_TrajectoryAndCostCharacteristics()
    {
        decimal totalOrderWan = 2000m;
        double dailyVol = 0.018; // 1.8% 日波动
        double totalAdvWan = 10000.0;
        int tradingDays = 5;
        int numberOfSteps = 10;

        // 测算中等风险厌恶
        var resultMed = QuantCalculator.CalculateAlmgrenChrissOptimalExecution(
            totalOrderWan, dailyVol, totalAdvWan, tradingDays, numberOfSteps, riskAversion: 1e-6);

        Assert.IsNotNull(resultMed, "Almgren-Chriss result must not be null.");
        Assert.AreEqual(numberOfSteps, resultMed.TrajectorySteps.Count, "Step count should equal numberOfSteps.");
        Assert.AreEqual(0.0m, resultMed.TrajectorySteps[numberOfSteps - 1].RemainingHoldingRatioPercent, "Final remaining holding must be 0%.");

        // 验证单调衰减
        for (int i = 1; i < numberOfSteps; i++)
        {
            Assert.IsTrue(resultMed.TrajectorySteps[i].RemainingHoldingWan <= resultMed.TrajectorySteps[i - 1].RemainingHoldingWan,
                $"Remaining holding at step {i} must be <= step {i - 1}.");
            Assert.IsTrue(resultMed.TrajectorySteps[i].TradeSharesWan >= 0m,
                $"Trade shares at step {i} must be non-negative.");
        }

        // 验证总清算量等于总规模
        decimal totalTraded = resultMed.TrajectorySteps.Sum(s => s.TradeSharesWan);
        Assert.AreEqual((double)totalOrderWan, (double)totalTraded, 0.1, "Sum of traded shares must equal total order capital.");

        // 验证冲击成本为正
        decimal totalPermImpact = resultMed.TrajectorySteps.Sum(s => s.PermanentImpactWan);
        decimal totalTempImpact = resultMed.TrajectorySteps.Sum(s => s.TemporaryImpactWan);
        Assert.IsTrue(resultMed.ExpectedTotalCostWan > 0m, "Expected execution cost must be strictly positive.");
        Assert.IsTrue(totalPermImpact > 0m, "Permanent impact cost must be positive.");
        Assert.IsTrue(totalTempImpact > 0m, "Temporary impact cost must be positive.");
        Assert.IsTrue(resultMed.ExecutionVarianceRiskWan > 0m, "Execution variance risk must be positive.");
        Assert.IsTrue(resultMed.ExecutionVaR95Wan > resultMed.ExpectedTotalCostWan, "Execution VaR (95%) must exceed expected cost.");
        Assert.IsTrue(resultMed.HalfLifeDays > 0m, "Optimal half-life must be positive.");

        // 对比激进清算模式 (高风险厌恶 lambda) vs 缓步清算模式 (低风险厌恶 lambda)
        var resultAggressive = QuantCalculator.CalculateAlmgrenChrissOptimalExecution(
            totalOrderWan, dailyVol, totalAdvWan, tradingDays, numberOfSteps, riskAversion: 1e-4);
        var resultPassive = QuantCalculator.CalculateAlmgrenChrissOptimalExecution(
            totalOrderWan, dailyVol, totalAdvWan, tradingDays, numberOfSteps, riskAversion: 1e-8);

        // 厌恶风险越高，越急于平仓，半衰期越短，时机方差越小，但临时市场冲击成本更高
        Assert.IsTrue(resultAggressive.HalfLifeDays < resultPassive.HalfLifeDays,
            "High risk aversion must result in shorter liquidation half-life.");
        Assert.IsTrue(resultAggressive.ExecutionVarianceRiskWan < resultPassive.ExecutionVarianceRiskWan,
            "High risk aversion must reduce timing variance risk.");
        decimal aggTempImpact = resultAggressive.TrajectorySteps.Sum(s => s.TemporaryImpactWan);
        decimal passTempImpact = resultPassive.TrajectorySteps.Sum(s => s.TemporaryImpactWan);
        Assert.IsTrue(aggTempImpact > passTempImpact,
            "Aggressive liquidation must cause higher temporary impact cost.");
    }

    [TestMethod]
    public void Test_MichaudResampledFrontier_GeneratesDiversifiedFrontier()
    {
        var components = CreateSampleComponents();
        int frontierCount = 10;
        int simulations = 30;

        var result = QuantCalculator.CalculateMichaudResampledFrontier(
            components, resampleSimulations: simulations, frontierPointsCount: frontierCount);

        Assert.IsNotNull(result, "Michaud resampled frontier result should not be null.");
        Assert.AreEqual(frontierCount, result.ResampledPoints.Count, "Must generate exactly the specified number of frontier points.");
        Assert.AreEqual(simulations, result.ResampleSimulations);
        Assert.IsTrue(result.ResampledRobustnessGainRatio >= 1.0m, "Robustness gain ratio must be >= 1.0x.");
        Assert.IsTrue(result.BestSharpeValue > 0m, "Best Sharpe value must be positive.");

        // 验证前沿各点的权重和为 100%，且波动率随风险等级递增
        for (int i = 0; i < result.ResampledPoints.Count; i++)
        {
            var pt = result.ResampledPoints[i];
            decimal sumWeights = pt.AssetWeights.Values.Sum();
            Assert.AreEqual(100.0, (double)sumWeights, 0.5, $"Point {i} asset weights must sum to 100%.");

            if (i > 0)
            {
                var prevPt = result.ResampledPoints[i - 1];
                Assert.IsTrue(pt.AnnualVolatilityPercent >= prevPt.AnnualVolatilityPercent - 0.5m,
                    $"Volatility along the resampled frontier should generally increase with rank ({pt.AnnualVolatilityPercent} vs {prevPt.AnnualVolatilityPercent}).");
            }
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ExecutiveSummary), "Executive summary must be present.");
    }

    [TestMethod]
    public void Test_SolveReverseStressTesting_ShockVectorAndLossMatch()
    {
        var components = CreateSampleComponents();
        decimal targetDrawdown = -15.0m;

        var result = QuantCalculator.SolveReverseStressTesting(components, targetDrawdown);

        Assert.IsNotNull(result, "Reverse stress testing result should not be null.");
        Assert.AreEqual(targetDrawdown, result.TargetThresholdDrawdownPercent);
        Assert.AreEqual(components.Count, result.ShockItems.Count);
        Assert.IsTrue(result.MahalanobisDistance > 0m, "Mahalanobis distance must be positive.");
        Assert.IsTrue(result.ProbabilityOfBreachNormalEstimatePercent > 0m, "Breach probability must be > 0%.");

        // 验证组合协同冲击带来的总损失恰好等于目标阈值 L
        // 总损失 = Sum(w_i * Delta_r_i)
        decimal totalPortfolioLoss = result.ShockItems.Sum(item => item.PortfolioWeightPercent / 100m * item.CriticalShockPercent);
        Assert.AreEqual((double)targetDrawdown, (double)totalPortfolioLoss, 0.1,
            $"Aggregated loss under reverse shock vector must match target loss threshold {targetDrawdown}%.");

        // 验证边际损失贡献度之和为 100%
        decimal totalContribution = result.ShockItems.Sum(item => item.LossContributionPercent);
        Assert.AreEqual(100.0, (double)totalContribution, 0.5, "Loss contributions must sum to 100%.");

        // 验证最脆弱核心资产已定位
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.MostVulnerableFundName), "Most vulnerable fund must be identified.");
        Assert.IsTrue(result.MostVulnerableShockPercent < 0m, "Vulnerable shock must be a drawdown.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.RiskOfficerVerdict), "Risk officer verdict must not be empty.");
    }

    [TestMethod]
    public void Test_CornishFisherModifiedVaR_FatTailAdjustment()
    {
        var baseDate = new DateTime(2023, 1, 1);
        // 生成具有显著左偏和超额峰度 (肥尾暴跌) 的样本
        var fatTailNav = GenerateAsymmetricFatTailNav(baseDate, 365, seed: 888);

        var result = QuantCalculator.CalculateCornishFisherModifiedVaR(fatTailNav);

        Assert.IsNotNull(result, "Cornish-Fisher result must not be null.");
        Assert.IsTrue(result.GaussianVaR95Percent > 0m, "Gaussian VaR must be positive.");
        Assert.IsTrue(result.CornishFisherVaR95Percent > 0m, "Cornish-Fisher VaR must be positive.");

        // 在左偏且肥尾序列中，修正 VaR 应显著大于高斯假定 VaR
        if (result.SampleExcessKurtosis > 0.5m || result.SampleSkewness < -0.2m)
        {
            Assert.IsTrue(result.CornishFisherVaR95Percent >= result.GaussianVaR95Percent,
                $"Cornish-Fisher 95% VaR ({result.CornishFisherVaR95Percent}%) must exceed Gaussian VaR ({result.GaussianVaR95Percent}%) for fat-tailed series.");
            Assert.IsTrue(result.CornishFisherVaR99Percent >= result.GaussianVaR99Percent,
                $"Cornish-Fisher 99% VaR ({result.CornishFisherVaR99Percent}%) must exceed Gaussian 99% VaR ({result.GaussianVaR99Percent}%).");
            Assert.IsTrue(result.TailRiskUnderestimationMultiplier >= 1.0m,
                "Tail risk underestimation multiplier must be >= 1.0x.");
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TailRiskHealthBadge), "Tail risk badge must be populated.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.AnalyticalSummary), "Summary must be populated.");
    }

    [TestMethod]
    public void Test_PortfolioEngine_Phase27EndToEnd()
    {
        var components = CreateSampleComponents();
        var inputComponents = components.Select(c => (c.Fund, c.Weight)).ToList();

        var result = PortfolioEngine.CalculatePortfolio(inputComponents, riskFreeRate: 2.0m);

        Assert.IsNotNull(result, "PortfolioResult should not be null.");

        // 验证 Phase 27 挂载的 4 大顶层算法模块全部无缝运转
        Assert.IsNotNull(result.AlmgrenChrissExecution, "AlmgrenChrissExecution should be populated.");
        Assert.IsTrue(result.AlmgrenChrissExecution.TrajectorySteps.Count > 0, "Trajectory steps must exist.");

        Assert.IsNotNull(result.MichaudResampledFrontier, "MichaudResampledFrontier should be populated.");
        Assert.AreEqual(10, result.MichaudResampledFrontier.ResampledPoints.Count, "Resampled points count should be 10.");

        Assert.IsNotNull(result.ReverseStressTopology, "ReverseStressTopology should be populated.");
        Assert.AreEqual(4, result.ReverseStressTopology.ShockItems.Count, "4 shock items expected.");

        Assert.IsNotNull(result.CornishFisherVaR, "CornishFisherVaR should be populated.");
        Assert.IsTrue(result.CornishFisherVaR.CornishFisherVaR95Percent > 0m, "Cornish-Fisher VaR must be positive.");

        // 验证尽调报告 HTML 生成完整包含 Phase 27 的四大章节
        string html = ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(result, inputComponents);
        Assert.IsTrue(html.Contains("Almgren-Chriss 最优算法执行与清算微观轨迹"),
            "HTML must contain Almgren-Chriss section.");
        Assert.IsTrue(html.Contains("Michaud 蒙特卡洛重抽样均值方差有效前沿"),
            "HTML must contain Michaud section.");
        Assert.IsTrue(html.Contains("Reverse Stress Testing 反向压力测试破产临界拓扑"),
            "HTML must contain Reverse Stress Testing section.");
        Assert.IsTrue(html.Contains("Cornish-Fisher 展开高阶矩 (偏度与峰度) 修正 VaR/CVaR"),
            "HTML must contain Cornish-Fisher section.");
    }
}
