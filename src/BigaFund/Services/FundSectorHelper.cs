using BigaFund.Models;

namespace BigaFund.Services;

/// <summary>
/// 基金行业/主题板块分类助手
/// 支持金融、人工智能、机械制造、化工、医药医疗、消费白酒、新能源、军工装备、周期资源等核心板块
/// </summary>
public static class FundSectorHelper
{
    public const string SectorAll = "全部板块";
    public const string SectorFinance = "金融";
    public const string SectorAI = "人工智能";
    public const string SectorMachinery = "机械制造";
    public const string SectorChemical = "化工";
    public const string SectorMedical = "医药医疗";
    public const string SectorConsumer = "消费白酒";
    public const string SectorNewEnergy = "新能源";
    public const string SectorMilitary = "军工装备";
    public const string SectorResources = "周期资源";
    public const string SectorBroad = "宽基综合";

    public static readonly List<string> MainSectors = new()
    {
        SectorAll,
        SectorFinance,
        SectorAI,
        SectorMachinery,
        SectorChemical,
        SectorMedical,
        SectorConsumer,
        SectorNewEnergy,
        SectorMilitary,
        SectorResources
    };

    private static readonly Dictionary<string, string[]> SectorKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        [SectorFinance] = new[] { "金融", "银行", "证券", "券商", "保险", "地产", "房地产", "多元金融", "金控", "财富", "金额" },
        [SectorAI] = new[] { "人工智能", "AI", "算力", "芯片", "半导体", "计算机", "软件", "电子", "智能", "机器人", "数字", "信创", "通信", "5G", "大数据", "云计算", "信息技术", "物联网", "智能车", "网络安全" },
        [SectorMachinery] = new[] { "机械", "制造", "高端制造", "装备", "工业", "机床", "自动化", "智能制造", "工程机械", "工业互联", "电气", "仪器" },
        [SectorChemical] = new[] { "化工", "化学", "新材料", "基础化工", "化纤", "涂料", "农药", "化肥", "聚合物", "氟化工", "磷化工", "精细化工", "材料" },
        [SectorMedical] = new[] { "医药", "医疗", "生物", "创新药", "中药", "健康", "疫苗", "器械", "生物医药", "精准医疗" },
        [SectorConsumer] = new[] { "消费", "白酒", "食品", "饮料", "家电", "乳业", "酒", "农业", "养殖", "商贸", "旅游", "免税" },
        [SectorNewEnergy] = new[] { "新能源", "光伏", "电池", "储能", "锂", "风电", "清洁能源", "电力设备", "绿电", "氢能", "碳中和" },
        [SectorMilitary] = new[] { "军工", "国防", "航天", "航空", "兵工", "船舶", "卫星" },
        [SectorResources] = new[] { "有色", "煤炭", "钢铁", "稀土", "石油", "石化", "油气", "采掘", "矿业", "黄金", "贵金属" }
    };

    /// <summary>
    /// 根据基金名称及品类智能识别判定所属板块
    /// </summary>
    public static string DetectSector(string? name, string? type = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            if (!string.IsNullOrEmpty(type) && type.Contains("债")) return "固收债券";
            if (!string.IsNullOrEmpty(type) && type.Contains("货币")) return "货币市场";
            return SectorBroad;
        }

        string upperName = name.ToUpperInvariant();

        // 优先按特征明确的主题板块匹配
        foreach (var kv in SectorKeywords)
        {
            foreach (var kw in kv.Value)
            {
                if (upperName.Contains(kw.ToUpperInvariant()))
                {
                    return kv.Key;
                }
            }
        }

        if (!string.IsNullOrEmpty(type))
        {
            if (type.Contains("债")) return "固收债券";
            if (type.Contains("货币")) return "货币市场";
        }

        // 宽基指数 / 混合平衡
        if (upperName.Contains("300") || upperName.Contains("500") || upperName.Contains("800") ||
            upperName.Contains("1000") || upperName.Contains("50") || upperName.Contains("蓝筹") ||
            upperName.Contains("价值") || upperName.Contains("成长") || upperName.Contains("沪深") ||
            upperName.Contains("中证") || upperName.Contains("标普") || upperName.Contains("纳斯达克") ||
            upperName.Contains("恒生") || upperName.Contains("核心") || upperName.Contains("精选"))
        {
            return SectorBroad;
        }

        return "综合配置";
    }

    /// <summary>
    /// 判断基金是否符合指定板块过滤条件
    /// </summary>
    public static bool MatchesSector(string? name, string? type, string sector)
    {
        if (string.IsNullOrWhiteSpace(sector) ||
            sector == SectorAll ||
            sector == "ALL" ||
            sector == "全部")
        {
            return true;
        }

        string detected = DetectSector(name, type);
        if (detected.Equals(sector, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 若直接包含该板块关键词亦视为命中
        if (SectorKeywords.TryGetValue(sector, out var kws))
        {
            string upperName = (name ?? string.Empty).ToUpperInvariant();
            return kws.Any(kw => upperName.Contains(kw.ToUpperInvariant()));
        }

        return false;
    }

    /// <summary>
    /// 获取用于 DuckDB 全市场目录检索的 SQL LIKE 关键词组
    /// </summary>
    public static List<string> GetKeywordsForSector(string sector)
    {
        if (string.IsNullOrWhiteSpace(sector) ||
            sector == SectorAll ||
            sector == "ALL" ||
            sector == "全部")
        {
            return new List<string>();
        }

        if (SectorKeywords.TryGetValue(sector, out var kws))
        {
            return kws.ToList();
        }

        return new List<string> { sector };
    }

    /// <summary>
    /// 获取各板块典型的精选代表基金，方便用户一键探索与切换
    /// </summary>
    public static List<FundInfo> GetSectorPicks(string sector)
    {
        return sector switch
        {
            SectorFinance => new List<FundInfo>
            {
                new() { Code = "001469", Name = "广发中证全指金融地产ETF联接A", Type = "指数型-股票" },
                new() { Code = "161027", Name = "国泰国证证券行业指数A", Type = "指数型-股票" },
                new() { Code = "162423", Name = "华宝中证银行ETF联接A", Type = "指数型-股票" },
                new() { Code = "000251", Name = "工银金融地产行业混合A", Type = "混合型-偏股" }
            },
            SectorAI => new List<FundInfo>
            {
                new() { Code = "167301", Name = "易方达中证人工智能主题ETF联接A", Type = "指数型-股票" },
                new() { Code = "159995", Name = "华夏国证半导体芯片ETF联接A", Type = "指数型-股票" },
                new() { Code = "515050", Name = "华夏中证5G通信主题ETF", Type = "指数型-股票" },
                new() { Code = "001630", Name = "天弘中证计算机主题ETF联接A", Type = "指数型-股票" }
            },
            SectorMachinery => new List<FundInfo>
            {
                new() { Code = "018446", Name = "广发中证工程机械ETF联接A", Type = "指数型-股票" },
                new() { Code = "001725", Name = "汇添富高端制造股票A", Type = "股票型" },
                new() { Code = "012498", Name = "华夏中证装备产业ETF联接A", Type = "指数型-股票" },
                new() { Code = "000535", Name = "长盛高端装备制造混合A", Type = "混合型-偏股" }
            },
            SectorChemical => new List<FundInfo>
            {
                new() { Code = "012706", Name = "富国中证细分化工产业主题ETF联接A", Type = "指数型-股票" },
                new() { Code = "013445", Name = "建信中证细分化工产业主题ETF联接A", Type = "指数型-股票" },
                new() { Code = "011832", Name = "易方达中证新材料主题ETF联接A", Type = "指数型-股票" }
            },
            SectorMedical => new List<FundInfo>
            {
                new() { Code = "003095", Name = "中欧医疗健康混合A", Type = "混合型-偏股" },
                new() { Code = "009881", Name = "广发中证医疗ETF联接A", Type = "指数型-股票" },
                new() { Code = "006228", Name = "中欧医疗创新股票A", Type = "股票型" }
            },
            SectorConsumer => new List<FundInfo>
            {
                new() { Code = "161725", Name = "招商中证白酒指数A", Type = "指数型-股票" },
                new() { Code = "110022", Name = "易方达消费行业股票", Type = "股票型" },
                new() { Code = "000248", Name = "汇添富主要消费ETF联接A", Type = "指数型-股票" }
            },
            SectorNewEnergy => new List<FundInfo>
            {
                new() { Code = "002190", Name = "农银汇理新能源主题混合A", Type = "混合型-偏股" },
                new() { Code = "011102", Name = "天弘中证光伏产业ETF联接A", Type = "指数型-股票" },
                new() { Code = "013107", Name = "华夏中证电池主题ETF联接A", Type = "指数型-股票" }
            },
            SectorMilitary => new List<FundInfo>
            {
                new() { Code = "004856", Name = "广发中证军工ETF联接A", Type = "指数型-股票" },
                new() { Code = "161024", Name = "富国中证军工指数A", Type = "指数型-股票" },
                new() { Code = "001475", Name = "易方达国防军工混合A", Type = "混合型-偏股" }
            },
            SectorResources => new List<FundInfo>
            {
                new() { Code = "165520", Name = "信诚中证有色金属指数A", Type = "指数型-股票" },
                new() { Code = "008279", Name = "国泰中证煤炭ETF联接A", Type = "指数型-股票" },
                new() { Code = "160416", Name = "华安中证全指石油指数A", Type = "指数型-股票" }
            },
            _ => new List<FundInfo>
            {
                new() { Code = "000001", Name = "华夏成长混合", Type = "混合型-灵活" },
                new() { Code = "005827", Name = "易方达蓝筹精选", Type = "混合型-偏股" },
                new() { Code = "510300", Name = "华泰柏瑞沪深300ETF", Type = "指数型-股票" },
                new() { Code = "161725", Name = "招商中证白酒指数", Type = "指数型-股票" },
                new() { Code = "110011", Name = "易方达优质精选混合", Type = "混合型-偏股" }
            }
        };
    }
}
