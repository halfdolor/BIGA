using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class AdvancedQuantModelTests
{
    private List<NavRecord> GenerateNavSequence(DateTime start, int days, decimal dailyReturn, decimal vol = 0m, int seed = 42)
    {
        var list = new List<NavRecord>();
        decimal cur = 1.0m;
        var rnd = new Random(seed);

        for (int i = 0; i < days; i++)
        {
            var date = start.AddDays(i);
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                continue;
            }

            decimal noise = vol > 0 ? (decimal)((rnd.NextDouble() - 0.5) * (double)vol * 2.0) : 0m;
            cur *= (1.0m + dailyReturn + noise);
            if (cur < 0.01m) cur = 0.01m;

            list.Add(new NavRecord
            {
                Date = date,
                UnitNav = Math.Round(cur, 4),
                CumulativeNav = Math.Round(cur, 4)
            });
        }

        return list;
    }

    [TestMethod]
    public void CorrelationMatrix_MathematicalProperties()
    {
        // 构造三只资产：
        // Fund 1: 股票型，高波动向上
        // Fund 2: 纯债型，微波动向上 (与股票低相关)
        // Fund 3: 股票型 2，与 Fund 1 强相关 (共用相近种子)
        var start = new DateTime(2023, 1, 1);
        var navs1 = GenerateNavSequence(start, 250, dailyReturn: 0.0005m, vol: 0.02m, seed: 100);
        var navs2 = GenerateNavSequence(start, 250, dailyReturn: 0.0001m, vol: 0.001m, seed: 200);
        var navs3 = GenerateNavSequence(start, 250, dailyReturn: 0.0004m, vol: 0.018m, seed: 100); // 强相关

        var fund1 = new FundDetail { Code = "000001", Name = "偏股混合A", NavHistory = navs1 };
        var fund2 = new FundDetail { Code = "000171", Name = "纯债基金B", NavHistory = navs2 };
        var fund3 = new FundDetail { Code = "005827", Name = "蓝筹偏股C", NavHistory = navs3 };

        var components = new List<(FundDetail Fund, decimal Weight)>
        {
            (fund1, 0.4m),
            (fund2, 0.4m),
            (fund3, 0.2m)
        };

        var rets = new Dictionary<string, List<double>>();
        foreach (var c in components)
        {
            var list = new List<double>();
            for (int i = 1; i < c.Fund.NavHistory.Count; i++)
            {
                double p0 = (double)c.Fund.NavHistory[i - 1].UnitNav;
                double p1 = (double)c.Fund.NavHistory[i].UnitNav;
                list.Add((p1 - p0) / p0);
            }
            rets[c.Fund.Code] = list;
        }

        var result = PortfolioEngine.CalculateCorrelationMatrix(components, rets);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.AssetCodes.Count);
        Assert.AreEqual(3, result.Matrix.GetLength(0));
        Assert.AreEqual(3, result.Matrix.GetLength(1));

        // 1. 对角线元素必须严格为 1.0 (自相关为1)
        for (int i = 0; i < 3; i++)
        {
            Assert.AreEqual(1.0, result.Matrix[i, i], 1e-4, $"对角线 [{i},{i}] 必须为 1.0");
        }

        // 2. 必须严格对称 (r_ij == r_ji)
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Assert.AreEqual(result.Matrix[i, j], result.Matrix[j, i], 1e-4, $"相关性矩阵必须对称 [{i},{j}] vs [{j},{i}]");
                Assert.IsTrue(result.Matrix[i, j] >= -1.0 && result.Matrix[i, j] <= 1.0, "相关系数必须在 [-1, 1] 区间内");
            }
        }

        // 3. 股票与纯债的相关性应当显著低于股票与股票的相关性
        double corrStockBond = result.Matrix[0, 1];
        double corrStockStock = result.Matrix[0, 2];
        Assert.IsTrue(corrStockStock > corrStockBond, $"同向股票相关性 ({corrStockStock:F2}) 应大于股债相关性 ({corrStockBond:F2})");
    }

    [TestMethod]
    public void StressTesting_IdentifiesCrisisAndDefenseRating()
    {
        // 构造覆盖 2018 年去杠杆阴跌区间的基金净值
        // 2018-01-26 至 2018-12-28
        var start = new DateTime(2018, 1, 1);
        var navsDefensive = new List<NavRecord>();
        var navsAggressive = new List<NavRecord>();

        decimal curDef = 1.0m;
        decimal curAgg = 1.0m;

        for (int i = 0; i < 365; i++)
        {
            var date = start.AddDays(i);
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) continue;

            // 防御型基金微涨 (如纯债/低波)
            curDef *= 1.00015m;
            // 进取型基金跟随熊市下跌 (单日 -0.15%)
            curAgg *= 0.9985m;

            navsDefensive.Add(new NavRecord { Date = date, UnitNav = curDef, CumulativeNav = curDef });
            navsAggressive.Add(new NavRecord { Date = date, UnitNav = curAgg, CumulativeNav = curAgg });
        }

        var stressDef = QuantCalculator.CalculateStressTestScenarios(navsDefensive);
        var stressAgg = QuantCalculator.CalculateStressTestScenarios(navsAggressive);

        Assert.IsTrue(stressDef.Count > 0, "应检测到 2018 危机情景");
        Assert.IsTrue(stressAgg.Count > 0, "应检测到 2018 危机情景");

        var scenarioDef = stressDef.FirstOrDefault(s => s.ScenarioName.Contains("2018"));
        var scenarioAgg = stressAgg.FirstOrDefault(s => s.ScenarioName.Contains("2018"));

        Assert.IsNotNull(scenarioDef);
        Assert.IsNotNull(scenarioAgg);

        Assert.IsTrue(scenarioDef.FundReturnRate > 0, "防御型基金在 2018 期间应获得正收益");
        Assert.AreEqual("卓越防御", scenarioDef.DefenseRating, "熊市逆势盈利应评为卓越防御");

        Assert.IsTrue(scenarioAgg.FundReturnRate < -15.0m, "进取型基金在 2018 期间应录得大幅回撤");
        Assert.IsTrue(scenarioDef.MaxDrawdown < scenarioAgg.MaxDrawdown, "防御型最大回撤应显著小于进取型");
    }

    [TestMethod]
    public void RollingMetrics_CalculatesVolatilityAndSharpe()
    {
        var start = new DateTime(2022, 1, 1);
        // 生成 400 个交易日
        var navs = GenerateNavSequence(start, 400, dailyReturn: 0.0004m, vol: 0.015m, seed: 777);

        var rolling = QuantCalculator.CalculateRollingMetrics(navs, window: 60, riskFreeRate: 2.0m);

        Assert.IsNotNull(rolling);
        Assert.IsTrue(rolling.Count > 0, "应成功计算滚动时序");
        Assert.AreEqual(navs.Count - 60, rolling.Count, "滚动点数应严格等于 总样本数 - 窗口长度");

        foreach (var pt in rolling)
        {
            Assert.IsTrue(pt.RollingAnnualizedVol > 0m, "滚动年化波动率应为正数");
            Assert.IsTrue(pt.Date >= navs[60].Date, "首个滚动点日期应位于第60个交易日之后");
        }
    }
}
