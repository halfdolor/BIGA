using System.Windows;
using System.Windows.Controls;
using BigaFund.Models;
using BigaFund.Services;

namespace BigaFund.Views;

public partial class FundScreenerWindow : Window
{
    private readonly FundDataService _dataService;
    private List<StoredFundItem> _allFunds = new();
    private bool _isInitialized;

    public event Action<string>? OnSelectFundForMain;
    public event Action<string>? OnSelectFundForCompare;

    public FundScreenerWindow(FundDataService dataService)
    {
        InitializeComponent();
        _dataService = dataService;

        Loaded += async (s, e) =>
        {
            _isInitialized = true;
            await LoadStoredFundsAsync();
        };
    }

    private async Task LoadStoredFundsAsync()
    {
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

            ApplyFilters();
        }
        catch (Exception ex)
        {
            TxtSummary.Text = $"加载基金池异常: {ex.Message}";
        }
    }

    private void ApplyFilters()
    {
        if (!_isInitialized) return;

        string kw = TxtKeyword?.Text?.Trim()?.ToLowerInvariant() ?? "";
        string cat = (CmbCategory?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ALL";
        string sector = (CmbSector?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ALL";

        var filtered = _allFunds.Where(f =>
        {
            bool matchKw = string.IsNullOrEmpty(kw) ||
                           (f.Code != null && f.Code.ToLowerInvariant().Contains(kw)) ||
                           (f.Name != null && f.Name.ToLowerInvariant().Contains(kw)) ||
                           (f.Manager != null && f.Manager.ToLowerInvariant().Contains(kw));

            bool matchCat = cat == "ALL" || (!string.IsNullOrEmpty(f.Type) && f.Type.Contains(cat));
            bool matchSector = sector == "ALL" || FundSectorHelper.MatchesSector(f.Name, f.Type, sector);

            return matchKw && matchCat && matchSector;
        }).ToList();

        int sortIdx = CmbSort?.SelectedIndex ?? 0;
        filtered = sortIdx switch
        {
            1 => filtered.OrderByDescending(f => f.Return1Y).ToList(),
            2 => filtered.OrderByDescending(f => f.SharpeRatio).ToList(),
            3 => filtered.OrderBy(f => f.MaxDrawdown).ToList(),
            4 => filtered.OrderByDescending(f => f.LatestNav).ToList(),
            _ => filtered.OrderByDescending(f => f.UpdatedAt).ToList()
        };

        if (GridFunds != null) GridFunds.ItemsSource = filtered;
        if (TxtSummary != null) TxtSummary.Text = $"当前筛选展示: {filtered.Count} 只基金 (本地基金库总计: {_allFunds.Count} 只)";
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

    private void CmbSector_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
        await LoadStoredFundsAsync();
    }

    private void BtnOpenMain_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is string code)
        {
            OnSelectFundForMain?.Invoke(code);
            Close();
        }
    }

    private void BtnOpenCompare_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is string code)
        {
            OnSelectFundForCompare?.Invoke(code);
            Close();
        }
    }

    private async void BtnAddFavorite_Click(object sender, RoutedEventArgs e)
    {
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
}
