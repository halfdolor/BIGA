using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using BigaFund.Models;
using BigaFund.Services;
using Microsoft.Win32;

namespace BigaFund.Views;

public partial class FundCompareControl : UserControl
{
    private FundDataService? _dataService;
    private FundDetail? _fundA;
    private FundDetail? _fundB;
    private QuantMetrics? _metricsA;
    private QuantMetrics? _metricsB;
    private FundHoldingOverlapResult? _overlapResult;
    private List<MetricCompareRow> _cachedMetricRows = new();
    private double _cachedCorrelation;
    private string _cachedCorrRating = string.Empty;
    private double _cachedWinRateA;
    private double _cachedWinRateB;

    private bool _isInitialized;
    private long _lastRenderTimestamp;
    private ScottPlot.Plottables.Crosshair? _crosshair;
    private List<NavRecord> _cachedA = new();
    private List<NavRecord> _cachedB = new();

    public FundCompareControl()
    {
        InitializeComponent();
        ComparePlot.MouseMove += ComparePlot_MouseMove;
        ComparePlot.MouseLeave += ComparePlot_MouseLeave;
    }

    public async void Initialize(FundDataService dataService, string defaultCodeA = "000001", string defaultCodeB = "005827")
    {
        _dataService = dataService;
        if (!_isInitialized)
        {
            _isInitialized = true;
            TxtFundA.Text = defaultCodeA;
            TxtFundB.Text = defaultCodeB;
            SetupPlotStyle();
            await ExecuteComparisonAsync();
        }
    }

    public async void SetFunds(string codeA, string codeB)
    {
        await SetFundsAsync(codeA, codeB);
    }

    public async Task SetFundsAsync(string codeA, string codeB)
    {
        TxtFundA.Text = codeA;
        TxtFundB.Text = codeB;
        await ExecuteComparisonAsync();
    }

    private void SetupPlotStyle()
    {
        var plot = ComparePlot.Plot;
        PlotStyleHelper.ApplyDarkThemeAndFont(plot);
    }

