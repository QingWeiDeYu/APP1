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

    public ObservableCollection<SensorData> Items { get; } = new();

    public HistoryViewModel(DatabaseService db) => _db = db;

    [RelayCommand]
    public async Task QueryAsync()
    {
        Items.Clear();

        // 用户选择的是“本地日期”，将其转换为本地时间的日至止，再统一转为 UTC 做查询
        var localStart = DateTime.SpecifyKind(FromDate.Date, DateTimeKind.Local);
        var localEndInclusive = DateTime.SpecifyKind(ToDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local);

        var fromUtc = new DateTimeOffset(localStart).ToUniversalTime();
        var toUtc   = new DateTimeOffset(localEndInclusive).ToUniversalTime();

        var list = await _db.Connection.Table<SensorData>()
            .Where(s => s.Timestamp >= fromUtc && s.Timestamp <= toUtc)
            // 改为降序：最新记录在前（UI 列表保持最新在前）
            .OrderByDescending(s => s.Timestamp)
            .ToListAsync();

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
    }

    private void BuildChart()
    {
        if (Items.Count == 0)
        {
            Chart = null;
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
            return;
        }

        // Items 在查询时已按时间降序（最新在前）。
        // 为了让图表从早到晚绘制（时间升序），对 values 按时间升序排序生成 entries。
        var valuesForChart = values.OrderBy(x => x.Data.Timestamp).ToList();

        // 为避免 X 轴标签拥挤：最多显示约 12 个标签（含首尾）
        int count = valuesForChart.Count;
        int step = count > 12 ? (int)Math.Ceiling(count / 12.0) : 1;

        var entries = valuesForChart
            .Select((x, i) =>
            {
                // 本地时间的 “MM-dd HH:mm” 作为 X 轴标签
                var label = (i == 0 || i == count - 1 || i % step == 0)
                    ? x.Data.Timestamp.ToLocalTime().ToString("MM-dd HH:mm")
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
