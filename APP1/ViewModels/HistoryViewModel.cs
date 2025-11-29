using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microcharts;
using SkiaSharp;
using SmartAgri.Models;
using SmartAgri.Services;

namespace SmartAgri.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    // 默认在界面上显示的记录数
    private const int DefaultDisplayCount = 10;

    private DateTime _fromDate = DateTime.Today.AddDays(-1);
    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    private DateTime _toDate = DateTime.Today;
    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    // 新增：时间选择（默认从 00:00 到 23:59:59）
    private TimeSpan _fromTime = TimeSpan.Zero;
    public TimeSpan FromTime
    {
        get => _fromTime;
        set => SetProperty(ref _fromTime, value);
    }

    private TimeSpan _toTime = new TimeSpan(23, 59, 59);
    public TimeSpan ToTime
    {
        get => _toTime;
        set => SetProperty(ref _toTime, value);
    }

    // 图表标题（顶部显示日期/时间范围）
    private string _chartTitle = string.Empty;
    public string ChartTitle
    {
        get => _chartTitle;
        set => SetProperty(ref _chartTitle, value);
    }

    // 新增：图表下方固定显示的三个时间标签（开始 / 中点 / 结束）
    private string _startLabel = string.Empty;
    public string StartLabel
    {
        get => _startLabel;
        set => SetProperty(ref _startLabel, value);
    }

    private string _midLabel = string.Empty;
    public string MidLabel
    {
        get => _midLabel;
        set => SetProperty(ref _midLabel, value);
    }

    private string _endLabel = string.Empty;
    public string EndLabel
    {
        get => _endLabel;
        set => SetProperty(ref _endLabel, value);
    }

    // 默认指标：温度
    private string _selectedMetric = "温度";
    public string SelectedMetric
    {
        get => _selectedMetric;
        set
        {
            if (SetProperty(ref _selectedMetric, value))
            {
                BuildChart();
            }
        }
    }

    private LineChart? _chart;
    public LineChart? Chart
    {
        get => _chart;
        set => SetProperty(ref _chart, value);
    }

    public ObservableCollection<string> Metrics { get; } = new()
    {
        "温度","湿度","烟雾","光照","CO2","水位"
    };

    // 用于绑定的集合（现在显示所有匹配的记录）
    public ObservableCollection<SensorData> Items { get; } = new();

    public HistoryViewModel(DatabaseService db) => _db = db;

    [RelayCommand]
    public async Task QueryAsync()
    {
        Items.Clear();

        // 将用户选择的本地日期与时间组合，然后统一转为 UTC 做查询
        var localStart = DateTime.SpecifyKind(FromDate.Date + FromTime, DateTimeKind.Local);
        var localEndInclusive = DateTime.SpecifyKind(ToDate.Date + ToTime, DateTimeKind.Local);

        // 如果结束时间早于开始时间（用户误选），交换或将结束设为开始
        if (localEndInclusive < localStart)
        {
            var tmp = localEndInclusive;
            localEndInclusive = localStart;
            localStart = tmp;
        }

        var fromUtc = new DateTimeOffset(localStart).ToUniversalTime();
        var toUtc   = new DateTimeOffset(localEndInclusive).ToUniversalTime();

        var list = await _db.Connection.Table<SensorData>()
            .Where(s => s.Timestamp >= fromUtc && s.Timestamp <= toUtc)
            // 降序：最新记录在前
            .OrderByDescending(s => s.Timestamp)
            .ToListAsync();

        // 取消原有的只显示最新 10 条限制 —— 现在将所有匹配记录加入 Items
        foreach (var s in list) Items.Add(s);

        BuildChart();
    }

    // 新增命令：清除所有历史（仅删除 SensorData 表）
    [RelayCommand]
    public async Task ClearAllHistoryAsync()
    {
        await _db.Connection.DeleteAllAsync<SensorData>();
        try { await _db.Connection.ExecuteAsync("VACUUM"); } catch { /* 部分平台可能不支持 */ }

        Items.Clear();
        Chart = null;
        ChartTitle = string.Empty;

        // 清空下方固定标签
        StartLabel = MidLabel = EndLabel = string.Empty;
    }

    private void BuildChart()
    {
        if (Items.Count == 0)
        {
            Chart = null;
            ChartTitle = string.Empty;

            // 清空下方固定标签
            StartLabel = MidLabel = EndLabel = string.Empty;
            return;
        }

        Func<SensorData, double> selector = SelectedMetric switch
        {
            "湿度" => s => s.Humidity,
            "烟雾" => s => s.Smoke,
            "光照" => s => s.Light,
            "CO2"  => s => s.CO2,
            "水位" => s => s.WaterLevel,
            _=> s => s.Temperature
        };

        var values = Items
            .Select(s => (Value: selector(s), Data: s))
            .Where(x => !double.IsNaN(x.Value) && !double.IsInfinity(x.Value))
            .ToList();

        if (values.Count == 0)
        {
            Chart = null;
            ChartTitle = string.Empty;

            // 清空下方固定标签
            StartLabel = MidLabel = EndLabel = string.Empty;
            return;
        }

        // Items 在查询时已按时间降序（最新在前）。
        // 为了让图表从早到晚绘制（时间升序），对 values 按时间升序排序生成 entries。
        var valuesForChart = values.OrderBy(x => x.Data.Timestamp).ToList();

        // 设置图表标题为日期/时间范围（更细粒度：如果在同一天显示时间区间）
        var firstTs = valuesForChart.First().Data.Timestamp.ToLocalTime();
        var lastTs = valuesForChart.Last().Data.Timestamp.ToLocalTime();
        if (firstTs.Date == lastTs.Date)
        {
            // 同一天：显示日期 + 时间范围（若为整天可只显示日期）
            bool isFullDay = firstTs.TimeOfDay == TimeSpan.Zero && lastTs.TimeOfDay.TotalSeconds >= 86399;
            ChartTitle = isFullDay
                ? firstTs.ToString("yyyy-MM-dd")
                : $"{firstTs:yyyy-MM-dd} {firstTs:HH:mm:ss} - {lastTs:HH:mm:ss}";
        }
        else
        {
            ChartTitle = $"{firstTs:yyyy-MM-dd HH:mm:ss} 至 {lastTs:yyyy-MM-dd HH:mm:ss}";
        }

        int count = valuesForChart.Count;

        // 固定时间点：开始 / 时间中点 / 结束（文本内容固定，不随数据量增减改变）
        var targetStart = valuesForChart.First().Data.Timestamp;
        var targetEnd = valuesForChart.Last().Data.Timestamp;
        var targetMid = targetStart + TimeSpan.FromTicks((targetEnd - targetStart).Ticks / 2);

        // 找到在当前数据中最接近这三个固定时间的索引（用于确定标签显示在图上哪个数据点位置）
        int FindNearestIndex(DateTimeOffset t)
        {
            long bestDiff = long.MaxValue;
            int bestIdx = 0;
            for (int i = 0; i < count; i++)
            {
                var diff = Math.Abs((valuesForChart[i].Data.Timestamp - t).Ticks);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        int idxStart = FindNearestIndex(targetStart);
        int idxMid = FindNearestIndex(targetMid);
        int idxEnd = FindNearestIndex(targetEnd);

        // 确保 idxStart <= idxMid <= idxEnd（如果存在交叉则调整）
        if (idxStart > idxMid) idxMid = idxStart;
        if (idxMid > idxEnd) idxMid = idxEnd;

        // 文本显示使用固定的目标时间（固定格式），不随 nearest point 的实际时间变化
        var labelStartText = targetStart.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        var labelMidText = targetMid.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        var labelEndText = targetEnd.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        // 将固定文本绑定到外部标签（XAML 中的三个 Label）
        StartLabel = labelStartText;
        MidLabel = labelMidText;
        EndLabel = labelEndText;

        // 为避免 Microcharts 在点下方绘制过长标签导致折叠，我们这里不在 Entry.Label 输出完整时间（保持空）。
        // 只在特定需要时显示 ValueLabel（示例保留一位小数）
        var entries = valuesForChart
            .Select((x, i) =>
            {
                return new Microcharts.ChartEntry((float)x.Value)
                {
                    Color = SKColor.Parse("#3f51b5"),
                    ValueLabel = x.Value.ToString("0.0"),
                    Label = string.Empty
                };
            })
            .ToList();

        Chart = new LineChart
        {
            Entries = entries,
            LineMode = LineMode.Straight,
            LineSize = 3,
            PointMode = PointMode.Circle,
            PointSize = 3,

            // 数值与标签均横向显示（此处 Entry.Label 为空，外部固定标签显示时间）
            LabelOrientation = Orientation.Horizontal,
            ValueLabelOrientation = Orientation.Horizontal,

            // 可根据需要微调文字大小
            LabelTextSize = 14,
            ValueLabelTextSize = 12
        };
    }
}
