using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using BigaFund.Models;
using BigaFund.Services;

namespace BigaFund.Views;

public partial class PortfolioWindow : Window
{
    private readonly FundDataService _dataService;
    private readonly ObservableCollection<PortfolioItem> _components = new();
    private readonly Dictionary<string, FundDetail> _loadedDetails = new();
    private ScottPlot.Plottables.Crosshair? _crosshair;
    private PortfolioResult? _lastResult;
    private List<(FundDetail Fund, decimal WeightPercent)>? _lastComponents;

    public PortfolioWindow(FundDataService dataService, IEnumerable<string>? initialCodes = null)
    {
        InitializeComponent();
        _dataService = dataService;
        GridComponents.ItemsSource = _components;
        PortfolioPlot.MouseMove += PortfolioPlot_MouseMove;
        PortfolioPlot.MouseLeave += PortfolioPlot_MouseLeave;

        Loaded += async (s, e) =>
        {
            SetupPlotStyle();
            if (initialCodes != null && initialCodes.Any())
            {
                foreach (var c in initialCodes)
                {
                    await AddFundByCodeAsync(c);
                }
            }
            else
            {
                // 默认加载经典股债平衡搭配 (华夏成长 50% + 易方达裕丰 50%)
                await AddFundByCodeAsync("000001", 50m);
                await AddFundByCodeAsync("000171", 50m);
            }

            UpdateWeightStatus();
            await ExecutePortfolioBacktestAsync();
        };
    }

    private void SetupPlotStyle()
    {
        var plot = PortfolioPlot.Plot;
        PlotStyleHelper.ApplyDarkThemeAndFont(plot);
    }

    private async Task AddFundByCodeAsync(string code, decimal defaultWeight = 50m)
    {
        code = code.Trim();
        if (string.IsNullOrEmpty(code)) return;

        if (_components.Any(c => c.Code == code))
        {
            return;
        }

        try
        {
            var detail = await _dataService.GetFundDetailAsync(code);
            if (detail != null && detail.NavHistory.Count > 0)
            {
                _loadedDetails[code] = detail;
                _components.Add(new PortfolioItem
                {
                    Code = detail.Code,
                    Name = detail.Name,
                    Type = detail.Type,
                    WeightPercent = defaultWeight
                });
            }
        }
        catch
        {
            // 忽略单只读取异常
        }
    }

    private async void BtnAddFund_Click(object sender, RoutedEventArgs e)
    {
        string code = TxtAddFundCode.Text.Trim();
        if (string.IsNullOrEmpty(code)) return;

        await AddFundByCodeAsync(code, 20m);
        UpdateWeightStatus();
    }

    private async void BtnQuickTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _components.Clear();
            _loadedDetails.Clear();

            var codes = tag.Split(',', StringSplitOptions.RemoveEmptyEntries);
            decimal perWeight = codes.Length > 0 ? 100m / codes.Length : 50m;

            foreach (var c in codes)
            {
                await AddFundByCodeAsync(c, perWeight);
            }

