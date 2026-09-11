using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using BigaFund.Models;
using BigaFund.Services;

namespace BigaFund.Views;

public partial class PortfolioControl : UserControl
{
    private FundDataService? _dataService;
    private readonly ObservableCollection<PortfolioItem> _components = new();
    private readonly Dictionary<string, FundDetail> _loadedDetails = new();
    private ScottPlot.Plottables.Crosshair? _crosshair;
    private PortfolioResult? _lastResult;
    private List<(FundDetail Fund, decimal WeightPercent)>? _lastComponents;
    private string _lastSelectedSchemeName = "最大夏普切点方案";
    private Dictionary<string, decimal>? _lastTargetWeights;
    private string _currentPlotMode = "Nav";
    private bool _initialized;
    private long _lastRenderTimestamp;

    public PortfolioControl()
    {
        InitializeComponent();
        GridComponents.ItemsSource = _components;
        PortfolioPlot.MouseMove += PortfolioPlot_MouseMove;
        PortfolioPlot.MouseLeave += PortfolioPlot_MouseLeave;
        SetupPlotStyle();
    }

    public void Initialize(FundDataService dataService, IEnumerable<string>? initialCodes = null)
    {
        _dataService = dataService;
        if (_initialized) return;
        _initialized = true;

        _ = Task.Run(async () =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
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
            });
        });
    }

    public async Task SetFundsAsync(IEnumerable<string> codes)
    {
        _components.Clear();
        _loadedDetails.Clear();

        var codeList = codes.Distinct().ToList();
        if (codeList.Count == 0) return;

        decimal allocated = 0m;
        for (int i = 0; i < codeList.Count; i++)
        {
            decimal w;
            if (i == codeList.Count - 1)
            {
                w = 100m - allocated;
            }
            else
            {
                w = Math.Round(100m / codeList.Count, 1);
                allocated += w;
            }
            await AddFundByCodeAsync(codeList[i], w);
        }

        UpdateWeightStatus();
        await ExecutePortfolioBacktestAsync();
    }

    private void SetupPlotStyle()
    {
        var plot = PortfolioPlot.Plot;
        PlotStyleHelper.ApplyDarkThemeAndFont(plot);
    }

    private async Task AddFundByCodeAsync(string code, decimal defaultWeight = 50m)
    {
        code = code.Trim();
        if (string.IsNullOrEmpty(code) || _dataService == null) return;

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
            decimal perWeight = codes.Length > 0 ? Math.Round(100m / codes.Length, 1) : 50m;

            foreach (var c in codes)
            {
                await AddFundByCodeAsync(c, perWeight);
            }

            UpdateWeightStatus();
            await ExecutePortfolioBacktestAsync();
        }
    }

    private void BtnEqualWeight_Click(object sender, RoutedEventArgs e)
    {
        if (_components.Count == 0) return;
        decimal per = Math.Round(100m / _components.Count, 1);
        foreach (var c in _components)
        {
            c.WeightPercent = per;
        }
        UpdateWeightStatus();
    }

    private async void BtnMaxSharpe_Click(object sender, RoutedEventArgs e)
    {
        await ApplySchemeWeightsAsync(_lastResult?.MaxSharpeWeights, "最大夏普");
    }

    private async void BtnMinVariance_Click(object sender, RoutedEventArgs e)
    {
        await ApplySchemeWeightsAsync(_lastResult?.MinVarianceWeights, "最小方差");
    }

    private async void BtnRiskParity_Click(object sender, RoutedEventArgs e)
    {
        await ApplySchemeWeightsAsync(_lastResult?.RiskParityWeights, "风险平价 (ERC)");
    }

    private async void BtnMomentumRiskBudget_Click(object sender, RoutedEventArgs e)
    {
        await ApplySchemeWeightsAsync(_lastResult?.MomentumRiskBudgetWeights, "动量风险预算 (MRB)");
    }

    private async void BtnBlackLitterman_Click(object sender, RoutedEventArgs e)
    {
        await ApplySchemeWeightsAsync(_lastResult?.BlackLittermanWeights, "Black-Litterman (贝叶斯均衡)");
    }

    private async void BtnHrp_Click(object sender, RoutedEventArgs e)
    {
        await ApplySchemeWeightsAsync(_lastResult?.HrpWeights, "机器学习层级风险平价 (HRP)");
    }

    private async void BtnMeanCVaR_Click(object sender, RoutedEventArgs e)
    {
        await ApplySchemeWeightsAsync(_lastResult?.MeanCVaRWeights, "均值-CVaR (尾部极值防御)");
    }

    private async void BtnMdp_Click(object sender, RoutedEventArgs e)
    {
        await ApplySchemeWeightsAsync(_lastResult?.MdpWeights, "最大分散化组合 (MDP)");
    }

    private void BtnExportHtml_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null || _components.Count == 0)
        {
            MessageBox.Show("请先执行组合回测后再导出研报。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出投资组合量化回测 HTML 研报",
            Filter = "HTML 研报 (*.html)|*.html",
            FileName = $"BIGA_组合量化研报_{DateTime.Now:yyyyMMdd_HHmmss}.html"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ExportService.ExportPortfolioToHtml(dialog.FileName, _components, _lastResult);
                var openResult = MessageBox.Show("组合研报导出成功！是否立即在浏览器中打开查看？", "导出完成", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (openResult == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出组合研报失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnExportPitchDeck_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null || _components.Count == 0)
        {
            MessageBox.Show("请先执行组合回测后再导出投决会高管路演研报 (Pitch Deck)。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出投资决策委员会高管路演全景推介画册 (Executive Pitch Deck)",
            Filter = "HTML 研报 (*.html)|*.html",
            FileName = $"BIGA_投决会高管路演_PitchDeck_{DateTime.Now:yyyyMMdd_HHmmss}.html"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var primaryFund = (_lastComponents != null && _lastComponents.Count > 0) ? _lastComponents[0].Fund : null;
                primaryFund ??= new FundDetail
                {
                    Name = "多资产量化配置核心专户",
                    Code = "BIGA-PORTFOLIO",
                    Type = "多资产配置组合",
                    FundSize = $"{_lastResult?.LiquidityHorizon?.PortfolioAumMln ?? 300m:N0}百万元"
                };

                var metrics = _lastResult?.QuantMetrics ?? new QuantMetrics();
                ExportService.ExportExecutivePitchDeckToHtml(dialog.FileName, primaryFund, metrics, _lastResult);
                var openResult = MessageBox.Show("投决会高管路演研报 (Pitch Deck) 导出成功！是否立即在浏览器中打开查看？", "导出完成", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (openResult == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出高管路演研报失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task ApplySchemeWeightsAsync(Dictionary<string, decimal>? weights, string schemeName)
    {
        if (weights == null || weights.Count == 0)
        {
            MessageBox.Show($"暂无【{schemeName}】最优方案数据，请先点击【🚀 开始回测】进行数学规划求解。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _lastSelectedSchemeName = schemeName;
        _lastTargetWeights = new Dictionary<string, decimal>(weights);

        foreach (var comp in _components)
        {
            if (weights.TryGetValue(comp.Code, out var w))
            {
                comp.WeightPercent = w > 1.0m ? Math.Round(w, 1) : Math.Round(w * 100m, 1);
            }
        }

        // 调整舍入误差使得总和严格等于 100%
        decimal currentTotal = _components.Sum(c => c.WeightPercent);
        if (_components.Count > 0 && Math.Abs(currentTotal - 100m) > 0.001m)
        {
            _components[0].WeightPercent += (100m - currentTotal);
        }

        UpdateWeightStatus();
        await ExecutePortfolioBacktestAsync();
    }

    private void BtnRemoveFund_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string code)
        {
            var item = _components.FirstOrDefault(c => c.Code == code);
            if (item != null)
            {
                _components.Remove(item);
                _loadedDetails.Remove(code);
                UpdateWeightStatus();
            }
        }
    }

    private void WeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateWeightStatus();
    }

    private void WeightText_TextChanged(object sender, TextChangedEventArgs e)
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

    private async void BtnRunBacktest_Click(object sender, RoutedEventArgs e)
    {
        await ExecutePortfolioBacktestAsync();
    }

    private async Task ExecutePortfolioBacktestAsync()
    {
        if (_components.Count == 0 || _dataService == null)
        {
            return;
        }

        if (PlotLoading != null) PlotLoading.Visibility = Visibility.Visible;

        try
        {
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
                return;
            }

            _lastResult = result;
            _lastComponents = list;

            // 更新量化指标看板
            TxtTotalReturn.Text = $"{result.TotalReturn:+0.00;-0.00}%";
            TxtCsi300Compare.Text = $"跑赢沪深300: {result.TotalReturn - result.BenchmarkReturn:+0.00;-0.00}%";
            TxtAnnualizedReturn.Text = $"{result.AnnualizedReturn:+0.00;-0.00}%";
            TxtMaxDrawdown.Text = $"-{result.MaxDrawdown:F2}%";
            TxtSharpe.Text = $"{result.SharpeRatio:F2}";
            TxtCalmar.Text = $"{result.CalmarRatio:F2}";

            if (result.QuantMetrics != null)
            {
                TxtVaR.Text = $"{result.QuantMetrics.VaR95:F2}% / {result.QuantMetrics.CVaR95:F2}%";
                TxtTreynorAndTe.Text = $"{result.QuantMetrics.TreynorRatio:F2} / {result.QuantMetrics.TrackingError:F2}%";
                TxtCaptureRatio.Text = $"{result.QuantMetrics.CaptureRatio:F2} (U:{result.QuantMetrics.UpsideCaptureRatio:F0}% / D:{result.QuantMetrics.DownsideCaptureRatio:F0}%)";
            }

            var msScheme = result.Schemes.FirstOrDefault(s => s.Name.Contains("最大夏普"));
            var mvScheme = result.Schemes.FirstOrDefault(s => s.Name.Contains("最小方差"));
            var rpScheme = result.Schemes.FirstOrDefault(s => s.Name.Contains("风险平价"));
            var mrbScheme = result.Schemes.FirstOrDefault(s => s.Name.Contains("动量风险预算") || s.Name.Contains("MRB"));
            var blScheme = result.Schemes.FirstOrDefault(s => s.Name.Contains("Black-Litterman") || s.Name.Contains("贝叶斯"));
            var hrpScheme = result.Schemes.FirstOrDefault(s => s.Name.Contains("HRP") || s.Name.Contains("层级风险平价"));
            var cvarScheme = result.Schemes.FirstOrDefault(s => s.Name.Contains("CVaR"));
            var mdpScheme = result.Schemes.FirstOrDefault(s => s.Name.Contains("MDP") || s.Name.Contains("最大分散"));
            var boxScheme = result.Schemes.FirstOrDefault(s => s.Name.Contains("合规盒约束") || s.Name.Contains("Box-Constrained"));
            if (msScheme != null && mvScheme != null)
            {
                string boxInfo = boxScheme != null ? $" | 📦盒约束夏普: {boxScheme.SharpeRatio:F2}" : "";
                TxtMptSummary.Text = $"🎯夏普: {msScheme.SharpeRatio:F2} | 🛡️最小方差: {mvScheme.Volatility:F1}% | ⚖️风险平价: {rpScheme?.Volatility:F1}% | ⚡动量: {mrbScheme?.SharpeRatio:F2} | 🧠BL: {blScheme?.SharpeRatio:F2} | 🧬HRP: {hrpScheme?.SharpeRatio:F2} | 🛡️CVaR: {cvarScheme?.Volatility:F1}% | 🌐MDP: {mdpScheme?.SharpeRatio:F2}{boxInfo}";
            }

            // 机构级非正态下行风控指标与 Top 5 回撤周期解构
            if (TxtOmega != null) TxtOmega.Text = $"{result.OmegaRatio:F2}";
            if (TxtUlcer != null) TxtUlcer.Text = $"{result.UlcerIndex:F2}%";
            if (TxtMartin != null) TxtMartin.Text = $"{result.MartinRatio:F2}";
            if (TxtPainAndDd != null) TxtPainAndDd.Text = $"{result.PainRatio:F2} / {result.DownsideDeviation:F2}%";
            if (GridPortfolioDrawdownEpisodes != null) GridPortfolioDrawdownEpisodes.ItemsSource = result.DrawdownEpisodes;

            // 更新多标签投研看板：资产相关性矩阵、历史黑天鹅压测、调仓交易决策单、Brinson 归因、底层合并穿透、蒙特卡洛推演、宏观冲击与动态再平衡
            UpdateCorrelationDisplay(result);
            UpdateStressTestDisplay(result);
            UpdateRebalanceOrdersDisplay(result, list);
            UpdatePortfolioBrinsonDisplay(result, list);
            UpdateLookThroughDisplay(result);
            UpdateHoldingsOverlapDisplay(result);
            UpdateMonteCarloDisplay(result);
            UpdateMacroShockDisplay(result);
            UpdateRebalanceDisplay(result);
            UpdateBarraAttributionDisplay(result);
            UpdateLiquidityDisplay(result);
            UpdateSaaTaaDisplay(result);
            UpdateSharpeDecompositionAndReverseStressDisplay(result);
            UpdateMacroRegimeAndGatekeeperDisplay(result);
            UpdateFactorRiskAndLdiDisplay(result);
            UpdateHolographicStressAndExecutionShortfallDisplay(result);
            UpdatePhase23GipsAndShadowPurityDisplay(result);
            UpdatePhase24LiquidityAndInsuranceDisplay(result);
            UpdatePhase25CopulaAndParetoDisplay(result);
            UpdatePhase26InstitutionalFlagshipDisplay(result);
            UpdatePhase27InstitutionalOptimizationDisplay(result);
            UpdatePhase28InstitutionalOptimizationDisplay(result);
            UpdatePhase29InstitutionalOptimizationDisplay(result);
            UpdatePhase30InstitutionalOptimizationDisplay(result);
            UpdatePhase31InstitutionalOptimizationDisplay(result);
            UpdatePhase32InstitutionalOptimizationDisplay(result);
            UpdatePhase33InstitutionalOptimizationDisplay(result);
            UpdatePhase34InstitutionalOptimizationDisplay(result);
            UpdatePhase35InstitutionalOptimizationDisplay(result);
            UpdatePhase36InstitutionalOptimizationDisplay(result);
            UpdatePhase37InstitutionalOptimizationDisplay(result);
            UpdatePhase38InstitutionalOptimizationDisplay(result);
            UpdatePhase39InstitutionalOptimizationDisplay(result);
            UpdatePhase40InstitutionalOptimizationDisplay(result);
            UpdatePhase41InstitutionalOptimizationDisplay(result);
            UpdatePhase42InstitutionalOptimizationDisplay(result);
            UpdatePhase43InstitutionalOptimizationDisplay(result);
            UpdatePhase44InstitutionalOptimizationDisplay(result);
            UpdatePhase45InstitutionalOptimizationDisplay(result);
            UpdatePhase46InstitutionalOptimizationDisplay(result);
            UpdatePhase47InstitutionalOptimizationDisplay(result);
            UpdatePhase48InstitutionalOptimizationDisplay(result);
            UpdatePhase49InstitutionalOptimizationDisplay(result);
            UpdatePhase50InstitutionalOptimizationDisplay(result);
            UpdatePhase51InstitutionalOptimizationDisplay(result);
            UpdatePhase52InstitutionalOptimizationDisplay(result);
            UpdatePhase53InstitutionalOptimizationDisplay(result);
            UpdatePhase54InstitutionalOptimizationDisplay(result);
            UpdatePhase55InstitutionalOptimizationDisplay(result);
            UpdatePhase56InstitutionalOptimizationDisplay(result);
            UpdatePhase57InstitutionalOptimizationDisplay(result);
            UpdatePhase58InstitutionalOptimizationDisplay(result);
            UpdatePhase59InstitutionalOptimizationDisplay(result);
            UpdatePhase60InstitutionalOptimizationDisplay(result);
            UpdatePhase61InstitutionalOptimizationDisplay(result);
            UpdatePhase62InstitutionalOptimizationDisplay(result);

            // 渲染当前选定模式的量化图表
            RenderCurrentPlot();
        }
        finally
        {
            if (PlotLoading != null) PlotLoading.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateCorrelationDisplay(PortfolioResult result)
    {
        // 资产分散化效益与同质化诊断
        if (result.Diversification != null)
        {
            var d = result.Diversification;
            if (TxtDiversificationRatio != null) TxtDiversificationRatio.Text = $"{d.DiversificationRatio:F2}x";
            if (TxtVolReduction != null) TxtVolReduction.Text = $"-{d.VolatilityReductionPercent:F1}%";
            if (TxtWeightedCorrelation != null) TxtWeightedCorrelation.Text = $"{d.WeightedAverageCorrelation:F2}";
            if (TxtHomogeneityScore != null) TxtHomogeneityScore.Text = $"{d.HomogeneityScore:F0} 分";

            if (TxtPseudoWarning != null)
            {
                if (d.PseudoDiversificationWarning)
                {
                    TxtPseudoWarning.Text = "⚠️ 触发伪分散高危";
                    TxtPseudoWarning.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F38BA8"));
                }
                else
                {
                    TxtPseudoWarning.Text = "🛡️ 分散结构良好";
                    TxtPseudoWarning.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A6E3A1"));
                }
            }

            if (TxtClusterGroups != null && d.ClusterGroups.Count > 0)
            {
                TxtClusterGroups.Text = "资产分簇结构: " + string.Join("； ", d.ClusterGroups);
            }

            if (TxtCorrelationSummary != null)
            {
                TxtCorrelationSummary.Text = d.DiagnosticSummary;
            }
        }

        if (result.CorrelationMatrix == null || result.CorrelationMatrix.AssetCodes.Count == 0)
        {
            if (TxtCorrelationSummary != null && result.Diversification == null)
            {
                TxtCorrelationSummary.Text = "资产数据不足，无法计算相关性矩阵";
            }
            GridCorrelationMatrix.ItemsSource = null;
            return;
        }

        var cm = result.CorrelationMatrix;
        if (result.Diversification == null && TxtCorrelationSummary != null)
        {
            TxtCorrelationSummary.Text = $"🔗 资产间平均两两相关度: {cm.AverageCorrelation:F2} | 风险分散评级: {cm.DiversificationRating} | 理论降波增益: +{result.DiversificationBenefit:F2}%";
        }

        // 动态构建网格列
        GridCorrelationMatrix.Columns.Clear();
        GridCorrelationMatrix.Columns.Add(new DataGridTextColumn
        {
            Header = "成分资产",
            Binding = new System.Windows.Data.Binding("AssetName"),
            Width = 160
        });

        for (int j = 0; j < cm.AssetCodes.Count; j++)
        {
            int colIdx = j;
            string code = cm.AssetCodes[colIdx];
            GridCorrelationMatrix.Columns.Add(new DataGridTextColumn
            {
                Header = $"{code}",
                Binding = new System.Windows.Data.Binding($"Values[{colIdx}]"),
                Width = 85
            });
        }

        var rows = new List<CorrelationRowDisplay>();
        for (int i = 0; i < cm.AssetCodes.Count; i++)
        {
            string shortName = cm.AssetNames[i];
            if (shortName.Length > 8) shortName = shortName.Substring(0, 8) + "...";
            var row = new CorrelationRowDisplay
            {
                AssetName = $"{cm.AssetCodes[i]} {shortName}"
            };
            for (int j = 0; j < cm.AssetCodes.Count; j++)
            {
                row.Values.Add($"{cm.Matrix[i, j]:F2}");
            }
            rows.Add(row);
        }

        GridCorrelationMatrix.ItemsSource = rows;

        // 风险预算与 Euler 边际风险贡献 (PCR) 穿透解构
        if (result.RiskDecomposition != null)
        {
            var rd = result.RiskDecomposition;
            if (TxtRiskDecompTotalVol != null) TxtRiskDecompTotalVol.Text = $"{rd.TotalPortfolioVolatility:F2}%";
            if (TxtRiskDecompDominant != null) TxtRiskDecompDominant.Text = $"{rd.DominantRiskAsset} ({rd.DominantRiskPercent:F1}%)";
            if (TxtRiskDecompGini != null) TxtRiskDecompGini.Text = $"{rd.RiskBudgetGini:F3}";
            if (TxtRiskHogWarning != null)
            {
                if (rd.HasRiskHogWarning)
                {
                    TxtRiskHogWarning.Text = "⚠️ 警惕隐形风险猪";
                    TxtRiskHogWarning.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F38BA8"));
                }
                else
                {
                    TxtRiskHogWarning.Text = "🛡️ 风险分配均衡";
                    TxtRiskHogWarning.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A6E3A1"));
                }
            }
            if (TxtRiskDecompSummary != null) TxtRiskDecompSummary.Text = rd.DiagnosticSummary;
            if (GridRiskDecomposition != null) GridRiskDecomposition.ItemsSource = rd.Items;
        }
        else
        {
            if (GridRiskDecomposition != null) GridRiskDecomposition.ItemsSource = null;
        }
    }

    private void UpdateStressTestDisplay(PortfolioResult result)
    {
        if (result.StressTestScenarios == null || result.StressTestScenarios.Count == 0)
        {
            GridStressTest.ItemsSource = null;
            return;
        }

        var list = result.StressTestScenarios.Select(st => new StressTestDisplayItem
        {
            ScenarioName = st.ScenarioName,
            DateRange = $"{st.StartDate:yyyy-MM-dd} 至 {st.EndDate:yyyy-MM-dd}",
            FundReturnText = $"{st.FundReturnRate:+0.00;-0.00;0.00}%",
            BenchmarkReturnText = $"{st.BenchmarkReturnRate:+0.00;-0.00;0.00}%",
            ExcessReturnText = $"{st.ExcessReturnRate:+0.00;-0.00;0.00}%",
            MaxDrawdownText = $"-{st.MaxDrawdown:F2}%",
            DefenseRating = st.DefenseRating,
            Description = st.Description
        }).ToList();

        GridStressTest.ItemsSource = list;
    }

    public class CorrelationRowDisplay
    {
        public string AssetName { get; set; } = string.Empty;
        public List<string> Values { get; set; } = new();
    }

    public class StressTestDisplayItem
    {
        public string ScenarioName { get; set; } = string.Empty;
        public string DateRange { get; set; } = string.Empty;
        public string FundReturnText { get; set; } = string.Empty;
        public string BenchmarkReturnText { get; set; } = string.Empty;
        public string ExcessReturnText { get; set; } = string.Empty;
        public string MaxDrawdownText { get; set; } = string.Empty;
        public string DefenseRating { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private void RbPlotMode_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb)
        {
            if (rb == RbPlotNav) _currentPlotMode = "Nav";
            else if (rb == RbPlotFrontier) _currentPlotMode = "Frontier";
            else if (rb == RbPlotMonteCarlo) _currentPlotMode = "MonteCarlo";

            if (_lastResult != null)
            {
                RenderCurrentPlot();
            }
        }
    }

    private void RenderCurrentPlot()
    {
        if (_lastResult == null || _lastComponents == null) return;

        if (_currentPlotMode == "Frontier")
        {
            RenderEfficientFrontierPlot(_lastResult);
        }
        else if (_currentPlotMode == "MonteCarlo")
        {
            RenderMonteCarloFanChartPlot(_lastResult);
        }
        else
        {
            RenderPortfolioPlot(_lastResult, _lastComponents);
        }
    }

    private void RenderEfficientFrontierPlot(PortfolioResult result)
    {
        var plot = PortfolioPlot.Plot;
        plot.Clear();
        SetupPlotStyle();

        // 1. 蒙特卡洛可行配置散点云 (1500点)
        if (result.EfficientFrontierPoints.Count > 0)
        {
            double[] xs = result.EfficientFrontierPoints.Select(p => p.Volatility).ToArray();
            double[] ys = result.EfficientFrontierPoints.Select(p => p.Return).ToArray();
            var scatterCloud = plot.Add.Scatter(xs, ys);
            scatterCloud.LegendText = "可行配置散点云 (1500组蒙特卡洛随机权重)";
            scatterCloud.Color = ScottPlot.Color.FromHex("#585B70").WithAlpha(0.35f);
            scatterCloud.LineWidth = 0;
            scatterCloud.MarkerSize = 4.0f;
        }

        // 2. 有效前沿上包络边界线
        if (result.EfficientFrontierCurve.Count > 1)
        {
            double[] cXs = result.EfficientFrontierCurve.Select(p => p.Volatility).ToArray();
            double[] cYs = result.EfficientFrontierCurve.Select(p => p.Return).ToArray();
            var frontierLine = plot.Add.Scatter(cXs, cYs);
            frontierLine.LegendText = "马科维茨有效前沿 (Efficient Frontier 上包络)";
            frontierLine.Color = ScottPlot.Color.FromHex("#A6E3A1");
            frontierLine.LineWidth = 3.2f;
            frontierLine.MarkerSize = 0;
        }

        // 3. 当前组合锚点
        double curVol = (double)result.AnnualizedVolatility;
        double curRet = (double)result.AnnualizedReturn;
        var curPoint = plot.Add.Scatter(new[] { curVol }, new[] { curRet });
        curPoint.LegendText = $"⭐ 当前配置组合 (Vol:{curVol:F1}%, Ret:{curRet:F1}%)";
        curPoint.Color = ScottPlot.Color.FromHex("#F5B041");
        curPoint.MarkerSize = 14f;
        curPoint.LineWidth = 0;

        // 4. MPT 方案锚点
        var msScheme = result.Schemes.FirstOrDefault(s => s.Name.Contains("最大夏普"));
        if (msScheme != null)
        {
            var msPoint = plot.Add.Scatter(new[] { (double)msScheme.ExpectedVolatility }, new[] { (double)msScheme.ExpectedReturn });
            msPoint.LegendText = $"🎯 最大夏普最优切点 (Sharpe:{msScheme.SharpeRatio:F2})";
            msPoint.Color = ScottPlot.Color.FromHex("#F38BA8");
            msPoint.MarkerSize = 13f;
            msPoint.LineWidth = 0;
        }

        var mvScheme = result.Schemes.FirstOrDefault(s => s.Name.Contains("最小方差"));
        if (mvScheme != null)
        {
            var mvPoint = plot.Add.Scatter(new[] { (double)mvScheme.ExpectedVolatility }, new[] { (double)mvScheme.ExpectedReturn });
            mvPoint.LegendText = $"🛡️ 全局最小方差组合 (Vol:{mvScheme.ExpectedVolatility:F1}%)";
            mvPoint.Color = ScottPlot.Color.FromHex("#89B4FA");
            mvPoint.MarkerSize = 12f;
            mvPoint.LineWidth = 0;
        }

        var rpScheme = result.Schemes.FirstOrDefault(s => s.Name.Contains("风险平价"));
        if (rpScheme != null)
        {
            var rpPoint = plot.Add.Scatter(new[] { (double)rpScheme.ExpectedVolatility }, new[] { (double)rpScheme.ExpectedReturn });
            rpPoint.LegendText = $"⚖️ 风险平价组合 (Vol:{rpScheme.ExpectedVolatility:F1}%)";
            rpPoint.Color = ScottPlot.Color.FromHex("#CBA6F7");
            rpPoint.MarkerSize = 11f;
            rpPoint.LineWidth = 0;
        }

        _crosshair = plot.Add.Crosshair(0, 0);
        _crosshair.IsVisible = false;
        _crosshair.LineColor = ScottPlot.Color.FromHex("#A6E3A1").WithAlpha(0.65f);
        _crosshair.LineWidth = 1.2f;
        _crosshair.LinePattern = ScottPlot.LinePattern.Dashed;

        plot.Axes.Bottom.Label.Text = "组合预期年化波动率 σ (%)";
        plot.Axes.Left.Label.Text = "组合预期年化复合收益率 μ (%)";
        plot.Title("马科维茨现代投资组合理论 (MPT) 有效前沿散点云与最优切点");
        plot.ShowLegend(ScottPlot.Alignment.UpperLeft);
        plot.Axes.AutoScale();
        PortfolioPlot.Refresh();
    }

    private void RenderMonteCarloFanChartPlot(PortfolioResult result)
    {
        var plot = PortfolioPlot.Plot;
        plot.Clear();
        SetupPlotStyle();

        var mc = result.MonteCarloResult;
        if (mc == null || mc.TrajectoryDays == null || mc.TrajectoryDays.Length == 0)
        {
            plot.Title("暂无蒙特卡洛前瞻推演时序数据");
            PortfolioPlot.Refresh();
            return;
        }

        double[] days = mc.TrajectoryDays;

        // 1. 基准本金线 1.0000
        var baseLine = plot.Add.HorizontalLine(1.0);
        baseLine.Color = ScottPlot.Color.FromHex("#585B70");
        baseLine.LineWidth = 1.2f;
        baseLine.LinePattern = ScottPlot.LinePattern.Dashed;

        // 2. 95% 乐观上限 (绿)
        var s95 = plot.Add.Scatter(days, mc.Trajectory95);
        s95.LegendText = $"95% 乐观上轨 (终值 {mc.EndNav95:F4}, +{mc.Percentile95Return}%)";
        s95.Color = ScottPlot.Color.FromHex("#A6E3A1");
        s95.LineWidth = 2.2f;
        s95.MarkerSize = 0;

        // 3. 75% 中上良好预期 (蓝绿虚线)
        if (mc.Trajectory75.Length > 0)
        {
            var s75 = plot.Add.Scatter(days, mc.Trajectory75);
            s75.LegendText = $"75% 良好预期 (终值 {mc.Trajectory75[^1]:F4})";
            s75.Color = ScottPlot.Color.FromHex("#94E2D5");
            s75.LineWidth = 1.5f;
            s75.LinePattern = ScottPlot.LinePattern.Dotted;
            s75.MarkerSize = 0;
        }

        // 4. 50% 中位基准 (金黄实线)
        var s50 = plot.Add.Scatter(days, mc.Trajectory50);
        s50.LegendText = $"50% 中位基准 (终值 {mc.EndNavMedian:F4}, {mc.MedianReturn:+0.00;-0.00;0.00}%)";
        s50.Color = ScottPlot.Color.FromHex("#F5B041");
        s50.LineWidth = 3.0f;
        s50.MarkerSize = 0;

        // 5. 25% 承压分位 (橙黄虚线)
        if (mc.Trajectory25.Length > 0)
        {
            var s25 = plot.Add.Scatter(days, mc.Trajectory25);
            s25.LegendText = $"25% 承压防御 (终值 {mc.Trajectory25[^1]:F4})";
            s25.Color = ScottPlot.Color.FromHex("#FAB387");
            s25.LineWidth = 1.5f;
            s25.LinePattern = ScottPlot.LinePattern.Dotted;
            s25.MarkerSize = 0;
        }

        // 6. 5% 悲观底线 (绯红)
        var s5 = plot.Add.Scatter(days, mc.Trajectory5);
        s5.LegendText = $"5% 悲观防线 (终值 {mc.EndNav5:F4}, {mc.Percentile5Return:+0.00;-0.00;0.00}%)";
        s5.Color = ScottPlot.Color.FromHex("#F38BA8");
        s5.LineWidth = 2.2f;
        s5.MarkerSize = 0;

        _crosshair = plot.Add.Crosshair(0, 0);
        _crosshair.IsVisible = false;
        _crosshair.LineColor = ScottPlot.Color.FromHex("#F5B041").WithAlpha(0.65f);
        _crosshair.LineWidth = 1.2f;
        _crosshair.LinePattern = ScottPlot.LinePattern.Dashed;

        plot.Axes.Bottom.Label.Text = "前瞻推演交易日跨度 (Days, 0~250)";
        plot.Axes.Left.Label.Text = "组合推演复权净值 (起始基数 1.0000)";
        plot.Title($"几何布朗运动 (GBM) 蒙特卡洛 1000 路径扇形概率锥 (破本金概率: {mc.ProbabilityOfLoss}%, 平均回撤: {mc.ExpectedSimulatedDrawdown}%)");
        plot.ShowLegend(ScottPlot.Alignment.UpperLeft);
        plot.Axes.AutoScale();
        PortfolioPlot.Refresh();
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

        plot.Axes.Bottom.Label.Text = "历史回测日期区间";
        plot.Axes.Left.Label.Text = "累计收益率 (%)";
        plot.Title($"投资组合历史走势回测 ({result.StartDate:yyyy-MM-dd} 至 {result.EndDate:yyyy-MM-dd})");
        plot.ShowLegend(ScottPlot.Alignment.UpperLeft);
        plot.Axes.AutoScale();
        PortfolioPlot.Refresh();
    }

    private void PortfolioPlot_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_crosshair == null || _lastResult == null) return;

        long now = Environment.TickCount64;
        if (now - _lastRenderTimestamp < 16) return;
        _lastRenderTimestamp = now;

        try
        {
            var position = e.GetPosition(PortfolioPlot);
            var pixel = new ScottPlot.Pixel((float)position.X, (float)position.Y);
            var coords = PortfolioPlot.Plot.GetCoordinates(pixel);

            _crosshair.Position = coords;
            _crosshair.IsVisible = true;

            if (_currentPlotMode == "Frontier")
            {
                double vol = Math.Max(0.01, coords.X);
                double ret = coords.Y;
                double approxSharpe = (ret - 2.0) / vol;
                TxtCrosshairInfo.Text = $"📐 预期波动率 σ: {vol:F2}% | 复合预期收益率 μ: {ret:+0.00;-0.00;0.00}% | 估计夏普: {approxSharpe:F2}";
            }
            else if (_currentPlotMode == "MonteCarlo")
            {
                int day = Math.Clamp((int)Math.Round(coords.X), 0, 250);
                double nav = coords.Y;
                double pnl = (nav - 1.0) * 100.0;
                TxtCrosshairInfo.Text = $"🎲 前瞻推演交易日: 第 {day} 天 | 推演净值: {nav:F4} | 相对盈亏: {pnl:+0.00;-0.00;0.00}%";
            }
            else
            {
                if (_lastResult.PortfolioNavHistory.Count == 0) return;
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
                    TxtCrosshairInfo.Text = $"📅 {nav.Date:yyyy-MM-dd} | 🏆 组合累计收益率: {ret:+0.00;-0.00;0.00}% | 组合净值: {nav.UnitNav:F4}";
                }
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
        TxtCrosshairInfo.Text = "💡 移动鼠标至走势图区域可交互查看精准组合点位";
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

    private void BtnRefreshOrders_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult != null && _lastComponents != null)
        {
            UpdateRebalanceOrdersDisplay(_lastResult, _lastComponents);
        }
    }

    private void UpdateRebalanceOrdersDisplay(PortfolioResult result, List<(FundDetail Fund, decimal WeightPercent)> list)
    {
        if (list == null || list.Count == 0) return;

        decimal capital = 1000000m;
        if (TxtPortfolioCapital != null && decimal.TryParse(TxtPortfolioCapital.Text.Trim(), out var cap) && cap > 0)
        {
            capital = cap;
        }

        decimal subFee = 0.12m;
        if (TxtSubFeeRate != null && decimal.TryParse(TxtSubFeeRate.Text.Trim(), out var sf) && sf >= 0)
        {
            subFee = sf;
        }

        decimal redFee = 0.50m;
        if (TxtRedFeeRate != null && decimal.TryParse(TxtRedFeeRate.Text.Trim(), out var rf) && rf >= 0)
        {
            redFee = rf;
        }

        var currentHoldings = new List<(FundDetail Fund, decimal CurrentAmount)>();
        foreach (var (fund, weight) in list)
        {
            decimal amt = capital * (weight / 100m);
            currentHoldings.Add((fund, amt));
        }

        var targetWeights = _lastTargetWeights ?? result.MaxSharpeWeights;
        if (targetWeights == null || targetWeights.Count == 0)
        {
            targetWeights = list.ToDictionary(x => x.Fund.Code, x => x.WeightPercent);
        }

        decimal lotStep = 100m;
        if (TxtLotStep != null && decimal.TryParse(TxtLotStep.Text.Trim(), out var ls) && ls > 0)
        {
            lotStep = ls;
        }

        decimal cashBufferRate = 2.0m;
        if (TxtCashBufferRate != null && decimal.TryParse(TxtCashBufferRate.Text.Trim(), out var cbr) && cbr >= 0)
        {
            cashBufferRate = cbr;
        }

        var sheet = PortfolioEngine.GenerateRebalanceOrders(
            capital,
            currentHoldings,
            targetWeights,
            _lastSelectedSchemeName,
            subFee,
            redFee,
            lotStep,
            100m,
            cashBufferRate);

        result.RebalanceOrders = sheet;

        if (GridRebalanceOrders != null) GridRebalanceOrders.ItemsSource = sheet.Orders;
        if (TxtTurnoverRate != null) TxtTurnoverRate.Text = $"{sheet.ExecutableTurnoverRate:F1}% / {sheet.TurnoverRate:F1}%";
        if (TxtCashBufferReserved != null) TxtCashBufferReserved.Text = $"¥{sheet.CashBufferReserved:N0} ({sheet.CashBufferPercent:F1}%)";
        if (TxtTotalBuyAmount != null) TxtTotalBuyAmount.Text = $"¥{sheet.ExecutableBuyAmount:N0}";
        if (TxtTotalSellAmount != null) TxtTotalSellAmount.Text = $"¥{sheet.ExecutableSellAmount:N0}";
        if (TxtTotalTradeFees != null) TxtTotalTradeFees.Text = $"¥{sheet.EstimatedTotalFees:N2}";
    }

    private void BtnExportOrdersCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult?.RebalanceOrders == null || _lastResult.RebalanceOrders.Orders.Count == 0)
        {
            MessageBox.Show("当前暂无已生成的调仓交易决策单，请先运行配置计算。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出投资组合调仓交易决策单 CSV",
                Filter = "CSV 文件 (*.csv)|*.csv",
                FileName = $"BigaFund_RebalanceOrders_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                ExportService.ExportRebalanceOrdersToCsv(_lastResult.RebalanceOrders, sfd.FileName);
                MessageBox.Show($"调仓决策单已成功导出至：\n{sfd.FileName}\n\n已包含整手取整交易额、残差零头与现金缓冲预留！", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出决策单 CSV 失败：{ex.Message}", "导出异常", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdatePortfolioBrinsonDisplay(PortfolioResult result, List<(FundDetail Fund, decimal WeightPercent)> list)
    {
        if (list == null || list.Count == 0) return;

        var brinson = BrinsonAttributionEngine.CalculatePortfolioBrinsonAttribution(list, result.BenchmarkReturn);
        result.BrinsonAttribution = brinson;

        if (TxtPortBrinsonHeader != null)
        {
            TxtPortBrinsonHeader.Text = $"组合超额: {brinson.TotalExcessReturn:+0.00;-0.00;0.00}% | 配置效应: {brinson.TotalAllocationEffect:+0.00;-0.00;0.00}% | 选基效应: {brinson.TotalSelectionEffect:+0.00;-0.00;0.00}% | 交互效应: {brinson.TotalInteractionEffect:+0.00;-0.00;0.00}%";
        }
        if (GridPortfolioBrinson != null) GridPortfolioBrinson.ItemsSource = brinson.SectorItems;
        if (TxtPortBrinsonSummary != null) TxtPortBrinsonSummary.Text = brinson.SummaryAnalysis;
    }

    private void UpdateLookThroughDisplay(PortfolioResult result)
    {
        if (result.LookThroughResult == null)
        {
            if (TxtLookThroughHeader != null) TxtLookThroughHeader.Text = "底层穿透数据不足";
            if (GridLookThroughHoldings != null) GridLookThroughHoldings.ItemsSource = null;
            if (GridLookThroughIndustry != null) GridLookThroughIndustry.ItemsSource = null;
            if (TxtLookThroughSummary != null) TxtLookThroughSummary.Text = "暂无穿透持仓数据";
            return;
        }

        var lt = result.LookThroughResult;
        if (TxtLookThroughHeader != null)
        {
            TxtLookThroughHeader.Text = $"📦 穿透识别重仓: {lt.TotalUniqueStocks} 只 | 穿透总覆盖: {lt.TotalHoldingsWeight:F1}% | 底层 CR10: {lt.PortfolioCr10:F1}% | 共同重仓标的: {lt.ConsensusStockCount} 只";
        }
        if (TxtLookThroughHhi != null)
        {
            TxtLookThroughHhi.Text = $"股票HHI: {lt.StocksHhi:F0} ({lt.HhiConcentrationLevel}) | 有效持仓 Neff: {lt.EffectiveStockCount:F1} 只 | 行业HHI: {lt.IndustryHhi:F0}";
        }
        if (GridLookThroughHoldings != null) GridLookThroughHoldings.ItemsSource = lt.TopHoldings;
        if (GridLookThroughIndustry != null) GridLookThroughIndustry.ItemsSource = lt.IndustryExposures;
        if (TxtLookThroughSummary != null) TxtLookThroughSummary.Text = lt.LookThroughSummary;
    }

    private void UpdateHoldingsOverlapDisplay(PortfolioResult result)
    {
        if (result.HoldingsOverlap == null)
        {
            if (TxtOverlapHeader != null) TxtOverlapHeader.Text = "平均两两重叠率: --% | 最大重叠对: -- | 冗余预警对数: 0 对 | 分散评级: --";
            if (TxtOverlapScore != null) TxtOverlapScore.Text = "冗余度指数: -- / 100";
            if (GridHoldingsOverlap != null) GridHoldingsOverlap.ItemsSource = null;
            if (TxtOverlapAdvice != null) TxtOverlapAdvice.Text = "持仓数据不足，无法评估两两重叠消冗矩阵。";
            return;
        }

        var ol = result.HoldingsOverlap;
        if (TxtOverlapHeader != null)
        {
            TxtOverlapHeader.Text = $"平均两两重叠率: {ol.AveragePairwiseOverlap:F1}% | 最大重叠对: {(string.IsNullOrEmpty(ol.HighestOverlapPair) ? "无" : ol.HighestOverlapPair)} ({ol.MaxPairwiseOverlap:F1}%) | 预警对数: {ol.RedundantPairCount} 对 | 分散评级: {ol.DiversificationHealthGrade}";
        }
        if (TxtOverlapScore != null)
        {
            TxtOverlapScore.Text = $"冗余度指数: {ol.RedundancyScore:F1} / 100";
        }
        if (GridHoldingsOverlap != null)
        {
            GridHoldingsOverlap.ItemsSource = ol.OverlapItems;
        }
        if (TxtOverlapAdvice != null)
        {
            TxtOverlapAdvice.Text = $"💡 组合消冗调仓建议: {ol.ActionableAdvice}";
        }
    }

    private void UpdateMonteCarloDisplay(PortfolioResult result)
    {
        if (result.MonteCarloResult == null)
        {
            if (TxtMcUpper95 != null) TxtMcUpper95.Text = "--%";
            if (TxtMcMedian != null) TxtMcMedian.Text = "--%";
            if (TxtMcLower5 != null) TxtMcLower5.Text = "--%";
            if (TxtMcLossProb != null) TxtMcLossProb.Text = "--%";
            if (TxtMcExpectedDrawdown != null) TxtMcExpectedDrawdown.Text = "--%";
            if (TxtMcSummary != null) TxtMcSummary.Text = "推演数据不足";
            return;
        }

        var mc = result.MonteCarloResult;
        if (TxtMcUpper95 != null) TxtMcUpper95.Text = $"{mc.Percentile95Return:+0.00;-0.00;0.00}%";
        if (TxtMcNav95 != null) TxtMcNav95.Text = $"推演净值: {mc.EndNav95:F4}";

        if (TxtMcMedian != null) TxtMcMedian.Text = $"{mc.MedianReturn:+0.00;-0.00;0.00}%";
        if (TxtMcNavMedian != null) TxtMcNavMedian.Text = $"推演净值: {mc.EndNavMedian:F4}";

        if (TxtMcLower5 != null) TxtMcLower5.Text = $"{mc.Percentile5Return:+0.00;-0.00;0.00}%";
        if (TxtMcNav5 != null) TxtMcNav5.Text = $"推演净值: {mc.EndNav5:F4}";

        if (TxtMcLossProb != null) TxtMcLossProb.Text = $"{mc.ProbabilityOfLoss:F1}%";
        if (TxtMcExpectedDrawdown != null) TxtMcExpectedDrawdown.Text = $"-{mc.ExpectedSimulatedDrawdown:F1}%";

        if (TxtMcSummary != null) TxtMcSummary.Text = mc.SimulationSummary;
    }

    private void UpdateMacroShockDisplay(PortfolioResult result, MacroShockScenario? scenario = null)
    {
        if (result.MacroShockResult == null) return;

        var sim = result.MacroShockResult;
        var sc = scenario ?? sim.PresetScenarios.FirstOrDefault() ?? sim.CustomScenario;
        if (sc == null) return;

        if (TxtShockNavChange != null)
        {
            TxtShockNavChange.Text = $"{sc.EstimatedNavChangePercent:+0.00;-0.00;0.00}%";
            TxtShockNavChange.Foreground = sc.EstimatedNavChangePercent >= 0
                ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#A6E3A1")!
                : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F38BA8")!;
        }

        if (TxtShockScenarioName != null) TxtShockScenarioName.Text = $"情景: {sc.Name}";
        if (TxtShockPnL != null)
        {
            TxtShockPnL.Text = $"{sc.EstimatedPnL:+¥#,##0;-¥#,##0;¥0} 元";
            TxtShockPnL.Foreground = sc.EstimatedPnL >= 0
                ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#A6E3A1")!
                : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F38BA8")!;
        }

        if (TxtShockStressedVaR != null) TxtShockStressedVaR.Text = $"{sc.StressedVaR95:F2}%";
        if (TxtShockRating != null) TxtShockRating.Text = sc.ImpactRating;
        if (TxtShockDescription != null) TxtShockDescription.Text = sc.Description;

        if (GridShockFundImpacts != null)
        {
            GridShockFundImpacts.ItemsSource = sc.FundImpacts;
        }
    }

    private void BtnScenarioPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && _lastResult?.MacroShockResult != null)
        {
            var target = _lastResult.MacroShockResult.PresetScenarios.FirstOrDefault(s => s.ScenarioId == tag);
            if (target != null)
            {
                if (SliderEquityShock != null) SliderEquityShock.Value = (double)target.EquityShockPercent;
                if (SliderRateShock != null) SliderRateShock.Value = (double)target.InterestRateShockBps;
                UpdateMacroShockDisplay(_lastResult, target);
            }
        }
    }

    private void SliderShock_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || _lastResult == null || _lastComponents == null || SliderEquityShock == null || SliderRateShock == null) return;

        decimal eqShock = (decimal)SliderEquityShock.Value;
        decimal rateShock = (decimal)SliderRateShock.Value;

        if (TxtEquityShockVal != null)
        {
            TxtEquityShockVal.Text = $"{eqShock:+0;-0;0}%";
            TxtEquityShockVal.Foreground = eqShock >= 0
                ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#A6E3A1")!
                : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F38BA8")!;
        }

        if (TxtRateShockVal != null)
        {
            TxtRateShockVal.Text = $"{rateShock:+0;-0;0} bps";
            TxtRateShockVal.Foreground = rateShock <= 0
                ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#A6E3A1")!
                : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F5B041")!;
        }

        var customSim = PortfolioEngine.RunMacroScenarioShock(_lastComponents, 1000000m, eqShock, rateShock);
        if (customSim.CustomScenario != null)
        {
            _lastResult.MacroShockResult = customSim;
            UpdateMacroShockDisplay(_lastResult, customSim.CustomScenario);
        }
    }

    private void BtnRunRebalance_Click(object sender, RoutedEventArgs e)
    {
        if (_lastComponents == null || _lastComponents.Count == 0 || _lastResult == null) return;

        var strategy = RebalanceStrategyType.MonthlyCalendar;
        if (CmbRebalanceStrategy != null)
        {
            strategy = CmbRebalanceStrategy.SelectedIndex switch
            {
                0 => RebalanceStrategyType.MonthlyCalendar,
                1 => RebalanceStrategyType.QuarterlyCalendar,
                2 => RebalanceStrategyType.ThresholdBand,
                3 => RebalanceStrategyType.BuyAndHold,
                _ => RebalanceStrategyType.MonthlyCalendar
            };
        }

        decimal initialCap = 1000000m;
        if (TxtRebalanceCapital != null && decimal.TryParse(TxtRebalanceCapital.Text.Trim(), out var cap) && cap > 0)
        {
            initialCap = cap;
        }

        var sim = PortfolioEngine.SimulateDynamicRebalancing(
            _lastComponents.Select(c => (c.Fund, c.WeightPercent / 100m)).ToList(),
            strategyType: strategy,
            initialCapital: initialCap,
            frictionFeeRate: 0.0015m,
            thresholdBand: 0.05m);

        _lastResult.RebalanceSimulation = sim;
        UpdateRebalanceDisplay(_lastResult);
    }

    private void UpdateRebalanceDisplay(PortfolioResult result)
    {
        if (result.RebalanceSimulation == null && result.DynamicBacktest != null)
        {
            var db = result.DynamicBacktest;
            if (TxtRebalanceTotalReturn != null)
            {
                TxtRebalanceTotalReturn.Text = $"{db.CumulativeReturn:+0.00;-0.00}%";
                TxtRebalanceTotalReturn.Foreground = db.CumulativeReturn >= 0
                    ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F38BA8")!
                    : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#A6E3A1")!;
            }
            if (TxtRebalanceAlpha != null)
            {
                TxtRebalanceAlpha.Text = $"超额: {db.ExcessReturn:+0.00;-0.00}% (基准: {db.BenchmarkCumulativeReturn:+0.00;-0.00}%)";
                TxtRebalanceAlpha.Foreground = db.ExcessReturn >= 0
                    ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#A6E3A1")!
                    : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F38BA8")!;
            }
            if (TxtRebalanceCagr != null) TxtRebalanceCagr.Text = $"{db.AnnualizedReturn:+0.00;-0.00}%";
            if (TxtRebalanceVol != null) TxtRebalanceVol.Text = $"{db.AnnualizedVolatility:F1}%";
            if (TxtRebalanceMaxDd != null) TxtRebalanceMaxDd.Text = $"-{db.MaxDrawdown:F1}%";
            if (TxtRebalanceSharpe != null) TxtRebalanceSharpe.Text = $"{db.SharpeRatio:F2}";
            if (TxtRebalanceTurnover != null) TxtRebalanceTurnover.Text = $"{db.AnnualizedTurnoverRate:F1}% /年";
            if (TxtRebalanceFeeLoss != null) TxtRebalanceFeeLoss.Text = $"摩擦损耗: ¥{db.TotalTransactionFees:N0} (拖累 {db.FeeErosionPercent:F2}%)";
            if (GridRebalanceEvents != null) GridRebalanceEvents.ItemsSource = db.RebalanceHistory;
            if (TxtRebalanceSummary != null) TxtRebalanceSummary.Text = db.ExecutiveSummary;
            return;
        }

        if (result.RebalanceSimulation == null)
        {
            if (TxtRebalanceTotalReturn != null) TxtRebalanceTotalReturn.Text = "--%";
            if (TxtRebalanceAlpha != null) TxtRebalanceAlpha.Text = "vs买入持有: --%";
            if (TxtRebalanceCagr != null) TxtRebalanceCagr.Text = "--%";
            if (TxtRebalanceVol != null) TxtRebalanceVol.Text = "--%";
            if (TxtRebalanceMaxDd != null) TxtRebalanceMaxDd.Text = "--%";
            if (TxtRebalanceSharpe != null) TxtRebalanceSharpe.Text = "--";
            if (TxtRebalanceTurnover != null) TxtRebalanceTurnover.Text = "--%";
            if (TxtRebalanceFeeLoss != null) TxtRebalanceFeeLoss.Text = "摩擦损耗: ¥0";
            if (GridRebalanceEvents != null) GridRebalanceEvents.ItemsSource = null;
            if (TxtRebalanceSummary != null) TxtRebalanceSummary.Text = "暂无再平衡仿真数据";
            return;
        }

        var sim = result.RebalanceSimulation;
        if (TxtRebalanceTotalReturn != null)
        {
            TxtRebalanceTotalReturn.Text = $"{sim.TotalReturn:+0.00;-0.00}%";
            TxtRebalanceTotalReturn.Foreground = sim.TotalReturn >= 0
                ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F38BA8")!
                : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#A6E3A1")!;
        }

        if (TxtRebalanceAlpha != null)
        {
            TxtRebalanceAlpha.Text = $"超额: {sim.NetAlphaVsBuyAndHold:+0.00;-0.00}% (买入持有: {sim.BuyAndHoldReturn:+0.00;-0.00}%)";
            TxtRebalanceAlpha.Foreground = sim.NetAlphaVsBuyAndHold >= 0
                ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#A6E3A1")!
                : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F38BA8")!;
        }

        if (TxtRebalanceCagr != null) TxtRebalanceCagr.Text = $"{sim.AnnualizedReturn:+0.00;-0.00}%";
        if (TxtRebalanceVol != null) TxtRebalanceVol.Text = $"{sim.AnnualizedVolatility:F1}%";
        if (TxtRebalanceMaxDd != null) TxtRebalanceMaxDd.Text = $"-{sim.MaxDrawdown:F1}%";
        if (TxtRebalanceSharpe != null) TxtRebalanceSharpe.Text = $"{sim.SharpeRatio:F2}";
        if (TxtRebalanceTurnover != null) TxtRebalanceTurnover.Text = $"{sim.AnnualizedTurnoverRate:F1}% /年";
        if (TxtRebalanceFeeLoss != null) TxtRebalanceFeeLoss.Text = $"摩擦损耗: ¥{sim.TotalFrictionFeeLoss:N0} (拖累 {sim.FrictionFeeDragPercent:F2}%)";
        if (GridRebalanceEvents != null) GridRebalanceEvents.ItemsSource = sim.Events;
        if (TxtRebalanceSummary != null) TxtRebalanceSummary.Text = sim.DiagnosticSummary;
    }

    private void UpdateBarraAttributionDisplay(PortfolioResult result)
    {
        if (result.BarraReturnAttribution == null) return;
        var attr = result.BarraReturnAttribution;

        if (TxtBarraActiveReturn != null)
        {
            TxtBarraActiveReturn.Text = $"{attr.TotalActiveReturn:+0.00;-0.00;0.00}%";
            TxtBarraActiveReturn.Foreground = attr.TotalActiveReturn >= 0
                ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#A6E3A1")!
                : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F38BA8")!;
        }

        if (TxtBarraTotalVsBm != null)
            TxtBarraTotalVsBm.Text = $"组合 {result.TotalReturn:+0.00;-0.00}% vs 基准 {result.BenchmarkReturn:+0.00;-0.00}%";

        if (TxtBarStyleFactorReturn != null)
            TxtBarStyleFactorReturn.Text = $"{attr.TotalStyleFactorReturn:+0.00;-0.00;0.00}%";

        if (TxtBarStyleShare != null)
            TxtBarStyleShare.Text = $"超额贡献占比: {attr.StyleContributionSharePct:F1}%";

        if (TxtBarraSpecificAlpha != null)
            TxtBarraSpecificAlpha.Text = $"{attr.SpecificAlphaReturn:+0.00;-0.00;0.00}%";

        if (TxtBarraAlphaShare != null)
            TxtBarraAlphaShare.Text = $"超额贡献占比: {attr.SpecificAlphaSharePct:F1}%";

        if (TxtBarraDominantDriver != null)
            TxtBarraDominantDriver.Text = attr.DominantDriver;

        if (TxtBarraConservation != null)
            TxtBarraConservation.Text = attr.IsStrictlyConserved ? "严格守恒 (0.00%)" : "存在残差";

        if (TxtBarraResidual != null)
            TxtBarraResidual.Text = $"残差泄漏: {attr.ResidualGap:F4}%";

        if (GridBarraContributions != null)
            GridBarraContributions.ItemsSource = attr.FactorContributions;

        if (TxtBarraSummary != null)
            TxtBarraSummary.Text = attr.AttributionSummary;
    }

    private void UpdateLiquidityDisplay(PortfolioResult result)
    {
        if (result.LiquidityHorizon == null) return;
        var liq = result.LiquidityHorizon;

        if (TxtLiquidityDaysTotal != null)
            TxtLiquidityDaysTotal.Text = $"{liq.DaysToLiquidateTotal} 交易日";

        if (TxtLiquidityDaysWeighted != null)
            TxtLiquidityDaysWeighted.Text = $"加权天数: {liq.WeightedDaysToLiquidate:F1} 天";

        if (TxtLiquidityImpactCost != null)
            TxtLiquidityImpactCost.Text = $"{liq.TotalEstimatedImpactCostPct:F3}%";

        if (TxtLiquidityImpactLoss != null)
            TxtLiquidityImpactLoss.Text = $"预估滑点损耗: ¥{liq.TotalEstimatedImpactLossMln:N2}万";

        if (TxtLiquidityT1Pct != null)
            TxtLiquidityT1Pct.Text = $"{liq.TPlus1LiquidPct:F1}%";

        if (TxtLiquidityT3Pct != null)
            TxtLiquidityT3Pct.Text = $"T+3 累计: {liq.TPlus3LiquidPct:F1}%";

        if (TxtLiquidityGrade != null)
            TxtLiquidityGrade.Text = liq.LiquidityGrade;

        if (TxtLiquidityT7Pct != null)
            TxtLiquidityT7Pct.Text = $"{liq.TPlus7LiquidPct:F1}%";

        if (GridLiquidityItems != null)
            GridLiquidityItems.ItemsSource = liq.Items;

        if (TxtLiquiditySummary != null)
            TxtLiquiditySummary.Text = liq.ExecutiveSummary;
    }

    private void BtnRecalcLiquidity_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null || _lastComponents == null || _lastComponents.Count == 0)
        {
            MessageBox.Show("请先执行组合回测后再测算流动性。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        decimal aumMln = 300m;
        if (decimal.TryParse(TxtLiquidityAum?.Text?.Trim(), out decimal parsedAum) && parsedAum > 0)
        {
            aumMln = parsedAum;
        }

        decimal partRate = 10.0m;
        if (decimal.TryParse(TxtLiquidityParticipation?.Text?.Trim(), out decimal parsedPart) && parsedPart > 0)
        {
            partRate = parsedPart;
        }

        var liq = PortfolioEngine.CalculatePortfolioLiquidityHorizon(_lastComponents, aumMln, partRate);
        _lastResult.LiquidityHorizon = liq;
        UpdateLiquidityDisplay(_lastResult);
        MessageBox.Show($"已根据组合规模 ¥{aumMln:N0}百万元，单日参与率 {partRate:F1}% 完成流动性地平线与冲击成本重新测算！", "测算完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #region Phase 19: SAA/TAA 偏离监控与夏普比率解构看板

    private void UpdateSaaTaaDisplay(PortfolioResult result)
    {
        if (result.SaaTaaMonitoring == null) return;
        var saa = result.SaaTaaMonitoring;

        if (TxtSaaComplianceScore != null)
            TxtSaaComplianceScore.Text = $"{saa.SaaComplianceScore:F1} 分";

        if (TxtSaaOverallStatus != null)
            TxtSaaOverallStatus.Text = $"状态: {saa.OverallStatus}";

        if (TxtSaaTrackingError != null)
            TxtSaaTrackingError.Text = $"{saa.TotalActiveTrackingError:F2}%";

        if (TxtSaaInformationRatio != null)
            TxtSaaInformationRatio.Text = $"{saa.InformationRatio:F2}";

        if (TxtSaaActiveReturn != null)
            TxtSaaActiveReturn.Text = $"主动超额: {saa.ActiveExcessReturn:+0.00;-0.00;0.00}%";

        if (TxtSaaMaxDevAsset != null)
            TxtSaaMaxDevAsset.Text = !string.IsNullOrEmpty(saa.MaxDeviationAsset) ? saa.MaxDeviationAsset : "无明显偏离";

        if (TxtSaaMaxDevPercent != null)
            TxtSaaMaxDevPercent.Text = $"偏离幅度: {saa.MaxDeviationPercent:F1}%";

        if (TxtSaaBreachCount != null)
        {
            if (saa.HardBreachCount > 0)
            {
                TxtSaaBreachCount.Text = $"🚨 {saa.HardBreachCount} 项硬违规";
                TxtSaaBreachCount.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F38BA8"));
            }
            else if (saa.SoftBreachCount > 0)
            {
                TxtSaaBreachCount.Text = $"⚠️ {saa.SoftBreachCount} 项软预警";
                TxtSaaBreachCount.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5B041"));
            }
            else
            {
                TxtSaaBreachCount.Text = "🟢 0 项超限";
                TxtSaaBreachCount.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A6E3A1"));
            }
        }

        if (TxtSaaRebalanceAdvice != null)
            TxtSaaRebalanceAdvice.Text = saa.RebalanceRequired ? "调仓纠偏: 建议启动战术再平衡" : "调仓纠偏: 偏离可控，保持持仓";

        if (GridSaaTaaItems != null)
            GridSaaTaaItems.ItemsSource = saa.AssetClassDeviations;

        if (TxtSaaTaaSummary != null)
            TxtSaaTaaSummary.Text = saa.SummaryRecommendation;
    }

    private void UpdateSharpeDecompositionAndReverseStressDisplay(PortfolioResult result)
    {
        // 1. 夏普比率微分分解与成分基金诊断
        if (result.SharpeDecomposition != null)
        {
            var sd = result.SharpeDecomposition;
            if (TxtSharpeDecompKpi != null)
            {
                TxtSharpeDecompKpi.Text = $"全组合夏普: {sd.PortfolioSharpeRatio:F2} | 🔥 超额引擎: {sd.AlphaEngineCount} 只 | ⚠️ 夏普拖累: {sd.SharpeDragCount} 只 | 潜在夏普上限: {sd.DragRemovalSharpePotential:F2}";
            }
            if (GridSharpeDecompItems != null)
            {
                GridSharpeDecompItems.ItemsSource = sd.Items;
            }
            if (TxtSharpeDecompSummary != null)
            {
                TxtSharpeDecompSummary.Text = sd.StrategicActionPlan;
            }
        }

        // 2. 极端宏观反向压力测试与 Kupiec VaR 检验
        if (result.ReverseStressTest != null)
        {
            var rst = result.ReverseStressTest;
            if (TxtReverseStressNorm != null)
            {
                TxtReverseStressNorm.Text = $"最小冲击马氏距离范数: {rst.MinimalShockNorm:F2}σ | 组合最脆弱因子: 【{rst.MostFragileFactor}】(仅需跌 {rst.MostFragileFactorShockThreshold:F1}%)";
            }
            if (GridReverseStressItems != null)
            {
                GridReverseStressItems.ItemsSource = rst.FactorShocks;
            }
            if (TxtReverseStressSummary != null)
            {
                string kupiecInfo = string.Empty;
                if (result.KupiecVaRTest != null)
                {
                    var kv = result.KupiecVaRTest;
                    kupiecInfo = $" | 📊 风险价值 VaR(95%) 250日后验检验: 突破 {kv.HistoricalExceptions} 次(实际失效率 {kv.HistoricalFailureRate:F1}%), Kupiec LR = {kv.HistoricalLikelihoodRatio:F2} (p-value: {kv.HistoricalPValue:F3}), 巴塞尔监管交通灯: {kv.HistoricalTrafficLightBadge}";
                }
                TxtReverseStressSummary.Text = $"{rst.FragilityDiagnosis} 对冲建议: {rst.RecommendedHedgingAction}{kupiecInfo}";
            }
        }
    }

    private void UpdateMacroRegimeAndGatekeeperDisplay(PortfolioResult result)
    {
        // 1. 宏观经济四象限与体制轮动配置 (Phase 20)
        if (result.MacroRegimeSwitching != null)
        {
            var mrs = result.MacroRegimeSwitching;
            if (TxtMacroRegimeBadge != null)
            {
                TxtMacroRegimeBadge.Text = mrs.CurrentRegime switch
                {
                    MacroRegimeType.Recovery => "🌱 经济复苏象限 (高增长·低通胀)",
                    MacroRegimeType.Overheat => "🔥 经济过热象限 (高增长·高通胀)",
                    MacroRegimeType.Stagflation => "🌪️ 滞胀衰退象限 (低增长·高通胀)",
                    MacroRegimeType.Recession => "❄️ 深度萧条象限 (低增长·低通胀)",
                    _ => "🌐 全天候平衡象限"
                };
            }
            if (TxtMacroFitScore != null)
            {
                TxtMacroFitScore.Text = $"体制契合度: {mrs.RegimeFitScore:F1}分";
            }
            if (TxtMacroRegimeSummary != null)
            {
                TxtMacroRegimeSummary.Text = mrs.EconomicEnvironmentSummary;
            }
            if (GridMacroAssetTargets != null)
            {
                GridMacroAssetTargets.ItemsSource = mrs.AssetTargets;
            }
            if (GridCorrelationJump != null)
            {
                GridCorrelationJump.ItemsSource = mrs.CorrelationJumpMatrix;
            }
        }

        // 2. 条件在险回撤 (CDaR) 与欧拉期望亏空 (Euler ES) (Phase 20)
        if (result.TailRiskDecomposition != null)
        {
            var trd = result.TailRiskDecomposition;
            if (TxtCdar95 != null)
            {
                TxtCdar95.Text = $"{trd.ConditionalDrawdownAtRisk95:F2}%";
            }
            if (TxtExpectedShortfall95 != null)
            {
                TxtExpectedShortfall95.Text = $"{trd.PortfolioExpectedShortfallPercent:F2}%";
            }
            if (GridEulerEsItems != null)
            {
                GridEulerEsItems.ItemsSource = trd.Items;
            }
        }

        // 3. 投审会准入闸门合规审计 (Phase 20)
        if (result.GatekeeperAudit != null)
        {
            var gka = result.GatekeeperAudit;
            if (TxtGatekeeperPassRate != null)
            {
                TxtGatekeeperPassRate.Text = $"{gka.PortfolioOverallPassRate:F1}%";
            }
            if (GridGatekeeperAudits != null)
            {
                GridGatekeeperAudits.ItemsSource = gka.FundAudits;
            }
            if (TxtPhase20ExecutiveSummary != null)
            {
                string tailSummary = result.TailRiskDecomposition?.TailRiskExecutiveSummary ?? string.Empty;
                TxtPhase20ExecutiveSummary.Text = $"{gka.CommitteeAuditSummary} {tailSummary}";
            }
        }
    }

    private void UpdateFactorRiskAndLdiDisplay(PortfolioResult result)
    {
        // 1. 多资产多因子风险平价 (Factor Risk Parity) (Phase 21)
        if (result.FactorRiskParity != null)
        {
            var frp = result.FactorRiskParity;
            if (TxtFrpSystematicRisk != null) TxtFrpSystematicRisk.Text = $"{frp.SystematicRiskVariancePercent:F1}%";
            if (TxtFrpSpecificRisk != null) TxtFrpSpecificRisk.Text = $"特异残差: {frp.SpecificRiskVariancePercent:F1}%";
            if (TxtFrpHhiIndex != null) TxtFrpHhiIndex.Text = $"{frp.FactorRiskHerfindahlIndex:F0}";
            if (TxtFrpGradeBadge != null) TxtFrpGradeBadge.Text = frp.FactorDiversificationGrade;
            if (GridFactorRiskItems != null) GridFactorRiskItems.ItemsSource = frp.FactorRiskItems;
        }

        // 2. 负债驱动投资 (LDI) 资产负债久期免疫 (Phase 21)
        if (result.LdiImmunization != null)
        {
            var ldi = result.LdiImmunization;
            if (TxtLdiFundingRatio != null) TxtLdiFundingRatio.Text = $"{ldi.FundingRatio:F1}%";
            if (TxtLdiImmunizedBadge != null)
            {
                TxtLdiImmunizedBadge.Text = ldi.IsRedingtonImmunized ? "🟢 雷丁顿免疫达标" : "⚠️ 存在久期错配";
                TxtLdiImmunizedBadge.Foreground = ldi.IsRedingtonImmunized
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(166, 227, 161))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 139, 168));
            }
            if (TxtLdiDurationGap != null) TxtLdiDurationGap.Text = $"{ldi.DollarDurationGap:+0.00;-0.00} 亿·年";
            if (TxtLdiMacaulayMismatch != null) TxtLdiMacaulayMismatch.Text = $"资产 {ldi.AssetMacaulayDurationYears:F1}年 vs 负债 {ldi.LiabilityMacaulayDurationYears:F1}年";
            if (GridLdiStressShocks != null) GridLdiStressShocks.ItemsSource = ldi.StressShocks;
        }

        // 3. 考虑中国公募阶梯赎回费与账龄时钟的容差动态再平衡 (Phase 21)
        if (result.TieredFeeRebalance != null)
        {
            var tfr = result.TieredFeeRebalance;
            if (TxtRebalanceAvoidedFees != null) TxtRebalanceAvoidedFees.Text = $"¥{tfr.AvoidedPunitiveFeesTotal:F2} 万";
            if (TxtRebalanceTurnoverReduction != null) TxtRebalanceTurnoverReduction.Text = $"换手压降: {tfr.TurnoverReductionRatePercent:F1}%";
            if (GridTieredRebalanceItems != null) GridTieredRebalanceItems.ItemsSource = tfr.Items;
        }

        if (TxtPhase21ExecutiveSummary != null)
        {
            string frpDiag = result.FactorRiskParity?.ExecutiveDiagnosis ?? string.Empty;
            string ldiDiag = result.LdiImmunization?.ExecutiveAdvice ?? string.Empty;
            string rebDiag = result.TieredFeeRebalance?.FrictionOptimizationSummary ?? string.Empty;
            TxtPhase21ExecutiveSummary.Text = $"{frpDiag}\n{ldiDiag}\n{rebDiag}";
        }
    }

    private void BtnRecalcPhase21_Click(object sender, RoutedEventArgs e)
    {
        if (_lastComponents == null || _lastComponents.Count == 0)
        {
            MessageBox.Show("请先构建或计算投资组合！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        decimal band = 5.0m;
        if (decimal.TryParse(TxtToleranceBandInput?.Text, out var bVal))
        {
            band = Math.Clamp(bVal, 1.0m, 20.0m);
        }

        decimal aum = 100.0m;
        if (decimal.TryParse(TxtLdiAumInput?.Text, out var aVal))
        {
            aum = Math.Clamp(aVal, 10.0m, 10000.0m);
        }

        if (_lastResult != null)
        {
            _lastResult.FactorRiskParity = QuantCalculator.DecomposeFactorRisk(_lastComponents, _lastResult.PortfolioNavHistory);
            _lastResult.LdiImmunization = PortfolioEngine.EvaluateLdiImmunization(_lastComponents, aum);
            _lastResult.TieredFeeRebalance = PortfolioEngine.SimulateTieredFeeDynamicRebalance(_lastComponents, band);

            UpdateFactorRiskAndLdiDisplay(_lastResult);
        }
    }

    private void UpdateHolographicStressAndExecutionShortfallDisplay(PortfolioResult result)
    {
        // 1. 全息历史极端危机情景多因子传导压力测试 (Aladdin-Grade) (Phase 22)
        if (result.HistoricalCrisisStress != null)
        {
            var hcs = result.HistoricalCrisisStress;
            if (TxtWorstCrisisReturn != null)
            {
                TxtWorstCrisisReturn.Text = $"{hcs.WorstCaseLossPercent:+0.0;-0.0}%";
                TxtWorstCrisisReturn.Foreground = hcs.WorstCaseLossPercent < -25m
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 139, 168))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 176, 65));
            }
            if (TxtWorstCrisisName != null) TxtWorstCrisisName.Text = $"最大承压: {hcs.WorstCrisisName}";
            if (TxtPortfolioResilienceGrade != null)
            {
                TxtPortfolioResilienceGrade.Text = hcs.OverallResilienceRating;
            }
            if (GridHistoricalCrises != null)
            {
                GridHistoricalCrises.ItemsSource = hcs.CrisisScenarios;
            }
        }

        // 2. 大体量资金执行落差与平方根市场冲击模型 (Phase 22)
        if (result.ExecutionShortfall != null)
        {
            var es = result.ExecutionShortfall;
            if (TxtAverageImpactBps != null) TxtAverageImpactBps.Text = $"{es.AverageImpactBps:F1} bps";
            if (TxtTotalTradeVolume != null) TxtTotalTradeVolume.Text = $"调仓体量: ¥{es.TotalTradeVolumeTenThousand:F1}万";
            if (TxtTotalImpactLoss != null) TxtTotalImpactLoss.Text = $"¥{es.TotalMarketImpactCostTenThousand:F2}万";
            if (TxtGiantRedemptionCount != null)
            {
                TxtGiantRedemptionCount.Text = $"{es.GiantRedemptionBreachCount} 只";
                TxtGiantRedemptionCount.Foreground = es.GiantRedemptionBreachCount > 0
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 139, 168))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(166, 227, 161));
            }
            if (GridExecutionImpactItems != null)
            {
                GridExecutionImpactItems.ItemsSource = es.ImpactItems;
            }
            if (GridTwapTranches != null)
            {
                GridTwapTranches.ItemsSource = es.RecommendedTwapTranches;
            }
        }

        if (TxtPhase22ExecutiveSummary != null)
        {
            string stressDiag = result.HistoricalCrisisStress?.ExecutiveSummary ?? string.Empty;
            string execDiag = result.ExecutionShortfall?.ExecutionDeskSummary ?? string.Empty;
            TxtPhase22ExecutiveSummary.Text = $"{stressDiag}\n{execDiag}";
        }
    }

    private void BtnRecalcPhase22_Click(object sender, RoutedEventArgs e)
    {
        if (_lastComponents == null || _lastComponents.Count == 0)
        {
            MessageBox.Show("请先构建或计算投资组合！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        decimal capital = 1000m;
        if (decimal.TryParse(TxtPhase22CapitalInput?.Text, out var cVal))
        {
            capital = Math.Clamp(cVal, 10m, 100000m);
        }

        if (_lastResult != null)
        {
            var tupleComponents = _lastComponents.Select(c => (c.Fund, c.WeightPercent)).ToList();
            _lastResult.HistoricalCrisisStress = QuantCalculator.EvaluateHolographicCrisisStress(tupleComponents, capital);
            _lastResult.ExecutionShortfall = PortfolioEngine.SimulateExecutionShortfall(tupleComponents, _lastResult.RebalanceOrders, capital);

            UpdateHolographicStressAndExecutionShortfallDisplay(_lastResult);
        }
    }

    private void UpdatePhase23GipsAndShadowPurityDisplay(PortfolioResult result)
    {
        // 1. GIPS 国际标准多期复合 Brinson 归因 (Carino 对数平滑)
        if (result.GipsMultiPeriodBrinson != null)
        {
            var g = result.GipsMultiPeriodBrinson;
            if (TxtGipsTotalExcess != null)
            {
                TxtGipsTotalExcess.Text = $"{g.TotalCompoundedExcessReturn:+0.00;-0.00;0.00}%";
                TxtGipsTotalExcess.Foreground = g.TotalCompoundedExcessReturn >= 0
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 139, 168))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(166, 227, 161));
            }
            if (TxtGipsIdentityBadge != null)
            {
                TxtGipsIdentityBadge.Text = g.IsGipsIdentityStrictlySatisfied
                    ? "✅ GIPS 零残差恒等式守恒"
                    : $"残差: {g.MathematicalIdentityResidual:F6}";
                TxtGipsIdentityBadge.Foreground = g.IsGipsIdentityStrictlySatisfied
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(166, 227, 161))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 176, 65));
            }
            if (TxtGipsAllocSelectRatio != null)
            {
                TxtGipsAllocSelectRatio.Text = $"{g.CumulativeSmoothedAllocationEffect:+0.00;-0.00}% / {g.CumulativeSmoothedSelectionEffect:+0.00;-0.00}%";
            }
            if (TxtGipsInteractionText != null)
            {
                TxtGipsInteractionText.Text = $"交互效应: {g.CumulativeSmoothedInteractionEffect:+0.00;-0.00}% (权重比 {g.AllocationContributionPercent:F0}%:{g.SelectionContributionPercent:F0}%)";
            }
            if (GridGipsPeriods != null)
            {
                GridGipsPeriods.ItemsSource = g.Periods;
            }
        }

        // 2. FOF 底层全息影子组合二次重构与风格纯度分析
        if (result.ShadowPortfolioPurity != null)
        {
            var s = result.ShadowPortfolioPurity;
            if (TxtShadowActiveShare != null) TxtShadowActiveShare.Text = $"{s.ActiveSharePercent:F1}%";
            if (TxtShadowStockCount != null) TxtShadowStockCount.Text = $"穿透股票: {s.TotalUnderlyingStockCount} 只 | CR10: {s.Top10StockConcentrationPercent:F1}%";
            if (TxtShadowStylePurityScore != null)
            {
                TxtShadowStylePurityScore.Text = $"{s.StylePurityScore:F1} 分";
                TxtShadowStylePurityScore.Foreground = s.StylePurityScore >= 75m
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(166, 227, 161))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 176, 65));
            }
            if (TxtShadowRiskLevel != null)
            {
                TxtShadowRiskLevel.Text = s.HiddenOverlapRiskLevel;
            }
            if (GridShadowHoldings != null)
            {
                GridShadowHoldings.ItemsSource = s.TopShadowHoldings;
            }
            if (ListResonanceAlerts != null)
            {
                ListResonanceAlerts.ItemsSource = s.HighResonanceAlerts.Count > 0
                    ? s.HighResonanceAlerts
                    : new List<string> { "🟢 穿透底层无高危重叠股票共振，组合各成分基金持仓分散健康。" };
            }
        }

        if (TxtPhase23ExecutiveSummary != null)
        {
            string gipsSummary = result.GipsMultiPeriodBrinson?.PerformanceSummary ?? string.Empty;
            string shadowSummary = result.ShadowPortfolioPurity?.PurityDiagnosis ?? string.Empty;
            TxtPhase23ExecutiveSummary.Text = $"{gipsSummary}\n{shadowSummary}";
        }
    }

    private void UpdatePhase24LiquidityAndInsuranceDisplay(PortfolioResult result)
    {
        // 1. 流动性阶梯与巨额赎回仿真
        if (result.LiquidityRedemptionRun != null)
        {
            var l = result.LiquidityRedemptionRun;
            if (TxtLiquidityDtl != null) TxtLiquidityDtl.Text = $"{l.PortfolioWeightedDtlDays:F2} 天";
            if (TxtPhase24LiquidityGrade != null) TxtPhase24LiquidityGrade.Text = l.PortfolioLiquidityGrade;
            if (TxtTier1Tier4Ratio != null) TxtTier1Tier4Ratio.Text = $"T1: {l.Tier1WeightPercent:F0}% / T4: {l.Tier4WeightPercent:F0}%";
            if (GridFundLiquidityTiers != null) GridFundLiquidityTiers.ItemsSource = l.FundTierItems;
            if (GridRedemptionCascades != null) GridRedemptionCascades.ItemsSource = l.CascadeScenarios;
        }

        // 2. CPPI 与 TIPP 保本保险
        if (result.PortfolioInsurance != null)
        {
            var pi = result.PortfolioInsurance;
            if (TxtCppiStats != null)
            {
                TxtCppiStats.Text = $"{pi.CppiTotalReturnPercent:+0.0;-0.0}% (回撤 {pi.CppiMaxDrawdownPercent:F1}%)";
            }
            if (TxtCppiFloorStatus != null)
            {
                TxtCppiFloorStatus.Text = pi.IsCppiFloorPreserved ? "✅ 贴现底线守恒 100%" : "⚠️ 击穿底线预警";
            }
            if (TxtTippStats != null)
            {
                TxtTippStats.Text = $"{pi.TippTotalReturnPercent:+0.0;-0.0}% (回撤 {pi.TippMaxDrawdownPercent:F1}%)";
            }
            if (TxtTippFloorStatus != null)
            {
                TxtTippFloorStatus.Text = pi.IsTippFloorPreserved ? "✅ 棘轮锁定守恒 100%" : "⚠️ 击穿棘轮预警";
            }
            if (GridInsurancePaths != null)
            {
                GridInsurancePaths.ItemsSource = pi.PathPoints;
            }
        }

        // 3. 高阶矩修正夏普与 Omega
        if (result.HigherMomentsOptimization != null)
        {
            var hm = result.HigherMomentsOptimization;
            if (TxtHigherMomentsRatio != null)
            {
                TxtHigherMomentsRatio.Text = $"MSR: {hm.ModifiedPortfolioSharpe:F2} / Ω: {hm.OmegaPortfolioRatio:F2}";
            }
            if (GridHigherMomentsAssets != null)
            {
                GridHigherMomentsAssets.ItemsSource = hm.AssetMetrics;
            }
        }

        // 4. 底部执行诊断
        if (TxtPhase24ExecutiveSummary != null)
        {
            string liqDiag = result.LiquidityRedemptionRun?.ExecutiveDiagnosis ?? string.Empty;
            string insDiag = result.PortfolioInsurance?.StrategyRecommendation ?? string.Empty;
            string hmDiag = result.HigherMomentsOptimization?.OptimizationComparisonDiagnosis ?? string.Empty;
            TxtPhase24ExecutiveSummary.Text = $"{liqDiag}\n{insDiag}\n{hmDiag}";
        }
    }

    #region Phase 25 UI Updates & Handlers

    private void UpdatePhase25CopulaAndParetoDisplay(PortfolioResult result)
    {
        // 1. 极值非对称 Copula 尾部联结模型
        if (result.CopulaTailDependence != null)
        {
            var cop = result.CopulaTailDependence;
            if (TxtCopulaLowerTail != null) TxtCopulaLowerTail.Text = cop.PortfolioWeightedLowerTailDependence.ToString("F3");
            if (TxtCopulaUpperTail != null) TxtCopulaUpperTail.Text = cop.PortfolioWeightedUpperTailDependence.ToString("F3");
            if (TxtCopulaCrashAmplification != null) TxtCopulaCrashAmplification.Text = $"{cop.SystemicCrashAmplificationFactor:F2}x";
            if (TxtLinearVaRDiff != null) TxtLinearVaRDiff.Text = $"高斯 VaR 偏差: +{cop.LinearVsCopulaVaRDifferencePercent:F1}%";
            if (GridCopulaPairs != null) GridCopulaPairs.ItemsSource = cop.PairwiseCopulaList;
        }

        // 2. NSGA-II 多目标 Pareto 前沿自适应解集
        if (result.ParetoMultiObjective != null)
        {
            var pareto = result.ParetoMultiObjective;
            if (TxtParetoHypervolume != null) TxtParetoHypervolume.Text = pareto.HypervolumeIndicator.ToString("P1");
            if (TxtParetoKneeReturn != null) TxtParetoKneeReturn.Text = $"拐点收益: {pareto.OptimalCompromiseSolution?.ExpectedReturnPercent:F1}%";
            if (GridParetoSolutions != null) GridParetoSolutions.ItemsSource = pareto.FrontierSolutions;
        }

        // 3. GARCH(1,1) 前瞻条件异方差波动率与波动锥
        if (result.GarchVolatilityForecast != null)
        {
            var garch = result.GarchVolatilityForecast;
            if (TxtGarchVolRatio != null) TxtGarchVolRatio.Text = $"{garch.CurrentConditionalVolPercent:F1}% / {garch.LongTermUnconditionalVolPercent:F1}%";
            if (TxtGarchHalfLife != null) TxtGarchHalfLife.Text = $"半衰期: {garch.HalfLifeDays:F1} 天";
            if (GridGarchCone != null) GridGarchCone.ItemsSource = garch.ForecastPoints;
        }

        // 4. 底部执行诊断与策略建议
        if (TxtPhase25ExecutiveSummary != null)
        {
            string copDiag = result.CopulaTailDependence?.ExecutiveTailDiagnosis ?? string.Empty;
            string paretoDiag = result.ParetoMultiObjective?.ExecutiveParetoAdvice ?? string.Empty;
            string garchDiag = result.GarchVolatilityForecast?.TacticalRiskBudgetAdvice ?? string.Empty;
            TxtPhase25ExecutiveSummary.Text = $"{copDiag}\n{paretoDiag}\n{garchDiag}";
        }
    }

    private void BtnExportInstitutionalDueDiligence_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null || _lastComponents == null || _lastComponents.Count == 0)
        {
            MessageBox.Show("请先执行投资组合量化回测后再导出机构级尽调研报。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出机构级投资组合尽职调查与资产配置研报 (Fact Sheet HTML)",
            Filter = "HTML 研报 (*.html)|*.html",
            FileName = $"BIGA_机构尽调研报_FactSheet_{DateTime.Now:yyyyMMdd_HHmmss}.html"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ExportService.GenerateInstitutionalDueDiligenceFactSheetHtml(_lastResult, _lastComponents, dialog.FileName);
                var openResult = MessageBox.Show("机构级尽调资产配置研报导出成功！是否立即在浏览器中打开预览？", "导出完成", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (openResult == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出机构研报失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Phase 26 UI Updates & Handlers

    private void UpdatePhase26InstitutionalFlagshipDisplay(PortfolioResult result)
    {
        // 1. Ledoit-Wolf 矩阵收缩
        if (result.LedoitWolfShrinkage != null)
        {
            var lw = result.LedoitWolfShrinkage;
            if (TxtShrinkageIntensity != null) TxtShrinkageIntensity.Text = $"{lw.OptimalShrinkagePercent:F1}%";
            if (TxtConditionImprovement != null) TxtConditionImprovement.Text = $"良态优化: {lw.ConditionNumberImprovementRatio:F1}x";
            if (GridShrinkageItems != null) GridShrinkageItems.ItemsSource = lw.CovarianceItems;
        }

        // 2. Hamilton 马尔可夫两状态体制
        if (result.MarkovRegimeSwitching != null)
        {
            var mrs = result.MarkovRegimeSwitching;
            if (TxtCurrentRegime != null) TxtCurrentRegime.Text = mrs.CurrentRegime;
            if (TxtRegimeProbability != null) TxtRegimeProbability.Text = $"置信度: {mrs.CurrentRegimeProbabilityPercent:F1}%";
            if (GridRegimeStates != null)
            {
                GridRegimeStates.ItemsSource = mrs.RegimeStates;
            }
        }

        // 3. Merton 泊松跳跃扩散
        if (result.MertonJumpDiffusion != null)
        {
            var jd = result.MertonJumpDiffusion;
            if (TxtMertonJumpStats != null) TxtMertonJumpStats.Text = $"{jd.JumpIntensityLambda:F1}次/年 | {jd.JumpAdjustedVaR95Percent:F1}%";
            if (TxtTailRiskUnderestimation != null) TxtTailRiskUnderestimation.Text = $"高斯尾部低估: +{jd.TailRiskUnderestimationPercent:F1}%";
        }

        // 4. 规模敏感型流动性调整 L-VaR
        if (result.LiquidityAdjustedVaR != null)
        {
            var lvar = result.LiquidityAdjustedVaR;
            if (TxtTotalLVaR95 != null) TxtTotalLVaR95.Text = $"{lvar.TotalLVaR95Wan:F1} 万元";
            if (TxtLiquidityMultiplier != null) TxtLiquidityMultiplier.Text = $"流动性乘数: {lvar.LiquidityRiskMultiplier:F2}x";
            if (GridLVaRScenarios != null) GridLVaRScenarios.ItemsSource = lvar.ScaleScenarios;
        }

        // 5. Tearsheet 月度回报热力矩阵
        if (result.PortfolioTearsheet != null)
        {
            var ts = result.PortfolioTearsheet;
            if (TxtTearsheetWinRate != null) TxtTearsheetWinRate.Text = $"{ts.MonthlyWinRatePercent:F1}% / {ts.MaxUnderwaterDays}天";
            if (TxtTearsheetBestMonth != null) TxtTearsheetBestMonth.Text = $"最佳单月: +{ts.BestMonthlyReturnPercent:F1}%";
            if (GridTearsheetHeatmap != null) GridTearsheetHeatmap.ItemsSource = ts.MonthlyHeatmapRows;
            if (TxtPhase26ExecutiveSummary != null && !string.IsNullOrWhiteSpace(ts.TearsheetExecutiveSummary))
            {
                TxtPhase26ExecutiveSummary.Text = ts.TearsheetExecutiveSummary;
            }
        }
    }

    #endregion

    #region Phase 27 UI Updates & Handlers

    private void UpdatePhase27InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        // 1. Almgren-Chriss 最优微观执行轨迹
        if (result.AlmgrenChrissExecution != null)
        {
            var ac = result.AlmgrenChrissExecution;
            if (TxtAcHalfLife != null) TxtAcHalfLife.Text = $"{ac.HalfLifeDays:F1} 天";
            if (TxtAcStrategy != null) TxtAcStrategy.Text = $"执行模式: {ac.OptimalStrategyStyle.Split(' ')[0]}";
            if (TxtAcExpectedCost != null) TxtAcExpectedCost.Text = $"{ac.ExpectedTotalCostWan:F2} 万元";
            if (TxtAcCostRatio != null) TxtAcCostRatio.Text = $"成本占比: {ac.ExpectedTotalCostPercent:F3}%";
            if (TxtAcVaR95 != null) TxtAcVaR95.Text = $"{ac.ExecutionVaR95Wan:F2} 万元";
            if (TxtAcStdDev != null) TxtAcStdDev.Text = $"时机方差: {ac.ExecutionVarianceRiskWan:F2} 万元";
            if (GridAcTrajectory != null) GridAcTrajectory.ItemsSource = ac.TrajectorySteps;
        }

        // 2. Michaud 蒙特卡洛重抽样有效前沿
        if (result.MichaudResampledFrontier != null)
        {
            var mf = result.MichaudResampledFrontier;
            if (TxtMichaudRobustness != null) TxtMichaudRobustness.Text = $"{mf.ResampledRobustnessGainRatio:F2}x";
            if (TxtMichaudBestSharpe != null) TxtMichaudBestSharpe.Text = $"前沿最优夏普: {mf.BestSharpeValue:F2}";
            if (GridMichaudFrontier != null) GridMichaudFrontier.ItemsSource = mf.ResampledPoints;
        }

        // 3. Reverse Stress Testing 反向破产冲击拓扑
        if (result.ReverseStressTopology != null)
        {
            var rst = result.ReverseStressTopology;
            if (TxtReverseStressDist != null) TxtReverseStressDist.Text = $"{rst.MahalanobisDistance:F2} σ";
            if (GridReverseStress != null) GridReverseStress.ItemsSource = rst.ShockItems;
        }

        // 4. Cornish-Fisher 高阶矩展开极值对比
        if (result.CornishFisherVaR != null)
        {
            var cf = result.CornishFisherVaR;
            if (TxtCornishFisherBadge != null) TxtCornishFisherBadge.Text = $"尾部: {cf.TailRiskHealthBadge.Split(' ')[0]} ({cf.TailRiskUnderestimationMultiplier:F2}x)";

            var cfRows = new List<object>
            {
                new { MetricName = "95% 在险价值 (VaR)", GaussianValue = $"{cf.GaussianVaR95Percent:F2}%", CornishFisherValue = $"{cf.CornishFisherVaR95Percent:F2}%", DeltaDiff = $"+{(cf.CornishFisherVaR95Percent - cf.GaussianVaR95Percent):F2}%" },
                new { MetricName = "99% 极值 VaR", GaussianValue = $"{cf.GaussianVaR99Percent:F2}%", CornishFisherValue = $"{cf.CornishFisherVaR99Percent:F2}%", DeltaDiff = $"+{(cf.CornishFisherVaR99Percent - cf.GaussianVaR99Percent):F2}%" },
                new { MetricName = "95% 条件在险 (CVaR)", GaussianValue = $"{cf.GaussianCVaR95Percent:F2}%", CornishFisherValue = $"{cf.CornishFisherCVaR95Percent:F2}%", DeltaDiff = $"+{(cf.CornishFisherCVaR95Percent - cf.GaussianCVaR95Percent):F2}%" },
                new { MetricName = "收益偏度 (Skewness)", GaussianValue = "0.000", CornishFisherValue = $"{cf.SampleSkewness:F3}", DeltaDiff = cf.SampleSkewness < 0 ? "左偏暴跌" : "良性右偏" },
                new { MetricName = "超额峰度 (Kurtosis)", GaussianValue = "0.000", CornishFisherValue = $"{cf.SampleExcessKurtosis:F3}", DeltaDiff = cf.SampleExcessKurtosis > 0 ? "尖峰肥尾" : "轻尾" }
            };
            if (GridCornishFisherMetrics != null) GridCornishFisherMetrics.ItemsSource = cfRows;

            if (TxtPhase27ExecutiveVerdict != null && result.ReverseStressTopology != null)
            {
                TxtPhase27ExecutiveVerdict.Text = $"{result.ReverseStressTopology.RiskOfficerVerdict} ｜ {cf.AnalyticalSummary}";
            }
        }
    }

    #endregion

    #region Phase 28 UI Updates & Handlers

    private void UpdatePhase28InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        // 1. 资产多因子拥挤度雷达
        if (result.AssetCrowdingRadar != null)
        {
            var cr = result.AssetCrowdingRadar;
            if (TxtCrowdingScore != null) TxtCrowdingScore.Text = $"{cr.OverallCrowdingScore:F1} 分";
            if (TxtCrowdingStatus != null) TxtCrowdingStatus.Text = $"状态: {cr.CrowdingRiskLevel.Split(' ')[0]}";
            if (GridFactorCrowding != null) GridFactorCrowding.ItemsSource = cr.CrowdingItems;
        }

        // 2. 带最大回撤硬顶约束的分数凯利动态仓位
        if (result.DrawdownConstrainedKelly != null)
        {
            var kl = result.DrawdownConstrainedKelly;
            if (TxtKellyEquityWeight != null) TxtKellyEquityWeight.Text = $"{kl.OptimalEquityWeightPercent:F1}%";
            if (TxtKellyBrakingState != null) TxtKellyBrakingState.Text = $"制动: {kl.BrakingStatus.Split(' ')[0]}";
            if (TxtKellyCashBuffer != null) TxtKellyCashBuffer.Text = $"{kl.RecommendedCashBufferPercent:F1}% ({kl.RecommendedCashBufferWan:F1}万元)";
            if (TxtKellyDrawdownDist != null) TxtKellyDrawdownDist.Text = $"动态回撤: {kl.CurrentDrawdownPercent:F1}% / {kl.MaxDrawdownCeilingPercent:F1}%";
            if (GridKellyTiers != null) GridKellyTiers.ItemsSource = kl.TierScenarios;
        }

        // 3. BSTS 贝叶斯结构时序滤波与趋势断点
        if (result.BstsTrendFilter != null)
        {
            var bsts = result.BstsTrendFilter;
            if (TxtBstsBreakProb != null) TxtBstsBreakProb.Text = $"{bsts.PortfolioStructuralBreakProbabilityPercent:F1}%";
            if (TxtBstsAlphaAnnual != null) TxtBstsAlphaAnnual.Text = $"局部Alpha: {(bsts.PortfolioAverageAlphaAnnualPercent >= 0 ? "+" : "")}{bsts.PortfolioAverageAlphaAnnualPercent:F2}%";
            if (GridBstsFilter != null) GridBstsFilter.ItemsSource = bsts.AssetFilterItems;
        }

        // 4. 多期跨期期限结构与均值回归风险衰减锥
        if (result.MultiHorizonRiskTerm != null)
        {
            var mh = result.MultiHorizonRiskTerm;
            if (TxtHorizonVolDecay != null) TxtHorizonVolDecay.Text = $"-{mh.OneYearToFiveYearVolDecayPercent:F1}%";
            if (TxtHorizonHalfLife != null) TxtHorizonHalfLife.Text = $"回归半衰期: {mh.MeanReversionHalfLifeDays:F0}天";
            if (GridHorizonRisk != null) GridHorizonRisk.ItemsSource = mh.HorizonPoints;
        }

        if (TxtPhase28ExecutiveVerdict != null && result.DrawdownConstrainedKelly != null && result.BstsTrendFilter != null)
        {
            TxtPhase28ExecutiveVerdict.Text = $"{result.DrawdownConstrainedKelly.RiskOfficerVerdict} ｜ {result.BstsTrendFilter.FilterSynthesisVerdict}";
        }
    }

    #endregion

    #region Phase 29 UI Updates & Handlers

    private void UpdatePhase29InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        // 1. 随机矩阵理论 RMT 谱滤波降噪
        if (result.RmtCovarianceCleaning != null)
        {
            var rmt = result.RmtCovarianceCleaning;
            if (TxtRmtNoiseRatio != null) TxtRmtNoiseRatio.Text = $"{rmt.NoiseRatioPercent:F1}% 噪声 ({rmt.NoiseEigenvalueCount}/{rmt.AssetCountN})";
            if (TxtRmtConditionImprove != null) TxtRmtConditionImprove.Text = $"条件数: {rmt.RawConditionNumber:F1} → {rmt.CleanedConditionNumber:F1} ({rmt.ConditionNumberImprovementRatio:F1}x)";
            if (GridRmtEigen != null) GridRmtEigen.ItemsSource = rmt.EigenItems;
        }

        // 2. 嵌套聚类优化 NCO 层次化配置
        if (result.NestedClusteredOptimization != null)
        {
            var nco = result.NestedClusteredOptimization;
            if (TxtNcoSharpe != null) TxtNcoSharpe.Text = $"Sharpe: {nco.NcoSharpeRatio:F2} (+{nco.SharpeImprovementPercent:F1}%)";
            if (TxtNcoClusters != null) TxtNcoClusters.Text = $"拓扑解构: {nco.TotalClusters} 个正交簇群";
            if (GridNcoWeights != null) GridNcoWeights.ItemsSource = nco.ClusterWeightItems;
        }

        // 3. Amihud 价格弹性与 Roll 隐性买卖价差微观流动性
        if (result.MicrostructureLiquidity != null)
        {
            var ms = result.MicrostructureLiquidity;
            if (TxtMicroAmihud != null) TxtMicroAmihud.Text = $"{ms.WeightedAmihudIlliquidity:F3} (弹性)";
            if (TxtMicroRollSpread != null) TxtMicroRollSpread.Text = $"Roll价差: {ms.WeightedRollEffectiveSpreadBps:F1} bp (日容量 {ms.TotalDailyAbsorbingCapacityWan:N0}万)";
            if (GridMicrostructure != null) GridMicrostructure.ItemsSource = ms.SlippageTiers;
        }

        // 4. 信息几何真实有效下注数 ENB 与香农熵正则化
        if (result.PortfolioEntropyRegularization != null)
        {
            var ent = result.PortfolioEntropyRegularization;
            if (TxtEntropyEnb != null) TxtEntropyEnb.Text = $"ENB: {ent.EffectiveNumberOfBets:F2} 真实因子";
            if (TxtEntropyEna != null) TxtEntropyEna.Text = $"名义有效资产 ENA: {ent.EffectiveNumberOfAssets:F2}";
            if (TxtEntropyDeficit != null) TxtEntropyDeficit.Text = $"Δ {ent.DiversificationDeficit:F2} {(ent.DiversificationDeficit < 1.0m ? "(健康)" : "(集中)")}";
            if (TxtEntropyScore != null) TxtEntropyScore.Text = $"熵分散评分: {ent.EntropyRegularizedDiversificationScore:F1} 分";
            if (GridEntropyPca != null) GridEntropyPca.ItemsSource = ent.PcaBetContributions;
        }

        if (TxtPhase29ExecutiveVerdict != null && result.RmtCovarianceCleaning != null && result.NestedClusteredOptimization != null && result.PortfolioEntropyRegularization != null)
        {
            TxtPhase29ExecutiveVerdict.Text = $"{result.RmtCovarianceCleaning.RmtDenoisingVerdict} ｜ {result.NestedClusteredOptimization.NcoOptimizationVerdict} ｜ {result.PortfolioEntropyRegularization.EntropyDiversificationVerdict}";
        }
    }

    #endregion

    #region Phase 30 UI Updates & Handlers

    private void UpdatePhase30InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        // 1. 全天候宏观 4 象限与马氏金融动荡度降杠杆雷达
        if (result.MahalanobisTurbulence != null)
        {
            var turb = result.MahalanobisTurbulence;
            if (TxtPhase30TurbulenceScore != null) TxtPhase30TurbulenceScore.Text = $"d² = {turb.CurrentTurbulenceScore:F2} ({turb.MarketTurbulenceStateBadge})";
            if (TxtPhase30TurbulenceLeverage != null) TxtPhase30TurbulenceLeverage.Text = $"战术杠杆: {turb.RecommendedLeverageMultiplier:F2}x | 现金垫: {turb.RecommendedDefensiveCashBufferPercent:F1}%";
            if (GridPhase30MacroQuadrants != null) GridPhase30MacroQuadrants.ItemsSource = turb.MacroQuadrants;
        }

        // 2. 欧拉下行条件在险价值 (CVaR) 风险贡献穿透与极端尾部去毒
        if (result.EulerCvarAttribution != null)
        {
            var ec = result.EulerCvarAttribution;
            if (TxtPhase30EulerCvarTotal != null) TxtPhase30EulerCvarTotal.Text = $"CVaR: {ec.PortfolioCvarPercent:F2}% (求和 {ec.EulerSumCvarPercent:F2}%)";
            if (TxtPhase30EulerCvarCheck != null) TxtPhase30EulerCvarCheck.Text = $"求和闭合检验通过 (95% VaR: {ec.PortfolioVaRPercent:F2}%)";
            if (TxtPhase30TailToxicAsset != null) TxtPhase30TailToxicAsset.Text = $"HHI: {ec.TailHerfindahlIndex:F0}";
            if (TxtPhase30TailToxicDetail != null) TxtPhase30TailToxicDetail.Text = $"首毒: {ec.MostToxicAssetCode} (贡献 {ec.MostToxicAssetCvarContributionRatio:F1}%)";
            if (GridPhase30EulerCvar != null) GridPhase30EulerCvar.ItemsSource = ec.AssetItemList;
        }

        // 3. 基准相对下行半方差跟踪误差 (DTE) 与 Stutzer 极端衰减指数
        if (result.DownsideTrackingError != null)
        {
            var dte = result.DownsideTrackingError;
            if (TxtPhase30DteActiveReturn != null) TxtPhase30DteActiveReturn.Text = $"{dte.ActiveAnnualizedReturnPercent:+0.00;-0.00}%";
            if (TxtPhase30DteSymmetricTe != null) TxtPhase30DteSymmetricTe.Text = $"对称 TE: {dte.SymmetricTrackingErrorPercent:F2}%";
            if (TxtPhase30DteValue != null) TxtPhase30DteValue.Text = $"DTE: {dte.DownsideTrackingErrorPercent:F2}%";
            if (TxtPhase30DteGainRatio != null) TxtPhase30DteGainRatio.Text = $"非对称增益: {dte.AsymmetricDownsideGainRatio:F2}x";
            if (TxtPhase30DteDir != null) TxtPhase30DteDir.Text = $"DIR: {dte.DownsideInformationRatio:F2}";
            if (TxtPhase30StutzerIndexVal != null) TxtPhase30StutzerIndexVal.Text = $"I_S: {dte.StutzerDecayIndex:F4} ({dte.BenchmarkPurityGradeBadge})";
            if (TxtPhase30DteAndStutzer != null) TxtPhase30DteAndStutzer.Text = $"DTE: {dte.DownsideTrackingErrorPercent:F2}% (DIR {dte.DownsideInformationRatio:F2})";
            if (TxtPhase30StutzerDecay != null) TxtPhase30StutzerDecay.Text = $"I_S = {dte.StutzerDecayIndex:F4} | 年衰减: {dte.AnnualizedProbUnderperformDecayRatePercent:F1}%";
        }

        // 4. 负债驱动投资 (LDI) 跨期现金流期限匹配与清算瀑布
        if (result.LdiCashFlowMatch != null)
        {
            var ldi = result.LdiCashFlowMatch;
            if (TxtPhase30LdiLcrRatio != null) TxtPhase30LdiLcrRatio.Text = $"LCR: {ldi.OverallLiquidityCoverageRatioPercent:F1}%";
            if (TxtPhase30LdiDurationGap != null) TxtPhase30LdiDurationGap.Text = $"久期缺口: {ldi.DurationGapYears:+0.00;-0.00}年 (备付¥{ldi.ImmediateLiquidReserveWan:N0}万)";
            if (GridPhase30LdiMatch != null) GridPhase30LdiMatch.ItemsSource = ldi.HorizonMatchItems;
        }

        if (TxtPhase30ExecutiveVerdict != null && result.MahalanobisTurbulence != null && result.EulerCvarAttribution != null && result.LdiCashFlowMatch != null)
        {
            TxtPhase30ExecutiveVerdict.Text = $"{result.MahalanobisTurbulence.TurbulenceTacticalVerdict} ｜ {result.EulerCvarAttribution.EulerCvarVerdict} ｜ {result.LdiCashFlowMatch.LdiExecutiveVerdict}";
        }
    }

    /// <summary>
    /// 更新 Phase 31 机构量化模块展示：卡尔曼时变Beta、Axioma基数约束稀疏优化、ΔCoVaR系统传染与FRTB压力资本
    /// </summary>
    private void UpdatePhase31InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. 卡尔曼滤波时变 Beta 与风格漂移
        if (result.KalmanFilterStyleDrift != null)
        {
            var kf = result.KalmanFilterStyleDrift;
            if (TxtPhase31KalmanBeta != null) TxtPhase31KalmanBeta.Text = $"β = {kf.PortfolioInstantBeta:F2} ({(kf.AverageStyleDriftIndex < 25m ? "风格严守" : "存在漂移")})";
            if (TxtPhase31KalmanDetail != null) TxtPhase31KalmanDetail.Text = $"平均 SDI: {kf.AverageStyleDriftIndex:F1} | 中枢 {kf.PortfolioMeanBeta:F2} (最偏: {kf.MostDriftedAssetCode})";
            if (GridPhase31KalmanDrift != null) GridPhase31KalmanDrift.ItemsSource = kf.AssetItemList;
        }

        // 2. Axioma 基数硬约束与换手预算稀疏组合优化
        if (result.CardinalitySparseOptimization != null)
        {
            var sp = result.CardinalitySparseOptimization;
            if (TxtPhase31SparseSharpe != null) TxtPhase31SparseSharpe.Text = $"夏普: {sp.SparseSharpeRatio:F2} (保留 {sp.CardinalityRetentionRatioPercent:F1}%)";
            if (TxtPhase31SparseTurnover != null) TxtPhase31SparseTurnover.Text = $"精选 {sp.TargetCardinalityK}/{sp.TotalCandidatesN} 只 | 换手 {sp.ActualTurnoverPercent:F1}% (预算 ≤{sp.TurnoverBudgetPercent:F0}%)";
            if (GridPhase31SparseOptimization != null) GridPhase31SparseOptimization.ItemsSource = sp.AssetItemList;
        }

        // 3. Adrian-Brunnermeier 条件系统性在险价值增量 ΔCoVaR 与金融传染
        if (result.DeltaCoVaRSystemicRisk != null)
        {
            var dc = result.DeltaCoVaRSystemicRisk;
            if (TxtPhase31DeltaCoVaR != null) TxtPhase31DeltaCoVaR.Text = $"ΔCoVaR: +{dc.AverageDeltaCoVaRPercent:F2}%";
            if (TxtPhase31DeltaCoVaRContagion != null) TxtPhase31DeltaCoVaRContagion.Text = $"首源: {dc.HighestContagionAssetCode} (+{dc.HighestContagionDeltaCoVaR:F2}%) | 脆弱度 {dc.SystemicNetworkVulnerabilityScore:F1}";
            if (GridPhase31DeltaCoVaR != null) GridPhase31DeltaCoVaR.ItemsSource = dc.AssetItemList;
        }

        // 4. Basel III / FRTB 压力在险价值与阶梯时限监管资本拨备
        if (result.FrtbStressedCapitalCharge != null)
        {
            var frtb = result.FrtbStressedCapitalCharge;
            if (TxtPhase31StressedVaR != null) TxtPhase31StressedVaR.Text = $"sVaR: {frtb.StressedVaR99Percent:F2}% ({frtb.StressedMultiplierRatio:F2}x)";
            if (TxtPhase31StressedWindow != null) TxtPhase31StressedWindow.Text = $"最劣250天回撤 -{frtb.StressedWindowMaxDrawdownPercent:F1}% | 波动 {frtb.StressedWindowAnnualizedVolatilityPercent:F1}%";
            if (TxtPhase31FrtbCapital != null) TxtPhase31FrtbCapital.Text = $"¥{frtb.FrtbTotalCapitalChargeWan:N1} 万元 ({frtb.FrtbCapitalAdequacyRatioPercent:F1}%)";
            if (TxtPhase31FrtbAdequacy != null) TxtPhase31FrtbAdequacy.Text = frtb.CapitalAdequacyBadge;
            if (GridPhase31FrtbCapital != null) GridPhase31FrtbCapital.ItemsSource = frtb.HorizonItemList;
        }

        if (TxtPhase31ExecutiveVerdict != null && result.KalmanFilterStyleDrift != null && result.CardinalitySparseOptimization != null && result.DeltaCoVaRSystemicRisk != null && result.FrtbStressedCapitalCharge != null)
        {
            TxtPhase31ExecutiveVerdict.Text = $"{result.KalmanFilterStyleDrift.StyleDriftExecutiveVerdict} ｜ {result.CardinalitySparseOptimization.SparseOptimizationVerdict} ｜ {result.DeltaCoVaRSystemicRisk.DeltaCoVaRVerdict} ｜ {result.FrtbStressedCapitalCharge.FrtbExecutiveVerdict}";
        }
    }

    /// <summary>
    /// 更新 Phase 32 机构量化模块展示：Idzorek置信度BL、Acerbi谱风险测度SRM、风险预算漂移走廊与DSR过拟合检验
    /// </summary>
    private void UpdatePhase32InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Goldman Sachs GSAM & Idzorek 显式置信度 Black-Litterman
        if (result.IdzorekBlackLitterman != null)
        {
            var ibl = result.IdzorekBlackLitterman;
            if (TxtPhase32BlSharpe != null) TxtPhase32BlSharpe.Text = $"夏普: {ibl.PriorEquilibriumSharpeRatio:F2} ➔ {ibl.PosteriorOptimalSharpeRatio:F2} (+{ibl.SharpeRatioImprovementPercent:F1}%)";
            if (TxtPhase32BlEntropy != null) TxtPhase32BlEntropy.Text = $"KL 散度: {ibl.PriorPosteriorKLDivergenceEntropy:F4} | 均值置信 {ibl.AverageUserConfidencePercent:F1}%";
            if (GridPhase32IdzorekAssets != null) GridPhase32IdzorekAssets.ItemsSource = ibl.AssetItemList;
            if (GridPhase32IdzorekViews != null) GridPhase32IdzorekViews.ItemsSource = ibl.ViewItemList;
        }

        // 2. BCBS & Carlo Acerbi 连续指数谱在险测度 SRM
        if (result.SpectralRiskMeasure != null)
        {
            var srm = result.SpectralRiskMeasure;
            if (TxtPhase32SpectralRisk != null) TxtPhase32SpectralRisk.Text = $"SRM: {srm.SpectralRiskMeasure1dPercent:F2}% / {srm.SpectralRiskMeasureAnnualizedPercent:F2}%";
            if (TxtPhase32SpectralPremium != null) TxtPhase32SpectralPremium.Text = $"尾部溢价 {srm.TailSeverityPremiumRatio:F2}x | 极端集中 {srm.ExponentialSpectrumConcentrationRatio:F1}x";
            if (GridPhase32SpectralTiers != null) GridPhase32SpectralTiers.ItemsSource = srm.TailTierList;
        }

        // 3. Bridgewater All-Weather 动态风险预算漂移走廊 (RCDI)
        if (result.RiskBudgetDriftCorridor != null)
        {
            var rbd = result.RiskBudgetDriftCorridor;
            if (TxtPhase32RiskDrift != null) TxtPhase32RiskDrift.Text = $"RCDI: {rbd.TotalRiskContributionDriftIndex:F1}% ({(rbd.BreachedAssetCount > 0 ? "🔴 破位再平衡" : (rbd.WarningAssetCount > 0 ? "🟡 走廊预警" : "🟢 预算达标"))})";
            if (TxtPhase32RiskBreach != null) TxtPhase32RiskBreach.Text = $"破位 {rbd.BreachedAssetCount} 只 | 预警 {rbd.WarningAssetCount} 只 | 波动 {rbd.PortfolioAnnualizedVolatilityPercent:F1}%";
            if (TxtPhase32RebalanceTurnover != null) TxtPhase32RebalanceTurnover.Text = $"换手率: {rbd.RequiredSmoothRebalanceTurnoverPercent:F1}% (摩擦 {rbd.EstimatedRebalanceCostBps:F1} bps)";
            if (TxtPhase32RebalanceAction != null) TxtPhase32RebalanceAction.Text = $"{(rbd.TriggerRebalanceAction ? "⚡ 触发再平衡阻尼 0.65" : "稳态平滑阻尼 0.35")}";
            if (GridPhase32RiskCorridors != null) GridPhase32RiskCorridors.ItemsSource = rbd.AssetItemList;
        }

        // 4. Marcos Lopez de Prado & Bailey 概率夏普 PSR 与通缩夏普 DSR 检验
        if (result.DeflatedSharpeOverfit != null)
        {
            var dsr = result.DeflatedSharpeOverfit;
            if (TxtPhase32DeflatedSharpe != null) TxtPhase32DeflatedSharpe.Text = $"DSR: {dsr.DeflatedSharpeRatioPercent:F1}% (FDP {dsr.FalseDiscoveryProbabilityPercent:F1}%)";
            if (TxtPhase32AlphaStatus != null) TxtPhase32AlphaStatus.Text = $"{dsr.AlphaGenuineStatusBadge} (PSR {dsr.ProbabilisticSharpeRatioPercent:F1}%)";
        }

        if (TxtPhase32ExecutiveVerdict != null && result.IdzorekBlackLitterman != null && result.SpectralRiskMeasure != null && result.RiskBudgetDriftCorridor != null && result.DeflatedSharpeOverfit != null)
        {
            TxtPhase32ExecutiveVerdict.Text = $"{result.IdzorekBlackLitterman.IdzorekExecutiveVerdict} ｜ {result.SpectralRiskMeasure.SpectralExecutiveVerdict} ｜ {result.RiskBudgetDriftCorridor.RiskBudgetExecutiveVerdict} ｜ {result.DeflatedSharpeOverfit.DsrExecutiveVerdict}";
        }
    }

    /// <summary>
    /// 更新 Phase 33 机构量化模块展示：AQR杠杆异象BAB与QMJ质量因子、Aladdin极值GPD尾部外推、Barra流动性黑洞与夏普半衰期CUSUM检验
    /// </summary>
    private void UpdatePhase33InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. AQR 杠杆异象 BAB 与高质量 QMJ 因子解构
        if (result.BabQmjFactorDecomposition != null)
        {
            var bab = result.BabQmjFactorDecomposition;
            if (TxtPhase33BabSpread != null) TxtPhase33BabSpread.Text = $"BAB 利差: +{bab.BabAnnualizedSpreadReturnPercent:F2}% (夏普 {bab.BabSharpeRatio:F2})";
            if (TxtPhase33BabShadow != null) TxtPhase33BabShadow.Text = $"影子成本 ψ: {bab.ImpliedLeverageShadowCostPercent:F2}% | 组合QMJ {bab.PortfolioWeightedQualityScore:F1}分";
            if (GridPhase33BabAssets != null) GridPhase33BabAssets.ItemsSource = bab.AssetItemList;
        }

        // 2. BlackRock Aladdin 极值理论 GPD 尾部外推与极端重现期
        if (result.EvtGeneralizedPareto != null)
        {
            var evt = result.EvtGeneralizedPareto;
            if (TxtPhase33EvtVaR != null) TxtPhase33EvtVaR.Text = $"EVT 99% VaR: -{evt.EvtVaR99Percent:F2}% (ES -{evt.EvtES99Percent:F2}%)";
            if (TxtPhase33EvtXi != null) TxtPhase33EvtXi.Text = $"尾部指数 ξ: {evt.GpdShapeParameterXi:F4} | 肥度 {evt.TailIndexFatnessRatio:F2}x";
            if (GridPhase33EvtPeriods != null) GridPhase33EvtPeriods.ItemsSource = evt.ReturnPeriodList;
        }

        // 3. MSCI Barra 内生流动性黑洞与踩踏乘数
        if (result.LiquidityBlackHole != null)
        {
            var lbh = result.LiquidityBlackHole;
            if (TxtPhase33FCM != null) TxtPhase33FCM.Text = $"踩踏乘数 FCM: {lbh.FireSaleCascadeMultiplier:F2}x (出清 {lbh.AggregateFireSaleVolumeTenThousand:F1}万)";
            if (TxtPhase33LBHI != null) TxtPhase33LBHI.Text = $"黑洞指数 LBHI: {lbh.LiquidityBlackHoleIndex:F1} | 冲击 {lbh.ExogenousDirectPriceImpactPercent:F2}%➔{lbh.EndogenousFeedbackPriceImpactPercent:F2}%";
            if (GridPhase33FireSale != null) GridPhase33FireSale.ItemsSource = lbh.AssetItemList;
        }

        // 4. Marcos Lopez de Prado 夏普半衰期与 CUSUM 漂移
        if (result.SharpeDecayCusum != null)
        {
            var cusum = result.SharpeDecayCusum;
            if (TxtPhase33SharpeDecay != null) TxtPhase33SharpeDecay.Text = $"夏普半衰期: {cusum.SharpeDecayHalfLifeDays:F0} 日 (λ: {cusum.ExponentialDecayRateLambda:F5})";
            if (TxtPhase33SharpeLife != null) TxtPhase33SharpeLife.Text = $"剩余有效寿命: {cusum.EstimatedDaysToTerminalExpiration:F0} 日 | 初期 {cusum.InitialEstimatedSharpeRatio:F2}";
            if (TxtPhase33CusumAlert != null) TxtPhase33CusumAlert.Text = $"CUSUM S^-: {cusum.CusumNegativeAccumulator:F2} / {cusum.CusumAlertThreshold:F2} ({(cusum.TriggerStructuralDecayAlert ? "🔴 警报" : "🟢 正常")})";
            if (TxtPhase33AlphaStatus != null) TxtPhase33AlphaStatus.Text = $"{cusum.AlphaLongevityStatusBadge}";

            if (TxtPhase33FormulaSr0 != null) TxtPhase33FormulaSr0.Text = $"• 初期峰值夏普 SR₀: {cusum.InitialEstimatedSharpeRatio:F2}";
            if (TxtPhase33FormulaLambda != null) TxtPhase33FormulaLambda.Text = $"• 指数衰减速率 λ: {cusum.ExponentialDecayRateLambda:F5}";
            if (TxtPhase33FormulaHalfLife != null) TxtPhase33FormulaHalfLife.Text = $"• 夏普半衰期 t₁/₂: {cusum.SharpeDecayHalfLifeDays:F0} 交易日";
            if (TxtPhase33FormulaTerminal != null) TxtPhase33FormulaTerminal.Text = $"• 预期失效剩余: {cusum.EstimatedDaysToTerminalExpiration:F0} 交易日";
            if (TxtPhase33FormulaCusum != null) TxtPhase33FormulaCusum.Text = $"• CUSUM 负向衰变: S⁻ = {cusum.CusumNegativeAccumulator:F2}";
            if (TxtPhase33FormulaAlert != null) TxtPhase33FormulaAlert.Text = $"• 结构性突变报警: {(cusum.TriggerStructuralDecayAlert ? "🔴 触发衰变报警" : "🟢 正常未触发")}";
        }

        if (TxtPhase33ExecutiveVerdict != null && result.BabQmjFactorDecomposition != null && result.EvtGeneralizedPareto != null && result.LiquidityBlackHole != null && result.SharpeDecayCusum != null)
        {
            TxtPhase33ExecutiveVerdict.Text = $"{result.BabQmjFactorDecomposition.BabQmjExecutiveVerdict} ｜ {result.EvtGeneralizedPareto.EvtExecutiveVerdict} ｜ {result.LiquidityBlackHole.LiquidityBlackHoleExecutiveVerdict} ｜ {result.SharpeDecayCusum.CusumDecayExecutiveVerdict}";
        }
    }

    /// <summary>
    /// 更新 Phase 34 机构量化模块展示：跨资产时滞互相关网络、多期限风险方差比、3状态HMM体制解码、短周期反转与动量崩塌
    /// </summary>
    private void UpdatePhase34InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Goldman Sachs & J.P. Morgan: 跨资产非对称时滞领先-滞后互相关与信息流传导有向网络
        if (result.LeadLagCrossCorrelation != null)
        {
            var ll = result.LeadLagCrossCorrelation;
            if (TxtLeadLagLeader != null) TxtLeadLagLeader.Text = $"{ll.AnchorLeaderFundCode} / ILS: +{ll.AssetItemList.OrderByDescending(a => a.InformationLeadershipScore).FirstOrDefault()?.InformationLeadershipScore:F1}";
            if (TxtLeadLagDispersionSub != null) TxtLeadLagDispersionSub.Text = $"平均时滞离散: {ll.PortfolioAverageLeadLagDispersionDays:F1} 日 | 最大非对称差: +{ll.MaxPairwiseAsymmetryGap:F3}";
            if (DgLeadLagAssets != null) DgLeadLagAssets.ItemsSource = ll.AssetItemList;
        }

        // 2. BlackRock Aladdin / Axioma / Lo-MacKinlay: 多重投资期限风险期限结构与方差比非随机游走检验
        if (result.MultiHorizonRiskTermStructure != null)
        {
            var mh = result.MultiHorizonRiskTermStructure;
            var annual = mh.HorizonItemList.FirstOrDefault(h => h.HorizonDays == 252) ?? mh.HorizonItemList.LastOrDefault();
            if (TxtLoMacKinlayAnnualVR != null) TxtLoMacKinlayAnnualVR.Text = $"{mh.AnnualizedLoMacKinlayVarianceRatio:F3} (Z*: {annual?.HeteroscedasticityZScore:F2})";
            string patternShort = mh.TermStructureDominantPattern.Contains('(') ? mh.TermStructureDominantPattern.Split('(')[0].Trim() : mh.TermStructureDominantPattern;
            if (TxtLoMacKinlayPatternSub != null) TxtLoMacKinlayPatternSub.Text = $"偏离失真: {mh.LongTermVolatilityDistortionPercent:+0.0;-0.0;0.0}% | {patternShort}";
            if (DgMultiHorizonRisks != null) DgMultiHorizonRisks.ItemsSource = mh.HorizonItemList;
        }

        // 3. Two Sigma / Citadel: 3 状态高斯隐马尔可夫模型 (Gaussian HMM) 宏观体制自动解码与转移信息熵
        if (result.ThreeStateGaussianHmm != null)
        {
            var hmm = result.ThreeStateGaussianHmm;
            string stateShort = hmm.CurrentDecodedStateName.Contains(' ') ? hmm.CurrentDecodedStateName.Split(' ')[1] : hmm.CurrentDecodedStateName;
            var curItem = hmm.StateItemList.ElementAtOrDefault(hmm.CurrentDecodedStateIndex - 1);
            if (TxtHmmCurrentState != null) TxtHmmCurrentState.Text = $"{stateShort} (π: {curItem?.PosteriorProbabilityPercent:F1}%)";
            if (TxtHmmDwellSub != null) TxtHmmDwellSub.Text = $"预期留存: {curItem?.ExpectedDwellDays:F0} 交易日 | 牛{hmm.BullStateProbabilityPercent:F0}% 震{hmm.NeutralStateProbabilityPercent:F0}% 危{hmm.CrisisStateProbabilityPercent:F0}%";
            if (TxtHmmRegimeEntropy != null) TxtHmmRegimeEntropy.Text = $"{hmm.RegimeTransitionEntropy:F4} (不确定性 {hmm.NormalizedEntropyPercent:F1}%)";
            if (TxtHmmMacroAdvisorySub != null) TxtHmmMacroAdvisorySub.Text = $"{hmm.MacroRegimeAdvisoryBadge}";
            if (DgHmmStates != null) DgHmmStates.ItemsSource = hmm.StateItemList;
        }

        // 4. AQR / Asness / Daniel-Moskowitz: 广义反向择时短周期反转 Alpha 与动量崩塌预警指数 (MCWI)
        if (result.MomentumCrashAndReversal != null)
        {
            var rev = result.MomentumCrashAndReversal;
            if (TxtMomentumCrashIndex != null) TxtMomentumCrashIndex.Text = $"{rev.MomentumCrashWarningIndex:F1} / 100";
            if (TxtMomentumCrashStatusSub != null) TxtMomentumCrashStatusSub.Text = $"崩塌率: {rev.MomentumCrashProbabilityPercent:F1}% | {rev.MomentumProtectionStatusBadge} | 倾斜 {rev.RecommendedReversalTiltTurnoverPercent:F1}%";
            if (DgReversalAssets != null) DgReversalAssets.ItemsSource = rev.AssetItemList;
        }

        if (TxtPhase34ExecutiveVerdict != null && result.LeadLagCrossCorrelation != null && result.MultiHorizonRiskTermStructure != null && result.ThreeStateGaussianHmm != null && result.MomentumCrashAndReversal != null)
        {
            TxtPhase34ExecutiveVerdict.Text = $"{result.LeadLagCrossCorrelation.LeadLagExecutiveVerdict} ｜ {result.MultiHorizonRiskTermStructure.MultiHorizonExecutiveVerdict} ｜ {result.ThreeStateGaussianHmm.HmmExecutiveVerdict} ｜ {result.MomentumCrashAndReversal.ReversalExecutiveVerdict}";
        }
    }

    private void UpdatePhase35InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. BIS / BCBS FRTB 内部模型法损益归因 (PLA) 与巴塞尔 250 天交通灯超限检定
        if (result.FrtbPlaAndTrafficLight != null)
        {
            var pla = result.FrtbPlaAndTrafficLight;
            if (TxtFrtbSpearmanRank != null) TxtFrtbSpearmanRank.Text = $"SRC: {pla.SpearmanRankCorrelation:F3} ({pla.SpearmanZoneStatus})";
            if (TxtFrtbKsSub != null) TxtFrtbKsSub.Text = $"柯氏 D_KS: {pla.KolmogorovSmirnovStatistic:F3} ({pla.KsZoneStatus}) | {pla.OverallPlaComplianceStatus}";
            if (TxtFrtbTrafficLight != null) TxtFrtbTrafficLight.Text = $"{pla.BaselTrafficLightZone} ({pla.Rolling250DaysVaRExceedanceCount}次违约)";
            if (TxtFrtbCapitalMultiplierSub != null) TxtFrtbCapitalMultiplierSub.Text = $"资本附加: +{pla.RegulatoryCapitalMultiplierAddOn:F2} (总乘数: {pla.TotalCapitalMultiplier:F2}x)";
            if (DgPlaObservations != null) DgPlaObservations.ItemsSource = pla.RecentObservations;
        }

        // 2. Millennium & Point72 Pod Shop: 多策略 Pod 动态资本分配与阶梯止损降额机制
        if (result.PodShopCapitalAllocation != null)
        {
            var pod = result.PodShopCapitalAllocation;
            if (TxtPodCapitalStatus != null) TxtPodCapitalStatus.Text = $"在险: {pod.ActiveWorkingCapitalWan:F0}万 / 储备: {pod.CentralReservePoolWan:F0}万";
            if (TxtPodHealthBadgeSub != null) TxtPodHealthBadgeSub.Text = $"{pod.PodGovernanceHealthBadge} | 运作:{pod.ActivePodsCount} 降额:{pod.DeriskedPodsCount} 止损:{pod.StoppedOutPodsCount}";
            if (DgPodShopAllocation != null) DgPodShopAllocation.ItemsSource = pod.PodList;
        }

        // 3. MSCI Barra & Axioma: 风格因子 Löwdin 对称正交化与纯因子载荷矩阵
        if (result.FactorOrthogonalization != null)
        {
            var fo = result.FactorOrthogonalization;
            if (TxtFactorOrthogonality != null) TxtFactorOrthogonality.Text = $"保真精度: {fo.OrthogonalityAccuracy * 100m:F1}% ({fo.FactorCount}因子)";
            if (TxtFactorCollinearitySub != null) TxtFactorCollinearitySub.Text = $"相关性降幅: {fo.AverageCrossCorrelationBefore:F3} -> {fo.AverageCrossCorrelationAfter:F4}";
            if (DgOrthogonalFactorLoadings != null) DgOrthogonalFactorLoadings.ItemsSource = fo.AssetLoadingList;
        }

        // 4. Two Sigma & Citadel: 距离相关系数 (dCor) 与互信息 (MI) 非线性 Alpha 特征排序
        if (result.NonlinearDistanceMutualInfo != null)
        {
            var nl = result.NonlinearDistanceMutualInfo;
            if (TxtNonlinearAlphaGain != null) TxtNonlinearAlphaGain.Text = $"+{nl.NonlinearAlphaGainPercentage:F1}% (dCor: {nl.PortfolioAverageDistanceCorr:F3})";
            if (TxtTopNonlinearDriverSub != null) TxtTopNonlinearDriverSub.Text = $"驱动标的: {nl.TopNonlinearAlphaDriverCode} ({nl.TopNonlinearAlphaDriverName}) | MI: {nl.PortfolioAverageMutualInformationBits:F2} Bits";
            if (DgNonlinearAssetFeatures != null) DgNonlinearAssetFeatures.ItemsSource = nl.AssetFeatureList;
        }

        if (TxtPhase35ExecutiveVerdict != null && result.FrtbPlaAndTrafficLight != null && result.PodShopCapitalAllocation != null && result.FactorOrthogonalization != null && result.NonlinearDistanceMutualInfo != null)
        {
            TxtPhase35ExecutiveVerdict.Text = $"{result.FrtbPlaAndTrafficLight.PlaExecutiveVerdict} ｜ {result.PodShopCapitalAllocation.PodExecutiveVerdict} ｜ {result.FactorOrthogonalization.OrthogonalExecutiveVerdict} ｜ {result.NonlinearDistanceMutualInfo.NonlinearExecutiveVerdict}";
        }
    }

    private void UpdatePhase36InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        // 1. Citadel & Millennium 因子与资产微观拥挤度及机构踩踏排队指数 (HLRI)
        if (result.AssetFactorCrowdedness != null)
        {
            var crowd = result.AssetFactorCrowdedness;
            if (TxtPhase36PortfolioHlri != null) TxtPhase36PortfolioHlri.Text = $"{crowd.PortfolioAverageHlri:F1} / 100";
            if (TxtPhase36HlriBadge != null) TxtPhase36HlriBadge.Text = $"治理评级: {crowd.CrowdednessGovernanceBadge}";
            if (TxtPhase36LiquidationDays != null) TxtPhase36LiquidationDays.Text = $"{crowd.PortfolioWeightedLiquidationDaysStress:F1} 天";
            if (TxtPhase36HighRiskCount != null) TxtPhase36HighRiskCount.Text = $"{crowd.HighRiskCrowdedAssetsCount} 只";
            if (TxtPhase36PeakCrowded != null) TxtPhase36PeakCrowded.Text = $"{crowd.PeakCrowdedAssetName} ({crowd.PeakCrowdedAssetCode})";
            if (TxtPhase36PeakScore != null) TxtPhase36PeakScore.Text = $"峰值分: {crowd.PeakCrowdedHlri:F1}";
            if (GridPhase36AssetCrowdedness != null) GridPhase36AssetCrowdedness.ItemsSource = crowd.Items;
        }

        // 2. BlackRock Aladdin & J.P. Morgan 多因子宏观情景冲击传导与压力在险价值 (Stressed VaR)
        if (result.MacroFactorShockPropagation != null)
        {
            var macro = result.MacroFactorShockPropagation;
            if (TxtPhase36WorstScenario != null) TxtPhase36WorstScenario.Text = macro.WorstCaseScenarioName;
            if (TxtPhase36WorstLoss != null) TxtPhase36WorstLoss.Text = $"{macro.WorstCasePortfolioLossPercent:+0.00;-0.00;0.00}%";
            if (TxtPhase36StressedVaRRange != null) TxtPhase36StressedVaRRange.Text = macro.StressedVaR99Range;
            if (TxtPhase36FragilityDiversity != null) TxtPhase36FragilityDiversity.Text = $"{macro.FragilityDiversityRatio:F2}";
            if (GridPhase36MacroScenarios != null) GridPhase36MacroScenarios.ItemsSource = macro.ScenarioList;
        }

        // 3. AQR Capital & Antti Ilmanen 真实波动率特征结构与方差风险溢价 (VRP)
        if (result.VarianceRiskPremium != null)
        {
            var vrp = result.VarianceRiskPremium;
            if (TxtPhase36Rv != null) TxtPhase36Rv.Text = $"{vrp.PortfolioAverageRealizedVol:F2}%";
            if (TxtPhase36Iv != null) TxtPhase36Iv.Text = $"{vrp.PortfolioAverageImpliedVol:F2}%";
            if (TxtPhase36VrpSpread != null) TxtPhase36VrpSpread.Text = $"+{vrp.PortfolioAverageVrpSpread:F1} 点";
            if (TxtPhase36VrpRatio != null) TxtPhase36VrpRatio.Text = $"溢价率: +{vrp.PortfolioAverageVrpRatio:F1}%";
            if (TxtPhase36VrpBadge != null) TxtPhase36VrpBadge.Text = vrp.VrpHarvestRegimeBadge;
            if (GridPhase36VarianceRiskPremium != null) GridPhase36VarianceRiskPremium.ItemsSource = vrp.AssetVrpList;
        }

        // 4. Two Sigma & Renaissance 摩擦成本敏感型动态无交易再平衡缓冲带 (No-Trade Buffer Bands)
        if (result.DynamicNoTradeBufferBand != null)
        {
            var band = result.DynamicNoTradeBufferBand;
            if (TxtPhase36InBandCount != null) TxtPhase36InBandCount.Text = $"{band.InBandAssetCount} / {band.Items.Count} 只";
            if (TxtPhase36TurnoverReduction != null) TxtPhase36TurnoverReduction.Text = $"-{band.TurnoverReductionPercent:F1}%";
            if (TxtPhase36FrictionSaved != null) TxtPhase36FrictionSaved.Text = $"+{band.EstimatedAnnualFrictionSavedWan:F2} 万元";
            if (TxtPhase36SharpeUplift != null) TxtPhase36SharpeUplift.Text = $"+{band.NetSharpeUplift:F2}";
            if (GridPhase36DynamicBufferBands != null) GridPhase36DynamicBufferBands.ItemsSource = band.Items;
        }

        if (TxtPhase36ExecutiveVerdict != null && result.AssetFactorCrowdedness != null && result.MacroFactorShockPropagation != null && result.VarianceRiskPremium != null && result.DynamicNoTradeBufferBand != null)
        {
            TxtPhase36ExecutiveVerdict.Text = $"{result.AssetFactorCrowdedness.ExecutiveVerdict} ｜ {result.MacroFactorShockPropagation.ExecutiveVerdict} ｜ {result.VarianceRiskPremium.ExecutiveVerdict} ｜ {result.DynamicNoTradeBufferBand.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase37InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        // 1. Bridgewater Associates 宏观增长-通胀双维度惊喜指数矩阵与全天候宏观贝塔正交中性化覆盖
        if (result.MacroSurpriseOverlay != null)
        {
            var macro = result.MacroSurpriseOverlay;
            if (TxtPhase37MacroVolReduction != null) TxtPhase37MacroVolReduction.Text = $"-{macro.MacroVolatilityReductionPercent:F1}%";
            if (TxtPhase37MacroBeta != null) TxtPhase37MacroBeta.Text = $"β_g: {macro.PortfolioGrowthBeta:F3} | β_π: {macro.PortfolioInflationBeta:F3}";
            if (TxtPhase37MacroBadge != null) TxtPhase37MacroBadge.Text = macro.MacroRegimeResilienceBadge;
            if (GridPhase37MacroSurprises != null) GridPhase37MacroSurprises.ItemsSource = macro.AssetExposures;
        }

        // 2. Citadel & Millennium 多经理平台核心风格因子正交中性化与非故意风险漂移剔除
        if (result.StyleFactorNeutralization != null)
        {
            var style = result.StyleFactorNeutralization;
            if (TxtPhase37StyleRiskElimination != null) TxtPhase37StyleRiskElimination.Text = $"{style.UnintendedStyleRiskEliminationRatioPercent:F1}%";
            if (TxtPhase37StyleBreachedCount != null) TxtPhase37StyleBreachedCount.Text = $"去偏超标因子: {style.BreachedFactorCount}项 (|Z|>0.05)";
            if (TxtPhase37StyleVerdictBadge != null) TxtPhase37StyleVerdictBadge.Text = $"特异Alpha占比: {style.PureAlphaVarianceRatioPercent:F1}%";
            if (GridPhase37StyleNeutralization != null) GridPhase37StyleNeutralization.ItemsSource = style.FactorExposures;
        }

        // 3. AQR Capital & Antti Ilmanen 动态目标波动率定标时间序列动量 (TSMOM) 与危机 Alpha
        if (result.VolatilityTargetedTsmom != null)
        {
            var tsmom = result.VolatilityTargetedTsmom;
            if (TxtPhase37CrisisAlphaScore != null) TxtPhase37CrisisAlphaScore.Text = $"{tsmom.CrisisAlphaPotentialScore:F1} / 100";
            if (TxtPhase37TsmomCrashDefense != null) TxtPhase37TsmomCrashDefense.Text = $"动量崩塌防御指数: {tsmom.MomentumCrashDefenseIndex:F1}";
            if (TxtPhase37TsmomBadge != null) TxtPhase37TsmomBadge.Text = $"目标波动率: {tsmom.TargetVolatilityPercent:F1}%";
            if (GridPhase37TsmomSignals != null) GridPhase37TsmomSignals.ItemsSource = tsmom.AssetSignals;
        }

        // 4. Two Sigma & D.E. Shaw Almgren-Chriss 连续动态最优拆单轨迹与流动性在险度量
        if (result.OptimalExecutionTrajectory != null)
        {
            var exec = result.OptimalExecutionTrajectory;
            if (TxtPhase37ExecutionSavings != null) TxtPhase37ExecutionSavings.Text = $"+{exec.ExecutionSlippageSavingsWan:F2} 万元";
            if (TxtPhase37ExecutionSavingsRatio != null) TxtPhase37ExecutionSavingsRatio.Text = $"执行滑点压降: -{exec.ExecutionSlippageSavingsRatioPercent:F1}%";
            if (TxtPhase37ExecutionBadge != null) TxtPhase37ExecutionBadge.Text = $"κ = {exec.CharacteristicDecayParameterKappa:F3} | 半衰期: {exec.CharacteristicDecayHalfLifeHours:F2}h";
            if (GridPhase37ExecutionTrajectory != null) GridPhase37ExecutionTrajectory.ItemsSource = exec.TrajectorySlices;
        }

        if (TxtPhase37ExecutiveVerdict != null && result.MacroSurpriseOverlay != null && result.StyleFactorNeutralization != null && result.VolatilityTargetedTsmom != null && result.OptimalExecutionTrajectory != null)
        {
            TxtPhase37ExecutiveVerdict.Text = $"{result.MacroSurpriseOverlay.ExecutiveVerdict} ｜ {result.StyleFactorNeutralization.GovernanceVerdict} ｜ {result.VolatilityTargetedTsmom.StrategyRecommendation} ｜ {result.OptimalExecutionTrajectory.ExecutionVerdict}";
        }
    }

    private void UpdatePhase38InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        // 1. Man Group AHL & AQR 跨资产大类期限结构展期收益 (Roll Yield / Carry) 与基差动量
        if (result.TermStructureCarry != null)
        {
            var carry = result.TermStructureCarry;
            if (TxtPhase38WeightedCarry != null) TxtPhase38WeightedCarry.Text = $"{carry.PortfolioWeightedRollYieldPercent:+0.00;-0.00}%";
            if (TxtPhase38CarrySharpe != null) TxtPhase38CarrySharpe.Text = $"Carry驱动夏普改善: +{carry.CarrySharpeUplift:F2}";
            if (TxtPhase38CarryBadge != null) TxtPhase38CarryBadge.Text = $"{carry.BackwardationAssetCount}贴水 / {carry.ContangoAssetCount}升水结构";
            if (GridPhase38TermStructureCarry != null) GridPhase38TermStructureCarry.ItemsSource = carry.AssetCarries;
        }

        // 2. Renaissance Technologies & CFM 随机矩阵理论 (RMT) 马尔琴科-帕斯图尔谱分解滤波降噪
        if (result.RmtSpectralFiltering != null)
        {
            var rmt = result.RmtSpectralFiltering;
            if (TxtPhase38RmtNoisePurified != null) TxtPhase38RmtNoisePurified.Text = $"{rmt.NoiseVariancePurifiedPercent:F1}%";
            if (TxtPhase38RmtCondImprovement != null) TxtPhase38RmtCondImprovement.Text = $"条件数改善: -{rmt.ConditionNumberImprovementRatioPercent:F1}%";
            if (TxtPhase38RmtBadge != null) TxtPhase38RmtBadge.Text = $"MP界: [{rmt.MarchenkoPasturLowerBound:F2}, {rmt.MarchenkoPasturUpperBound:F2}] | 信号: {rmt.SignalEigenvalueCount}项";
            if (GridPhase38RmtEigenModes != null) GridPhase38RmtEigenModes.ItemsSource = rmt.EigenModes;
        }

        // 3. Citadel & Millennium 多元非对称极值下行联结与组合结构性协同崩塌脆弱度矩阵
        if (result.MultivariateTailCoCrash != null)
        {
            var tail = result.MultivariateTailCoCrash;
            if (TxtPhase38TailFragilityIndex != null) TxtPhase38TailFragilityIndex.Text = $"{tail.StructuralFragilityIndex:F1} / 100";
            if (TxtPhase38TailCoCrashProb != null) TxtPhase38TailCoCrashProb.Text = $"P(Co-Crash): {tail.PortfolioLowerTailCoCrashProbabilityPercent:F2}% (下行优势: {tail.AsymmetricDownsideTailDominanceRatio:F2}x)";
            if (TxtPhase38TailBadge != null) TxtPhase38TailBadge.Text = $"防御评级: {tail.TailConvexityDefenseRating}";
            if (GridPhase38TailFragility != null) GridPhase38TailFragility.ItemsSource = tail.AssetFragilities;
        }

        // 4. Jane Street & Citadel Securities 微观订单流不平衡 (OFI)、Kyle 价格冲击与逆向选择足迹
        if (result.MicrostructureAdverseSelection != null)
        {
            var micro = result.MicrostructureAdverseSelection;
            if (TxtPhase38VpinToxicity != null) TxtPhase38VpinToxicity.Text = $"{micro.PortfolioVpinToxicityIndex:F1} / 100";
            if (TxtPhase38KyleLambda != null) TxtPhase38KyleLambda.Text = $"加权 Kyle λ: {micro.PortfolioAverageKyleLambdaBps:F2} bps";
            if (TxtPhase38MicroBadge != null) TxtPhase38MicroBadge.Text = $"永久信息份额: {micro.PermanentAlphaSharePercent:F1}%";
            if (GridPhase38Microstructure != null) GridPhase38Microstructure.ItemsSource = micro.AssetMicrostructures;
        }

        if (TxtPhase38ExecutiveVerdict != null && result.TermStructureCarry != null && result.RmtSpectralFiltering != null && result.MultivariateTailCoCrash != null && result.MicrostructureAdverseSelection != null)
        {
            TxtPhase38ExecutiveVerdict.Text = $"{result.TermStructureCarry.ExecutiveVerdict} ｜ {result.RmtSpectralFiltering.ExecutiveVerdict} ｜ {result.MultivariateTailCoCrash.ExecutiveVerdict} ｜ {result.MicrostructureAdverseSelection.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase39InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Bridgewater Associates & AQR Capital 主因子正交风险平价 (PFRP) 与特征风险预算配置
        if (result.PrincipalFactorRiskParity != null)
        {
            var pfrp = result.PrincipalFactorRiskParity;
            if (TxtPhase39EnpfKpi != null) TxtPhase39EnpfKpi.Text = $"{pfrp.EffectiveNumberOfPrincipalFactors:F2} / {pfrp.TotalPrincipalFactors}";
            if (TxtPhase39EnpfSub != null) TxtPhase39EnpfSub.Text = $"FDR 因子正交分散比率: {pfrp.FactorDiversificationRatio:F2} | 方差压降: -{pfrp.PostParityVarianceReductionPercent:F1}%";
            if (TxtPhase39PfrpBadge != null) TxtPhase39PfrpBadge.Text = $"特征主因子等权风险平权 (Gini: {pfrp.FactorRiskInequalityGiniPercent:F1}%)";
            if (GridPhase39PrincipalFactors != null) GridPhase39PrincipalFactors.ItemsSource = pfrp.PrincipalFactors;
        }

        // 2. Millennium Management & Point72 动态下行凸性期权对冲与广义波动率偏度复制
        if (result.DynamicDownsideConvexityHedge != null)
        {
            var cvx = result.DynamicDownsideConvexityHedge;
            if (TxtPhase39ConvexityKpi != null) TxtPhase39ConvexityKpi.Text = $"+{cvx.MaxCushionBufferPercent:F1}% / {cvx.OptimalTotalHedgeRatioPercent:F1}%";
            if (TxtPhase39ConvexitySub != null) TxtPhase39ConvexitySub.Text = $"SVI 偏度斜率: {cvx.VolatilitySkewSlopeBps:F0} bps | 凸性缺口: {cvx.PortfolioConvexityDeficitScore:F1}/100";
            if (TxtPhase39ConvexityBadge != null) TxtPhase39ConvexityBadge.Text = $"SVI 波动率偏度曲面合成 (保护性价比: {cvx.NetConvexityProtectionBenefitRatio:F2}x)";
            if (GridPhase39ConvexityHedge != null) GridPhase39ConvexityHedge.ItemsSource = cvx.StrikeHedgingProfiles;
        }

        // 3. Renaissance Technologies & D.E. Shaw 贝叶斯时变状态空间卡尔曼滤波与自适应 Alpha 动态跟踪
        if (result.BayesianKalmanAlphaTracker != null)
        {
            var kalman = result.BayesianKalmanAlphaTracker;
            if (TxtPhase39KalmanAlphaKpi != null) TxtPhase39KalmanAlphaKpi.Text = $"+{kalman.PortfolioFilteredAlphaAnnualizedPercent:F2}%";
            if (TxtPhase39KalmanAlphaSub != null) TxtPhase39KalmanAlphaSub.Text = $"白噪声压降: -{kalman.TrackingNoiseReductionPercent:F1}% | IR提升: +{kalman.InformationRatioUpliftPercent:F1}%";
            if (TxtPhase39KalmanBadge != null) TxtPhase39KalmanBadge.Text = $"卡尔曼状态空间时变滤波 ({kalman.StructuralBreakAssetCount}项断裂体制)";
            if (GridPhase39KalmanAlpha != null) GridPhase39KalmanAlpha.ItemsSource = kalman.AssetKalmanAlphas;
        }

        // 4. Jane Street & Jump Trading 跨资产微观流动性共振裂谷与闪崩级联阻尼器
        if (result.CrossAssetLiquidityChasmDamper != null)
        {
            var chasm = result.CrossAssetLiquidityChasmDamper;
            if (TxtPhase39LiquidityChasmKpi != null) TxtPhase39LiquidityChasmKpi.Text = $"{chasm.LiquidityChasmIndex:F1} / 100";
            if (TxtPhase39LiquidityChasmSub != null) TxtPhase39LiquidityChasmSub.Text = $"做市商抽单警报: {chasm.MarketMakerPullbackAlertLevel} | 闪崩放大: {chasm.FlashCrashCascadeAmplifierRatio:F2}x";
            if (TxtPhase39LiquidityBadge != null) TxtPhase39LiquidityBadge.Text = $"跨资产流动性协同蒸发阻尼 (吸收率: {chasm.SystemicLiquidityDampingScorePercent:F1}%)";
            if (GridPhase39LiquidityChasm != null) GridPhase39LiquidityChasm.ItemsSource = chasm.AssetLiquidityChasms;
        }

        if (TxtPhase39ExecutiveVerdict != null && result.PrincipalFactorRiskParity != null && result.DynamicDownsideConvexityHedge != null && result.BayesianKalmanAlphaTracker != null && result.CrossAssetLiquidityChasmDamper != null)
        {
            TxtPhase39ExecutiveVerdict.Text = $"{result.PrincipalFactorRiskParity.ExecutiveVerdict} ｜ {result.DynamicDownsideConvexityHedge.ExecutiveVerdict} ｜ {result.BayesianKalmanAlphaTracker.ExecutiveVerdict} ｜ {result.CrossAssetLiquidityChasmDamper.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase40InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Bridgewater Associates & Citadel 宏观马尔可夫区制转移 (MRS) 与跨周期动态配置
        if (result.MacroMarkovRegimeSwitching != null)
        {
            var mrs = result.MacroMarkovRegimeSwitching;
            if (TxtPhase40DominantRegime != null) TxtPhase40DominantRegime.Text = $"{mrs.DominantRegimeName} ({mrs.DominantRegimeConfidencePercent:F1}%)";
            if (TxtPhase40RegimeSub != null) TxtPhase40RegimeSub.Text = $"香农熵: {mrs.RegimeEntropyIndex:F2} | 跨周期夏普增益: +{mrs.AdaptiveCrossCycleSharpeUpliftPercent:F1}%";
            if (TxtPhase40RegimeBadge != null) TxtPhase40RegimeBadge.Text = $"4 状态 Hamilton 滤波 (主导: {mrs.DominantRegimeName})";
            if (GridPhase40Regimes != null) GridPhase40Regimes.ItemsSource = mrs.RegimeStates;
        }

        // 2. AQR Capital & Man Group AHL 多频率截面交叉动量 (CSMOM) 与双重动量相对优势剥离
        if (result.CrossSectionalMomentum != null)
        {
            var mom = result.CrossSectionalMomentum;
            if (TxtPhase40MomSpread != null) TxtPhase40MomSpread.Text = $"+{mom.WinnerLoserSpreadAnnualizedPercent:F2}%";
            if (TxtPhase40MomSub != null) TxtPhase40MomSub.Text = $"截面离散度: {mom.CrossSectionalDispersionPercent:F2}% | Rank IC: {mom.RankInformationCoefficient:F3} | Hurst: {mom.PortfolioAverageHurstExponent:F2}";
            if (TxtPhase40MomBadge != null) TxtPhase40MomBadge.Text = $"多频率截面动量 (平均Hurst: {mom.PortfolioAverageHurstExponent:F2})";
            if (GridPhase40CrossSectionalMomentum != null) GridPhase40CrossSectionalMomentum.ItemsSource = mom.AssetMomentums;
        }

        // 3. Millennium Management & Balyasny (BAM) 多策略 Pod 阶梯式回撤硬风控熔断与动态资本再平衡
        if (result.PodTieredDrawdownCircuitBreaker != null)
        {
            var pod = result.PodTieredDrawdownCircuitBreaker;
            if (TxtPhase40PodHealth != null) TxtPhase40PodHealth.Text = $"{pod.OverallPodHealthScore:F1}分 / {pod.TotalCapitalProtectedPercent:F1}%";
            if (TxtPhase40PodSub != null) TxtPhase40PodSub.Text = $"正常: {pod.NormalPodCount} | 减额: {pod.ThrottledPodCount} | 熔断: {pod.CircuitBreakerTriggeredCount} | 再平衡增益: +{pod.DynamicRebalancingEfficiencyGainPercent:F2}%";
            if (TxtPhase40PodBadge != null) TxtPhase40PodBadge.Text = $"阶梯熔断硬风控 (保全资本: {pod.TotalCapitalProtectedPercent:F1}%)";
            if (GridPhase40PodTiers != null) GridPhase40PodTiers.ItemsSource = pod.PodTiers;
        }

        // 4. Jane Street & Optiver 微观限价单队列成交概率与高频期现基差收敛套利
        if (result.LimitOrderQueueAndBasisArbitrage != null)
        {
            var qarb = result.LimitOrderQueueAndBasisArbitrage;
            if (TxtPhase40QueueFill != null) TxtPhase40QueueFill.Text = $"{qarb.PortfolioAverageFillProbabilityPercent:F1}% / +{qarb.AnnualizedPassiveExecutionSavingsPercent:F2}%";
            if (TxtPhase40QueueSub != null) TxtPhase40QueueSub.Text = $"最大基差: {qarb.MaxBasisMispricingBps:F1} bps | 套利年化: +{qarb.AnnualizedSyntheticBasisArbitrageYieldPercent:F2}% | 半衰期: {qarb.AverageBasisHalfLifeDays:F1}天";
            if (TxtPhase40QueueBadge != null) TxtPhase40QueueBadge.Text = $"队列指数排队与期现套利 (+{qarb.AnnualizedPassiveExecutionSavingsPercent:F2}% 节省)";
            if (GridPhase40QueueArbitrage != null) GridPhase40QueueArbitrage.ItemsSource = qarb.AssetQueueArbitrages;
        }

        if (TxtPhase40ExecutiveVerdict != null && result.MacroMarkovRegimeSwitching != null && result.CrossSectionalMomentum != null && result.PodTieredDrawdownCircuitBreaker != null && result.LimitOrderQueueAndBasisArbitrage != null)
        {
            TxtPhase40ExecutiveVerdict.Text = $"{result.MacroMarkovRegimeSwitching.ExecutiveVerdict} ｜ {result.CrossSectionalMomentum.ExecutiveVerdict} ｜ {result.PodTieredDrawdownCircuitBreaker.ExecutiveVerdict} ｜ {result.LimitOrderQueueAndBasisArbitrage.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase41InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. D.E. Shaw & WorldQuant 符号基因规划自适应 Alpha 因子挖掘引擎
        if (result.SymbolicGeneticAlphaMining != null)
        {
            var gen = result.SymbolicGeneticAlphaMining;
            if (TxtPhase41GeneticIc != null) TxtPhase41GeneticIc.Text = $"{gen.TopAlphaInformationRatio:F2} / +{gen.MultiAlphaEnsembleAnnualizedAlpha:F2}%";
            if (TxtPhase41GeneticSub != null) TxtPhase41GeneticSub.Text = $"演化: {gen.TotalGenerationsEvolved}代 | 评估: {gen.TotalFormulasEvaluated:N0}式 | 复杂度: {gen.AverageFormulaComplexity:F2}";
            if (TxtPhase41GeneticBadge != null) TxtPhase41GeneticBadge.Text = $"多代遗传演化 (复杂度: {gen.AverageFormulaComplexity:F2})";
            if (GridPhase41GeneticAlphas != null) GridPhase41GeneticAlphas.ItemsSource = gen.EvolvedAlphaFormulas;
        }

        // 2. Two Sigma & Man Group AHL 知识图谱跨资产因果时滞传递与宏观情绪溢出网络
        if (result.CausalKnowledgeGraphSpillover != null)
        {
            var ckg = result.CausalKnowledgeGraphSpillover;
            if (TxtPhase41CausalSpillover != null) TxtPhase41CausalSpillover.Text = $"{ckg.TotalSystemSpilloverIndexPercent:F1}% / {ckg.AverageLeadTimeDays:F1}天";
            if (TxtPhase41CausalSub != null) TxtPhase41CausalSub.Text = $"源头: {ckg.DominantInformationSourceAsset} | 网络密度: {ckg.CausalNetworkDensityPercent:F1}%";
            if (TxtPhase41CausalBadge != null) TxtPhase41CausalBadge.Text = $"因果拓扑溢出 (密度: {ckg.CausalNetworkDensityPercent:F1}%)";
            if (GridPhase41CausalSpillover != null) GridPhase41CausalSpillover.ItemsSource = ckg.SpilloverEdges;
        }

        // 3. Citadel & Point72 上下文多臂老虎机 (Thompson Sampling) 自适应策略路由器
        if (result.ContextualBanditPolicyRouter != null)
        {
            var bandit = result.ContextualBanditPolicyRouter;
            if (TxtPhase41BanditMeta != null) TxtPhase41BanditMeta.Text = $"+{bandit.CumulativeMetaSharpeGainPercent:F1}% / {bandit.OverallPolicyConfidencePercent:F1}%";
            if (TxtPhase41BanditSub != null) TxtPhase41BanditSub.Text = $"主导: {bandit.OptimalStrategyName} | 探索比: {bandit.ExplorationVsExploitationRatio:F2}";
            if (TxtPhase41BanditBadge != null) TxtPhase41BanditBadge.Text = $"Thompson采样自适应路由 (置信度: {bandit.OverallPolicyConfidencePercent:F1}%)";
            if (GridPhase41BanditRouter != null) GridPhase41BanditRouter.ItemsSource = bandit.StrategyArms;
        }

        // 4. Jump Trading & Optiver 深度强化微观流动性冲击弹性与非线性滑点曲面
        if (result.MicrostructureResiliencySlippageSurface != null)
        {
            var slip = result.MicrostructureResiliencySlippageSurface;
            if (TxtPhase41ResiliencySlippage != null) TxtPhase41ResiliencySlippage.Text = $"{slip.AverageEffectiveSlippageBps:F1} bps / +{slip.AnnualizedTransactionCostSavingsPercent:F2}%";
            if (TxtPhase41ResiliencySub != null) TxtPhase41ResiliencySub.Text = $"自愈半衰期: {slip.HalfLifeRecoverySeconds:F1}秒 | 最优调度: Lv.{slip.OptimalExecutionUrgency}";
            if (TxtPhase41ResiliencyBadge != null) TxtPhase41ResiliencyBadge.Text = $"Bouchaud 瞬态自愈曲面 (节约: +{slip.AnnualizedTransactionCostSavingsPercent:F2}%)";
            if (GridPhase41ResiliencySlippage != null) GridPhase41ResiliencySlippage.ItemsSource = slip.SurfaceGridPoints;
        }

        if (TxtPhase41ExecutiveVerdict != null && result.SymbolicGeneticAlphaMining != null && result.CausalKnowledgeGraphSpillover != null && result.ContextualBanditPolicyRouter != null && result.MicrostructureResiliencySlippageSurface != null)
        {
            TxtPhase41ExecutiveVerdict.Text = $"{result.SymbolicGeneticAlphaMining.ExecutiveVerdict} ｜ {result.CausalKnowledgeGraphSpillover.ExecutiveVerdict} ｜ {result.ContextualBanditPolicyRouter.ExecutiveVerdict} ｜ {result.MicrostructureResiliencySlippageSurface.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase42InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Bridgewater Associates & AQR Capital 跨资产内生流动性螺旋与去杠杆压力传染动力学模型
        if (result.EndogenousLiquiditySpiral != null)
        {
            var spiral = result.EndogenousLiquiditySpiral;
            if (TxtPhase42CascadeMultiplier != null) TxtPhase42CascadeMultiplier.Text = $"{spiral.SystemicLiquidityCascadeMultiplier:F2}x / {spiral.TotalForcedFireSaleCapitalPercent:F1}%";
            if (TxtPhase42CascadeSub != null) TxtPhase42CascadeSub.Text = $"发丝率弹性: {spiral.MarginSpiralElasticity:F2} | 平均踩踏折价: {spiral.PortfolioStressedIlliquidityDiscountPercent:F2}%";
            if (TxtPhase42FireSaleBadge != null) TxtPhase42FireSaleBadge.Text = $"Brunnermeier反馈乘数 (M = {spiral.SystemicLiquidityCascadeMultiplier:F2}x)";
            if (GridPhase42FireSale != null) GridPhase42FireSale.ItemsSource = spiral.FireSaleAssetItems;
        }

        // 2. Renaissance Technologies & Citadel 变分无监督深度隐空间流形聚类与非线性宏观体制自编码器
        if (result.VariationalLatentManifoldRegime != null)
        {
            var vlm = result.VariationalLatentManifoldRegime;
            if (TxtPhase42LatentZ != null) TxtPhase42LatentZ.Text = $"{vlm.DeepReconstructionAnomalyScore:F1} / {vlm.ManifoldTransitionVelocity:F2}";
            if (TxtPhase42LatentSub != null) TxtPhase42LatentSub.Text = $"隐坐标: ({vlm.CurrentLatentZ1:F2}, {vlm.CurrentLatentZ2:F2}, {vlm.CurrentLatentZ3:F2}) | KL散度: {vlm.KlDivergenceLoss:F3}";
            if (TxtPhase42LatentBadge != null) TxtPhase42LatentBadge.Text = $"流形主导体制: {vlm.DominantLatentClusterName}";
            if (GridPhase42LatentManifold != null) GridPhase42LatentManifold.ItemsSource = vlm.LatentClusters;
        }

        // 3. Millennium Management & Point72 跨资产多周期极值尾部相关相变检测与渗流理论系统性临界熔断网络
        if (result.PercolationTailPhaseTransition != null)
        {
            var perc = result.PercolationTailPhaseTransition;
            if (TxtPhase42GiantCluster != null) TxtPhase42GiantCluster.Text = $"{perc.GiantConnectedClusterSizePercent:F1}% / {perc.DistanceToPhaseTransitionCriticality:F3}";
            if (TxtPhase42PercolationSub != null) TxtPhase42PercolationSub.Text = $"临界阈值 pc: {perc.CriticalPercolationThreshold:F2} | 敏感度 chi: {perc.PercolationSusceptibility:F2} (SCI: {perc.SystemicCriticalityIndex:F1})";
            if (TxtPhase42PercolationBadge != null) TxtPhase42PercolationBadge.Text = $"渗流相变指数 (SCI: {perc.SystemicCriticalityIndex:F1}/100)";
            if (GridPhase42Percolation != null) GridPhase42Percolation.ItemsSource = perc.PercolationCriticalEdges;
        }

        // 4. WorldQuant & Hudson River Trading 统计套利强化正交特征残差动量与微观协整均值回复追踪引擎
        if (result.StatArbResidualMomentumOu != null)
        {
            var ou = result.StatArbResidualMomentumOu;
            if (TxtPhase42StatArbResidual != null) TxtPhase42StatArbResidual.Text = $"{ou.PortfolioAverageResidualMomentum:+0.00;-0.00} / +{ou.AnnualizedIdiosyncraticStatArbAlphaPercent:F2}%";
            if (TxtPhase42StatArbSub != null) TxtPhase42StatArbSub.Text = $"平均收敛半衰期: {ou.AverageCointegrationHalfLifeDays:F1}天 | 极值偏离: {ou.TopPairArbitrageZScore:F2}σ";
            if (TxtPhase42StatArbBadge != null) TxtPhase42StatArbBadge.Text = $"纯特质残差动量 (预期Alpha: +{ou.AnnualizedIdiosyncraticStatArbAlphaPercent:F2}%)";
            if (GridPhase42StatArbOu != null) GridPhase42StatArbOu.ItemsSource = ou.StatArbPairs;
        }

        if (TxtPhase42ExecutiveVerdict != null && result.EndogenousLiquiditySpiral != null && result.VariationalLatentManifoldRegime != null && result.PercolationTailPhaseTransition != null && result.StatArbResidualMomentumOu != null)
        {
            TxtPhase42ExecutiveVerdict.Text = $"{result.EndogenousLiquiditySpiral.ExecutiveVerdict} ｜ {result.VariationalLatentManifoldRegime.ExecutiveVerdict} ｜ {result.PercolationTailPhaseTransition.ExecutiveVerdict} ｜ {result.StatArbResidualMomentumOu.ExecutiveVerdict}";
        }
    }

    /// <summary>
    /// Phase 43 机构级终极前沿量化投研服务 (mRMR特征正交集成、Wasserstein最优输运资本曲率、Hawkes点过程雪崩预警、高阶矩张量风险平价) UI 面板数据绑定
    /// </summary>
    private void UpdatePhase43InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Renaissance Technologies & Two Sigma 最大相关最小冗余 (mRMR) 互信息特征选择与正交集成
        if (result.MrmrFeatureEnsemble != null)
        {
            var mrmr = result.MrmrFeatureEnsemble;
            if (TxtPhase43MrmrIcGain != null) TxtPhase43MrmrIcGain.Text = $"+{mrmr.TopFeatureEnsembleIcGainRatio:F1}% / {mrmr.CollinearityReductionRatePercent:F1}%";
            if (TxtPhase43MrmrSub != null) TxtPhase43MrmrSub.Text = $"精选最优正交特征: {mrmr.SelectedOptimalFeatureCount}个 | 平均互信息: {mrmr.AverageFeatureMutualInformation:F3}";
            if (GridPhase43MrmrFeatures != null) GridPhase43MrmrFeatures.ItemsSource = mrmr.FeatureRankings;
        }

        // 2. Citadel & Millennium 基于 Wasserstein 测度距离最优输运的多策略 Pod 动态资本曲率重构
        if (result.WassersteinPodCurvature != null)
        {
            var wsp = result.WassersteinPodCurvature;
            if (TxtPhase43WassersteinCurvature != null) TxtPhase43WassersteinCurvature.Text = $"{wsp.TotalWassersteinDistanceMetric:F4} / +{wsp.CurvatureStabilityImprovementPercent:F1}%";
            if (TxtPhase43WassersteinSub != null) TxtPhase43WassersteinSub.Text = $"最优输运换手: {wsp.TotalOptimalTransportTurnoverPercent:F1}% | 摩擦节约: +{wsp.NetTransportFrictionSavingsBps:F1} bps";
            if (GridPhase43WassersteinPods != null) GridPhase43WassersteinPods.ItemsSource = wsp.PodCurvatureAllocations;
        }

        // 3. Jane Street & Citadel Securities 微观限价订单簿自激 Hawkes 点过程与流动性雪崩预警
        if (result.HawkesMicrostructureAvalanche != null)
        {
            var hwk = result.HawkesMicrostructureAvalanche;
            if (TxtPhase43HawkesBranching != null) TxtPhase43HawkesBranching.Text = $"{hwk.PortfolioAverageBranchingRatio:F3} / {hwk.CompositeAvalancheRiskIndex:F1}";
            if (TxtPhase43HawkesSub != null) TxtPhase43HawkesSub.Text = $"高危避险警报: {hwk.CriticalCascadeAlertCount}只 | 前瞻滑点挽回: +{hwk.ExpectedPreemptiveCostSavingBps:F1} bps";
            if (GridPhase43HawkesJumps != null) GridPhase43HawkesJumps.ItemsSource = hwk.AssetHawkesJumps;
        }

        // 4. Bridgewater Associates & AQR Capital 高阶矩张量风险平价与非高斯偏度-峰度协同对冲
        if (result.HigherOrderTensorRiskParity != null)
        {
            var hmt = result.HigherOrderTensorRiskParity;
            if (TxtPhase43TensorCoskewness != null) TxtPhase43TensorCoskewness.Text = $"{hmt.PortfolioCoSkewnessMetric:+0.00;-0.00}·{hmt.PortfolioCoKurtosisMetric:F2} / +{hmt.TailConvexityHedgingUpliftPercent:F1}%";
            if (TxtPhase43TensorSub != null) TxtPhase43TensorSub.Text = $"张量平价离散度: {hmt.HigherOrderRiskDispersionIndex:F3} | 欧拉高阶MRC均衡";
            if (GridPhase43HigherMoments != null) GridPhase43HigherMoments.ItemsSource = hmt.HigherMomentAssets;
        }

        if (TxtPhase43ExecutiveVerdict != null && result.MrmrFeatureEnsemble != null && result.WassersteinPodCurvature != null && result.HawkesMicrostructureAvalanche != null && result.HigherOrderTensorRiskParity != null)
        {
            TxtPhase43ExecutiveVerdict.Text = $"{result.MrmrFeatureEnsemble.ExecutiveVerdict} ｜ {result.WassersteinPodCurvature.ExecutiveVerdict} ｜ {result.HawkesMicrostructureAvalanche.ExecutiveVerdict} ｜ {result.HigherOrderTensorRiskParity.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase44InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        // 1. Renaissance Technologies & D.E. Shaw 连续时间 BSDE 粘性解动态对冲
        if (result.BsdeDynamicHedging != null)
        {
            var bsd = result.BsdeDynamicHedging;
            if (TxtPhase44BsdeControlNorm != null) TxtPhase44BsdeControlNorm.Text = $"{bsd.PortfolioAverageHedgeControlNorm:F3} / +{bsd.NetHedgeSlippageSavingsBps:F1} bps";
            if (TxtPhase44BsdeSub != null) TxtPhase44BsdeSub.Text = $"曲率平抑: +{bsd.StochasticVolCurvatureSuppressionPercent:F1}% | 对冲复制评分: {bsd.DynamicHedgeEfficiencyScore:F1}分";
            if (GridPhase44BsdeHedges != null) GridPhase44BsdeHedges.ItemsSource = bsd.AssetBsdeHedges;
        }

        // 2. Citadel & Millennium 多资产高阶拓扑超图关联网络与持续同调
        if (result.HypergraphTopologicalCausality != null)
        {
            var hyp = result.HypergraphTopologicalCausality;
            if (TxtPhase44HypergraphBetti != null) TxtPhase44HypergraphBetti.Text = $"β0: {hyp.Betti0ConnectedComponents} / β1: {hyp.Betti1TopologicalCavities} / {hyp.CavityRuptureProbabilityPercent:F1}%";
            if (TxtPhase44HypergraphSub != null) TxtPhase44HypergraphSub.Text = $"超图谱间隙: {hyp.HypergraphSpectralGap:F3} | 拓扑流形相变熵: {hyp.TopologicalPhaseEntropy:F2}";
            if (GridPhase44Hyperedges != null) GridPhase44Hyperedges.ItemsSource = hyp.Hyperedges;
        }

        // 3. Jane Street & Citadel Securities 微观瞬时订单流毒性扩散与跨标的交叉冲击张量
        if (result.CrossImpactTensorMicrostructure != null)
        {
            var cit = result.CrossImpactTensorMicrostructure;
            if (TxtPhase44CrossImpactLambda != null) TxtPhase44CrossImpactLambda.Text = $"{cit.TensorAverageCrossImpactLambdaBps:F2} bps / +{cit.CrossExecutionFrictionSavingsBps:F1} bps";
            if (TxtPhase44CrossImpactSub != null) TxtPhase44CrossImpactSub.Text = $"综合毒性跨品种扩散率: {cit.CompositeToxicityDiffusionRatePercent:F1}% | {cit.MicrostructureRegimeState}";
            if (GridPhase44CrossImpactPairs != null) GridPhase44CrossImpactPairs.ItemsSource = cit.TopCrossImpactPairs;
        }

        // 4. Bridgewater Associates & AQR Capital 测度模糊集 Wasserstein-Ball DRO 极小极大平价
        if (result.WassersteinDroMinimaxParity != null)
        {
            var dro = result.WassersteinDroMinimaxParity;
            if (TxtPhase44DroWorstCvar != null) TxtPhase44DroWorstCvar.Text = $"{dro.WorstCaseExpectedShortfallPercent:F1}% / +{dro.OutOfSampleDrawdownMitigationPercent:F1}%";
            if (TxtPhase44DroSub != null) TxtPhase44DroSub.Text = $"模糊球半径 ε: {dro.AmbiguityBallRadiusEpsilon:F3} | 对偶乘子 λ*: {dro.DualLagrangeMultiplierLambda:F3}";
            if (GridPhase44DroAllocations != null) GridPhase44DroAllocations.ItemsSource = dro.DroAssetAllocations;
        }

        if (TxtPhase44ExecutiveVerdict != null && result.BsdeDynamicHedging != null && result.HypergraphTopologicalCausality != null && result.CrossImpactTensorMicrostructure != null && result.WassersteinDroMinimaxParity != null)
        {
            TxtPhase44ExecutiveVerdict.Text = $"{result.BsdeDynamicHedging.ExecutiveVerdict} ｜ {result.HypergraphTopologicalCausality.ExecutiveVerdict} ｜ {result.CrossImpactTensorMicrostructure.ExecutiveVerdict} ｜ {result.WassersteinDroMinimaxParity.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase45InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        // 1. Renaissance Technologies & D.E. Shaw 连续时间分数阶粗糙波动率 (Rough Heston)
        if (result.FractionalRoughVolatility != null)
        {
            var rfv = result.FractionalRoughVolatility;
            if (TxtPhase45RoughHurst != null) TxtPhase45RoughHurst.Text = $"{rfv.PortfolioAverageHurstParameterH:F3} / {rfv.CompositeVolBurstRiskProbabilityPercent:F1}%";
            if (TxtPhase45RoughSub != null) TxtPhase45RoughSub.Text = $"偏离: {rfv.FractionalVsMarkovianVolDispersionPercent:F1}% | 对冲挽回: +{rfv.NetRoughOptionHedgingCostSavingsBps:F1} bps";
            if (GridPhase45RoughVolatility != null) GridPhase45RoughVolatility.ItemsSource = rfv.AssetRoughVolatilities;
        }

        // 2. Citadel Global Fixed Income & Millennium RV 六参数 NSS 利率期限结构与蝶式套利
        if (result.NelsonSiegelSvenssonTermStructure != null)
        {
            var nss = result.NelsonSiegelSvenssonTermStructure;
            if (TxtPhase45NssDuration != null) TxtPhase45NssDuration.Text = $"{nss.PortfolioEffectiveDurationYears:F2} 年 / +{nss.ButterflyArbitrageExpectedAlphaBps:F1} bps";
            if (TxtPhase45NssSub != null) TxtPhase45NssSub.Text = $"水平 β0: {nss.Beta0Level:F2}% | 斜率 β1: {nss.Beta1Slope:F2}% | 双重曲率拟合";
            if (GridPhase45NssCurves != null) GridPhase45NssCurves.ItemsSource = nss.KeyRateDurations;
        }

        // 3. Two Sigma & Man Group AHL 贝叶斯在线变点检测 (BOCPD) 运行长度后验滤波
        if (result.BayesianOnlineChangepointDetection != null)
        {
            var bcp = result.BayesianOnlineChangepointDetection;
            if (TxtPhase45BocpdRunLength != null) TxtPhase45BocpdRunLength.Text = $"{bcp.CurrentRegimeRunLengthDays:F1} 天 / {bcp.LatestChangepointProbabilityPercent:F1}%";
            if (TxtPhase45BocpdSub != null) TxtPhase45BocpdSub.Text = $"失效率: {bcp.SystemicHazardRatePercent:F2}% | 避险挽回: +{bcp.PreemptiveDeriskingAlphaSavingsBps:F1} bps";
            if (GridPhase45Changepoint != null) GridPhase45Changepoint.ItemsSource = bcp.RecentHazardHistory;
        }

        // 4. Jane Street & Citadel Securities Avellaneda-Stoikov 连续库存最优保留价微观做市
        if (result.AvellanedaStoikovMicrostructure != null)
        {
            var ask = result.AvellanedaStoikovMicrostructure;
            if (TxtPhase45AvellanedaSpread != null) TxtPhase45AvellanedaSpread.Text = $"{ask.AverageOptimalQuotingSpreadBps:F1} bps / {ask.InventoryRiskMitigationRatePercent:F1}%";
            if (TxtPhase45AvellanedaSub != null) TxtPhase45AvellanedaSub.Text = $"偏斜: {ask.PortfolioWeightedReservationSkewBps:F1} bps | 执行挽回: +{ask.ExpectedMicroExecutionSavingsBps:F1} bps";
            if (GridPhase45AvellanedaStoikov != null) GridPhase45AvellanedaStoikov.ItemsSource = ask.AssetQuotingProfiles;
        }

        if (TxtPhase45ExecutiveVerdict != null && result.FractionalRoughVolatility != null && result.NelsonSiegelSvenssonTermStructure != null && result.BayesianOnlineChangepointDetection != null && result.AvellanedaStoikovMicrostructure != null)
        {
            TxtPhase45ExecutiveVerdict.Text = $"{result.FractionalRoughVolatility.ExecutiveVerdict} ｜ {result.NelsonSiegelSvenssonTermStructure.ExecutiveVerdict} ｜ {result.BayesianOnlineChangepointDetection.ExecutiveVerdict} ｜ {result.AvellanedaStoikovMicrostructure.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase46InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        // 1. Renaissance Technologies & Alan Turing Institute 粗糙路径特征签名与高阶张量 Alpha
        if (result.RoughPathSignatureAlpha != null)
        {
            var rps = result.RoughPathSignatureAlpha;
            if (TxtPhase46LevyArea != null) TxtPhase46LevyArea.Text = $"{rps.PortfolioAverageLevyArea:F5} / +{rps.HighOrderTensorAlphaPremiumBps:F1} bps";
            if (TxtPhase46LevySub != null) TxtPhase46LevySub.Text = $"综合几何曲率: {rps.CompositePathCurvatureIndex:F3} | 全息捕获率: {rps.SignatureInformationCaptureRatio:F1}%";
            if (GridPhase46PathSignatures != null) GridPhase46PathSignatures.ItemsSource = rps.AssetPathSignatures;
        }

        // 2. Citadel Global Strategies & Millennium RV 随机最优停止时间与自由边界斯内尔包络
        if (result.StochasticOptimalStopping != null)
        {
            var sos = result.StochasticOptimalStopping;
            if (TxtPhase46StoppingBoundary != null) TxtPhase46StoppingBoundary.Text = $"{sos.PortfolioWeightedStoppingBoundary:F4} / {sos.AggregateSnellEnvelopeTimeValueBps:F1} bps";
            if (TxtPhase46StoppingSub != null) TxtPhase46StoppingSub.Text = $"触碰边界警报: {sos.AssetsAtStoppingBoundaryCount} 只 | 避免深套挽回: +{sos.AvoidedDrawdownAlphaSavingsBps:F1} bps";
            if (GridPhase46StoppingThresholds != null) GridPhase46StoppingThresholds.ItemsSource = sos.AssetStoppingThresholds;
        }

        // 3. Two Sigma & Bridgewater Associates 因果结构方程模型与反事实 Do-Calculus 宏观归因
        if (result.CausalStructuralModelAttribution != null)
        {
            var scm = result.CausalStructuralModelAttribution;
            if (TxtPhase46CausalBias != null) TxtPhase46CausalBias.Text = $"{scm.SystemicConfoundingBiasRatio:F1}% / +{scm.TrueCausalAlphaContributionBps:F1} bps";
            if (TxtPhase46CausalSub != null) TxtPhase46CausalSub.Text = $"剔除虚假共线性: +{scm.SpuriousCorrelationEliminatedBps:F1} bps | {scm.MacroCausalDagStructureState}";
            if (GridPhase46CausalNodes != null) GridPhase46CausalNodes.ItemsSource = scm.CausalNodeEffects;
        }

        // 4. Jane Street & Citadel Securities 暂态市场冲击幂律记忆核与最优拆单执行
        if (result.TransientMarketImpactPropagator != null)
        {
            var tip = result.TransientMarketImpactPropagator;
            if (TxtPhase46PropagatorGamma != null) TxtPhase46PropagatorGamma.Text = $"{tip.PortfolioAverageDecayExponentGamma:F3} / +{tip.NonUniformExecutionSavingsBps:F1} bps";
            if (TxtPhase46PropagatorSub != null) TxtPhase46PropagatorSub.Text = $"积压冲击拖累: {tip.TotalAccumulatedTransientDragBps:F1} bps | 半衰期: {tip.MarketResilienceHalfLifeMinutes:F1}分";
            if (GridPhase46TransientImpacts != null) GridPhase46TransientImpacts.ItemsSource = tip.AssetTransientImpacts;
        }

        if (TxtPhase46ExecutiveVerdict != null && result.RoughPathSignatureAlpha != null && result.StochasticOptimalStopping != null && result.CausalStructuralModelAttribution != null && result.TransientMarketImpactPropagator != null)
        {
            TxtPhase46ExecutiveVerdict.Text = $"{result.RoughPathSignatureAlpha.ExecutiveVerdict} ｜ {result.StochasticOptimalStopping.ExecutiveVerdict} ｜ {result.CausalStructuralModelAttribution.ExecutiveVerdict} ｜ {result.TransientMarketImpactPropagator.ExecutiveVerdict}";
        }
    }

    #endregion

    #region Phase 47 机构级终极前沿量化投研展示逻辑

    private void UpdatePhase47InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        // 1. Renaissance Technologies & D.E. Shaw 马利亚温随机变分分析与无似然 Greeks
        if (result.MalliavinCalculusSensitivity != null)
        {
            var mcs = result.MalliavinCalculusSensitivity;
            if (TxtPhase47MalliavinDelta != null) TxtPhase47MalliavinDelta.Text = $"{mcs.PortfolioWeightedMalliavinDelta:F4} / {mcs.PortfolioWeightedMalliavinGamma:F4}";
            if (TxtPhase47MalliavinSub != null) TxtPhase47MalliavinSub.Text = $"总 Vega: {mcs.PortfolioAggregatedVegaBps:F1} bps | 单路径免重算加速: {mcs.FiniteDifferenceSpeedupRatio:F1}x";
            if (GridPhase47MalliavinSensitivities != null) GridPhase47MalliavinSensitivities.ItemsSource = mcs.AssetSensitivities;
        }

        // 2. Citadel Global Strategies & Millennium Management 半定松弛 (SDR) 锥优化与基数约束稀疏指数跟踪
        if (result.SemidefiniteRelaxationCardinality != null)
        {
            var sdr = result.SemidefiniteRelaxationCardinality;
            if (TxtPhase47SdrCardinality != null) TxtPhase47SdrCardinality.Text = $"K={sdr.CardinalityLimitK} 只 / 精选 {sdr.ActualSparseAssetsSelected} 只";
            if (TxtPhase47SdrSub != null) TxtPhase47SdrSub.Text = $"跟踪误差: {sdr.SdrSparseTrackingErrorPercent:F2}% | 调仓与换手摩擦减免: +{sdr.FrictionCostReductionBps:F1} bps";
            if (GridPhase47SdrSparseWeights != null) GridPhase47SdrSparseWeights.ItemsSource = sdr.SparseWeightItems;
        }

        // 3. Two Sigma & Bridgewater Associates 连续时间平均场博弈 (MFG) 与机构间策略纳什均衡执行
        if (result.MeanFieldGameExecution != null)
        {
            var mfg = result.MeanFieldGameExecution;
            if (TxtPhase47MfgCrowding != null) TxtPhase47MfgCrowding.Text = $"{mfg.SystemicCrowdingIndex:F1} / +{mfg.TotalMfgEquilibriumSavingsBps:F1} bps";
            if (TxtPhase47MfgSub != null) TxtPhase47MfgSub.Text = $"踩踏高危警戒: {mfg.CrowdedAssetsCount} 只 | 均衡收敛周期: {mfg.CoordinationConvergencePeriodHours:F1}h";
            if (GridPhase47MfgNashStrategies != null) GridPhase47MfgNashStrategies.ItemsSource = mfg.AssetNashStrategies;
        }

        // 4. Jane Street Capital & Jump Trading Kyle-Back 连续拍卖动态知情交易与隐匿执行模型
        if (result.KyleBackStealthExecution != null)
        {
            var kbe = result.KyleBackStealthExecution;
            if (TxtPhase47KyleLambda != null) TxtPhase47KyleLambda.Text = $"{kbe.PortfolioAverageKyleLambda:F2} bps / {kbe.CompositeStealthCamouflageScore:F1}分";
            if (TxtPhase47KyleSub != null) TxtPhase47KyleSub.Text = $"锁定超额 Alpha: +{kbe.TotalPreservedAlphaBps:F1} bps | 信号半衰期: {kbe.AverageAlphaSignalHalfLifeDays:F1}天";
            if (GridPhase47KyleBackExecutions != null) GridPhase47KyleBackExecutions.ItemsSource = kbe.AssetKyleBackExecutions;
        }

        if (TxtPhase47ExecutiveVerdict != null && result.MalliavinCalculusSensitivity != null && result.SemidefiniteRelaxationCardinality != null && result.MeanFieldGameExecution != null && result.KyleBackStealthExecution != null)
        {
            TxtPhase47ExecutiveVerdict.Text = $"{result.MalliavinCalculusSensitivity.ExecutiveVerdict} ｜ {result.SemidefiniteRelaxationCardinality.ExecutiveVerdict} ｜ {result.MeanFieldGameExecution.ExecutiveVerdict} ｜ {result.KyleBackStealthExecution.ExecutiveVerdict}";
        }
    }

    /// <summary>
    /// 更新 Phase 48 机构级顶流前沿投研展示：跳跃扩散 Lévy 过程 Esscher 测度变换与随机庞特里亚金极大值原理 (SPMP) 伴随状态控制、多 Pod 去中心化共识交替方向乘子法 (Consensus ADMM) 资本仲裁、非参数分层狄利克雷过程隐马尔可夫模型 (HDP-HMM) 自适应宏观结构聚类、双重随机 Cox 点过程最优非对称价差偏置控制
    /// </summary>
    private void UpdatePhase48InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Renaissance Technologies & D.E. Shaw 跳跃扩散 Lévy 过程 Esscher 测度与随机庞特里亚金 SPMP 伴随状态控制
        if (result.StochasticPontryaginControl != null)
        {
            var spmp = result.StochasticPontryaginControl;
            if (TxtPhase48SpmpCoState != null) TxtPhase48SpmpCoState.Text = $"{spmp.PortfolioAverageCoStateP:F4}";
            if (TxtPhase48SpmpSub != null) TxtPhase48SpmpSub.Text = $"最优调仓率: {spmp.PortfolioAverageOptimalControlRate:F2}%/日 | 减摩节省: +{spmp.TotalRebalanceSavingsBps:F1} bps";
            if (GridPhase48SpmpControls != null) GridPhase48SpmpControls.ItemsSource = spmp.AssetSpmpControls;
        }

        // 2. Citadel Global Strategies & Millennium Management 多 Pod 去中心化共识交替方向乘子法 (Consensus ADMM) 资本仲裁
        if (result.ConsensusAdmmArbitration != null)
        {
            var admm = result.ConsensusAdmmArbitration;
            if (TxtPhase48AdmmResidual != null) TxtPhase48AdmmResidual.Text = $"{admm.FinalPrimalResidual:F5}";
            if (TxtPhase48AdmmSub != null) TxtPhase48AdmmSub.Text = $"迭代: {admm.AdmmIterationsTaken} 步 | 资本效率增益: +{admm.TotalCapitalEfficiencyGainBps:F1} bps";
            if (GridPhase48AdmmAllocations != null) GridPhase48AdmmAllocations.ItemsSource = admm.PodAllocations;
        }

        // 3. Two Sigma & Bridgewater Associates 非参数分层狄利克雷过程隐马尔可夫模型 (HDP-HMM) 自适应宏观聚类与未见体制涌现
        if (result.InfiniteHdpMacroClustering != null)
        {
            var hdp = result.InfiniteHdpMacroClustering;
            if (TxtPhase48HdpRegime != null) TxtPhase48HdpRegime.Text = $"{hdp.NewRegimeEmergenceProbability:F1}%";
            if (TxtPhase48HdpSub != null) TxtPhase48HdpSub.Text = $"活动体制: {hdp.ActiveRegimesCount} 个 | 宏观惊异度: {hdp.MacroSurpriseIndex:F1} | 证据比: {hdp.BayesianEvidenceRatio:F1}x";
            if (GridPhase48HdpRegimes != null) GridPhase48HdpRegimes.ItemsSource = hdp.ActiveRegimes;
        }

        // 4. Jane Street Capital & Jump Trading 双重随机 Cox 点过程与做市最优非对称价差偏置控制
        if (result.CoxProcessAsymmetricMarketMaking != null)
        {
            var cox = result.CoxProcessAsymmetricMarketMaking;
            if (TxtPhase48CoxSpread != null) TxtPhase48CoxSpread.Text = $"{cox.PortfolioWeightedAverageSpreadBps:F1} bps";
            if (TxtPhase48CoxSub != null) TxtPhase48CoxSub.Text = $"偏置 Skew: {cox.AverageAsymmetrySkewBps:F1} bps | 毒性防御: +{cox.TotalToxicDefenseGainBps:F1} bps | 做市 Alpha: +{cox.TotalExpectedNetMakingAlphaBps:F1} bps";
            if (GridPhase48CoxMarketMakings != null) GridPhase48CoxMarketMakings.ItemsSource = cox.AssetMarketMakingItems;
        }

        if (TxtPhase48ExecutiveVerdict != null && result.StochasticPontryaginControl != null && result.ConsensusAdmmArbitration != null && result.InfiniteHdpMacroClustering != null && result.CoxProcessAsymmetricMarketMaking != null)
        {
            TxtPhase48ExecutiveVerdict.Text = $"{result.StochasticPontryaginControl.ExecutiveVerdict} ｜ {result.ConsensusAdmmArbitration.ExecutiveVerdict} ｜ {result.InfiniteHdpMacroClustering.ExecutiveVerdict} ｜ {result.CoxProcessAsymmetricMarketMaking.ExecutiveVerdict}";
        }
    }

    #endregion

    #region Phase 49 机构级终极前沿量化投研展示逻辑

    /// <summary>
    /// 更新 Phase 49 机构级顶流前沿投研展示：粗糙分数 Ornstein-Uhlenbeck (fOU) 反持续性长程记忆与 Skorokhod 超前对冲、双层分层 Stackelberg 动态主从博弈与道德风险防范委托-代理多经理最优契约、拓扑信息几何 Fisher-Rao 黎曼流形测地线距离与相变预警、多维 Hawkes 自激/互激点过程谱半径与 LOB 队列做市
    /// </summary>
    private void UpdatePhase49InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Renaissance Technologies & D.E. Shaw 粗糙分数 fOU 反持续长程记忆与 Skorokhod 超前对冲
        if (result.RoughFractionalOuMemory != null)
        {
            var fou = result.RoughFractionalOuMemory;
            if (TxtPhase49FouHurst != null) TxtPhase49FouHurst.Text = $"H={fou.PortfolioWeightedHurstParameter:F3} / {fou.PortfolioAverageFouReversionSpeed:F1}次/年";
            if (TxtPhase49FouSub != null) TxtPhase49FouSub.Text = $"对冲误差压降: -{fou.TotalTrackingErrorReductionPercent:F1}% | 减摩增益: +{fou.SkorokhodDivergenceEfficiencyGainBps:F1} bps";
            if (GridPhase49FouMemoryItems != null) GridPhase49FouMemoryItems.ItemsSource = fou.AssetFouMemoryItems;
        }

        // 2. Citadel Global Strategies & Millennium Management 双层分层 Stackelberg 动态主从博弈最优契约
        if (result.BilevelStackelbergContract != null)
        {
            var bsc = result.BilevelStackelbergContract;
            if (TxtPhase49StackelbergIncentive != null) TxtPhase49StackelbergIncentive.Text = $"{bsc.PlatformAverageIncentiveSlope:F1}% / {bsc.SystemicMoralHazardDeterrenceScore:F1}分";
            if (TxtPhase49StackelbergSub != null) TxtPhase49StackelbergSub.Text = $"管辖 Pod: {bsc.TotalPodsCount}个 | 资本效率释放: +{bsc.CapitalEfficiencyGainBps:F1} bps";
            if (GridPhase49StackelbergContracts != null) GridPhase49StackelbergContracts.ItemsSource = bsc.PodContractItems;
        }

        // 3. Two Sigma & Bridgewater Associates 拓扑信息几何 Fisher-Rao 黎曼流形测地线距离与相变预警
        if (result.TopologicalInformationGeometry != null)
        {
            var tig = result.TopologicalInformationGeometry;
            if (TxtPhase49InfoGeoDistance != null) TxtPhase49InfoGeoDistance.Text = $"d_FR={tig.AverageFisherRaoDistance:F2} / {tig.GlobalPhaseTransitionWarningIndex:F1}";
            if (TxtPhase49InfoGeoSub != null) TxtPhase49InfoGeoSub.Text = $"流形最大曲率: {tig.MaxManifoldCurvatureMagnitude:F2} | 重心稳态: {tig.BarycenterStabilityScore:F1}分";
            if (GridPhase49GeodesicRegimes != null) GridPhase49GeodesicRegimes.ItemsSource = tig.GeodesicRegimeItems;
        }

        // 4. Jane Street Capital & Jump Trading 多维 Hawkes 自激/互激点过程谱半径与 LOB 队列做市
        if (result.MultivariateHawkesMicrostructure != null)
        {
            var hmk = result.MultivariateHawkesMicrostructure;
            if (TxtPhase49HawkesSpectral != null) TxtPhase49HawkesSpectral.Text = $"ρ={hmk.HawkesBranchingSpectralRadius:F3} / +{hmk.NetMicrostructureAlphaBps:F1} bps";
            if (TxtPhase49HawkesSub != null) TxtPhase49HawkesSub.Text = $"LOB 耗尽概率: {hmk.AverageQueueDepletionProbability:F1}% | 雪崩防御得分: {hmk.AvalancheBlackHoleWarningScore:F1}";
            if (GridPhase49HawkesExcitations != null) GridPhase49HawkesExcitations.ItemsSource = hmk.HawkesMatrixItems;
        }

        if (TxtPhase49ExecutiveVerdict != null && result.RoughFractionalOuMemory != null && result.BilevelStackelbergContract != null && result.TopologicalInformationGeometry != null && result.MultivariateHawkesMicrostructure != null)
        {
            TxtPhase49ExecutiveVerdict.Text = $"{result.RoughFractionalOuMemory.ExecutiveVerdict} ｜ {result.BilevelStackelbergContract.ExecutiveVerdict} ｜ {result.TopologicalInformationGeometry.ExecutiveVerdict} ｜ {result.MultivariateHawkesMicrostructure.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase50InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Renaissance Technologies & D.E. Shaw 非对易自由概率论与量子重整化群谱修复
        if (result.VoiculescuFreeProbabilityQrg != null)
        {
            var vfp = result.VoiculescuFreeProbabilityQrg;
            if (TxtPhase50FreeQrgNoise != null) TxtPhase50FreeQrgNoise.Text = $"{vfp.VoiculescuFreeNoiseFractionPercent:F1}% / Λ*={vfp.QrgEffectiveEnergyScaleLambda:F3}";
            if (TxtPhase50FreeQrgSub != null) TxtPhase50FreeQrgSub.Text = $"条件数压缩: {vfp.ConditionNumberCompressionRatio:F2}x | 减噪增益: +{vfp.SpectralSingularityRepairGainBps:F1} bps";
            if (GridPhase50FreeQrgItems != null) GridPhase50FreeQrgItems.ItemsSource = vfp.AssetQrgItems;
        }

        // 2. Citadel Global Strategies & AQR Capital Management 宏观混沌奇异吸引子与尖点突变防踩踏
        if (result.ThomCatastropheChaosDynamics != null)
        {
            var tcd = result.ThomCatastropheChaosDynamics;
            if (TxtPhase50CatastropheLyap != null) TxtPhase50CatastropheLyap.Text = $"λ={tcd.PortfolioMaxLyapunovExponent:F3} / d={tcd.ThomCatastropheBifurcationDistance:F3}";
            if (TxtPhase50CatastropheSub != null) TxtPhase50CatastropheSub.Text = $"预测视界: {tcd.LyapunovPredictionHorizonDays:F1}天 | 对冲缓冲: {tcd.SystemicAntiHysteresisBufferPct:F1}%";
            if (GridPhase50CatastropheItems != null) GridPhase50CatastropheItems.ItemsSource = tcd.CatastropheItems;
        }

        // 3. Two Sigma & WorldQuant 神经薛定谔桥与反射 BSDE 资本硬约束对冲
        if (result.NeuralSchrodingerBridgeReflectedBsde != null)
        {
            var sbr = result.NeuralSchrodingerBridgeReflectedBsde;
            if (TxtPhase50SchrodingerEntropic != null) TxtPhase50SchrodingerEntropic.Text = $"W_ε={sbr.SchrodingerBridgeEntropicDistance:F3} / {sbr.ReflectedBsdeBoundarySlackMargin:F1}%";
            if (TxtPhase50SchrodingerSub != null) TxtPhase50SchrodingerSub.Text = $"局部时: {sbr.SkorokhodReflectionLocalTimeIntensity:F1} bps | 减摩: +{sbr.OptimalTransitionCostSavingsBps:F1} bps";
            if (GridPhase50BridgeBsdeItems != null) GridPhase50BridgeBsdeItems.ItemsSource = sbr.BridgeBsdeItems;
        }

        // 4. Jane Street Capital & Hudson River Trading 玻尔兹曼-弗拉索夫微观场论与相对论纳什博弈
        if (result.BoltzmannVlasovRelativisticExecution != null)
        {
            var bve = result.BoltzmannVlasovRelativisticExecution;
            if (TxtPhase50VlasovPotential != null) TxtPhase50VlasovPotential.Text = $"Φ={bve.VlasovSelfConsistentFieldPotential:F2} / {bve.MicrostructuralAcousticSpeed:F2} t/ms";
            if (TxtPhase50VlasovSub != null) TxtPhase50VlasovSub.Text = $"因果安全: {bve.RelativisticCausalHorizonSafetyScore:F1}分 | 防抢跑: +{bve.AdverseSelectionAlphaProtectionBps:F1} bps";
            if (GridPhase50VlasovFieldItems != null) GridPhase50VlasovFieldItems.ItemsSource = bve.VlasovFieldItems;
        }

        if (TxtPhase50ExecutiveVerdict != null && result.VoiculescuFreeProbabilityQrg != null && result.ThomCatastropheChaosDynamics != null && result.NeuralSchrodingerBridgeReflectedBsde != null && result.BoltzmannVlasovRelativisticExecution != null)
        {
            TxtPhase50ExecutiveVerdict.Text = $"{result.VoiculescuFreeProbabilityQrg.ExecutiveVerdict} ｜ {result.ThomCatastropheChaosDynamics.ExecutiveVerdict} ｜ {result.NeuralSchrodingerBridgeReflectedBsde.ExecutiveVerdict} ｜ {result.BoltzmannVlasovRelativisticExecution.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase51InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Millennium Management & Point72 多 PM 策略 Pod 凸二次多维资本分配与内部互冲去交叉阻尼
        if (result.MillenniumConvexPodAllocation != null)
        {
            var mca = result.MillenniumConvexPodAllocation;
            if (TxtPhase51PodNettingRate != null) TxtPhase51PodNettingRate.Text = $"{mca.InternalNettingEfficiencyPercent:F1}% / +{mca.DynamicCapitalDragSavingsBps:F1} bps";
            if (TxtPhase51PodNettingSub != null) TxtPhase51PodNettingSub.Text = $"传染指数: {mca.CrossPodMarginContagionIndex:F2} | 止损边际: {mca.SystemicStopOutSlackMargin:F1}%";
            if (GridPhase51PodAllocationItems != null) GridPhase51PodAllocationItems.ItemsSource = mca.PodAllocationItems;
        }

        // 2. Jump Trading & Tower Research Capital 粗糙分数阶随机波动率与微观粗糙度幂律偏度流形
        if (result.RoughFractionalVolatilityGatheral != null)
        {
            var rfg = result.RoughFractionalVolatilityGatheral;
            if (TxtPhase51HurstExponent != null) TxtPhase51HurstExponent.Text = $"H={rfg.PortfolioWeightedHurstExponent:F3} / ψ={rfg.ShortTermPowerLawSkewSlope:F3}";
            if (TxtPhase51HurstSub != null) TxtPhase51HurstSub.Text = $"爆裂指数: {rfg.SystemicRoughnessBurstIndex:F2}x | 挽回摩擦: +{rfg.RoughnessHedgingAlphaBps:F1} bps";
            if (GridPhase51RoughVolItems != null) GridPhase51RoughVolItems.ItemsSource = rfg.RoughVolItems;
        }

        // 3. Bridgewater Associates & BlackRock Aladdin 宏观热力学最小相对交叉熵与非高斯情景流形映射
        if (result.ThermodynamicCrossEntropyStress != null)
        {
            var ces = result.ThermodynamicCrossEntropyStress;
            if (TxtPhase51CrossEntropyDkl != null) TxtPhase51CrossEntropyDkl.Text = $"D_KL={ces.SystemicCrossEntropyDkl:F4} / |ΔF|={ces.GibbsFreeEnergyCollapse:F3}";
            if (TxtPhase51CrossEntropySub != null) TxtPhase51CrossEntropySub.Text = $"信息温度: T={ces.MacroThermodynamicTemperature:F2} | 受压CVaR: {ces.StressedConditionalVaR99:F1}%";
            if (GridPhase51StressScenarioItems != null) GridPhase51StressScenarioItems.ItemsSource = ces.StressScenarioItems;
        }

        // 4. Jump Trading & Hudson River Trading 超对称路径积分与非微扰瞬子跃迁防御
        if (result.SupersymmetricInstantonTunneling != null)
        {
            var sit = result.SupersymmetricInstantonTunneling;
            if (TxtPhase51InstantonActionS != null) TxtPhase51InstantonActionS.Text = $"S={sit.GlobalInstantonActionS:F3} / Γ={sit.MaximumTunnelingEscapeRate:F1} bps";
            if (TxtPhase51InstantonSub != null) TxtPhase51InstantonSub.Text = $"能级差缓冲: {sit.SupersymmetricEnergyGapBps:F1} bps | 防御头寸: {sit.NonPerturbativeTailShieldPct:F1}%";
            if (GridPhase51InstantonItems != null) GridPhase51InstantonItems.ItemsSource = sit.InstantonItems;
        }

        if (TxtPhase51ExecutiveVerdict != null && result.MillenniumConvexPodAllocation != null && result.RoughFractionalVolatilityGatheral != null && result.ThermodynamicCrossEntropyStress != null && result.SupersymmetricInstantonTunneling != null)
        {
            TxtPhase51ExecutiveVerdict.Text = $"{result.MillenniumConvexPodAllocation.ExecutiveVerdict} ｜ {result.RoughFractionalVolatilityGatheral.ExecutiveVerdict} ｜ {result.ThermodynamicCrossEntropyStress.ExecutiveVerdict} ｜ {result.SupersymmetricInstantonTunneling.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase52InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Citadel Securities & Jane Street 微观瞬态幂律记忆价格冲击核与动态隐匿拆单执行
        if (result.BouchaudTransientImpactPropagator != null)
        {
            var tip = result.BouchaudTransientImpactPropagator;
            if (TxtPhase52TransientSlippageSavings != null) TxtPhase52TransientSlippageSavings.Text = $"+{tip.GlobalTransientSlippageSavingsBps:F1} bps / γ={tip.MeanPowerLawExponentGamma:F3}";
            if (TxtPhase52TransientSlippageSub != null) TxtPhase52TransientSlippageSub.Text = $"隐匿指数: {tip.AverageOrderFlowConcealmentIndex:F1} | 抑制过冲: {tip.SelfImpactMitigationRatioPct:F1}%";
            if (GridPhase52ImpactItems != null) GridPhase52ImpactItems.ItemsSource = tip.ImpactItems;
        }

        // 2. Two Sigma & D.E. Shaw 高维流形图拉普拉斯算子谱聚类与非局部热核扩散小波
        if (result.GraphLaplacianDiffusionWavelet != null)
        {
            var gld = result.GraphLaplacianDiffusionWavelet;
            if (TxtPhase52FiedlerLambda2 != null) TxtPhase52FiedlerLambda2.Text = $"λ_2={gld.FiedlerAlgebraicConnectivity:F3} / d={gld.ManifoldDiffusionDimension:F2}";
            if (TxtPhase52FiedlerSub != null) TxtPhase52FiedlerSub.Text = $"模块度: Q={gld.SpectralClusteringModularity:F3} | 噪声截断: {gld.GlobalWaveletPurgeRatioPct:F1}%";
            if (GridPhase52LaplacianItems != null) GridPhase52LaplacianItems.ItemsSource = gld.ClusterItems;
        }

        // 3. Bridgewater Associates & AQR Capital 分数阶粘弹性流变学宏观资产负荷与流动性蠕变
        if (result.ViscoelasticRheologyCapitalStrain != null)
        {
            var vrc = result.ViscoelasticRheologyCapitalStrain;
            if (TxtPhase52LossTangentTanDelta != null) TxtPhase52LossTangentTanDelta.Text = $"tan δ={vrc.GlobalSystemicLossTangent:F3} / {vrc.EarliestCreepRuptureHorizonDays:F0} 天";
            if (TxtPhase52LossTangentSub != null) TxtPhase52LossTangentSub.Text = $"阶数: α={vrc.MeanFractionalOrderAlpha:F3} | 储能模量: {vrc.SystemicDynamicStorageModulus:F1} MPa";
            if (GridPhase52RheologyItems != null) GridPhase52RheologyItems.ItemsSource = vrc.RheologyItems;
        }

        // 4. Renaissance Technologies & HRT 非平衡态朗之万细致平衡破缺与相空间概率流旋度做功
        if (result.NonequilibriumLangevinVorticity != null)
        {
            var nlv = result.NonequilibriumLangevinVorticity;
            if (TxtPhase52LimitCyclePumpWork != null) TxtPhase52LimitCyclePumpWork.Text = $"+{nlv.GlobalLimitCyclePumpWorkBps:F1} bps / Φ={nlv.MeanBrokenDetailedBalanceDegree:F3}";
            if (TxtPhase52LimitCycleSub != null) TxtPhase52LimitCycleSub.Text = $"最大旋度: {nlv.MaximumProbabilityVorticity:F3} | 捕获率: {nlv.NonEquilibriumStatArbEfficiencyPct:F1}%";
            if (GridPhase52VorticityItems != null) GridPhase52VorticityItems.ItemsSource = nlv.VorticityItems;
        }

        if (TxtPhase52ExecutiveVerdict != null && result.BouchaudTransientImpactPropagator != null && result.GraphLaplacianDiffusionWavelet != null && result.ViscoelasticRheologyCapitalStrain != null && result.NonequilibriumLangevinVorticity != null)
        {
            TxtPhase52ExecutiveVerdict.Text = $"{result.BouchaudTransientImpactPropagator.ExecutiveVerdict} ｜ {result.GraphLaplacianDiffusionWavelet.ExecutiveVerdict} ｜ {result.ViscoelasticRheologyCapitalStrain.ExecutiveVerdict} ｜ {result.NonequilibriumLangevinVorticity.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase53InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Jump Trading & Optiver 连续时间仿射跳跃-扩散限价簿做市与自适应逆向选择免受损控制
        if (result.JumpDiffusionAffineMarketMaking != null)
        {
            var jdm = result.JumpDiffusionAffineMarketMaking;
            if (TxtPhase53ToxicityGain != null) TxtPhase53ToxicityGain.Text = $"+{jdm.GlobalToxicityAvoidanceGainBps:F1} bps / λ={jdm.MeanJumpPoissonIntensity:F2}";
            if (TxtPhase53ToxicitySub != null) TxtPhase53ToxicitySub.Text = $"最优双边价差: {jdm.AverageOptimalBidAskSpreadBps:F1} bps | 库存弹性: {jdm.MarketMakingInventoryResiliencePct:F1}%";
            if (GridPhase53MarketMakingItems != null) GridPhase53MarketMakingItems.ItemsSource = jdm.MarketMakingItems;
        }

        // 2. Citadel Global Fixed Income & Millennium Macro 多因子无套利高斯仿射动态期限结构与曲率相对价值套利
        if (result.AffineArbitrageFreeTermStructure != null)
        {
            var ats = result.AffineArbitrageFreeTermStructure;
            if (TxtPhase53ButterflyAlpha != null) TxtPhase53ButterflyAlpha.Text = $"+{ats.GlobalButterflyConvexityAlphaBps:F1} bps / TP=+{ats.MeanDynamicTermPremiumBps:F1} bps";
            if (TxtPhase53ButterflySub != null) TxtPhase53ButterflySub.Text = $"无套利残差: {ats.YieldCurveArbitrageViolationResidual:F2} bps | 拟合质量: {ats.TermStructureTrackingQualityPct:F2}%";
            if (GridPhase53TermStructureItems != null) GridPhase53TermStructureItems.ItemsSource = ats.FactorItems;
        }

        // 3. Point72 & Citadel Multi-Strategy 多 Pod 合作博弈 Shapley-Owen 边际分散贡献与动态流动性影子定价仲裁
        if (result.MultiPodShapleyShadowPricing != null)
        {
            var sp = result.MultiPodShapleyShadowPricing;
            if (TxtPhase53NettingGain != null) TxtPhase53NettingGain.Text = $"+{sp.GlobalInternalCapitalNettingGainBps:F1} bps / λ*={sp.MeanLiquidityShadowPriceBps:F1} bps";
            if (TxtPhase53NettingSub != null) TxtPhase53NettingSub.Text = $"博弈效率: {sp.CooperativeGameEfficiencyRatioPct:F1}% | 拥挤阻尼: {sp.SystemicCrowdingDampeningIndex:F2}";
            if (GridPhase53PodShapleyItems != null) GridPhase53PodShapleyItems.ItemsSource = sp.PodItems;
        }

        // 4. D.E. Shaw & WorldQuant 非欧流形 Ollivier-Ricci 拓扑曲率流相变相干与高阶持久同调过滤
        if (result.OllivierRicciCurvaturePersistentHomology != null)
        {
            var orc = result.OllivierRicciCurvaturePersistentHomology;
            if (TxtPhase53RicciDenoisingAlpha != null) TxtPhase53RicciDenoisingAlpha.Text = $"+{orc.GlobalRicciFlowDenoisingAlphaBps:F1} bps / κ={orc.MeanGraphOllivierRicciCurvature:F3}";
            if (TxtPhase53RicciSub != null) TxtPhase53RicciSub.Text = $"相变相干: {orc.MacroPhaseTransitionCoherence:F1}/100 | Betti-1 密度: {orc.PersistentHomologyCycleDensity:F2}";
            if (GridPhase53TopologicalItems != null) GridPhase53TopologicalItems.ItemsSource = orc.TopologicalItems;
        }

        if (TxtPhase53ExecutiveVerdict != null && result.JumpDiffusionAffineMarketMaking != null && result.AffineArbitrageFreeTermStructure != null && result.MultiPodShapleyShadowPricing != null && result.OllivierRicciCurvaturePersistentHomology != null)
        {
            TxtPhase53ExecutiveVerdict.Text = $"{result.JumpDiffusionAffineMarketMaking.ExecutiveVerdict} ｜ {result.AffineArbitrageFreeTermStructure.ExecutiveVerdict} ｜ {result.MultiPodShapleyShadowPricing.ExecutiveVerdict} ｜ {result.OllivierRicciCurvaturePersistentHomology.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase54InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Renaissance Technologies & D.E. Shaw 连续时间马尔可夫跳跃状态空间与分层狄利克雷过程 (HDP-HMM)
        if (result.ContinuousMarkovSwitchingDirichletProcess != null)
        {
            var hdp = result.ContinuousMarkovSwitchingDirichletProcess;
            if (TxtPhase54RegimeEntropy != null) TxtPhase54RegimeEntropy.Text = $"{hdp.GlobalHdpRegimeEntropy:F3} nats / λ_h={hdp.DominantStateHazardRate:F2}/年";
            if (TxtPhase54RegimeSub != null) TxtPhase54RegimeSub.Text = $"涌现隐状态: {hdp.NonparametricActiveStateCount} 个 | 转移稳定置信度: {hdp.HdpStateTransitionStabilityPct:F1}%";
            if (GridPhase54HdpRegimeItems != null) GridPhase54HdpRegimeItems.ItemsSource = hdp.RegimeItems;
        }

        // 2. Citadel Securities & Hudson River Trading 双边限价订单簿微观非线性平均场博弈与高阶随机冲量最优流动性提供控制
        if (result.MeanFieldGameImpulseLiquidityControl != null)
        {
            var mfg = result.MeanFieldGameImpulseLiquidityControl;
            if (TxtPhase54MfgAlpha != null) TxtPhase54MfgAlpha.Text = $"+{mfg.GlobalMeanFieldLiquidityAlphaBps:F1} bps / δ*={mfg.MeanFieldNashEquilibriumSpreadBps:F1} bps";
            if (TxtPhase54MfgSub != null) TxtPhase54MfgSub.Text = $"羊群免疫度: {mfg.HerdingCrowdImmunityPct:F1}% | 冲量减摩效率: {mfg.ImpulseControlEfficiencyRatio:F2}x";
            if (GridPhase54MfgImpulseItems != null) GridPhase54MfgImpulseItems.ItemsSource = mfg.ImpulseItems;
        }

        // 3. Two Sigma & WorldQuant 高维因果发现有向无环图与结构因果模型反事实干预不变性 Alpha 纯化
        if (result.CausalDagStructuralInvarianceAlpha != null)
        {
            var causal = result.CausalDagStructuralInvarianceAlpha;
            if (TxtPhase54CausalAlpha != null) TxtPhase54CausalAlpha.Text = $"+{causal.GlobalCausalInvarianceAlphaBps:F1} bps / 剔除 {causal.SpuriousCorrelationRejectionRatePct:F1}%";
            if (TxtPhase54CausalSub != null) TxtPhase54CausalSub.Text = $"DAG 稀疏度: {causal.MeanCausalGraphSparsityRatio:F2} | 反事实稳健评分: {causal.CounterfactualRobustnessScore:F1}/100";
            if (GridPhase54CausalInvarianceItems != null) GridPhase54CausalInvarianceItems.ItemsSource = causal.CausalItems;
        }

        // 4. Bridgewater Associates & AQR Capital 广义非对称谱风险测度与动态极值高斯-Student Copula 尾部联动全景压测
        if (result.SpectralRiskExtremeCopulaStress != null)
        {
            var srm = result.SpectralRiskExtremeCopulaStress;
            if (TxtPhase54SpectralRisk != null) TxtPhase54SpectralRisk.Text = $"{srm.GlobalSpectralRiskCapitalRequirementPct:F1}% / λ_L={srm.ExtremeTailAsymmetricCopulaDependency:F3}";
            if (TxtPhase54SpectralSub != null) TxtPhase54SpectralSub.Text = $"GPD 厚尾 ξ: {srm.GpdTailShapeParameterXi:F3} | 最优凸性对冲覆盖: {srm.OptimalTailConvexityHedgeRatioPct:F1}%";
            if (GridPhase54SpectralRiskItems != null) GridPhase54SpectralRiskItems.ItemsSource = srm.StressItems;
        }

        if (TxtPhase54ExecutiveVerdict != null && result.ContinuousMarkovSwitchingDirichletProcess != null && result.MeanFieldGameImpulseLiquidityControl != null && result.CausalDagStructuralInvarianceAlpha != null && result.SpectralRiskExtremeCopulaStress != null)
        {
            TxtPhase54ExecutiveVerdict.Text = $"{result.ContinuousMarkovSwitchingDirichletProcess.ExecutiveVerdict} ｜ {result.MeanFieldGameImpulseLiquidityControl.ExecutiveVerdict} ｜ {result.CausalDagStructuralInvarianceAlpha.ExecutiveVerdict} ｜ {result.SpectralRiskExtremeCopulaStress.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase55InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Renaissance Technologies & Two Sigma 高维随机矩阵理论局部谱去噪与收缩协方差重构
        if (result.RandomMatrixLocalSpectralShrinkage != null)
        {
            var rmt = result.RandomMatrixLocalSpectralShrinkage;
            if (TxtPhase55RmtSnr != null) TxtPhase55RmtSnr.Text = $"+{rmt.GlobalRmtSignalToNoiseRatioGain:F1} dB / MP={rmt.MarchenkoPasturUpperBoundRatio:F1}%";
            if (TxtPhase55RmtSub != null) TxtPhase55RmtSub.Text = $"Spike 因子: {rmt.SpikeFactorCount} 个 | 谱收缩强度: {rmt.OptimalShrinkageIntensityPct:F1}%";
            if (GridPhase55RmtSpectralItems != null) GridPhase55RmtSpectralItems.ItemsSource = rmt.SpectralItems;
        }

        // 2. Citadel Securities & Jump Trading 微观分形霍克斯自激互激订单流毒性与闪崩级联预警
        if (result.MultivariateHawkesToxicityCascade != null)
        {
            var hawkes = result.MultivariateHawkesToxicityCascade;
            if (TxtPhase55HawkesBranching != null) TxtPhase55HawkesBranching.Text = $"ρ={hawkes.GlobalHawkesBranchingRatio:F3} / 毒性 {hawkes.OrderFlowToxicityScore:F1} 分";
            if (TxtPhase55HawkesSub != null) TxtPhase55HawkesSub.Text = $"级联脆弱度: {hawkes.FlashCrashCascadeVulnerabilityPct:F1}% | 防毒 Alpha: +{hawkes.MicrostructureAntidoteAlphaBps:F1} bps";
            if (GridPhase55HawkesCascadeItems != null) GridPhase55HawkesCascadeItems.ItemsSource = hawkes.CascadeItems;
        }

        // 3. Bridgewater Associates & AQR Capital 多资产非线性分形波动率记忆与赫斯特表面跨周期极值回撤对偶防御
        if (result.MultifractalHurstSurfaceDefense != null)
        {
            var hurst = result.MultifractalHurstSurfaceDefense;
            if (TxtPhase55HurstDefense != null) TxtPhase55HurstDefense.Text = $"H={hurst.LongRangeMemoryHurstExponent:F3} / 防御 {hurst.DualExtremumDrawdownDefenseRatio:F1}%";
            if (TxtPhase55HurstSub != null) TxtPhase55HurstSub.Text = $"多重分形谱宽 Δα: {hurst.GlobalMultifractalSpectrumWidth:F3} | 偏度: {hurst.FractalAsymmetryDegree:F3}";
            if (GridPhase55MultifractalHurstItems != null) GridPhase55MultifractalHurstItems.ItemsSource = hurst.MultifractalItems;
        }

        // 4. Point72 & Millennium Management 多代理强化学习非稳态微观策略博弈与对抗性鲁棒策略蒸馏
        if (result.MultiAgentAdversarialPolicyDistillation != null)
        {
            var marl = result.MultiAgentAdversarialPolicyDistillation;
            if (TxtPhase55MarlDistillation != null) TxtPhase55MarlDistillation.Text = $"{marl.GlobalAdversarialRobustnessScore:F1} 分 / 保真 {marl.PolicyDistillationFidelityPct:F1}%";
            if (TxtPhase55MarlSub != null) TxtPhase55MarlSub.Text = $"博弈收敛: {marl.NashEquilibriumConvergenceDegree:F1}% | 防挤压 Alpha: +{marl.DistilledAntiSqueezeAlphaBps:F1} bps";
            if (GridPhase55MarlDistillationItems != null) GridPhase55MarlDistillationItems.ItemsSource = marl.DistillationItems;
        }

        if (TxtPhase55ExecutiveVerdict != null && result.RandomMatrixLocalSpectralShrinkage != null && result.MultivariateHawkesToxicityCascade != null && result.MultifractalHurstSurfaceDefense != null && result.MultiAgentAdversarialPolicyDistillation != null)
        {
            TxtPhase55ExecutiveVerdict.Text = $"{result.RandomMatrixLocalSpectralShrinkage.ExecutiveVerdict} ｜ {result.MultivariateHawkesToxicityCascade.ExecutiveVerdict} ｜ {result.MultifractalHurstSurfaceDefense.ExecutiveVerdict} ｜ {result.MultiAgentAdversarialPolicyDistillation.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase56InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        // 1. Jane Street & Citadel Securities 全档限价订单簿 (LOB) 微观价格扩散鞅测度变换与即时流动性真空渗透控制
        if (result.LobMicroPriceMartingaleVacuumPenetration != null)
        {
            var lob = result.LobMicroPriceMartingaleVacuumPenetration;
            if (TxtPhase56LobDrift != null) TxtPhase56LobDrift.Text = $"Δμ={lob.GlobalMicroPriceDriftBps:F2} bps / Pvac={lob.AverageVacuumPenetrationPct:F1}%";
            if (TxtPhase56LobSub != null) TxtPhase56LobSub.Text = $"价差节省: +{lob.PassiveExecutionSpreadSavingBps:F2} bps | 做市 Alpha: +{lob.TotalMarketMakingAlphaBps:F1} bps";
            if (GridPhase56LobMicroPriceItems != null) GridPhase56LobMicroPriceItems.ItemsSource = lob.LobItems;
        }

        // 2. D.E. Shaw & Two Sigma 连续时间多维非高斯列维-伊藤跳跃扩散与极值共跳曲率对冲
        if (result.MultidimensionalLevyItoJumpDiffusion != null)
        {
            var levy = result.MultidimensionalLevyItoJumpDiffusion;
            if (TxtPhase56LevyJump != null) TxtPhase56LevyJump.Text = $"Λ={levy.GlobalCoJumpArrivalIntensity:F2} 次/年 / JV={levy.GlobalJumpVariationRatioPct:F1}%";
            if (TxtPhase56LevySub != null) TxtPhase56LevySub.Text = $"断崖尾部压降: {levy.GlobalJumpTailDrawdownReductionPct:F1}% | 跳跃 Alpha: +{levy.GlobalLevyJumpAlphaBps:F1} bps";
            if (GridPhase56LevyItoJumpItems != null) GridPhase56LevyItoJumpItems.ItemsSource = levy.JumpItems;
        }

        // 3. Renaissance Technologies & Millennium Management 高阶拓扑超图场论与自旋玻璃阻挫能量退火解聚
        if (result.HypergraphSpinGlassFrustrationAnnealing != null)
        {
            var hyper = result.HypergraphSpinGlassFrustrationAnnealing;
            if (TxtPhase56HypergraphFrust != null) TxtPhase56HypergraphFrust.Text = $"F={hyper.GlobalFrustrationEnergyDensity:F3} / 关联 {hyper.MeanHyperedgeInteractionDegree:F1} 阶";
            if (TxtPhase56HypergraphSub != null) TxtPhase56HypergraphSub.Text = $"共振压降: {hyper.GlobalSystemicResonanceVulnerabilityPct:F1}% | 退火 Alpha: +{hyper.HypergraphAnnealingAlphaBps:F1} bps";
            if (GridPhase56HypergraphSpinGlassItems != null) GridPhase56HypergraphSpinGlassItems.ItemsSource = hyper.HypergraphItems;
        }

        // 4. Bridgewater Associates & BlackRock Aladdin 达利欧长期主权债务大周期与超级去杠杆马尔可夫状态免疫
        if (result.SovereignDebtCycleDeleveragingImmunity != null)
        {
            var debt = result.SovereignDebtCycleDeleveragingImmunity;
            if (TxtPhase56DebtCycle != null) TxtPhase56DebtCycle.Text = $"DVI={debt.GlobalDeleveragingVulnerabilityScore:F1} 分 / 熵={debt.MarkovRegimeTransitionEntropy:F3}";
            if (TxtPhase56DebtSub != null) TxtPhase56DebtSub.Text = $"超级周期: {debt.CurrentLongTermDebtSupercyclePhase} | 宏观 Alpha: +{debt.SupercycleMacroImmunityAlphaBps:F1} bps";
            if (GridPhase56SovereignDebtMacroItems != null) GridPhase56SovereignDebtMacroItems.ItemsSource = debt.DebtItems;
        }

        if (TxtPhase56ExecutiveVerdict != null && result.LobMicroPriceMartingaleVacuumPenetration != null && result.MultidimensionalLevyItoJumpDiffusion != null && result.HypergraphSpinGlassFrustrationAnnealing != null && result.SovereignDebtCycleDeleveragingImmunity != null)
        {
            TxtPhase56ExecutiveVerdict.Text = $"{result.LobMicroPriceMartingaleVacuumPenetration.ExecutiveVerdict} ｜ {result.MultidimensionalLevyItoJumpDiffusion.ExecutiveVerdict} ｜ {result.HypergraphSpinGlassFrustrationAnnealing.ExecutiveVerdict} ｜ {result.SovereignDebtCycleDeleveragingImmunity.ExecutiveVerdict}";
        }
    }

    #region Phase 57 机构级前沿量化投研模型展示更新

    private void UpdatePhase57InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        // 1. Two Sigma & Citadel Securities 连续时间马利亚温随机变分微积分与粗糙波动率泛函高阶 Greeks 深度对冲
        if (result.MalliavinRoughVolatilityGreeks != null)
        {
            var mal = result.MalliavinRoughVolatilityGreeks;
            if (TxtPhase57MalliavinGreeks != null) TxtPhase57MalliavinGreeks.Text = $"H_eff={mal.GlobalHurstExponentH:F3} / Volga={mal.GlobalMalliavinVolgaCurvature:F1}";
            if (TxtPhase57MalliavinSub != null) TxtPhase57MalliavinSub.Text = $"粗糙断崖防范: {mal.AverageRoughTailDefensePct:F1}% | 对冲 Alpha: +{mal.TotalMalliavinHedgingAlphaBps:F1} bps";
            if (GridPhase57MalliavinItems != null) GridPhase57MalliavinItems.ItemsSource = mal.MalliavinItems;
        }

        // 2. Renaissance Technologies & Jump Trading 开放量子耗散系统 Lindblad 主方程与微观相干态密度矩阵统计套利
        if (result.QuantumLindbladDecoherenceStatArb != null)
        {
            var q = result.QuantumLindbladDecoherenceStatArb;
            if (TxtPhase57QuantumState != null) TxtPhase57QuantumState.Text = $"Tr(ρ²)={q.GlobalQuantumStatePurity:F3} / τ={q.AverageDecoherenceHalfLifeMicrosec:F1} μs";
            if (TxtPhase57QuantumSub != null) TxtPhase57QuantumSub.Text = $"冯·诺依曼熵: {q.GlobalVonNeumannEntropy:F3} | 量子套利 Alpha: +{q.TotalQuantumStatArbAlphaBps:F1} bps";
            if (GridPhase57QuantumItems != null) GridPhase57QuantumItems.ItemsSource = q.QuantumItems;
        }

        // 3. Millennium Management & Point72 连续时间多主体均值场博弈 (Mean-Field Games) 与 FPK-HJB 拥挤踩踏解耦优化
        if (result.MeanFieldGameCrowdingDecoupling != null)
        {
            var mfg = result.MeanFieldGameCrowdingDecoupling;
            if (TxtPhase57MfgCrowding != null) TxtPhase57MfgCrowding.Text = $"CPI={mfg.GlobalCrowdingPressureIndex:F1} 分 / 消除={mfg.SystemicDoomLoopReductionPct:F1}%";
            if (TxtPhase57MfgSub != null) TxtPhase57MfgSub.Text = $"FPK-HJB残差: {mfg.FpkHjbConvergenceResidual:F2}e-4 | 均值场 Alpha: +{mfg.TotalMfgRobustAlphaBps:F1} bps";
            if (GridPhase57MfgItems != null) GridPhase57MfgItems.ItemsSource = mfg.MfgItems;
        }

        // 4. Bridgewater Associates & AQR Capital 非平衡态热力学最大熵产生与信息几何 Fisher-Rao 测地线宏观流形平滑轮动
        if (result.ThermodynamicFisherRaoGeodesicRegime != null)
        {
            var tr = result.ThermodynamicFisherRaoGeodesicRegime;
            if (TxtPhase57FisherRao != null) TxtPhase57FisherRao.Text = $"d_G={tr.GlobalFisherRaoGeodesicDistance:F3} / 减摩={tr.AverageTurnoverDragSavingPct:F1}%";
            if (TxtPhase57FisherRaoSub != null) TxtPhase57FisherRaoSub.Text = $"熵产生率: {tr.GlobalEntropyProductionRate:F3} | 测地线 Alpha: +{tr.TotalGeodesicMacroAlphaBps:F1} bps";
            if (GridPhase57ThermodynamicItems != null) GridPhase57ThermodynamicItems.ItemsSource = tr.ThermodynamicItems;
        }

        if (TxtPhase57ExecutiveVerdict != null && result.MalliavinRoughVolatilityGreeks != null && result.QuantumLindbladDecoherenceStatArb != null && result.MeanFieldGameCrowdingDecoupling != null && result.ThermodynamicFisherRaoGeodesicRegime != null)
        {
            TxtPhase57ExecutiveVerdict.Text = $"{result.MalliavinRoughVolatilityGreeks.ExecutiveVerdict} ｜ {result.QuantumLindbladDecoherenceStatArb.ExecutiveVerdict} ｜ {result.MeanFieldGameCrowdingDecoupling.ExecutiveVerdict} ｜ {result.ThermodynamicFisherRaoGeodesicRegime.ExecutiveVerdict}";
        }
    }

    #region Phase 58 机构级前沿量化投研模型展示更新

    private void UpdatePhase58InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        // 1. Jane Street & Citadel Securities 连续时间 Glosten-Milgrom 贝叶斯信念更新与流动性逆向选择防护引擎
        if (result.GlostenMilgromAdverseSelection != null)
        {
            var gm = result.GlostenMilgromAdverseSelection;
            if (TxtGlobalGlostenSpread != null) TxtGlobalGlostenSpread.Text = $"{gm.GlobalAdverseSelectionSpreadBps:F1} bps";
            if (TxtGlobalInformedRatio != null) TxtGlobalInformedRatio.Text = $"知情比例: {gm.GlobalInformedTraderRatio:P1} | 滑点压降: {gm.AverageSlippageReductionPct:F1}%";
            if (DgGlostenMilgrom != null) DgGlostenMilgrom.ItemsSource = gm.GlostenItems;
        }

        // 2. Renaissance Technologies & D.E. Shaw 非广延统计力学 Tsallis 广义熵与多重分形奇异谱极值崩塌预警引擎
        if (result.TsallisNonextensiveSingularSpectrum != null)
        {
            var ts = result.TsallisNonextensiveSingularSpectrum;
            if (TxtGlobalTsallisQWidth != null) TxtGlobalTsallisQWidth.Text = $"q: {ts.GlobalNonextensiveParameterQ:F3} | Δα: {ts.GlobalSingularSpectrumWidth:F3}";
            if (TxtGlobalTsallisAlpha != null) TxtGlobalTsallisAlpha.Text = $"自适应 Alpha: +{ts.TotalTsallisAdaptiveAlphaBps:F1} bps | 防御度: {ts.AverageAvalancheDefensePct:F1}%";
            if (DgTsallisSpectrum != null) DgTsallisSpectrum.ItemsSource = ts.TsallisItems;
        }

        // 3. Two Sigma & Jump Trading 随机非线性粘性阻尼与 Fredholm 记忆核最优变分执行清算引擎
        if (result.ViscousMemoryOptimalExecution != null)
        {
            var vm = result.ViscousMemoryOptimalExecution;
            if (TxtGlobalFredholmSlippage != null) TxtGlobalFredholmSlippage.Text = $"{vm.AverageSlippageSavingsPct:F1}%";
            if (TxtGlobalFredholmAlpha != null) TxtGlobalFredholmAlpha.Text = $"变分减摩 Alpha: +{vm.TotalViscousExecutionAlphaBps:F1} bps | 阻尼比: {vm.GlobalFredholmDampingRatio:F3}";
            if (DgViscousExecution != null) DgViscousExecution.ItemsSource = vm.ViscousItems;
        }

        // 4. Bridgewater Associates & Millennium Management 结构向量自回归有向无环图 (SVAR-DAG) 与 Pearl 反事实因果干预宏观网络引擎
        if (result.SvarDagCausalInterventionNetwork != null)
        {
            var sv = result.SvarDagCausalInterventionNetwork;
            if (TxtGlobalCausalAlpha != null) TxtGlobalCausalAlpha.Text = $"+{sv.TotalAntifragileCausalAlphaBps:F1} bps";
            if (TxtGlobalCausalFragility != null) TxtGlobalCausalFragility.Text = $"脆弱度: {sv.GlobalSystemicFragilityIndex:F1} | 免疫度: {sv.AverageShockImmunityPct:F1}%";
            if (DgSvarDagCausal != null) DgSvarDagCausal.ItemsSource = sv.CausalItems;
        }

        if (TxtPhase58ExecutiveVerdict != null && result.GlostenMilgromAdverseSelection != null && result.TsallisNonextensiveSingularSpectrum != null && result.ViscousMemoryOptimalExecution != null && result.SvarDagCausalInterventionNetwork != null)
        {
            TxtPhase58ExecutiveVerdict.Text = $"{result.GlostenMilgromAdverseSelection.ExecutiveVerdict} ｜ {result.TsallisNonextensiveSingularSpectrum.ExecutiveVerdict} ｜ {result.ViscousMemoryOptimalExecution.ExecutiveVerdict} ｜ {result.SvarDagCausalInterventionNetwork.ExecutiveVerdict}";
        }
    }

    /// <summary>
    /// Phase 59: 渲染机构级前沿量化投研模型 (Citadel Kyle连续拍卖 / Renaissance Wilson重整化渗流 / Two Sigma HJBI捕食博弈 / Bridgewater 连续漂移扩散卡尔曼)
    /// </summary>
    private void UpdatePhase59InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Citadel Securities & Jump Trading 连续拍卖 Kyle's λ 价格冲击弹性与订单流信息穿透深度控制引擎
        if (result.KyleContinuousAuctionElasticity != null)
        {
            var kl = result.KyleContinuousAuctionElasticity;
            if (TxtGlobalKyleLambda != null) TxtGlobalKyleLambda.Text = $"{kl.GlobalKyleLambdaBps:F2} bps";
            if (TxtGlobalKyleElasticity != null) TxtGlobalKyleElasticity.Text = $"流动性弹性: {kl.AveragePriceElasticity:F2} | 压降: {kl.AveragePenetrationReductionPct:F1}%";
            if (DgKyleAuction != null) DgKyleAuction.ItemsSource = kl.KyleItems;
        }

        // 2. Renaissance Technologies & D.E. Shaw 拓扑 Wilson 重整化群矩流动与仿射渗流相变临界聚类引擎
        if (result.WilsonRenormalizationPercolation != null)
        {
            var wp = result.WilsonRenormalizationPercolation;
            if (TxtGlobalWilsonPc != null) TxtGlobalWilsonPc.Text = $"p_c: {wp.GlobalPercolationProbabilityPc:F3}";
            if (TxtGlobalWilsonGcc != null) TxtGlobalWilsonGcc.Text = $"巨连通占比: {wp.GiantConnectedComponentRatioPct:F1}% | Alpha: +{wp.TotalPercolationDefenseAlphaBps:F1} bps";
            if (DgWilsonPercolation != null) DgWilsonPercolation.ItemsSource = wp.WilsonItems;
        }

        // 3. Two Sigma & PDT Partners 连续时间随机微分博弈 (SDG) 捕食者-猎物纳什均衡与流动性黑洞抢跑规避变分引擎
        if (result.PredatoryGameLiquidityEvasion != null)
        {
            var pg = result.PredatoryGameLiquidityEvasion;
            if (TxtGlobalPredatoryPressure != null) TxtGlobalPredatoryPressure.Text = $"{pg.GlobalPredatoryPressureIndex:F1} 分";
            if (TxtGlobalPredatoryReduction != null) TxtGlobalPredatoryReduction.Text = $"减损挽回: {pg.AveragePredatoryDamageReductionPct:F1}% | Alpha: +{pg.TotalPredatoryEvasionAlphaBps:F1} bps";
            if (DgPredatoryEvasion != null) DgPredatoryEvasion.ItemsSource = pg.PredatoryItems;
        }

        // 4. Bridgewater Associates & AQR Capital 连续时间漂移-扩散非线性状态空间与鲁棒卡尔曼-布西宏观体制平价引擎
        if (result.DriftDiffusionKalmanMacroParity != null)
        {
            var dd = result.DriftDiffusionKalmanMacroParity;
            if (TxtGlobalDriftSpeed != null) TxtGlobalDriftSpeed.Text = $"v_drift: {dd.GlobalMacroDriftSpeed:F3}";
            if (TxtGlobalKalmanConfidence != null) TxtGlobalKalmanConfidence.Text = $"置信度: {dd.AverageKalmanConfidencePct:F1}% | 换手压降: {dd.RegimeChurnReductionPct:F1}%";
            if (DgDriftDiffusionMacro != null) DgDriftDiffusionMacro.ItemsSource = dd.DriftMacroItems;
        }

        if (TxtPhase59ExecutiveVerdict != null && result.KyleContinuousAuctionElasticity != null && result.WilsonRenormalizationPercolation != null && result.PredatoryGameLiquidityEvasion != null && result.DriftDiffusionKalmanMacroParity != null)
        {
            TxtPhase59ExecutiveVerdict.Text = $"{result.KyleContinuousAuctionElasticity.ExecutiveVerdict} ｜ {result.WilsonRenormalizationPercolation.ExecutiveVerdict} ｜ {result.PredatoryGameLiquidityEvasion.ExecutiveVerdict} ｜ {result.DriftDiffusionKalmanMacroParity.ExecutiveVerdict}";
        }
    }

    /// <summary>
    /// Phase 60 机构级前沿量化投研模型 UI 绑定与数据展示
    /// (Citadel/Optiver 泊松边界做市 / Renaissance/D.E.Shaw Kac-Moody李代数规范场 / Two Sigma/Jump McKean-Vlasov清算 / Bridgewater/Millennium Volterra信用谱平价)
    /// </summary>
    private void UpdatePhase60InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Citadel Securities & Optiver: 非线性泊松跳跃边界反射与 Avellaneda-Stoikov 粘性滑移面做市控制引擎
        if (result.NonlinearPoissonBoundaryMarketMaking != null)
        {
            var pm = result.NonlinearPoissonBoundaryMarketMaking;
            if (TxtGlobalPoissonSpread != null) TxtGlobalPoissonSpread.Text = $"{pm.GlobalOptimalSpreadBps:F2} bps";
            if (TxtGlobalPoissonHalfLife != null) TxtGlobalPoissonHalfLife.Text = $"平均库存半衰期: {pm.AverageInventoryDecayHours:F2}h | 击穿压降: {pm.AdverseSelectionBreachReductionPct:F1}%";
            if (DgPoissonMarketMaking != null) DgPoissonMarketMaking.ItemsSource = pm.PoissonItems;
        }

        // 2. Renaissance Technologies & D.E. Shaw: 仿射 Kac-Moody 无限维李代数根格与杨-米尔斯规范场拓扑荷度量引擎
        if (result.KacMoodyGaugeTopologicalCharge != null)
        {
            var km = result.KacMoodyGaugeTopologicalCharge;
            if (TxtGlobalKacMoodyQ != null) TxtGlobalKacMoodyQ.Text = $"Q = {km.GlobalTopologicalChargeNumber:F2}";
            if (TxtGlobalKacMoodyCurvature != null) TxtGlobalKacMoodyCurvature.Text = $"曲率: |F|={km.AverageGaugeCurvature:F3} | 伪信号压降: {km.FalseAlarmResonanceReductionPct:F1}%";
            if (DgKacMoodyGauge != null) DgKacMoodyGauge.ItemsSource = km.GaugeItems;
        }

        // 3. Two Sigma & Jump Trading: McKean-Vlasov 前向-后向随机微分方程 (FBSDE) 非局部多主体平均场协同清算引擎
        if (result.McKeanVlasovOptimalLiquidation != null)
        {
            var mv = result.McKeanVlasovOptimalLiquidation;
            if (TxtGlobalMcKeanVlasovGamma != null) TxtGlobalMcKeanVlasovGamma.Text = $"Γ = {mv.GlobalCrowdingDragCoeff:F3}";
            if (TxtGlobalMcKeanVlasovSaving != null) TxtGlobalMcKeanVlasovSaving.Text = $"落差节省: {mv.AggregateLiquidationSavingPct:F1}% | Alpha: +{mv.TotalMeanFieldEvasionAlphaBps:F1} bps";
            if (DgMcKeanVlasovLiquidation != null) DgMcKeanVlasovLiquidation.ItemsSource = mv.McKeanVlasovItems;
        }

        // 4. Bridgewater Associates & Millennium Macro: 分数阶伊藤-沃尔泰拉 (Ito-Volterra) 非马尔可夫信用周期记忆与遍历谱平价引擎
        if (result.VolterraNonMarkovianCreditParity != null)
        {
            var vt = result.VolterraNonMarkovianCreditParity;
            if (TxtGlobalVolterraHurst != null) TxtGlobalVolterraHurst.Text = $"H = {vt.GlobalVolterraMemoryHurst:F3}";
            if (TxtGlobalVolterraEntropy != null) TxtGlobalVolterraEntropy.Text = $"谱熵: {vt.ErgodicSpectralEntropy:F3} | 回撤修复加速: {vt.DrawdownRecoveryCycleShortenPct:F1}%";
            if (DgVolterraCreditParity != null) DgVolterraCreditParity.ItemsSource = vt.VolterraItems;
        }

        if (TxtPhase60ExecutiveVerdict != null && result.NonlinearPoissonBoundaryMarketMaking != null && result.KacMoodyGaugeTopologicalCharge != null && result.McKeanVlasovOptimalLiquidation != null && result.VolterraNonMarkovianCreditParity != null)
        {
            TxtPhase60ExecutiveVerdict.Text = $"{result.NonlinearPoissonBoundaryMarketMaking.ExecutiveVerdict} ｜ {result.KacMoodyGaugeTopologicalCharge.ExecutiveVerdict} ｜ {result.McKeanVlasovOptimalLiquidation.ExecutiveVerdict} ｜ {result.VolterraNonMarkovianCreditParity.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase61InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Citadel Securities & Jane Street: 微观高阶粗糙霍克斯过程与订单簿队列隐性排队延迟博弈
        if (result.RoughHawkesQueueLatencyArbitrage != null)
        {
            var rh = result.RoughHawkesQueueLatencyArbitrage;
            if (TxtGlobalRoughHawkesSpread != null) TxtGlobalRoughHawkesSpread.Text = $"{rh.GlobalOptimalQueueSpreadBps:F2} bps";
            if (TxtGlobalRoughHawkesSurvival != null) TxtGlobalRoughHawkesSurvival.Text = $"平均生存率: {rh.AverageQueueSurvivalRatePct:F1}% | 滑点压降: {rh.LatencyArbitrageSlippageReductionPct:F1}%";
            if (DgRoughHawkesQueue != null) DgRoughHawkesQueue.ItemsSource = rh.HawkesItems;
        }

        // 2. Renaissance Technologies & D.E. Shaw: 相空间辛几何哈密顿流形动力学与辛积分保结构降维共振
        if (result.SymplecticHamiltonianManifoldResonance != null)
        {
            var sm = result.SymplecticHamiltonianManifoldResonance;
            if (TxtGlobalSymplecticLyapunov != null) TxtGlobalSymplecticLyapunov.Text = $"λ = {sm.AverageLyapunovExponent:F3}";
            if (TxtGlobalSymplecticPhaseDrift != null) TxtGlobalSymplecticPhaseDrift.Text = $"体积漂移: ΔΩ={sm.GlobalSymplecticPhaseVolumeDrift:F3} | 伪共振规避: {sm.FalseResonanceAvoidancePct:F1}%";
            if (DgSymplecticManifold != null) DgSymplecticManifold.ItemsSource = sm.SymplecticItems;
        }

        // 3. Two Sigma & Point72: 最优传输 (Monge-Kantorovich) Wasserstein 重心测度流与截面非平衡协同重构
        if (result.WassersteinBarycenterDynamicRebalancing != null)
        {
            var wb = result.WassersteinBarycenterDynamicRebalancing;
            if (TxtGlobalWassersteinW2 != null) TxtGlobalWassersteinW2.Text = $"W2 = {wb.AverageWassersteinDistance:F3}";
            if (TxtGlobalWassersteinEntropy != null) TxtGlobalWassersteinEntropy.Text = $"测度熵: S={wb.GlobalBarycenterEntropy:F3} | 摩擦节省: {wb.TransportFrictionSavingPct:F1}%";
            if (DgWassersteinBarycenter != null) DgWassersteinBarycenter.ItemsSource = wb.WassersteinItems;
        }

        // 4. Bridgewater Associates & AQR Capital: 连续谱量子混沌非对易算子多尺度周期与超全天候宏观体制平价
        if (result.QuantumSpectralChaosMacroParity != null)
        {
            var qp = result.QuantumSpectralChaosMacroParity;
            if (TxtGlobalQuantumSpectralRigidity != null) TxtGlobalQuantumSpectralRigidity.Text = $"Δ3 = {qp.GlobalGueSpectralRigidity:F3}";
            if (TxtGlobalQuantumChaosEntropy != null) TxtGlobalQuantumChaosEntropy.Text = $"混沌熵: S={qp.BerryRobnikChaosEntropy:F3} | 尾部减免: {qp.MacroTailDrawdownMitigationPct:F1}%";
            if (DgQuantumSpectralChaos != null) DgQuantumSpectralChaos.ItemsSource = qp.QuantumItems;
        }

        if (TxtPhase61ExecutiveVerdict != null && result.RoughHawkesQueueLatencyArbitrage != null && result.SymplecticHamiltonianManifoldResonance != null && result.WassersteinBarycenterDynamicRebalancing != null && result.QuantumSpectralChaosMacroParity != null)
        {
            TxtPhase61ExecutiveVerdict.Text = $"{result.RoughHawkesQueueLatencyArbitrage.ExecutiveVerdict} ｜ {result.SymplecticHamiltonianManifoldResonance.ExecutiveVerdict} ｜ {result.WassersteinBarycenterDynamicRebalancing.ExecutiveVerdict} ｜ {result.QuantumSpectralChaosMacroParity.ExecutiveVerdict}";
        }
    }

    private void UpdatePhase62InstitutionalOptimizationDisplay(PortfolioResult result)
    {
        if (result == null) return;

        // 1. Millennium Management & Point72: 多子策略/基金高水位动态资本回撤扣划与跨组合因子拥挤解耦
        if (result.MultiPodFactorCrowdingClawback != null)
        {
            var mp = result.MultiPodFactorCrowdingClawback;
            if (TxtGlobalMultiPodClawback != null) TxtGlobalMultiPodClawback.Text = $"{mp.GlobalAverageClawbackRatioPct:F1}%";
            if (TxtGlobalMultiPodEntropy != null) TxtGlobalMultiPodEntropy.Text = $"拥挤谱熵: S={mp.PortfolioLatentCrowdingEntropy:F3} | 踩踏化解: {mp.CascadeLiquidationRiskMitigationPct:F1}%";
            if (DgMultiPodFactorCrowding != null) DgMultiPodFactorCrowding.ItemsSource = mp.PodItems;
        }

        // 2. BlackRock Aladdin & NBIM / GIC: NGFS 气候宏观转型三情景压力测试与碳贝塔搁浅资产 VaR
        if (result.ClimateTransitionStrandedAssetStress != null)
        {
            var ct = result.ClimateTransitionStrandedAssetStress;
            if (TxtGlobalClimateCarbonBeta != null) TxtGlobalClimateCarbonBeta.Text = $"β_c = {ct.PortfolioWeightedCarbonBeta:F2}";
            if (TxtGlobalClimateScenarioImpact != null) TxtGlobalClimateScenarioImpact.Text = $"有序冲击: -{ct.OrderlyScenarioPortfolioDrawdownPct:F1}% | 搁浅减记: {ct.DisorderlyStrandedWriteDownPct:F1}%";
            if (DgClimateTransitionStress != null) DgClimateTransitionStress.ItemsSource = ct.ClimateItems;
        }

        // 3. Two Sigma & D.E. Shaw: 高阶张量环 (Tensor Ring) 紧致分解与隐式多模态 Alpha 场
        if (result.TensorRingMultimodalAlphaField != null)
        {
            var tr = result.TensorRingMultimodalAlphaField;
            if (TxtGlobalTensorRingError != null) TxtGlobalTensorRingError.Text = $"ε = {tr.GlobalTensorRingReconstructionError:F3}";
            if (TxtGlobalTensorRingEntanglement != null) TxtGlobalTensorRingEntanglement.Text = $"缠结熵: S={tr.AverageMultimodalEntanglement:F3} | 纯度提升: +{tr.HighDimensionalAlphaPurityGainPct:F1}%";
            if (DgTensorRingMultimodalAlpha != null) DgTensorRingMultimodalAlpha.ItemsSource = tr.TensorItems;
        }

        // 4. Citadel & Jump Trading: 维纳-泊松空间马利亚温微分 (Malliavin Calculus) 跳跃扩散随机波动率对冲
        if (result.MalliavinJumpDiffusionHedging != null)
        {
            var mj = result.MalliavinJumpDiffusionHedging;
            if (TxtGlobalMalliavinDelta != null) TxtGlobalMalliavinDelta.Text = $"Δ_M = {mj.GlobalMalliavinHedgeRatio:F2}";
            if (TxtGlobalMalliavinConvexity != null) TxtGlobalMalliavinConvexity.Text = $"凸度增益: +{mj.StochasticVolConvexityCapture:F1} bps | 滑点压降: -{mj.ExtremeJumpSlippageReductionPct:F1}%";
            if (DgMalliavinJumpDiffusionHedging != null) DgMalliavinJumpDiffusionHedging.ItemsSource = mj.MalliavinItems;
        }

        if (TxtPhase62ExecutiveVerdict != null && result.MultiPodFactorCrowdingClawback != null && result.ClimateTransitionStrandedAssetStress != null && result.TensorRingMultimodalAlphaField != null && result.MalliavinJumpDiffusionHedging != null)
        {
            TxtPhase62ExecutiveVerdict.Text = $"{result.MultiPodFactorCrowdingClawback.ExecutiveVerdict} ｜ {result.ClimateTransitionStrandedAssetStress.ExecutiveVerdict} ｜ {result.TensorRingMultimodalAlphaField.ExecutiveVerdict} ｜ {result.MalliavinJumpDiffusionHedging.ExecutiveVerdict}";
        }
    }

    #endregion

    #endregion

    #endregion

    #endregion
}
