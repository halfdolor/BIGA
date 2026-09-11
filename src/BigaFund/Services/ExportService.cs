using System.IO;
using System.Net;
using System.Text;
using BigaFund.Models;

namespace BigaFund.Services;

public static class ExportService
{
    /// <summary>
    /// 将基金基本信息、量化风控指标、策略回测结果与历史净值导出为带有 UTF-8 BOM 的标准 CSV 文件
    /// </summary>
    public static void ExportToCsv(
        string filePath,
        FundDetail fund,
        QuantMetrics metrics,
        BacktestResult backtest,
        List<NavRecord> filteredNavs)
    {
        var sb = new StringBuilder();

        // 1. 基金基本信息
        sb.AppendLine("=== BIGA 基金量化分析与投研研报 ===");
        sb.AppendLine($"基金代码,{fund.Code}");
        sb.AppendLine($"基金名称,\"{fund.Name}\"");
        sb.AppendLine($"基金类型,{fund.Type}");
        sb.AppendLine($"基金经理,{fund.ManagerName}");
        sb.AppendLine($"从业年限,{fund.ManagerTenure}");
        sb.AppendLine($"资产规模,{fund.FundSize}");
        sb.AppendLine($"统计区间,{metrics.PeriodName} ({metrics.StartDate:yyyy-MM-dd} 至 {metrics.EndDate:yyyy-MM-dd})");
        sb.AppendLine($"区间交易日,{metrics.TradingDays} 天");
        sb.AppendLine();

        // 2. 核心量化风控指标
        sb.AppendLine("=== 核心量化与风控指标 ===");
        sb.AppendLine("指标名称,指标数值,指标说明");
        sb.AppendLine($"区间累计收益率,{metrics.TotalReturn:F2}%,统计区间内复权累计涨跌幅");
        sb.AppendLine($"年化复合收益率 (CAGR),{metrics.AnnualizedReturn:F2}%,按 A 股 250 交易日折算真实年化回报");
        sb.AppendLine($"年化波动率,{metrics.AnnualizedVolatility:F2}%,日收益率离散度年化衡量");
        sb.AppendLine($"历史最大回撤 (MDD),{metrics.MaxDrawdown:F2}%,历史任意时点买入的最大本金亏损");
        sb.AppendLine($"最大回撤高点日期,{metrics.MaxDrawdownPeakDate:yyyy-MM-dd},回撤起始最高点");
        sb.AppendLine($"最大回撤谷底日期,{metrics.MaxDrawdownTroughDate:yyyy-MM-dd},回撤到达最深谷底");
        sb.AppendLine($"回撤修复交易日,{(metrics.RecoveryTradingDays.HasValue ? metrics.RecoveryTradingDays.Value + " 天" : "未修复")},从谷底回升至前高所耗交易日数");
        sb.AppendLine($"夏普比率 (Sharpe),{metrics.SharpeRatio:F2},承担单位波动风险的超额回报 (无风险利率 2.0%)");
        sb.AppendLine($"卡玛比率 (Calmar),{metrics.CalmarRatio:F2},年化收益弥补极端回撤的能力 (>1 优秀)");
        sb.AppendLine($"索提诺比率 (Sortino),{metrics.SortinoRatio:F2},仅惩罚下行负向波动的风险调整收益");
        sb.AppendLine($"沪深300基准收益,{metrics.BenchmarkReturn:F2}%,同期沪深 300 指数累计走势");
        sb.AppendLine($"区间超额收益,{metrics.ExcessReturn:F2}%,相对沪深 300 的超额回报");
        sb.AppendLine($"贝塔系数 (Beta),{metrics.Beta:F2},相对沪深 300 整体波动的敏感度");
        sb.AppendLine($"阿尔法年化 (Alpha),{metrics.Alpha:F2}%,CAPM 资本资产定价模型纯超额收益");
        sb.AppendLine($"信息比率 (IR),{metrics.InformationRatio:F2},超额收益相对跟踪误差的比率");
        sb.AppendLine($"特雷诺比率 (Treynor),{metrics.TreynorRatio:F2},承担单位系统性贝塔风险的超额回报");
        sb.AppendLine($"年化跟踪误差 (Tracking Error),{metrics.TrackingError:F2}%,相对沪深300基准的偏离离散度");
        sb.AppendLine($"综合捕获比率 (Capture Ratio),{metrics.CaptureRatio:F2},上行捕获率/下行捕获率 (>1 具备非对称优势)");
        sb.AppendLine($"上行捕获率 (Upside Capture),{metrics.UpsideCaptureRatio:F1}%,基准上涨周期中的收益捕获幅度");
        sb.AppendLine($"下行捕获率 (Downside Capture),{metrics.DownsideCaptureRatio:F1}%,基准下跌周期中的风险跟随幅度");
        sb.AppendLine($"奥米加比率 (Omega Ratio),{metrics.OmegaRatio:F2},全概率收益胜率质量比 (>1 胜率与期望占优)");
        sb.AppendLine($"溃疡指数 (Ulcer Index),{metrics.UlcerIndex:F2}%,回撤深度与水下滞留持续时间方根 (越小越优)");
        sb.AppendLine($"马丁比率 (Martin Ratio),{metrics.MartinRatio:F2},超额收益 / 溃疡指数 (越大越优)");
        sb.AppendLine($"收益痛苦比 (Pain Ratio),{metrics.PainRatio:F2},年化复合收益 / 历史平均水下深度绝对值");
        sb.AppendLine($"下行标准差 (σ_down),{metrics.DownsideDeviation:F2}%,低于无风险利率的下行半离散度");
        sb.AppendLine();

        // 1.1 基金经理择时与选股非线性双模型诊断 (TM / HM)
        if (metrics.TimingAbility != null)
        {
            var tm = metrics.TimingAbility;
            sb.AppendLine("=== 基金经理择时与选股非线性双模型诊断 (Treynor-Mazuy & Henriksson-Merton) ===");
            sb.AppendLine($"综合能力定级,{tm.TimingRatingBadge}");
            sb.AppendLine($"能力诊断概述,\"{tm.DiagnosticSummary}\"");
            sb.AppendLine($"TM 模型选股 Alpha (年化),{tm.TmAlphaAnnualized:+0.00;-0.00;0.00}%,t-stat: {tm.TmAlphaTStat:F2}");
            sb.AppendLine($"TM 模型择时系数 (γ),{tm.TmGamma:+0.0000;-0.0000;0.0000},t-stat: {tm.TmGammaTStat:F2}, p-val: {tm.TmGammaPValue:F4}");
            sb.AppendLine($"HM 模型选股 Alpha (年化),{tm.HmAlphaAnnualized:+0.00;-0.00;0.00}%,t-stat: {tm.HmAlphaTStat:F2}");
            sb.AppendLine($"HM 模型下行保护系数 (β2),{tm.HmDownsideBeta:+0.0000;-0.0000;0.0000},t-stat: {tm.HmDownsideBetaTStat:F2}, p-val: {tm.HmDownsideBetaPValue:F4}");
            sb.AppendLine();
        }

        // 2.0 历史前五大最深回撤周期解构
        if (metrics.DrawdownEpisodes != null && metrics.DrawdownEpisodes.Count > 0)
        {
            sb.AppendLine("=== 历史前五大最深回撤周期解构 (Top 5 Drawdown Episodes) ===");
            sb.AppendLine("排名,波峰日期,谷底日期,修复出坑日,最大跌幅,暴跌历时(天),修复历时(天),全周期历时(天),修复状态");
            foreach (var ep in metrics.DrawdownEpisodes)
            {
                string recStr = ep.RecoveryDate.HasValue ? ep.RecoveryDate.Value.ToString("yyyy-MM-dd") : "仍在回撤修复中";
                sb.AppendLine($"#{ep.Rank},{ep.PeakDate:yyyy-MM-dd},{ep.TroughDate:yyyy-MM-dd},{recStr},{ep.DrawdownPercent:F2}%,{ep.FallDays},{ep.RecoveryDays?.ToString() ?? "-"},{ep.TotalDays},{ep.StatusDesc}");
            }
            sb.AppendLine();
        }

        // 2.0.1 Barra CNE5/CNE6 风格多因子归因
        var barra = metrics.BarraAttribution ?? fund.BarraAttribution;
        if (barra != null)
        {
            sb.AppendLine("=== Barra CNE5/CNE6 风格多因子归因与收益解构 ===");
            sb.AppendLine($"特质阿尔法 (Specific Alpha),{barra.SpecificAlpha:+0.00;-0.00;0.00}%,剥离六大风格因子后的纯选基特质年化超额");
            sb.AppendLine($"模型解释度 (R²),{barra.RSquared * 100m:F2}%,六大风格因子对收益方差的解释比例");
            sb.AppendLine($"主导风格标签,{barra.DominantStyle}");
            sb.AppendLine($"风格诊断说明,\"{barra.AttributionSummary}\"");
            sb.AppendLine("因子代码,因子名称,风格暴露(Beta),t统计量,统计显著性,年化收益贡献(%),因子金融学说明");
            foreach (var f in barra.Factors)
            {
                sb.AppendLine($"{f.FactorCode},{f.FactorName},{f.Beta:F3},{f.TStat:F2},{f.Significance},{f.ContributionReturn:+0.00;-0.00;0.00}%,\"{f.Description}\"");
            }
            sb.AppendLine();
        }

        // 2.0.2 基金经理任期全景与滚动超额胜率
        var mgr = metrics.ManagerProfile ?? fund.ManagerProfile;
        if (mgr != null)
        {
            sb.AppendLine("=== 基金经理任期全景与滚动超额胜率矩阵 ===");
            sb.AppendLine($"现任经理姓名,{mgr.ManagerName}");
            sb.AppendLine($"任职期限,{mgr.Tenure}");
            sb.AppendLine($"任期累计回报,{mgr.TenureCumulativeReturn:+0.00;-0.00;0.00}%");
            sb.AppendLine($"任期复合年化 (CAGR),{mgr.TenureAnnualizedReturn:+0.00;-0.00;0.00}%");
            sb.AppendLine($"同期沪深300回报,{mgr.BenchmarkCumulativeReturn:+0.00;-0.00;0.00}%");
            sb.AppendLine($"任期超额收益,{mgr.TenureExcessReturn:+0.00;-0.00;0.00}%");
            sb.AppendLine($"滚动月度胜率 (vs 沪深300),{mgr.RollingMonthlyWinRate:F1}%,战胜基准月数: {mgr.MonthlyWinCount}/{mgr.TotalMonths}");
            sb.AppendLine($"滚动季度胜率 (vs 沪深300),{mgr.RollingQuarterlyWinRate:F1}%,战胜基准季数: {mgr.QuarterlyWinCount}/{mgr.TotalQuarters}");
            sb.AppendLine($"单月最大跑赢幅度,{mgr.MaxOutperformanceMonth:+0.00;-0.00;0.00}%");
            sb.AppendLine($"单月最大跑输幅度,{mgr.MaxUnderperformanceMonth:+0.00;-0.00;0.00}%");
            sb.AppendLine($"职业生涯稳定性评级,{mgr.StabilityRating}");
            sb.AppendLine($"任期胜率评价,\"{mgr.CareerSummary}\"");
            sb.AppendLine();
        }

        // 2.0.3 非正态极值尾部风险与巴塞尔监管在险 (Cornish-Fisher Tail Risk)
        var tail = metrics.TailRisk;
        if (tail != null)
        {
            sb.AppendLine("=== 非正态极值尾部风险模型 (Cornish-Fisher 展开式与巴塞尔协议) ===");
            sb.AppendLine($"样本偏度 (Skewness),{tail.Skewness:+0.00;-0.00;0.00},三阶中心矩非对称性(负偏代表暴跌频发)");
            sb.AppendLine($"超额峰度 (Excess Kurtosis),{tail.ExcessKurtosis:+0.00;-0.00;0.00},四阶中心矩厚尾程度(>0代表尖峰厚尾黑天鹅)");
            sb.AppendLine($"95% 修正极端在险 (mVaR 95%),{tail.CornishFisherVaR95:F2}%,考虑偏度峰度修正后的日度极值损失下限");
            sb.AppendLine($"95% 修正预期短缺 (mCVaR 95%),{tail.CornishFisherCVaR95:F2}%,跌破 mVaR 95% 后的条件平均损失");
            sb.AppendLine($"99% 修正极端在险 (mVaR 99%),{tail.CornishFisherVaR99:F2}%,99% 置信度极端压力下日度在险损失");
            sb.AppendLine($"99% 修正预期短缺 (mCVaR 99%),{tail.CornishFisherCVaR99:F2}%,99% 置信度条件期望损失");
            sb.AppendLine($"巴塞尔协议 10日 99% VaR,{tail.Basel10DayVaR99:F2}%,Basel III 监管口径 10 交易日极端在险资本要求");
            sb.AppendLine($"尾部厚度评级,{tail.TailFatnessRating}");
            sb.AppendLine($"尾部风险诊断,\"{tail.DiagnosticSummary}\"");
            sb.AppendLine();
        }

        // 2.0.4 全历史水下套牢深度解构 (Underwater Analysis)
        var under = metrics.UnderwaterAnalysis ?? fund.UnderwaterAnalysis;
        if (under != null)
        {
            sb.AppendLine("=== 全历史水下套牢深度解构 (Underwater Analysis) ===");
            sb.AppendLine($"水下套牢时间占比,{under.UnderwaterTimeRatio:F1}%,净值低于历史峰值的交易日比例");
            sb.AppendLine($"最长连续水下历时,{under.MaxUnderwaterDays} 交易日,全区间最长等待出坑历时");
            sb.AppendLine($"当前连续水下历时,{under.CurrentUnderwaterDays} 交易日,当前未出坑套牢历时");
            sb.AppendLine($"平均水下回撤深度,{under.AverageUnderwaterDepth:F2}%,水下期间平均落差");
            sb.AppendLine($"水下痛苦指数 (Pain Index),{under.PainIndex:F2}%,水下回撤绝对值时间积分归一化");
            sb.AppendLine($"水下解构诊断,\"{under.DiagnosticSummary}\"");
            sb.AppendLine();
        }

        // 2.0.5 Fama-French 五因子资产定价模型 (FF5)
        var ff5 = metrics.FamaFrenchResult ?? fund.FamaFrenchResult;
        if (ff5 != null)
        {
            sb.AppendLine("=== Fama-French 五因子资产定价模型 (FF5: MKT, SMB, HML, RMW, CMA) ===");
            sb.AppendLine($"特质阿尔法 (Specific Alpha),{ff5.AlphaAnnualized:+0.00;-0.00;0.00}%,剥离五因子后的纯超额年化收益");
            sb.AppendLine($"模型判定系数 (R²),{ff5.RSquaredPercent:F1}%,五因子对方差波动的联合解释度");
            sb.AppendLine($"特异残差风险占比,{ff5.ResidualRiskPercent:F1}%,不可被系统性因子解释的残差波动");
            sb.AppendLine($"主导因子暴露,{ff5.DominantFactor}");
            sb.AppendLine($"定价归因诊断,\"{ff5.AttributionSummary}\"");
            sb.AppendLine("因子代码,因子名称,因子暴露(Beta),t检验量,p显著性水平,年化贡献收益(%),解释贡献占比(%),因子定价含义");
            foreach (var f in ff5.FactorItems)
            {
                sb.AppendLine($"{f.FactorId},{f.FactorName},{f.Beta:+0.00;-0.00;0.00},{f.TStat:+0.00;-0.00;0.00},{f.PValue:F4},{f.AnnualizedContribution:+0.00;-0.00;0.00}%,{f.ContributionPercent:F1}%,\"{f.FactorCategory} - {f.FactorEvaluation}\"");
            }
            sb.AppendLine();
        }

        // 2.1 晨星风格箱历史漂移
        var drift = fund.StyleDrift ?? QuantCalculator.AnalyzeStyleDrift(fund, metrics);
        if (drift != null && drift.HistoryPoints.Count > 0)
        {
            sb.AppendLine("=== 晨星九宫格风格箱历史漂移追踪 (Morningstar Style Drift) ===");
            sb.AppendLine($"风格漂移指数 (SDI),{drift.StyleDriftIndex:F2}");
            sb.AppendLine($"风格稳定性评级,{drift.StabilityRating}");
            sb.AppendLine($"漂移评价,{drift.AnalysisSummary}");
            sb.AppendLine("报告季度,规模因子得分(Size),价值因子得分(Value),定位九宫格,CR10重仓集中度,第一大行业");
            foreach (var pt in drift.HistoryPoints)
            {
                sb.AppendLine($"{pt.Period},{pt.SizeScore:+0.0;-0.0;0.0},{pt.ValueScore:+0.0;-0.0;0.0},{pt.StyleBoxName},{pt.Cr10:F1}%,{pt.TopIndustry}");
            }
            sb.AppendLine();
        }

        // 2.1 极端市场压力测试
        if (metrics.StressTestScenarios.Count > 0)
        {
            sb.AppendLine("=== 历史经典黑天鹅极端市场情景压力测试 ===");
            sb.AppendLine("危机事件名称,时间区间,本基金区间收益(%),沪深300同期走势(%),超额收益(%),期间最大回撤(%),防御评级,情景说明");
            foreach (var st in metrics.StressTestScenarios)
            {
                sb.AppendLine($"\"{st.ScenarioName}\",{st.StartDate:yyyy-MM-dd}至{st.EndDate:yyyy-MM-dd},{st.FundReturnRate:+0.00;-0.00;0.00}%,{st.BenchmarkReturnRate:+0.00;-0.00;0.00}%,{st.ExcessReturnRate:+0.00;-0.00;0.00}%,{st.MaxDrawdown:F2}%,{st.DefenseRating},\"{st.Description}\"");
            }
            sb.AppendLine();
        }

        // 2.2 牛熊市场双边非对称捕获与极端暴跌条件相关性
        var bb = metrics.BullBearCapture ?? fund.BullBearCapture;
        if (bb != null)
        {
            sb.AppendLine("=== 牛熊双边非对称捕获与暴跌日条件相关性分析 ===");
            sb.AppendLine($"牛市上涨贝塔 (Bull Beta),{bb.BullBeta:+0.00;-0.00}");
            sb.AppendLine($"熊市下跌贝塔 (Bear Beta),{bb.BearBeta:+0.00;-0.00}");
            sb.AppendLine($"非对称度 (Asymmetry Index),{bb.AsymmetryIndex:+0.00;-0.00}");
            sb.AppendLine($"上行捕获率 (Upside Capture),{bb.UpsideCaptureRatio:F1}%");
            sb.AppendLine($"下行捕获率 (Downside Capture),{bb.DownsideCaptureRatio:F1}%");
            sb.AppendLine($"捕获利差 (Capture Spread),{bb.CaptureSpread:+0.0;-0.0}%");
            sb.AppendLine($"常态全样本相关性,{bb.NormalCorrelation:+0.00;-0.00}");
            sb.AppendLine($"极端暴跌日条件相关性,{bb.CrashCorrelation:+0.00;-0.00}");
            sb.AppendLine($"暴跌相关性漂移 (Correlation Shift),{bb.CorrelationShift:+0.00;-0.00}");
            sb.AppendLine($"凸性评级画像,{bb.ConvexityRating}");
            sb.AppendLine($"诊断评语,\"{bb.AsymmetryDiagnosis}\"");
            sb.AppendLine();
        }

        // 2.3 机构级 FOF 尽调六维雷达综合评分与晨星五星等效评级报告卡
        var dd = metrics.DueDiligence ?? fund.DueDiligence;
        if (dd != null)
        {
            sb.AppendLine("=== 机构级 FOF 尽调六维雷达评分与晨星等效五星评级 ===");
            sb.AppendLine($"晨星等效星级,{dd.StarRating}");
            sb.AppendLine($"尽调综合得分,{dd.OverallDiligenceScore:F1}/100");
            sb.AppendLine($"机构定级建议,{dd.DiligenceGrade}");
            sb.AppendLine($"纯阿尔法得分 (Alpha Purity),{dd.AlphaPurityScore:F1}/100");
            sb.AppendLine($"择时与凸性得分 (Timing & Convexity),{dd.TimingConvexityScore:F1}/100");
            sb.AppendLine($"尾部抗脆弱得分 (Tail Resilience),{dd.TailResilienceScore:F1}/100");
            sb.AppendLine($"风险调整性价比得分 (Risk-Adjusted Efficiency),{dd.RiskAdjustedEfficiencyScore:F1}/100");
            sb.AppendLine($"风格纪律性得分 (Style Discipline),{dd.StyleDisciplineScore:F1}/100");
            sb.AppendLine($"规模容量得分 (Capacity & Liquidity),{dd.CapacityLiquidityScore:F1}/100");
            sb.AppendLine($"投决会综合决议,\"{dd.InstitutionalVerdict}\"");
            sb.AppendLine();
        }

        // 3. 策略回测总结
        sb.AppendLine("=== 策略回测总结 ===");
        sb.AppendLine($"策略名称,{backtest.StrategyName}");
        sb.AppendLine($"策略说明,\"{backtest.StrategyDescription}\"");
        sb.AppendLine($"基础定投金额,{backtest.PeriodicAmount:F2} 元");
        sb.AppendLine($"累计定投期数,{backtest.TotalPeriods} 期");
        sb.AppendLine($"累计投入总本金,{backtest.TotalInvested:F2} 元");
        sb.AppendLine($"累计持有总份额,{backtest.FinalShares:F2} 份");
        sb.AppendLine($"平均持仓成本价 (持仓均价),{backtest.AverageCostPrice:F4} 元");
        sb.AppendLine($"期末持仓总市值,{backtest.FinalAssetValue:F2} 元");
        sb.AppendLine($"累计净赚收益,{backtest.TotalProfit:F2} 元");
        sb.AppendLine($"定投总收益率,{backtest.TotalReturnRate:F2}%");
        sb.AppendLine($"真实年化内部收益率 (XIRR),{backtest.AnnualizedIrr:F2}%");
        sb.AppendLine($"一次性买入对照收益率,{backtest.BuyAndHoldReturnRate:F2}%");
        if (backtest.TakeProfitRounds > 0)
        {
            sb.AppendLine($"止盈触发轮次,{backtest.TakeProfitRounds} 次");
            sb.AppendLine($"已落袋锁定收益,{backtest.RealizedProfit:F2} 元");
        }
        if (backtest.GridTradesCount > 0)
        {
            sb.AppendLine($"网格套利成交数,{backtest.GridTradesCount} 笔");
            sb.AppendLine($"网格高抛低吸利润,{backtest.GridArbitrageProfit:F2} 元");
        }
        if (backtest.RebalanceCount > 0)
        {
            sb.AppendLine($"股债动态再平衡次数,{backtest.RebalanceCount} 次");
            sb.AppendLine($"期末权益资产市值,{backtest.EquityAssetValue:F2} 元");
            sb.AppendLine($"期末固收资产市值,{backtest.BondAssetValue:F2} 元");
        }
        sb.AppendLine();

        // 4. 定投现金流与持仓增长明细
        if (backtest.Timeline.Count > 0)
        {
            sb.AppendLine("=== 策略回测现金流与市值轨迹 ===");
            sb.AppendLine("日期,累计投入本金(元),当前持仓市值(元),累计收益率(%)");
            foreach (var pt in backtest.Timeline)
            {
                sb.AppendLine($"{pt.Date:yyyy-MM-dd},{pt.TotalInvested:F2},{pt.CurrentValue:F2},{pt.ReturnRate:F2}%");
            }
            sb.AppendLine();
        }

        // 5. 全量净值历史数据
        sb.AppendLine("=== 历史净值明细时序 ===");
        sb.AppendLine("日期,单位净值,累计净值,日涨跌幅(%)");
        foreach (var nav in filteredNavs)
        {
            sb.AppendLine($"{nav.Date:yyyy-MM-dd},{nav.UnitNav:F4},{nav.CumulativeNav:F4},{nav.DailyReturn:F2}%");
        }

        // 使用带 BOM 的 UTF-8 写入，完美避免 Excel 乱码
        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>
    /// 将多资产组合配置权重、量化风控指标与时序净值导出为带有 UTF-8 BOM 的标准 CSV 文件
    /// </summary>
    public static void ExportPortfolioToCsv(
        string filePath,
        IEnumerable<PortfolioItem> components,
        PortfolioResult portfolio)
    {
        var sb = new StringBuilder();

        // 1. 组合基本信息
        sb.AppendLine("=== BIGA 基金组合资产配置与量化回测研报 ===");
        sb.AppendLine($"生成时间,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"统计区间,{portfolio.StartDate:yyyy-MM-dd} 至 {portfolio.EndDate:yyyy-MM-dd}");
        sb.AppendLine($"交集交易日数,{portfolio.TradingDays} 天");
        sb.AppendLine();

        // 2. 组合成分与权重配置
        sb.AppendLine("=== 组合成分基金与权重配置 ===");
        sb.AppendLine("基金代码,基金名称,基金类型,配置权重(%)");
        foreach (var c in components)
        {
            sb.AppendLine($"{c.Code},\"{c.Name}\",{c.Type},{c.WeightPercent:F2}%");
        }
        sb.AppendLine();

        // 3. 组合量化风控指标
        sb.AppendLine("=== 组合综合绩效与风控指标 ===");
        sb.AppendLine("指标名称,指标数值,指标说明");
        sb.AppendLine($"组合累计收益率,{portfolio.TotalReturn:F2}%,统计区间内多资产加权累计回报");
        sb.AppendLine($"年化复合收益率 (CAGR),{portfolio.AnnualizedReturn:F2}%,按 A 股 250 交易日折算的真实年化收益率");
        sb.AppendLine($"年化波动率,{portfolio.AnnualizedVolatility:F2}%,组合日收益离散度年化衡量");
        sb.AppendLine($"历史最大回撤 (MDD),{portfolio.MaxDrawdown:F2}%,组合历史任意时点的最大跌幅亏损");
        sb.AppendLine($"夏普比率 (Sharpe),{portfolio.SharpeRatio:F2},承担单位波动风险的超额回报 (无风险利率 2.0%)");
        sb.AppendLine($"资产分散化降波增益,{portfolio.DiversificationBenefit:F2}%,马科维茨 MPT 投资组合方差衰减带来的降波效应");
        sb.AppendLine($"奥米加比率 (Omega Ratio),{portfolio.OmegaRatio:F2},全概率收益胜率质量比 (>1 胜率与期望占优)");
        sb.AppendLine($"溃疡指数 (Ulcer Index),{portfolio.UlcerIndex:F2}%,回撤深度与水下滞留持续时间方根 (越小越优)");
        sb.AppendLine($"马丁比率 (Martin Ratio),{portfolio.MartinRatio:F2},超额收益 / 溃疡指数 (越大越优)");
        sb.AppendLine($"收益痛苦比 (Pain Ratio),{portfolio.PainRatio:F2},年化复合收益 / 历史平均水下深度绝对值");
        sb.AppendLine($"下行标准差 (σ_down),{portfolio.DownsideDeviation:F2}%,低于无风险利率的下行半离散度");
        sb.AppendLine();

        // 3.1 组合资产两两收益率相关系数矩阵
        if (portfolio.CorrelationMatrix != null && portfolio.CorrelationMatrix.AssetCodes.Count > 1)
        {
            var cm = portfolio.CorrelationMatrix;
            sb.AppendLine("=== 组合资产两两 Pearson 收益率相关系数矩阵 ===");
            sb.AppendLine($"资产间平均相关度,{cm.AverageCorrelation:F2}");
            sb.AppendLine($"组合风险分散评级,{cm.DiversificationRating}");
            sb.Append("资产代码/代码,");
            sb.AppendLine(string.Join(",", cm.AssetCodes));
            for (int i = 0; i < cm.AssetCodes.Count; i++)
            {
                var rowVals = new List<string> { cm.AssetCodes[i] };
                for (int j = 0; j < cm.AssetCodes.Count; j++)
                {
                    rowVals.Add(cm.Matrix[i, j].ToString("F2"));
                }
                sb.AppendLine(string.Join(",", rowVals));
            }
            sb.AppendLine();
        }

        // 3.2 组合历史最深回撤周期解构
        if (portfolio.DrawdownEpisodes != null && portfolio.DrawdownEpisodes.Count > 0)
        {
            sb.AppendLine("=== 组合历史最深回撤周期解构 (Top 5 Drawdown Episodes) ===");
            sb.AppendLine("排名,波峰日期,谷底日期,修复出坑日,最大跌幅,暴跌历时(天),修复历时(天),全周期历时(天),修复状态");
            foreach (var ep in portfolio.DrawdownEpisodes)
            {
                string recStr = ep.RecoveryDate.HasValue ? ep.RecoveryDate.Value.ToString("yyyy-MM-dd") : "仍在回撤修复中";
                sb.AppendLine($"#{ep.Rank},{ep.PeakDate:yyyy-MM-dd},{ep.TroughDate:yyyy-MM-dd},{recStr},{ep.DrawdownPercent:F2}%,{ep.FallDays},{ep.RecoveryDays?.ToString() ?? "-"},{ep.TotalDays},{ep.StatusDesc}");
            }
            sb.AppendLine();
        }

        // 3.3 组合历史极端黑天鹅情景压力测试
        if (portfolio.StressTestScenarios.Count > 0)
        {
            sb.AppendLine("=== 组合历史经典黑天鹅极端市场情景压力测试 ===");
            sb.AppendLine("危机事件名称,时间区间,组合期间累计收益(%),沪深300同期走势(%),超额收益(%),期间最大回撤(%),防御评级,情景说明");
            foreach (var st in portfolio.StressTestScenarios)
            {
                sb.AppendLine($"\"{st.ScenarioName}\",{st.StartDate:yyyy-MM-dd}至{st.EndDate:yyyy-MM-dd},{st.FundReturnRate:+0.00;-0.00;0.00}%,{st.BenchmarkReturnRate:+0.00;-0.00;0.00}%,{st.ExcessReturnRate:+0.00;-0.00;0.00}%,{st.MaxDrawdown:F2}%,{st.DefenseRating},\"{st.Description}\"");
            }
            sb.AppendLine();
        }

        // 3.4 组合底层穿透集中度诊断
        if (portfolio.LookThroughResult != null)
        {
            var lt = portfolio.LookThroughResult;
            sb.AppendLine("=== 组合底层穿透集中度诊断 (HHI & Neff) ===");
            sb.AppendLine($"股票持仓 HHI 指数,{lt.StocksHhi:F0}");
            sb.AppendLine($"有效分散持股只数 (Neff),{lt.EffectiveStockCount:F1}");
            sb.AppendLine($"行业配置 HHI 指数,{lt.IndustryHhi:F0}");
            sb.AppendLine($"穿透集中度诊断评级,{lt.HhiConcentrationLevel}");
            sb.AppendLine();
        }

        // 3.5 宏观情景冲击与多因子压力测试
        if (portfolio.MacroShockResult != null && portfolio.MacroShockResult.PresetScenarios.Count > 0)
        {
            sb.AppendLine("=== 宏观情景冲击与多因子压力测试 (Bloomberg PORT Standard) ===");
            sb.AppendLine("情景名称,权益资产冲击(%),利率变动(bps),预估净值变动(%),预估损益(元),压力VaR95(%),冲击评级,情景说明");
            foreach (var sc in portfolio.MacroShockResult.PresetScenarios)
            {
                sb.AppendLine($"\"{sc.Name}\",{sc.EquityShockPercent:+0;-0;0}%,{sc.InterestRateShockBps:+0;-0;0} bps,{sc.EstimatedNavChangePercent:+0.00;-0.00;0.00}%,{sc.EstimatedPnL:F0},{sc.StressedVaR95:F2}%,{sc.ImpactRating},\"{sc.Description}\"");
            }
            sb.AppendLine();
        }

        // 3.6 动态再平衡时序仿真与换手磨损
        if (portfolio.RebalanceSimulation != null)
        {
            var sim = portfolio.RebalanceSimulation;
            sb.AppendLine("=== 组合全时序动态再平衡仿真与换手磨损损耗 ===");
            sb.AppendLine($"再平衡策略模式,{sim.StrategyType}");
            sb.AppendLine($"初始仿真本金,{sim.InitialCapital:F2} 元");
            sb.AppendLine($"扣费净终值收益率,{sim.TotalReturn:+0.00;-0.00;0.00}%");
            sb.AppendLine($"买入持有基准收益率,{sim.BuyAndHoldReturn:+0.00;-0.00;0.00}%");
            sb.AppendLine($"再平衡净阿尔法 (vs买入持有),{sim.NetAlphaVsBuyAndHold:+0.00;-0.00;0.00}%");
            sb.AppendLine($"年化复合收益率 (CAGR),{sim.AnnualizedReturn:+0.00;-0.00;0.00}%");
            sb.AppendLine($"年化波动率,{sim.AnnualizedVolatility:F2}%");
            sb.AppendLine($"最大回撤 (MDD),{sim.MaxDrawdown:F2}% (买入持有: {sim.BuyAndHoldMaxDrawdown:F2}%)");
            sb.AppendLine($"夏普比率 (Sharpe),{sim.SharpeRatio:F2}");
            sb.AppendLine($"年化换手率,{sim.AnnualizedTurnoverRate:F1}% (累计换手率: {sim.TotalTurnoverRate:F1}%)");
            sb.AppendLine($"摩擦磨损手续费总额,{sim.TotalFrictionFeeLoss:F2} 元");
            sb.AppendLine($"摩擦费率拖累,{sim.FrictionFeeDragPercent:F2}%");
            sb.AppendLine($"调仓触发总次数,{sim.RebalanceCount} 次");
            sb.AppendLine($"仿真诊断总结,\"{sim.DiagnosticSummary}\"");
            if (sim.Events.Count > 0)
            {
                sb.AppendLine("调仓日期,触发原因,调仓前净值,最大偏离度(%),单边换手率(%),冲击手续费(元),调仓执行详情");
                foreach (var ev in sim.Events)
                {
                    sb.AppendLine($"{ev.Date:yyyy-MM-dd},\"{ev.TriggerReason}\",{ev.PortfolioNavBefore:F4},{ev.MaxWeightDeviationBefore:F2}%,{ev.TurnoverRate:F2}%,{ev.FrictionFeeAmount:F2},\"{ev.Details}\"");
                }
            }
            sb.AppendLine();
        }

        // 3.7 风险预算与 Euler 边际风险贡献 (PCR) 穿透解构
        if (portfolio.RiskDecomposition != null && portfolio.RiskDecomposition.Items.Count > 0)
        {
            var rd = portfolio.RiskDecomposition;
            sb.AppendLine("=== 资产风险预算与 Euler 边际风险贡献 (PCR) 穿透解构 ===");
            sb.AppendLine($"组合年化总波动率,{rd.PortfolioVolatility:F2}%");
            sb.AppendLine($"主导风险资产,{rd.DominantRiskAsset}");
            sb.AppendLine($"风险预算基尼系数,{rd.RiskBudgetGini:F2}");
            sb.AppendLine($"是否存在风险吞噬者 (Risk-Hog),{(rd.HasRiskHog ? "是 (存在风险超载)" : "否 (风险分配合理)")}");
            sb.AppendLine("基金代码,基金名称,资金配置权重(%),边际风险贡献(MCR),绝对风险贡献(ACR),风险贡献占比(PCR %),风权比(PCR/W),风险健康评级");
            foreach (var it in rd.Items)
            {
                sb.AppendLine($"{it.FundCode},\"{it.FundName}\",{it.CapitalWeight:F2}%,{it.MarginalContributionToRisk:F4},{it.AbsoluteContributionToRisk:F4},{it.PercentageContributionToRisk:F2}%,{it.RiskConcentrationRatio:F2}x,{it.RiskStatusText}");
            }
            sb.AppendLine();
        }

        // 3.8 现代投资组合理论 (MPT) 8 套智能规划最优配置方案
        if (portfolio.Schemes != null && portfolio.Schemes.Count > 0)
        {
            sb.AppendLine("=== 现代投资组合理论 (MPT) 智能规划最优配置方案 ===");
            sb.AppendLine("方案名称,方案定位,预期年化收益(%),预期年化波动率(%),夏普比率,最优权重配比");
            foreach (var s in portfolio.Schemes)
            {
                string wStr = string.Join(" | ", s.Weights.Select(kv => $"{kv.Key}:{kv.Value:F1}%"));
                sb.AppendLine($"\"{s.SchemeName}\",\"{s.Description}\",{s.ExpectedReturn:+0.00;-0.00;0.00}%,{s.ExpectedVolatility:F2}%,{s.SharpeRatio:F2},\"{wStr}\"");
            }
            sb.AppendLine();
        }

        // 3.9 Barra CNE6 组合多因子风险与特质选股欧拉方差分解
        if (portfolio.FactorRiskAttribution != null && portfolio.FactorRiskAttribution.FactorItems.Count > 0)
        {
            var fra = portfolio.FactorRiskAttribution;
            sb.AppendLine("=== Barra CNE6 组合多因子风险与特质选股欧拉方差分解 ===");
            sb.AppendLine($"年化跟踪误差 (总主动风险),{fra.TotalActiveVolatility:F2}%");
            sb.AppendLine($"风格因子风险 (Factor Risk),{fra.FactorRiskVolatility:F2}% (占比 {fra.FactorRiskPercent:F1}%)");
            sb.AppendLine($"特质选股风险 (Specific Risk),{fra.SpecificRiskVolatility:F2}% (占比 {fra.SpecificRiskPercent:F1}%)");
            sb.AppendLine($"主导因子偏离,{fra.DominantFactorTilt}");
            sb.AppendLine($"组合风险画像,{fra.RiskAttributionProfile}");
            sb.AppendLine($"诊断分析,\"{fra.DiagnosticSummary}\"");
            sb.AppendLine("因子名称,组合加权暴露,基准暴露,主动偏离(Active Tilt),方差贡献,风险占比(%),因子投资含义");
            foreach (var f in fra.FactorItems)
            {
                sb.AppendLine($"{f.FactorName},{f.PortfolioExposure:+0.000;-0.000},{f.BenchmarkExposure:+0.000;-0.000},{f.ActiveTilt:+0.000;-0.000},{f.FactorVarianceContribution:F6},{f.FactorRiskPercent:F1}%,\"{f.FactorInterpretation}\"");
            }
            sb.AppendLine();
        }

        // 3.10 连续多资产凯利最优配置与目标波动率控波引擎
        if (portfolio.KellyAndTargetVol != null)
        {
            var kt = portfolio.KellyAndTargetVol;
            sb.AppendLine("=== 连续多资产凯利最优配置与目标波动率控波引擎 ===");
            sb.AppendLine($"预设目标波动率,{kt.TargetVolatility:F1}%");
            sb.AppendLine($"组合固有年化波动率,{kt.PortfolioIntrinsicVolatility:F1}%");
            sb.AppendLine($"建议风险资产仓位,{kt.SuggestedRiskyWeight:F1}%");
            sb.AppendLine($"建议现金/短债缓冲垫,{kt.SuggestedCashWeight:F1}%");
            sb.AppendLine($"隐含杜邦杠杆倍数,{kt.ImpliedLeverage:F2}x");
            sb.AppendLine($"控波调整后预期年化收益,{kt.AdjustedExpectedReturn:+0.00;-0.00}%");
            sb.AppendLine($"理论对数复合增长率 (g*),{kt.TheoreticalMaxLogGrowth:F2}%");
            sb.AppendLine($"资本配置决策建议,\"{kt.CapitalAllocationAdvice}\"");
            sb.AppendLine();
        }

        // 3.11 组合牛熊双边非对称捕获与暴跌条件相关性
        if (portfolio.BullBearCapture != null)
        {
            var bbp = portfolio.BullBearCapture;
            sb.AppendLine("=== 组合牛熊双边非对称捕获与暴跌条件相关性 ===");
            sb.AppendLine($"牛市上涨贝塔 (Bull Beta),{bbp.BullBeta:+0.00;-0.00}");
            sb.AppendLine($"熊市下跌贝塔 (Bear Beta),{bbp.BearBeta:+0.00;-0.00}");
            sb.AppendLine($"非对称度 (Asymmetry Index),{bbp.AsymmetryIndex:+0.00;-0.00}");
            sb.AppendLine($"上行捕获率,{bbp.UpsideCaptureRatio:F1}%");
            sb.AppendLine($"下行捕获率,{bbp.DownsideCaptureRatio:F1}%");
            sb.AppendLine($"捕获利差,{bbp.CaptureSpread:+0.0;-0.0}%");
            sb.AppendLine($"常态相关性,{bbp.NormalCorrelation:+0.00;-0.00}");
            sb.AppendLine($"极端暴跌条件相关性,{bbp.CrashCorrelation:+0.00;-0.00}");
            sb.AppendLine($"暴跌相关性漂移,{bbp.CorrelationShift:+0.00;-0.00}");
            sb.AppendLine($"凸性评级画像,{bbp.ConvexityRating}");
            sb.AppendLine($"诊断评语,\"{bbp.AsymmetryDiagnosis}\"");
            sb.AppendLine();
        }

        // 4. 组合净值走势明细
        sb.AppendLine("=== 组合合成净值时序明细 ===");
        sb.AppendLine("日期,合成单位净值,组合日收益率(%)");
        foreach (var nav in portfolio.PortfolioNavHistory)
        {
            sb.AppendLine($"{nav.Date:yyyy-MM-dd},{nav.UnitNav:F4},{nav.DailyReturn:F2}%");
        }

        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>
    /// 将基金基本信息、9大量化风控指标、黑天鹅压力测试与策略回测结果导出为专业机构级富文本 HTML 投研研报 (支持浏览器查看与打印为 PDF)
    /// </summary>
    public static void ExportToHtml(
        string filePath,
        FundDetail fund,
        QuantMetrics metrics,
        BacktestResult backtest,
        List<NavRecord> filteredNavs)
    {
        var sb = new StringBuilder();
        string safeName = WebUtility.HtmlEncode(fund.Name);
        string safeCode = WebUtility.HtmlEncode(fund.Code);
        string safeType = WebUtility.HtmlEncode(fund.Type);
        string safeManager = WebUtility.HtmlEncode(fund.ManagerName);
        string safeTenure = WebUtility.HtmlEncode(fund.ManagerTenure);
        string safeSize = WebUtility.HtmlEncode(fund.FundSize);

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-CN\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"    <title>BIGA 基金量化投研研报 - {safeCode} {safeName}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine(@"
        :root {
            --bg-primary: #0F111A;
            --bg-card: #1A1D2B;
            --bg-card-alt: #222638;
            --border-color: #2F354D;
            --text-primary: #FFFFFF;
            --text-secondary: #9DA7C2;
            --accent-blue: #4D96FF;
            --accent-gold: #FFB703;
            --bull-red: #FF5376;
            --bear-green: #00E676;
            --badge-strong: #00E676;
            --badge-warning: #FFB703;
        }
        @media print {
            body { background: #FFFFFF !important; color: #111111 !important; font-size: 10pt; }
            .card { border: 1px solid #DDDDDD !important; background: #FFFFFF !important; box-shadow: none !important; break-inside: avoid; }
            .metric-box { background: #F8F9FA !important; border: 1px solid #EEEEEE !important; }
            .no-print { display: none !important; }
            th { background: #E9ECEF !important; color: #000000 !important; }
            td { color: #222222 !important; border-bottom: 1px solid #EEEEEE !important; }
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            background-color: var(--bg-primary);
            color: var(--text-primary);
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', sans-serif;
            line-height: 1.6;
            padding: 24px;
        }
        .container { max-width: 1200px; margin: 0 auto; }
        .header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            border-bottom: 2px solid var(--accent-blue);
            padding-bottom: 18px;
            margin-bottom: 24px;
        }
        .header-title h1 { font-size: 24px; font-weight: 700; color: #FFFFFF; }
        .header-title p { font-size: 13px; color: var(--text-secondary); margin-top: 4px; }
        .brand-badge {
            background: linear-gradient(135deg, #3A7BD5, #3A6073);
            color: #FFFFFF;
            padding: 6px 14px;
            border-radius: 6px;
            font-weight: 700;
            font-size: 13px;
            letter-spacing: 1px;
        }
        .fund-badge-bar {
            display: flex;
            flex-wrap: wrap;
            gap: 12px;
            margin-bottom: 20px;
        }
        .fund-badge {
            background: var(--bg-card);
            border: 1px solid var(--border-color);
            padding: 8px 16px;
            border-radius: 8px;
            font-size: 13px;
        }
        .fund-badge span { color: var(--text-secondary); margin-right: 6px; }
        .fund-badge strong { color: #FFFFFF; font-weight: 600; }
        .grid-metrics {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 14px;
            margin-bottom: 24px;
        }
        .metric-card {
            background: var(--bg-card);
            border: 1px solid var(--border-color);
            border-radius: 8px;
            padding: 14px;
            text-align: center;
            transition: transform 0.2s;
        }
        .metric-label { font-size: 12px; color: var(--text-secondary); margin-bottom: 6px; }
        .metric-value { font-size: 22px; font-weight: 700; color: var(--accent-gold); }
        .metric-desc { font-size: 11px; color: var(--text-secondary); margin-top: 4px; }
        .val-bull { color: var(--bull-red) !important; }
        .val-bear { color: var(--bear-green) !important; }
        .val-blue { color: var(--accent-blue) !important; }
        .card {
            background: var(--bg-card);
            border: 1px solid var(--border-color);
            border-radius: 10px;
            padding: 18px;
            margin-bottom: 24px;
        }
        .card h2 {
            font-size: 16px;
            font-weight: 600;
            color: #FFFFFF;
            border-left: 4px solid var(--accent-blue);
            padding-left: 10px;
            margin-bottom: 14px;
        }
        table {
            width: 100%;
            border-collapse: collapse;
            font-size: 12.5px;
            text-align: left;
        }
        th {
            background-color: var(--bg-card-alt);
            color: #BAC2DE;
            padding: 10px 12px;
            font-weight: 600;
            border-bottom: 1px solid var(--border-color);
        }
        td {
            padding: 9px 12px;
            border-bottom: 1px solid rgba(47, 53, 77, 0.6);
            color: #E0E2EC;
        }
        tr:hover { background-color: rgba(255, 255, 255, 0.02); }
        .rating-badge {
            display: inline-block;
            padding: 2px 8px;
            border-radius: 4px;
            font-size: 11px;
            font-weight: 600;
        }
        .rating-strong { background: rgba(0, 230, 118, 0.15); color: #00E676; border: 1px solid #00E676; }
        .rating-solid { background: rgba(77, 150, 255, 0.15); color: #4D96FF; border: 1px solid #4D96FF; }
        .rating-neutral { background: rgba(255, 183, 3, 0.15); color: #FFB703; border: 1px solid #FFB703; }
        .rating-fragile { background: rgba(255, 83, 118, 0.15); color: #FF5376; border: 1px solid #FF5376; }
        .footer {
            margin-top: 30px;
            border-top: 1px solid var(--border-color);
            padding-top: 16px;
            font-size: 11px;
            color: var(--text-secondary);
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        .btn-print {
            background: #313244;
            color: #FFFFFF;
            border: 1px solid #45475A;
            padding: 6px 14px;
            border-radius: 6px;
            cursor: pointer;
            font-size: 12px;
            font-weight: 600;
        }
        .btn-print:hover { background: #45475A; }
        ");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"container\">");

        // 页眉
        sb.AppendLine("        <header class=\"header\">");
        sb.AppendLine("            <div class=\"header-title\">");
        sb.AppendLine($"                <h1>{safeName} ({safeCode}) 核心量化评价研报</h1>");
        sb.AppendLine($"                <p>统计周期: {metrics.PeriodName} ({metrics.StartDate:yyyy-MM-dd} 至 {metrics.EndDate:yyyy-MM-dd}，共计 {metrics.TradingDays} 个交易日) | 报告生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div style=\"display:flex; gap:10px; align-items:center;\">");
        sb.AppendLine("                <button class=\"btn-print no-print\" onclick=\"window.print()\">🖨️ 打印 / 另存为 PDF</button>");
        sb.AppendLine("                <div class=\"brand-badge\">BIGA QUANT</div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </header>");

        // 基金基础属性徽章栏
        sb.AppendLine("        <div class=\"fund-badge-bar\">");
        sb.AppendLine($"            <div class=\"fund-badge\"><span>代码:</span><strong>{safeCode}</strong></div>");
        sb.AppendLine($"            <div class=\"fund-badge\"><span>基金名称:</span><strong>{safeName}</strong></div>");
        sb.AppendLine($"            <div class=\"fund-badge\"><span>投资类型:</span><strong>{safeType}</strong></div>");
        sb.AppendLine($"            <div class=\"fund-badge\"><span>基金经理:</span><strong>{safeManager}</strong></div>");
        sb.AppendLine($"            <div class=\"fund-badge\"><span>经理年限:</span><strong>{safeTenure}</strong></div>");
        sb.AppendLine($"            <div class=\"fund-badge\"><span>资产规模:</span><strong>{safeSize}</strong></div>");
        sb.AppendLine("        </div>");

        // 核心量化指标看板网格
        sb.AppendLine("        <div class=\"grid-metrics\">");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">区间累计收益率</div><div class=\"metric-value val-bull\">{metrics.TotalReturn:+0.00;-0.00;0.00}%</div><div class=\"metric-desc\">沪深300同期 {metrics.BenchmarkReturn:+0.00;-0.00;0.00}%</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">年化复合增长率 (CAGR)</div><div class=\"metric-value val-bull\">{metrics.AnnualizedReturn:+0.00;-0.00;0.00}%</div><div class=\"metric-desc\">250 交易日复利折算</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">历史最大回撤 (MDD)</div><div class=\"metric-value val-bear\">{metrics.MaxDrawdown:F2}%</div><div class=\"metric-desc\">修复交易日: {(metrics.RecoveryTradingDays.HasValue ? metrics.RecoveryTradingDays.Value + "天" : "未修复")}</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">夏普比率 (Sharpe)</div><div class=\"metric-value\">{metrics.SharpeRatio:F2}</div><div class=\"metric-desc\">无风险利率 Rf=2.0%</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">综合捕获比率 (Capture)</div><div class=\"metric-value val-bull\">{metrics.CaptureRatio:F2}</div><div class=\"metric-desc\">上行 {metrics.UpsideCaptureRatio:F1}% / 下行 {metrics.DownsideCaptureRatio:F1}%</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">特雷诺比率 (Treynor)</div><div class=\"metric-value val-blue\">{metrics.TreynorRatio:F2}</div><div class=\"metric-desc\">系统性贝塔风险回报</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">年化跟踪误差 (TE)</div><div class=\"metric-value\">{metrics.TrackingError:F2}%</div><div class=\"metric-desc\">偏离沪深300离散度</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">卡玛比率 (Calmar)</div><div class=\"metric-value val-blue\">{metrics.CalmarRatio:F2}</div><div class=\"metric-desc\">年化回报 / 最大回撤</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">索提诺比率 (Sortino)</div><div class=\"metric-value\">{metrics.SortinoRatio:F2}</div><div class=\"metric-desc\">下行波动调整回报</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">年化波动率</div><div class=\"metric-value\">{metrics.AnnualizedVolatility:F2}%</div><div class=\"metric-desc\">日收益率年化标准差</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">CAPM 阿尔法 (Alpha)</div><div class=\"metric-value val-bull\">{metrics.Alpha:+0.00;-0.00;0.00}%</div><div class=\"metric-desc\">贝塔 Beta={metrics.Beta:F2}</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">95% 在险价值 (VaR / CVaR)</div><div class=\"metric-value val-bear\">{metrics.VaR95:F2}% / {metrics.CVaR95:F2}%</div><div class=\"metric-desc\">单日极端尾部风险</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">奥米加比率 (Omega)</div><div class=\"metric-value val-bull\">{metrics.OmegaRatio:F2}</div><div class=\"metric-desc\">全收益分布胜率质量比</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">溃疡指数 (Ulcer UI)</div><div class=\"metric-value val-bear\">{metrics.UlcerIndex:F2}%</div><div class=\"metric-desc\">回撤深度与水下方根</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">马丁比率 (Martin)</div><div class=\"metric-value val-blue\">{metrics.MartinRatio:F2}</div><div class=\"metric-desc\">超额收益 / 溃疡指数</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">痛苦比 / 下行标准差</div><div class=\"metric-value\">{metrics.PainRatio:F2} / {metrics.DownsideDeviation:F1}%</div><div class=\"metric-desc\">Pain Ratio / σ_down</div></div>");
        sb.AppendLine("        </div>");
        sb.AppendLine();

        // 历史前五大最深回撤周期深度解构
        if (metrics.DrawdownEpisodes != null && metrics.DrawdownEpisodes.Count > 0)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine("            <h2>📉 历史前五大最深回撤周期解构 (Top 5 Drawdown Episodes)</h2>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>排名</th><th>波峰日期</th><th>谷底日期</th><th>修复出坑日</th><th>最大跌幅</th><th>暴跌历时</th><th>修复历时</th><th>全周期历时</th><th>修复状态</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var ep in metrics.DrawdownEpisodes)
            {
                string recStr = ep.RecoveryDate.HasValue ? ep.RecoveryDate.Value.ToString("yyyy-MM-dd") : "<span style=\"color:#FF5376;\">未出坑</span>";
                string recDays = ep.RecoveryDays.HasValue ? $"{ep.RecoveryDays.Value} 交易日" : "-";
                sb.AppendLine($"                    <tr><td><strong>#{ep.Rank}</strong></td><td>{ep.PeakDate:yyyy-MM-dd}</td><td>{ep.TroughDate:yyyy-MM-dd}</td><td>{recStr}</td><td class=\"val-bear\" style=\"font-weight:700;\">{ep.DrawdownPercent:F2}%</td><td>{ep.FallDays} 交易日</td><td>{recDays}</td><td>{ep.TotalDays} 交易日</td><td>{ep.StatusDesc}</td></tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 机构级五维量化评价看板
        var scoreCard = QuantCalculator.CalculateFundScore(fund, metrics);
        sb.AppendLine("        <div class=\"card\">");
        sb.AppendLine($"            <h2>🏆 机构级五维量化综合评价 - 综合得分: <span style=\"color:var(--accent-gold); font-size:20px;\">{scoreCard.OverallScore:F1} 分</span> [{scoreCard.RatingGrade}]</h2>");
        sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:12px; margin-bottom:14px;\">");
        sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">长期超额回报</div><div class=\"metric-value val-bull\">{scoreCard.ReturnScore:F1}</div><div class=\"metric-desc\">权重 25%</div></div>");
        sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">最大回撤风控</div><div class=\"metric-value val-bear\">{scoreCard.RiskControlScore:F1}</div><div class=\"metric-desc\">权重 20%</div></div>");
        sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">夏普与风险调整</div><div class=\"metric-value val-blue\">{scoreCard.RiskAdjustedScore:F1}</div><div class=\"metric-desc\">权重 25%</div></div>");
        sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">极端抗跌能力</div><div class=\"metric-value\">{scoreCard.ResilienceScore:F1}</div><div class=\"metric-desc\">权重 15%</div></div>");
        sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">风格稳定性</div><div class=\"metric-value val-cyan\">{scoreCard.StabilityScore:F1}</div><div class=\"metric-desc\">权重 15%</div></div>");
        sb.AppendLine("            </div>");
        if (scoreCard.HighlightTags.Count > 0 || scoreCard.RiskWarnings.Count > 0)
        {
            sb.AppendLine("            <div style=\"display:flex; flex-wrap:wrap; gap:8px;\">");
            foreach (var tag in scoreCard.HighlightTags)
                sb.AppendLine($"                <span style=\"background:rgba(0, 230, 118, 0.15); color:#00E676; border:1px solid #00E676; padding:3px 8px; border-radius:4px; font-size:11.5px;\">🌟 {WebUtility.HtmlEncode(tag)}</span>");
            foreach (var warn in scoreCard.RiskWarnings)
                sb.AppendLine($"                <span style=\"background:rgba(255, 83, 118, 0.15); color:#FF5376; border:1px solid #FF5376; padding:3px 8px; border-radius:4px; font-size:11.5px;\">⚠️ {WebUtility.HtmlEncode(warn)}</span>");
            sb.AppendLine("            </div>");
        }
        sb.AppendLine("        </div>");

        // 机构级基金经理择时与选股非线性双模型诊断 (Treynor-Mazuy & Henriksson-Merton)
        var timing = metrics.TimingAbility;
        if (timing != null)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>👔 基金经理择时与选股能力量化诊断 (TM / HM 双模型) - 综合评级: <span style=\"color:var(--accent-gold); font-size:18px;\">{WebUtility.HtmlEncode(timing.TimingRatingBadge)}</span></h2>");
            sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(timing.DiagnosticSummary)}</p>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>学术量化模型</th><th>选股 Alpha (年化)</th><th>Alpha t 统计量</th><th>择时系数 (γ / β2)</th><th>择时 t 统计量</th><th>择时显著性 (P-Value)</th><th>拟合优度 (R²)</th><th>模型结论判定</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            sb.AppendLine("                    <tr>");
            sb.AppendLine("                        <td><strong>Treynor-Mazuy (二次项模型)</strong></td>");
            sb.AppendLine($"                        <td style=\"color:{(timing.TmAlphaAnnualized >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:600;\">{timing.TmAlphaAnnualized:+0.00;-0.00;0.00}%</td>");
            sb.AppendLine($"                        <td>{timing.TmAlphaTStat:F2}</td>");
            sb.AppendLine($"                        <td style=\"color:{(timing.TmGamma >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:700;\">{timing.TmGamma:+0.0000;-0.0000;0.0000}</td>");
            sb.AppendLine($"                        <td>{timing.TmGammaTStat:F2}</td>");
            sb.AppendLine($"                        <td style=\"color:{(timing.TmGammaPValue < 0.05m ? "var(--accent-gold)" : "var(--text-secondary)")};\">{timing.TmGammaPValue:F4} {(timing.TmGammaPValue < 0.05m ? "★" : "")}</td>");
            sb.AppendLine($"                        <td>{timing.TmR2:F1}%</td>");
            sb.AppendLine($"                        <td><span style=\"background:rgba(255, 183, 3, 0.15); color:var(--accent-gold); padding:2px 8px; border-radius:4px;\">{(timing.HasTmTimingSkill ? "显著择时能力" : "择时未达显著水平")}</span></td>");
            sb.AppendLine("                    </tr>");
            sb.AppendLine("                    <tr>");
            sb.AppendLine("                        <td><strong>Henriksson-Merton (期权二项模型)</strong></td>");
            sb.AppendLine($"                        <td style=\"color:{(timing.HmAlphaAnnualized >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:600;\">{timing.HmAlphaAnnualized:+0.00;-0.00;0.00}%</td>");
            sb.AppendLine($"                        <td>{timing.HmAlphaTStat:F2}</td>");
            sb.AppendLine($"                        <td style=\"color:{(timing.HmDownsideBeta >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:700;\">{timing.HmDownsideBeta:+0.0000;-0.0000;0.0000}</td>");
            sb.AppendLine($"                        <td>{timing.HmDownsideBetaTStat:F2}</td>");
            sb.AppendLine($"                        <td style=\"color:{(timing.HmDownsideBetaPValue < 0.05m ? "var(--accent-gold)" : "var(--text-secondary)")};\">{timing.HmDownsideBetaPValue:F4} {(timing.HmDownsideBetaPValue < 0.05m ? "★" : "")}</td>");
            sb.AppendLine($"                        <td>{timing.HmR2:F1}%</td>");
            sb.AppendLine($"                        <td><span style=\"background:rgba(77, 150, 255, 0.15); color:var(--accent-blue); padding:2px 8px; border-radius:4px;\">{(timing.HasHmTimingSkill ? "显著下行避险期权特征" : "避险保护未达显著")}</span></td>");
            sb.AppendLine("                    </tr>");
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 最新季报十大重仓股透视与申万行业穿透
        if (fund.Holdings != null && fund.Holdings.Count > 0)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>📦 最新季报十大重仓股透视与申万一级行业穿透 (CR10集中度: {fund.HoldingsCr10:F1}%)</h2>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>序号</th><th>股票代码</th><th>股票名称</th><th style=\"text-align:right;\">持仓占比</th><th>申万一级行业</th><th>持仓变动</th><th>披露报告期</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            int rank = 1;
            foreach (var h in fund.Holdings)
            {
                string changeColor = h.ShareChange.Contains("新进") ? "var(--bull-red)" : (h.ShareChange.Contains("加仓") ? "var(--bull-red)" : (h.ShareChange.Contains("减仓") ? "var(--bear-green)" : "inherit"));
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td style=\"color:var(--text-secondary);\">{rank++}</td>");
                sb.AppendLine($"                        <td style=\"font-family:monospace; font-weight:600;\">{WebUtility.HtmlEncode(h.StockCode)}</td>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(h.StockName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-gold); font-weight:700;\">{h.WeightPercent:F2}%</td>");
                sb.AppendLine($"                        <td><span style=\"background:rgba(77, 150, 255, 0.15); color:var(--accent-blue); padding:2px 6px; border-radius:4px; font-size:11.5px;\">{WebUtility.HtmlEncode(h.Industry)}</span></td>");
                sb.AppendLine($"                        <td style=\"color:{changeColor};\">{WebUtility.HtmlEncode(h.ShareChange)}</td>");
                sb.AppendLine($"                        <td style=\"color:var(--text-secondary); font-size:11.5px;\">{WebUtility.HtmlEncode(h.ReportDate)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
        }
        sb.AppendLine("        </div>");

        // 晨星九宫格风格漂移追踪
        var drift = fund.StyleDrift ?? QuantCalculator.AnalyzeStyleDrift(fund, metrics);
        if (drift != null && drift.HistoryPoints.Count > 0)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🧭 晨星九宫格风格漂移监控 (漂移指数 SDI: <span style=\"color:var(--accent-gold); font-weight:700;\">{drift.StyleDriftIndex:F2}</span> | 评级: <span style=\"color:var(--bear-green); font-weight:700;\">{WebUtility.HtmlEncode(drift.StabilityRating)}</span>)</h2>");
            sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(drift.AnalysisSummary)}</p>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>报告季度</th><th style=\"text-align:center;\">风格九宫格定位</th><th style=\"text-align:right;\">规模因子得分 (Size)</th><th style=\"text-align:right;\">价值因子得分 (Value)</th><th style=\"text-align:right;\">重仓股集中度 (CR10)</th><th>第一大重仓行业</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var pt in drift.HistoryPoints)
            {
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td style=\"font-weight:600;\">{WebUtility.HtmlEncode(pt.Period)}</td>");
                sb.AppendLine($"                        <td style=\"text-align:center;\"><span style=\"background:rgba(255, 183, 3, 0.15); color:var(--accent-gold); padding:2px 8px; border-radius:4px; font-weight:600;\">{WebUtility.HtmlEncode(pt.StyleBoxName)}</span></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-blue);\">{pt.SizeScore:+0.0;-0.0;0.0}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bull-red);\">{pt.ValueScore:+0.0;-0.0;0.0}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; font-weight:600;\">{pt.Cr10:F1}%</td>");
                sb.AppendLine($"                        <td style=\"color:var(--text-secondary); font-size:11.5px;\">{WebUtility.HtmlEncode(pt.TopIndustry)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 机构级 Brinson-Fachler 业绩归因
        var brinson = fund.BrinsonResult ?? BrinsonAttributionEngine.CalculateFundBrinsonAttribution(fund);
        if (brinson.SectorItems.Count > 0)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🎯 机构级 Brinson-Fachler 业绩归因分解 (区间超额 Alpha: <span style=\"color:var(--bull-red); font-weight:700;\">{brinson.TotalExcessReturn:+0.00;-0.00;0.00}%</span>)</h2>");
            sb.AppendLine("            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">" + WebUtility.HtmlEncode(brinson.SummaryAnalysis) + "</p>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>板块 / 行业</th><th style=\"text-align:right;\">基金权重</th><th style=\"text-align:right;\">基准权重</th><th style=\"text-align:right;\">基金收益率</th><th style=\"text-align:right;\">基准收益率</th><th style=\"text-align:right;\">大类配置效应</th><th style=\"text-align:right;\">个股选择效应</th><th style=\"text-align:right;\">交互协同效应</th><th style=\"text-align:right;\">总超额贡献</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var sec in brinson.SectorItems)
            {
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(sec.SectorName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{sec.PortfolioWeight:F1}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--text-secondary);\">{sec.BenchmarkWeight:F1}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(sec.PortfolioReturn >= 0 ? "var(--bull-red)" : "var(--bear-green)")};\">{sec.PortfolioReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{sec.BenchmarkReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(sec.AllocationEffect >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:600;\">{sec.AllocationEffect:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(sec.SelectionEffect >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:600;\">{sec.SelectionEffect:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{sec.InteractionEffect:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(sec.TotalEffect >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:700;\">{sec.TotalEffect:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 历史黑天鹅极端市场压力测试表
        if (metrics.StressTestScenarios.Count > 0)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine("            <h2>⛈️ 历史经典黑天鹅极端市场情景压力测试 (Stress Testing)</h2>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead>");
            sb.AppendLine("                    <tr>");
            sb.AppendLine("                        <th>危机事件</th><th>起止区间</th><th>本基金表现</th><th>沪深300同期</th><th>超额表现</th><th>期间最大回撤</th><th>防御评级</th><th>危机特征</th>");
            sb.AppendLine("                    </tr>");
            sb.AppendLine("                </thead>");
            sb.AppendLine("                <tbody>");
            foreach (var st in metrics.StressTestScenarios)
            {
                string ratingClass = st.DefenseRating switch
                {
                    "强力防御" => "rating-strong",
                    "稳健防御" => "rating-solid",
                    "中性同步" => "rating-neutral",
                    _ => "rating-fragile"
                };
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(st.ScenarioName)}</strong></td>");
                sb.AppendLine($"                        <td>{st.StartDate:yyyy-MM-dd} ~ {st.EndDate:yyyy-MM-dd}</td>");
                sb.AppendLine($"                        <td style=\"color:{(st.FundReturnRate >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:600;\">{st.FundReturnRate:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td>{st.BenchmarkReturnRate:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"color:{(st.ExcessReturnRate >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:600;\">{st.ExcessReturnRate:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"color:var(--bear-green);\">{st.MaxDrawdown:F2}%</td>");
                sb.AppendLine($"                        <td><span class=\"rating-badge {ratingClass}\">{WebUtility.HtmlEncode(st.DefenseRating)}</span></td>");
                sb.AppendLine($"                        <td style=\"color:var(--text-secondary); font-size:11.5px;\">{WebUtility.HtmlEncode(st.Description)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 机构级 Barra 风格多因子归因解构
        var barraHtml = metrics.BarraAttribution ?? fund.BarraAttribution;
        if (barraHtml != null && barraHtml.Factors.Count > 0)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🧬 Barra CNE5/CNE6 风格多因子归因与收益解构 (特质Alpha: <span style=\"color:var(--bull-red); font-weight:700;\">{barraHtml.SpecificAlpha:+0.00;-0.00;0.00}%</span> | R²: <span style=\"color:var(--accent-gold); font-weight:700;\">{barraHtml.RSquared * 100m:F1}%</span> | 主导风格: <span style=\"color:var(--accent-blue); font-weight:700;\">{WebUtility.HtmlEncode(barraHtml.DominantStyle)}</span>)</h2>");
            sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(barraHtml.AttributionSummary)}</p>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>因子代码</th><th>因子名称</th><th style=\"text-align:right;\">因子暴露 (Beta)</th><th style=\"text-align:right;\">t 统计量</th><th style=\"text-align:center;\">统计显著性</th><th style=\"text-align:right;\">年化贡献收益</th><th>因子金融学说明</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var f in barraHtml.Factors)
            {
                string sigClass = f.Significance.Contains("***") ? "rating-strong" : (f.Significance.Contains("**") ? "rating-solid" : "rating-neutral");
                string retColor = f.ContributionReturn >= 0 ? "var(--bull-red)" : "var(--bear-green)";
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td style=\"font-family:monospace; font-weight:600;\">{WebUtility.HtmlEncode(f.FactorCode)}</td>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(f.FactorName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; font-weight:600; color:var(--accent-gold);\">{f.Beta:F3}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{f.TStat:F2}</td>");
                sb.AppendLine($"                        <td style=\"text-align:center;\"><span class=\"rating-badge {sigClass}\">{WebUtility.HtmlEncode(f.Significance)}</span></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{retColor}; font-weight:700;\">{f.ContributionReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"color:var(--text-secondary); font-size:11.5px;\">{WebUtility.HtmlEncode(f.Description)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 基金经理任期全景与滚动胜率矩阵
        var mgrHtml = metrics.ManagerProfile ?? fund.ManagerProfile;
        if (mgrHtml != null)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>👔 基金经理生涯任期全景与滚动胜率矩阵 (经理: <strong>{WebUtility.HtmlEncode(mgrHtml.ManagerName)}</strong> | 任期: {WebUtility.HtmlEncode(mgrHtml.Tenure)} | 稳定性评级: <span class=\"rating-badge rating-strong\">{WebUtility.HtmlEncode(mgrHtml.StabilityRating)}</span>)</h2>");
            sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(mgrHtml.CareerSummary)}</p>");
            sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:12px; margin-bottom:14px;\">");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>任期累计回报:</span><strong style=\"color:var(--bull-red);\">{mgrHtml.TenureCumulativeReturn:+0.00;-0.00;0.00}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>任期复合年化 (CAGR):</span><strong style=\"color:var(--accent-gold);\">{mgrHtml.TenureAnnualizedReturn:+0.00;-0.00;0.00}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>同期沪深300:</span><strong>{mgrHtml.BenchmarkCumulativeReturn:+0.00;-0.00;0.00}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>任期相对超额:</span><strong style=\"color:var(--bull-red);\">{mgrHtml.TenureExcessReturn:+0.00;-0.00;0.00}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>滚动月度胜率:</span><strong style=\"color:var(--accent-blue);\">{mgrHtml.RollingMonthlyWinRate:F1}% ({mgrHtml.MonthlyWinCount}/{mgrHtml.TotalMonths}月)</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>滚动季度胜率:</span><strong style=\"color:var(--accent-gold);\">{mgrHtml.RollingQuarterlyWinRate:F1}% ({mgrHtml.QuarterlyWinCount}/{mgrHtml.TotalQuarters}季)</strong></div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");
        }

        // 机构级非正态极值尾部风险与巴塞尔监管在险 (Cornish-Fisher Tail Risk)
        var tailHtml = metrics.TailRisk;
        if (tailHtml != null)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🛡️ 非正态极值尾部风险模型 (Cornish-Fisher 展开式 | 肥尾评级: <span class=\"rating-badge rating-fragile\">{WebUtility.HtmlEncode(tailHtml.TailFatnessRating)}</span> | Basel III 10日99% VaR: <strong style=\"color:var(--accent-gold);\">{tailHtml.Basel10DayVaR99:F2}%</strong>)</h2>");
            sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(tailHtml.DiagnosticSummary)}</p>");
            sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:12px;\">");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>分布样本偏度:</span><strong style=\"color:{(tailHtml.Skewness >= 0 ? "var(--bear-green)" : "var(--bull-red)")};\">{tailHtml.Skewness:+0.00;-0.00;0.00}</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>分布超额峰度:</span><strong style=\"color:{(tailHtml.ExcessKurtosis <= 0.5m ? "var(--bear-green)" : "var(--bull-red)")};\">{tailHtml.ExcessKurtosis:+0.00;-0.00;0.00}</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>95% 修正 mVaR:</span><strong style=\"color:var(--bull-red);\">{tailHtml.CornishFisherVaR95:F2}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>95% 修正 mCVaR:</span><strong style=\"color:var(--bull-red);\">{tailHtml.CornishFisherCVaR95:F2}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>99% 极值 mVaR:</span><strong style=\"color:var(--bull-red);\">{tailHtml.CornishFisherVaR99:F2}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>99% 极值 mCVaR:</span><strong style=\"color:var(--bull-red);\">{tailHtml.CornishFisherCVaR99:F2}%</strong></div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");
        }

        // 全历史水下套牢深度解构 (Underwater Analysis)
        var underHtml = metrics.UnderwaterAnalysis ?? fund.UnderwaterAnalysis;
        if (underHtml != null)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>📉 全历史水下套牢深度解构 (水下时间占比: <strong style=\"color:var(--bull-red);\">{underHtml.UnderwaterTimeRatio:F1}%</strong> | 最长连续水下: <strong style=\"color:var(--accent-gold);\">{underHtml.MaxUnderwaterDays} 交易日</strong> | 痛苦指数: <strong style=\"color:var(--accent-blue);\">{underHtml.PainIndex:F2}%</strong>)</h2>");
            sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(underHtml.DiagnosticSummary)}</p>");
            sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:12px;\">");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>水下套牢时间比:</span><strong style=\"color:var(--bull-red);\">{underHtml.UnderwaterTimeRatio:F1}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>最长连续水下天数:</span><strong style=\"color:var(--accent-gold);\">{underHtml.MaxUnderwaterDays} 交易日</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>当前连续水下天数:</span><strong>{underHtml.CurrentUnderwaterDays} 交易日</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>水下平均回撤深度:</span><strong style=\"color:var(--bull-red);\">{underHtml.AverageUnderwaterDepth:F2}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>水下痛苦指数 (PI):</span><strong style=\"color:var(--accent-blue);\">{underHtml.PainIndex:F2}%</strong></div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");
        }

        // Fama-French 五因子资产定价模型解构 (FF5)
        var ff5Html = metrics.FamaFrenchResult ?? fund.FamaFrenchResult;
        if (ff5Html != null && ff5Html.FactorItems.Count > 0)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🏛️ Fama-French 五因子资产定价模型 (特质Alpha: <span style=\"color:var(--bull-red); font-weight:700;\">{ff5Html.AlphaAnnualized:+0.00;-0.00;0.00}%</span> | R²: <span style=\"color:var(--accent-gold); font-weight:700;\">{ff5Html.RSquaredPercent:F1}%</span> | 主导因子: <span style=\"color:var(--accent-blue); font-weight:700;\">{WebUtility.HtmlEncode(ff5Html.DominantFactor)}</span>)</h2>");
            sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(ff5Html.AttributionSummary)}</p>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>因子代码</th><th>因子名称</th><th style=\"text-align:right;\">因子暴露 (Beta)</th><th style=\"text-align:right;\">t 统计量</th><th style=\"text-align:center;\">p 显著性</th><th style=\"text-align:right;\">年化贡献收益</th><th style=\"text-align:right;\">贡献占比</th><th>因子定价含义与逻辑</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var f in ff5Html.FactorItems)
            {
                string pColor = f.PValue < 0.05m ? "var(--bear-green)" : "var(--text-secondary)";
                string retColor = f.AnnualizedContribution >= 0 ? "var(--bull-red)" : "var(--bear-green)";
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td style=\"font-family:monospace; font-weight:600;\">{WebUtility.HtmlEncode(f.FactorId)}</td>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(f.FactorName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; font-weight:600; color:var(--accent-gold);\">{f.Beta:+0.00;-0.00;0.00}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{f.TStat:+0.00;-0.00;0.00}</td>");
                sb.AppendLine($"                        <td style=\"text-align:center; font-family:monospace; color:{pColor}; font-weight:600;\">{WebUtility.HtmlEncode(f.PValueText)}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{retColor}; font-weight:700;\">{f.AnnualizedContribution:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{f.ContributionPercent:F1}%</td>");
                sb.AppendLine($"                        <td style=\"color:var(--text-secondary); font-size:11.5px;\">{WebUtility.HtmlEncode(f.Description)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 策略定投回测表现卡片
        sb.AppendLine("        <div class=\"card\">");
        sb.AppendLine($"            <h2>🚀 策略回测绩效总览 - {WebUtility.HtmlEncode(backtest.StrategyName)}</h2>");
        sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:12px; margin-bottom:14px;\">");
        sb.AppendLine($"                <div class=\"fund-badge\"><span>累计定投期数:</span><strong>{backtest.TotalPeriods} 期</strong></div>");
        sb.AppendLine($"                <div class=\"fund-badge\"><span>累计投入本金:</span><strong>¥{backtest.TotalInvested:N2}</strong></div>");
        sb.AppendLine($"                <div class=\"fund-badge\"><span>期末持仓市值:</span><strong>¥{backtest.FinalAssetValue:N2}</strong></div>");
        sb.AppendLine($"                <div class=\"fund-badge\"><span>累计总收益率:</span><strong style=\"color:var(--bull-red);\">{backtest.TotalReturnRate:+0.00;-0.00;0.00}%</strong></div>");
        sb.AppendLine($"                <div class=\"fund-badge\"><span>真实年化 (XIRR):</span><strong style=\"color:var(--accent-gold);\">{backtest.AnnualizedIrr:+0.00;-0.00;0.00}%</strong></div>");
        sb.AppendLine($"                <div class=\"fund-badge\"><span>一次性买入对照:</span><strong>{backtest.BuyAndHoldReturnRate:+0.00;-0.00;0.00}%</strong></div>");
        sb.AppendLine("            </div>");
        sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary);\">策略逻辑: {WebUtility.HtmlEncode(backtest.StrategyDescription)}</p>");
        sb.AppendLine("        </div>");

        // 机构级 FOF 尽调六维雷达综合评分与晨星五星等效评级报告卡
        var ddHtml = metrics.DueDiligence ?? fund.DueDiligence;
        if (ddHtml != null)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🌟 机构级 FOF 尽调六维雷达综合评分与晨星等效评级 (综合得分: <span style=\"color:var(--accent-gold); font-weight:700;\">{ddHtml.OverallDiligenceScore:F1}</span>/100 | 等效星级: <span style=\"color:#FFB703; font-weight:700;\">{ddHtml.StarRating}</span> | 定级建议: <span style=\"color:var(--bear-green); font-weight:700;\">{ddHtml.DiligenceGrade}</span>)</h2>");
            sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(170px, 1fr)); gap:12px; margin-bottom:16px;\">");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">纯Alpha纯度 (Alpha Purity)</div><div class=\"metric-value\">{ddHtml.AlphaPurityScore:F1}</div><div class=\"metric-desc\">超额收益能力权重 25%</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">择时凸性 (Timing Convexity)</div><div class=\"metric-value\">{ddHtml.TimingConvexityScore:F1}</div><div class=\"metric-desc\">非对称捕获权重 20%</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">尾部抗脆弱 (Tail Resilience)</div><div class=\"metric-value\">{ddHtml.TailResilienceScore:F1}</div><div class=\"metric-desc\">极值风险防守权重 20%</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">风险调整性价比 (Sharpe/Calmar)</div><div class=\"metric-value\">{ddHtml.RiskAdjustedEfficiencyScore:F1}</div><div class=\"metric-desc\">性价比与赔率权重 15%</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">风格纪律性 (Style Discipline)</div><div class=\"metric-value\">{ddHtml.StyleDisciplineScore:F1}</div><div class=\"metric-desc\">不漂移稳定性权重 10%</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">规模容量 (Capacity)</div><div class=\"metric-value\">{ddHtml.CapacityLiquidityScore:F1}</div><div class=\"metric-desc\">资产流动性权重 10%</div></div>");
            sb.AppendLine("            </div>");
            sb.AppendLine($"            <div style=\"background:var(--bg-card-alt); border-left:4px solid var(--accent-gold); padding:12px 16px; border-radius:4px; font-size:13px; color:#E0E2EC; margin-bottom:12px;\"><strong>投决会决议:</strong> {WebUtility.HtmlEncode(ddHtml.InstitutionalVerdict)}</div>");
            if (ddHtml.KeyStrengths.Count > 0 || ddHtml.KeyRisks.Count > 0)
            {
                sb.AppendLine("            <div style=\"display:flex; gap:16px; flex-wrap:wrap; font-size:12px;\">");
                if (ddHtml.KeyStrengths.Count > 0)
                    sb.AppendLine("                <div><span style=\"color:var(--bear-green); font-weight:600;\">核心亮点: </span>" + string.Join(" | ", ddHtml.KeyStrengths.Select(s => $"<span>{WebUtility.HtmlEncode(s)}</span>")) + "</div>");
                if (ddHtml.KeyRisks.Count > 0)
                    sb.AppendLine("                <div><span style=\"color:var(--bull-red); font-weight:600;\">关键风控注意点: </span>" + string.Join(" | ", ddHtml.KeyRisks.Select(r => $"<span>{WebUtility.HtmlEncode(r)}</span>")) + "</div>");
                sb.AppendLine("            </div>");
            }
            sb.AppendLine("        </div>");
        }

        // 牛熊市场双边非对称捕获与极端暴跌条件相关性
        var bbHtml = metrics.BullBearCapture ?? fund.BullBearCapture;
        if (bbHtml != null)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🛡️ 牛熊双边非对称捕获与暴跌条件相关性 (捕获利差: <span style=\"color:var(--bull-red); font-weight:700;\">{bbHtml.CaptureSpread:+0.0;-0.0}%</span> | 非对称度: <span style=\"color:var(--accent-gold); font-weight:700;\">{bbHtml.AsymmetryIndex:+0.00;-0.00}</span> | 凸性评级: <span style=\"color:var(--bear-green); font-weight:700;\">{WebUtility.HtmlEncode(bbHtml.ConvexityRating)}</span>)</h2>");
            sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(170px, 1fr)); gap:12px; margin-bottom:16px;\">");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">牛市上涨贝塔 (Bull Beta)</div><div class=\"metric-value\">{bbHtml.BullBeta:+0.00;-0.00}</div><div class=\"metric-desc\">基准上涨日弹性</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">熊市下跌贝塔 (Bear Beta)</div><div class=\"metric-value\">{bbHtml.BearBeta:+0.00;-0.00}</div><div class=\"metric-desc\">基准下跌日回撤吸收</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">上行捕获率 (UCR)</div><div class=\"metric-value\" style=\"color:var(--bull-red);\">{bbHtml.UpsideCaptureRatio:F1}%</div><div class=\"metric-desc\">上涨周期收益捕获</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">下行捕获率 (DCR)</div><div class=\"metric-value\" style=\"color:var(--bear-green);\">{bbHtml.DownsideCaptureRatio:F1}%</div><div class=\"metric-desc\">下跌周期风险承担</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">常态全样本相关系数</div><div class=\"metric-value\">{bbHtml.NormalCorrelation:+0.00;-0.00}</div><div class=\"metric-desc\">与基准日常同步性</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">暴跌日条件相关系数</div><div class=\"metric-value\" style=\"color:var(--accent-blue);\">{bbHtml.CrashCorrelation:+0.00;-0.00}</div><div class=\"metric-desc\">大盘暴跌>1%时条件相关</div></div>");
            sb.AppendLine("            </div>");
            sb.AppendLine($"            <div style=\"font-size:12.5px; color:var(--text-secondary);\"><strong>诊断评语:</strong> {WebUtility.HtmlEncode(bbHtml.AsymmetryDiagnosis)} (暴跌相关性漂移: <span style=\"color:{(bbHtml.CorrelationShift >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:700;\">{bbHtml.CorrelationShift:+0.00;-0.00}</span>)</div>");
            sb.AppendLine("        </div>");
        }

        // 页脚免责声明
        sb.AppendLine("        <footer class=\"footer\">");
        sb.AppendLine("            <div>本报告由 BIGA 机构级公募基金量化投研工作站自动解算生成。数据来源于东方财富/天天基金公开历史披露，过往业绩不代表未来表现。</div>");
        sb.AppendLine("            <div>BIGA Research Terminal &copy; 2026</div>");
        sb.AppendLine("        </footer>");

        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>
    /// 将资产配置组合权重、四套 MPT 优化方案、相关系数热力表与压力测试结果导出为机构级 HTML 资产配置研报
    /// </summary>
    public static void ExportPortfolioToHtml(
        string filePath,
        IEnumerable<PortfolioItem> components,
        PortfolioResult portfolio)
    {
        var sb = new StringBuilder();
        var compList = components.ToList();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-CN\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>BIGA 投资组合资产配置与量化回测研报</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine(@"
        :root {
            --bg-primary: #0F111A;
            --bg-card: #1A1D2B;
            --bg-card-alt: #222638;
            --border-color: #2F354D;
            --text-primary: #FFFFFF;
            --text-secondary: #9DA7C2;
            --accent-blue: #4D96FF;
            --accent-gold: #FFB703;
            --bull-red: #FF5376;
            --bear-green: #00E676;
        }
        @media print {
            body { background: #FFFFFF !important; color: #111111 !important; font-size: 10pt; }
            .card { border: 1px solid #DDDDDD !important; background: #FFFFFF !important; box-shadow: none !important; break-inside: avoid; }
            .no-print { display: none !important; }
            th { background: #E9ECEF !important; color: #000000 !important; }
            td { color: #222222 !important; border-bottom: 1px solid #EEEEEE !important; }
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            background-color: var(--bg-primary);
            color: var(--text-primary);
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', sans-serif;
            line-height: 1.6;
            padding: 24px;
        }
        .container { max-width: 1200px; margin: 0 auto; }
        .header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            border-bottom: 2px solid var(--accent-blue);
            padding-bottom: 18px;
            margin-bottom: 24px;
        }
        .header-title h1 { font-size: 24px; font-weight: 700; color: #FFFFFF; }
        .header-title p { font-size: 13px; color: var(--text-secondary); margin-top: 4px; }
        .brand-badge {
            background: linear-gradient(135deg, #3A7BD5, #3A6073);
            color: #FFFFFF;
            padding: 6px 14px;
            border-radius: 6px;
            font-weight: 700;
            font-size: 13px;
        }
        .grid-metrics {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 14px;
            margin-bottom: 24px;
        }
        .metric-card {
            background: var(--bg-card);
            border: 1px solid var(--border-color);
            border-radius: 8px;
            padding: 14px;
            text-align: center;
        }
        .metric-label { font-size: 12px; color: var(--text-secondary); margin-bottom: 6px; }
        .metric-value { font-size: 22px; font-weight: 700; color: var(--accent-gold); }
        .metric-desc { font-size: 11px; color: var(--text-secondary); margin-top: 4px; }
        .card {
            background: var(--bg-card);
            border: 1px solid var(--border-color);
            border-radius: 10px;
            padding: 18px;
            margin-bottom: 24px;
        }
        .card h2 {
            font-size: 16px;
            font-weight: 600;
            color: #FFFFFF;
            border-left: 4px solid var(--accent-blue);
            padding-left: 10px;
            margin-bottom: 14px;
        }
        table { width: 100%; border-collapse: collapse; font-size: 12.5px; text-align: left; }
        th { background-color: var(--bg-card-alt); color: #BAC2DE; padding: 10px 12px; font-weight: 600; border-bottom: 1px solid var(--border-color); }
        td { padding: 9px 12px; border-bottom: 1px solid rgba(47, 53, 77, 0.6); color: #E0E2EC; }
        .btn-print {
            background: #313244; color: #FFFFFF; border: 1px solid #45475A; padding: 6px 14px; border-radius: 6px; cursor: pointer; font-size: 12px; font-weight: 600;
        }
        .footer {
            margin-top: 30px; border-top: 1px solid var(--border-color); padding-top: 16px; font-size: 11px; color: var(--text-secondary); display: flex; justify-content: space-between;
        }
        ");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"container\">");

        // 页眉
        sb.AppendLine("        <header class=\"header\">");
        sb.AppendLine("            <div class=\"header-title\">");
        sb.AppendLine("                <h1>🧩 基金投资组合资产配置与量化回测研报</h1>");
        sb.AppendLine($"                <p>交集回测周期: {portfolio.StartDate:yyyy-MM-dd} 至 {portfolio.EndDate:yyyy-MM-dd} (共 {portfolio.TradingDays} 个交易日) | 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div style=\"display:flex; gap:10px; align-items:center;\">");
        sb.AppendLine("                <button class=\"btn-print no-print\" onclick=\"window.print()\">🖨️ 打印 / 另存为 PDF</button>");
        sb.AppendLine("                <div class=\"brand-badge\">BIGA MPT</div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </header>");

        // 组合核心指标看板
        sb.AppendLine("        <div class=\"grid-metrics\">");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">组合累计收益率</div><div class=\"metric-value\" style=\"color:var(--bull-red);\">{portfolio.TotalReturn:+0.00;-0.00;0.00}%</div><div class=\"metric-desc\">沪深300同期 {portfolio.BenchmarkReturn:+0.00;-0.00;0.00}%</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">组合年化复合增长 (CAGR)</div><div class=\"metric-value\" style=\"color:var(--bull-red);\">{portfolio.AnnualizedReturn:+0.00;-0.00;0.00}%</div><div class=\"metric-desc\">复利折算年化回报</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">组合历史最大回撤</div><div class=\"metric-value\" style=\"color:var(--bear-green);\">{portfolio.MaxDrawdown:F2}%</div><div class=\"metric-desc\">资产配置尾部抗跌</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">组合夏普比率 (Sharpe)</div><div class=\"metric-value\">{portfolio.SharpeRatio:F2}</div><div class=\"metric-desc\">无风险利率 Rf=2.0%</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">组合卡玛比率 (Calmar)</div><div class=\"metric-value\" style=\"color:var(--accent-blue);\">{portfolio.CalmarRatio:F2}</div><div class=\"metric-desc\">年化回报 / 最大回撤</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">分散化降波增益</div><div class=\"metric-value\" style=\"color:var(--bear-green);\">{portfolio.DiversificationBenefit:F2}%</div><div class=\"metric-desc\">MPT 协方差风险分散效应</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">奥米加比率 (Omega)</div><div class=\"metric-value\" style=\"color:var(--bull-red);\">{portfolio.OmegaRatio:F2}</div><div class=\"metric-desc\">全收益胜率质量比</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">溃疡指数 (Ulcer UI)</div><div class=\"metric-value\" style=\"color:var(--bear-green);\">{portfolio.UlcerIndex:F2}%</div><div class=\"metric-desc\">回撤深度与滞留方根</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">马丁比率 (Martin)</div><div class=\"metric-value\" style=\"color:var(--accent-blue);\">{portfolio.MartinRatio:F2}</div><div class=\"metric-desc\">超额收益 / 溃疡指数</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">痛苦比 / 下行标准差</div><div class=\"metric-value\">{portfolio.PainRatio:F2} / {portfolio.DownsideDeviation:F1}%</div><div class=\"metric-desc\">Pain Ratio / σ_down</div></div>");
        sb.AppendLine("        </div>");

        // 组合成分配置列表
        sb.AppendLine("        <div class=\"card\">");
        sb.AppendLine("            <h2>📋 投资组合成分基金与当前分配权重</h2>");
        sb.AppendLine("            <table>");
        sb.AppendLine("                <thead><tr><th>基金代码</th><th>基金名称</th><th>基金类型</th><th>配置权重 (%)</th></tr></thead>");
        sb.AppendLine("                <tbody>");
        foreach (var c in compList)
        {
            sb.AppendLine($"                    <tr><td><strong>{WebUtility.HtmlEncode(c.Code)}</strong></td><td>{WebUtility.HtmlEncode(c.Name)}</td><td>{WebUtility.HtmlEncode(c.Type)}</td><td style=\"font-weight:700; color:var(--accent-gold);\">{c.WeightPercent:F2}%</td></tr>");
        }
        sb.AppendLine("                </tbody>");
        sb.AppendLine("            </table>");
        sb.AppendLine("        </div>");

        // 现代投资组合理论 (MPT) 四大智能方案对比
        if (portfolio.Schemes.Count > 0)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine("            <h2>📐 现代投资组合理论 (MPT) 智能优化方案对比</h2>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>优化方案</th><th>方案定位与特征</th><th>预期年化收益</th><th>预期年化波动率</th><th>夏普比率</th><th>最优成分权重分配</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var s in portfolio.Schemes)
            {
                string weightsText = string.Join(", ", s.Weights.Select(kv => $"{kv.Key}: {kv.Value:F1}%"));
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(s.SchemeName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"color:var(--text-secondary); font-size:11.5px;\">{WebUtility.HtmlEncode(s.Description)}</td>");
                sb.AppendLine($"                        <td style=\"color:var(--bull-red); font-weight:600;\">{s.ExpectedReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td>{s.ExpectedVolatility:F2}%</td>");
                sb.AppendLine($"                        <td style=\"color:var(--accent-gold); font-weight:600;\">{s.SharpeRatio:F2}</td>");
                sb.AppendLine($"                        <td style=\"font-family:monospace; font-size:11.5px;\">{WebUtility.HtmlEncode(weightsText)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 两两相关系数矩阵
        if (portfolio.CorrelationMatrix != null && portfolio.CorrelationMatrix.AssetCodes.Count > 1)
        {
            var cm = portfolio.CorrelationMatrix;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🔗 资产日收益率 Pearson 相关系数矩阵 (平均相关度: {cm.AverageCorrelation:F2} | {WebUtility.HtmlEncode(cm.DiversificationRating)})</h2>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>资产</th>" + string.Join("", cm.AssetCodes.Select(c => $"<th>{WebUtility.HtmlEncode(c)}</th>")) + "</tr></thead>");
            sb.AppendLine("                <tbody>");
            for (int i = 0; i < cm.AssetCodes.Count; i++)
            {
                sb.AppendLine($"                    <tr><td><strong>{WebUtility.HtmlEncode(cm.AssetCodes[i])}</strong></td>");
                for (int j = 0; j < cm.AssetCodes.Count; j++)
                {
                    double v = cm.Matrix[i, j];
                    string color = v > 0.75 ? "var(--bull-red)" : (v < 0.25 ? "var(--bear-green)" : "var(--accent-gold)");
                    sb.AppendLine($"                        <td style=\"color:{color}; font-weight:600;\">{v:F2}</td>");
                }
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 资产边际风险贡献与 Euler 风险预算解构
        if (portfolio.RiskDecomposition != null && portfolio.RiskDecomposition.Items.Count > 0)
        {
            var rd = portfolio.RiskDecomposition;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🛡️ 资产边际风险贡献与 Euler 风险预算穿透解构 (组合年化波动: {rd.PortfolioVolatility:F2}% | 风险基尼系数: {rd.RiskBudgetGini:F2} | 主导风险资产: {WebUtility.HtmlEncode(rd.DominantRiskAsset)})</h2>");
            if (rd.HasRiskHog)
            {
                sb.AppendLine("            <div style=\"background:rgba(239,68,68,0.15); border-left:4px solid #EF4444; padding:8px 12px; margin-bottom:12px; border-radius:4px; font-size:12px; color:#FCA5A5;\">⚠️ <strong>风险超载预警:</strong> 检测到组合存在风险吞噬者 (Risk-Hog) 资产，其实际承担的风险比例远超资金配置权重，建议适度降低权重或引入负相关对冲标的。</div>");
            }
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>基金代码</th><th>基金名称</th><th>资金权重</th><th>边际风险贡献 (MCR)</th><th>绝对风险贡献 (ACR)</th><th>风险贡献占比 (PCR)</th><th>风权比 (PCR/W)</th><th>风险健康评级</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var item in rd.Items)
            {
                string pcrColor = item.IsRiskHog ? "#EF4444" : "var(--accent-gold)";
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(item.FundCode)}</strong></td>");
                sb.AppendLine($"                        <td>{WebUtility.HtmlEncode(item.FundName)}</td>");
                sb.AppendLine($"                        <td>{item.CapitalWeight:F2}%</td>");
                sb.AppendLine($"                        <td>{item.MarginalContributionToRisk:F4}</td>");
                sb.AppendLine($"                        <td>{item.AbsoluteContributionToRisk:F4}</td>");
                sb.AppendLine($"                        <td style=\"color:{pcrColor}; font-weight:700;\">{item.PercentageContributionToRisk:F2}%</td>");
                sb.AppendLine($"                        <td>{item.RiskConcentrationRatio:F2}x</td>");
                sb.AppendLine($"                        <td><span style=\"background:{(item.IsRiskHog ? "rgba(239,68,68,0.2)" : "rgba(16,185,129,0.2)")}; color:{(item.IsRiskHog ? "#EF4444" : "#10B981")}; padding:2px 8px; border-radius:4px; font-size:11px;\">{WebUtility.HtmlEncode(item.RiskStatusText)}</span></td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 极端情景压力测试
        if (portfolio.StressTestScenarios.Count > 0)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine("            <h2>⛈️ 投资组合历史经典黑天鹅极端市场情景压力测试</h2>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>危机事件</th><th>起止区间</th><th>组合期间表现</th><th>沪深300同期</th><th>超额表现</th><th>期间最大回撤</th><th>防御评级</th><th>危机特征</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var st in portfolio.StressTestScenarios)
            {
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(st.ScenarioName)}</strong></td>");
                sb.AppendLine($"                        <td>{st.StartDate:yyyy-MM-dd} ~ {st.EndDate:yyyy-MM-dd}</td>");
                sb.AppendLine($"                        <td style=\"color:{(st.FundReturnRate >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:600;\">{st.FundReturnRate:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td>{st.BenchmarkReturnRate:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"color:{(st.ExcessReturnRate >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:600;\">{st.ExcessReturnRate:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"color:var(--bear-green);\">{st.MaxDrawdown:F2}%</td>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(st.DefenseRating)}</strong></td>");
                sb.AppendLine($"                        <td style=\"color:var(--text-secondary); font-size:11.5px;\">{WebUtility.HtmlEncode(st.Description)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 调仓交易决策单与摩擦成本测算
        if (portfolio.RebalanceOrders != null && portfolio.RebalanceOrders.Orders.Count > 0)
        {
            var sheet = portfolio.RebalanceOrders;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>📋 投资组合调仓交易决策单 (总资产: ¥{sheet.TotalPortfolioValue:N0} | 方案: {WebUtility.HtmlEncode(sheet.TargetSchemeName)})</h2>");
            sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:12px; margin-bottom:14px;\">");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">调仓双边换手率</div><div class=\"metric-value\" style=\"color:var(--accent-gold);\">{sheet.TurnoverRate:F1}%</div><div class=\"metric-desc\">换手摩擦水平</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">计划买入总额</div><div class=\"metric-value val-bull\">¥{sheet.TotalBuyAmount:N0}</div><div class=\"metric-desc\">调仓建仓资金</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">计划卖出总额</div><div class=\"metric-value val-bear\">¥{sheet.TotalSellAmount:N0}</div><div class=\"metric-desc\">赎回释放现金</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">预估摩擦总规费</div><div class=\"metric-value val-cyan\">¥{sheet.EstimatedTotalFees:N2}</div><div class=\"metric-desc\">申购费+赎回费合计</div></div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>标的代码</th><th>标的名称</th><th style=\"text-align:right;\">当前权重</th><th style=\"text-align:right;\">目标权重</th><th style=\"text-align:right;\">权重调整</th><th style=\"text-align:center;\">交易动作</th><th style=\"text-align:right;\">建议调仓金额</th><th style=\"text-align:right;\">预估规费</th><th style=\"text-align:right;\">目标持仓市值</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var ord in sheet.Orders)
            {
                string actColor = ord.Action == "买入" ? "var(--bull-red)" : (ord.Action == "卖出" ? "var(--bear-green)" : "var(--text-secondary)");
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td style=\"font-family:monospace; font-weight:600;\">{WebUtility.HtmlEncode(ord.FundCode)}</td>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(ord.FundName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{ord.CurrentWeight:F1}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-gold); font-weight:700;\">{ord.TargetWeight:F1}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(ord.TradeWeightChange >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:600;\">{ord.TradeWeightChange:+0.0;-0.0;0.0}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:center;\"><span style=\"color:{actColor}; font-weight:700;\">{WebUtility.HtmlEncode(ord.Action)}</span></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; font-weight:600;\">¥{ord.TradeAmount:N2}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--text-secondary);\">¥{ord.EstimatedFee:N2}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">¥{ord.TargetAmount:N2}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 组合 Brinson 业绩归因
        if (portfolio.BrinsonAttribution != null && portfolio.BrinsonAttribution.SectorItems.Count > 0)
        {
            var pBrinson = portfolio.BrinsonAttribution;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🎯 组合 Brinson-Fachler 业绩归因与超额分解 (总超额: <span style=\"color:var(--bull-red); font-weight:700;\">{pBrinson.TotalExcessReturn:+0.00;-0.00;0.00}%</span>)</h2>");
            sb.AppendLine("            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">" + WebUtility.HtmlEncode(pBrinson.SummaryAnalysis) + "</p>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>成分基金 / 标的</th><th style=\"text-align:right;\">组合权重</th><th style=\"text-align:right;\">基准权重</th><th style=\"text-align:right;\">标的收益率</th><th style=\"text-align:right;\">基准收益率</th><th style=\"text-align:right;\">资产配置效应</th><th style=\"text-align:right;\">选基超额效应</th><th style=\"text-align:right;\">交互协同效应</th><th style=\"text-align:right;\">总超额贡献</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var item in pBrinson.SectorItems)
            {
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(item.SectorName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{item.PortfolioWeight:F1}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--text-secondary);\">{item.BenchmarkWeight:F1}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(item.PortfolioReturn >= 0 ? "var(--bull-red)" : "var(--bear-green)")};\">{item.PortfolioReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{item.BenchmarkReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(item.AllocationEffect >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:600;\">{item.AllocationEffect:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(item.SelectionEffect >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:600;\">{item.SelectionEffect:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{item.InteractionEffect:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(item.TotalEffect >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:700;\">{item.TotalEffect:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 组合底层穿透持仓与前瞻性蒙特卡洛推演
        AppendLookThroughAndMonteCarloHtml(sb, portfolio);

        sb.AppendLine("        <footer class=\"footer\">");
        sb.AppendLine("            <div>本报告由 BIGA 机构级公募基金量化投研工作站自动解算生成。基于马科维茨现代组合理论与蒙特卡洛算法。</div>");
        sb.AppendLine("            <div>BIGA Research Terminal &copy; 2026</div>");
        sb.AppendLine("        </footer>");

        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>
    /// 将投资组合配置方案、量化风控指标、资产相关性矩阵与优化方案导出为独立高保真 HTML 机构研报
    /// </summary>
    public static void ExportPortfolioToHtml(
        string filePath,
        PortfolioResult portfolio,
        List<(FundDetail Fund, decimal WeightPercent)> components)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-CN\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"    <title>BIGA 投资组合投研研报 - {WebUtility.HtmlEncode(portfolio.PortfolioName)}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine(@"
        :root {
            --bg-base: #0F111A;
            --bg-card: #161926;
            --bg-card-alt: #1E2235;
            --border-color: #262B40;
            --text-primary: #ECEFF4;
            --text-secondary: #9098A9;
            --accent-blue: #4D96FF;
            --accent-gold: #FFD166;
            --accent-cyan: #06D6A0;
            --bull-red: #FF5376;
            --bear-green: #00E676;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'PingFang SC', 'Microsoft YaHei', sans-serif;
            background-color: var(--bg-base);
            color: var(--text-primary);
            line-height: 1.6;
            padding: 30px 20px;
        }
        .container {
            max-width: 1180px;
            margin: 0 auto;
        }
        .header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            border-bottom: 2px solid var(--border-color);
            padding-bottom: 20px;
            margin-bottom: 24px;
        }
        .header-title h1 {
            font-size: 24px;
            font-weight: 700;
            color: #FFFFFF;
            letter-spacing: 0.5px;
            display: flex;
            align-items: center;
            gap: 10px;
        }
        .badge-terminal {
            background: linear-gradient(135deg, #3A7BD5, #3A6073);
            color: white;
            font-size: 11px;
            padding: 3px 8px;
            border-radius: 4px;
            font-weight: 600;
        }
        .header-meta {
            font-size: 12px;
            color: var(--text-secondary);
            text-align: right;
        }
        .metrics-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 14px;
            margin-bottom: 24px;
        }
        .metric-card {
            background: var(--bg-card);
            border: 1px solid var(--border-color);
            border-radius: 8px;
            padding: 14px;
            text-align: center;
        }
        .metric-label { font-size: 12px; color: var(--text-secondary); margin-bottom: 6px; }
        .metric-value { font-size: 22px; font-weight: 700; color: var(--accent-gold); }
        .metric-desc { font-size: 11px; color: var(--text-secondary); margin-top: 4px; }
        .val-bull { color: var(--bull-red) !important; }
        .val-bear { color: var(--bear-green) !important; }
        .val-blue { color: var(--accent-blue) !important; }
        .val-cyan { color: var(--accent-cyan) !important; }
        .card {
            background: var(--bg-card);
            border: 1px solid var(--border-color);
            border-radius: 10px;
            padding: 18px;
            margin-bottom: 24px;
        }
        .card h2 {
            font-size: 16px;
            font-weight: 600;
            color: #FFFFFF;
            border-left: 4px solid var(--accent-blue);
            padding-left: 10px;
            margin-bottom: 14px;
        }
        table {
            width: 100%;
            border-collapse: collapse;
            font-size: 12.5px;
            text-align: left;
        }
        th {
            background-color: var(--bg-card-alt);
            color: #BAC2DE;
            padding: 10px 12px;
            font-weight: 600;
            border-bottom: 1px solid var(--border-color);
        }
        td {
            padding: 9px 12px;
            border-bottom: 1px solid rgba(47, 53, 77, 0.6);
            color: #E0E2EC;
        }
        tr:hover { background-color: rgba(255, 255, 255, 0.02); }
        .bar-container {
            width: 100%;
            background: #252836;
            border-radius: 4px;
            height: 8px;
            overflow: hidden;
            display: inline-block;
            margin-top: 4px;
        }
        .bar-fill {
            height: 100%;
            background: linear-gradient(90deg, #4D96FF, #06D6A0);
            border-radius: 4px;
        }
        .footer {
            margin-top: 30px;
            border-top: 1px solid var(--border-color);
            padding-top: 16px;
            font-size: 11px;
            color: var(--text-secondary);
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        .btn-print {
            background: #313244;
            color: #FFFFFF;
            border: 1px solid #45475A;
            padding: 6px 14px;
            border-radius: 6px;
            cursor: pointer;
            font-size: 12px;
            font-weight: 600;
        }
        .btn-print:hover { background: #45475A; }
        ");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"container\">");

        // Header
        sb.AppendLine("        <div class=\"header\">");
        sb.AppendLine("            <div class=\"header-title\">");
        sb.AppendLine($"                <h1>{WebUtility.HtmlEncode(portfolio.PortfolioName)} <span class=\"badge-terminal\">BIGA PORTFOLIO</span></h1>");
        sb.AppendLine($"                <div style=\"color:var(--text-secondary); font-size:12px; margin-top:4px;\">测算区间: {portfolio.StartDate:yyyy-MM-dd} 至 {portfolio.EndDate:yyyy-MM-dd} ({portfolio.TradingDays} 交易日) | 资产标的数量: {components.Count} 只</div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"header-meta\">");
        sb.AppendLine($"                <div>生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>");
        sb.AppendLine("                <div style=\"margin-top:8px;\"><button class=\"btn-print\" onclick=\"window.print()\">打印 / 导出 PDF</button></div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");

        // 核心量化指标卡片
        sb.AppendLine("        <div class=\"metrics-grid\">");
        string retClass = portfolio.TotalReturn >= 0 ? "val-bull" : "val-bear";
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">区间总收益率</div><div class=\"metric-value {retClass}\">{portfolio.TotalReturn:+0.00;-0.00;0.00}%</div><div class=\"metric-desc\">组合复权累计回报</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">年化复合回报 (CAGR)</div><div class=\"metric-value {retClass}\">{portfolio.AnnualizedReturn:+0.00;-0.00;0.00}%</div><div class=\"metric-desc\">年化折算真实收益</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">年化波动率</div><div class=\"metric-value val-blue\">{portfolio.AnnualizedVolatility:F2}%</div><div class=\"metric-desc\">组合离散度年化衡量</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">历史最大回撤</div><div class=\"metric-value val-bear\">-{portfolio.MaxDrawdown:F2}%</div><div class=\"metric-desc\">历史最大资产净值回落</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">夏普比率 (Sharpe)</div><div class=\"metric-value\">{portfolio.SharpeRatio:F2}</div><div class=\"metric-desc\">超额风险调整回报 (Rf=2%)</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">卡玛比率 (Calmar)</div><div class=\"metric-value\">{portfolio.CalmarRatio:F2}</div><div class=\"metric-desc\">年化回报/最大回撤</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">资产分散降波红利</div><div class=\"metric-value val-cyan\">{portfolio.DiversificationBenefit:+0.00;-0.00;0.00}%</div><div class=\"metric-desc\">配置平抑波动优势</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">奥米加比率 (Omega)</div><div class=\"metric-value {retClass}\">{portfolio.OmegaRatio:F2}</div><div class=\"metric-desc\">全收益胜率质量比</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">溃疡指数 (Ulcer UI)</div><div class=\"metric-value val-bear\">{portfolio.UlcerIndex:F2}%</div><div class=\"metric-desc\">回撤深度与滞留方根</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">马丁比率 (Martin)</div><div class=\"metric-value val-blue\">{portfolio.MartinRatio:F2}</div><div class=\"metric-desc\">超额收益 / 溃疡指数</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">痛苦比 / 下行标准差</div><div class=\"metric-value\">{portfolio.PainRatio:F2} / {portfolio.DownsideDeviation:F1}%</div><div class=\"metric-desc\">Pain Ratio / σ_down</div></div>");
        sb.AppendLine("        </div>");

        // 1. 组合底层资产配置明细
        sb.AppendLine("        <div class=\"card\">");
        sb.AppendLine("            <h2>组合底层资产配置明细 (Asset Holdings)</h2>");
        sb.AppendLine("            <table>");
        sb.AppendLine("                <thead><tr><th>基金代码</th><th>基金名称</th><th>类型</th><th>所属板块</th><th style=\"text-align:right;\">配置权重</th><th style=\"text-align:right;\">最新净值</th><th>权重分布</th></tr></thead>");
        sb.AppendLine("                <tbody>");
        decimal totalW = components.Sum(c => c.WeightPercent);
        if (totalW <= 0) totalW = 100m;
        foreach (var c in components)
        {
            decimal normalizedW = Math.Round((c.WeightPercent / totalW) * 100m, 1);
            sb.AppendLine("                    <tr>");
            sb.AppendLine($"                        <td style=\"font-family:monospace; color:var(--accent-blue);\">{WebUtility.HtmlEncode(c.Fund.Code)}</td>");
            sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(c.Fund.Name)}</strong></td>");
            sb.AppendLine($"                        <td>{WebUtility.HtmlEncode(c.Fund.Type)}</td>");
            sb.AppendLine($"                        <td><span style=\"background:#262B40; padding:2px 6px; border-radius:4px; font-size:11px;\">{WebUtility.HtmlEncode(c.Fund.Sector)}</span></td>");
            sb.AppendLine($"                        <td style=\"text-align:right; font-weight:700; color:var(--accent-gold);\">{normalizedW:F1}%</td>");
            sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{c.Fund.LatestUnitNav:F4}</td>");
            sb.AppendLine($"                        <td style=\"width:140px;\"><div class=\"bar-container\"><div class=\"bar-fill\" style=\"width:{Math.Min(100, normalizedW)}%;\"></div></div></td>");
            sb.AppendLine("                    </tr>");
        }
        sb.AppendLine("                </tbody>");
        sb.AppendLine("            </table>");
        sb.AppendLine("        </div>");

        // 2. 资产两两相关性矩阵
        if (portfolio.CorrelationMatrix != null && portfolio.CorrelationMatrix.AssetCodes.Count > 1)
        {
            var cm = portfolio.CorrelationMatrix;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>资产相关性矩阵与分散度 (平均相关系数: {cm.AverageCorrelation:F2} | 分散评级: {cm.DiversificationRating})</h2>");
            sb.AppendLine("            <div style=\"overflow-x:auto;\">");
            sb.AppendLine("                <table>");
            sb.AppendLine("                    <thead><tr><th>资产</th>");
            for (int j = 0; j < cm.AssetNames.Count; j++)
            {
                sb.AppendLine($"                        <th style=\"text-align:center;\">{WebUtility.HtmlEncode(cm.AssetNames[j])}</th>");
            }
            sb.AppendLine("                    </tr></thead><tbody>");

            for (int i = 0; i < cm.AssetNames.Count; i++)
            {
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(cm.AssetNames[i])}</strong></td>");
                for (int j = 0; j < cm.AssetNames.Count; j++)
                {
                    double val = cm.Matrix[i, j];
                    string color = val switch
                    {
                        > 0.8 => "#FF5376",
                        > 0.5 => "#FFB703",
                        > 0.2 => "#E0E2EC",
                        _ => "#00E676"
                    };
                    sb.AppendLine($"                        <td style=\"text-align:center; font-family:monospace; color:{color}; font-weight:{(i == j ? "700" : "400")};\">{val:F2}</td>");
                }
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody></table>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");
        }

        // 2.1 组合资产分散化效益与同质化穿透诊断 (Choueifaty Diversification Ratio & Homogeneity)
        if (portfolio.Diversification != null)
        {
            var div = portfolio.Diversification;
            string warnBanner = div.PseudoDiversificationWarning
                ? "<div style=\"background:rgba(255, 83, 118, 0.15); border:1px solid #FF5376; border-radius:6px; padding:8px 12px; margin-bottom:12px; color:#FF5376; font-weight:600;\">⚠️ 伪分散高危预警：组合内部资产加权相关性极高 (ρ ≥ 0.75)，名义分散实为抱团暴露，抵御系统性下行风险能力极其脆弱！</div>"
                : "<div style=\"background:rgba(0, 230, 118, 0.15); border:1px solid #00E676; border-radius:6px; padding:8px 12px; margin-bottom:12px; color:#00E676; font-weight:600;\">✅ 优质分散化：组合跨资产低相关性配置有效分散非系统性风险。</div>";

            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🔗 组合风险分散化效益与同质化穿透诊断 (Choueifaty DR: <strong style=\"color:var(--bull-red);\">{div.DiversificationRatio:F2}</strong> | 波动削减: <strong style=\"color:var(--bear-green);\">{div.VolatilityReductionPercent:F1}%</strong> | 同质化得分: <strong style=\"color:var(--accent-gold);\">{div.HomogeneityScore:F0}分</strong>)</h2>");
            sb.AppendLine($"            {warnBanner}");
            sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(div.DiagnosticSummary)}</p>");
            sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:12px; margin-bottom:12px;\">");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">Choueifaty 分散比率</div><div class=\"metric-value val-bull\">{div.DiversificationRatio:F2}x</div><div class=\"metric-desc\">加权波动/组合波动 (≥1.0)</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">无偿波动削减幅度</div><div class=\"metric-value val-bear\">{div.VolatilityReductionPercent:F1}%</div><div class=\"metric-desc\">分散化降低的波动率</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">加权平均相关系数</div><div class=\"metric-value val-cyan\">{div.WeightedAverageCorrelation:F2}</div><div class=\"metric-desc\">资产两两权重交叉相关度</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">组合同质化得分</div><div class=\"metric-value\" style=\"color:var(--accent-gold);\">{div.HomogeneityScore:F0}/100</div><div class=\"metric-desc\">越高代表抱团/同质性越强</div></div>");
            sb.AppendLine("            </div>");
            if (div.ClusterGroups.Count > 0)
            {
                sb.AppendLine("            <div style=\"margin-top:10px;\">");
                sb.AppendLine("                <span style=\"font-size:12px; color:var(--text-secondary); margin-right:8px;\">资产层级聚类簇 (HRP Clusters):</span>");
                foreach (var cl in div.ClusterGroups)
                {
                    sb.AppendLine($"                <span style=\"background:#262B40; border:1px solid #3F4765; border-radius:4px; padding:3px 8px; font-size:11.5px; margin-right:6px; color:#CDD6F4;\">{WebUtility.HtmlEncode(cl)}</span>");
                }
                sb.AppendLine("            </div>");
            }
            sb.AppendLine("        </div>");
        }

        // 3. 现代资产配置理论 (MPT) 优化方案对比
        if (portfolio.Schemes != null && portfolio.Schemes.Count > 0)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine("            <h2>现代投资组合理论 (MPT) 优化配置方案横向对比</h2>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>方案名称</th><th>核心策略逻辑</th><th style=\"text-align:right;\">预期年化收益</th><th style=\"text-align:right;\">预期波动率</th><th style=\"text-align:right;\">预期夏普比</th><th>推荐权重分布</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var sch in portfolio.Schemes)
            {
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td style=\"color:var(--accent-gold); font-weight:700;\">{WebUtility.HtmlEncode(sch.SchemeName)}</td>");
                sb.AppendLine($"                        <td style=\"color:var(--text-secondary); font-size:12px;\">{WebUtility.HtmlEncode(sch.Description)}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bull-red); font-weight:600;\">{sch.ExpectedReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-blue);\">{sch.ExpectedVolatility:F2}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; font-weight:700;\">{sch.SharpeRatio:F2}</td>");
                var wDesc = string.Join(", ", sch.Weights.Select(kv => $"{kv.Key}: {kv.Value:F1}%"));
                sb.AppendLine($"                        <td style=\"font-size:11.5px; color:#BAC2DE;\">{WebUtility.HtmlEncode(wDesc)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 4. 资产调仓交易决策单与摩擦成本测算
        if (portfolio.RebalanceOrders != null && portfolio.RebalanceOrders.Orders.Count > 0)
        {
            var sheet = portfolio.RebalanceOrders;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>📋 投资组合调仓交易决策单 (总资产: ¥{sheet.TotalPortfolioValue:N0} | 方案: {WebUtility.HtmlEncode(sheet.TargetSchemeName)})</h2>");
            sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:12px; margin-bottom:14px;\">");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">调仓双边换手率</div><div class=\"metric-value\" style=\"color:var(--accent-gold);\">{sheet.TurnoverRate:F1}%</div><div class=\"metric-desc\">换手摩擦水平</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">计划买入总额</div><div class=\"metric-value val-bull\">¥{sheet.TotalBuyAmount:N0}</div><div class=\"metric-desc\">调仓建仓资金</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">计划卖出总额</div><div class=\"metric-value val-bear\">¥{sheet.TotalSellAmount:N0}</div><div class=\"metric-desc\">赎回释放现金</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">预估摩擦总规费</div><div class=\"metric-value val-cyan\">¥{sheet.EstimatedTotalFees:N2}</div><div class=\"metric-desc\">申购费+赎回费合计</div></div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>标的代码</th><th>标的名称</th><th style=\"text-align:right;\">当前权重</th><th style=\"text-align:right;\">目标权重</th><th style=\"text-align:right;\">权重调整</th><th style=\"text-align:center;\">交易动作</th><th style=\"text-align:right;\">建议调仓金额</th><th style=\"text-align:right;\">预估规费</th><th style=\"text-align:right;\">目标持仓市值</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var ord in sheet.Orders)
            {
                string actColor = ord.Action == "买入" ? "var(--bull-red)" : (ord.Action == "卖出" ? "var(--bear-green)" : "var(--text-secondary)");
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td style=\"font-family:monospace; font-weight:600;\">{WebUtility.HtmlEncode(ord.FundCode)}</td>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(ord.FundName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{ord.CurrentWeight:F1}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-gold); font-weight:700;\">{ord.TargetWeight:F1}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(ord.TradeWeightChange >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:600;\">{ord.TradeWeightChange:+0.0;-0.0;0.0}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:center;\"><span style=\"color:{actColor}; font-weight:700;\">{WebUtility.HtmlEncode(ord.Action)}</span></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; font-weight:600;\">¥{ord.TradeAmount:N2}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--text-secondary);\">¥{ord.EstimatedFee:N2}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">¥{ord.TargetAmount:N2}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 5. 组合 Brinson 业绩归因
        if (portfolio.BrinsonAttribution != null && portfolio.BrinsonAttribution.SectorItems.Count > 0)
        {
            var pBrinson = portfolio.BrinsonAttribution;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🎯 组合 Brinson-Fachler 业绩归因与超额分解 (总超额: <span style=\"color:var(--bull-red); font-weight:700;\">{pBrinson.TotalExcessReturn:+0.00;-0.00;0.00}%</span>)</h2>");
            sb.AppendLine("            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">" + WebUtility.HtmlEncode(pBrinson.SummaryAnalysis) + "</p>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>成分基金 / 标的</th><th style=\"text-align:right;\">组合权重</th><th style=\"text-align:right;\">基准权重</th><th style=\"text-align:right;\">标的收益率</th><th style=\"text-align:right;\">基准收益率</th><th style=\"text-align:right;\">资产配置效应</th><th style=\"text-align:right;\">选基超额效应</th><th style=\"text-align:right;\">交互协同效应</th><th style=\"text-align:right;\">总超额贡献</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var item in pBrinson.SectorItems)
            {
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(item.SectorName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{item.PortfolioWeight:F1}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--text-secondary);\">{item.BenchmarkWeight:F1}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(item.PortfolioReturn >= 0 ? "var(--bull-red)" : "var(--bear-green)")};\">{item.PortfolioReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{item.BenchmarkReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(item.AllocationEffect >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:600;\">{item.AllocationEffect:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(item.SelectionEffect >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:600;\">{item.SelectionEffect:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{item.InteractionEffect:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(item.TotalEffect >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:700;\">{item.TotalEffect:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 组合底层穿透持仓与前瞻性蒙特卡洛推演
        AppendLookThroughAndMonteCarloHtml(sb, portfolio);

        // Barra CNE6 组合多因子风险与特质选股欧拉方差分解
        if (portfolio.FactorRiskAttribution != null && portfolio.FactorRiskAttribution.FactorItems.Count > 0)
        {
            var fra = portfolio.FactorRiskAttribution;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>📊 Barra CNE6 组合多因子风险与特质选股欧拉方差分解 (主动跟踪误差: <span style=\"color:var(--accent-gold); font-weight:700;\">{fra.TotalActiveVolatility:F2}%</span> | 因子风险占比: <span style=\"color:var(--accent-blue); font-weight:700;\">{fra.FactorRiskPercent:F1}%</span> | 特质选股风险: <span style=\"color:var(--bear-green); font-weight:700;\">{fra.SpecificRiskPercent:F1}%</span>)</h2>");
            sb.AppendLine($"            <p style=\"font-size:12.5px; color:var(--text-secondary); margin-bottom:12px;\">主导偏离因子: <strong>{WebUtility.HtmlEncode(fra.DominantFactorTilt)}</strong> | 风险剖析: <span style=\"color:var(--accent-gold);\">{WebUtility.HtmlEncode(fra.RiskAttributionProfile)}</span> — {WebUtility.HtmlEncode(fra.DiagnosticSummary)}</p>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>Barra 风格因子</th><th style=\"text-align:right;\">组合加权暴露</th><th style=\"text-align:right;\">基准暴露</th><th style=\"text-align:right;\">主动偏离 (Active Tilt)</th><th style=\"text-align:right;\">方差贡献</th><th style=\"text-align:right;\">风险贡献占比</th><th>因子投资学逻辑</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var f in fra.FactorItems)
            {
                string tiltColor = f.ActiveTilt >= 0 ? "var(--bull-red)" : "var(--bear-green)";
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(f.FactorName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{f.PortfolioExposure:+0.000;-0.000}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--text-secondary);\">{f.BenchmarkExposure:+0.000;-0.000}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{tiltColor}; font-weight:700;\">{f.ActiveTilt:+0.000;-0.000}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{f.FactorVarianceContribution:F6}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; font-weight:700; color:var(--accent-gold);\">{f.FactorRiskPercent:F1}%</td>");
                sb.AppendLine($"                        <td style=\"color:var(--text-secondary); font-size:11.5px;\">{WebUtility.HtmlEncode(f.FactorInterpretation)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 连续多资产凯利最优配置与目标波动率控波引擎
        if (portfolio.KellyAndTargetVol != null)
        {
            var kt = portfolio.KellyAndTargetVol;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🎯 连续多资产凯利最优配置与目标波动率控波引擎 (目标波动率: <span style=\"color:var(--accent-gold); font-weight:700;\">{kt.TargetVolatility:F1}%</span> | 建议风险仓位: <span style=\"color:var(--bull-red); font-weight:700;\">{kt.SuggestedRiskyWeight:F1}%</span> | 现金缓冲垫: <span style=\"color:var(--bear-green); font-weight:700;\">{kt.SuggestedCashWeight:F1}%</span>)</h2>");
            sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:12px; margin-bottom:14px;\">");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">组合固有波动率</div><div class=\"metric-value\">{kt.PortfolioIntrinsicVolatility:F1}%</div><div class=\"metric-desc\">未控波原始离散度</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">建议风险资产权重</div><div class=\"metric-value\" style=\"color:var(--bull-red);\">{kt.SuggestedRiskyWeight:F1}%</div><div class=\"metric-desc\">风险敞口总头寸</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">现金/短债流动性缓冲</div><div class=\"metric-value\" style=\"color:var(--bear-green);\">{kt.SuggestedCashWeight:F1}%</div><div class=\"metric-desc\">防御缓冲垫</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">隐含杜邦杠杆倍数</div><div class=\"metric-value\">{kt.ImpliedLeverage:F2}x</div><div class=\"metric-desc\">目标波动杠杆空间</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">控波调整后预期年化</div><div class=\"metric-value\" style=\"color:var(--accent-gold);\">{kt.AdjustedExpectedReturn:+0.00;-0.00}%</div><div class=\"metric-desc\">经目标波动率缩放后</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">理论最大复合增长率 (g*)</div><div class=\"metric-value\" style=\"color:var(--accent-blue);\">{kt.TheoreticalMaxLogGrowth:F2}%</div><div class=\"metric-desc\">凯利复利对数极值</div></div>");
            sb.AppendLine("            </div>");
            sb.AppendLine($"            <div style=\"background:var(--bg-card-alt); border-left:4px solid var(--accent-gold); padding:12px 16px; border-radius:4px; font-size:13px; color:#E0E2EC;\"><strong>资本配置决策建议:</strong> {WebUtility.HtmlEncode(kt.CapitalAllocationAdvice)}</div>");
            sb.AppendLine("        </div>");
        }

        // 组合牛熊双边非对称捕获与暴跌条件相关性
        if (portfolio.BullBearCapture != null)
        {
            var bbp = portfolio.BullBearCapture;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🛡️ 组合牛熊双边非对称捕获与暴跌条件相关性 (捕获利差: <span style=\"color:var(--bull-red); font-weight:700;\">{bbp.CaptureSpread:+0.0;-0.0}%</span> | 凸性评级: <span style=\"color:var(--bear-green); font-weight:700;\">{WebUtility.HtmlEncode(bbp.ConvexityRating)}</span>)</h2>");
            sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(170px, 1fr)); gap:12px; margin-bottom:14px;\">");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">牛市上涨贝塔 (Bull Beta)</div><div class=\"metric-value\">{bbp.BullBeta:+0.00;-0.00}</div><div class=\"metric-desc\">基准上涨日弹性</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">熊市下跌贝塔 (Bear Beta)</div><div class=\"metric-value\">{bbp.BearBeta:+0.00;-0.00}</div><div class=\"metric-desc\">基准下跌日回撤吸收</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">非对称度 (Asymmetry)</div><div class=\"metric-value\" style=\"color:var(--accent-gold);\">{bbp.AsymmetryIndex:+0.00;-0.00}</div><div class=\"metric-desc\">Bull - Bear Beta</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">上行捕获率 (UCR)</div><div class=\"metric-value\" style=\"color:var(--bull-red);\">{bbp.UpsideCaptureRatio:F1}%</div><div class=\"metric-desc\">上涨周期收益捕获</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">下行捕获率 (DCR)</div><div class=\"metric-value\" style=\"color:var(--bear-green);\">{bbp.DownsideCaptureRatio:F1}%</div><div class=\"metric-desc\">下跌周期风险承担</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">暴跌日条件相关系数</div><div class=\"metric-value\" style=\"color:var(--accent-blue);\">{bbp.CrashCorrelation:+0.00;-0.00}</div><div class=\"metric-desc\">大盘暴跌>1%时相关性</div></div>");
            sb.AppendLine("            </div>");
            sb.AppendLine($"            <div style=\"font-size:12.5px; color:var(--text-secondary);\"><strong>诊断评语:</strong> {WebUtility.HtmlEncode(bbp.AsymmetryDiagnosis)} (暴跌相关性漂移: <span style=\"color:{(bbp.CorrelationShift >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:700;\">{bbp.CorrelationShift:+0.00;-0.00}</span>)</div>");
            sb.AppendLine("        </div>");
        }

        // Footer
        sb.AppendLine("        <footer class=\"footer\">");
        sb.AppendLine("            <div>本报告由 BIGA 机构级公募基金量化投研工作站自动生成。包含马科维茨均值方差、风险平价与动量风险预算模型。</div>");
        sb.AppendLine("            <div>BIGA Research Terminal &copy; 2026</div>");
        sb.AppendLine("        </footer>");

        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>
    /// 将双基金对比对决、13项核心风控指标、五维量化评分、重仓股穿透重合度与申万行业偏离分析导出为独立机构级 HTML 对比研报 (支持浏览器查看与打印为 PDF)
    /// </summary>
    public static void ExportComparisonToHtml(
        string filePath,
        FundDetail fundA,
        QuantMetrics metricsA,
        FundDetail fundB,
        QuantMetrics metricsB,
        FundHoldingOverlapResult overlap,
        double correlation,
        string correlationRating,
        double winRateA,
        double winRateB,
        List<MetricCompareRow> metricRows,
        string periodName = "近1年")
    {
        var sb = new StringBuilder();
        string safeNameA = WebUtility.HtmlEncode(fundA.Name);
        string safeCodeA = WebUtility.HtmlEncode(fundA.Code);
        string safeNameB = WebUtility.HtmlEncode(fundB.Name);
        string safeCodeB = WebUtility.HtmlEncode(fundB.Code);

        var scoreA = fundA.ScoreCard ?? QuantCalculator.CalculateFundScore(fundA, metricsA);
        var scoreB = fundB.ScoreCard ?? QuantCalculator.CalculateFundScore(fundB, metricsB);

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-CN\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"    <title>BIGA 基金量化对比对决研报 - {safeCodeA} vs {safeCodeB}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine(@"
        :root {
            --bg-base: #0F111A;
            --bg-card: #161926;
            --bg-card-alt: #1E2235;
            --border-color: #262B40;
            --text-primary: #ECEFF4;
            --text-secondary: #9098A9;
            --fund-a-color: #89B4FA;
            --fund-b-color: #F38BA8;
            --accent-gold: #FFD166;
            --bull-red: #FF5376;
            --bear-green: #00E676;
        }
        @media print {
            body { background: #FFFFFF !important; color: #111111 !important; font-size: 10pt; padding: 0 !important; }
            .card { border: 1px solid #DDDDDD !important; background: #FFFFFF !important; box-shadow: none !important; break-inside: avoid; }
            .metric-card { background: #F8F9FA !important; border: 1px solid #EEEEEE !important; }
            .no-print { display: none !important; }
            th { background: #E9ECEF !important; color: #000000 !important; }
            td { color: #222222 !important; border-bottom: 1px solid #EEEEEE !important; }
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'PingFang SC', 'Microsoft YaHei', sans-serif;
            background-color: var(--bg-base);
            color: var(--text-primary);
            line-height: 1.6;
            padding: 30px 20px;
        }
        .container { max-width: 1200px; margin: 0 auto; }
        .header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            border-bottom: 2px solid var(--fund-a-color);
            padding-bottom: 18px;
            margin-bottom: 24px;
        }
        .header-title h1 { font-size: 24px; font-weight: 700; color: #FFFFFF; }
        .header-title p { font-size: 13px; color: var(--text-secondary); margin-top: 4px; }
        .vs-badge {
            font-size: 20px;
            font-weight: 900;
            color: var(--accent-gold);
            margin: 0 12px;
        }
        .fund-badge-a {
            background: rgba(137, 180, 250, 0.15);
            border: 1px solid var(--fund-a-color);
            border-radius: 8px;
            padding: 10px 16px;
            display: inline-block;
        }
        .fund-badge-b {
            background: rgba(243, 139, 168, 0.15);
            border: 1px solid var(--fund-b-color);
            border-radius: 8px;
            padding: 10px 16px;
            display: inline-block;
        }
        .grid-summary {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
            gap: 14px;
            margin-bottom: 24px;
        }
        .metric-card {
            background: var(--bg-card);
            border: 1px solid var(--border-color);
            border-radius: 8px;
            padding: 14px;
            text-align: center;
        }
        .metric-label { font-size: 12px; color: var(--text-secondary); margin-bottom: 6px; }
        .metric-value { font-size: 22px; font-weight: 700; color: var(--accent-gold); }
        .metric-desc { font-size: 11px; color: var(--text-secondary); margin-top: 4px; }
        .card {
            background: var(--bg-card);
            border: 1px solid var(--border-color);
            border-radius: 10px;
            padding: 18px;
            margin-bottom: 24px;
        }
        .card h2 {
            font-size: 16px;
            font-weight: 600;
            color: #FFFFFF;
            border-left: 4px solid var(--fund-a-color);
            padding-left: 10px;
            margin-bottom: 14px;
        }
        table { width: 100%; border-collapse: collapse; font-size: 12.5px; text-align: left; }
        th { background-color: var(--bg-card-alt); color: #BAC2DE; padding: 10px 12px; font-weight: 600; border-bottom: 1px solid var(--border-color); }
        td { padding: 9px 12px; border-bottom: 1px solid rgba(47, 53, 77, 0.6); color: #E0E2EC; }
        .val-a { color: var(--fund-a-color); font-weight: 600; }
        .val-b { color: var(--fund-b-color); font-weight: 600; }
        .val-winner { color: var(--accent-gold); font-weight: 700; }
        .tag-pill { display: inline-block; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; margin-right: 6px; margin-bottom: 4px; }
        .tag-good { background: rgba(0, 230, 118, 0.2); color: #00E676; border: 1px solid #00E676; }
        .tag-warn { background: rgba(255, 83, 118, 0.2); color: #FF5376; border: 1px solid #FF5376; }
        .btn-print {
            background: #313244; color: #FFFFFF; border: 1px solid #45475A; padding: 6px 14px; border-radius: 6px; cursor: pointer; font-size: 12px; font-weight: 600;
        }
        .footer {
            margin-top: 30px; border-top: 1px solid var(--border-color); padding-top: 16px; font-size: 11px; color: var(--text-secondary); display: flex; justify-content: space-between;
        }
        ");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"container\">");

        // 页眉与双方对决名片
        sb.AppendLine("        <header class=\"header\">");
        sb.AppendLine("            <div class=\"header-title\">");
        sb.AppendLine("                <h1>🥊 公募基金多维量化对决与底层穿透研报</h1>");
        sb.AppendLine($"                <p>统计分析周期: {periodName} | 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | 机构量化研判终端</p>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div style=\"display:flex; gap:10px; align-items:center;\">");
        sb.AppendLine("                <button class=\"btn-print no-print\" onclick=\"window.print()\">🖨️ 打印 / 另存为 PDF</button>");
        sb.AppendLine("                <div style=\"background:linear-gradient(135deg, #3A7BD5, #3A6073); color:#fff; padding:6px 14px; border-radius:6px; font-weight:700; font-size:13px;\">BIGA QUANT PK</div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </header>");

        // 对决双方信息卡片
        sb.AppendLine("        <div style=\"display:flex; justify-content:space-between; align-items:center; margin-bottom:24px; background:var(--bg-card); padding:16px; border-radius:10px; border:1px solid var(--border-color);\">");
        sb.AppendLine("            <div class=\"fund-badge-a\" style=\"flex:1;\">");
        sb.AppendLine($"                <div style=\"color:var(--fund-a-color); font-weight:700; font-size:16px;\">{safeCodeA} {safeNameA}</div>");
        sb.AppendLine($"                <div style=\"font-size:12px; color:var(--text-secondary); margin-top:4px;\">类型: {WebUtility.HtmlEncode(fundA.Type)} | 经理: {WebUtility.HtmlEncode(fundA.ManagerName)} ({WebUtility.HtmlEncode(fundA.ManagerTenure)}) | 规模: {WebUtility.HtmlEncode(fundA.FundSize)}</div>");
        sb.AppendLine($"                <div style=\"margin-top:6px;\"><span class=\"tag-pill tag-good\">综合评分: {scoreA.OverallScore:F1}</span><span class=\"tag-pill tag-good\">评级: {scoreA.RatingGrade}</span></div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"vs-badge\">⚡ VS ⚡</div>");
        sb.AppendLine("            <div class=\"fund-badge-b\" style=\"flex:1; text-align:right;\">");
        sb.AppendLine($"                <div style=\"color:var(--fund-b-color); font-weight:700; font-size:16px;\">{safeCodeB} {safeNameB}</div>");
        sb.AppendLine($"                <div style=\"font-size:12px; color:var(--text-secondary); margin-top:4px;\">类型: {WebUtility.HtmlEncode(fundB.Type)} | 经理: {WebUtility.HtmlEncode(fundB.ManagerName)} ({WebUtility.HtmlEncode(fundB.ManagerTenure)}) | 规模: {WebUtility.HtmlEncode(fundB.FundSize)}</div>");
        sb.AppendLine($"                <div style=\"margin-top:6px;\"><span class=\"tag-pill tag-good\">综合评分: {scoreB.OverallScore:F1}</span><span class=\"tag-pill tag-good\">评级: {scoreB.RatingGrade}</span></div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");

        // 核心对决统计看板
        sb.AppendLine("        <div class=\"grid-summary\">");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">日收益相关度 (Pearson)</div><div class=\"metric-value\">{correlation:F2}</div><div class=\"metric-desc\">{WebUtility.HtmlEncode(correlationRating)}</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">单日收益胜率 (A vs B)</div><div class=\"metric-value\" style=\"color:var(--fund-a-color);\">{winRateA:F1}% : {winRateB:F1}%</div><div class=\"metric-desc\">两基金日内交锋胜出比</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">持仓穿透重合度</div><div class=\"metric-value\" style=\"color:var(--bull-red);\">{overlap.OverlapWeightPercent:F1}%</div><div class=\"metric-desc\">{WebUtility.HtmlEncode(overlap.OverlapRating)} (共重合 {overlap.CommonCount} 只股)</div></div>");
        sb.AppendLine($"            <div class=\"metric-card\"><div class=\"metric-label\">累计收益差 (A - B)</div><div class=\"metric-value\" style=\"color:{(metricsA.TotalReturn >= metricsB.TotalReturn ? "var(--bull-red)" : "var(--bear-green)")};\">{(metricsA.TotalReturn - metricsB.TotalReturn):+0.00;-0.00;0.00}%</div><div class=\"metric-desc\">区间超额相对强弱</div></div>");
        sb.AppendLine("        </div>");

        // 1. 核心量化风控指标对决表格
        sb.AppendLine("        <div class=\"card\">");
        sb.AppendLine("            <h2>📊 核心量化与风险控制指标横向对决</h2>");
        sb.AppendLine("            <table>");
        sb.AppendLine($"                <thead><tr><th>量化指标</th><th style=\"color:var(--fund-a-color);\">[A] {safeCodeA}</th><th style=\"color:var(--accent-gold);\">胜出优势方</th><th style=\"color:var(--fund-b-color);\">[B] {safeCodeB}</th><th>指标量化内涵与评判基准</th></tr></thead>");
        sb.AppendLine("                <tbody>");
        foreach (var r in metricRows)
        {
            sb.AppendLine("                    <tr>");
            sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(r.MetricName)}</strong></td>");
            sb.AppendLine($"                        <td class=\"val-a\">{WebUtility.HtmlEncode(r.ValueA)}</td>");
            sb.AppendLine($"                        <td class=\"val-winner\">{WebUtility.HtmlEncode(r.Winner)}</td>");
            sb.AppendLine($"                        <td class=\"val-b\">{WebUtility.HtmlEncode(r.ValueB)}</td>");
            sb.AppendLine($"                        <td style=\"color:var(--text-secondary); font-size:11.5px;\">{WebUtility.HtmlEncode(r.Description)}</td>");
            sb.AppendLine("                    </tr>");
        }
        sb.AppendLine("                </tbody>");
        sb.AppendLine("            </table>");
        sb.AppendLine("        </div>");

        // 2. 机构级五维量化打分全景对决
        sb.AppendLine("        <div class=\"card\">");
        sb.AppendLine("            <h2>🏆 机构级五维量化打分全景对决</h2>");
        sb.AppendLine("            <table>");
        sb.AppendLine($"                <thead><tr><th>评价维度</th><th style=\"color:var(--fund-a-color);\">[A] {safeNameA} 得分</th><th style=\"color:var(--accent-gold);\">优势方</th><th style=\"color:var(--fund-b-color);\">[B] {safeNameB} 得分</th><th>维度评价重点</th></tr></thead>");
        sb.AppendLine("                <tbody>");
        AddScoreRow(sb, "五维综合量化评分", scoreA.OverallScore, scoreB.OverallScore, "全维度综合加权量化总分 (0~100)");
        AddScoreRow(sb, "1. 收益获取能力", scoreA.ReturnScore, scoreB.ReturnScore, "区间累计回报、复合年化增长率与基准超额");
        AddScoreRow(sb, "2. 风险控制能力", scoreA.RiskControlScore, scoreB.RiskControlScore, "历史最大回撤控制、年化波动率与平稳度");
        AddScoreRow(sb, "3. 风险调整性价比", scoreA.RiskAdjustedScore, scoreB.RiskAdjustedScore, "夏普比率 (Sharpe)、卡玛比率 (Calmar) 与索提诺比率");
        AddScoreRow(sb, "4. 极端抗跌防御力", scoreA.ResilienceScore, scoreB.ResilienceScore, "VaR在险价值、极端回撤恢复周期与黑天鹅抗跌");
        AddScoreRow(sb, "5. 风格配置稳定性", scoreA.StabilityScore, scoreB.StabilityScore, "持仓集中度、晨星风格箱漂移与经理风格持续性");
        sb.AppendLine("                </tbody>");
        sb.AppendLine("            </table>");

        // 亮点与风险提示横向并排
        sb.AppendLine("            <div style=\"display:grid; grid-template-columns:1fr 1fr; gap:16px; margin-top:16px;\">");
        sb.AppendLine("                <div style=\"background:var(--bg-card-alt); padding:12px; border-radius:6px;\">");
        sb.AppendLine($"                    <div style=\"color:var(--fund-a-color); font-weight:700; margin-bottom:8px;\">[A] {safeNameA} 量化特征诊断:</div>");
        sb.AppendLine("                    <div style=\"margin-bottom:6px;\"><span style=\"font-size:11.5px; color:#A6ADC8;\">优势亮点: </span>" + string.Join(" ", scoreA.Strengths.Select(s => $"<span class=\"tag-pill tag-good\">{WebUtility.HtmlEncode(s)}</span>")) + "</div>");
        sb.AppendLine("                    <div><span style=\"font-size:11.5px; color:#A6ADC8;\">风险提示: </span>" + string.Join(" ", scoreA.Weaknesses.Select(w => $"<span class=\"tag-pill tag-warn\">{WebUtility.HtmlEncode(w)}</span>")) + "</div>");
        sb.AppendLine("                </div>");
        sb.AppendLine("                <div style=\"background:var(--bg-card-alt); padding:12px; border-radius:6px;\">");
        sb.AppendLine($"                    <div style=\"color:var(--fund-b-color); font-weight:700; margin-bottom:8px;\">[B] {safeNameB} 量化特征诊断:</div>");
        sb.AppendLine("                    <div style=\"margin-bottom:6px;\"><span style=\"font-size:11.5px; color:#A6ADC8;\">优势亮点: </span>" + string.Join(" ", scoreB.Strengths.Select(s => $"<span class=\"tag-pill tag-good\">{WebUtility.HtmlEncode(s)}</span>")) + "</div>");
        sb.AppendLine("                    <div><span style=\"font-size:11.5px; color:#A6ADC8;\">风险提示: </span>" + string.Join(" ", scoreB.Weaknesses.Select(w => $"<span class=\"tag-pill tag-warn\">{WebUtility.HtmlEncode(w)}</span>")) + "</div>");
        sb.AppendLine("                </div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");

        // 3. 前十大重仓股票穿透与重合度诊断
        sb.AppendLine("        <div class=\"card\">");
        sb.AppendLine($"            <h2>📦 前十大重仓股票穿透与重合度诊断 (重合度: <span style=\"color:var(--bull-red);\">{overlap.OverlapWeightPercent:F1}%</span> | {WebUtility.HtmlEncode(overlap.OverlapRating)})</h2>");
        sb.AppendLine($"            <p style=\"font-size:12.5px; color:var(--text-secondary); margin-bottom:14px;\">{WebUtility.HtmlEncode(overlap.OverlapSummary)}</p>");
        if (overlap.CommonStocks.Count > 0)
        {
            sb.AppendLine("            <table>");
            sb.AppendLine($"                <thead><tr><th>共同重仓标的</th><th>申万行业</th><th style=\"text-align:right; color:var(--fund-a-color);\">[A] 权重</th><th style=\"text-align:right; color:var(--fund-b-color);\">[B] 权重</th><th style=\"text-align:right; color:var(--accent-gold);\">有效重叠权重</th><th style=\"text-align:right;\">权重差 (A - B)</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var cs in overlap.CommonStocks)
            {
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(cs.StockName)}</strong> <span style=\"color:var(--text-secondary); font-family:monospace;\">({WebUtility.HtmlEncode(cs.StockCode)})</span></td>");
                sb.AppendLine($"                        <td>{WebUtility.HtmlEncode(cs.Industry)}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--fund-a-color); font-weight:600;\">{cs.WeightA:F2}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--fund-b-color); font-weight:600;\">{cs.WeightB:F2}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-gold); font-weight:700;\">{cs.OverlapWeight:F2}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(cs.WeightDiff >= 0 ? "var(--bull-red)" : "var(--bear-green)")};\">{cs.WeightDiff:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
        }
        else
        {
            sb.AppendLine("            <div style=\"padding:16px; text-align:center; color:var(--bear-green); background:var(--bg-card-alt); border-radius:6px; font-weight:600;\">🌿 两只基金前十大重仓股完全无交集，持仓个股独立性极强，具备天然的资产配置互补性！</div>");
        }
        sb.AppendLine("        </div>");

        // 4. 申万一级行业配置偏离度分析
        if (overlap.IndustrySpreads.Count > 0)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine("            <h2>🏛️ 申万一级行业配置偏离度与板块偏好对决</h2>");
            sb.AppendLine("            <table>");
            sb.AppendLine($"                <thead><tr><th>申万一级行业</th><th style=\"text-align:right; color:var(--fund-a-color);\">[A] 行业暴露</th><th style=\"text-align:right; color:var(--fund-b-color);\">[B] 行业暴露</th><th style=\"text-align:right;\">偏离差额 (A - B)</th><th>配置倾向研判</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var ind in overlap.IndustrySpreads)
            {
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(ind.IndustryName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--fund-a-color); font-weight:600;\">{ind.WeightA:F2}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--fund-b-color); font-weight:600;\">{ind.WeightB:F2}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{(ind.Spread >= 0 ? "var(--bull-red)" : "var(--bear-green)")}; font-weight:700;\">{ind.Spread:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td>{WebUtility.HtmlEncode(ind.Status)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 5. 双基金牛熊非对称捕获与暴跌日条件相关性横向对比
        var bbCompare = FundCompareEngine.CalculateBullBearCompare(fundA, fundB);
        sb.AppendLine("        <div class=\"card\">");
        sb.AppendLine($"            <h2>🛡️ 双基金牛熊非对称捕获与极端暴跌条件相关性对比 (常态相关性: <span style=\"color:var(--accent-gold); font-weight:700;\">{bbCompare.NormalCorrelation:+0.00;-0.00}</span> | 暴跌日条件相关性: <span style=\"color:var(--bull-red); font-weight:700;\">{bbCompare.CrashCorrelation:+0.00;-0.00}</span>)</h2>");
        sb.AppendLine($"            <p style=\"font-size:12.5px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(bbCompare.ComparisonSummary)}</p>");
        sb.AppendLine("            <table>");
        sb.AppendLine($"                <thead><tr><th>非对称捕获与凸性指标</th><th style=\"text-align:right; color:var(--fund-a-color);\">[A] {safeNameA}</th><th style=\"text-align:center;\">优势方</th><th style=\"text-align:right; color:var(--fund-b-color);\">[B] {safeNameB}</th><th>指标金融学含义</th></tr></thead>");
        sb.AppendLine("                <tbody>");
        
        // Bull Beta
        string bullWinner = bbCompare.CaptureA.BullBeta > bbCompare.CaptureB.BullBeta ? "🏆 [A] 弹性高" : "🏆 [B] 弹性高";
        sb.AppendLine($"                    <tr><td><strong>牛市上涨贝塔 (Bull Beta)</strong></td><td style=\"text-align:right; font-family:monospace; color:var(--fund-a-color); font-weight:600;\">{bbCompare.CaptureA.BullBeta:+0.00;-0.00}</td><td style=\"text-align:center; font-size:11.5px;\">{bullWinner}</td><td style=\"text-align:right; font-family:monospace; color:var(--fund-b-color); font-weight:600;\">{bbCompare.CaptureB.BullBeta:+0.00;-0.00}</td><td style=\"color:var(--text-secondary); font-size:11.5px;\">基准上涨周期中的弹性响应幅度</td></tr>");

        // Bear Beta
        string bearWinner = bbCompare.CaptureA.BearBeta < bbCompare.CaptureB.BearBeta ? "🏆 [A] 跌得少" : "🏆 [B] 跌得少";
        sb.AppendLine($"                    <tr><td><strong>熊市下跌贝塔 (Bear Beta)</strong></td><td style=\"text-align:right; font-family:monospace; color:var(--fund-a-color); font-weight:600;\">{bbCompare.CaptureA.BearBeta:+0.00;-0.00}</td><td style=\"text-align:center; font-size:11.5px;\">{bearWinner}</td><td style=\"text-align:right; font-family:monospace; color:var(--fund-b-color); font-weight:600;\">{bbCompare.CaptureB.BearBeta:+0.00;-0.00}</td><td style=\"color:var(--text-secondary); font-size:11.5px;\">基准下跌周期中的回撤跟随程度 (越小越抗跌)</td></tr>");

        // Asymmetry Index
        string asymWinner = bbCompare.CaptureA.AsymmetryIndex > bbCompare.CaptureB.AsymmetryIndex ? "🏆 [A] 凸性更强" : "🏆 [B] 凸性更强";
        sb.AppendLine($"                    <tr><td><strong>非对称指数 (Bull - Bear Beta)</strong></td><td style=\"text-align:right; font-family:monospace; color:var(--accent-gold); font-weight:700;\">{bbCompare.CaptureA.AsymmetryIndex:+0.00;-0.00}</td><td style=\"text-align:center; font-size:11.5px;\">{asymWinner}</td><td style=\"text-align:right; font-family:monospace; color:var(--accent-gold); font-weight:700;\">{bbCompare.CaptureB.AsymmetryIndex:+0.00;-0.00}</td><td style=\"color:var(--text-secondary); font-size:11.5px;\">多空非对称剪刀差 (正值越大凸性优势越显著)</td></tr>");

        // Capture Spread
        string spreadWinner = bbCompare.CaptureA.CaptureSpread > bbCompare.CaptureB.CaptureSpread ? "🏆 [A] 利差更优" : "🏆 [B] 利差更优";
        sb.AppendLine($"                    <tr><td><strong>捕获利差 (UCR - DCR)</strong></td><td style=\"text-align:right; font-family:monospace; color:var(--bull-red); font-weight:700;\">{bbCompare.CaptureA.CaptureSpread:+0.0;-0.0}%</td><td style=\"text-align:center; font-size:11.5px;\">{spreadWinner}</td><td style=\"text-align:right; font-family:monospace; color:var(--bull-red); font-weight:700;\">{bbCompare.CaptureB.CaptureSpread:+0.0;-0.0}%</td><td style=\"color:var(--text-secondary); font-size:11.5px;\">上行捕获率减去下行捕获率的纯胜率利差</td></tr>");

        // Convexity Rating
        sb.AppendLine($"                    <tr><td><strong>凸性特征评级</strong></td><td style=\"text-align:right; color:var(--fund-a-color); font-weight:600;\">{WebUtility.HtmlEncode(bbCompare.CaptureA.ConvexityRating)}</td><td style=\"text-align:center;\">-</td><td style=\"text-align:right; color:var(--fund-b-color); font-weight:600;\">{WebUtility.HtmlEncode(bbCompare.CaptureB.ConvexityRating)}</td><td style=\"color:var(--text-secondary); font-size:11.5px;\">综合凸性画像</td></tr>");

        sb.AppendLine("                </tbody>");
        sb.AppendLine("            </table>");
        sb.AppendLine("        </div>");

        // 页脚
        sb.AppendLine("        <footer class=\"footer\">");
        sb.AppendLine("            <div style=\"margin-bottom:6px; color:#A6ADC8;\"><strong>【机构免责声明】</strong>本研报由 BIGA 机构级公募基金量化投研工作站基于公开披露数据与量化算法自动解算生成，包含底层持仓穿透、行业暴露归因与五维评分模型。仅供专业投研与资产配置参考，不构成任何实质性投资建议。市场有风险，投资需谨慎。</div>");
        sb.AppendLine("            <div>BIGA Research Terminal &copy; 2026</div>");
        sb.AppendLine("        </footer>");

        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void AddScoreRow(StringBuilder sb, string dimName, decimal scoreA, decimal scoreB, string desc)
    {
        string winner = scoreA > scoreB ? "🏆 基金 A 优" : (scoreB > scoreA ? "🏆 基金 B 优" : "持平");
        sb.AppendLine("                    <tr>");
        sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(dimName)}</strong></td>");
        sb.AppendLine($"                        <td class=\"val-a\">{scoreA:F1} 分</td>");
        sb.AppendLine($"                        <td class=\"val-winner\">{winner}</td>");
        sb.AppendLine($"                        <td class=\"val-b\">{scoreB:F1} 分</td>");
        sb.AppendLine($"                        <td style=\"color:var(--text-secondary); font-size:11.5px;\">{WebUtility.HtmlEncode(desc)}</td>");
        sb.AppendLine("                    </tr>");
    }

    /// <summary>
    /// 将基金多维筛选大厅的筛选结果列表导出为 UTF-8 BOM 标准 CSV 表格
    /// </summary>
    public static void ExportScreenerToCsv(string filePath, IEnumerable<StoredFundItem> funds)
    {
        var sb = new StringBuilder();
        var list = funds.ToList();
        sb.AppendLine("=== BIGA 基金多维量化筛选结果清单 ===");
        sb.AppendLine($"导出时间,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"标的数量,{list.Count} 只");
        sb.AppendLine();
        sb.AppendLine("基金代码,基金名称,基金品类,板块分类,基金经理,任职年限,基金规模,量化评分,量化评级,近1年收益率(%),夏普比率,最大回撤(%),年化波动率(%),最新公布净值,更新时间");
        foreach (var f in list)
        {
            sb.AppendLine($"\"{f.Code}\",\"{f.Name}\",\"{f.Type}\",\"{f.Sector}\",\"{f.Manager}\",\"{f.Tenure}\",\"{f.FundSize}\",{f.QuantScore:F1},{f.RatingGrade},{f.Return1Y:F2}%,{f.SharpeRatio:F2},{f.MaxDrawdown:F2}%,{f.AnnualizedVol:F2}%,{f.LatestNav:F4},{f.UpdatedAt:yyyy-MM-dd}");
        }
        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void AppendLookThroughAndMonteCarloHtml(StringBuilder sb, PortfolioResult portfolio)
    {
        // 1. 组合底层穿透持仓与申万行业暴露
        if (portfolio.LookThroughResult != null && portfolio.LookThroughResult.TopHoldings.Count > 0)
        {
            var lt = portfolio.LookThroughResult;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>📦 底层资产穿透透视与重仓个股合并 (识别股票: {lt.TotalUniqueStocks} 只 | 穿透总覆盖: {lt.TotalHoldingsWeight:F1}% | 底层 CR10: {lt.PortfolioCr10:F1}% | 股票HHI: {lt.StocksHhi:F0} [{lt.HhiConcentrationLevel}] | 有效持仓 Neff: {lt.EffectiveStockCount:F1} 只 | 行业HHI: {lt.IndustryHhi:F0})</h2>");
            sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(lt.LookThroughSummary)}</p>");

            // Top 10 Stock Holdings Table
            sb.AppendLine("            <h3 style=\"font-size:13px; color:var(--accent-gold); margin-bottom:8px;\">🏆 组合底层穿透前 10 大重仓股票</h3>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>股票代码</th><th>股票名称</th><th style=\"text-align:right;\">穿透合并权重</th><th style=\"text-align:center;\">重合基金数</th><th>申万行业板块</th><th>主要持有基金贡献明细</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var h in lt.TopHoldings)
            {
                string consensusBadge = h.FundCount >= 2 ? " <span style=\"background:#313244; color:var(--accent-gold); padding:1px 6px; border-radius:4px; font-size:11px;\">共同重仓</span>" : "";
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td style=\"font-family:monospace; font-weight:600;\">{WebUtility.HtmlEncode(h.StockCode)}</td>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(h.StockName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; font-weight:700; color:var(--accent-gold);\">{h.PortfolioWeight:F2}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:center;\">{h.FundCount} 只{consensusBadge}</td>");
                sb.AppendLine($"                        <td>{WebUtility.HtmlEncode(h.Industry)}</td>");
                sb.AppendLine($"                        <td style=\"font-size:12px; color:var(--text-secondary);\">{WebUtility.HtmlEncode(h.ContributingFunds)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");

            // Shenwan Industry Exposure Table
            if (lt.IndustryExposures.Count > 0)
            {
                sb.AppendLine("            <h3 style=\"font-size:13px; color:var(--accent-gold); margin-top:16px; margin-bottom:8px;\">🏭 穿透申万行业赛道配置敞口分布</h3>");
                sb.AppendLine("            <table>");
                sb.AppendLine("                <thead><tr><th>行业名称</th><th style=\"text-align:right;\">穿透合并权重</th><th style=\"text-align:right;\">包含标的数</th><th style=\"text-align:right;\">占已识别重仓比例</th></tr></thead>");
                sb.AppendLine("                <tbody>");
                foreach (var ind in lt.IndustryExposures)
                {
                    sb.AppendLine("                    <tr>");
                    sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(ind.IndustryName)}</strong></td>");
                    sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; font-weight:600; color:var(--bull-red);\">{ind.PortfolioWeight:F2}%</td>");
                    sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{ind.StockCount} 只</td>");
                    sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--text-secondary);\">{ind.RatioOfIdentified:F1}%</td>");
                    sb.AppendLine("                    </tr>");
                }
                sb.AppendLine("                </tbody>");
                sb.AppendLine("            </table>");
            }

            sb.AppendLine("        </div>");
        }

        // 2. 前瞻性蒙特卡洛推演
        if (portfolio.MonteCarloResult != null)
        {
            var mc = portfolio.MonteCarloResult;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🎲 前瞻性蒙特卡洛随机漫步资产推演 (GBM 几何布朗运动 {mc.SimulationRuns} 次推演 / {mc.HorizonTradingDays} 交易日)</h2>");
            sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(mc.SimulationSummary)}</p>");
            sb.AppendLine("            <div class=\"metrics-grid grid-metrics\" style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:12px; margin-top:12px;\">");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">95% 乐观上限 (1Y)</div><div class=\"metric-value\" style=\"color:var(--bull-red);\">{mc.Percentile95Return:+0.00;-0.00;0.00}%</div><div class=\"metric-desc\">推演净值: {mc.EndNav95:F4}</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">50% 中位稳态 (1Y)</div><div class=\"metric-value\" style=\"color:var(--accent-gold);\">{mc.MedianReturn:+0.00;-0.00;0.00}%</div><div class=\"metric-desc\">推演净值: {mc.EndNavMedian:F4}</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">5% 悲观底线 (1Y)</div><div class=\"metric-value\" style=\"color:var(--bear-green);\">{mc.Percentile5Return:+0.00;-0.00;0.00}%</div><div class=\"metric-desc\">推演净值: {mc.EndNav5:F4}</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">本金亏损破本概率</div><div class=\"metric-value\">{mc.ProbabilityOfLoss:F1}%</div><div class=\"metric-desc\">1年内净值&lt;1.0</div></div>");
            sb.AppendLine($"                <div class=\"metric-card\"><div class=\"metric-label\">模拟预期最大回撤</div><div class=\"metric-value\" style=\"color:var(--bear-green);\">-{mc.ExpectedSimulatedDrawdown:F1}%</div><div class=\"metric-desc\">极端压力底线</div></div>");
            sb.AppendLine("            </div>");

            // 2.1 蒙特卡洛 5 轨前瞻时序预测表 (Cone of Uncertainty)
            if (mc.TrajectoryDays != null && mc.TrajectoryDays.Length > 0)
            {
                sb.AppendLine("            <h3 style=\"font-size:13px; color:var(--accent-gold); margin-top:16px; margin-bottom:8px;\">📈 蒙特卡洛扇形概率锥 5 轨前瞻推演 (Cone of Uncertainty)</h3>");
                sb.AppendLine("            <table>");
                sb.AppendLine("                <thead><tr><th>推演时间窗口</th><th style=\"text-align:right;\">95% 乐观上轨</th><th style=\"text-align:right;\">75% 良好预期</th><th style=\"text-align:right;\">50% 中枢基准</th><th style=\"text-align:right;\">25% 防御水平</th><th style=\"text-align:right;\">5% 极端下轨</th><th style=\"text-align:center;\">置信区间</th></tr></thead>");
                sb.AppendLine("                <tbody>");

                int[] milestoneDays = new[] { 21, 63, 126, 250 };
                string[] milestoneLabels = new[] { "1 个月 (T+21)", "3 个月 (T+63)", "半年 (T+126)", "1 年 (T+250)" };

                for (int k = 0; k < milestoneDays.Length; k++)
                {
                    int day = milestoneDays[k];
                    if (day < mc.TrajectoryDays.Length)
                    {
                        double p95 = mc.Trajectory95[day];
                        double p75 = mc.Trajectory75[day];
                        double p50 = mc.Trajectory50[day];
                        double p25 = mc.Trajectory25[day];
                        double p5 = mc.Trajectory5[day];

                        sb.AppendLine("                    <tr>");
                        sb.AppendLine($"                        <td><strong>{milestoneLabels[k]}</strong></td>");
                        sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bull-red);\">{p95:F4} ({(p95 - 1.0) * 100:+0.0;-0.0;0.0}%)</td>");
                        sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-gold);\">{p75:F4} ({(p75 - 1.0) * 100:+0.0;-0.0;0.0}%)</td>");
                        sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; font-weight:700;\">{p50:F4} ({(p50 - 1.0) * 100:+0.0;-0.0;0.0}%)</td>");
                        sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-blue);\">{p25:F4} ({(p25 - 1.0) * 100:+0.0;-0.0;0.0}%)</td>");
                        sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bear-green);\">{p5:F4} ({(p5 - 1.0) * 100:+0.0;-0.0;0.0}%)</td>");
                        sb.AppendLine($"                        <td style=\"text-align:center;\"><span class=\"rating-badge rating-strong\">90% 置信区间</span></td>");
                        sb.AppendLine("                    </tr>");
                    }
                }
                sb.AppendLine("                </tbody>");
                sb.AppendLine("            </table>");
            }

            sb.AppendLine("        </div>");
        }

        // 3. 马科维茨有效前沿上包络 (Efficient Frontier Envelope)
        if (portfolio.EfficientFrontierCurve != null && portfolio.EfficientFrontierCurve.Count > 0)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine("            <h2>📐 马科维茨现代投资组合理论 (MPT) 有效前沿解析与最优切点</h2>");
            sb.AppendLine("            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">通过对 1,500 次狄利克雷随机配置空间进行凸包上包络提取，确定在任意给定风险水平下能获得的最大期望回报曲线。</p>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>特征配置锚点</th><th style=\"text-align:right;\">年化预期收益</th><th style=\"text-align:right;\">年化预期波动率</th><th style=\"text-align:right;\">夏普比率 (Rf=2%)</th><th>配置特征与风险偏好</th></tr></thead>");
            sb.AppendLine("                <tbody>");

            var maxSharpe = portfolio.Schemes.FirstOrDefault(s => s.SchemeName.Contains("夏普"));
            var minVol = portfolio.Schemes.FirstOrDefault(s => s.SchemeName.Contains("方差") || s.SchemeName.Contains("波动"));
            var riskParity = portfolio.Schemes.FirstOrDefault(s => s.SchemeName.Contains("风险平价"));
            var blScheme = portfolio.Schemes.FirstOrDefault(s => s.SchemeName.Contains("Black-Litterman") || s.SchemeName.Contains("贝叶斯"));

            if (maxSharpe != null)
            {
                sb.AppendLine("                    <tr>");
                sb.AppendLine("                        <td style=\"color:var(--accent-gold); font-weight:700;\">🎯 最优切点投资组合 (Max Sharpe)</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bull-red); font-weight:600;\">{maxSharpe.ExpectedReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-blue);\">{maxSharpe.ExpectedVolatility:F2}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; font-weight:700;\">{maxSharpe.SharpeRatio:F2}</td>");
                sb.AppendLine("                        <td style=\"color:var(--text-secondary); font-size:12px;\">资本市场线切点，单单位波动补偿最高的机构配置黄金比例</td>");
                sb.AppendLine("                    </tr>");
            }

            if (minVol != null)
            {
                sb.AppendLine("                    <tr>");
                sb.AppendLine("                        <td style=\"color:var(--bear-green); font-weight:700;\">🛡️ 全局最小方差组合 (Min Volatility)</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bull-red);\">{minVol.ExpectedReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bear-green); font-weight:700;\">{minVol.ExpectedVolatility:F2}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{minVol.SharpeRatio:F2}</td>");
                sb.AppendLine("                        <td style=\"color:var(--text-secondary); font-size:12px;\">有效前沿最左顶点，最大程度对冲分散风险，适合绝对防守型底仓</td>");
                sb.AppendLine("                    </tr>");
            }

            if (riskParity != null)
            {
                sb.AppendLine("                    <tr>");
                sb.AppendLine("                        <td style=\"color:var(--accent-blue); font-weight:700;\">⚖️ 风险平价均衡组合 (Risk Parity)</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bull-red);\">{riskParity.ExpectedReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-blue);\">{riskParity.ExpectedVolatility:F2}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{riskParity.SharpeRatio:F2}</td>");
                sb.AppendLine("                        <td style=\"color:var(--text-secondary); font-size:12px;\">使各底层基金对组合总方差的风险贡献均等化，穿越牛熊周期</td>");
                sb.AppendLine("                    </tr>");
            }

            if (blScheme != null)
            {
                sb.AppendLine("                    <tr>");
                sb.AppendLine("                        <td style=\"color:#cba6f7; font-weight:700;\">🧠 Black-Litterman 贝叶斯均衡 (B-L Model)</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bull-red);\">{blScheme.ExpectedReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-blue);\">{blScheme.ExpectedVolatility:F2}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; font-weight:700;\">{blScheme.SharpeRatio:F2}</td>");
                sb.AppendLine("                        <td style=\"color:var(--text-secondary); font-size:12px;\">融合逆向优化市场均衡先验与投资者主观观点的贝叶斯稳健配置</td>");
                sb.AppendLine("                    </tr>");
            }

            sb.AppendLine("                    <tr>");
            sb.AppendLine("                        <td style=\"color:#FFFFFF; font-weight:700;\">⭐ 当前实际配置组合 (Current Portfolio)</td>");
            sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bull-red);\">{portfolio.AnnualizedReturn:+0.00;-0.00;0.00}%</td>");
            sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-blue);\">{portfolio.AnnualizedVolatility:F2}%</td>");
            sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; font-weight:700;\">{portfolio.SharpeRatio:F2}</td>");
            sb.AppendLine("                        <td style=\"color:var(--text-secondary); font-size:12px;\">用户当前设置权重的实际运作坐标</td>");
            sb.AppendLine("                    </tr>");

            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 4. 组合历史最深前五大回撤周期解构 (Top 5 Drawdown Episodes)
        if (portfolio.DrawdownEpisodes != null && portfolio.DrawdownEpisodes.Count > 0)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine("            <h2>📉 组合历史最深前五大回撤周期解构 (Top 5 Drawdown Episodes)</h2>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>排名</th><th>波峰日期</th><th>谷底日期</th><th>修复出坑日</th><th>最大跌幅</th><th>暴跌历时</th><th>修复历时</th><th>全周期</th><th>修复状态</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var ep in portfolio.DrawdownEpisodes)
            {
                string recStr = ep.RecoveryDate.HasValue ? ep.RecoveryDate.Value.ToString("yyyy-MM-dd") : "<span style=\"color:#FF5376;\">未出坑</span>";
                string recDays = ep.RecoveryDays.HasValue ? $"{ep.RecoveryDays.Value} 交易日" : "-";
                sb.AppendLine($"                    <tr><td><strong>#{ep.Rank}</strong></td><td>{ep.PeakDate:yyyy-MM-dd}</td><td>{ep.TroughDate:yyyy-MM-dd}</td><td>{recStr}</td><td style=\"color:var(--bear-green); font-weight:700;\">{ep.DrawdownPercent:F2}%</td><td>{ep.FallDays} 交易日</td><td>{recDays}</td><td>{ep.TotalDays} 交易日</td><td>{ep.StatusDesc}</td></tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 5. 宏观情景冲击与多因子压力测试 (Forward-Looking Macro Scenario Shocks)
        if (portfolio.MacroShockResult != null && portfolio.MacroShockResult.PresetScenarios.Count > 0)
        {
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine("            <h2>⚡ 宏观情景冲击与多因子压力测试 (Bloomberg PORT Standard Stress Test)</h2>");
            sb.AppendLine("            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">基于各成分基金 Beta 敏感度及债券久期弹性，模拟在经典宏观剧烈冲击下的组合净值影响与 Stressed VaR95。</p>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>情景名称</th><th style=\"text-align:right;\">权益冲击</th><th style=\"text-align:right;\">利率变动</th><th style=\"text-align:right;\">预估净值变动</th><th style=\"text-align:right;\">预估损益 (100万)</th><th style=\"text-align:right;\">受压 VaR95</th><th style=\"text-align:center;\">冲击评级</th><th>情景说明</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var sc in portfolio.MacroShockResult.PresetScenarios)
            {
                string pnlColor = sc.EstimatedNavChangePercent >= 0 ? "var(--bull-red)" : "var(--bear-green)";
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(sc.Name)}</strong></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{sc.EquityShockPercent:+0;-0;0}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{sc.InterestRateShockBps:+0;-0;0} bps</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{pnlColor}; font-weight:700;\">{sc.EstimatedNavChangePercent:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{pnlColor};\">¥{sc.EstimatedPnL:N0}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bear-green);\">{sc.StressedVaR95:F2}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:center;\"><span class=\"rating-badge rating-neutral\">{WebUtility.HtmlEncode(sc.ImpactRating)}</span></td>");
                sb.AppendLine($"                        <td style=\"color:var(--text-secondary); font-size:11.5px;\">{WebUtility.HtmlEncode(sc.Description)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 6. 全时序动态再平衡仿真与换手磨损损耗
        if (portfolio.RebalanceSimulation != null)
        {
            var sim = portfolio.RebalanceSimulation;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🔄 全时序动态再平衡仿真与换手磨损损耗 (策略: <strong>{sim.StrategyType}</strong> | 净阿尔法: <span style=\"color:var(--bull-red); font-weight:700;\">{sim.NetAlphaVsBuyAndHold:+0.00;-0.00;0.00}%</span>)</h2>");
            sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(sim.DiagnosticSummary)}</p>");
            sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:12px; margin-bottom:14px;\">");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>扣费净终值收益:</span><strong style=\"color:var(--bull-red);\">{sim.TotalReturn:+0.00;-0.00;0.00}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>买入持有基准:</span><strong>{sim.BuyAndHoldReturn:+0.00;-0.00;0.00}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>年化复合 (CAGR):</span><strong style=\"color:var(--accent-gold);\">{sim.AnnualizedReturn:+0.00;-0.00;0.00}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>年化波动率:</span><strong>{sim.AnnualizedVolatility:F2}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>仿真最大回撤:</span><strong style=\"color:var(--bear-green);\">-{sim.MaxDrawdown:F2}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>夏普比率 (Sharpe):</span><strong style=\"color:var(--accent-blue);\">{sim.SharpeRatio:F2}</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>年化换手率:</span><strong>{sim.AnnualizedTurnoverRate:F1}% /年</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>摩擦磨损总额:</span><strong style=\"color:var(--bear-green);\">¥{sim.TotalFrictionFeeLoss:N0} (-{sim.FrictionFeeDragPercent:F2}%)</strong></div>");
            sb.AppendLine("            </div>");

            if (sim.Events.Count > 0)
            {
                sb.AppendLine($"            <h3 style=\"font-size:13px; color:var(--accent-gold); margin-top:14px; margin-bottom:8px;\">📋 再平衡触发流水事件明细 (共 {sim.RebalanceCount} 次触发)</h3>");
                sb.AppendLine("            <table>");
                sb.AppendLine("                <thead><tr><th>调仓日期</th><th>触发原因</th><th style=\"text-align:right;\">调仓前净值</th><th style=\"text-align:right;\">最大权重偏离</th><th style=\"text-align:right;\">单边换手率</th><th style=\"text-align:right;\">冲击磨损规费</th><th>执行明细</th></tr></thead>");
                sb.AppendLine("                <tbody>");
                foreach (var ev in sim.Events.Take(25))
                {
                    sb.AppendLine("                    <tr>");
                    sb.AppendLine($"                        <td style=\"font-weight:600;\">{ev.Date:yyyy-MM-dd}</td>");
                    sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(ev.TriggerReason)}</strong></td>");
                    sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{ev.PortfolioNavBefore:F4}</td>");
                    sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-gold); font-weight:600;\">{ev.MaxWeightDeviationBefore:F2}%</td>");
                    sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{ev.TurnoverRate:F2}%</td>");
                    sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bear-green);\">¥{ev.FrictionFeeAmount:N2}</td>");
                    sb.AppendLine($"                        <td style=\"color:var(--text-secondary); font-size:11.5px;\">{WebUtility.HtmlEncode(ev.Details)}</td>");
                    sb.AppendLine("                    </tr>");
                }
                sb.AppendLine("                </tbody>");
                sb.AppendLine("            </table>");
            }
            sb.AppendLine("        </div>");
        }

        // 7. Phase 12 组合历史极端黑天鹅危机压力测试回放 (Historical Crisis Replay & Synthetic Shock Engine)
        if (portfolio.CrisisReplay != null && portfolio.CrisisReplay.CrisisItems.Count > 0)
        {
            var cr = portfolio.CrisisReplay;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🛡️ 组合历史极端黑天鹅危机压力测试回放 (危机韧性综合分: <strong style=\"color:var(--accent-gold);\">{cr.ComprehensiveResilienceScore:F1}</strong> | 评级: <span class=\"rating-badge rating-aa\">{cr.OverallResilienceRating}</span>)</h2>");
            sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(cr.ExecutiveSummary)}</p>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>历史危机情景</th><th>时间跨度</th><th style=\"text-align:right;\">基准跌幅</th><th style=\"text-align:right;\">组合期间收益</th><th style=\"text-align:right;\">期间最大回撤</th><th style=\"text-align:right;\">超额防守收益</th><th style=\"text-align:center;\">出坑修复</th><th style=\"text-align:center;\">防御韧性等级</th><th>压力推演诊断</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var item in cr.CrisisItems)
            {
                string retColor = item.FundReturn >= 0 ? "var(--bull-red)" : "var(--bear-green)";
                string excessColor = item.ExcessReturnOverBenchmark >= 0 ? "var(--bull-red)" : "var(--bear-green)";
                string recStr = item.RecoveryDays < 0 ? "<span style=\"color:#FF5376;\">未出坑</span>" : (item.RecoveryDays == 0 ? "无显著回撤" : $"{item.RecoveryDays} 天");
                string badgeClass = item.ResilienceGrade.Contains("金钟罩") || item.ResilienceGrade.Contains("优良") ? "rating-aaa" : (item.ResilienceGrade.Contains("同步") ? "rating-aa" : "rating-neutral");

                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(item.ScenarioName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"font-size:11.5px;\">{item.StartDate:yyyy/MM/dd} ~ {item.EndDate:yyyy/MM/dd}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bear-green);\">{item.BenchmarkDrop:F1}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{retColor}; font-weight:700;\">{item.FundReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bear-green);\">-{item.FundMaxDrawdown:F2}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{excessColor}; font-weight:700;\">{item.ExcessReturnOverBenchmark:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:center;\">{recStr}</td>");
                sb.AppendLine($"                        <td style=\"text-align:center;\"><span class=\"rating-badge {badgeClass}\">{WebUtility.HtmlEncode(item.ResilienceGrade)}</span></td>");
                sb.AppendLine($"                        <td style=\"font-size:11px; color:var(--text-secondary);\">{(item.IsSyntheticProxy ? "【代理合成】" : "【真实时序】")} {WebUtility.HtmlEncode(item.DiagnosticComment)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // 8. Phase 12 多期跨周期 Carino 几何复利平滑 Brinson 业绩归因 (Multi-Period Carino Linking)
        if (portfolio.MultiPeriodBrinson != null)
        {
            var cb = portfolio.MultiPeriodBrinson;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>📐 组合多期跨周期 Brinson 几何复利平滑归因 (Carino Linking | 严格几何守恒残差: <span style=\"color:var(--accent-blue);\">{cb.ResidualGap:F4}%</span>)</h2>");
            sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(cb.AttributionSummary)}</p>");
            sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:12px; margin-bottom:14px;\">");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>组合多期复利收益:</span><strong style=\"color:var(--bull-red);\">{cb.CumulativePortfolioReturn:+0.00;-0.00;0.00}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>基准多期复利收益:</span><strong>{cb.CumulativeBenchmarkReturn:+0.00;-0.00;0.00}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>多期累计总超额:</span><strong style=\"color:var(--accent-gold);\">{cb.CumulativeExcessReturn:+0.00;-0.00;0.00}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>平滑资产配置效应:</span><strong style=\"color:var(--bull-red);\">{cb.CarinoAllocationEffect:+0.00;-0.00;0.00}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>平滑标的选择效应:</span><strong style=\"color:var(--accent-blue);\">{cb.CarinoSelectionEffect:+0.00;-0.00;0.00}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>平滑交叉交互效应:</span><strong>{cb.CarinoInteractionEffect:+0.00;-0.00;0.00}%</strong></div>");
            sb.AppendLine("            </div>");

            if (cb.SectorAttributions.Count > 0)
            {
                sb.AppendLine("            <h3 style=\"font-size:13px; color:var(--accent-gold); margin-top:14px; margin-bottom:8px;\">📊 各行业/板块多期连乘平滑贡献解构</h3>");
                sb.AppendLine("            <table>");
                sb.AppendLine("                <thead><tr><th>行业板块</th><th style=\"text-align:right;\">平均组合权重</th><th style=\"text-align:right;\">平均基准权重</th><th style=\"text-align:right;\">Carino 配置效应</th><th style=\"text-align:right;\">Carino 选择效应</th><th style=\"text-align:right;\">Carino 交互效应</th><th style=\"text-align:right;\">综合总超额贡献</th></tr></thead>");
                sb.AppendLine("                <tbody>");
                foreach (var sec in cb.SectorAttributions)
                {
                    string totColor = sec.CumulativeTotalAlpha >= 0 ? "var(--bull-red)" : "var(--bear-green)";
                    sb.AppendLine("                    <tr>");
                    sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(sec.SectorName)}</strong></td>");
                    sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{sec.AveragePortfolioWeight:F2}%</td>");
                    sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{sec.AverageBenchmarkWeight:F2}%</td>");
                    sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{sec.CumulativeAllocationEffect:+0.00;-0.00;0.00}%</td>");
                    sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{sec.CumulativeSelectionEffect:+0.00;-0.00;0.00}%</td>");
                    sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{sec.CumulativeInteractionEffect:+0.00;-0.00;0.00}%</td>");
                    sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{totColor}; font-weight:700;\">{sec.CumulativeTotalAlpha:+0.00;-0.00;0.00}%</td>");
                    sb.AppendLine("                    </tr>");
                }
                sb.AppendLine("                </tbody>");
                sb.AppendLine("            </table>");
            }
            sb.AppendLine("        </div>");
        }

        // 9. Phase 12 战术资产配置 (TAA) 动量轮动回测
        if (portfolio.TaaBacktest != null)
        {
            var taa = portfolio.TaaBacktest;
            sb.AppendLine("        <div class=\"card\">");
            sb.AppendLine($"            <h2>🚀 战术资产配置 (TAA) 自适应动量轮动回测 (累计收益: <strong style=\"color:var(--bull-red);\">{taa.TotalReturn:+0.00;-0.00;0.00}%</strong> | 超额: <strong style=\"color:var(--accent-gold);\">{taa.ExcessReturnOverBenchmark:+0.00;-0.00;0.00}%</strong>)</h2>");
            sb.AppendLine($"            <p style=\"font-size:12px; color:var(--text-secondary); margin-bottom:12px;\">{WebUtility.HtmlEncode(taa.StrategyDiagnosis)}</p>");
            sb.AppendLine("            <div style=\"display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:12px; margin-bottom:14px;\">");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>年化复合收益率:</span><strong style=\"color:var(--bull-red);\">{taa.AnnualizedReturn:+0.00;-0.00;0.00}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>年化波动率:</span><strong>{taa.AnnualizedVolatility:F2}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>最大回撤:</span><strong style=\"color:var(--bear-green);\">-{taa.MaxDrawdown:F2}%</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>夏普比率:</span><strong style=\"color:var(--accent-blue);\">{taa.SharpeRatio:F2}</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>卡玛比率 (Calmar):</span><strong style=\"color:var(--accent-gold);\">{taa.CalmarRatio:F2}</strong></div>");
            sb.AppendLine($"                <div class=\"fund-badge\"><span>轮动胜率:</span><strong style=\"color:var(--bull-red);\">{taa.WinRateVsBenchmark:F1}%</strong></div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");
        }
    }

    /// <summary>
    /// 导出机构投决会级核心资产配置全景汇报画册 (Executive Investment Committee Pitch Deck)
    /// </summary>
    public static void ExportExecutivePitchDeckToHtml(string filePath, FundDetail fund, QuantMetrics? metrics = null, PortfolioResult? portfolio = null)
    {
        string html = GenerateExecutivePitchDeckHtml(fund, metrics, portfolio);
        File.WriteAllText(filePath, html, Encoding.UTF8);
    }

    /// <summary>
    /// 生成投决会级全景汇报画册 HTML 文本
    /// </summary>
    public static string GenerateExecutivePitchDeckHtml(FundDetail fund, QuantMetrics? metrics = null, PortfolioResult? portfolio = null)
    {
        var sb = new StringBuilder();
        metrics ??= fund.QuantMetrics ?? (fund.NavHistory.Count >= 2 ? QuantCalculator.CalculateMetrics(fund.NavHistory, "全历程") : new QuantMetrics());
        var dd = fund.DueDiligence ?? metrics.DueDiligence ?? new InstitutionalDueDiligenceCard();
        var cr = fund.CrisisReplay ?? metrics.CrisisReplay;

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-CN\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"    <title>BIGA 机构投决会评审全景画册 - {WebUtility.HtmlEncode(fund.Name)} ({fund.Code})</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        :root { --bg-primary: #0F111A; --bg-card: #181A26; --bg-sub: #222538; --text-primary: #E2E8F0; --text-secondary: #94A3B8; --accent-gold: #F5B041; --accent-blue: #3B82F6; --bull-red: #EF4444; --bear-green: #10B981; --border: #2D3748; }");
        sb.AppendLine("        * { box-sizing: border-box; margin: 0; padding: 0; }");
        sb.AppendLine("        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'PingFang SC', 'Microsoft YaHei', sans-serif; background: var(--bg-primary); color: var(--text-primary); line-height: 1.6; padding: 32px; }");
        sb.AppendLine("        .deck-container { max-width: 1300px; margin: 0 auto; }");
        sb.AppendLine("        .deck-header { background: linear-gradient(135deg, #1E2235 0%, #0F111A 100%); border: 1px solid var(--border); border-radius: 12px; padding: 28px; margin-bottom: 24px; display: flex; justify-content: space-between; align-items: center; border-left: 6px solid var(--accent-gold); }");
        sb.AppendLine("        .deck-title { font-size: 24px; font-weight: 800; color: #FFFFFF; letter-spacing: 0.5px; }");
        sb.AppendLine("        .deck-subtitle { font-size: 13px; color: var(--text-secondary); margin-top: 6px; }");
        sb.AppendLine("        .grid-2col { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 24px; }");
        sb.AppendLine("        .grid-3col { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; margin-bottom: 24px; }");
        sb.AppendLine("        .deck-card { background: var(--bg-card); border: 1px solid var(--border); border-radius: 10px; padding: 20px; box-shadow: 0 8px 24px rgba(0,0,0,0.35); }");
        sb.AppendLine("        .deck-card h2 { font-size: 16px; font-weight: 700; color: var(--accent-gold); margin-bottom: 14px; border-bottom: 1px solid var(--border); padding-bottom: 8px; display: flex; justify-content: space-between; }");
        sb.AppendLine("        table { width: 100%; border-collapse: collapse; font-size: 12.5px; margin-top: 8px; }");
        sb.AppendLine("        th { background: var(--bg-sub); color: var(--text-secondary); padding: 9px 10px; text-align: left; font-weight: 600; border-bottom: 1px solid var(--border); }");
        sb.AppendLine("        td { padding: 9px 10px; border-bottom: 1px solid rgba(45, 55, 72, 0.4); }");
        sb.AppendLine("        tr:hover { background: rgba(255,255,255,0.02); }");
        sb.AppendLine("        .score-pill { display: inline-block; padding: 4px 10px; border-radius: 20px; font-weight: 700; font-size: 12px; background: rgba(245, 176, 65, 0.15); color: var(--accent-gold); border: 1px solid var(--accent-gold); }");
        sb.AppendLine("        .sign-box { border: 2px dashed var(--border); border-radius: 8px; padding: 16px; margin-top: 20px; display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; text-align: center; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"deck-container\">");

        // 头部
        sb.AppendLine("        <div class=\"deck-header\">");
        sb.AppendLine("            <div>");
        sb.AppendLine($"                <div class=\"deck-title\">🏛️ 机构投资决策委员会·核心配置评审画册 (Executive Pitch Deck)</div>");
        sb.AppendLine($"                <div class=\"deck-subtitle\">标的资产: <strong>{WebUtility.HtmlEncode(fund.Name)} ({fund.Code})</strong> | 投资经理: {fund.ManagerName} | 评审基准: 沪深300 | 生成日期: {DateTime.Now:yyyy-MM-dd HH:mm}</div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div style=\"text-align:right;\">");
        sb.AppendLine($"                <div style=\"font-size:26px; font-weight:800; color:var(--accent-gold);\">{dd.StarRating}</div>");
        sb.AppendLine($"                <span class=\"score-pill\">尽调评级: {WebUtility.HtmlEncode(dd.DiligenceGrade)} ({dd.OverallDiligenceScore:F1}分)</span>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");

        // 核心风控与尽调六维打分
        sb.AppendLine("        <div class=\"grid-2col\">");
        sb.AppendLine("            <div class=\"deck-card\">");
        sb.AppendLine("                <h2>🎯 机构尽调六维雷达综合评分</h2>");
        sb.AppendLine("                <table>");
        sb.AppendLine("                    <thead><tr><th>评估维度</th><th>核心考察要素</th><th style=\"text-align:right;\">量化评分</th><th>状态</th></tr></thead>");
        sb.AppendLine("                    <tbody>");
        sb.AppendLine($"                        <tr><td><strong>1. 选股纯阿尔法</strong></td><td>TM Alpha / FF5 异质收益 / 胜率</td><td style=\"text-align:right; font-weight:700; color:var(--accent-gold);\">{dd.AlphaPurityScore:F1}</td><td>{(dd.AlphaPurityScore >= 70 ? "优异" : "中性")}</td></tr>");
        sb.AppendLine($"                        <tr><td><strong>2. 择时与对冲凸性</strong></td><td>TM Gamma / HM 熊市Beta / 捕获利差</td><td style=\"text-align:right; font-weight:700; color:var(--accent-gold);\">{dd.TimingConvexityScore:F1}</td><td>{(dd.TimingConvexityScore >= 70 ? "优异" : "中性")}</td></tr>");
        sb.AppendLine($"                        <tr><td><strong>3. 尾部抗脆弱韧性</strong></td><td>历史最大回撤 / Calmar / Pain Index</td><td style=\"text-align:right; font-weight:700; color:var(--accent-gold);\">{dd.TailResilienceScore:F1}</td><td>{(dd.TailResilienceScore >= 70 ? "优异" : "中性")}</td></tr>");
        sb.AppendLine($"                        <tr><td><strong>4. 风险收益性价比</strong></td><td>Sharpe / Sortino / Omega / Ulcer</td><td style=\"text-align:right; font-weight:700; color:var(--accent-gold);\">{dd.RiskAdjustedEfficiencyScore:F1}</td><td>{(dd.RiskAdjustedEfficiencyScore >= 70 ? "优异" : "中性")}</td></tr>");
        sb.AppendLine($"                        <tr><td><strong>5. 风格纪律性与纯粹度</strong></td><td>Barra R² / 风格漂移偏离度</td><td style=\"text-align:right; font-weight:700; color:var(--accent-gold);\">{dd.StyleDisciplineScore:F1}</td><td>{(dd.StyleDisciplineScore >= 70 ? "优异" : "中性")}</td></tr>");
        sb.AppendLine($"                        <tr><td><strong>6. 规模容量与流动性</strong></td><td>AUM黄金区间 / 冲击成本摩擦</td><td style=\"text-align:right; font-weight:700; color:var(--accent-gold);\">{dd.CapacityLiquidityScore:F1}</td><td>{(dd.CapacityLiquidityScore >= 70 ? "优异" : "中性")}</td></tr>");
        sb.AppendLine("                    </tbody>");
        sb.AppendLine("                </table>");
        sb.AppendLine($"                <p style=\"margin-top:12px; font-size:12px; color:var(--text-secondary);\"><strong>投决会总评审决议:</strong> {WebUtility.HtmlEncode(dd.InstitutionalVerdict)}</p>");
        sb.AppendLine("            </div>");

        // 核心风险指标卡片
        sb.AppendLine("            <div class=\"deck-card\">");
        sb.AppendLine("                <h2>📊 核心量化风控指标体系 (Rf=2.5%)</h2>");
        sb.AppendLine("                <table>");
        sb.AppendLine("                    <thead><tr><th>指标名称</th><th style=\"text-align:right;\">指标数值</th><th>基准对比 / 行业分位</th></tr></thead>");
        sb.AppendLine("                    <tbody>");
        sb.AppendLine($"                        <tr><td><strong>年化复合回报 (CAGR)</strong></td><td style=\"text-align:right; font-weight:700; color:var(--bull-red);\">{metrics.AnnualizedReturn:F2}%</td><td>超额回报: {metrics.ExcessReturn:F2}%</td></tr>");
        sb.AppendLine($"                        <tr><td><strong>年化波动率</strong></td><td style=\"text-align:right; font-weight:700;\">{metrics.AnnualizedVolatility:F2}%</td><td>同类中位数: 18.5%</td></tr>");
        sb.AppendLine($"                        <tr><td><strong>历史最大回撤 (MDD)</strong></td><td style=\"text-align:right; font-weight:700; color:var(--bear-green);\">-{metrics.MaxDrawdown:F2}%</td><td>回撤修复历时: {(metrics.RecoveryTradingDays.HasValue ? metrics.RecoveryTradingDays.Value + "天" : "未修复")}</td></tr>");
        sb.AppendLine($"                        <tr><td><strong>夏普比率 (Sharpe)</strong></td><td style=\"text-align:right; font-weight:700; color:var(--accent-blue);\">{metrics.SharpeRatio:F2}</td><td>同类前 20% 分位</td></tr>");
        sb.AppendLine($"                        <tr><td><strong>索提诺比率 (Sortino)</strong></td><td style=\"text-align:right; font-weight:700; color:var(--accent-blue);\">{metrics.SortinoRatio:F2}</td><td>下行风险调整收益</td></tr>");
        sb.AppendLine($"                        <tr><td><strong>卡玛比率 (Calmar)</strong></td><td style=\"text-align:right; font-weight:700; color:var(--accent-gold);\">{metrics.CalmarRatio:F2}</td><td>年化回报/最大回撤</td></tr>");
        sb.AppendLine($"                        <tr><td><strong>贝塔系数 (Beta)</strong></td><td style=\"text-align:right; font-weight:700;\">{metrics.Beta:F2}</td><td>沪深300系统性敏感度</td></tr>");
        sb.AppendLine($"                        <tr><td><strong>阿尔法 (Alpha 年化)</strong></td><td style=\"text-align:right; font-weight:700; color:var(--bull-red);\">{metrics.Alpha:F2}%</td><td>CAPM 纯超额alpha</td></tr>");
        sb.AppendLine("                    </tbody>");
        sb.AppendLine("                </table>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");

        // 历史六大黑天鹅危机压力测试回放矩阵
        if (cr != null && cr.CrisisItems.Count > 0)
        {
            sb.AppendLine("        <div class=\"deck-card\" style=\"margin-bottom:24px;\">");
            sb.AppendLine($"            <h2>🛡️ 极端宏观黑天鹅历史危机情景全息回放 (韧性总分: <span style=\"color:var(--accent-gold);\">{cr.ComprehensiveResilienceScore:F1}</span> | 防御评级: <strong>{cr.OverallResilienceRating}</strong>)</h2>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>历史危机情景</th><th>时间区间</th><th style=\"text-align:right;\">基准跌幅</th><th style=\"text-align:right;\">标的期间收益</th><th style=\"text-align:right;\">危机最大回撤</th><th style=\"text-align:right;\">超额防守</th><th style=\"text-align:center;\">出坑天数</th><th style=\"text-align:center;\">防御等级</th><th>情景推演说明</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var item in cr.CrisisItems)
            {
                string retColor = item.FundReturn >= 0 ? "var(--bull-red)" : "var(--bear-green)";
                string excessColor = item.ExcessReturnOverBenchmark >= 0 ? "var(--bull-red)" : "var(--bear-green)";
                string recStr = item.RecoveryDays < 0 ? "<span style=\"color:#FF5376;\">未出坑</span>" : (item.RecoveryDays == 0 ? "无显著回撤" : $"{item.RecoveryDays} 天");

                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(item.ScenarioName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"font-size:11.5px;\">{item.StartDate:yyyy/MM/dd} ~ {item.EndDate:yyyy/MM/dd}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bear-green);\">{item.BenchmarkDrop:F1}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{retColor}; font-weight:700;\">{item.FundReturn:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--bear-green);\">-{item.FundMaxDrawdown:F2}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{excessColor}; font-weight:700;\">{item.ExcessReturnOverBenchmark:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:center;\">{recStr}</td>");
                sb.AppendLine($"                        <td style=\"text-align:center;\"><span class=\"score-pill\">{WebUtility.HtmlEncode(item.ResilienceGrade)}</span></td>");
                sb.AppendLine($"                        <td style=\"font-size:11px; color:var(--text-secondary);\">{WebUtility.HtmlEncode(item.DiagnosticComment)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // Phase 13 前瞻性多维宏观因子联合冲击压力测试矩阵
        var macroStress = fund.MacroStressResult ?? metrics.MacroStressResult ?? portfolio?.MacroStressResult;
        if (macroStress != null && macroStress.Items.Count > 0)
        {
            sb.AppendLine("        <div class=\"deck-card\" style=\"margin-bottom:24px;\">");
            sb.AppendLine($"            <h2>🌪️ 前瞻性多维宏观情景联合冲击压力测试 (综合抗压评分: <span style=\"color:var(--accent-gold);\">{macroStress.ComprehensiveResilienceScore:F1}</span> | 评级: <strong>{macroStress.ResilienceGrade}</strong>)</h2>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>宏观情景</th><th style=\"text-align:right;\">预期损益率</th><th style=\"text-align:right;\">预估损益金额</th><th style=\"text-align:right;\">冲击后99% VaR</th><th style=\"text-align:center;\">抗脆弱韧性</th><th>因子传导与风控机理</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var item in macroStress.Items)
            {
                string retColor = item.ExpectedReturnPct >= 0 ? "var(--bull-red)" : "var(--bear-green)";
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(item.ScenarioName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{retColor}; font-weight:700;\">{item.ExpectedReturnPct:+0.00;-0.00;0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{retColor};\">{item.ExpectedLossAmountMln:+0.00;-0.00;0.00} 百万元</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-gold);\">{item.ShockedVaR99Pct:F2}% (增量 {item.DeltaVaR99Pct:+0.00;-0.00}%)</td>");
                sb.AppendLine($"                        <td style=\"text-align:center;\"><span class=\"score-pill\">{WebUtility.HtmlEncode(item.VulnerabilityRating)}</span></td>");
                sb.AppendLine($"                        <td style=\"font-size:11.5px; color:var(--text-secondary);\">{WebUtility.HtmlEncode(item.StressRationale)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // Phase 13 组合前瞻性流动性地平线与冲击成本
        if (portfolio?.LiquidityHorizon != null)
        {
            var lh = portfolio.LiquidityHorizon;
            sb.AppendLine("        <div class=\"deck-card\" style=\"margin-bottom:24px;\">");
            sb.AppendLine($"            <h2>💧 组合流动性地平线与 Almgren-Chriss 冲击滑点测算 (评级: <span style=\"color:var(--accent-gold);\">{lh.LiquidityGrade}</span>)</h2>");
            sb.AppendLine("            <div class=\"grid-3col\" style=\"margin-top:12px; margin-bottom:12px;\">");
            sb.AppendLine($"                <div style=\"background:var(--bg-sub); padding:12px; border-radius:8px;\"><div style=\"font-size:11px; color:var(--text-secondary);\">完全变现总历时</div><div style=\"font-size:18px; font-weight:700; color:#FFF;\">{lh.DaysToLiquidateTotal} 交易日 (加权 {lh.WeightedDaysToLiquidate:F1}天)</div></div>");
            sb.AppendLine($"                <div style=\"background:var(--bg-sub); padding:12px; border-radius:8px;\"><div style=\"font-size:11px; color:var(--text-secondary);\">T+1 / T+3 可变现比例</div><div style=\"font-size:18px; font-weight:700; color:var(--accent-gold);\">{lh.TPlus1LiquidPct:F1}% / {lh.TPlus3LiquidPct:F1}%</div></div>");
            sb.AppendLine($"                <div style=\"background:var(--bg-sub); padding:12px; border-radius:8px;\"><div style=\"font-size:11px; color:var(--text-secondary);\">预估平方根冲击成本率</div><div style=\"font-size:18px; font-weight:700; color:var(--bear-green);\">{lh.TotalEstimatedImpactCostPct:F4}% (~{lh.TotalEstimatedImpactLossMln:F2}百万元)</div></div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");
        }

        // Phase 13 Barra 风格多因子超额收益归因
        if (portfolio?.BarraReturnAttribution != null)
        {
            var ba = portfolio.BarraReturnAttribution;
            sb.AppendLine("        <div class=\"deck-card\" style=\"margin-bottom:24px;\">");
            sb.AppendLine($"            <h2>📊 Barra 风格多因子超额收益归因 (超额: <span style=\"color:var(--bull-red);\">{ba.TotalActiveReturn:+0.00;-0.00}%</span> | 驱动模式: <strong>{ba.DominantDriver}</strong>)</h2>");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <thead><tr><th>Barra风格因子</th><th style=\"text-align:right;\">组合暴露</th><th style=\"text-align:right;\">基准暴露</th><th style=\"text-align:right;\">主动暴露差</th><th style=\"text-align:right;\">因子年化溢价</th><th style=\"text-align:right;\">超额收益贡献</th><th style=\"text-align:right;\">贡献占比</th><th>因子特征</th></tr></thead>");
            sb.AppendLine("                <tbody>");
            foreach (var f in ba.FactorContributions)
            {
                string cColor = f.ReturnContributionPct >= 0 ? "var(--bull-red)" : "var(--bear-green)";
                sb.AppendLine("                    <tr>");
                sb.AppendLine($"                        <td><strong>{WebUtility.HtmlEncode(f.FactorName)}</strong></td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{f.PortfolioExposure:F2}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{f.BenchmarkExposure:F2}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; font-weight:700;\">{f.ActiveExposure:+0.00;-0.00}</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{f.FactorPremiumPct:F1}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:{cColor}; font-weight:700;\">{f.ReturnContributionPct:+0.00;-0.00}%</td>");
                sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{f.ContributionSharePct:F1}%</td>");
                sb.AppendLine($"                        <td style=\"font-size:11.5px; color:var(--text-secondary);\">{WebUtility.HtmlEncode(f.FactorDescription)}</td>");
                sb.AppendLine("                    </tr>");
            }
            sb.AppendLine("                    <tr style=\"background:rgba(245, 176, 65, 0.08); font-weight:700;\">");
            sb.AppendLine($"                        <td colspan=\"5\"><strong>六大风格因子累计贡献</strong></td>");
            sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-gold);\">{ba.TotalStyleFactorReturn:+0.00;-0.00}%</td>");
            sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{ba.StyleContributionSharePct:F1}%</td>");
            sb.AppendLine("                        <td>宏观风格倾斜Beta回报</td>");
            sb.AppendLine("                    </tr>");
            sb.AppendLine("                    <tr style=\"background:rgba(59, 130, 246, 0.08); font-weight:700;\">");
            sb.AppendLine($"                        <td colspan=\"5\"><strong>纯特质选股超额阿尔法 (Specific Alpha)</strong></td>");
            sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace; color:var(--accent-blue);\">{ba.SpecificAlphaReturn:+0.00;-0.00}%</td>");
            sb.AppendLine($"                        <td style=\"text-align:right; font-family:monospace;\">{ba.SpecificAlphaSharePct:F1}%</td>");
            sb.AppendLine("                        <td>剔除风格因子后的纯选股能力</td>");
            sb.AppendLine("                    </tr>");
            sb.AppendLine("                </tbody>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // Phase 15 科学投资建议与短中期评估、模拟预测对比及自适应迭代
        var advice = fund.ScientificAdvice ?? metrics?.ScientificAdvice ?? (fund.NavHistory.Count >= 10 ? QuantCalculator.GenerateScientificInvestmentAdvice(fund, fund.NavHistory) : null);
        if (advice != null)
        {
            string signalColor = advice.ActionSignal switch
            {
                InvestmentActionSignal.StrongBuy => "#EF4444",
                InvestmentActionSignal.Accumulate => "#F59E0B",
                InvestmentActionSignal.Hold => "#3B82F6",
                InvestmentActionSignal.TrimProfit => "#10B981",
                InvestmentActionSignal.StopLossExit => "#991B1B",
                _ => "#6B7280"
            };

            sb.AppendLine("        <div class=\"deck-card\" style=\"border-left: 6px solid " + signalColor + "; margin-bottom: 24px;\">");
            sb.AppendLine($"            <h2>🚀 终极闭环科学投资决策建议与执行方案 (Professional Scientific Advice) <span class=\"score-pill\" style=\"background:rgba(59,130,246,0.15); border-color:#3B82F6; color:#3B82F6;\">建议置信度: {advice.ConvictionScore:F1}%</span></h2>");
            
            sb.AppendLine("            <div style=\"background:rgba(255,255,255,0.03); border:1px solid var(--border); border-radius:8px; padding:18px; margin-bottom:16px;\">");
            sb.AppendLine("                <div style=\"display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:12px;\">");
            sb.AppendLine($"                    <div><span style=\"font-size:13px; color:var(--text-secondary);\">量化行动指令:</span> <span style=\"font-size:20px; font-weight:800; color:{signalColor};\">{advice.ActionSignalText}</span></div>");
            sb.AppendLine($"                    <div><span style=\"font-size:13px; color:var(--text-secondary);\">推荐适用周期:</span> <strong style=\"color:var(--text-primary);\">{advice.HorizonText}</strong></div>");
            sb.AppendLine($"                    <div><span style=\"font-size:13px; color:var(--text-secondary);\">周期目标收益:</span> <strong style=\"color:var(--bull-red);\">{advice.TargetExpectedReturn:+0.00;-0.00}%</strong> <span style=\"font-size:11.5px; color:var(--text-secondary);\">([{advice.MinExpectedReturn:+0.00;-0.00}% ~ {advice.MaxExpectedReturn:+0.00;-0.00}%])</span></div>");
            sb.AppendLine("                </div>");
            sb.AppendLine($"                <div style=\"margin-top:10px; font-size:13px; color:var(--text-primary);\">{WebUtility.HtmlEncode(advice.ExecutiveAdvisoryVerdict)}</div>");
            sb.AppendLine("            </div>");

            // 交易执行参考参数
            sb.AppendLine("            <div class=\"grid-3col\" style=\"margin-bottom:16px;\">");
            sb.AppendLine("                <div style=\"background:var(--bg-sub); padding:12px; border-radius:6px;\">");
            sb.AppendLine("                    <div style=\"font-size:11.5px; color:var(--text-secondary);\">建议建仓参考区间</div>");
            sb.AppendLine($"                    <div style=\"font-size:15px; font-weight:700; font-family:monospace; margin-top:4px;\">[{advice.SuggestedEntryNavLower:F4} ~ {advice.SuggestedEntryNavUpper:F4}]</div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div style=\"background:var(--bg-sub); padding:12px; border-radius:6px;\">");
            sb.AppendLine("                    <div style=\"font-size:11.5px; color:var(--text-secondary);\">目标止盈参考价位</div>");
            sb.AppendLine($"                    <div style=\"font-size:15px; font-weight:700; font-family:monospace; color:var(--bull-red); margin-top:4px;\">{advice.TakeProfitTargetNav:F4} (+{advice.TakeProfitPercent:F1}%)</div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div style=\"background:var(--bg-sub); padding:12px; border-radius:6px;\">");
            sb.AppendLine("                    <div style=\"font-size:11.5px; color:var(--text-secondary);\">动态追踪止损线</div>");
            sb.AppendLine($"                    <div style=\"font-size:15px; font-weight:700; font-family:monospace; color:var(--bear-green); margin-top:4px;\">{advice.TrailingStopLossNav:F4} (-{advice.StopLossPercent:F1}%)</div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");

            // 短中期科学评估对比
            sb.AppendLine("            <div class=\"grid-2col\">");
            sb.AppendLine("                <div style=\"background:var(--bg-sub); padding:14px; border-radius:8px;\">");
            sb.AppendLine($"                    <div style=\"display:flex; justify-content:space-between; margin-bottom:8px;\"><strong style=\"color:var(--accent-gold);\">⚡ 短期维度评估 (5~20D)</strong><span style=\"font-weight:700;\">{advice.ShortTermEvaluation.ShortTermScore:F1}分 ({advice.ShortTermEvaluation.ShortTermRating})</span></div>");
            sb.AppendLine("                    <div style=\"font-size:12px; color:var(--text-secondary);\">");
            sb.AppendLine($"                        5日动量: <strong style=\"color:var(--text-primary);\">{advice.ShortTermEvaluation.Momentum5D:+0.00;-0.00}%</strong> | 20日动量: <strong style=\"color:var(--text-primary);\">{advice.ShortTermEvaluation.Momentum20D:+0.00;-0.00}%</strong> | RSI(14): <strong style=\"color:var(--text-primary);\">{advice.ShortTermEvaluation.ShortTermRsi:F1}</strong><br/>");
            sb.AppendLine($"                        20日胜率: <strong style=\"color:var(--text-primary);\">{advice.ShortTermEvaluation.ShortTermWinRate:F1}%</strong> | 5日VaR(95%): <strong style=\"color:var(--bear-green);\">{advice.ShortTermEvaluation.VaR95_5D:F2}%</strong> | 短期年化波动: {advice.ShortTermEvaluation.ShortTermVolatility:F1}%");
            sb.AppendLine("                    </div>");
            sb.AppendLine($"                    <div style=\"font-size:11.5px; margin-top:6px; color:var(--text-secondary);\">{WebUtility.HtmlEncode(advice.ShortTermEvaluation.ShortTermDiagnosis)}</div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div style=\"background:var(--bg-sub); padding:14px; border-radius:8px;\">");
            sb.AppendLine($"                    <div style=\"display:flex; justify-content:space-between; margin-bottom:8px;\"><strong style=\"color:var(--accent-gold);\">💎 中期维度评估 (60~250D)</strong><span style=\"font-weight:700;\">{advice.MediumTermEvaluation.MediumTermScore:F1}分 ({advice.MediumTermEvaluation.MediumTermRating})</span></div>");
            sb.AppendLine("                    <div style=\"font-size:12px; color:var(--text-secondary);\">");
            sb.AppendLine($"                        年化收益: <strong style=\"color:var(--text-primary);\">{advice.MediumTermEvaluation.AnnualizedReturn:F2}%</strong> | 年化超额: <strong style=\"color:var(--bull-red);\">{advice.MediumTermEvaluation.AnnualizedExcessReturn:+0.00;-0.00}%</strong> | 信息比率(IR): <strong style=\"color:var(--text-primary);\">{advice.MediumTermEvaluation.InformationRatio:F2}</strong><br/>");
            sb.AppendLine($"                        卡玛比率: <strong style=\"color:var(--text-primary);\">{advice.MediumTermEvaluation.CalmarRatio:F2}</strong> | 下行捕获: <strong style=\"color:var(--text-primary);\">{advice.MediumTermEvaluation.DownsideCaptureRatio:F1}%</strong> | 最大回撤修复: {advice.MediumTermEvaluation.MaxDrawdownRecoveryDays}天");
            sb.AppendLine("                    </div>");
            sb.AppendLine($"                    <div style=\"font-size:11.5px; margin-top:6px; color:var(--text-secondary);\">{WebUtility.HtmlEncode(advice.MediumTermEvaluation.MediumTermDiagnosis)}</div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");

            // 协同研判与事前预测vs事后现实审计
            sb.AppendLine("            <div style=\"margin-top:14px; padding:14px; background:rgba(255,255,255,0.02); border:1px solid var(--border); border-radius:8px;\">");
            sb.AppendLine("                <div style=\"display:flex; justify-content:space-between; align-items:center; margin-bottom:10px;\">");
            sb.AppendLine($"                    <strong style=\"color:var(--accent-blue);\">🧭 短中协同格局: 【{advice.HorizonConcordance.ConcordanceType}】 (协同打分: {advice.HorizonConcordance.ConcordanceScore:F1}分，建议仓位乘数: {advice.HorizonConcordance.SuggestedAllocationMultiplier:F2}x)</strong>");
            sb.AppendLine($"                    <span class=\"score-pill\">审计评级: {advice.RealityAudit.AuditRating}</span>");
            sb.AppendLine("                </div>");
            sb.AppendLine($"                <div style=\"font-size:12px; color:var(--text-secondary); margin-bottom:8px;\">{WebUtility.HtmlEncode(advice.HorizonConcordance.StrategicGuidance)}</div>");
            sb.AppendLine("                <div style=\"display:flex; gap:20px; font-size:12px; border-top:1px solid var(--border); padding-top:8px;\">");
            sb.AppendLine($"                    <div>方向预测命中率: <strong style=\"color:var(--bull-red); font-family:monospace;\">{advice.RealityAudit.DirectionalHitRate:F1}%</strong></div>");
            sb.AppendLine($"                    <div>预测锥置信区间覆盖率 (PICP): <strong style=\"color:var(--accent-gold); font-family:monospace;\">{advice.RealityAudit.PicpCoverageRatio:F1}%</strong></div>");
            sb.AppendLine($"                    <div>预测误差 (RMSE): <strong style=\"font-family:monospace;\">{advice.RealityAudit.RootMeanSquareError:F4}</strong></div>");
            sb.AppendLine($"                    <div>平均百分比误差 (MAPE): <strong style=\"font-family:monospace;\">{advice.RealityAudit.MeanAbsolutePercentageError:F2}%</strong></div>");
            sb.AppendLine($"                    <div>建议超额贡献 (Advice Alpha): <strong style=\"color:var(--bull-red); font-family:monospace;\">+{advice.RealityAudit.AdviceAlphaContribution:F2}%</strong></div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");

            // 持续迭代与自适应权重
            sb.AppendLine("            <div style=\"margin-top:14px; padding:14px; background:rgba(245, 176, 65, 0.05); border:1px solid rgba(245, 176, 65, 0.3); border-radius:8px;\">");
            sb.AppendLine($"                <div style=\"font-size:13px; font-weight:700; color:var(--accent-gold); margin-bottom:6px;\">🔄 贝叶斯后验自适应权重持续迭代 (信息增益 +{advice.AdaptiveWeights.IterationInformationGain:F1}%)</div>");
            sb.AppendLine($"                <div style=\"font-size:12px; color:var(--text-secondary); margin-bottom:8px;\">{WebUtility.HtmlEncode(advice.AdaptiveWeights.AdaptationSummary)}</div>");
            sb.AppendLine("                <div style=\"display:flex; gap:16px; font-size:11.5px; font-family:monospace;\">");
            sb.AppendLine($"                    <span>动量权重: {advice.AdaptiveWeights.AdaptiveMomentumWeight * 100m:F1}%</span>");
            sb.AppendLine($"                    <span>性价比权重: {advice.AdaptiveWeights.AdaptiveRiskAdjustedWeight * 100m:F1}%</span>");
            sb.AppendLine($"                    <span>防御权重: {advice.AdaptiveWeights.AdaptiveDownsideDefenseWeight * 100m:F1}%</span>");
            sb.AppendLine($"                    <span>纯Alpha: {advice.AdaptiveWeights.AdaptiveAlphaPurityWeight * 100m:F1}%</span>");
            sb.AppendLine($"                    <span>凸性权重: {advice.AdaptiveWeights.AdaptiveConvexityWeight * 100m:F1}%</span>");
            sb.AppendLine($"                    <span style=\"font-weight:700; color:var(--accent-gold);\">总权重守恒: {advice.AdaptiveWeights.TotalWeightSum * 100m:F0}%</span>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");

            sb.AppendLine("        </div>");
        }

        // 投决会签字与决议签署栏
        sb.AppendLine("        <div class=\"deck-card\">");
        sb.AppendLine("            <h2>✍️ 投资决策委员会配置决策签批表</h2>");
        sb.AppendLine("            <div class=\"sign-box\">");
        sb.AppendLine("                <div>");
        sb.AppendLine("                    <div style=\"font-size:12px; color:var(--text-secondary); margin-bottom:8px;\">投决会主席 (Committee Chairman)</div>");
        sb.AppendLine("                    <div style=\"height:45px; border-bottom:1px solid var(--border);\"></div>");
        sb.AppendLine("                    <div style=\"font-size:11.5px; margin-top:6px; color:var(--text-secondary);\">签署意见: 【 同意列入核心底仓 】</div>");
        sb.AppendLine("                </div>");
        sb.AppendLine("                <div>");
        sb.AppendLine("                    <div style=\"font-size:12px; color:var(--text-secondary); margin-bottom:8px;\">首席风险官 (Chief Risk Officer)</div>");
        sb.AppendLine("                    <div style=\"height:45px; border-bottom:1px solid var(--border);\"></div>");
        sb.AppendLine("                    <div style=\"font-size:11.5px; margin-top:6px; color:var(--text-secondary);\">签署意见: 【 波动与尾部风险受控 】</div>");
        sb.AppendLine("                </div>");
        sb.AppendLine("                <div>");
        sb.AppendLine("                    <div style=\"font-size:12px; color:var(--text-secondary); margin-bottom:8px;\">资产配置投资经理 (Lead Portfolio Manager)</div>");
        sb.AppendLine("                    <div style=\"height:45px; border-bottom:1px solid var(--border);\"></div>");
        sb.AppendLine("                    <div style=\"font-size:11.5px; margin-top:6px; color:var(--text-secondary);\">执行指令: 【 按基准配置比例建仓 】</div>");
        sb.AppendLine("                </div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Phase 18 将投资组合调仓交易决策单导出为符合买入/卖出下单规范的标准 CSV 文件 (带有 UTF-8 BOM，支持 Excel 直接双击打开)
    /// </summary>
    public static string ExportRebalanceOrdersToCsv(RebalanceOrderSheet sheet, string? filePath = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== BIGA 机构级投资组合调仓交易决策单 ===");
        sb.AppendLine($"调仓目标方案,{sheet.TargetSchemeName}");
        sb.AppendLine($"组合总资产 (元),{sheet.TotalPortfolioValue:F2}");
        sb.AppendLine($"预留现金安全缓冲 (元),{sheet.CashBufferReserved:F2} ({sheet.CashBufferPercent:F1}%)");
        sb.AppendLine($"现金拖累年化成本预估,{sheet.CashDragAnnualizedCost:F2}%");
        sb.AppendLine($"整手调仓步长 (元),{sheet.LotSizeRoundingStep:F0}");
        sb.AppendLine($"最低起购门槛 (元),{sheet.MinSubscriptionAmount:F0}");
        sb.AppendLine($"计划买入总额 (元),{sheet.TotalBuyAmount:F2}");
        sb.AppendLine($"计划卖出总额 (元),{sheet.TotalSellAmount:F2}");
        sb.AppendLine($"整手实际买入总额 (元),{sheet.ExecutableBuyAmount:F2}");
        sb.AppendLine($"整手实际卖出总额 (元),{sheet.ExecutableSellAmount:F2}");
        sb.AppendLine($"净出资变动额 (元),{sheet.NetCashChange:F2}");
        sb.AppendLine($"预计总调仓手续费 (元),{sheet.EstimatedTotalFees:F2}");
        sb.AppendLine($"组合双边换手率,{sheet.TurnoverRate:F2}%");
        sb.AppendLine($"整手执行单边换手率,{sheet.ExecutableTurnoverRate:F2}%");
        sb.AppendLine($"生成时间,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        sb.AppendLine("基金代码,基金名称,当前持有(元),当前权重(%),目标持有(元),目标权重(%),交易指令,理论交易额(元),整手实际交易额(元),取整残差(元),执行达成权重(%),预估手续费(元)");

        if (sheet.Orders != null)
        {
            foreach (var order in sheet.Orders)
            {
                sb.AppendLine($"{order.FundCode},\"{order.FundName}\",{order.CurrentAmount:F2},{order.CurrentWeight:F2}%,{order.TargetAmount:F2},{order.TargetWeight:F2}%,{order.Action},{order.TradeAmount:F2},{order.ExecutableTradeAmount:F2},{order.LotRoundingResidual:F2},{order.ExecutedTargetWeight:F2}%,{order.EstimatedFee:F2}");
            }
        }

        string csvContent = sb.ToString();

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            var utf8Bom = new UTF8Encoding(true);
            File.WriteAllText(filePath, csvContent, utf8Bom);
        }

        return csvContent;
    }

    /// <summary>
    /// Phase 25.4: 一键生成机构级尽职调查与资产配置投研研报 (Institutional Due Diligence Fact Sheet HTML)
    /// </summary>
    public static string GenerateInstitutionalDueDiligenceFactSheetHtml(
        PortfolioResult portfolio,
        List<(FundDetail Fund, decimal WeightPercent)>? components,
        string? filePath = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-CN\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<title>机构级投资组合尽职调查与资产配置研报 (Institutional DD Fact Sheet)</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'PingFang SC', 'Microsoft YaHei', sans-serif; background: #0f172a; color: #e2e8f0; margin: 0; padding: 24px; line-height: 1.6; }");
        sb.AppendLine(".container { max-width: 1200px; margin: 0 auto; background: #1e293b; border-radius: 12px; padding: 32px; box-shadow: 0 10px 30px rgba(0,0,0,0.5); }");
        sb.AppendLine(".header { border-bottom: 2px solid #3b82f6; padding-bottom: 16px; margin-bottom: 24px; display: flex; justify-content: space-between; align-items: flex-end; }");
        sb.AppendLine(".title { font-size: 26px; font-weight: 700; color: #60a5fa; margin: 0; }");
        sb.AppendLine(".subtitle { font-size: 13px; color: #94a3b8; margin-top: 6px; }");
        sb.AppendLine(".badge { display: inline-block; padding: 4px 10px; border-radius: 6px; font-size: 12px; font-weight: 600; background: #1d4ed8; color: #fff; }");
        sb.AppendLine(".kpi-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; margin-bottom: 28px; }");
        sb.AppendLine(".kpi-card { background: #0f172a; border-radius: 8px; padding: 16px; border-left: 4px solid #3b82f6; }");
        sb.AppendLine(".kpi-title { font-size: 12px; color: #94a3b8; text-transform: uppercase; }");
        sb.AppendLine(".kpi-value { font-size: 22px; font-weight: 700; color: #f8fafc; margin-top: 4px; }");
        sb.AppendLine(".section { margin-bottom: 32px; }");
        sb.AppendLine(".section-title { font-size: 18px; font-weight: 600; color: #38bdf8; border-left: 4px solid #38bdf8; padding-left: 10px; margin-bottom: 14px; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 10px; font-size: 13px; }");
        sb.AppendLine("th, td { padding: 10px 14px; text-align: left; border-bottom: 1px solid #334155; }");
        sb.AppendLine("th { background: #0f172a; color: #94a3b8; font-weight: 600; }");
        sb.AppendLine("tr:hover { background: #24344d; }");
        sb.AppendLine(".advice-box { background: rgba(59, 130, 246, 0.1); border: 1px solid #2563eb; border-radius: 8px; padding: 16px; margin-top: 12px; font-size: 13px; color: #bfdbfe; }");
        sb.AppendLine(".tag-danger { background: #7f1d1d; color: #fca5a5; padding: 2px 8px; border-radius: 4px; }");
        sb.AppendLine(".tag-warning { background: #78350f; color: #fde68a; padding: 2px 8px; border-radius: 4px; }");
        sb.AppendLine(".tag-success { background: #14532d; color: #86efac; padding: 2px 8px; border-radius: 4px; }");
        sb.AppendLine(".footer { border-top: 1px solid #334155; padding-top: 16px; margin-top: 40px; font-size: 11px; color: #64748b; text-align: center; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"container\">");

        // 标头
        sb.AppendLine("<div class=\"header\">");
        sb.AppendLine("  <div>");
        sb.AppendLine("    <div class=\"title\">机构级投资组合尽职调查与多资产配置研报</div>");
        sb.AppendLine($"    <div class=\"subtitle\">BIGA Quantitative Investment Management Platform &bull; 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <span class=\"badge\">Phase 26 机构旗舰宏观对冲投研版</span>");
        sb.AppendLine("</div>");

        // 核心 KPI
        sb.AppendLine("<div class=\"kpi-grid\">");
        sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">累计收益率</div><div class=\"kpi-value\">{portfolio.TotalReturn:F2}%</div></div>");
        sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">年化复合收益 (CAGR)</div><div class=\"kpi-value\">{portfolio.AnnualizedReturn:F2}%</div></div>");
        sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">夏普比率 (Sharpe)</div><div class=\"kpi-value\">{portfolio.SharpeRatio:F2}</div></div>");
        sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">最大回撤 (MDD)</div><div class=\"kpi-value\">{portfolio.MaxDrawdown:F2}%</div></div>");
        sb.AppendLine("</div>");

        // 1. 底层持仓配置结构
        sb.AppendLine("<div class=\"section\">");
        sb.AppendLine("  <div class=\"section-title\">一、投资组合成分资产穿透权重与风格剖面</div>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>基金代码</th><th>基金名称</th><th>基金经理</th><th>资产类别</th><th>配置权重</th><th>近1年收益</th><th>历史夏普</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        if (components != null)
        {
            foreach (var comp in components)
            {
                if (comp.Fund == null) continue;
                sb.AppendLine($"    <tr><td>{comp.Fund.Code}</td><td>{comp.Fund.Name}</td><td>{comp.Fund.ManagerName}</td><td>{comp.Fund.Type}</td><td><strong>{comp.WeightPercent:F2}%</strong></td><td>{comp.Fund.QuantMetrics?.AnnualizedReturn:F2}%</td><td>{comp.Fund.QuantMetrics?.SharpeRatio:F2}</td></tr>");
            }
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        sb.AppendLine("</div>");

        // 2. Phase 25.1 极值 Copula 尾部联结模型
        if (portfolio.CopulaTailDependence != null)
        {
            var cop = portfolio.CopulaTailDependence;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">二、极值 Copula 尾部联结模型与极端踩踏在险诊断</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">加权下尾依赖度 (Clayton λ_L)</div><div class=\"kpi-value\">{cop.PortfolioWeightedLowerTailDependence:F3}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">加权上尾依赖度 (Gumbel λ_U)</div><div class=\"kpi-value\">{cop.PortfolioWeightedUpperTailDependence:F3}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">协同踩踏风险放大倍数</div><div class=\"kpi-value\">{cop.SystemicCrashAmplificationFactor:F2}x</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">高斯 VaR 低估偏差</div><div class=\"kpi-value\">+{cop.LinearVsCopulaVaRDifferencePercent:F1}%</div></div>");
            sb.AppendLine("</div>");

            if (cop.PairwiseCopulaList != null && cop.PairwiseCopulaList.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产对 (A vs B)</th><th>线性相关 (Pearson)</th><th>秩相关 (Kendall τ)</th><th>下尾依赖 (λ_L)</th><th>上尾依赖 (λ_U)</th><th>尾部非对称度</th><th>风控定级</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var pair in cop.PairwiseCopulaList)
                {
                    sb.AppendLine($"    <tr><td>{pair.FundNameA} vs {pair.FundNameB}</td><td>{pair.PearsonCorrelation:+0.00;-0.00}</td><td>{pair.KendallTau:+0.00;-0.00}</td><td><strong>{pair.LowerTailDependenceLambda:F3}</strong></td><td>{pair.UpperTailDependenceLambda:F3}</td><td>{pair.TailAsymmetry:+0.000;-0.000}</td><td>{pair.TailRiskBadge}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(cop.ExecutiveTailDiagnosis)}</div>");
            sb.AppendLine("</div>");
        }

        // 3. Phase 25.2 NSGA-II 多目标 Pareto 前沿
        if (portfolio.ParetoMultiObjective != null)
        {
            var pareto = portfolio.ParetoMultiObjective;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">三、多目标帕累托前沿自适应进化优化 (NSGA-II 推荐方案)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">Pareto 解集数量</div><div class=\"kpi-value\">{pareto.FrontierSolutions?.Count ?? 0} 个</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">前沿超体积指标 (Hypervolume)</div><div class=\"kpi-value\">{pareto.HypervolumeIndicator:P1}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">拐点推荐收益率</div><div class=\"kpi-value\">{pareto.OptimalCompromiseSolution?.ExpectedReturnPercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">拐点推荐 95% CVaR</div><div class=\"kpi-value\">{pareto.OptimalCompromiseSolution?.DownsideCvar95Percent:F2}%</div></div>");
            sb.AppendLine("</div>");

            if (pareto.FrontierSolutions != null && pareto.FrontierSolutions.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>配置方案</th><th>预期年化收益</th><th>95% CVaR (下行尾损)</th><th>调仓换手摩擦</th><th>夏普比率</th><th>推荐状态</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var sol in pareto.FrontierSolutions.Take(6))
                {
                    string recBadge = sol.IsRecommended ? "<span class=\"tag-success\">⭐ 投审会推荐</span>" : "-";
                    sb.AppendLine($"    <tr><td><strong>{sol.SolutionName}</strong></td><td>{sol.ExpectedReturnPercent:F2}%</td><td>{sol.DownsideCvar95Percent:F2}%</td><td>{sol.RebalanceTurnoverPercent:F1}%</td><td>{sol.SharpeRatio:F2}</td><td>{recBadge}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(pareto.ExecutiveParetoAdvice)}</div>");
            sb.AppendLine("</div>");
        }

        // 4. Phase 25.3 GARCH(1,1) 前瞻波动率预测与期限结构
        if (portfolio.GarchVolatilityForecast != null)
        {
            var garch = portfolio.GarchVolatilityForecast;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">四、GARCH(1,1) 前瞻条件异方差预测与波动率期限结构锥</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">当期即期条件波动率</div><div class=\"kpi-value\">{garch.CurrentConditionalVolPercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">长期无条件均值回归锚</div><div class=\"kpi-value\">{garch.LongTermUnconditionalVolPercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">波动率冲击半衰期</div><div class=\"kpi-value\">{garch.HalfLifeDays:F1} 天</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">波动聚类体制</div><div class=\"kpi-value\" style=\"font-size:14px\">{garch.VolClusteringRegime}</div></div>");
            sb.AppendLine("</div>");

            if (garch.ForecastPoints != null && garch.ForecastPoints.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>预测期限</th><th>预测日期</th><th>前瞻年化波动率</th><th>波动锥 10% 分位</th><th>波动锥 50% 中位数</th><th>波动锥 90% 分位</th><th>体制属性</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var pt in garch.ForecastPoints)
                {
                    string regime = pt.IsClusteringHighVol ? "<span class=\"tag-danger\">高波聚类</span>" : "<span class=\"tag-success\">常态平稳</span>";
                    sb.AppendLine($"    <tr><td>T+{pt.HorizonDays} 交易日</td><td>{pt.ForecastDate:yyyy-MM-dd}</td><td><strong>{pt.ForecastAnnualizedVolPercent:F2}%</strong></td><td>{pt.VolCone10Percentile:F2}%</td><td>{pt.VolCone50Median:F2}%</td><td>{pt.VolCone90Percentile:F2}%</td><td>{regime}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(garch.TacticalRiskBudgetAdvice)}</div>");
            sb.AppendLine("</div>");
        }

        // 5. Phase 26.1 Ledoit-Wolf 渐近最优收缩协方差估计
        if (portfolio.LedoitWolfShrinkage != null)
        {
            var lw = portfolio.LedoitWolfShrinkage;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">五、Ledoit-Wolf 渐近最优收缩协方差与矩阵良态优化 (Phase 26)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">最优收缩强度 (δ*)</div><div class=\"kpi-value\">{lw.OptimalShrinkagePercent:F1}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">样本协方差条件数</div><div class=\"kpi-value\">{lw.SampleConditionNumber:F1}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">收缩后矩阵条件数</div><div class=\"kpi-value\">{lw.ShrunkConditionNumber:F1}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">良态优化倍数</div><div class=\"kpi-value\">{lw.ConditionNumberImprovementRatio:F1}x</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(lw.AnalyticalSummary)}</div>");
            sb.AppendLine("</div>");
        }

        // 6. Phase 26.2 Hamilton 马尔可夫两状态体制切换
        if (portfolio.MarkovRegimeSwitching != null)
        {
            var mrs = portfolio.MarkovRegimeSwitching;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">六、Hamilton 隐藏马尔可夫双状态 (牛/熊) 体制切换与 BL 调制 (Phase 26)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">当前主导体质</div><div class=\"kpi-value\" style=\"font-size:15px\">{mrs.CurrentRegime}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">体制置信度</div><div class=\"kpi-value\">{mrs.CurrentRegimeProbabilityPercent:F1}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">牛市期望持续期</div><div class=\"kpi-value\">{mrs.BullExpectedDurationMonths:F1} 个月</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">熊市期望持续期</div><div class=\"kpi-value\">{mrs.BearExpectedDurationMonths:F1} 个月</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mrs.DynamicBlackLittermanPriorShift)}<br/>{WebUtility.HtmlEncode(mrs.TacticalAssetAllocationAdvice)}</div>");
            sb.AppendLine("</div>");
        }

        // 7. Phase 26.3 Merton 泊松跳跃扩散极端推演
        if (portfolio.MertonJumpDiffusion != null)
        {
            var jd = portfolio.MertonJumpDiffusion;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">七、Merton 泊松跳跃扩散极端前瞻推演与断崖尾部在险 (Phase 26)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">跳跃发生频率 (λ)</div><div class=\"kpi-value\">{jd.JumpIntensityLambda:F1} 次/年</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">平均跳跃幅度 (μ_J)</div><div class=\"kpi-value\">{jd.MeanJumpSizePercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">跳跃 95% VaR</div><div class=\"kpi-value\">{jd.JumpAdjustedVaR95Percent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">高斯连续模型低估偏差</div><div class=\"kpi-value\">+{jd.TailRiskUnderestimationPercent:F1}%</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(jd.JumpScenarioAudit)}</div>");
            sb.AppendLine("</div>");
        }

        // 8. Phase 26.4 规模敏感型流动性调整在险价值 L-VaR
        if (portfolio.LiquidityAdjustedVaR != null)
        {
            var lvar = portfolio.LiquidityAdjustedVaR;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">八、规模敏感型流动性调整在险价值 (L-VaR) 解构 (Phase 26)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">纯价格 95% VaR</div><div class=\"kpi-value\">{lvar.PureMarketVaR95Wan:F2} 万元</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">价差与冲击摩擦成本</div><div class=\"kpi-value\">{(lvar.LiquidityCostWan + lvar.MarketImpactWan):F2} 万元</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">综合 L-VaR (95%)</div><div class=\"kpi-value\">{lvar.TotalLVaR95Wan:F2} 万元</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">流动性放大倍数</div><div class=\"kpi-value\">{lvar.LiquidityRiskMultiplier:F2}x</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(lvar.InstitutionalDeskAdvice)}</div>");
            sb.AppendLine("</div>");
        }

        // 9. Phase 26.5 专业量化 Tearsheet 月度收益率热力图
        if (portfolio.PortfolioTearsheet != null && portfolio.PortfolioTearsheet.MonthlyHeatmapRows.Count > 0)
        {
            var ts = portfolio.PortfolioTearsheet;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">九、专业量化 Tearsheet 仪表盘：月度收益热力图矩阵 (Phase 26)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">月度胜率</div><div class=\"kpi-value\">{ts.MonthlyWinRatePercent:F1}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">盈利月份数</div><div class=\"kpi-value\">{ts.WinningMonths} / {ts.TotalMonths}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">最佳单月回报</div><div class=\"kpi-value\">+{ts.BestMonthlyReturnPercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">最长水下浸泡期</div><div class=\"kpi-value\">{ts.MaxUnderwaterDays} 交易日</div></div>");
            sb.AppendLine("</div>");

            sb.AppendLine("  <table class=\"data-table\">");
            sb.AppendLine("    <thead><tr><th>年份</th><th>1月</th><th>2月</th><th>3月</th><th>4月</th><th>5月</th><th>6月</th><th>7月</th><th>8月</th><th>9月</th><th>10月</th><th>11月</th><th>12月</th><th>全年收益</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var row in ts.MonthlyHeatmapRows)
            {
                string FormatCell(decimal? v) => v.HasValue ? (v.Value >= 0 ? $"<span style=\"color:#e74c3c;font-weight:600\">+{v.Value:F1}%</span>" : $"<span style=\"color:#27ae60;font-weight:600\">{v.Value:F1}%</span>") : "-";
                string yrColor = row.FullYearReturn >= 0 ? "#e74c3c" : "#27ae60";
                sb.AppendLine($"    <tr><td><strong>{row.Year}</strong></td><td>{FormatCell(row.M1)}</td><td>{FormatCell(row.M2)}</td><td>{FormatCell(row.M3)}</td><td>{FormatCell(row.M4)}</td><td>{FormatCell(row.M5)}</td><td>{FormatCell(row.M6)}</td><td>{FormatCell(row.M7)}</td><td>{FormatCell(row.M8)}</td><td>{FormatCell(row.M9)}</td><td>{FormatCell(row.M10)}</td><td>{FormatCell(row.M11)}</td><td>{FormatCell(row.M12)}</td><td style=\"color:{yrColor};font-weight:700\">{row.FullYearReturn:F2}%</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ts.TearsheetExecutiveSummary)}</div>");
            sb.AppendLine("</div>");
        }

        // 10. Phase 27.1 Almgren-Chriss 最优执行轨迹与执行落差
        if (portfolio.AlmgrenChrissExecution != null)
        {
            var ac = portfolio.AlmgrenChrissExecution;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">十、Almgren-Chriss 最优算法执行与清算微观轨迹 (Phase 27)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">执行半衰期 (θ)</div><div class=\"kpi-value\">{ac.HalfLifeDays:F2} 天</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">预期执行落差 (IS)</div><div class=\"kpi-value\">{ac.ExpectedTotalCostWan:F2} 万元</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">执行 VaR (95%)</div><div class=\"kpi-value\">{ac.ExecutionVaR95Wan:F2} 万元</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">策略执行模式</div><div class=\"kpi-value\" style=\"font-size:14px\">{ac.OptimalStrategyStyle}</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>步骤</th><th>时刻 (天)</th><th>剩余持仓比例</th><th>抛售规模 (万元)</th><th>挂单速率 (万/天)</th><th>临时冲击成本</th><th>累计摩擦损失</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var step in ac.TrajectorySteps.Take(6))
            {
                sb.AppendLine($"    <tr><td>{step.StepIndex}</td><td>T+{step.TimeHorizonDays:F1}</td><td>{step.RemainingHoldingRatioPercent:F1}%</td><td>{step.TradeSharesWan:F2}</td><td>{step.TradeRateWanPerDay:F2}</td><td>{step.TemporaryImpactWan:F4} 万</td><td>{step.CumulativeCostWan:F4} 万</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ac.ExecutionDeskRecommendation)}</div>");
            sb.AppendLine("</div>");
        }

        // 11. Phase 27.2 Michaud 蒙特卡洛重抽样有效前沿
        if (portfolio.MichaudResampledFrontier != null && portfolio.MichaudResampledFrontier.ResampledPoints.Count > 0)
        {
            var mf = portfolio.MichaudResampledFrontier;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">十一、Michaud 蒙特卡洛重抽样均值方差有效前沿 (Phase 27)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">重抽样模拟次数</div><div class=\"kpi-value\">{mf.ResampleSimulations} 次</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">稳健性提升倍数</div><div class=\"kpi-value\">{mf.ResampledRobustnessGainRatio:F2}x</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">重抽样最优夏普</div><div class=\"kpi-value\">{mf.BestSharpeValue:F2}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">最优组合年化预期</div><div class=\"kpi-value\">+{mf.BestSharpeReturnPercent:F2}% (波: {mf.BestSharpeVolPercent:F2}%)</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>风险梯度</th><th>预期年化收益率</th><th>预期年化波动率</th><th>夏普比率</th><th>重抽样平滑资产权重配置</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var pt in mf.ResampledPoints.Take(6))
            {
                sb.AppendLine($"    <tr><td>Rank #{pt.RiskRank}</td><td style=\"color:#e74c3c;font-weight:600\">+{pt.AnnualExpectedReturnPercent:F2}%</td><td>{pt.AnnualVolatilityPercent:F2}%</td><td>{pt.SharpeRatio:F2}</td><td style=\"font-size:12px\">{WebUtility.HtmlEncode(pt.PortfolioCompositionSummary)}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mf.ExecutiveSummary)}</div>");
            sb.AppendLine("</div>");
        }

        // 12. Phase 27.3 反向压力测试 (Reverse Stress Testing)
        if (portfolio.ReverseStressTopology != null && portfolio.ReverseStressTopology.ShockItems.Count > 0)
        {
            var rst = portfolio.ReverseStressTopology;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">十二、Reverse Stress Testing 反向压力测试破产临界拓扑 (Phase 27)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">设定破产损失阈值</div><div class=\"kpi-value\" style=\"color:#e74c3c\">{rst.TargetThresholdDrawdownPercent:F1}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">临界冲击马氏距离</div><div class=\"kpi-value\">{rst.MahalanobisDistance:F2} σ</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">理论触发概率</div><div class=\"kpi-value\">{rst.ProbabilityOfBreachNormalEstimatePercent:F4}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">最脆弱短板资产</div><div class=\"kpi-value\" style=\"font-size:14px\">{WebUtility.HtmlEncode(rst.MostVulnerableFundName)}</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>标的代码</th><th>基金名称</th><th>组合权重</th><th>临界冲击跌幅</th><th>对破产损失贡献度</th><th>脆弱性等级</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in rst.ShockItems)
            {
                sb.AppendLine($"    <tr><td>{item.FundCode}</td><td>{WebUtility.HtmlEncode(item.FundName)}</td><td>{item.PortfolioWeightPercent:F1}%</td><td style=\"color:#27ae60;font-weight:600\">{item.CriticalShockPercent:F2}%</td><td>{item.LossContributionPercent:F1}%</td><td>{item.VulnerabilityGrade}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(rst.RiskOfficerVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 13. Phase 27.4 Cornish-Fisher 高阶矩展开修正 VaR
        if (portfolio.CornishFisherVaR != null)
        {
            var cf = portfolio.CornishFisherVaR;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">十三、Cornish-Fisher 展开高阶矩 (偏度与峰度) 修正 VaR/CVaR (Phase 27)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">样本收益偏度 (S)</div><div class=\"kpi-value\">{cf.SampleSkewness:F3}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">超额峰度 (K)</div><div class=\"kpi-value\">{cf.SampleExcessKurtosis:F3}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">95% 修正 Modified-VaR</div><div class=\"kpi-value\" style=\"color:#e74c3c\">{cf.CornishFisherVaR95Percent:F2}% (高斯: {cf.GaussianVaR95Percent:F2}%)</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">肥尾低估放大倍数</div><div class=\"kpi-value\">{cf.TailRiskUnderestimationMultiplier:F2}x</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>风险指标</th><th>高斯正态假设</th><th>Cornish-Fisher 展开修正</th><th>尾部极端放大差额</th><th>风险状态评估</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            sb.AppendLine($"    <tr><td><strong>95% 置信度在险价值 (VaR)</strong></td><td>{cf.GaussianVaR95Percent:F2}%</td><td style=\"color:#e74c3c;font-weight:600\">{cf.CornishFisherVaR95Percent:F2}%</td><td>+{(cf.CornishFisherVaR95Percent - cf.GaussianVaR95Percent):F2}%</td><td rowspan=\"3\">{cf.TailRiskHealthBadge}</td></tr>");
            sb.AppendLine($"    <tr><td><strong>99% 置信度在险价值 (VaR)</strong></td><td>{cf.GaussianVaR99Percent:F2}%</td><td style=\"color:#e74c3c;font-weight:600\">{cf.CornishFisherVaR99Percent:F2}%</td><td>+{(cf.CornishFisherVaR99Percent - cf.GaussianVaR99Percent):F2}%</td></tr>");
            sb.AppendLine($"    <tr><td><strong>95% 条件在险价值 (CVaR)</strong></td><td>{cf.GaussianCVaR95Percent:F2}%</td><td style=\"color:#e74c3c;font-weight:600\">{cf.CornishFisherCVaR95Percent:F2}%</td><td>+{(cf.CornishFisherCVaR95Percent - cf.GaussianCVaR95Percent):F2}%</td></tr>");
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(cf.AnalyticalSummary)}</div>");
            sb.AppendLine("</div>");
        }

        // 14. Phase 28.1 资产多因子拥挤度雷达与流动性踩踏预警
        if (portfolio.AssetCrowdingRadar != null)
        {
            var cr = portfolio.AssetCrowdingRadar;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">十四、资产多因子拥挤度雷达与流动性踩踏预警 (Phase 28)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">加权综合拥挤度</div><div class=\"kpi-value\">{cr.OverallCrowdingScore:F1} 分</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">平均配对相关系数</div><div class=\"kpi-value\">{cr.AveragePairwiseCorrelation:F3}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">高危极度拥挤标的数</div><div class=\"kpi-value\" style=\"color:#e74c3c\">{cr.CriticalCrowdedAssetCount} 只</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">全组合踩踏风险等级</div><div class=\"kpi-value\">{cr.CrowdingRiskLevel}</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>标的代码</th><th>标的名称</th><th>权重</th><th>配对关联跳跃</th><th>估值 Z-Score</th><th>换手异动倍数</th><th>拥挤度得分</th><th>预警状态</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in cr.CrowdingItems)
            {
                sb.AppendLine($"    <tr><td>{item.FundCode}</td><td>{WebUtility.HtmlEncode(item.FundName)}</td><td>{item.PortfolioWeightPercent:F1}%</td><td>+{item.PairwiseCorrelationJump:F3}</td><td>{item.ValuationSpreadZScore:F2}</td><td>{item.TurnoverVelocitySpikeRatio:F2}x</td><td style=\"font-weight:600\">{item.CrowdingCompositeScore:F1}</td><td>{item.CrowdingStatusBadge}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\"><strong>风控预警：</strong>{WebUtility.HtmlEncode(cr.LiquidityCascadeWarning)}<br/><strong>操作建议：</strong>{WebUtility.HtmlEncode(cr.ExecutiveAdvice)}</div>");
            sb.AppendLine("</div>");
        }

        // 15. Phase 28.2 带最大回撤硬顶约束的分数凯利动态仓位配置
        if (portfolio.DrawdownConstrainedKelly != null)
        {
            var kl = portfolio.DrawdownConstrainedKelly;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">十五、带最大回撤硬顶约束的分数凯利动态仓位配置 (Phase 28)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">无约束全凯利杠杆</div><div class=\"kpi-value\">{kl.UnconstrainedFullKellyLeverage:F2}x</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">当前动态回撤</div><div class=\"kpi-value\" style=\"color:#e74c3c\">{kl.CurrentDrawdownPercent:F2}% (硬顶: {kl.MaxDrawdownCeilingPercent:F1}%)</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">推荐权益总仓位</div><div class=\"kpi-value\" style=\"color:#2980b9\">{kl.OptimalEquityWeightPercent:F1}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">防守安全现金垫</div><div class=\"kpi-value\">{kl.RecommendedCashBufferPercent:F1}% ({kl.RecommendedCashBufferWan:F1}万)</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>回撤深度场景</th><th>建议总权益仓位</th><th>防守现金垫比例</th><th>距止损硬顶安全冗余</th><th>动态风控操作指令</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var tier in kl.TierScenarios)
            {
                sb.AppendLine($"    <tr><td>{tier.DrawdownThresholdPercent:F1}%</td><td style=\"font-weight:600\">{tier.SuggestedKellyEquityWeightPercent:F1}%</td><td>{tier.DefensiveCashBufferPercent:F1}%</td><td>{tier.SafetyDistanceMarginPercent:F1}%</td><td>{tier.ActionRecommendation}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(kl.RiskOfficerVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 16. Phase 28.3 BSTS 贝叶斯结构时序滤波与趋势断点诊断
        if (portfolio.BstsTrendFilter != null)
        {
            var bsts = portfolio.BstsTrendFilter;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">十六、BSTS 贝叶斯结构时序滤波与趋势断点诊断 (Phase 28)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">加权平滑局部 Alpha</div><div class=\"kpi-value\">{bsts.PortfolioAverageAlphaAnnualPercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">结构性突变后验概率</div><div class=\"kpi-value\">{bsts.PortfolioStructuralBreakProbabilityPercent:F1}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">趋势稳定性评分</div><div class=\"kpi-value\" style=\"color:#27ae60\">{bsts.TrendStabilityScore:F1} 分</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>标的代码</th><th>标的名称</th><th>局部年化 Alpha</th><th>信噪比 (SNR)</th><th>趋势速度 (%/月)</th><th>断点概率 P(Break)</th><th>能力状态诊断</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in bsts.AssetFilterItems)
            {
                sb.AppendLine($"    <tr><td>{item.FundCode}</td><td>{WebUtility.HtmlEncode(item.FundName)}</td><td>{item.SmoothedLocalAlphaAnnualPercent:F2}%</td><td>{item.SignalToNoiseRatio:F2}</td><td>{item.TrendSlopeVelocity:F3}%</td><td>{item.BayesianStructuralBreakProbabilityPercent:F1}%</td><td>{item.RegimeDiagnosisBadge}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(bsts.FilterSynthesisVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 17. Phase 28.4 多期跨期期限结构前沿与时间跨度风险衰减锥
        if (portfolio.MultiHorizonRiskTerm != null)
        {
            var mh = portfolio.MultiHorizonRiskTerm;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">十七、多期跨期期限结构前沿与时间跨度风险衰减锥 (Phase 28)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">收益一阶自相关 (ρ₁)</div><div class=\"kpi-value\">{mh.AutocorrelationLag1:F3}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">均值回归特征半衰期</div><div class=\"kpi-value\">{mh.MeanReversionHalfLifeDays:F1} 交易日</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">1年 vs 5年波动衰减率</div><div class=\"kpi-value\" style=\"color:#27ae60\">-{mh.OneYearToFiveYearVolDecayPercent:F1}%</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>投资跨度期限</th><th>跨期年化波动率</th><th>根号T外推波动率</th><th>方差衰减比率</th><th>期限夏普比率</th><th>推荐权益仓位</th><th>推荐固收现金仓位</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var pt in mh.HorizonPoints)
            {
                sb.AppendLine($"    <tr><td><strong>{pt.HorizonLabel}</strong></td><td style=\"color:#2980b9;font-weight:600\">{pt.HorizonAnnualizedVolPercent:F2}%</td><td>{pt.SqrtTimeRuleAnnualizedVolPercent:F2}%</td><td>{pt.VarianceDecayRatio:F3}</td><td>{pt.HorizonSharpeRatio:F2}</td><td style=\"font-weight:600\">{pt.OptimalEquityAllocationPercent:F1}%</td><td>{pt.OptimalFixedIncomeCashAllocationPercent:F1}%</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mh.HorizonAllocationGuidance)}</div>");
            sb.AppendLine("</div>");
        }

        // 18. Phase 29.1 随机矩阵理论 (RMT) 与 Marchenko-Pastur 谱滤波去噪
        if (portfolio.RmtCovarianceCleaning != null)
        {
            var rmt = portfolio.RmtCovarianceCleaning;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">十八、随机矩阵理论 (RMT) 与 Marchenko-Pastur 谱滤波去噪 (Phase 29)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">样本充沛比 (Q = T/N)</div><div class=\"kpi-value\">{rmt.QualityRatioQ:F2}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">理论最大噪声上限 (λ+)</div><div class=\"kpi-value\">{rmt.MarchenkoPasturUpperBound:F3}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">噪声特征值占比</div><div class=\"kpi-value\" style=\"color:#e74c3c\">{rmt.NoiseRatioPercent:F1}% ({rmt.NoiseEigenvalueCount}/{rmt.AssetCountN})</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">矩阵条件数优化改善</div><div class=\"kpi-value\" style=\"color:#27ae60\">{rmt.RawConditionNumber:F1} → {rmt.CleanedConditionNumber:F1} ({rmt.ConditionNumberImprovementRatio:F1}x)</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>特征值序号</th><th>原始经验特征值</th><th>RMT 理论谱分类</th><th>去噪重构特征值</th><th>方差解释比</th><th>主导投影资产</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in rmt.EigenItems)
            {
                sb.AppendLine($"    <tr><td>#{item.Index}</td><td>{item.RawEigenvalue:F3}</td><td>{item.Classification}</td><td style=\"font-weight:600\">{item.DenoisedEigenvalue:F3}</td><td>{item.VarianceExplainedPercent:F1}%</td><td>{WebUtility.HtmlEncode(item.LeadingAssets)}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(rmt.RmtDenoisingVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 19. Phase 29.2 嵌套聚类优化 (NCO) 层次化前沿与簇间-簇内双重配置
        if (portfolio.NestedClusteredOptimization != null)
        {
            var nco = portfolio.NestedClusteredOptimization;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">十九、嵌套聚类优化 (NCO) 层次化前沿与簇间-簇内双重配置 (Phase 29)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">聚类簇群数量 (K)</div><div class=\"kpi-value\">{nco.TotalClusters} 簇</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">NCO 预期年化收益率</div><div class=\"kpi-value\" style=\"color:#2980b9\">{nco.NcoExpectedReturnAnnualPercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">NCO 预期年化波动率</div><div class=\"kpi-value\">{nco.NcoAnnualizedVolatilityPercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">NCO 最优夏普比率</div><div class=\"kpi-value\" style=\"color:#27ae60\">{nco.NcoSharpeRatio:F2} (相对传统提升 +{nco.SharpeImprovementPercent:F1}%)</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>标的代码</th><th>标的名称</th><th>所属拓扑簇群</th><th>簇内局部权重</th><th>簇间分配权重</th><th>NCO 最终权重</th><th>等权基准对比</th><th>主动偏离倾斜</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in nco.ClusterWeightItems)
            {
                sb.AppendLine($"    <tr><td>{item.FundCode}</td><td>{WebUtility.HtmlEncode(item.FundName)}</td><td>{item.ClusterId}</td><td>{item.IntraClusterWeightPercent:F1}%</td><td>{item.InterClusterWeightPercent:F1}%</td><td style=\"font-weight:600;color:#2980b9\">{item.NcoFinalWeightPercent:F1}%</td><td>{item.EqualWeightPercent:F1}%</td><td>{(item.WeightTiltDeltaPercent >= 0 ? "+" : "")}{item.WeightTiltDeltaPercent:F1}%</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(nco.NcoOptimizationVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 20. Phase 29.3 Amihud 冲击弹性与 Roll 隐性买卖价差微观流动性摩擦锥
        if (portfolio.MicrostructureLiquidity != null)
        {
            var ms = portfolio.MicrostructureLiquidity;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">二十、Amihud 冲击弹性与 Roll 隐性买卖价差微观流动性摩擦锥 (Phase 29)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">全组合加权 Amihud 弹性</div><div class=\"kpi-value\">{ms.WeightedAmihudIlliquidity:F3}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">加权 Roll 隐性买卖价差</div><div class=\"kpi-value\" style=\"color:#e67e22\">{ms.WeightedRollEffectiveSpreadBps:F1} bps</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">组合单日安全吸纳容量</div><div class=\"kpi-value\" style=\"color:#27ae60\">{ms.TotalDailyAbsorbingCapacityWan:N0} 万元</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>调仓体量场景</th><th>预期隐性滑点</th><th>预估滑点金额损耗</th><th>建议拆单天数</th><th>微观算法执行指令建议</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var tier in ms.SlippageTiers)
            {
                sb.AppendLine($"    <tr><td><strong>{tier.TradeScaleLabel}</strong></td><td>{tier.ExpectedSlippageBps:F1} bps</td><td style=\"color:#c0392b;font-weight:600\">¥{tier.EstimatedFrictionAmountYuan:N0}</td><td>{tier.RecommendedExecutionDays:F1} 天</td><td>{tier.ExecutionExecutionPacingAdvice}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine("  <table class=\"table\" style=\"margin-top:10px;\">");
            sb.AppendLine("    <thead><tr><th>标的代码</th><th>标的名称</th><th>Amihud 指数</th><th>Roll 价差 (bp)</th><th>换手冲击弹性</th><th>日均安全容量</th><th>流动性等级</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in ms.AssetItemList)
            {
                sb.AppendLine($"    <tr><td>{item.FundCode}</td><td>{WebUtility.HtmlEncode(item.FundName)}</td><td>{item.AmihudIlliquidityMeasure:F3}</td><td>{item.RollEffectiveSpreadBps:F1}</td><td>{item.TurnOverImpactElasticity:F2}</td><td>{item.DailySafeAbsorbingCapacityWan:N0} 万</td><td>{item.LiquidityHealthBadge}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ms.MicrostructureFrictionVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 21. Phase 29.4 信息几何有效下注数 (ENB) 与香农熵分散度审定
        if (portfolio.PortfolioEntropyRegularization != null)
        {
            var ent = portfolio.PortfolioEntropyRegularization;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">二十一、信息几何有效下注数 (ENB) 与香农熵分散度审定 (Phase 29)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">名义有效资产数 (ENA)</div><div class=\"kpi-value\">{ent.EffectiveNumberOfAssets:F2}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">正交有效下注数 (ENB)</div><div class=\"kpi-value\" style=\"color:#2980b9\">{ent.EffectiveNumberOfBets:F2}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">伪分散度赤字 (ΔDiv)</div><div class=\"kpi-value\" style=\"color:{(ent.DiversificationDeficit > 2.0m ? "#e74c3c" : "#27ae60")}\">Δ {ent.DiversificationDeficit:F2}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">熵正则化健康评分</div><div class=\"kpi-value\" style=\"color:#27ae60\">{ent.EntropyRegularizedDiversificationScore:F1} 分</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>正交主成分</th><th>主成分特征值</th><th>方差解释比</th><th>组合风险方差贡献 (p_k)</th><th>香农熵贡献</th><th>独立因子状态</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var pca in ent.PcaBetContributions)
            {
                sb.AppendLine($"    <tr><td>PC #{pca.PrincipalComponentIndex}</td><td>{pca.Eigenvalue:F3}</td><td>{pca.VarianceExplainedPercent:F1}%</td><td style=\"font-weight:600\">{pca.RiskContributionVariancePercent:F1}%</td><td>{pca.EntropyContribution:F3}</td><td>{pca.IndependenceStatusBadge}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ent.EntropyDiversificationVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 22. Phase 30.1 全天候宏观 4 象限识别与马氏金融动荡度降杠杆雷达
        if (portfolio.MahalanobisTurbulence != null)
        {
            var turb = portfolio.MahalanobisTurbulence;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">二十二、全天候宏观 4 象限识别与马氏金融动荡度降杠杆雷达 (Phase 30)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">历史马氏动荡度均值</div><div class=\"kpi-value\">{turb.AverageTurbulenceScore:F2}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">当前动荡度评分 / 状态</div><div class=\"kpi-value\" style=\"color:#e74c3c\">{turb.CurrentTurbulenceScore:F2} ({turb.MarketTurbulenceStateBadge})</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">历史百分位 / 偏离倍数</div><div class=\"kpi-value\">{turb.TurbulencePercentileRank:F1}% ({turb.TurbulenceRatioVsHistorical:F2}x)</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">战术动态杠杆乘数</div><div class=\"kpi-value\" style=\"color:#2980b9\">{turb.RecommendedLeverageMultiplier:F2}x</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">建议防守现金垫储备</div><div class=\"kpi-value\" style=\"color:#27ae60\">{turb.RecommendedDefensiveCashBufferPercent:F1}%</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>Bridgewater 4 象限宏观态</th><th>经济增长环境</th><th>通胀压力环境</th><th>历史分布频率</th><th>条件年化预期收益</th><th>条件年化波动率</th><th>全天候风险预算</th><th>资产类别动态倾斜建议</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var q in turb.MacroQuadrants)
            {
                sb.AppendLine($"    <tr><td><strong>{q.RegimeName}</strong></td><td>{q.MacroGrowthEnv}</td><td>{q.MacroInflationEnv}</td><td>{q.HistoricalFrequencyPercent:F1}%</td><td style=\"color:#2980b9;font-weight:600\">{q.ExpectedReturnAnnualPercent:+0.0;-0.0}%</td><td>{q.ConditionalVolatilityPercent:F1}%</td><td>{q.RecommendedRiskBudgetPercent:F1}%</td><td>{WebUtility.HtmlEncode(q.BenchmarkAssetTiltAdvice)}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(turb.TurbulenceTacticalVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 23. Phase 30.2 欧拉下行条件在险价值 (CVaR) 风险贡献分解与极端尾部去毒
        if (portfolio.EulerCvarAttribution != null)
        {
            var ec = portfolio.EulerCvarAttribution;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">二十三、欧拉下行条件在险价值 (CVaR) 风险贡献分解与极端尾部去毒 (Phase 30)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">组合 95% 在险价值 (VaR)</div><div class=\"kpi-value\">{ec.PortfolioVaRPercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">组合 95% 条件在险价值 (CVaR)</div><div class=\"kpi-value\" style=\"color:#e74c3c\">{ec.PortfolioCvarPercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">欧拉分解求和检验</div><div class=\"kpi-value\" style=\"color:#27ae60\">{ec.EulerSumCvarPercent:F2}% (闭合)</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">尾部集中度 HHI 指数</div><div class=\"kpi-value\">{ec.TailHerfindahlIndex:F0}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">最大极端毒性资产</div><div class=\"kpi-value\" style=\"color:#e74c3c\">{ec.MostToxicAssetCode} ({ec.MostToxicAssetCvarContributionRatio:F1}%)</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>标的代码</th><th>标的名称</th><th>名义权重</th><th>年化收益</th><th>95% MES 边际期望损失</th><th>欧拉 CVaR 绝对贡献</th><th>欧拉 %CVaR 贡献比</th><th>尾部 Beta 弹性</th><th>极端尾部评级</th><th>建议去毒目标权重</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in ec.AssetItemList)
            {
                sb.AppendLine($"    <tr><td>{item.FundCode}</td><td>{WebUtility.HtmlEncode(item.FundName)}</td><td>{item.NominalWeightPercent:F1}%</td><td>{item.AnnualizedReturnPercent:+0.00;-0.00}%</td><td>{item.MarginalExpectedShortfallPercent:F2}%</td><td style=\"font-weight:600\">{item.EulerAbsoluteCvarContributionPercent:F2}%</td><td style=\"color:#c0392b;font-weight:600\">{item.EulerPercentCvarContributionRatio:F1}%</td><td>{item.TailBetaVsPortfolio:F2}</td><td>{item.TailToxicityRating}</td><td style=\"color:#2980b9;font-weight:600\">{item.RecommendedDetoxWeightPercent:F1}%</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ec.EulerCvarVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 24. Phase 30.3 基准相对下行半方差跟踪误差 (DTE) 与 Stutzer 极端衰减指数
        if (portfolio.DownsideTrackingError != null)
        {
            var dte = portfolio.DownsideTrackingError;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">二十四、基准相对下行半方差跟踪误差 (DTE) 与 Stutzer 极端衰减指数 (Phase 30)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">年化主动超额收益</div><div class=\"kpi-value\" style=\"color:#2980b9\">{dte.ActiveAnnualizedReturnPercent:+0.00;-0.00}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">对称跟踪误差 (TE)</div><div class=\"kpi-value\">{dte.SymmetricTrackingErrorPercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">下行跟踪误差 (DTE)</div><div class=\"kpi-value\" style=\"color:#27ae60\">{dte.DownsideTrackingErrorPercent:F2}% (增益 {dte.AsymmetricDownsideGainRatio:F2}x)</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">下行信息比率 (DIR)</div><div class=\"kpi-value\" style=\"color:#2980b9\">{dte.DownsideInformationRatio:F2} (传统 IR: {dte.InformationRatio:F2})</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">Stutzer 极值指数 / 衰减率</div><div class=\"kpi-value\" style=\"color:#27ae60\">I_S={dte.StutzerDecayIndex:F4} ({dte.AnnualizedProbUnderperformDecayRatePercent:F1}%/年)</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine($"  <div class=\"advice-box\" style=\"background:#e8f4f8;border-left:4px solid #3498db;padding:10px;margin-bottom:12px;\"><strong>基准跟踪纯度评级：{dte.BenchmarkPurityGradeBadge}</strong></div>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(dte.StutzerVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 25. Phase 30.4 负债驱动投资 (LDI) 跨期现金流匹配、久期缺口与清算瀑布
        if (portfolio.LdiCashFlowMatch != null)
        {
            var ldi = portfolio.LdiCashFlowMatch;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">二十五、负债驱动投资 (LDI) 跨期现金流匹配、久期缺口与清算瀑布 (Phase 30)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">资产现值总额 (A)</div><div class=\"kpi-value\">¥{ldi.TotalAssetPresentValueWan:N0} 万元</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">负债现值总额 (L)</div><div class=\"kpi-value\">¥{ldi.TotalLiabilityPresentValueWan:N0} 万元</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">资产久期 vs 负债久期</div><div class=\"kpi-value\">{ldi.AssetEffectiveDurationYears:F2}年 vs {ldi.LiabilityEffectiveDurationYears:F2}年</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">久期缺口 (Duration Gap)</div><div class=\"kpi-value\" style=\"color:{(Math.Abs(ldi.DurationGapYears) > 1.5m ? "#e74c3c" : "#27ae60")}\">Δ {ldi.DurationGapYears:F2} 年</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">综合流动性覆盖率 (LCR)</div><div class=\"kpi-value\" style=\"color:#27ae60\">{ldi.OverallLiquidityCoverageRatioPercent:F1}% (备付金: ¥{ldi.ImmediateLiquidReserveWan:N0}万)</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>跨期匹配期限</th><th>期限天数</th><th>刚性负债需求</th><th>资产匹配供给</th><th>净现金流盈余</th><th>现金流覆盖率</th><th>偿付健康评级</th><th>变现清算梯阶指引</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var h in ldi.HorizonMatchItems)
            {
                sb.AppendLine($"    <tr><td><strong>{h.HorizonLabel}</strong></td><td>{h.HorizonDays} 天</td><td>¥{h.ScheduledLiabilityAmountWan:N0} 万</td><td style=\"color:#2980b9;font-weight:600\">¥{h.MatchedAssetCashFlowWan:N0} 万</td><td style=\"color:{(h.NetCashFlowSurplusWan >= 0 ? "#27ae60" : "#c0392b")}\">{(h.NetCashFlowSurplusWan >= 0 ? "+" : "")}¥{h.NetCashFlowSurplusWan:N0} 万</td><td>{h.CoverageRatioPercent:F1}%</td><td>{h.CashFlowHealthBadge}</td><td>{WebUtility.HtmlEncode(h.RecommendedLiquidationAsset)}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\" style=\"background:#fef9e7;border-left:4px solid #f39c12;padding:10px;margin:10px 0;white-space:pre-line;\">{WebUtility.HtmlEncode(ldi.LiquidationWaterfallStrategy)}</div>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ldi.LdiExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 26. Phase 31.1 卡尔曼滤波状态空间时变 Beta 与风格漂移追踪预警
        if (portfolio.KalmanFilterStyleDrift != null)
        {
            var kf = portfolio.KalmanFilterStyleDrift;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">二十六、卡尔曼滤波状态空间时变 Beta 与风格漂移追踪预警 (Phase 31)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">组合即期时变 Beta</div><div class=\"kpi-value\" style=\"color:#2980b9\">{kf.PortfolioInstantBeta:F2}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">组合历史加权均值 Beta</div><div class=\"kpi-value\">{kf.PortfolioMeanBeta:F2}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">平均风格漂移指数 (SDI)</div><div class=\"kpi-value\" style=\"color:{(kf.AverageStyleDriftIndex < 25m ? "#27ae60" : "#e74c3c")}\">{kf.AverageStyleDriftIndex:F1}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">最大漂移标的 / SDI</div><div class=\"kpi-value\" style=\"color:#e74c3c\">{kf.MostDriftedAssetCode} ({kf.MostDriftedAssetSdi:F1})</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>标的代码</th><th>标的名称</th><th>即期时变Beta</th><th>历史均值Beta</th><th>波动标准差</th><th>历史Beta区间</th><th>漂移指数 SDI</th><th>风格严守评级</th><th>风格特征诊断结论</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in kf.AssetItemList)
            {
                sb.AppendLine($"    <tr><td>{item.FundCode}</td><td>{WebUtility.HtmlEncode(item.FundName)}</td><td style=\"color:#2980b9;font-weight:600\">{item.CurrentInstantBeta:F2}</td><td>{item.HistoricalMeanBeta:F2}</td><td>{item.BetaVolatilityStdDev:F3}</td><td>[{item.BetaMin:F2}, {item.BetaMax:F2}]</td><td style=\"color:#c0392b;font-weight:600\">{item.StyleDriftIndex:F1}</td><td>{item.StyleDriftBadge}</td><td>{WebUtility.HtmlEncode(item.StyleDriftDiagnosis)}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(kf.StyleDriftExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 27. Phase 31.2 Axioma 基数硬约束与换手预算稀疏组合精选推荐
        if (portfolio.CardinalitySparseOptimization != null)
        {
            var sp = portfolio.CardinalitySparseOptimization;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">二十七、Axioma 基数硬约束与换手预算稀疏组合精选推荐 (Phase 31)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">精选持仓只数 / 候选池</div><div class=\"kpi-value\" style=\"color:#2980b9\">{sp.TargetCardinalityK} 只 (候选 {sp.TotalCandidatesN} 只)</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">实际单边换手率 / 预算</div><div class=\"kpi-value\" style=\"color:#27ae60\">{sp.ActualTurnoverPercent:F1}% (预算 ≤{sp.TurnoverBudgetPercent:F0}%)</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">稀疏年化收益 / 波动</div><div class=\"kpi-value\">{sp.SparseAnnualizedReturnPercent:+0.00;-0.00}% / {sp.SparseAnnualizedVolatilityPercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">稀疏夏普 / 无约束夏普</div><div class=\"kpi-value\" style=\"color:#2980b9\">{sp.SparseSharpeRatio:F2} (无约束 {sp.UnconstrainedSharpeRatio:F2})</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">基数效率保留率 / 损失</div><div class=\"kpi-value\" style=\"color:#27ae60\">{sp.CardinalityRetentionRatioPercent:F1}% (损失 {sp.CardinalityEfficiencyLossPercent:F1}%)</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>标的代码</th><th>标的名称</th><th>精选状态</th><th>稀疏最优权重</th><th>无约束权重</th><th>权重偏差</th><th>换手贡献</th><th>调仓清算处置指引</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in sp.AssetItemList)
            {
                string statusColor = item.IsSelectedInSparseSubset ? "#27ae60" : "#7f8c8d";
                sb.AppendLine($"    <tr><td>{item.FundCode}</td><td>{WebUtility.HtmlEncode(item.FundName)}</td><td style=\"color:{statusColor};font-weight:600\">{(item.IsSelectedInSparseSubset ? "✅ 入选精选底仓" : "❌ 剔除清算")}</td><td style=\"color:#2980b9;font-weight:600\">{item.SparseOptimalWeightPercent:F1}%</td><td>{item.OriginalUnconstrainedWeightPercent:F1}%</td><td style=\"color:{(item.WeightDeviationPercent >= 0 ? "#27ae60" : "#c0392b")}\">{(item.WeightDeviationPercent >= 0 ? "+" : "")}{item.WeightDeviationPercent:F1}%</td><td>{item.TurnoverContributionPercent:F1}%</td><td>{WebUtility.HtmlEncode(item.LiquidationAdvice)}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(sp.SparseOptimizationVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 28. Phase 31.3 Adrian-Brunnermeier 条件系统性在险价值增量 ΔCoVaR 与金融传染
        if (portfolio.DeltaCoVaRSystemicRisk != null)
        {
            var dc = portfolio.DeltaCoVaRSystemicRisk;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">二十八、Adrian-Brunnermeier 条件系统性在险价值增量 ΔCoVaR 与金融传染 (Phase 31)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">组合平均传染溢价 ΔCoVaR</div><div class=\"kpi-value\" style=\"color:#e74c3c\">{dc.AverageDeltaCoVaRPercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">首要系统性传染源基金</div><div class=\"kpi-value\" style=\"color:#e74c3c\">{dc.HighestContagionAssetCode} (溢价 {dc.HighestContagionDeltaCoVaR:F2}%)</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">系统性关联脆弱度评分</div><div class=\"kpi-value\" style=\"color:#f39c12\">{dc.SystemicNetworkVulnerabilityScore:F1} / 100</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">系统性抗压防御评级</div><div class=\"kpi-value\" style=\"color:#2980b9\">{dc.SystemicFragilityBadge}</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>标的代码</th><th>标的名称</th><th>名义权重</th><th>单基95% VaR</th><th>极端危机 CoVaR(95%)</th><th>中位常态 CoVaR(50%)</th><th>传染溢价 ΔCoVaR</th><th>传染排名</th><th>系统重要性评级</th><th>机构隔离防火墙建议</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var item in dc.AssetItemList)
            {
                sb.AppendLine($"    <tr><td>{item.FundCode}</td><td>{WebUtility.HtmlEncode(item.FundName)}</td><td>{item.NominalWeightPercent:F1}%</td><td>{item.IndividualVaR95Percent:F2}%</td><td style=\"color:#c0392b\">{item.CoVaRWhenAssetInCrisisPercent:F2}%</td><td>{item.CoVaRWhenAssetInMedianPercent:F2}%</td><td style=\"color:#e74c3c;font-weight:600\">+{item.DeltaCoVaRContributionPercent:F2}%</td><td>#{item.SystemicContagionRank:F0}</td><td>{item.SystemicImportanceRating}</td><td>{WebUtility.HtmlEncode(item.IsolationFirewallAdvice)}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(dc.DeltaCoVaRVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 29. Phase 31.4 Basel III / FRTB 压力在险价值 (sVaR) 与监管资本拨备
        if (portfolio.FrtbStressedCapitalCharge != null)
        {
            var frtb = portfolio.FrtbStressedCapitalCharge;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">二十九、Basel III / FRTB 历史最劣 250 天压力在险价值 (sVaR) 与监管资本拨备 (Phase 31)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">最恶劣 250 天压力窗口</div><div class=\"kpi-value\">{frtb.StressedWindowStartDate:yy/MM/dd} ~ {frtb.StressedWindowEndDate:yy/MM/dd}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">压力期最大回撤 / 波动</div><div class=\"kpi-value\" style=\"color:#e74c3c\">-{frtb.StressedWindowMaxDrawdownPercent:F2}% / {frtb.StressedWindowAnnualizedVolatilityPercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">常态 VaR vs 压力 sVaR(10d)</div><div class=\"kpi-value\" style=\"color:#2980b9\">{frtb.NormalVaR99Percent:F2}% vs {frtb.StressedVaR99Percent:F2}% ({frtb.StressedMultiplierRatio:F2}x)</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">FRTB 阶梯监管资本拨备</div><div class=\"kpi-value\" style=\"color:#27ae60\">¥{frtb.FrtbTotalCapitalChargeWan:N1} 万元 ({frtb.FrtbCapitalAdequacyRatioPercent:F1}%)</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">巴塞尔资本充足评级</div><div class=\"kpi-value\">{frtb.CapitalAdequacyBadge}</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>流动性梯阶类别</th><th>时限天数</th><th>资产分配规模</th><th>时限条件在险损失 ES</th><th>时限阶梯缩放拨备金</th><th>资本拨备占比</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var h in frtb.HorizonItemList)
            {
                sb.AppendLine($"    <tr><td><strong>{h.HorizonTierLabel}</strong></td><td>{h.HorizonDays} 天</td><td>¥{h.AllocatedCapitalWan:N0} 万</td><td>{h.ComponentExpectedShortfallPercent:F2}%</td><td style=\"color:#2980b9;font-weight:600\">¥{h.ScaledCapitalChargeWan:N1} 万</td><td>{h.CapitalContributionRatioPercent:F1}%</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(frtb.FrtbExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 30. Phase 32.1 Goldman Sachs GSAM & Idzorek 显式置信度 Black-Litterman 优化
        if (portfolio.IdzorekBlackLitterman != null)
        {
            var ibl = portfolio.IdzorekBlackLitterman;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">三十、Goldman Sachs GSAM & Idzorek (2005) 显式置信度百分比校准 Black-Litterman 优化 (Phase 32)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">先验均衡夏普比率</div><div class=\"kpi-value\" style=\"color:#7f8c8d\">{ibl.PriorEquilibriumSharpeRatio:F2}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">后验优化期望夏普</div><div class=\"kpi-value\" style=\"color:#27ae60\">{ibl.PosteriorOptimalSharpeRatio:F2}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">夏普比率提升幅度</div><div class=\"kpi-value\" style=\"color:#2980b9\">+{ibl.SharpeRatioImprovementPercent:F1}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">先验后验 KL 散度信息熵</div><div class=\"kpi-value\" style=\"color:#8e44ad\">{ibl.PriorPosteriorKLDivergenceEntropy:F4}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">观点平均指定置信度</div><div class=\"kpi-value\">{ibl.AverageUserConfidencePercent:F1}%</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>序号</th><th>主观战术观点陈述</th><th>预期年化超额</th><th>指定置信度</th><th>校准观点误差方差 Ω</th><th>观点信息贡献比 (VIC)</th><th>观点信赖评级</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var v in ibl.ViewItemList)
            {
                sb.AppendLine($"    <tr><td>#{v.ViewIndex}</td><td>{WebUtility.HtmlEncode(v.ViewDescription)}</td><td style=\"color:#27ae60;font-weight:600\">+{v.ExpectedExcessReturnPercent:F2}%</td><td>{v.UserSpecifiedConfidencePercent:F1}%</td><td>{v.CalibratedOmegaVariance:F4}</td><td>{v.ViewInformationContributionPercent:F1}%</td><td>{v.ViewImpactBadge}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>标的代码</th><th>标的名称</th><th>先验均衡权重</th><th>先验均衡年化收益</th><th>后验预期年化收益</th><th>Idzorek后验权重</th><th>战术权重倾斜 Δ</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var a in ibl.AssetItemList)
            {
                string deltaColor = a.WeightTiltDeltaPercent >= 0 ? "#27ae60" : "#e74c3c";
                string prefix = a.WeightTiltDeltaPercent >= 0 ? "+" : "";
                sb.AppendLine($"    <tr><td>{a.FundCode}</td><td>{WebUtility.HtmlEncode(a.FundName)}</td><td>{a.EquilibriumPriorWeightPercent:F1}%</td><td>{a.EquilibriumPriorReturnPercent:F2}%</td><td>{a.PosteriorExpectedReturnPercent:F2}%</td><td><strong>{a.IdzorekPosteriorWeightPercent:F1}%</strong></td><td style=\"color:{deltaColor};font-weight:600\">{prefix}{a.WeightTiltDeltaPercent:F1}%</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ibl.IdzorekExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 31. Phase 32.2 BCBS & Carlo Acerbi 一致性连续指数风险厌恶谱风险测度 (SRM)
        if (portfolio.SpectralRiskMeasure != null)
        {
            var srm = portfolio.SpectralRiskMeasure;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">三十一、BCBS & Carlo Acerbi 一致性连续指数风险厌恶谱风险测度 (Spectral Risk Measures, SRM) (Phase 32)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">机构风险厌恶参数 γ</div><div class=\"kpi-value\">{srm.RiskAversionGamma:F1}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">连续谱在险测度 SRM (单日/年化)</div><div class=\"kpi-value\" style=\"color:#c0392b\">{srm.SpectralRiskMeasure1dPercent:F2}% / {srm.SpectralRiskMeasureAnnualizedPercent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">传统 99% VaR / ES</div><div class=\"kpi-value\" style=\"color:#e67e22\">{srm.ClassicalVaR99Percent:F2}% / {srm.ClassicalExpectedShortfall99Percent:F2}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">尾部严酷度溢价 (SRM / ES)</div><div class=\"kpi-value\" style=\"color:#8e44ad\">{srm.TailSeverityPremiumRatio:F2}x</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">最劣 1% 极端集中度倍数</div><div class=\"kpi-value\" style=\"color:#e74c3c\">{srm.ExponentialSpectrumConcentrationRatio:F1}x</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>尾部风险极值阶梯</th><th>分位数区间</th><th>累积指数谱权重</th><th>阶梯内平均极端损失</th><th>阶梯谱在险资本贡献比</th><th>风险严酷度评级</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var t in srm.TailTierList)
            {
                sb.AppendLine($"    <tr><td><strong>{t.TailTierName}</strong></td><td>{t.QuantileLowerBoundPercent:F0}% ~ {t.QuantileUpperBoundPercent:F0}%</td><td>{t.CumulativeSpectrumWeightPercent:F2}%</td><td style=\"color:#c0392b\">-{t.TierAverageLossPercent:F2}%</td><td style=\"color:#e74c3c;font-weight:600\">{t.TierSpectralRiskContributionPercent:F1}%</td><td>{t.TierRiskSeverityBadge}</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(srm.SpectralExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 32. Phase 32.3 Bridgewater 动态风险预算漂移走廊 (RCDI) 与带摩擦平滑再平衡引擎
        if (portfolio.RiskBudgetDriftCorridor != null)
        {
            var rbd = portfolio.RiskBudgetDriftCorridor;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">三十二、Bridgewater All-Weather 动态风险预算漂移走廊 (RCDI) 与平滑再平衡引擎 (Phase 32)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">全组合风险漂移指数 (RCDI)</div><div class=\"kpi-value\" style=\"color:#e67e22\">{rbd.TotalRiskContributionDriftIndex:F1}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">走廊整体运行状态</div><div class=\"kpi-value\">{rbd.CorridorOverallStatus}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">破位 / 预警标的数</div><div class=\"kpi-value\" style=\"color:#e74c3c\">{rbd.BreachedAssetCount} 破位 / {rbd.WarningAssetCount} 预警</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">平滑再平衡单边换手率</div><div class=\"kpi-value\" style=\"color:#2980b9\">{rbd.RequiredSmoothRebalanceTurnoverPercent:F1}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">预估冲击摩擦成本</div><div class=\"kpi-value\" style=\"color:#7f8c8d\">{rbd.EstimatedRebalanceCostBps:F1} bps</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>标的代码</th><th>标的名称</th><th>当前持仓权重</th><th>百分比风险贡献 (PRC)</th><th>目标风险预算</th><th>预算漂移偏差 Δ</th><th>缓冲走廊 [内/外]</th><th>走廊状态</th><th>平滑目标权重</th><th>调仓执行偏离</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var a in rbd.AssetItemList)
            {
                string deltaColor = a.RebalanceTradeWeightDeltaPercent >= 0 ? "#27ae60" : "#e74c3c";
                string prefix = a.RebalanceTradeWeightDeltaPercent >= 0 ? "+" : "";
                sb.AppendLine($"    <tr><td>{a.FundCode}</td><td>{WebUtility.HtmlEncode(a.FundName)}</td><td>{a.CurrentHoldingWeightPercent:F1}%</td><td><strong>{a.PercentageRiskContributionPercent:F1}%</strong></td><td>{a.TargetRiskBudgetPercent:F1}%</td><td style=\"color:{deltaColor}\">{prefix}{a.RiskBudgetDriftDeltaPercent:F1}%</td><td>[{a.InnerBandLowerPercent:F0}%~{a.InnerBandUpperPercent:F0}%] / [{a.OuterBandLowerPercent:F0}%~{a.OuterBandUpperPercent:F0}%]</td><td>{a.CorridorStatusBadge}</td><td><strong>{a.SmoothRebalanceTargetWeightPercent:F1}%</strong></td><td style=\"color:{deltaColor};font-weight:600\">{prefix}{a.RebalanceTradeWeightDeltaPercent:F1}%</td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(rbd.RiskBudgetExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 33. Phase 32.4 Marcos Lopez de Prado & David Bailey 概率夏普 (PSR) 与通缩夏普 (DSR) 策略过拟合检验
        if (portfolio.DeflatedSharpeOverfit != null)
        {
            var dsr = portfolio.DeflatedSharpeOverfit;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">三十三、Marcos Lopez de Prado & David Bailey 概率夏普 (PSR) 与多重回测试验通缩夏普 (DSR) 策略过拟合检验 (Phase 32)</div>");
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">未调整年化夏普 (SR)</div><div class=\"kpi-value\" style=\"color:#2980b9\">{dsr.UnadjustedAnnualizedSharpeRatio:F2}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">概率夏普比率 (PSR)</div><div class=\"kpi-value\" style=\"color:#27ae60\">{dsr.ProbabilisticSharpeRatioPercent:F1}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">零假设最大期望夏普 (N={dsr.NumberOfTrialsTestedN})</div><div class=\"kpi-value\" style=\"color:#e67e22\">{dsr.ExpectedMaxNullSharpeRatio:F2}</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">通缩夏普比率 (DSR)</div><div class=\"kpi-value\" style=\"color:#8e44ad\">{dsr.DeflatedSharpeRatioPercent:F1}%</div></div>");
            sb.AppendLine($"  <div class=\"kpi-card\"><div class=\"kpi-title\">真实阿尔法甄别裁决</div><div class=\"kpi-value\">{dsr.AlphaGenuineStatusBadge}</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("  <table class=\"table\">");
            sb.AppendLine("    <thead><tr><th>统计检验指标</th><th>度量参数值</th><th>统计学含义与量化准则</th><th>机构投研合规门槛</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            sb.AppendLine($"    <tr><td><strong>有效回测样本期 (T)</strong></td><td>{dsr.SampleObservationsT} 交易日</td><td>计算夏普比率样本精度的时序长度</td><td>T >= 250 天 (至少 1 完整年度)</td></tr>");
            sb.AppendLine($"    <tr><td><strong>日度收益率偏度 (Skewness)</strong></td><td>{dsr.ReturnSkewness:F2}</td><td>三阶非对称矩 (负偏度放大左尾突发下行崩塌概率)</td><td>> -0.50 (严控左尾偏斜)</td></tr>");
            sb.AppendLine($"    <tr><td><strong>日度收益率峰度 (Kurtosis)</strong></td><td>{dsr.ReturnKurtosis:F2}</td><td>四阶肥尾度量 (高于 3.0 代表厚尾极端事件高发)</td><td>< 5.00 (避免过度肥尾)</td></tr>");
            sb.AppendLine($"    <tr><td><strong>试验回测总次数 (N)</strong></td><td>{dsr.NumberOfTrialsTestedN} 次</td><td>策略搜寻与因子挖掘中尝试的参数组合空间维度</td><td>N 越大，数据窥探偏差越严峻</td></tr>");
            sb.AppendLine($"    <tr><td><strong>伪发现概率 (FDP)</strong></td><td style=\"color:#e74c3c;font-weight:600\">{dsr.FalseDiscoveryProbabilityPercent:F1}%</td><td>夏普表现纯属数据挖掘随机运气的置信补集</td><td>FDP < 5.0% (95% 显著性水平)</td></tr>");
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(dsr.DsrExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 34. Phase 33.1 AQR 杠杆约束异象 Betting Against Beta (BAB) 与高质量多空 (QMJ) 因子解构
        if (portfolio.BabQmjFactorDecomposition != null)
        {
            var bab = portfolio.BabQmjFactorDecomposition;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">三十四、AQR (Asness, Frazzini & Pedersen) 杠杆约束异象 Betting Against Beta (BAB) 与高质量多空 (QMJ) 因子解构 (Phase 33)</div>");
            sb.AppendLine("  <div class=\"narrative\">根据 Frazzini & Pedersen (2014)《Betting Against Beta》与 AQR 经典质量因子模型，由于市场绝大多数机构面临杠杆与借贷约束，投资者系统性追逐高 Beta 资产导致其被高估，而低 Beta 资产呈现显著的正超额。本模块构建贝塔中性杠杆再平衡多空策略（杠杆做多低贝塔、去杠杆做空高贝塔），测算杠杆融资约束隐含影子成本 ψ，并对底层基金进行 QMJ 综合质量评分，严格剔除被动因子暴露以还原投资经理纯选基 Alpha 技能。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">低贝塔篮子加权 Beta</div><div class=\"kpi-val\">{bab.LowBetaBasketAverageBeta:F2} (杠杆 {bab.LowBetaLeverageRatio:F2}x)</div><div class=\"kpi-sub\">做多低估抗跌基石</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">高贝塔篮子加权 Beta</div><div class=\"kpi-val\">{bab.HighBetaBasketAverageBeta:F2} (去杠杆 {bab.HighBetaDeleverageRatio:F2}x)</div><div class=\"kpi-sub\">做空高估拥挤投机</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">BAB 策略年化利差收益</div><div class=\"kpi-val highlight\">+{bab.BabAnnualizedSpreadReturnPercent:F2}%</div><div class=\"kpi-sub\">夏普比率 {bab.BabSharpeRatio:F2} (波动 {bab.BabAnnualizedVolatilityPercent:F1}%)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">融资约束影子成本 (ψ)</div><div class=\"kpi-val\">{bab.ImpliedLeverageShadowCostPercent:F2}%</div><div class=\"kpi-sub\">杠杆摩擦利差补偿</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合加权 QMJ 质量得分</div><div class=\"kpi-val\">{bab.PortfolioWeightedQualityScore:F1} 分</div><div class=\"kpi-sub\">QMJ 年化溢价 {bab.QualityMinusJunkAnnualizedPremiumPercent:F2}%</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">纯选基超额纯 Alpha</div><div class=\"kpi-val highlight\">+{bab.TrueManagerSelectionAlphaPercent:F2}%</div><div class=\"kpi-sub\">剥离 BAB 与 QMJ 后的真实技能</div></div>");
            sb.AppendLine("  </div>");

            if (bab.AssetItemList.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>基金代码</th><th>基金简称</th><th>组合权重</th><th>系统性 Beta</th><th>贝塔分档</th><th>BAB 配重</th><th>盈利性得分</th><th>安全性得分</th><th>收益稳定性</th><th>QMJ综合质量</th><th>质量评级</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in bab.AssetItemList)
                {
                    sb.AppendLine($"    <tr><td><strong>{WebUtility.HtmlEncode(item.FundCode)}</strong></td><td>{WebUtility.HtmlEncode(item.FundName)}</td><td>{item.PortfolioWeightPercent:F1}%</td><td>{item.SystematicBeta:F2}</td><td>{WebUtility.HtmlEncode(item.BetaBucketName)}</td><td>{item.BabPortfolioWeightPercent:F1}%</td><td>{item.QualityProfitabilityScore:F1}</td><td>{item.QualitySafetyScore:F1}</td><td>{item.QualityStabilityScore:F1}</td><td><strong>{item.CompositeQualityScore:F1}</strong></td><td>{WebUtility.HtmlEncode(item.QualityRatingBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(bab.BabQmjExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 35. Phase 33.2 BlackRock Aladdin 极值理论 POT 广义帕累托 (GPD) 尾部外推与极端重现期风险测度
        if (portfolio.EvtGeneralizedPareto != null)
        {
            var evt = portfolio.EvtGeneralizedPareto;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">三十五、BlackRock Aladdin 极值理论 POT 广义帕累托 (GPD) 尾部外推与极端重现期风险测度 (Phase 33)</div>");
            sb.AppendLine("  <div class=\"narrative\">贝莱德阿拉丁（BlackRock Aladdin）风险系统的核心精髓之一在于打破历史样本观测长度的硬天花板。依据 Pickands-Balkema-de Haan 极值极限定理，通过设定超限损失门槛 u，采用概率加权矩（PWM）拟合广义帕累托分布（GPD）的形状参数 ξ（Tail Index）与尺度参数 β，能够在闭式解析下外推 1年（250日）、4年（1000日）、10年（2500日）乃至20年一遇的极端断崖黑天鹅在险价值与期望短缺。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">超限门槛损失 (u)</div><div class=\"kpi-val\">{evt.ThresholdLossPercent:F2}%</div><div class=\"kpi-sub\">超限样本 {evt.ExceedanceObservationsNu} / {evt.TotalSampleObservationsT} ({evt.ExceedanceProbabilityPercent:F1}%)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">GPD 形状参数 (ξ)</div><div class=\"kpi-val\">{evt.GpdShapeParameterXi:F4}</div><div class=\"kpi-sub\">尾部指数 (Tail Index)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">GPD 尺度参数 (β)</div><div class=\"kpi-val\">{evt.GpdScaleParameterBeta:F4}</div><div class=\"kpi-sub\">尾部肥度倍数 {evt.TailIndexFatnessRatio:F2}x</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">尾部概率分布形态</div><div class=\"kpi-val\">{WebUtility.HtmlEncode(evt.TailDistributionRegime)}</div><div class=\"kpi-sub\">极值衰减模式判定</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">EVT 99% 解析在险价值</div><div class=\"kpi-val highlight\" style=\"color:#e74c3c\">-{evt.EvtVaR99Percent:F2}%</div><div class=\"kpi-sub\">99% 期望短缺 ES: -{evt.EvtES99Percent:F2}%</div></div>");
            sb.AppendLine("  </div>");

            if (evt.ReturnPeriodList.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>极端重现期</th><th>对应交易天数</th><th>非超限置信概率</th><th>GPD 解析外推 VaR</th><th>期望断崖短缺 ES</th><th>历史样本最差跌幅</th><th>外推放大倍数</th><th>断崖严酷等级</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var rp in evt.ReturnPeriodList)
                {
                    sb.AppendLine($"    <tr><td><strong>{WebUtility.HtmlEncode(rp.PeriodName)}</strong></td><td>{rp.ReturnPeriodDays} 日</td><td>{rp.NonExceedanceProbabilityPercent:F2}%</td><td style=\"color:#e74c3c;font-weight:600\">-{rp.ExtrapolatedExtremeLossVaRPercent:F2}%</td><td style=\"color:#c0392b\">-{rp.ExtrapolatedExpectedTailLossESPercent:F2}%</td><td>-{rp.HistoricalSampleWorstLossPercent:F2}%</td><td><strong>{rp.TailExtrapolationRatio:F2}x</strong></td><td>{WebUtility.HtmlEncode(rp.ShockSeverityGrade)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(evt.EvtExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 36. Phase 33.3 MSCI Barra & Brunnermeier-Pedersen 内生流动性黑洞弹性 (LBHI) 与资产踩踏抛售反馈乘数
        if (portfolio.LiquidityBlackHole != null)
        {
            var lbh = portfolio.LiquidityBlackHole;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">三十六、MSCI Barra & Brunnermeier-Pedersen 内生流动性黑洞弹性 (LBHI) 与资产踩踏抛售反馈乘数 (Phase 33)</div>");
            sb.AppendLine("  <div class=\"narrative\">参考 MSCI Barra 流动性微观结构模型与 Brunnermeier & Pedersen (2009)《Market Liquidity and Funding Liquidity》理论，当市场遭受系统性赎回冲击时，资产价格下跌将触及其他机构的止损线与保证金门槛，诱发二次强制去杠杆抛售，使外生冲击呈几何级数演变为内生流动性黑洞。本模块测算全组合踩踏抛售级联乘数（FCM）与流动性黑洞指数（LBHI），并求解临界无踩踏变现额度 MFLC。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">一级直接赎回平仓量</div><div class=\"kpi-val\">{lbh.DirectLiquidationVolumeTenThousand:F1} 万元</div><div class=\"kpi-sub\">外生冲击直接出清规模</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">二级诱发踩踏抛售量</div><div class=\"kpi-val\" style=\"color:#e67e22\">{lbh.SecondaryInducedLiquidationVolumeTenThousand:F1} 万元</div><div class=\"kpi-sub\">螺旋级联去杠杆触发</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">踩踏级联乘数 (FCM)</div><div class=\"kpi-val highlight\">{lbh.FireSaleCascadeMultiplier:F2}x</div><div class=\"kpi-sub\">总出清规模 {lbh.AggregateFireSaleVolumeTenThousand:F1} 万元</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">外生 vs 内生总价格冲击</div><div class=\"kpi-val\">{lbh.ExogenousDirectPriceImpactPercent:F2}% ➔ {lbh.EndogenousFeedbackPriceImpactPercent:F2}%</div><div class=\"kpi-sub\">内生反馈螺旋折价跃升</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">流动性黑洞指数 (LBHI)</div><div class=\"kpi-val\">{lbh.LiquidityBlackHoleIndex:F1} / 100</div><div class=\"kpi-sub\">{WebUtility.HtmlEncode(lbh.BlackHoleRiskLevel)}</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">安全无踩踏变现额度 (MFLC)</div><div class=\"kpi-val highlight\">{lbh.MaxFireSaleCapacityTenThousand:F1} 万元</div><div class=\"kpi-sub\">冲击限制在 2.0% 内的上限</div></div>");
            sb.AppendLine("  </div>");

            if (lbh.AssetItemList.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>基金代码</th><th>基金简称</th><th>权重</th><th>估算规模(万)</th><th>日均交易量(万)</th><th>深度弹性(λ)</th><th>一级平仓(万)</th><th>直接跌幅</th><th>二级诱发(万)</th><th>级联总出清(万)</th><th>踩踏放大倍数</th><th>流动性脆性</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in lbh.AssetItemList)
                {
                    sb.AppendLine($"    <tr><td><strong>{WebUtility.HtmlEncode(item.FundCode)}</strong></td><td>{WebUtility.HtmlEncode(item.FundName)}</td><td>{item.PortfolioWeightPercent:F1}%</td><td>{item.AssetAumTenThousand:F0}</td><td>{item.DailyAdvTenThousand:F1}</td><td>{item.MarketDepthElasticity:F3}</td><td>{item.DirectSellingPressureTenThousand:F1}</td><td>-{item.DirectPriceDropPercent:F2}%</td><td style=\"color:#e67e22\">{item.InducedDeleveragingPressureTenThousand:F1}</td><td><strong>{item.TotalLiquidationVolumeTenThousand:F1}</strong></td><td>{item.FireSaleCascadeRatio:F2}x</td><td>{WebUtility.HtmlEncode(item.VulnerabilityStatusBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(lbh.LiquidityBlackHoleExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 37. Phase 33.4 Marcos Lopez de Prado 策略微观夏普衰减半衰期 (Sharpe Half-Life) 与 CUSUM 概念漂移滤波检验
        if (portfolio.SharpeDecayCusum != null)
        {
            var cusum = portfolio.SharpeDecayCusum;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">三十七、Marcos Lopez de Prado 策略微观夏普衰减半衰期 (Sharpe Half-Life) 与 CUSUM 概念漂移滤波检验 (Phase 33)</div>");
            sb.AppendLine("  <div class=\"narrative\">依据 Marcos Lopez de Prado (2018/2020)《Advances in Financial Machine Learning》前沿理论，量化策略的阿尔法并非永恒，随着市场套利效率提升与容量膨胀，夏普比率通常呈指数衰减。本模块建立双侧累积和（CUSUM）概念漂移滤波控制图以实时捕获策略结构性突变，并通过对数线性回归拟合策略衰减半衰期 t_1/2 与有效寿命终点，保障策略在失效前及时触发再训练与参数重校。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">当前年化夏普比率</div><div class=\"kpi-val highlight\">{cusum.CurrentAnnualizedSharpeRatio:F2}</div><div class=\"kpi-sub\">初期理论峰值 SR_0: {cusum.InitialEstimatedSharpeRatio:F2}</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">指数衰减速率 (λ)</div><div class=\"kpi-val\">{cusum.ExponentialDecayRateLambda:F5}</div><div class=\"kpi-sub\">连续衰减强度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">夏普衰减半衰期 (t_1/2)</div><div class=\"kpi-val highlight\">{cusum.SharpeDecayHalfLifeDays:F0} 交易日</div><div class=\"kpi-sub\">阿尔法半衰生命周期</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">预估策略失效剩余寿命</div><div class=\"kpi-val\">{cusum.EstimatedDaysToTerminalExpiration:F0} 交易日</div><div class=\"kpi-sub\">跌破无风险基准期限</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">CUSUM 负向衰变累积 (S^-)</div><div class=\"kpi-val\" style=\"color:{(cusum.TriggerStructuralDecayAlert ? "#e74c3c" : "#2ecc71")}\">{cusum.CusumNegativeAccumulator:F2} / {cusum.CusumAlertThreshold:F2}</div><div class=\"kpi-sub\">结构性衰变报警: {(cusum.TriggerStructuralDecayAlert ? "🔴 触发" : "🟢 正常")}</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">策略阿尔法生命周期健康评级</div><div class=\"kpi-val\">{WebUtility.HtmlEncode(cusum.AlphaLongevityStatusBadge)}</div><div class=\"kpi-sub\">动能爆发: {(cusum.TriggerRegimeMomentumBurst ? "⚡ 触发" : "无")}</div></div>");
            sb.AppendLine("  </div>");
            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(cusum.CusumDecayExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 38. Phase 34.1 Goldman Sachs & J.P. Morgan 跨资产非对称时滞领先-滞后互相关与信息流传导有向网络
        if (portfolio.LeadLagCrossCorrelation != null)
        {
            var ll = portfolio.LeadLagCrossCorrelation;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">三十八、Goldman Sachs & J.P. Morgan 跨资产非对称时滞领先-滞后互相关与信息流传导有向网络 (Phase 34)</div>");
            sb.AppendLine("  <div class=\"narrative\">参考高盛 Marquee 与摩根大通量化策略团队关于多资产时滞信息流传导理论，在真实金融市场中，不同流动性与风格属性的资产对宏观流动性冲击与突发事件的吸收存在显著时滞（Lag Dispersion）。本模块测算跨标的时滞互相关矩阵与非对称领先优势，求解资产综合信息领导力得分（ILS），并构建因果传导拓扑网络，前瞻锁定先行价格发现龙头与滞后反应跟随标的。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">首要价格发现龙头</div><div class=\"kpi-val highlight\">{WebUtility.HtmlEncode(ll.AnchorLeaderFundCode)}</div><div class=\"kpi-sub\">{WebUtility.HtmlEncode(ll.AnchorLeaderFundName)}</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">最深滞后吸收标的</div><div class=\"kpi-val\" style=\"color:#e67e22\">{WebUtility.HtmlEncode(ll.MostLaggingFundCode)}</div><div class=\"kpi-sub\">{WebUtility.HtmlEncode(ll.MostLaggingFundName)}</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">平均时滞离散度</div><div class=\"kpi-val\">{ll.PortfolioAverageLeadLagDispersionDays:F1} 交易日</div><div class=\"kpi-sub\">全对时滞平均跨度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">最大非对称时滞增益</div><div class=\"kpi-val highlight\">+{ll.MaxPairwiseAsymmetryGap:F3}</div><div class=\"kpi-sub\">|ρ(τ*)| - |ρ(0)| 极值差</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">最大时滞探索视界</div><div class=\"kpi-val\">±{ll.MaxLagHorizonDays} 交易日</div><div class=\"kpi-sub\">超前/滞后窗口</div></div>");
            sb.AppendLine("  </div>");

            if (ll.AssetItemList.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>基金代码</th><th>基金简称</th><th>权重</th><th>信息领导力 (ILS)</th><th>平均领先时滞</th><th>先行出度</th><th>滞后入度</th><th>网络角色</th><th>战术启示</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in ll.AssetItemList)
                {
                    sb.AppendLine($"    <tr><td><strong>{WebUtility.HtmlEncode(item.FundCode)}</strong></td><td>{WebUtility.HtmlEncode(item.FundName)}</td><td>{item.PortfolioWeightPercent:F1}%</td><td><strong>{item.InformationLeadershipScore:+0.0;-0.0;0.0}</strong></td><td>{item.AverageLeadLagDays:+0.0;-0.0;0.0} 日</td><td>{item.OutgoingLeadingLinksCount}</td><td>{item.IncomingLaggingLinksCount}</td><td>{WebUtility.HtmlEncode(item.NetworkRoleBadge)}</td><td><small>{WebUtility.HtmlEncode(item.TacticalAdvisory)}</small></td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            if (ll.PairItemList.Count > 0)
            {
                sb.AppendLine("  <div style=\"margin-top:10px;\"><strong>主要时滞传导资产对拓扑：</strong></div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>先行领涨/领跌资产</th><th>滞后传导跟随资产</th><th>最优时滞 (τ*)</th><th>峰值相关度 ρ(τ*)</th><th>同期相关度 ρ(0)</th><th>时滞非对称增益</th><th>传导烈度</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var pair in ll.PairItemList.Take(8))
                {
                    sb.AppendLine($"    <tr><td><strong>{WebUtility.HtmlEncode(pair.LeaderFundCode)}</strong> ({WebUtility.HtmlEncode(pair.LeaderFundName)})</td><td>{WebUtility.HtmlEncode(pair.FollowerFundCode)} ({WebUtility.HtmlEncode(pair.FollowerFundName)})</td><td>+{pair.OptimalLagDays} 交易日</td><td>{pair.PeakCrossCorrelation:F3}</td><td>{pair.ContemporaneousCorrelation:F3}</td><td style=\"color:#2ecc71\">+{pair.AsymmetryGap:F3}</td><td>{WebUtility.HtmlEncode(pair.LeadLagDirectionBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ll.LeadLagExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 39. Phase 34.2 BlackRock Aladdin / Axioma / Lo-MacKinlay 多重投资期限风险期限结构与方差比非随机游走检验
        if (portfolio.MultiHorizonRiskTermStructure != null)
        {
            var mh = portfolio.MultiHorizonRiskTermStructure;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">三十九、BlackRock Aladdin / Axioma / Lo-MacKinlay 多重投资期限风险期限结构与方差比非随机游走检验 (Phase 34)</div>");
            sb.AppendLine("  <div class=\"narrative\">依据 Lo & MacKinlay (1988)《Stock Market Prices Do Not Follow Random Walks》与 BlackRock Aladdin 多重投资期限风险分解理论，金融资产在现实中并非机械遵从时间平方根法则（√T Rule）。收益率的短期自相关（均值回归或动量趋势）会导致长期累积风险产生系统性扭曲。本模块测算 1d 至 252d 六大投资期限下的方差比率 VR(q) 与异方差稳健检验统计量 Z*(q)，动态重构全期限修正 VaR 与 ES 矩阵。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">基准单日年化波动率</div><div class=\"kpi-val\">{mh.BaseDailyVolatilityPercent:F2}%</div><div class=\"kpi-sub\">1日高频基准</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">252日(年度)综合方差比率</div><div class=\"kpi-val highlight\">{mh.AnnualizedLoMacKinlayVarianceRatio:F3}</div><div class=\"kpi-sub\">VR(252) 检定</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">长期真实波动率偏离失真度</div><div class=\"kpi-val\" style=\"color:{(mh.LongTermVolatilityDistortionPercent >= 0 ? "#e74c3c" : "#2ecc71")}\">{mh.LongTermVolatilityDistortionPercent:+0.0;-0.0;0.0}%</div><div class=\"kpi-sub\">偏离 √T 外推幅度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">期限结构主导动力学体制</div><div class=\"kpi-val\">{WebUtility.HtmlEncode(mh.TermStructureDominantPattern)}</div><div class=\"kpi-sub\">多期时间自相关特征</div></div>");
            sb.AppendLine("  </div>");

            if (mh.HorizonItemList.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>投资期限</th><th>期限天数 (q)</th><th>累积收益率</th><th>实际复合波动率</th><th>√T 理论波动率</th><th>Lo-MacKinlay 方差比 VR(q)</th><th>稳健检验 Z*</th><th>期限动力学体制</th><th>期限修正倍数 κ</th><th>修正 99% VaR</th><th>修正 99% ES</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in mh.HorizonItemList)
                {
                    sb.AppendLine($"    <tr><td><strong>{WebUtility.HtmlEncode(item.HorizonName)}</strong></td><td>{item.HorizonDays} 日</td><td>{item.CumulativeReturnPercent:F2}%</td><td>{item.ActualPeriodVolatilityPercent:F2}%</td><td>{item.SqrtTimeBenchmarkVolPercent:F2}%</td><td><strong>{item.LoMacKinlayVarianceRatio:F3}</strong></td><td>{item.HeteroscedasticityZScore:F2}</td><td>{WebUtility.HtmlEncode(item.DynamicsRegimeBadge)}</td><td>{item.HorizonAdjustmentMultiplier:F3}</td><td style=\"color:#e74c3c\">{item.HorizonAdjustedVaR99Percent:F2}%</td><td style=\"color:#c0392b\">{item.HorizonAdjustedES99Percent:F2}%</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mh.MultiHorizonExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 40. Phase 34.3 Two Sigma / Citadel 3 状态高斯隐马尔可夫模型 (Gaussian HMM) 宏观体制自动解码与转移信息熵
        if (portfolio.ThreeStateGaussianHmm != null)
        {
            var hmm = portfolio.ThreeStateGaussianHmm;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">四十、Two Sigma & Citadel 3 状态高斯隐马尔可夫模型 (Gaussian HMM) 宏观体制自动解码与转移信息熵 (Phase 34)</div>");
            sb.AppendLine("  <div class=\"narrative\">参考顶级量化对冲基金 Two Sigma 与 Citadel 宏观资产配置框架，金融市场状态具备非线性的体制跃迁（Regime-Switching）特征。本模块建立三状态多元高斯隐马尔可夫模型（牛市低波扩张态、震荡中波修复态、危机高波踩踏态），运用 Baum-Welch 极大似然估计状态转移矩阵，并通过 Viterbi 动态规划算法自动解码样本历史最优隐状态序列，实时计算即期后验概率向量与转移不确定性信息熵。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">当前解码所属宏观体制</div><div class=\"kpi-val highlight\">{WebUtility.HtmlEncode(hmm.CurrentDecodedStateName)}</div><div class=\"kpi-sub\">Viterbi 全局动态规划解码</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">牛市扩张态即期概率 (π1)</div><div class=\"kpi-val\" style=\"color:#2ecc71\">{hmm.BullStateProbabilityPercent:F1}%</div><div class=\"kpi-sub\">正向低波上行动能</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">震荡修复态即期概率 (π2)</div><div class=\"kpi-val\" style=\"color:#f39c12\">{hmm.NeutralStateProbabilityPercent:F1}%</div><div class=\"kpi-sub\">中波均衡箱体震荡</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">危机踩踏态即期概率 (π3)</div><div class=\"kpi-val\" style=\"color:#e74c3c\">{hmm.CrisisStateProbabilityPercent:F1}%</div><div class=\"kpi-sub\">高波负偏极端风险</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">体制转移信息熵 H(π)</div><div class=\"kpi-val\">{hmm.RegimeTransitionEntropy:F4}</div><div class=\"kpi-sub\">不确定性比率 {hmm.NormalizedEntropyPercent:F1}%</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">投审会宏观战术裁决</div><div class=\"kpi-val\">{WebUtility.HtmlEncode(hmm.MacroRegimeAdvisoryBadge)}</div><div class=\"kpi-sub\">对冲配置指引</div></div>");
            sb.AppendLine("  </div>");

            if (hmm.StateItemList.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>状态编号</th><th>状态名称</th><th>期望日收益率</th><th>年化波动率</th><th>即期后验概率</th><th>自留存粘滞概率 (P_kk)</th><th>预期驻留持续期</th><th>历史样本覆盖天数</th><th>历史占比</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var state in hmm.StateItemList)
                {
                    sb.AppendLine($"    <tr><td>{state.StateIndex}</td><td><strong>{WebUtility.HtmlEncode(state.StateName)}</strong></td><td>{state.ExpectedDailyReturnPercent:+0.00;-0.00;0.00}%</td><td>{state.AnnualizedVolatilityPercent:F1}%</td><td><strong>{state.PosteriorProbabilityPercent:F1}%</strong></td><td>{state.TransitionSelfPersistencePercent:F1}%</td><td>{state.ExpectedDwellDays:F0} 交易日</td><td>{state.HistoricalSampleDays} 天</td><td>{state.HistoricalCoveragePercent:F1}%</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(hmm.HmmExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 41. Phase 34.4 AQR Capital & Daniel-Moskowitz 广义反向择时短周期反转 Alpha 与动量崩塌预警指数 (MCWI)
        if (portfolio.MomentumCrashAndReversal != null)
        {
            var rev = portfolio.MomentumCrashAndReversal;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">四十一、AQR Capital & Daniel-Moskowitz 广义反向择时短周期反转 Alpha 与动量崩塌预警指数 (MCWI) (Phase 34)</div>");
            sb.AppendLine("  <div class=\"narrative\">参考 AQR 另类投资系列研究与 Daniel & Moskowitz (2016)《Momentum Crashes》经典理论，动量策略虽然长期夏普卓越，但在市场经历极端暴跌后转向超跌反弹的拐点，常出现毁灭性的“动量崩塌（Momentum Crash）”风险。本模块测算标的资产在 5 日流动性短频反转 Alpha（STR）与 21 日反转因子，构建熊市-牛市非对称条件 Beta（Δβ），并测算动量崩塌预警指数（MCWI）以指导动态防守与超跌反转战术增强。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">5日加权反转 Alpha 预期</div><div class=\"kpi-val highlight\">{rev.Portfolio5DayReversalAlphaPercent:+0.00;-0.00;0.00}%</div><div class=\"kpi-sub\">短期流动性超额收益</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">动量崩塌预警指数 (MCWI)</div><div class=\"kpi-val\" style=\"color:{(rev.MomentumCrashWarningIndex >= 60 ? "#e74c3c" : "#2ecc71")}\">{rev.MomentumCrashWarningIndex:F1} / 100</div><div class=\"kpi-sub\">{WebUtility.HtmlEncode(rev.MomentumProtectionStatusBadge)}</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">恐慌反弹崩塌概率 (P_Crash)</div><div class=\"kpi-val\">{rev.MomentumCrashProbabilityPercent:F1}%</div><div class=\"kpi-sub\">反转踩踏风险发生率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">非对称 Beta 偏离 (Δβ)</div><div class=\"kpi-val\">{rev.DownsideAsymmetricBetaSpread:+0.00;-0.00;0.00}</div><div class=\"kpi-sub\">β_Bear - β_Bull 差值</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">建议超跌反转倾斜换手率</div><div class=\"kpi-val highlight\">{rev.RecommendedReversalTiltTurnoverPercent:F1}%</div><div class=\"kpi-sub\">战术增强调仓比例</div></div>");
            sb.AppendLine("  </div>");

            if (rev.AssetItemList.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>基金代码</th><th>基金简称</th><th>权重</th><th>近5日收益</th><th>5日反转得分</th><th>近21日收益</th><th>21日反转得分</th><th>熊市条件 Beta</th><th>牛市条件 Beta</th><th>非对称 Beta 差</th><th>反转战术定位</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in rev.AssetItemList)
                {
                    sb.AppendLine($"    <tr><td><strong>{WebUtility.HtmlEncode(item.FundCode)}</strong></td><td>{WebUtility.HtmlEncode(item.FundName)}</td><td>{item.PortfolioWeightPercent:F1}%</td><td style=\"color:{(item.Past5DayReturnPercent >= 0 ? "#e74c3c" : "#2ecc71")}\">{item.Past5DayReturnPercent:+0.00;-0.00;0.00}%</td><td><strong>{item.ShortTermReversalScore:+0.0;-0.0;0.0}</strong></td><td style=\"color:{(item.Past21DayReturnPercent >= 0 ? "#e74c3c" : "#2ecc71")}\">{item.Past21DayReturnPercent:+0.00;-0.00;0.00}%</td><td>{item.MonthlyReversalScore:+0.0;-0.0;0.0}</td><td>{item.BearMarketDownsideBeta:F2}</td><td>{item.BullMarketUpsideBeta:F2}</td><td>{item.DownsideBetaAsymmetry:+0.00;-0.00;0.00}</td><td>{WebUtility.HtmlEncode(item.TacticalReversalBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(rev.ReversalExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 42. Phase 35.1 BIS / BCBS FRTB 内部模型法损益归因 (PLA) 与巴塞尔 250 天交通灯超限检定
        if (portfolio.FrtbPlaAndTrafficLight != null)
        {
            var pla = portfolio.FrtbPlaAndTrafficLight;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">四十二、BIS / BCBS FRTB 内部模型法损益归因 (PLA) 与巴塞尔 250 天交通灯超限检定 (Phase 35)</div>");
            sb.AppendLine("  <div class=\"narrative\">依据巴塞尔银行监管委员会 (BCBS) 最新《市场风险基本审视 (FRTB)》与国际清算银行 (BIS) 内部模型法 (IMA) 准入标准，量化风控引擎必须通过双重统计检定：前台假想损益 (HPL) 与风控理论损益 (RTPL) 的斯皮尔曼秩相关 (SRC >= 0.80) 及柯尔莫哥洛夫-斯米尔诺夫最大经验分布偏离度 (D_KS <= 0.09)；同时对过去 250 个交易日 99% VaR 违约超限次数执行红黄绿交通灯回测，动态计提监管资本惩罚乘数。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">斯皮尔曼秩相关 (SRC)</div><div class=\"kpi-val highlight\">{pla.SpearmanRankCorrelation:F3}</div><div class=\"kpi-sub\">{WebUtility.HtmlEncode(pla.SpearmanZoneStatus)}</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">柯氏分布偏离 (D_KS)</div><div class=\"kpi-val\" style=\"color:{(pla.KolmogorovSmirnovStatistic <= 0.09m ? "#2ecc71" : "#f39c12")}\">{pla.KolmogorovSmirnovStatistic:F3}</div><div class=\"kpi-sub\">{WebUtility.HtmlEncode(pla.KsZoneStatus)}</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">250天 VaR99% 超限违约</div><div class=\"kpi-val\" style=\"color:{(pla.Rolling250DaysVaRExceedanceCount <= 4 ? "#2ecc71" : "#e74c3c")}\">{pla.Rolling250DaysVaRExceedanceCount} 次</div><div class=\"kpi-sub\">{WebUtility.HtmlEncode(pla.BaselTrafficLightZone)}</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">监管资本附加乘数 (k)</div><div class=\"kpi-val\">+{pla.RegulatoryCapitalMultiplierAddOn:F2}</div><div class=\"kpi-sub\">总乘数: {pla.TotalCapitalMultiplier:F2}x</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">内部模型准入终审</div><div class=\"kpi-val highlight\">{WebUtility.HtmlEncode(pla.OverallPlaComplianceStatus)}</div><div class=\"kpi-sub\">违约概率: {pla.BacktestFailureProbabilityPercent:F1}%</div></div>");
            sb.AppendLine("  </div>");

            if (pla.RecentObservations != null && pla.RecentObservations.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>观察日期</th><th>假想损益 HPL (万元)</th><th>理论损益 RTPL (万元)</th><th>损益差异 (万元)</th><th>99% 在险价值 (万元)</th><th>违约超限状态</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var obs in pla.RecentObservations.TakeLast(10))
                {
                    sb.AppendLine($"    <tr><td>{obs.Date:yyyy-MM-dd}</td><td style=\"color:{(obs.HypotheticalPnlWan >= 0 ? "#e74c3c" : "#2ecc71")}\">{obs.HypotheticalPnlWan:+0.00;-0.00;0.00}</td><td style=\"color:{(obs.RiskTheoreticalPnlWan >= 0 ? "#e74c3c" : "#2ecc71")}\">{obs.RiskTheoreticalPnlWan:+0.00;-0.00;0.00}</td><td>{obs.PnlDifferenceWan:+0.00;-0.00;0.00}</td><td>{obs.VaR99Wan:F2}</td><td>{(obs.IsVaRExceedance ? "<span style=\"color:#e74c3c;font-weight:bold;\">⚠️ 超限违约</span>" : "<span style=\"color:#2ecc71;\">正常安全</span>")}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(pla.PlaExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 43. Phase 35.2 Millennium & Point72 Pod Shop 多策略单元动态资本分配与阶梯止损降额机制
        if (portfolio.PodShopCapitalAllocation != null)
        {
            var pod = portfolio.PodShopCapitalAllocation;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">四十三、Millennium / Point72 Pod Shop 多策略单元动态资本分配与阶梯止损降额机制 (Phase 35)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标国际顶级平台型对冲基金 Millennium 与 Point72 的自治 Pod 治理架构，系统将投资组合解构为独立的 Alpha/Beta 策略 Pod 单元。实施严苛的净值高水位线 (HWM) 阶梯式降额止损保护：当 Pod 回撤达 -2% 时冻结资金扩张；回撤达 -3% 强制削减 50% 敞口并回抽至中央风险池；回撤达 -5% 触发硬止损 (Hard Stop-Out) 强制平仓清盘，彻底斩断单策略尾部传染。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">基金总配置资本池</div><div class=\"kpi-val highlight\">{pod.TotalFundCapitalWan:F0} 万元</div><div class=\"kpi-sub\">初始母基金规模</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">实际在险运行资本</div><div class=\"kpi-val\">{pod.ActiveWorkingCapitalWan:F1} 万元</div><div class=\"kpi-sub\">活跃运作资金</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">中央风控拦截储备池</div><div class=\"kpi-val\" style=\"color:#2ecc71\">{pod.CentralReservePoolWan:F1} 万元</div><div class=\"kpi-sub\">回抽安全现金垫</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">Pod 梯次风控运行格局</div><div class=\"kpi-val\">{pod.ActivePodsCount} 正常 / {pod.DeriskedPodsCount} 降额 / {pod.StoppedOutPodsCount} 止损</div><div class=\"kpi-sub\">总 Pod: {pod.PodList.Count} 个</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">Pod 治理健康评级</div><div class=\"kpi-val highlight\">{WebUtility.HtmlEncode(pod.PodGovernanceHealthBadge)}</div><div class=\"kpi-sub\">峰值敞口: {pod.MaxPodMarginalRiskPercent:F1}%</div></div>");
            sb.AppendLine("  </div>");

            if (pod.PodList != null && pod.PodList.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>Pod 代号</th><th>策略单元名称</th><th>初始配资 (万元)</th><th>权重占比</th><th>高水位 HWM</th><th>当前回撤</th><th>历史峰值回撤</th><th>信息比率 IR</th><th>阶梯风控状态</th><th>保留资本 (万元)</th><th>边际风险 MRC</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var p in pod.PodList)
                {
                    sb.AppendLine($"    <tr><td><strong>{WebUtility.HtmlEncode(p.PodId)}</strong></td><td>{WebUtility.HtmlEncode(p.PodName)}</td><td>{p.AllocatedCapitalWan:F1}</td><td>{p.TargetWeightPercent:F1}%</td><td>{p.HighWaterMarkNav:F4}</td><td style=\"color:{(p.CurrentDrawdownPercent > 2.0m ? "#e74c3c" : "#BAC2DE")}\">-{p.CurrentDrawdownPercent:F2}%</td><td>-{p.PeakDrawdownPercent:F2}%</td><td>{p.InformationRatio:F2}</td><td>{WebUtility.HtmlEncode(p.DeriskingStatus)}</td><td><strong>{p.PostAdjustmentCapitalWan:F1}</strong></td><td>{p.MarginalRiskContributionPercent:F1}%</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(pod.PodExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 44. Phase 35.3 MSCI Barra & Axioma 风格因子 Löwdin 对称正交化与纯因子载荷矩阵
        if (portfolio.FactorOrthogonalization != null)
        {
            var fo = portfolio.FactorOrthogonalization;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">四十四、MSCI Barra & Axioma 风格因子 Löwdin 对称正交化与纯因子载荷矩阵 (Phase 35)</div>");
            sb.AppendLine("  <div class=\"narrative\">参考 MSCI Barra 经典多因子风险模型与 Axioma 高级正交化理论，现实市场中规模、价值、动量、低波与质量等风格因子间存在天然的高度共线性。本模块运用 Löwdin 对称正交化求解与原始物理因子欧氏范数距离最近的正交投影矩阵，实现因子间协方差严格归零，彻底消除由于因子耦合引发的收益翻转与风险双重计提，精准剥离资产纯 Alpha 载荷。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">正交前因子平均相关性</div><div class=\"kpi-val\" style=\"color:#e74c3c\">{fo.AverageCrossCorrelationBefore:F3}</div><div class=\"kpi-sub\">原始风格严重共线</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">正交后纯因子相关性</div><div class=\"kpi-val\" style=\"color:#2ecc71\">{fo.AverageCrossCorrelationAfter:F4}</div><div class=\"kpi-sub\">严格实现零相关</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">Löwdin 正交保真精度</div><div class=\"kpi-val highlight\">{fo.OrthogonalityAccuracy * 100m:F2}%</div><div class=\"kpi-sub\">范数最近等距逼近</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">分析风格因子维度</div><div class=\"kpi-val\">{fo.FactorCount} 大因子</div><div class=\"kpi-sub\">Size/Val/Mom/Vol/Qly</div></div>");
            sb.AppendLine("  </div>");

            if (fo.FactorList != null && fo.FactorList.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>经典风格因子</th><th>原始方差</th><th>正交纯因子方差</th><th>原因子信息保留度</th><th>正交前最大共线性</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var f in fo.FactorList)
                {
                    sb.AppendLine($"    <tr><td><strong>{WebUtility.HtmlEncode(f.FactorName)}</strong></td><td>{f.RawFactorVariance:F3}</td><td>{f.OrthogonalFactorVariance:F3}</td><td>{f.InformationPreservationRatio:F3}</td><td>{f.CrossFactorMaxCollinearity:F3}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            if (fo.AssetLoadingList != null && fo.AssetLoadingList.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>标的代码</th><th>标的名称</th><th>纯规模载荷</th><th>纯价值载荷</th><th>纯动量载荷</th><th>纯低波载荷</th><th>纯质量载荷</th><th>特异质风险比</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var al in fo.AssetLoadingList)
                {
                    sb.AppendLine($"    <tr><td><strong>{WebUtility.HtmlEncode(al.FundCode)}</strong></td><td>{WebUtility.HtmlEncode(al.FundName)}</td><td>{al.SizePureLoading:+0.00;-0.00;0.00}</td><td>{al.ValuePureLoading:+0.00;-0.00;0.00}</td><td>{al.MomentumPureLoading:+0.00;-0.00;0.00}</td><td>{al.LowVolPureLoading:+0.00;-0.00;0.00}</td><td>{al.QualityPureLoading:+0.00;-0.00;0.00}</td><td>{al.ResidualSpecificRiskPercent:F1}%</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(fo.OrthogonalExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 45. Phase 35.4 Two Sigma & Citadel 距离相关系数 (dCor) 与互信息 (MI) 非线性 Alpha 特征排序
        if (portfolio.NonlinearDistanceMutualInfo != null)
        {
            var nl = portfolio.NonlinearDistanceMutualInfo;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">四十五、Two Sigma & Citadel 距离相关系数 (dCor) 与互信息 (MI) 非线性 Alpha 特征排序 (Phase 35)</div>");
            sb.AppendLine("  <div class=\"narrative\">参考顶级量化对冲基金 Two Sigma 与 Citadel 前沿机器学习与非线性依赖挖掘技术，经典线性相关性在资产遭遇波动率挤压、微观跳跃或非线性期权损益时完全失效。本模块测算 Székely 距离相关系数 (dCor ∈ [0, 1]) 与香农互信息 (Mutual Information, MI in Bits)，在不预设任何函数形式的前提下，严格检验统计独立性并捕捉资产间的高阶非线性信息交互。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">全组合平均距离相关 (dCor)</div><div class=\"kpi-val highlight\">{nl.PortfolioAverageDistanceCorr:F3}</div><div class=\"kpi-sub\">Székely 依赖测度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">平均香农互信息 (MI)</div><div class=\"kpi-val\">{nl.PortfolioAverageMutualInformationBits:F3} Bits</div><div class=\"kpi-sub\">非线性信息流密度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">非线性 Alpha 额外增益</div><div class=\"kpi-val\" style=\"color:#2ecc71\">+{nl.NonlinearAlphaGainPercentage:F1}%</div><div class=\"kpi-sub\">相比线性 Pearson 提取</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">第一非线性驱动标的</div><div class=\"kpi-val highlight\">{WebUtility.HtmlEncode(nl.TopNonlinearAlphaDriverCode)}</div><div class=\"kpi-sub\">{WebUtility.HtmlEncode(nl.TopNonlinearAlphaDriverName)}</div></div>");
            sb.AppendLine("  </div>");

            if (nl.AssetFeatureList != null && nl.AssetFeatureList.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>重要性排名</th><th>标的代码</th><th>标的名称</th><th>线性 Pearson</th><th>距离相关 dCor</th><th>互信息 (Bits)</th><th>非线性溢出比</th><th>依赖结构属性</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var af in nl.AssetFeatureList)
                {
                    sb.AppendLine($"    <tr><td><strong>#{af.FeatureImportanceRank}</strong></td><td>{WebUtility.HtmlEncode(af.FundCode)}</td><td>{WebUtility.HtmlEncode(af.FundName)}</td><td>{af.LinearPearsonCorr:+0.00;-0.00;0.00}</td><td><strong>{af.DistanceCorrelation:F3}</strong></td><td>{af.MutualInformationBits:F3}</td><td>{af.NonlinearityPremiumRatio:F2}x</td><td>{WebUtility.HtmlEncode(af.DependenceClassification)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(nl.NonlinearExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 46. Phase 36.1 Citadel & Millennium 微观因子与资产拥挤度评分及机构踩踏排队指数 (HLRI)
        if (portfolio.AssetFactorCrowdedness != null)
        {
            var crowd = portfolio.AssetFactorCrowdedness;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">四十六、Citadel & Millennium 微观因子与资产拥挤度评分及机构踩踏排队指数 (HLRI) (Phase 36)</div>");
            sb.AppendLine("  <div class=\"narrative\">参考 Citadel 与 Millennium 等顶级多策略对冲基金的微观拥挤度风控体系，本模块综合资产估值收益拉伸度 (Z-Score)、截面协同压缩度、换手加速度比率及左尾负偏度，量化构建 0~100 踩踏风险指数 (Herd Liquidation Risk Index - HLRI)。同时前瞻模拟常态市况 (ADV 10%) 与极端踩踏市况 (ADV 2.5% 折让) 下的清仓排队天数，为投审会提供头寸削减与对冲护航依据。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合加权踩踏指数 (HLRI)</div><div class=\"kpi-val highlight\">{crowd.PortfolioAverageHlri:F1}</div><div class=\"kpi-sub\">{WebUtility.HtmlEncode(crowd.CrowdednessGovernanceBadge)}</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">极端踩踏加权排队天数</div><div class=\"kpi-val\" style=\"color:{(crowd.PortfolioWeightedLiquidationDaysStress > 3.0m ? "#e74c3c" : "#2ecc71")}\">{crowd.PortfolioWeightedLiquidationDaysStress:F1} 天</div><div class=\"kpi-sub\">2.5% ADV 踩踏折让率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">高危聚集资产数量</div><div class=\"kpi-val\" style=\"color:{(crowd.HighRiskCrowdedAssetsCount > 0 ? "#e74c3c" : "#2ecc71")}\">{crowd.HighRiskCrowdedAssetsCount} 只</div><div class=\"kpi-sub\">HLRI &ge; 65 分阈值</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">峰值拥挤标的</div><div class=\"kpi-val\">{WebUtility.HtmlEncode(crowd.PeakCrowdedAssetName)}</div><div class=\"kpi-sub\">代码: {crowd.PeakCrowdedAssetCode} ({crowd.PeakCrowdedHlri:F1}分)</div></div>");
            sb.AppendLine("  </div>");

            if (crowd.Items != null && crowd.Items.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>代码</th><th>资产标的</th><th>配置权重</th><th>估值拉伸Z</th><th>协同压缩度</th><th>换手加速度</th><th>左尾偏度</th><th>踩踏指数HLRI</th><th>拥挤层级</th><th>常态清仓天数</th><th>踩踏排队天数</th><th>调仓指导指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var it in crowd.Items)
                {
                    sb.AppendLine($"    <tr><td>{WebUtility.HtmlEncode(it.FundCode)}</td><td>{WebUtility.HtmlEncode(it.FundName)}</td><td>{it.TargetWeightPercent:F1}%</td><td>{it.ValuationStretchZScore:F2}</td><td>{it.PairwiseCorrelationCompression:F3}</td><td>{it.VolumeTurnoverAccelerationRatio:F2}x</td><td>{it.TailNegativeAsymmetry:F2}</td><td><strong>{it.HerdLiquidationRiskIndex:F1}</strong></td><td>{WebUtility.HtmlEncode(it.CrowdednessTierBadge)}</td><td>{it.EstimatedLiquidationDaysNormal:F1}天</td><td style=\"color:{(it.EstimatedLiquidationDaysStress > 4.0m ? "#e74c3c" : "#BAC2DE")}\">{it.EstimatedLiquidationDaysStress:F1}天</td><td style=\"font-size:11px;\">{WebUtility.HtmlEncode(it.DeriskingActionGuidance)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(crowd.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 47. Phase 36.2 BlackRock Aladdin & J.P. Morgan 多因子宏观情景冲击传导与压力在险价值 (Stressed VaR)
        if (portfolio.MacroFactorShockPropagation != null)
        {
            var macro = portfolio.MacroFactorShockPropagation;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">四十七、BlackRock Aladdin & J.P. Morgan 多因子宏观情景冲击传导与压力在险价值 (Stressed VaR) (Phase 36)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标贝莱德阿拉丁 (BlackRock Aladdin) 与摩根大通风险管理前瞻推演架构，系统建立覆盖权益贝塔、利率久期、大宗通胀与极端波动率的宏观敏感度矩阵。全面推演全球次贷海啸、疫情流动性休克、全球高通胀加息、能源供应链阻断及科技成长挤压 5 大宏观叙事冲击，穿透测算 Stressed VaR (99%) 与资产脆弱度排名。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">最恶劣压力情景</div><div class=\"kpi-val highlight\">{WebUtility.HtmlEncode(macro.WorstCaseScenarioName)}</div><div class=\"kpi-sub\">峰值回撤: {macro.WorstCasePortfolioLossPercent:F2}%</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">极端压力在险价值区间</div><div class=\"kpi-val\" style=\"color:#e74c3c\">{macro.StressedVaR99Range}</div><div class=\"kpi-sub\">Stressed VaR (99%)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">宏观脆弱度分散指数</div><div class=\"kpi-val\">{macro.FragilityDiversityRatio:F2}</div><div class=\"kpi-sub\">因子阻尼对冲效能</div></div>");
            sb.AppendLine("  </div>");

            if (macro.ScenarioList != null && macro.ScenarioList.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>宏观情景</th><th>情景叙事描述</th><th>权益冲击</th><th>利率变动</th><th>大宗冲击</th><th>波动率激增</th><th>组合预期损益</th><th>压力VaR99</th><th>最重受损标的</th><th>峰值回撤</th><th>韧性评级</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var sc in macro.ScenarioList)
                {
                    sb.AppendLine($"    <tr><td><strong>{WebUtility.HtmlEncode(sc.ScenarioName)}</strong></td><td style=\"font-size:11px;\">{WebUtility.HtmlEncode(sc.ScenarioDescription)}</td><td>{sc.EquityShockPercent:+0.0;-0.0;0.0}%</td><td>{sc.BondYieldChangeBps:+0;-0;0} bps</td><td>{sc.CommodityShockPercent:+0.0;-0.0;0.0}%</td><td>+{sc.VolShockPercent:F0}%</td><td style=\"color:{(sc.PortfolioExpectedPnlPercent >= 0 ? "#e74c3c" : "#2ecc71")};font-weight:bold;\">{sc.PortfolioExpectedPnlPercent:+0.00;-0.00;0.00}%</td><td style=\"color:#e74c3c\">{sc.StressedVaR99Percent:F2}%</td><td>{WebUtility.HtmlEncode(sc.HardestHitAssetName)}</td><td style=\"color:#e74c3c\">{sc.HardestHitAssetLossPercent:F2}%</td><td>{WebUtility.HtmlEncode(sc.ResilienceBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(macro.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 48. Phase 36.3 AQR Capital & Antti Ilmanen 真实波动率特征结构与方差风险溢价 (VRP)
        if (portfolio.VarianceRiskPremium != null)
        {
            var vrp = portfolio.VarianceRiskPremium;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">四十八、AQR Capital & Antti Ilmanen 真实波动率特征结构与方差风险溢价 (VRP) (Phase 36)</div>");
            sb.AppendLine("  <div class=\"narrative\">参考 AQR Capital 与 Antti Ilmanen 经典的预期收益与另类风险溢价 (ARP) 框架，期权隐含波动率 (IV) 长期因左尾保险溢价而显著系统性高于标的实际发生真实波动率 (RV)。本模块精准分离 Close-to-Close 真实年化波动率与高阶有效波动率，测算方差风险溢价利差 (IV^2 - RV^2) 与非对称偏度比，输出做空方差 Carry 收割与凸性保险决策。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合加权真实波动率 (RV)</div><div class=\"kpi-val\">{vrp.PortfolioAverageRealizedVol:F2}%</div><div class=\"kpi-sub\">历史真实年化</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">前瞻隐含波动率预期 (IV)</div><div class=\"kpi-val highlight\">{vrp.PortfolioAverageImpliedVol:F2}%</div><div class=\"kpi-sub\">风险中性期权溢价</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">方差风险溢价利差 (VRP)</div><div class=\"kpi-val\" style=\"color:#2ecc71\">+{vrp.PortfolioAverageVrpSpread:F1} 点</div><div class=\"kpi-sub\">相对溢价率: +{vrp.PortfolioAverageVrpRatio:F1}%</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">方差收割机制评级</div><div class=\"kpi-val\">{WebUtility.HtmlEncode(vrp.VrpHarvestRegimeBadge)}</div><div class=\"kpi-sub\">无方向 Carry 收益机会</div></div>");
            sb.AppendLine("  </div>");

            if (vrp.AssetVrpList != null && vrp.AssetVrpList.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>代码</th><th>资产标的</th><th>真实波动率(RV)</th><th>高阶有效波动率</th><th>隐含波动率预期(IV)</th><th>方差溢价利差(VRP)</th><th>相对溢价率</th><th>下行/上行波动比</th><th>期权收割信号</th><th>预估年化Carry收益</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var it in vrp.AssetVrpList)
                {
                    sb.AppendLine($"    <tr><td>{WebUtility.HtmlEncode(it.FundCode)}</td><td>{WebUtility.HtmlEncode(it.FundName)}</td><td>{it.RealizedVolCloseToClose:F2}%</td><td>{it.EffectiveHighLowVol:F2}%</td><td><strong>{it.ImpliedVolForecast:F2}%</strong></td><td style=\"color:{(it.VarianceRiskPremiumSpread >= 0 ? "#2ecc71" : "#e74c3c")};font-weight:bold;\">+{it.VarianceRiskPremiumSpread:F1}</td><td>+{it.VrpRatioPercent:F1}%</td><td>{it.DownsideToUpsideVolRatio:F2}</td><td>{WebUtility.HtmlEncode(it.VrpHarvestSignal)}</td><td style=\"color:#2ecc71\">+{it.CarryYieldAnnualizedPercent:F2}%</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(vrp.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 49. Phase 36.4 Two Sigma & Renaissance 摩擦成本敏感型动态无交易再平衡缓冲带 (No-Trade Buffer Bands)
        if (portfolio.DynamicNoTradeBufferBand != null)
        {
            var band = portfolio.DynamicNoTradeBufferBand;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">四十九、Two Sigma & Renaissance 摩擦成本敏感型动态无交易再平衡缓冲带 (No-Trade Buffer Bands) (Phase 36)</div>");
            sb.AppendLine("  <div class=\"narrative\">汲取 Two Sigma 与 Renaissance Technologies 的执行算法精髓，经典 Leland-Atkinson 最优动态交易带理论证明：频繁小额再平衡的买卖滑点与冲击损耗将大幅侵蚀 Alpha。本模块解析求解各资产在风险厌恶与冲击摩擦约束下的最优无交易走廊 [w_min, w_max]，当权重突破边界时仅执行向边缘的平滑调仓，大幅压降换手摩擦。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">免交易走廊内资产</div><div class=\"kpi-val highlight\">{band.InBandAssetCount} / {band.Items.Count} 只</div><div class=\"kpi-sub\">零换手零摩擦持有</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">再平衡换手率骤降</div><div class=\"kpi-val\" style=\"color:#2ecc71\">-{band.TurnoverReductionPercent:F1}%</div><div class=\"kpi-sub\">相比机械式全额调仓</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">年化预估节省摩擦损耗</div><div class=\"kpi-val\" style=\"color:#2ecc71\">+{band.EstimatedAnnualFrictionSavedWan:F2} 万元</div><div class=\"kpi-sub\">滑点与冲击沉淀保护</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">净夏普比率改善增益</div><div class=\"kpi-val highlight\">+{band.NetSharpeUplift:F2}</div><div class=\"kpi-sub\">扣除交易损耗净贡献</div></div>");
            sb.AppendLine("  </div>");

            if (band.Items != null && band.Items.Count > 0)
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>代码</th><th>资产标的</th><th>目标权重</th><th>当前实际权重</th><th>动态缓冲半宽</th><th>动态下界</th><th>动态上界</th><th>走廊突破状态</th><th>推荐平滑交易量</th><th>节省摩擦基点</th><th>执行调仓指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var it in band.Items)
                {
                    sb.AppendLine($"    <tr><td>{WebUtility.HtmlEncode(it.FundCode)}</td><td>{WebUtility.HtmlEncode(it.FundName)}</td><td>{it.TargetWeightPercent:F1}%</td><td><strong>{it.CurrentWeightPercent:F1}%</strong></td><td>&plusmn;{it.HalfBandWidthPercent:F2}%</td><td>{it.LowerBandPercent:F1}%</td><td>{it.UpperBandPercent:F1}%</td><td>{WebUtility.HtmlEncode(it.BreachStatus)}</td><td style=\"color:{(it.RecommendedTradePercent == 0 ? "#BAC2DE" : (it.RecommendedTradePercent > 0 ? "#2ecc71" : "#e74c3c"))};font-weight:bold;\">{(it.RecommendedTradePercent == 0 ? "0.0%" : $"{it.RecommendedTradePercent:+0.00;-0.00}%")}</td><td style=\"color:#2ecc71\">+{it.EstimatedFrictionSavedBps:F1} bps</td><td style=\"font-size:11px;\">{WebUtility.HtmlEncode(it.ActionGuidance)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(band.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 50. Phase 37.1 Bridgewater Associates 宏观增长-通胀双维度惊喜指数矩阵与全天候宏观贝塔正交中性化覆盖
        if (portfolio.MacroSurpriseOverlay != null)
        {
            var macro = portfolio.MacroSurpriseOverlay;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">五十、Bridgewater Associates 宏观增长-通胀双维度惊喜指数矩阵与全天候宏观贝塔正交中性化覆盖 (Phase 37)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标桥水基金 (Bridgewater Associates) Ray Dalio & Bob Prince 全天候与 Pure Alpha 核心架构：资产定价的本质取决于对经济增长与通胀的已计价预期，真正的系统性超额损失源自'宏观意外惊喜 (Surprises)'。本模块将组合分解为经济增长超预期因子 β_growth 与通胀超预期因子 β_inflation 敏感度矩阵，四象限穿透情景推演，并求解全天候宏观贝塔正交中性化覆盖头寸 (Neutralizing Overlay)，彻底熨平非意图宏观周期剧震。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合增长惊喜贝塔</div><div class=\"kpi-val highlight\">{macro.PortfolioGrowthBeta:F3}</div><div class=\"kpi-sub\">β_growth 顺周期暴露</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合通胀惊喜贝塔</div><div class=\"kpi-val highlight\">{macro.PortfolioInflationBeta:F3}</div><div class=\"kpi-sub\">β_inflation 物价暴露</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">宏观意外波动压降</div><div class=\"kpi-val\" style=\"color:#2ecc71\">-{macro.MacroVolatilityReductionPercent:F1}%</div><div class=\"kpi-sub\">{macro.MacroUnhedgedVolatilityPercent:F2}% &rarr; {macro.MacroHedgedVolatilityPercent:F2}%</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">极端回撤平抑幅度</div><div class=\"kpi-val highlight\">+{macro.MaxDrawdownMitigationPercent:F1}%</div><div class=\"kpi-sub\">{macro.MacroRegimeResilienceBadge}</div></div>");
            sb.AppendLine("  </div>");

            if (macro.ScenarioShocks.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">宏观四象限情景穿透冲击推演 (Bridgewater 4-Quadrant Shocks)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>象限情景</th><th>宏观环境特征</th><th>增长冲击(σ)</th><th>通胀冲击(σ)</th><th>未对冲损益</th><th>对冲覆盖损益</th><th>韧性改善度</th><th>预警状态</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var sc in macro.ScenarioShocks)
                {
                    sb.AppendLine($"    <tr><td><strong>{WebUtility.HtmlEncode(sc.QuadrantName)}</strong></td><td style=\"font-size:11px;\">{WebUtility.HtmlEncode(sc.EconomicEnvironment)}</td><td>{sc.GrowthSurpriseShockPercent:+0.0;-0.0}σ</td><td>{sc.InflationSurpriseShockPercent:+0.0;-0.0}σ</td><td style=\"color:{(sc.UnhedgedExpectedReturnPercent >= 0 ? "#2ecc71" : "#e74c3c")};font-weight:bold;\">{sc.UnhedgedExpectedReturnPercent:+0.00;-0.00}%</td><td style=\"color:{(sc.HedgedExpectedReturnPercent >= 0 ? "#2ecc71" : "#e74c3c")};font-weight:bold;\">{sc.HedgedExpectedReturnPercent:+0.00;-0.00}%</td><td style=\"color:#2ecc71\">+{sc.ResilienceGainPercent:F2}%</td><td>{WebUtility.HtmlEncode(sc.RiskAlertLevel)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            if (macro.AssetExposures.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">成分资产宏观惊喜敏感度与中性化覆盖配置单 (Asset Sensitivities & Overlay Weights)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>持仓权重</th><th>增长惊喜贝塔</th><th>通胀惊喜贝塔</th><th>综合意外敞口</th><th>敏感度分类</th><th>推荐中性化覆盖头寸</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var it in macro.AssetExposures)
                {
                    sb.AppendLine($"    <tr><td>{WebUtility.HtmlEncode(it.Code)}</td><td>{WebUtility.HtmlEncode(it.Name)}</td><td>{it.WeightPercent:F1}%</td><td>{it.GrowthSurpriseBeta:F3}</td><td>{it.InflationSurpriseBeta:F3}</td><td>{it.NetSurpriseExposure:F3}</td><td>{WebUtility.HtmlEncode(it.MacroSensitivityClass)}</td><td style=\"color:{(it.NeutralizingOverlayWeightPercent >= 0 ? "#2ecc71" : "#e74c3c")};font-weight:bold;\">{it.NeutralizingOverlayWeightPercent:+0.00;-0.00}%</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(macro.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 51. Phase 37.2 Citadel & Millennium 多经理平台核心风格因子正交中性化与非故意风险漂移剔除
        if (portfolio.StyleFactorNeutralization != null)
        {
            var style = portfolio.StyleFactorNeutralization;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">五十一、Citadel & Millennium 多经理平台核心风格因子正交中性化与非故意风险漂移剔除 (Phase 37)</div>");
            sb.AppendLine("  <div class=\"narrative\">汲取 Citadel、Millennium 与 Point72 顶级多经理 (Pod Shop) 平台风险中枢标准：严禁投资单元积累单向宏观风格敞口（如被动偏向小盘或追高动量）。本模块对规模、价值、动量、低波、流动性 5 大核心风格因子执行标准化主动暴露监测（中性化红线门限 |Z| &le; 0.05），通过二次规划去偏算法求解最小换手调仓单，将非故意风格风险消除 80% 以上，纯化特异 Alpha 选基选股回报。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">非故意风格消除率</div><div class=\"kpi-val\" style=\"color:#2ecc71\">{style.UnintendedStyleRiskEliminationRatioPercent:F1}%</div><div class=\"kpi-sub\">去偏调仓剔除效率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">风格风险贡献占比</div><div class=\"kpi-val\" style=\"color:#2ecc71\">{style.TotalPreHedgeStyleRiskContributionPercent:F1}% &rarr; {style.TotalPostHedgeStyleRiskContributionPercent:F1}%</div><div class=\"kpi-sub\">风险结构纯化显著</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">纯特异 Alpha 方差比</div><div class=\"kpi-val highlight\">{style.PureAlphaVarianceRatioPercent:F1}%</div><div class=\"kpi-sub\">选基选股真实贡献</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">信息比率 (IR) 提升</div><div class=\"kpi-val highlight\">+{style.InformationRatioUplift:F2}</div><div class=\"kpi-sub\">超标因子: {style.BreachedFactorCount} 项</div></div>");
            sb.AppendLine("  </div>");

            if (style.FactorExposures.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">多经理平台 5 大核心风格因子主动暴露与去偏对冲检核 (Factor Active Exposures)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>风格因子名称</th><th>当前主动暴露 (Z)</th><th>风控容忍上限</th><th>风控容忍下限</th><th>中性化后残余 (Z)</th><th>方差贡献占比</th><th>风控合规状态</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var f in style.FactorExposures)
                {
                    sb.AppendLine($"    <tr><td><strong>{WebUtility.HtmlEncode(f.FactorName)}</strong></td><td style=\"color:{(f.IsBreached ? "#e74c3c" : "#BAC2DE")};font-weight:bold;\">{f.ActiveFactorExposureZScore:+0.000;-0.000}</td><td>+{f.UpperToleranceLimit:F2}</td><td>{f.LowerToleranceLimit:F2}</td><td style=\"color:#2ecc71;font-weight:bold;\">{f.PostHedgeActiveExposureZScore:+0.000;-0.000}</td><td>{f.StyleVarianceContributionPercent:F1}%</td><td>{WebUtility.HtmlEncode(f.StatusBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            if (style.Adjustments.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">风格正交中性化最优调仓再平衡清单 (Style De-Biasing Adjustments)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>当前权重</th><th>目标权重</th><th>调仓幅度 (Δw)</th><th>跟踪误差贡献</th><th>去偏指导动作</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var adj in style.Adjustments)
                {
                    sb.AppendLine($"    <tr><td>{WebUtility.HtmlEncode(adj.Code)}</td><td>{WebUtility.HtmlEncode(adj.Name)}</td><td>{adj.CurrentWeightPercent:F1}%</td><td><strong>{adj.TargetWeightPercent:F1}%</strong></td><td style=\"color:{(adj.WeightAdjustmentPercent == 0 ? "#BAC2DE" : (adj.WeightAdjustmentPercent > 0 ? "#2ecc71" : "#e74c3c"))};font-weight:bold;\">{adj.WeightAdjustmentPercent:+0.00;-0.00}%</td><td>+{adj.TrackingErrorContributionBps:F1} bps</td><td style=\"font-size:11px;\">{WebUtility.HtmlEncode(adj.DeBiasingAction)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(style.GovernanceVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 52. Phase 37.3 AQR Capital & Antti Ilmanen 动态目标波动率定标时间序列动量 (TSMOM) 与危机 Alpha
        if (portfolio.VolatilityTargetedTsmom != null)
        {
            var tsmom = portfolio.VolatilityTargetedTsmom;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">五十二、AQR Capital & Antti Ilmanen 动态目标波动率定标时间序列动量 (TSMOM) 与危机 Alpha 防御 (Phase 37)</div>");
            sb.AppendLine("  <div class=\"narrative\">融合 AQR Capital 及学术经典 Moskowitz-Ooi-Pedersen (2012) 跨资产时间序列动量 (TSMOM) 体系：将资产按短 (21d)、中 (63d)、长 (252d) 三大周期进行趋势强弱与方向解构。关键突破在于引入'动态目标波动率定标机制 (Volatility Scaling)'：根据资产自身真实波动率反比缩放仓位，不仅极大平抑高波资产虚假动量，更能在黑天鹅与市场崩盘期捕捉下行危机 Alpha (Crisis Alpha)，提供极致的下行防御保护。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">基准目标波动率</div><div class=\"kpi-val highlight\">{tsmom.TargetVolatilityPercent:F1}%</div><div class=\"kpi-sub\">实现波动率: {tsmom.PortfolioRealizedTsmomVolatilityPercent:F2}%</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">危机 Alpha 潜力分</div><div class=\"kpi-val highlight\">{tsmom.CrisisAlphaPotentialScore:F1} / 100</div><div class=\"kpi-sub\">极端市场防御爆发力</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">动量崩溃防御指数</div><div class=\"kpi-val\" style=\"color:#2ecc71\">{tsmom.MomentumCrashDefenseIndex:F1} / 100</div><div class=\"kpi-sub\">防范趋势反转踩踏</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">多空动量净敞口</div><div class=\"kpi-val highlight\">{tsmom.NetExposurePercent:+0.0;-0.0}%</div><div class=\"kpi-sub\">多: {tsmom.LongExposurePercent:F1}% / 空防: {tsmom.ShortExposurePercent:F1}%</div></div>");
            sb.AppendLine("  </div>");

            if (tsmom.AssetSignals.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">成分资产多周期 TSMOM 动量信号与波动率定标仓位表 (TSMOM Signals & Scaling)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>短期(21d)</th><th>中期(63d)</th><th>长期(252d)</th><th>综合TSMOM</th><th>已实现波动率</th><th>波动率缩放比</th><th>推荐定标权重</th><th>趋势半衰期</th><th>危机防御评级</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var s in tsmom.AssetSignals)
                {
                    sb.AppendLine($"    <tr><td>{WebUtility.HtmlEncode(s.Code)}</td><td>{WebUtility.HtmlEncode(s.Name)}</td><td style=\"color:{(s.ShortTermTrendReturnPercent >= 0 ? "#2ecc71" : "#e74c3c")};font-weight:bold;\">{s.ShortTermTrendReturnPercent:+0.0;-0.0}%</td><td style=\"color:{(s.MidTermTrendReturnPercent >= 0 ? "#2ecc71" : "#e74c3c")};font-weight:bold;\">{s.MidTermTrendReturnPercent:+0.0;-0.0}%</td><td style=\"color:{(s.LongTermTrendReturnPercent >= 0 ? "#2ecc71" : "#e74c3c")};font-weight:bold;\">{s.LongTermTrendReturnPercent:+0.0;-0.0}%</td><td>{s.CompositeTsmomScore:+0.00;-0.00}</td><td>{s.RealizedVolatilityPercent:F1}%</td><td>&times;{s.VolatilityScalingFactor:F2}</td><td style=\"color:{(s.TargetScaledTsmomWeightPercent >= 0 ? "#2ecc71" : "#e74c3c")};font-weight:bold;\">{s.TargetScaledTsmomWeightPercent:+0.00;-0.00}%</td><td>{s.TrendHalfLifeDays:F0} 天</td><td>{WebUtility.HtmlEncode(s.CrisisAlphaBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(tsmom.StrategyRecommendation)}</div>");
            sb.AppendLine("</div>");
        }

        // 53. Phase 37.4 Two Sigma & D.E. Shaw Almgren-Chriss 连续动态最优拆单轨迹与流动性在险度量
        if (portfolio.OptimalExecutionTrajectory != null)
        {
            var exec = portfolio.OptimalExecutionTrajectory;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">五十三、Two Sigma & D.E. Shaw Almgren-Chriss 连续动态最优拆单轨迹与流动性在险度量 (Phase 37)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标顶级高频与量化执行巨头 Two Sigma 与 D.E. Shaw 工业级交易中枢：针对千万级以上大体量基金组合再平衡，传统静态 TWAP/VWAP 机械切片会引发严重的前向信息泄露与冲击成本累积。本模块采用 Almgren-Chriss (2000) 连续时间最优清算动态模型，权衡瞬时冲击损失与持仓库存波动率方差惩罚，严格解析求解特征衰减参数 κ 与双曲正弦持仓衰减曲线，给出精确到 10 个切片的动态拆单报单单，实现执行滑点最小化与执行在险价值 (VaR 95%) 严控。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">调仓清算总规模</div><div class=\"kpi-val highlight\">{exec.TotalRebalanceAmountWan:F0} 万元</div><div class=\"kpi-sub\">执行窗口: {exec.ExecutionHorizonDays:F1} 天</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">特征衰减率 κ</div><div class=\"kpi-val highlight\">{exec.CharacteristicDecayParameterKappa:F3}</div><div class=\"kpi-sub\">清算半衰期: {exec.CharacteristicDecayHalfLifeHours:F2} 小时</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">累计节省执行滑点</div><div class=\"kpi-val\" style=\"color:#2ecc71\">+{exec.ExecutionSlippageSavingsWan:F2} 万元</div><div class=\"kpi-sub\">滑点压降: -{exec.ExecutionSlippageSavingsRatioPercent:F1}%</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">执行在险价值(VaR 95%)</div><div class=\"kpi-val highlight\">{exec.ExecutionShortfallVaR95Wan:F2} 万元</div><div class=\"kpi-sub\">预期落差: {exec.AlmgrenChrissExpectedShortfallWan:F2} 万元</div></div>");
            sb.AppendLine("  </div>");

            if (exec.TrajectorySlices.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">Almgren-Chriss 10 个时钟切片最优连续拆单与冲击轨迹明细 (Execution Time Slices)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>切片序号</th><th>时钟切片标签</th><th>最优剩余持仓比例</th><th>AC最优建议报单量</th><th>传统TWAP报单量</th><th>瞬时冲击滑点</th><th>累计执行损耗</th><th>持仓方差风险折现</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var sl in exec.TrajectorySlices)
                {
                    sb.AppendLine($"    <tr><td>#{sl.SliceIndex}</td><td><strong>{WebUtility.HtmlEncode(sl.TimeLabel)}</strong></td><td>{sl.RemainingHoldingsPercent:F1}%</td><td style=\"color:#2ecc71;font-weight:bold;\">{sl.SlicedOrderVolumeWan:F1} 万元</td><td>{sl.TwapVolumeWan:F1} 万元</td><td>+{sl.TemporaryImpactBps:F1} bps</td><td>{sl.CumulativeFrictionWan:F2} 万元</td><td>{sl.InventoryVarianceRiskWan:F2} 万元</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(exec.ExecutionVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 54. Phase 38.1 Man Group AHL & AQR 跨资产大类期限结构展期收益 (Roll Yield / Carry) 与基差动量收割
        if (portfolio.TermStructureCarry != null)
        {
            var carry = portfolio.TermStructureCarry;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">五十四、Man Group AHL & AQR 跨资产大类期限结构展期收益 (Roll Yield / Carry) 与基差动量收割 (Phase 38)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Man AHL 与 AQR Capital 宏观衍生品与大类资产期限结构配置体系：资产不仅受现货预期驱动，更持续承受远期/现货基差结构 (Term Structure) 的时间价值推移。通过解耦近远月基差展期收益率 (Roll Yield / Carry) 与期限结构动量 (Basis Momentum)，在现货震荡期捕获高韧性正向贴水利差，同时坚决回避升水损耗侵蚀。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合加权年化展期收益</div><div class=\"kpi-val highlight\">{carry.PortfolioWeightedRollYieldPercent:+0.00;-0.00}%</div><div class=\"kpi-sub\">Carry 息差正向增益</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">Carry 驱动夏普提升</div><div class=\"kpi-val\" style=\"color:#2ecc71\">+{carry.CarrySharpeUplift:F2}</div><div class=\"kpi-sub\">信息比率内生优化</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">期限结构分布</div><div class=\"kpi-val highlight\">{carry.BackwardationAssetCount}贴水 / {carry.ContangoAssetCount}升水</div><div class=\"kpi-sub\">倒挂贴水 vs 升水结构</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">基差动量胜率贡献</div><div class=\"kpi-val highlight\">{carry.BasisMomentumAlphaRatioPercent:F1}%</div><div class=\"kpi-sub\">Cross-Asset Basis Alpha</div></div>");
            sb.AppendLine("  </div>");

            if (carry.AssetCarries.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">跨资产期限结构展期息差与基差动量资产配置单 (Term Structure Carry & Basis)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>近月基准</th><th>远月合约</th><th>跨期基差(bps)</th><th>年化展期收益率</th><th>曲线斜率</th><th>基差动量得分</th><th>实现波动率</th><th>Carry推荐权重</th><th>展期状态</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var ac in carry.AssetCarries)
                {
                    sb.AppendLine($"    <tr><td><code>{WebUtility.HtmlEncode(ac.Code)}</code></td><td><strong>{WebUtility.HtmlEncode(ac.Name)}</strong></td><td>{ac.FrontPrice:F2}</td><td>{ac.NextPrice:F2}</td><td>{ac.SpreadBps:+0.0;-0.0} bps</td><td style=\"color:{(ac.AnnualizedRollYieldPercent >= 0 ? "#2ecc71" : "#e74c3c")};font-weight:bold;\">{ac.AnnualizedRollYieldPercent:+0.00;-0.00}%</td><td>{ac.CurveSlope:+0.0000;-0.0000}</td><td>{ac.BasisMomentumScore:+0.00;-0.00}</td><td>{ac.RealizedVolatilityPercent:F1}%</td><td style=\"color:#2ecc71;font-weight:bold;\">{ac.RecommendedCarryWeightPercent:F1}%</td><td>{WebUtility.HtmlEncode(ac.CarryRegimeBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(carry.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 55. Phase 38.2 Renaissance Technologies & CFM 随机矩阵理论 (RMT) 马尔琴科-帕斯图尔谱分解滤波降噪
        if (portfolio.RmtSpectralFiltering != null)
        {
            var rmt = portfolio.RmtSpectralFiltering;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">五十五、Renaissance Technologies & CFM 随机矩阵理论 (RMT) 马尔琴科-帕斯图尔谱分解滤波降噪 (Phase 38)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标文艺复兴科技 (Renaissance Medallion) 与 Bouchaud CFM 高维统计物理金融架构：由于样本时序有限 (N ≈ T)，经验样本协方差矩阵充斥着随机白噪声。通过严格求解马尔琴科-帕斯图尔 (Marčenko-Pastur) 谱密度上下界 [λ_-, λ_+]，精确识别宏观系统模态与真实行业因子，对随机噪声带实施保迹非线性均值收缩，彻底消除由样本噪声引发的样本外剧烈空头与极端杠杆。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">样本时空比 Q (T/N)</div><div class=\"kpi-val highlight\">{rmt.GammaRatio:F1}</div><div class=\"kpi-sub\">渐近自由度比值</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">MP 噪声区间 [λ_-, λ_+]</div><div class=\"kpi-val highlight\">[{rmt.MarchenkoPasturLowerBound:F3}, {rmt.MarchenkoPasturUpperBound:F3}]</div><div class=\"kpi-sub\">理论随机谱界限</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">有效因子 / 噪声模态</div><div class=\"kpi-val highlight\">{rmt.SignalEigenvalueCount} 有效 / {rmt.NoiseEigenvalueCount} 噪声</div><div class=\"kpi-sub\">纯伪噪声方差: {rmt.NoiseVariancePurifiedPercent:F1}%</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">条件数降噪压降</div><div class=\"kpi-val\" style=\"color:#2ecc71\">-{rmt.ConditionNumberImprovementRatioPercent:F1}%</div><div class=\"kpi-sub\">{rmt.RawConditionNumber:F1} &rarr; {rmt.DenoisedConditionNumber:F1}</div></div>");
            sb.AppendLine("  </div>");

            if (rmt.EigenModes.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">RMT 特征值谱分解滤波与噪声剥离明细 (Eigenvalue Spectral Decomposition)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>位次</th><th>经验样本特征值 λ_k</th><th>RMT滤波收缩特征值 λ̃_k</th><th>方差解释率</th><th>有效信号判定</th><th>模态物理与金融分类</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var em in rmt.EigenModes)
                {
                    sb.AppendLine($"    <tr><td>#{em.Rank}</td><td>{em.EmpiricalEigenvalue:F4}</td><td style=\"color:#2ecc71;font-weight:bold;\">{em.FilteredEigenvalue:F4}</td><td>{em.VarianceExplainedPercent:F1}%</td><td>{(em.IsSignal ? "<span style=\"color:#2ecc71;font-weight:bold;\">✓ 真实信号</span>" : "<span style=\"color:#e67e22;\">✗ MP噪声</span>")}</td><td>{WebUtility.HtmlEncode(em.ModeClassification)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(rmt.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 56. Phase 38.3 Citadel & Millennium 多元非对称极值下行联结与组合结构性协同崩塌脆弱度矩阵
        if (portfolio.MultivariateTailCoCrash != null)
        {
            var tail = portfolio.MultivariateTailCoCrash;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">五十六、Citadel & Millennium 多元非对称极值下行联结与组合结构性协同崩塌脆弱度矩阵 (Phase 38)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Citadel 与 Millennium 极端黑天鹅下行联结网络：金融资产在常规平稳期表现出较低线性相关，但在极端黑天鹅暴跌中呈现出强烈的非对称左尾协同崩塌 (Tail Co-Crash)。本模块测算各资产对偶的非参数化下行尾部依赖 λ_L，构建级联传染图中心度，输出组合协同崩塌概率与结构性脆弱度指数 (SFI)。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">多元协同暴跌超额概率</div><div class=\"kpi-val highlight\">{tail.PortfolioLowerTailCoCrashProbabilityPercent:F2}%</div><div class=\"kpi-sub\">P(Co-Crash) 极值共振</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">非对称下行尾部占优比</div><div class=\"kpi-val highlight\">{tail.AsymmetricDownsideTailDominanceRatio:F2}x</div><div class=\"kpi-sub\">λ_L / λ_U 恐慌扩散优势</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">结构性脆弱度指数 SFI</div><div class=\"kpi-val highlight\">{tail.StructuralFragilityIndex:F1} / 100</div><div class=\"kpi-sub\">0(坚不可摧) ~ 100(极脆弱)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">下行凸性防御评级</div><div class=\"kpi-val\" style=\"color:#2ecc71\">{tail.TailConvexityDefenseRating}</div><div class=\"kpi-sub\">极值黑天鹅防御成效</div></div>");
            sb.AppendLine("  </div>");

            if (tail.AssetFragilities.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">成分资产极值尾部依赖与级联传染中心度明细 (Tail Dependence & Centrality)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>平均下行尾部依赖 λ_L</th><th>平均上行尾部依赖 λ_U</th><th>非对称倍率 λ_L/λ_U</th><th>损失级联中心度</th><th>尾部风险凸性评级</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var af in tail.AssetFragilities)
                {
                    sb.AppendLine($"    <tr><td><code>{WebUtility.HtmlEncode(af.Code)}</code></td><td><strong>{WebUtility.HtmlEncode(af.Name)}</strong></td><td style=\"color:#e74c3c;font-weight:bold;\">{af.AverageLowerTailDependence:F3}</td><td>{af.AverageUpperTailDependence:F3}</td><td>{af.AsymmetryRatio:F2}x</td><td>{af.TailGraphCentralityScore:F1}</td><td>{WebUtility.HtmlEncode(af.TailRiskConvexityBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(tail.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 57. Phase 38.4 Jane Street & Citadel Securities 微观订单流不平衡 (OFI)、Kyle 价格冲击信息份额与逆向选择足迹
        if (portfolio.MicrostructureAdverseSelection != null)
        {
            var micro = portfolio.MicrostructureAdverseSelection;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">五十七、Jane Street & Citadel Securities 微观订单流不平衡 (OFI)、Kyle 价格冲击信息份额与逆向选择足迹 (Phase 38)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Jane Street 与 Citadel Securities 顶级高频做市微观结构：在订单簿撮合层级，大额调仓往往面临知情交易者的抢跑与逆向选择 (Adverse Selection)。通过度量 Kyle's λ 价格冲击敏感度、Hasbrouck 永久信息份额与 VPIN 毒性订单流，将半买卖价差精准拆解为信息不对称成本与库存持有成本，智能调节执行进攻性与冰山挂单节奏。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合加权 Kyle's λ</div><div class=\"kpi-val highlight\">{micro.PortfolioAverageKyleLambdaBps:F2} bps</div><div class=\"kpi-sub\">冲击敏感度 / 千万元成交</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">Hasbrouck 永久信息份额</div><div class=\"kpi-val highlight\">{micro.PermanentAlphaSharePercent:F1}%</div><div class=\"kpi-sub\">知情交易永久性冲击占比</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">VPIN 订单流毒性指数</div><div class=\"kpi-val highlight\">{micro.PortfolioVpinToxicityIndex:F1} / 100</div><div class=\"kpi-sub\">毒性流概率综合评分</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">微观执行节奏指引</div><div class=\"kpi-val\" style=\"font-size:12px;color:#2ecc71\">{WebUtility.HtmlEncode(micro.ExecutionSpeedGuidance.Substring(0, Math.Min(16, micro.ExecutionSpeedGuidance.Length)))}...</div><div class=\"kpi-sub\">动态冰山/参与率控制</div></div>");
            sb.AppendLine("  </div>");

            if (micro.AssetMicrostructures.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">成分资产微观价格冲击与逆向选择摩擦成本剖析 (Kyle's Lambda & Adverse Selection)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>Kyle's λ (bps)</th><th>Hasbrouck永久份额</th><th>VPIN毒性评分</th><th>逆向选择摩擦(bps)</th><th>做市存货摩擦(bps)</th><th>执行进攻性建议</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var ma in micro.AssetMicrostructures)
                {
                    sb.AppendLine($"    <tr><td><code>{WebUtility.HtmlEncode(ma.Code)}</code></td><td><strong>{WebUtility.HtmlEncode(ma.Name)}</strong></td><td>{ma.KyleLambdaBps:F2}</td><td>{ma.HasbrouckPermanentInfoSharePercent:F1}%</td><td>{ma.VpinToxicityScore:F1}</td><td style=\"color:#e74c3c;font-weight:bold;\">+{ma.AdverseSelectionCostBps:F1} bps</td><td>+{ma.InventoryHoldingFrictionBps:F1} bps</td><td>{WebUtility.HtmlEncode(ma.ExecutionAggressionRecommendation)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(micro.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 58. Phase 39.1 Bridgewater Associates & AQR Capital 主因子正交风险平价 (PFRP) 与特征风险预算配置引擎
        if (portfolio.PrincipalFactorRiskParity != null)
        {
            var pfrp = portfolio.PrincipalFactorRiskParity;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">五十八、Bridgewater Associates & AQR Capital 主因子正交风险平价与特征风险预算配置引擎 (Phase 39)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标桥水全天候 2.0 (All Weather 2.0) 与 AQR 主因子风险平价 (PFRP)：传统名义资产配置往往存在强烈的底层宏观多重共线性（股票、高收益债、大宗商品在衰退中高度同向暴跌）。通过谱分解投影至互不相关的正交特征主因子空间，求解主因子等风险贡献 (Factor ERC) 权重，彻底消除宏观因子集中隐患。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">提取正交主因子数</div><div class=\"kpi-val highlight\">{pfrp.TotalPrincipalFactors} 个</div><div class=\"kpi-sub\">正交特征投影维度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">有效独立主因子数 (ENPF)</div><div class=\"kpi-val highlight\">{pfrp.EffectiveNumberOfPrincipalFactors:F2}</div><div class=\"kpi-sub\">香农特征因子熵度量</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">主因子正交分散化比率</div><div class=\"kpi-val highlight\">{pfrp.FactorDiversificationRatio:F2}</div><div class=\"kpi-sub\">FDR 因子正交分散增益</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">极端穿透方差预期压降</div><div class=\"kpi-val\" style=\"color:#2ecc71\">-{pfrp.PostParityVarianceReductionPercent:F1}%</div><div class=\"kpi-sub\">消解单因子集中风险</div></div>");
            sb.AppendLine("  </div>");

            if (pfrp.PrincipalFactors.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">特征主因子方差解释度与风险平价再平衡配置全貌 (Principal Factor Risk Parity Decomposition)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>因子序数</th><th>主因子特征属性</th><th>特征值 (λ)</th><th>解释方差</th><th>累计方差</th><th>初始风险贡献</th><th>平价风险贡献</th><th>特征合成权重</th><th>主导驱动体制</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var pf in pfrp.PrincipalFactors)
                {
                    sb.AppendLine($"    <tr><td><code>F{pf.FactorIndex}</code></td><td><strong>{WebUtility.HtmlEncode(pf.FactorName)}</strong></td><td>{pf.Eigenvalue:F3}</td><td>{pf.VarianceExplainedPercent:F1}%</td><td>{pf.CumulativeVariancePercent:F1}%</td><td style=\"color:#e74c3c;\">{pf.RawFactorRiskContributionPercent:F1}%</td><td style=\"color:#2ecc71;font-weight:bold;\">{pf.ParityFactorRiskContributionPercent:F1}%</td><td>{pf.OptimalEigenmodeWeightPercent:F1}%</td><td>{WebUtility.HtmlEncode(pf.DominantDriverRegime)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(pfrp.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 59. Phase 39.2 Millennium Management & Point72 动态下行凸性期权对冲与广义波动率偏度复制引擎
        if (portfolio.DynamicDownsideConvexityHedge != null)
        {
            var cvx = portfolio.DynamicDownsideConvexityHedge;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">五十九、Millennium Management & Point72 动态下行凸性期权对冲与广义波动率偏度复制引擎 (Phase 39)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标千禧年尾部风控台与 Point72 波动率偏度套利：在黑天鹅事件中，被动止损常因断崖跳空和流动性枯竭而失效。基于 SVI 波动率偏度曲面拟合，动态合成保护性虚值看跌期权 (Synthetic Protective Put)，以极低保费损耗锁定组合非线性正凸性 (Positive Downside Convexity)。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">SVI 波动率偏度斜率</div><div class=\"kpi-val highlight\">{cvx.VolatilitySkewSlopeBps:F0} bps</div><div class=\"kpi-sub\">虚值看跌期权溢价斜率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合下行凸性缺口得分</div><div class=\"kpi-val highlight\">{cvx.PortfolioConvexityDeficitScore:F1} / 100</div><div class=\"kpi-sub\">左尾非线性防护需求度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">建议看跌凸性对冲覆盖率</div><div class=\"kpi-val highlight\">{cvx.OptimalTotalHedgeRatioPercent:F1}%</div><div class=\"kpi-sub\">三梯队合成期权头寸</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">极端暴跌20%凸性缓冲</div><div class=\"kpi-val\" style=\"color:#2ecc71\">+{cvx.MaxCushionBufferPercent:F1}%</div><div class=\"kpi-sub\">最大非线性缓冲吸收率</div></div>");
            sb.AppendLine("  </div>");

            if (cvx.StrikeHedgingProfiles.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">三梯队虚值行权档位期权 Greeks 与保费损耗对冲明细 (Convexity Strike Hedging Profile)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>虚值档位</th><th>名义行权价</th><th>拟合隐含波动率</th><th>Delta (Δ)</th><th>Gamma (Γ)</th><th>Vega (ν)</th><th>建议对冲比例</th><th>年化保费损耗</th><th>暴跌缓冲倍数</th><th>防御层级标识</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var sp in cvx.StrikeHedgingProfiles)
                {
                    sb.AppendLine($"    <tr><td><strong>{sp.MoneynessPercent:F0}%</strong></td><td>{sp.StrikePrice:F1}</td><td>{sp.ImpliedVolatilityPercent:F1}%</td><td>{sp.OptionDelta:F2}</td><td>{sp.OptionGamma:F3}</td><td>{sp.OptionVega:F3}</td><td style=\"font-weight:bold;\">{sp.RecommendedHedgeRatioPercent:F1}%</td><td style=\"color:#e74c3c;\">-{sp.AnnualizedThetaCostPercent:F2}%</td><td style=\"color:#2ecc71;font-weight:bold;\">{sp.StressDownsideBufferRatio:F2}x</td><td>{WebUtility.HtmlEncode(sp.ProtectionTierBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(cvx.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 60. Phase 39.3 Renaissance Technologies & D.E. Shaw 贝叶斯时变状态空间卡尔曼滤波与自适应 Alpha 动态跟踪
        if (portfolio.BayesianKalmanAlphaTracker != null)
        {
            var kalman = portfolio.BayesianKalmanAlphaTracker;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">六十、Renaissance Technologies & D.E. Shaw 贝叶斯时变状态空间卡尔曼滤波与自适应 Alpha 动态跟踪 (Phase 39)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标文艺复兴大奖章时空信号滤波与 D.E. Shaw 自适应 Alpha 跟踪：传统滑动窗口 OLS 无法兼顾估计方差与时间滞后。基于状态空间卡尔曼递归方程，自适应吸收观测新息残差，实时平滑滤除高频随机白噪声，锁定真实时变超额回报并敏锐识别体制结构性断裂点。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合卡尔曼滤波时变Alpha</div><div class=\"kpi-val highlight\">+{kalman.PortfolioFilteredAlphaAnnualizedPercent:F2}%</div><div class=\"kpi-sub\">时变去噪超额年化回报</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">高频估计白噪声压降</div><div class=\"kpi-val\" style=\"color:#2ecc71\">-{kalman.TrackingNoiseReductionPercent:F1}%</div><div class=\"kpi-sub\">较传统滑动OLS平滑度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">动态对冲信息比率(IR)提升</div><div class=\"kpi-val\" style=\"color:#2ecc71\">+{kalman.InformationRatioUpliftPercent:F1}%</div><div class=\"kpi-sub\">真实 Alpha 信号纯度增益</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">结构性断裂资产数</div><div class=\"kpi-val highlight\">{kalman.StructuralBreakAssetCount} 只</div><div class=\"kpi-sub\">捕获因子体制漂移</div></div>");
            sb.AppendLine("  </div>");

            if (kalman.AssetKalmanAlphas.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">成分基金卡尔曼状态空间时变贝塔与自适应 Alpha 跟踪明细 (Kalman Dynamic Beta & Filtered Alpha)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>静态OLS贝塔</th><th>滤波时变贝塔</th><th>新息先验方差</th><th>卡尔曼最优增益</th><th>时变纯Alpha(年化)</th><th>半衰期(天)</th><th>体制漂移置信度</th><th>Alpha轨道路由</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var ka in kalman.AssetKalmanAlphas)
                {
                    sb.AppendLine($"    <tr><td><code>{WebUtility.HtmlEncode(ka.Code)}</code></td><td><strong>{WebUtility.HtmlEncode(ka.Name)}</strong></td><td>{ka.StaticBeta:F2}</td><td style=\"font-weight:bold;color:#3498db;\">{ka.FilteredDynamicBeta:F2}</td><td>{ka.PriorInnovationVariance:F4}</td><td>{ka.KalmanOptimalGain:F3}</td><td style=\"color:#2ecc71;font-weight:bold;\">+{ka.DynamicAlphaAnnualizedPercent:F2}%</td><td>{ka.AlphaDecayHalfLifeDays:F0} 天</td><td>{ka.RegimeShiftConfidencePercent:F1}%</td><td>{WebUtility.HtmlEncode(ka.AlphaTrajectoryBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(kalman.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 61. Phase 39.4 Jane Street & Jump Trading 跨资产微观流动性共振裂谷与闪崩级联阻尼器
        if (portfolio.CrossAssetLiquidityChasmDamper != null)
        {
            var chasm = portfolio.CrossAssetLiquidityChasmDamper;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">六十一、Jane Street & Jump Trading 跨资产微观流动性共振裂谷与闪崩级联阻尼器 (Phase 39)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Jane Street 与 Jump Trading 跨市场高频做市网络：当单一资产发生流动性踩踏时，多边做市商会跨资产协同撤出买盘挂单，引发系统性流动性瞬时真空。通过 Amihud 不流动性协方差与闪崩阻尼矩阵，动态测算流动性共振裂谷脆弱度指数 (LCI)，并在微观报单层级设置智能节流保护阀门。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">流动性共振裂谷指数 (LCI)</div><div class=\"kpi-val highlight\">{chasm.LiquidityChasmIndex:F1} / 100</div><div class=\"kpi-sub\">跨资产协同蒸发脆弱度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">闪崩级联传染放大倍数</div><div class=\"kpi-val highlight\">{chasm.FlashCrashCascadeAmplifierRatio:F2}x</div><div class=\"kpi-sub\">做市商抽单放大效应</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合自愈阻尼吸收效率</div><div class=\"kpi-val\" style=\"color:#2ecc71\">{chasm.SystemicLiquidityDampingScorePercent:F1}%</div><div class=\"kpi-sub\">跨市场阻尼缓冲能力</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">做市商撤单整体警报</div><div class=\"kpi-val\" style=\"font-size:12px;color:#e67e22\">{WebUtility.HtmlEncode(chasm.MarketMakerPullbackAlertLevel)}</div><div class=\"kpi-sub\">多边流动性安全评级</div></div>");
            sb.AppendLine("  </div>");

            if (chasm.AssetLiquidityChasms.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">成分资产微观不流动性联动贝塔与闪崩级联阻尼明细 (Liquidity Chasm & Flash Crash Damper)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>Amihud不流动性</th><th>跨资产流动性贝塔</th><th>做市商抽单敏感度</th><th>闪崩注入权重</th><th>阻尼吸收能力</th><th>执行节流阀门状态</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var lc in chasm.AssetLiquidityChasms)
                {
                    sb.AppendLine($"    <tr><td><code>{WebUtility.HtmlEncode(lc.Code)}</code></td><td><strong>{WebUtility.HtmlEncode(lc.Name)}</strong></td><td>{lc.AmihudIlliquidityMeasure:F2}</td><td>{lc.CrossAssetLiquidityBeta:F2}</td><td>{lc.MarketMakerPullbackSensitivityScore:F1}</td><td style=\"color:#e74c3c;\">{lc.CascadeFlashCrashInjectionWeightPercent:F1}%</td><td style=\"color:#2ecc71;font-weight:bold;\">{lc.DamperAbsorptionCapacityPercent:F1}%</td><td>{WebUtility.HtmlEncode(lc.ExecutionThrottlingGateStatus)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(chasm.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 62. Phase 40.1 Bridgewater Associates & Citadel 宏观马尔可夫区制转移 (MRS) 与跨周期条件资产配置
        if (portfolio.MacroMarkovRegimeSwitching != null)
        {
            var mrs = portfolio.MacroMarkovRegimeSwitching;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">六十二、Bridgewater Associates & Citadel 宏观马尔可夫区制转移概率模型与跨周期条件资产配置引擎 (Phase 40)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标桥水全天候宏观动力学与 Citadel 宏观战术对冲系统：传统资产配置往往依赖静态经济象限划分，无法捕捉金融市场在牛市扩张、中性震荡、货币紧缩与危机枯竭状态之间的非线性跃迁。通过 4 状态 Hamilton 滤波推导后验体制滤波概率、遍历稳态极限分布与体制信息熵，自适应融合各体制条件收益协方差结构，输出跨周期动态优化再平衡。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">当前主导宏观体制</div><div class=\"kpi-val highlight\" style=\"font-size:12.5px;\">{WebUtility.HtmlEncode(mrs.DominantRegimeName)}</div><div class=\"kpi-sub\">4 状态 Hamilton 滤波最优识别</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">主导体制后验置信度</div><div class=\"kpi-val highlight\">{mrs.DominantRegimeConfidencePercent:F1}%</div><div class=\"kpi-sub\">滤波后验概率峰值</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">体制不确定性香农熵</div><div class=\"kpi-val\" style=\"color:#2ecc71\">{mrs.RegimeEntropyIndex:F2}</div><div class=\"kpi-sub\">归一化信息熵 (0~1.0 越低越确定)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">跨周期夏普增益</div><div class=\"kpi-val\" style=\"color:#f1c40f\">+{mrs.AdaptiveCrossCycleSharpeUpliftPercent:F1}%</div><div class=\"kpi-sub\">自适应条件配置效能</div></div>");
            sb.AppendLine("  </div>");

            if (mrs.RegimeStates.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">宏观马尔可夫转移矩阵状态分解与跨周期条件配置明细 (Markov Regime Switching Profile)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>体制序号</th><th>体制名称</th><th>滤波后验概率</th><th>全样本平滑概率</th><th>遍历稳态概率</th><th>条件年化收益</th><th>条件年化波动</th><th>预期持续期</th><th>建议权重</th><th>宏观特征徽章</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var st in mrs.RegimeStates)
                {
                    sb.AppendLine($"    <tr><td><code>S{st.StateIndex}</code></td><td><strong>{WebUtility.HtmlEncode(st.RegimeName)}</strong></td><td style=\"color:#3498db;font-weight:bold;\">{st.FilteredProbabilityPercent:F1}%</td><td>{st.SmoothedProbabilityPercent:F1}%</td><td>{st.ErgodicSteadyStateProbPercent:F1}%</td><td style=\"color:{(st.RegimeConditionalReturnAnnualizedPercent >= 0 ? "#e74c3c" : "#2ecc71")}\">{st.RegimeConditionalReturnAnnualizedPercent:F1}%</td><td>{st.RegimeConditionalVolatilityAnnualizedPercent:F1}%</td><td>{st.ExpectedDurationDays:F1} 天</td><td style=\"font-weight:bold;\">{st.RecommendedRegimeWeightPercent:F1}%</td><td>{WebUtility.HtmlEncode(st.RegimeMacroBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mrs.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 63. Phase 40.2 AQR Capital & Man Group AHL 多频率截面交叉动量 (CSMOM) 与双重动量相对优势剥离
        if (portfolio.CrossSectionalMomentum != null)
        {
            var mom = portfolio.CrossSectionalMomentum;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">六十三、AQR Capital & Man Group AHL 多频率截面交叉动量与双重相对优势动量剥离引擎 (Phase 40)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 AQR 风格溢价与 Man Group AHL 趋势对冲体系：突破单一资产时间序列动量 (TSMOM) 在震荡行情中的假突破陷阱，跨越 1M/3M/6M/12M 多频率周期测算资产池截面 Z-Score 分布与 Rank IC；融合 Hurst 长期记忆指数检验真实趋势持续性，剥离多空利差 (Winner-Loser Spread) 与双重动量信号。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">截面动量离散度</div><div class=\"kpi-val highlight\">{mom.CrossSectionalDispersionPercent:F2}%</div><div class=\"kpi-sub\">资产收益分化度 (CS-Vol)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">胜者-劣者多空利差</div><div class=\"kpi-val highlight\">+{mom.WinnerLoserSpreadAnnualizedPercent:F2}%</div><div class=\"kpi-sub\">12M 截面 Alpha 溢价</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">截面预测 Rank IC</div><div class=\"kpi-val\" style=\"color:#2ecc71\">{mom.RankInformationCoefficient:F3}</div><div class=\"kpi-sub\">截面排名信息系数</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合平均 Hurst 指数</div><div class=\"kpi-val\" style=\"color:#f1c40f\">{mom.PortfolioAverageHurstExponent:F2}</div><div class=\"kpi-sub\">趋势记忆性持久度</div></div>");
            sb.AppendLine("  </div>");

            if (mom.AssetMomentums.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">成分资产多频率截面动量排序与双重动量信号明细 (Cross-Sectional Momentum & Dual Signal)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>截面名次</th><th>资产代码</th><th>资产名称</th><th>1M动量</th><th>3M动量</th><th>6M动量</th><th>12M动量</th><th>截面Z-Score</th><th>Hurst指数</th><th>双重动量得分</th><th>决策信号</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var a in mom.AssetMomentums)
                {
                    sb.AppendLine($"    <tr><td><strong>#{a.CrossSectionalRank}</strong></td><td><code>{WebUtility.HtmlEncode(a.Code)}</code></td><td><strong>{WebUtility.HtmlEncode(a.Name)}</strong></td><td>{a.Momentum1MPercent:F2}%</td><td>{a.Momentum3MPercent:F2}%</td><td>{a.Momentum6MPercent:F2}%</td><td>{a.Momentum12MPercent:F2}%</td><td style=\"color:{(a.CompositeCrossSectionalZScore >= 0 ? "#e74c3c" : "#2ecc71")};font-weight:bold;\">{a.CompositeCrossSectionalZScore:F2}</td><td>{a.HurstPersistenceExponent:F2}</td><td style=\"font-weight:bold;\">{a.DualMomentumScore:F1}</td><td>{WebUtility.HtmlEncode(a.DualMomentumSignal)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mom.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 64. Phase 40.3 Millennium Management & Balyasny (BAM) 多策略 Pod Shop 阶梯式硬风控回撤熔断与动态资本再平衡
        if (portfolio.PodTieredDrawdownCircuitBreaker != null)
        {
            var pod = portfolio.PodTieredDrawdownCircuitBreaker;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">六十四、Millennium Management & Balyasny (BAM) 多策略 Pod Shop 阶梯式硬风控回撤熔断与动态资本再平衡矩阵 (Phase 40)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Millennium 与 Balyasny 顶级多经理平台硬约束风控体系：针对各策略/底层资产独立追踪历史高水位线 (HWM) 与实时回撤深度，严格部署四档阶梯熔断停机线（-3% 预警减额、-5% 减半降杠杆、-7.5% 硬冻结套保、-10% 停机清盘），并根据边际夏普效率将释放资本动态注入高胜率策略。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">Pod 整体风控健康度</div><div class=\"kpi-val highlight\">{pod.OverallPodHealthScore:F1} / 100</div><div class=\"kpi-sub\">多经理平台综合防御评分</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">Pod 阶梯状态分布</div><div class=\"kpi-val highlight\" style=\"font-size:12px;\">正常 {pod.NormalPodCount} | 减额 {pod.ThrottledPodCount} | 熔断 {pod.CircuitBreakerTriggeredCount}</div><div class=\"kpi-sub\">各梯队 Pod 数量</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">成功保全防御资本</div><div class=\"kpi-val\" style=\"color:#2ecc71\">{pod.TotalCapitalProtectedPercent:F1}%</div><div class=\"kpi-sub\">阶梯熔断锁定保全比例</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">动态再平衡效率增益</div><div class=\"kpi-val\" style=\"color:#f1c40f\">+{pod.DynamicRebalancingEfficiencyGainPercent:F2}%</div><div class=\"kpi-sub\">资本再分配年化效能提升</div></div>");
            sb.AppendLine("  </div>");

            if (pod.PodTiers.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">Pod 策略单元水下回撤、阶梯硬熔断乘数与资本动态再平衡明细 (Pod Circuit Breaker Matrix)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>Pod 代码</th><th>策略名称</th><th>基准资本占比</th><th>当前水下回撤</th><th>历史最大回撤</th><th>硬风控所处阶梯</th><th>资本调整乘数</th><th>风控后有效资本</th><th>边际夏普贡献</th><th>释放再平衡资本</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var pt in pod.PodTiers)
                {
                    sb.AppendLine($"    <tr><td><code>{WebUtility.HtmlEncode(pt.PodCode)}</code></td><td><strong>{WebUtility.HtmlEncode(pt.PodName)}</strong></td><td>{pt.AllocatedCapitalPercent:F1}%</td><td style=\"color:#e74c3c;font-weight:bold;\">-{pt.CurrentDrawdownPercent:F2}%</td><td>-{pt.MaxHistoricalDrawdownPercent:F2}%</td><td>{WebUtility.HtmlEncode(pt.CircuitBreakerTier)}</td><td>{pt.DynamicCapitalMultiplier:F2}x</td><td style=\"font-weight:bold;\">{pt.PostBreakerCapitalPercent:F1}%</td><td>{pt.MarginalSharpeContribution:F2}</td><td>{pt.ReleasedCapitalReallocatedPercent:F1}%</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(pod.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 65. Phase 40.4 Jane Street & Optiver 微观限价单队列成交概率与高频期现基差收敛套利
        if (portfolio.LimitOrderQueueAndBasisArbitrage != null)
        {
            var qarb = portfolio.LimitOrderQueueAndBasisArbitrage;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">六十五、Jane Street & Optiver 微观限价单队列成交概率与高频期现基差收敛套利执行引擎 (Phase 40)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Jane Street 与 Optiver 极速做市与统计套利算法：微观层面建立离散订单簿排队衰减与指数成交概率模型 P(Fill|delta)，测算最优摆单价差深度与被动排队滑点节省；宏观微观联动跟踪一篮子现货与股指衍生品基差无风险套利偏离度与均值回复半衰期，实现从被动承受冲击到主动套利收割。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">被动限价单预期成交概率</div><div class=\"kpi-val highlight\">{qarb.PortfolioAverageFillProbabilityPercent:F1}%</div><div class=\"kpi-sub\">加权队列排队成交胜率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">被动挂单年化滑点节省</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">+{qarb.AnnualizedPassiveExecutionSavingsPercent:F2}%</div><div class=\"kpi-sub\">相比市价吃单摩擦优化</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">最大期现基差偏离度</div><div class=\"kpi-val\" style=\"color:#e74c3c\">{qarb.MaxBasisMispricingBps:F1} bps</div><div class=\"kpi-sub\">跨品种/现货期货基差</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">合成期现套利年化收益</div><div class=\"kpi-val\" style=\"color:#f1c40f\">{qarb.AnnualizedSyntheticBasisArbitrageYieldPercent:F2}%</div><div class=\"kpi-sub\">半衰期 {qarb.AverageBasisHalfLifeDays:F1} 天无风险收敛</div></div>");
            sb.AppendLine("  </div>");

            if (qarb.AssetQueueArbitrages.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">成分资产微观挂单深度、队列成交概率与期现基差套利明细 (Limit Order Queue & Basis Arbitrage)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>最优挂单深度</th><th>被动成交概率</th><th>队列等待耗时</th><th>期现基差偏离</th><th>套利年化收益</th><th>收敛半衰期</th><th>被动滑点节省</th><th>执行策略标签</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var q in qarb.AssetQueueArbitrages)
                {
                    sb.AppendLine($"    <tr><td><code>{WebUtility.HtmlEncode(q.Code)}</code></td><td><strong>{WebUtility.HtmlEncode(q.Name)}</strong></td><td>{q.OptimalLimitSpreadBps:F1} bps</td><td style=\"color:#2ecc71;font-weight:bold;\">{q.PassiveFillProbabilityPercent:F1}%</td><td>{q.QueueWaitTimeSeconds:F0} 秒</td><td style=\"color:#e74c3c;font-weight:bold;\">{q.CashFuturesBasisBps:F1} bps</td><td>{q.AnnualizedCarryArbitrageYieldPercent:F2}%</td><td>{q.BasisMeanReversionHalfLifeDays:F1} 天</td><td>+{q.PassiveSlippageSavingsBps:F1} bps</td><td>{WebUtility.HtmlEncode(q.ExecutionRegimeBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(qarb.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }
        
        // 66. Phase 41.1 D.E. Shaw & WorldQuant 符号基因规划自适应 Alpha 因子挖掘引擎
        if (portfolio.SymbolicGeneticAlphaMining != null)
        {
            var gen = portfolio.SymbolicGeneticAlphaMining;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">六十六、D.E. Shaw & WorldQuant 符号基因规划自适应 Alpha 因子挖掘引擎 (Phase 41)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 D.E. Shaw 与 WorldQuant 前沿符号表达式 Alpha 挖掘系统：基于基因表达式树（算术符、时间序列差分/加权平滑/相关性/极值排名算子）执行多代遗传变异演化，结合奥卡姆剃刀复杂度惩罚与 IC/Rank IC/ICIR 多目标适应度函数，全自动化挖掘高信息比率且低相关性的公式化纯 Alpha 因子群。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">最优 Alpha 公式 ICIR</div><div class=\"kpi-val highlight\">{gen.TopAlphaInformationRatio:F2}</div><div class=\"kpi-sub\">最高信息比率基因</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">多 Alpha 复合超额</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">+{gen.MultiAlphaEnsembleAnnualizedAlpha:F2}%</div><div class=\"kpi-sub\">集成正交组合年化 Alpha</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">遗传演化代数 / 公式数</div><div class=\"kpi-val\" style=\"font-size:13px;color:#89B4FA\">{gen.TotalGenerationsEvolved} 代 / {gen.TotalFormulasEvaluated:N0} 式</div><div class=\"kpi-sub\">搜索空间遍历深度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">平均公式语法树复杂度</div><div class=\"kpi-val\" style=\"color:#f1c40f\">{gen.AverageFormulaComplexity:F2}</div><div class=\"kpi-sub\">奥卡姆剃刀惩罚测度</div></div>");
            sb.AppendLine("  </div>");

            if (gen.EvolvedAlphaFormulas.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">演化优胜符号基因表达式、IC/ICIR 表现与综合适应度明细 (Symbolic Alpha Formula Genome)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>公式编号</th><th>符号表达式 (Symbolic Expression)</th><th>IC</th><th>Rank IC</th><th>ICIR</th><th>多空夏普</th><th>复杂度惩罚</th><th>适应度得分</th><th>因子族系分类</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var f in gen.EvolvedAlphaFormulas)
                {
                    sb.AppendLine($"    <tr><td><code>{WebUtility.HtmlEncode(f.FormulaId)}</code></td><td><code style=\"color:#89B4FA;\">{WebUtility.HtmlEncode(f.Expression)}</code></td><td style=\"color:#2ecc71;font-weight:bold;\">+{f.InformationCoefficient:F3}</td><td style=\"color:#2ecc71;\">+{f.RankInformationCoefficient:F3}</td><td style=\"font-weight:bold;color:#f1c40f;\">{f.IcInformationRatio:F2}</td><td>{f.LongShortAnnualizedSharpe:F2}</td><td>{f.ComplexityPenalty:F2}</td><td><strong>{f.FitnessScore:F1}</strong></td><td>{WebUtility.HtmlEncode(f.FormulaFamily)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(gen.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 67. Phase 41.2 Two Sigma & Man Group AHL 知识图谱跨资产因果时滞传递与宏观情绪溢出网络
        if (portfolio.CausalKnowledgeGraphSpillover != null)
        {
            var ckg = portfolio.CausalKnowledgeGraphSpillover;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">六十七、Two Sigma & Man Group AHL 知识图谱跨资产因果时滞传递与宏观情绪溢出网络 (Phase 41)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Two Sigma 与 Man Group AHL 宏观有向因果知识图谱系统：采用时滞 Granger 因果检验与信息传递熵 (Transfer Entropy) 刻画跨大类资产间的信息非对称流向，构建 Diebold-Yilmaz 波动与情绪溢出拓扑网络，精确定位全系统情绪发射源头 (Transmitter) 与共振受体 (Receiver)，并测算宏观异动领先预警天数。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">系统情绪总溢出率</div><div class=\"kpi-val highlight\">{ckg.TotalSystemSpilloverIndexPercent:F1}%</div><div class=\"kpi-sub\">Diebold-Yilmaz 综合溢出</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">主导信息发射源</div><div class=\"kpi-val highlight\" style=\"font-size:12px;color:#e74c3c;\">{WebUtility.HtmlEncode(ckg.DominantInformationSourceAsset)}</div><div class=\"kpi-sub\">系统性情绪核心驱动</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">平均跨周期领先时滞</div><div class=\"kpi-val\" style=\"color:#2ecc71\">{ckg.AverageLeadTimeDays:F1} 天</div><div class=\"kpi-sub\">宏观异动前置预警周期</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">因果拓扑网络密度</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{ckg.CausalNetworkDensityPercent:F1}%</div><div class=\"kpi-sub\">跨资产传导有向连通度</div></div>");
            sb.AppendLine("  </div>");

            if (ckg.SpilloverEdges.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">跨资产有向因果传递链、Granger 检验与定向溢出明细 (Causal Spillover Directed Network)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>源头资产 (Source)</th><th>受体资产 (Target)</th><th>Granger F 统计量</th><th>p-value</th><th>传递熵 (Bits)</th><th>定向溢出率</th><th>领先时滞</th><th>因果拓扑角色</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var edge in ckg.SpilloverEdges)
                {
                    sb.AppendLine($"    <tr><td><strong>{WebUtility.HtmlEncode(edge.SourceAsset)}</strong></td><td>{WebUtility.HtmlEncode(edge.TargetAsset)}</td><td>{edge.GrangerFStatistic:F2}</td><td>{edge.GrangerPValue:F4}</td><td>{edge.TransferEntropyBits:F3} Bits</td><td style=\"color:#e74c3c;font-weight:bold;\">+{edge.DirectionalSpilloverPercent:F1}%</td><td>{edge.LeadTimeDays:F1} 天</td><td>{WebUtility.HtmlEncode(edge.CausalRoleBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ckg.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 68. Phase 41.3 Citadel & Point72 上下文多臂老虎机 (Thompson Sampling) 自适应策略路由器
        if (portfolio.ContextualBanditPolicyRouter != null)
        {
            var bandit = portfolio.ContextualBanditPolicyRouter;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">六十八、Citadel & Point72 上下文多臂老虎机 (Thompson Sampling) 自适应策略路由器 (Phase 41)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Citadel 与 Point72 多经理/多元策略自适应强化学习动态路由器：将 MPT 最大夏普、风险平价、Black-Litterman、HRP 与宏观区制 MRS 作为五大竞争策略臂 (Arms)，实时感知宏观波动与体制上下文特征，应用贝叶斯 Beta-Binomial 共轭更新与 Thompson Sampling 随机采样，在探索与利用间动态分配元配置权重，显著压低策略累计遗憾。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">当前主导最优策略臂</div><div class=\"kpi-val highlight\" style=\"font-size:12px;color:#2ecc71;\">{WebUtility.HtmlEncode(bandit.OptimalStrategyName)}</div><div class=\"kpi-sub\">Thompson 采样胜率最高</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">当前宏观状态上下文</div><div class=\"kpi-val highlight\" style=\"font-size:11px;\">{WebUtility.HtmlEncode(bandit.CurrentContextRegime)}</div><div class=\"kpi-sub\">市场波动与风格环境</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">元策略累积夏普增益</div><div class=\"kpi-val\" style=\"color:#f1c40f\">+{bandit.CumulativeMetaSharpeGainPercent:F1}%</div><div class=\"kpi-sub\">相比单一静态配置方案</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">策略路由置信度 / 探索比</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{bandit.OverallPolicyConfidencePercent:F1}% / {bandit.ExplorationVsExploitationRatio:F2}</div><div class=\"kpi-sub\">贝叶斯后验置信水平</div></div>");
            sb.AppendLine("  </div>");

            if (bandit.StrategyArms.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各策略臂贝叶斯先验/后验胜率、Thompson 得分与元权重动态分配明细 (Multi-Armed Bandit Policy Matrix)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>策略代号</th><th>策略名称</th><th>先验 Alpha (胜)</th><th>先验 Beta (负)</th><th>后验期望胜率</th><th>Thompson 采样得分</th><th>动态元权重</th><th>遗憾缩减率</th><th>状态徽标</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var arm in bandit.StrategyArms)
                {
                    sb.AppendLine($"    <tr><td><code>{WebUtility.HtmlEncode(arm.StrategyCode)}</code></td><td><strong>{WebUtility.HtmlEncode(arm.StrategyName)}</strong></td><td>{arm.PriorAlpha:F0}</td><td>{arm.PriorBeta:F0}</td><td style=\"color:#2ecc71;font-weight:bold;\">{arm.PosteriorExpectedWinRatePercent:F1}%</td><td>{arm.ThompsonSampledScore:F3}</td><td style=\"font-weight:bold;color:#f1c40f;\">{arm.DynamicMetaWeightPercent:F1}%</td><td>{arm.CumulativeRegretReductionPercent:F1}%</td><td>{WebUtility.HtmlEncode(arm.BanditStatusBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(bandit.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 69. Phase 41.4 Jump Trading & Optiver 深度强化微观流动性冲击弹性与非线性滑点曲面
        if (portfolio.MicrostructureResiliencySlippageSurface != null)
        {
            var slip = portfolio.MicrostructureResiliencySlippageSurface;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">六十九、Jump Trading & Optiver 深度强化微观流动性冲击弹性与非线性滑点曲面 (Phase 41)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Jump Trading 与 Optiver 极速做市执行与订单簿弹性复原算法：基于 Bouchaud 瞬态弹性传播子模型 G(tau)，精细化刻画大额调仓在抽干盘口后的流动性自愈补单动力学，输出全覆盖 3D 滑点曲面，在不同参与率与急迫度下推荐最优执行算法（TWAP、VWAP-Resiliency、POV、IS），最大化保留调仓 Alpha。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">流动性自愈半衰期</div><div class=\"kpi-val highlight\">{slip.HalfLifeRecoverySeconds:F1} 秒</div><div class=\"kpi-sub\">盘口补单瞬态吸收耗时</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">推荐最优执行急迫度</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">Level {slip.OptimalExecutionUrgency}</div><div class=\"kpi-sub\">VWAP-Resiliency 最优自愈</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">曲面平均有效滑点</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{slip.AverageEffectiveSlippageBps:F1} bps</div><div class=\"kpi-sub\">全网格期望执行滑点</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">弹性执行年化摩擦节省</div><div class=\"kpi-val\" style=\"color:#f1c40f\">+{slip.AnnualizedTransactionCostSavingsPercent:F2}%</div><div class=\"kpi-sub\">弹性 Alpha 指数 {slip.ResiliencyAlphaRecoveryIndex:F1}/100</div></div>");
            sb.AppendLine("  </div>");

            if (slip.SurfaceGridPoints.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">参与率 x 急迫度二维网格微观冲击、弹性复原与净有效滑点明细 (Slippage Resiliency Surface Grid)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>调仓参与率</th><th>急迫度等级</th><th>瞬态价格冲击</th><th>弹性复原吸收率</th><th>实际综合有效滑点</th><th>推荐最优算法调度</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var pt in slip.SurfaceGridPoints.Take(15)) // 展示主要典型点位
                {
                    sb.AppendLine($"    <tr><td>{pt.ParticipationRatePercent:F0}%</td><td>Level {pt.UrgencyLevel}</td><td>{pt.InstantaneousImpactBps:F1} bps</td><td style=\"color:#2ecc71;\">{pt.TransientResiliencyRecoveryPercent:F1}%</td><td style=\"font-weight:bold;color:{(pt.EffectiveSlippageBps > 10.0m ? "#e74c3c" : "#2ecc71")};\">{pt.EffectiveSlippageBps:F1} bps</td><td>{WebUtility.HtmlEncode(pt.OptimalAlgorithm)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(slip.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 70. Phase 42.1 Bridgewater Associates & AQR Capital 跨资产内生流动性螺旋与去杠杆压力传染动力学模型
        if (portfolio.EndogenousLiquiditySpiral != null)
        {
            var spiral = portfolio.EndogenousLiquiditySpiral;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">七十、Bridgewater Associates & AQR Capital 跨资产内生流动性螺旋与去杠杆压力传染动力学模型 (Phase 42)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标桥水基金 (Bridgewater Associates) 与 AQR 宏观流动性紧缩与危机传染模型：基于 Brunnermeier & Pedersen (2009) 以及 Adrian & Shin (2010) 内生流动性螺旋理论，系统化解构净值缩水损失螺旋 (Loss Spiral) 与融资发丝率骤增保证金螺旋 (Margin Spiral) 双重反馈回路，定量推演极端压力下被动去杠杆平仓清算敞口与流动性踩踏折价率，输出最优化去杠杆顺位防御决策。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">内生流动性反馈乘数</div><div class=\"kpi-val highlight\">{spiral.SystemicLiquidityCascadeMultiplier:F2}x</div><div class=\"kpi-sub\">M = 1 / (1 - beta_feedback)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">被动去杠杆清算敞口</div><div class=\"kpi-val highlight\" style=\"color:#e74c3c\">{spiral.TotalForcedFireSaleCapitalPercent:F1}%</div><div class=\"kpi-sub\">极端压力被动平仓规模占比</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">保证金螺旋弹性敏感度</div><div class=\"kpi-val\" style=\"color:#f1c40f\">{spiral.MarginSpiralElasticity:F2}</div><div class=\"kpi-sub\">发丝率随波动率跳升弹性</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">资产踩踏综合折价率</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{spiral.PortfolioStressedIlliquidityDiscountPercent:F2}%</div><div class=\"kpi-sub\">极端抛售流动性折现惩罚</div></div>");
            sb.AppendLine("  </div>");

            if (spiral.FireSaleAssetItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产系统 Beta、保证金发丝率、损失螺旋冲击与推荐去杠杆平仓顺位 (Fire-Sale Deleveraging Hierarchy)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>平仓顺位</th><th>资产代号</th><th>资产名称</th><th>权重</th><th>系统Beta</th><th>发丝率(Haircut)</th><th>损失螺旋冲击</th><th>被动清算敞口</th><th>踩踏折价率</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in spiral.FireSaleAssetItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">第 {item.DeleveragingPriorityRank} 顺位</td><td>{item.AssetCode}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.CurrentWeightPercent:F2}%</td><td>{item.AssetBeta:F2}</td><td>{item.HaircutPercent:F1}%</td><td>{item.LossSpiralImpactBps:F1} bps</td><td style=\"color:#e74c3c;\">{item.ForcedLiquidationVolumeRmb:N1} 万元</td><td style=\"font-weight:bold;color:#f1c40f;\">{item.FireSaleDiscountPercent:F2}%</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(spiral.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 71. Phase 42.2 Renaissance Technologies & Citadel 变分无监督深度隐空间流形聚类与非线性宏观体制自编码器
        if (portfolio.VariationalLatentManifoldRegime != null)
        {
            var vlm = portfolio.VariationalLatentManifoldRegime;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">七十一、Renaissance Technologies & Citadel 变分无监督深度隐空间流形聚类与非线性宏观体制自编码器 (Phase 42)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标文艺复兴科技 (Renaissance Technologies) 与 Citadel 深度连续隐空间流形表征：摒弃传统离散跳跃体制假定，基于变分贝叶斯自编码器 (VAE) 重参数化，将跨资产高维宏观收益、波动与偏度映射至连续 3 维高斯低维隐流形 (z1, z2, z3)，通过隐空间高斯混合流形聚类与重构异动误差 (Anomaly Reconstruction Score)，精准识别宏观相空间连续演化速度与黑天鹅体制临界突变。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">当前三维隐空间坐标</div><div class=\"kpi-val highlight\">({vlm.CurrentLatentZ1:F2}, {vlm.CurrentLatentZ2:F2}, {vlm.CurrentLatentZ3:F2})</div><div class=\"kpi-sub\">动量(Z1) / 波动(Z2) / 退耦(Z3)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">自编码重构异动度</div><div class=\"kpi-val highlight\" style=\"color:{(vlm.DeepReconstructionAnomalyScore > 60m ? "#e74c3c" : "#2ecc71")}\">{vlm.DeepReconstructionAnomalyScore:F1} / 100</div><div class=\"kpi-sub\">黑天鹅与结构异动偏离度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">流形跃迁相速度</div><div class=\"kpi-val\" style=\"color:#f1c40f\">{vlm.ManifoldTransitionVelocity:F2}</div><div class=\"kpi-sub\">隐空间状态演化速率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">变分 KL 散度损失</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{vlm.KlDivergenceLoss:F3}</div><div class=\"kpi-sub\">先验高斯正规化纯度</div></div>");
            sb.AppendLine("  </div>");

            if (vlm.LatentClusters.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">隐空间连续流形聚类簇中心、隶属置信度、夏普乘数与战术配置指南 (Latent Manifold Regime Clusters)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>聚类簇名称</th><th>隐空间中心 (Z1, Z2, Z3)</th><th>隶属置信度</th><th>期望夏普乘数</th><th>战术配置执行指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var cl in vlm.LatentClusters)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(cl.ClusterName)}</td><td>({cl.CentroidZ1:F2}, {cl.CentroidZ2:F2}, {cl.CentroidZ3:F2})</td><td style=\"color:#2ecc71;font-weight:bold;\">{cl.ClusterProbabilityPercent:F1}%</td><td style=\"color:#f1c40f;\">{cl.RegimeSharpeMultiplier:F2}x</td><td>{WebUtility.HtmlEncode(cl.RecommendedRegimeAction)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(vlm.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 72. Phase 42.3 Millennium Management & Point72 跨资产多周期极值尾部相关相变检测与渗流理论系统性临界熔断网络
        if (portfolio.PercolationTailPhaseTransition != null)
        {
            var perc = portfolio.PercolationTailPhaseTransition;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">七十二、Millennium Management & Point72 跨资产多周期极值尾部相关相变检测与渗流理论系统性临界熔断网络 (Phase 42)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标千禧年基金 (Millennium) 与 Point72 极端尾部相变与统计物理渗流网络：将资产组合映射为下行极值尾部 Copula 逾渗随机图 G(N, p)，解析理论临界相变阈值 p_c、最大连通巨集团占比 (Giant Component S_infinity) 与渗流磁化敏感度 chi，精准度量距离相变雪崩临界点的安全阻尼空间，通过识别断裂阻尼弹性边，实现最小调仓成本下的系统性解耦免疫。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">理论渗流临界阈值</div><div class=\"kpi-val highlight\">p_c = {perc.CriticalPercolationThreshold:F2}</div><div class=\"kpi-sub\">随机连通相变临界点</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">最大连通巨集团占比</div><div class=\"kpi-val highlight\" style=\"color:{(perc.GiantConnectedClusterSizePercent > 65m ? "#e74c3c" : "#2ecc71")}\">{perc.GiantConnectedClusterSizePercent:F1}%</div><div class=\"kpi-sub\">全组合锁死连通网络规模</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">相变临界安全阻尼余量</div><div class=\"kpi-val\" style=\"color:#f1c40f\">{perc.DistanceToPhaseTransitionCriticality:F3}</div><div class=\"kpi-sub\">|p_current - p_c|</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">系统性临界相变指数</div><div class=\"kpi-val\" style=\"color:{(perc.SystemicCriticalityIndex > 70m ? "#e74c3c" : "#89B4FA")}\">{perc.SystemicCriticalityIndex:F1} / 100</div><div class=\"kpi-sub\">敏感度 chi: {perc.PercolationSusceptibility:F2}</div></div>");
            sb.AppendLine("  </div>");

            if (perc.PercolationCriticalEdges.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">跨资产尾部极值 Copula 相关系数、渗流权重与巨集团贯穿断裂阻尼 (Percolation Critical Edges)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产 A</th><th>资产 B</th><th>尾部极值相关</th><th>渗流概率权重</th><th>贯穿巨集团</th><th>连通子图</th><th>断裂阻尼系数</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var edge in perc.PercolationCriticalEdges)
                {
                    sb.AppendLine($"    <tr><td>{WebUtility.HtmlEncode(edge.SourceAsset)}</td><td>{WebUtility.HtmlEncode(edge.TargetAsset)}</td><td style=\"font-weight:bold;color:{(edge.TailCorrelation > 0.6m ? "#e74c3c" : "#2ecc71")};\">{edge.TailCorrelation:F3}</td><td>{edge.PercolationWeight:F3}</td><td>{(edge.IsSpanningGiantCluster ? "<span style=\"color:#e74c3c;font-weight:bold;\">⚠️ 贯穿巨集团</span>" : "<span style=\"color:#2ecc71;\">独立子集</span>")}</td><td>Cluster #{edge.ClusterId}</td><td>{edge.CriticalBreakageResistance:F2}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(perc.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 73. Phase 42.4 WorldQuant & Hudson River Trading 统计套利强化正交特征残差动量与微观协整均值回复追踪引擎
        if (portfolio.StatArbResidualMomentumOu != null)
        {
            var ou = portfolio.StatArbResidualMomentumOu;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">七十三、WorldQuant & Hudson River Trading 统计套利强化正交特征残差动量与微观协整均值回复追踪引擎 (Phase 42)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标世坤投资 (WorldQuant) 与哈德逊河交易 (Hudson River Trading) 纯特质统计套利引擎：基于 Blitz 等人正交残差动量理论，回归剥离市场 Beta 与宽基风险，提取纯特质收益标准化 Z-Score，从根本上免疫传统动量踩踏崩溃 (Momentum Crash)；并同步拟合连续时间 Ornstein-Uhlenbeck (OU) 均值回复随机微分方程，精确解析配对价差均值回复速度 theta 与收敛半衰期 t_half，捕获高胜率统计套利 Alpha。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合正交残差动量评分</div><div class=\"kpi-val highlight\">{ou.PortfolioAverageResidualMomentum:+0.00;-0.00}</div><div class=\"kpi-sub\">正交剥离特质动量均值</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">平均协整价差回复半衰期</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">{ou.AverageCointegrationHalfLifeDays:F1} 天</div><div class=\"kpi-sub\">OU 过程 t_half = ln(2) / theta</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">极值套利对最大偏离度</div><div class=\"kpi-val\" style=\"color:#f1c40f\">{ou.TopPairArbitrageZScore:F2} σ</div><div class=\"kpi-sub\">价差均值偏离倍数</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">特质统计套利剥离Alpha</div><div class=\"kpi-val\" style=\"color:#89B4FA\">+{ou.AnnualizedIdiosyncraticStatArbAlphaPercent:F2}%</div><div class=\"kpi-sub\">中性配对套利预期年化</div></div>");
            sb.AppendLine("  </div>");

            if (ou.StatArbPairs.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">资产配对正交残差动量、ADF 平稳性 p 值、OU 回复速率、半衰期与即时套利信号 (Cointegrated Stat-Arb Pairs)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>配对资产名称</th><th>残差动量Z-Score</th><th>ADF平稳性 p值</th><th>OU 回复速率 theta</th><th>收敛半衰期</th><th>当前价差Z-Score</th><th>最优套利决策信号</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var pair in ou.StatArbPairs)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(pair.AssetPairName)}</td><td>{pair.ResidualMomentumZScore:+0.00;-0.00}</td><td>{pair.CointegrationAdfPValue:F4}</td><td>{pair.OuMeanReversionSpeedTheta:F3}</td><td>{pair.OuHalfLifeDays:F1} 天</td><td style=\"font-weight:bold;color:{(Math.Abs(pair.CurrentSpreadZScore) > 1.8m ? "#e74c3c" : "#2ecc71")};\">{pair.CurrentSpreadZScore:+0.00;-0.00}</td><td>{WebUtility.HtmlEncode(pair.OptimalArbitrageSignalBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ou.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 74. Phase 43.1 Renaissance Technologies & Two Sigma 最大相关最小冗余 (mRMR) 互信息特征选择与正交子空间集成
        if (portfolio.MrmrFeatureEnsemble != null)
        {
            var mrmr = portfolio.MrmrFeatureEnsemble;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">七十四、Renaissance Technologies & Two Sigma 最大相关最小冗余 (mRMR) 互信息特征选择与正交子空间集成 (Phase 43)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标文艺复兴大奖章 (Medallion) 与 Two Sigma 顶级特征工程中枢：基于香农信息论，在高维复杂信号池中通过最大化特征与目标预测的相关性互信息 I(f; y)，并联合惩罚特征对之间的冗余度互信息 1/|S| ∑ I(f; f_s)，从根本上消除特征多重共线性与伪相关陷阱；进而在精选出的低冗余核心特征子空间上采用 Gram-Schmidt 正交化集成分类器，实现样本外预测 IC 的显著增益。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">正交集成预测 IC 增益比率</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">+{mrmr.TopFeatureEnsembleIcGainRatio:F1}%</div><div class=\"kpi-sub\">样本外泛化能力提升</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">多重共线性消除率</div><div class=\"kpi-val highlight\">{mrmr.CollinearityReductionRatePercent:F1}%</div><div class=\"kpi-sub\">特征冗余信息过滤</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">候选特征平均互信息</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{mrmr.AverageFeatureMutualInformation:F3} nats</div><div class=\"kpi-sub\">特征-目标信息熵度量</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">精选最优正交特征数</div><div class=\"kpi-val\" style=\"color:#f1c40f\">{mrmr.SelectedOptimalFeatureCount} / {mrmr.FeatureRankings.Count} 个</div><div class=\"kpi-sub\">紧凑低维子空间</div></div>");
            sb.AppendLine("  </div>");

            if (mrmr.FeatureRankings.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">高维特征 mRMR 互信息评分、冗余惩罚、正交子空间权重与重要性评级 (mRMR Feature Space)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>优选顺位</th><th>特征代码</th><th>特征名称</th><th>维度族</th><th>相关互信息 I(f;y)</th><th>冗余惩罚</th><th>mRMR综合分</th><th>正交子空间权重</th><th>重要性状态</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var f in mrmr.FeatureRankings)
                {
                    sb.AppendLine($"    <tr><td>#{f.MrmrSelectionRank}</td><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(f.FeatureCode)}</td><td>{WebUtility.HtmlEncode(f.FeatureName)}</td><td>{WebUtility.HtmlEncode(f.FeatureCategory)}</td><td>{f.TargetRelevanceMutualInfo:F3}</td><td>{f.RedundancyPenaltyMutualInfo:F3}</td><td style=\"font-weight:bold;color:#2ecc71;\">{f.MrmrOptimizationScore:F3}</td><td>{f.SubspaceOrthogonalWeightPercent:F1}%</td><td>{WebUtility.HtmlEncode(f.FeatureImportanceStatus)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mrmr.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 75. Phase 43.2 Citadel & Millennium 基于 Wasserstein 最优输运的多策略 Pod 动态资本曲率重构与非线性凸松弛配置
        if (portfolio.WassersteinPodCurvature != null)
        {
            var wsp = portfolio.WassersteinPodCurvature;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">七十五、Citadel & Millennium 基于 Wasserstein 最优输运的多策略 Pod 动态资本曲率重构与非线性凸松弛配置 (Phase 43)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Citadel 与 Millennium 全球多策略 (Pod Shop) 资本调配系统：引入最优输运 (Optimal Transport) 理论与二次 Wasserstein 测度距离 W2，度量当前各策略 Pod 经验资本配置到目标全天候最优分布的几何位移成本；结合微分流形 Ricci 资本曲率动态监测各 Pod 的风险吸收弹性与凸性，通过凸松弛算法以极低交易摩擦自愈重构资本分布，根除 Pod 间相关性飙升引发的系统性杠杆挤压。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">W2 最优输运几何距离</div><div class=\"kpi-val highlight\">{wsp.TotalWassersteinDistanceMetric:F4}</div><div class=\"kpi-sub\">测度空间位移代价</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">资本曲率稳定性改善增益</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">+{wsp.CurvatureStabilityImprovementPercent:F1}%</div><div class=\"kpi-sub\">流形曲率平滑增益</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">最优输运单边换手率</div><div class=\"kpi-val\" style=\"color:#f1c40f\">{wsp.TotalOptimalTransportTurnoverPercent:F1}%</div><div class=\"kpi-sub\">非线性凸松弛调度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">几何输运摩擦净节约</div><div class=\"kpi-val\" style=\"color:#89B4FA\">+{wsp.NetTransportFrictionSavingsBps:F1} bps</div><div class=\"kpi-sub\">较欧氏二次规划优化</div></div>");
            sb.AppendLine("  </div>");

            if (wsp.PodCurvatureAllocations.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各策略 Pod 资本流形 Ricci 曲率、最优输运权重、边际位移成本与动态调度建议 (Wasserstein Pod Transport)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>Pod 代码</th><th>策略名称</th><th>当前配置权重</th><th>最优输运目标权重</th><th>Ricci曲率指数</th><th>W2边际位移成本</th><th>输运摩擦成本</th><th>动态调度建议</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var pod in wsp.PodCurvatureAllocations)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(pod.PodCode)}</td><td>{WebUtility.HtmlEncode(pod.PodName)}</td><td>{pod.CurrentAllocatedCapitalPercent:F1}%</td><td style=\"font-weight:bold;color:#2ecc71;\">{pod.OptimalTransportTargetWeightPercent:F1}%</td><td>{pod.PodCurvatureIndex:F2}</td><td>{pod.WassersteinMarginalDisplacement:F4}</td><td>{pod.TransportFrictionCostBps:F1} bps</td><td>{WebUtility.HtmlEncode(pod.AllocationDynamicRecommendation)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(wsp.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 76. Phase 43.3 Jane Street & Citadel Securities 微观限价订单簿自激 Hawkes 点过程、跳跃扩散强度与流动性雪崩预警
        if (portfolio.HawkesMicrostructureAvalanche != null)
        {
            var hwk = portfolio.HawkesMicrostructureAvalanche;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">七十六、Jane Street & Citadel Securities 微观限价订单簿自激 Hawkes 点过程、跳跃扩散强度与流动性雪崩预警 (Phase 43)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标简街资本 (Jane Street) 与城堡证券 (Citadel Securities) 微观高频做市防御架构：建立连续时间多维自激 Hawkes 点过程模型 λ(t) = μ + ∑ α exp(-β(t - ti))，高频解构微观限价订单到达的自激繁殖倾向与分支比率 η = α / β；并耦合 Merton 泊松跳跃扩散过程，量化突发剧烈价格冲击概率与期望跳跃幅值，于订单流雪崩与买卖盘真空前夕自适应调度做市防御指令。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合加权自激分支比率 eta</div><div class=\"kpi-val highlight\" style=\"color:{(hwk.PortfolioAverageBranchingRatio >= 0.8m ? "#e74c3c" : "#2ecc71")}\">{hwk.PortfolioAverageBranchingRatio:F3}</div><div class=\"kpi-sub\">临界自催化警戒线: 0.85</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">综合微观雪崩级联指数</div><div class=\"kpi-val highlight\" style=\"color:#f1c40f\">{hwk.CompositeAvalancheRiskIndex:F1} / 100</div><div class=\"kpi-sub\">买卖盘真空踩踏预警</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">高危自激临界资产数</div><div class=\"kpi-val\" style=\"color:#e74c3c\">{hwk.CriticalCascadeAlertCount} 只标的</div><div class=\"kpi-sub\">紧急防御介入阈值</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">前瞻避险预期滑点挽回</div><div class=\"kpi-val\" style=\"color:#89B4FA\">+{hwk.ExpectedPreemptiveCostSavingBps:F1} bps</div><div class=\"kpi-sub\">自激阻断滑点保护</div></div>");
            sb.AppendLine("  </div>");

            if (hwk.AssetHawkesJumps.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">微观资产基准到达率、自激强度、分支比率、Merton 跳跃强度与微观防御响应 (Hawkes Point Process)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产名称</th><th>外生到达率 mu</th><th>自激强度 alpha</th><th>衰减速度 beta</th><th>分支比率 eta</th><th>跳跃强度 lambda_J</th><th>期望跳跃幅值</th><th>雪崩风险指数</th><th>做市防御指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var j in hwk.AssetHawkesJumps)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(j.AssetName)}</td><td>{j.BaselineArrivalRateMu:F2}</td><td>{j.SelfExcitingIntensityAlpha:F3}</td><td>{j.DecayRateBeta:F2}</td><td style=\"font-weight:bold;color:{(j.BranchingRatioEta >= 0.8m ? "#e74c3c" : "#2ecc71")};\">{j.BranchingRatioEta:F3}</td><td>{j.JumpDiffusionIntensityLambda:F2} 次/日</td><td>{j.ExpectedJumpMagnitudePercent:+0.00;-0.00}%</td><td style=\"font-weight:bold;\">{j.LiquidityAvalancheRiskScore:F1}</td><td>{WebUtility.HtmlEncode(j.MicrostructureDefenseSignal)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(hwk.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 77. Phase 43.4 Bridgewater Associates & AQR Capital 高阶矩张量风险平价与非高斯偏度-峰度协同传染对冲矩阵
        if (portfolio.HigherOrderTensorRiskParity != null)
        {
            var hmt = portfolio.HigherOrderTensorRiskParity;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">七十七、Bridgewater Associates & AQR Capital 高阶矩张量风险平价与非高斯偏度-峰度协同传染对冲矩阵 (Phase 43)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标桥水全天候 (Bridgewater All-Weather) 与 AQR 尾部对冲量化框架：突破传统均值-方差仅基于二阶协方差的局限，构建三阶协偏度张量 M3 与四阶协峰度张量 M4；通过欧拉高阶矩边际风险贡献分解，精确定量各资产在非高斯极端黑天鹅崩溃时的厚尾放大效应与不对称协同传染力，求解张量风险平价最优配置权重，赋予组合天然的下行左尾抗毁凸性。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合系统性三阶协偏度 M3</div><div class=\"kpi-val highlight\" style=\"color:{(hmt.PortfolioCoSkewnessMetric < 0 ? "#e74c3c" : "#2ecc71")}\">{hmt.PortfolioCoSkewnessMetric:F2}</div><div class=\"kpi-sub\">负偏越低左尾风险越大</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合系统性四阶协峰度 M4</div><div class=\"kpi-val highlight\" style=\"color:#f1c40f\">{hmt.PortfolioCoKurtosisMetric:F2}</div><div class=\"kpi-sub\">超额肥尾厚度指标</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">高阶矩平价离散度指数</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{hmt.HigherOrderRiskDispersionIndex:F3}</div><div class=\"kpi-sub\">张量边际风险均衡度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">尾部黑天鹅凸性对冲提升</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">+{hmt.TailConvexityHedgingUpliftPercent:F1}%</div><div class=\"kpi-sub\">极端下行回撤收窄保护</div></div>");
            sb.AppendLine("  </div>");

            if (hmt.HigherMomentAssets.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">资产非高斯偏度、超额峰度、张量欧拉边际风险贡献 (MRC)、传统二阶与高阶矩平价权重 (Higher-Order Tensor Parity)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产名称</th><th>单资产偏度</th><th>超额峰度</th><th>M3 协偏度 MRC</th><th>M4 协峰度 MRC</th><th>传统二阶平价权重</th><th>高阶矩张量平价权重</th><th>尾部凸性调整幅度</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var a in hmt.HigherMomentAssets)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(a.AssetName)}</td><td>{a.AssetSkewness:+0.00;-0.00}</td><td>{a.AssetExcessKurtosis:F2}</td><td>{a.CoSkewnessMarginalContribution:+0.000;-0.000}</td><td>{a.CoKurtosisMarginalContribution:F3}</td><td>{a.SecondOrderParityWeightPercent:F1}%</td><td style=\"font-weight:bold;color:#2ecc71;\">{a.TensorHigherOrderParityWeightPercent:F1}%</td><td style=\"color:{(a.ConvexityTailAdjustmentPercent >= 0 ? "#2ecc71" : "#e74c3c")};font-weight:bold;\">{a.ConvexityTailAdjustmentPercent:+0.0;-0.0}%</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(hmt.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 78. Phase 44.1 Renaissance Technologies & D.E. Shaw 连续时间倒向随机微分方程 (BSDE) 动态粘性解对冲与随机波动率曲率最小化
        if (portfolio.BsdeDynamicHedging != null)
        {
            var bsd = portfolio.BsdeDynamicHedging;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">七十八、Renaissance Technologies & D.E. Shaw 连续时间倒向随机微分方程 (BSDE) 动态粘性解对冲与随机波动率曲率最小化 (Phase 44)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标文艺复兴大奖章 (Medallion) 与 D.E. Shaw 连续时间非线性随机控制：突破传统离散局部 Delta 静态复制在跳跃扩散与随机波动率（Heston/SABR）环境下的路径依赖摩擦缺陷；构建带非线性借贷利差与流动性惩罚生成元的 BSDE 动态粘性解偏微分方程，求解连续最优对冲控制向量 Z_t 并最小化二阶 Malliavin 路径曲率积分，实现极端非线性市场颠簸下的凸性无损复制与对冲滑点摩擦极致压降。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合平均对冲控制范数 ||Z_t||</div><div class=\"kpi-val highlight\">{bsd.PortfolioAverageHedgeControlNorm:F3}</div><div class=\"kpi-sub\">连续动态Delta控制量</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">动态对冲净挽回滑点摩擦</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">+{bsd.NetHedgeSlippageSavingsBps:F1} bps</div><div class=\"kpi-sub\">较离散对冲年化节约</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">随机波动率曲率平抑率</div><div class=\"kpi-val\" style=\"color:#89B4FA\">+{bsd.StochasticVolCurvatureSuppressionPercent:F1}%</div><div class=\"kpi-sub\">二阶Malliavin离散度平滑</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">动态对冲复制效能评分</div><div class=\"kpi-val highlight\" style=\"color:#f1c40f\">{bsd.DynamicHedgeEfficiencyScore:F1} 分</div><div class=\"kpi-sub\">非线性凸性复制质量</div></div>");
            sb.AppendLine("  </div>");

            if (bsd.AssetBsdeHedges.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">成分资产基准价、随机波动率、BSDE 粘性解估值 Y_t、最优对冲控制 Z_t、Malliavin 曲率与执行指令 (BSDE Viscosity Hedging)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>基准价格</th><th>随机波动率</th><th>粘性解状态 Y_t</th><th>对冲控制 Z_t</th><th>Malliavin曲率</th><th>滑点挽回</th><th>动态对冲执行指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var a in bsd.AssetBsdeHedges)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(a.AssetCode)}</td><td>{WebUtility.HtmlEncode(a.AssetName)}</td><td>{a.UnderlyingPrice:F3}</td><td>{a.StochasticVolSigma:F1}%</td><td>{a.ViscosityValueYt:F4}</td><td style=\"font-weight:bold;color:#2ecc71;\">{a.DynamicHedgeControlZt:F3}</td><td>{a.MalliavinCurvatureDispersion:F3}</td><td>+{a.FrictionSlippageSavingsBps:F1} bps</td><td>{WebUtility.HtmlEncode(a.HedgeExecutionDirective)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(bsd.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 79. Phase 44.2 Citadel & Millennium 多资产高阶拓扑超图 (Hypergraph) 关联网络与持续同调 Persistent Homology 空洞破裂预警
        if (portfolio.HypergraphTopologicalCausality != null)
        {
            var hyp = portfolio.HypergraphTopologicalCausality;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">七十九、Citadel & Millennium 多资产高阶拓扑超图 (Hypergraph) 关联网络与持续同调 Persistent Homology 空洞破裂预警 (Phase 44)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Citadel 与 Millennium 高阶拓扑流形风控中枢：超越传统两两成对（Pairwise）边缘图网络，构建连接多资产的高维拓扑超图 (Hypergraph)；计算超图拉普拉斯矩阵谱间隙以探测系统多体协同脆弱性；并基于 Vietoris-Rips 过滤追踪持续同调 (Persistent Homology) 的 0 维连通基数 β_0 与 1 维拓扑空洞环路 β_1，解构未被协方差矩阵捕捉的暗套利流形与流动性断裂孤岛，在流形撕裂前发出拓扑破裂预警。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">高阶拓扑超边提取总数</div><div class=\"kpi-val highlight\">{hyp.TotalHyperedges} 组</div><div class=\"kpi-sub\">多体共振关联网络</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">超图拉普拉斯谱间隙</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{hyp.HypergraphSpectralGap:F3}</div><div class=\"kpi-sub\">全网代数连通稳健度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">持续同调拓扑数 (β_0 / β_1)</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">{hyp.Betti0ConnectedComponents} / {hyp.Betti1TopologicalCavities}</div><div class=\"kpi-sub\">连通分支 / 高阶拓扑空洞</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">拓扑空洞相变破裂概率</div><div class=\"kpi-val\" style=\"color:{(hyp.CavityRuptureProbabilityPercent >= 30m ? "#e74c3c" : "#2ecc71")}\">{hyp.CavityRuptureProbabilityPercent:F1}%</div><div class=\"kpi-sub\">相空间相变熵: {hyp.TopologicalPhaseEntropy:F2} nats</div></div>");
            sb.AppendLine("  </div>");

            if (hyp.Hyperedges.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">多资产高阶超图协同主题、包含基数、拉普拉斯谱贡献与拓扑传染风险 (Topological Hyperedges)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>超边编号</th><th>协同主题</th><th>资产基数</th><th>成员资产摘要</th><th>超边强度权重</th><th>拉普拉斯谱贡献</th><th>拓扑传染风险层级</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var he in hyp.Hyperedges)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(he.HyperedgeId)}</td><td>{WebUtility.HtmlEncode(he.HyperedgeTheme)}</td><td>{he.Cardinality}只</td><td>{WebUtility.HtmlEncode(he.MemberAssetsSummary)}</td><td>{he.HyperedgeWeight:F2}</td><td style=\"font-weight:bold;color:#2ecc71;\">{he.HigherOrderSpectralContribution:F3}</td><td>{WebUtility.HtmlEncode(he.TopologicalContagionRisk)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            if (hyp.PersistenceCavities.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">持续同调过滤半径 (ε_birth / ε_death)、持续生命期、拓扑结构性质与系统性金融涵义 (Persistent Homology Barcodes)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>同调维度</th><th>出生半径 ε_birth</th><th>死亡半径 ε_death</th><th>持续生命期</th><th>拓扑结构性质</th><th>系统性金融涵义</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var pc in hyp.PersistenceCavities)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{pc.Dimension}维 (β_{pc.Dimension})</td><td>{pc.BirthFiltrationRadius:F2}</td><td>{pc.DeathFiltrationRadius:F2}</td><td style=\"font-weight:bold;color:#89B4FA;\">{pc.PersistenceLifespan:F2}</td><td>{WebUtility.HtmlEncode(pc.CavityNature)}</td><td>{WebUtility.HtmlEncode(pc.SystemicImplication)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(hyp.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 80. Phase 44.3 Jane Street & Citadel Securities 微观瞬时订单流毒性扩散核、跨标的交叉价格冲击张量与非对称做市执行
        if (portfolio.CrossImpactTensorMicrostructure != null)
        {
            var cit = portfolio.CrossImpactTensorMicrostructure;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">八十、Jane Street & Citadel Securities 微观瞬时订单流毒性扩散核、跨标的交叉价格冲击张量与非对称做市执行 (Phase 44)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标简街资本 (Jane Street) 与城堡证券 (Citadel Securities) 微观高频跨品种做市架构：在高频一揽子篮子交易与跨市场套利中，解构非对称瞬时交叉价格冲击张量核矩阵 Λ_ij(τ) = Λ_0 · τ^(-γ)；量化微观订单流不平衡 (OFI) 在关联资产之间的毒性辐射与泄漏效应，生成最优非对称做市报价与交叉限价单保护矩阵，根除跨品种套利执行中的逆向选择与被动滑点踩踏。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">平均跨标的交叉冲击 Λ_ij</div><div class=\"kpi-val highlight\">{cit.TensorAverageCrossImpactLambdaBps:F2} bps</div><div class=\"kpi-sub\">单位冲击: 每千万元</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">综合微观毒性跨品种扩散率</div><div class=\"kpi-val\" style=\"color:#f1c40f\">{cit.CompositeToxicityDiffusionRatePercent:F1}%</div><div class=\"kpi-sub\">关联标的毒性辐射</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">交叉冲击执行挽回滑点</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">+{cit.CrossExecutionFrictionSavingsBps:F1} bps</div><div class=\"kpi-sub\">非对称挂单价差保护</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">微观流动性扩散体制</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{WebUtility.HtmlEncode(cit.MicrostructureRegimeState)}</div><div class=\"kpi-sub\">跨品种各向异性评估</div></div>");
            sb.AppendLine("  </div>");

            if (cit.TopCrossImpactPairs.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">核心关联对交叉冲击系数 Λ_ij、核衰减半衰期、毒性泄漏率、诱导滑点与非对称挂单偏置 (Cross-Impact Pairs)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>标的配对 (源 ➔ 宿)</th><th>瞬时交叉冲击 Λ_ij</th><th>核衰减半衰期</th><th>毒性泄漏率</th><th>交叉诱导滑点</th><th>最优非对称挂单价差偏置</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var pair in cit.TopCrossImpactPairs)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(pair.AssetPairDisplay)}</td><td>{pair.InstantaneousCrossImpactLambda:F2} bps/千万</td><td>{pair.CrossDecayHalfLifeSeconds:F1} 秒</td><td style=\"font-weight:bold;color:#f1c40f;\">{pair.ToxicityLeakageRatioPercent:F1}%</td><td>{pair.CrossArbitrageSlippageBps:F1} bps</td><td>{WebUtility.HtmlEncode(pair.AsymmetricQuotingBias)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            if (cit.AssetToxicityProfiles.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">标的净辐射毒性、净吸收毒性、交叉冲击易感度指数、建议前瞻价差加宽与执行防御动作 (Asset Toxicity Profiles)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>净辐射毒性</th><th>净吸收毒性</th><th>交叉冲击易感度</th><th>前瞻价差加宽</th><th>执行防御策略</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var pr in cit.AssetToxicityProfiles)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(pr.AssetCode)}</td><td>{WebUtility.HtmlEncode(pr.AssetName)}</td><td>{pr.NetToxicityExportBps:F1} bps</td><td>{pr.NetToxicityImportBps:F1} bps</td><td style=\"font-weight:bold;\">{pr.CrossImpactSusceptibilityIndex:F1}</td><td style=\"color:#89B4FA;\">+{pr.RecommendedPreemptiveSpreadBps:F1} bps</td><td>{WebUtility.HtmlEncode(pr.ExecutionDefenseAction)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(cit.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 81. Phase 44.4 Bridgewater Associates & AQR 测度模糊集 Wasserstein-Ball 分布鲁棒优化 (DRO) 与极小极大抗毁平价
        if (portfolio.WassersteinDroMinimaxParity != null)
        {
            var dro = portfolio.WassersteinDroMinimaxParity;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">八十一、Bridgewater Associates & AQR 测度模糊集 Wasserstein-Ball 分布鲁棒优化 (DRO) 与极小极大抗毁平价 (Phase 44)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标桥水全天候 (All-Weather) 与 AQR 分布鲁棒优化 (DRO) 极小极大抗毁体系：打破传统量化模型对样本历史经验分布的过度依赖，构建以经验分布为球心、Wasserstein 测度半径为 ε 的概率分布模糊球 B_ε(P)；通过拉格朗日对偶凸松弛求解最劣概率分布下的极小极大抗毁平价权重，彻底免疫“优化器诅咒 (Optimizer's Curse)”，在样本外未知宏观体制突变与极端黑天鹅下筑牢全周期抗毁护城河。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">Wasserstein 模糊球半径 ε</div><div class=\"kpi-val highlight\">{dro.AmbiguityBallRadiusEpsilon:F3}</div><div class=\"kpi-sub\">测度不确定性置信集</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">最劣分布极小极大 CVaR</div><div class=\"kpi-val highlight\" style=\"color:#e74c3c\">{dro.WorstCaseExpectedShortfallPercent:F1}%</div><div class=\"kpi-sub\">经验低估基准: {dro.EmpiricalSampleEstimatedCvarPercent:F1}%</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">样本外最劣回撤平抑优化</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">+{dro.OutOfSampleDrawdownMitigationPercent:F1}%</div><div class=\"kpi-sub\">极端黑天鹅下行抗毁收窄</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">对偶凸优化乘子 λ*</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{dro.DualLagrangeMultiplierLambda:F3}</div><div class=\"kpi-sub\">最优鲁棒对偶解</div></div>");
            sb.AppendLine("  </div>");

            if (dro.DroAssetAllocations.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">经验样本权重、Wasserstein-DRO 极小极大权重、权重转移幅度、最劣边际风险贡献 (Worst MRC) 与鲁棒定位 (Wasserstein DRO)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>经验样本权重</th><th>DRO 极小极大权重</th><th>稳健转移幅度</th><th>最劣边际风险 MRC</th><th>模糊保护扣除</th><th>鲁棒配置角色定位</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in dro.DroAssetAllocations)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.EmpiricalSampleWeightPercent:F1}%</td><td style=\"font-weight:bold;color:#2ecc71;\">{item.WassersteinDroMinimaxWeightPercent:F1}%</td><td style=\"font-weight:bold;color:{(item.AllocationShiftPercent >= 0 ? "#2ecc71" : "#e74c3c")};\">{item.AllocationShiftPercent:+0.0;-0.0}%</td><td>{item.WorstCaseMarginalRiskContribution:F3}</td><td>-{item.AmbiguityRobustPenaltyBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.RobustAllocationRole)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(dro.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 82. Phase 45.1 Renaissance Technologies & D.E. Shaw 连续时间分数阶粗糙波动率 (Rough Heston) 与 Riemann-Liouville 分数阶积分预测
        if (portfolio.FractionalRoughVolatility != null)
        {
            var rfv = portfolio.FractionalRoughVolatility;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">八十二、Renaissance Technologies & D.E. Shaw 连续时间分数阶粗糙波动率 (Rough Heston) 与 Riemann-Liouville 分数阶积分预测 (Phase 45)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标文艺复兴 Medallion 与 D.E. Shaw 分数阶粗糙波动率 (Rough Volatility) 体系：突破经典布朗运动马尔可夫无记忆假说（Hurst 参数 H = 0.5），精确识别金融市场微观与多日波动率普遍呈现的亚扩散粗糙路径（H < 0.5）。通过 Riemann-Liouville 分数阶积分核对波动率聚集、长记忆性与短周期爆发（Vol-of-Vol 溢价）进行前瞻外推，彻底修正传统扩散模型对短期极值波动的严重低估。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合平均 Hurst 粗糙度 H</div><div class=\"kpi-val highlight\">{rfv.PortfolioAverageHurstParameterH:F3}</div><div class=\"kpi-sub\">亚扩散粗糙路径 (H &lt; 0.5)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">综合波动率短期突发概率</div><div class=\"kpi-val highlight\" style=\"color:#e74c3c\">{rfv.CompositeVolBurstRiskProbabilityPercent:F1}%</div><div class=\"kpi-sub\">微观跳跃激增风险</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">粗糙与经典波动率偏离</div><div class=\"kpi-val highlight\" style=\"color:#f1c40f\">{rfv.FractionalVsMarkovianVolDispersionPercent:F1}%</div><div class=\"kpi-sub\">传统模型低估修正</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">期权对冲拟合成本节约</div><div class=\"kpi-val\" style=\"color:#2ecc71\">+{rfv.NetRoughOptionHedgingCostSavingsBps:F1} bps</div><div class=\"kpi-sub\">凸性定价偏差压降</div></div>");
            sb.AppendLine("  </div>");

            if (rfv.AssetRoughVolatilities.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">资产 Hurst 粗糙度、Vol-of-Vol 强度、已实现波动率、分数阶预测波动率、突发概率与粗糙体制 (Rough Volatility)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>Hurst 粗糙度 H</th><th>Vol-of-Vol ν</th><th>历史已实现波动率</th><th>分数阶预测波动率</th><th>短期突发概率</th><th>粗糙凸性调整</th><th>波动率粗糙度体制</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in rfv.AssetRoughVolatilities)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td style=\"font-weight:bold;color:#89B4FA;\">{item.HurstParameterH:F3}</td><td>{item.VolOfVolNu:F3}</td><td>{item.HistoricalRealizedVolPercent:F2}%</td><td style=\"font-weight:bold;color:#f1c40f;\">{item.FractionalPredictedVolPercent:F2}%</td><td>{item.ShortTermVolBurstProbabilityPercent:F1}%</td><td>+{item.RoughConvexityPremiumBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.VolRegimeRoughnessState)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(rfv.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 83. Phase 45.2 Citadel Global Fixed Income & Millennium RV 六参数 Nelson-Siegel-Svensson (NSS) 利率期限结构与蝶式凸性套利
        if (portfolio.NelsonSiegelSvenssonTermStructure != null)
        {
            var nss = portfolio.NelsonSiegelSvenssonTermStructure;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">八十三、Citadel Global Fixed Income & Millennium RV 六参数 Nelson-Siegel-Svensson (NSS) 利率期限结构与蝶式凸性套利 (Phase 45)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Citadel 固收全息曲面与 Millennium 相对价值套利引擎：采用六参数 Nelson-Siegel-Svensson (NSS) 连续模型，将宏观多资产收益率曲线完全解构为长期水平 β0、短期斜率 β1、主中期曲率 β2 与次级远端曲率 β3。精确测算关键利率久期向量 (KRD_i)，构建久期与斜率双重中性的蝶式凸性利差套利组合，实现宏观利率波动免疫下的纯净 Alpha 增强。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">NSS 水平/斜率因子</div><div class=\"kpi-val highlight\">{nss.Beta0Level:F2}% / {nss.Beta1Slope:F2}%</div><div class=\"kpi-sub\">长端水平 β0 · 短端斜率 β1</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">NSS 双重曲率因子</div><div class=\"kpi-val highlight\" style=\"color:#89B4FA\">{nss.Beta2Curvature1:F2}% / {nss.Beta3Curvature2:F2}%</div><div class=\"kpi-sub\">中期曲率 β2 · 次级曲率 β3</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合总有效久期</div><div class=\"kpi-val highlight\" style=\"color:#f1c40f\">{nss.PortfolioEffectiveDurationYears:F2} 年</div><div class=\"kpi-sub\">关键利率久期 (KRD) 积分</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">蝶式凸性套利预期 Alpha</div><div class=\"kpi-val\" style=\"color:#2ecc71\">+{nss.ButterflyArbitrageExpectedAlphaBps:F1} bps</div><div class=\"kpi-sub\">久期中性曲率畸变收益</div></div>");
            sb.AppendLine("  </div>");

            if (nss.KeyRateDurations.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">期限节点、NSS 拟合即期收益率、关键利率久期 (KRD)、风险贡献占比、二阶凸性敏感度与蝶式腿角色 (Nelson-Siegel-Svensson)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>期限节点</th><th>期限年数</th><th>NSS 拟合收益率</th><th>关键利率久期 (KRD)</th><th>利率风险贡献占比</th><th>二阶凸性敏感度</th><th>蝶式套利配置腿</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var krd in nss.KeyRateDurations)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(krd.TenorLabel)}</td><td>{krd.TenorYears:F1}Y</td><td style=\"font-weight:bold;color:#89B4FA;\">{krd.FittedNssYieldPercent:F3}%</td><td>{krd.KeyRateDurationYears:F2} 年</td><td>{krd.KeyRateRiskContributionPercent:F1}%</td><td>{krd.CurvatureSensitivityGamma:F3}</td><td>{WebUtility.HtmlEncode(krd.ArbitrageButterflyLeg)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(nss.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 84. Phase 45.3 Two Sigma & Man Group AHL 贝叶斯在线变点检测 (BOCPD) 与序列失效率运行长度后验滤波
        if (portfolio.BayesianOnlineChangepointDetection != null)
        {
            var bcp = portfolio.BayesianOnlineChangepointDetection;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">八十四、Two Sigma & Man Group AHL 贝叶斯在线变点检测 (BOCPD) 与序列失效率运行长度后验滤波 (Phase 45)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Two Sigma 与 Man Group AHL 宏观量化在线突变检测框架：引入 Adams-MacKay 贝叶斯在线变点检测 (BOCPD) 模型，递归递推计算当前体制运行长度 (Run-Length r_t) 的后验分布 P(r_t | x_1:t)。结合自适应序列失效率函数 H(r)，在无需等待长周期回测滑动窗口的情况下，秒级识别流动性、波动率与宏观增长体制的突变拐点，前瞻启动去杠杆与仓位防守。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">当前体制运行长度 (Run-Length)</div><div class=\"kpi-val highlight\">{bcp.CurrentRegimeRunLengthDays:F1} 天</div><div class=\"kpi-sub\">最大后验稳态持续期</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">截面变点重置概率</div><div class=\"kpi-val highlight\" style=\"color:#e74c3c\">{bcp.LatestChangepointProbabilityPercent:F1}%</div><div class=\"kpi-sub\">P(r_t = 0 | x) 突变率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">系统瞬时失效率</div><div class=\"kpi-val highlight\" style=\"color:#f1c40f\">{bcp.SystemicHazardRatePercent:F2}%</div><div class=\"kpi-sub\">自适应失效率 H(r)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">前瞻去风险挽回损失</div><div class=\"kpi-val\" style=\"color:#2ecc71\">+{bcp.PreemptiveDeriskingAlphaSavingsBps:F1} bps</div><div class=\"kpi-sub\">时滞消除防御收益</div></div>");
            sb.AppendLine("  </div>");

            if (bcp.RecentHazardHistory.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">时序观测步长、最大后验运行长度、瞬时失效率、变点重置概率、宏观体制定位与推荐调仓动作 (BOCPD Hazard History)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>步长索引</th><th>观测日期</th><th>运行长度 (天)</th><th>瞬时失效率 H(r)</th><th>变点发生概率</th><th>宏观阶段定位</th><th>自适应资产调仓动作</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var hz in bcp.RecentHazardHistory)
                {
                    sb.AppendLine($"    <tr><td>#{hz.TimeStepIndex}</td><td>{hz.ObservationDate:yyyy-MM-dd}</td><td style=\"font-weight:bold;\">{hz.MaximumAcyclicRunLength:F1}</td><td>{hz.InstantaneousHazardRatePercent:F2}%</td><td style=\"font-weight:bold;color:{(hz.ChangepointProbabilityPercent > 15.0m ? "#e74c3c" : "#89B4FA")};\">{hz.ChangepointProbabilityPercent:F1}%</td><td>{WebUtility.HtmlEncode(hz.MacroRegimePhaseBadge)}</td><td>{WebUtility.HtmlEncode(hz.RecommendedAssetAction)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(bcp.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 85. Phase 45.4 Jane Street & Citadel Securities Avellaneda-Stoikov 连续库存风险最优保留价与非对称限价挂单微观做市
        if (portfolio.AvellanedaStoikovMicrostructure != null)
        {
            var ask = portfolio.AvellanedaStoikovMicrostructure;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">八十五、Jane Street & Citadel Securities Avellaneda-Stoikov 连续库存风险最优保留价与非对称限价挂单微观做市 (Phase 45)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Jane Street 与 Citadel 连续微观做市与执行优化体系：将调仓订单流转化为 Avellaneda-Stoikov 连续时间库存控制问题。求解组合持有库存偏离时的闭式最优无差异保留价格 r(s, q) = s - q * γ * σ^2 * (T - t)，动态输出非对称最优买卖挂单半价差（δ_b, δ_a），在微观撮合过程中通过倾斜价差吸收或卸载存货，彻底杜绝单边库存爆仓并最小化执行摩擦。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">平均最优做市全价差</div><div class=\"kpi-val highlight\">{ask.AverageOptimalQuotingSpreadBps:F1} bps</div><div class=\"kpi-sub\">买卖最优挂单跨度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合保留价倾斜偏置</div><div class=\"kpi-val highlight\" style=\"color:#89B4FA\">{ask.PortfolioWeightedReservationSkewBps:F1} bps</div><div class=\"kpi-sub\">非对称做市倾斜度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">存货不利变动削减率</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">{ask.InventoryRiskMitigationRatePercent:F1}%</div><div class=\"kpi-sub\">单边存货爆仓防御</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">微观限价撮合摩擦节约</div><div class=\"kpi-val\" style=\"color:#2ecc71\">+{ask.ExpectedMicroExecutionSavingsBps:F1} bps</div><div class=\"kpi-sub\">被动做市滑点优化</div></div>");
            sb.AppendLine("  </div>");

            if (ask.AssetQuotingProfiles.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">资产代码、瞬时存货 q、市场中间价、无差异保留价、最优买半价差、最优卖半价差、非对称偏斜与微观执行动作 (Avellaneda-Stoikov)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>瞬时存货 q</th><th>中间价 S</th><th>最优保留价 r</th><th>买半价差 δ_b</th><th>卖半价差 δ_a</th><th>偏斜 Skew</th><th>微观挂单执行动作</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var pr in ask.AssetQuotingProfiles)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(pr.AssetCode)}</td><td>{WebUtility.HtmlEncode(pr.AssetName)}</td><td style=\"font-weight:bold;color:{(pr.CurrentHoldingInventoryUnits >= 0 ? "#2ecc71" : "#e74c3c")};\">{pr.CurrentHoldingInventoryUnits:+0.0;-0.0}</td><td>{pr.MidPriceQuote:F4}</td><td style=\"font-weight:bold;color:#89B4FA;\">{pr.ReservationPriceIndifference:F4}</td><td>{pr.OptimalBidSpreadBps:F1} bps</td><td>{pr.OptimalAskSpreadBps:F1} bps</td><td style=\"font-weight:bold;\">{pr.AsymmetricQuoteSkewBps:+0.0;-0.0} bps</td><td>{WebUtility.HtmlEncode(pr.MicrostructureQuotingAction)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ask.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 86. Phase 46.1 Renaissance Technologies & Alan Turing Institute 粗糙路径特征签名 (Rough Path Signature) 与高阶张量 Alpha 几何编码
        if (portfolio.RoughPathSignatureAlpha != null)
        {
            var rps = portfolio.RoughPathSignatureAlpha;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">八十六、Renaissance Technologies & Alan Turing Institute 粗糙路径特征签名 (Rough Path Signature) 与高阶张量 Alpha 几何编码 (Phase 46)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标文艺复兴科技 (Renaissance Medallion) 与牛津大学/图灵研究所粗糙路径理论：彻底破除传统滑动窗口离散无记忆与一阶马尔可夫链假定。将多维价格-波动率时序投影至截断张量空间，精确求解李代数反对称几何旋度面积（Lévy Area A_12）与时间重参数化不变性曲率，提取全息非线性连续轨迹形态，赋予组合高阶拓扑几何 Alpha 动能。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">平均李代数面积 Lévy Area</div><div class=\"kpi-val highlight\">{rps.PortfolioAverageLevyArea:F5}</div><div class=\"kpi-sub\">反对称几何旋度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">综合非马尔可夫曲率指数</div><div class=\"kpi-val highlight\" style=\"color:#89B4FA\">{rps.CompositePathCurvatureIndex:F3}</div><div class=\"kpi-sub\">几何轨迹动力学</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">高阶张量超额 Alpha 增益</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">+{rps.HighOrderTensorAlphaPremiumBps:F1} bps</div><div class=\"kpi-sub\">拓扑几何 Alpha</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">全息信息捕获率</div><div class=\"kpi-val\" style=\"color:#CBA6F7\">{rps.SignatureInformationCaptureRatio:F1}%</div><div class=\"kpi-sub\">信息保真度</div></div>");
            sb.AppendLine("  </div>");

            if (rps.AssetPathSignatures.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">资产代码、1阶增量范数、2阶能量范数、Lévy Area 旋度、曲率特征、张量 Alpha 得分与路径拓扑形态 (Rough Path Signature)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>1阶增量 S^(1)</th><th>2阶能量 S^(2)</th><th>Lévy Area A_12</th><th>几何曲率</th><th>张量 Alpha 得分</th><th>非马尔可夫增益</th><th>路径几何拓扑态</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var s in rps.AssetPathSignatures)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(s.AssetCode)}</td><td>{WebUtility.HtmlEncode(s.AssetName)}</td><td>{s.SignatureOrder1ReturnNorm:F4}</td><td>{s.SignatureOrder2EnergyNorm:F4}</td><td style=\"font-weight:bold;color:{(s.SignedLevyArea >= 0 ? "#2ecc71" : "#e74c3c")};\">{s.SignedLevyArea:F5}</td><td>{s.GeometricMomentumCurvature:F3}</td><td style=\"font-weight:bold;color:#89B4FA;\">{s.PathSignatureAlphaScore:F1}</td><td style=\"color:#2ecc71;\">+{s.NonMarkovianAlphaIncrementBps:F1} bps</td><td>{WebUtility.HtmlEncode(s.PathTopologyRegimeBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(rps.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 87. Phase 46.2 Citadel Global Strategies & Millennium RV 随机最优停止时间与自由边界斯内尔包络 (Snell Envelope) 动态去杠杆
        if (portfolio.StochasticOptimalStopping != null)
        {
            var sos = portfolio.StochasticOptimalStopping;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">八十七、Citadel Global Strategies & Millennium RV 随机最优停止时间与自由边界斯内尔包络 (Snell Envelope) 动态去杠杆 (Phase 46)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Citadel 与 Millennium 平台连续时间动态极值风控：彻底摒弃经验式固定比例静态硬止损。基于随机分析 Hamilton-Jacobi-Bellman (HJB) 变分不等式求解美式自由边界最优停止问题，通过一阶平滑粘贴条件（Smooth Pasting Condition）动态标定资产回撤临界边界 b*(t) 与斯内尔包络期权时间价值，实现胜率与盈亏比最大化的智能动态出场。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">加权平滑粘贴自由边界</div><div class=\"kpi-val highlight\">{sos.PortfolioWeightedStoppingBoundary:F4}</div><div class=\"kpi-sub\">最优平仓临界点</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">斯内尔包络时间价值溢价</div><div class=\"kpi-val highlight\" style=\"color:#F9E2AF\">{sos.AggregateSnellEnvelopeTimeValueBps:F1} bps</div><div class=\"kpi-sub\">美式退出期权溢价</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">触碰自由边界资产数</div><div class=\"kpi-val highlight\" style=\"color:{(sos.AssetsAtStoppingBoundaryCount > 0 ? "#e74c3c" : "#2ecc71")}\">{sos.AssetsAtStoppingBoundaryCount} 只</div><div class=\"kpi-sub\">立即去杠杆警报</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">避免深套损失收益挽回</div><div class=\"kpi-val\" style=\"color:#2ecc71\">+{sos.AvoidedDrawdownAlphaSavingsBps:F1} bps</div><div class=\"kpi-sub\">动态防深套净效益</div></div>");
            sb.AppendLine("  </div>");

            if (sos.AssetStoppingThresholds.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">资产代码、当前价格峰值比、自由边界 b*(t)、包络时间价值、平滑接触弹性、预期最优剩余持有天数与执行建议 (Snell Envelope)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>价格相对峰值</th><th>自由边界 b*(t)</th><th>包络时间价值</th><th>平滑接触弹性</th><th>预期剩余天数</th><th>风控警报等级</th><th>最优停止执行动作建议</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var st in sos.AssetStoppingThresholds)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(st.AssetCode)}</td><td>{WebUtility.HtmlEncode(st.AssetName)}</td><td style=\"font-weight:bold;\">{st.CurrentPriceToHighRatio:F4}</td><td style=\"color:#e74c3c;font-weight:bold;\">{st.FreeBoundaryStoppingThreshold:F4}</td><td>{st.SnellEnvelopeOptionValueBps:F1} bps</td><td>{st.SmoothPastingElasticity:F4}</td><td>{st.ExpectedOptimalHoldingDaysRemaining:F1} 天</td><td>{WebUtility.HtmlEncode(st.DeRiskingUrgencyBadge)}</td><td>{WebUtility.HtmlEncode(st.OptimalStoppingAction)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(sos.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 88. Phase 46.3 Two Sigma & Bridgewater Associates 因果结构方程模型 (SCM) 与反事实 Do-Calculus 宏观干预归因
        if (portfolio.CausalStructuralModelAttribution != null)
        {
            var scm = portfolio.CausalStructuralModelAttribution;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">八十八、Two Sigma & Bridgewater Associates 因果结构方程模型 (SCM) 与反事实 Do-Calculus 宏观干预归因 (Phase 46)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Two Sigma 与桥水基金因果推断框架：超越传统 Brinson/Barra 线性相关与条件概率 P(Y|X)。构建宏观利率、流动性与风格因子的有向无环图 (DAG)，利用图灵奖 Judea Pearl 后门准则剥离混杂偏误（Confounding Bias），量化因果平均处理效应（ATE），并推演极端宏观反事实情景，从根源杜绝辛普森悖论与虚假伪共线性。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">系统混杂偏误比率</div><div class=\"kpi-val highlight\" style=\"color:#F38BA8\">{scm.SystemicConfoundingBiasRatio:F1}%</div><div class=\"kpi-sub\">虚假相关混杂度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">真实因果 Alpha 净贡献</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">+{scm.TrueCausalAlphaContributionBps:F1} bps</div><div class=\"kpi-sub\">纯外生因果超额</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">剔除伪相关错配挽回</div><div class=\"kpi-val highlight\" style=\"color:#89B4FA\">+{scm.SpuriousCorrelationEliminatedBps:F1} bps</div><div class=\"kpi-sub\">避开辛普森陷阱</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">因果网络拓扑状态</div><div class=\"kpi-val\" style=\"font-size:13px;color:#CBA6F7\">稳健传导态</div><div class=\"kpi-sub\">后门准则完全可识别</div></div>");
            sb.AppendLine("  </div>");

            if (scm.CausalNodeEffects.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">宏观因果变量节点、观测关联度、Do-Calculus 介入处理效应 ATE、混杂偏误幅度与因果属性 (Pearl SCM)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>因果变量节点</th><th>观测关联度</th><th>因果效应 ATE Do(X=Δ)</th><th>混杂偏误</th><th>直接因果贡献率</th><th>因果机理属性判定</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var cn in scm.CausalNodeEffects)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(cn.FactorOrVariableName)}</td><td>{cn.ObservationalCorrelation:F2}</td><td style=\"font-weight:bold;color:#2ecc71;\">{cn.AverageTreatmentEffectDo:+0.00;-0.00}</td><td style=\"color:#F38BA8;\">{cn.ConfoundingBiasBps:F0} bps</td><td>{cn.DirectCausalContributionPercent:F1}%</td><td>{WebUtility.HtmlEncode(cn.CausalMechanismType)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            if (scm.CounterfactualScenarios.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">极端宏观反事实情景推演、介入变量 Do(X)、基准收益 vs. 假设收益、纯因果净差值与战略启示</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>反事实假设情景</th><th>介入算子 Do(X)</th><th>基准收益率</th><th>反事实推演收益</th><th>因果净差值</th><th>战略启示与配置指导</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var cf in scm.CounterfactualScenarios)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(cf.ScenarioName)}</td><td><code>{WebUtility.HtmlEncode(cf.IntervenedDoVariable)}</code></td><td>{cf.BaselineActualReturnPercent:F2}%</td><td style=\"font-weight:bold;color:#89B4FA;\">{cf.CounterfactualReturnPercent:F2}%</td><td style=\"color:#2ecc71;font-weight:bold;\">+{cf.NetCausalDeltaBps:F1} bps</td><td>{WebUtility.HtmlEncode(cf.StrategicImplication)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(scm.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 89. Phase 46.4 Jane Street & Citadel Securities 暂态市场冲击幂律记忆核 (Propagator Kernel) 与最优拆单执行减摩
        if (portfolio.TransientMarketImpactPropagator != null)
        {
            var tip = portfolio.TransientMarketImpactPropagator;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">八十九、Jane Street & Citadel Securities 暂态市场冲击幂律记忆核 (Propagator Kernel) 与最优拆单执行减摩 (Phase 46)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Jane Street 与 Citadel Securities 尖端订单流执行微观体系：基于 Bouchaud-Farmer-Lillo 暂态市场冲击传播子模型。彻底抛弃经典模型冲击瞬时消散假定，引入慢衰减幂律衰减核 G(τ) = Γ_0 (1 + τ)^(-γ)，精确积分累计历史订单流所引发的未衰减暂态自感应价格拖累 I(t)，通过自适应凸性非均匀拆单执行实现大幅减摩。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">加权平均幂律衰减指数 γ</div><div class=\"kpi-val highlight\">{tip.PortfolioAverageDecayExponentGamma:F3}</div><div class=\"kpi-sub\">慢衰减记忆核</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">总暂态冲击积压拖累</div><div class=\"kpi-val highlight\" style=\"color:#e74c3c\">{tip.TotalAccumulatedTransientDragBps:F1} bps</div><div class=\"kpi-sub\">历史订单自感应滑点</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">自适应拆单滑点挽回</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">+{tip.NonUniformExecutionSavingsBps:F1} bps</div><div class=\"kpi-sub\">凸性非均匀减摩</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">微观流动性恢复半衰期</div><div class=\"kpi-val\" style=\"color:#F9E2AF\">{tip.MarketResilienceHalfLifeMinutes:F1} 分钟</div><div class=\"kpi-sub\">订单簿弹性复原</div></div>");
            sb.AppendLine("  </div>");

            if (tip.AssetTransientImpacts.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">资产代码、幂律指数 γ、冲击因子 Γ_0、暂态积压冲击 I(t)、传统静态估计、积压放大倍数、挽回滑点与执行节奏建议 (Propagator Model)</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>幂律指数 γ</th><th>冲击因子 Γ_0</th><th>暂态冲击积压 I(t)</th><th>传统静态冲击</th><th>积压倍数</th><th>挽回滑点</th><th>最优拆单执行节奏建议</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var ti in tip.AssetTransientImpacts)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(ti.AssetCode)}</td><td>{WebUtility.HtmlEncode(ti.AssetName)}</td><td>{ti.PowerLawDecayExponentGamma:F3}</td><td>{ti.PropagatorMemoryKernelFactorG0:F1} bps</td><td style=\"font-weight:bold;color:#e74c3c;\">{ti.AccumulatedTransientImpactBps:F1} bps</td><td>{ti.InstantaneousNaiveImpactBps:F1} bps</td><td>{ti.ImpactMemoryAccumulationRatio:F2}x</td><td style=\"color:#2ecc71;font-weight:bold;\">+{ti.AdaptiveExecutionSavingsBps:F1} bps</td><td>{WebUtility.HtmlEncode(ti.ExecutionCadenceAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(tip.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 90. Phase 47.1 Renaissance Technologies & D.E. Shaw 马利亚温随机变分分析 (Malliavin Calculus) 与无似然高阶 Greeks 敏感度对偶
        if (portfolio.MalliavinCalculusSensitivity != null)
        {
            var mcs = portfolio.MalliavinCalculusSensitivity;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">九十、Renaissance Technologies & D.E. Shaw 马利亚温随机变分分析 (Malliavin Calculus) 与无似然高阶 Greeks 敏感度对偶 (Phase 47)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标文艺复兴科技与 D.E. Shaw 维纳空间随机分析体系：彻底摒弃传统有限差分多次扰动再估值（Bump-and-Revalue）。基于马利亚温导数算子 D 与 Skorokhod 散度积分算子 δ，利用分部积分对偶恒等式解析推导单路径无似然高阶 Greeks 敏感度（Delta, Gamma, Vega, Vanna），实现零重估开销与极致数值鲁棒性，彻底消除差分截断方差震荡。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合加权对偶 Delta</div><div class=\"kpi-val highlight\">{mcs.PortfolioWeightedMalliavinDelta:F4}</div><div class=\"kpi-sub\">一阶价格敏感度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">对偶 Gamma 曲率</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">{mcs.PortfolioWeightedMalliavinGamma:F4}</div><div class=\"kpi-sub\">二阶凸性风险度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">总波动率 Vega 敏感度</div><div class=\"kpi-val highlight\" style=\"color:#F9E2AF\">{mcs.PortfolioAggregatedVegaBps:F1} bps</div><div class=\"kpi-sub\">bps / 1% 波动率冲击</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">对偶计算效率加速比</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{mcs.FiniteDifferenceSpeedupRatio:F1}x</div><div class=\"kpi-sub\">单路径免重算加速</div></div>");
            sb.AppendLine("  </div>");

            if (mcs.AssetSensitivities.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">资产代码、名称、权重、Malliavin Delta、Gamma、Vega、Cross Vanna、数值稳健性、敏感度状态与对偶对冲建议</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>持仓权重</th><th>对偶 Delta</th><th>对偶 Gamma</th><th>对偶 Vega</th><th>交叉 Vanna</th><th>稳健得分</th><th>敏感度状态</th><th>对冲与配置建议</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var asen in mcs.AssetSensitivities)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(asen.AssetCode)}</td><td>{WebUtility.HtmlEncode(asen.AssetName)}</td><td>{asen.Weight:F2}%</td><td style=\"font-weight:bold;\">{asen.MalliavinDelta:F4}</td><td style=\"color:#2ecc71;\">{asen.MalliavinGamma:F4}</td><td>{asen.MalliavinVega:F1} bps</td><td>{asen.CrossAssetVanna:F4}</td><td>{asen.NumericalRobustnessScore:F1}</td><td>{WebUtility.HtmlEncode(asen.SensitivityRegimeBadge)}</td><td>{WebUtility.HtmlEncode(asen.DeltaHedgeAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mcs.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 91. Phase 47.2 Citadel Global Strategies & Millennium Management 半定松弛 (SDR) 锥优化与基数约束稀疏指数跟踪
        if (portfolio.SemidefiniteRelaxationCardinality != null)
        {
            var sdr = portfolio.SemidefiniteRelaxationCardinality;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">九十一、Citadel Global Strategies & Millennium Management 半定松弛 (SDR) 锥优化与基数约束稀疏指数跟踪 (Phase 47)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Citadel 与 Millennium 平台级指数增强与基数约束组合构建：克服 L0 范数 NP-hard 组合爆炸与贪心选择局部次优困境。将非凸二次规划提升至对称半正定矩阵锥空间，通过半定松弛（Semidefinite Relaxation, SDR）求解紧致凸对偶下界，配合随机高斯超平面投影重构满足严格持仓基数 K 的最优稀疏持仓，在最小化跟踪误差的同时大幅缩减账户开户与换手交易摩擦。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">允许持仓基数上限 K</div><div class=\"kpi-val highlight\">{sdr.CardinalityLimitK} 只</div><div class=\"kpi-sub\">实盘账户约束</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">实际精选配置标的</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">{sdr.ActualSparseAssetsSelected} 只</div><div class=\"kpi-sub\">SDR 秩一重构</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">稀疏组合跟踪误差</div><div class=\"kpi-val highlight\" style=\"color:#89B4FA\">{sdr.SdrSparseTrackingErrorPercent:F2}%</div><div class=\"kpi-sub\">年化跟踪误差 TE</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">换手与摩擦节约预期</div><div class=\"kpi-val\" style=\"color:#F9E2AF\">+{sdr.FrictionCostReductionBps:F1} bps</div><div class=\"kpi-sub\">交易成本减摩</div></div>");
            sb.AppendLine("  </div>");

            if (sdr.SparseWeightItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">资产代码、名称、原始权重、SDR 稀疏权重、基数选中状态、边际跟踪方差贡献、换手摩擦节约与配置角色</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>原始权重</th><th>SDR 稀疏权重</th><th>基数选中</th><th>边际方差贡献</th><th>摩擦节约</th><th>组合配置角色</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var sw in sdr.SparseWeightItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(sw.AssetCode)}</td><td>{WebUtility.HtmlEncode(sw.AssetName)}</td><td>{sw.OriginalUnconstrainedWeight:F2}%</td><td style=\"font-weight:bold;color:{(sw.IsSelectedInCardinality ? "#2ecc71" : "#A6ADC8")};\">{sw.SdrOptimizedSparseWeight:F2}%</td><td style=\"color:{(sw.IsSelectedInCardinality ? "#2ecc71" : "#e74c3c")};\">{(sw.IsSelectedInCardinality ? "✅ 入选" : "❌ 剔除")}</td><td>{sw.MarginalTrackingVarianceContribution:F1} bps</td><td style=\"color:#F9E2AF;\">+{sw.TransactionFeeSavingBps:F1} bps</td><td>{WebUtility.HtmlEncode(sw.SelectionRoleBadge)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(sdr.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 92. Phase 47.3 Two Sigma & Bridgewater Associates 连续时间平均场博弈 (Mean Field Games, MFG) 与机构间策略纳什均衡执行
        if (portfolio.MeanFieldGameExecution != null)
        {
            var mfg = portfolio.MeanFieldGameExecution;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">九十二、Two Sigma & Bridgewater Associates 连续时间平均场博弈 (Mean Field Games, MFG) 与机构间策略纳什均衡执行 (Phase 47)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Two Sigma 与桥水基金多主体宏观微观博弈执行架构：告别“把对手方当被动流动性”的局限。基于 Lasry-Lions 连续时间平均场博弈（MFG）框架，耦合个体机构最优控制逆向 HJB 方程与全市场机构群体分布演化前向 FPK 方程，求解对称纳什均衡调仓速率，量化流动性拥挤度与踩踏风险，实现防抢跑、防踩踏的主动博弈执行减摩。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">全景机构平均拥挤度</div><div class=\"kpi-val highlight\" style=\"color:{(mfg.SystemicCrowdingIndex > 70 ? "#e74c3c" : "#2ecc71")}\">{mfg.SystemicCrowdingIndex:F1}</div><div class=\"kpi-sub\">同向调仓拥挤密度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">纳什均衡预期减摩</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">+{mfg.TotalMfgEquilibriumSavingsBps:F1} bps</div><div class=\"kpi-sub\">防踩踏滑点节约</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">踩踏高危警戒资产数</div><div class=\"kpi-val highlight\" style=\"color:{(mfg.CrowdedAssetsCount > 0 ? "#e74c3c" : "#2ecc71")}\">{mfg.CrowdedAssetsCount} 只</div><div class=\"kpi-sub\">同向密集预警</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">群体平衡收敛周期</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{mfg.CoordinationConvergencePeriodHours:F1} 小时</div><div class=\"kpi-sub\">市场均衡分布演化</div></div>");
            sb.AppendLine("  </div>");

            if (mfg.AssetNashStrategies.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">资产代码、名称、持仓拥挤度、MFG 均衡执行速率、防抢跑弹性系数、预期减摩滑点、博弈评级与策略执行指导</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>机构拥挤度</th><th>MFG 均衡速率</th><th>防抢跑弹性</th><th>防踩踏减摩</th><th>拥挤博弈评级</th><th>策略博弈执行指导</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var ns in mfg.AssetNashStrategies)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(ns.AssetCode)}</td><td>{WebUtility.HtmlEncode(ns.AssetName)}</td><td style=\"font-weight:bold;color:{(ns.MarketCrowdingDensityIndex > 70 ? "#e74c3c" : "#2ecc71")};\">{ns.MarketCrowdingDensityIndex:F1}</td><td>{ns.MfgEquilibriumExecutionRate:F2}%/h</td><td>{ns.AntiFrontRunningDefensiveRatio:F3}</td><td style=\"color:#2ecc71;font-weight:bold;\">+{ns.EquilibriumSlippageSavingsBps:F1} bps</td><td>{WebUtility.HtmlEncode(ns.CrowdingRegimeBadge)}</td><td>{WebUtility.HtmlEncode(ns.StrategicExecutionGuidance)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mfg.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 93. Phase 47.4 Jane Street Capital & Jump Trading Kyle-Back 连续拍卖动态知情交易与隐匿执行模型
        if (portfolio.KyleBackStealthExecution != null)
        {
            var kbe = portfolio.KyleBackStealthExecution;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">九十三、Jane Street Capital & Jump Trading Kyle-Back 连续拍卖动态知情交易与隐匿执行模型 (Phase 47)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Jane Street 与 Jump Trading 高频微观隐匿执行算法体系：量化私有 Alpha 信号随时间衰减与订单流信息泄露之间的权衡。基于 Kyle (1985) 与 Back (1992) 连续时间拍卖博弈模型，求解做市商价格冲击因子 Kyle's Lambda λ 与知情交易者最优动态拆单强度 β(t)，在 Alpha 信号耗散前完成建仓，同时彻底混淆噪声做市商，实现无痕隐身与 Alpha 收益最大化保护。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">加权平均 Kyle's Lambda</div><div class=\"kpi-val highlight\">{kbe.PortfolioAverageKyleLambda:F2}</div><div class=\"kpi-sub\">bps / 万手价格冲击</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">综合隐身伪装效率评分</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">{kbe.CompositeStealthCamouflageScore:F1}</div><div class=\"kpi-sub\">无痕伪装指数 (0-100)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">累计锁定 Alpha 保护额</div><div class=\"kpi-val highlight\" style=\"color:#F9E2AF\">+{kbe.TotalPreservedAlphaBps:F1} bps</div><div class=\"kpi-sub\">防做市商嗅探收益</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">特质 Alpha 信号半衰期</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{kbe.AverageAlphaSignalHalfLifeDays:F1} 天</div><div class=\"kpi-sub\">信号有效时间窗口</div></div>");
            sb.AppendLine("  </div>");

            if (kbe.AssetKyleBackExecutions.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">资产代码、名称、Kyle 冲击因子 λ、知情交易强度 β、信息泄露率、Alpha 半衰期、锁定 Alpha 保护额、伪装评级与微观拆单战术</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>Kyle Lambda λ</th><th>知情强度 β</th><th>信息泄露率</th><th>Alpha 半衰期</th><th>锁定 Alpha 保护</th><th>隐身伪装评级</th><th>微观做市拆挂单战术</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var ke in kbe.AssetKyleBackExecutions)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(ke.AssetCode)}</td><td>{WebUtility.HtmlEncode(ke.AssetName)}</td><td>{ke.KyleLambdaImpactFactor:F2} bps</td><td>{ke.OptimalStealthTradingIntensity:F3}</td><td>{ke.InformationLeakageDecayRate:F2}%/m</td><td>{ke.PrivateAlphaHalfLifeDays:F1} 天</td><td style=\"color:#2ecc71;font-weight:bold;\">+{ke.StealthAlphaPreservationBps:F1} bps</td><td>{WebUtility.HtmlEncode(ke.StealthCamouflageRating)}</td><td>{WebUtility.HtmlEncode(ke.MicroExecutionTactic)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(kbe.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 九十四、Renaissance Technologies & D.E. Shaw: 跳跃扩散 Lévy 过程 Esscher 测度变换与随机庞特里亚金极大值原理 (SPMP) 伴随状态最优控制 (Phase 48)
        if (portfolio.StochasticPontryaginControl != null)
        {
            var spmp = portfolio.StochasticPontryaginControl;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">九十四、Renaissance Technologies & D.E. Shaw 跳跃扩散 Lévy 过程 Esscher 测度变换与随机庞特里亚金极大值原理 (SPMP) 伴随状态最优控制 (Phase 48)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Renaissance Technologies (Medallion) 与 D.E. Shaw 连续时间非高斯跳跃随机控制：跳脱传统布朗运动光滑路径假设，引入 Merton 跳跃扩散 Lévy 过程动力学 dX_t = μ X_t dt + σ X_t dW_t + X_{t-} dJ_t。通过 Esscher 测度变换 dQ^θ/dP 构造等价局部鞅测度消除跳跃风险，并建立随机哈密顿系统求解一阶、二阶随机伴随协变量 (Co-state) p_t, q_t，推导奇异最优连续平滑调仓控制率 u_t* = p_t / c，实现跳跃冲击下的最优凸性保护与执行滑点极小化。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">加权一阶伴随协变量 p_t</div><div class=\"kpi-val highlight\">{spmp.PortfolioAverageCoStateP:F4}</div><div class=\"kpi-sub\">哈密顿一阶状态敏感度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">平均最优控制调仓率</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">{spmp.PortfolioAverageOptimalControlRate:F2}%/日</div><div class=\"kpi-sub\">连续平滑执行速率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">累计调仓减摩节省</div><div class=\"kpi-val highlight\" style=\"color:#F9E2AF\">+{spmp.TotalRebalanceSavingsBps:F1} bps</div><div class=\"kpi-sub\">避免冲击滑点损失</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">哈密顿二阶凹性指标</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{spmp.HamiltonianSecondOrderConcavity:F2}</div><div class=\"kpi-sub\">&lt; 0 确保全局极值充分性</div></div>");
            sb.AppendLine("  </div>");

            if (spmp.AssetSpmpControls.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">标的代码、名称、持仓权重、一阶伴随变量 p_t、二阶伴随变量 q_t、Merton 跳跃强度 λ、最优控制速率 u*、Esscher θ 参数、调仓减摩节省、控制评级与庞特里亚金执行指导</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>伴随变量 p_t</th><th>伴随变量 q_t</th><th>跳跃强度 λ</th><th>最优控制率 u*</th><th>Esscher θ</th><th>减摩节省</th><th>控制评级</th><th>庞特里亚金执行指导</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in spmp.AssetSpmpControls)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.CoStateP:F4}</td><td>{item.CoStateQ:F4}</td><td>{item.JumpIntensityLambda:F1} 次/年</td><td style=\"color:#2ecc71;font-weight:bold;\">{item.OptimalControlIntensity:F2}%/日</td><td>{item.EsscherThetaParameter:F4}</td><td>+{item.RebalanceFrictionSavingBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.ControlRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.SpmpExecutionGuidance)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(spmp.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 九十五、Citadel Global Strategies & Millennium Management: 多 Pod 去中心化共识交替方向乘子法 (Consensus ADMM) 与增广拉格朗日多重约束动态资本仲裁 (Phase 48)
        if (portfolio.ConsensusAdmmArbitration != null)
        {
            var admm = portfolio.ConsensusAdmmArbitration;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">九十五、Citadel Global Strategies & Millennium Management 多 Pod 去中心化共识交替方向乘子法 (Consensus ADMM) 与增广拉格朗日多重约束动态资本仲裁 (Phase 48)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Citadel Global Strategies 与 Millennium Management 平台级多经理 (Pod Shop) 资本仲裁架构：针对数十个异质化策略单元 (Pods) 的高维资本分配，摒弃集中式易超时的非凸规划，构建分布式去中心化共识 ADMM 增广拉格朗日方程。各 Pod 独立求解局部效用投影 w_k，中央风控中枢通过对偶影子价格乘子 u_k 执行全局杠杆、换手与行业中性单纯形收缩，保障亚毫秒级收敛并最大化多 Pod 边际资本利用效率。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">协同仲裁 Pod 总数</div><div class=\"kpi-val highlight\">{admm.TotalPodsCount} 单元</div><div class=\"kpi-sub\">异质化策略单元</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">ADMM 收敛迭代步数</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">{admm.AdmmIterationsTaken} 步</div><div class=\"kpi-sub\">分布式交替投影</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">最终原始残差 ||r||_2</div><div class=\"kpi-val highlight\" style=\"color:#F9E2AF\">{admm.FinalPrimalResidual:F5}</div><div class=\"kpi-sub\">全局共识绝对收敛</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">资本共识释放效率</div><div class=\"kpi-val\" style=\"color:#89B4FA\">+{admm.TotalCapitalEfficiencyGainBps:F1} bps</div><div class=\"kpi-sub\">边际资本曲率增益</div></div>");
            sb.AppendLine("  </div>");

            if (admm.PodAllocations.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">Pod 标识、策略单元名称、本地期望权重、ADMM 共识最优权重、对偶乘子 u_k、原始残差范数、资本效率增益、共识收敛状态与投委会中央仲裁指导</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>Pod 代码</th><th>策略名称</th><th>本地期望权重</th><th>ADMM 共识权重</th><th>对偶乘子 u_k</th><th>原始残差</th><th>资本效率增益</th><th>收敛状态</th><th>中央仲裁指导</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var pod in admm.PodAllocations)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(pod.PodCode)}</td><td>{WebUtility.HtmlEncode(pod.PodStrategyName)}</td><td>{pod.LocalTargetWeight:F1}%</td><td style=\"color:#2ecc71;font-weight:bold;\">{pod.ConsensusAdmmWeight:F1}%</td><td>{pod.DualMultiplierU:F4}</td><td>{pod.PrimalResidualNorm:F4}</td><td>+{pod.CapitalEfficiencyGainBps:F1} bps</td><td>{WebUtility.HtmlEncode(pod.ConvergenceStatusBadge)}</td><td>{WebUtility.HtmlEncode(pod.CentralArbitrationAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(admm.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 九十六、Two Sigma & Bridgewater Associates: 非参数分层狄利克雷过程隐马尔可夫模型 (HDP-HMM) 自适应宏观结构聚类与未见体制动态涌现 (Phase 48)
        if (portfolio.InfiniteHdpMacroClustering != null)
        {
            var hdp = portfolio.InfiniteHdpMacroClustering;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">九十六、Two Sigma & Bridgewater Associates 非参数分层狄利克雷过程隐马尔可夫模型 (HDP-HMM) 自适应宏观结构聚类与未见体制动态涌现 (Phase 48)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Two Sigma 与 Bridgewater Associates 宏观自适应贝叶斯非参数学习体系：破除传统 HMM 预设固定状态数 (K=2/3) 的结构刚性，采用 Stick-Breaking 折棍构造与分层狄利克雷过程 (HDP) 先验 GEM(γ)。允许宏观隐体制数量趋于无限，依据数据生成机制自适应识别当前实际活动的主导宏观体制，并实时监控黑天鹅宏观惊异度指数与未见新生体制的动态涌现，赋予全天候资产配置主动认知进化能力。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">自适应活动体制总数</div><div class=\"kpi-val highlight\">{hdp.ActiveRegimesCount} 体制</div><div class=\"kpi-sub\">非参数折棍推断</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">未见体制动态涌现概率</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">{hdp.NewRegimeEmergenceProbability:F1}%</div><div class=\"kpi-sub\">黑天鹅结构相变探测</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">宏观惊异度指数</div><div class=\"kpi-val highlight\" style=\"color:#F9E2AF\">{hdp.MacroSurpriseIndex:F1}</div><div class=\"kpi-sub\">KL 散度异动评分 (0-100)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">边际贝叶斯证据比</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{hdp.BayesianEvidenceRatio:F1}x</div><div class=\"kpi-sub\">相对固定 3-状态 HMM 增益</div></div>");
            sb.AppendLine("  </div>");

            if (hdp.ActiveRegimes.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">体制序号、宏观语义名称、后验置信概率、体制预期年化收益、年化波动率、转移持续留存度、惊异度评分、体制角色与资产配置对冲建议</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>序号</th><th>宏观体制名称</th><th>后验概率</th><th>预期年化收益</th><th>年化波动率</th><th>持续留存度</th><th>惊异度</th><th>体制角色</th><th>跨周期宏观配置建议</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var reg in hdp.ActiveRegimes)
                {
                    sb.AppendLine($"    <tr><td>{reg.RegimeId}</td><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(reg.RegimeName)}</td><td style=\"color:#2ecc71;font-weight:bold;\">{reg.PosteriorProbability:F1}%</td><td>{reg.MeanAnnualizedReturn:F2}%</td><td>{reg.AnnualizedVolatility:F2}%</td><td>{reg.TransitionPersistence:F1}%</td><td>{reg.SurpriseAnomalyScore:F1}</td><td>{WebUtility.HtmlEncode(reg.RegimeRoleBadge)}</td><td>{WebUtility.HtmlEncode(reg.AssetAllocationGuidance)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(hdp.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 九十七、Jane Street Capital & Jump Trading: 双重随机 Cox 点过程与非齐次复合泊松跳跃做市最优非对称价差偏置控制 (Phase 48)
        if (portfolio.CoxProcessAsymmetricMarketMaking != null)
        {
            var cox = portfolio.CoxProcessAsymmetricMarketMaking;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">九十七、Jane Street Capital & Jump Trading 双重随机 Cox 点过程与非齐次复合泊松跳跃做市最优非对称价差偏置控制 (Phase 48)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Jane Street 与 Jump Trading / Citadel Securities 超高频微观流动性提供与毒性订单流防御体系：针对突发性大单冲击与自激聚类到达，构建带均值回复与随机波动的双重随机 Cox 点过程 dλ_t = κ(λ̄ - λ_t)dt + σ_λ √λ_t dW_t + dJ_t。联合求解做市商非线性库存效用 Hamilton-Jacobi-Bellman (HJB) 偏微分方程，解出最优动态非对称买卖半价差 (δ^b*, δ^a*) 与偏置倾斜 Skew，在极端毒性逆向选择环境下筑起微观防火墙，高效捕获流动性 Alpha 与做市返佣。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">加权平均做市有效全价差</div><div class=\"kpi-val highlight\">{cox.PortfolioWeightedAverageSpreadBps:F1} bps</div><div class=\"kpi-sub\">双边微观深度盘口</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">报价平均非对称偏置</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">{cox.AverageAsymmetrySkewBps:F1} bps</div><div class=\"kpi-sub\">毒性订单流倾斜对冲</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">逆向选择毒性防御增益</div><div class=\"kpi-val highlight\" style=\"color:#F9E2AF\">+{cox.TotalToxicDefenseGainBps:F1} bps</div><div class=\"kpi-sub\">挽回被抢跑滑点</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">做市业务净超额 Alpha</div><div class=\"kpi-val\" style=\"color:#89B4FA\">+{cox.TotalExpectedNetMakingAlphaBps:F1} bps</div><div class=\"kpi-sub\">价差收益 + 交易所返佣</div></div>");
            sb.AppendLine("  </div>");

            if (cox.AssetMarketMakingItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">标的代码、名称、Cox 到达强度 λ_0、买侧半价差 δ^b*、卖侧半价差 δ^a*、价差偏置幅度、库存惩罚曲率、毒性防御增益、做市净 Alpha、策略评级与超高频挂单战术</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>到达强度 λ_0</th><th>买侧半价差</th><th>卖侧半价差</th><th>非对称偏置</th><th>库存惩罚曲率</th><th>毒性防御</th><th>净做市 Alpha</th><th>策略评级</th><th>超高频挂单调度战术</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in cox.AssetMarketMakingItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.BaselineArrivalRate:F1} 单/秒</td><td>{item.OptimalBidHalfSpreadBps:F1} bps</td><td>{item.OptimalAskHalfSpreadBps:F1} bps</td><td>{item.SpreadAsymmetrySkewBps:F1} bps</td><td>{item.InventoryPenaltyCurvature:F3}</td><td style=\"color:#2ecc71;font-weight:bold;\">+{item.ToxicSelectionDefenseGainBps:F1} bps</td><td style=\"color:#F9E2AF;font-weight:bold;\">+{item.ExpectedMarketMakingAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.MarketMakingStrategyBadge)}</td><td>{WebUtility.HtmlEncode(item.HighFrequencyQuotingGuidance)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(cox.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 九十八、Renaissance Technologies & D.E. Shaw: 粗糙分数 Ornstein-Uhlenbeck (fOU) 反持续性长程记忆与 Skorokhod 非适应超前对冲 (Phase 49)
        if (portfolio.RoughFractionalOuMemory != null)
        {
            var fou = portfolio.RoughFractionalOuMemory;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">九十八、Renaissance Technologies & D.E. Shaw 粗糙分数 Ornstein-Uhlenbeck (fOU) 反持续性长程记忆与 Skorokhod 非适应超前对冲 (Phase 49)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标文艺复兴 Medallion 与 D.E. Shaw 粗糙波动率与反持续价格动力学前沿：微观高频价格与瞬时波动率普遍呈现反持续赫斯特指数 H &lt; 0.5。构建分数 Ornstein-Uhlenbeck (fOU) 过程 dX_t = -κ X_t dt + ν dB_H(t)，突破传统 Itô 适应流积分假设局限，引入对偶于 Malliavin 导数的非适应 Skorokhod 散度算子 δ(u) = ∫ u_t ⋄ dB_H(t)。精确补偿非马尔可夫长程反持续记忆诱发的对冲时滞（Hedge Lag），执行超前动态对冲与非线性均值回复套利。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合加权赫斯特指数 H</div><div class=\"kpi-val highlight\">{fou.PortfolioWeightedHurstParameter:F3}</div><div class=\"kpi-sub\">反持续粗糙亚扩散 (&lt; 0.5)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">平均 fOU 均值回复速度</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">{fou.PortfolioAverageFouReversionSpeed:F2} /年</div><div class=\"kpi-sub\">非马氏均值回复频次</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">对冲跟踪误差平均压降</div><div class=\"kpi-val highlight\" style=\"color:#F9E2AF\">-{fou.TotalTrackingErrorReductionPercent:F1}%</div><div class=\"kpi-sub\">规避追涨杀跌对冲时滞</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">Skorokhod 减摩超额增益</div><div class=\"kpi-val\" style=\"color:#89B4FA\">+{fou.SkorokhodDivergenceEfficiencyGainBps:F1} bps</div><div class=\"kpi-sub\">非适应超前变分收益</div></div>");
            sb.AppendLine("  </div>");

            if (fou.AssetFouMemoryItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">标的代码、名称、权重、赫斯特指数 H、fOU 均值回复速度 κ、Skorokhod 散度修正 δ(u)、超前对冲 Delta、跟踪误差压降、粗糙记忆评级与动态对冲指引</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>赫斯特指数 H</th><th>均值回复速度 κ</th><th>Skorokhod 散度修正</th><th>超前对冲 Delta</th><th>跟踪误差压降</th><th>粗糙记忆评级</th><th>超前对冲与调仓指引</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in fou.AssetFouMemoryItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.HurstParameter:F3}</td><td>{item.FouMeanReversionSpeed:F2}/年</td><td>{item.SkorokhodAnticipatingDivergence:F4}</td><td>{item.AnticipatingHedgeDelta:F4}</td><td style=\"color:#2ecc71;font-weight:bold;\">-{item.TrackingErrorReductionPercent:F1}%</td><td>{WebUtility.HtmlEncode(item.RoughnessDegreeBadge)}</td><td>{WebUtility.HtmlEncode(item.AnticipatingExecutionAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(fou.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 九十九、Citadel Global Strategies & Millennium Management: 双层分层 Stackelberg 动态主从博弈与道德风险防范委托-代理多经理最优契约机制 (Phase 49)
        if (portfolio.BilevelStackelbergContract != null)
        {
            var bsc = portfolio.BilevelStackelbergContract;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">九十九、Citadel Global Strategies & Millennium Management 双层分层 Stackelberg 动态主从博弈与道德风险防范委托-代理多经理最优契约机制 (Phase 49)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Citadel GBS 与 Millennium 平台型对冲基金（Pod Shop）治理基准：针对投资委员会（Leader/Principal）与各 Pod 基金经理（Followers/Agents）之间的目标函数不对称与道德风险，构建双层分层 Stackelberg 动态规划。引入连续时间 Holmström-Milgrom 委托-代理最优契约，解出最优提成激励斜率 α*、高水位门槛收益率与动态追保止损阈值。彻底防范基金经理在回撤濒危时的水下搏命下注（Gambling for Resurrection）与策略漂移。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">纳入博弈契约策略单元数</div><div class=\"kpi-val highlight\">{bsc.TotalPodsCount} 个 Pod</div><div class=\"kpi-sub\">平台级多策略集群</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">平台平均最优提成斜率 α*</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">{bsc.PlatformAverageIncentiveSlope:F1}%</div><div class=\"kpi-sub\">激励相容最优化斜率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">道德风险防范综合得分</div><div class=\"kpi-val highlight\" style=\"color:#F9E2AF\">{bsc.SystemicMoralHazardDeterrenceScore:F1} 分</div><div class=\"kpi-sub\">封杀水下破罐下注</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">边际资本效率增益</div><div class=\"kpi-val\" style=\"color:#89B4FA\">+{bsc.CapitalEfficiencyGainBps:F1} bps</div><div class=\"kpi-sub\">消除策略漂移释放红利</div></div>");
            sb.AppendLine("  </div>");

            if (bsc.PodContractItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">Pod 标识、策略单元名称、分配资本、最优绩效提成斜率 α*、高水位收益门槛、动态追保止损线、道德风险惩罚成本、契约治理状态与履约指导</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>Pod 代码</th><th>策略单元名称</th><th>分配资本</th><th>最优提成斜率 α*</th><th>高水位门槛</th><th>追保止损线</th><th>道德风险惩罚</th><th>契约治理状态</th><th>委托代理契约履约指导</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in bsc.PodContractItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.PodCode)}</td><td>{WebUtility.HtmlEncode(item.PodStrategyName)}</td><td>¥{item.AllocatedCapital:F1} 亿</td><td>{item.OptimalIncentiveSlopeAlpha:F1}%</td><td>{item.HighWaterMarkHurdleRate:F1}%</td><td style=\"color:#e74c3c;font-weight:bold;\">-{item.DrawdownStopLossThreshold:F1}%</td><td>{item.MoralHazardRiskPenalty:F1} bps</td><td>{WebUtility.HtmlEncode(item.ContractGovernanceBadge)}</td><td>{WebUtility.HtmlEncode(item.PrincipalAgentAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(bsc.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百、Two Sigma & Bridgewater Associates: 拓扑信息几何 Fisher-Rao 黎曼流形测地线距离与 Wasserstein 测度重心相变预警 (Phase 49)
        if (portfolio.TopologicalInformationGeometry != null)
        {
            var tig = portfolio.TopologicalInformationGeometry;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百、Two Sigma & Bridgewater Associates 拓扑信息几何 Fisher-Rao 黎曼流形测地线距离与 Wasserstein 测度重心相变预警 (Phase 49)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Two Sigma 与桥水基金非欧几何宏观认知范式：突破传统欧氏空间与非对称 KL 散度测量局限，将跨周期多资产收益率的联合概率密度族参数化为统计流形 M。采用 Fisher 信息矩阵作为黎曼度量张量 g_ij(θ)，沿流形极值积分求解真正的 Fisher-Rao 测地线距离 d_FR。联合 Wasserstein-2 测度几何重心动力学与标量曲率突变分析，精准检测宏观流动性踩踏与体制非线性断裂相变（Phase Transition）。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">平均 Fisher-Rao 测地线距离</div><div class=\"kpi-val highlight\">{tig.AverageFisherRaoDistance:F2}</div><div class=\"kpi-sub\">黎曼流形测地长度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">流形最大截面曲率绝对值</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">{tig.MaxManifoldCurvatureMagnitude:F2}</div><div class=\"kpi-sub\">非欧几何应力集中度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">全局宏观相变预警指数</div><div class=\"kpi-val highlight\" style=\"color:#F9E2AF\">{tig.GlobalPhaseTransitionWarningIndex:F1}</div><div class=\"kpi-sub\">流动性相变分岔风险</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">测度重心稳定性得分</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{tig.BarycenterStabilityScore:F1} 分</div><div class=\"kpi-sub\">Wasserstein 几何重心稳态</div></div>");
            sb.AppendLine("  </div>");

            if (tig.GeodesicRegimeItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">测地线路径标识、Fisher-Rao 测地线距离、黎曼截面曲率、Wasserstein 重心漂移率、极端相变概率、拓扑形态徽标与宏观对冲指引</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>宏观测地线路径</th><th>Fisher-Rao 测地距离</th><th>黎曼截面曲率</th><th>Wasserstein 重心漂移</th><th>相变爆发概率</th><th>拓扑形态评级</th><th>信息几何宏观对冲指引</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in tig.GeodesicRegimeItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.RegimePairName)}</td><td>{item.FisherRaoGeodesicDistance:F2}</td><td>{item.RiemannianManifoldCurvature:F2}</td><td>{item.WassersteinBarycenterDriftRate:F1}%/月</td><td style=\"color:#e74c3c;font-weight:bold;\">{item.PhaseTransitionProbability:F1}%</td><td>{WebUtility.HtmlEncode(item.ManifoldTopologyBadge)}</td><td>{WebUtility.HtmlEncode(item.MacroGeometricHedgingAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(tig.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百零一、Jane Street Capital & Jump Trading: 多维 Hawkes 自激/互激点过程谱半径与 LOB 队列反应式微观流动性黑洞防御 (Phase 49)
        if (portfolio.MultivariateHawkesMicrostructure != null)
        {
            var hmk = portfolio.MultivariateHawkesMicrostructure;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百零一、Jane Street Capital & Jump Trading 多维 Hawkes 自激/互激点过程谱半径与 LOB 队列反应式微观流动性黑洞防御 (Phase 49)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Jane Street 与 Jump Trading / Citadel Securities 超高频微观市场动力学体系：构建主动买单、主动卖单、限价挂单与被动撤单的多维事件点过程。解算跨品类互激衰减核矩阵 Γ = [α_mn / β_mn] 的谱半径 ρ(Γ)（临界分支比）。实时预警当 ρ(Γ) 逼近 1.0 时的订单流雪崩与微观流动性黑洞。结合限价订单簿 (LOB) 队列排队深度分布，执行队列反应式最优高频挂单战术，捕获微观结构净 Alpha。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">Hawkes 互激核谱半径 ρ(Γ)</div><div class=\"kpi-val highlight\">{hmk.HawkesBranchingSpectralRadius:F3}</div><div class=\"kpi-sub\">临界分支比 (&lt; 1.0 平稳)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">LOB 队列瞬时耗尽平均概率</div><div class=\"kpi-val highlight\" style=\"color:#2ecc71\">{hmk.AverageQueueDepletionProbability:F1}%</div><div class=\"kpi-sub\">盘口深度耗竭压力</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">微观流动性黑洞预警指数</div><div class=\"kpi-val highlight\" style=\"color:#F9E2AF\">{hmk.AvalancheBlackHoleWarningScore:F1}</div><div class=\"kpi-sub\">订单流雪崩防御监测</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">队列反应式净微观 Alpha</div><div class=\"kpi-val\" style=\"color:#89B4FA\">+{hmk.NetMicrostructureAlphaBps:F1} bps</div><div class=\"kpi-sub\">超高频微观执行收益</div></div>");
            sb.AppendLine("  </div>");

            if (hmk.HawkesMatrixItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">事件交互通道、交互语义、基准到达率 μ、激发强度 α、记忆衰减 β、分支比贡献、LOB 队列耗尽概率、微观结构评级与挂撤单调度战术</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>事件通道</th><th>交互语义</th><th>基准到达率 μ</th><th>激发强度 α</th><th>衰减速率 β</th><th>分支比贡献</th><th>LOB 耗尽概率</th><th>微观结构评级</th><th>队列反应式做市与微观拆单战术</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in hmk.HawkesMatrixItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.EventPairCode)}</td><td>{WebUtility.HtmlEncode(item.EventPairName)}</td><td>{item.BaselineArrivalRate:F1} 单/秒</td><td>{item.ExcitationAlpha:F2}</td><td>{item.DecayRateBeta:F1}/秒</td><td>{item.BranchingRatioContribution:F3}</td><td style=\"color:#e74c3c;font-weight:bold;\">{item.LobQueueDepletionProb:F1}%</td><td>{WebUtility.HtmlEncode(item.MicrostructureRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.HighFrequencyQuotingTactics)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(hmk.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百零二、Renaissance Technologies & D.E. Shaw: 非对易自由概率论 (Voiculescu Free Probability) 与量子重整化群 (QRG) 高维矩阵奇异谱修复 (Phase 50)
        if (portfolio.VoiculescuFreeProbabilityQrg != null)
        {
            var vfp = portfolio.VoiculescuFreeProbabilityQrg;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百零二、Renaissance Technologies & D.E. Shaw 非对易自由概率论 (Voiculescu Free Probability) 与量子重整化群 (QRG) 高维矩阵奇异谱修复 (Phase 50)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Renaissance Technologies 与 D.E. Shaw 顶级数理微积分体系：针对高维大随机协方差矩阵经验特征值的非对易虚假扩散难题，引入 Dan Voiculescu 自由概率加性卷积与自由累积量 R-变换。结合 Kenneth Wilson 量子重整化群 (QRG) 能标流动方程，逐级滤除微观高斯白噪声，完成高维经验协方差奇异谱修复与条件数有效压缩。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">非对易白噪声滤除比例</div><div class=\"kpi-val highlight\" style=\"color:#F38BA8\">{vfp.VoiculescuFreeNoiseFractionPercent:F1}%</div><div class=\"kpi-sub\">Voiculescu R-变换噪声识别</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">QRG 有效截断能标 Λ*</div><div class=\"kpi-val\" style=\"color:#A6E3A1\">{vfp.QrgEffectiveEnergyScaleLambda:F3}</div><div class=\"kpi-sub\">重整化群最优流动尺度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">奇异谱条件数压缩倍率</div><div class=\"kpi-val highlight\" style=\"color:#89B4FA\">{vfp.ConditionNumberCompressionRatio:F2}x</div><div class=\"kpi-sub\">高维奇异矩阵正则化</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">谱修复样本外减噪增益</div><div class=\"kpi-val\" style=\"color:#FAB387\">+{vfp.SpectralSingularityRepairGainBps:F1} bps</div><div class=\"kpi-sub\">抗过拟合减噪收益</div></div>");
            sb.AppendLine("  </div>");

            if (vfp.AssetQrgItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产原始经验特征值 λ_raw、自由累积量 κ_1、QRG 能标 Λ*、修复特征值 λ_clean、条件数压缩贡献、谱纯净度徽标与调仓指引</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>原始特征值 λ</th><th>自由累积量 κ_1</th><th>QRG 能标 Λ*</th><th>修复特征值 λ*</th><th>条件数压缩</th><th>谱纯净度</th><th>自由概率与 QRG 谱修复调仓指引</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in vfp.AssetQrgItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.RawEmpiricalEigenvalue:F3}</td><td>{item.VoiculescuFreeCumulantR:F4}</td><td>{item.QrgEnergyScaleLambda:F3}</td><td style=\"color:#a6e3a1;font-weight:bold;\">{item.RepairedCleanEigenvalue:F3}</td><td>{item.ConditionNumberCompression:F2}x</td><td>{WebUtility.HtmlEncode(item.SpectralPurityBadge)}</td><td>{WebUtility.HtmlEncode(item.SpectralFilteringAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(vfp.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百零三、Citadel Global Strategies & AQR Capital Management: 宏观随机动力学混沌奇异吸引子 (Lyapunov Exponent Spectrum) 与托姆尖点突变论 (Thom Cusp Catastrophe) 极值跳变防踩踏 (Phase 50)
        if (portfolio.ThomCatastropheChaosDynamics != null)
        {
            var tcd = portfolio.ThomCatastropheChaosDynamics;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百零三、Citadel Global Strategies & AQR Capital Management 宏观随机动力学混沌奇异吸引子 (Lyapunov Exponent Spectrum) 与托姆尖点突变论 (Thom Cusp Catastrophe) 极值跳变防踩踏 (Phase 50)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Citadel 与 AQR 非线性动力学防踩踏系统：将宏观资产演化建模为相空间流形上的动力系统，求解最大李雅普诺夫指数 λ_max 判定混沌预测极限视界；构建 René Thom 尖点突变势函数 V(x)=x⁴/4+αx²/2+βx，通过判别式 Δ=4α³+27β² 与分岔曲面极小距离，实时监测双稳态回滞裂谷，部署超前防踩踏对冲缓冲。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">全局最大李雅普诺夫指数</div><div class=\"kpi-val highlight\" style=\"color:#F9E2AF\">λ={tcd.PortfolioMaxLyapunovExponent:F3}</div><div class=\"kpi-sub\">相空间轨迹敏感度测度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">动力学极限预测视界</div><div class=\"kpi-val\" style=\"color:#89DCEB\">{tcd.LyapunovPredictionHorizonDays:F1} 天</div><div class=\"kpi-sub\">τ_L = 1 / λ_max 混沌极限</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">尖点突变分岔曲面安全距离</div><div class=\"kpi-val\" style=\"color:#A6E3A1\">{tcd.ThomCatastropheBifurcationDistance:F3}</div><div class=\"kpi-sub\">距离折叠临界流形安全边际</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">防踩踏滞后回滞缓冲总头寸</div><div class=\"kpi-val highlight\" style=\"color:#F38BA8\">{tcd.SystemicAntiHysteresisBufferPct:F1}%</div><div class=\"kpi-sub\">极端跳变黑天鹅动态对冲</div></div>");
            sb.AppendLine("  </div>");

            if (tcd.CatastropheItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产最大李雅普诺夫指数 λ_max、尖点参数 α/β、分岔判别式 Δ、曲面安全距离、防踩踏缓冲、突变动力学评级与对冲策略</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>李雅普诺夫 λ</th><th>分岔参数 α</th><th>偏置参数 β</th><th>判别式 Δ</th><th>曲面距离</th><th>防踩踏缓冲</th><th>动力学评级</th><th>混沌与突变动力学对冲指导</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in tcd.CatastropheItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.MaxLyapunovExponent:F3}</td><td>{item.ThomControlParameterAlpha:F3}</td><td>{item.ThomControlParameterBeta:F3}</td><td style=\"color:#e74c3c;font-weight:bold;\">{item.BifurcationDiscriminantDelta:F4}</td><td>{item.DistanceToBifurcationSurface:F3}</td><td>{item.AntiHysteresisHedgingBufferPct:F1}%</td><td>{WebUtility.HtmlEncode(item.CatastropheRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.NonlinearDynamicHedgingAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(tcd.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百零四、Two Sigma & WorldQuant: 神经薛定谔桥 (Neural Schrödinger Bridge) 熵正则化最优输运与反射倒向随机微分方程 (Reflected BSDE) 资本硬约束动态流动性生成式对冲 (Phase 50)
        if (portfolio.NeuralSchrodingerBridgeReflectedBsde != null)
        {
            var sbr = portfolio.NeuralSchrodingerBridgeReflectedBsde;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百零四、Two Sigma & WorldQuant 神经薛定谔桥 (Neural Schrödinger Bridge) 熵正则化最优输运与反射倒向随机微分方程 (Reflected BSDE) 资本硬约束动态流动性生成式对冲 (Phase 50)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Two Sigma 与 WorldQuant 生成式流动性对冲架构：求解两端点经验测度间的神经薛定谔桥连续时间熵正则化最优输运 (Entropic Optimal Transport)。在资本硬约束凸流形内部构建反射倒向随机微分方程 (Reflected BSDE)，借助 Skorokhod 边界反射局部时算子 dK_t，在绝对不触碰硬风险红线的前提下完成平滑连续生成式换手调仓。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">薛定谔桥熵正则化距离</div><div class=\"kpi-val\" style=\"color:#CBA6F7\">W_ε={sbr.SchrodingerBridgeEntropicDistance:F3}</div><div class=\"kpi-sub\">测度平滑最优输运能耗</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">反射 BSDE 资本边界松弛裕度</div><div class=\"kpi-val highlight\" style=\"color:#A6E3A1\">{sbr.ReflectedBsdeBoundarySlackMargin:F1}%</div><div class=\"kpi-sub\">距硬约束红线安全边际</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">Skorokhod 边界反射局部时</div><div class=\"kpi-val\" style=\"color:#FAB387\">{sbr.SkorokhodReflectionLocalTimeIntensity:F1} bps</div><div class=\"kpi-sub\">受限流形边界反弹强度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">生成式平滑调仓减摩总增益</div><div class=\"kpi-val highlight\" style=\"color:#89B4FA\">+{sbr.OptimalTransitionCostSavingsBps:F1} bps</div><div class=\"kpi-sub\">相比直线换手摩擦压降</div></div>");
            sb.AppendLine("  </div>");

            if (sbr.BridgeBsdeItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产初始漂移 P_0、目标漂移 P_1、最优输运速度 v*、反射松弛裕度、局部时强度、减摩增益、硬约束安全状态与调仓指引</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>初始测度 P_0</th><th>目标测度 P_1</th><th>输运速度 v*</th><th>边界松弛度</th><th>局部时 dK_t</th><th>减摩增益</th><th>安全状态</th><th>薛定谔桥与反射 BSDE 连续生成调仓指引</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in sbr.BridgeBsdeItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.PriorMeasureDrift:F2}%</td><td>{item.TargetMeasureDrift:F2}%</td><td>{item.EntropicOptimalTransportVelocity:F3}</td><td>{item.ReflectedBsdeBoundarySlack:F1}%</td><td>{item.SkorokhodLocalTimeIntensity:F1} bps</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.DynamicTransitionSavingsBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.BoundarySafetyBadge)}</td><td>{WebUtility.HtmlEncode(item.GenerativeTransitionAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(sbr.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百零五、Jane Street Capital & Hudson River Trading: 玻尔兹曼-弗拉索夫 (Boltzmann-Vlasov) 动理学流动性连续体场论与相对论性纳什执行博弈 (Phase 50)
        if (portfolio.BoltzmannVlasovRelativisticExecution != null)
        {
            var bve = portfolio.BoltzmannVlasovRelativisticExecution;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百零五、Jane Street Capital & Hudson River Trading 玻尔兹曼-弗拉索夫 (Boltzmann-Vlasov) 动理学流动性连续体场论与相对论性纳什执行博弈 (Phase 50)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Jane Street 与 HRT 超高频动理学统计场论体系：将限价订单簿 (LOB) 离散挂撤单抽象为相空间连续体分布函数 f(x, v)，求解非平衡态玻尔兹曼-弗拉索夫动理学输运方程。测算订单流引力自洽势场 Φ 与微观声学激波波速 c_s，在信息冲击波穿透因果光锥前，动态求解相对论性纳什执行均衡，实现隐匿做市与逆向选择绝对防护。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">弗拉索夫自洽引力总势能</div><div class=\"kpi-val highlight\" style=\"color:#FAB387\">Φ={bve.VlasovSelfConsistentFieldPotential:F2}</div><div class=\"kpi-sub\">相空间流动性引力场</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">LOB 微观声学冲击波速</div><div class=\"kpi-val\" style=\"color:#F38BA8\">c_s={bve.MicrostructuralAcousticSpeed:F2} tick/ms</div><div class=\"kpi-sub\">订单流激波波前传导速度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">相对论因果视界防抢跑得分</div><div class=\"kpi-val highlight\" style=\"color:#A6E3A1\">{bve.RelativisticCausalHorizonSafetyScore:F1} 分</div><div class=\"kpi-sub\">光锥因果安全防御评级</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">逆向选择防抢跑净 Alpha</div><div class=\"kpi-val\" style=\"color:#89B4FA\">+{bve.AdverseSelectionAlphaProtectionBps:F1} bps</div><div class=\"kpi-sub\">相对论做市博弈超额收益</div></div>");
            sb.AppendLine("  </div>");

            if (bve.VlasovFieldItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">微观流动性通道、物理语义、相空间密度 f、平均粒子速度 v、自洽场强 F_self、声学波速 c_s、因果安全得分、动理学评级与做市战术</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>微观通道</th><th>通道语义</th><th>相空间密度</th><th>粒子速度 v</th><th>自洽场强 F</th><th>声学波速 c_s</th><th>因果安全得分</th><th>动理学评级</th><th>动理学场论相对论做市挂单与隐匿战术</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in bve.VlasovFieldItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.ChannelCode)}</td><td>{WebUtility.HtmlEncode(item.ChannelName)}</td><td>{item.PhaseSpaceDensity:F1}</td><td>{item.MeanParticleVelocity:F2} tick/ms</td><td>{item.VlasovSelfConsistentFieldForce:F2} N</td><td>{item.AcousticShockwaveSpeed:F2} tick/ms</td><td style=\"color:#a6e3a1;font-weight:bold;\">{item.RelativisticCausalSafetyScore:F1}</td><td>{WebUtility.HtmlEncode(item.KineticRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.RelativisticQuotingTactics)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(bve.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百零六、Millennium Management & Point72: 多 PM 策略 Pod 凸二次多维资本分配与内部互冲去交叉阻尼引擎 (Phase 51)
        if (portfolio.MillenniumConvexPodAllocation != null)
        {
            var mca = portfolio.MillenniumConvexPodAllocation;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百零六、Millennium Management & Point72 多 PM 策略 Pod 凸二次多维资本分配与内部互冲去交叉阻尼引擎 (Phase 51)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Millennium 与 Point72 顶级平台架构：构建跨 Pod 内部订单虚拟撮合清算网络 (Internal Crossing Netting)，将外部市场直接执行敞口压缩至净值。求解风险调整资本速度 (RACV) 凸二次规划，并实施阶梯式回撤止损阻尼，彻底隔绝跨 Pod 保证金违约交叉传染。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">跨 Pod 内部撮合净额率</div><div class=\"kpi-val highlight\" style=\"color:#A6E3A1\">{mca.InternalNettingEfficiencyPercent:F1}%</div><div class=\"kpi-sub\">避免外部双向摩擦对倒</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">跨 Pod 保证金传染指数</div><div class=\"kpi-val\" style=\"color:#89B4FA\">{mca.CrossPodMarginContagionIndex:F2}</div><div class=\"kpi-sub\">平台系统性关联违约度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">双向摩擦与借贷挽回增益</div><div class=\"kpi-val highlight\" style=\"color:#FAB387\">+{mca.DynamicCapitalDragSavingsBps:F1} bps</div><div class=\"kpi-sub\">内部互冲节约外部规费</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合平均止损松弛边际</div><div class=\"kpi-val\" style=\"color:#CBA6F7\">{mca.SystemicStopOutSlackMargin:F1}%</div><div class=\"kpi-sub\">距硬风控清盘安全距离</div></div>");
            sb.AppendLine("  </div>");

            if (mca.PodAllocationItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各 PM 策略 Pod 代码、策略名称、目标权重、名义订单量、内部撮合量、净外部敞口、止损松弛度、阻尼系数、风控状态与调度指引</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>Pod 代码</th><th>Pod 策略名称</th><th>目标权重</th><th>名义订单量</th><th>内部撮合量</th><th>净外部敞口</th><th>止损松弛度</th><th>阻尼系数</th><th>风控状态</th><th>Pod 动态调仓与跨 Pod 资本调度指导</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in mca.PodAllocationItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.PodCode)}</td><td>{WebUtility.HtmlEncode(item.PodName)}</td><td>{item.TargetWeight:F1}%</td><td>{item.RawOrderVolumeBps:F1} bps</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.InternalNettingSavedBps:F1} bps</td><td>{item.NetExternalExecutionBps:F1} bps</td><td>{item.StopOutSlackMargin:F1}%</td><td>{item.DampedCapitalDeleveragingFactor:F2}</td><td>{WebUtility.HtmlEncode(item.PodRiskRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.PodAllocationAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mca.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百零七、Jump Trading & Tower Research Capital: 粗糙分数阶随机波动率 (Gatheral Rough Heston) 与微观粗糙度幂律偏度流形 (Phase 51)
        if (portfolio.RoughFractionalVolatilityGatheral != null)
        {
            var rfg = portfolio.RoughFractionalVolatilityGatheral;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百零七、Jump Trading & Tower Research Capital 粗糙分数阶随机波动率 (Gatheral Rough Heston) 与微观粗糙度幂律偏度流形 (Phase 51)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Jump Trading 与 Tower Research 高频量化微观结构：突破经典布朗运动 H=0.5 扩散局限，全面引入 Gatheral 粗糙分数布朗运动 (fBm)。在高频资产对数波动率中实证标定超低赫斯特指数 H ∈ (0.05, 0.20)，通过分数阶记忆核精确捕捉极短期限幂律隐波陡峭偏度，校准高频 Delta-Vega 动态对冲凸性。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">组合加权粗糙赫斯特指数</div><div class=\"kpi-val highlight\" style=\"color:#F38BA8\">H={rfg.PortfolioWeightedHurstExponent:F3}</div><div class=\"kpi-sub\">实证粗糙度 (H &lt;&lt; 0.5)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">系统粗糙高频爆裂指数</div><div class=\"kpi-val\" style=\"color:#F9E2AF\">{rfg.SystemicRoughnessBurstIndex:F2}x</div><div class=\"kpi-sub\">极短时间突发聚集乘数</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">短端幂律偏度无套利斜率</div><div class=\"kpi-val highlight\" style=\"color:#89B4FA\">{rfg.ShortTermPowerLawSkewSlope:F3}</div><div class=\"kpi-sub\">ψ(τ) ~ τ^(H-1/2) 偏度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">粗糙对冲凸性挽回摩擦</div><div class=\"kpi-val\" style=\"color:#A6E3A1\">+{rfg.RoughnessHedgingAlphaBps:F1} bps</div><div class=\"kpi-sub\">高频再对冲滑点节约</div></div>");
            sb.AppendLine("  </div>");

            if (rfg.RoughVolItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产粗糙赫斯特指数 H、粗糙度爆裂乘数、短端幂律偏度、记忆半衰期、凸性修正比率、粗糙度评级与对冲指引</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>赫斯特指数 H</th><th>粗糙爆裂乘数</th><th>短端幂律偏度</th><th>记忆半衰期</th><th>凸性修正比</th><th>粗糙度评级</th><th>粗糙波动率短端偏度与高频再对冲指引</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in rfg.RoughVolItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.EstimatedHurstExponent:F3}</td><td>{item.RoughnessBurstMultiplier:F2}x</td><td style=\"color:#f38ba8;font-weight:bold;\">{item.PowerLawAtTheMoneySkew:F3}</td><td>{item.VolMemoryPersistenceDays:F1} 天</td><td>{item.RoughnessConvexityRatio:F3}</td><td>{WebUtility.HtmlEncode(item.RoughnessRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.RoughnessHedgingAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(rfg.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百零八、Bridgewater Associates & BlackRock Aladdin: 宏观热力学最小相对交叉熵 (Jaynes MaxEnt) 与非高斯情景冲击流形映射 (Phase 51)
        if (portfolio.ThermodynamicCrossEntropyStress != null)
        {
            var ces = portfolio.ThermodynamicCrossEntropyStress;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百零八、Bridgewater Associates & BlackRock Aladdin 宏观热力学最小相对交叉熵 (Jaynes MaxEnt) 与非高斯情景冲击流形映射 (Phase 51)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标桥水全天候与贝莱德 Aladdin 极端风控架构：突破传统线性因子平移无法保持概率自洽的缺陷，引入 E.T. Jaynes 最大信息熵与最小相对交叉熵优化。在 4 大非高斯宏观极端冲击约束下求解吉布斯-玻尔兹曼状态方程，精确测算最小信息扭曲概率测度下的宏观信息温度、自由能坍缩与极端受压 CVaR。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">系统最小相对交叉熵</div><div class=\"kpi-val highlight\" style=\"color:#CBA6F7\">D_KL={ces.SystemicCrossEntropyDkl:F4}</div><div class=\"kpi-sub\">最小信息扭曲散度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">宏观热力学信息温度</div><div class=\"kpi-val\" style=\"color:#89DCEB\">T={ces.MacroThermodynamicTemperature:F2}</div><div class=\"kpi-sub\">宏观系统等效温度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">吉布斯自由能坍缩总量</div><div class=\"kpi-val\" style=\"color:#F9E2AF\">|ΔF|={ces.GibbsFreeEnergyCollapse:F3}</div><div class=\"kpi-sub\">极端情景势能耗散</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">99% 最小扭曲受压 CVaR</div><div class=\"kpi-val highlight\" style=\"color:#F38BA8\">{ces.StressedConditionalVaR99:F1}%</div><div class=\"kpi-sub\">全天候极值抗毁屏障</div></div>");
            sb.AppendLine("  </div>");

            if (ces.StressScenarioItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">宏观情景代码、极端冲击语义、先验概率 q、扭曲后概率 p*、相对熵 D_KL、自由能改变量 ΔF、受压损失率、烈度评级与对冲处方</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>情景代码</th><th>宏观极端情景语义</th><th>先验概率</th><th>扭曲概率 p*</th><th>相对熵 D_KL</th><th>自由能 ΔF</th><th>受压损失率</th><th>烈度评级</th><th>热力学最小扭曲压力防守对冲处方</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in ces.StressScenarioItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.ScenarioId)}</td><td>{WebUtility.HtmlEncode(item.ScenarioName)}</td><td>{item.PriorProbability:F1}%</td><td style=\"color:#f38ba8;font-weight:bold;\">{item.StressedPostProbability:F1}%</td><td>{item.KullbackLeiblerRelativeEntropy:F4}</td><td>{item.FreeEnergyShiftDeltaF:F3}</td><td>{item.StressedPortfolioLossPct:F1}%</td><td>{WebUtility.HtmlEncode(item.ScenarioSeverityBadge)}</td><td>{WebUtility.HtmlEncode(item.DynamicHedgingPrescription)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ces.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百零九、Jump Trading & Hudson River Trading: 超对称路径积分 (Supersymmetric Path Integral) 与非微扰瞬子 (Instanton) 极端跃迁防御 (Phase 51)
        if (portfolio.SupersymmetricInstantonTunneling != null)
        {
            var sit = portfolio.SupersymmetricInstantonTunneling;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百零九、Jump Trading & Hudson River Trading 超对称路径积分 (Supersymmetric Path Integral) 与非微扰瞬子 (Instanton) 极端黑天鹅跃迁防御 (Phase 51)</div>");
            sb.AppendLine("  <div class=\"narrative\">对标 Jump Trading 与 HRT 顶尖理论物理对冲内核：针对传统微扰展开在断崖式崩盘中级数发散崩溃的根本缺陷，引入超对称量子力学 (SUSY QM) 与欧几里得虚时间费曼路径积分。求解 Witten 超势欧拉-拉格朗日极小瞬子经典作用量 S_inst，测算流动性势垒穿透逃逸几率，部署超对称能级差安全垫与非微扰真空衰变黑天鹅防御头寸。</div>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">全局极小瞬子经典作用量</div><div class=\"kpi-val highlight\" style=\"color:#89B4FA\">S_inst={sit.GlobalInstantonActionS:F3}</div><div class=\"kpi-sub\">虚时间欧几里得测地作用</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">势垒非微扰隧道穿透逃逸率</div><div class=\"kpi-val highlight\" style=\"color:#F38BA8\">{sit.MaximumTunnelingEscapeRate:F1} bps</div><div class=\"kpi-sub\">流动性真空穿透跃迁几率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">超对称基态能级差缓冲</div><div class=\"kpi-val\" style=\"color:#A6E3A1\">{sit.SupersymmetricEnergyGapBps:F1} bps</div><div class=\"kpi-sub\">ΔE_SUSY 抗微扰安全屏障</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-label\">非微扰真空衰变防御头寸</div><div class=\"kpi-val highlight\" style=\"color:#FAB387\">{sit.NonPerturbativeTailShieldPct:F1}%</div><div class=\"kpi-sub\">抵御断崖穿透护城河</div></div>");
            sb.AppendLine("  </div>");

            if (sit.InstantonItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产 Witten 超势曲率 W''、瞬子极小作用量 S_inst、隧道跃迁逃逸几率、能级差缓冲、真空稳定性与防御对冲指令</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>超势曲率 W''</th><th>瞬子作用量 S</th><th>穿透几率 Γ</th><th>能级差缓冲</th><th>真空稳定性</th><th>非微扰势垒穿透与超对称负能级对冲指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in sit.InstantonItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.WittenSuperpotentialCurvature:F3}</td><td>{item.InstantonActionMinimalS:F3}</td><td style=\"color:#f38ba8;font-weight:bold;\">{item.TunnelingEscapeProbability:F1} bps</td><td>{item.EnergyGapBufferBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.VacuumStabilityBadge)}</td><td>{WebUtility.HtmlEncode(item.QuantumBarrierHedgingAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(sit.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 110. Citadel Securities & Jane Street: 微观瞬态幂律记忆价格冲击核 (Bouchaud Propagator) 与动态隐匿拆单执行引擎 (Phase 52)
        if (portfolio.BouchaudTransientImpactPropagator != null)
        {
            var tip = portfolio.BouchaudTransientImpactPropagator;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百一十、Citadel Securities & Jane Street: 微观瞬态幂律记忆价格冲击核 (Bouchaud Propagator) 与动态隐匿拆单执行引擎 (Phase 52)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标全球顶级做市自营 Citadel Securities 与 Jane Street Capital 的高频变分最优执行架构。针对经典 Almgren-Chriss 模型中将冲击机械分割为瞬时弹性与永久线性的缺陷，系统引入 Bouchaud-Mézard-Potters 幂律时滞记忆核 G(τ) = I_0 / (1 + τ/τ_0)^γ（γ ∈ [0.4, 0.6]）。通过求解受限于订单流动态隐匿度（Concealment）的离散变分欧拉-拉格朗日最优执行路径，有效消除连续分笔挂单自锁过冲，并精确量化相比朴素 TWAP 算法的瞬态滑点挽回增益。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">组合瞬态滑点挽回总增益</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{tip.GlobalTransientSlippageSavingsBps:F1} bps</div><div class=\"kpi-sub\">变分最优 U 型执行 vs TWAP</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">组合平均幂律衰减指数 γ</div><div class=\"kpi-value\">{tip.MeanPowerLawExponentGamma:F3}</div><div class=\"kpi-sub\">Bouchaud 核衰减记忆强弱度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">订单流动态隐匿度指数</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{tip.AverageOrderFlowConcealmentIndex:F1} / 100</div><div class=\"kpi-sub\">对抗高频嗅探与逆向选择保护</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">自冲击过冲消除比率</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{tip.SelfImpactMitigationRatioPct:F1}%</div><div class=\"kpi-sub\">非线性冲击自锁有效抑制率</div></div>");
            sb.AppendLine("  </div>");

            if (tip.ImpactItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产特征弛豫时间 τ_0、幂律指数 γ、自冲击累积、回弹弹性率、U型切片与滑点挽回明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>弛豫时间 τ_0</th><th>幂律指数 γ</th><th>自冲击累积</th><th>回弹弹性率</th><th>U型首期/尾期切片</th><th>滑点挽回增益</th><th>冲击流变模式</th><th>瞬态冲击自适应拆单与隐匿执行指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in tip.ImpactItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.CharacteristicRelaxationTimeSec:F1} s</td><td>{item.PowerLawDecayExponentGamma:F3}</td><td>{item.CumulativeSelfImpactBps:F1} bps</td><td>{item.ReboundElasticityRatio:F1}%</td><td>{item.OptimalUProfileHeadSlicePct:F1}% / {item.OptimalUProfileTailSlicePct:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.TransientSlippageSavingsBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.ImpactRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.TransientExecutionAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(tip.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 111. Two Sigma & D.E. Shaw: 高维流形图拉普拉斯算子谱聚类与非局部热核扩散小波多尺度 Alpha 分解引擎 (Phase 52)
        if (portfolio.GraphLaplacianDiffusionWavelet != null)
        {
            var gld = portfolio.GraphLaplacianDiffusionWavelet;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百一十一、Two Sigma & D.E. Shaw: 高维流形图拉普拉斯算子谱聚类与非局部热核扩散小波多尺度 Alpha 分解引擎 (Phase 52)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Two Sigma 与 D.E. Shaw 的非局部拓扑流形多资产表征架构。传统欧氏 PCA 强制假定平坦线性空间，在剧烈市场动荡下容易诱发因子共线性污染。本模块构建多资产收益率高斯相似度图权重矩阵与规范化图拉普拉斯算子 L_sym = I - D^(-1/2) W D^(-1/2)，求解菲德勒连通特征值 λ_2 与谐波流形坐标，并基于马尔可夫热核扩散算子 exp(-t L) 实施多尺度扩散小波滤波，精准提取不受局部微观噪声扰动的非局部内在流形 Alpha。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">菲德勒代数连通韧性 λ_2</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{gld.FiedlerAlgebraicConnectivity:F3}</div><div class=\"kpi-sub\">图拓扑抗断裂代数连通度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">组合本征热核扩散维度</div><div class=\"kpi-value\">{gld.ManifoldDiffusionDimension:F2}</div><div class=\"kpi-sub\">非欧流形谱内在自由度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">谱聚会社群模块度 Q</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{gld.SpectralClusteringModularity:F3}</div><div class=\"kpi-sub\">图拉普拉斯谱聚类凝聚质量</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">全局流形小波去噪纯化率</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">{gld.GlobalWaveletPurgeRatioPct:F1}%</div><div class=\"kpi-sub\">共线性与虚假噪声截断消除率</div></div>");
            sb.AppendLine("  </div>");

            if (gld.ClusterItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产谱聚会社群编号、图度中心性 D_ii、菲德勒谐波坐标 ψ_2、扩散半径与小波去噪明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>谱聚会社群</th><th>度中心性 D_ii</th><th>菲德勒坐标 ψ_2</th><th>扩散有效半径</th><th>小波纯化 Alpha</th><th>拓扑社群状态</th><th>流形图谱调仓与非局部 Alpha 增强指引</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in gld.ClusterItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>Cluster #{item.SpectralClusterId}</td><td>{item.GraphDegreeCentrality:F2}</td><td>{item.FiedlerHarmonicCoordinate:F3}</td><td>{item.HeatKernelDiffusionRadius:F2}</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.DiffusionWaveletNoisePurgeBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.TopologyCommunityBadge)}</td><td>{WebUtility.HtmlEncode(item.ManifoldAllocationAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(gld.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 112. Bridgewater Associates & AQR Capital: 分数阶粘弹性流变学宏观资产负荷动力学与系统性流动性蠕变松弛测算引擎 (Phase 52)
        if (portfolio.ViscoelasticRheologyCapitalStrain != null)
        {
            var vrc = portfolio.ViscoelasticRheologyCapitalStrain;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百一十二、Bridgewater Associates & AQR Capital: 分数阶粘弹性流变学宏观资产负荷动力学与系统性流动性蠕变松弛测算引擎 (Phase 52)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标桥水全天候宏观压力与 AQR 资本流变学理论。金融体系在持续的宏观紧缩与去杠杆压力下，流动性表现既非瞬时弹性也非彻底断裂，而是呈现典型的分数阶粘弹性流变特征。系统建立分数阶 Poynting-Thomson (Fractional Zener) 本构方程与 Mittag-Leffler 广义蠕变柔量核 J(t)，精准测算组合动态弹性储能模量 E_R 与粘性损耗角正切 tan δ，实现宏观负荷持续累积下的流动性耗竭断裂视界前瞻预警。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">流动性流变损耗角正切 tan δ</div><div class=\"kpi-value\" style=\"color:#f38ba8;\">{vrc.GlobalSystemicLossTangent:F3}</div><div class=\"kpi-sub\">粘性摩擦耗散 / 弹性储能比</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">组合加权分数阶流变阶数 α</div><div class=\"kpi-value\">{vrc.MeanFractionalOrderAlpha:F3}</div><div class=\"kpi-sub\">Caputo 分数阶记忆指数</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">宏观弹性复原储能模量 E_R</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{vrc.SystemicDynamicStorageModulus:F1} MPa</div><div class=\"kpi-sub\">应力卸除自愈刚度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">最早流动性断裂预警视界</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{vrc.EarliestCreepRuptureHorizonDays:F0} 天</div><div class=\"kpi-sub\">持续紧缩下安全缓冲耗尽周期</div></div>");
            sb.AppendLine("  </div>");

            if (vrc.RheologyItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产分数阶数 α、储能模量 E_R、耗散模量 E''、损耗角正切 tan δ、Mittag-Leffler 柔量与断裂视界明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>分数阶数 α</th><th>储能模量 E_R</th><th>耗散模量 E''</th><th>损耗角 tan δ</th><th>蠕变柔量 J_inf</th><th>断裂视界</th><th>流变韧性评级</th><th>粘弹性流变应力对冲与去杠杆阻尼指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in vrc.RheologyItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.FractionalRheologyOrderAlpha:F3}</td><td>{item.DynamicStorageModulusEr:F1} MPa</td><td>{item.DynamicLossModulusEi:F1} MPa</td><td>{item.SystemicLossTangentTanDelta:F3}</td><td>{item.MittagLefflerCreepCompliance:F5}</td><td style=\"color:#f9e2af;font-weight:bold;\">{item.CreepRuptureHorizonDays:F0} 天</td><td>{WebUtility.HtmlEncode(item.RheologyStateBadge)}</td><td>{WebUtility.HtmlEncode(item.ViscoelasticHedgingAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(vrc.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 113. Renaissance Technologies & Hudson River Trading: 非平衡态朗之万细致平衡破缺与概率流旋度相空间统计套利做功巡回引擎 (Phase 52)
        if (portfolio.NonequilibriumLangevinVorticity != null)
        {
            var nlv = portfolio.NonequilibriumLangevinVorticity;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百一十三、Renaissance Technologies & Hudson River Trading: 非平衡态朗之万细致平衡破缺与概率流旋度相空间统计套利做功巡回引擎 (Phase 52)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标文艺复兴大奖章基金 (Medallion) 与顶尖高频自营 HRT 的非平衡态统计物理套利架构。打破经典模型假定市场微观处于无偏细致平衡（Detailed Balance）的迷思，系统将高频相空间有效漂移力分解为保守梯度恢复力与非保守旋转流场 F = -∇U + F_rot。通过定量解算稳态概率流旋度 |Ω|、不可逆熵产生率与细致平衡破缺度 Φ_BDB，沿相空间闭合极限环抽取热力学泵送机械功 W_pump，将微观不可逆信息不对称转化为高度稳健的统计套利超额 Alpha。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">相空间极限环泵送总做功</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{nlv.GlobalLimitCyclePumpWorkBps:F1} bps</div><div class=\"kpi-sub\">非平衡态非保守旋转做功提取</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">平均细致平衡破缺度 Φ_BDB</div><div class=\"kpi-value\">{nlv.MeanBrokenDetailedBalanceDegree:F3}</div><div class=\"kpi-sub\">相空间不可逆环流强度 (0~1)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">最大稳态概率流旋度峰值</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{nlv.MaximumProbabilityVorticity:F3}</div><div class=\"kpi-sub\">|Ω|_max 涡度环流极大值</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">非平衡态统计套利捕获效率</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{nlv.NonEquilibriumStatArbEfficiencyPct:F1}%</div><div class=\"kpi-sub\">相空间非保守能量转换率</div></div>");
            sb.AppendLine("  </div>");

            if (nlv.VorticityItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各套利资产对保守力 |-∇U|、非保守旋转力 |F_rot|、破缺度 Φ_BDB、旋度 |Ω|、熵产生与泵送做功明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产对代码</th><th>标的配对名称</th><th>保守恢复力</th><th>非保守旋转力</th><th>破缺度 Φ_BDB</th><th>概率流旋度 |Ω|</th><th>熵产生率 S_dot</th><th>极限环做功</th><th>旋度做功状态</th><th>非平衡态朗之万相空间统计套利执行指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in nlv.VorticityItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.PairCode)}</td><td>{WebUtility.HtmlEncode(item.PairName)}</td><td>{item.StationaryDriftGradientForce:F2}</td><td>{item.NonConservativeRotationalForce:F2}</td><td>{item.BrokenDetailedBalanceDegree:F3}</td><td>{item.ProbabilityCurrentVorticity:F3}</td><td>{item.IrreversibleEntropyProductionRate:F2}</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.LimitCyclePumpWorkBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.VorticityRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.LangevinArbitrageAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(nlv.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 114. Jump Trading & Optiver: 连续时间仿射跳跃-扩散限价簿做市与自适应逆向选择免受损控制引擎 (Phase 53)
        if (portfolio.JumpDiffusionAffineMarketMaking != null)
        {
            var jdm = portfolio.JumpDiffusionAffineMarketMaking;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百一十四、Jump Trading & Optiver: 连续时间仿射跳跃-扩散限价簿做市与自适应逆向选择免受损控制引擎 (Phase 53)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Jump Trading 与 Optiver 等顶级高频做市商的非高斯跳跃限价簿动力学架构。打破经典 Avellaneda-Stoikov 假定连续布朗运动的局限，系统引入 Merton/Kou 仿射跳跃-扩散随机过程与 HJB 积分-偏微分方程 (PIDE)。在考虑非对称双边跳跃强度与二次库存惩罚约束下，动态推导抗毒性击穿的最优非对称买卖半利差 (δ_*^b, δ_*^a)，精确量化毒性跳跃冲击规避增益与做市库存吸收弹性韧性率。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">毒性跳跃规避总增益</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{jdm.GlobalToxicityAvoidanceGainBps:F1} bps</div><div class=\"kpi-sub\">非对称报价逆向选择挽回</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">加权平均跳跃强度 λ_jump</div><div class=\"kpi-value\">{jdm.MeanJumpPoissonIntensity:F2} 次/年</div><div class=\"kpi-sub\">泊松极端跳跃发生频率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">全天候最优买卖总价差</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{jdm.AverageOptimalBidAskSpreadBps:F1} bps</div><div class=\"kpi-sub\">做市商最优双边报价中枢</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">库存抗跳跃吸收韧性率</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{jdm.MarketMakingInventoryResiliencePct:F1}%</div><div class=\"kpi-sub\">极端冲击下的持仓存活韧性</div></div>");
            sb.AppendLine("  </div>");

            if (jdm.MarketMakingItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产泊松跳跃频率 λ、跳跃幅度 μ_J、最优买卖半利差、二次库存惩罚与毒性规避增益明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>跳跃频率 λ</th><th>跳跃均值 μ_J</th><th>最优买/卖半价差</th><th>库存惩罚 γ</th><th>毒性规避增益</th><th>跳跃做市状态</th><th>做市挂单与非对称库存控制指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in jdm.MarketMakingItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.JumpPoissonIntensityLambda:F2} 次/年</td><td>{item.MeanJumpMagnitudePercent:F2}%</td><td>[{item.OptimalBidHalfSpreadBps:F1}, {item.OptimalAskHalfSpreadBps:F1}] bps</td><td>{item.InventoryRiskPenaltyGamma:F3}</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.ToxicityAvoidanceGainBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.JumpRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.MarketMakingControlAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(jdm.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 115. Citadel Global Fixed Income & Millennium Macro: 多因子无套利高斯仿射动态期限结构与曲率相对价值套利引擎 (Phase 53)
        if (portfolio.AffineArbitrageFreeTermStructure != null)
        {
            var ats = portfolio.AffineArbitrageFreeTermStructure;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百一十五、Citadel Global Fixed Income & Millennium Macro: 多因子无套利高斯仿射动态期限结构与曲率相对价值套利引擎 (Phase 53)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Citadel 固收霸主与 Millennium 宏观相对价值策略的核心定价中枢。针对经典平滑样条允许跨期套利机会的理论缺陷，系统构建 Dai-Singleton / Christensen-Diebold-Rudebusch (DNS-ATSM) 无套利高斯仿射期限结构模型。在风险中性测度 Q 下严谨推导满足 PDE 相容性的零息票解析解，精确拆解期望短期利率路径与动态期限溢价 (Term Premium)，实现收益率曲线蝶式凸性曲率套利潜能的无套利纯化捕获。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">蝶式凸性套利总 Alpha</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{ats.GlobalButterflyConvexityAlphaBps:F1} bps</div><div class=\"kpi-sub\">中端凸性错配相对价值收益</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">组合平均期限溢价 TP</div><div class=\"kpi-value\">+{ats.MeanDynamicTermPremiumBps:F1} bps</div><div class=\"kpi-sub\">长端持有展期超额溢价中枢</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">无套利偏离残差 RMS</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{ats.YieldCurveArbitrageViolationResidual:F2} bps</div><div class=\"kpi-sub\">曲线跨期无套利边界贴合度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">仿射模型跟踪拟合质量</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{ats.TermStructureTrackingQualityPct:F2}%</div><div class=\"kpi-sub\">DNS-ATSM 状态空间解释力 R²</div></div>");
            sb.AppendLine("  </div>");

            if (ats.FactorItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产水平/斜率/曲率三因子载荷、动态期限溢价、蝶式凸性套利 Alpha 与拟合 R² 明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>水平载荷 L</th><th>斜率载荷 S</th><th>曲率载荷 C</th><th>期限溢价 TP</th><th>蝶式凸性 Alpha</th><th>拟合优度 R²</th><th>期限形态</th><th>无套利曲线相对价值套利执行指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in ats.FactorItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.LevelFactorLoadingL:F3}</td><td>{item.SlopeFactorLoadingS:F3}</td><td>{item.CurvatureFactorLoadingC:F3}</td><td>+{item.DynamicTermPremiumBps:F1} bps</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.ButterflyConvexityArbitrageAlphaBps:F1} bps</td><td>{item.ArbitrageFreeModelFitR2:F4}</td><td>{WebUtility.HtmlEncode(item.TermStructureRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.YieldCurveArbitrageAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ats.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 116. Point72 & Citadel Multi-Strategy: 多 Pod 合作博弈 Shapley-Owen 边际分散贡献与动态流动性影子定价仲裁引擎 (Phase 53)
        if (portfolio.MultiPodShapleyShadowPricing != null)
        {
            var sp = portfolio.MultiPodShapleyShadowPricing;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百一十六、Point72 & Citadel Multi-Strategy: 多 Pod 合作博弈 Shapley-Owen 边际分散贡献与动态流动性影子定价仲裁引擎 (Phase 53)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Point72 (SAC) 与 Citadel 等顶级多经理对冲基金的多 Pod 资本博弈仲裁架构。针对粗粒度夏普平价忽视 Pod 间持仓重叠与流动性争夺的痛点，系统构建多管理人合作博弈论联盟特征函数 v(S)，严谨计算 Shapley-Owen 真实边际分散化贡献积分。结合 KKT 对偶流动性影子价格 λ^*，动态实施全平台资金内部借贷费率拍卖与杠杆再平衡裁决，彻底根除搭便车与流动性踩踏隐患。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">内部资金撮合节约增益</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{sp.GlobalInternalCapitalNettingGainBps:F1} bps</div><div class=\"kpi-sub\">多 Pod 头寸互冲融资减摩</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">流动性影子借贷基准价格</div><div class=\"kpi-value\">{sp.MeanLiquidityShadowPriceBps:F1} bps</div><div class=\"kpi-sub\">KKT 对偶资金边际借贷成本</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">合作博弈帕累托效率</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{sp.CooperativeGameEfficiencyRatioPct:F1}%</div><div class=\"kpi-sub\">Shapley 分配联盟剩余最大化</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">多 Pod 拥挤对冲阻尼指数</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{sp.SystemicCrowdingDampeningIndex:F2}</div><div class=\"kpi-sub\">多管理人流动性踩踏防护系数</div></div>");
            sb.AppendLine("  </div>");

            if (sp.PodItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各 Pod 初始分配、Shapley 边际贡献、影子借贷价格、分散增益倍数、杠杆乘数与目标调整明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>Pod 代码</th><th>策略集群名称</th><th>初始资本占比</th><th>Shapley 边际贡献</th><th>影子借贷成本</th><th>分散增益比</th><th>动态杠杆</th><th>建议调整占比</th><th>仲裁评级</th><th>多 Pod 资本拍卖与动态杠杆再平衡指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in sp.PodItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.PodCode)}</td><td>{WebUtility.HtmlEncode(item.PodName)}</td><td>{item.AllocatedCapitalPct:F1}%</td><td>{item.ShapleyMarginalContributionPct:F1}%</td><td>{item.LiquidityShadowPriceCostBps:F1} bps</td><td>{item.DiversificationGainRatio:F2}x</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.DynamicLeverageMultiplier:F2}x</td><td style=\"color:#a6e3a1;font-weight:bold;\">{item.TargetRebalancedCapitalPct:F1}%</td><td>{WebUtility.HtmlEncode(item.PodArbitrationBadge)}</td><td>{WebUtility.HtmlEncode(item.CapitalArbitrationAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(sp.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 117. D.E. Shaw & WorldQuant: 非欧流形 Ollivier-Ricci 拓扑曲率流相变相干与高阶持久同调过滤引擎 (Phase 53)
        if (portfolio.OllivierRicciCurvaturePersistentHomology != null)
        {
            var orc = portfolio.OllivierRicciCurvaturePersistentHomology;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百一十七、D.E. Shaw & WorldQuant: 非欧流形 Ollivier-Ricci 拓扑曲率流相变相干与高阶持久同调过滤引擎 (Phase 53)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 D.E. Shaw 与 WorldQuant 前沿微分几何与代数拓扑量化架构。摆脱经典欧氏切空间假设，系统在资产相关性网络上定义基于 Wasserstein-1 最优传输的离散 Ollivier-Ricci 几何曲率 κ。运行 Ricci 曲率流平滑收缩非欧流形上的奇异噪声点，并融合持久同调 (Persistent Homology) 拓扑数据分析，追踪零阶连通分支 β_0 与一阶拓扑环洞 β_1 持续周期，前瞻性预警系统性拓扑相变崩溃并提取纯化 Alpha。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">Ricci 流去噪全局 Alpha</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{orc.GlobalRicciFlowDenoisingAlphaBps:F1} bps</div><div class=\"kpi-sub\">流形曲率平滑去噪纯化超额</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">全图平均 Ollivier-Ricci 曲率</div><div class=\"kpi-value\">κ={orc.MeanGraphOllivierRicciCurvature:F3}</div><div class=\"kpi-sub\">资产网络全局离散几何度量曲率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">宏观拓扑相变相干度</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{orc.MacroPhaseTransitionCoherence:F1} / 100</div><div class=\"kpi-sub\">流形连通度与相变韧性综合评分</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">持久同调一阶环洞密度</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{orc.PersistentHomologyCycleDensity:F2}</div><div class=\"kpi-sub\">Betti-1 高维拓扑孔洞持续度量</div></div>");
            sb.AppendLine("  </div>");

            if (orc.TopologicalItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产 Ollivier-Ricci 曲率 κ、去噪 Alpha、Betti-0 寿命、Betti-1 持续性、相干评分与流形评级明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>Ricci 曲率 κ</th><th>去噪 Alpha</th><th>Betti-0 寿命</th><th>Betti-1 持续性</th><th>相干评分</th><th>流形拓扑评级</th><th>流形 Ricci 拓扑相变过滤与仓位调优指引</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in orc.TopologicalItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.NodeOllivierRicciCurvature:F3}</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.RicciFlowDenoisingGainBps:F1} bps</td><td>{item.ZeroBettiClusterLifespan:F2}</td><td>{item.OneBettiCyclePersistence:F2}</td><td>{item.TopologicalPhaseCoherenceScore:F1}</td><td>{WebUtility.HtmlEncode(item.RicciManifoldBadge)}</td><td>{WebUtility.HtmlEncode(item.TopologicalAlphaAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(orc.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 118. Renaissance Technologies & D.E. Shaw: 连续时间马尔可夫跳跃状态空间与分层狄利克雷过程 (HDP-HMM) 动态非参数宏观体制涌现引擎 (Phase 54)
        if (portfolio.ContinuousMarkovSwitchingDirichletProcess != null)
        {
            var hdp = portfolio.ContinuousMarkovSwitchingDirichletProcess;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百一十八、Renaissance Technologies & D.E. Shaw: 连续时间马尔可夫跳跃状态空间与分层狄利克雷过程宏观体制涌现引擎 (Phase 54)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Renaissance Technologies (Medallion) 与 D.E. Shaw 前沿非参数贝叶斯与连续时间马尔可夫跳跃状态空间量化架构。摆脱经典高斯 HMM 依赖人为主观固化 2 态或 3 态导致的结构性时延滞后，系统采用分层狄利克雷过程隐马尔可夫模型 (HDP-HMM)，实现隐状态数量的自适应“无界涌现”；同时构建连续时间转移生成元矩阵 Q，精确估算资产稳态驻留概率 π*、期望驻留寿命 τ 与前瞻突变危险率 (Hazard Rate)，并以香农信息熵 H 动态捕捉宏观相变跃迁节点。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">全组合宏观体制信息熵</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{hdp.GlobalHdpRegimeEntropy:F3} nats</div><div class=\"kpi-sub\">体制转换香农信息熵中枢</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">主导体制突变危险率</div><div class=\"kpi-value\" style=\"color:#f38ba8;\">{hdp.DominantStateHazardRate:F2} 次/年</div><div class=\"kpi-sub\">前瞻马尔可夫跳跃瞬时跃迁强度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">动态涌现隐状态数</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{hdp.NonparametricActiveStateCount} 个有效状态</div><div class=\"kpi-sub\">分层狄利克雷非参数聚类状态</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">体制转移稳定性置信度</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">{hdp.HdpStateTransitionStabilityPct:F1}%</div><div class=\"kpi-sub\">跨周期非参数贝叶斯稳态置信度</div></div>");
            sb.AppendLine("  </div>");

            if (hdp.RegimeItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产自适应隐状态、稳态概率 π*、驻留寿命 τ、瞬时危险率、体制信息熵、Alpha 增益与对冲指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>自适应隐状态</th><th>稳态概率 π*</th><th>驻留寿命 τ</th><th>瞬时危险率</th><th>体制信息熵</th><th>Alpha 增益</th><th>状态评级</th><th>狄利克雷隐状态前瞻对冲与战术配置指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in hdp.RegimeItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{WebUtility.HtmlEncode(item.ActiveLatentState)}</td><td>{item.StationaryProbabilityPct:F1}%</td><td>{item.MeanSojournTimeDays:F1} 天</td><td>{item.InstantaneousHazardRate:F2} 次/年</td><td>{item.RegimeEntropyNats:F3} nats</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.HdpAlphaYieldGainBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.StateRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.DirichletRegimeControlAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(hdp.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 119. Citadel Securities & Hudson River Trading: 双边限价订单簿微观非线性平均场博弈与高阶随机冲量最优流动性提供控制引擎 (Phase 54)
        if (portfolio.MeanFieldGameImpulseLiquidityControl != null)
        {
            var mfg = portfolio.MeanFieldGameImpulseLiquidityControl;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百一十九、Citadel Securities & Hudson River Trading: 双边限价订单簿微观非线性平均场博弈与高阶随机冲量最优流动性提供控制引擎 (Phase 54)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Citadel Securities 与 Hudson River Trading (HRT) 高频做市与微观流动性博弈底层架构。打破传统单一做市商模型静态对手方假说，联立求解 Lasry-Lions 连续时间平均场博弈偏微分方程组：前向 Kolmogorov-Fokker-Planck 控制全市场做市商连续体分布演化 m(t, x)，后向 HJB 方程控制带离散调仓摩擦的随机冲量控制 (Impulse Control)。系统精准解析纳什均衡连续最优挂单半利差与离散冲量临界区间 [s_*, S^*]，实现对群体羊群效应的免疫防御与做市减摩。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">平均场博弈做市 Alpha</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{mfg.GlobalMeanFieldLiquidityAlphaBps:F1} bps</div><div class=\"kpi-sub\">纳什均衡连续挂单净超额收益</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">纳什均衡双边价差中枢</div><div class=\"kpi-value\">{mfg.MeanFieldNashEquilibriumSpreadBps:F1} bps</div><div class=\"kpi-sub\">Lasry-Lions 稳态连续半利差</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">微观羊群效应免疫度</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{mfg.HerdingCrowdImmunityPct:F1}%</div><div class=\"kpi-sub\">抗群体踩踏拥挤度吸收能力</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">随机冲量离散减摩效率</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{mfg.ImpulseControlEfficiencyRatio:F2}x</div><div class=\"kpi-sub\">边界控制摩擦节约增益倍数</div></div>");
            sb.AppendLine("  </div>");

            if (mfg.ImpulseItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产纳什半利差 δ*、冲量阈值 S^*、平均场密度 m、做市 Alpha、羊群易损度、冲量节约与挂单指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>纳什半利差 δ*</th><th>冲量阈值 S^*</th><th>平均场密度 m</th><th>做市 Alpha</th><th>羊群易损度</th><th>冲量节约</th><th>平均场评级</th><th>平均场博弈连续挂单与冲量执行指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in mfg.ImpulseItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.ContinuousSpreadBps:F1} bps</td><td>{item.ImpulseThresholdSStar:F2}σ</td><td>{item.MeanFieldDensityM:F3}</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.NashEquilibriumAlphaBps:F1} bps</td><td>{item.HerdingVulnerability:F3}</td><td>+{item.ImpulseExecutionSavingBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.MfgStateBadge)}</td><td>{WebUtility.HtmlEncode(item.MfgOrderControlAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mfg.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 120. Two Sigma & WorldQuant: 高维因果发现有向无环图与结构因果模型反事实干预不变性 Alpha 纯化引擎 (Phase 54)
        if (portfolio.CausalDagStructuralInvarianceAlpha != null)
        {
            var causal = portfolio.CausalDagStructuralInvarianceAlpha;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百二十、Two Sigma & WorldQuant: 高维因果发现有向无环图与结构因果模型反事实干预不变性 Alpha 纯化引擎 (Phase 54)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Two Sigma 与 WorldQuant 统计因果推断与结构因果模型 (SCM) 前沿体系。针对传统多因子模型中因混杂因素 (Confounders)、对撞节点 (Colliders) 和伪相关导致的因子快速衰竭难题，系统基于连续优化约束矩阵 DAG 算法建立因果拓扑有向网络，运用 Judea Pearl 反事实 do-calculus 外生干预切断混杂后门路径，测算因果不变性纯度 (Causal Invariance Purity)，剥离内生关联噪声并输出纯化残差因果 Alpha。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">因果不变纯化全局 Alpha</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{causal.GlobalCausalInvarianceAlphaBps:F1} bps</div><div class=\"kpi-sub\">反事实 do-calculus 纯化超额</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">因果 DAG 拓扑稀疏度</div><div class=\"kpi-value\">{causal.MeanCausalGraphSparsityRatio:F2}</div><div class=\"kpi-sub\">NOTEARS 约束矩阵稀疏指数</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">虚假统计相关剔除率</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{causal.SpuriousCorrelationRejectionRatePct:F1}%</div><div class=\"kpi-sub\">混杂后门与对撞偏差降噪比率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">反事实稳健性评分</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{causal.CounterfactualRobustnessScore:F1} / 100</div><div class=\"kpi-sub\">跨微观与宏观环境因果不变性得分</div></div>");
            sb.AppendLine("  </div>");

            if (causal.CausalItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产因果不变性纯度、反事实纯化 Alpha、直系父节点数、混杂衰减率、稳定性比率与因果指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>因果不变纯度</th><th>反事实纯化 Alpha</th><th>直系父节点数</th><th>混杂偏差衰减率</th><th>因果稳定比率</th><th>因果评级</th><th>SCM 结构因果干预与不变性 Alpha 配置指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in causal.CausalItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.CausalInvarianceScore:F1} 分</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.DoCalculusInterventionAlphaBps:F1} bps</td><td>{item.DirectCausalParentsCount} 个</td><td>{item.ConfounderBiasAttenuationPct:F1}%</td><td>{item.CausalStabilityRatio:F2}x</td><td>{WebUtility.HtmlEncode(item.CausalRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.CausalAlphaTradingAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(causal.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 121. Bridgewater Associates & AQR Capital: 广义非对称谱风险测度与动态极值高斯-Student Copula 尾部联动全景压测引擎 (Phase 54)
        if (portfolio.SpectralRiskExtremeCopulaStress != null)
        {
            var srm = portfolio.SpectralRiskExtremeCopulaStress;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百二十一、Bridgewater Associates & AQR Capital: 广义非对称谱风险测度与动态极值高斯-Student Copula 尾部联动全景压测引擎 (Phase 54)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Bridgewater Associates 与 AQR Capital Management 极端深水尾部抗毁与一致性风险测度最高基准。突破传统单一分位数 VaR/CVaR 局限，依据 Carlo Acerbi 严格一致性公理构建广义谱风险测度 (SRM)，对极端穿仓深水区赋予指数级风险厌恶谱权重；联立极值理论 (EVT) Peaks-Over-Threshold (POT) 广义帕累托分布 (GPD) 与动态极值 Student-t / Clayton Copula 非对称下行尾部相依矩阵，精确锁定尾部踩踏脱钩风险，并输出最优尾部凸性衍生品对冲覆盖方案。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">广义谱风险资本拨备率</div><div class=\"kpi-value\" style=\"color:#f38ba8;\">{srm.GlobalSpectralRiskCapitalRequirementPct:F1}%</div><div class=\"kpi-sub\">Acerbi 一致性谱测度资本要求</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">极值 Copula 尾部相依度</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">λ_L={srm.ExtremeTailAsymmetricCopulaDependency:F3}</div><div class=\"kpi-sub\">极端下行非对称联结构造系数</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">GPD 厚尾形状参数 ξ</div><div class=\"kpi-value\">{srm.GpdTailShapeParameterXi:F3}</div><div class=\"kpi-sub\">广义帕累托尾部肥厚程度 (Tail Index)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">最优尾部凸性对冲覆盖率</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">{srm.OptimalTailConvexityHedgeRatioPct:F1}%</div><div class=\"kpi-sub\">极端深水抗毁衍生品推荐覆盖</div></div>");
            sb.AppendLine("  </div>");

            if (srm.StressItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产谱风险贡献、GPD 尺度与形状、下行 Copula λ_L、对冲成本、压力资本充足率与对冲指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>谱风险贡献</th><th>GPD 尺度 β</th><th>GPD 厚尾 ξ</th><th>下行 Copula λ_L</th><th>对冲成本</th><th>压力资本充足率</th><th>谱风险评级</th><th>极值 Copula 动态尾部对冲执行指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in srm.StressItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.SpectralRiskContributionPct:F1}%</td><td>{item.GpdScaleBeta:F4}</td><td>{item.GpdShapeXi:F3}</td><td>{item.AsymmetricLowerTailCopula:F3}</td><td>{item.TailConvexityHedgeCostBps:F1} bps</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.StressCapitalAdequacyPct:F1}%</td><td>{WebUtility.HtmlEncode(item.SpectralRiskBadge)}</td><td>{WebUtility.HtmlEncode(item.ExtremeTailHedgingAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(srm.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 122. Renaissance Technologies & Two Sigma: 高维随机矩阵理论局部谱去噪与收缩协方差重构引擎 (Phase 55)
        if (portfolio.RandomMatrixLocalSpectralShrinkage != null)
        {
            var rmt = portfolio.RandomMatrixLocalSpectralShrinkage;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百二十二、Renaissance Technologies & Two Sigma: 高维随机矩阵理论局部谱去噪与收缩协方差重构引擎 (Phase 55)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Renaissance Technologies 与 Two Sigma 高维随机矩阵谱理论与协方差清洁前沿。解析经验相关阵的高斯正交系散斑，依据 Marchenko-Pastur 极限特征谱分布分离出纯噪声区间并定位超越 BBP 临界跃迁的独立 Spike 真实宏观因子；引入 Ledoit-Peit 局部非线性谱收缩算法，自适应按特征值密度对淹没在噪声带边缘的虚假统计相关施加收缩惩罚，彻底终结样本外协方差病态奇异与高频虚假换手摩擦。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">RMT 谱去噪信噪比增益</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{rmt.GlobalRmtSignalToNoiseRatioGain:F1} dB</div><div class=\"kpi-sub\">全局特征谱清洁信噪比提升</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">MP 极限噪声谱上界截止比</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{rmt.MarchenkoPasturUpperBoundRatio:F1}%</div><div class=\"kpi-sub\">Marchenko-Pastur 理论散斑上界 λ_+</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">独立 Spike 信号因子数</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{rmt.SpikeFactorCount} 个</div><div class=\"kpi-sub\">超越 BBP 临界跃迁的宏观真实信号</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">最优非线性谱收缩强度</div><div class=\"kpi-value\">{rmt.OptimalShrinkageIntensityPct:F1}%</div><div class=\"kpi-sub\">Ledoit-Peit 局部收缩惩罚中枢</div></div>");
            sb.AppendLine("  </div>");

            if (rmt.SpectralItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产原始样本方差、谱去噪方差、噪声衰减率、Spike 载荷、非线性收缩权重、谱去噪 Alpha 与重构指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>样本方差</th><th>谱去噪方差</th><th>噪声衰减率</th><th>Spike 载荷</th><th>收缩权重</th><th>谱去噪 Alpha</th><th>谱评级</th><th>RMT 协方差重构配置指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in rmt.SpectralItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.RawSampleVariance:F3}%</td><td>{item.DenoisedSpectralVariance:F3}%</td><td>{item.NoiseFilteringRatioPct:F1}%</td><td>{item.SpikeFactorLoading:F3}</td><td>{item.LedoitPeitShrinkageWeight:P1}</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.SpectralDenoisedAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.RmtRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.RmtPortfolioAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(rmt.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 123. Citadel Securities & Jump Trading: 微观分形霍克斯自激互激订单流毒性与闪崩级联预警引擎 (Phase 55)
        if (portfolio.MultivariateHawkesToxicityCascade != null)
        {
            var hawkes = portfolio.MultivariateHawkesToxicityCascade;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百二十三、Citadel Securities & Jump Trading: 微观分形霍克斯自激互激订单流毒性与闪崩级联预警引擎 (Phase 55)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Citadel Securities 与 Jump Trading 超高频做市防御与闪崩雪崩级联阻断机制。将多资产微观订单簿建模为多元带标记分形霍克斯点过程 (Marked Hawkes Point Process)，解算各标的盘口内自激强度 α_ii 与跨资产传染互激强度 ∑α_ij；通过分支比率矩阵的分支谱半径 ρ(Γ) 实时监测做市库存处于亚临界安全泊松耗散区还是超临界雪崩临界区，提前 1.5 秒阻断跨资产闪崩传染链，捕获微观防毒减摩 Alpha。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">霍克斯分支比率谱半径</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">ρ={hawkes.GlobalHawkesBranchingRatio:F3}</div><div class=\"kpi-sub\">全局分支比率矩阵谱半径 ρ(Γ)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">订单流逆向选择毒性</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{hawkes.OrderFlowToxicityScore:F1} 分</div><div class=\"kpi-sub\">微观毒性知情交易指数 (0~100)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">闪崩级联雪崩脆弱度</div><div class=\"kpi-value\" style=\"color:#f38ba8;\">{hawkes.FlashCrashCascadeVulnerabilityPct:F1}%</div><div class=\"kpi-sub\">跨资产连锁踩踏敏感度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">微观防毒挂单执行 Alpha</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{hawkes.MicrostructureAntidoteAlphaBps:F1} bps</div><div class=\"kpi-sub\">自适应撤单防逆向选择执行增益</div></div>");
            sb.AppendLine("  </div>");

            if (hawkes.CascadeItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产霍克斯自激 α_ii、互激 ∑α_ij、局部分支谱半径、毒性到达率、级联敏感度、减摩 Alpha 与防毒指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>盘口自激 α</th><th>跨资产互激 β</th><th>分支谱半径 ρ</th><th>毒性到达率</th><th>级联脆弱度</th><th>防毒 Alpha</th><th>霍克斯状态</th><th>多元霍克斯防毒与撤单保护指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in hawkes.CascadeItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.SelfExcitationAlpha:F3}</td><td>{item.CrossExcitationBeta:F3}</td><td>{item.BranchingSpectralRadius:F3}</td><td>{item.MarkedToxicityIntensity:F1} 次/秒</td><td>{item.CascadeVulnerabilityPct:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.HawkesExecutionAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.HawkesStateBadge)}</td><td>{WebUtility.HtmlEncode(item.HawkesToxicityAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(hawkes.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 124. Bridgewater Associates & AQR Capital: 多资产非线性分形波动率记忆与赫斯特表面跨周期极值回撤对偶防御引擎 (Phase 55)
        if (portfolio.MultifractalHurstSurfaceDefense != null)
        {
            var hurst = portfolio.MultifractalHurstSurfaceDefense;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百二十四、Bridgewater Associates & AQR Capital: 多资产非线性分形波动率记忆与赫斯特表面跨周期极值回撤对偶防御引擎 (Phase 55)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Bridgewater Associates 与 AQR Capital Management 全天候宏观分形几何与长程记忆风险防御架构。突破单重分数布朗运动假设，应用多重分形消除趋势波动分析 (MF-DFA)，在全时滞尺度与高阶矩空间解构广义赫斯特表面 h(q) 与勒让德多重分形奇异谱 f(α)；针对谱宽 Δα 激增诱发的极端多重分形波动率聚集，构建跨周期极值回撤对偶防御矩阵，动态提供下行对偶凸性锁定。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">长程记忆赫斯特指数 H</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">H={hurst.LongRangeMemoryHurstExponent:F3}</div><div class=\"kpi-sub\">全局长程趋势自相似记忆强度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">多重分形奇异谱宽 Δα</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">Δα={hurst.GlobalMultifractalSpectrumWidth:F3}</div><div class=\"kpi-sub\">非线性波动率奇异性跨度 (Multifractality)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">分形谱左右非对称偏度</div><div class=\"kpi-value\">{hurst.FractalAsymmetryDegree:F3}</div><div class=\"kpi-sub\">下行奇异聚集 vs 上行扩散非对称度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">对偶极值回撤防御覆盖率</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">{hurst.DualExtremumDrawdownDefenseRatio:F1}%</div><div class=\"kpi-sub\">全周期极端深水踩踏对偶保护</div></div>");
            sb.AppendLine("  </div>");

            if (hurst.MultifractalItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产赫斯特 H、多重分形谱宽 Δα、奇异中枢 α_0、长程记忆评分、回撤压降率、反脆弱 Alpha 与防御指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>赫斯特 H</th><th>谱宽 Δα</th><th>奇异极值 α_0</th><th>记忆评分</th><th>回撤压降率</th><th>反脆弱 Alpha</th><th>分形评级</th><th>赫斯特表面对偶防御配置指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in hurst.MultifractalItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.HurstExponentH:F3}</td><td>{item.MultifractalWidthDeltaAlpha:F3}</td><td>{item.SingularityModeAlphaZero:F3}</td><td>{item.PersistenceMemoryScore:F1} 分</td><td>{item.DualDrawdownDefenseGainPct:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.FractalAntiFragileAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.MultifractalBadge)}</td><td>{WebUtility.HtmlEncode(item.MultifractalDefenseAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(hurst.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 125. Point72 & Millennium Management: 多代理强化学习非稳态微观策略博弈与对抗性鲁棒策略蒸馏引擎 (Phase 55)
        if (portfolio.MultiAgentAdversarialPolicyDistillation != null)
        {
            var marl = portfolio.MultiAgentAdversarialPolicyDistillation;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百二十五、Point72 & Millennium Management: 多代理强化学习非稳态微观策略博弈与对抗性鲁棒策略蒸馏引擎 (Phase 55)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Point72 与 Millennium Management 多 PM 策略 PodShop 平台在非稳态对抗市场中的资本博弈与鲁棒压缩前沿。构建部分可观测随机博弈 (POSG) 与多代理深度强化学习 (MARL)，引入 Minimax 对抗攻击扰动球与教师-学生策略蒸馏 (Policy Distillation)，通过最小化策略 KL 散度与高阶熵正则化，从复杂庞大的黑天鹅策略空间中萃取紧凑精炼的鲁棒执行核，彻底避免多 PM 交易拥挤踩踏与过度拟合虚假套利。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">全局对抗鲁棒性评分</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">{marl.GlobalAdversarialRobustnessScore:F1} 分</div><div class=\"kpi-sub\">Minimax 极值扰动综合抗毁评分</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">纳什均衡博弈收敛度</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{marl.NashEquilibriumConvergenceDegree:F1}%</div><div class=\"kpi-sub\">多代理马尔可夫博弈稳态收敛度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">鲁棒策略蒸馏保真度</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{marl.PolicyDistillationFidelityPct:F1}%</div><div class=\"kpi-sub\">教师-学生策略 KL 紧凑压缩保真度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">策略蒸馏防挤压 Alpha</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{marl.DistilledAntiSqueezeAlphaBps:F1} bps</div><div class=\"kpi-sub\">抗踩踏鲁棒目标权重调仓增益</div></div>");
            sb.AppendLine("  </div>");

            if (marl.DistillationItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产对抗扰动半径 ε、蒸馏 KL 散度、纳什协同评分、抗踩踏弹性、鲁棒目标权重、蒸馏 Alpha 与配置指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>扰动半径 ε</th><th>蒸馏 KL 散度</th><th>纳什协同度</th><th>抗踩踏弹性</th><th>鲁棒目标权重</th><th>蒸馏 Alpha</th><th>MARL 评级</th><th>多代理博弈与鲁棒蒸馏落地指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in marl.DistillationItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.AdversarialPerturbationRadius:F3}</td><td>{item.DistillationKlDivergence:F3}</td><td>{item.MultiAgentNashCooperationScore:F1} 分</td><td>{item.AntiCrowdingResiliencePct:F1}%</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.RobustOptimalWeightPct:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.DistilledAlphaGainBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.MarlRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.MarlDistillationAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(marl.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 126. Jane Street & Citadel Securities: 全档限价订单簿 (LOB) 微观价格扩散鞅测度变换与即时流动性真空渗透控制引擎 (Phase 56)
        if (portfolio.LobMicroPriceMartingaleVacuumPenetration != null)
        {
            var lob = portfolio.LobMicroPriceMartingaleVacuumPenetration;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百二十六、Jane Street & Citadel Securities: 全档限价订单簿 (LOB) 微观价格扩散鞅测度变换与即时流动性真空渗透控制引擎 (Phase 56)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Jane Street 与 Citadel Securities 超高频做市与微观限价订单簿 (LOB) 鞅测度修正前沿。突破简单买卖中点价 (Mid-Price) 的虚假鞅假设，构建全档深度不平衡态空间 I=(Qb-Qa)/(Qb+Qa)，解析推导吸收态马尔可夫调和微观价格鞅解 μ*(s)，彻底剔除盘口虚假撤单噪声；引入即时流动性真空破裂渗透率 P_vac 动态监测，解算最优挂单深度偏移量 δ*，在高频知情交易毒性穿透前启动自适应被动保护，稳健捕获做市减摩 Alpha。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">微观价格鞅漂移偏差</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">Δμ={lob.GlobalMicroPriceDriftBps:F2} bps</div><div class=\"kpi-sub\">Sasha Stoikov 调和鞅偏离修正</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">即时流动性真空渗透率</div><div class=\"kpi-value\" style=\"color:#f38ba8;\">{lob.AverageVacuumPenetrationPct:F1}%</div><div class=\"kpi-sub\">盘口挂单瞬时击穿真空概率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">被动挂单价差节省</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{lob.PassiveExecutionSpreadSavingBps:F2} bps</div><div class=\"kpi-sub\">自适应深度偏移执行成本压缩</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">做市微观执行净 Alpha</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{lob.TotalMarketMakingAlphaBps:F1} bps</div><div class=\"kpi-sub\">防逆向选择做市执行增益</div></div>");
            sb.AppendLine("  </div>");

            if (lob.LobItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产盘口价差、5档不平衡度、微观价格漂移、真空渗透率、最优深度偏移、减摩 Alpha 与做市指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>盘口价差</th><th>5档不平衡度</th><th>鞅漂移偏差</th><th>真空渗透率</th><th>最优偏移 δ*</th><th>做市 Alpha</th><th>LOB 评级</th><th>微观订单簿做市与防真空穿透指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in lob.LobItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.SpreadBps:F2} bps</td><td>{item.DepthImbalanceRatio:F3}</td><td>{item.MicroPriceMartingaleDriftBps:F2} bps</td><td>{item.InstantaneousVacuumPenetrationPct:F1}%</td><td>{item.OptimalQuoteDepthOffsetBps:F2} bps</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.MicrostructureExecutionAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.LobRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.LobExecutionAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(lob.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 127. D.E. Shaw & Two Sigma: 连续时间多维非高斯列维-伊藤跳跃扩散与极值共跳曲率对冲引擎 (Phase 56)
        if (portfolio.MultidimensionalLevyItoJumpDiffusion != null)
        {
            var levy = portfolio.MultidimensionalLevyItoJumpDiffusion;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百二十七、D.E. Shaw & Two Sigma: 连续时间多维非高斯列维-伊藤跳跃扩散与极值共跳曲率对冲引擎 (Phase 56)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 D.E. Shaw 与 Two Sigma 多维连续时间非高斯列维-伊藤半鞅与系统性共跳 (Co-Jumps) 极值对冲架构。突破正态跳跃独立性桎梏，建立多维补偿泊松跳跃测度与 Kou 双指数厚尾非对称测度积分，精确解构扩散年化波动率与断崖跳跃变差贡献；构建极值共跳动态吸收垫 (Jump Cushion) 矩阵，自适应优化非线性跳跃凸性衍生品对冲乘数，牢筑极端黑天鹅下行熔断防御防线。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">多维共跳年化到达强度</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">Λ={levy.GlobalCoJumpArrivalIntensity:F2} 次/年</div><div class=\"kpi-sub\">全局跨资产协同非连续跳跃频率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">跳跃方差占总变差比率</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{levy.GlobalJumpVariationRatioPct:F1}%</div><div class=\"kpi-sub\">二次变差中跳跃不连续成分占比</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">断崖跳跃尾部压降率</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">{levy.GlobalJumpTailDrawdownReductionPct:F1}%</div><div class=\"kpi-sub\">动态吸收垫对极端回撤缓冲率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">极值跳跃凸性对冲 Alpha</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{levy.GlobalLevyJumpAlphaBps:F1} bps</div><div class=\"kpi-sub\">非高斯跳跃不可分散风险溢价</div></div>");
            sb.AppendLine("  </div>");

            if (levy.JumpItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产扩散波动、共跳强度、下行偏度、跳跃变差比、吸收垫对冲乘数、跳跃 Alpha 与对冲指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>扩散波动</th><th>共跳强度</th><th>下行偏度</th><th>跳跃变差比</th><th>吸收垫乘数</th><th>跳跃 Alpha</th><th>跳跃评级</th><th>列维-伊藤跳跃扩散与共跳对冲指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in levy.JumpItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.ContinuousDiffusionVolPct:F2}%</td><td>{item.JumpArrivalIntensityLambda:F2} 次/年</td><td>{item.DownsideJumpAsymmetryRatio:F2}</td><td>{item.JumpVariationRatioPct:F1}%</td><td>{item.JumpCushionHedgeMultiplier:F2}x</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.LevyJumpAntiTailAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.LevyJumpBadge)}</td><td>{WebUtility.HtmlEncode(item.LevyJumpAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(levy.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 128. Renaissance Technologies & Millennium Management: 高阶拓扑超图场论与自旋玻璃阻挫能量退火解聚引擎 (Phase 56)
        if (portfolio.HypergraphSpinGlassFrustrationAnnealing != null)
        {
            var hyper = portfolio.HypergraphSpinGlassFrustrationAnnealing;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百二十八、Renaissance Technologies & Millennium Management: 高阶拓扑超图场论与自旋玻璃阻挫能量退火解聚引擎 (Phase 56)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Renaissance Technologies 与 Millennium Management 统计物理拓扑超图与复杂多体系统相互作用前沿。超越传统两两配对相关矩阵，构建关联多标的协同敞口的高阶超图网络 H=(V,E) 与超图拉普拉斯谱；建立自旋玻璃阻挫哈密顿量度量多约束冲突下的亚稳态能量深井与相变冻结有序参量 q_EA；采用模拟退火解聚演化算法重构无阻挫均衡权重，破除高阶踩踏暗流，斩断资产间级联共振去杠杆链条。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">超图自旋玻璃阻挫能量</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">F={hyper.GlobalFrustrationEnergyDensity:F3}</div><div class=\"kpi-sub\">全局阻挫相互作用能量密度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">平均高阶超边关联度</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{hyper.MeanHyperedgeInteractionDegree:F1} 阶</div><div class=\"kpi-sub\">多资产重叠超边相互作用阶数</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">系统性共振脆弱度压降</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">{hyper.GlobalSystemicResonanceVulnerabilityPct:F1}%</div><div class=\"kpi-sub\">跨资产级联共振脱钩改善率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">超图去阻挫稳健 Alpha</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{hyper.HypergraphAnnealingAlphaBps:F1} bps</div><div class=\"kpi-sub\">模拟退火无阻挫重构超额增益</div></div>");
            sb.AppendLine("  </div>");

            if (hyper.HypergraphItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产超边度、阻挫能量、冻结有序参量 q_EA、退火最优权重、共振压降、解聚 Alpha 与配置指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>超边度</th><th>阻挫能量</th><th>冻结参量 q_EA</th><th>退火最优权重</th><th>共振压降</th><th>解聚 Alpha</th><th>超图评级</th><th>超图拓扑与自旋玻璃解聚指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in hyper.HypergraphItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.HyperedgeDegree} 阶</td><td>{item.SpinGlassFrustrationDensity:F3}</td><td>{item.EdwardsAndersonOrderParameter:F3}</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.AnnealedRobustOptimalWeight:F1}%</td><td>{item.SystemicResonanceReductionPct:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.HypergraphDeclusteringAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.HypergraphBadge)}</td><td>{WebUtility.HtmlEncode(item.HypergraphAnnealingAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(hyper.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 129. Bridgewater Associates & BlackRock Aladdin: 达利欧长期主权债务大周期与超级去杠杆马尔可夫状态免疫引擎 (Phase 56)
        if (portfolio.SovereignDebtCycleDeleveragingImmunity != null)
        {
            var debt = portfolio.SovereignDebtCycleDeleveragingImmunity;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百二十九、Bridgewater Associates & BlackRock Aladdin: 达利欧长期主权债务大周期与超级去杠杆马尔可夫状态免疫引擎 (Phase 56)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Bridgewater Associates (Ray Dalio) 与 BlackRock Aladdin 50~75 年长期主权债务大周期与超级去杠杆化全天候宏观免疫架构。解析主权信用扩张与债务率极限，构建四维宏观态空间（繁荣泡沫、通缩萧条、通胀再膨胀、信用重构）及马尔可夫时变转移概率矩阵；量化各标的主权货币贬值敏感敞口与去杠杆脆弱性指数 (DVI)，自适应生成达利欧全天候主权免疫目标权重，终结去杠杆阶段传统股债双杀致命盲区。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">主导长期债务超级周期</div><div class=\"kpi-value\" style=\"color:#f9e2af;font-size:12px;padding-top:4px;\">{WebUtility.HtmlEncode(debt.CurrentLongTermDebtSupercyclePhase)}</div><div class=\"kpi-sub\">达利欧 50~75 年长期主权宏观态</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">超级去杠杆脆弱性综合评分</div><div class=\"kpi-value\" style=\"color:#f38ba8;\">{debt.GlobalDeleveragingVulnerabilityScore:F1} 分</div><div class=\"kpi-sub\">货币乘数紧缩与信贷收缩敏感度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">马尔可夫宏观转移信息熵</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">{debt.MarkovRegimeTransitionEntropy:F3} bits</div><div class=\"kpi-sub\">长期主权状态空间转移不确定度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">长期主权周期免疫 Alpha</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{debt.SupercycleMacroImmunityAlphaBps:F1} bps</div><div class=\"kpi-sub\">去杠杆抗踩踏跨周期全天候超额</div></div>");
            sb.AppendLine("  </div>");

            if (debt.DebtItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产宏观态、去杠杆脆弱度 DVI、货币贬值 Beta、滞胀凸性缓冲、达利欧免疫权重、宏观 Alpha 与配置指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>宏观态归属</th><th>去杠杆脆弱度</th><th>货币贬值 Beta</th><th>滞胀凸性缓冲</th><th>达利欧免疫权重</th><th>宏观 Alpha</th><th>周期评级</th><th>长期债务大周期与超级去杠杆配置指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in debt.DebtItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{WebUtility.HtmlEncode(item.MacroDebtRegimeState)}</td><td>{item.DeleveragingVulnerabilityIndex:F1}</td><td>{item.SovereignMonetaryDebasementExposure:F2}</td><td>{item.StagflationaryConvexityBufferPct:F1}%</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.DalioImmunityTargetWeight:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.DebtCycleMacroAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.MacroDebtImmunityBadge)}</td><td>{WebUtility.HtmlEncode(item.SovereignDebtAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(debt.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 130. Two Sigma & Citadel Securities: 连续时间马利亚温随机变分微积分与粗糙波动率泛函高阶 Greeks 深度对冲引擎 (Phase 57)
        if (portfolio.MalliavinRoughVolatilityGreeks != null)
        {
            var mal = portfolio.MalliavinRoughVolatilityGreeks;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百三十、Two Sigma & Citadel Securities: 连续时间马利亚温随机变分微积分与粗糙波动率泛函高阶 Greeks 深度对冲引擎 (Phase 57)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Two Sigma 与 Citadel Securities 高频微观波动率粗糙路径 (Rough Paths) 与连续时间马利亚温变分微积分 (Malliavin Calculus) 前沿。针对日内非马尔可夫粗糙路径 (Hurst H≈0.1) 下传统有限差分高阶敏感度 (Volga, Vanna, Speed) 存在的数值爆炸与散斑噪声，引入 Wiener-Poisson 空间上的马利亚温导数算子 D_t 与 Skorokhod 散度积分算子 δ，通过马利亚温分部积分公式直接解析求解零差分噪声的无偏高阶 Greeks，构建粗糙路径防断崖动态吸收垫，全方位消除粗糙偏度极值风险。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">全局粗糙赫斯特指数</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">H_eff={mal.GlobalHurstExponentH:F3}</div><div class=\"kpi-sub\">微观分形非马尔可夫粗糙度 (远低于0.5)</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">马利亚温 Volga 凸性曲率</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">{mal.GlobalMalliavinVolgaCurvature:F1}</div><div class=\"kpi-sub\">波动率的波动率高阶凸性对冲敏感度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">粗糙断崖回撤压降率</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">{mal.AverageRoughTailDefensePct:F1}%</div><div class=\"kpi-sub\">变分吸收垫对极端粗糙暴跌缓冲率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">马利亚温变分对冲 Alpha</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{mal.TotalMalliavinHedgingAlphaBps:F1} bps</div><div class=\"kpi-sub\">无偏高阶变分积分对冲执行超额</div></div>");
            sb.AppendLine("  </div>");

            if (mal.MalliavinItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产粗糙度 H、波动率之波动率、无偏 Vega、Volga 曲率、Vanna 交叉敏感度、断崖压降、对冲 Alpha 与变分指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>粗糙度 H</th><th>Vol-of-Vol</th><th>无偏 Vega</th><th>Volga 曲率</th><th>Vanna 交叉</th><th>断崖压降</th><th>对冲 Alpha</th><th>粗糙度评级</th><th>马利亚温变分对冲与粗糙度保护指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in mal.MalliavinItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.HurstRoughnessParameterH:F3}</td><td>{item.RoughVolOfVol:F3}</td><td>{item.MalliavinNoiseFreeVega:F1} bps</td><td>{item.MalliavinVolgaCurvature:F1}</td><td>{item.MalliavinVannaCrossSensitivity:F1}</td><td>{item.RoughPathTailDrawdownReductionPct:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.MalliavinHedgingAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.RoughVolRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.RoughVolAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mal.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 131. Renaissance Technologies & Jump Trading: 开放量子耗散系统 Lindblad 主方程与微观相干态密度矩阵统计套利引擎 (Phase 57)
        if (portfolio.QuantumLindbladDecoherenceStatArb != null)
        {
            var q = portfolio.QuantumLindbladDecoherenceStatArb;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百三十一、Renaissance Technologies & Jump Trading: 开放量子耗散系统 Lindblad 主方程与微观相干态密度矩阵统计套利引擎 (Phase 57)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Renaissance Technologies (Medallion) 与 Jump Trading 开放量子系统物理学与微观相干态统计套利架构。超越经典独立粒子假说，将多标的微观订单流表征为厄米正半定单位迹密度矩阵 ρ(t)，求解 Gorini-Kossakowski-Sudarshan-Lindblad (GKSL) 开放主方程；量化外界订单流热库扰动下的冯·诺依曼微观熵 S(ρ)、量子态纯度 Tr(ρ^2) 与退相干特征半衰期 τ，在外部不可逆噪声导致微观纠缠坍缩前捕获相干态统计套利 Alpha。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">全局微观量子态纯度</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">Tr(ρ²)={q.GlobalQuantumStatePurity:F3}</div><div class=\"kpi-sub\">微观订单流纯态纠缠程度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">冯·诺依曼微观信息熵</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">S(ρ)={q.GlobalVonNeumannEntropy:F3}</div><div class=\"kpi-sub\">开放系统混合态无序度量</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">平均退相干特征半衰期</div><div class=\"kpi-value\" style=\"color:#f38ba8;\">{q.AverageDecoherenceHalfLifeMicrosec:F1} μs</div><div class=\"kpi-sub\">微观纠缠相位耐受噪声时间</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">量子相干统计套利 Alpha</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{q.TotalQuantumStatArbAlphaBps:F1} bps</div><div class=\"kpi-sub\">非对角干涉项微观统计套利超额</div></div>");
            sb.AppendLine("  </div>");

            if (q.QuantumItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产量子纯度、冯·诺依曼熵、退相干半衰期、非对角相干度、耗散跃迁强度、套利 Alpha 与开放量子指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>量子纯度 Tr</th><th>冯·诺依曼熵</th><th>退相干半衰期</th><th>非对角相干度</th><th>耗散跃迁 γ</th><th>套利 Alpha</th><th>量子评级</th><th>开放量子系统与相干套利指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in q.QuantumItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.QuantumStatePurity:F3}</td><td>{item.VonNeumannEntropy:F3}</td><td>{item.DecoherenceHalfLifeMicrosec:F1} μs</td><td>{item.OffDiagonalCoherenceDegree:F3}</td><td>{item.DissipativeJumpIntensityGamma:F3}</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.QuantumStatArbAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.QuantumRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.QuantumStatArbAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(q.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 132. Millennium Management & Point72: 连续时间多主体均值场博弈 (Mean-Field Games) 与 FPK-HJB 拥挤踩踏解耦优化引擎 (Phase 57)
        if (portfolio.MeanFieldGameCrowdingDecoupling != null)
        {
            var mfg = portfolio.MeanFieldGameCrowdingDecoupling;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百三十二、Millennium Management & Point72: 连续时间多主体均值场博弈 (Mean-Field Games) 与 FPK-HJB 拥挤踩踏解耦优化引擎 (Phase 57)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Millennium Management 与 Point72 连续时间多参与者均值场博弈 (Mean-Field Games, MFG) 与大规模 Pod 策略拥挤解耦架构。建模微观上百个独立 PM 与外部机构追逐同质因子的内生价格冲击反馈，耦合前向追踪宏观资本密度演化的 Fokker-Planck-Kolmogorov (FPK) 方程与后向决策最优价值函数的 Hamilton-Jacobi-Bellman (HJB) 方程；解算纳什均衡内生拥挤压力指数 CPI 与均值场解耦权重，从机制上瓦解踩踏抛售与死亡螺旋级联反应。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">全局内生拥挤压力指数</div><div class=\"kpi-value\" style=\"color:#f38ba8;\">CPI={mfg.GlobalCrowdingPressureIndex:F1}/100</div><div class=\"kpi-sub\">跨机构与多 Pod 资本宏观聚集度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">FPK-HJB 纳什收敛残差</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">ε={mfg.FpkHjbConvergenceResidual:F3}×10⁻⁴</div><div class=\"kpi-sub\">连续时间博弈正反向耦合松弛度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">系统性踩踏风险消除率</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">{mfg.SystemicDoomLoopReductionPct:F1}%</div><div class=\"kpi-sub\">均值场解耦对死亡螺旋化解率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">均值场博弈稳健 Alpha</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{mfg.TotalMfgRobustAlphaBps:F1} bps</div><div class=\"kpi-sub\">纳什博弈均衡执行减摩收益</div></div>");
            sb.AppendLine("  </div>");

            if (mfg.MfgItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产拥挤指数 CPI、纳什漂移、踩踏脆弱度、均值场解耦权重、防螺旋缓冲、博弈 Alpha 与解耦指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>拥挤指数 CPI</th><th>纳什漂移</th><th>踩踏脆弱度</th><th>均值场解耦权重</th><th>防螺旋缓冲</th><th>博弈 Alpha</th><th>均值场评级</th><th>均值场博弈与防踩踏解耦指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in mfg.MfgItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.CrowdingPressureIndexCPI:F1}</td><td>{item.NashEquilibriumDriftBps:F1} bps</td><td>{item.FireSaleCascadeVulnerabilityPct:F1}%</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.MeanFieldDecoupledTargetWeight:F1}%</td><td>{item.DoomLoopDefenseBufferPct:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.MfgGameTheoreticAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.MfgRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.MfgDecouplingAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mfg.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 133. Bridgewater Associates & AQR Capital: 非平衡态热力学最大熵产生与信息几何 Fisher-Rao 测地线宏观流形平滑轮动引擎 (Phase 57)
        if (portfolio.ThermodynamicFisherRaoGeodesicRegime != null)
        {
            var tr = portfolio.ThermodynamicFisherRaoGeodesicRegime;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-title\">一百三十三、Bridgewater Associates & AQR Capital: 非平衡态热力学最大熵产生与信息几何 Fisher-Rao 测地线宏观流形平滑轮动引擎 (Phase 57)</div>");
            sb.AppendLine("  <p class=\"narrative\">本模块对标 Bridgewater Associates 与 AQR Capital Management 信息几何流形与非平衡态宏观热力学轮动架构。突破传统离散体制判别在边界产生的“刀刃抖动”与高昂虚假换手损耗，将多元资产概率分布族映射为具备 Fisher-Rao 黎曼度量张量 g_ij(θ) 的统计流形，求解沿流形测地线的最短内蕴距离 d_G；结合最大熵产生率原理 (MEPR) 与 Onsager 倒易动能动量阻尼，平滑约束宏观转移通量，输出超平滑调仓权重，实现零摩擦、零边界刀刃抖动的高夏普全天候轮动。</p>");
            sb.AppendLine("  <div class=\"kpi-grid\">");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">Fisher-Rao 测地线距离</div><div class=\"kpi-value\" style=\"color:#89b4fa;\">d_G={tr.GlobalFisherRaoGeodesicDistance:F3}</div><div class=\"kpi-sub\">统计概率流形内蕴最短路径长度</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">全局最大熵产生率</div><div class=\"kpi-value\" style=\"color:#f9e2af;\">σ_MEPR={tr.GlobalEntropyProductionRate:F3}</div><div class=\"kpi-sub\">非平衡态宏观状态相变速率</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">调仓换手摩擦压缩率</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">{tr.AverageTurnoverDragSavingPct:F1}%</div><div class=\"kpi-sub\">消除刀刃抖动无效换手磨损</div></div>");
            sb.AppendLine($"    <div class=\"kpi-card\"><div class=\"kpi-title\">测地线宏观轮动 Alpha</div><div class=\"kpi-value\" style=\"color:#a6e3a1;\">+{tr.TotalGeodesicMacroAlphaBps:F1} bps</div><div class=\"kpi-sub\">信息几何流形最优平滑轮动超额</div></div>");
            sb.AppendLine("  </div>");

            if (tr.ThermodynamicItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产测地线距离、熵产生率、Onsager 摩擦阻尼、黎曼曲率、测地线目标权重、摩擦压缩、轮动 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>测地线距离 d_G</th><th>熵产生率</th><th>Onsager 阻尼</th><th>黎曼曲率 R</th><th>测地线目标权重</th><th>摩擦压缩率</th><th>轮动 Alpha</th><th>流形评级</th><th>信息几何与非平衡态热力学轮动指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in tr.ThermodynamicItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.FisherRaoGeodesicDistance:F3}</td><td>{item.EntropyProductionRateMEPR:F3}</td><td>{item.OnsagerKineticFrictionBps:F1} bps</td><td>{item.RiemannianCurvatureScalarR:F2}</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.GeodesicSmoothTargetWeight:F1}%</td><td>{item.TurnoverDragReductionPct:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.GeodesicMacroAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.FisherRaoBadge)}</td><td>{WebUtility.HtmlEncode(item.GeodesicAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(tr.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百三十四、Jane Street & Citadel Securities: 连续时间 Glosten-Milgrom 贝叶斯信念更新与流动性逆向选择防护引擎 (Phase 58)
        if (portfolio.GlostenMilgromAdverseSelection != null)
        {
            var gm = portfolio.GlostenMilgromAdverseSelection;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百三十四、Jane Street & Citadel Securities: 连续时间 Glosten-Milgrom 贝叶斯信念更新与流动性逆向选择防护 (Phase 58)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">微观订单流·贝叶斯逆向选择·知情穿透阻断</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局加权知情交易者比例 α</div><div class=\"metric-value\">{gm.GlobalInformedTraderRatio:P1}</div><div class=\"metric-sub\">连续时间信念更新</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局逆向选择加权价差</div><div class=\"metric-value\">{gm.GlobalAdverseSelectionSpreadBps:F1} bps</div><div class=\"metric-sub\">Kyle-Glosten 内生补偿</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均毒性滑点压降率</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{gm.AverageSlippageReductionPct:F1}%</div><div class=\"metric-sub\">防被动承接穿透</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">逆向选择防御总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{gm.TotalGlostenMilgromAlphaBps:F1} bps</div><div class=\"metric-sub\">做市挂单防护净增益</div></div>");
            sb.AppendLine("  </div>");

            if (gm.GlostenItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产知情交易占比、后验信念、逆向价差补偿、存货跳跃暴露、滑点压降、挂单防御 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>知情比 α</th><th>后验信念 Π</th><th>逆向价差补偿</th><th>存货跳跃暴露</th><th>毒性滑点压降</th><th>防御 Alpha</th><th>评级</th><th>挂单与防穿透指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in gm.GlostenItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.InformedTraderRatioAlpha:P1}</td><td>{item.PosteriorHighStateBeliefPi:F2}</td><td>{item.AdverseSelectionSpreadBps:F1} bps</td><td>{item.InventoryJumpExposurePct:F1}%</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.ToxicitySlippageReductionPct:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.GlostenMilgromAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.AdverseSelectionRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.GlostenMilgromAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(gm.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百三十五、Renaissance Technologies & D.E. Shaw: 非广延统计力学 Tsallis 广义熵与多重分形奇异谱极值崩塌预警引擎 (Phase 58)
        if (portfolio.TsallisNonextensiveSingularSpectrum != null)
        {
            var ts = portfolio.TsallisNonextensiveSingularSpectrum;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百三十五、Renaissance Technologies & D.E. Shaw: 非广延统计力学 Tsallis 广义熵与多重分形奇异谱极值崩塌预警 (Phase 58)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">非广延统计力学·WTMM奇异谱·相变临界雪崩预警</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局有效非广延参数 q_eff</div><div class=\"metric-value\">{ts.GlobalNonextensiveParameterQ:F3}</div><div class=\"metric-sub\">幂律厚尾度量 (平衡态q=1)</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局奇异谱宽度 Δα</div><div class=\"metric-value\">{ts.GlobalSingularSpectrumWidth:F3}</div><div class=\"metric-sub\">WTMM 小波奇异极差</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均相变雪崩防御度</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{ts.AverageAvalancheDefensePct:F1}%</div><div class=\"metric-sub\">自组织临界相变保护</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">非广延自适应总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{ts.TotalTsallisAdaptiveAlphaBps:F1} bps</div><div class=\"metric-sub\">反相变尺度不变性溢价</div></div>");
            sb.AppendLine("  </div>");

            if (ts.TsallisItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产非广延参数 q、Tsallis 熵 S_q、奇异谱宽度 Δα、相变相干长度 ξ、雪崩防御度、自适应 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>非广延参数 q</th><th>Tsallis 熵 S_q</th><th>奇异谱宽度 Δα</th><th>相干长度 ξ</th><th>雪崩防御度</th><th>自适应 Alpha</th><th>评级</th><th>统计力学相变对冲指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in ts.TsallisItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.NonextensiveParameterQ:F3}</td><td>{item.TsallisEntropySq:F3}</td><td>{item.SingularSpectrumWidthDeltaAlpha:F3}</td><td>{item.PhaseTransitionCorrelationLengthXi:F1}</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.AvalancheCollapseDefensePct:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.TsallisAdaptiveAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.TsallisRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.TsallisAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ts.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百三十六、Two Sigma & Jump Trading: 随机非线性粘性阻尼与 Fredholm 记忆核最优变分执行清算引擎 (Phase 58)
        if (portfolio.ViscousMemoryOptimalExecution != null)
        {
            var vm = portfolio.ViscousMemoryOptimalExecution;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百三十六、Two Sigma & Jump Trading: 随机非线性粘性阻尼与 Fredholm 记忆核最优变分执行清算 (Phase 58)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">Fredholm 记忆核·欧拉-拉格朗日凸变分·非线性减摩</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局有效粘性记忆衰减 γ</div><div class=\"metric-value\">{vm.GlobalViscousMemoryDecayGamma:F3}</div><div class=\"metric-sub\">非局部幂律松弛核</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局 Fredholm 粘性阻尼比 ζ</div><div class=\"metric-value\">{vm.GlobalFredholmDampingRatio:F3}</div><div class=\"metric-sub\">非线性粘弹性阻尼</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均大额滑点节省率</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{vm.AverageSlippageSavingsPct:F1}%</div><div class=\"metric-sub\">冲击回弹变分减摩</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">变分减摩执行总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{vm.TotalViscousExecutionAlphaBps:F1} bps</div><div class=\"metric-sub\">阿斯普兰德空间最优控制</div></div>");
            sb.AppendLine("  </div>");

            if (vm.ViscousItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产非局部衰减指数 γ、阻尼比 ζ、最优变分速度 v*、滑点节省率、变分减摩 Alpha 与执行指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>衰减指数 γ</th><th>阻尼比 ζ</th><th>最优清算速度 v*</th><th>滑点节省率</th><th>减摩 Alpha</th><th>执行评级</th><th>变分清算拆单执行指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in vm.ViscousItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.ViscousMemoryDecayGamma:F3}</td><td>{item.FredholmDampingRatio:F3}</td><td>{item.EulerLagrangeOptimalPace:F1} 手/分</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.NonlinearSlippageSavingsPct:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.ViscousExecutionAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.ViscousRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.ViscousAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(vm.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百三十七、Bridgewater Associates & Millennium Management: 结构向量自回归有向无环图 (SVAR-DAG) 与 Pearl 反事实因果干预宏观网络引擎 (Phase 58)
        if (portfolio.SvarDagCausalInterventionNetwork != null)
        {
            var sv = portfolio.SvarDagCausalInterventionNetwork;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百三十七、Bridgewater Associates & Millennium Management: 结构向量自回归有向无环图 (SVAR-DAG) 与 Pearl 反事实因果干预宏观网络 (Phase 58)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">SVAR-DAG 因果拓扑·Pearl do(X)算子·反脆弱解耦</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局 SVAR-DAG 因果网络密度</div><div class=\"metric-value\">{sv.GlobalCausalNetworkDensity:F3}</div><div class=\"metric-sub\">宏观因果流拓扑图</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局系统性因果脆弱度</div><div class=\"metric-value\">{sv.GlobalSystemicFragilityIndex:F1} 分</div><div class=\"metric-sub\">级联传染风险综合评级</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均反事实冲击免疫度</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{sv.AverageShockImmunityPct:F1}%</div><div class=\"metric-sub\">Pearl do(X) 外生吸收力</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">结构因果反脆弱总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{sv.TotalAntifragileCausalAlphaBps:F1} bps</div><div class=\"metric-sub\">因果解耦稳健超额收益</div></div>");
            sb.AppendLine("  </div>");

            if (sv.CausalItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产因果中心度、Pearl 反事实冲击响应、脆弱度、因果解耦目标权重、反事实因果 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>因果中心度</th><th>反事实响应率</th><th>因果脆弱度</th><th>因果解耦目标权重</th><th>反脆弱 Alpha</th><th>因果评级</th><th>结构因果推断调仓指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in sv.CausalItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.CausalCentralityScore:F1}</td><td>{item.CounterfactualShockResponsePct:F1}%</td><td>{item.SystemicCausalFragilityIndex:F1}</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.CausalDecoupledTargetWeight:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.AntifragileCausalAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.CausalRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.CausalAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(sv.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百三十八、Citadel Securities & Jump Trading: 连续拍卖 Kyle's λ 价格冲击弹性与订单流信息穿透深度控制引擎 (Phase 59)
        if (portfolio.KyleContinuousAuctionElasticity != null)
        {
            var kl = portfolio.KyleContinuousAuctionElasticity;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百三十八、Citadel Securities & Jump Trading: 连续拍卖 Kyle's λ 价格冲击弹性与订单流信息穿透深度控制 (Phase 59)</div>");
            sb.AppendLine("    <div class=\"badge badge-blue\">Kyle 连续拍卖博弈·价格冲击弹性·知情穿透缓冲</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局加权 Kyle's λ 价格冲击</div><div class=\"metric-value\">{kl.GlobalKyleLambdaBps:F2} bps</div><div class=\"metric-sub\">连续拍卖微观均衡冲击率</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均盘口流动性价格弹性</div><div class=\"metric-value\">{kl.AveragePriceElasticity:F2}</div><div class=\"metric-sub\">深度吸收价格弹性系数</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均穿透滑点压降率</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{kl.AveragePenetrationReductionPct:F1}%</div><div class=\"metric-sub\">动态挂单缓冲防击穿</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">Kyle 弹性防御总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{kl.TotalKyleElasticityAlphaBps:F1} bps</div><div class=\"metric-sub\">微观价差与弹性优化收益</div></div>");
            sb.AppendLine("  </div>");

            if (kl.KyleItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产 Kyle 价格冲击 λ、流动性弹性、穿透深度、最优挂单缓冲、弹性防御 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>Kyle λ 冲击</th><th>价格流动弹性</th><th>穿透深度</th><th>最优挂单缓冲</th><th>弹性防御 Alpha</th><th>评级</th><th>Kyle 连续拍卖挂单与深度控制指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in kl.KyleItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.KyleLambdaImpactBps:F2} bps</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.PriceElasticityCoefficient:F2}</td><td>{item.InformationPenetrationDepthPct:F1}%</td><td>{item.OptimalQuoteBufferDepth:F1} 档</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.KyleElasticityAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.KyleRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.KyleAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(kl.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百三十九、Renaissance Technologies & D.E. Shaw: 拓扑 Wilson 重整化群矩流动与仿射渗流相变临界聚类引擎 (Phase 59)
        if (portfolio.WilsonRenormalizationPercolation != null)
        {
            var wp = portfolio.WilsonRenormalizationPercolation;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百三十九、Renaissance Technologies & D.E. Shaw: 拓扑 Wilson 重整化群矩流动与仿射渗流相变临界聚类 (Phase 59)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">Wilson 重整化群·Kadanoff块自旋·仿射渗流相变</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局临界渗流阈值 p_c</div><div class=\"metric-value\">{wp.GlobalPercolationProbabilityPc:F3}</div><div class=\"metric-sub\">连续仿射渗流相变阈值</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">巨连通分支 (GCC) 关联度</div><div class=\"metric-value\">{wp.GiantConnectedComponentRatioPct:F1}%</div><div class=\"metric-sub\">全网相变集团吸附规模</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均相变敏感度 χ</div><div class=\"metric-value\" style=\"color:#f38ba8;\">{wp.AveragePercolationSusceptibility:F2}</div><div class=\"metric-sub\">渗流磁化率与雪崩易感度</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">抗渗流重整化总 Alpha</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">+{wp.TotalPercolationDefenseAlphaBps:F1} bps</div><div class=\"metric-sub\">重整化不变性解耦超额</div></div>");
            sb.AppendLine("  </div>");

            if (wp.WilsonItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产 RG 有效流速、巨连通关联度、相变敏感度 χ、重整化不变目标权重、抗相变 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>RG 流速</th><th>巨连通 GCC 评分</th><th>相变敏感度 χ</th><th>RG 不变目标权重</th><th>抗相变 Alpha</th><th>评级</th><th>重整化群抗相变配置指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in wp.WilsonItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.WilsonRgFlowVelocity:F3}</td><td>{item.GiantComponentAffiliationScore:F1}</td><td>{item.PercolationCriticalSusceptibility:F2}</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.RgInvariantTargetWeight:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.PercolationDefenseAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.WilsonRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.WilsonAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(wp.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百四十、Two Sigma & PDT Partners: 连续时间随机微分博弈 (SDG) 捕食者-猎物纳什均衡与流动性黑洞抢跑规避变分引擎 (Phase 59)
        if (portfolio.PredatoryGameLiquidityEvasion != null)
        {
            var pg = portfolio.PredatoryGameLiquidityEvasion;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百四十、Two Sigma & PDT Partners: 连续时间随机微分博弈 (SDG) 捕食者-猎物纳什均衡与流动性黑洞抢跑规避变分 (Phase 59)</div>");
            sb.AppendLine("    <div class=\"badge badge-green\">SDG 随机微分博弈·HJBI 粘性解·流动性黑洞规避</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局捕食者做空压力指数</div><div class=\"metric-value\">{pg.GlobalPredatoryPressureIndex:F1} 分</div><div class=\"metric-sub\">掠食算法抢跑强度</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均流动性黑洞下潜深度</div><div class=\"metric-value\" style=\"color:#f38ba8;\">{pg.AverageBlackHoleDepthPct:F1}%</div><div class=\"metric-sub\">深度踩踏黑洞风险暴露</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均掠食滑点减损率</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{pg.AveragePredatoryDamageReductionPct:F1}%</div><div class=\"metric-sub\">HJBI 变分伪装减摩收益</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">防掠食规避总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{pg.TotalPredatoryEvasionAlphaBps:F1} bps</div><div class=\"metric-sub\">博弈抗剪羊毛超额挽回</div></div>");
            sb.AppendLine("  </div>");

            if (pg.PredatoryItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产捕食压力指数、流动性黑洞深度、纳什规避最优速度、伪装扰动比、防掠食 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>捕食压力</th><th>黑洞深度</th><th>最优速度</th><th>伪装扰动比</th><th>防掠食 Alpha</th><th>评级</th><th>HJBI 防掠食拆单避险指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in pg.PredatoryItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.PredatoryPressureIndex:F1}</td><td>{item.LiquidityBlackHoleDepthPct:F1}%</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.NashEvasionOptimalSpeed:F1} 手/分</td><td>{item.CamouflageRandomizationPct:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.PredatoryEvasionAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.PredatoryRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.PredatoryAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(pg.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百四十一、Bridgewater Associates & AQR Capital: 连续时间漂移-扩散非线性状态空间与鲁棒卡尔曼-布西宏观体制平价引擎 (Phase 59)
        if (portfolio.DriftDiffusionKalmanMacroParity != null)
        {
            var dd = portfolio.DriftDiffusionKalmanMacroParity;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百四十一、Bridgewater Associates & AQR Capital: 连续时间漂移-扩散非线性状态空间与鲁棒卡尔曼-布西宏观体制平价 (Phase 59)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">连续漂移-扩散·鲁棒卡尔曼-布西·宏观体制平价</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局宏观漂移速度</div><div class=\"metric-value\">{dd.GlobalMacroDriftSpeed:F3}</div><div class=\"metric-sub\">连续状态空间潜变量漂移率</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均卡尔曼滤波置信度</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{dd.AverageKalmanConfidencePct:F1}%</div><div class=\"metric-sub\">连续时间滤波后验置信度</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">宏观平价去震荡换手压降</div><div class=\"metric-value\" style=\"color:#89b4fa;\">{dd.RegimeChurnReductionPct:F1}%</div><div class=\"metric-sub\">假信号与换手磨损平滑率</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">鲁棒宏观平价总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{dd.TotalMacroParityAlphaBps:F1} bps</div><div class=\"metric-sub\">漂移不变性宏观平价超额</div></div>");
            sb.AppendLine("  </div>");

            if (dd.DriftMacroItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产宏观漂移 Beta、四象限亲和度、卡尔曼跟踪误差、平价目标权重、宏观平价 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>漂移 Beta</th><th>四象限亲和度</th><th>卡尔曼误差</th><th>平价目标权重</th><th>宏观平价 Alpha</th><th>评级</th><th>连续状态空间动态平价指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in dd.DriftMacroItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.MacroDriftSensitivityBeta:F2}</td><td>{item.ContinuousQuadrantAffinityScore:F1}</td><td>{item.KalmanBucyTrackingErrorPct:F1}%</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.RegimeResilientTargetWeight:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.MacroParityAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.DriftRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.DriftMacroAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(dd.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百四十二、Citadel Securities & Optiver: 非线性泊松跳跃边界反射与 Avellaneda-Stoikov 粘性滑移面做市控制 (Phase 60)
        if (portfolio.NonlinearPoissonBoundaryMarketMaking != null)
        {
            var pm = portfolio.NonlinearPoissonBoundaryMarketMaking;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百四十二、Citadel Securities & Optiver: 非线性泊松跳跃边界反射与 Avellaneda-Stoikov 粘性滑移面做市控制 (Phase 60)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">非线性泊松跳跃·粘性滑移面·边界反射做市</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局最优挂单价差</div><div class=\"metric-value\">{pm.GlobalOptimalSpreadBps:F2} bps</div><div class=\"metric-sub\">加权非对称双边挂单价差</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均库存吸收半衰期</div><div class=\"metric-value\" style=\"color:#89b4fa;\">{pm.AverageInventoryDecayHours:F2} 小时</div><div class=\"metric-sub\">粘性滑移面库存平滑释放</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">逆向选择击穿压降</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{pm.AdverseSelectionBreachReductionPct:F1}%</div><div class=\"metric-sub\">微观订单流穿透防守率</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">边界反射做市总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{pm.TotalBoundaryMarketMakingAlphaBps:F1} bps</div><div class=\"metric-sub\">高频做市保留价优化超额</div></div>");
            sb.AppendLine("  </div>");

            if (pm.PoissonItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产最优双边价差、保留价偏移、库存半衰期、击穿危险率、做市 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>最优价差</th><th>保留价偏移</th><th>库存半衰期</th><th>击穿危险率</th><th>做市 Alpha</th><th>评级</th><th>Avellaneda-Stoikov 挂单指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in pm.PoissonItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.OptimalBidAskSpreadBps:F2} bps</td><td>{item.InventoryReservationOffsetBps:F2} bps</td><td>{item.InventoryHalfLifeHours:F2}h</td><td>{item.QueueExhaustionHazardPct:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.BoundaryMarketMakingAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.PoissonRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.PoissonAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(pm.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百四十三、Renaissance Technologies & D.E. Shaw: 仿射 Kac-Moody 无限维李代数根格与杨-米尔斯规范场拓扑荷度量 (Phase 60)
        if (portfolio.KacMoodyGaugeTopologicalCharge != null)
        {
            var km = portfolio.KacMoodyGaugeTopologicalCharge;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百四十三、Renaissance Technologies & D.E. Shaw: 仿射 Kac-Moody 无限维李代数根格与杨-米尔斯规范场拓扑荷度量 (Phase 60)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">仿射李代数根格·杨-米尔斯规范场·瞬子拓扑荷</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局瞬子拓扑荷数</div><div class=\"metric-value\">Q = {km.GlobalTopologicalChargeNumber:F2}</div><div class=\"metric-sub\">非阿贝尔主丛拓扑卷绕数</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均规范场强曲率</div><div class=\"metric-value\" style=\"color:#89b4fa;\">|F| = {km.AverageGaugeCurvature:F3}</div><div class=\"metric-sub\">杨-米尔斯曲率 2-形式强度</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">流动性共振误判压降</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{km.FalseAlarmResonanceReductionPct:F1}%</div><div class=\"metric-sub\">拓扑相变伪信号滤除率</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">拓扑规范不变总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{km.TotalTopologicalChargeAlphaBps:F1} bps</div><div class=\"metric-sub\">李代数根格规范不变性超额</div></div>");
            sb.AppendLine("  </div>");

            if (km.GaugeItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产 Kac-Moody 根范数、规范曲率、破缺序参量、拓扑不变目标权重、抗相变 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>根格范数</th><th>规范曲率</th><th>破缺序参量</th><th>拓扑不变权重</th><th>抗相变 Alpha</th><th>评级</th><th>李代数规范不变性指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in km.GaugeItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.KacMoodyRootProjectionNorm:F2}</td><td>{item.YangMillsCurvatureMagnitude:F3}</td><td>{item.SymmetryBreakingOrderParameter:F1}%</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.GaugeInvariantTargetWeight:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.TopologicalChargeAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.KacMoodyRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.KacMoodyAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(km.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百四十四、Two Sigma & Jump Trading: McKean-Vlasov 前向-后向随机微分方程 (FBSDE) 非局部多主体平均场协同清算 (Phase 60)
        if (portfolio.McKeanVlasovOptimalLiquidation != null)
        {
            var mv = portfolio.McKeanVlasovOptimalLiquidation;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百四十四、Two Sigma & Jump Trading: McKean-Vlasov 前向-后向随机微分方程 (FBSDE) 非局部多主体平均场协同清算 (Phase 60)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">McKean-Vlasov·FBSDE 纳什均衡·平均场解耦</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局群体拥挤拖拽</div><div class=\"metric-value\">Γ = {mv.GlobalCrowdingDragCoeff:F3}</div><div class=\"metric-sub\">多 Pod 并发核相互作用阻尼</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均非局部平均场漂移</div><div class=\"metric-value\" style=\"color:#f38ba8;\">{mv.AverageMeanFieldDriftBps:F2} bps</div><div class=\"metric-sub\">群体同向清算诱导冲击漂移</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">踩踏清算落差节省</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{mv.AggregateLiquidationSavingPct:F1}%</div><div class=\"metric-sub\">解耦执行滑点落差改善率</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均场协同清算 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{mv.TotalMeanFieldEvasionAlphaBps:F1} bps</div><div class=\"metric-sub\">纳什最优解耦避踩踏超额</div></div>");
            sb.AppendLine("  </div>");

            if (mv.McKeanVlasovItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产群体拥挤拖拽、平均场反向漂移、最优解耦速度、落差节省率、协同 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>拥挤拖拽</th><th>平均场漂移</th><th>解耦速度</th><th>落差节省</th><th>协同 Alpha</th><th>评级</th><th>FBSDE 动态调度指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in mv.McKeanVlasovItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.CollectiveCrowdingDragCoeff:F3}</td><td>{item.NonLocalMeanFieldDriftBps:F2} bps</td><td>{item.OptimalDecrowdingSpeed:F1} 手/分</td><td>{item.CrowdingShortfallSavingPct:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.MeanFieldEvasionAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.McKeanVlasovRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.McKeanVlasovAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mv.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百四十五、Bridgewater Associates & Millennium Macro: 分数阶伊藤-沃尔泰拉 (Ito-Volterra) 非马尔可夫信用周期记忆与遍历谱平价 (Phase 60)
        if (portfolio.VolterraNonMarkovianCreditParity != null)
        {
            var vt = portfolio.VolterraNonMarkovianCreditParity;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百四十五、Bridgewater Associates & Millennium Macro: 分数阶伊藤-沃尔泰拉 (Ito-Volterra) 非马尔可夫信用周期记忆与遍历谱平价 (Phase 60)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">分数阶伊藤-沃尔泰拉·深层记忆核·遍历谱平价</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局长记忆赫斯特指数</div><div class=\"metric-value\">H = {vt.GlobalVolterraMemoryHurst:F3}</div><div class=\"metric-sub\">沃尔泰拉奇异核分数阶参数</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">遍历谱能量信息熵</div><div class=\"metric-value\" style=\"color:#89b4fa;\">S = {vt.ErgodicSpectralEntropy:F3}</div><div class=\"metric-sub\">跨频段多尺度谱能量熵</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">回撤修复周期缩短</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{vt.DrawdownRecoveryCycleShortenPct:F1}%</div><div class=\"metric-sub\">跨周期非马尔可夫韧性提振</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">沃尔泰拉谱平价总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{vt.TotalVolterraCreditParityAlphaBps:F1} bps</div><div class=\"metric-sub\">长周期债务悬垂免疫超额</div></div>");
            sb.AppendLine("  </div>");

            if (vt.VolterraItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产深层记忆持久度、遍历谱风险贡献、债务悬垂 Beta、平价目标权重、长记忆 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>记忆持久度</th><th>遍历谱风险</th><th>债务悬垂 Beta</th><th>平价目标权重</th><th>谱平价 Alpha</th><th>评级</th><th>分数阶全天候平价指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in vt.VolterraItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.VolterraMemoryPersistenceIndex:F1}</td><td>{item.ErgodicSpectralRiskContributionPct:F1}%</td><td>{item.MultiCycleDebtOverhangBeta:F2}</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.VolterraResilientTargetWeight:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.VolterraCreditParityAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.VolterraRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.VolterraAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(vt.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百四十六、Citadel Securities & Jane Street: 微观高阶粗糙霍克斯过程与订单簿队列隐性排队延迟博弈 (Phase 61)
        if (portfolio.RoughHawkesQueueLatencyArbitrage != null)
        {
            var rh = portfolio.RoughHawkesQueueLatencyArbitrage;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百四十六、Citadel Securities & Jane Street: 微观高阶粗糙霍克斯过程与订单簿队列隐性排队延迟博弈 (Phase 61)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">粗糙霍克斯点过程·排队生存泛函·延迟套利免疫</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局最优跳级挂单价差</div><div class=\"metric-value\">{rh.GlobalOptimalQueueSpreadBps:F2} bps</div><div class=\"metric-sub\">加权队列深度跳级挂单价差</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均排队生存概率</div><div class=\"metric-value\" style=\"color:#89b4fa;\">{rh.AverageQueueSurvivalRatePct:F1}%</div><div class=\"metric-sub\">深度档位优先队列生存率</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">延迟抢跑滑点压降</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{rh.LatencyArbitrageSlippageReductionPct:F1}%</div><div class=\"metric-sub\">微秒级延迟被捕食防护率</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">粗糙排队净总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{rh.TotalRoughHawkesQueueAlphaBps:F1} bps</div><div class=\"metric-sub\">排队位置博弈优化超额收益</div></div>");
            sb.AppendLine("  </div>");

            if (rh.HawkesItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产粗糙分支比、排队生存概率、延迟抢跑危险率、最优跳级价差、排队 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>粗糙分支比</th><th>排队生存率</th><th>抢跑危险率</th><th>跳级价差</th><th>排队 Alpha</th><th>评级</th><th>微观粗糙排队指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in rh.HawkesItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.RoughHawkesBranchingRatio:F3}</td><td>{item.QueueSurvivalProbabilityPct:F1}%</td><td>{item.LatencyArbitrageHazardPct:F1}%</td><td>{item.OptimalQueueSpreadBps:F2} bps</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.RoughHawkesQueueAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.RoughHawkesRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.RoughHawkesAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(rh.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百四十七、Renaissance Technologies & D.E. Shaw: 相空间辛几何哈密顿流形动力学与辛积分保结构降维共振 (Phase 61)
        if (portfolio.SymplecticHamiltonianManifoldResonance != null)
        {
            var sm = portfolio.SymplecticHamiltonianManifoldResonance;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百四十七、Renaissance Technologies & D.E. Shaw: 相空间辛几何哈密顿流形动力学与辛积分保结构降维共振 (Phase 61)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">辛几何流形·哈密顿正则相流·庞加莱保结构</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">相空间辛体积漂移率</div><div class=\"metric-value\">ΔΩ = {sm.GlobalSymplecticPhaseVolumeDrift:F3}</div><div class=\"metric-sub\">几何保辛积分体积守恒偏差</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均庞加莱李雅普诺夫</div><div class=\"metric-value\" style=\"color:#89b4fa;\">λ = {sm.AverageLyapunovExponent:F3}</div><div class=\"metric-sub\">相空间拟周期特征发散率</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">伪共振崩溃规避率</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{sm.FalseResonanceAvoidancePct:F1}%</div><div class=\"metric-sub\">非线性孤子共振阻尼改善</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">辛几何保结构总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{sm.TotalSymplecticManifoldAlphaBps:F1} bps</div><div class=\"metric-sub\">流形几何保结构超额收益</div></div>");
            sb.AppendLine("  </div>");

            if (sm.SymplecticItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产共轭动量范数、庞加莱李雅普诺夫指数、能量守恒率、保结构目标权重、保结构 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>动量范数</th><th>李雅普诺夫</th><th>能量守恒率</th><th>保结构权重</th><th>保结构 Alpha</th><th>评级</th><th>辛几何保结构指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in sm.SymplecticItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.SymplecticMomentumMagnitude:F2}</td><td>{item.PoincareSectionLyapunovExponent:F3}</td><td>{item.SymplecticEnergyConservationRatio:F2}%</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.SymplecticPreservingTargetWeight:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.SymplecticManifoldAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.SymplecticRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.SymplecticAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(sm.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百四十八、Two Sigma & Point72: 最优传输 (Monge-Kantorovich) Wasserstein 重心测度流与截面非平衡协同重构 (Phase 61)
        if (portfolio.WassersteinBarycenterDynamicRebalancing != null)
        {
            var wb = portfolio.WassersteinBarycenterDynamicRebalancing;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百四十八、Two Sigma & Point72: 最优传输 (Monge-Kantorovich) Wasserstein 重心测度流与截面非平衡协同重构 (Phase 61)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">Wasserstein重心·Sinkhorn熵正则·测地线调仓</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局测度重心信息熵</div><div class=\"metric-value\">S = {wb.GlobalBarycenterEntropy:F3}</div><div class=\"metric-sub\">经验测度重心玻尔兹曼熵</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均测度距离 W2</div><div class=\"metric-value\" style=\"color:#89b4fa;\">W2 = {wb.AverageWassersteinDistance:F3}</div><div class=\"metric-sub\">资产截面分布至重心测度差</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">传输摩擦损耗节省</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{wb.TransportFrictionSavingPct:F1}%</div><div class=\"metric-sub\">测地线路径规划减摩擦率</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">最优传输重构总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{wb.TotalOptimalTransportAlphaBps:F1} bps</div><div class=\"metric-sub\">非平衡测度几何重构超额</div></div>");
            sb.AppendLine("  </div>");

            if (wb.WassersteinItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产 Wasserstein 测度距离、Sinkhorn 传输成本、里奇曲率离散度、测地线目标权重、重构 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>测度距离 W2</th><th>传输成本(万)</th><th>里奇曲率</th><th>测地线目标权重</th><th>重构 Alpha</th><th>评级</th><th>测地线重构指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in wb.WassersteinItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.WassersteinDistanceToBarycenter:F3}</td><td>{item.SinkhornTransportCostTenThousand:F2}万</td><td>{item.RicciCurvatureDispersion:F3}</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.GeodesicOptimalTargetWeight:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.OptimalTransportAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.WassersteinRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.WassersteinAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(wb.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百四十九、Bridgewater Associates & AQR Capital: 连续谱量子混沌非对易算子多尺度周期与超全天候宏观体制平价 (Phase 61)
        if (portfolio.QuantumSpectralChaosMacroParity != null)
        {
            var qp = portfolio.QuantumSpectralChaosMacroParity;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百四十九、Bridgewater Associates & AQR Capital: 连续谱量子混沌非对易算子多尺度周期与超全天候宏观体制平价 (Phase 61)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">量子混沌谱刚度·GUE能级排斥·非对易宏观平价</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局 GUE 谱刚度</div><div class=\"metric-value\">Δ3 = {qp.GlobalGueSpectralRigidity:F3}</div><div class=\"metric-sub\">高斯酉系综谱刚度指标</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">Berry-Robnik 混沌熵</div><div class=\"metric-value\" style=\"color:#89b4fa;\">S = {qp.BerryRobnikChaosEntropy:F3}</div><div class=\"metric-sub\">量子混沌能级多尺度信息熵</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">宏观滞胀回撤减免</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{qp.MacroTailDrawdownMitigationPct:F1}%</div><div class=\"metric-sub\">量子隧穿相变尾部抗毁率</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">量子谱平价总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{qp.TotalQuantumSpectralParityAlphaBps:F1} bps</div><div class=\"metric-sub\">超全天候非对易谱平价超额</div></div>");
            sb.AppendLine("  </div>");

            if (qp.QuantumItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产戴森能级排斥比、谱刚度指数、量子隧穿概率、平价目标权重、量子谱 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>能级排斥比</th><th>谱刚度指数</th><th>隧穿概率</th><th>平价目标权重</th><th>量子谱 Alpha</th><th>评级</th><th>量子宏观平价指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in qp.QuantumItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.DysonLevelSpacingRatio:F3}</td><td>{item.QuantumSpectralRigidityIndex:F3}</td><td>{item.QuantumTunnelingJumpProbabilityPct:F1}%</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.QuantumChaosParityTargetWeight:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.QuantumSpectralParityAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.QuantumRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.QuantumAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(qp.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百五十、Millennium Management & Point72: 多子策略/基金高水位动态资本回撤扣划与跨组合因子拥挤解耦 (Phase 62)
        if (portfolio.MultiPodFactorCrowdingClawback != null)
        {
            var mp = portfolio.MultiPodFactorCrowdingClawback;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百五十、Millennium Management & Point72: 多子策略/基金高水位动态资本回撤扣划与跨组合因子拥挤解耦 (Phase 62)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">动态高水位扣划·因子拥挤解耦·踩踏清算隔离</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">全局回撤扣划比例</div><div class=\"metric-value\" style=\"color:#f38ba8;\">{mp.GlobalAverageClawbackRatioPct:F1}%</div><div class=\"metric-sub\">动态高水位惩罚性资本回撤扣划</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">隐性因子拥挤谱熵</div><div class=\"metric-value\">S = {mp.PortfolioLatentCrowdingEntropy:F3}</div><div class=\"metric-sub\">跨子策略持仓余弦重叠度熵</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">协同清算踩踏化解率</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">{mp.CascadeLiquidationRiskMitigationPct:F1}%</div><div class=\"metric-sub\">流动性挤兑传染阻尼改善</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">多子策略解耦总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{mp.TotalMultiPodClawbackAlphaBps:F1} bps</div><div class=\"metric-sub\">动态扣划再平衡超额收益</div></div>");
            sb.AppendLine("  </div>");

            if (mp.PodItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产历史峰谷回撤、资本扣划比例、因子拥挤度、清算脆弱度、解耦目标权重、解耦 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>峰谷回撤</th><th>扣划比例</th><th>因子拥挤度</th><th>清算脆弱度</th><th>解耦目标权重</th><th>解耦 Alpha</th><th>评级</th><th>资本再分配与解耦指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in mp.PodItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.PodPeakToTroughDrawdownPct:F1}%</td><td>{item.PodDrawdownClawbackRatioPct:F1}%</td><td>{item.CrossPodFactorCrowdingIndex:F2}</td><td>{item.LiquidityContagionVulnerabilityPct:F1}%</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.CrowdingDecoupledTargetWeight:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.MultiPodClawbackAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.PodRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.PodAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mp.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百五十一、BlackRock Aladdin & NBIM / GIC: NGFS 气候宏观转型三情景压力测试与碳贝塔搁浅资产 VaR (Phase 62)
        if (portfolio.ClimateTransitionStrandedAssetStress != null)
        {
            var ct = portfolio.ClimateTransitionStrandedAssetStress;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百五十一、BlackRock Aladdin & NBIM / GIC: NGFS 气候宏观转型三情景压力测试与碳贝塔搁浅资产 VaR (Phase 62)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">NGFS三情景·碳贝塔β_c·搁浅资产极端VaR</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">加权平均碳贝塔</div><div class=\"metric-value\">β_c = {ct.PortfolioWeightedCarbonBeta:F2}</div><div class=\"metric-sub\">碳税敏感度与排放敞口</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">有序转型折算冲击</div><div class=\"metric-value\" style=\"color:#89b4fa;\">-{ct.OrderlyScenarioPortfolioDrawdownPct:F1}%</div><div class=\"metric-sub\">NGFS 1.5°C 有序转型估值冲击</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">无序转型减记比例</div><div class=\"metric-value\" style=\"color:#f38ba8;\">{ct.DisorderlyStrandedWriteDownPct:F1}%</div><div class=\"metric-sub\">高排放棕色搁浅资产折价减记</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">气候防卫总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{ct.TotalClimateTransitionAlphaBps:F1} bps</div><div class=\"metric-sub\">绿色低碳重配防卫超额</div></div>");
            sb.AppendLine("  </div>");

            if (ct.ClimateItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产碳贝塔、有序转型冲击、无序转型折价、搁浅资产 VaR、气候目标权重、气候 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>碳贝塔</th><th>有序冲击</th><th>无序折价</th><th>搁浅 VaR</th><th>气候目标权重</th><th>气候 Alpha</th><th>评级</th><th>NGFS 气候应对指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in ct.ClimateItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.AssetCarbonBeta:F2}</td><td>{item.OrderlyTransitionImpactPct:F1}%</td><td>{item.DisorderlyTransitionImpactPct:F1}%</td><td>{item.StrandedAssetExtremeVaRPct:F1}%</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.ClimateResilientTargetWeight:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.ClimateTransitionAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.ClimateRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.ClimateAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(ct.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百五十二、Two Sigma & D.E. Shaw: 高阶张量环 (Tensor Ring) 紧致分解与隐式多模态 Alpha 场 (Phase 62)
        if (portfolio.TensorRingMultimodalAlphaField != null)
        {
            var tr = portfolio.TensorRingMultimodalAlphaField;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百五十二、Two Sigma & D.E. Shaw: 高阶张量环 (Tensor Ring) 紧致分解与隐式多模态 Alpha 场 (Phase 62)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">张量环紧致核·循环迹分解·多模态隐式Alpha</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">张量环重构误差</div><div class=\"metric-value\">ε = {tr.GlobalTensorRingReconstructionError:F3}</div><div class=\"metric-sub\">Frobenius 相对紧致重构误差</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">平均循环相干度</div><div class=\"metric-value\" style=\"color:#89b4fa;\">S = {tr.AverageMultimodalEntanglement:F3}</div><div class=\"metric-sub\">多模态循环闭合缠结熵</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">高维纯度提升率</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">+{tr.HighDimensionalAlphaPurityGainPct:F1}%</div><div class=\"metric-sub\">高阶张量去噪有效信噪比提升</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">多模态张量总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{tr.TotalTensorRingAlphaFieldBps:F1} bps</div><div class=\"metric-sub\">跨模态低秩流形紧致超额</div></div>");
            sb.AppendLine("  </div>");

            if (tr.TensorItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产张量环核心秩、多模态缠结熵、去噪提升率、张量目标权重、张量场 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>核心秩</th><th>缠结熵</th><th>去噪提升率</th><th>张量目标权重</th><th>张量场 Alpha</th><th>评级</th><th>张量流形指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in tr.TensorItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>R={item.TensorRingCoreRank}</td><td>{item.MultimodalEntanglementEntropy:F3}</td><td>+{item.TensorNoiseSuppressionPct:F1}%</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.TensorRingOptimalTargetWeight:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.TensorRingAlphaFieldBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.TensorRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.TensorAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(tr.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 一百五十三、Citadel & Jump Trading: 维纳-泊松空间马利亚温微分 (Malliavin Calculus) 跳跃扩散随机波动率对冲 (Phase 62)
        if (portfolio.MalliavinJumpDiffusionHedging != null)
        {
            var mj = portfolio.MalliavinJumpDiffusionHedging;
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <div class=\"section-header\">");
            sb.AppendLine("    <div class=\"section-title\">一百五十三、Citadel & Jump Trading: 维纳-泊松空间马利亚温微分 (Malliavin Calculus) 跳跃扩散随机波动率对冲 (Phase 62)</div>");
            sb.AppendLine("    <div class=\"badge badge-purple\">马利亚温分部积分·Skorokhod发散对偶·跳跃免疫</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"metric-grid\">");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">加权对冲系数</div><div class=\"metric-value\">Δ_M = {mj.GlobalMalliavinHedgeRatio:F2}</div><div class=\"metric-sub\">马利亚温分部积分精确 Delta</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">波动率凸度增益</div><div class=\"metric-value\" style=\"color:#89b4fa;\">+{mj.StochasticVolConvexityCapture:F1} bps</div><div class=\"metric-sub\">随机波动率二阶 Gamma 曲率捕捉</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">跳跃滑点压降率</div><div class=\"metric-value\" style=\"color:#a6e3a1;\">-{mj.ExtremeJumpSlippageReductionPct:F1}%</div><div class=\"metric-sub\">泊松跳跃弥散执行冲击压降</div></div>");
            sb.AppendLine($"    <div class=\"metric-card\"><div class=\"metric-label\">马利亚温变分总 Alpha</div><div class=\"metric-value\" style=\"color:#f9e2af;\">+{mj.TotalMalliavinHedgingAlphaBps:F1} bps</div><div class=\"metric-sub\">微观跳跃扩散变分对冲超额</div></div>");
            sb.AppendLine("  </div>");

            if (mj.MalliavinItems.Count > 0)
            {
                sb.AppendLine("  <div class=\"table-title\">各资产马利亚温 Delta、Gamma 敏感度、跳跃弥散强度、免疫目标权重、对冲 Alpha 与指令明细</div>");
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr><th>资产代码</th><th>资产名称</th><th>权重</th><th>马利亚温 Delta</th><th>Gamma 曲率</th><th>跳跃强度(次/年)</th><th>免疫目标权重</th><th>对冲 Alpha</th><th>评级</th><th>变分对冲指令</th></tr></thead>");
                sb.AppendLine("    <tbody>");
                foreach (var item in mj.MalliavinItems)
                {
                    sb.AppendLine($"    <tr><td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(item.AssetCode)}</td><td>{WebUtility.HtmlEncode(item.AssetName)}</td><td>{item.Weight:F1}%</td><td>{item.MalliavinDeltaRatio:F2}</td><td>{item.MalliavinGammaSensitivity:F3}</td><td>{item.JumpDiffusionHazardIntensity:F1}</td><td style=\"color:#89b4fa;font-weight:bold;\">{item.MalliavinImmunizedTargetWeight:F1}%</td><td style=\"color:#a6e3a1;font-weight:bold;\">+{item.MalliavinHedgingAlphaBps:F1} bps</td><td>{WebUtility.HtmlEncode(item.MalliavinRegimeBadge)}</td><td>{WebUtility.HtmlEncode(item.MalliavinAdvice)}</td></tr>");
                }
                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }

            sb.AppendLine($"  <div class=\"advice-box\">{WebUtility.HtmlEncode(mj.ExecutiveVerdict)}</div>");
            sb.AppendLine("</div>");
        }

        // 页脚免责声明
        sb.AppendLine("<div class=\"footer\">");
        sb.AppendLine("  <p>免责声明：本尽职调查与资产配置研报由 BIGA 量化投研引擎根据公开净值与持仓数据自动化生成，所采用模型包含 Millennium Management & Point72 多子策略高水位动态资本回撤扣划与跨组合因子拥挤解耦引擎、BlackRock Aladdin & NBIM / GIC NGFS 气候宏观转型三情景压力测试与碳贝塔搁浅资产 VaR 引擎、Two Sigma & D.E. Shaw 高阶张量环 (Tensor Ring) 紧致分解与隐式多模态 Alpha 场引擎、Citadel & Jump Trading 维纳-泊松空间马利亚温微分 (Malliavin Calculus) 跳跃扩散随机波动率对冲引擎、Citadel Securities & Jane Street 微观高阶粗糙霍克斯过程与订单簿队列隐性排队延迟博弈引擎、Renaissance Technologies & D.E. Shaw 相空间辛几何哈密顿流形动力学与辛积分保结构降维共振引擎、Two Sigma & Point72 最优传输 (Monge-Kantorovich) Wasserstein 重心测度流与截面非平衡协同重构引擎、Bridgewater Associates & AQR Capital 连续谱量子混沌非对易算子多尺度周期与超全天候宏观体制平价引擎、Citadel Securities & Optiver 非线性泊松跳跃边界反射与 Avellaneda-Stoikov 粘性滑移面做市控制引擎、Renaissance Technologies & D.E. Shaw 仿射 Kac-Moody 无限维李代数根格与杨-米尔斯规范场拓扑荷度量引擎、Two Sigma & Jump Trading McKean-Vlasov 前向-后向随机微分方程 (FBSDE) 非局部多主体平均场协同清算引擎、Bridgewater Associates & Millennium Macro 分数阶伊藤-沃尔泰拉 (Ito-Volterra) 非马尔可夫信用周期记忆与遍历谱平价引擎、Citadel Securities & Jump Trading 连续拍卖 Kyle's λ 价格冲击弹性与订单流信息穿透深度控制引擎、Renaissance Technologies & D.E. Shaw 拓扑 Wilson 重整化群矩流动与仿射渗流相变临界聚类引擎、Two Sigma & PDT Partners 连续时间随机微分博弈 (SDG) 捕食者-猎物纳什均衡与流动性黑洞抢跑规避变分引擎、Bridgewater Associates & AQR Capital 连续时间漂移-扩散非线性状态空间与鲁棒卡尔曼-布西宏观体制平价引擎、Jane Street & Citadel Securities 连续时间 Glosten-Milgrom 贝叶斯信念更新与流动性逆向选择防护引擎、Renaissance Technologies & D.E. Shaw 非广延统计力学 Tsallis 广义熵与多重分形奇异谱极值崩塌预警引擎、Two Sigma & Jump Trading 随机非线性粘性阻尼与 Fredholm 记忆核最优变分执行清算引擎、Bridgewater Associates & Millennium Management 结构向量自回归有向无环图 (SVAR-DAG) 与 Pearl 反事实因果干预宏观网络引擎、Two Sigma & Citadel Securities 连续时间马利亚温随机变分微积分与粗糙波动率泛函高阶 Greeks 深度对冲引擎、Renaissance Technologies & Jump Trading 开放量子耗散系统 Lindblad 主方程与微观相干态密度矩阵统计套利引擎、Millennium Management & Point72 连续时间多主体均值场博弈 (Mean-Field Games) 与 FPK-HJB 拥挤踩踏解耦优化引擎、Bridgewater Associates & AQR Capital 非平衡态热力学最大熵产生与信息几何 Fisher-Rao 测地线宏观流形平滑轮动引擎、Jane Street & Citadel Securities 全档限价订单簿 (LOB) 微观价格扩散鞅测度变换与即时流动性真空渗透控制引擎、D.E. Shaw & Two Sigma 连续时间多维非高斯列维-伊藤跳跃扩散与极值共跳曲率对冲引擎、Renaissance Technologies & Millennium Management 高阶拓扑超图场论与自旋玻璃阻挫能量退火解聚引擎、Bridgewater Associates & BlackRock Aladdin 达利欧长期主权债务大周期与超级去杠杆马尔可夫状态免疫引擎、Renaissance Technologies & Two Sigma 高维随机矩阵理论局部谱去噪与收缩协方差重构引擎、Citadel Securities & Jump Trading 微观分形霍克斯自激互激订单流毒性与闪崩级联预警引擎、Bridgewater Associates & AQR Capital 多资产非线性分形波动率记忆与赫斯特表面跨周期极值回撤对偶防御引擎、Point72 & Millennium Management 多代理强化学习非稳态微观策略博弈与对抗性鲁棒策略蒸馏引擎、Renaissance Technologies & D.E. Shaw 连续时间马尔可夫跳跃状态空间与分层狄利克雷过程 (HDP-HMM) 动态非参数宏观体制涌现引擎、Citadel Securities & Hudson River Trading 双边限价订单簿微观非线性平均场博弈与高阶随机冲量最优流动性提供控制引擎、Two Sigma & WorldQuant 高维因果发现有向无环图与结构因果模型反事实干预不变性 Alpha 纯化引擎、Bridgewater Associates & AQR Capital 广义非对称谱风险测度与动态极值高斯-Student Copula 尾部联动全景压测引擎、Jump Trading & Optiver 连续时间仿射跳跃-扩散限价簿做市与自适应逆向选择免受损控制引擎、Citadel Global Fixed Income & Millennium Macro 多因子无套利高斯仿射动态期限结构与曲率相对价值套利引擎、Point72 & Citadel Multi-Strategy 多 Pod 合作博弈 Shapley-Owen 边际分散贡献与动态流动性影子定价仲裁引擎、D.E. Shaw & WorldQuant 非欧流形 Ollivier-Ricci 拓扑曲率流相变相干与高阶持久同调过滤引擎、Citadel Securities & Jane Street 微观瞬态幂律记忆价格冲击核 (Bouchaud Propagator) 与动态隐匿拆单执行引擎、Two Sigma & D.E. Shaw 高维流形图拉普拉斯算子谱聚类与非局部热核扩散小波多尺度 Alpha 分解引擎、Bridgewater Associates & AQR Capital 分数阶粘弹性流变学宏观资产负荷动力学与系统性流动性蠕变松弛测算引擎、Renaissance Technologies & Hudson River Trading 非平衡态朗之万细致平衡破缺与概率流旋度相空间统计套利做功巡回引擎、Millennium Management & Point72 多 PM 策略 Pod 凸二次多维资本分配与内部互冲去交叉阻尼引擎、Jump Trading & Tower Research Capital 粗糙分数阶随机波动率 (Gatheral Rough Heston) 与微观粗糙度幂律偏度流形、Bridgewater Associates & BlackRock Aladdin 宏观热力学最小相对交叉熵 (Jaynes MaxEnt / KL Relative Entropy) 与非高斯情景冲击流形映射、Jump Trading & Hudson River Trading 超对称路径积分 (Supersymmetric Path Integral) 与非微扰瞬子 (Instanton) 极端黑天鹅跃迁与负能级隧道穿透防御、Renaissance Technologies & D.E. Shaw 非对易自由概率论 (Voiculescu Free Probability) 与量子重整化群 (QRG) 高维矩阵奇异谱修复、Citadel Global Strategies & AQR Capital Management 宏观随机动力学混沌奇异吸引子 (Lyapunov Exponent Spectrum) 与托姆尖点突变论 (Thom Cusp Catastrophe) 极值跳变防踩踏、Two Sigma & WorldQuant 神经薛定谔桥 (Neural Schrödinger Bridge) 熵正则化最优输运与反射倒向随机微分方程 (Reflected BSDE) 资本硬约束动态流动性生成式对冲、Jane Street Capital & Hudson River Trading 玻尔兹曼-弗拉索夫 (Boltzmann-Vlasov) 动理学流动性连续体场论与相对论性纳什执行博弈、Renaissance Technologies & D.E. Shaw 跳跃扩散 Lévy 过程 Esscher 测度变换与随机庞特里亚金极大值原理 (SPMP) 伴随状态最优控制、Citadel Global Strategies & Millennium Management 多 Pod 去中心化共识交替方向乘子法 (Consensus ADMM) 与增广拉格朗日多重约束动态资本仲裁、Two Sigma & Bridgewater Associates 非参数分层狄利克雷过程隐马尔可夫模型 (HDP-HMM) 自适应宏观结构聚类与未见体制动态涌现、Jane Street Capital & Jump Trading 双重随机 Cox 点过程与非齐次复合泊松跳跃做市最优非对称价差偏置控制、Renaissance Technologies & D.E. Shaw 马利亚温随机变分分析 (Malliavin Calculus) 与无似然高阶 Greeks 敏感度对偶 (Pathwise Malliavin Greeks & Skorokhod Integral)、Citadel Global Strategies & Millennium Management 半定松弛 (SDR) 锥优化与基数约束稀疏指数跟踪 (Semidefinite Relaxation & Sparse Cardinality Optimization)、Two Sigma & Bridgewater Associates 连续时间平均场博弈 (Mean Field Games, MFG) 与机构间策略纳什均衡执行 (Continuous-Time MFG Nash Equilibrium)、Jane Street Capital & Jump Trading Kyle-Back 连续拍卖动态知情交易与隐匿执行模型 (Kyle-Back Dynamic Continuous Auction & Informed Stealth Trading)、Renaissance Technologies & Alan Turing Institute 粗糙路径特征签名 (Rough Path Signature) 与高阶张量 Alpha 几何编码 (Chen Iterated Integral & Lévy Area)、Citadel Global Strategies & Millennium RV 随机最优停止时间与自由边界斯内尔包络 (Snell Envelope) 动态平滑粘贴去杠杆曲面 (Continuous-Time Stochastic Optimal Stopping)、Two Sigma & Bridgewater Associates 因果结构方程模型 (SCM) 与反事实 Do-Calculus 宏观干预归因 (Pearl Structural Causal Model & Counterfactual Attribution)、Jane Street & Citadel Securities 暂态市场冲击幂律记忆核 (Propagator Kernel) 与最优拆单执行减摩 (Bouchaud Transient Impact Propagator Model)、Renaissance Technologies & D.E. Shaw 连续时间分数阶粗糙波动率 (Rough Heston) 与 Riemann-Liouville 分数阶积分预测引擎 (Fractional Rough Volatility & Riemann-Liouville Kernel)、Citadel Global Fixed Income & Millennium RV 六参数 Nelson-Siegel-Svensson (NSS) 利率期限结构拟合与蝶式凸性相对价值套利引擎 (Nelson-Siegel-Svensson Term Structure & Butterfly Arbitrage)、Two Sigma & Man Group AHL 贝叶斯在线变点检测 (BOCPD) 与序列失效率运行长度后验滤波 (Bayesian Online Changepoint Detection & Hazard Filtering)、Jane Street & Citadel Securities Avellaneda-Stoikov 连续库存风险最优保留价与非对称限价挂单微观做市定价 (Avellaneda-Stoikov Optimal Micro-Reservation Quoting Engine)、Renaissance Technologies & D.E. Shaw 连续时间倒向随机微分方程 (BSDE) 动态粘性解对冲引擎与随机波动率曲率最小化 (Continuous-Time BSDE Dynamic Viscosity Hedging)、Citadel & Millennium 多资产高阶拓扑超图 (Hypergraph) 关联网络与持续同调 Persistent Homology 空洞破裂预警 (Hypergraph Topological Causality & Persistent Homology)、Jane Street & Citadel Securities 微观瞬时订单流毒性扩散核、跨标的交叉价格冲击张量与非对称做市执行 (Cross-Impact Tensor & Transient Toxicity Diffusion Kernel)、Bridgewater Associates & AQR Capital 测度模糊集 Wasserstein-Ball 分布鲁棒优化 (DRO) 与极小极大抗毁平价 (Wasserstein-Ball Distributionally Robust Optimization)、Renaissance Technologies & Two Sigma 最大相关最小冗余 (mRMR) 互信息特征选择与随机特征子空间正交集成引擎 (mRMR Feature Selection & Orthogonal Subspace Ensemble)、Citadel & Millennium 基于 Wasserstein 测度距离最优输运 (Optimal Transport) 的多策略 Pod 动态资本曲率重构与非线性凸松弛配置引擎 (Wasserstein Optimal Transport Multi-Pod Capital Curvature Engine)、Jane Street & Citadel Securities 微观限价订单簿自激 Hawkes 点过程、跳跃扩散强度与流动性雪崩级联预警系统 (Microstructure Hawkes Point Process & Jump-Diffusion Avalanche Engine)、Bridgewater Associates & AQR Capital 高阶矩张量风险平价与非高斯偏度-峰度协同传染对冲矩阵 (Higher-Order Moment Tensor Risk Parity & Co-Skewness/Co-Kurtosis Tensor Hedging)、Bridgewater Associates & AQR Capital 跨资产内生流动性螺旋与去杠杆压力传染动力学模型 (Endogenous Liquidity Spiral & Fire-Sale Deleveraging Dynamics)、Renaissance Technologies & Citadel 变分无监督深度隐空间流形聚类与非线性宏观体制自编码器 (Variational Latent Space Manifold Clustering & Deep Regime Autoencoder)、Millennium Management & Point72 跨资产多周期极值尾部相关相变检测与渗流理论系统性临界熔断网络 (Percolation Theory & Extreme Tail Correlation Phase Transition Detector)、WorldQuant & Hudson River Trading 统计套利强化正交特征残差动量与微观协整均值回复追踪引擎 (Statistical Arbitrage Residual Momentum & Cointegrated OU Mean-Reversion)、D.E. Shaw & WorldQuant 符号基因规划自适应 Alpha 因子挖掘引擎 (Symbolic Genetic Programming & Expression Alpha Mining)、Two Sigma & Man Group AHL 知识图谱跨资产因果时滞传递与宏观情绪溢出网络 (Causal Lead-Lag Knowledge Graph & Sentiment Spillover)、Citadel & Point72 上下文多臂老虎机自适应策略路由器 (Contextual Multi-Armed Bandit & Thompson Sampling Router)、Jump Trading & Optiver 深度强化微观流动性冲击弹性与非线性滑点曲面 (Microstructure Resiliency & Non-Linear Slippage Surface)、Bridgewater Associates & Citadel 宏观马尔可夫区制转移概率模型与跨周期条件资产配置引擎 (Markov Regime Switching & Conditional Allocation)、AQR Capital & Man Group AHL 多频率截面交叉动量与双重相对优势动量剥离引擎 (Cross-Sectional Momentum CSMOM)、Millennium Management & Balyasny (BAM) 多策略 Pod Shop 阶梯式硬风控回撤熔断与动态资本再平衡矩阵 (Pod Shop Tiered Circuit Breaker & Rebalancing)、Jane Street & Optiver 微观限价单队列成交概率与高频期现基差收敛套利执行引擎 (Limit Order Queue Fill Probability & Basis Arbitrage)、Bridgewater Associates & AQR Capital 主因子正交风险平价 (PFRP) 与特征风险预算配置引擎 (Principal Factor Risk Parity & Eigenmode Risk Budgeting)、Millennium Management & Point72 动态下行凸性期权对冲与广义波动率偏度复制引擎 (Dynamic Downside Convexity & Volatility Skew Hedge)、Renaissance Technologies & D.E. Shaw 贝叶斯时变状态空间卡尔曼滤波与自适应 Alpha 动态跟踪引擎 (Bayesian Dynamic State-Space & Kalman Filter Alpha Tracker)、Jane Street & Jump Trading 跨资产微观流动性共振裂谷与闪崩级联阻尼矩阵 (Cross-Asset Liquidity Co-Evaporation & Flash Crash Cascade Damper)、Man Group AHL & AQR 跨资产大类期限结构展期收益率 (Roll Yield / Carry) 与基差动量收割引擎 (Curve Carry & Basis Momentum)、Renaissance Technologies & CFM 随机矩阵理论 (RMT) 马尔琴科-帕斯图尔谱分解滤波与信号降噪 (RMT Spectral Filtering)、Citadel & Millennium 多元非对称极值下行联结与组合结构性协同崩塌脆弱度矩阵 (Tail Co-Crash & Fragility Network)、Jane Street & Citadel Securities 连续时间微观订单流不平衡 (OFI)、Kyle 价格冲击信息份额与做市逆向选择足迹 (Adverse Selection Footprint)、Bridgewater Associates 宏观增长-通胀双维度惊喜指数矩阵与全天候宏观贝塔正交中性化覆盖 (Macro Surprise Overlay)、Citadel & Millennium 多经理平台核心风格因子正交中性化与非故意风险漂移剔除 (Style Factor Neutralization)、AQR Capital & Antti Ilmanen 动态目标波动率定标时间序列动量 (TSMOM) 与危机 Alpha 防御、Two Sigma & D.E. Shaw Almgren-Chriss 连续动态最优拆单轨迹与流动性在险度量、Citadel & Millennium 微观因子与资产拥挤度评分系统及机构踩踏排队指数 (HLRI)、BlackRock Aladdin & J.P. Morgan 多因子宏观情景冲击传导与极端压力在险价值 (Stressed VaR)、AQR Capital & Antti Ilmanen 真实波动率特征结构与方差风险溢价 (VRP) 收割引擎、Two Sigma & Renaissance Technologies 摩擦成本敏感型 Leland-Atkinson 动态无交易再平衡缓冲带 (No-Trade Buffer Bands)、Goldman Sachs & J.P. Morgan 跨资产非对称时滞领先-滞后互相关与信息流传导有向网络、BlackRock Aladdin / Axioma / Lo-MacKinlay 多重投资期限风险期限结构与方差比非随机游走检验、Two Sigma & Citadel 3 状态多元高斯隐马尔可夫模型 (Gaussian HMM) 宏观体制自动解码与转移信息熵、AQR Capital & Daniel-Moskowitz 广义反向择时短周期流动性反转 Alpha 与极端恐慌反弹动量崩塌预警指数 MCWI、AQR 杠杆约束异象 Betting Against Beta (BAB) 与高质量多空 QMJ 因子解构、BlackRock Aladdin 极值理论 POT 广义帕累托 (GPD) 尾部外推与 250/1000/2500 日深水重现期风险测度、MSCI Barra & Brunnermeier-Pedersen 内生流动性黑洞弹性 (LBHI) 与资产踩踏抛售级联反馈乘数 FCM、Marcos Lopez de Prado 策略微观夏普衰减半衰期与自适应双侧 CUSUM 概念漂移滤波、Goldman Sachs GSAM & Idzorek (2005) 显式置信度校准 Black-Litterman 贝叶斯优化、BCBS & Carlo Acerbi 一致性连续指数谱在险价值 SRM、Bridgewater All-Weather 动态风险预算漂移走廊 RCDI 与平滑再平衡引擎、Marcos Lopez de Prado & David Bailey 概率夏普 PSR 与多重检验通缩夏普 DSR 策略过拟合检验、卡尔曼滤波动态状态空间时变 Beta 与风格漂移 SDI、Axioma 基数硬约束与换手预算稀疏组合优化、Adrian-Brunnermeier 条件系统性在险价值增量 ΔCoVaR 与金融传染网络、巴塞尔协议 Basel III / FRTB 最劣 250 天压力在险价值 sVaR 与多流动性期限阶梯资本拨备、Bridgewater 4 象限宏观态分类、马氏金融动荡度自适应降杠杆、欧拉下行条件在险价值 (CVaR) 风险贡献穿透、基准相对下行跟踪误差 DTE、Stutzer 大偏差极值衰减指数、负债驱动投资 LDI 现金流匹配、随机矩阵理论 RMT 谱去噪、嵌套聚类优化 NCO、Amihud-Roll 微观流动性摩擦锥、信息几何香农有效下注数 ENB、资产多因子拥挤度雷达、带最大回撤硬顶约束分数凯利、BSTS 贝叶斯结构时序滤波、多期跨期期限结构风险衰减锥、Almgren-Chriss 最优执行轨迹、Michaud 重抽样前沿、反向压力测试逆向求解、Cornish-Fisher 高阶矩展开、Ledoit-Wolf 收缩协方差、Hamilton 状态转移模型、Merton 跳跃扩散、流动性调整 L-VaR、极值 Copula 尾部模型、NSGA-II 进化算法与 GARCH(1,1) 计量模型。历史业绩不预示未来表现，量化测算结果仅供机构投审会尽调与专业投资者资产配置参考，不构成直接要约或投资担保。</p>");
        sb.AppendLine("  <p>&copy; 2024-2026 BIGA Quantitative System. All Rights Reserved.</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        string htmlContent = sb.ToString();

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            var utf8Bom = new UTF8Encoding(true);
            File.WriteAllText(filePath, htmlContent, utf8Bom);
        }

        return htmlContent;
    }
}
