using SmartAgri.ViewModels;

namespace SmartAgri.Views;

public partial class DashboardPage : ContentPage
{
    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        Loaded += async (_, __) => await vm.InitializeAsync();
    }
}