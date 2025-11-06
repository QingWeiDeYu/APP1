using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SmartAgri.Models;

namespace SmartAgri.Services;

public class AuthService
{
    private readonly DatabaseService _db;

    public AuthService(DatabaseService db) => _db = db;

    public User? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser != null;

    public event EventHandler<User>? LoggedIn;
    public event EventHandler? LoggedOut;

    public static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    public async Task<User?> LoginAsync(string username, string password)
    {
        var hash = HashPassword(password);
        var user = await _db.Connection.Table<User>()
            .Where(u => u.Username == username && u.PasswordHash == hash && u.IsActive)
            .FirstOrDefaultAsync();

        if (user == null) return null;
        if (user.ExpiresAt < DateTimeOffset.UtcNow) return null;

        CurrentUser = user;
        LoggedIn?.Invoke(this, user);
        return user;
    }

    // 免密自动登录（用于“免登录模式”）
    public async Task<User?> TryAutoLoginAsync(string username)
    {
        var user = await _db.Connection.Table<User>()
            .Where(u => u.Username == username && u.IsActive)
            .FirstOrDefaultAsync();

        if (user == null) return null;
        if (user.ExpiresAt < DateTimeOffset.UtcNow) return null;

        CurrentUser = user;
        LoggedIn?.Invoke(this, user);
        return user;
    }

    public void Logout()
    {
        if (CurrentUser != null)
        {
            CurrentUser = null;
            LoggedOut?.Invoke(this, EventArgs.Empty);
        }
    }

    public Task<int> AddUserAsync(string username, string password, UserRole role, TimeSpan validity)
    {
        var user = new User
        {
            Username = username,
            PasswordHash = HashPassword(password),
            Role = role,
            ExpiresAt = DateTimeOffset.UtcNow.Add(validity), // 基于 UTC
            IsActive = true
        };
        return _db.Connection.InsertAsync(user);
    }

    public Task<int> RemoveUserAsync(int userId)
        => _db.Connection.DeleteAsync<User>(userId);

    public Task<int> DeactivateUserAsync(int userId)
        => _db.Connection.ExecuteAsync("UPDATE User SET IsActive=0 WHERE Id=?", userId);

    public Task<int> ActivateUserAsync(int userId)
        => _db.Connection.ExecuteAsync("UPDATE User SET IsActive=1 WHERE Id=?", userId);
}