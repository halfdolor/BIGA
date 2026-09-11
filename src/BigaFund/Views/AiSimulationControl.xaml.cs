using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BigaFund.Models;
using BigaFund.Services;
using ScottPlot;

namespace BigaFund.Views;

public partial class AiSimulationControl : UserControl
{
    private FundDataService? _dataService;
    private AiSimulationService? _simService;
    private SimulatedAccount? _currentAccount;
    private AiAllocationProposal? _currentProposal;
    private string _currentRange = "ALL";
    private string _currentSellFundCode = string.Empty;
    private decimal _currentSellMaxShares = 0m;

    public event Action<string>? OnSelectFundForMain;

    public AiSimulationControl()
    {
        InitializeComponent();
    }

    public void Initialize(FundDataService dataService)
    {
        _dataService = dataService;
        _simService = new AiSimulationService(dataService);

        PlotStyleHelper.ApplyDarkThemeAndFont(PerformancePlot.Plot);

        _ = LoadAccountDataAsync(true);
        _ = LoadCandidatesAsync();
    }

    /// <summary>
    /// 供外部直接调用买入指定基金 (例如从单基量化看板点击【模拟买入】)
    /// </summary>
    public void OpenQuickBuyForFund(string fundCode)
    {
        TxtBuyFundCode.Text = fundCode;
        ModalManualBuy.Visibility = Visibility.Visible;
    }

