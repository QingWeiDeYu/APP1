using System;

namespace SmartAgri.Models;

public enum RelayMode
{
    Immediate = 0, // 直接点击触发
    Schedule = 1   // 时间段触发
}

public class RelayButton
{
    [SQLite.PrimaryKey, SQLite.AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = "继电器";

    public int RelayId { get; set; } = 1; // 另一端区分继电器的编号

    public RelayMode Mode { get; set; } = RelayMode.Immediate;

    // 时间段采用 HHmm（本地时间），例如 0830 - 1730
    public string? StartHHmm { get; set; }
    public string? EndHHmm { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset? LastTriggeredAt { get; set; }
}