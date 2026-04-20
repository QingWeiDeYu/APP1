using SQLite;

namespace SmartAgri.Models;

public class SensorData
{
    [PrimaryKey, AutoIncrement]
    public long Id { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double Smoke { get; set; }
    public double Light { get; set; }
    public double CO2 { get; set; }
    public double WaterLevel { get; set; }
}