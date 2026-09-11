using System.Windows;
using System.Windows.Controls;
using BigaFund.Services;

namespace BigaFund.Views;

public partial class QuickCalculatorControl : UserControl
{
    private bool _isInitialized = false;

    public QuickCalculatorControl()
    {
        InitializeComponent();
        SetupPlotStyle();
        _isInitialized = true;
        Calculate();
    }

    private void SetupPlotStyle()
    {
        if (WealthPlot == null) return;
        PlotStyleHelper.ApplyDarkThemeAndFont(WealthPlot.Plot);
    }

    private void Input_Changed(object sender, EventArgs e)
    {
        if (!_isInitialized) return;
        Calculate();
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;
        if (TxtReturnRateDisplay != null && SliderReturnRate != null)
        {
            TxtReturnRateDisplay.Text = $"{SliderReturnRate.Value:F1}%";
        }
        if (TxtInflationDisplay != null && SliderInflation != null)
        {
            TxtInflationDisplay.Text = $"{SliderInflation.Value:F1}%";
        }
        if (TxtFeeDisplay != null && SliderFee != null)
        {
            TxtFeeDisplay.Text = $"{SliderFee.Value:F1}%";
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

        double annualRate = (SliderReturnRate?.Value ?? 8.0) / 100.0;
        double inflationRate = (SliderInflation?.Value ?? 2.5) / 100.0;
        double feeRate = (SliderFee?.Value ?? 1.2) / 100.0;

        double netAnnualRate = Math.Max(0.0, annualRate - feeRate);
        int periodsPerYear = CmbFrequency.SelectedIndex == 1 ? 52 : 12;
        int totalPeriods = years * periodsPerYear;
        double netPeriodRate = netAnnualRate / periodsPerYear;
        double grossPeriodRate = annualRate / periodsPerYear;

        decimal totalInvested = principal + periodic * totalPeriods;

        // 1. 名义扣费后期末资产 (Net Nominal Asset)
        double fvPrincipal = (double)principal * Math.Pow(1.0 + netPeriodRate, totalPeriods);
        double fvPeriodic = netPeriodRate > 0
            ? (double)periodic * (Math.Pow(1.0 + netPeriodRate, totalPeriods) - 1.0) / netPeriodRate
            : (double)(periodic * totalPeriods);
        double totalAsset = fvPrincipal + fvPeriodic;

        // 2. 无费率摩擦下的理想总资产 (Gross Asset)
        double fvGrossP = (double)principal * Math.Pow(1.0 + grossPeriodRate, totalPeriods);
        double fvGrossPer = grossPeriodRate > 0
            ? (double)periodic * (Math.Pow(1.0 + grossPeriodRate, totalPeriods) - 1.0) / grossPeriodRate
            : (double)(periodic * totalPeriods);
        double grossAsset = fvGrossP + fvGrossPer;
        double feeDrag = Math.Max(0.0, grossAsset - totalAsset);

        // 3. 通胀调整后的真实购买力资产 (Real Purchasing Power Asset)
        double realAsset = totalAsset / Math.Pow(1.0 + inflationRate, years);
        double inflationErosion = Math.Max(0.0, totalAsset - realAsset);
        double totalErosion = feeDrag + inflationErosion;

        // 4. 净利润与收益率
        double totalProfit = totalAsset - (double)totalInvested;
        double returnRatio = totalInvested > 0 ? (totalProfit / (double)totalInvested) * 100.0 : 0.0;

        if (TxtTotalCost != null) TxtTotalCost.Text = $"¥ {totalInvested:N0}";
        if (TxtFinalAsset != null) TxtFinalAsset.Text = $"¥ {totalAsset:N0}";
        if (TxtRealAsset != null) TxtRealAsset.Text = $"¥ {realAsset:N0}";
        if (TxtTotalProfit != null) TxtTotalProfit.Text = $"{(totalProfit >= 0 ? "+" : "")}¥ {totalProfit:N0}";
        if (TxtReturnRatio != null) TxtReturnRatio.Text = $"{(returnRatio >= 0 ? "+" : "")}{returnRatio:F2}%";
        if (TxtErosionLoss != null) TxtErosionLoss.Text = $"-¥ {totalErosion:N0}";

        if (TxtRule72Info != null)
        {
            string doubleYearsText = netAnnualRate > 0 ? (72.0 / (netAnnualRate * 100.0)).ToString("F1") : "--";
            TxtRule72Info.Text = $"💡 72 法则参考：在扣费后净年化 {netAnnualRate * 100.0:F1}% 下，本金翻倍大约需要 {doubleYearsText} 年。\n" +
                                 $"🔥 长期磨损透视：投资长跑 {years} 年后，年化 {feeRate * 100.0:F1}% 的管理费累计吞噬收益 ¥{feeDrag:N0}，" +
                                 $"{inflationRate * 100.0:F1}% 通胀使名义财富购买力缩水 ¥{inflationErosion:N0}，两者合计侵蚀财富 ¥{totalErosion:N0}！";
        }

        RenderWealthChart(principal, periodic, years, periodsPerYear, netPeriodRate, inflationRate);
    }

    private void RenderWealthChart(decimal principal, decimal periodic, int years, int periodsPerYear, double netPeriodRate, double inflationRate)
    {
        if (WealthPlot == null) return;
        var plot = WealthPlot.Plot;
        plot.Clear();
        PlotStyleHelper.ApplyDarkThemeAndFont(plot);

        int count = years + 1;
        double[] xs = new double[count];
        double[] ysCost = new double[count];
        double[] ysAsset = new double[count];
        double[] ysReal = new double[count];

        for (int y = 0; y <= years; y++)
        {
            xs[y] = y;
            int n = y * periodsPerYear;
            double cost = (double)(principal + periodic * n);
            ysCost[y] = cost;

            double fvP = (double)principal * Math.Pow(1.0 + netPeriodRate, n);
            double fvPer = netPeriodRate > 0
                ? (double)periodic * (Math.Pow(1.0 + netPeriodRate, n) - 1.0) / netPeriodRate
                : (double)(periodic * n);
            double asset = fvP + fvPer;
            ysAsset[y] = asset;

            double real = asset / Math.Pow(1.0 + inflationRate, y);
            ysReal[y] = real;
        }

        // 1. 累计投入本金
        var lineCost = plot.Add.ScatterLine(xs, ysCost);
        lineCost.LegendText = "累计投入本金 (元)";
        lineCost.Color = ScottPlot.Color.FromHex("#A6ADC8");
        lineCost.LineWidth = 2.0f;
        lineCost.LinePattern = ScottPlot.LinePattern.Dashed;

        // 2. 名义期末总资产
        var lineAsset = plot.Add.ScatterLine(xs, ysAsset);
        lineAsset.LegendText = "期末名义资产 (元)";
        lineAsset.Color = ScottPlot.Color.FromHex("#A6E3A1");
        lineAsset.LineWidth = 2.8f;

        // 3. 真实抗通胀购买力
        var lineReal = plot.Add.ScatterLine(xs, ysReal);
        lineReal.LegendText = "抗通胀实际购买力 (元)";
        lineReal.Color = ScottPlot.Color.FromHex("#F5B041");
        lineReal.LineWidth = 2.4f;

        plot.ShowLegend(ScottPlot.Alignment.UpperLeft);
        plot.Legend.FontName = PlotStyleHelper.ResolveChineseFont();
        plot.Legend.SetBestFontOnEachRender = true;
        plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#1E1E2E");
        plot.Legend.FontColor = ScottPlot.Color.FromHex("#CDD6F4");
        plot.Legend.OutlineColor = ScottPlot.Color.FromHex("#45475A");

        plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();
        plot.Axes.AutoScale();
        WealthPlot.Refresh();
    }
}
