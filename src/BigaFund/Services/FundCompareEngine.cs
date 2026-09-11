using BigaFund.Models;

namespace BigaFund.Services;

/// <summary>
/// 机构级双基金穿透对比引擎：解算持仓重合度、共同重仓股、申万行业配置偏离度及综合量化对决
/// </summary>
public static class FundCompareEngine
{
    /// <summary>
    /// 解算两只基金的前十大/穿透重仓股票重合度与行业配置偏离度
    /// </summary>
    public static FundHoldingOverlapResult CalculateHoldingOverlap(
        List<FundStockHolding>? holdingsA,
        List<FundStockHolding>? holdingsB)
    {
        var result = new FundHoldingOverlapResult();
        holdingsA ??= new List<FundStockHolding>();
        holdingsB ??= new List<FundStockHolding>();

        // 规范化字典：以股票代码（或名称）为 Key
        var mapA = new Dictionary<string, FundStockHolding>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in holdingsA)
        {
            string key = !string.IsNullOrWhiteSpace(h.StockCode) ? h.StockCode.Trim() : h.StockName.Trim();
            if (!string.IsNullOrEmpty(key) && !mapA.ContainsKey(key))
            {
                mapA[key] = h;
            }
        }

        var mapB = new Dictionary<string, FundStockHolding>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in holdingsB)
        {
            string key = !string.IsNullOrWhiteSpace(h.StockCode) ? h.StockCode.Trim() : h.StockName.Trim();
            if (!string.IsNullOrEmpty(key) && !mapB.ContainsKey(key))
            {
                mapB[key] = h;
            }
        }

        decimal totalOverlapWeight = 0m;
        var commonList = new List<CommonStockItem>();

        foreach (var kvp in mapA)
        {
            string key = kvp.Key;
            var itemA = kvp.Value;
            if (mapB.TryGetValue(key, out var itemB))
            {
                decimal wA = itemA.WeightPercent;
                decimal wB = itemB.WeightPercent;
                decimal overlap = Math.Min(wA, wB);
                totalOverlapWeight += overlap;

                commonList.Add(new CommonStockItem
                {
                    StockCode = !string.IsNullOrEmpty(itemA.StockCode) ? itemA.StockCode : itemB.StockCode,
                    StockName = !string.IsNullOrEmpty(itemA.StockName) ? itemA.StockName : itemB.StockName,
                    WeightA = wA,
                    WeightB = wB,
                    Industry = !string.IsNullOrEmpty(itemA.Industry) ? itemA.Industry : itemB.Industry
                });
            }
            else
            {
                result.UniqueStocksA.Add(new UniqueStockItem
                {
                    StockCode = itemA.StockCode,
                    StockName = itemA.StockName,
                    Weight = itemA.WeightPercent,
                    Industry = itemA.Industry,
                    FundOwner = "A"
                });
            }
        }

        foreach (var kvp in mapB)
        {
            string key = kvp.Key;
            var itemB = kvp.Value;
            if (!mapA.ContainsKey(key))
            {
                result.UniqueStocksB.Add(new UniqueStockItem
                {
                    StockCode = itemB.StockCode,
                    StockName = itemB.StockName,
                    Weight = itemB.WeightPercent,
                    Industry = itemB.Industry,
                    FundOwner = "B"
                });
            }
        }

        // 按重合权重从大到小排序
        result.CommonStocks = commonList.OrderByDescending(c => c.OverlapWeight).ToList();
        result.CommonCount = result.CommonStocks.Count;
        result.OverlapWeightPercent = Math.Round(totalOverlapWeight, 2);

        // 评级与诊断总结
        if (result.OverlapWeightPercent >= 50m)
        {
            result.OverlapRating = "极高同质化";
            result.OverlapSummary = $"两只基金持仓高度重合（重合度达 {result.OverlapWeightPercent:F1}%，共重合 {result.CommonCount} 只重仓股）。二者风格和底层资产高度趋同，组合配置时建议避免重复配置。";
        }
        else if (result.OverlapWeightPercent >= 30m)
        {
            result.OverlapRating = "中度重合";
            result.OverlapSummary = $"两只基金存在中度持仓重叠（重合度 {result.OverlapWeightPercent:F1}%，共同持有 {result.CommonCount} 只重仓股）。核心持仓有交集但选股各有偏重，可作为同赛道双基金对冲参考。";
        }
        else if (result.OverlapWeightPercent >= 10m)
        {
            result.OverlapRating = "低度重合";
            result.OverlapSummary = $"两只基金持仓重叠度低（重合度仅 {result.OverlapWeightPercent:F1}%，共同持有 {result.CommonCount} 只股票）。二者底层个股差异明显，具备较好的分散化配置潜力。";
        }
        else
        {
            result.OverlapRating = "互补分散";
            result.OverlapSummary = $"两只基金底层持仓几乎无交集（重合度仅 {result.OverlapWeightPercent:F1}%）。属于典型不同赛道或独立选股标的，非常适合加入投资组合进行跨行业风险分散。";
        }

        // 解算申万行业配置分布与偏离度
        result.IndustrySpreads = CalculateIndustrySpreads(holdingsA, holdingsB);

        return result;
    }

    /// <summary>
    /// 解算两只基金在各申万一级行业的配置权重与暴露差额
    /// </summary>
    public static List<IndustryExposureSpreadItem> CalculateIndustrySpreads(
        List<FundStockHolding> holdingsA,
        List<FundStockHolding> holdingsB)
    {
        var indMapA = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in holdingsA)
        {
            string ind = NormalizeIndustry(h.Industry);
            indMapA[ind] = indMapA.GetValueOrDefault(ind) + h.WeightPercent;
        }

        var indMapB = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in holdingsB)
        {
            string ind = NormalizeIndustry(h.Industry);
            indMapB[ind] = indMapB.GetValueOrDefault(ind) + h.WeightPercent;
        }

        var allIndustries = indMapA.Keys.Union(indMapB.Keys).ToList();
        var spreads = new List<IndustryExposureSpreadItem>();

        foreach (var ind in allIndustries)
        {
            decimal wA = Math.Round(indMapA.GetValueOrDefault(ind, 0m), 2);
            decimal wB = Math.Round(indMapB.GetValueOrDefault(ind, 0m), 2);
            spreads.Add(new IndustryExposureSpreadItem
            {
                IndustryName = ind,
                WeightA = wA,
                WeightB = wB
            });
        }

        return spreads.OrderByDescending(s => Math.Abs(s.Spread)).ToList();
    }

    private static string NormalizeIndustry(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "--" || raw == "其它")
        {
            return "其它行业";
        }
        return raw.Trim();
    }

    /// <summary>
    /// 计算两组日收益率序列的 Pearson 相关系数
    /// </summary>
    public static (double Correlation, string Rating) CalculateCorrelation(double[] retsA, double[] retsB)
    {
        int n = Math.Min(retsA.Length, retsB.Length);
        if (n < 3) return (0.0, "数据样本不足");

        double avgA = 0, avgB = 0;
        for (int i = 0; i < n; i++)
        {
            avgA += retsA[i];
            avgB += retsB[i];
        }
        avgA /= n;
        avgB /= n;

        double num = 0, denA = 0, denB = 0;
        for (int i = 0; i < n; i++)
        {
            double diffA = retsA[i] - avgA;
            double diffB = retsB[i] - avgB;
            num += diffA * diffB;
            denA += diffA * diffA;
            denB += diffB * diffB;
        }

        double corr = (denA > 0 && denB > 0) ? num / Math.Sqrt(denA * denB) : 0.0;
        string rating = corr switch
        {
            >= 0.85 => "高度同质化（强正相关）",
            >= 0.50 => "中度同向相关",
            >= -0.20 => "低相关（资产配置价值高）",
            _ => "负相关（对冲避险效应）"
        };

        return (corr, rating);
    }

    /// <summary>
    /// 计算胜率与单日表现对比
    /// </summary>
    public static (double WinRateA, double WinRateB, int AWinCount, int BWinCount, int TotalDays) CalculateWinRates(double[] retsA, double[] retsB)
    {
        int n = Math.Min(retsA.Length, retsB.Length);
        if (n == 0) return (0, 0, 0, 0, 0);

        int aWin = 0, bWin = 0;
        for (int i = 0; i < n; i++)
        {
            if (retsA[i] > retsB[i] + 1e-6) aWin++;
            else if (retsB[i] > retsA[i] + 1e-6) bWin++;
        }

        double rateA = Math.Round((double)aWin / n * 100.0, 1);
        double rateB = Math.Round((double)bWin / n * 100.0, 1);
        return (rateA, rateB, aWin, bWin, n);
    }

    /// <summary>
    /// 双基金牛熊非对称捕获与极端暴跌条件相关性横向对比
    /// </summary>
    public static BullBearCompareResult CalculateBullBearCompare(
        FundDetail fundA,
        FundDetail fundB,
        IReadOnlyList<BenchmarkRecord>? benchmarkHistory = null)
    {
        var result = new BullBearCompareResult();
        var bm = benchmarkHistory ?? fundA.BenchmarkCsi300;

        result.CaptureA = fundA.BullBearCapture ?? QuantCalculator.CalculateBullBearCapture(fundA.NavHistory, bm);
        result.CaptureB = fundB.BullBearCapture ?? QuantCalculator.CalculateBullBearCapture(fundB.NavHistory, bm);

        // 提取两只基金对齐的日收益率序列，计算 A 与 B 之间的常态相关系数与暴跌日条件相关系数
        var dictA = new Dictionary<DateTime, double>();
        for (int i = 1; i < fundA.NavHistory.Count; i++)
        {
            decimal p = QuantCalculator.GetEffectiveNav(fundA.NavHistory[i - 1]);
            decimal c = QuantCalculator.GetEffectiveNav(fundA.NavHistory[i]);
            if (p > 0) dictA[fundA.NavHistory[i].Date.Date] = (double)((c - p) / p);
        }

        var listA = new List<double>();
        var listB = new List<double>();
        var bmList = new List<double>();

        var bmDict = new Dictionary<DateTime, double>();
        if (bm != null && bm.Count >= 2)
        {
            var sortedBm = bm.OrderBy(b => b.Date).ToList();
            for (int i = 1; i < sortedBm.Count; i++)
            {
                double prevEff = Math.Max(1.0, 100.0 + (double)sortedBm[i - 1].CumulativeReturnRate);
                double currEff = Math.Max(1.0, 100.0 + (double)sortedBm[i].CumulativeReturnRate);
                if (prevEff > 0) bmDict[sortedBm[i].Date.Date] = (currEff - prevEff) / prevEff;
            }
        }

        for (int i = 1; i < fundB.NavHistory.Count; i++)
        {
            var d = fundB.NavHistory[i].Date.Date;
            if (dictA.TryGetValue(d, out double rA))
            {
                decimal p = QuantCalculator.GetEffectiveNav(fundB.NavHistory[i - 1]);
                decimal c = QuantCalculator.GetEffectiveNav(fundB.NavHistory[i]);
                if (p > 0)
                {
                    double rB = (double)((c - p) / p);
                    listA.Add(rA);
                    listB.Add(rB);
                    if (bmDict.TryGetValue(d, out double rBm)) bmList.Add(rBm);
                    else bmList.Add((rA + rB) / 2.0);
                }
            }
        }

        if (listA.Count >= 3)
        {
            var (normCorr, _) = CalculateCorrelation(listA.ToArray(), listB.ToArray());
            result.NormalCorrelation = Math.Round(normCorr, 3);

            // 筛选暴跌日：基准跌幅 <= -1.0% 或两者平均跌幅 <= -1.0%
            var crashA = new List<double>();
            var crashB = new List<double>();
            for (int i = 0; i < listA.Count; i++)
            {
                if (bmList[i] <= -0.01 || (listA[i] + listB[i]) / 2.0 <= -0.01)
                {
                    crashA.Add(listA[i]);
                    crashB.Add(listB[i]);
                }
            }

            if (crashA.Count < 3 && listA.Count >= 6)
            {
                var sorted = listA.Zip(listB, (a, b) => (A: a, B: b, Avg: (a + b) / 2.0)).OrderBy(p => p.Avg).Take(Math.Max(3, (int)(listA.Count * 0.15))).ToList();
                crashA = sorted.Select(p => p.A).ToList();
                crashB = sorted.Select(p => p.B).ToList();
            }

            if (crashA.Count >= 2)
            {
                var (cCorr, _) = CalculateCorrelation(crashA.ToArray(), crashB.ToArray());
                result.CrashCorrelation = Math.Round(cCorr, 3);
            }
            else
            {
                result.CrashCorrelation = result.NormalCorrelation;
            }
        }

        // 胜出判定与对比评价
        decimal spreadA = result.CaptureA.CaptureSpread;
        decimal spreadB = result.CaptureB.CaptureSpread;
        string superiorFund = spreadA >= spreadB ? $"{fundA.Name} ({fundA.Code})" : $"{fundB.Name} ({fundB.Code})";

        result.ComparisonSummary = $"【{fundA.Name}】捕获利差为 {spreadA:+0.0;-0.0}% (非对称度 {result.CaptureA.AsymmetryIndex:+0.00;-0.00})；" +
            $"【{fundB.Name}】捕获利差为 {spreadB:+0.0;-0.0}% (非对称度 {result.CaptureB.AsymmetryIndex:+0.00;-0.00})。" +
            $"两标的常态相关性为 {result.NormalCorrelation:+0.00;-0.00}，极端暴跌日条件相关性为 {result.CrashCorrelation:+0.00;-0.00} (相关性漂移 {result.CorrelationShift:+0.00;-0.00})。" +
            $"正凸性占优标的为【{superiorFund}】。";

        return result;
    }
}
