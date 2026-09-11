using System.IO;
using BigaFund.Models;
using BigaFund.Services;
using BigaFund.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class FundSectorClassificationTests
{
    [TestMethod]
    public void Test_FundSectorHelper_Detection()
    {
        // 人工智能 / 芯片 / 科技
        Assert.AreEqual(FundSectorHelper.SectorAI, FundSectorHelper.DetectSector("易方达中证人工智能主题ETF联接A"));
        Assert.AreEqual(FundSectorHelper.SectorAI, FundSectorHelper.DetectSector("华夏国证半导体芯片ETF联接A"));
        Assert.AreEqual(FundSectorHelper.SectorAI, FundSectorHelper.DetectSector("华夏中证5G通信主题ETF"));

        // 机械制造 / 高端装备
        Assert.AreEqual(FundSectorHelper.SectorMachinery, FundSectorHelper.DetectSector("广发中证工程机械ETF联接A"));
        Assert.AreEqual(FundSectorHelper.SectorMachinery, FundSectorHelper.DetectSector("汇添富高端制造股票A"));
        Assert.AreEqual(FundSectorHelper.SectorMachinery, FundSectorHelper.DetectSector("华夏中证装备产业ETF联接A"));

        // 化工 / 新材料
        Assert.AreEqual(FundSectorHelper.SectorChemical, FundSectorHelper.DetectSector("富国中证细分化工产业主题ETF联接A"));
        Assert.AreEqual(FundSectorHelper.SectorChemical, FundSectorHelper.DetectSector("建信中证细分化工产业主题ETF联接A"));
        Assert.AreEqual(FundSectorHelper.SectorChemical, FundSectorHelper.DetectSector("易方达中证新材料主题ETF联接A"));

        // 金融 (银行 / 证券 / 保险 / 地产)
        Assert.AreEqual(FundSectorHelper.SectorFinance, FundSectorHelper.DetectSector("广发中证全指金融地产ETF联接A"));
        Assert.AreEqual(FundSectorHelper.SectorFinance, FundSectorHelper.DetectSector("国泰国证证券行业指数A"));
        Assert.AreEqual(FundSectorHelper.SectorFinance, FundSectorHelper.DetectSector("华宝中证银行ETF联接A"));

        // 医药 / 消费 / 新能源 / 军工 / 周期
        Assert.AreEqual(FundSectorHelper.SectorMedical, FundSectorHelper.DetectSector("中欧医疗健康混合A"));
        Assert.AreEqual(FundSectorHelper.SectorConsumer, FundSectorHelper.DetectSector("招商中证白酒指数A"));
        Assert.AreEqual(FundSectorHelper.SectorNewEnergy, FundSectorHelper.DetectSector("农银汇理新能源主题混合A"));
        Assert.AreEqual(FundSectorHelper.SectorMilitary, FundSectorHelper.DetectSector("富国中证军工指数A"));
        Assert.AreEqual(FundSectorHelper.SectorResources, FundSectorHelper.DetectSector("国泰中证煤炭ETF联接A"));

        // 宽基指数
        Assert.AreEqual(FundSectorHelper.SectorBroad, FundSectorHelper.DetectSector("华泰柏瑞沪深300ETF"));
    }

    [TestMethod]
    public void Test_FundSectorHelper_MatchesSector()
    {
        string aiFund = "易方达中证人工智能主题ETF";
        Assert.IsTrue(FundSectorHelper.MatchesSector(aiFund, "指数型", FundSectorHelper.SectorAll));
        Assert.IsTrue(FundSectorHelper.MatchesSector(aiFund, "指数型", FundSectorHelper.SectorAI));
        Assert.IsFalse(FundSectorHelper.MatchesSector(aiFund, "指数型", FundSectorHelper.SectorChemical));

        string chemFund = "富国中证细分化工产业主题ETF";
        Assert.IsTrue(FundSectorHelper.MatchesSector(chemFund, "指数型", FundSectorHelper.SectorChemical));
        Assert.IsFalse(FundSectorHelper.MatchesSector(chemFund, "指数型", FundSectorHelper.SectorFinance));

        string finFund = "广发金融地产ETF";
        Assert.IsTrue(FundSectorHelper.MatchesSector(finFund, "指数型", FundSectorHelper.SectorFinance));
    }

    [TestMethod]
    public async Task Test_DuckDb_MarketCatalog_SectorFilter()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"biga_sector_test_{Guid.NewGuid():N}.duckdb");
        var duckDb = new DuckDbService(testDbPath);

        try
        {
            var testItems = new List<MarketCatalogItem>
            {
                new() { Code = "167301", Name = "易方达中证人工智能主题ETF联接A", Type = "指数型-股票", Pinyin = "YFDZZRGZNZTETFLJA" },
                new() { Code = "012706", Name = "富国中证细分化工产业主题ETF联接A", Type = "指数型-股票", Pinyin = "FGZZXFHGCYZTETFLJA" },
                new() { Code = "018446", Name = "广发中证工程机械ETF联接A", Type = "指数型-股票", Pinyin = "GFZZGCJXETFLJA" },
                new() { Code = "001469", Name = "广发中证全指金融地产ETF联接A", Type = "指数型-股票", Pinyin = "GFZZQZJRDCETFLJA" },
                new() { Code = "000001", Name = "华夏成长混合", Type = "混合型-灵活", Pinyin = "HXCZHH" }
            };

            await duckDb.SaveMarketCatalogAsync(testItems);

            // 1. 检索人工智能板块
            var aiResults = await duckDb.SearchMarketCatalogAsync("", "全部", FundSectorHelper.SectorAI, 10);
            Assert.AreEqual(1, aiResults.Count);
            Assert.AreEqual("167301", aiResults[0].Code);
            Assert.AreEqual(FundSectorHelper.SectorAI, aiResults[0].Sector);

            // 2. 检索化工板块
            var chemResults = await duckDb.SearchMarketCatalogAsync("", "全部", FundSectorHelper.SectorChemical, 10);
            Assert.AreEqual(1, chemResults.Count);
            Assert.AreEqual("012706", chemResults[0].Code);
            Assert.AreEqual(FundSectorHelper.SectorChemical, chemResults[0].Sector);

            // 3. 检索机械制造板块
            var mechResults = await duckDb.SearchMarketCatalogAsync("", "全部", FundSectorHelper.SectorMachinery, 10);
            Assert.AreEqual(1, mechResults.Count);
            Assert.AreEqual("018446", mechResults[0].Code);
            Assert.AreEqual(FundSectorHelper.SectorMachinery, mechResults[0].Sector);

            // 4. 检索金融板块
            var finResults = await duckDb.SearchMarketCatalogAsync("", "全部", FundSectorHelper.SectorFinance, 10);
            Assert.AreEqual(1, finResults.Count);
            Assert.AreEqual("001469", finResults[0].Code);
            Assert.AreEqual(FundSectorHelper.SectorFinance, finResults[0].Sector);

            // 5. 全部板块检索
            var allResults = await duckDb.SearchMarketCatalogAsync("", "全部", FundSectorHelper.SectorAll, 10);
            Assert.AreEqual(5, allResults.Count);
        }
        finally
        {
            if (File.Exists(testDbPath))
            {
                try { File.Delete(testDbPath); } catch { }
            }
        }
    }

    [TestMethod]
    public void Test_MainViewModel_SectorSwitching()
    {
        var vm = new MainViewModel();

        // 默认全部板块
        Assert.AreEqual(FundSectorHelper.SectorAll, vm.SelectedSector);
        Assert.IsTrue(vm.QuickPicks.Count > 0);

        // 切换至人工智能
        vm.SelectedSector = FundSectorHelper.SectorAI;
        Assert.IsTrue(vm.QuickPicks.Any(p => p.Code == "167301"));
        Assert.IsTrue(vm.QuickPicks.All(p => FundSectorHelper.MatchesSector(p.Name, p.Type, FundSectorHelper.SectorAI)));

        // 切换至机械制造
        vm.SelectedSector = FundSectorHelper.SectorMachinery;
        Assert.IsTrue(vm.QuickPicks.Any(p => p.Code == "018446" || p.Code == "001725"));

        // 切换至化工
        vm.SelectedSector = FundSectorHelper.SectorChemical;
        Assert.IsTrue(vm.QuickPicks.Any(p => p.Code == "012706"));

        // 切换至金融
        vm.SelectedSector = FundSectorHelper.SectorFinance;
        Assert.IsTrue(vm.QuickPicks.Any(p => p.Code == "001469" || p.Code == "161027"));
    }
}
