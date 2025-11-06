using Microsoft.Maui;                   // IActivationState
using Microsoft.Maui.ApplicationModel; // MainThread
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;          // Preferences
using SmartAgri;                 // AppShell
using SmartAgri.Services;
using SmartAgri.ViewModels;
using SmartAgri.Views;
using SmartAgri.Models;         // UserRole
using System.Linq;

namespace APP1;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var db = new DatabaseService();
        Task.Run(() => db.InitializeAsync()).GetAwaiter().GetResult();

        var wifi = new WifiClientService();
        var auth = new AuthService(db);

        var loginVm = new LoginViewModel(auth);
        var dashboardVm = new DashboardViewModel(db, wifi);
        var usersVm = new UsersViewModel(auth, db);
        var historyVm = new HistoryViewModel(db);

        var shell = new AppShell();
        shell.Items.Clear();

        // 等待页（临时）
        var autoLoginContent = new ShellContent { Route = "AutoLogin", Title = "启动中", Content = new AutoLoginPage() };

        // 正常页面
        var loginContent = new ShellContent { Route = "LoginPage", Title = "登录", Content = new LoginPage(loginVm) };
        var dashboardContent = new ShellContent { Route = "DashboardPage", Title = "仪表盘", Content = new DashboardPage(dashboardVm) };
        var usersContent = new ShellContent { Route = "UsersPage", Title = "用户管理", Content = new UsersPage(usersVm) };
        var historyContent = new ShellContent { Route = "HistoryPage", Title = "历史记录", Content = new HistoryPage(historyVm) };

        // 先添加等待页
        shell.Items.Add(autoLoginContent);
        shell.Items.Add(loginContent);
        shell.Items.Add(dashboardContent);
        shell.Items.Add(usersContent);
        shell.Items.Add(historyContent);

        // 默认禁用菜单
        shell.FlyoutBehavior = FlyoutBehavior.Disabled;

        // 导航拦截：未登录禁止访问；免登录开放仪表盘/历史记录；用户管理需超管
        var restricted = new[] { "DashboardPage", "UsersPage", "HistoryPage" };
        shell.Navigating += async (_, e) =>
        {
            try
            {
                var target = e.Target?.Location?.OriginalString ?? string.Empty;
                var toRestricted = restricted.Any(r => target.Contains(r, StringComparison.OrdinalIgnoreCase));
                var toUsers = target.Contains("UsersPage", StringComparison.OrdinalIgnoreCase);
                var noLoginEnabled = Preferences.Default.Get("no_login_enabled", false);

                if (toRestricted && !auth.IsAuthenticated)
                {
                    if (noLoginEnabled && !toUsers)
                        return; // 免登录放行 仪表盘/历史记录

                    e.Cancel();
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        shell.DisplayAlert("提示", "请先登录后再使用该功能。", "确定"));
                    return;
                }

                if (toUsers && auth.IsAuthenticated && auth.CurrentUser?.Role != UserRole.SuperAdmin)
                {
                    e.Cancel();
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        shell.DisplayAlert("权限不足", "只有超级管理员可以访问用户管理。", "确定"));
                    return;
                }
            }
            catch { }
        };

        // 登录后：启用菜单
        auth.LoggedIn += (_, __) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                shell.FlyoutBehavior = FlyoutBehavior.Flyout;
                loginContent.Title = "切换账号";
            });
        };

        // 登出：禁用菜单
        auth.LoggedOut += (_, __) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                shell.FlyoutBehavior = FlyoutBehavior.Disabled;
                loginContent.Title = "登录";
                _ = shell.GoToAsync("//LoginPage");
            });
        };

        // 初始切到等待页
        shell.CurrentItem = autoLoginContent;

        // 启动后异步执行免登录/跳转
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300); // 防闪屏
                var noLogin = Preferences.Default.Get("no_login_enabled", false);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (noLogin)
                    {
                        // 访客模式：不开启管理员权限，但放行业务功能
                        shell.FlyoutBehavior = FlyoutBehavior.Flyout;
                        shell.CurrentItem = dashboardContent;
                    }
                    else
                    {
                        shell.CurrentItem = loginContent;
                    }

                    shell.Items.Remove(autoLoginContent); // 移除临时等待页
                });
            }
            catch
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    shell.CurrentItem = loginContent;
                    shell.Items.Remove(autoLoginContent);
                });
            }
        });

        // 关键：通过 CreateWindow 返回窗口，避免设置 MainPage
        return new Window(shell);
    }
}