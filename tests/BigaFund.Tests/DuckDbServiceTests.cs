using System.IO;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class DuckDbServiceTests
{
    private string _tempDbPath = string.Empty;
    private DuckDbService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"biga_test_{Guid.NewGuid():N}.duckdb");
        _service = new DuckDbService(_tempDbPath);
        _service.Initialize();
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (File.Exists(_tempDbPath))
            {
                File.Delete(_tempDbPath);
            }
            string walPath = $"{_tempDbPath}.wal";
            if (File.Exists(walPath))
            {
                File.Delete(walPath);
            }
        }
        catch { }
    }

    [TestMethod]
    public async Task DuckDb_SaveAndRetrieveFundDetail_WorksAccurately()
    {
        // Arrange
        var testFund = new FundDetail
        {
            Code = "005827",
            Name = "易方达蓝筹精选混合",
            Type = "混合型-偏股",
            ManagerName = "张坤",
            ManagerTenure = "5年又280天",
            FundSize = "415.20亿元"
        };

        var baseDate = new DateTime(2023, 1, 1);
        for (int i = 0; i < 50; i++)
        {
            testFund.NavHistory.Add(new NavRecord
            {
                Date = baseDate.AddDays(i),
                UnitNav = 1.5000m + i * 0.01m,
                CumulativeNav = 2.0000m + i * 0.01m,
                DailyReturn = 0.5m
            });

            testFund.BenchmarkCsi300.Add(new BenchmarkRecord
            {
                Date = baseDate.AddDays(i),
                CumulativeReturnRate = 10.5m + i * 0.05m
            });

            testFund.PeerAverage.Add(new BenchmarkRecord
            {
                Date = baseDate.AddDays(i),
                CumulativeReturnRate = 8.2m + i * 0.03m
            });
        }

        // Act - Save
        await _service.SaveFundDetailAsync(testFund);

        // Act - Retrieve
        var (retrieved, updatedAt) = await _service.GetFundDetailAsync("005827");

        // Assert
        Assert.IsNotNull(retrieved);
        Assert.IsNotNull(updatedAt);
        Assert.AreEqual("005827", retrieved.Code);
        Assert.AreEqual("易方达蓝筹精选混合", retrieved.Name);
        Assert.AreEqual("张坤", retrieved.ManagerName);
        Assert.AreEqual(50, retrieved.NavHistory.Count);
        Assert.AreEqual(50, retrieved.BenchmarkCsi300.Count);
        Assert.AreEqual(50, retrieved.PeerAverage.Count);

        // Verify first and last records
        Assert.AreEqual(new DateTime(2023, 1, 1), retrieved.NavHistory[0].Date);
        Assert.AreEqual(1.5000m, retrieved.NavHistory[0].UnitNav);
        Assert.AreEqual(1.5000m + 49 * 0.01m, retrieved.NavHistory[^1].UnitNav);
    }

    [TestMethod]
    public async Task DuckDb_GetStatsAndClear_WorksCorrectly()
    {
        // Arrange
        var testFund = new FundDetail
        {
            Code = "110011",
            Name = "易方达中小盘",
            Type = "混合型-偏股"
        };

        for (int i = 0; i < 20; i++)
        {
            testFund.NavHistory.Add(new NavRecord
            {
                Date = new DateTime(2023, 1, 1).AddDays(i),
                UnitNav = 2.0m + i * 0.01m,
                CumulativeNav = 3.0m,
                DailyReturn = 0.1m
            });
        }

        await _service.SaveFundDetailAsync(testFund);

        // Act - Get stats
        var stats = await _service.GetStatsAsync();

        // Assert
        Assert.AreEqual(1, stats.FundCount);
        Assert.AreEqual(20, stats.TotalNavRecords);
        Assert.IsTrue(stats.FileSizeBytes > 0);

        // Act - Clear
        await _service.ClearAllDataAsync();
        var clearedStats = await _service.GetStatsAsync();

        // Assert cleared
        Assert.AreEqual(0, clearedStats.FundCount);
        Assert.AreEqual(0, clearedStats.TotalNavRecords);

        var (clearedFund, _) = await _service.GetFundDetailAsync("110011");
        Assert.IsNull(clearedFund);
    }

    [TestMethod]
    public async Task DuckDb_Favorites_Crud_WorksCorrectly()
    {
        // 1. 初始状态未收藏
        bool isFav = await _service.IsFavoriteAsync("000001");
        Assert.IsFalse(isFav);

        // 2. 添加收藏
        await _service.AddFavoriteAsync("000001", "华夏成长混合", "混合型-偏股");
        await _service.AddFavoriteAsync("161725", "招商中证白酒指数", "指数型-股票");

        // 3. 验证收藏状态与列表
        isFav = await _service.IsFavoriteAsync("000001");
        Assert.IsTrue(isFav);

        var list = await _service.GetFavoritesAsync();
        Assert.AreEqual(2, list.Count);
        Assert.IsTrue(list.Any(f => f.Code == "000001" && f.Name == "华夏成长混合"));
        Assert.IsTrue(list.Any(f => f.Code == "161725" && f.Name == "招商中证白酒指数"));

        // 4. 重复添加不报错 (ON CONFLICT DO NOTHING)
        await _service.AddFavoriteAsync("000001", "华夏成长混合", "混合型-偏股");
        list = await _service.GetFavoritesAsync();
        Assert.AreEqual(2, list.Count);

        // 5. 移除收藏
        await _service.RemoveFavoriteAsync("000001");
        isFav = await _service.IsFavoriteAsync("000001");
        Assert.IsFalse(isFav);

        list = await _service.GetFavoritesAsync();
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("161725", list[0].Code);
    }
}
