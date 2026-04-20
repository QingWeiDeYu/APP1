using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartAgri.Models;

namespace SmartAgri.Services;

public class WifiClientService
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;

    public bool IsConnected => _client?.Connected == true;

    public event EventHandler<SensorData>? SensorDataReceived;
    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<string>? RawMessageReceived;

    public async Task ConnectAsync(string host, int port, CancellationToken token = default)
    {
        await DisconnectAsync();

        _client = new TcpClient();
        await _client.ConnectAsync(host, port, token);
        _stream = _client.GetStream();
        _reader = new StreamReader(_stream, Encoding.UTF8);
        _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };

        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _ = Task.Run(() => ReadLoopAsync(_cts.Token));
        ConnectionStateChanged?.Invoke(this, true);
    }

    public async Task DisconnectAsync()
    {
        try { _cts?.Cancel(); } catch { }
        try { _reader?.Dispose(); } catch { }
        try { _writer?.Dispose(); } catch { }
        try { _stream?.Dispose(); } catch { }
        try { _client?.Close(); } catch { }
        _reader = null; _writer = null; _stream = null; _client = null;
        ConnectionStateChanged?.Invoke(this, false);
        await Task.CompletedTask;
    }

    public Task SendAsync(string line)
    {
        if (_writer == null) return Task.CompletedTask;
        return _writer.WriteAsync(line);
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _reader != null)
            {
                var line = await _reader.ReadLineAsync();
                if (line == null) break;

                RawMessageReceived?.Invoke(this, line);

                // 简单协议解析
                if (line.StartsWith("DATA,", StringComparison.OrdinalIgnoreCase))
                {
                    // DATA,timestamp,Temp,Hum,Smoke,Light,CO2,Level
                    // timestamp 可为秒或毫秒，或 "now"
                    var parts = line.Split(',');
                    if (parts.Length >= 8)
                    {
                        var data = new SensorData
                        {
                            Timestamp = DateTimeOffset.UtcNow,
                            Temperature = ParseDouble(parts[2]),
                            Humidity = ParseDouble(parts[3]),
                            Smoke = ParseDouble(parts[4]),
                            Light = ParseDouble(parts[5]),
                            CO2 = ParseDouble(parts[6]),
                            WaterLevel = ParseDouble(parts[7])
                        };
                        SensorDataReceived?.Invoke(this, data);
                    }
                }
                else if (line.StartsWith("PONG", StringComparison.OrdinalIgnoreCase))
                {
                    // 心跳响应
                }
                // 可扩展其它指令
            }
        }
        catch
        {
            // 连接失败/对端关闭
        }
        finally
        {
            await DisconnectAsync();
        }
    }

    private static double ParseDouble(string s)
        => double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v : 0;

    // 对端心跳
    public Task SendPingAsync() => SendAsync("PING\r\n");

    // 同步阈值
    public Task SendThresholdsAsync(Thresholds thr, string alarmBits)
        => SendAsync(thr.ToProtocolString(alarmBits));

    // 继电器立即触发
    public Task SendRelayImmediateAsync(int relayId)
        => SendAsync($"RELAY,{relayId},1\r\n");

    // 继电器时间段触发（下发到对端让其在时间段内自行控制，也可本端定时触发——按需二选一）
    public Task SendRelayScheduleAsync(int relayId, string startHHmm, string endHHmm)
        => SendAsync($"RELAY_SCHED,{relayId},{startHHmm}-{endHHmm}\r\n");

    // 告警上行（手机->另一端）
    public Task SendAlarmAsync(string alarmBits)
        => SendAsync($"ALARM,{DateTimeOffset.UtcNow.ToUnixTimeSeconds()},{alarmBits}\r\n");
}