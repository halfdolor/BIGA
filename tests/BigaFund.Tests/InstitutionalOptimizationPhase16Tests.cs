using System;
using System.Collections.Generic;
using System.Linq;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase16Tests
{
    private static List<NavRecord> CreateMockNavHistory(int days = 120, decimal startNav = 1.0000m, decimal dailyDrift = 0.0008m, decimal dailyVol = 0.012m)
    {
        var list = new List<NavRecord>();
        var baseDate = new DateTime(2025, 1, 1);
        decimal nav = startNav;

        for (int i = 0; i < days; i++)
        {
            double cycle = Math.Sin(i * 0.15) * (double)dailyVol;
            double ret = (double)dailyDrift + cycle;
            nav = Math.Max(0.1m, nav * (1.0m + (decimal)ret));

            list.Add(new NavRecord
            {
                Date = baseDate.AddDays(i),
                UnitNav = Math.Round(nav, 4),
                CumulativeNav = Math.Round(nav, 4),
                DailyReturn = Math.Round((decimal)ret * 100m, 2)
            });
        }
        return list;
    }

    [TestMethod]
    public void TestStudentTForwardSimulationForecast_MonotonicityAndFatTail()
    {
        var navs = CreateMockNavHistory(100, 1.5000m, 0.0005m, 0.015m);
        var forecast = QuantCalculator.GenerateForwardSimulationForecast("001122", 1.5000m, navs, 60);

        Assert.IsNotNull(forecast, "前瞻模拟预测结果不应为空");
        Assert.AreEqual(61, forecast.ForecastTrajectory.Count, "应包含基准日(Day 0)及未来60交易日的推演轨迹");
        Assert.IsTrue(forecast.ConditionalVolatility > 0m, "GARCH条件波动率应大于0");

        // 验证每一天严格单调性 P10 <= P50 <= P90
        for (int i = 0; i < forecast.ForecastTrajectory.Count; i++)
        {
            var pt = forecast.ForecastTrajectory[i];
            Assert.IsTrue(pt.P10_Pessimistic <= pt.P50_Expected, $"第{pt.DayIndex}天 P10 应小于等于 P50: {pt.P10_Pessimistic} <= {pt.P50_Expected}");
            Assert.IsTrue(pt.P50_Expected <= pt.P90_Optimistic, $"第{pt.DayIndex}天 P50 应小于等于 P90: {pt.P50_Expected} <= {pt.P90_Optimistic}");
            if (i > 0)
            {
                Assert.IsTrue(pt.ForecastDate > forecast.BaseDate, "预测日期应晚于基准日期");
            }
        }

        // 验证置信带随时间推移呈扩散漏斗型 (t=60 的带宽度显著大于 t=5 的带宽度)
        var pt5 = forecast.ForecastTrajectory.First(p => p.DayIndex == 5);
        var pt60 = forecast.ForecastTrajectory.First(p => p.DayIndex == 60);
        decimal spread5 = pt5.P90_Optimistic - pt5.P10_Pessimistic;
        decimal spread60 = pt60.P90_Optimistic - pt60.P10_Pessimistic;
        Assert.IsTrue(spread60 > spread5, $"预测锥散度应随周期递增：T+60 spread ({spread60}) 应大于 T+5 spread ({spread5})");
    }

    [TestMethod]
    public void TestGenerateScientificInvestmentAdvice_CompleteModelAndTheses()
    {
        var navs = CreateMockNavHistory(180, 1.2500m, 0.0010m, 0.010m);
        var fund = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘",
            Type = "混合型",
            NavHistory = navs
        };

        var advice = QuantCalculator.GenerateScientificInvestmentAdvice(fund, navs);

        Assert.IsNotNull(advice, "科学投决建议不应为空");
        Assert.IsFalse(string.IsNullOrEmpty(advice.ActionSignalText), "行动信号文本不应为空");
        Assert.IsTrue(advice.ConvictionScore >= 0m && advice.ConvictionScore <= 100m, "置信度应在 0~100 区间");
        Assert.IsTrue(advice.HorizonConcordance.SuggestedAllocationMultiplier > 0m, "建议仓位乘数应大于0");
        Assert.IsTrue(advice.TrailingStopLossNav > 0m, "动态追踪止损线应大于0");
        Assert.IsTrue(advice.TakeProfitTargetNav > advice.TrailingStopLossNav, "目标止盈线应高于追踪止损线");

        // 验证前瞻预测与复盘审计对象绑定
        Assert.IsNotNull(advice.SimulationForecast, "advice中前瞻模拟对象应已注入");
        Assert.AreEqual(61, advice.SimulationForecast.ForecastTrajectory.Count);
        Assert.IsNotNull(advice.ExperienceReport, "advice中复盘经验报告应已注入");
        Assert.IsTrue(advice.ExperienceReport.LessonsLearned.Count > 0, "应有经验复盘清单");

        // 验证贝叶斯自适应权重归一化和为 1.0 (100%)
        var weights = advice.AdaptiveWeights;
        Assert.IsNotNull(weights);
        Assert.AreEqual(1.0m, Math.Round(weights.TotalWeightSum, 2), "贝叶斯自适应权重归一化总和必须为 1.0 (100%)");

        // 验证核心投资论据和下行风险
        Assert.IsTrue(advice.KeyInvestmentTheses.Count >= 2, "核心投资论据应至少有2条");
        Assert.IsTrue(advice.PrimaryRiskWarnings.Count >= 1, "下行风险警示应至少有1条");
        Assert.IsFalse(string.IsNullOrEmpty(advice.ExecutiveAdvisoryVerdict), "投决会CIO全景评述不应为空");
    }

    [TestMethod]
    public void TestFundScreener_MultiHorizonEnrichment()
    {
        var funds = new List<StoredFundItem>
        {
            new StoredFundItem
            {
                Code = "000001",
                Name = "华夏成长",
                Type = "混合型",
                Return1Y = 22.5m,
                MaxDrawdown = 12.0m,
                SharpeRatio = 1.35m,
                QuantScore = 85.0m
            },
            new StoredFundItem
            {
                Code = "000002",
                Name = "华夏稳健",
                Type = "债券型",
                Return1Y = 5.2m,
                MaxDrawdown = 2.1m,
                SharpeRatio = 1.80m,
                QuantScore = 78.0m
            }
        };

        var ranking = QuantCalculator.CalculateMultiFactorCrossSectionalRanking(funds);
        Assert.IsNotNull(ranking);

        // 模拟筛选器横截面填充
        var scoreMap = ranking.AllScoredFunds.ToDictionary(s => s.FundCode, s => s);
        foreach (var f in funds)
        {
            if (scoreMap.TryGetValue(f.Code, out var item))
            {
                f.MultiFactorCompositeScore = item.CompositeScore;
                f.MultiFactorPercentile = item.PercentileRank;
                f.MultiFactorRatingTag = item.RatingTag;
                f.MomentumScore = item.MomentumScore;
                f.RiskAdjustedScore = item.RiskAdjustedScore;
            }

            if (f.ShortTermScore == 0 && f.MediumTermScore == 0)
            {
                decimal shortScore = Math.Clamp(50m + (f.MomentumScore - 50m) * 0.7m + (f.RiskAdjustedScore - 50m) * 0.3m, 0m, 100m);
                decimal medScore = Math.Clamp(f.MultiFactorCompositeScore > 0 ? f.MultiFactorCompositeScore : f.QuantScore, 0m, 100m);
                f.ShortTermScore = Math.Round(shortScore, 1);
                f.MediumTermScore = Math.Round(medScore, 1);
            }

            if (string.IsNullOrEmpty(f.ScientificActionSignal))
            {
                if (f.MediumTermScore >= 65m && f.ShortTermScore >= 60m)
                {
                    f.ScientificActionSignal = "积极买入";
                    f.ConcordanceType = "短中双优·共振顺风";
                    f.ScientificConviction = Math.Min(95m, 65m + (f.MediumTermScore + f.ShortTermScore) / 4m);
                }
                else
                {
                    f.ScientificActionSignal = "中性持有";
                    f.ConcordanceType = "结构分化·中性震荡";
                    f.ScientificConviction = 70m;
                }
            }
        }

        foreach (var f in funds)
        {
            Assert.IsTrue(f.ShortTermScore > 0m, "短期评分应已填充有效分数");
            Assert.IsTrue(f.MediumTermScore > 0m, "中期评分应已填充有效分数");
            Assert.IsFalse(string.IsNullOrEmpty(f.ScientificActionSignal), "科学投决行动信号应已填充");
            Assert.IsFalse(string.IsNullOrEmpty(f.ConcordanceType), "跨周期协同格局应已填充");
        }
    }
}
