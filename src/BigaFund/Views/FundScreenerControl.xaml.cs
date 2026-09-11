using System.Windows;
using System.Windows.Controls;
using BigaFund.Models;
using BigaFund.Services;

namespace BigaFund.Views;

public partial class FundScreenerControl : UserControl
{
    private FundDataService? _dataService;
    private List<StoredFundItem> _allFunds = new();
    private List<StoredFundItem> _currentFilteredFunds = new();
    private List<MarketCatalogItem> _catalogFunds = new();
    private bool _isInitialized;
    private bool _isMarketMode => (CmbViewMode?.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "MARKET";

    public event Action<string>? OnSelectFundForMain;
    public event Action<string>? OnSelectFundForCompare;
    public event Action<List<string>>? OnSendFundsToPortfolio;
    public event Action<List<string>>? OnSendFundsToCompare;

    public FundScreenerControl()
    {
        InitializeComponent();
    }

    public async void Initialize(FundDataService dataService)
    {
        _dataService = dataService;
        if (!_isInitialized)
        {
            _isInitialized = true;
            await LoadStoredFundsAsync();
        }
    }

    public async Task LoadStoredFundsAsync()
    {
        if (_dataService == null) return;

        if (_isMarketMode)
        {
            await QueryMarketCatalogAsync();
            return;
        }

        TxtSummary.Text = "正在从 DuckDB 读取本地收录基金时序库...";

        try
        {
            _allFunds = await _dataService.DuckDb.GetAllStoredFundsAsync();

            // 若本地数据库为空，预填几只知名基准基金以供首次探索
            if (_allFunds.Count == 0)
            {
                var seedCodes = new[] { "000001", "005827", "161725", "161005", "000171" };
                foreach (var code in seedCodes)
                {
                    await _dataService.GetFundDetailAsync(code);
                }
                _allFunds = await _dataService.DuckDb.GetAllStoredFundsAsync();
            }

            if (_allFunds.Count > 0)
            {
                PopulateCrossSectionalScores(_allFunds);
            }

            ApplyFilters();
        }
        catch (Exception ex)
        {
            TxtSummary.Text = $"加载基金池异常: {ex.Message}";
        }
    }

    private async Task QueryMarketCatalogAsync()
    {
        if (_dataService == null) return;
        if (ScreenerProgress != null) ScreenerProgress.Visibility = Visibility.Visible;

        try
        {
            string kw = TxtKeyword.Text.Trim();
            string cat = (CmbCategory.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ALL";
            string sector = (CmbSector?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ALL";

            int totalCount = await _dataService.GetMarketCatalogCountAsync();
            if (totalCount == 0)
            {
                TxtSummary.Text = "本地尚未同步全市场公募目录，请点击右上角【⚡ 同步全市场公募目录】拉取 ~1.8 万只基金数据。";
                GridMarketCatalog.ItemsSource = null;
                return;
            }

            _catalogFunds = await _dataService.SearchMarketCatalogAsync(kw, cat, sector, limit: 300);
            GridMarketCatalog.ItemsSource = _catalogFunds;
            TxtSummary.Text = $"全市场公募大厅检索匹配: {_catalogFunds.Count} 只基金 (全市场本地库已建立索引总数: {totalCount} 只，展示前300条)";
        }
        catch (Exception ex)
        {
            TxtSummary.Text = $"全市场检索异常: {ex.Message}";
        }
        finally
        {
            if (ScreenerProgress != null) ScreenerProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyFilters()
    {
        if (!_isInitialized) return;

        if (_isMarketMode)
        {
            _ = QueryMarketCatalogAsync();
            return;
        }

        string kw = TxtKeyword?.Text?.Trim()?.ToLowerInvariant() ?? "";
        string cat = (CmbCategory?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ALL";
        string sector = (CmbSector?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ALL";
        string grade = (CmbGrade?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ALL";
        string strategy = (CmbStrategy?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ALL";

        decimal minReturn = 0;
        if (decimal.TryParse((CmbMinReturn?.SelectedItem as ComboBoxItem)?.Tag?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedRet)) minReturn = parsedRet;

        decimal maxDd = 100;
        if (decimal.TryParse((CmbMaxDrawdown?.SelectedItem as ComboBoxItem)?.Tag?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedDd)) maxDd = parsedDd;

        decimal minSharpe = -99;
        if (decimal.TryParse((CmbMinSharpe?.SelectedItem as ComboBoxItem)?.Tag?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedSharpe)) minSharpe = parsedSharpe;

        decimal minScore = 0;
        if (decimal.TryParse((CmbMinScore?.SelectedItem as ComboBoxItem)?.Tag?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedScore)) minScore = parsedScore;

        decimal wRet = 30m, wDd = 25m, wSharpe = 25m, wStab = 20m;
        if (TxtWeightReturn != null && decimal.TryParse(TxtWeightReturn.Text.Trim(), out var pRet)) wRet = Math.Max(0, pRet);
        if (TxtWeightDrawdown != null && decimal.TryParse(TxtWeightDrawdown.Text.Trim(), out var pDd)) wDd = Math.Max(0, pDd);
        if (TxtWeightSharpe != null && decimal.TryParse(TxtWeightSharpe.Text.Trim(), out var pSh)) wSharpe = Math.Max(0, pSh);
        if (TxtWeightStability != null && decimal.TryParse(TxtWeightStability.Text.Trim(), out var pSt)) wStab = Math.Max(0, pSt);

        foreach (var f in _allFunds)
        {
            f.CustomWeightedScore = CalculateCustomScore(f, wRet, wDd, wSharpe, wStab);
        }

        var filtered = _allFunds.Where(f =>
        {
            bool matchKw = string.IsNullOrEmpty(kw) ||
                           (f.Code != null && f.Code.ToLowerInvariant().Contains(kw)) ||
                           (f.Name != null && f.Name.ToLowerInvariant().Contains(kw)) ||
                           (f.Manager != null && f.Manager.ToLowerInvariant().Contains(kw));

            bool matchCat = cat == "ALL" || (!string.IsNullOrEmpty(f.Type) && f.Type.Contains(cat));
            bool matchSector = sector == "ALL" || FundSectorHelper.MatchesSector(f.Name, f.Type, sector);
            bool matchGrade = grade switch
            {
                "AAA" => f.RatingGrade == "AAA" || f.QuantScore >= 80m,
                "AA+" => f.RatingGrade == "AAA" || f.RatingGrade == "AA" || f.QuantScore >= 70m,
                "A+" => f.RatingGrade == "AAA" || f.RatingGrade == "AA" || f.RatingGrade == "A" || f.QuantScore >= 60m,
                _ => true
            };

            bool matchStrategy = strategy switch
            {
                "SCIENTIFIC_BUY" => f.ScientificActionSignal.Contains("买入") || f.ScientificActionSignal.Contains("吸筹") || f.MediumTermScore >= 65m,
                "DOUBLE_EXCELLENT" => (f.MediumTermScore >= 65m && f.ShortTermScore >= 60m) || f.ConcordanceType.Contains("共振") || f.ConcordanceType.Contains("双优"),
                "MEDIUM_DIP_ACCUM" => (f.MediumTermScore >= 65m && f.ShortTermScore < 50m) || f.ConcordanceType.Contains("超跌") || f.ConcordanceType.Contains("吸筹"),
                "LOW_VOL" => f.MaxDrawdown <= 15m && f.SharpeRatio >= 0.8m,
                "HIGH_ALPHA" => f.Return1Y >= 15m,
                "AAA_ONLY" => f.RatingGrade == "AAA" || f.QuantScore >= 80m,
                "CALMAR_ELITE" => f.CalmarRatio >= 1.2m || (f.MaxDrawdown > 0 && f.Return1Y / f.MaxDrawdown >= 1.0m),
                "SORTINO_HIGH" => f.SortinoRatio >= 1.2m || f.SharpeRatio >= 1.2m,
                "MORNINGSTAR_5STAR" => f.StarRating.Contains("★★★★★") || f.QuantScore >= 85m,
                _ => true
            };

            bool matchFunnel = true;
            if (minReturn > 0 && f.Return1Y < minReturn) matchFunnel = false;
            if (maxDd < 100 && f.MaxDrawdown > maxDd) matchFunnel = false;
            if (minSharpe > -90 && f.SharpeRatio < minSharpe) matchFunnel = false;
            if (minScore > 0 && f.QuantScore < minScore) matchFunnel = false;

            return matchKw && matchCat && matchSector && matchGrade && matchStrategy && matchFunnel;
        }).ToList();

        int sortIdx = CmbSort?.SelectedIndex ?? 0;
        filtered = sortIdx switch
        {
            1 => filtered.OrderByDescending(f => f.QuantScore).ToList(),
            2 => filtered.OrderByDescending(f => f.MultiFactorCompositeScore).ToList(),
            3 => filtered.OrderByDescending(f => f.MomentumScore).ToList(),
            4 => filtered.OrderByDescending(f => f.DownsideDefenseScore).ToList(),
            5 => filtered.OrderByDescending(f => f.AlphaPurityScore).ToList(),
            6 => filtered.OrderByDescending(f => f.CustomWeightedScore).ToList(),
            7 => filtered.OrderByDescending(f => f.Return1Y).ToList(),
            8 => filtered.OrderByDescending(f => f.SharpeRatio).ToList(),
            9 => filtered.OrderBy(f => f.MaxDrawdown).ToList(),
            10 => filtered.OrderByDescending(f => f.LatestNav).ToList(),
            11 => filtered.OrderByDescending(f => f.Passed4433 ? 1 : 0).ThenByDescending(f => f.QuantScore).ToList(),
            12 => filtered.OrderByDescending(f => f.AlphaPersistenceScore).ToList(),
            _ => filtered.OrderByDescending(f => f.UpdatedAt).ToList()
        };

        _currentFilteredFunds = filtered;
        if (GridFunds != null) GridFunds.ItemsSource = filtered;
        if (TxtSummary != null) TxtSummary.Text = $"当前筛选展示: {filtered.Count} 只基金 (本地深度投研库总计: {_allFunds.Count} 只)";
    }

    private void CmbFunnel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        ApplyFilters();
    }

    private void CmbGrade_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        ApplyFilters();
    }

    private void CmbStrategy_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        ApplyFilters();
    }

    private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_isMarketMode)
        {
            MessageBox.Show("请在【本地投研深度池】模式下导出详尽多维量化筛选结果。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_currentFilteredFunds.Count == 0)
        {
            MessageBox.Show("当前筛选列表为空，无法导出。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出基金多维量化筛选池 (CSV)",
            Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            FileName = $"BIGA_基金量化筛选池_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                ExportService.ExportScreenerToCsv(sfd.FileName, _currentFilteredFunds);
                var choice = MessageBox.Show($"已成功导出 {_currentFilteredFunds.Count} 只基金至:\n{sfd.FileName}\n\n是否立即在系统默认表格软件中打开查看？", "导出成功", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (choice == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出 CSV 失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CmbSector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        ApplyFilters();
    }

    public void SetSectorFilter(string sector)
    {
        if (CmbSector == null) return;
        foreach (ComboBoxItem item in CmbSector.Items)
        {
            if (item.Tag?.ToString() == sector || item.Content?.ToString()?.Contains(sector) == true)
            {
                CmbSector.SelectedItem = item;
                break;
            }
        }
    }

    private void CmbViewMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;

        if (_isMarketMode)
        {
            GridFunds.Visibility = Visibility.Collapsed;
            GridMarketCatalog.Visibility = Visibility.Visible;
            if (LblSort != null) LblSort.Visibility = Visibility.Collapsed;
            if (CmbSort != null) CmbSort.Visibility = Visibility.Collapsed;
            if (LblGrade != null) LblGrade.Visibility = Visibility.Collapsed;
            if (CmbGrade != null) CmbGrade.Visibility = Visibility.Collapsed;
            if (LblStrategy != null) LblStrategy.Visibility = Visibility.Collapsed;
            if (CmbStrategy != null) CmbStrategy.Visibility = Visibility.Collapsed;
            if (BtnSendToPortfolio != null) BtnSendToPortfolio.Visibility = Visibility.Collapsed;
            if (BtnSendToCompare != null) BtnSendToCompare.Visibility = Visibility.Collapsed;
            if (PanelCustomWeights != null) PanelCustomWeights.Visibility = Visibility.Collapsed;
            _ = QueryMarketCatalogAsync();
        }
        else
        {
            GridFunds.Visibility = Visibility.Visible;
            GridMarketCatalog.Visibility = Visibility.Collapsed;
            if (LblSort != null) LblSort.Visibility = Visibility.Visible;
            if (CmbSort != null) CmbSort.Visibility = Visibility.Visible;
            if (LblGrade != null) LblGrade.Visibility = Visibility.Visible;
            if (CmbGrade != null) CmbGrade.Visibility = Visibility.Visible;
            if (LblStrategy != null) LblStrategy.Visibility = Visibility.Visible;
            if (CmbStrategy != null) CmbStrategy.Visibility = Visibility.Visible;
            if (BtnSendToPortfolio != null) BtnSendToPortfolio.Visibility = Visibility.Visible;
            if (BtnSendToCompare != null) BtnSendToCompare.Visibility = Visibility.Visible;
            if (PanelCustomWeights != null) PanelCustomWeights.Visibility = Visibility.Visible;
            ApplyFilters();
        }
    }

    private void TxtKeyword_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized) return;
        ApplyFilters();
    }

    private void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        ApplyFilters();
    }

    private void CmbSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        ApplyFilters();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_isMarketMode)
        {
            await QueryMarketCatalogAsync();
        }
        else
        {
            await LoadStoredFundsAsync();
        }
    }

    private async void BtnSyncCatalog_Click(object sender, RoutedEventArgs e)
    {
        if (_dataService == null) return;
        if (ScreenerProgress != null) ScreenerProgress.Visibility = Visibility.Visible;
        TxtSummary.Text = "正在从东方财富同步全市场约 1.8 万只公募基金目录索引至 DuckDB...";

        try
        {
            int count = await _dataService.SyncMarketCatalogAsync();
            if (count > 0)
            {
                MessageBox.Show($"全市场公募基金目录已成功同步入库！\n共同步收录 {count:N0} 只公募基金信息至 DuckDB 高性能索引表。", "全市场同步成功", MessageBoxButton.OK, MessageBoxImage.Information);
                // 切换到全市场视图查看
                CmbViewMode.SelectedIndex = 1;
                await QueryMarketCatalogAsync();
            }
            else
            {
                MessageBox.Show("未能同步全市场基金目录，请检查网络连接。", "同步提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"同步全市场目录异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (ScreenerProgress != null) ScreenerProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnSendToPortfolio_Click(object sender, RoutedEventArgs e)
    {
        if (_isMarketMode)
        {
            MessageBox.Show("请在【本地投研深度池】模式下勾选基金批量发送至组合。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selectedCodes = _currentFilteredFunds.Where(f => f.IsSelected).Select(f => f.Code).Distinct().ToList();

        // 若未勾选 CheckBox，则尝试从 DataGrid 选中高亮行中获取
        if (selectedCodes.Count == 0 && GridFunds.SelectedItems != null && GridFunds.SelectedItems.Count > 0)
        {
            selectedCodes = GridFunds.SelectedItems.OfType<StoredFundItem>().Select(f => f.Code).Distinct().ToList();
        }

        if (selectedCodes.Count == 0)
        {
            MessageBox.Show("请先在表格首列勾选 2~10 只基金（或按住 Ctrl/Shift 高亮多行），然后点击发送至组合。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (selectedCodes.Count > 10)
        {
            MessageBox.Show($"建议单次组合配置 2~10 只基金，当前选中了 {selectedCodes.Count} 只，已自动截取前 10 只进入投资组合。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            selectedCodes = selectedCodes.Take(10).ToList();
        }

        OnSendFundsToPortfolio?.Invoke(selectedCodes);
    }

    private void BtnSendToCompare_Click(object sender, RoutedEventArgs e)
    {
        if (_isMarketMode)
        {
            MessageBox.Show("请在【本地投研深度池】模式下勾选基金发送至多基对比。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selectedCodes = _currentFilteredFunds.Where(f => f.IsSelected).Select(f => f.Code).Distinct().ToList();

        // 若未勾选 CheckBox，则尝试从 DataGrid 选中高亮行中获取
        if (selectedCodes.Count == 0 && GridFunds.SelectedItems != null && GridFunds.SelectedItems.Count > 0)
        {
            selectedCodes = GridFunds.SelectedItems.OfType<StoredFundItem>().Select(f => f.Code).Distinct().ToList();
        }

        if (selectedCodes.Count == 0)
        {
            MessageBox.Show("请先在表格首列勾选 2 只基金（或按住 Ctrl 高亮 2 行），然后点击【🥊 发送至对比 PK】。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (selectedCodes.Count > 2)
        {
            MessageBox.Show($"对比 PK 目前支持对比 2 只基金，当前选中了 {selectedCodes.Count} 只，已自动截取前 2 只进入对比台。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            selectedCodes = selectedCodes.Take(2).ToList();
        }

        OnSendFundsToCompare?.Invoke(selectedCodes);
    }

    private void WeightInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 留空由用户点击“⚡ 重新计分”或切换排序时统一重新计算，避免输入打字时抖动
    }

    private void BtnRecalcWeights_Click(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        ApplyFilters();
    }

    public static decimal CalculateCustomScore(StoredFundItem fund, decimal weightRet, decimal weightDd, decimal weightSharpe, decimal weightStability)
    {
        decimal totalW = weightRet + weightDd + weightSharpe + weightStability;
        if (totalW <= 0) totalW = 100m;

        // 1. 收益得分 (近1年收益率 -20% -> 0分, +40% -> 100分)
        decimal retScore = Math.Clamp((fund.Return1Y + 20m) * (100m / 60m), 0m, 100m);

        // 2. 抗跌得分 (最大回撤 0% -> 100分, 40% -> 0分)
        decimal ddScore = Math.Clamp((40m - fund.MaxDrawdown) * (100m / 40m), 0m, 100m);

        // 3. 夏普得分 (夏普 -0.5 -> 0分, 2.5 -> 100分)
        decimal sharpeScore = Math.Clamp((fund.SharpeRatio + 0.5m) * (100m / 3.0m), 0m, 100m);

        // 4. 稳定性得分 (取已有 QuantScore 0-100)
        decimal stabScore = Math.Clamp(fund.QuantScore, 0m, 100m);

        decimal composite = (retScore * weightRet + ddScore * weightDd + sharpeScore * weightSharpe + stabScore * weightStability) / totalW;
        return Math.Round(composite, 1);
    }

    private void BtnOpenMain_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is string code)
        {
            OnSelectFundForMain?.Invoke(code);
        }
    }

    private void BtnOpenCompare_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is string code)
        {
            OnSelectFundForCompare?.Invoke(code);
        }
    }

    private async void BtnMarketIngestAndOpen_Click(object sender, RoutedEventArgs e)
    {
        if (_dataService == null) return;
        if (sender is FrameworkElement elem && elem.Tag is string code)
        {
            if (ScreenerProgress != null) ScreenerProgress.Visibility = Visibility.Visible;
            TxtSummary.Text = $"正在从东方财富获取基金 [{code}] 的全量历史时序并计算量化指标...";

            try
            {
                var detail = await _dataService.GetFundDetailAsync(code, forceRefresh: true);
                if (detail != null)
                {
                    OnSelectFundForMain?.Invoke(code);
                }
                else
                {
                    MessageBox.Show($"无法拉取基金 [{code}] 的详细行情净值数据。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                if (ScreenerProgress != null) ScreenerProgress.Visibility = Visibility.Collapsed;
            }
        }
    }

    private async void BtnAddFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (_dataService == null) return;
        if (sender is FrameworkElement elem && elem.Tag is string code)
        {
            var item = _allFunds.FirstOrDefault(f => f.Code == code);
            if (item != null)
            {
                await _dataService.DuckDb.AddFavoriteAsync(item.Code, item.Name, item.Type);
                MessageBox.Show($"已成功将 [{item.Code}] {item.Name} 添加至我的自选！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private async void BtnAddFavoriteCatalog_Click(object sender, RoutedEventArgs e)
    {
        if (_dataService == null) return;
        if (sender is FrameworkElement elem && elem.Tag is string code)
        {
            var item = _catalogFunds.FirstOrDefault(f => f.Code == code);
            if (item != null)
            {
                await _dataService.DuckDb.AddFavoriteAsync(item.Code, item.Name, item.Type);
                MessageBox.Show($"已成功将 [{item.Code}] {item.Name} 添加至我的自选！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void PopulateCrossSectionalScores(List<StoredFundItem> funds)
    {
        if (funds == null || funds.Count == 0) return;
        try
        {
            var rankingResult = QuantCalculator.CalculateMultiFactorCrossSectionalRanking(funds);
            var scoreMap = rankingResult.AllScoredFunds.ToDictionary(s => s.FundCode, s => s);

            foreach (var f in funds)
            {
                if (scoreMap.TryGetValue(f.Code, out var item))
                {
                    f.MultiFactorCompositeScore = item.CompositeScore;
                    f.MultiFactorPercentile = item.PercentileRank;
                    f.MultiFactorRatingTag = item.RatingTag;
                    f.MomentumScore = item.MomentumScore;
                    f.RiskAdjustedScore = item.RiskAdjustedScore;
                    f.DownsideDefenseScore = item.DownsideDefenseScore;
                    f.AlphaPurityScore = item.AlphaPurityScore;
                    f.ConvexityScore = item.ConvexityScore;
                }

                // 科学双周期评估与投决信号填充
                if (f.ShortTermScore == 0 && f.MediumTermScore == 0)
                {
                    decimal shortScore = Math.Clamp(50m + (f.MomentumScore - 50m) * 0.7m + (f.RiskAdjustedScore - 50m) * 0.3m, 0m, 100m);
                    decimal medScore = Math.Clamp(f.MultiFactorCompositeScore > 0 ? f.MultiFactorCompositeScore : (f.QuantScore > 0 ? f.QuantScore : 50m), 0m, 100m);
                    f.ShortTermScore = Math.Round(shortScore, 1);
                    f.MediumTermScore = Math.Round(medScore, 1);
                }

                if (string.IsNullOrEmpty(f.ScientificActionSignal))
                {
                    if (f.MediumTermScore >= 65m && f.ShortTermScore >= 60m)
                    {
                        f.ScientificActionSignal = "积极买入";
                        f.ConcordanceType = "短中双优·共振顺风";
                        f.ScientificConviction = Math.Min(95m, 65m + (f.MediumTermScore + f.ShortTermScore) / 4m);
                    }
                    else if (f.MediumTermScore >= 65m && f.ShortTermScore < 45m)
                    {
                        f.ScientificActionSignal = "逢低吸筹";
                        f.ConcordanceType = "中期绩优·短期超跌";
                        f.ScientificConviction = 78m;
                    }
                    else if (f.MediumTermScore < 45m && f.ShortTermScore >= 60m)
                    {
                        f.ScientificActionSignal = "脉冲反弹";
                        f.ConcordanceType = "短期脉冲·中期偏弱";
                        f.ScientificConviction = 65m;
                    }
                    else if (f.MediumTermScore < 40m && f.ShortTermScore < 40m)
                    {
                        f.ScientificActionSignal = "规避观望";
                        f.ConcordanceType = "短中双弱·全面共振";
                        f.ScientificConviction = 85m;
                    }
                    else
                    {
                        f.ScientificActionSignal = "中性持有";
                        f.ConcordanceType = "结构分化·中性震荡";
                        f.ScientificConviction = 70m;
                    }
                }
            }
        }
        catch { }
    }

    private void BtnCrossSectionalRanking_Click(object sender, RoutedEventArgs e)
    {
        if (_allFunds == null || _allFunds.Count == 0)
        {
            MessageBox.Show("当前本地投研基金池为空，请先刷新或导入基金。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            PopulateCrossSectionalScores(_allFunds);
            if (CmbSort != null)
            {
                CmbSort.SelectedIndex = 2; // 自动切换为截面多因子标准化总分排序
            }
            ApplyFilters();
            MessageBox.Show($"已成功对全池 {_allFunds.Count} 只基金完成横截面标准化打分与分位数评级！\n\n已自动切换为【💎 截面多因子标准化总分】倒序排列。", "截面多因子打分完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"截面多因子打分异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnDecileSpreadBacktest_Click(object sender, RoutedEventArgs e)
    {
        if (BorderDecileModal != null)
        {
            BorderDecileModal.Visibility = Visibility.Visible;
        }
        ExecuteDecileSpreadBacktest();
    }

    private void BtnCloseDecileModal_Click(object sender, RoutedEventArgs e)
    {
        if (BorderDecileModal != null)
        {
            BorderDecileModal.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnRunDecileTest_Click(object sender, RoutedEventArgs e)
    {
        ExecuteDecileSpreadBacktest();
    }

    private void ExecuteDecileSpreadBacktest()
    {
        var universe = _currentFilteredFunds.Count >= 10 ? _currentFilteredFunds : _allFunds;
        if (universe.Count < 10)
        {
            MessageBox.Show("执行十分位数回测检验至少需要 10 只样本基金！当前样本不足。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 确保因子分值已横截面计算
        PopulateCrossSectionalScores(universe);

        string factorType = (CmbDecileFactor?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Composite";
        var result = QuantCalculator.CalculateDecileSpreadBacktest(universe, factorType);

        if (TxtDecileSpread != null) TxtDecileSpread.Text = $"{result.LongShortAnnualizedSpread:+0.00;-0.00}%";
        if (TxtDecileSharpe != null) TxtDecileSharpe.Text = $"{result.LongShortSharpe:F2}";
        if (TxtDecileTStat != null)
        {
            TxtDecileTStat.Text = $"t = {result.FactorTStatistic:F2}";
            TxtDecileTStat.Foreground = result.IsStatisticallySignificant
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(166, 227, 161))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 139, 168));
        }
        if (TxtDecileSigBadge != null)
        {
            TxtDecileSigBadge.Text = result.IsStatisticallySignificant ? "✅ 显著 (p<0.05)" : "⚠️ 不显著";
        }
        if (TxtDecileMonotonicity != null) TxtDecileMonotonicity.Text = $"{result.MonotonicityScorePercent:F1}%";
        if (TxtDecileSpearman != null) TxtDecileSpearman.Text = $"Spearman ρ: {result.SpearmanRankCorrelation:+0.00;-0.00}";
        if (TxtDecileGrade != null) TxtDecileGrade.Text = result.FactorEfficacyGrade;
        if (GridDecileLadder != null) GridDecileLadder.ItemsSource = result.Deciles;
        if (TxtDecileDiagnosis != null) TxtDecileDiagnosis.Text = result.DiagnosticSummary;
    }

    private void BtnDynamicFactorTiming_Click(object sender, RoutedEventArgs e)
    {
        if (BorderFactorTimingModal != null)
        {
            BorderFactorTimingModal.Visibility = Visibility.Visible;
        }
        ExecuteDynamicFactorTiming();
    }

    private void BtnCloseFactorTimingModal_Click(object sender, RoutedEventArgs e)
    {
        if (BorderFactorTimingModal != null)
        {
            BorderFactorTimingModal.Visibility = Visibility.Collapsed;
        }
    }

    private void ExecuteDynamicFactorTiming()
    {
        var universe = _currentFilteredFunds.Count >= 5 ? _currentFilteredFunds : _allFunds;
        if (universe.Count == 0)
        {
            MessageBox.Show("基金池数据为空，无法执行动态因子时序轮动！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = QuantCalculator.EvaluateDynamicFactorTiming(universe);

        if (TxtTimingRegime != null) TxtTimingRegime.Text = result.CurrentMarketRegime;
        if (TxtTimingConfidence != null) TxtTimingConfidence.Text = $"模型置信度: {result.RegimeConfidencePercent:F0}%";
        if (TxtTimingTopOverweight != null) TxtTimingTopOverweight.Text = result.TopOverweightFactor;
        if (TxtTimingTopUnderweight != null) TxtTimingTopUnderweight.Text = result.TopUnderweightFactor;
        if (TxtTimingDispersion != null) TxtTimingDispersion.Text = $"{result.FactorDispersionPercent:F1}%";
        if (GridFactorTiming != null) GridFactorTiming.ItemsSource = result.FactorMetrics;
        if (TxtTimingDiagnosis != null) TxtTimingDiagnosis.Text = $"{result.MacroFactorAdvice}\n{result.RotationCycleDiagnosis}";
    }
}
