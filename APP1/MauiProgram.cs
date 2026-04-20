using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Controls.Hosting;
using Microcharts.Maui;

namespace APP1;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // 初始化 SQLitePCL（sqlite-net-pcl 的底层依赖）
        SQLitePCL.Batteries_V2.Init();

        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMicrocharts()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        return builder.Build();
    }
}