            UpdateWeightStatus();
            await ExecutePortfolioBacktestAsync();
        }
    }

    private void BtnRemoveComponent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is PortfolioItem item)
        {
            _components.Remove(item);
            _loadedDetails.Remove(item.Code);
            UpdateWeightStatus();
        }
    }

    private void Weight_LostFocus(object sender, RoutedEventArgs e)
    {
        UpdateWeightStatus();
    }

    private void UpdateWeightStatus()
    {
        decimal total = _components.Sum(c => c.WeightPercent);
        if (Math.Abs(total - 100m) < 0.01m)
        {
            TxtWeightStatus.Text = $"当前总权重: {total:F0}% (有效)";
            TxtWeightStatus.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#A6E3A1")!;
        }
        else
        {
            TxtWeightStatus.Text = $"当前总权重: {total:F0}% (需等于100%)";
            TxtWeightStatus.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F9E2AF")!;
        }
    }

    private async void CmbTimeRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && _components.Count > 0)
        {
            await ExecutePortfolioBacktestAsync();
        }
    }

    private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null || _lastComponents == null || _lastResult.PortfolioNavHistory.Count == 0)
        {
            MessageBox.Show("请先执行组合回测后再导出研报。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV 文件 (*.csv)|*.csv",
            FileName = $"BIGA_Portfolio_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                ExportService.ExportPortfolioToCsv(sfd.FileName, _components, _lastResult);
                MessageBox.Show($"组合量化投研研报已成功导出至：\n{sfd.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出研报失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnRunPortfolio_Click(object sender, RoutedEventArgs e)
    {
        await ExecutePortfolioBacktestAsync();
    }

    private async Task ExecutePortfolioBacktestAsync()
    {
        if (_components.Count == 0)
        {
            MessageBox.Show("请先至少添加一只基金到组合中。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 确保所有成分基金都已拉取详情
        foreach (var item in _components)
        {
            if (!_loadedDetails.ContainsKey(item.Code))
            {
                var detail = await _dataService.GetFundDetailAsync(item.Code);
                if (detail != null)
                {
                    _loadedDetails[item.Code] = detail;
                }
            }
        }

        var list = new List<(FundDetail Fund, decimal WeightPercent)>();
        foreach (var item in _components)
        {
            if (_loadedDetails.TryGetValue(item.Code, out var d))
            {
                list.Add((d, item.WeightPercent));
            }
        }

        string timeRange = (CmbTimeRange?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ALL";
        var result = PortfolioEngine.CalculatePortfolio(list, riskFreeRate: 2.0m, timeRange: timeRange);
        if (result.PortfolioNavHistory.Count < 2)
        {
            MessageBox.Show("所选时间区间内无共同历史交集交易日，无法合成组合走势。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _lastResult = result;
        _lastComponents = list;

        // 更新量化指标看板
        TxtDateRange.Text = $"统计区间: {result.StartDate:yyyy-MM-dd} 至 {result.EndDate:yyyy-MM-dd} ({result.TradingDays} 个交易日)";
        TxtTotalReturn.Text = $"{result.TotalReturn:+0.00;-0.00}%";
        TxtBenchmarkReturn.Text = $"同期沪深300: {result.BenchmarkReturn:+0.00;-0.00}%";
        TxtCagr.Text = $"{result.AnnualizedReturn:+0.00;-0.00}%";
        TxtVolatility.Text = $"{result.AnnualizedVolatility:F2}%";
        TxtMaxDrawdown.Text = $"-{result.MaxDrawdown:F2}%";
        TxtSharpe.Text = $"{result.SharpeRatio:F2}";
        TxtDivBenefit.Text = $"-{result.DiversificationBenefit:F2}% 降波";

        // 渲染走势图
        RenderPortfolioPlot(result, list);
    }

    private void RenderPortfolioPlot(PortfolioResult result, List<(FundDetail Fund, decimal WeightPercent)> components)
    {
        var plot = PortfolioPlot.Plot;
        plot.Clear();
        SetupPlotStyle();

        var navs = result.PortfolioNavHistory;
        double[] xs = navs.Select(n => n.Date.ToOADate()).ToArray();
        double[] ys = navs.Select(n => (double)(n.UnitNav - 1.0m) * 100.0).ToArray();

        // 1. 组合收益曲线 (金黄色高亮)
        var portScatter = plot.Add.Scatter(xs, ys);
        portScatter.LegendText = "🏆 资产配置组合走势 (%)";
        portScatter.Color = ScottPlot.Color.FromHex("#F5B041");
        portScatter.LineWidth = 2.8f;
        portScatter.MarkerSize = 0;

        // 2. 沪深300基准曲线 (天蓝色)
        if (components.Count > 0 && components[0].Fund.BenchmarkCsi300.Count > 0)
        {
            var bms = components[0].Fund.BenchmarkCsi300
                .Where(b => b.Date >= result.StartDate && b.Date <= result.EndDate)
                .OrderBy(b => b.Date)
                .ToList();

            if (bms.Count >= 2)
            {
                double bmBase = (double)bms[0].CumulativeReturnRate;
                double[] bmXs = bms.Select(b => b.Date.ToOADate()).ToArray();
                double[] bmYs = bms.Select(b => (double)b.CumulativeReturnRate - bmBase).ToArray();

                var bmScatter = plot.Add.Scatter(bmXs, bmYs);
                bmScatter.LegendText = "沪深300基准走势 (%)";
                bmScatter.Color = ScottPlot.Color.FromHex("#89B4FA");
                bmScatter.LineWidth = 1.6f;
                bmScatter.MarkerSize = 0;
            }
        }

        // 3. 各成分单基走势 (细线淡化展示)
        string[] colors = new[] { "#A6E3A1", "#F38BA8", "#CBA6F7", "#94E2D5", "#FAB387" };
        int colorIdx = 0;

        foreach (var comp in components)
        {
            var fNavs = comp.Fund.NavHistory
                .Where(n => n.Date >= result.StartDate && n.Date <= result.EndDate)
                .OrderBy(n => n.Date)
                .ToList();

            if (fNavs.Count >= 2)
            {
                decimal baseNav = fNavs[0].UnitNav > 0 ? fNavs[0].UnitNav : 1m;
                double[] fXs = fNavs.Select(n => n.Date.ToOADate()).ToArray();
                double[] fYs = fNavs.Select(n => (double)((n.UnitNav - baseNav) / baseNav * 100m)).ToArray();

                var fScatter = plot.Add.Scatter(fXs, fYs);
                fScatter.LegendText = $"{comp.Fund.Name} ({comp.WeightPercent:0.#}%)";
                fScatter.Color = ScottPlot.Color.FromHex(colors[colorIdx % colors.Length]);
                fScatter.LineWidth = 1.0f;
                fScatter.LinePattern = ScottPlot.LinePattern.Dotted;
                fScatter.MarkerSize = 0;
                colorIdx++;
            }
        }

        // 零轴基准虚线
        var zeroLine = plot.Add.HorizontalLine(0);
        zeroLine.Color = ScottPlot.Color.FromHex("#585B70");
        zeroLine.LineWidth = 1;
        zeroLine.LinePattern = ScottPlot.LinePattern.Dashed;

        // 十字准星
        _crosshair = plot.Add.Crosshair(0, 0);
        _crosshair.IsVisible = false;
        _crosshair.LineColor = ScottPlot.Color.FromHex("#F5B041").WithAlpha(0.65f);
        _crosshair.LineWidth = 1.2f;
        _crosshair.LinePattern = ScottPlot.LinePattern.Dashed;

        plot.ShowLegend(ScottPlot.Alignment.UpperLeft);
        plot.Axes.AutoScale();
        PortfolioPlot.Refresh();
    }

    private void PortfolioPlot_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_crosshair == null || _lastResult == null || _lastResult.PortfolioNavHistory.Count == 0) return;

        try
        {
            var position = e.GetPosition(PortfolioPlot);
            var pixel = new ScottPlot.Pixel((float)position.X, (float)position.Y);
            var coords = PortfolioPlot.Plot.GetCoordinates(pixel);

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

            var nav = FindClosestNav(_lastResult.PortfolioNavHistory, hoverDate);
            if (nav != null)
            {
                decimal ret = (nav.UnitNav - 1.0m) * 100m;
                TxtPortfolioHud.Text = $"📅 {nav.Date:yyyy-MM-dd} | 🏆 组合累计收益率: {ret:+0.00;-0.00;0.00}% | 组合归一化净值: {nav.UnitNav:F4}";
            }

            PortfolioPlot.Refresh();
        }
        catch
        {
            // 忽略边界计算
        }
    }

    private void PortfolioPlot_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_crosshair != null)
        {
            _crosshair.IsVisible = false;
            PortfolioPlot.Refresh();
        }
        TxtPortfolioHud.Text = "💡 移动鼠标至走势图区域可交互查看精准组合点位";
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
}
