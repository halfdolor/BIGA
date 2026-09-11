using System.Windows;
using System.Windows.Controls;

namespace BigaFund.Views;

public partial class QuickCalculatorWindow : Window
{
    private bool _isInitialized = false;

    public QuickCalculatorWindow()
    {
        InitializeComponent();
        _isInitialized = true;
        Calculate();
    }

    private void Input_Changed(object sender, EventArgs e)
    {
        if (!_isInitialized) return;
        Calculate();
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;
        if (TxtReturnRateDisplay != null)
        {
            TxtReturnRateDisplay.Text = $"{SliderReturnRate.Value:F1}%";
        }
        Calculate();
    }

    private void Calculate()
    {
        if (!_isInitialized) return;

        if (!decimal.TryParse(TxtPrincipal.Text.Trim(), out decimal principal) || principal < 0) principal = 0;
        if (!decimal.TryParse(TxtPeriodic.Text.Trim(), out decimal periodic) || periodic < 0) periodic = 0;
        if (!int.TryParse(TxtYears.Text.Trim(), out int years) || years <= 0) years = 1;
        if (years > 60) years = 60;

        double annualRate = SliderReturnRate.Value / 100.0;
        int periodsPerYear = CmbFrequency.SelectedIndex == 1 ? 52 : 12;
        int totalPeriods = years * periodsPerYear;
        double periodRate = annualRate / periodsPerYear;

        decimal totalInvested = principal + periodic * totalPeriods;

        double fvPrincipal = (double)principal * Math.Pow(1.0 + periodRate, totalPeriods);
        double fvPeriodic = 0.0;
        if (periodRate > 0)
        {
            fvPeriodic = (double)periodic * (Math.Pow(1.0 + periodRate, totalPeriods) - 1.0) / periodRate;
        }
        else
        {
            fvPeriodic = (double)(periodic * totalPeriods);
        }

        double totalAsset = fvPrincipal + fvPeriodic;
        double totalProfit = totalAsset - (double)totalInvested;
        double returnRatio = totalInvested > 0 ? (totalProfit / (double)totalInvested) * 100.0 : 0.0;

        TxtTotalCost.Text = $"¥ {totalInvested:N0}";
        TxtFinalAsset.Text = $"¥ {totalAsset:N0}";
        TxtTotalProfit.Text = $"{(totalProfit >= 0 ? "+" : "")}¥ {totalProfit:N0}";
        TxtReturnRatio.Text = $"{(returnRatio >= 0 ? "+" : "")}{returnRatio:F2}%";

        if (annualRate > 0)
        {
            double doubleYears = 72.0 / (annualRate * 100.0);
            TxtRule72Info.Text = $"💡 72 法则参考：在 {annualRate * 100.0:F1}% 年化复合收益率下，本金翻倍大约需要 {doubleYears:F1} 年；持续长跑 {years} 年后，净利息收益占总资产比例为 {(totalProfit / totalAsset * 100.0):F1}%。";
        }
        else
        {
            TxtRule72Info.Text = "💡 年化收益率为 0% 时无复利增值效应。";
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
