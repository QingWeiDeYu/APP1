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

        // 按“本地自然日”构造起止，再用于比较（避免偏移不匹配）
        var tz = TimeZoneInfo.Local;

        var localStart = DateTime.SpecifyKind(FromDate.Date, DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(ToDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);

        var from = new DateTimeOffset(localStart, tz.GetUtcOffset(localStart));
        var to   = new DateTimeOffset(localEnd,   tz.GetUtcOffset(localEnd));

        var list = await _db.Connection.Table<SensorData>()
            .Where(s => s.Timestamp >= from && s.Timestamp <= to)
            .OrderBy(s => s.Timestamp)
            .ToListAsync();

        foreach (var s in list) Items.Add(s);

        BuildChart();
    }

    private void BuildChart()
    {
        // 当没有记录时，清空图表避免 Microcharts 对空序列做 Min/Max 导致异常
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
            _=> s => s.Temperature // 温度
        };

        // 过滤 NaN/Infinity，避免绘制时异常
        var values = Items
            .Select(s => (Value: selector(s), Data: s))
            .Where(x => !double.IsNaN(x.Value) && !double.IsInfinity(x.Value))
            .ToList();

        if (values.Count == 0)
        {
            Chart = null;
            return;
        }

        var entries = values.Select(x => new Microcharts.ChartEntry((float)x.Value)
        {
            Color = SKColor.Parse("#3f51b5"),
            ValueLabel = x.Value.ToString("0.0")
        }).ToList();

        Chart = new LineChart
        {
            Entries = entries,
            LineMode = LineMode.Straight,
            LineSize = 3,
            PointMode = PointMode.Circle,
            PointSize = 3
        };
    }
}