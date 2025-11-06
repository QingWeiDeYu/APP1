using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartAgri.Models;
using SmartAgri.Services;

namespace SmartAgri.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private string _message = string.Empty;
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public User? CurrentUser { get; private set; }

    public LoginViewModel(AuthService auth) => _auth = auth;

    [RelayCommand]
    private async Task LoginAsync()
    {
        Message = string.Empty;
        var user = await _auth.LoginAsync(Username, Password);
        if (user == null)
        {
            Message = "登录失败或账号过期";
            return;
        }
        CurrentUser = user;
        // 跳转到仪表盘
        await Shell.Current.GoToAsync("//DashboardPage");
    }
}