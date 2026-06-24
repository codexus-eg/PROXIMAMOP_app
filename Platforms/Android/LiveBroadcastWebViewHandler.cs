using Android;
using Android.Content.PM;
using Android.Views;
using Android.Webkit;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Handlers;
using AWebView = Android.Webkit.WebView;

namespace PROXIMAMOP.Platforms.Android;

public sealed class LiveBroadcastWebViewHandler : WebViewHandler
{
    protected override AWebView CreatePlatformView()
    {
        var webView = base.CreatePlatformView();
        ConfigureWebView(webView);
        return webView;
    }

    protected override void ConnectHandler(AWebView platformView)
    {
        base.ConnectHandler(platformView);
        ConfigureWebView(platformView);
    }

    private static void ConfigureWebView(AWebView webView)
    {
        var settings = webView.Settings;
        if (settings is null)
            return;

        settings.JavaScriptEnabled = true;
        settings.DomStorageEnabled = true;
        settings.DatabaseEnabled = true;
        settings.AllowFileAccess = true;
        settings.AllowContentAccess = true;
        settings.JavaScriptCanOpenWindowsAutomatically = true;
        settings.SetSupportMultipleWindows(false);
        settings.MediaPlaybackRequiresUserGesture = false;
        settings.LoadWithOverviewMode = true;
        settings.UseWideViewPort = true;
        settings.CacheMode = CacheModes.Default;

        try
        {
            settings.MixedContentMode = MixedContentHandling.AlwaysAllow;
        }
        catch { }

        webView.SetBackgroundColor(global::Android.Graphics.Color.Black);
        webView.HorizontalScrollBarEnabled = false;
        webView.VerticalScrollBarEnabled = false;

        webView.SetWebChromeClient(new LiveBroadcastChromeClient());
        webView.SetWebViewClient(new LiveBroadcastClient());

        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Kitkat)
        {
            AWebView.SetWebContentsDebuggingEnabled(true);
        }
    }

    private sealed class LiveBroadcastChromeClient : WebChromeClient
    {
        private const int RequestCode = 9911;
        private static PermissionRequest? _pendingRequest;

        public override void OnPermissionRequest(PermissionRequest? request)
        {
            if (request == null)
            {
                base.OnPermissionRequest(request);
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
            try
            {
                var activity = MainActivity.Current;
                if (activity == null)
                {
                    request.Deny();
                    return;
                }

                var resources = request.GetResources() ?? Array.Empty<string>();

                var needsCamera = resources.Contains(PermissionRequest.ResourceVideoCapture);
                var needsMic = resources.Contains(PermissionRequest.ResourceAudioCapture);

                var missingPermissions = new List<string>();

                if (needsCamera &&
                    ContextCompat.CheckSelfPermission(activity, Manifest.Permission.Camera) != Permission.Granted)
                {
                    missingPermissions.Add(Manifest.Permission.Camera);
                }

                if (needsMic &&
                    ContextCompat.CheckSelfPermission(activity, Manifest.Permission.RecordAudio) != Permission.Granted)
                {
                    missingPermissions.Add(Manifest.Permission.RecordAudio);
                }

                // 🔥 إذا يحتاج يطلب صلاحيات
                if (missingPermissions.Count > 0)
                {
                    _pendingRequest = request;

                    ActivityCompat.RequestPermissions(
                        activity,
                        missingPermissions.ToArray(),
                        RequestCode);

                    return;
                }
                    // 🔥 إذا كلشي تمام → وافق فورًا
                    request.Grant(resources);
                }
                catch
                {
                    try { request.Deny(); } catch { }
                }
            });
        }

        public static void HandlePermissionResult(
            string[] permissions,
            Permission[] grantResults)
        {
            try
            {
                if (_pendingRequest == null)
                    return;

                if (grantResults.Length > 0 && grantResults.All(x => x == Permission.Granted))
                {
                    _pendingRequest.Grant(_pendingRequest.GetResources());
                }
                else
                {
                    _pendingRequest.Deny();
                }

                _pendingRequest = null;
            }
            catch
            {
                try { _pendingRequest?.Deny(); } catch { }
                _pendingRequest = null;
            }
        }
    }

    private sealed class LiveBroadcastClient : WebViewClient
    {
        public override bool ShouldOverrideUrlLoading(AWebView? view, IWebResourceRequest? request)
        {
            return false;
        }
    }

    public static void NotifyPermissionsResult(
        string[] permissions,
        Permission[] grantResults)
    {
        LiveBroadcastChromeClient.HandlePermissionResult(permissions, grantResults);
    }
}