using System.Configuration;
using System.Data;
using System.Windows;

namespace BigaFund;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        // 初始化 ScottPlot 全局中文字体支持
        string chineseFont = Services.PlotStyleHelper.ResolveChineseFont();
        ScottPlot.Fonts.Default = chineseFont;
        ScottPlot.Fonts.Sans = chineseFont;
        try
        {
            ScottPlot.Fonts.DefaultFontStyle = SkiaSharp.SKTypeface.FromFamilyName(chineseFont);
        }
        catch
        {
            // 忽略 Typeface 异常
        }

        DispatcherUnhandledException += (sender, e) =>
        {
            LogCrash("DispatcherUnhandledException", e.Exception);
            MessageBox.Show($"应用程序发生异常:\n{e.Exception.Message}\n\n堆栈跟踪:\n{e.Exception.StackTrace}",
                "BIGA - 运行异常", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogCrash("AppDomainUnhandledException", ex);
                MessageBox.Show($"应用程序发生未处理的严重异常:\n{ex.Message}\n\n堆栈跟踪:\n{ex.StackTrace}",
                    "BIGA - 严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            LogCrash("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    private static void LogCrash(string category, Exception ex)
    {
        try
        {
            string logDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BIGA", "logs");
            if (!System.IO.Directory.Exists(logDir))
            {
                System.IO.Directory.CreateDirectory(logDir);
            }
            string logFile = System.IO.Path.Combine(logDir, "crash.log");
            string content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{category}]\n{ex}\n\n";
            System.IO.File.AppendAllText(logFile, content);
        }
        catch
        {
            // 忽略日志写入失败
        }
    }
}

