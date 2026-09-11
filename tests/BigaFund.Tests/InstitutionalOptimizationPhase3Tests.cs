using System.Globalization;
using System.IO;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class InstitutionalOptimizationPhase3Tests
{
    [TestMethod]
    public void Test_FundStockHolding_And_RealtimeValuation_Models()
    {
        var holding = new FundStockHolding
        {
            StockCode = "600519",
            StockName = "贵州茅台",
            WeightPercent = 8.52m,
            ShareChange = "持仓",
            ReportDate = "2024-06-30",
            Industry = "食品饮料"
        };

        Assert.AreEqual("600519", holding.StockCode);
        Assert.AreEqual("贵州茅台", holding.StockName);
        Assert.AreEqual(8.52m, holding.WeightPercent);
        Assert.AreEqual("食品饮料", holding.Industry);

        var val = new RealtimeValuation
        {
            FundCode = "005827",
            FundName = "易方达蓝筹精选混合",
            UnitNav = 1.8500m,
            EstimatedNav = 1.8685m,
            EstimatedGrowthRate = 1.00m,
            ValuationTime = "2024-06-30 15:00",
            NavDate = "2024-06-28",
            DailyReturn = 0.85m
        };

        Assert.AreEqual("005827", val.FundCode);
        Assert.AreEqual(1.8500m, val.UnitNav);
        Assert.AreEqual(1.8685m, val.EstimatedNav);
        Assert.AreEqual(1.00m, val.EstimatedGrowthRate);
    }

    [TestMethod]
    public async Task Test_DuckDbService_Holdings_With_Industry_Persistence()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"bigafund_phase3_{Guid.NewGuid():N}.duckdb");
        try
        {
            var duckDb = new DuckDbService(tempDb);

            var detail = new FundDetail
            {
                Code = "TEST01",
                Name = "测试全行业穿透基金",
                Type = "混合型-偏股",
                Holdings = new List<FundStockHolding>
                {
                    new FundStockHolding
                    {
                        StockCode = "000858",
                        StockName = "五粮液",
                        WeightPercent = 9.20m,
                        ShareChange = "增持",
                        ReportDate = "2024-06-30",
                        Industry = "食品饮料"
                    },
                    new FundStockHolding
                    {
                        StockCode = "300750",
                        StockName = "宁德时代",
                        WeightPercent = 8.50m,
                        ShareChange = "减持",
                        ReportDate = "2024-06-30",
                        Industry = "电力设备"
                    },
                    new FundStockHolding
                    {
                        StockCode = "688981",
                        StockName = "中芯国际",
                        WeightPercent = 6.80m,
                        ShareChange = "持仓",
                        ReportDate = "2024-06-30",
                        Industry = "电子"
                    }
                },
                NavHistory = new List<NavRecord>
                {
                    new NavRecord { Date = new DateTime(2024, 1, 2), UnitNav = 1.0000m, CumulativeNav = 1.0000m },
                    new NavRecord { Date = new DateTime(2024, 1, 3), UnitNav = 1.0200m, CumulativeNav = 1.0200m }
                }
            };

            await duckDb.SaveFundDetailAsync(detail);

            var (retrieved, _) = await duckDb.GetFundDetailAsync("TEST01");
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(3, retrieved.Holdings.Count);

            var wly = retrieved.Holdings.Find(h => h.StockCode == "000858");
            Assert.IsNotNull(wly);
            Assert.AreEqual("五粮液", wly.StockName);
            Assert.AreEqual("食品饮料", wly.Industry);
            Assert.AreEqual(9.20m, wly.WeightPercent);

            var ndsd = retrieved.Holdings.Find(h => h.StockCode == "300750");
            Assert.IsNotNull(ndsd);
            Assert.AreEqual("电力设备", ndsd.Industry);

            var zxgj = retrieved.Holdings.Find(h => h.StockCode == "688981");
            Assert.IsNotNull(zxgj);
            Assert.AreEqual("电子", zxgj.Industry);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [TestMethod]
    public void Test_Brinson_Attribution_Engine_Mathematical_Identity()
    {
        // 构造三板块组合与基准配置
        // 组合：板块A(50%, 收益15%), 板块B(30%, 收益-5%), 板块C(20%, 收益8%)
        // 基准：板块A(30%, 收益10%), 板块B(50%, 收益0%), 板块C(20%, 收益5%)
        var sectors = new List<BrinsonSectorItem>
        {
            new BrinsonSectorItem
            {
                SectorName = "科技信息",
                PortfolioWeight = 50m,
                BenchmarkWeight = 30m,
                PortfolioReturn = 15.0m,
                BenchmarkReturn = 10.0m
            },
            new BrinsonSectorItem
            {
                SectorName = "消费医药",
                PortfolioWeight = 30m,
                BenchmarkWeight = 50m,
                PortfolioReturn = -5.0m,
                BenchmarkReturn = 0.0m
            },
            new BrinsonSectorItem
            {
                SectorName = "金融地产",
                PortfolioWeight = 20m,
                BenchmarkWeight = 20m,
                PortfolioReturn = 8.0m,
                BenchmarkReturn = 5.0m
            }
        };

        var result = BrinsonAttributionEngine.CalculateBrinsonAttribution(sectors);

        // 验证 R_P = 0.5*15 + 0.3*(-5) + 0.2*8 = 7.5 - 1.5 + 1.6 = 7.6%
        Assert.AreEqual(7.60m, result.TotalPortfolioReturn);

        // 验证 R_B = 0.3*10 + 0.5*0 + 0.2*5 = 3.0 + 0 + 1.0 = 4.0%
        Assert.AreEqual(4.00m, result.TotalBenchmarkReturn);

        // 验证超额收益 = 7.60 - 4.00 = 3.60%
        Assert.AreEqual(3.60m, result.TotalExcessReturn);

        // 核心数学恒等式验证：超额收益 == 资产配置效应 + 标的选择效应 + 交叉效应 (误差允许在保留两位小数引起的 0.02 以内)
        decimal sumEffects = result.TotalAllocationEffect + result.TotalSelectionEffect + result.TotalInteractionEffect;
        decimal diff = Math.Abs(result.TotalExcessReturn - sumEffects);
        Assert.IsTrue(diff <= 0.02m, $"Brinson 恒等式不成立: 超额={result.TotalExcessReturn}, 三项分解之和={sumEffects}");

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.SummaryAnalysis));
        Assert.IsTrue(result.SummaryAnalysis.Contains("跑赢基准"));
    }

    [TestMethod]
    public void Test_Brinson_Attribution_Automatic_Fund_Decomposition()
    {
        var fund = new FundDetail
        {
            Code = "000001",
            Name = "华夏成长混合",
            Holdings = new List<FundStockHolding>
            {
                new FundStockHolding { StockCode = "000001", StockName = "平安银行", WeightPercent = 40m, Industry = "银行" },
                new FundStockHolding { StockCode = "600036", StockName = "招商银行", WeightPercent = 30m, Industry = "银行" },
                new FundStockHolding { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 20m, Industry = "食品饮料" }
            },
            NavHistory = new List<NavRecord>
            {
                new NavRecord { Date = new DateTime(2023, 1, 1), CumulativeNav = 1.0000m, UnitNav = 1.0000m },
                new NavRecord { Date = new DateTime(2023, 12, 31), CumulativeNav = 1.2000m, UnitNav = 1.2000m }
            }
        };

        var bench = new List<BenchmarkRecord>
        {
            new BenchmarkRecord { Date = new DateTime(2023, 1, 1), CumulativeReturnRate = 0.0m },
            new BenchmarkRecord { Date = new DateTime(2023, 12, 31), CumulativeReturnRate = 10.0m }
        };

        var result = BrinsonAttributionEngine.CalculateFundBrinsonAttribution(fund, bench);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.SectorItems.Count >= 2);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.SummaryAnalysis));
    }

    [TestMethod]
    public void Test_Quant_MultiFactor_ScoreCard_Generation()
    {
        var detail = new FundDetail
        {
            Code = "001234",
            Name = "明星量化价值精选",
            Holdings = new List<FundStockHolding>
            {
                new FundStockHolding { StockCode = "A", WeightPercent = 5m },
                new FundStockHolding { StockCode = "B", WeightPercent = 5m },
                new FundStockHolding { StockCode = "C", WeightPercent = 5m },
                new FundStockHolding { StockCode = "D", WeightPercent = 5m },
                new FundStockHolding { StockCode = "E", WeightPercent = 5m },
                new FundStockHolding { StockCode = "F", WeightPercent = 5m },
                new FundStockHolding { StockCode = "G", WeightPercent = 5m },
                new FundStockHolding { StockCode = "H", WeightPercent = 5m },
                new FundStockHolding { StockCode = "I", WeightPercent = 5m },
                new FundStockHolding { StockCode = "J", WeightPercent = 5m }
            }
        };

        var metrics = new QuantMetrics
        {
            AnnualizedReturn = 18.5m,      // 优质年化收益
            MaxDrawdown = -12.0m,          // 优异回撤控制
            AnnualizedVolatility = 14.2m,  // 低波动
            SharpeRatio = 1.45m,           // 高夏普
            CalmarRatio = 1.54m,           // 高卡玛
            SortinoRatio = 1.80m,          // 高索提诺
            Alpha = 6.2m,
            VaR95 = 1.4m,
            RecoveryTradingDays = 35
        };

        var scoreCard = QuantCalculator.CalculateFundScore(detail, metrics);

        Assert.IsNotNull(scoreCard);
        Assert.IsTrue(scoreCard.OverallScore >= 80m && scoreCard.OverallScore <= 100m, $"综合评分应为优良或卓越, 实际: {scoreCard.OverallScore}");
        Assert.IsTrue(scoreCard.RatingGrade == "AAA 卓越" || scoreCard.RatingGrade == "AA 优良");
        Assert.IsTrue(scoreCard.ReturnScore >= 80m);
        Assert.IsTrue(scoreCard.RiskControlScore >= 80m);
        Assert.IsTrue(scoreCard.RiskAdjustedScore >= 80m);
        Assert.IsTrue(scoreCard.Strengths.Count > 0);
    }

    [TestMethod]
    public void Test_Portfolio_Rebalance_Order_Sheet_Generation()
    {
        decimal totalValue = 100000m; // 10 万元资金池

        var fundA = new FundDetail { Code = "001001", Name = "大盘成长A" };
        var fundB = new FundDetail { Code = "002002", Name = "红利低波B" };

        // 当前持仓：基金A 6万元 (60%)，基金B 4万元 (40%)
        var currentHoldings = new List<(FundDetail Fund, decimal CurrentAmount)>
        {
            (fundA, 60000m),
            (fundB, 40000m)
        };

        // 目标优化权重：基金A 30%，基金B 70%
        var targetWeights = new Dictionary<string, decimal>
        {
            ["001001"] = 30m,
            ["002002"] = 70m
        };

        var sheet = PortfolioEngine.GenerateRebalanceOrders(
            totalValue,
            currentHoldings,
            targetWeights,
            "最小方差方案",
            subscriptionFeeRate: 0.12m,
            redemptionFeeRate: 0.50m);

        Assert.IsNotNull(sheet);
        Assert.AreEqual(100000m, sheet.TotalPortfolioValue);
        Assert.AreEqual(2, sheet.Orders.Count);

        var orderA = sheet.Orders.Find(o => o.FundCode == "001001");
        var orderB = sheet.Orders.Find(o => o.FundCode == "002002");

        Assert.IsNotNull(orderA);
        Assert.IsNotNull(orderB);

        // 基金A应卖出 30000 元 (目标金额 30000，原金额 60000)
        Assert.AreEqual("卖出", orderA.Action);
        Assert.AreEqual(30000m, orderA.TradeAmount);
        Assert.AreEqual(150m, orderA.EstimatedFee); // 30000 * 0.50% = 150元

        // 基金B应买入 30000 元 (目标金额 70000，原金额 40000)
        Assert.AreEqual("买入", orderB.Action);
        Assert.AreEqual(30000m, orderB.TradeAmount);
        Assert.AreEqual(36m, orderB.EstimatedFee); // 30000 * 0.12% = 36元

        // 总买入 30000，总卖出 30000，净现金变动 0，双边换手率 30.0%
        Assert.AreEqual(30000m, sheet.TotalBuyAmount);
        Assert.AreEqual(30000m, sheet.TotalSellAmount);
        Assert.AreEqual(0m, sheet.NetCashChange);
        Assert.AreEqual(30.00m, sheet.TurnoverRate);
        Assert.AreEqual(186m, sheet.EstimatedTotalFees);
    }

    [TestMethod]
    public async Task Test_Live_Api_Holdings_And_Valuation_Fetch()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"bigafund_live_{Guid.NewGuid():N}.duckdb");
        try
        {
            var duckDb = new DuckDbService(tempDb);
            var fundService = new FundDataService(duckDb);

            // 1. 测试真实十大重仓股穿透拉取
            var holdings = await fundService.FetchRealHoldingsAsync("005827");
            Assert.IsNotNull(holdings);
            if (holdings.Count > 0)
            {
                Assert.IsTrue(holdings.Count <= 10);
                Assert.IsFalse(string.IsNullOrWhiteSpace(holdings[0].StockName));
                Assert.IsTrue(holdings[0].WeightPercent > 0m);
            }

            // 2. 测试盘中实时估值拉取
            var valuation = await fundService.GetRealtimeValuationAsync("005827");
            if (valuation != null)
            {
                Assert.AreEqual("005827", valuation.FundCode);
                Assert.IsTrue(valuation.UnitNav > 0m);
                Assert.IsFalse(string.IsNullOrWhiteSpace(valuation.FundName));
            }
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [TestMethod]
    public void Test_CalculatePortfolioBrinsonAttribution()
    {
        var fundA = new FundDetail
        {
            Code = "005827",
            Name = "易方达蓝筹精选",
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 9.5m, Industry = "食品饮料" },
                new() { StockCode = "000858", StockName = "五粮液", WeightPercent = 8.5m, Industry = "食品饮料" },
                new() { StockCode = "00700", StockName = "腾讯控股", WeightPercent = 9.0m, Industry = "互联网" }
            },
            NavHistory = new List<NavRecord>
            {
                new() { Date = new DateTime(2023, 1, 1), UnitNav = 1.0m, CumulativeNav = 1.0m },
                new() { Date = new DateTime(2023, 12, 31), UnitNav = 1.15m, CumulativeNav = 1.15m }
            }
        };

        var fundB = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘",
            Holdings = new List<FundStockHolding>
            {
                new() { StockCode = "300750", StockName = "宁德时代", WeightPercent = 8.0m, Industry = "新能源" }
            },
            NavHistory = new List<NavRecord>
            {
                new() { Date = new DateTime(2023, 1, 1), UnitNav = 1.0m, CumulativeNav = 1.0m },
                new() { Date = new DateTime(2023, 12, 31), UnitNav = 1.05m, CumulativeNav = 1.05m }
            }
        };

        var components = new List<(FundDetail Fund, decimal WeightPercent)>
        {
            (fundA, 60m),
            (fundB, 40m)
        };

        decimal benchmarkReturn = 8.0m;
        var result = BrinsonAttributionEngine.CalculatePortfolioBrinsonAttribution(components, benchmarkReturn);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.SectorItems.Count);

        // 组合加权收益: 60% * 15% + 40% * 5% = 9.0% + 2.0% = 11.0%
        Assert.AreEqual(11.0m, Math.Round(result.TotalPortfolioReturn, 1));
        Assert.AreEqual(8.0m, Math.Round(result.TotalBenchmarkReturn, 1));
        // 超额收益: 11.0% - 8.0% = 3.0%
        Assert.AreEqual(3.0m, Math.Round(result.TotalExcessReturn, 1));

        // 验证三效应之和恒等于总超额
        decimal sumEffects = result.TotalAllocationEffect + result.TotalSelectionEffect + result.TotalInteractionEffect;
        Assert.AreEqual(result.TotalExcessReturn, Math.Round(sumEffects, 2));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.SummaryAnalysis));
    }

    [TestMethod]
    public async Task Test_DuckDbService_GetAllStoredFunds_Populates_QuantScore()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"bigafund_score_{Guid.NewGuid():N}.duckdb");
        try
        {
            var duckDb = new DuckDbService(tempDb);
            var fund = new FundDetail
            {
                Code = "TEST99",
                Name = "量化综合评分测试基金",
                Type = "混合型-偏股",
                ManagerName = "量化经理",
                ManagerTenure = "5年",
                FundSize = "20亿元"
            };
            var navs = new List<NavRecord>();
            DateTime baseDate = new DateTime(2023, 1, 1);
            decimal nav = 1.0m;
            for (int i = 0; i < 260; i++)
            {
                nav *= (1m + 0.0008m);
                navs.Add(new NavRecord
                {
                    Date = baseDate.AddDays(i),
                    UnitNav = nav,
                    CumulativeNav = nav
                });
            }

            fund.NavHistory = navs;
            await duckDb.SaveFundDetailAsync(fund);

            var storedFunds = await duckDb.GetAllStoredFundsAsync();
            Assert.AreEqual(1, storedFunds.Count);
            var item = storedFunds[0];
            Assert.AreEqual("TEST99", item.Code);
            Assert.IsTrue(item.QuantScore > 0m, "QuantScore 应大于 0");
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.RatingGrade), "RatingGrade 不应为空");
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [TestMethod]
    public void Test_ExportService_SingleAndPortfolioHtml_ContainsInstitutionalSections()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"biga_export_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string singleReportPath = Path.Combine(tempDir, "SingleReport.html");
        string portfolioReportPath = Path.Combine(tempDir, "PortfolioReport.html");

        try
        {
            // 1. 测试单基金量化研报 HTML
            var fund = new FundDetail
            {
                Code = "005827",
                Name = "易方达蓝筹精选混合",
                Type = "混合型-偏股",
                ManagerName = "张坤",
                ManagerTenure = "10.5年",
                FundSize = "450亿元",
                Holdings = new List<FundStockHolding>
                {
                    new() { StockCode = "600519", StockName = "贵州茅台", WeightPercent = 9.8m, Industry = "食品饮料", ShareChange = "持仓不变", ReportDate = "2024Q4" },
                    new() { StockCode = "000858", StockName = "五粮液", WeightPercent = 8.5m, Industry = "食品饮料", ShareChange = "增持", ReportDate = "2024Q4" }
                },
                NavHistory = new List<NavRecord>
                {
                    new() { Date = new DateTime(2023, 1, 1), UnitNav = 1.0m, CumulativeNav = 1.0m },
                    new() { Date = new DateTime(2023, 12, 31), UnitNav = 1.25m, CumulativeNav = 1.25m }
                }
            };
            var metrics = QuantCalculator.CalculateMetrics(fund.NavHistory, "全周期");
            var backtest = new BacktestResult
            {
                StrategyName = "智能均线定投",
                StrategyDescription = "低估定投测试",
                TotalPeriods = 24,
                TotalInvested = 24000m,
                FinalAssetValue = 30000m,
                AnnualizedIrr = 18.5m,
                BuyAndHoldReturnRate = 25.0m
            };

            ExportService.ExportToHtml(singleReportPath, fund, metrics, backtest, fund.NavHistory);
            Assert.IsTrue(File.Exists(singleReportPath));
            string singleHtml = File.ReadAllText(singleReportPath);
            Assert.IsTrue(singleHtml.Contains("机构级五维量化综合评价"), "单基金报告应包含五维量化评价");
            Assert.IsTrue(singleHtml.Contains("最新季报十大重仓股透视与申万一级行业穿透"), "单基金报告应包含重仓股与申万行业穿透");
            Assert.IsTrue(singleHtml.Contains("机构级 Brinson-Fachler 业绩归因分解"), "单基金报告应包含 Brinson 归因");

            // 2. 测试组合资产配置研报 HTML
            var compList = new List<PortfolioItem>
            {
                new() { Code = "005827", Name = "易方达蓝筹精选混合", WeightPercent = 60m },
                new() { Code = "110011", Name = "易方达中小盘", WeightPercent = 40m }
            };
            var portfolioResult = new PortfolioResult
            {
                PortfolioName = "核心+卫星组合",
                StartDate = new DateTime(2023, 1, 1),
                EndDate = new DateTime(2023, 12, 31),
                TradingDays = 250,
                TotalReturn = 15.8m,
                AnnualizedReturn = 15.8m,
                AnnualizedVolatility = 14.2m,
                MaxDrawdown = -8.5m,
                SharpeRatio = 1.15m,
                RebalanceOrders = new RebalanceOrderSheet
                {
                    TotalPortfolioValue = 1000000m,
                    TargetSchemeName = "最大夏普比率组合",
                    TotalBuyAmount = 100000m,
                    TotalSellAmount = 100000m,
                    NetCashChange = 0m,
                    EstimatedTotalFees = 650m,
                    TurnoverRate = 10.0m,
                    Orders = new List<RebalanceOrderItem>
                    {
                        new() { FundCode = "005827", FundName = "易方达蓝筹精选", CurrentWeight = 60m, TargetWeight = 70m, TradeWeightChange = 10m, Action = "买入", TradeAmount = 100000m, EstimatedFee = 120m, TargetAmount = 700000m }
                    }
                },
                BrinsonAttribution = new BrinsonAttributionResult
                {
                    TotalPortfolioReturn = 15.8m,
                    TotalBenchmarkReturn = 10.0m,
                    TotalAllocationEffect = 2.5m,
                    TotalSelectionEffect = 3.0m,
                    TotalInteractionEffect = 0.3m,
                    SummaryAnalysis = "组合超额收益显著",
                    SectorItems = new List<BrinsonSectorItem>
                    {
                        new() { SectorName = "易方达蓝筹精选混合", PortfolioWeight = 60m, BenchmarkWeight = 50m, PortfolioReturn = 18m, BenchmarkReturn = 10m, AllocationEffect = 1.2m, SelectionEffect = 2.0m, InteractionEffect = 0.1m }
                    }
                }
            };

            ExportService.ExportPortfolioToHtml(portfolioReportPath, compList, portfolioResult);
            Assert.IsTrue(File.Exists(portfolioReportPath));
            string portHtml = File.ReadAllText(portfolioReportPath);
            Assert.IsTrue(portHtml.Contains("投资组合调仓交易决策单"), "组合研报应包含调仓交易决策单");
            Assert.IsTrue(portHtml.Contains("组合 Brinson-Fachler 业绩归因与超额分解"), "组合研报应包含组合 Brinson 业绩归因");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}

