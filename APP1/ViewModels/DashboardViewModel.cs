using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
 
using SmartAgri.Models;
using SmartAgri.Services;
using Microsoft.Maui.ApplicationModel; // MainThread
using Microsoft.Maui.Storage;          // Preferences
using Microsoft.Maui.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Microcharts;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;         // Application.DisplayAlert

namespace SmartAgri.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly WifiClientService _wifi;

    private Thresholds _thresholds = new();
    private CancellationTokenSource? _cts;

    private const string PrefHost = "wifi_host";
    private const string PrefPort = "wifi_port";

    // AOT 兼容：替代 [ObservableProperty] private string host = "192.168.4.1";
    private string host = "192.168.4.1";
    public string Host
    {
        get => host;
        set
        {
            if (SetProperty(ref host, value))
            {
                OnHostChanged(value);
            }
        }
    }

    // 改为手写实现，避免 CS9248，同时满足 AOT 需求
    private int port = 5577;
    public int Port
    {
        get => port;
        set
        {
            if (SetProperty(ref port, value))
            {
                OnPortChanged(value);
            }
        }
    }

    // ===== 以下为你要求改造的属性：手写实现，替代 [ObservableProperty] 字段 =====
    private bool isConnected;
    public bool IsConnected
    {
        get => isConnected;
        set => SetProperty(ref isConnected, value);
    }

    private double temperature;
    public double Temperature
    {
        get => temperature;
        set => SetProperty(ref temperature, value);
    }

    private double humidity;
    public double Humidity
    {
        get => humidity;
        set => SetProperty(ref humidity, value);
    }

    private double smoke;
    public double Smoke
    {
        get => smoke;
        set => SetProperty(ref smoke, value);
    }

    private double light;
    public double Light
    {
        get => light;
        set => SetProperty(ref light, value);
    }

    // 注意：保持公开属性名为 CO2
    private double cO2;
    public double CO2
    {
        get => cO2;
        set => SetProperty(ref cO2, value);
    }

    private double waterLevel;
    public double WaterLevel
    {
        get => waterLevel;
        set => SetProperty(ref waterLevel, value);
    }

    // 阈值编辑绑定
    private double tempMin;
    public double TempMin
    {
        get => tempMin;
        set => SetProperty(ref tempMin, value);
    }

    private double tempMax;
    public double TempMax
    {
        get => tempMax;
        set => SetProperty(ref tempMax, value);
    }

    private double humMin;
    public double HumMin
    {
        get => humMin;
        set => SetProperty(ref humMin, value);
    }

    private double humMax;
    public double HumMax
    {
        get => humMax;
        set => SetProperty(ref humMax, value);
    }

    private double smokeMin;
    public double SmokeMin
    {
        get => smokeMin;
        set => SetProperty(ref smokeMin, value);
    }

    private double smokeMax;
    public double SmokeMax
    {
        get => smokeMax;
        set => SetProperty(ref smokeMax, value);
    }

    private double lightMin;
    public double LightMin
    {
        get => lightMin;
        set => SetProperty(ref lightMin, value);
    }

    private double lightMax;
    public double LightMax
    {
        get => lightMax;
        set => SetProperty(ref lightMax, value);
    }

    private double cO2Min;
    public double CO2Min
    {
        get => cO2Min;
        set => SetProperty(ref cO2Min, value);
    }

    private double cO2Max;
    public double CO2Max
    {
        get => cO2Max;
        set => SetProperty(ref cO2Max, value);
    }

    private double levelMin;
    public double LevelMin
    {
        get => levelMin;
        set => SetProperty(ref levelMin, value);
    }

    private double levelMax;
    public double LevelMax
    {
        get => levelMax;
        set => SetProperty(ref levelMax, value);
    }
    // ===== 手写属性结束 =====

    public ObservableCollection<SensorData> Recent { get; } = new();
    public ObservableCollection<RelayButton> Relays { get; } = new();

    // AOT 兼容：替代 [ObservableProperty] private LineChart? tempChart;
    private LineChart? tempChart;
    public LineChart? TempChart
    {
        get => tempChart;
        set => SetProperty(ref tempChart, value);
    }

    public DashboardViewModel(DatabaseService db, WifiClientService wifi)
    {
        _db = db; _wifi = wifi;
        _wifi.SensorDataReceived += OnSensorDataReceived;
        _wifi.ConnectionStateChanged += (_, s) => IsConnected = s;

        // 启动时恢复上次 Host/Port
        Host = Preferences.Default.Get(PrefHost, "192.168.4.1");
        Port = Preferences.Default.Get(PrefPort, 5577);
    }

    public async Task InitializeAsync()
    {
        _thresholds = await _db.Connection.Table<Thresholds>().FirstAsync();
        ApplyThresholdsToVm(_thresholds);

        // 加载继电器按钮（可在 UI 中增删改）
        var relays = await _db.Connection.Table<RelayButton>().ToListAsync();
        foreach (var r in relays) Relays.Add(r);
    }

    private void ApplyThresholdsToVm(Thresholds t)
    {
        TempMin = t.TempMin; TempMax = t.TempMax;
        HumMin = t.HumMin; HumMax = t.HumMax;
        SmokeMin = t.SmokeMin; SmokeMax = t.SmokeMax;
        LightMin = t.LightMin; LightMax = t.LightMax;
        CO2Min = t.CO2Min; CO2Max = t.CO2Max;
        LevelMin = t.LevelMin; LevelMax = t.LevelMax;
    }

    // 当 Host/Port 被用户修改时自动持久化
    //partial void OnHostChanged(string value)
    //    => Preferences.Default.Set(PrefHost, value ?? string.Empty);
    private void OnHostChanged(string value)
    => Preferences.Default.Set(PrefHost, value ?? string.Empty);


    private void OnPortChanged(int value)
    {
        var safe = Math.Clamp(value, 1, 65535);
        if (safe != value) Port = safe; // 回写合法范围
        Preferences.Default.Set(PrefPort, safe);
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        // 已连接时提示并返回，避免重复连接
        if (_wifi.IsConnected)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Shell.Current is { } shell)
                    await shell.DisplayAlert("提示", $"已连接到 {Host}:{Port}", "确定");
            });
            return;
        }

        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            await _wifi.ConnectAsync(Host, Port, _cts.Token);

            Preferences.Default.Set(PrefHost, Host);
            Preferences.Default.Set(PrefPort, Port);

            await _wifi.SendThresholdsAsync(_thresholds, "000000");

            _ = Task.Run(async () =>
            {
                while (!_cts!.IsCancellationRequested)
                {
                    try { await _wifi.SendPingAsync(); } catch { }
                    await Task.Delay(TimeSpan.FromSeconds(10), _cts.Token);
                }
            }, _cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _cts?.Cancel();
            _cts = null;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var msg = $"无法连接到 {Host}:{Port}\n{ex.Message}";
                if (Shell.Current is { } shell)
                    await shell.DisplayAlert("连接失败", msg, "确定");
            });
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        try
        {
            _cts?.Cancel(); // 停止心跳
            await _wifi.DisconnectAsync();
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Shell.Current is { } shell)
                    await shell.DisplayAlert("断开失败", ex.Message, "确定");
            });
        }
        finally
        {
            IsConnected = false; // 保险置位（服务也会触发 ConnectionStateChanged=false）
        }
    }

    [RelayCommand]
    private async Task SaveThresholdsAsync()
    {
        _thresholds.TempMin = TempMin; _thresholds.TempMax = TempMax;
        _thresholds.HumMin = HumMin; _thresholds.HumMax = HumMax;
        _thresholds.SmokeMin = SmokeMin; _thresholds.SmokeMax = SmokeMax;
        _thresholds.LightMin = LightMin; _thresholds.LightMax = LightMax;
        _thresholds.CO2Min = CO2Min; _thresholds.CO2Max = CO2Max;
        _thresholds.LevelMin = LevelMin; _thresholds.LevelMax = LevelMax;

        await _db.Connection.UpdateAsync(_thresholds);

        // 实时下发阈值
        await _wifi.SendThresholdsAsync(_thresholds, "000000");
    }

    private async void OnSensorDataReceived(object? sender, SensorData e)
    {
        Temperature = e.Temperature;
        Humidity = e.Humidity;
        Smoke = e.Smoke;
        Light = e.Light;
        CO2 = e.CO2;
        WaterLevel = e.WaterLevel;

        // 保存到本地
        await _db.Connection.InsertAsync(e);

        // 维护最近数据（做图表）
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Recent.Add(e);
            while (Recent.Count > 100) Recent.RemoveAt(0);
            TempChart = BuildTempChart();
        });

        // 阈值判断与告警
        var bits = AlarmService.BuildAlarmBits(e, _thresholds);
        if (bits.Contains('1'))
        {
            await AlarmService.PlayAlarmAsync(bits);
            await _wifi.SendAlarmAsync(bits);
        }
    }

    private LineChart BuildTempChart()
    {
        var entries = Recent.Select(d => new ChartEntry((float)d.Temperature)
        {
            Color = SKColor.Parse("#ff5722"),
            ValueLabel = d.Temperature.ToString("0.0")
        }).ToList();

        return new LineChart
        {
            Entries = entries,
            LineMode = LineMode.Straight,
            LineSize = 3,
            PointMode = PointMode.Circle,
            PointSize = 3
        };
    }

    [RelayCommand]
    private async Task TriggerRelayAsync(RelayButton relay)
    {
        if (relay.Mode == RelayMode.Immediate)
        {
            await _wifi.SendRelayImmediateAsync(relay.RelayId);
            relay.LastTriggeredAt = DateTimeOffset.Now;
            await _db.Connection.UpdateAsync(relay);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(relay.StartHHmm) && !string.IsNullOrWhiteSpace(relay.EndHHmm))
            {
                await _wifi.SendRelayScheduleAsync(relay.RelayId, relay.StartHHmm!, relay.EndHHmm!);
            }
        }
    }
}