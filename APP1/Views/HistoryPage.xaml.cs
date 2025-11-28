using System;
using Microsoft.Maui.Controls;
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

    // async void 是页面事件处理器的常见写法
    async void OnClearAllClicked(object sender, EventArgs e)
    {
        if (BindingContext is not HistoryViewModel vm)
            return;

        var accept = await DisplayAlert("确认", "是否清除所有历史记录？此操作无法恢复。", "确定", "取消");
        if (!accept)
            return;

        // 直接调用 ViewModel 中的异步方法（方法在你的 ViewModel 中已存在）
        await vm.ClearAllHistoryAsync();
    }
}