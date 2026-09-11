using System.IO;
using System.Text;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase5Tests
{
    [TestMethod]
    public void Test_PortfolioEngine_CalculateLookThroughHoldings_AggregationAndConsensus()
    {
        // 模拟两只成分基金: 基金 A (权重 60%), 基金 B (权重 40%)
        var fundA = new FundDetail
        {
            Code = "000001",
            Name = "华夏成长精选",
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 8.0m, Industry = "食品饮料" },
                new() { StockCode = "300750", StockName = "宁德时代", WeightPercent = 6.0m, Industry = "电力设备" },
                new() { StockCode = "000858", StockName = "五粮液", WeightPercent = 5.0m, Industry = "食品饮料" }
            }
        };

        var fundB = new FundDetail
        {
            Code = "000171",
            Name = "易方达高端制造",
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 5.0m, Industry = "食品饮料" },
                new() { StockCode = "601318", StockName = "中国平安", WeightPercent = 7.0m, Industry = "非银金融" },
                new() { StockCode = "300750", StockName = "宁德时代", WeightPercent = 4.0m, Industry = "电力设备" }
            }
        };

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (fundA, 60m),
            (fundB, 40m)
        };

        var result = PortfolioEngine.CalculateLookThroughHoldings(components);

        // 1. 验证穿透股票总数与共同重仓统计
        Assert.AreEqual(4, result.TotalUniqueStocks);
        Assert.AreEqual(2, result.ConsensusStockCount); // 贵州茅台与宁德时代被 2 只基金同时持有

        // 2. 验证各标的穿透权重计算精确性
        // 贵州茅台: 60% * 8% + 40% * 5% = 4.8% + 2.0% = 6.80%
        var maotai = result.TopHoldings.FirstOrDefault(h => h.StockCode == "600519");
        Assert.IsNotNull(maotai);
        Assert.AreEqual(6.80m, maotai.PortfolioWeight);
        Assert.AreEqual(2, maotai.FundCount);
        Assert.IsTrue(maotai.IsConsensusHeavy);
        Assert.AreEqual("食品饮料", maotai.Industry);
        Assert.IsTrue(maotai.ContributingFunds.Contains("华夏成长精选"));
        Assert.IsTrue(maotai.ContributingFunds.Contains("易方达高端制造"));

        // 宁德时代: 60% * 6% + 40% * 4% = 3.6% + 1.6% = 5.20%
        var catl = result.TopHoldings.FirstOrDefault(h => h.StockCode == "300750");
        Assert.IsNotNull(catl);
        Assert.AreEqual(5.20m, catl.PortfolioWeight);
        Assert.AreEqual(2, catl.FundCount);
        Assert.IsTrue(catl.IsConsensusHeavy);

        // 五粮液: 60% * 5% = 3.00%
        var wly = result.TopHoldings.FirstOrDefault(h => h.StockCode == "000858");
        Assert.IsNotNull(wly);
        Assert.AreEqual(3.00m, wly.PortfolioWeight);
        Assert.AreEqual(1, wly.FundCount);
        Assert.IsFalse(wly.IsConsensusHeavy);

        // 中国平安: 40% * 7% = 2.80%
        var pingan = result.TopHoldings.FirstOrDefault(h => h.StockCode == "601318");
        Assert.IsNotNull(pingan);
        Assert.AreEqual(2.80m, pingan.PortfolioWeight);
        Assert.AreEqual(1, pingan.FundCount);

        // 3. 验证 CR10 与总穿透权重
        decimal expectedTotalWeight = 6.80m + 5.20m + 3.00m + 2.80m; // 17.80%
        Assert.AreEqual(expectedTotalWeight, result.TotalHoldingsWeight);
        Assert.AreEqual(expectedTotalWeight, result.PortfolioCr10);

        // 4. 验证申万行业赛道穿透暴露
        // 食品饮料: 6.80% + 3.00% = 9.80%
        var foodIndustry = result.IndustryExposures.FirstOrDefault(i => i.IndustryName == "食品饮料");
        Assert.IsNotNull(foodIndustry);
        Assert.AreEqual(9.80m, foodIndustry.PortfolioWeight);
        Assert.AreEqual(2, foodIndustry.StockCount);
        Assert.AreEqual(Math.Round(9.80m / 17.80m * 100m, 1), foodIndustry.RatioOfIdentified);

        // 电力设备: 5.20%
        var powerIndustry = result.IndustryExposures.FirstOrDefault(i => i.IndustryName == "电力设备");
        Assert.IsNotNull(powerIndustry);
        Assert.AreEqual(5.20m, powerIndustry.PortfolioWeight);
        Assert.AreEqual(1, powerIndustry.StockCount);

        // 5. 验证诊断报告
        Assert.IsTrue(result.LookThroughSummary.Contains("底层穿透识别 4 只重仓股票"));
        Assert.IsTrue(result.LookThroughSummary.Contains("抱团共振"));
    }

    [TestMethod]
    public void Test_PortfolioEngine_RunMonteCarloSimulation_GBMProperties()
    {
        decimal annualizedReturn = 12.0m; // 12% 年化
        decimal annualizedVol = 18.0m;    // 18% 年化波动率
        int horizon = 250;
        int runs = 1000;

        var mc = PortfolioEngine.RunMonteCarloSimulation(annualizedReturn, annualizedVol, horizon, runs);

        Assert.IsNotNull(mc);
        Assert.AreEqual(250, mc.HorizonTradingDays);
        Assert.AreEqual(1000, mc.SimulationRuns);

        // 1. 验证收益率百分位单调性: 5% (悲观) <= 50% (中位) <= 95% (乐观)
        Assert.IsTrue(mc.Percentile5Return < mc.MedianReturn, $"Percentile5 ({mc.Percentile5Return}) 应该小于 Median ({mc.MedianReturn})");
        Assert.IsTrue(mc.MedianReturn < mc.Percentile95Return, $"Median ({mc.MedianReturn}) 应该小于 Percentile95 ({mc.Percentile95Return})");

        // 2. 验证推演期末净值单调性: EndNav5 < EndNavMedian < EndNav95
        Assert.IsTrue(mc.EndNav5 < mc.EndNavMedian);
        Assert.IsTrue(mc.EndNavMedian < mc.EndNav95);

        // 3. 验证破本金概率位于合理区间 [0%, 100%]
        Assert.IsTrue(mc.ProbabilityOfLoss >= 0m && mc.ProbabilityOfLoss <= 100m);

        // 4. 验证模拟预期回撤为非负
        Assert.IsTrue(mc.ExpectedSimulatedDrawdown >= 0m);

        // 5. 验证结论包含关键推演指标
        Assert.IsTrue(mc.SimulationSummary.Contains("几何布朗运动 (GBM)"));
        Assert.IsTrue(mc.SimulationSummary.Contains("50% 中位稳态"));
    }

    [TestMethod]
    public void Test_CompoundInterest_InflationAndFeeErosionMath()
    {
        decimal principal = 10000m;
        decimal periodic = 1000m;
        int years = 10;
        int periodsPerYear = 12;
        int totalPeriods = years * periodsPerYear;

        double annualRate = 0.08;     // 8.0% 预期收益率
        double inflationRate = 0.025; // 2.5% 通胀率
        double feeRate = 0.012;       // 1.2% 基金综合费率

        double netAnnualRate = Math.Max(0.0, annualRate - feeRate); // 6.8%
        double netPeriodRate = netAnnualRate / periodsPerYear;
        double grossPeriodRate = annualRate / periodsPerYear;

        decimal totalInvested = principal + periodic * totalPeriods; // 10000 + 1000 * 120 = 130,000 元
        Assert.AreEqual(130000m, totalInvested);

        // 1. 名义扣费后期末资产
        double fvP = (double)principal * Math.Pow(1.0 + netPeriodRate, totalPeriods);
        double fvPer = (double)periodic * (Math.Pow(1.0 + netPeriodRate, totalPeriods) - 1.0) / netPeriodRate;
        double netNominalAsset = fvP + fvPer;

        // 2. 无费率理想总资产
        double fvGrossP = (double)principal * Math.Pow(1.0 + grossPeriodRate, totalPeriods);
        double fvGrossPer = (double)periodic * (Math.Pow(1.0 + grossPeriodRate, totalPeriods) - 1.0) / grossPeriodRate;
        double grossAsset = fvGrossP + fvGrossPer;

        // 3. 费率磨损摩擦
        double feeDrag = grossAsset - netNominalAsset;
        Assert.IsTrue(feeDrag > 0, "管理费应当造成期末财富磨损损耗");

        // 4. 真实抗通胀购买力资产
        double realAsset = netNominalAsset / Math.Pow(1.0 + inflationRate, years);
        double inflationErosion = netNominalAsset - realAsset;
        Assert.IsTrue(inflationErosion > 0, "通胀应当造成实际购买力缩水");

        // 5. 验证相对大小关系: 理想资产 > 扣费后名义资产 > 真实购买力资产 > 投入本金
        Assert.IsTrue(grossAsset > netNominalAsset);
        Assert.IsTrue(netNominalAsset > realAsset);
        Assert.IsTrue(realAsset > (double)totalInvested, "8%年化收益率在扣除1.2%费率与2.5%通胀后净收益仍大于本金投入");
    }

    [TestMethod]
    public void Test_ExportService_PortfolioHtml_ContainsLookThroughAndMonteCarlo()
    {
        string tempHtml = Path.Combine(Path.GetTempPath(), $"biga_portfolio_test_{Guid.NewGuid():N}.html");
        try
        {
            var portfolio = new PortfolioResult
            {
                PortfolioName = "机构多因子稳健精选组合",
                TotalReturn = 18.5m,
                AnnualizedReturn = 11.2m,
                AnnualizedVolatility = 14.3m,
                MaxDrawdown = 9.8m,
                SharpeRatio = 1.15m,
                LookThroughResult = new PortfolioLookThroughResult
                {
                    TotalUniqueStocks = 15,
                    ConsensusStockCount = 3,
                    TotalHoldingsWeight = 42.5m,
                    PortfolioCr10 = 32.8m,
                    LookThroughSummary = "组合底层共穿透覆盖 15 只股票，存在 3 只共同重仓标的，无过度隐性抱团风险。",
                    TopHoldings = new List<PortfolioLookThroughHolding>
                    {
                        new() { StockCode = "600519", StockName = "贵州茅台", PortfolioWeight = 6.5m, Industry = "食品饮料", FundCount = 2, ContributingFunds = "基金A, 基金B" }
                    },
                    IndustryExposures = new List<PortfolioLookThroughIndustry>
                    {
                        new() { IndustryName = "食品饮料", PortfolioWeight = 12.5m, StockCount = 2, RatioOfIdentified = 29.4m }
                    }
                },
                MonteCarloResult = new MonteCarloSimulationResult
                {
                    SimulationRuns = 1000,
                    HorizonTradingDays = 250,
                    Percentile95Return = 28.5m,
                    MedianReturn = 10.8m,
                    Percentile5Return = -8.2m,
                    ProbabilityOfLoss = 15.6m,
                    ExpectedSimulatedDrawdown = 12.4m,
                    EndNav95 = 1.2850m,
                    EndNavMedian = 1.1080m,
                    EndNav5 = 0.9180m,
                    SimulationSummary = "基于 GBM 几何布朗运动进行了 1000 次蒙特卡洛模拟推演。"
                }
            };

            var components = new List<PortfolioItem>
            {
                new() { Code = "000001", Name = "华夏成长", WeightPercent = 50m },
                new() { Code = "000171", Name = "易方达裕丰", WeightPercent = 50m }
            };

            ExportService.ExportPortfolioToHtml(tempHtml, components, portfolio);

            Assert.IsTrue(File.Exists(tempHtml), "HTML 文件应当成功导出");
            string htmlContent = File.ReadAllText(tempHtml, Encoding.UTF8);

            // 验证是否包含了底层穿透和蒙特卡洛的关键标题与标签
            Assert.IsTrue(htmlContent.Contains("底层资产穿透透视与重仓个股合并"), "必须包含底层穿透段落");
            Assert.IsTrue(htmlContent.Contains("贵州茅台"), "必须包含穿透重仓股票");
            Assert.IsTrue(htmlContent.Contains("共同重仓"), "必须包含共同重仓徽章");
            Assert.IsTrue(htmlContent.Contains("穿透申万行业赛道配置敞口分布"), "必须包含申万行业暴露表");
            Assert.IsTrue(htmlContent.Contains("前瞻性蒙特卡洛随机漫步资产推演"), "必须包含蒙特卡洛推演段落");
            Assert.IsTrue(htmlContent.Contains("95% 乐观上限 (1Y)"), "必须包含蒙特卡洛乐观上限卡片");
            Assert.IsTrue(htmlContent.Contains("5% 悲观底线 (1Y)"), "必须包含蒙特卡洛悲观下限卡片");
            Assert.IsTrue(htmlContent.Contains("本金亏损破本概率"), "必须包含本金破本概率");
        }
        finally
        {
            if (File.Exists(tempHtml))
            {
                try { File.Delete(tempHtml); } catch { }
            }
        }
    }
}
