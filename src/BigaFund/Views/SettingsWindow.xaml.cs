using System.Windows;
using BigaFund.Services;

namespace BigaFund.Views;

public partial class SettingsWindow : Window
{
    private readonly DuckDbService _duckDb;

    public SettingsWindow(DuckDbService duckDb)
    {
        InitializeComponent();
        _duckDb = duckDb;

        Loaded += async (s, e) =>
        {
            await LoadSettingsAsync();
        };
    }

    private async Task LoadSettingsAsync()
    {
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
            MessageBox.Show($"读取配置失败: {ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
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

            MessageBox.Show("系统首选项与量化参数已成功保存至 DuckDB 数据库！", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
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
    }
}