    private void ComparePlot_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_crosshair == null || _cachedA.Count < 2 || _cachedB.Count < 2) return;

        long now = Environment.TickCount64;
        if (now - _lastRenderTimestamp < 16) return;
        _lastRenderTimestamp = now;

        var position = e.GetPosition(ComparePlot);
        var pixel = new ScottPlot.Pixel((float)position.X, (float)position.Y);
        var coords = ComparePlot.Plot.GetCoordinates(pixel);
        DateTime hoveredDate;
        try
        {
            hoveredDate = DateTime.FromOADate(coords.X).Date;
        }
        catch
        {
            return;
        }

        var recA = FindClosestNav(_cachedA, hoveredDate);
        var recB = FindClosestNav(_cachedB, hoveredDate);
        if (recA == null || recB == null) return;

        decimal baseA = _cachedA[0].CumulativeNav > 0 ? _cachedA[0].CumulativeNav : _cachedA[0].UnitNav;
        decimal baseB = _cachedB[0].CumulativeNav > 0 ? _cachedB[0].CumulativeNav : _cachedB[0].UnitNav;
        if (baseA <= 0) baseA = 1m;
        if (baseB <= 0) baseB = 1m;

        decimal effA = recA.CumulativeNav > 0 ? recA.CumulativeNav : recA.UnitNav;
        decimal effB = recB.CumulativeNav > 0 ? recB.CumulativeNav : recB.UnitNav;
        double retA = (double)((effA - baseA) / baseA * 100m);
        double retB = (double)((effB - baseB) / baseB * 100m);

        _crosshair.IsVisible = true;
        _crosshair.Position = new ScottPlot.Coordinates(recA.Date.ToOADate(), retA);
        ComparePlot.ToolTip = $"📅 日期: {recA.Date:yyyy-MM-dd}\n🔵 [A] {_fundA?.Name}: {retA:+0.00;-0.00;0.00}%\n🔴 [B] {_fundB?.Name}: {retB:+0.00;-0.00;0.00}%\n⚡ 超额差值 (A-B): {(retA - retB):+0.00;-0.00;0.00}%";
        ComparePlot.Refresh();
    }

    private static NavRecord? FindClosestNav(List<NavRecord> list, DateTime target)
    {
        if (list == null || list.Count == 0) return null;
        int low = 0, high = list.Count - 1;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            if (list[mid].Date == target) return list[mid];
            if (list[mid].Date < target) low = mid + 1;
            else high = mid - 1;
        }
        int bestIdx = Math.Clamp(low, 0, list.Count - 1);
        int prevIdx = Math.Clamp(high, 0, list.Count - 1);
        return Math.Abs((list[bestIdx].Date - target).Ticks) < Math.Abs((list[prevIdx].Date - target).Ticks) ? list[bestIdx] : list[prevIdx];
    }

    private void ComparePlot_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_crosshair != null)
        {
            _crosshair.IsVisible = false;
            ComparePlot.ToolTip = null;
            ComparePlot.Refresh();
        }
    }

    private async void BtnCompare_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteComparisonAsync();
    }

    private async void TimeRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        await ExecuteComparisonAsync();
    }

    private async Task ExecuteComparisonAsync()
    {
        if (_dataService == null) return;
        string codeA = TxtFundA.Text.Trim();
        string codeB = TxtFundB.Text.Trim();

        if (string.IsNullOrEmpty(codeA) || string.IsNullOrEmpty(codeB))
        {
            MessageBox.Show("请输入两只对比基金的代码。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PlotLoading.Visibility = Visibility.Visible;

        try
        {
            var taskA = _dataService.GetFundDetailAsync(codeA);
            var taskB = _dataService.GetFundDetailAsync(codeB);
            await Task.WhenAll(taskA, taskB);

            _fundA = await taskA;
            _fundB = await taskB;

            if (_fundA == null || _fundA.NavHistory.Count == 0)
            {
                MessageBox.Show($"未获取到基金 A [{codeA}] 的历史净值数据。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_fundB == null || _fundB.NavHistory.Count == 0)
            {
                MessageBox.Show($"未获取到基金 B [{codeB}] 的历史净值数据。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            TxtNameA.Text = _fundA.Name;
            TxtNameB.Text = _fundB.Name;
            TxtScoreHeaderA.Text = $"[A] {_fundA.Name} ({_fundA.Code})";
            TxtScoreHeaderB.Text = $"[B] {_fundB.Name} ({_fundB.Code})";
            TxtValuationTitleA.Text = $"[A] {_fundA.Name} 盘中估算";
            TxtValuationTitleB.Text = $"[B] {_fundB.Name} 盘中估算";

            RenderComparisonPlot();
            CalculateAndRenderMetrics();
            RenderScoreCardPK();
            RenderHoldingOverlapAndIndustry();
            RenderRealtimeValuation();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"对比计算失败: {ex.Message}", "异常", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PlotLoading.Visibility = Visibility.Collapsed;
        }
    }

    private void RenderComparisonPlot()
    {
        if (_fundA == null || _fundB == null) return;

        var plot = ComparePlot.Plot;
        plot.Clear();
        SetupPlotStyle();

        string range = (CmbTimeRange.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1Y";

        var (filteredA, filteredB) = AlignNavHistory(_fundA.NavHistory, _fundB.NavHistory, range);
        if (filteredA.Count < 2 || filteredB.Count < 2)
        {
            ComparePlot.Refresh();
            return;
        }

        _cachedA = filteredA;
        _cachedB = filteredB;

        decimal baseA = filteredA[0].CumulativeNav > 0 ? filteredA[0].CumulativeNav : filteredA[0].UnitNav;
        decimal baseB = filteredB[0].CumulativeNav > 0 ? filteredB[0].CumulativeNav : filteredB[0].UnitNav;
        if (baseA <= 0) baseA = 1m;
        if (baseB <= 0) baseB = 1m;

        double[] xsA = filteredA.Select(n => n.Date.ToOADate()).ToArray();
        double[] ysA = filteredA.Select(n =>
        {
            decimal eff = n.CumulativeNav > 0 ? n.CumulativeNav : n.UnitNav;
            return (double)((eff - baseA) / baseA * 100m);
        }).ToArray();

        double[] xsB = filteredB.Select(n => n.Date.ToOADate()).ToArray();
        double[] ysB = filteredB.Select(n =>
        {
            decimal eff = n.CumulativeNav > 0 ? n.CumulativeNav : n.UnitNav;
            return (double)((eff - baseB) / baseB * 100m);
        }).ToArray();

        var lineA = plot.Add.ScatterLine(xsA, ysA);
        lineA.LegendText = $"[A] {_fundA.Name} ({_fundA.Code})";
        lineA.Color = ScottPlot.Color.FromHex("#89B4FA");
        lineA.LineWidth = 2.2f;

        var lineB = plot.Add.ScatterLine(xsB, ysB);
        lineB.LegendText = $"[B] {_fundB.Name} ({_fundB.Code})";
        lineB.Color = ScottPlot.Color.FromHex("#F38BA8");
        lineB.LineWidth = 2.2f;

        // 3. 相对强弱走势线 (Spread: (1 + RetA) / (1 + RetB) - 1) %
        double[] ysSpread = new double[filteredA.Count];
        for (int i = 0; i < filteredA.Count; i++)
        {
            decimal effA = filteredA[i].CumulativeNav > 0 ? filteredA[i].CumulativeNav : filteredA[i].UnitNav;
            decimal effB = filteredB[i].CumulativeNav > 0 ? filteredB[i].CumulativeNav : filteredB[i].UnitNav;
            decimal rA = (effA - baseA) / baseA;
            decimal rB = (effB - baseB) / baseB;
            ysSpread[i] = (double)(((1m + rA) / (1m + rB) - 1m) * 100m);
        }

        var lineSpread = plot.Add.ScatterLine(xsA, ysSpread);
        lineSpread.LegendText = "相对强弱 (A/B 超额比 %)";
        lineSpread.Color = ScottPlot.Color.FromHex("#F9E2AF");
        lineSpread.LineWidth = 1.6f;
        lineSpread.LinePattern = ScottPlot.LinePattern.Dotted;

        var zeroLine = plot.Add.HorizontalLine(0);
        zeroLine.Color = ScottPlot.Color.FromHex("#585B70");
        zeroLine.LineWidth = 1;
        zeroLine.LinePattern = ScottPlot.LinePattern.Dashed;

        _crosshair = plot.Add.Crosshair(0, 0);
        _crosshair.IsVisible = false;
        _crosshair.LineColor = ScottPlot.Color.FromHex("#F5B041").WithAlpha(0.65f);
        _crosshair.LineWidth = 1.2f;
        _crosshair.LinePattern = ScottPlot.LinePattern.Dashed;

        plot.ShowLegend(ScottPlot.Alignment.UpperLeft);
        plot.Legend.FontName = PlotStyleHelper.ResolveChineseFont();
        plot.Legend.SetBestFontOnEachRender = true;
        plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#1E1E2E");
        plot.Legend.FontColor = ScottPlot.Color.FromHex("#FFFFFF");
        plot.Legend.OutlineColor = ScottPlot.Color.FromHex("#45475A");

        plot.Axes.AutoScale();
        ComparePlot.Refresh();
    }

    private void CalculateAndRenderMetrics()
    {
        if (_fundA == null || _fundB == null) return;

        string range = (CmbTimeRange.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1Y";
        var (filteredA, filteredB) = AlignNavHistory(_fundA.NavHistory, _fundB.NavHistory, range);

        if (filteredA.Count < 2 || filteredB.Count < 2) return;

        _metricsA = QuantCalculator.CalculateMetrics(filteredA, range, _fundA.BenchmarkCsi300);
        _metricsB = QuantCalculator.CalculateMetrics(filteredB, range, _fundB.BenchmarkCsi300);
        if (_metricsA.ManagerProfile != null) _metricsA.ManagerProfile.ManagerName = _fundA.ManagerName;
        if (_metricsB.ManagerProfile != null) _metricsB.ManagerProfile.ManagerName = _fundB.ManagerName;

        int n = Math.Min(filteredA.Count, filteredB.Count);
        double[] retsA = new double[n - 1];
        double[] retsB = new double[n - 1];

        for (int i = 1; i < n; i++)
        {
            decimal prevA = filteredA[i - 1].CumulativeNav > 0 ? filteredA[i - 1].CumulativeNav : filteredA[i - 1].UnitNav;
            decimal currA = filteredA[i].CumulativeNav > 0 ? filteredA[i].CumulativeNav : filteredA[i].UnitNav;
            decimal prevB = filteredB[i - 1].CumulativeNav > 0 ? filteredB[i - 1].CumulativeNav : filteredB[i - 1].UnitNav;
            decimal currB = filteredB[i].CumulativeNav > 0 ? filteredB[i].CumulativeNav : filteredB[i].UnitNav;

            retsA[i - 1] = prevA > 0 ? (double)((currA - prevA) / prevA) : 0;
            retsB[i - 1] = prevB > 0 ? (double)((currB - prevB) / prevB) : 0;
        }

        var (corr, corrRating) = FundCompareEngine.CalculateCorrelation(retsA, retsB);
        var (winRateA, winRateB, aWinCount, bWinCount, totalDays) = FundCompareEngine.CalculateWinRates(retsA, retsB);

        _cachedCorrelation = corr;
        _cachedCorrRating = corrRating;
        _cachedWinRateA = winRateA;
        _cachedWinRateB = winRateB;

        var rows = new List<MetricCompareRow>
        {
            new MetricCompareRow
            {
                MetricName = "区间累计收益率",
                ValueA = $"{_metricsA.TotalReturn:+0.00;-0.00;0.00}%",
                ValueB = $"{_metricsB.TotalReturn:+0.00;-0.00;0.00}%",
                Winner = _metricsA.TotalReturn > _metricsB.TotalReturn ? $"🏆 A 胜出 (+{(_metricsA.TotalReturn - _metricsB.TotalReturn):F2}%)" : (_metricsA.TotalReturn < _metricsB.TotalReturn ? $"🏆 B 胜出 (+{(_metricsB.TotalReturn - _metricsA.TotalReturn):F2}%)" : "平局"),
                Description = "选定周期内的资产复权累积增长幅度（越高越好）"
            },
            new MetricCompareRow
            {
                MetricName = "年化复合增长率 (CAGR)",
                ValueA = $"{_metricsA.AnnualizedReturn:+0.00;-0.00;0.00}%",
                ValueB = $"{_metricsB.AnnualizedReturn:+0.00;-0.00;0.00}%",
                Winner = _metricsA.AnnualizedReturn > _metricsB.AnnualizedReturn ? "🏆 基金 A 优" : "🏆 基金 B 优",
                Description = "按复利换算为每年的平均增长回报（越高越好）"
            },
            new MetricCompareRow
            {
                MetricName = "历史最大回撤 (MDD)",
                ValueA = $"{_metricsA.MaxDrawdown:F2}%",
                ValueB = $"{_metricsB.MaxDrawdown:F2}%",
                Winner = _metricsA.MaxDrawdown < _metricsB.MaxDrawdown ? "🏆 基金 A 更抗跌" : "🏆 基金 B 更抗跌",
                Description = "周期内从任何最高峰买入可能遭遇的最大浮亏比例（绝对值越低越优）"
            },
            new MetricCompareRow
            {
                MetricName = "夏普比率 (Sharpe)",
                ValueA = $"{_metricsA.SharpeRatio:F2}",
                ValueB = $"{_metricsB.SharpeRatio:F2}",
                Winner = _metricsA.SharpeRatio > _metricsB.SharpeRatio ? "🏆 基金 A 性价比高" : "🏆 基金 B 性价比高",
                Description = "承担每单位总波动风险所获得的超额收益补偿（大于 1.0 为优）"
            },
            new MetricCompareRow
            {
                MetricName = "卡玛比率 (Calmar)",
                ValueA = $"{_metricsA.CalmarRatio:F2}",
                ValueB = $"{_metricsB.CalmarRatio:F2}",
                Winner = _metricsA.CalmarRatio > _metricsB.CalmarRatio ? "🏆 基金 A 韧性更强" : "🏆 基金 B 韧性更强",
                Description = "年化收益与最大回撤的比值，检验进攻与防守的综合平衡能力"
            },
            new MetricCompareRow
            {
                MetricName = "索提诺比率 (Sortino)",
                ValueA = $"{_metricsA.SortinoRatio:F2}",
                ValueB = $"{_metricsB.SortinoRatio:F2}",
                Winner = _metricsA.SortinoRatio > _metricsB.SortinoRatio ? "🏆 基金 A 优" : "🏆 基金 B 优",
                Description = "仅惩罚亏损下行波动的风险调整回报"
            },
            new MetricCompareRow
            {
                MetricName = "年化波动率 (Volatility)",
                ValueA = $"{_metricsA.AnnualizedVolatility:F2}%",
                ValueB = $"{_metricsB.AnnualizedVolatility:F2}%",
                Winner = _metricsA.AnnualizedVolatility < _metricsB.AnnualizedVolatility ? "🏆 基金 A 更平稳" : "🏆 基金 B 更平稳",
                Description = "净值日收益率的离散程度（越低持仓体验越稳健）"
            },
            new MetricCompareRow
            {
                MetricName = "95% 在险价值 (VaR)",
                ValueA = $"{_metricsA.VaR95:F2}%",
                ValueB = $"{_metricsB.VaR95:F2}%",
                Winner = _metricsA.VaR95 < _metricsB.VaR95 ? "🏆 基金 A 尾部损失小" : (_metricsA.VaR95 > _metricsB.VaR95 ? "🏆 基金 B 尾部损失小" : "持平"),
                Description = "历史模拟法单日 95% 置信度下的最大潜在亏损阈值（损失越小越优）"
            },
            new MetricCompareRow
            {
                MetricName = "95% 条件在险价值 (CVaR)",
                ValueA = $"{_metricsA.CVaR95:F2}%",
                ValueB = $"{_metricsB.CVaR95:F2}%",
                Winner = _metricsA.CVaR95 < _metricsB.CVaR95 ? "🏆 基金 A 极端风险小" : (_metricsA.CVaR95 > _metricsB.CVaR95 ? "🏆 基金 B 极端风险小" : "持平"),
                Description = "突破 VaR 阈值后极端行情下的预期均值损失（极端风险越小越优）"
            },
            new MetricCompareRow
            {
                MetricName = "CAPM 阿尔法 (Alpha)",
                ValueA = $"{_metricsA.Alpha:+0.00;-0.00;0.00}%",
                ValueB = $"{_metricsB.Alpha:+0.00;-0.00;0.00}%",
                Winner = _metricsA.Alpha > _metricsB.Alpha ? "🏆 基金 A 胜" : "🏆 基金 B 胜",
                Description = "剔除市场系统性贝塔后的纯超额经理主动收益"
            },
            new MetricCompareRow
            {
                MetricName = "信息比率 (IR)",
                ValueA = $"{_metricsA.InformationRatio:F2}",
                ValueB = $"{_metricsB.InformationRatio:F2}",
                Winner = _metricsA.InformationRatio > _metricsB.InformationRatio ? "🏆 基金 A 胜" : "🏆 基金 B 胜",
                Description = "主动超额回报相对跟踪误差的稳定性比率"
            },
            new MetricCompareRow
            {
                MetricName = "日收益率相关度 (Pearson)",
                ValueA = $"{corr:F2}",
                ValueB = $"{corr:F2}",
                Winner = corrRating,
                Description = "两只基金日收益率时序关联性（>0.85 严重同质化，<0.5 具备资产配置分散效应）"
            },
            new MetricCompareRow
            {
                MetricName = "单日表现胜率 (Win Rate)",
                ValueA = $"{winRateA:F1}%",
                ValueB = $"{winRateB:F1}%",
                Winner = winRateA > winRateB ? $"🏆 基金 A 胜率高 ({winRateA:F1}%)" : (winRateB > winRateA ? $"🏆 基金 B 胜率高 ({winRateB:F1}%)" : "双方均等"),
                Description = $"两基共同交易日 ({totalDays} 天) 中，单日收益战胜对方的交易日占比"
            },
            new MetricCompareRow
            {
                MetricName = "综合捕获比率 (Capture Ratio)",
                ValueA = $"{_metricsA.CaptureRatio:F2}",
                ValueB = $"{_metricsB.CaptureRatio:F2}",
                Winner = _metricsA.CaptureRatio > _metricsB.CaptureRatio ? "🏆 基金 A 捕获力强" : (_metricsA.CaptureRatio < _metricsB.CaptureRatio ? "🏆 基金 B 捕获力强" : "持平"),
                Description = "上行捕获率/下行捕获率，大于 1.0 说明牛市跑赢大盘幅度高于熊市回撤幅度"
            },
            new MetricCompareRow
            {
                MetricName = "上行捕获率 (Upside Capture)",
                ValueA = $"{_metricsA.UpsideCaptureRatio:F1}%",
                ValueB = $"{_metricsB.UpsideCaptureRatio:F1}%",
                Winner = _metricsA.UpsideCaptureRatio > _metricsB.UpsideCaptureRatio ? "🏆 基金 A 进攻强" : (_metricsA.UpsideCaptureRatio < _metricsB.UpsideCaptureRatio ? "🏆 基金 B 进攻强" : "持平"),
                Description = "基准上涨周期中基金超额上涨比率（越高进攻能力越强）"
            },
            new MetricCompareRow
            {
                MetricName = "下行捕获率 (Downside Capture)",
                ValueA = $"{_metricsA.DownsideCaptureRatio:F1}%",
                ValueB = $"{_metricsB.DownsideCaptureRatio:F1}%",
                Winner = _metricsA.DownsideCaptureRatio < _metricsB.DownsideCaptureRatio ? "🏆 基金 A 防守好" : (_metricsA.DownsideCaptureRatio > _metricsB.DownsideCaptureRatio ? "🏆 基金 B 防守好" : "持平"),
                Description = "基准下跌周期中基金跟随下跌比率（越低防守能力越好）"
            },
            new MetricCompareRow
            {
                MetricName = "特雷诺比率 (Treynor)",
                ValueA = $"{_metricsA.TreynorRatio:F2}",
                ValueB = $"{_metricsB.TreynorRatio:F2}",
                Winner = _metricsA.TreynorRatio > _metricsB.TreynorRatio ? "🏆 基金 A 优" : (_metricsA.TreynorRatio < _metricsB.TreynorRatio ? "🏆 基金 B 优" : "持平"),
                Description = "承担每单位系统性贝塔风险所获得的超额回报（适合核心配置资产）"
            },
            new MetricCompareRow
            {
                MetricName = "年化跟踪误差 (Tracking Error)",
                ValueA = $"{_metricsA.TrackingError:F2}%",
                ValueB = $"{_metricsB.TrackingError:F2}%",
                Winner = _metricsA.TrackingError < _metricsB.TrackingError ? "🏆 基金 A 紧跟基准" : (_metricsA.TrackingError > _metricsB.TrackingError ? "🏆 基金 B 紧跟基准" : "持平"),
                Description = "基金相对业绩比较基准超额收益的年化离散度（被动指数越小越优）"
            },
            new MetricCompareRow
            {
                MetricName = "风格漂移指数 (SDI)",
                ValueA = _fundA.StyleDrift != null ? $"{_fundA.StyleDrift.StyleDriftIndex:F2} ({_fundA.StyleDrift.StabilityRating})" : "未计算",
                ValueB = _fundB.StyleDrift != null ? $"{_fundB.StyleDrift.StyleDriftIndex:F2} ({_fundB.StyleDrift.StabilityRating})" : "未计算",
                Winner = (_fundA.StyleDrift?.StyleDriftIndex ?? 999) < (_fundB.StyleDrift?.StyleDriftIndex ?? 999) ? "🏆 基金 A 风格更稳定" : "🏆 基金 B 风格更稳定",
                Description = "晨星九宫格近4期重仓历史漂移度（SDI < 0.6 为极度稳定，> 1.5 为显著漂移）"
            },
            new MetricCompareRow
            {
                MetricName = "奥米加比率 (Omega Ratio)",
                ValueA = $"{_metricsA.OmegaRatio:F2}",
                ValueB = $"{_metricsB.OmegaRatio:F2}",
                Winner = _metricsA.OmegaRatio > _metricsB.OmegaRatio ? "🏆 基金 A 收益胜率质量高" : (_metricsA.OmegaRatio < _metricsB.OmegaRatio ? "🏆 基金 B 收益胜率质量高" : "持平"),
                Description = "考虑收益率非正态偏度与峰度的全概率比率（收益概率质量比，>1 说明正期望胜率占优）"
            },
            new MetricCompareRow
            {
                MetricName = "溃疡指数 (Ulcer Index)",
                ValueA = $"{_metricsA.UlcerIndex:F2}%",
                ValueB = $"{_metricsB.UlcerIndex:F2}%",
                Winner = _metricsA.UlcerIndex < _metricsB.UlcerIndex ? "🏆 基金 A 溃疡风险低" : (_metricsA.UlcerIndex > _metricsB.UlcerIndex ? "🏆 基金 B 溃疡风险低" : "持平"),
                Description = "Peter Martin 衡量回撤深度与水下滞留持续时间的痛苦程度（越低持仓痛苦越小）"
            },
            new MetricCompareRow
            {
                MetricName = "马丁比率 (Martin Ratio)",
                ValueA = $"{_metricsA.MartinRatio:F2}",
                ValueB = $"{_metricsB.MartinRatio:F2}",
                Winner = _metricsA.MartinRatio > _metricsB.MartinRatio ? "🏆 基金 A 风险性价比高" : (_metricsA.MartinRatio < _metricsB.MartinRatio ? "🏆 基金 B 风险性价比高" : "持平"),
                Description = "年化超额收益 / 溃疡指数，综合考量收益与回撤滞留痛苦（越大越优）"
            },
            new MetricCompareRow
            {
                MetricName = "收益痛苦比 (Pain Ratio)",
                ValueA = $"{_metricsA.PainRatio:F2}",
                ValueB = $"{_metricsB.PainRatio:F2}",
                Winner = _metricsA.PainRatio > _metricsB.PainRatio ? "🏆 基金 A 回报痛苦比高" : (_metricsA.PainRatio < _metricsB.PainRatio ? "🏆 基金 B 回报痛苦比高" : "持平"),
                Description = "年化复合收益 / 历史平均水下回撤绝对值（越高说明收益相对回撤体验越佳）"
            },
            new MetricCompareRow
            {
                MetricName = "下行标准差 (σ_down)",
                ValueA = $"{_metricsA.DownsideDeviation:F2}%",
                ValueB = $"{_metricsB.DownsideDeviation:F2}%",
                Winner = _metricsA.DownsideDeviation < _metricsB.DownsideDeviation ? "🏆 基金 A 下行波动更小" : (_metricsA.DownsideDeviation > _metricsB.DownsideDeviation ? "🏆 基金 B 下行波动更小" : "持平"),
                Description = "仅统计低于无风险收益率 (Rf=2%) 的负向波动风险（越低下行防御越佳）"
            },
            new MetricCompareRow
            {
                MetricName = "经理月度胜率 (vs 沪深300)",
                ValueA = _metricsA.ManagerProfile != null ? $"{_metricsA.ManagerProfile.RollingMonthlyWinRate:F1}% ({_metricsA.ManagerProfile.StabilityRating})" : "未统计",
                ValueB = _metricsB.ManagerProfile != null ? $"{_metricsB.ManagerProfile.RollingMonthlyWinRate:F1}% ({_metricsB.ManagerProfile.StabilityRating})" : "未统计",
                Winner = (_metricsA.ManagerProfile?.RollingMonthlyWinRate ?? 0) > (_metricsB.ManagerProfile?.RollingMonthlyWinRate ?? 0) ? "🏆 基金 A 经理胜率高" : "🏆 基金 B 经理胜率高",
                Description = "现任基金经理任职期间滚动月度战胜沪深300指数的月份胜率占比"
            },
            new MetricCompareRow
            {
                MetricName = "经理任期复合年化 (CAGR)",
                ValueA = _metricsA.ManagerProfile != null ? $"{_metricsA.ManagerProfile.TenureAnnualizedReturn:+0.00;-0.00;0.00}%" : "未统计",
                ValueB = _metricsB.ManagerProfile != null ? $"{_metricsB.ManagerProfile.TenureAnnualizedReturn:+0.00;-0.00;0.00}%" : "未统计",
                Winner = (_metricsA.ManagerProfile?.TenureAnnualizedReturn ?? -999) > (_metricsB.ManagerProfile?.TenureAnnualizedReturn ?? -999) ? "🏆 基金 A 经理任期更优" : "🏆 基金 B 经理任期更优",
                Description = "现任基金经理在任期间净值实际年化复合增长率"
            },
            new MetricCompareRow
            {
                MetricName = "Barra 特质阿尔法 (Specific Alpha)",
                ValueA = _metricsA.BarraAttribution != null ? $"{_metricsA.BarraAttribution.SpecificAlpha:+0.00;-0.00;0.00}%" : "未统计",
                ValueB = _metricsB.BarraAttribution != null ? $"{_metricsB.BarraAttribution.SpecificAlpha:+0.00;-0.00;0.00}%" : "未统计",
                Winner = (_metricsA.BarraAttribution?.SpecificAlpha ?? -999) > (_metricsB.BarraAttribution?.SpecificAlpha ?? -999) ? "🏆 基金 A 特质选基能力强" : "🏆 基金 B 特质选基能力强",
                Description = "Barra CNE6 多因子模型剥离六大风格风险后，基金经理纯粹的主动特质阿尔法年化收益"
            },
            new MetricCompareRow
            {
                MetricName = "Barra 主导风格与拟合度 (R²)",
                ValueA = _metricsA.BarraAttribution != null ? $"{_metricsA.BarraAttribution.DominantStyle} (R²: {_metricsA.BarraAttribution.RSquared * 100m:F1}%)" : "未统计",
                ValueB = _metricsB.BarraAttribution != null ? $"{_metricsB.BarraAttribution.DominantStyle} (R²: {_metricsB.BarraAttribution.RSquared * 100m:F1}%)" : "未统计",
                Winner = (_metricsA.BarraAttribution?.RSquared ?? 0) > (_metricsB.BarraAttribution?.RSquared ?? 0) ? "基金 A 模型解释度高" : "基金 B 模型解释度高",
                Description = "Barra 岭回归对基金净值波动的拟合优度 R² 及当前显著主导的风格标签"
            },
            new MetricCompareRow
            {
                MetricName = "95% 修正极端在险 (mVaR 95%)",
                ValueA = $"{_metricsA.CornishFisherVaR95:F2}% (ES: {_metricsA.CornishFisherCVaR95:F2}%)",
                ValueB = $"{_metricsB.CornishFisherVaR95:F2}% (ES: {_metricsB.CornishFisherCVaR95:F2}%)",
                Winner = _metricsA.CornishFisherVaR95 < _metricsB.CornishFisherVaR95 ? "🏆 基金 A 极端尾部风险更低" : "🏆 基金 B 极端尾部风险更低",
                Description = "基于 Cornish-Fisher 展开式将三阶偏度与四阶超额峰度纳入修正计算的 95% 置信度日度极值损失在险价值"
            },
            new MetricCompareRow
            {
                MetricName = "偏度与超额峰度 (Skewness / Kurt)",
                ValueA = $"S: {_metricsA.Skewness:+0.00;-0.00;0.00} | K: {_metricsA.ExcessKurtosis:+0.00;-0.00;0.00}",
                ValueB = $"S: {_metricsB.Skewness:+0.00;-0.00;0.00} | K: {_metricsB.ExcessKurtosis:+0.00;-0.00;0.00}",
                Winner = _metricsA.Skewness > _metricsB.Skewness ? "🏆 基金 A 收益分布偏度更正向(抗暴跌)" : "🏆 基金 B 收益分布偏度更正向(抗暴跌)",
                Description = "偏度衡量收益暴跌厚尾非对称性(越正向越安全)，超额峰度衡量发生极端黑天鹅行情的厚尾厚度"
            },
            new MetricCompareRow
            {
                MetricName = "水下套牢时间占比 (Underwater Ratio)",
                ValueA = $"{_metricsA.UnderwaterTimeRatio:F1}% (最长: {_metricsA.MaxUnderwaterDays}日)",
                ValueB = $"{_metricsB.UnderwaterTimeRatio:F1}% (最长: {_metricsB.MaxUnderwaterDays}日)",
                Winner = _metricsA.UnderwaterTimeRatio < _metricsB.UnderwaterTimeRatio ? "🏆 基金 A 处于亏损水下时间更短" : "🏆 基金 B 处于亏损水下时间更短",
                Description = "全历史区间中基金净值低于历史峰值的交易日比例，度量基民买入后遭遇漫长套牢煎熬的生理痛苦时间"
            },
            new MetricCompareRow
            {
                MetricName = "FF5 特质阿尔法 (Fama-French Alpha)",
                ValueA = _metricsA.FamaFrenchResult != null ? $"{_metricsA.FamaFrenchResult.AlphaAnnualized:+0.00;-0.00;0.00}% (R²: {_metricsA.FamaFrenchResult.RSquaredPercent}%)" : "未统计",
                ValueB = _metricsB.FamaFrenchResult != null ? $"{_metricsB.FamaFrenchResult.AlphaAnnualized:+0.00;-0.00;0.00}% (R²: {_metricsB.FamaFrenchResult.RSquaredPercent}%)" : "未统计",
                Winner = (_metricsA.FamaFrenchResult?.AlphaAnnualized ?? -999) > (_metricsB.FamaFrenchResult?.AlphaAnnualized ?? -999) ? "🏆 基金 A FF5 特质超额能力更优" : "🏆 基金 B FF5 特质超额能力更优",
                Description = "经典 Fama-French 五因子模型 (市场/规模/价值/盈利/投资) 剥离系统性因子后的真年化超额阿尔法收益"
            }
        };

        // 科学投决与短中期双维度评估及前瞻预测审计对比
        var adviceA = QuantCalculator.GenerateScientificInvestmentAdvice(_fundA, filteredA, _fundA.BenchmarkCsi300);
        var adviceB = QuantCalculator.GenerateScientificInvestmentAdvice(_fundB, filteredB, _fundB.BenchmarkCsi300);

        rows.Add(new MetricCompareRow
        {
            MetricName = "科学投决行动信号 (Action Signal)",
            ValueA = $"{adviceA.ActionSignalText} (置信度 {adviceA.ConvictionScore:F0}%)",
            ValueB = $"{adviceB.ActionSignalText} (置信度 {adviceB.ConvictionScore:F0}%)",
            Winner = adviceA.ConvictionScore > adviceB.ConvictionScore ? "🏆 基金 A 投决置信度更高" : (adviceA.ConvictionScore < adviceB.ConvictionScore ? "🏆 基金 B 投决置信度更高" : "持平"),
            Description = "CIO 投决会综合宏观动量、微观尾部在险价值及跨周期共振输出的操作决议"
        });

        rows.Add(new MetricCompareRow
        {
            MetricName = "短期敏捷研判得分 (5~20D)",
            ValueA = $"{adviceA.ShortTermEvaluation.ShortTermScore:F1}分 ({adviceA.ShortTermEvaluation.ShortTermRating})",
            ValueB = $"{adviceB.ShortTermEvaluation.ShortTermScore:F1}分 ({adviceB.ShortTermEvaluation.ShortTermRating})",
            Winner = adviceA.ShortTermEvaluation.ShortTermScore > adviceB.ShortTermEvaluation.ShortTermScore ? "🏆 基金 A 短期动量与防守占优" : "🏆 基金 B 短期动量与防守占优",
            Description = "基于 5D/20D 动量、14D RSI、超短 95% CVaR 及胜率评估的短期量化敏捷得分"
        });

        rows.Add(new MetricCompareRow
        {
            MetricName = "中期稳健研判得分 (60~250D)",
            ValueA = $"{adviceA.MediumTermEvaluation.MediumTermScore:F1}分 ({adviceA.MediumTermEvaluation.MediumTermRating})",
            ValueB = $"{adviceB.MediumTermEvaluation.MediumTermScore:F1}分 ({adviceB.MediumTermEvaluation.MediumTermRating})",
            Winner = adviceA.MediumTermEvaluation.MediumTermScore > adviceB.MediumTermEvaluation.MediumTermScore ? "🏆 基金 A 中期配置价值更优" : "🏆 基金 B 中期配置价值更优",
            Description = "基于年化超额、信息比率 IR、卡玛比率及风格漂移 SDI 评估的中期底仓稳健得分"
        });

        rows.Add(new MetricCompareRow
        {
            MetricName = "跨周期协同格局 (Concordance)",
            ValueA = $"{adviceA.HorizonConcordance.ConcordanceType} ({adviceA.HorizonConcordance.SuggestedAllocationMultiplier:F2}x)",
            ValueB = $"{adviceB.HorizonConcordance.ConcordanceType} ({adviceB.HorizonConcordance.SuggestedAllocationMultiplier:F2}x)",
            Winner = adviceA.HorizonConcordance.SuggestedAllocationMultiplier > adviceB.HorizonConcordance.SuggestedAllocationMultiplier ? "🏆 基金 A 跨周期协同度高(仓位权重大)" : "🏆 基金 B 跨周期协同度高(仓位权重大)",
            Description = "短微观与中宏观四象限协同状态与建议仓位动态分配乘数"
        });

        rows.Add(new MetricCompareRow
        {
            MetricName = "模拟预测检验命中率 (Hit Rate & PICP)",
            ValueA = $"胜率 {adviceA.RealityAudit.DirectionalHitRate:F1}% (覆盖 {adviceA.RealityAudit.PicpCoverageRatio:F1}%)",
            ValueB = $"胜率 {adviceB.RealityAudit.DirectionalHitRate:F1}% (覆盖 {adviceB.RealityAudit.PicpCoverageRatio:F1}%)",
            Winner = adviceA.RealityAudit.DirectionalHitRate > adviceB.RealityAudit.DirectionalHitRate ? "🏆 基金 A 预测轨迹命中率高" : "🏆 基金 B 预测轨迹命中率高",
            Description = "前瞻模拟分位数预测锥对事后真实净值的方向胜率与 90% 区间覆盖率 PICP"
        });

        rows.Add(new MetricCompareRow
        {
            MetricName = "建议超额增益贡献 (Advice Alpha)",
            ValueA = $"{adviceA.RealityAudit.AdviceAlphaContribution:+0.00;-0.00}%",
            ValueB = $"{adviceB.RealityAudit.AdviceAlphaContribution:+0.00;-0.00}%",
            Winner = adviceA.RealityAudit.AdviceAlphaContribution > adviceB.RealityAudit.AdviceAlphaContribution ? "🏆 基金 A 投资建议超额更高" : "🏆 基金 B 投资建议超额更高",
            Description = "遵循科学动态止损与仓位建议相对于基准买入持有获取的超额 Alpha"
        });

        _cachedMetricRows = rows;
        GridMetrics.ItemsSource = rows;
    }

    private void RenderScoreCardPK()
    {
        if (_fundA == null || _fundB == null || _metricsA == null || _metricsB == null) return;

        var scoreA = _fundA.ScoreCard ?? QuantCalculator.CalculateFundScore(_fundA, _metricsA);
        var scoreB = _fundB.ScoreCard ?? QuantCalculator.CalculateFundScore(_fundB, _metricsB);

        TxtOverallScoreA.Text = $"{scoreA.OverallScore:F1} 分";
        TxtRatingGradeA.Text = $"评级: {scoreA.RatingGrade}";
        TxtOverallScoreB.Text = $"{scoreB.OverallScore:F1} 分";
        TxtRatingGradeB.Text = $"评级: {scoreB.RatingGrade}";

        var rows = new List<ScoreCardCompareRow>
        {
            new ScoreCardCompareRow
            {
                Dimension = "综合量化总分",
                WeightText = "100%",
                ScoreA = $"{scoreA.OverallScore:F1} 分",
                Winner = scoreA.OverallScore > scoreB.OverallScore ? "🏆 基金 A 胜" : (scoreB.OverallScore > scoreA.OverallScore ? "🏆 基金 B 胜" : "平手"),
                ScoreB = $"{scoreB.OverallScore:F1} 分",
                Description = "五维综合加权评价 (卓越 AAA / 优良 AA / 稳健 A / 中性 B / 谨慎 C)"
            },
            new ScoreCardCompareRow
            {
                Dimension = "1. 收益获取能力",
                WeightText = "25%",
                ScoreA = $"{scoreA.ReturnScore:F1} 分",
                Winner = scoreA.ReturnScore > scoreB.ReturnScore ? "🏆 基金 A 胜" : (scoreB.ReturnScore > scoreA.ReturnScore ? "🏆 基金 B 胜" : "持平"),
                ScoreB = $"{scoreB.ReturnScore:F1} 分",
                Description = "区间累计回报、复合年化收益率与跑赢基准能力"
            },
            new ScoreCardCompareRow
            {
                Dimension = "2. 风险控制能力",
                WeightText = "20%",
                ScoreA = $"{scoreA.RiskControlScore:F1} 分",
                Winner = scoreA.RiskControlScore > scoreB.RiskControlScore ? "🏆 基金 A 胜" : (scoreB.RiskControlScore > scoreA.RiskControlScore ? "🏆 基金 B 胜" : "持平"),
                ScoreB = $"{scoreB.RiskControlScore:F1} 分",
                Description = "历史最大回撤控制、波动率平稳度与下行抑制"
            },
            new ScoreCardCompareRow
            {
                Dimension = "3. 风险调整性价比",
                WeightText = "25%",
                ScoreA = $"{scoreA.RiskAdjustedScore:F1} 分",
                Winner = scoreA.RiskAdjustedScore > scoreB.RiskAdjustedScore ? "🏆 基金 A 胜" : (scoreB.RiskAdjustedScore > scoreA.RiskAdjustedScore ? "🏆 基金 B 胜" : "持平"),
                ScoreB = $"{scoreB.RiskAdjustedScore:F1} 分",
                Description = "夏普比率 (Sharpe)、卡玛比率 (Calmar) 与索提诺比率"
            },
            new ScoreCardCompareRow
            {
                Dimension = "4. 极端抗跌防御力",
                WeightText = "15%",
                ScoreA = $"{scoreA.ResilienceScore:F1} 分",
                Winner = scoreA.ResilienceScore > scoreB.ResilienceScore ? "🏆 基金 A 胜" : (scoreB.ResilienceScore > scoreA.ResilienceScore ? "🏆 基金 B 胜" : "持平"),
                ScoreB = $"{scoreB.ResilienceScore:F1} 分",
                Description = "在险价值 VaR95 / CVaR95 与尾部暴跌抵抗能力"
            },
            new ScoreCardCompareRow
            {
                Dimension = "5. 风格配置稳定性",
                WeightText = "15%",
                ScoreA = $"{scoreA.StabilityScore:F1} 分",
                Winner = scoreA.StabilityScore > scoreB.StabilityScore ? "🏆 基金 A 胜" : (scoreB.StabilityScore > scoreA.StabilityScore ? "🏆 基金 B 胜" : "持平"),
                ScoreB = $"{scoreB.StabilityScore:F1} 分",
                Description = "前十大持仓集中度与行业配置一致性"
            }
        };

        GridScoreCardPK.ItemsSource = rows;

        TxtStrengthsA.Text = "优势亮点: " + (scoreA.Strengths.Count > 0 ? string.Join(" | ", scoreA.Strengths) : "稳健均衡");
        TxtWeaknessesA.Text = "风险提示: " + (scoreA.Weaknesses.Count > 0 ? string.Join(" | ", scoreA.Weaknesses) : "未见显著异常");
        TxtStrengthsB.Text = "优势亮点: " + (scoreB.Strengths.Count > 0 ? string.Join(" | ", scoreB.Strengths) : "稳健均衡");
        TxtWeaknessesB.Text = "风险提示: " + (scoreB.Weaknesses.Count > 0 ? string.Join(" | ", scoreB.Weaknesses) : "未见显著异常");
    }

    private void RenderHoldingOverlapAndIndustry()
    {
        if (_fundA == null || _fundB == null) return;

        _overlapResult = FundCompareEngine.CalculateHoldingOverlap(_fundA.Holdings, _fundB.Holdings);

        TxtOverlapBadge.Text = $"重合度: {_overlapResult.OverlapWeightPercent:F1}% ({_overlapResult.OverlapRating})";
        TxtOverlapSummary.Text = _overlapResult.OverlapSummary;

        GridCommonStocks.ItemsSource = _overlapResult.CommonStocks;
        GridIndustrySpread.ItemsSource = _overlapResult.IndustrySpreads;
    }

    private void RenderRealtimeValuation()
    {
        if (_fundA == null || _fundB == null) return;

        var valA = _fundA.RealtimeValuation;
        if (valA != null && valA.EstimatedNav > 0)
        {
            TxtEstimatedNavA.Text = $"{valA.EstimatedNav:F4}";
            TxtEstimatedGrowthA.Text = $"{valA.EstimatedGrowthRate:+0.00;-0.00;0.00}%";
            TxtEstimatedGrowthA.Foreground = System.Windows.Media.Brushes.OrangeRed;
            TxtUnitNavA.Text = $"{valA.UnitNav:F4} 元";
            TxtNavDateA.Text = !string.IsNullOrEmpty(valA.NavDate) ? valA.NavDate : "--";
            TxtValuationTimeA.Text = !string.IsNullOrEmpty(valA.ValuationTime) ? valA.ValuationTime : "盘中实时";
        }
        else
        {
            TxtEstimatedNavA.Text = $"{_fundA.LatestNav:F4}";
            TxtEstimatedGrowthA.Text = "--%";
            TxtUnitNavA.Text = $"{_fundA.LatestNav:F4} 元";
            TxtNavDateA.Text = "--";
            TxtValuationTimeA.Text = "盘后公布数据";
        }

        var valB = _fundB.RealtimeValuation;
        if (valB != null && valB.EstimatedNav > 0)
        {
            TxtEstimatedNavB.Text = $"{valB.EstimatedNav:F4}";
            TxtEstimatedGrowthB.Text = $"{valB.EstimatedGrowthRate:+0.00;-0.00;0.00}%";
            TxtEstimatedGrowthB.Foreground = System.Windows.Media.Brushes.OrangeRed;
            TxtUnitNavB.Text = $"{valB.UnitNav:F4} 元";
            TxtNavDateB.Text = !string.IsNullOrEmpty(valB.NavDate) ? valB.NavDate : "--";
            TxtValuationTimeB.Text = !string.IsNullOrEmpty(valB.ValuationTime) ? valB.ValuationTime : "盘中实时";
        }
        else
        {
            TxtEstimatedNavB.Text = $"{_fundB.LatestNav:F4}";
            TxtEstimatedGrowthB.Text = "--%";
            TxtUnitNavB.Text = $"{_fundB.LatestNav:F4} 元";
            TxtNavDateB.Text = "--";
            TxtValuationTimeB.Text = "盘后公布数据";
        }
    }

    private void BtnExportReport_Click(object sender, RoutedEventArgs e)
    {
        if (_fundA == null || _fundB == null || _metricsA == null || _metricsB == null)
        {
            MessageBox.Show("请先完成两只基金的对比计算。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _overlapResult ??= FundCompareEngine.CalculateHoldingOverlap(_fundA.Holdings, _fundB.Holdings);

        string defaultFileName = $"BIGA_基金对比研报_{_fundA.Code}_{_fundB.Code}_{DateTime.Now:yyyyMMdd}.html";
        var sfd = new SaveFileDialog
        {
            Title = "导出双基金量化对比投研研报 (HTML/PDF)",
            Filter = "HTML 研报文件 (*.html)|*.html|所有文件 (*.*)|*.*",
            FileName = defaultFileName
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                string range = (CmbTimeRange.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "近1年";
                ExportService.ExportComparisonToHtml(
                    sfd.FileName,
                    _fundA,
                    _metricsA,
                    _fundB,
                    _metricsB,
                    _overlapResult,
                    _cachedCorrelation,
                    _cachedCorrRating,
                    _cachedWinRateA,
                    _cachedWinRateB,
                    _cachedMetricRows,
                    range);

                var choice = MessageBox.Show($"对比研报已成功导出至:\n{sfd.FileName}\n\n是否立即在默认浏览器中打开查看？", "导出成功", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (choice == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出对比研报失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private static (List<NavRecord> A, List<NavRecord> B) AlignNavHistory(List<NavRecord> navsA, List<NavRecord> navsB, string range)
    {
        if (navsA.Count == 0 || navsB.Count == 0) return (new List<NavRecord>(), new List<NavRecord>());

        DateTime latestDate = navsA[^1].Date < navsB[^1].Date ? navsA[^1].Date : navsB[^1].Date;
        DateTime cutoff = range switch
        {
            "1M" => latestDate.AddMonths(-1),
            "3M" => latestDate.AddMonths(-3),
            "6M" => latestDate.AddMonths(-6),
            "1Y" => latestDate.AddYears(-1),
            "3Y" => latestDate.AddYears(-3),
            "5Y" => latestDate.AddYears(-5),
            _ => DateTime.MinValue
        };

        var dictB = navsB.ToDictionary(n => n.Date);
        var alignedA = new List<NavRecord>();
        var alignedB = new List<NavRecord>();

        foreach (var na in navsA)
        {
            if (na.Date >= cutoff && dictB.TryGetValue(na.Date, out var nb))
            {
                alignedA.Add(na);
                alignedB.Add(nb);
            }
        }

        return (alignedA, alignedB);
    }
}
