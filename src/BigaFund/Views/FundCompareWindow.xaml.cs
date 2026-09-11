using System.Windows;
using System.Windows.Controls;
using BigaFund.Models;
using BigaFund.Services;

namespace BigaFund.Views;

public partial class FundCompareWindow : Window
{
    private readonly FundDataService _dataService;
    private FundDetail? _fundA;
    private FundDetail? _fundB;
    private bool _isLoaded = false;

    public FundCompareWindow(FundDataService dataService, string defaultCodeA = "000001", string defaultCodeB = "005827")
    {
        InitializeComponent();
        _dataService = dataService;

        TxtFundA.Text = defaultCodeA;
        TxtFundB.Text = defaultCodeB;

        Loaded += async (s, e) =>
        {
            SetupPlotStyle();
            _isLoaded = true;
            await ExecuteComparisonAsync();
        };
    }

    private void SetupPlotStyle()
    {
        var plot = ComparePlot.Plot;
        PlotStyleHelper.ApplyDarkThemeAndFont(plot);
    }

    private async void BtnCompare_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteComparisonAsync();
    }

    private async void TimeRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        await ExecuteComparisonAsync();
    }

    private async Task ExecuteComparisonAsync()
    {
        string codeA = TxtFundA.Text.Trim();
        string codeB = TxtFundB.Text.Trim();

        if (string.IsNullOrEmpty(codeA) || string.IsNullOrEmpty(codeB))
        {
            MessageBox.Show("请输入两只对比基金的代码。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TxtStatus.Text = "正在加载两只基金净值时序数据...";

        try
        {
            _fundA = await _dataService.GetFundDetailAsync(codeA);
            _fundB = await _dataService.GetFundDetailAsync(codeB);

            if (_fundA == null || _fundA.NavHistory.Count < 2)
            {
                TxtStatus.Text = $"未获取到基金 A [{codeA}] 的有效净值。";
                return;
            }

            if (_fundB == null || _fundB.NavHistory.Count < 2)
            {
                TxtStatus.Text = $"未获取到基金 B [{codeB}] 的有效净值。";
                return;
            }

            TxtNameA.Text = _fundA.Name;
            TxtNameB.Text = _fundB.Name;

            RenderComparisonPlotAndMetrics();
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"对比异常: {ex.Message}";
        }
    }

    private void RenderComparisonPlotAndMetrics()
    {
        if (_fundA == null || _fundB == null) return;

        // 1. 寻找共同时间交集
        DateTime startA = _fundA.NavHistory[0].Date;
        DateTime startB = _fundB.NavHistory[0].Date;
        DateTime commonStart = startA > startB ? startA : startB;

        DateTime endA = _fundA.NavHistory[^1].Date;
        DateTime endB = _fundB.NavHistory[^1].Date;
        DateTime commonEnd = endA < endB ? endA : endB;

        if (commonStart >= commonEnd)
        {
            TxtStatus.Text = "两只基金历史交易日无重叠交集，无法同台对比。";
            return;
        }

        // 2. 根据选中的时间跨度进行筛选
        string rangeTag = (CmbTimeRange.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1Y";
        DateTime cutoff = rangeTag switch
        {
            "1M" => commonEnd.AddMonths(-1),
            "3M" => commonEnd.AddMonths(-3),
            "6M" => commonEnd.AddMonths(-6),
            "1Y" => commonEnd.AddYears(-1),
            "3Y" => commonEnd.AddYears(-3),
            "5Y" => commonEnd.AddYears(-5),
            _ => commonStart
        };

        if (cutoff < commonStart) cutoff = commonStart;

        var navsA = _fundA.NavHistory.Where(x => x.Date >= cutoff && x.Date <= commonEnd).OrderBy(x => x.Date).ToList();
        var navsB = _fundB.NavHistory.Where(x => x.Date >= cutoff && x.Date <= commonEnd).OrderBy(x => x.Date).ToList();

        if (navsA.Count < 2 || navsB.Count < 2)
        {
            TxtStatus.Text = "选定区间内有效交易日较少，无法生成对比。";
            return;
        }

        // 3. 计算对齐后的累计收益率曲线 (以区间首日为 0% 归一化)
        decimal baseNavA = navsA[0].CumulativeNav > 0 ? navsA[0].CumulativeNav : navsA[0].UnitNav;
        if (baseNavA <= 0) baseNavA = 1m;
        double[] xsA = navsA.Select(n => n.Date.ToOADate()).ToArray();
        double[] ysA = navsA.Select(n => (double)(((n.CumulativeNav > 0 ? n.CumulativeNav : n.UnitNav) - baseNavA) / baseNavA * 100m)).ToArray();

        decimal baseNavB = navsB[0].CumulativeNav > 0 ? navsB[0].CumulativeNav : navsB[0].UnitNav;
        if (baseNavB <= 0) baseNavB = 1m;
        double[] xsB = navsB.Select(n => n.Date.ToOADate()).ToArray();
        double[] ysB = navsB.Select(n => (double)(((n.CumulativeNav > 0 ? n.CumulativeNav : n.UnitNav) - baseNavB) / baseNavB * 100m)).ToArray();

        // 4. 绘图
        var plot = ComparePlot.Plot;
        plot.Clear();
        SetupPlotStyle();

        var scatterA = plot.Add.Scatter(xsA, ysA);
        scatterA.LegendText = $"{_fundA.Name} ({_fundA.Code})";
        scatterA.Color = ScottPlot.Color.FromHex("#89B4FA");
        scatterA.LineWidth = 2.2f;
        scatterA.MarkerSize = 0;

        var scatterB = plot.Add.Scatter(xsB, ysB);
        scatterB.LegendText = $"{_fundB.Name} ({_fundB.Code})";
        scatterB.Color = ScottPlot.Color.FromHex("#F38BA8");
        scatterB.LineWidth = 2.2f;
        scatterB.MarkerSize = 0;

        var zeroLine = plot.Add.HorizontalLine(0);
        zeroLine.Color = ScottPlot.Color.FromHex("#585B70");
        zeroLine.LineWidth = 1;
        zeroLine.LinePattern = ScottPlot.LinePattern.Dashed;

        plot.ShowLegend(ScottPlot.Alignment.UpperLeft);
        plot.Axes.AutoScale();
        ComparePlot.Refresh();

        // 5. 量化指标计算与擂台对决
        var metA = QuantCalculator.CalculateMetrics(navsA, rangeTag, _fundA.BenchmarkCsi300);
        var metB = QuantCalculator.CalculateMetrics(navsB, rangeTag, _fundB.BenchmarkCsi300);

        var rows = new List<MetricCompareRow>
        {
            BuildRow("区间累计收益率", $"{metA.TotalReturn:+0.00;-0.00;0.00}%", $"{metB.TotalReturn:+0.00;-0.00;0.00}%", 
                     metA.TotalReturn > metB.TotalReturn ? "🏆 基金 A 胜" : (metA.TotalReturn < metB.TotalReturn ? "🏆 基金 B 胜" : "平局"), "统计区间复权走势累计涨跌"),

            BuildRow("年化复合收益率 (CAGR)", $"{metA.AnnualizedReturn:+0.00;-0.00;0.00}%", $"{metB.AnnualizedReturn:+0.00;-0.00;0.00}%", 
                     metA.AnnualizedReturn > metB.AnnualizedReturn ? "🏆 基金 A 胜" : "🏆 基金 B 胜", "按 A 股 250 交易日折算真实年化收益"),

            BuildRow("历史最大回撤 (MDD)", $"{metA.MaxDrawdown:F2}%", $"{metB.MaxDrawdown:F2}%", 
                     metA.MaxDrawdown < metB.MaxDrawdown ? "🏆 基金 A 胜 (抗跌)" : "🏆 基金 B 胜 (抗跌)", "区间任意买点可能面临的最大本金回撤 (绝对值越低越优)"),

            BuildRow("回撤修复耗时", metA.RecoveryTradingDays.HasValue ? $"{metA.RecoveryTradingDays} 天" : "未修复", 
                     metB.RecoveryTradingDays.HasValue ? $"{metB.RecoveryTradingDays} 天" : "未修复", 
                     DecideRecovery(metA.RecoveryTradingDays, metB.RecoveryTradingDays), "从谷底跌幅回升至前高所需的交易日数"),

            BuildRow("夏普比率 (Sharpe)", $"{metA.SharpeRatio:F2}", $"{metB.SharpeRatio:F2}", 
                     metA.SharpeRatio > metB.SharpeRatio ? "🏆 基金 A 胜" : "🏆 基金 B 胜", "承担单位总风险所获得的超额回报 (>1 优秀)"),

            BuildRow("卡玛比率 (Calmar)", $"{metA.CalmarRatio:F2}", $"{metB.CalmarRatio:F2}", 
                     metA.CalmarRatio > metB.CalmarRatio ? "🏆 基金 A 胜" : "🏆 基金 B 胜", "年化收益弥补极端回撤的能力 (CAGR / MDD)"),

            BuildRow("索提诺比率 (Sortino)", $"{metA.SortinoRatio:F2}", $"{metB.SortinoRatio:F2}", 
                     metA.SortinoRatio > metB.SortinoRatio ? "🏆 基金 A 胜" : "🏆 基金 B 胜", "仅惩罚亏损下行波动的风险调整回报"),

            BuildRow("年化波动率 (Volatility)", $"{metA.AnnualizedVolatility:F2}%", $"{metB.AnnualizedVolatility:F2}%", 
                     metA.AnnualizedVolatility < metB.AnnualizedVolatility ? "🏆 基金 A 胜 (更平稳)" : "🏆 基金 B 胜 (更平稳)", "净值变动离散度年化衡量 (越低越平稳)"),

            BuildRow("CAPM 阿尔法 (Alpha)", $"{metA.Alpha:+0.00;-0.00;0.00}%", $"{metB.Alpha:+0.00;-0.00;0.00}%", 
                     metA.Alpha > metB.Alpha ? "🏆 基金 A 胜" : "🏆 基金 B 胜", "剔除市场系统性贝塔后的纯超额经理主动收益"),

            BuildRow("贝塔系数 (Beta)", $"{metA.Beta:F2}", $"{metB.Beta:F2}", 
                     "-- 对比参考 --", "相对沪深300指数波动的弹性敏感度"),

            BuildRow("信息比率 (IR)", $"{metA.InformationRatio:F2}", $"{metB.InformationRatio:F2}", 
                     metA.InformationRatio > metB.InformationRatio ? "🏆 基金 A 胜" : "🏆 基金 B 胜", "主动超额回报相对跟踪误差的稳定性比率")
        };

        GridMetrics.ItemsSource = rows;
        TxtStatus.Text = $"已完成对比：{navsA.Count} 个共同交易日 ({navsA[0].Date:yyyy-MM-dd} 至 {navsA[^1].Date:yyyy-MM-dd})";
    }

    private static MetricCompareRow BuildRow(string metric, string valA, string valB, string winner, string desc)
    {
        return new MetricCompareRow
        {
            MetricName = metric,
            ValueA = valA,
            ValueB = valB,
            Winner = winner,
            Description = desc
        };
    }

    private static string DecideRecovery(int? a, int? b)
    {
        if (a.HasValue && !b.HasValue) return "🏆 基金 A 胜";
        if (!a.HasValue && b.HasValue) return "🏆 基金 B 胜";
        if (a.HasValue && b.HasValue)
        {
            return a.Value < b.Value ? "🏆 基金 A 胜 (更快)" : (a.Value > b.Value ? "🏆 基金 B 胜 (更快)" : "平局");
        }
        return "均未修复";
    }
}
