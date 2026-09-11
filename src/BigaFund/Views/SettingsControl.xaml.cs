using System.Windows;
using System.Windows.Controls;
using BigaFund.Services;

namespace BigaFund.Views;

public partial class SettingsControl : UserControl
{
    private DuckDbService? _duckDb;
    private bool _isLoaded = false;

    public SettingsControl()
    {
        InitializeComponent();
    }

    public void Initialize(DuckDbService duckDb)
    {
        _duckDb = duckDb;
        if (_isLoaded) return;
        _isLoaded = true;

        TxtDbPath.Text = duckDb.DatabasePath;

        _ = Task.Run(async () =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                await LoadSettingsAsync();
                await LoadDbStatsAsync();
            });
        });
    }

    public async Task RefreshAllAsync()
    {
        await LoadSettingsAsync();
        await LoadDbStatsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        if (_duckDb == null) return;
        try
        {
            string rf = await _duckDb.GetSettingAsync("RiskFreeRate", "2.0");
            string fee = await _duckDb.GetSettingAsync("FeeRate", "0.10");
            string dca = await _duckDb.GetSettingAsync("DefaultDcaAmount", "1000");
            string tp = await _duckDb.GetSettingAsync("TargetProfitRate", "15.0");
            string grid = await _duckDb.GetSettingAsync("GridSpacing", "3.0");

            TxtRiskFreeRate.Text = rf;
            TxtFeeRate.Text = fee;
            TxtDefaultDcaAmount.Text = dca;
            TxtTargetProfitRate.Text = tp;
            TxtGridSpacing.Text = grid;
        }
        catch (Exception ex)
        {
            TxtSaveFeedback.Text = $"⚠️ 读取配置失败: {ex.Message}";
        }
    }

    private async Task LoadDbStatsAsync()
    {
        if (_duckDb == null) return;
        try
        {
            var stats = await _duckDb.GetStatsAsync();
            TxtCachedFundsCount.Text = $"{stats.FundCount} 只";
            TxtDbFileSize.Text = $"{stats.FileSizeMb:F2} MB";
            TxtNavRecordsCount.Text = $"{stats.TotalNavRecords:N0} 条";
            TxtBenchmarkRecordsCount.Text = $"{stats.TotalBenchmarkRecords:N0} 条";
            TxtDbPath.Text = stats.DatabasePath;
        }
        catch
        {
            // 忽略统计读取异常
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_duckDb == null) return;

        if (!decimal.TryParse(TxtRiskFreeRate.Text.Trim(), out decimal rf) || rf < 0 || rf > 20)
        {
            MessageBox.Show("请输入有效的无风险年化利率 (0% ~ 20%)", "输入校验", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtFeeRate.Text.Trim(), out decimal fee) || fee < 0 || fee > 5)
        {
            MessageBox.Show("请输入有效的手续费率 (0% ~ 5%)", "输入校验", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtDefaultDcaAmount.Text.Trim(), out decimal dca) || dca <= 0)
        {
            MessageBox.Show("请输入有效的定投基准金额 (> 0)", "输入校验", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtTargetProfitRate.Text.Trim(), out decimal tp) || tp <= 0 || tp > 500)
        {
            MessageBox.Show("请输入有效的目标止盈阈值 (1% ~ 500%)", "输入校验", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtGridSpacing.Text.Trim(), out decimal grid) || grid <= 0 || grid > 50)
        {
            MessageBox.Show("请输入有效的网格间距 (0.1% ~ 50%)", "输入校验", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await _duckDb.SaveSettingAsync("RiskFreeRate", rf.ToString("0.##"));
            await _duckDb.SaveSettingAsync("FeeRate", fee.ToString("0.##"));
            await _duckDb.SaveSettingAsync("DefaultDcaAmount", dca.ToString("0.##"));
            await _duckDb.SaveSettingAsync("TargetProfitRate", tp.ToString("0.##"));
            await _duckDb.SaveSettingAsync("GridSpacing", grid.ToString("0.##"));

            TxtSaveFeedback.Text = "✅ 首选项已成功保存至 DuckDB！";

            // 3秒后淡出提示
            _ = Task.Run(async () =>
            {
                await Task.Delay(3500);
                await Dispatcher.InvokeAsync(() =>
                {
                    if (TxtSaveFeedback.Text.StartsWith("✅"))
                    {
                        TxtSaveFeedback.Text = "";
                    }
                });
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        TxtRiskFreeRate.Text = "2.0";
        TxtFeeRate.Text = "0.10";
        TxtDefaultDcaAmount.Text = "1000";
        TxtTargetProfitRate.Text = "15.0";
        TxtGridSpacing.Text = "3.0";
        TxtSaveFeedback.Text = "已重置为系统默认值，点击保存即可生效";
    }

    private async void BtnRefreshDbStats_Click(object sender, RoutedEventArgs e)
    {
        await LoadDbStatsAsync();
    }
}
