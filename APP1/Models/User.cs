using System;
using SQLite; // 确保引用 sqlite-net-pcl 的命名空间

namespace SmartAgri.Models;

public enum UserRole
{
    SuperAdmin = 0,
    User = 1
}

public class User
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "IX_User_Username", Unique = true)]
    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.User;

    // 登录有效期
    public DateTimeOffset ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}