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

    private string _selectedMetric = "温度";
    public string SelectedMetric
    {
        get => _selectedMetric;
        set
        {
            if (SetProperty(ref _selectedMetric, value))
            {
                // 原本由生成器调用的 OnSelectedMetricChanged，这里直接在 setter 中处理
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

        // 查询 [From, To]（UTC）
        var from = new DateTimeOffset(FromDate.Date, TimeSpan.Zero);
        var to = new DateTimeOffset(ToDate.Date.AddDays(1).AddTicks(-1), TimeSpan.Zero);

        var list = await _db.Connection.Table<SensorData>()
            .Where(s => s.Timestamp >= from && s.Timestamp <= to)
            .OrderBy(s => s.Timestamp)
            .ToListAsync();

        foreach (var s in list) Items.Add(s);

        BuildChart();
    }

    private void BuildChart()
    {
        Func<SensorData, double> selector = SelectedMetric switch
        {
            "湿度" => s => s.Humidity,
            "烟雾" => s => s.Smoke,
            "光照" => s => s.Light,
            "CO2" => s => s.CO2,
            "水位" => s => s.WaterLevel,
            _ => s => s.Temperature
        };

        var entries = Items.Select(s => new Microcharts.ChartEntry((float)selector(s))
        {
            Color = SKColor.Parse("#3f51b5"),
            ValueLabel = selector(s).ToString("0.0")
        });

        Chart = new LineChart
        {
            Entries = entries.ToList(),
            LineMode = LineMode.Straight,
            LineSize = 3,
            PointMode = PointMode.Circle,
            PointSize = 3
        };
    }
}