    private async Task LoadAccountDataAsync(bool refreshNavs = true)
    {
        if (_simService == null) return;

        try
        {
            _currentAccount = await _simService.GetAccountAsync(refreshNavs);
            UpdateAccountUi(_currentAccount);
            RenderPlot(_currentRange);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载模拟操盘数据失败: {ex.Message}", "错误提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task LoadCandidatesAsync()
    {
        if (_simService == null) return;
        try
        {
            var proposal = await _simService.GenerateAiAllocationProposalAsync(100000m, "BALANCED");
            ItemsCandidates.ItemsSource = proposal.Items;
        }
        catch { }
    }

    private void UpdateAccountUi(SimulatedAccount account)
    {
        // 1. 顶栏 6 联卡片数据绑定
        TxtTotalAsset.Text = $"¥{account.TotalAsset:N2}";
        TxtCash.Text = $"¥{account.Cash:N2}";

        string profitSign = account.TotalProfit >= 0 ? "+" : "";
        TxtTotalProfit.Text = $"{profitSign}¥{account.TotalProfit:N2}";
        TxtTotalProfitRate.Text = $"{profitSign}{account.TotalProfitRate:F2}%";
        TxtTotalProfitRate.Foreground = account.TotalProfit >= 0 
            ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A6E3A1"))
            : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F38BA8"));

        string todaySign = account.TodayProfit >= 0 ? "+" : "";
        TxtTodayProfit.Text = $"{todaySign}¥{account.TodayProfit:N2}";
        TxtTodayProfitRate.Text = $"{todaySign}{account.TodayProfitRate:F2}%";

        TxtPositionRatio.Text = $"{account.PositionRatio:F1}%";
        TxtMarketValue.Text = $"¥{account.TotalMarketValue:N2}";

        TxtAnnualizedAndSharpe.Text = $"{account.AnnualizedReturn:F1}% / {account.SharpeRatio:F2}";
        TxtMaxDrawdown.Text = $"{account.MaxDrawdown:F1}%";

        TxtWinRate.Text = $"{account.WinRate:F1}%";
        TxtTradeCount.Text = $"{account.ClosedTradeCount} / {account.ProfitableTradeCount} 笔";

        // 2. 表格数据刷新
        GridPositions.ItemsSource = null;
        GridPositions.ItemsSource = account.Positions;

        GridTrades.ItemsSource = null;
        GridTrades.ItemsSource = account.Trades;
    }

    /// <summary>
    /// 绘制收益率走势图 (与沪深 300 基准对标)
    /// </summary>
    private void RenderPlot(string range)
    {
        if (_currentAccount == null) return;

        var plot = PerformancePlot.Plot;
        plot.Clear();
        PlotStyleHelper.ApplyDarkThemeAndFont(plot);

        var snapshots = _currentAccount.DailySnapshots;
        if (snapshots == null || snapshots.Count == 0)
        {
            plot.Axes.SetLimits(0, 10, -5, 5);
            PerformancePlot.Refresh();
            TxtPlotSummary.Text = "💡 当前模拟账户尚无持仓时序数据，点击顶栏【AI 智能分析建仓】或【手动自由买入】开始模拟操盘。";
            return;
        }

        // 根据所选时间范围过滤
        DateTime now = DateTime.Now.Date;
        DateTime cutoff = range switch
        {
            "1W" => now.AddDays(-7),
            "1M" => now.AddMonths(-1),
            "3M" => now.AddMonths(-3),
            "6M" => now.AddMonths(-6),
            _ => DateTime.MinValue
        };

        var filtered = snapshots.Where(s => s.Date >= cutoff).OrderBy(s => s.Date).ToList();
        if (filtered.Count == 0) filtered = snapshots;

        // 基准化对齐（选定区间起点的收益率为 0%）
        decimal startPortVal = filtered[0].TotalAsset;
        decimal startBmVal = filtered[0].BenchmarkReturn;

        double[] xs = new double[filtered.Count];
        double[] portReturns = new double[filtered.Count];
        double[] bmReturns = new double[filtered.Count];

        for (int i = 0; i < filtered.Count; i++)
        {
            xs[i] = filtered[i].Date.ToOADate();
            if (range == "ALL")
            {
                portReturns[i] = (double)filtered[i].CumulativeReturn;
                bmReturns[i] = (double)filtered[i].BenchmarkReturn;
            }
            else
            {
                // 区间相对收益
                portReturns[i] = startPortVal > 0 ? (double)((filtered[i].TotalAsset - startPortVal) / startPortVal * 100m) : 0;
                bmReturns[i] = (double)(filtered[i].BenchmarkReturn - startBmVal);
            }
        }

        // 1. 模拟组合收益曲线 (金黄色实线)
        var portLine = plot.Add.ScatterLine(xs, portReturns);
        portLine.Color = ScottPlot.Color.FromHex("#F5B041");
        portLine.LineWidth = 2.5f;
        portLine.LegendText = "模拟总资产收益率 (%)";

        // 2. 沪深 300 基准指数曲线 (浅蓝色实线)
        var bmLine = plot.Add.ScatterLine(xs, bmReturns);
        bmLine.Color = ScottPlot.Color.FromHex("#89B4FA");
        bmLine.LineWidth = 1.5f;
        bmLine.LegendText = "沪深300同期基准 (%)";

        // 3. 坐标轴与零轴样式
        var zeroLine = plot.Add.HorizontalLine(0);
        zeroLine.Color = ScottPlot.Color.FromHex("#45475A");
        zeroLine.LineWidth = 1f;

        plot.Axes.DateTimeTicksBottom();
        plot.Axes.AutoScale();
        plot.ShowLegend(Alignment.UpperLeft);

        PerformancePlot.Refresh();

        // 更新图表下方总结文字
        decimal lastPortRet = (decimal)portReturns[^1];
        decimal lastBmRet = (decimal)bmReturns[^1];
        decimal alpha = lastPortRet - lastBmRet;
        string sign = alpha >= 0 ? "+" : "";
        TxtPlotSummary.Text = $"📊 区间收益总结：模拟组合收益率 {lastPortRet:F2}% | 沪深300基准 {lastBmRet:F2}% | 相对超额 Alpha: {sign}{alpha:F2}% | 历史最大回撤: {_currentAccount.MaxDrawdown:F1}% | 夏普比率: {_currentAccount.SharpeRatio:F2}";
    }

    #region 顶部工具栏按钮事件

    private async void BtnAiAutoBuild_Click(object sender, RoutedEventArgs e)
    {
        if (_simService == null) return;
        ModalAiProposal.Visibility = Visibility.Visible;
        await LoadProposalForSelectedStrategy();
    }

    private async Task LoadProposalForSelectedStrategy()
    {
        if (_simService == null) return;
        string strat = "BALANCED";
        if (RbStrategyGrowth.IsChecked == true) strat = "GROWTH";
        else if (RbStrategyDefensive.IsChecked == true) strat = "DEFENSIVE";

        decimal capital = _currentAccount?.Cash ?? 100000m;
        if (capital < 1000m) capital = 100000m;

        _currentProposal = await _simService.GenerateAiAllocationProposalAsync(capital, strat);
        TxtProposalDesc.Text = $"{_currentProposal.Title}: {_currentProposal.StrategyDescription} (总分配金: ¥{_currentProposal.AllocatedAmount:N0}, 预留现金: ¥{_currentProposal.ReservedCash:N0})";
        GridProposalItems.ItemsSource = _currentProposal.Items;
    }

    private async void RbStrategy_Click(object sender, RoutedEventArgs e)
    {
        await LoadProposalForSelectedStrategy();
    }

    private void BtnCloseProposal_Click(object sender, RoutedEventArgs e)
    {
        ModalAiProposal.Visibility = Visibility.Collapsed;
    }

    private async void BtnConfirmAiBuild_Click(object sender, RoutedEventArgs e)
    {
        if (_simService == null || _currentProposal == null) return;

        var (success, msg) = await _simService.ExecuteAiBuildProposalAsync(_currentProposal);
        ModalAiProposal.Visibility = Visibility.Collapsed;
        MessageBox.Show(msg, success ? "AI 建仓成功" : "建仓提示", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);

        await LoadAccountDataAsync(true);
    }

    private void BtnManualBuy_Click(object sender, RoutedEventArgs e)
    {
        TxtBuyFundCode.Text = "510300";
        TxtBuyAmount.Text = "10000";
        ModalManualBuy.Visibility = Visibility.Visible;
    }

    private async void BtnAiDiagnose_Click(object sender, RoutedEventArgs e)
    {
        if (_simService == null) return;
        var diagnoses = await _simService.DiagnoseHoldingsAsync();
        if (diagnoses.Count == 0)
        {
            MessageBox.Show("当前模拟账户暂无持仓，无需盯盘调仓。", "AI 盯盘提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string report = string.Join("\n\n", diagnoses.Select(d => $"【{d.FundName} ({d.FundCode})】\n动作建议: {d.ActionProposal}\n量化依据: {d.Rationale}"));
        MessageBox.Show(report, "🤖 AI 实时持仓诊断与调仓建议", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnRefreshNavs_Click(object sender, RoutedEventArgs e)
    {
        await LoadAccountDataAsync(true);
        MessageBox.Show("已成功更新全部持仓最新净值与时序资产收益！", "刷新完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnResetAccount_Click(object sender, RoutedEventArgs e)
    {
        var res = MessageBox.Show("确定要重置模拟操盘账户吗？\n重置后将清除全部模拟交易流水与持仓，并将现金恢复为 100,000 元初始金。", "确认重置模拟账户", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes && _simService != null)
        {
            await _simService.ResetAccountAsync(100000m);
            await LoadAccountDataAsync(false);
            MessageBox.Show("模拟操盘账户已重置为 10 万元现金初始状态！", "重置成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    #endregion

    #region 手动买入交互

    private async void TxtBuyFundCode_TextChanged(object sender, TextChangedEventArgs e)
    {
        string code = TxtBuyFundCode.Text.Trim();
        if (code.Length == 6 && _dataService != null)
        {
            try
            {
                var detail = await _dataService.GetFundDetailAsync(code, false);
                if (detail != null)
                {
                    decimal nav = detail.NavHistory.Count > 0 ? detail.NavHistory[^1].UnitNav : 1.0m;
                    TxtBuyFundNamePreview.Text = $"{detail.Name} (最新净值 {nav:F4})";
                    return;
                }
            }
            catch { }
        }
        TxtBuyFundNamePreview.Text = "输入6位基金代码自动匹配";
    }

    private void BtnQuickBuyAmount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag != null)
        {
            TxtBuyAmount.Text = btn.Tag.ToString();
        }
    }

    private void BtnQuickBuyRatio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag != null && decimal.TryParse(btn.Tag.ToString(), out decimal ratio) && _currentAccount != null)
        {
            decimal amt = Math.Round(_currentAccount.Cash * ratio, 2);
            TxtBuyAmount.Text = amt.ToString("F0");
        }
    }

    private void BtnCloseBuyModal_Click(object sender, RoutedEventArgs e)
    {
        ModalManualBuy.Visibility = Visibility.Collapsed;
    }

    private async void BtnConfirmManualBuy_Click(object sender, RoutedEventArgs e)
    {
        if (_simService == null) return;
        string code = TxtBuyFundCode.Text.Trim();
        if (!decimal.TryParse(TxtBuyAmount.Text.Trim(), out decimal amount) || amount <= 0)
        {
            MessageBox.Show("请输入有效的买入金额。", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string reason = string.IsNullOrWhiteSpace(TxtBuyReason.Text) ? "🛒 手动择时买入" : TxtBuyReason.Text.Trim();
        var (success, msg) = await _simService.ExecuteBuyAsync(code, amount, reason, isAi: false);
        ModalManualBuy.Visibility = Visibility.Collapsed;
        MessageBox.Show(msg, success ? "买入成功" : "买入失败", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);

        if (success)
        {
            await LoadAccountDataAsync(true);
        }
    }

    #endregion

    #region 手动卖出/赎回交互

    private void BtnPositionSell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string code && _currentAccount != null)
        {
            var pos = _currentAccount.Positions.FirstOrDefault(p => p.FundCode == code);
            if (pos != null)
            {
                _currentSellFundCode = pos.FundCode;
                _currentSellMaxShares = pos.Shares;
                TxtSellFundInfo.Text = $"标的: {pos.FundName} ({pos.FundCode}) | 持有: {pos.Shares:N2} 份 | 均价: {pos.CostBasis:F4} | 现价: {pos.LatestNav:F4}";
                TxtSellShares.Text = pos.Shares.ToString("F2");
                ModalManualSell.Visibility = Visibility.Visible;
            }
        }
    }

    private void BtnQuickSellRatio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag != null && decimal.TryParse(btn.Tag.ToString(), out decimal ratio))
        {
            decimal shares = Math.Round(_currentSellMaxShares * ratio, 2);
            TxtSellShares.Text = shares.ToString("F2");
        }
    }

    private void BtnCloseSellModal_Click(object sender, RoutedEventArgs e)
    {
        ModalManualSell.Visibility = Visibility.Collapsed;
    }

    private async void BtnConfirmManualSell_Click(object sender, RoutedEventArgs e)
    {
        if (_simService == null) return;
        if (!decimal.TryParse(TxtSellShares.Text.Trim(), out decimal shares) || shares <= 0)
        {
            MessageBox.Show("请输入有效的卖出份额。", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string reason = string.IsNullOrWhiteSpace(TxtSellReason.Text) ? "💰 手动赎回" : TxtSellReason.Text.Trim();
        var (success, msg) = await _simService.ExecuteSellAsync(_currentSellFundCode, shares, reason, isAi: false);
        ModalManualSell.Visibility = Visibility.Collapsed;
        MessageBox.Show(msg, success ? "卖出成功" : "卖出失败", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);

        if (success)
        {
            await LoadAccountDataAsync(true);
        }
    }

    #endregion

    #region 列表快速操作与导航联动

    private void BtnPositionBuy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string code)
        {
            OpenQuickBuyForFund(code);
        }
    }

    private void BtnPositionDetail_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string code)
        {
            OnSelectFundForMain?.Invoke(code);
        }
    }

    private void BtnCandidateBuy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string code)
        {
            OpenQuickBuyForFund(code);
        }
    }

    private void RbRange_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb)
        {
            if (rb == RbRange1W) _currentRange = "1W";
            else if (rb == RbRange1M) _currentRange = "1M";
            else if (rb == RbRange3M) _currentRange = "3M";
            else if (rb == RbRange6M) _currentRange = "6M";
            else _currentRange = "ALL";

            RenderPlot(_currentRange);
        }
    }

    #endregion
}
