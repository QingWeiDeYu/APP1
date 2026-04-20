using System.Threading.Tasks;
using SmartAgri.Models;

namespace SmartAgri.Services;

public class AlarmService
{
    public static string BuildAlarmBits(SensorData d, Thresholds t)
    {
        // 位序: 温度,湿度,烟雾,光照,二氧化碳,水位
        char b(double v, double min, double max) => (v < min || v > max) ? '1' : '0';

        var bits = new char[6];
        bits[0] = b(d.Temperature, t.TempMin, t.TempMax);
        bits[1] = b(d.Humidity, t.HumMin, t.HumMax);
        bits[2] = b(d.Smoke, t.SmokeMin, t.SmokeMax);
        bits[3] = b(d.Light, t.LightMin, t.LightMax);
        bits[4] = b(d.CO2, t.CO2Min, t.CO2Max);
        bits[5] = b(d.WaterLevel, t.LevelMin, t.LevelMax);
        return new string(bits);
    }

    public static async Task PlayAlarmAsync(string bits)
    {
        if (bits.Contains('1'))
        {
            // 简单方式：文字播报或震动。避免额外多媒体依赖。
            await TextToSpeech.SpeakAsync("环境参数超限，请检查");
            try { Vibration.Vibrate(TimeSpan.FromSeconds(0.5)); } catch { }
        }
    }
}