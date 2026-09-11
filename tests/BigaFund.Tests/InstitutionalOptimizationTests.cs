using System.IO;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationTests
{
    [TestMethod]
    public void Test_VaR_And_CVaR_Calculation()
    {
        // 模拟100天的收益率序列，包含5个极端下跌日（-5%, -6%, -7%, -8%, -10%）
        var returns = new List<double>();
        for (int i = 0; i < 95; i++)
        {
            returns.Add(0.002); // 正常正收益 +0.2%
        }
        returns.Add(-0.05);
        returns.Add(-0.06);
        returns.Add(-0.07);
        returns.Add(-0.08);
        returns.Add(-0.10);

        var (var95, cvar95) = QuantCalculator.CalculateVaRAndCVaR(returns, 0.95);

        // 95% 历史分位数损失应约为 5%~6%，CVaR（尾部期望损失）应大于 VaR
        Assert.IsTrue(var95 >= 5.0m, $"VaR95 应大于等于 5.0%, 实际: {var95}");
        Assert.IsTrue(cvar95 >= var95, $"CVaR95 ({cvar95}) 应大于等于 VaR95 ({var95})");
        Assert.IsTrue(cvar95 <= 10.0m, $"CVaR95 应小于等于极端最大损失 10.0%, 实际: {cvar95}");
    }

    [TestMethod]
    public void Test_Simplex_Projection()
    {
        double[] unconstrained = [0.8, -0.4, 1.2, 0.0];
        double[] projected = PortfolioEngine.ProjectToSimplex(unconstrained);

        Assert.AreEqual(4, projected.Length);
        double sum = 0;
        foreach (var w in projected)
        {
            Assert.IsTrue(w >= -1e-6, $"投影权重应非负, 实际: {w}");
            sum += w;
        }
        Assert.AreEqual(1.0, sum, 1e-4, "投影权重之和必须严格为 1.0");
    }

    [TestMethod]
    public void Test_MinVariance_AllocatesMoreToLowVolAsset()
    {
        // 构建两只基金：A 为高波（股），B 为低波（债）
        var fundA = new FundDetail
        {
            Code = "A",
            Name = "高波股票基金",
            Type = "股票型",
            NavHistory = new List<NavRecord>()
        };

        var fundB = new FundDetail
        {
            Code = "B",
            Name = "低波债券基金",
            Type = "债券型",
            NavHistory = new List<NavRecord>()
        };

        var start = new DateTime(2023, 1, 1);
        decimal navA = 1.0m;
        decimal navB = 1.0m;

        // 构造 100 个交易日净值：A 大幅震荡，B 平稳微升
        for (int i = 0; i < 100; i++)
        {
            var dt = start.AddDays(i);
            // A 波动 ±3%
            decimal retA = (i % 2 == 0) ? 0.03m : -0.028m;
            navA *= (1.0m + retA);

            // B 波动 ±0.1%
            decimal retB = 0.0003m;
            navB *= (1.0m + retB);

            fundA.NavHistory.Add(new NavRecord { Date = dt, UnitNav = navA, CumulativeNav = navA });
            fundB.NavHistory.Add(new NavRecord { Date = dt, UnitNav = navB, CumulativeNav = navB });
        }

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (fundA, 50m),
            (fundB, 50m)
        };

        var result = PortfolioEngine.CalculatePortfolio(components, riskFreeRate: 2.0m, timeRange: "ALL");

        Assert.IsNotNull(result.MinVarianceWeights);
        Assert.IsTrue(result.MinVarianceWeights.ContainsKey("A"));
        Assert.IsTrue(result.MinVarianceWeights.ContainsKey("B"));

        decimal weightA = result.MinVarianceWeights["A"];
        decimal weightB = result.MinVarianceWeights["B"];

        Assert.AreEqual(100.0m, weightA + weightB, 0.05m, "最小方差权重和应为 100%");
        // 低波基金 B 的配置权重必须远高于高波基金 A
        Assert.IsTrue(weightB > weightA, $"最小方差模型中低波资产权重 ({weightB}%) 应当高于高波资产权重 ({weightA}%)");
    }

    [TestMethod]
    public void Test_RiskParity_AllocatesInverseToVolatility()
    {
        // 构造两只不同波动的基金，测试风险平价（ERC）求解
        var fundA = new FundDetail { Code = "A", Name = "股票", Type = "股票型", NavHistory = new List<NavRecord>() };
        var fundB = new FundDetail { Code = "B", Name = "纯债", Type = "债券型", NavHistory = new List<NavRecord>() };

        var start = new DateTime(2023, 1, 1);
        decimal navA = 1.0m;
        decimal navB = 1.0m;

        for (int i = 0; i < 120; i++)
        {
            var dt = start.AddDays(i);
            decimal retA = (i % 2 == 0) ? 0.025m : -0.023m;
            decimal retB = 0.0002m;
            navA *= (1.0m + retA);
            navB *= (1.0m + retB);

            fundA.NavHistory.Add(new NavRecord { Date = dt, UnitNav = navA, CumulativeNav = navA });
            fundB.NavHistory.Add(new NavRecord { Date = dt, UnitNav = navB, CumulativeNav = navB });
        }

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (fundA, 50m),
            (fundB, 50m)
        };

        var result = PortfolioEngine.CalculatePortfolio(components, riskFreeRate: 2.0m, timeRange: "ALL");

        Assert.IsNotNull(result.RiskParityWeights);
        decimal weightA = result.RiskParityWeights["A"];
        decimal weightB = result.RiskParityWeights["B"];

        Assert.AreEqual(100.0m, weightA + weightB, 0.05m, "风险平价权重和应为 100%");
        Assert.IsTrue(weightB > weightA, $"风险平价模型中低波资产权重 ({weightB}%) 应显著大于高波资产 ({weightA}%)");
    }

    [TestMethod]
    public async Task Test_DuckDb_MarketCatalog_Appender_And_Search()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"biga_test_catalog_{Guid.NewGuid():N}.duckdb");
        var duckDb = new DuckDbService(tempDb);
        duckDb.Initialize();

        try
        {
            var items = new List<MarketCatalogItem>
            {
                new MarketCatalogItem { Code = "000001", Pinyin = "HXCZHH", Name = "华夏成长混合", Type = "混合型-偏股", PinyinFull = "HUAXIACHENGZHANGHUNHE", UpdatedAt = DateTime.Now },
                new MarketCatalogItem { Code = "005827", Pinyin = "YFDLKCX", Name = "易方达蓝筹精选混合", Type = "混合型-偏股", PinyinFull = "YIFANGDALANCHOUJINGXUAN", UpdatedAt = DateTime.Now },
                new MarketCatalogItem { Code = "000171", Pinyin = "YFDYF", Name = "易方达裕丰回报债券", Type = "债券型-混合债", PinyinFull = "YIFANGDAYUFENGHUIBAO", UpdatedAt = DateTime.Now }
            };

            await duckDb.SaveMarketCatalogAsync(items);

            int count = await duckDb.GetMarketCatalogCountAsync();
            Assert.AreEqual(3, count, "目录总条数应为3");

            // 拼音检索测试
            var searchPinyin = await duckDb.SearchMarketCatalogAsync("HXCZ", "全部", 10);
            Assert.AreEqual(1, searchPinyin.Count);
            Assert.AreEqual("000001", searchPinyin[0].Code);

            // 名称模糊检索测试
            var searchName = await duckDb.SearchMarketCatalogAsync("易方达", "全部", 10);
            Assert.AreEqual(2, searchName.Count);

            // 品类过滤检索测试
            var searchCat = await duckDb.SearchMarketCatalogAsync("易方达", "债券", 10);
            Assert.AreEqual(1, searchCat.Count);
            Assert.AreEqual("000171", searchCat[0].Code);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }
}
