using System.Collections.Generic;

namespace BigaFund.Services;

public static class PlotStyleHelper
{
    private static string? _cachedFontName;

    /// <summary>
    /// 解析适合当前操作系统的中文字体名称（在 Windows 上优先解析 Microsoft YaHei UI / 微软雅黑）
    /// </summary>
    public static string ResolveChineseFont()
    {
        if (_cachedFontName != null) return _cachedFontName;

        try
        {
            string? detected = ScottPlot.Fonts.Detect("华夏成长混合");
            if (!string.IsNullOrEmpty(detected))
            {
                _cachedFontName = detected;
                return _cachedFontName;
            }
        }
        catch
        {
            // 忽略异常，降级到候选字体
        }

        string[] candidates = ["Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "PingFang SC", "WenQuanYi Micro Hei", "Noto Sans CJK SC"];
        foreach (var font in candidates)
        {
            try
            {
                if (ScottPlot.Fonts.GetTypeface(font, false, false) != null)
                {
                    _cachedFontName = font;
                    return _cachedFontName;
                }
            }
            catch
            {
                // 继续寻找下一个候选
            }
        }

        _cachedFontName = "Microsoft YaHei UI";
        return _cachedFontName;
    }

    /// <summary>
    /// 为指定的 ScottPlot.Plot 统一应用暗色主题与中文字体配置（包含坐标轴、图例等）
    /// </summary>
    public static void ApplyDarkThemeAndFont(ScottPlot.Plot plot)
    {
        string fontName = ResolveChineseFont();
        ScottPlot.Fonts.Default = fontName;
        ScottPlot.Fonts.Sans = fontName;
        try
        {
            ScottPlot.Fonts.DefaultFontStyle = SkiaSharp.SKTypeface.FromFamilyName(fontName);
        }
        catch
        {
            // 忽略 Typeface 异常
        }

        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#1E1E2E");
        plot.DataBackground.Color = ScottPlot.Color.FromHex("#181825");
        plot.Axes.Color(ScottPlot.Color.FromHex("#A6ADC8"));
        plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#313244");
        plot.Axes.DateTimeTicksBottom();

        // 统一设置轴刻度、标题字体为中文字体
        plot.Font.Set(fontName);

        // 统一设置图例暗色主题与中文字体，防止中文显示为方块 (豆腐块)
        plot.Legend.FontName = fontName;
        plot.Legend.FontSize = 12;
        plot.Legend.FontColor = ScottPlot.Color.FromHex("#CDD6F4");
        plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#1E1E2E").WithAlpha(0.88f);
        plot.Legend.OutlineColor = ScottPlot.Color.FromHex("#45475A");
        plot.Legend.ShadowColor = ScottPlot.Colors.Transparent;
        plot.Legend.SetBestFontOnEachRender = true;
    }
}
