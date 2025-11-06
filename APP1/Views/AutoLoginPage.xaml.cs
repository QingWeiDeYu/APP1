using Microsoft.Maui.Controls;

namespace SmartAgri.Views;

public partial class AutoLoginPage : ContentPage
{
    public AutoLoginPage() => InitializeComponent();

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        // 优先使用 Shell.Current，避免使用已弃用的 Application.Current.MainPage
        var shell = Shell.Current ?? this.Window?.Page as Shell;
        if (shell is not null)
        {
            await shell.GoToAsync("//LoginPage");
        }
    }
}