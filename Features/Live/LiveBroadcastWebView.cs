namespace PROXIMAMOP.Features.Live;

public sealed class LiveBroadcastWebView : WebView
{
    public static readonly BindableProperty LiveUrlProperty =
        BindableProperty.Create(
            nameof(LiveUrl),
            typeof(string),
            typeof(LiveBroadcastWebView),
            default(string));

    public string? LiveUrl
    {
        get => (string?)GetValue(LiveUrlProperty);
        set => SetValue(LiveUrlProperty, value);
    }

    public void LoadLiveUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        LiveUrl = url.Trim();
        Source = new UrlWebViewSource
        {
            Url = LiveUrl
        };
    }

    public void ClearLive()
    {
        LiveUrl = null;
        Source = new HtmlWebViewSource
        {
            Html = """
                   <!doctype html>
                   <html>
                     <head>
                       <meta charset="utf-8" />
                       <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0" />
                       <title>Live</title>
                       <style>
                         html, body {
                           margin: 0;
                           padding: 0;
                           width: 100%;
                           height: 100%;
                           background: #0B1020;
                           color: white;
                           font-family: Arial, Helvetica, sans-serif;
                         }

                         body {
                           display: flex;
                           align-items: center;
                           justify-content: center;
                         }

                         .box {
                           text-align: center;
                           padding: 24px;
                           border: 1px solid rgba(255,255,255,0.12);
                           border-radius: 16px;
                           background: rgba(255,255,255,0.03);
                         }

                         .title {
                           font-size: 22px;
                           font-weight: 700;
                           margin-bottom: 10px;
                         }

                         .text {
                           font-size: 15px;
                           opacity: 0.85;
                         }
                       </style>
                     </head>
                     <body>
                       <div class="box">
                         <div class="title">Live</div>
                         <div class="text">Broadcast not started yet.</div>
                       </div>
                     </body>
                   </html>
                   """
        };
    }
}