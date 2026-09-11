using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BigaFund.Models;
using BigaFund.Services;

namespace BigaFund.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly FundDataService _dataService;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isSearchOpen;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private FundDetail? _currentFund;

    [ObservableProperty]
    private FundQuantScoreCard? _scoreCard;

    [ObservableProperty]
    private RealtimeValuation? _realtimeValuation;

    [ObservableProperty]
    private BrinsonAttributionResult? _brinsonResult;

    [ObservableProperty]
    private StyleDriftAnalysisResult? _styleDrift;

    [ObservableProperty]
    private BarraAttributionResult? _barraAttribution;

    [ObservableProperty]
    private ManagerCareerProfile? _managerProfile;

    [ObservableProperty]
    private ManagerTimingAbilityResult? _timingAbility;

    [ObservableProperty]
    private BullBearCaptureResult? _bullBearCapture;

    [ObservableProperty]
    private InstitutionalDueDiligenceCard? _dueDiligence;

    [ObservableProperty]
    private ScientificInvestmentAdvice? _scientificAdvice;

    [ObservableProperty]
    private ShortTermEvaluationResult? _shortTermEvaluation;

    [ObservableProperty]
    private MediumTermEvaluationResult? _mediumTermEvaluation;

    [ObservableProperty]
    private HorizonConcordanceResult? _horizonConcordance;

    [ObservableProperty]
    private ForecastRealityComparisonResult? _forecastRealityComparison;

    [ObservableProperty]
    private ForwardSimulationForecastItem? _simulationForecast;

    [ObservableProperty]
    private PostMortemExperienceReport? _experienceReport;

    // Phase 17 机构级 4433 选基、阿尔法持续性与多因子 IC/IR 效力检验
    [ObservableProperty]
    private Fund4433CheckResult? _result4433;

    [ObservableProperty]
    private AlphaPersistenceResult? _alphaPersistence;

    [ObservableProperty]
    private FactorIcAnalysisResult? _factorIcAnalysis;

    // Phase 18 机构级因子拥挤度与估值差离散度监测
    [ObservableProperty]
    private FactorCrowdingAnalysisResult? _factorCrowding;

    [ObservableProperty]
    private string _selectedTimeRange = "1Y"; // 1M, 3M, 6M, 1Y, 3Y, 5Y, ALL

    [ObservableProperty]
    private QuantMetrics? _metrics;

    [ObservableProperty]
    private BacktestResult? _backtestResult;

    [ObservableProperty]
    private decimal _dcaAmount = 1000m;

    [ObservableProperty]
    private int _selectedFrequencyIndex = 0; // 0=每周四, 1=每周一, 2=每月初

    [ObservableProperty]
    private int _selectedStrategyIndex = 0; // 0=普通定投, 1=60日均线择时定投

    [ObservableProperty]
    private bool _isCurrentFavorite;

    [ObservableProperty]
    private bool _hasFavorites;

    [ObservableProperty]
    private int _activeChartTab = 0; // 0=净值与基准, 1=定投回测, 2=回撤分析

    [ObservableProperty]
    private decimal _riskFreeRate = 2.0m;

    [ObservableProperty]
    private decimal _targetProfitThreshold = 15.0m; // 目标止盈线 15%

    [ObservableProperty]
    private decimal _gridSpacingPercent = 3.0m; // 网格间距 3%

    [ObservableProperty]
    private decimal _feeRatePercent = 0.10m; // 申购费率 0.1%

    [ObservableProperty]
    private int _maPeriod = 60; // 均线周期，默认 60 日

    [ObservableProperty]
    private int _valuationWindow = 250; // 估值分位滚动窗口，默认 250 日

    [ObservableProperty]
    private decimal _rebalanceThresholdPercent = 5.0m; // 股债再平衡偏离阈值，默认 5.0%

    [ObservableProperty]
    private int _selectedMainTabIndex = 0; // 0=单基量化, 1=多维选基, 2=基金对比, 3=组合配置, 4=AI模拟操盘, 5=复利试算, 6=系统首选项

    [ObservableProperty]
    private string _selectedSector = FundSectorHelper.SectorAll;

    public ObservableCollection<string> SectorCategories { get; } = new(FundSectorHelper.MainSectors);

    public FundDataService DataService => _dataService;

    public ObservableCollection<FundInfo> SearchResults { get; } = new();
    public ObservableCollection<FundInfo> QuickPicks { get; } = new();
    public ObservableCollection<UserFavorite> Favorites { get; } = new();

    public event Action? RequestPlotRefresh;
    public event Action<string, string>? RequestOpenCompare;
    public event Action<IEnumerable<string>>? RequestOpenPortfolio;
    public event Action<string>? RequestOpenScreenerWithSector;
    public event Action<string>? RequestSimulateBuy;

    partial void OnSelectedSectorChanged(string value)
    {
        UpdateQuickPicksForSector(value);
    }

    public void UpdateQuickPicksForSector(string sector)
    {
        QuickPicks.Clear();
        var picks = FundSectorHelper.GetSectorPicks(sector);
        foreach (var p in picks)
        {
            QuickPicks.Add(p);
        }
    }

    [RelayCommand]
    private void OpenScreenerWithSector(string? sector)
    {
        string sec = string.IsNullOrEmpty(sector) ? SelectedSector : sector;
        SelectedMainTabIndex = 1;
        RequestOpenScreenerWithSector?.Invoke(sec);
    }

    public MainViewModel()
    {
        _dataService = new FundDataService();

        UpdateQuickPicksForSector(FundSectorHelper.SectorAll);

        // 异步加载本地自选基金池与系统首选项
        Task.Run(async () =>
        {
            await LoadSettingsAsync();
            await LoadFavoritesAsync();
        });
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword))
        {
            SearchResults.Clear();
            IsSearchOpen = false;
            return;
        }

        IsSearching = true;
        StatusMessage = $"正在搜索: {SearchKeyword}...";

        try
        {
            var list = await _dataService.SearchFundsAsync(SearchKeyword);
            SearchResults.Clear();
            foreach (var item in list)
            {
                SearchResults.Add(item);
            }
            IsSearchOpen = SearchResults.Count > 0;
            StatusMessage = $"找到 {SearchResults.Count} 个相关基金";
        }
        catch (Exception ex)
        {
            StatusMessage = $"搜索异常: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    public async Task SelectFundAsync(FundInfo? fund)
    {
        if (fund == null) return;
        IsSearchOpen = false;
        SearchKeyword = $"{fund.Code} {fund.Name}";
        await LoadFundDetailAsync(fund.Code);
    }

    [RelayCommand]
    public async Task LoadFundDetailAsync(string fundCode)
    {
        await LoadFundDetailInternalAsync(fundCode, forceRefresh: false);
    }

    public async Task LoadFundDetailInternalAsync(string fundCode, bool forceRefresh)
    {
        if (string.IsNullOrWhiteSpace(fundCode)) return;

        IsLoading = true;
        StatusMessage = forceRefresh 
            ? $"正在从网络强制刷新基金 [{fundCode}] 最新历史数据..."
            : $"正在加载基金 [{fundCode}] 历史净值与基准数据...";

        try
        {
            var detail = await _dataService.GetFundDetailAsync(fundCode, forceRefresh);
            if (detail == null || detail.NavHistory.Count == 0)
            {
                StatusMessage = $"未获取到基金 [{fundCode}] 的净值数据，请检查代码。";
                return;
            }

            CurrentFund = detail;
            ScoreCard = detail.ScoreCard;
            RealtimeValuation = detail.RealtimeValuation;
            BrinsonResult = detail.BrinsonResult;
            IsCurrentFavorite = await _dataService.DuckDb.IsFavoriteAsync(detail.Code);
            StatusMessage = $"成功加载: {detail.Name} ({detail.Code})，共 {detail.NavHistory.Count} 条交易日记录";

            UpdateCalculationsAndPlot();
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RefreshCurrentFundAsync()
    {
        if (CurrentFund != null && !string.IsNullOrEmpty(CurrentFund.Code))
        {
            await LoadFundDetailInternalAsync(CurrentFund.Code, forceRefresh: true);
        }
        else
        {
            StatusMessage = "当前未选择任何基金。";
        }
    }

    [RelayCommand]
    private void OpenDatabaseFolder()
    {
        string dbDir = Path.GetDirectoryName(_dataService.DuckDb.DatabasePath) ?? string.Empty;
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dbDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"无法打开数据库目录: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenCacheFolder() => OpenDatabaseFolder();

    [RelayCommand]
    private async Task ClearDatabaseAsync()
    {
        var confirm = System.Windows.MessageBox.Show(
            "确定要清空 DuckDB 数据库中的所有基金历史净值与基准数据吗？\n清空后下次打开基金将自动从网络重新拉取并存入数据库。",
            "清空 DuckDB 数据库确认",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            await _dataService.DuckDb.ClearAllDataAsync();
            StatusMessage = "已成功清空 DuckDB 数据库数据。";
            System.Windows.MessageBox.Show(
                "DuckDB 数据库中的基金信息、历史净值与基准指数走势数据已全部清空。\n下次加载基金时将从东方财富重新获取并自动写入 DuckDB。",
                "数据库清空成功",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"清空失败: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"清空数据库失败: {ex.Message}",
                "错误",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ClearCacheAsync() => await ClearDatabaseAsync();

    [RelayCommand]
    private async Task ShowDatabaseStatsAsync()
    {
        try
        {
            var stats = await _dataService.DuckDb.GetStatsAsync();
            System.Windows.MessageBox.Show(
                $"【DuckDB 列式时序数据库运行状态】\n\n" +
                $"📁 数据库文件: {stats.DatabasePath}\n" +
                $"💾 磁盘占用: {stats.FileSizeMb:F2} MB ({stats.FileSizeBytes:N0} 字节)\n" +
                $"📈 已收录基金数: {stats.FundCount} 只\n" +
                $"📅 历史净值时序记录: {stats.TotalNavRecords:N0} 条\n" +
                $"📊 指数基准点记录: {stats.TotalBenchmarkRecords:N0} 条\n\n" +
                $"⚡ 架构优势: 采用嵌入式列式存储与高效向量化执行引擎，支持本地秒级分析千万行级金融时序数据。",
                "DuckDB 数据库运行状态",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"获取 DuckDB 数据库运行状态失败: {ex.Message}",
                "错误",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task CheckDataHealthAsync()
    {
        StatusMessage = "正在测试东方财富接口连通性...";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var list = await _dataService.SearchFundsAsync("000001");
            sw.Stop();
            if (list.Count > 0)
            {
                StatusMessage = $"数据接口正常，响应耗时 {sw.ElapsedMilliseconds}ms";
                System.Windows.MessageBox.Show(
                    $"【数据源健康状况】\n\n" +
                    $"状态: 🟢 正常连通\n" +
                    $"延迟: {sw.ElapsedMilliseconds} 毫秒\n" +
                    $"接口: 东方财富 / 天天基金公开金融 API\n" +
                    $"服务节点: 全国 CDN 智能加速集群",
                    "数据源健康检查",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = "数据接口返回空数据。";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"数据源异常: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"数据源连通失败: {ex.Message}\n请检查网络连接是否正常。",
                "网络异常",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void ShowAbout()
    {
        System.Windows.MessageBox.Show(
            "BIGA\n\n" +
            "版本: v1.0.0 (Windows Professional)\n" +
            "技术架构: C# .NET 8 + WPF (CommunityToolkit.Mvvm)\n" +
            "图表引擎: ScottPlot 5.x 矢量渲染引擎\n" +
            "数据引擎: 东方财富 / 天天基金免 Key 公开金融接口\n\n" +
            "核心功能:\n" +
            " • 实时代码/拼音/汉字联想选基\n" +
            " • 成立以来真实累计复权走势 & 沪深300基准比对\n" +
            " • 机构级量化风控归因 (CAGR / 最大回撤 / 夏普 / 卡玛 / 索提诺)\n" +
            " • 真实定投策略现金流仿真与 XIRR 真实年化报酬率解算\n\n" +
            "© 2026 BIGA. All rights reserved.",
            "关于 BIGA",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ShowIndicatorHelp()
    {
        System.Windows.MessageBox.Show(
            "【核心量化与风控指标计算说明】\n\n" +
            "1. 年化复合收益率 (CAGR)：\n" +
            "   公式：(P_end / P_start) ^ (250 / N) - 1\n" +
            "   采用 A 股公募基金 250 交易日标准，基于复权净值复合计算。\n\n" +
            "2. 历史最大回撤 (Max Drawdown)：\n" +
            "   公式：max((Peak - Nav) / Peak)\n" +
            "   衡量统计区间内任意时点买入可能遭受的最大本金回撤亏损。\n\n" +
            "3. 夏普比率 (Sharpe Ratio)：\n" +
            "   公式：(CAGR - 无风险利率 2.0%) / 年化波动率\n" +
            "   衡量承担单位总波动风险所获得的超额投资回报。\n\n" +
            "4. 卡玛比率 (Calmar Ratio)：\n" +
            "   公式：CAGR / 最大回撤\n" +
            "   衡量基金长期收益弥补历史极端下跌的能力。>1 优秀，>2 极佳。\n\n" +
            "5. 索提诺比率 (Sortino Ratio)：\n" +
            "   仅惩罚产生亏损的下行负向波动，不惩罚上行盈利波动。\n\n" +
            "6. 定投 XIRR 内部收益率：\n" +
            "   基于牛顿-拉弗森数值迭代法，精确折算每期定投现金流的时间贴现价值。",
            "量化风控指标释义",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    public void ShowPlaceholderStrategy(string strategyName)
    {
        if (strategyName.Contains("估值"))
        {
            SetStrategy("4");
            return;
        }
        if (strategyName.Contains("股债") || strategyName.Contains("再平衡"))
        {
            SetStrategy("5");
            return;
        }
        if (strategyName.Contains("网格"))
        {
            SetStrategy("3");
            return;
        }
        if (strategyName.Contains("止盈"))
        {
            SetStrategy("2");
            return;
        }
        if (strategyName.Contains("系统首选项"))
        {
            OpenSettings();
            return;
        }
        if (strategyName.Contains("对比") || strategyName.Contains("PK"))
        {
            OpenCompare();
            return;
        }
        if (strategyName.Contains("组合") || strategyName.Contains("资产配置"))
        {
            OpenPortfolio();
            return;
        }
        if (strategyName.Contains("选基") || strategyName.Contains("筛选"))
        {
            OpenScreener();
            return;
        }
    }

    [RelayCommand]
    public void OpenPortfolio()
    {
        string codeA = CurrentFund?.Code ?? "000001";
        SelectedMainTabIndex = 3;
        RequestOpenPortfolio?.Invoke(new[] { codeA, "000171" });
    }

    [RelayCommand]
    public void OpenScreener()
    {
        SelectedMainTabIndex = 1;
    }

    [RelayCommand]
    public void OpenSettings()
    {
        SelectedMainTabIndex = 6;
    }

    [RelayCommand]
    public void OpenAiSimulation()
    {
        SelectedMainTabIndex = 4;
    }

    [RelayCommand]
    public void SimulateBuyCurrentFund()
    {
        string code = CurrentFund?.Code ?? "510300";
        SelectedMainTabIndex = 4;
        RequestSimulateBuy?.Invoke(code);
    }

    [RelayCommand]
    public void OpenCompare()
    {
        string currentCode = CurrentFund?.Code ?? "000001";
        string targetCode = (currentCode == "005827") ? "161725" : "005827";
        SelectedMainTabIndex = 2;
        RequestOpenCompare?.Invoke(currentCode, targetCode);
    }

    [RelayCommand]
    public void SwitchMainTab(string tabIndexStr)
    {
        if (int.TryParse(tabIndexStr, out int tab))
        {
            SelectedMainTabIndex = tab;
        }
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            string rfStr = await _dataService.DuckDb.GetSettingAsync("RiskFreeRate", "2.0");
            string feeStr = await _dataService.DuckDb.GetSettingAsync("FeeRate", "0.10");
            string dcaStr = await _dataService.DuckDb.GetSettingAsync("DefaultDcaAmount", "1000");
            string tpStr = await _dataService.DuckDb.GetSettingAsync("TargetProfitRate", "15.0");
            string gridStr = await _dataService.DuckDb.GetSettingAsync("GridSpacing", "3.0");

            if (decimal.TryParse(rfStr, out decimal rf)) RiskFreeRate = rf;
            if (decimal.TryParse(feeStr, out decimal fee)) FeeRatePercent = fee;
            if (decimal.TryParse(dcaStr, out decimal dca)) DcaAmount = dca;
            if (decimal.TryParse(tpStr, out decimal tp)) TargetProfitThreshold = tp;
            if (decimal.TryParse(gridStr, out decimal grid)) GridSpacingPercent = grid;
        }
        catch { }
    }

    [RelayCommand]
    private void SetTimeRange(string range)
    {
        if (SelectedTimeRange == range) return;
        SelectedTimeRange = range;
        UpdateCalculationsAndPlot();
    }

    [RelayCommand]
    private void SetChartTab(string tabIndexStr)
    {
        if (int.TryParse(tabIndexStr, out int tab))
        {
            ActiveChartTab = tab;
            RequestPlotRefresh?.Invoke();
        }
    }

    [RelayCommand]
    public void SetStrategy(string stratIndexStr)
    {
        if (int.TryParse(stratIndexStr, out int idx))
        {
            SelectedStrategyIndex = idx;
            ActiveChartTab = 1; // 自动跳转至策略回测图表
            RunBacktest();
            RequestPlotRefresh?.Invoke();
        }
    }

    [RelayCommand]
    public void ExportReport()
    {
        if (CurrentFund == null || CurrentFund.NavHistory.Count == 0 || Metrics == null || BacktestResult == null)
        {
            System.Windows.MessageBox.Show("当前无有效基金数据可供导出，请先加载基金。", "导出提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出基金量化分析与策略回测报告 (CSV)",
            Filter = "CSV 报表文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            FileName = $"{CurrentFund.Code}_{CurrentFund.Name}_量化投研报告_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                var filteredNavs = GetFilteredNavHistory();
                ExportService.ExportToCsv(saveDialog.FileName, CurrentFund, Metrics, BacktestResult, filteredNavs);
                StatusMessage = $"成功导出量化报表至: {saveDialog.FileName}";

                var openResult = System.Windows.MessageBox.Show(
                    $"量化分析报表已成功导出！\n\n文件保存路径：\n{saveDialog.FileName}\n\n是否立即打开该文件？",
                    "导出成功",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);

                if (openResult == System.Windows.MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveDialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"导出失败: {ex.Message}";
                System.Windows.MessageBox.Show($"导出报表失败: {ex.Message}", "导出异常", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void ExportHtmlReport()
    {
        if (CurrentFund == null || CurrentFund.NavHistory.Count == 0 || Metrics == null || BacktestResult == null)
        {
            System.Windows.MessageBox.Show("当前无有效基金数据可供导出，请先加载基金。", "导出提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出基金量化分析与投研研报 (HTML / PDF)",
            Filter = "HTML 投研研报 (*.html)|*.html|所有文件 (*.*)|*.*",
            FileName = $"{CurrentFund.Code}_{CurrentFund.Name}_量化投研研报_{DateTime.Now:yyyyMMdd_HHmm}.html"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                var filteredNavs = GetFilteredNavHistory();
                ExportService.ExportToHtml(saveDialog.FileName, CurrentFund, Metrics, BacktestResult, filteredNavs);
                StatusMessage = $"成功导出量化研报至: {saveDialog.FileName}";

                var openResult = System.Windows.MessageBox.Show(
                    $"机构级量化研报已成功生成！\n\n文件保存路径：\n{saveDialog.FileName}\n\n是否立即在默认浏览器中查看该研报？(支持浏览器一键另存/打印为 PDF)",
                    "导出成功",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);

                if (openResult == System.Windows.MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveDialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"导出失败: {ex.Message}";
                System.Windows.MessageBox.Show($"导出研报失败: {ex.Message}", "导出异常", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void ExportPitchDeckReport()
    {
        if (CurrentFund == null || CurrentFund.NavHistory.Count == 0 || Metrics == null)
        {
            System.Windows.MessageBox.Show("当前无有效基金数据可供导出，请先加载基金。", "导出提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出投资决策委员会高管路演推介画册 (Executive Pitch Deck)",
            Filter = "HTML 研报 (*.html)|*.html|所有文件 (*.*)|*.*",
            FileName = $"{CurrentFund.Code}_{CurrentFund.Name}_高管路演PitchDeck_{DateTime.Now:yyyyMMdd_HHmm}.html"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                ExportService.ExportExecutivePitchDeckToHtml(saveDialog.FileName, CurrentFund, Metrics);
                StatusMessage = $"成功导出高管路演 Pitch Deck 研报至: {saveDialog.FileName}";

                var openResult = System.Windows.MessageBox.Show(
                    $"机构高管路演推介研报已成功生成！\n\n文件保存路径：\n{saveDialog.FileName}\n\n是否立即在默认浏览器中查看该研报？(支持浏览器一键另存/打印为高分辨率 PDF)",
                    "导出成功",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);

                if (openResult == System.Windows.MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveDialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"导出失败: {ex.Message}";
                System.Windows.MessageBox.Show($"导出高管路演研报失败: {ex.Message}", "导出异常", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void OpenCalculator()
    {
        SelectedMainTabIndex = 5;
    }

    public async Task LoadFavoritesAsync()
    {
        try
        {
            var favs = await _dataService.DuckDb.GetFavoritesAsync();
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Favorites.Clear();
                foreach (var f in favs)
                {
                    Favorites.Add(f);
                }
                HasFavorites = Favorites.Count > 0;
            });
        }
        catch { }
    }

    [RelayCommand]
    public async Task ToggleFavoriteAsync()
    {
        if (CurrentFund == null || string.IsNullOrEmpty(CurrentFund.Code)) return;

        try
        {
            if (IsCurrentFavorite)
            {
                await _dataService.DuckDb.RemoveFavoriteAsync(CurrentFund.Code);
                IsCurrentFavorite = false;
                StatusMessage = $"已从自选池移除: {CurrentFund.Name}";
            }
            else
            {
                await _dataService.DuckDb.AddFavoriteAsync(CurrentFund.Code, CurrentFund.Name, CurrentFund.Type);
                IsCurrentFavorite = true;
                StatusMessage = $"已添加至自选池: {CurrentFund.Name}";
            }

            await LoadFavoritesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"更新自选失败: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task AddFavoriteQuickAsync(FundInfo? fund)
    {
        if (fund == null || string.IsNullOrEmpty(fund.Code)) return;
        try
        {
            bool isFav = await _dataService.DuckDb.IsFavoriteAsync(fund.Code);
            if (isFav)
            {
                await _dataService.DuckDb.RemoveFavoriteAsync(fund.Code);
                StatusMessage = $"已从自选池移除: {fund.Name}";
            }
            else
            {
                await _dataService.DuckDb.AddFavoriteAsync(fund.Code, fund.Name, fund.Type);
                StatusMessage = $"已成功添加自选: {fund.Name} ({fund.Code})";
            }

            if (CurrentFund != null && CurrentFund.Code == fund.Code)
            {
                IsCurrentFavorite = !isFav;
            }

            await LoadFavoritesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"操作自选失败: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RemoveFavoriteQuickAsync(string? code)
    {
        if (string.IsNullOrEmpty(code)) return;
        try
        {
            await _dataService.DuckDb.RemoveFavoriteAsync(code);
            if (CurrentFund != null && CurrentFund.Code == code)
            {
                IsCurrentFavorite = false;
            }
            StatusMessage = $"已移除自选基金: {code}";
            await LoadFavoritesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"移除自选失败: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ClearAllFavoritesAsync()
    {
        if (Favorites.Count == 0)
        {
            System.Windows.MessageBox.Show("当前自选池为空，无需清空。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"确定要清空所有自选基金吗？（共 {Favorites.Count} 只）",
            "清空自选确认",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            foreach (var fav in Favorites.ToList())
            {
                await _dataService.DuckDb.RemoveFavoriteAsync(fav.Code);
            }
            IsCurrentFavorite = false;
            StatusMessage = "已清空所有自选基金。";
            await LoadFavoritesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"清空自选失败: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SelectFavoriteAsync(UserFavorite? fav)
    {
        if (fav == null) return;
        IsSearchOpen = false;
        SearchKeyword = $"{fav.Code} {fav.Name}";
        await LoadFundDetailAsync(fav.Code);
    }

    [RelayCommand]
    public void RunBacktest()
    {
        if (CurrentFund == null || CurrentFund.NavHistory.Count < 2) return;

        var filteredNavs = GetFilteredNavHistory();
        if (filteredNavs.Count < 2) return;

        var freq = SelectedFrequencyIndex switch
        {
            1 => DcaFrequency.WeeklyMonday,
            2 => DcaFrequency.MonthlyFirstDay,
            _ => DcaFrequency.WeeklyThursday
        };

        var strat = SelectedStrategyIndex switch
        {
            1 => StrategyType.MaTimingDca,
            2 => StrategyType.TargetProfitDca,
            3 => StrategyType.GridTrading,
            4 => StrategyType.ValuationPercentileDca,
            5 => StrategyType.StockBondRebalance,
            _ => StrategyType.RegularDca
        };

        BacktestResult = BacktestEngine.RunBacktest(
            filteredNavs,
            periodicAmount: DcaAmount,
            frequency: freq,
            strategyType: strat,
            maPeriod: MaPeriod > 0 ? MaPeriod : 60,
            targetProfitRate: TargetProfitThreshold / 100m,
            gridSpacing: GridSpacingPercent / 100m,
            rebalanceThreshold: RebalanceThresholdPercent / 100m,
            valuationWindow: ValuationWindow > 0 ? ValuationWindow : 250,
            subscriptionFeeRate: FeeRatePercent / 100m);
        RequestPlotRefresh?.Invoke();
    }

    public void UpdateCalculationsAndPlot()
    {
        if (CurrentFund == null || CurrentFund.NavHistory.Count == 0) return;

        var filteredNavs = GetFilteredNavHistory();
        if (filteredNavs.Count == 0) return;

        // 1. 量化指标计算 (采用 DuckDB 中用户配置的无风险利率)
        Metrics = QuantCalculator.CalculateMetrics(
            filteredNavs,
            GetPeriodName(SelectedTimeRange),
            CurrentFund.BenchmarkCsi300,
            riskFreeRate: RiskFreeRate);

        // 2. 五维量化综合打分与评级计算
        ScoreCard = QuantCalculator.CalculateFundScore(CurrentFund, Metrics);
        CurrentFund.ScoreCard = ScoreCard;

        // 3. Brinson-Fachler 超额收益业绩归因计算
        BrinsonResult = BrinsonAttributionEngine.CalculateFundBrinsonAttribution(CurrentFund, CurrentFund.BenchmarkCsi300);
        CurrentFund.BrinsonResult = BrinsonResult;

        // 4. 投资风格漂移分析 (Style Drift)
        StyleDrift = QuantCalculator.AnalyzeStyleDrift(CurrentFund, Metrics);
        CurrentFund.StyleDrift = StyleDrift;

        // 5. 实时估值状态同步
        RealtimeValuation = CurrentFund.RealtimeValuation;

        // 6. Barra CNE5/CNE6 风格多因子归因计算
        BarraAttribution = QuantCalculator.CalculateBarraAttribution(CurrentFund, filteredNavs, CurrentFund.BenchmarkCsi300, riskFreeRate: RiskFreeRate);
        CurrentFund.BarraAttribution = BarraAttribution;
        if (Metrics != null) Metrics.BarraAttribution = BarraAttribution;

        // 7. 基金经理职业生涯任期统计与滚动胜率矩阵
        ManagerProfile = QuantCalculator.CalculateManagerProfileAndWinRate(CurrentFund, CurrentFund.BenchmarkCsi300);
        CurrentFund.ManagerProfile = ManagerProfile;
        TimingAbility = Metrics?.TimingAbility ?? QuantCalculator.CalculateTimingAndSelectionAbility(filteredNavs, CurrentFund.BenchmarkCsi300, RiskFreeRate);
        CurrentFund.TimingAbility = TimingAbility;
        if (Metrics != null)
        {
            Metrics.ManagerProfile = ManagerProfile;
            Metrics.TimingAbility = TimingAbility;
            if (Metrics.FamaFrenchResult != null) CurrentFund.FamaFrenchResult = Metrics.FamaFrenchResult;
            if (Metrics.UnderwaterAnalysis != null) CurrentFund.UnderwaterAnalysis = Metrics.UnderwaterAnalysis;
            BullBearCapture = Metrics.BullBearCapture;
            CurrentFund.BullBearCapture = BullBearCapture;
            DueDiligence = Metrics.DueDiligence;
            CurrentFund.DueDiligence = DueDiligence;
        }

        // 8. 策略定投回测计算
        RunBacktest();

        // 9. Phase 15 全闭环科学投资建议与短中期科学评估、模拟预测对比
        ScientificAdvice = QuantCalculator.GenerateScientificInvestmentAdvice(CurrentFund, filteredNavs, CurrentFund.BenchmarkCsi300);
        ShortTermEvaluation = ScientificAdvice.ShortTermEvaluation;
        MediumTermEvaluation = ScientificAdvice.MediumTermEvaluation;
        HorizonConcordance = ScientificAdvice.HorizonConcordance;
        ForecastRealityComparison = ScientificAdvice.RealityAudit;
        SimulationForecast = ScientificAdvice.SimulationForecast;
        ExperienceReport = ScientificAdvice.ExperienceReport;

        // 10. Phase 17 机构级 4433 选基、阿尔法持续性与因子 IC/IR 效力检验
        Result4433 = CurrentFund.Result4433;
        AlphaPersistence = CurrentFund.AlphaPersistence;
        FactorIcAnalysis = CurrentFund.FactorIcAnalysis;
        FactorCrowding = CurrentFund.FactorCrowding;

        // 11. 通知图表重绘
        RequestPlotRefresh?.Invoke();
    }

    public List<NavRecord> GetFilteredNavHistory()
    {
        if (CurrentFund == null || CurrentFund.NavHistory.Count == 0)
        {
            return new List<NavRecord>();
        }

        DateTime latestDate = CurrentFund.NavHistory[^1].Date;
        DateTime cutoff = SelectedTimeRange switch
        {
            "1M" => latestDate.AddMonths(-1),
            "3M" => latestDate.AddMonths(-3),
            "6M" => latestDate.AddMonths(-6),
            "1Y" => latestDate.AddYears(-1),
            "3Y" => latestDate.AddYears(-3),
            "5Y" => latestDate.AddYears(-5),
            _ => DateTime.MinValue
        };

        return CurrentFund.NavHistory.Where(x => x.Date >= cutoff).ToList();
    }

    private static string GetPeriodName(string range) => range switch
    {
        "1M" => "近1个月",
        "3M" => "近3个月",
        "6M" => "近6个月",
        "1Y" => "近1年",
        "3Y" => "近3年",
        "5Y" => "近5年",
        _ => "成立以来"
    };
}
