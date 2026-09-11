using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase20Tests
{
    private static List<NavRecord> GenerateNavSeries(DateTime start, int days, double dailyMean, double dailyStd)
    {
        var list = new List<NavRecord>();
        double nav = 1.0;
        var rng = new Random(12345);

        for (int i = 0; i < days; i++)
        {
            list.Add(new NavRecord
            {
                Date = start.AddDays(i),
                UnitNav = (decimal)nav,
                CumulativeNav = (decimal)nav
            });

            // Box-Muller standard normal
            double u1 = Math.Max(1e-8, rng.NextDouble());
            double u2 = Math.Max(1e-8, rng.NextDouble());
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            double ret = dailyMean + dailyStd * z;
            nav *= (1.0 + ret);
        }
        return list;
    }

    [TestMethod]
    public void Test_MacroRegimeSwitching_FourRegimes_GeneratesAdaptiveWeightsAndCorrelationJump()
    {
        var fund1 = new FundDetail { Code = "000001", Name = "华夏成长", Type = "混合型" };
        var fund2 = new FundDetail { Code = "000002", Name = "易方达稳健", Type = "债券型" };
        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fund1, 60m),
            (fund2, 40m)
        };

        foreach (MacroRegimeType regime in Enum.GetValues(typeof(MacroRegimeType)))
        {
            var res = QuantCalculator.SimulateMacroRegimeSwitching(components, regime);

            Assert.IsNotNull(res);
            Assert.AreEqual(regime, res.CurrentRegime);
            Assert.IsNotNull(res.AssetTargets);
            Assert.IsTrue(res.AssetTargets.Count >= 4, "Should have targets for Equity, Bond, Commodity, Cash");

            decimal totalOptimalWeight = res.AssetTargets.Sum(t => t.RegimeOptimalWeightPercent);
            Assert.AreEqual(100.0m, Math.Round(totalOptimalWeight, 1), "Adaptive target weights must sum to 100%");

            Assert.IsTrue(res.RegimeFitScore >= 0m && res.RegimeFitScore <= 100m, "Fit score must be bounded in [0, 100]");
            Assert.IsFalse(string.IsNullOrWhiteSpace(res.TransitionTacticalAdvice));

            // Stagflation regime must alert on stock-bond correlation breakdown
            if (regime == MacroRegimeType.Stagflation)
            {
                var sbJump = res.CorrelationJumpMatrix.FirstOrDefault(p => p.AssetPair.Contains("股票") && p.AssetPair.Contains("债券"));
                Assert.IsNotNull(sbJump);
                Assert.IsTrue(sbJump.RegimeShockCorrelation > sbJump.BaselineCorrelation, "Stagflation should induce positive correlation jump");
                Assert.IsTrue(sbJump.RiskAlert.Contains("预警") || sbJump.RiskAlert.Contains("双杀"));
            }
        }
    }

    [TestMethod]
    public void Test_ConditionalDrawdownAtRisk_Calculation_Accurate()
    {
        // 构造一个包含显著下跌和修复的净值曲线
        var navList = new List<NavRecord>();
        DateTime dt = new DateTime(2023, 1, 1);
        double[] navs = new double[]
        {
            1.00, 1.05, 1.10, 1.08, 0.95, 0.90, 0.85, 0.88, 0.92, 1.00, 1.12, 1.15, 1.00, 0.92, 0.88, 0.95, 1.05, 1.18
        };

        for (int i = 0; i < navs.Length; i++)
        {
            navList.Add(new NavRecord
            {
                Date = dt.AddDays(i),
                UnitNav = (decimal)navs[i],
                CumulativeNav = (decimal)navs[i]
            });
        }

        var (cdar95, episodes, avgRecovery) = QuantCalculator.CalculateConditionalDrawdownAtRisk(navList, 0.95);

        Assert.IsTrue(cdar95 > 15.0m, "CDaR at 95% should capture severe drawdown levels");
        Assert.IsTrue(episodes >= 1, "Should identify at least one deep drawdown episode");
        Assert.IsTrue(avgRecovery >= 0, "Recovery days should be non-negative");
    }

    [TestMethod]
    public void Test_EulerExpectedShortfall_EulerIdentity_StrictSumEqualsPortfolioES()
    {
        // 构造3只基金的收益率数据
        int days = 100;
        var f1 = new FundDetail { Code = "001", Name = "大盘成长股票" };
        var f2 = new FundDetail { Code = "002", Name = "纯债稳健" };
        var f3 = new FundDetail { Code = "003", Name = "中盘价值" };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (f1, 50m),
            (f2, 30m),
            (f3, 20m)
        };

        var rng = new Random(999);
        var r1 = new List<double>();
        var r2 = new List<double>();
        var r3 = new List<double>();
        var portfolioNav = new List<NavRecord>();
        double currentNav = 1.0;

        for (int i = 0; i < days; i++)
        {
            double ret1 = (rng.NextDouble() - 0.5) * 0.04;
            double ret2 = (rng.NextDouble() - 0.45) * 0.008;
            double ret3 = (rng.NextDouble() - 0.5) * 0.03;

            // 偶尔触发系统性极端尾部暴跌 (比如第20, 50, 80天)
            if (i == 20 || i == 50 || i == 80)
            {
                ret1 = -0.06;
                ret2 = 0.005; // 债券对冲
                ret3 = -0.04;
            }

            r1.Add(ret1);
            r2.Add(ret2);
            r3.Add(ret3);

            double portRet = 0.50 * ret1 + 0.30 * ret2 + 0.20 * ret3;
            currentNav *= (1.0 + portRet);
            portfolioNav.Add(new NavRecord
            {
                Date = DateTime.Today.AddDays(i - days),
                UnitNav = (decimal)currentNav,
                CumulativeNav = (decimal)currentNav
            });
        }

        var individualReturns = new Dictionary<string, List<double>>
        {
            { "001", r1 },
            { "002", r2 },
            { "003", r3 }
        };

        var result = QuantCalculator.DecomposeEulerExpectedShortfall(components, individualReturns, portfolioNav, 0.95);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.PortfolioExpectedShortfallPercent > 0m, "Portfolio ES must be positive");
        Assert.AreEqual(3, result.Items.Count);

        // 验证欧拉齐次定理恒等性：sum(CES_i) == ES_p
        decimal sumCes = result.Items.Sum(x => x.ComponentExpectedShortfall);
        Assert.AreEqual((double)result.PortfolioExpectedShortfallPercent, (double)sumCes, 0.05, "Sum of Component ES must match Portfolio ES by Euler's Theorem");

        // 验证风险贡献占比加总为 100%
        decimal sumPct = result.Items.Sum(x => x.TailRiskContributionPercent);
        Assert.AreEqual(100.0, (double)sumPct, 0.2, "Sum of tail risk contribution percentages must equal 100%");
    }

    [TestMethod]
    public void Test_EulerExpectedShortfall_IdentifiesTailRiskBlackHole()
    {
        // 构造一个权重仅 10%，但在尾部极值日暴跌 15% 的极端风险黑洞标的
        var fNormal = new FundDetail { Code = "001", Name = "平稳基金" };
        var fBlackHole = new FundDetail { Code = "002", Name = "极端博弈黑洞基金" };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fNormal, 90m),
            (fBlackHole, 10m)
        };

        int days = 60;
        var rNormal = new List<double>();
        var rBH = new List<double>();
        var pNav = new List<NavRecord>();
        double nav = 1.0;

        for (int i = 0; i < days; i++)
        {
            double retN = 0.0005;
            double retBH = 0.0002;
            if (i >= 55) // 连续极端尾部失血
            {
                retN = -0.01;
                retBH = -0.15; // 极端暴跌
            }

            rNormal.Add(retN);
            rBH.Add(retBH);

            double pRet = 0.90 * retN + 0.10 * retBH;
            nav *= (1.0 + pRet);
            pNav.Add(new NavRecord { Date = DateTime.Today.AddDays(i - days), UnitNav = (decimal)nav, CumulativeNav = (decimal)nav });
        }

        var individualReturns = new Dictionary<string, List<double>>
        {
            { "001", rNormal },
            { "002", rBH }
        };

        var result = QuantCalculator.DecomposeEulerExpectedShortfall(components, individualReturns, pNav, 0.95);

        var bhItem = result.Items.FirstOrDefault(x => x.FundCode == "002");
        Assert.IsNotNull(bhItem);
        Assert.IsTrue(bhItem.IsTailRiskBlackHole, "Asset with 10% weight but massive tail drawdown should be flagged as Tail Risk Black Hole");
        Assert.IsTrue(result.TailRiskBlackHoleCount >= 1);
        Assert.IsTrue(result.WorstTailRiskFund.Contains("极端博弈黑洞基金"));
    }

    [TestMethod]
    public void Test_InstitutionalGatekeeper_ApprovedFund_PassesAllGates()
    {
        var fund = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘",
            FundSize = "45.80 亿元",
            ManagerTenure = "850 天",
            StyleDrift = new StyleDriftAnalysisResult { StyleDriftIndex = 18.5m },
            Result4433 = new Fund4433CheckResult { Passed4433 = true },
            QuantMetrics = new QuantMetrics { MaxDrawdown = 16.5m, SharpeRatio = 1.85m },
            AlphaPersistence = new AlphaPersistenceResult { RollingWinRate12M = 65.0m }
        };

        var audit = PortfolioEngine.EvaluateInstitutionalGatekeeper(fund);

        Assert.IsNotNull(audit);
        Assert.AreEqual(6, audit.PassedRulesCount);
        Assert.AreEqual(0, audit.HardVetoCount);
        Assert.IsTrue(audit.OverallGateScore >= 90m);
        Assert.IsTrue(audit.CommitteeResolution.Contains("准入"));
        Assert.AreEqual(0, audit.VetoRedlines.Count);
    }

    [TestMethod]
    public void Test_InstitutionalGatekeeper_VetoedFund_TriggersHardVeto()
    {
        // 案例 1: 规模 0.42 亿元 (< 1 亿元清盘底线) -> 触发一票否决
        var fundTiny = new FundDetail
        {
            Code = "009999",
            Name = "迷你袖珍基金",
            FundSize = "0.42 亿元",
            ManagerTenure = "600 天",
            QuantMetrics = new QuantMetrics { MaxDrawdown = 15m }
        };

        var auditTiny = PortfolioEngine.EvaluateInstitutionalGatekeeper(fundTiny);
        Assert.IsTrue(auditTiny.HardVetoCount >= 1);
        Assert.IsTrue(auditTiny.OverallGateScore <= 45m);
        Assert.IsTrue(auditTiny.CommitteeResolution.Contains("否决"));
        Assert.IsTrue(auditTiny.VetoRedlines.Any(r => r.Contains("1 亿元红线")));

        // 案例 2: 投资经理刚上任 45 天 (< 180 天任期红线) -> 触发一票否决
        var fundRookie = new FundDetail
        {
            Code = "008888",
            Name = "走马换将基金",
            FundSize = "20 亿元",
            ManagerTenure = "45 天",
            QuantMetrics = new QuantMetrics { MaxDrawdown = 15m }
        };

        var auditRookie = PortfolioEngine.EvaluateInstitutionalGatekeeper(fundRookie);
        Assert.IsTrue(auditRookie.HardVetoCount >= 1);
        Assert.IsTrue(auditRookie.OverallGateScore <= 45m);
        Assert.IsTrue(auditRookie.CommitteeResolution.Contains("否决"));
        Assert.IsTrue(auditRookie.VetoRedlines.Any(r => r.Contains("180天")));
    }

    [TestMethod]
    public void Test_PortfolioGatekeeper_EvaluatesEntirePortfolio_WeightedScoresAndPassRate()
    {
        var fundGood = new FundDetail
        {
            Code = "110011",
            Name = "优质白马基金",
            FundSize = "30 亿元",
            ManagerTenure = "700 天",
            QuantMetrics = new QuantMetrics { MaxDrawdown = 18m, SharpeRatio = 1.6m }
        };

        var fundBad = new FundDetail
        {
            Code = "009999",
            Name = "迷你清盘风险基金",
            FundSize = "0.5 亿元", // 一票否决
            ManagerTenure = "500 天",
            QuantMetrics = new QuantMetrics { MaxDrawdown = 20m }
        };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fundGood, 70m),
            (fundBad, 30m)
        };

        var result = PortfolioEngine.EvaluatePortfolioGatekeeper(components);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.TotalComponentsCount);
        Assert.AreEqual(1, result.ApprovedCount);
        Assert.AreEqual(1, result.VetoedCount);
        Assert.AreEqual(70.0m, result.PortfolioOverallPassRate);
        Assert.IsTrue(result.PortfolioWeightedScore > 0);
        Assert.IsTrue(result.CommitteeAuditSummary.Contains("一票否决红线"));
    }

    [TestMethod]
    public void Test_PortfolioEngine_CalculatePortfolio_Phase20_EndToEndIntegration()
    {
        var f1 = new FundDetail
        {
            Code = "001",
            Name = "标杆成长",
            Type = "混合型",
            FundSize = "50 亿元",
            ManagerTenure = "800 天",
            NavHistory = GenerateNavSeries(DateTime.Today.AddDays(-260), 260, 0.0006, 0.015)
        };

        var f2 = new FundDetail
        {
            Code = "002",
            Name = "稳健增利",
            Type = "债券型",
            FundSize = "80 亿元",
            ManagerTenure = "1200 天",
            NavHistory = GenerateNavSeries(DateTime.Today.AddDays(-260), 260, 0.0002, 0.003)
        };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (f1, 60m),
            (f2, 40m)
        };

        var portfolioResult = PortfolioEngine.CalculatePortfolio(components, 2.0m);

        Assert.IsNotNull(portfolioResult);

        // 验证 Phase 20 产物
        Assert.IsNotNull(portfolioResult.MacroRegimeSwitching, "MacroRegimeSwitching must be instantiated in CalculatePortfolio");
        Assert.IsTrue(portfolioResult.MacroRegimeSwitching.AssetTargets.Count > 0);

        Assert.IsNotNull(portfolioResult.TailRiskDecomposition, "TailRiskDecomposition must be instantiated in CalculatePortfolio");
        Assert.IsTrue(portfolioResult.TailRiskDecomposition.ConditionalDrawdownAtRisk95 >= 0m);
        Assert.IsTrue(portfolioResult.TailRiskDecomposition.Items.Count == 2);

        Assert.IsNotNull(portfolioResult.GatekeeperAudit, "GatekeeperAudit must be instantiated in CalculatePortfolio");
        Assert.AreEqual(2, portfolioResult.GatekeeperAudit.FundAudits.Count);
        Assert.IsTrue(portfolioResult.GatekeeperAudit.PortfolioOverallPassRate >= 0m);
    }
}
