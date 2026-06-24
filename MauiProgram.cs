using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using PROXIMAMOP.Features.Live;
using PROXIMAMOP.Services;
using PROXIMAMOP.Services.Live;

#if ANDROID
using PROXIMAMOP.Platforms.Android;
using PROXIMAMOP.Platforms.Android.Services;
#endif

#if IOS
using PROXIMAMOP.Platforms.iOS.Services;
#endif

namespace PROXIMAMOP;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .AddAudio()
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler(typeof(LiveBroadcastWebView), typeof(LiveBroadcastWebViewHandler));
#endif
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<AppSettingsService>();
        builder.Services.AddSingleton<FeedService>();
        builder.Services.AddSingleton<FeedMonitorService>();
        builder.Services.AddSingleton<LiveTokenService>();
        builder.Services.AddSingleton<ILiveInAppBrowserService, DefaultLiveInAppBrowserService>();

#if ANDROID
        builder.Services.AddSingleton<INotificationService, AndroidNotificationService>();
        builder.Services.AddSingleton<IAudioPlaybackService, AndroidAudioPlaybackService>();
#elif IOS
        builder.Services.AddSingleton<INotificationService, IosNotificationService>();
#else
        builder.Services.AddSingleton<INotificationService, DefaultNotificationService>();
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        ServiceHelper.Services = app.Services;

        var feedMonitor = app.Services.GetRequiredService<FeedMonitorService>();
        feedMonitor.Start();

        return app;
    }
}