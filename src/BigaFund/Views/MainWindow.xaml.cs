using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BigaFund.Models;
using BigaFund.Services;
using BigaFund.ViewModels;

namespace BigaFund.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private long _lastRenderTimestamp;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        _vm.RequestPlotRefresh += RefreshPlot;

        MainPlot.MouseMove += MainPlot_MouseMove;
        MainPlot.MouseLeave += MainPlot_MouseLeave;

        Loaded += async (s, e) =>
        {
            SetupPlotStyle();
            await _vm.LoadFavoritesAsync();
            // 启动时默认加载明星经典公募基金：华夏成长 (000001)
            await _vm.LoadFundDetailAsync("000001");

            // 初始化各 Tab 工作台子系统
            ScreenerControl.Initialize(_vm.DataService);
            CompareControl.Initialize(_vm.DataService, "000001", "005827");
            PortfolioControl.Initialize(_vm.DataService, new[] { "000001", "000171" });
            SimulationControl.Initialize(_vm.DataService);
            SettingsControl.Initialize(_vm.DataService.DuckDb);

            // 跨 Tab 联动事件绑定
            ScreenerControl.OnSelectFundForMain += async (code) =>
            {
                _vm.SelectedMainTabIndex = 0;
                await _vm.LoadFundDetailAsync(code);
            };

            SimulationControl.OnSelectFundForMain += async (code) =>
            {
                _vm.SelectedMainTabIndex = 0;
                await _vm.LoadFundDetailAsync(code);
            };

            ScreenerControl.OnSelectFundForCompare += async (code) =>
            {
                _vm.SelectedMainTabIndex = 2;
                await CompareControl.SetFundsAsync(_vm.CurrentFund?.Code ?? "000001", code);
            };

            ScreenerControl.OnSendFundsToPortfolio += async (codes) =>
            {
                _vm.SelectedMainTabIndex = 3;
                await PortfolioControl.SetFundsAsync(codes);
            };

            ScreenerControl.OnSendFundsToCompare += async (codes) =>
            {
                _vm.SelectedMainTabIndex = 2;
                if (codes.Count >= 2)
                {
                    await CompareControl.SetFundsAsync(codes[0], codes[1]);
                }
                else if (codes.Count == 1)
                {
                    await CompareControl.SetFundsAsync(_vm.CurrentFund?.Code ?? "000001", codes[0]);
                }
            };

            _vm.RequestOpenCompare += async (codeA, codeB) =>
            {
                await CompareControl.SetFundsAsync(codeA, codeB);
            };

            _vm.RequestOpenPortfolio += async (codes) =>
            {
                await PortfolioControl.SetFundsAsync(codes);
            };

            _vm.RequestOpenScreenerWithSector += (sec) =>
            {
                ScreenerControl.SetSectorFilter(sec);
            };

            _vm.RequestSimulateBuy += (code) =>
            {
                SimulationControl.OpenQuickBuyForFund(code);
            };
        };
    }

    private ScottPlot.Plottables.Crosshair? _crosshair;
    private List<NavRecord> _cachedNavs = new();
    private List<DcaPoint> _cachedTimeline = new();
    private List<BenchmarkRecord> _cachedBenchmark = new();
    private readonly Dictionary<DateTime, double> _cachedDrawdownMap = new();
    private readonly Dictionary<DateTime, (double Volatility, double Sharpe)> _cachedRollingMap = new();
    private readonly List<SimulationQuantilePoint> _cachedForecastPoints = new();

    private void SetupPlotStyle()
    {
        var plot = MainPlot.Plot;
        PlotStyleHelper.ApplyDarkThemeAndFont(plot);
        plot.Axes.Left.Label.Text = string.Empty;
        plot.Axes.Right.Label.Text = string.Empty;
    }

    private void RefreshPlot()
    {
        Dispatcher.Invoke(() =>
        {
            var plot = MainPlot.Plot;
            plot.Clear();
            SetupPlotStyle();

            if (_vm.CurrentFund == null || _vm.CurrentFund.NavHistory.Count < 2)
            {
                MainPlot.Refresh();
                return;
            }

            var filteredNavs = _vm.GetFilteredNavHistory();
            if (filteredNavs.Count < 2)
            {
                MainPlot.Refresh();
                return;
            }

            _cachedNavs = filteredNavs;
            _cachedTimeline = _vm.BacktestResult?.Timeline ?? new();
            _cachedBenchmark = _vm.CurrentFund?.BenchmarkCsi300 ?? new();

            switch (_vm.ActiveChartTab)
            {
                case 0:
                    RenderPerformanceChart(plot, filteredNavs);
                    break;
                case 1:
                    RenderBacktestChart(plot);
                    break;
                case 2:
                    RenderDrawdownChart(plot, filteredNavs);
                    break;
                case 3:
                    RenderRollingMetricsChart(plot, filteredNavs);
                    break;
                case 4:
                    RenderSimulationForecastChart(plot, filteredNavs);
                    break;
            }

            _crosshair = plot.Add.Crosshair(0, 0);
            _crosshair.IsVisible = false;
            _crosshair.LineColor = ScottPlot.Color.FromHex("#89B4FA").WithAlpha(0.65f);
            _crosshair.LineWidth = 1.2f;
            _crosshair.LinePattern = ScottPlot.LinePattern.Dashed;

            plot.Axes.AutoScale();
            MainPlot.Refresh();
        });
    }

    /// <summary>
    /// 模式 0: 基金累计收益走势 vs 沪深300基准走势
    /// </summary>
    private void RenderPerformanceChart(ScottPlot.Plot plot, List<NavRecord> navs)
    {
        decimal baseNav = navs[0].CumulativeNav > 0 ? navs[0].CumulativeNav : navs[0].UnitNav;
        if (baseNav <= 0) baseNav = 1m;

        double[] xs = navs.Select(n => n.Date.ToOADate()).ToArray();
        double[] ys = navs.Select(n =>
        {
            decimal eff = n.CumulativeNav > 0 ? n.CumulativeNav : n.UnitNav;
            return (double)((eff - baseNav) / baseNav * 100m);
        }).ToArray();

        // 1. 基金累计收益走势曲线 (鲜亮红粉色)
        var fundScatter = plot.Add.Scatter(xs, ys);
        fundScatter.LegendText = $"{_vm.CurrentFund?.Name} (累计收益率 %)";
        fundScatter.Color = ScottPlot.Color.FromHex("#F38BA8");
        fundScatter.LineWidth = 2;
        fundScatter.MarkerSize = 0;

        // 2. 沪深300对比基准曲线 (橙色)
        if (_vm.CurrentFund?.BenchmarkCsi300 != null && _vm.CurrentFund.BenchmarkCsi300.Count > 0)
        {
            DateTime start = navs[0].Date;
            DateTime end = navs[^1].Date;
            var bm = _vm.CurrentFund.BenchmarkCsi300
                .Where(b => b.Date >= start && b.Date <= end)
                .ToList();

            if (bm.Count >= 2)
            {
                decimal bmBase = bm[0].CumulativeReturnRate;
                double[] bmXs = bm.Select(b => b.Date.ToOADate()).ToArray();
                double[] bmYs = bm.Select(b => (double)(b.CumulativeReturnRate - bmBase)).ToArray();

                var bmScatter = plot.Add.Scatter(bmXs, bmYs);
                bmScatter.LegendText = "沪深300基准 (对比走势 %)";
                bmScatter.Color = ScottPlot.Color.FromHex("#FAB387");
                bmScatter.LineWidth = 1.8f;
                bmScatter.MarkerSize = 0;
            }
        }

        // 零轴基准线 (灰色虚线)
        var zeroLine = plot.Add.HorizontalLine(0);
        zeroLine.Color = ScottPlot.Color.FromHex("#585B70");
        zeroLine.LineWidth = 1;
        zeroLine.LinePattern = ScottPlot.LinePattern.Dashed;

        plot.ShowLegend(ScottPlot.Alignment.UpperLeft);
    }

    /// <summary>
    /// 模式 1: 定投策略资产增长与成本线
    /// </summary>
    private void RenderBacktestChart(ScottPlot.Plot plot)
    {
        if (_vm.BacktestResult == null || _vm.BacktestResult.Timeline.Count < 2) return;

        var timeline = _vm.BacktestResult.Timeline;
        double[] xs = timeline.Select(t => t.Date.ToOADate()).ToArray();
        double[] values = timeline.Select(t => (double)t.CurrentValue).ToArray();
        double[] costs = timeline.Select(t => (double)t.TotalInvested).ToArray();

        // 1. 期末市值走势 (亮蓝色)
        var valScatter = plot.Add.Scatter(xs, values);
        valScatter.LegendText = "定投持仓总市值 (元)";
        valScatter.Color = ScottPlot.Color.FromHex("#89B4FA");
        valScatter.LineWidth = 2.2f;
        valScatter.MarkerSize = 0;

        // 2. 累计投入本金曲线 (暖黄色)
        var costScatter = plot.Add.Scatter(xs, costs);
        costScatter.LegendText = "累计投入总本金 (元)";
        costScatter.Color = ScottPlot.Color.FromHex("#F9E2AF");
        costScatter.LineWidth = 1.8f;
        costScatter.MarkerSize = 0;

        plot.ShowLegend(ScottPlot.Alignment.UpperLeft);
    }

    /// <summary>
    /// 模式 2: 历史动态水下回撤曲线 (Underwater Chart)
    /// </summary>
    private void RenderDrawdownChart(ScottPlot.Plot plot, List<NavRecord> navs)
    {
        var ddSeries = BigaFund.Services.QuantCalculator.CalculateDrawdownSeries(navs);
        if (ddSeries.Count == 0) return;

        double[] xs = new double[ddSeries.Count];
        double[] dds = new double[ddSeries.Count];

        _cachedDrawdownMap.Clear();
        for (int i = 0; i < ddSeries.Count; i++)
        {
            xs[i] = ddSeries[i].Date.ToOADate();
            dds[i] = (double)ddSeries[i].DrawdownRate;
            _cachedDrawdownMap[ddSeries[i].Date] = (double)ddSeries[i].DrawdownRate;
        }

        var ddScatter = plot.Add.Scatter(xs, dds);
        ddScatter.LegendText = "全历史动态回撤 (Underwater %)";
        ddScatter.Color = ScottPlot.Color.FromHex("#F38BA8"); // 机构风控水下回撤警示粉红色
        ddScatter.LineWidth = 2;
        ddScatter.MarkerSize = 0;

        var zeroLine = plot.Add.HorizontalLine(0);
        zeroLine.Color = ScottPlot.Color.FromHex("#585B70");
        zeroLine.LineWidth = 1.2f;
        zeroLine.LinePattern = ScottPlot.LinePattern.Dashed;

        plot.ShowLegend(ScottPlot.Alignment.LowerLeft);
    }

    /// <summary>
    /// 模式 3: 滚动风控走势 (60交易日滚动年化波动率与夏普比率双轴可视化)
    /// </summary>
    private void RenderRollingMetricsChart(ScottPlot.Plot plot, List<NavRecord> navs)
    {
        var rolling = BigaFund.Services.QuantCalculator.CalculateRollingMetrics(navs, window: 60, riskFreeRate: 2.0m);
        _cachedRollingMap.Clear();
        if (rolling.Count < 2) return;

        double[] xs = new double[rolling.Count];
        double[] vols = new double[rolling.Count];
        double[] sharpes = new double[rolling.Count];

        for (int i = 0; i < rolling.Count; i++)
        {
            var r = rolling[i];
            xs[i] = r.Date.ToOADate();
            vols[i] = (double)r.RollingVolatility;
            sharpes[i] = (double)r.RollingSharpe;
            _cachedRollingMap[r.Date] = (vols[i], sharpes[i]);
        }

        // 1. 滚动年化波动率 (主左轴，珊瑚红)
        var volScatter = plot.Add.Scatter(xs, vols);
        volScatter.LegendText = "60日滚动年化波动率 (%)";
        volScatter.Color = ScottPlot.Color.FromHex("#FF5376");
        volScatter.LineWidth = 2;
        volScatter.MarkerSize = 0;
        volScatter.Axes.YAxis = plot.Axes.Left;
        plot.Axes.Left.Label.Text = "波动率 (%)";
        plot.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#FF5376");

        // 2. 滚动夏普比率 (副右轴，天蓝色)
        var sharpeScatter = plot.Add.Scatter(xs, sharpes);
        sharpeScatter.LegendText = "60日滚动夏普 (右轴)";
        sharpeScatter.Color = ScottPlot.Color.FromHex("#89B4FA");
        sharpeScatter.LineWidth = 1.8f;
        sharpeScatter.MarkerSize = 0;
        sharpeScatter.Axes.YAxis = plot.Axes.Right;
        plot.Axes.Right.Label.Text = "夏普比率";
        plot.Axes.Right.Label.ForeColor = ScottPlot.Color.FromHex("#89B4FA");

        // 3. 夏普比率 0 轴参考线 (挂在右轴)
        var zeroLine = plot.Add.HorizontalLine(0);
        zeroLine.Color = ScottPlot.Color.FromHex("#585B70");
        zeroLine.LineWidth = 1;
        zeroLine.LinePattern = ScottPlot.LinePattern.Dashed;
        zeroLine.Axes.YAxis = plot.Axes.Right;

        plot.ShowLegend(ScottPlot.Alignment.UpperLeft);
    }

    /// <summary>
    /// 模式 4: 前瞻模拟预测分位数锥与现实走势对比 (Forward Simulation Cone & Reality Audit)
    /// </summary>
    private void RenderSimulationForecastChart(ScottPlot.Plot plot, List<NavRecord> navs)
    {
        _cachedForecastPoints.Clear();

        // 1. 历史真实单位净值走势曲线 (冰蓝色实线)
        double[] histXs = navs.Select(n => n.Date.ToOADate()).ToArray();
        double[] histYs = navs.Select(n => (double)n.UnitNav).ToArray();

        var histScatter = plot.Add.Scatter(histXs, histYs);
        histScatter.LegendText = $"{_vm.CurrentFund?.Name} 历史真实单位净值 (元)";
        histScatter.Color = ScottPlot.Color.FromHex("#89DCEB");
        histScatter.LineWidth = 2.2f;
        histScatter.MarkerSize = 0;

        // 2. 前瞻分位数预测锥 (P10 悲观, P50 预期, P90 乐观)
        var forecast = _vm.SimulationForecast ?? _vm.ScientificAdvice?.SimulationForecast;
        if (forecast != null && forecast.ForecastTrajectory.Count > 1)
        {
            _cachedForecastPoints.AddRange(forecast.ForecastTrajectory);

            var traj = forecast.ForecastTrajectory;
            double[] fXs = traj.Select(p => p.ForecastDate.ToOADate()).ToArray();
            double[] p50s = traj.Select(p => (double)p.P50_Expected).ToArray();
            double[] p10s = traj.Select(p => (double)p.P10_Pessimistic).ToArray();
            double[] p90s = traj.Select(p => (double)p.P90_Optimistic).ToArray();

            // P50 预期中枢轨迹 (黄色虚线)
            var p50Line = plot.Add.Scatter(fXs, p50s);
            p50Line.LegendText = "预测中枢期望 P50 (元)";
            p50Line.Color = ScottPlot.Color.FromHex("#F9E2AF");
            p50Line.LineWidth = 2.2f;
            p50Line.LinePattern = ScottPlot.LinePattern.Dashed;
            p50Line.MarkerSize = 0;

            // P10 悲观下界 (粉红色点线)
            var p10Line = plot.Add.Scatter(fXs, p10s);
            p10Line.LegendText = "悲观下界 P10 (80%置信底)";
            p10Line.Color = ScottPlot.Color.FromHex("#F38BA8");
            p10Line.LineWidth = 1.8f;
            p10Line.LinePattern = ScottPlot.LinePattern.Dotted;
            p10Line.MarkerSize = 0;

            // P90 乐观上界 (浅绿色点线)
            var p90Line = plot.Add.Scatter(fXs, p90s);
            p90Line.LegendText = "乐观上界 P90 (80%置信顶)";
            p90Line.Color = ScottPlot.Color.FromHex("#A6E3A1");
            p90Line.LineWidth = 1.8f;
            p90Line.LinePattern = ScottPlot.LinePattern.Dotted;
            p90Line.MarkerSize = 0;
        }

        // 3. 动态追踪止损线与目标止盈线水平参考
        if (_vm.ScientificAdvice != null)
        {
            if (_vm.ScientificAdvice.TrailingStopLossNav > 0)
            {
                var stopLine = plot.Add.HorizontalLine((double)_vm.ScientificAdvice.TrailingStopLossNav);
                stopLine.Color = ScottPlot.Color.FromHex("#F38BA8");
                stopLine.LineWidth = 1.2f;
                stopLine.LinePattern = ScottPlot.LinePattern.Dashed;
                stopLine.LegendText = $"动态追踪止损 ({(double)_vm.ScientificAdvice.TrailingStopLossNav:F4})";
            }

            if (_vm.ScientificAdvice.TakeProfitTargetNav > 0)
            {
                var profitLine = plot.Add.HorizontalLine((double)_vm.ScientificAdvice.TakeProfitTargetNav);
                profitLine.Color = ScottPlot.Color.FromHex("#FAB387");
                profitLine.LineWidth = 1.2f;
                profitLine.LinePattern = ScottPlot.LinePattern.Dashed;
                profitLine.LegendText = $"目标止盈位 ({(double)_vm.ScientificAdvice.TakeProfitTargetNav:F4})";
            }
        }

        plot.Axes.Left.Label.Text = "单位净值 (元)";
        plot.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#CDD6F4");
        plot.ShowLegend(ScottPlot.Alignment.UpperLeft);
    }

    private void MainPlot_MouseMove(object sender, MouseEventArgs e)
    {
        if (_crosshair == null || _vm == null || _vm.CurrentFund == null) return;

        long now = Environment.TickCount64;
        if (now - _lastRenderTimestamp < 16) return;
        _lastRenderTimestamp = now;

        try
        {
            var position = e.GetPosition(MainPlot);
            var pixel = new ScottPlot.Pixel((float)position.X, (float)position.Y);
            var coords = MainPlot.Plot.GetCoordinates(pixel);

            _crosshair.Position = coords;
            _crosshair.IsVisible = true;

            DateTime hoverDate;
            try
            {
                hoverDate = DateTime.FromOADate(coords.X);
            }
            catch
            {
                return;
            }

            UpdatePlotHud(hoverDate, coords.Y);
            MainPlot.Refresh();
        }
        catch
        {
            // 忽略非有效浮点边界
        }
    }

    private void MainPlot_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_crosshair != null)
        {
            _crosshair.IsVisible = false;
            MainPlot.Refresh();
        }
        TxtPlotHud.Text = "💡 移动鼠标至图表区域可交互读取时序精准数据";
    }

    private void UpdatePlotHud(DateTime hoverDate, double yCoord)
    {
        if (_vm == null || _vm.CurrentFund == null) return;

        switch (_vm.ActiveChartTab)
        {
            case 0: // 收益走势 vs 沪深300基准
                if (_cachedNavs.Count > 0)
                {
                    var nav = FindClosestNav(_cachedNavs, hoverDate);
                    if (nav != null)
                    {
                        decimal baseNav = _cachedNavs[0].CumulativeNav > 0 ? _cachedNavs[0].CumulativeNav : _cachedNavs[0].UnitNav;
                        if (baseNav <= 0) baseNav = 1m;
                        decimal eff = nav.CumulativeNav > 0 ? nav.CumulativeNav : nav.UnitNav;
                        decimal fundRet = (eff - baseNav) / baseNav * 100m;

                        string bmText = "--";
                        if (_cachedBenchmark.Count > 0)
                        {
                            var bm = FindClosestBenchmark(_cachedBenchmark, nav.Date);
                            if (bm != null)
                            {
                                decimal bmBase = _cachedBenchmark[0].CumulativeReturnRate;
                                decimal bmRet = bm.CumulativeReturnRate - bmBase;
                                bmText = $"{bmRet:+0.00;-0.00;0.00}%";
                            }
                        }

                        TxtPlotHud.Text = $"📅 {nav.Date:yyyy-MM-dd} | 📈 基金收益: {fundRet:+0.00;-0.00;0.00}% | 📊 沪深300: {bmText} | 最新净值: {nav.UnitNav:F4}";
                    }
                }
                break;

            case 1: // 定投策略回测
                if (_cachedTimeline.Count > 0)
                {
                    var pt = FindClosestTimeline(_cachedTimeline, hoverDate);
                    if (pt != null)
                    {
                        decimal profit = pt.CurrentValue - pt.TotalInvested;
                        TxtPlotHud.Text = $"📅 {pt.Date:yyyy-MM-dd} | 💰 持仓市值: {pt.CurrentValue:N2}元 | 💳 累计本金: {pt.TotalInvested:N2}元 | 📊 收益率: {pt.ReturnRate:+0.00;-0.00;0.00}% | 盈亏: {profit:+0.00;-0.00;0.00}元";
                    }
                }
                break;

            case 2: // 动态水下回撤
                if (_cachedNavs.Count > 0)
                {
                    var nav = FindClosestNav(_cachedNavs, hoverDate);
                    if (nav != null && _cachedDrawdownMap.TryGetValue(nav.Date, out double dd))
                    {
                        TxtPlotHud.Text = $"📅 {nav.Date:yyyy-MM-dd} | 📉 水下回撤: {dd:+0.00;-0.00;0.00}% | 单位净值: {nav.UnitNav:F4}";
                    }
                }
                break;

            case 3: // 滚动风控 (波动率 & 夏普)
                if (_cachedNavs.Count > 0)
                {
                    var nav = FindClosestNav(_cachedNavs, hoverDate);
                    if (nav != null && _cachedRollingMap.TryGetValue(nav.Date, out var roll))
                    {
                        TxtPlotHud.Text = $"📅 {nav.Date:yyyy-MM-dd} | ⚡ 60日滚动年化波动率: {roll.Volatility:F2}% | 🎯 滚动夏普: {roll.Sharpe:F2} | 单位净值: {nav.UnitNav:F4}";
                    }
                    else if (nav != null)
                    {
                        TxtPlotHud.Text = $"📅 {nav.Date:yyyy-MM-dd} | ⚡ 滚动指标样本不足60交易日 | 单位净值: {nav.UnitNav:F4}";
                    }
                }
                break;

            case 4: // 前瞻模拟预测锥与现实对比
                if (_cachedNavs.Count > 0)
                {
                    DateTime lastRealDate = _cachedNavs[^1].Date;
                    if (hoverDate <= lastRealDate)
                    {
                        var nav = FindClosestNav(_cachedNavs, hoverDate);
                        if (nav != null)
                        {
                            TxtPlotHud.Text = $"📅 {nav.Date:yyyy-MM-dd} (历史真实) | 单位净值: {nav.UnitNav:F4} | 累计净值: {nav.CumulativeNav:F4} | 日涨跌幅: {nav.DailyReturn:+0.00;-0.00;0.00}%";
                        }
                    }
                    else if (_cachedForecastPoints.Count > 0)
                    {
                        var pt = FindClosestForecastPoint(_cachedForecastPoints, hoverDate);
                        if (pt != null)
                        {
                            decimal spread = pt.P90_Optimistic - pt.P10_Pessimistic;
                            TxtPlotHud.Text = $"🔮 预测 T+{pt.DayIndex}日 ({pt.ForecastDate:yyyy-MM-dd}) | 悲观P10: {pt.P10_Pessimistic:F4} | 中枢P50: {pt.P50_Expected:F4} | 乐观P90: {pt.P90_Optimistic:F4} | 80%置信跨度: {spread:F4}";
                        }
                    }
                }
                break;
        }
    }

    private static SimulationQuantilePoint? FindClosestForecastPoint(List<SimulationQuantilePoint> list, DateTime target)
    {
        if (list == null || list.Count == 0) return null;
        SimulationQuantilePoint? closest = null;
        double minDiff = double.MaxValue;
        foreach (var item in list)
        {
            double diff = Math.Abs((item.ForecastDate - target).TotalDays);
            if (diff < minDiff)
            {
                minDiff = diff;
                closest = item;
            }
        }
        return closest;
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

    private static BenchmarkRecord? FindClosestBenchmark(List<BenchmarkRecord> list, DateTime target)
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

    private static DcaPoint? FindClosestTimeline(List<DcaPoint> list, DateTime target)
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

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _vm.SearchCommand.Execute(null);
        }
    }

    private void SearchList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedItem is FundInfo info)
        {
            _vm.SelectFundCommand.Execute(info);
        }
    }

    private void QuickPick_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is FundInfo info)
        {
            _vm.SelectFundCommand.Execute(info);
        }
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is UserFavorite fav)
        {
            _vm.SelectFavoriteCommand.Execute(fav);
        }
    }

    private void BtnQuickAddFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FundInfo info)
        {
            _vm.AddFavoriteQuickCommand.Execute(info);
        }
    }

    private void BtnRemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string code)
        {
            _vm.RemoveFavoriteQuickCommand.Execute(code);
        }
    }

    private void FavoritesScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private void BtnScrollFavoritesLeft_Click(object sender, RoutedEventArgs e)
    {
        FavoritesScrollViewer.ScrollToHorizontalOffset(Math.Max(0, FavoritesScrollViewer.HorizontalOffset - 180));
    }

    private void BtnScrollFavoritesRight_Click(object sender, RoutedEventArgs e)
    {
        FavoritesScrollViewer.ScrollToHorizontalOffset(FavoritesScrollViewer.HorizontalOffset + 180);
    }

    private void FavoritesScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (BtnScrollLeft != null && BtnScrollRight != null)
        {
            bool hasOverflow = FavoritesScrollViewer.ScrollableWidth > 0;
            BtnScrollLeft.Visibility = hasOverflow ? Visibility.Visible : Visibility.Collapsed;
            BtnScrollRight.Visibility = hasOverflow ? Visibility.Visible : Visibility.Collapsed;
            BtnScrollLeft.IsEnabled = FavoritesScrollViewer.HorizontalOffset > 0;
            BtnScrollRight.IsEnabled = FavoritesScrollViewer.HorizontalOffset < FavoritesScrollViewer.ScrollableWidth;
        }
    }

    private void StrategyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm == null || _vm.CurrentFund == null) return;
        _vm.RunBacktestCommand.Execute(null);
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void MenuCalc_Click(object sender, RoutedEventArgs e)
    {
        _vm.OpenCalculatorCommand.Execute(null);
    }
}
