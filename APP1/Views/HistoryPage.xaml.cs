using SmartAgri.ViewModels;

namespace SmartAgri.Views;

public partial class HistoryPage : ContentPage
{
    public HistoryPage(HistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        Loaded += async (_, __) =>
        {
            try
            {
                await vm.QueryAsync(); // 确保 QueryAsync 是 public
            }
            catch (Exception ex)
            {
                await DisplayAlert("加载失败", $"历史数据加载出错：{ex.Message}", "确定");
            }
        };
    }
}