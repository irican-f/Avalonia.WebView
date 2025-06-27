using System.IO;

namespace Avalonia.WebView.Android.Clients;

public class CustomWebViewClient : WebViewClient
{
    private readonly WebViewCreationProperties _creationProperties;
    private readonly IVirtualWebViewControlCallBack _callBack;

    public static readonly Dictionary<string, string> MimeTypes = new()
    {
        { ".html", "text/html" },
        { ".js", "application/javascript" },
        { ".css", "text/css" },
        { ".ttf", "font/ttf" },
        { ".png", "image/png" },
        { ".svg", "image/svg+xml" }
    };
    
    // Using an IP address means that WebView2 doesn't wait for any DNS resolution,
    // making it substantially faster. Note that this isn't real HTTP traffic, since
    // we intercept all the requests within this origin.
    private static readonly string AppHostAddress = "0.0.0.0";

    /// <summary>
    /// Gets the application's base URI. Defaults to <c>https://0.0.0.0/</c>
    /// </summary>
    private static readonly string AppOrigin = $"https://{AppHostAddress}/";
    
    private static readonly Uri AppOriginUri = new(AppOrigin);
    
    private const string ProxyRequestPath = "proxy";

    public CustomWebViewClient(WebViewCreationProperties creationProperties, IVirtualWebViewControlCallBack callBack)
    {
        _creationProperties = creationProperties;
        _callBack = callBack;
    }


    public override AndroidWebResourceResponse? ShouldInterceptRequest(AndroidWebView? view, IWebResourceRequest? request)
    {
        var contentType = "text/plain";
        var requestUri = request?.Url?.ToString() ?? string.Empty;

        if (request == null || new Uri(requestUri) is not Uri uri || !AppOriginUri.IsBaseOf(uri))
        {
            return base.ShouldInterceptRequest(view, request);
        }

        var relativePath = AppOriginUri.MakeRelativeUri(uri).ToString().Replace('/', '\\');
        var relativePathWithoutQuery= QueryStringHelper.RemovePossibleQueryString(requestUri).Replace(AppOrigin, string.Empty);
        Stream? contentStream = null;
                
        AndroidWebResourceResponse? response = null;
        var mainHandler = new Handler(Looper.MainLooper!);

        // Run code on the main thread
        mainHandler.Post(() =>
        {
            if (relativePathWithoutQuery == ProxyRequestPath)
            {
                var args = new WebViewRequestEventArgs(request.Url?.ToString() ?? string.Empty, new MemoryStream());

                OnProxyRequestMessage(args).Wait();

                if (args.ResponseStream != null)
                {
                    contentType = args.ResponseContentType ?? "text/plain";
                    contentStream = args.ResponseStream;
                }
            }
        
            if (contentStream is null)
            {
                var args = new WebViewRequestEventArgs(request.Url?.ToString() ?? string.Empty, new MemoryStream());
                HandleAssetRequest(uri, args);

                contentType = args.ResponseContentType ?? "text/plain";
                contentStream = args.ResponseStream;
            }

            // Make sure the response is constructed in the main thread
            response = new AndroidWebResourceResponse(
                contentType, 
                "UTF-8",
                200, 
                "OK", 
                new Dictionary<string, string>
                {
                    {"Access-Control-Allow-Origin", "*"},
                    {"Access-Control-Allow-Methods", "GET, POST, OPTIONS, PUT, DELETE"},
                    {"Access-Control-Allow-Headers", "*"}
                },
                contentStream);
        });

        // Wait for the response to be prepared on the main thread
        while (response == null)
        {
            Thread.Sleep(10);
        }

        return response;
    }
    
    private void HandleAssetRequest(Uri uri, WebViewRequestEventArgs e)
    {
        if (_creationProperties == null || string.IsNullOrEmpty(_creationProperties.AssetRootFolder))
        {
            return;
        }
        
        var req = uri.LocalPath;
        var filePath = Path.Combine(
            _creationProperties.AssetRootFolder!,
            Path.Combine(req.Split('/'))
        );
    
        var assembly = _creationProperties.ResourceAssembly;

        if (assembly is null)
        {
            return;
        }
        
        var fileExtension = Path.GetExtension(filePath);
        var resourceName = $"{assembly.GetName().Name}.{_creationProperties.AssetRootFolder}{req.Replace('/', '.')}";
        
        if (!MimeTypes.TryGetValue(fileExtension, out var mimeType))
        {
            return;
        }
        
        e.ResponseContentType = mimeType;
        e.ResponseStream = assembly.GetManifestResourceStream(resourceName);
    }
    
    private async Task OnProxyRequestMessage(WebViewRequestEventArgs args)
    {
        if (_callBack is null)
        {
            return;
        }
        
        await _callBack.PlatformProxyRequestReceived(args);
    }
}