using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase15Tests
{
    private static List<NavRecord> CreateMockNavHistory(int days = 120, decimal startNav = 1.0000m, decimal dailyDrift = 0.0008m, decimal dailyVol = 0.012m)
    {
        var list = new List<NavRecord>();
        var baseDate = new DateTime(2025, 1, 1);
        decimal nav = startNav;

        for (int i = 0; i < days; i++)
        {
            // 构造有周期波动的净值
            double cycle = Math.Sin(i * 0.15) * (double)dailyVol;
            double ret = (double)dailyDrift + cycle;
            nav = Math.Max(0.1m, nav * (1.0m + (decimal)ret));

            list.Add(new NavRecord
            {
                Date = baseDate.AddDays(i),
                UnitNav = Math.Round(nav, 4),
                CumulativeNav = Math.Round(nav, 4)
            });
        }
        return list;
    }

    [TestMethod]
    public void TestEvaluateShortTermHorizon_MomentumRsiVaRWinRate()
    {
        var navs = CreateMockNavHistory(60, 1.2000m, 0.0015m, 0.01m);
        var result = QuantCalculator.EvaluateShortTermHorizon(navs);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Momentum5D != 0m, "5日动量应有有效计算值");
        Assert.IsTrue(result.Momentum20D != 0m, "20日动量应有有效计算值");
        Assert.IsTrue(result.ShortTermRsi >= 0m && result.ShortTermRsi <= 100m, "短期 RSI 应处于 0~100 区间");
        Assert.IsTrue(result.ShortTermWinRate >= 0m && result.ShortTermWinRate <= 100m, "短期胜率应处于 0~100% 区间");
        Assert.IsTrue(result.VaR95_5D > 0m, "5日 VaR(95%) 必须为正值");
        Assert.IsTrue(result.CVaR95_5D >= result.VaR95_5D, "CVaR(95%) 条件在险价值应大于等于 VaR");
        Assert.IsTrue(result.ShortTermScore >= 0m && result.ShortTermScore <= 100m, "短期综合评分应在 0~100 区间");
        Assert.IsFalse(string.IsNullOrEmpty(result.ShortTermRating), "短期评级标签不应为空");
        Assert.IsFalse(string.IsNullOrEmpty(result.ShortTermDiagnosis), "短期诊断结论不应为空");
    }

    [TestMethod]
    public void TestEvaluateMediumTermHorizon_AnnualizedReturnAndInformationRatio()
    {
        var navs = CreateMockNavHistory(260, 1.0000m, 0.0010m, 0.012m);
        var fund = new FundDetail
        {
            Code = "000001",
            Name = "华夏成长",
            NavHistory = navs
        };

        var result = QuantCalculator.EvaluateMediumTermHorizon(fund, navs);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.AnnualizedReturn > 0m, "年化收益率应大于 0");
        Assert.IsTrue(result.CalmarRatio > 0m, "卡玛比率应大于 0");
        Assert.IsTrue(result.MediumTermScore >= 0m && result.MediumTermScore <= 100m, "中期综合评分应在 0~100 区间");
        Assert.IsFalse(string.IsNullOrEmpty(result.MediumTermRating), "中期评级标签不应为空");
        Assert.IsFalse(string.IsNullOrEmpty(result.MediumTermDiagnosis), "中期诊断结论不应为空");
        Assert.IsTrue(result.MaxDrawdownRecoveryDays >= 0, "最大回撤修复天数必须非负");
    }

    [TestMethod]
    public void TestHorizonConcordance_FourQuadrantMatrixLogic()
    {
        // 象限 1: 短中双优
        var short1 = new ShortTermEvaluationResult { ShortTermScore = 75m };
        var med1 = new MediumTermEvaluationResult { MediumTermScore = 80m };
        var conc1 = QuantCalculator.EvaluateHorizonConcordance(short1, med1);
        Assert.IsTrue(conc1.ConcordanceType.Contains("短中双优"));
        Assert.IsTrue(conc1.SuggestedAllocationMultiplier >= 1.25m, "短中双优应建议超配加仓");

        // 象限 2: 中期优质，短线超跌
        var short2 = new ShortTermEvaluationResult { ShortTermScore = 42m };
        var med2 = new MediumTermEvaluationResult { MediumTermScore = 78m };
        var conc2 = QuantCalculator.EvaluateHorizonConcordance(short2, med2);
        Assert.IsTrue(conc2.ConcordanceType.Contains("短线超跌") || conc2.ConcordanceType.Contains("中期优质"));
        Assert.IsTrue(conc2.SuggestedAllocationMultiplier > 1.0m, "中期优质短期超跌应建议逢低吸筹或定投");

        // 象限 3: 短线过热，中期透支
        var short3 = new ShortTermEvaluationResult { ShortTermScore = 75m };
        var med3 = new MediumTermEvaluationResult { MediumTermScore = 45m };
        var conc3 = QuantCalculator.EvaluateHorizonConcordance(short3, med3);
        Assert.IsTrue(conc3.ConcordanceType.Contains("短线过热"));
        Assert.IsTrue(conc3.SuggestedAllocationMultiplier < 1.0m, "短线过热中期透支应建议逢高减仓");

        // 象限 4: 短中双杀
        var short4 = new ShortTermEvaluationResult { ShortTermScore = 35m };
        var med4 = new MediumTermEvaluationResult { MediumTermScore = 38m };
        var conc4 = QuantCalculator.EvaluateHorizonConcordance(short4, med4);
        Assert.IsTrue(conc4.ConcordanceType.Contains("短中双杀"));
        Assert.IsTrue(conc4.SuggestedAllocationMultiplier <= 0.30m, "短中双杀应建议坚决防守规避");
    }

    [TestMethod]
    public void TestForwardSimulationForecast_GarchConeAndQuantiles()
    {
        var navs = CreateMockNavHistory(100, 1.5000m, 0.0006m, 0.015m);
        var forecast = QuantCalculator.GenerateForwardSimulationForecast("001234", 1.5000m, navs, 60);

        Assert.IsNotNull(forecast);
        Assert.IsTrue(forecast.ConditionalVolatility > 0m, "条件年化波动率必须大于 0");
        Assert.IsTrue(forecast.ForecastTrajectory.Count > 40, "预测轨迹点数量充足");

        // 严格检验分位数锥形单调性: P10 <= P50 <= P90
        foreach (var pt in forecast.ForecastTrajectory)
        {
            Assert.IsTrue(pt.P10_Pessimistic <= pt.P50_Expected, $"第{pt.DayIndex}天 P10 ({pt.P10_Pessimistic}) 应 <= P50 ({pt.P50_Expected})");
            Assert.IsTrue(pt.P50_Expected <= pt.P90_Optimistic, $"第{pt.DayIndex}天 P50 ({pt.P50_Expected}) 应 <= P90 ({pt.P90_Optimistic})");
        }

        Assert.IsTrue(forecast.ExpectedP10EndNav < forecast.ExpectedP90EndNav, "期末悲观净值应小于乐观净值");
        Assert.IsFalse(string.IsNullOrEmpty(forecast.ForecastSummary), "前瞻预测摘要不应为空");
    }

    [TestMethod]
    public void TestEvaluateForecastVsReality_AuditingMetrics()
    {
        var navs = CreateMockNavHistory(120, 1.0000m, 0.0008m, 0.01m);
        var audit = QuantCalculator.EvaluateForecastVsReality(navs, 50, 20);

        Assert.IsNotNull(audit);
        Assert.IsTrue(audit.AuditedDaysCount > 0, "审计交易日数必须大于0");
        Assert.IsTrue(audit.DirectionalHitRate >= 0m && audit.DirectionalHitRate <= 100m, "方向命中率应在 0~100% 之间");
        Assert.IsTrue(audit.PicpCoverageRatio >= 0m && audit.PicpCoverageRatio <= 100m, "置信区间覆盖率 PICP 应在 0~100% 之间");
        Assert.IsTrue(audit.RootMeanSquareError >= 0m, "RMSE 必须非负");
        Assert.IsTrue(audit.MeanAbsolutePercentageError >= 0m, "MAPE 必须非负");
        Assert.IsFalse(string.IsNullOrEmpty(audit.AuditRating), "模型审计评级不应为空");
        Assert.IsFalse(string.IsNullOrEmpty(audit.AuditConclusion), "模型审计结论不应为空");
    }

    [TestMethod]
    public void TestSummarizeExperienceAndIterateWeights_StrictSumConservation()
    {
        var realityAudit = new ForecastRealityComparisonResult
        {
            AuditedDaysCount = 60,
            DirectionalHitRate = 65.5m,
            PicpCoverageRatio = 86.0m,
            AdviceAlphaContribution = 4.2m
        };
        var shortTerm = new ShortTermEvaluationResult
        {
            ShortTermScore = 72m,
            ShortTermVolatility = 16.5m,
            VaR95_5D = 2.8m,
            ShortTermWinRate = 58m
        };
        var medTerm = new MediumTermEvaluationResult
        {
            MediumTermScore = 78m,
            CalmarRatio = 1.65m,
            InformationRatio = 1.15m,
            AnnualizedExcessReturn = 8.5m
        };

        var (report, weights) = QuantCalculator.SummarizeExperienceAndIterateWeights(realityAudit, shortTerm, medTerm);

        Assert.IsNotNull(report);
        Assert.IsTrue(report.LessonsLearned.Count >= 3, "应至少总结 3 项因子的复盘经验教训");
        Assert.IsFalse(string.IsNullOrEmpty(report.OverallExperienceSummary), "复盘总述不应为空");

        Assert.IsNotNull(weights);
        Assert.IsTrue(weights.AdaptiveMomentumWeight > 0m, "动量权重必须大于0");
        Assert.IsTrue(weights.AdaptiveRiskAdjustedWeight > 0m, "风险性价比权重必须大于0");
        Assert.IsTrue(weights.AdaptiveDownsideDefenseWeight > 0m, "防御权重必须大于0");
        Assert.IsTrue(weights.AdaptiveAlphaPurityWeight > 0m, "纯Alpha权重必须大于0");
        Assert.IsTrue(weights.AdaptiveConvexityWeight > 0m, "凸性权重必须大于0");

        // 核心数学定律：自适应权重必须 100% 严格守恒！
        decimal sum = weights.TotalWeightSum;
        Assert.AreEqual(1.000m, Math.Round(sum, 3), "自适应多因子权重和必须严格守恒等于 1.0 (100%)");
        Assert.IsTrue(weights.IterationInformationGain > 0m, "贝叶斯自适应迭代应产生正向信息增益");
    }

    [TestMethod]
    public void TestGenerateScientificInvestmentAdvice_CompleteEndToEnd()
    {
        var navs = CreateMockNavHistory(150, 1.1000m, 0.0012m, 0.014m);
        var fund = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘",
            NavHistory = navs,
            QuantMetrics = new QuantMetrics()
        };

        var advice = QuantCalculator.GenerateScientificInvestmentAdvice(fund, navs);

        Assert.IsNotNull(advice);
        Assert.AreEqual("110011", advice.FundCode);
        Assert.AreEqual("易方达中小盘", advice.FundName);

        // 验证行动信号与置信度
        Assert.IsTrue(Enum.IsDefined(typeof(InvestmentActionSignal), advice.ActionSignal), "行动信号应为有效枚举值");
        Assert.IsTrue(advice.ConvictionScore >= 25m && advice.ConvictionScore <= 98m, "置信度评分应在 25%~98% 之间");

        // 验证建仓区间与动态止损/止盈参考价格
        Assert.IsTrue(advice.SuggestedEntryNavLower <= advice.CurrentUnitNav && advice.CurrentUnitNav <= advice.SuggestedEntryNavUpper,
            "当前净值应位于建议建仓区间之内或附近");
        Assert.IsTrue(advice.TrailingStopLossNav < advice.CurrentUnitNav, "动态止损位必须低于当前净值");
        Assert.IsTrue(advice.TakeProfitTargetNav > advice.CurrentUnitNav, "目标止盈位必须高于当前净值");
        Assert.IsTrue(advice.StopLossPercent >= 3.5m && advice.StopLossPercent <= 12.0m, "止损比例应在合理风控阈值 3.5%~12% 之间");

        // 验证逻辑论据与风险警示
        Assert.IsTrue(advice.KeyInvestmentTheses.Count >= 3, "核心投资论据应至少包含3条");
        Assert.IsTrue(advice.PrimaryRiskWarnings.Count >= 2, "风险预警应至少包含2条");
        Assert.IsTrue(advice.ExecutiveAdvisoryVerdict.Contains("【投决会首席投资官决议】"), "决议文本应包含首席投资官决议头衔");

        // 验证回填至 fund 实体
        Assert.AreSame(advice, fund.ScientificAdvice, "应成功回填至 FundDetail.ScientificAdvice");
        Assert.AreSame(advice, fund.QuantMetrics?.ScientificAdvice, "应成功回填至 QuantMetrics.ScientificAdvice");
    }

    [TestMethod]
    public void TestPortfolioEngine_EvaluatePortfolioMultiHorizonAndAdvice()
    {
        var navs1 = CreateMockNavHistory(100, 1.2000m, 0.0010m, 0.015m);
        var navs2 = CreateMockNavHistory(100, 1.0500m, 0.0005m, 0.005m);

        var fund1 = new FundDetail { Code = "001", Name = "进攻基金", NavHistory = navs1 };
        var fund2 = new FundDetail { Code = "002", Name = "防守基金", NavHistory = navs2 };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fund1, 60m),
            (fund2, 40m)
        };

        var portAdvice = PortfolioEngine.EvaluatePortfolioMultiHorizonAndAdvice(components);

        Assert.IsNotNull(portAdvice);
        Assert.AreEqual("PORTFOLIO", portAdvice.FundCode);
        Assert.IsTrue(portAdvice.ConvictionScore > 0m, "组合建议置信度应大于0");
        Assert.IsTrue(portAdvice.CurrentUnitNav > 0m, "合成组合当前净值必须大于0");
        Assert.IsFalse(string.IsNullOrEmpty(portAdvice.ExecutiveAdvisoryVerdict), "组合首席投资官决议不应为空");
    }

    [TestMethod]
    public void TestExportExecutivePitchDeckToHtml_ContainsPhase15AdvisorySection()
    {
        var navs = CreateMockNavHistory(100, 1.3500m, 0.0010m, 0.012m);
        var fund = new FundDetail
        {
            Code = "005827",
            Name = "易方达蓝筹精选",
            ManagerName = "张坤",
            NavHistory = navs,
            QuantMetrics = new QuantMetrics()
        };

        // 生成建议
        QuantCalculator.GenerateScientificInvestmentAdvice(fund, navs);

        string html = ExportService.GenerateExecutivePitchDeckHtml(fund, fund.QuantMetrics);

        Assert.IsFalse(string.IsNullOrEmpty(html));
        Assert.IsTrue(html.Contains("终极闭环科学投资决策建议与执行方案"), "HTML 画册应包含 Phase 15 科学投资决策章节");
        Assert.IsTrue(html.Contains("短中协同格局"), "HTML 画册应包含短中协同格局");
        Assert.IsTrue(html.Contains("预测锥置信区间覆盖率 (PICP)"), "HTML 画册应包含预测现实对比审计指标 PICP");
        Assert.IsTrue(html.Contains("贝叶斯后验自适应权重持续迭代"), "HTML 画册应包含贝叶斯自适应权重持续迭代章节");
        Assert.IsTrue(html.Contains("动态追踪止损线"), "HTML 画册应包含动态追踪止损参数");
    }
}
