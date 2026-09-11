namespace BigaFund.Tests;
using System.IO;
using BigaFund.Services;

[TestClass]
public sealed class Test1
{
    [TestMethod]
    public void TestMethod1()
    {
        var plot = new ScottPlot.Plot();
        PlotStyleHelper.ApplyDarkThemeAndFont(plot);
        var s = plot.Add.Scatter(new double[] { 1, 2, 3 }, new double[] { 10, 20, 15 });
        s.LegendText = "华夏成长混合C (累计收益率 %)";
        plot.ShowLegend(ScottPlot.Alignment.UpperLeft);

        Assert.IsNotNull(plot.Legend.FontName);
        Assert.IsTrue(plot.Legend.IsVisible);
    }
}
