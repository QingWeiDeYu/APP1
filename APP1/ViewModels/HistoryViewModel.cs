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

    // 用于绑定的集合（界面默认只填充最近 DefaultDisplayCount 条）
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
            // 降序：最新记录在前（方便取最新的 DefaultDisplayCount 条）
            .OrderByDescending(s => s.Timestamp)
            .ToListAsync();

        // 仅将最新的 DefaultDisplayCount 条加入 Items（界面默认显示不拥挤）
        foreach (var s in list.Take(DefaultDisplayCount)) Items.Add(s);

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
    }

    private void BuildChart()
    {
        if (Items.Count == 0)
        {
            Chart = null;
            ChartTitle = string.Empty;
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

        // 为避免 X 轴标签拥挤：最多显示约 12 个标签（含首尾）
        int count = valuesForChart.Count;
        int step = count > 12 ? (int)Math.Ceiling(count / 12.0) : 1;

        var entries = valuesForChart
            .Select((x, i) =>
            {
                // X 轴只显示本地时间（时:分:秒），其它点显示为空以避免拥挤
                var label = (i == 0 || i == count - 1 || i % step == 0)
                    ? x.Data.Timestamp.ToLocalTime().ToString("HH:mm:ss")
                    : string.Empty;

                return new Microcharts.ChartEntry((float)x.Value)
                {
                    Color = SKColor.Parse("#3f51b5"),
                    ValueLabel = x.Value.ToString("0.0"),
                    Label = label
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

            // 数值与标签均横向显示
            LabelOrientation = Orientation.Horizontal,
            ValueLabelOrientation = Orientation.Horizontal,

            // 可根据需要微调文字大小
            LabelTextSize = 18,
            ValueLabelTextSize = 18
        };
    }
}
