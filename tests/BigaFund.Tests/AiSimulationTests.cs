using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BigaFund.Models;
using BigaFund.Services;

namespace BigaFund.Tests;

[TestClass]
public class AiSimulationTests
{
    private string _testDbPath = string.Empty;
    private DuckDbService _duckDb = null!;
    private FundDataService _dataService = null!;
    private AiSimulationService _simService = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"biga_test_sim_{Guid.NewGuid():N}.duckdb");
        _duckDb = new DuckDbService(_testDbPath);
        _duckDb.Initialize();
        _dataService = new FundDataService(_duckDb);
        _simService = new AiSimulationService(_dataService);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch
        {
            // 忽略临时文件占用
        }
    }

    [TestMethod]
    public async Task TestAccountInitialization_DefaultsTo100kCash()
    {
        var account = await _simService.GetAccountAsync(false);

        Assert.IsNotNull(account);
        Assert.AreEqual(100000m, account.InitialCash);
        Assert.AreEqual(100000m, account.Cash);
        Assert.AreEqual(100000m, account.TotalAsset);
        Assert.AreEqual(0m, account.TotalMarketValue);
        Assert.AreEqual(0m, account.TotalProfit);
        Assert.AreEqual(0m, account.TotalProfitRate);
        Assert.AreEqual(0, account.Positions.Count);
        Assert.AreEqual(0, account.Trades.Count);
    }

    [TestMethod]
    public async Task TestAiAllocationProposal_BalancedStrategy_SumsToTotalCapital()
    {
        decimal capital = 100000m;
        var proposal = await _simService.GenerateAiAllocationProposalAsync(capital, "BALANCED");

        Assert.IsNotNull(proposal);
        Assert.IsTrue(proposal.Items.Count >= 3);
        Assert.AreEqual(capital, proposal.AllocatedAmount + proposal.ReservedCash);

        foreach (var item in proposal.Items)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.FundCode));
            Assert.IsTrue(item.TargetAmount > 0);
            Assert.IsTrue(item.WeightPercent > 0);
            Assert.IsTrue(item.ConditionsPassed.Count > 0);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.DetailedRationale));
        }
    }

    [TestMethod]
    public async Task TestExecuteBuy_DeductsCashAndCalculatesSharesAndFee()
    {
        decimal buyAmount = 20000m;
        var (success, msg) = await _simService.ExecuteBuyAsync("510300", buyAmount, "测试买入", false);

        Assert.IsTrue(success, msg);

        var account = await _simService.GetAccountAsync(false);
        Assert.AreEqual(80000m, account.Cash);
        Assert.AreEqual(1, account.Positions.Count);

        var pos = account.Positions[0];
        Assert.AreEqual("510300", pos.FundCode);
        Assert.AreEqual(buyAmount, pos.TotalCost);
        Assert.IsTrue(pos.Shares > 0);
        Assert.IsTrue(pos.CostBasis > 0);

        // 验证交易流水记录
        Assert.AreEqual(1, account.Trades.Count);
        var trade = account.Trades[0];
        Assert.AreEqual("510300", trade.FundCode);
        Assert.AreEqual(buyAmount, trade.Amount);
        Assert.AreEqual(SimulatedTradeAction.ManualBuy, trade.Action);
        Assert.IsTrue(trade.Fee > 0); // 申购费
    }

    [TestMethod]
    public async Task TestExecuteSell_PartialSell_CalculatesRealizedProfitAndReturnsCash()
    {
        // 先买入 50000 元
        await _simService.ExecuteBuyAsync("000001", 50000m, "建仓", false);
        var accountAfterBuy = await _simService.GetAccountAsync(false);
        var pos = accountAfterBuy.Positions.First(p => p.FundCode == "000001");
        decimal initialShares = pos.Shares;

        // 卖出一半份额
        decimal sellShares = Math.Round(initialShares / 2, 2);
        var (sellSuccess, sellMsg) = await _simService.ExecuteSellAsync("000001", sellShares, "止盈赎回", false);

        Assert.IsTrue(sellSuccess, sellMsg);

        var accountAfterSell = await _simService.GetAccountAsync(false);
        Assert.IsTrue(accountAfterSell.Cash > 50000m); // 现金回笼
        var posAfter = accountAfterSell.Positions.First(p => p.FundCode == "000001");
        Assert.IsTrue(posAfter.Shares < initialShares);

        // 验证卖出交易流水
        var sellTrade = accountAfterSell.Trades.First(t => t.Action == SimulatedTradeAction.ManualSell);
        Assert.AreEqual("000001", sellTrade.FundCode);
        Assert.AreEqual(sellShares, sellTrade.Shares);
        Assert.IsTrue(sellTrade.Fee > 0); // 赎回费
    }

    [TestMethod]
    public async Task TestExecuteSell_FullSell_RemovesPosition()
    {
        await _simService.ExecuteBuyAsync("000001", 30000m, "建仓", false);
        var account = await _simService.GetAccountAsync(false);
        var pos = account.Positions.First(p => p.FundCode == "000001");

        // 全额清仓
        await _simService.ExecuteSellAsync("000001", pos.Shares, "全额清仓", false);

        var accountAfter = await _simService.GetAccountAsync(false);
        Assert.AreEqual(0, accountAfter.Positions.Count);
        Assert.IsTrue(accountAfter.Cash > 95000m);
    }

    [TestMethod]
    public async Task TestExecuteAiBuildProposal_ExecutesMultiConditionAllocation()
    {
        var proposal = await _simService.GenerateAiAllocationProposalAsync(100000m, "BALANCED");
        var (success, msg) = await _simService.ExecuteAiBuildProposalAsync(proposal);

        Assert.IsTrue(success, msg);

        var account = await _simService.GetAccountAsync(false);
        Assert.IsTrue(account.Positions.Count >= 3);
        Assert.IsTrue(account.Cash < 20000m); // 大部分已建仓
        Assert.AreEqual(proposal.Items.Count, account.Trades.Count);

        foreach (var trade in account.Trades)
        {
            Assert.AreEqual(SimulatedTradeAction.AiInitialBuild, trade.Action);
            Assert.IsTrue(trade.Reason.Contains("AI智能建仓"));
        }
    }

    [TestMethod]
    public async Task TestDiagnoseHoldings_ProvidesActionableAdvice()
    {
        await _simService.ExecuteBuyAsync("510300", 20000m, "底仓", false);
        var diagnoses = await _simService.DiagnoseHoldingsAsync();

        Assert.IsNotNull(diagnoses);
        Assert.AreEqual(1, diagnoses.Count);
        Assert.AreEqual("510300", diagnoses[0].FundCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(diagnoses[0].ActionProposal));
        Assert.IsFalse(string.IsNullOrWhiteSpace(diagnoses[0].Rationale));
    }

    [TestMethod]
    public async Task TestResetAccount_ClearsAllTradesAndPositionsAndRestores100k()
    {
        await _simService.ExecuteBuyAsync("510300", 30000m, "买入1", false);
        await _simService.ExecuteBuyAsync("000001", 30000m, "买入2", false);

        var accountBefore = await _simService.GetAccountAsync(false);
        Assert.AreEqual(2, accountBefore.Positions.Count);
        Assert.AreEqual(2, accountBefore.Trades.Count);

        // 重置
        await _simService.ResetAccountAsync(100000m);

        var accountAfter = await _simService.GetAccountAsync(false);
        Assert.AreEqual(100000m, accountAfter.Cash);
        Assert.AreEqual(100000m, accountAfter.TotalAsset);
        Assert.AreEqual(0, accountAfter.Positions.Count);
        Assert.AreEqual(0, accountAfter.Trades.Count);
    }
}
