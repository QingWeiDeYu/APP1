using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage; // Preferences
using SmartAgri.Models;
using SmartAgri.Services;

namespace SmartAgri.ViewModels;

public partial class UsersViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly DatabaseService _db;

    // 显式实现，替代 [ObservableProperty] 字段（AOT/WinRT 兼容）
    private string newUsername = string.Empty;
    public string NewUsername
    {
        get => newUsername;
        set => SetProperty(ref newUsername, value);
    }

    private string newPassword = string.Empty;
    public string NewPassword
    {
        get => newPassword;
        set => SetProperty(ref newPassword, value);
    }

    private UserRole selectedRole = UserRole.User;
    public UserRole SelectedRole
    {
        get => selectedRole;
        set => SetProperty(ref selectedRole, value);
    }

    // 有效期（天）——WinRT AOT 兼容：使用手写属性
    private int newValidityDays = 10;
    public int NewValidityDays
    {
        get => newValidityDays;
        set => SetProperty(ref newValidityDays, value);
    }

    private string message = string.Empty;
    public string Message
    {
        get => message;
        set => SetProperty(ref message, value);
    }

    // 管理面板可见性与当前选择的用户
    private bool isManagePanelVisible;
    public bool IsManagePanelVisible
    {
        get => isManagePanelVisible;
        set => SetProperty(ref isManagePanelVisible, value);
    }

    private User? selectedUser;
    public User? SelectedUser
    {
        get => selectedUser;
        set
        {
            if (SetProperty(ref selectedUser, value))
            {
                OnSelectedUserChanged(value);
            }
        }
    }

    public ObservableCollection<User> Users { get; } = new();

    // 仅超级管理员可操作免登录模式
    public bool CanManageNoLogin => _auth.CurrentUser?.Role == UserRole.SuperAdmin;

    // 免登录模式开关（全局设置）
    public bool NoLoginEnabled
    {
        get => Preferences.Default.Get("no_login_enabled", false);
        set
        {
            Preferences.Default.Set("no_login_enabled", value);
            OnPropertyChanged();

            // 新的免登录说明
            Message = value
                ? "已开启免登录：未登录用户可直接进入仪表盘与历史记录，但无法访问权限功能！"
                : "已关闭免登录：权限功能需登录后使用。";
        }
    }

    // 依据选择用户动态启用/禁用按钮
    public bool CanActivate => SelectedUser is { IsActive: false };
    public bool CanDeactivate => SelectedUser is { IsActive: true };
    public bool CanDelete => SelectedUser != null;

    // 修正：不再使用 partial，改为普通私有方法，避免 CS0759
    private void OnSelectedUserChanged(User? value)
    {
        OnPropertyChanged(nameof(CanActivate));
        OnPropertyChanged(nameof(CanDeactivate));
        OnPropertyChanged(nameof(CanDelete));
    }

    public UsersViewModel(AuthService auth, DatabaseService db)
    {
        _auth = auth;
        _db = db;
    }

    public async Task LoadAsync()
    {
        Users.Clear();
        var list = await _db.Connection.Table<User>().OrderBy(u => u.Id).ToListAsync();
        foreach (var u in list) Users.Add(u);

        if (SelectedUser == null) SelectedUser = Users.FirstOrDefault();

        // 刷新权限相关的显示
        OnPropertyChanged(nameof(CanManageNoLogin));
        OnPropertyChanged(nameof(NoLoginEnabled));
        OnPropertyChanged(nameof(CanActivate));
        OnPropertyChanged(nameof(CanDeactivate));
        OnPropertyChanged(nameof(CanDelete));
    }

    [RelayCommand]
    private async Task AddUserAsync()
    {
        Message = string.Empty;
        if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewPassword))
        {
            Message = "请输入用户名和密码";
            return;
        }

        try
        {
            var days = Math.Max(1, NewValidityDays);
            await _auth.AddUserAsync(NewUsername.Trim(), NewPassword, SelectedRole, TimeSpan.FromDays(days));
            NewUsername = string.Empty;
            NewPassword = string.Empty;

            await LoadAsync();
            Message = "新增用户成功";
        }
        catch (Exception ex)
        {
            Message = $"新增失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RemoveUserAsync(User? user)
    {
        if (user == null) return;
        try
        {
            await _auth.RemoveUserAsync(user.Id);
            await LoadAsync();
            if (SelectedUser?.Id == user.Id) SelectedUser = Users.FirstOrDefault();
            Message = $"已删除用户 {user.Username}";
        }
        catch (Exception ex)
        {
            Message = $"删除失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeactivateUserAsync(User? user)
    {
        if (user == null) return;
        try
        {
            await _auth.DeactivateUserAsync(user.Id);
            await LoadAsync();
            // 仍保持选择，刷新按钮启用状态
            SelectedUser = Users.FirstOrDefault(u => u.Id == user.Id) ?? Users.FirstOrDefault();
            Message = $"已停用用户 {user.Username}";
        }
        catch (Exception ex)
        {
            Message = $"停用失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ActivateUserAsync(User? user)
    {
        if (user == null) return;
        try
        {
            await _auth.ActivateUserAsync(user.Id);
            await LoadAsync();
            SelectedUser = Users.FirstOrDefault(u => u.Id == user.Id) ?? Users.FirstOrDefault();
            Message = $"已启用用户 {user.Username}";
        }
        catch (Exception ex)
        {
            Message = $"启用失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenManagePanelAsync()
    {
        if (Users.Count == 0) await LoadAsync();
        IsManagePanelVisible = true;
    }

    [RelayCommand]
    private void CloseManagePanel()
        => IsManagePanelVisible = false;
}