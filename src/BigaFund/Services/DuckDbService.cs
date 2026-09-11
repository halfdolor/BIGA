using System.IO;
using DuckDB.NET.Data;
using BigaFund.Models;

namespace BigaFund.Services;

public class DatabaseStats
{
    public int FundCount { get; set; }
    public long TotalNavRecords { get; set; }
    public long TotalBenchmarkRecords { get; set; }
    public long FileSizeBytes { get; set; }
    public string DatabasePath { get; set; } = string.Empty;

    public double FileSizeMb => Math.Round((double)FileSizeBytes / (1024 * 1024), 2);
}

public class DuckDbService
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly object _lock = new();
    private bool _isInitialized;

    public string DatabasePath => _dbPath;

    public DuckDbService()
    {
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BIGA", "data");

        if (!Directory.Exists(baseDir))
        {
            Directory.CreateDirectory(baseDir);
        }

        _dbPath = Path.Combine(baseDir, "biga.duckdb");
        _connectionString = $"Data Source={_dbPath}";
    }

    public DuckDbService(string dbPath)
    {
        _dbPath = dbPath;
        _connectionString = $"Data Source={_dbPath}";
    }

    /// <summary>
    /// 初始化 DuckDB 数据库与表结构
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;

        lock (_lock)
        {
            if (_isInitialized) return;

            using var connection = new DuckDBConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                -- 1. 基金主表
                CREATE TABLE IF NOT EXISTS funds (
                    code VARCHAR PRIMARY KEY,
                    name VARCHAR NOT NULL,
                    type VARCHAR,
                    manager VARCHAR,
                    tenure VARCHAR,
                    fund_size VARCHAR,
                    updated_at TIMESTAMP
                );

                -- 2. 基金历史净值时序表
                CREATE TABLE IF NOT EXISTS nav_history (
                    fund_code VARCHAR,
                    nav_date DATE,
                    unit_nav DOUBLE,
                    cumulative_nav DOUBLE,
                    daily_return DOUBLE,
                    PRIMARY KEY (fund_code, nav_date)
                );

                -- 3. 基准指数时序表 (沪深300 / 同类平均)
                CREATE TABLE IF NOT EXISTS benchmarks (
                    fund_code VARCHAR,
                    benchmark_type VARCHAR,
                    record_date DATE,
                    return_rate DOUBLE,
                    PRIMARY KEY (fund_code, benchmark_type, record_date)
                );

                -- 4. 用户自选基金表
                CREATE TABLE IF NOT EXISTS favorites (
                    code VARCHAR PRIMARY KEY,
                    name VARCHAR,
                    type VARCHAR,
                    added_at TIMESTAMP
                );

                -- 5. 系统用户首选项与量化参数配置表
                CREATE TABLE IF NOT EXISTS app_settings (
                    key VARCHAR PRIMARY KEY,
                    value VARCHAR,
                    updated_at TIMESTAMP
                );

                -- 6. 全市场公募基金大厅目录索引表 (~1.8万只公募基金轻量全景)
                CREATE TABLE IF NOT EXISTS market_catalog (
                    code VARCHAR PRIMARY KEY,
                    pinyin VARCHAR,
                    name VARCHAR,
                    type VARCHAR,
                    pinyin_full VARCHAR,
                    updated_at TIMESTAMP
                );

                -- 7. 基金前十大重仓股持仓明细表
                CREATE TABLE IF NOT EXISTS fund_holdings (
                    fund_code VARCHAR,
                    stock_code VARCHAR,
                    stock_name VARCHAR,
                    weight_percent DOUBLE,
                    share_change VARCHAR,
                    report_date VARCHAR,
                    industry VARCHAR,
                    PRIMARY KEY (fund_code, stock_code, report_date)
                );
                ALTER TABLE fund_holdings ADD COLUMN IF NOT EXISTS industry VARCHAR;

                -- 8. 基金季度大类资产配置比例表
                CREATE TABLE IF NOT EXISTS fund_asset_allocations (
                    fund_code VARCHAR,
                    report_date VARCHAR,
                    stock_ratio DOUBLE,
                    bond_ratio DOUBLE,
                    cash_ratio DOUBLE,
                    other_ratio DOUBLE,
                    net_asset DOUBLE,
                    PRIMARY KEY (fund_code, report_date)
                );

                -- 9. AI 模拟操盘账户主表 (默认 10 万初始金)
                CREATE TABLE IF NOT EXISTS sim_account (
                    account_id VARCHAR PRIMARY KEY,
                    account_name VARCHAR,
                    initial_cash DOUBLE,
                    cash DOUBLE,
                    created_at TIMESTAMP,
                    updated_at TIMESTAMP
                );

                -- 10. AI 模拟操盘持仓头寸明细表
                CREATE TABLE IF NOT EXISTS sim_positions (
                    account_id VARCHAR,
                    fund_code VARCHAR,
                    fund_name VARCHAR,
                    fund_type VARCHAR,
                    shares DOUBLE,
                    cost_basis DOUBLE,
                    total_cost DOUBLE,
                    latest_nav DOUBLE,
                    nav_date VARCHAR,
                    ai_signal VARCHAR,
                    ai_score DOUBLE,
                    ai_recommendation VARCHAR,
                    updated_at TIMESTAMP,
                    PRIMARY KEY (account_id, fund_code)
                );

                -- 11. AI 模拟操盘交易流水记录表
                CREATE TABLE IF NOT EXISTS sim_trades (
                    trade_id VARCHAR PRIMARY KEY,
                    account_id VARCHAR,
                    trade_time TIMESTAMP,
                    fund_code VARCHAR,
                    fund_name VARCHAR,
                    action_type VARCHAR,
                    action_text VARCHAR,
                    nav DOUBLE,
                    shares DOUBLE,
                    amount DOUBLE,
                    fee DOUBLE,
                    realized_profit DOUBLE,
                    realized_profit_rate DOUBLE,
                    reason VARCHAR
                );
            ";
            cmd.ExecuteNonQuery();

            _isInitialized = true;
        }
    }

    /// <summary>
    /// 将基金基础信息、全部历史净值和基准走势批量写入 DuckDB
    /// </summary>
    public async Task SaveFundDetailAsync(FundDetail detail, CancellationToken ct = default)
    {
        if (detail == null || string.IsNullOrWhiteSpace(detail.Code)) return;
        Initialize();

        await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var transaction = connection.BeginTransaction();
                try
                {
                    // 1. 保存/更新基金主信息
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            DELETE FROM funds WHERE code = $1;
                            INSERT INTO funds (code, name, type, manager, tenure, fund_size, updated_at)
                            VALUES ($1, $2, $3, $4, $5, $6, current_timestamp);
                        ";
                        cmd.Parameters.Add(new DuckDBParameter(detail.Code));
                        cmd.Parameters.Add(new DuckDBParameter(detail.Name ?? ""));
                        cmd.Parameters.Add(new DuckDBParameter(detail.Type ?? ""));
                        cmd.Parameters.Add(new DuckDBParameter(detail.ManagerName ?? ""));
                        cmd.Parameters.Add(new DuckDBParameter(detail.ManagerTenure ?? ""));
                        cmd.Parameters.Add(new DuckDBParameter(detail.FundSize ?? ""));
                        cmd.ExecuteNonQuery();
                    }

                    // 2. 清理旧净值与写入新净值
                    using (var delCmd = connection.CreateCommand())
                    {
                        delCmd.CommandText = "DELETE FROM nav_history WHERE fund_code = $1;";
                        delCmd.Parameters.Add(new DuckDBParameter(detail.Code));
                        delCmd.ExecuteNonQuery();
                    }

                    if (detail.NavHistory != null && detail.NavHistory.Count > 0)
                    {
                        using var appender = connection.CreateAppender("nav_history");
                        foreach (var nav in detail.NavHistory)
                        {
                            var row = appender.CreateRow();
                            row.AppendValue(detail.Code);
                            row.AppendValue(new DateOnly(nav.Date.Year, nav.Date.Month, nav.Date.Day));
                            row.AppendValue((double)nav.UnitNav);
                            row.AppendValue((double)nav.CumulativeNav);
                            row.AppendValue((double)nav.DailyReturn);
                            row.EndRow();
                        }
                    }

                    // 3. 清理旧基准与写入新基准
                    using (var delBmCmd = connection.CreateCommand())
                    {
                        delBmCmd.CommandText = "DELETE FROM benchmarks WHERE fund_code = $1;";
                        delBmCmd.Parameters.Add(new DuckDBParameter(detail.Code));
                        delBmCmd.ExecuteNonQuery();
                    }

                    if (detail.BenchmarkCsi300 != null && detail.BenchmarkCsi300.Count > 0)
                    {
                        using var bmAppender = connection.CreateAppender("benchmarks");
                        foreach (var bm in detail.BenchmarkCsi300)
                        {
                            var row = bmAppender.CreateRow();
                            row.AppendValue(detail.Code);
                            row.AppendValue("CSI300");
                            row.AppendValue(new DateOnly(bm.Date.Year, bm.Date.Month, bm.Date.Day));
                            row.AppendValue((double)bm.CumulativeReturnRate);
                            row.EndRow();
                        }

                        if (detail.PeerAverage != null)
                        {
                            foreach (var peer in detail.PeerAverage)
                            {
                                var row = bmAppender.CreateRow();
                                row.AppendValue(detail.Code);
                                row.AppendValue("PEER_AVG");
                                row.AppendValue(new DateOnly(peer.Date.Year, peer.Date.Month, peer.Date.Day));
                                row.AppendValue((double)peer.CumulativeReturnRate);
                                row.EndRow();
                            }
                        }
                    }

                    // 4. 清理旧持仓与写入前十大重仓股
                    using (var delHoldCmd = connection.CreateCommand())
                    {
                        delHoldCmd.CommandText = "DELETE FROM fund_holdings WHERE fund_code = $1;";
                        delHoldCmd.Parameters.Add(new DuckDBParameter(detail.Code));
                        delHoldCmd.ExecuteNonQuery();
                    }

                    if (detail.Holdings != null && detail.Holdings.Count > 0)
                    {
                        using var holdAppender = connection.CreateAppender("fund_holdings");
                        foreach (var hold in detail.Holdings)
                        {
                            var row = holdAppender.CreateRow();
                            row.AppendValue(detail.Code);
                            row.AppendValue(hold.StockCode ?? "");
                            row.AppendValue(hold.StockName ?? "");
                            row.AppendValue((double)hold.WeightPercent);
                            row.AppendValue(hold.ShareChange ?? "");
                            row.AppendValue(hold.ReportDate ?? "");
                            row.AppendValue(hold.Industry ?? "");
                            row.EndRow();
                        }
                    }

                    // 5. 清理旧大类资产配置与写入新配置
                    using (var delAllocCmd = connection.CreateCommand())
                    {
                        delAllocCmd.CommandText = "DELETE FROM fund_asset_allocations WHERE fund_code = $1;";
                        delAllocCmd.Parameters.Add(new DuckDBParameter(detail.Code));
                        delAllocCmd.ExecuteNonQuery();
                    }

                    if (detail.AssetAllocations != null && detail.AssetAllocations.Count > 0)
                    {
                        using var allocAppender = connection.CreateAppender("fund_asset_allocations");
                        foreach (var alloc in detail.AssetAllocations)
                        {
                            var row = allocAppender.CreateRow();
                            row.AppendValue(detail.Code);
                            row.AppendValue(alloc.ReportDate ?? "");
                            row.AppendValue((double)alloc.StockRatio);
                            row.AppendValue((double)alloc.BondRatio);
                            row.AppendValue((double)alloc.CashRatio);
                            row.AppendValue((double)alloc.OtherRatio);
                            row.AppendValue((double)alloc.NetAsset);
                            row.EndRow();
                        }
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }, ct);
    }

    /// <summary>
    /// 从 DuckDB 读取基金完整详情、历史净值与基准数据
    /// </summary>
    public async Task<(FundDetail? Detail, DateTime? UpdatedAt)> GetFundDetailAsync(string fundCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fundCode)) return (null, null);
        Initialize();

        return await Task.Run<(FundDetail?, DateTime?)>(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                // 1. 查询基金主信息
                FundDetail? detail = null;
                DateTime? updatedAt = null;

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT code, name, type, manager, tenure, fund_size, updated_at FROM funds WHERE code = $1;";
                    cmd.Parameters.Add(new DuckDBParameter(fundCode));

                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        detail = new FundDetail
                        {
                            Code = reader.GetString(0),
                            Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            Type = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            ManagerName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            ManagerTenure = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            FundSize = reader.IsDBNull(5) ? "" : reader.GetString(5)
                        };

                        if (!reader.IsDBNull(6))
                        {
                            updatedAt = reader.GetDateTime(6);
                        }
                    }
                }

                if (detail == null) return (null, null);

                // 2. 查询历史净值时序 (按日期升序)
                using (var navCmd = connection.CreateCommand())
                {
                    navCmd.CommandText = @"
                        SELECT nav_date, unit_nav, cumulative_nav, daily_return 
                        FROM nav_history 
                        WHERE fund_code = $1 
                        ORDER BY nav_date ASC;
                    ";
                    navCmd.Parameters.Add(new DuckDBParameter(fundCode));

                    using var navReader = navCmd.ExecuteReader();
                    while (navReader.Read())
                    {
                        var d = navReader.GetDateTime(0);
                        detail.NavHistory.Add(new NavRecord
                        {
                            Date = d,
                            UnitNav = (decimal)navReader.GetDouble(1),
                            CumulativeNav = (decimal)navReader.GetDouble(2),
                            DailyReturn = (decimal)navReader.GetDouble(3)
                        });
                    }
                }

                // 3. 查询基准指数走势
                using (var bmCmd = connection.CreateCommand())
                {
                    bmCmd.CommandText = @"
                        SELECT benchmark_type, record_date, return_rate 
                        FROM benchmarks 
                        WHERE fund_code = $1 
                        ORDER BY record_date ASC;
                    ";
                    bmCmd.Parameters.Add(new DuckDBParameter(fundCode));

                    using var bmReader = bmCmd.ExecuteReader();
                    while (bmReader.Read())
                    {
                        string type = bmReader.GetString(0);
                        var date = bmReader.GetDateTime(1);
                        decimal ret = (decimal)bmReader.GetDouble(2);

                        var rec = new BenchmarkRecord
                        {
                            Date = date,
                            CumulativeReturnRate = ret
                        };

                        if (type == "CSI300")
                        {
                            detail.BenchmarkCsi300.Add(rec);
                        }
                        else if (type == "PEER_AVG")
                        {
                            detail.PeerAverage.Add(rec);
                        }
                    }
                }

                // 4. 查询前十大重仓股持仓
                using (var holdCmd = connection.CreateCommand())
                {
                    holdCmd.CommandText = @"
                        SELECT stock_code, stock_name, weight_percent, share_change, report_date, industry
                        FROM fund_holdings
                        WHERE fund_code = $1
                        ORDER BY weight_percent DESC;
                    ";
                    holdCmd.Parameters.Add(new DuckDBParameter(fundCode));
                    using var holdReader = holdCmd.ExecuteReader();
                    while (holdReader.Read())
                    {
                        detail.Holdings.Add(new FundStockHolding
                        {
                            StockCode = holdReader.IsDBNull(0) ? "" : holdReader.GetString(0),
                            StockName = holdReader.IsDBNull(1) ? "" : holdReader.GetString(1),
                            WeightPercent = holdReader.IsDBNull(2) ? 0m : (decimal)holdReader.GetDouble(2),
                            ShareChange = holdReader.IsDBNull(3) ? "" : holdReader.GetString(3),
                            ReportDate = holdReader.IsDBNull(4) ? "" : holdReader.GetString(4),
                            Industry = holdReader.IsDBNull(5) ? "" : holdReader.GetString(5)
                        });
                    }
                }

                // 5. 查询大类资产配置比例历史
                using (var allocCmd = connection.CreateCommand())
                {
                    allocCmd.CommandText = @"
                        SELECT report_date, stock_ratio, bond_ratio, cash_ratio, other_ratio, net_asset
                        FROM fund_asset_allocations
                        WHERE fund_code = $1
                        ORDER BY report_date DESC;
                    ";
                    allocCmd.Parameters.Add(new DuckDBParameter(fundCode));
                    using var allocReader = allocCmd.ExecuteReader();
                    while (allocReader.Read())
                    {
                        detail.AssetAllocations.Add(new FundAssetAllocation
                        {
                            ReportDate = allocReader.IsDBNull(0) ? "" : allocReader.GetString(0),
                            StockRatio = allocReader.IsDBNull(1) ? 0m : (decimal)allocReader.GetDouble(1),
                            BondRatio = allocReader.IsDBNull(2) ? 0m : (decimal)allocReader.GetDouble(2),
                            CashRatio = allocReader.IsDBNull(3) ? 0m : (decimal)allocReader.GetDouble(3),
                            OtherRatio = allocReader.IsDBNull(4) ? 0m : (decimal)allocReader.GetDouble(4),
                            NetAsset = allocReader.IsDBNull(5) ? 0m : (decimal)allocReader.GetDouble(5)
                        });
                    }
                }

                detail.StyleBox = QuantCalculator.EvaluateMorningstarStyle(detail);

                return (detail, updatedAt);
            }
        }, ct);
    }

    /// <summary>
    /// 获取 DuckDB 数据库运行与数据统计概况
    /// </summary>
    public async Task<DatabaseStats> GetStatsAsync(CancellationToken ct = default)
    {
        Initialize();
        var stats = new DatabaseStats
        {
            DatabasePath = _dbPath
        };

        if (File.Exists(_dbPath))
        {
            stats.FileSizeBytes = new FileInfo(_dbPath).Length;
        }

        await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();

                cmd.CommandText = "SELECT COUNT(*) FROM funds;";
                stats.FundCount = Convert.ToInt32(cmd.ExecuteScalar());

                cmd.CommandText = "SELECT COUNT(*) FROM nav_history;";
                stats.TotalNavRecords = Convert.ToInt64(cmd.ExecuteScalar());

                cmd.CommandText = "SELECT COUNT(*) FROM benchmarks;";
                stats.TotalBenchmarkRecords = Convert.ToInt64(cmd.ExecuteScalar());
            }
        }, ct);

        return stats;
    }

    /// <summary>
    /// 清空 DuckDB 中的全部数据表
    /// </summary>
    public async Task ClearAllDataAsync(CancellationToken ct = default)
    {
        Initialize();
        await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    DELETE FROM nav_history;
                    DELETE FROM benchmarks;
                    DELETE FROM funds;
                    CHECKPOINT;
                ";
                cmd.ExecuteNonQuery();
            }
        }, ct);
    }

    /// <summary>
    /// 添加自选基金
    /// </summary>
    public async Task AddFavoriteAsync(string code, string name, string type, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        Initialize();

        await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    DELETE FROM favorites WHERE code = $1;
                    INSERT INTO favorites (code, name, type, added_at)
                    VALUES ($1, $2, $3, current_timestamp);
                ";
                cmd.Parameters.Add(new DuckDBParameter(code));
                cmd.Parameters.Add(new DuckDBParameter(name ?? ""));
                cmd.Parameters.Add(new DuckDBParameter(type ?? ""));
                cmd.ExecuteNonQuery();
            }
        }, ct);
    }

    /// <summary>
    /// 移除自选基金
    /// </summary>
    public async Task RemoveFavoriteAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        Initialize();

        await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM favorites WHERE code = $1;";
                cmd.Parameters.Add(new DuckDBParameter(code));
                cmd.ExecuteNonQuery();
            }
        }, ct);
    }

    /// <summary>
    /// 检查指定基金是否为自选
    /// </summary>
    public async Task<bool> IsFavoriteAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        Initialize();

        return await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM favorites WHERE code = $1;";
                cmd.Parameters.Add(new DuckDBParameter(code));
                long count = Convert.ToInt64(cmd.ExecuteScalar());
                return count > 0;
            }
        }, ct);
    }

    /// <summary>
    /// 获取所有自选基金列表（按添加时间降序）
    /// </summary>
    public async Task<List<UserFavorite>> GetFavoritesAsync(CancellationToken ct = default)
    {
        Initialize();
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var list = new List<UserFavorite>();
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT code, name, type, added_at FROM favorites ORDER BY added_at DESC;";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new UserFavorite
                    {
                        Code = reader.GetString(0),
                        Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Type = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        AddedAt = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3)
                    });
                }

                return list;
            }
        }, ct);
    }

    /// <summary>
    /// 保存或更新系统首选项参数
    /// </summary>
    public async Task SaveSettingAsync(string key, string value, CancellationToken ct = default)
    {
        Initialize();
        await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO app_settings (key, value, updated_at)
                    VALUES ($1, $2, $3)
                    ON CONFLICT (key) DO UPDATE SET
                        value = EXCLUDED.value,
                        updated_at = EXCLUDED.updated_at;
                ";
                cmd.Parameters.Add(new DuckDBParameter(key));
                cmd.Parameters.Add(new DuckDBParameter(value));
                cmd.Parameters.Add(new DuckDBParameter(DateTime.Now));
                cmd.ExecuteNonQuery();
            }
        }, ct);
    }

    /// <summary>
    /// 读取系统首选项参数，若不存在则返回默认值
    /// </summary>
    public async Task<string> GetSettingAsync(string key, string defaultValue = "", CancellationToken ct = default)
    {
        Initialize();
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT value FROM app_settings WHERE key = $1;";
                cmd.Parameters.Add(new DuckDBParameter(key));
                using var reader = cmd.ExecuteReader();
                if (reader.Read() && !reader.IsDBNull(0))
                {
                    return reader.GetString(0);
                }
                return defaultValue;
            }
        }, ct);
    }

    /// <summary>
    /// 获取全部系统首选项配置字典
    /// </summary>
    public async Task<Dictionary<string, string>> GetAllSettingsAsync(CancellationToken ct = default)
    {
        Initialize();
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var dict = new Dictionary<string, string>();
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT key, value FROM app_settings;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    dict[reader.GetString(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1);
                }
                return dict;
            }
        }, ct);
    }

    /// <summary>
    /// 获取 DuckDB 本地存储的所有基金概览名录（用于量化多因子选基工作台）
    /// </summary>
    public async Task<List<StoredFundItem>> GetAllStoredFundsAsync(CancellationToken ct = default)
    {
        Initialize();
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var list = new List<StoredFundItem>();
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT f.code, f.name, f.type, f.manager, f.tenure, f.fund_size, f.updated_at,
                           COALESCE((SELECT unit_nav FROM nav_history WHERE fund_code = f.code ORDER BY nav_date DESC LIMIT 1), 0.0) as latest_nav
                    FROM funds f
                    ORDER BY f.updated_at DESC;
                ";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new StoredFundItem
                    {
                        Code = reader.GetString(0),
                        Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Type = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Manager = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Tenure = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        FundSize = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        UpdatedAt = reader.IsDBNull(6) ? DateTime.MinValue : reader.GetDateTime(6),
                        LatestNav = reader.IsDBNull(7) ? 0m : (decimal)reader.GetDouble(7)
                    });
                }

                // 高速一次性全表读取所有基金净值时序，进行向量化量化因子深度赋能
                if (list.Count > 0)
                {
                    var navsByFund = new Dictionary<string, List<NavRecord>>();
                    using var navCmd = connection.CreateCommand();
                    navCmd.CommandText = "SELECT fund_code, nav_date, unit_nav, cumulative_nav FROM nav_history ORDER BY fund_code, nav_date ASC;";
                    using (var navReader = navCmd.ExecuteReader())
                    {
                        while (navReader.Read())
                        {
                            string fCode = navReader.GetString(0);
                            if (!navsByFund.TryGetValue(fCode, out var nList))
                            {
                                nList = new List<NavRecord>();
                                navsByFund[fCode] = nList;
                            }
                            nList.Add(new NavRecord
                            {
                                Date = navReader.GetDateTime(1),
                                UnitNav = (decimal)navReader.GetDouble(2),
                                CumulativeNav = (decimal)navReader.GetDouble(3)
                            });
                        }
                    }

                    foreach (var item in list)
                    {
                        if (navsByFund.TryGetValue(item.Code, out var navs) && navs.Count > 1)
                        {
                            DateTime latest = navs[^1].Date;
                            DateTime cutoff1Y = latest.AddYears(-1);
                            var navs1Y = navs.Where(x => x.Date >= cutoff1Y).ToList();
                            var targetNavs = navs1Y.Count >= 2 ? navs1Y : navs;
                            var metrics = QuantCalculator.CalculateMetrics(targetNavs, "选基因子统计");

                            item.Return1Y = metrics.TotalReturn;
                            item.MaxDrawdown = metrics.MaxDrawdown;
                            item.SharpeRatio = metrics.SharpeRatio;
                            item.AnnualizedVol = metrics.AnnualizedVolatility;

                            var dummyFund = new FundDetail
                            {
                                Code = item.Code,
                                Name = item.Name,
                                Type = item.Type,
                                ManagerName = item.Manager,
                                ManagerTenure = item.Tenure,
                                FundSize = item.FundSize,
                                NavHistory = targetNavs
                            };
                            var scoreCard = QuantCalculator.CalculateFundScore(dummyFund, metrics);
                            item.QuantScore = scoreCard.OverallScore;
                            item.RatingGrade = scoreCard.RatingGrade;

                            // Phase 12 高阶机构风控与评级因子
                            item.SortinoRatio = metrics.SortinoRatio;
                            item.CaptureSpread = metrics.BullBearCapture?.CaptureSpread ?? 0m;
                            item.StarRating = metrics.DueDiligence?.StarRating ?? "★★★☆☆";

                            // Phase 17 机构 4433 严选与阿尔法持续性评级
                            var check4433 = QuantCalculator.Evaluate4433Rule(dummyFund);
                            item.Passed4433 = check4433.Passed4433;

                            var alphaPersist = QuantCalculator.CalculateAlphaPersistence(dummyFund, navs, null);
                            item.AlphaPersistenceScore = alphaPersist.PersistenceScore;
                            item.AlphaPersistenceGrade = alphaPersist.PersistenceRating.Split(' ')[^1];
                        }
                    }
                }

                return list;
            }
        }, ct);
    }

    /// <summary>
    /// 获取全市场公募基金目录总条目数
    /// </summary>
    public async Task<int> GetMarketCatalogCountAsync(CancellationToken ct = default)
    {
        Initialize();
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM market_catalog;";
                var val = cmd.ExecuteScalar();
                return Convert.ToInt32(val);
            }
        }, ct);
    }

    /// <summary>
    /// 批量保存全市场基金目录数据 (采用高速 Appender 与事务写入)
    /// </summary>
    public async Task SaveMarketCatalogAsync(IReadOnlyList<MarketCatalogItem> items, CancellationToken ct = default)
    {
        if (items == null || items.Count == 0) return;
        Initialize();

        await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var trans = connection.BeginTransaction();
                try
                {
                    using (var delCmd = connection.CreateCommand())
                    {
                        delCmd.Transaction = trans;
                        delCmd.CommandText = "DELETE FROM market_catalog;";
                        delCmd.ExecuteNonQuery();
                    }

                    using var appender = connection.CreateAppender("market_catalog");
                    foreach (var item in items)
                    {
                        ct.ThrowIfCancellationRequested();
                        var row = appender.CreateRow();
                        row.AppendValue(item.Code);
                        row.AppendValue(item.Pinyin);
                        row.AppendValue(item.Name);
                        row.AppendValue(item.Type);
                        row.AppendValue(item.PinyinFull);
                        row.AppendValue(item.UpdatedAt);
                        row.EndRow();
                    }
                    appender.Close();

                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
        }, ct);
    }

    /// <summary>
    /// 高性能检索全市场公募基金目录 (支持按代码、全拼、简拼、名称混合搜索，支持品类及板块过滤)
    /// </summary>
    public Task<List<MarketCatalogItem>> SearchMarketCatalogAsync(
        string keyword,
        string typeFilter,
        int limit,
        CancellationToken ct = default) => SearchMarketCatalogAsync(keyword, typeFilter, "全部", limit, ct);

    public async Task<List<MarketCatalogItem>> SearchMarketCatalogAsync(
        string keyword = "",
        string typeFilter = "全部",
        string sectorFilter = "全部",
        int limit = 300,
        CancellationToken ct = default)
    {
        Initialize();
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var list = new List<MarketCatalogItem>();
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                string sql = "SELECT code, pinyin, name, type, pinyin_full, updated_at FROM market_catalog WHERE 1=1 ";

                keyword = keyword.Trim();
                if (!string.IsNullOrEmpty(keyword))
                {
                    string safeKw = keyword.Replace("'", "''");
                    string safeUpper = keyword.ToUpper().Replace("'", "''");
                    sql += $" AND (code LIKE '%{safeKw}%' OR name LIKE '%{safeKw}%' OR pinyin LIKE '%{safeUpper}%' OR pinyin_full LIKE '%{safeUpper}%') ";
                }

                if (!string.IsNullOrEmpty(typeFilter) && typeFilter != "全部" && typeFilter != "ALL")
                {
                    string safeType = typeFilter.Replace("'", "''");
                    sql += $" AND type LIKE '%{safeType}%' ";
                }

                if (!string.IsNullOrEmpty(sectorFilter) && sectorFilter != "全部" && sectorFilter != "ALL" && sectorFilter != FundSectorHelper.SectorAll)
                {
                    var kws = FundSectorHelper.GetKeywordsForSector(sectorFilter);
                    if (kws.Count > 0)
                    {
                        var conditions = kws.Select(k => $"name LIKE '%{k.Replace("'", "''")}%'");
                        sql += $" AND ({string.Join(" OR ", conditions)}) ";
                    }
                }

                sql += $" ORDER BY code ASC LIMIT {limit};";
                cmd.CommandText = sql;

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new MarketCatalogItem
                    {
                        Code = reader.GetString(0),
                        Pinyin = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Name = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Type = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        PinyinFull = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        UpdatedAt = reader.IsDBNull(5) ? DateTime.MinValue : reader.GetDateTime(5)
                    });
                }

                return list;
            }
        }, ct);
    }

    /// <summary>
    /// 独立批量保存基金前十大重仓股持仓数据
    /// </summary>
    public async Task SaveFundHoldingsAsync(string fundCode, List<FundStockHolding> holdings, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fundCode) || holdings == null) return;
        Initialize();

        await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var trans = connection.BeginTransaction();
                try
                {
                    using (var delCmd = connection.CreateCommand())
                    {
                        delCmd.Transaction = trans;
                        delCmd.CommandText = "DELETE FROM fund_holdings WHERE fund_code = $1;";
                        delCmd.Parameters.Add(new DuckDBParameter(fundCode));
                        delCmd.ExecuteNonQuery();
                    }

                    if (holdings.Count > 0)
                    {
                        using var appender = connection.CreateAppender("fund_holdings");
                        foreach (var hold in holdings)
                        {
                            var row = appender.CreateRow();
                            row.AppendValue(fundCode);
                            row.AppendValue(hold.StockCode ?? "");
                            row.AppendValue(hold.StockName ?? "");
                            row.AppendValue((double)hold.WeightPercent);
                            row.AppendValue(hold.ShareChange ?? "");
                            row.AppendValue(hold.ReportDate ?? "");
                            row.AppendValue(hold.Industry ?? "");
                            row.EndRow();
                        }
                    }

                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
        }, ct);
    }

    /// <summary>
    /// 读取指定基金的前十大重仓股持仓明细
    /// </summary>
    public async Task<List<FundStockHolding>> GetFundHoldingsAsync(string fundCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fundCode)) return new List<FundStockHolding>();
        Initialize();

        return await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                var list = new List<FundStockHolding>();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT stock_code, stock_name, weight_percent, share_change, report_date, industry
                    FROM fund_holdings
                    WHERE fund_code = $1
                    ORDER BY weight_percent DESC;
                ";
                cmd.Parameters.Add(new DuckDBParameter(fundCode));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new FundStockHolding
                    {
                        StockCode = reader.IsDBNull(0) ? "" : reader.GetString(0),
                        StockName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        WeightPercent = reader.IsDBNull(2) ? 0m : (decimal)reader.GetDouble(2),
                        ShareChange = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        ReportDate = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Industry = reader.IsDBNull(5) ? "" : reader.GetString(5)
                    });
                }

                return list;
            }
        }, ct);
    }

    /// <summary>
    /// 独立批量保存基金大类资产配置历史
    /// </summary>
    public async Task SaveFundAssetAllocationsAsync(string fundCode, List<FundAssetAllocation> allocations, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fundCode) || allocations == null) return;
        Initialize();

        await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var trans = connection.BeginTransaction();
                try
                {
                    using (var delCmd = connection.CreateCommand())
                    {
                        delCmd.Transaction = trans;
                        delCmd.CommandText = "DELETE FROM fund_asset_allocations WHERE fund_code = $1;";
                        delCmd.Parameters.Add(new DuckDBParameter(fundCode));
                        delCmd.ExecuteNonQuery();
                    }

                    if (allocations.Count > 0)
                    {
                        using var appender = connection.CreateAppender("fund_asset_allocations");
                        foreach (var alloc in allocations)
                        {
                            var row = appender.CreateRow();
                            row.AppendValue(fundCode);
                            row.AppendValue(alloc.ReportDate ?? "");
                            row.AppendValue((double)alloc.StockRatio);
                            row.AppendValue((double)alloc.BondRatio);
                            row.AppendValue((double)alloc.CashRatio);
                            row.AppendValue((double)alloc.OtherRatio);
                            row.AppendValue((double)alloc.NetAsset);
                            row.EndRow();
                        }
                    }

                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
        }, ct);
    }

    /// <summary>
    /// 读取指定基金的大类资产配置历史
    /// </summary>
    public async Task<List<FundAssetAllocation>> GetFundAssetAllocationsAsync(string fundCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fundCode)) return new List<FundAssetAllocation>();
        Initialize();

        return await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                var list = new List<FundAssetAllocation>();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT report_date, stock_ratio, bond_ratio, cash_ratio, other_ratio, net_asset
                    FROM fund_asset_allocations
                    WHERE fund_code = $1
                    ORDER BY report_date DESC;
                ";
                cmd.Parameters.Add(new DuckDBParameter(fundCode));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new FundAssetAllocation
                    {
                        ReportDate = reader.IsDBNull(0) ? "" : reader.GetString(0),
                        StockRatio = reader.IsDBNull(1) ? 0m : (decimal)reader.GetDouble(1),
                        BondRatio = reader.IsDBNull(2) ? 0m : (decimal)reader.GetDouble(2),
                        CashRatio = reader.IsDBNull(3) ? 0m : (decimal)reader.GetDouble(3),
                        OtherRatio = reader.IsDBNull(4) ? 0m : (decimal)reader.GetDouble(4),
                        NetAsset = reader.IsDBNull(5) ? 0m : (decimal)reader.GetDouble(5)
                    });
                }

                return list;
            }
        }, ct);
    }

    #region AI 模拟操盘持久化服务 (Phase: AI Paper Trading)

    /// <summary>
    /// 读取模拟投资账户信息及当前持仓列表 (若不存在则自动初始化 10 万元账户)
    /// </summary>
    public async Task<SimulatedAccount> GetSimulatedAccountAsync(string accountId = "default_ai_account", CancellationToken ct = default)
    {
        Initialize();
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                var account = new SimulatedAccount { AccountId = accountId };

                // 1. 查询账户主信息
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT account_name, initial_cash, cash, created_at, updated_at FROM sim_account WHERE account_id = $1;";
                    cmd.Parameters.Add(new DuckDBParameter(accountId));
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        account.AccountName = reader.IsDBNull(0) ? "AI 智能量化模拟盘" : reader.GetString(0);
                        account.InitialCash = reader.IsDBNull(1) ? 100000m : (decimal)reader.GetDouble(1);
                        account.Cash = reader.IsDBNull(2) ? 100000m : (decimal)reader.GetDouble(2);
                        account.CreatedAt = reader.IsDBNull(3) ? DateTime.Now : reader.GetDateTime(3);
                        account.UpdatedAt = reader.IsDBNull(4) ? DateTime.Now : reader.GetDateTime(4);
                    }
                    else
                    {
                        // 首次运行，插入默认 10 万账户
                        using var insCmd = connection.CreateCommand();
                        insCmd.CommandText = @"
                            INSERT INTO sim_account (account_id, account_name, initial_cash, cash, created_at, updated_at)
                            VALUES ($1, $2, 100000.0, 100000.0, current_timestamp, current_timestamp);
                        ";
                        insCmd.Parameters.Add(new DuckDBParameter(accountId));
                        insCmd.Parameters.Add(new DuckDBParameter(account.AccountName));
                        insCmd.ExecuteNonQuery();
                    }
                }

                // 2. 查询持仓明细
                using (var posCmd = connection.CreateCommand())
                {
                    posCmd.CommandText = @"
                        SELECT fund_code, fund_name, fund_type, shares, cost_basis, total_cost, latest_nav, nav_date, ai_signal, ai_score, ai_recommendation, updated_at
                        FROM sim_positions
                        WHERE account_id = $1 AND shares > 0.0001
                        ORDER BY total_cost DESC;
                    ";
                    posCmd.Parameters.Add(new DuckDBParameter(accountId));
                    using var reader = posCmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var pos = new SimulatedPosition
                        {
                            AccountId = accountId,
                            FundCode = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            FundName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            FundType = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Shares = reader.IsDBNull(3) ? 0m : (decimal)reader.GetDouble(3),
                            CostBasis = reader.IsDBNull(4) ? 0m : (decimal)reader.GetDouble(4),
                            TotalCost = reader.IsDBNull(5) ? 0m : (decimal)reader.GetDouble(5),
                            LatestNav = reader.IsDBNull(6) ? 0m : (decimal)reader.GetDouble(6),
                            NavDate = reader.IsDBNull(7) ? "" : reader.GetString(7),
                            AiSignal = reader.IsDBNull(8) ? "🛡️ 继续持有" : reader.GetString(8),
                            AiScore = reader.IsDBNull(9) ? 75m : (decimal)reader.GetDouble(9),
                            AiRecommendation = reader.IsDBNull(10) ? "" : reader.GetString(10),
                            UpdatedAt = reader.IsDBNull(11) ? DateTime.Now : reader.GetDateTime(11)
                        };
                        pos.MarketValue = pos.Shares * (pos.LatestNav > 0 ? pos.LatestNav : pos.CostBasis);
                        pos.FloatingProfit = pos.MarketValue - pos.TotalCost;
                        pos.FloatingProfitRate = pos.TotalCost > 0 ? (pos.FloatingProfit / pos.TotalCost) * 100m : 0m;
                        account.Positions.Add(pos);
                    }
                }

                // 3. 计算账户总体估值
                account.TotalMarketValue = account.Positions.Sum(p => p.MarketValue);
                account.TotalAsset = account.Cash + account.TotalMarketValue;
                account.TotalProfit = account.TotalAsset - account.InitialCash;
                account.TotalProfitRate = account.InitialCash > 0 ? (account.TotalProfit / account.InitialCash) * 100m : 0m;
                account.PositionRatio = account.TotalAsset > 0 ? (account.TotalMarketValue / account.TotalAsset) * 100m : 0m;

                foreach (var pos in account.Positions)
                {
                    pos.WeightPercent = account.TotalAsset > 0 ? (pos.MarketValue / account.TotalAsset) * 100m : 0m;
                }

                return account;
            }
        }, ct);
    }

    /// <summary>
    /// 保存/更新模拟账户资金状态与当前持仓
    /// </summary>
    public async Task SaveSimulatedAccountAsync(SimulatedAccount account, CancellationToken ct = default)
    {
        if (account == null) return;
        Initialize();

        await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();
                using var trans = connection.BeginTransaction();
                try
                {
                    // 1. 更新账户主表
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        cmd.CommandText = @"
                            INSERT OR REPLACE INTO sim_account (account_id, account_name, initial_cash, cash, created_at, updated_at)
                            VALUES ($1, $2, $3, $4, $5, current_timestamp);
                        ";
                        cmd.Parameters.Add(new DuckDBParameter(account.AccountId));
                        cmd.Parameters.Add(new DuckDBParameter(account.AccountName));
                        cmd.Parameters.Add(new DuckDBParameter((double)account.InitialCash));
                        cmd.Parameters.Add(new DuckDBParameter((double)account.Cash));
                        cmd.Parameters.Add(new DuckDBParameter(account.CreatedAt));
                        cmd.ExecuteNonQuery();
                    }

                    // 2. 清理并重新保存持仓
                    using (var delCmd = connection.CreateCommand())
                    {
                        delCmd.Transaction = trans;
                        delCmd.CommandText = "DELETE FROM sim_positions WHERE account_id = $1;";
                        delCmd.Parameters.Add(new DuckDBParameter(account.AccountId));
                        delCmd.ExecuteNonQuery();
                    }

                    foreach (var pos in account.Positions.Where(p => p.Shares > 0.0001m))
                    {
                        using var insCmd = connection.CreateCommand();
                        insCmd.Transaction = trans;
                        insCmd.CommandText = @"
                            INSERT INTO sim_positions (account_id, fund_code, fund_name, fund_type, shares, cost_basis, total_cost, latest_nav, nav_date, ai_signal, ai_score, ai_recommendation, updated_at)
                            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, current_timestamp);
                        ";
                        insCmd.Parameters.Add(new DuckDBParameter(account.AccountId));
                        insCmd.Parameters.Add(new DuckDBParameter(pos.FundCode));
                        insCmd.Parameters.Add(new DuckDBParameter(pos.FundName));
                        insCmd.Parameters.Add(new DuckDBParameter(pos.FundType));
                        insCmd.Parameters.Add(new DuckDBParameter((double)pos.Shares));
                        insCmd.Parameters.Add(new DuckDBParameter((double)pos.CostBasis));
                        insCmd.Parameters.Add(new DuckDBParameter((double)pos.TotalCost));
                        insCmd.Parameters.Add(new DuckDBParameter((double)pos.LatestNav));
                        insCmd.Parameters.Add(new DuckDBParameter(pos.NavDate ?? ""));
                        insCmd.Parameters.Add(new DuckDBParameter(pos.AiSignal ?? ""));
                        insCmd.Parameters.Add(new DuckDBParameter((double)pos.AiScore));
                        insCmd.Parameters.Add(new DuckDBParameter(pos.AiRecommendation ?? ""));
                        insCmd.ExecuteNonQuery();
                    }

                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
        }, ct);
    }

    /// <summary>
    /// 记录一笔模拟操盘交易流水
    /// </summary>
    public async Task SaveSimulatedTradeAsync(SimulatedTrade trade, CancellationToken ct = default)
    {
        if (trade == null) return;
        Initialize();

        await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO sim_trades (
                        trade_id, account_id, trade_time, fund_code, fund_name, action_type, action_text,
                        nav, shares, amount, fee, realized_profit, realized_profit_rate, reason
                    ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14);
                ";
                cmd.Parameters.Add(new DuckDBParameter(trade.TradeId));
                cmd.Parameters.Add(new DuckDBParameter(trade.AccountId));
                cmd.Parameters.Add(new DuckDBParameter(trade.TradeTime));
                cmd.Parameters.Add(new DuckDBParameter(trade.FundCode));
                cmd.Parameters.Add(new DuckDBParameter(trade.FundName));
                cmd.Parameters.Add(new DuckDBParameter(trade.Action.ToString()));
                cmd.Parameters.Add(new DuckDBParameter(trade.ActionText));
                cmd.Parameters.Add(new DuckDBParameter((double)trade.Nav));
                cmd.Parameters.Add(new DuckDBParameter((double)trade.Shares));
                cmd.Parameters.Add(new DuckDBParameter((double)trade.Amount));
                cmd.Parameters.Add(new DuckDBParameter((double)trade.Fee));
                cmd.Parameters.Add(new DuckDBParameter((double)trade.RealizedProfit));
                cmd.Parameters.Add(new DuckDBParameter((double)trade.RealizedProfitRate));
                cmd.Parameters.Add(new DuckDBParameter(trade.Reason ?? ""));
                cmd.ExecuteNonQuery();
            }
        }, ct);
    }

    /// <summary>
    /// 获取账户的所有历史交易流水 (按时间倒序排列)
    /// </summary>
    public async Task<List<SimulatedTrade>> GetSimulatedTradesAsync(string accountId = "default_ai_account", CancellationToken ct = default)
    {
        Initialize();
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();

                var list = new List<SimulatedTrade>();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT trade_id, account_id, trade_time, fund_code, fund_name, action_type, action_text,
                           nav, shares, amount, fee, realized_profit, realized_profit_rate, reason
                    FROM sim_trades
                    WHERE account_id = $1
                    ORDER BY trade_time DESC;
                ";
                cmd.Parameters.Add(new DuckDBParameter(accountId));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string actionStr = reader.IsDBNull(5) ? "ManualBuy" : reader.GetString(5);
                    _ = Enum.TryParse<SimulatedTradeAction>(actionStr, out var action);

                    list.Add(new SimulatedTrade
                    {
                        TradeId = reader.IsDBNull(0) ? "" : reader.GetString(0),
                        AccountId = reader.IsDBNull(1) ? accountId : reader.GetString(1),
                        TradeTime = reader.IsDBNull(2) ? DateTime.Now : reader.GetDateTime(2),
                        FundCode = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        FundName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Action = action,
                        ActionText = reader.IsDBNull(6) ? "🛒 手动买入" : reader.GetString(6),
                        Nav = reader.IsDBNull(7) ? 0m : (decimal)reader.GetDouble(7),
                        Shares = reader.IsDBNull(8) ? 0m : (decimal)reader.GetDouble(8),
                        Amount = reader.IsDBNull(9) ? 0m : (decimal)reader.GetDouble(9),
                        Fee = reader.IsDBNull(10) ? 0m : (decimal)reader.GetDouble(10),
                        RealizedProfit = reader.IsDBNull(11) ? 0m : (decimal)reader.GetDouble(11),
                        RealizedProfitRate = reader.IsDBNull(12) ? 0m : (decimal)reader.GetDouble(12),
                        Reason = reader.IsDBNull(13) ? "" : reader.GetString(13)
                    });
                }

                return list;
            }
        }, ct);
    }

    /// <summary>
    /// 重置模拟账户 (清空交易记录、清空持仓、将现金恢复至指定本金如 10 万元)
    /// </summary>
    public async Task ResetSimulatedAccountAsync(string accountId = "default_ai_account", decimal initialCash = 100000m, CancellationToken ct = default)
    {
        Initialize();
        await Task.Run(() =>
        {
            lock (_lock)
            {
                using var connection = new DuckDBConnection(_connectionString);
                connection.Open();
                using var trans = connection.BeginTransaction();
                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        cmd.CommandText = "DELETE FROM sim_trades WHERE account_id = $1;";
                        cmd.Parameters.Add(new DuckDBParameter(accountId));
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        cmd.CommandText = "DELETE FROM sim_positions WHERE account_id = $1;";
                        cmd.Parameters.Add(new DuckDBParameter(accountId));
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        cmd.CommandText = @"
                            INSERT OR REPLACE INTO sim_account (account_id, account_name, initial_cash, cash, created_at, updated_at)
                            VALUES ($1, 'AI 智能量化模拟盘 (10万实操金)', $2, $2, current_timestamp, current_timestamp);
                        ";
                        cmd.Parameters.Add(new DuckDBParameter(accountId));
                        cmd.Parameters.Add(new DuckDBParameter((double)initialCash));
                        cmd.ExecuteNonQuery();
                    }

                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
        }, ct);
    }

    #endregion
}
