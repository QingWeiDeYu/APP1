using SmartAgri.Models;
using SmartAgri.ViewModels;

namespace SmartAgri.Views;

public partial class UsersPage : ContentPage
{
    private readonly UsersViewModel _vm;

    public UsersPage(UsersViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        Loaded += async (_, __) => await _vm.LoadAsync();
    }

    private void OnRoleChanged(object? sender, EventArgs e)
    {
        if (sender is Picker p)
        {
            _vm.SelectedRole = p.SelectedIndex == 1 ? UserRole.SuperAdmin : UserRole.User;
        }
    }
}