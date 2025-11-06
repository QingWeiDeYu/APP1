using System;
using System.IO;
using System.Threading.Tasks;
using SQLite;
using SmartAgri.Models;
using Microsoft.Maui.Storage; // FileSystem

namespace SmartAgri.Services;

public class DatabaseService
{
    private readonly SQLiteAsyncConnection _conn;
    public SQLiteAsyncConnection Connection => _conn;

    public DatabaseService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "smartagri.db3");
        _conn = new SQLiteAsyncConnection(dbPath);
    }

    public async Task InitializeAsync()
    {
        // 建表（幂等）
        await _conn.CreateTableAsync<User>();
        await _conn.CreateTableAsync<SensorData>();
        await _conn.CreateTableAsync<Thresholds>();
        await _conn.CreateTableAsync<RelayButton>();

        // 默认阈值
        if (await _conn.Table<Thresholds>().CountAsync() == 0)
            await _conn.InsertAsync(new Thresholds());

        // 以“年”为单位配置种子账号有效期
        const int SeedAdminValidityYears = 100;

        // 首次启动：种子账号 admin/admin123（超级管理员，按年计算有效期）
        var admin = await _conn.Table<User>()
            .Where(u => u.Username == "admin")
            .FirstOrDefaultAsync();

        if (admin is null)
        {
            var hash = AuthService.HashPassword("admin123"); // 与登录同一算法
            admin = new User
            {
                Username = "admin",
                PasswordHash = hash,
                Role = UserRole.SuperAdmin,
                ExpiresAt = DateTimeOffset.UtcNow.AddYears(SeedAdminValidityYears),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _conn.InsertAsync(admin);
        }
        else
        {
            // 已存在但过期：自动顺延 SeedAdminValidityYears 年，并确保启用
            if (admin.ExpiresAt < DateTimeOffset.UtcNow)
            {
                admin.ExpiresAt = DateTimeOffset.UtcNow.AddYears(SeedAdminValidityYears);
                admin.IsActive = true;
                await _conn.UpdateAsync(admin);
            }
        }
    }
}