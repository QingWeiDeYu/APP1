using SQLite;

namespace SmartAgri.Models;

public class Thresholds
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public double TempMin { get; set; } = 0;
    public double TempMax { get; set; } = 50;

    public double HumMin { get; set; } = 0;
    public double HumMax { get; set; } = 100;

    public double SmokeMin { get; set; } = 0;
    public double SmokeMax { get; set; } = 1000;

    public double LightMin { get; set; } = 0;
    public double LightMax { get; set; } = 100000;

    public double CO2Min { get; set; } = 400;
    public double CO2Max { get; set; } = 5000;

    public double LevelMin { get; set; } = 0;
    public double LevelMax { get; set; } = 100;

    // 生成阈值同步报文
    public string ToProtocolString(string alarmFlagsBits)
    {
        return $"THR,{TempMin},{TempMax},{HumMin},{HumMax},{SmokeMin},{SmokeMax},{LightMin},{LightMax},{CO2Min},{CO2Max},{LevelMin},{LevelMax},{alarmFlagsBits}\r\n";
    }
}