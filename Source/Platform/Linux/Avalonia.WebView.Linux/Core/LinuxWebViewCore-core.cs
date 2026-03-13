using Linux.WebView.Core;
using WebViewCore.Events;
using WebViewCore.Helpers;

namespace Avalonia.WebView.Linux.Core;

unsafe partial class LinuxWebViewCore
{
    private static readonly string AppHostAddress = "0.0.0.0";
    private static readonly string AppOrigin = $"https://{AppHostAddress}/";
    private static readonly Uri AppOriginUri = new(AppOrigin);
    private const string ProxyRequestPath = "proxy";

    public static readonly Dictionary<string, string> MimeTypes = new()
    {
        { ".html", "text/html" },
        { ".js", "application/javascript" },
        { ".css", "text/css" },
        { ".ttf", "font/ttf" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".webp", "image/webp" },
        { ".svg", "image/svg+xml" }
    };

    void RegisterAppOriginInterception(WebKitWebView webView)
    {
        if (webView is null)
            return;

        var bRet = _dispatcher.InvokeAsync(() =>
        {
            // Always intercept app-origin requests, independent of Blazor mode/provider.
            webView.Context.RegisterUriScheme("https", WebView_WebResourceRequest);
        }).Result;
    }

    Task PrepareBlazorWebViewStarting(IVirtualBlazorWebViewProvider? provider, WebKitWebView webView)
    {
        if (provider is null || WebView is null)
            return Task.CompletedTask;

        var bRet = _dispatcher.InvokeAsync(() =>
        {
            if (!provider.ResourceRequestedFilterProvider(this, out var filter))
                return;

            _webScheme = filter;
            if (!string.Equals(filter.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                webView.Context.RegisterUriScheme(filter.Scheme, WebView_WebResourceRequest);
            }

            var userContentManager = webView.UserContentManager;

            var script = GtkApi.CreateUserScriptX(BlazorScriptHelper.BlazorStartingScript);
            GtkApi.AddScriptForUserContentManager(userContentManager.Handle, script);
            GtkApi.ReleaseScript(script);

            GtkApi.AddSignalConnect(userContentManager.Handle, $"script-message-received::{_messageKeyWord}", LinuxApplicationManager.LoadFunction(_userContentMessageReceived), IntPtr.Zero);
            GtkApi.RegisterScriptMessageHandler(userContentManager.Handle, _messageKeyWord);

        }).Result;

        _isBlazorWebView = true;
        return Task.CompletedTask;
    }

    void ClearBlazorWebViewCompleted(WebKitWebView webView)
    {
        if (webView is null)
            return;

        var bRet = _dispatcher.InvokeAsync(() =>
        {
            //webView.UserContentManager.UnregisterScriptMessageHandler(_messageKeyWord);
            //webView.RemoveSignalHandler($"script-message-received::{_messageKeyWord}", WebView_WebMessageReceived);
        }).Result;

        _isBlazorWebView = false;
    }

    void WebView_WebMessageReceived(nint pContentManager, nint pJsResult, nint pArg)
    {
        //var userContentManager = new UserContentManager(pContentManager);
        //var jsValue = JavascriptResult.New(pJsResult);

        if (_provider is null)
            return;

        var pJsStringValue = GtkApi.CreateJavaScriptResult(pJsResult);
        if (!pJsStringValue.IsStringEx())
            return;

        var message = new WebViewMessageReceivedEventArgs
        {
            Message = pJsStringValue.ToStringEx(),
            Source = _provider.BaseUri,
        };
        GtkApi.ReleaseJavaScriptResult(pJsResult);

        _callBack.PlatformWebViewMessageReceived(this, message);
        _provider?.PlatformWebViewMessageReceived(this, message);
    }

    unsafe void WebView_WebResourceRequest(URISchemeRequest request)
    {
        if (request is null)
            return;

        if (!Uri.TryCreate(request.Uri, UriKind.Absolute, out var requestUri))
            return;

        if (AppOriginUri.IsBaseOf(requestUri))
        {
            var relativePath = AppOriginUri.MakeRelativeUri(requestUri).ToString().Trim('/');
            Stream? contentStream = null;
            var contentType = "text/plain";

            if (string.Equals(relativePath, ProxyRequestPath, StringComparison.OrdinalIgnoreCase))
            {
                var args = new WebViewRequestEventArgs(requestUri.AbsoluteUri, Stream.Null);
                OnProxyRequestMessage(args);
                contentType = args.ResponseContentType ?? "text/plain";
                contentStream = args.ResponseStream;
            }

            if (contentStream is null)
            {
                var args = new WebViewRequestEventArgs(requestUri.AbsoluteUri, Stream.Null);
                HandleAssetRequest(requestUri, args);
                contentType = args.ResponseContentType ?? "text/plain";
                contentStream = args.ResponseStream;
            }

            if (contentStream is null)
            {
                contentType = "text/plain";
                contentStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Resource not found (404)"));
            }

            using (contentStream)
            {
                FinishSchemeRequest(request, contentType, contentStream);
            }
            return;
        }

        if (_provider is null || _webScheme is null || request.Scheme != _webScheme.Scheme)
            return;

        var allowFallbackOnHostPage = _webScheme.BaseUri.IsBaseOfPage(request.Uri);
        var requestWrapper = new WebResourceRequest { RequestUri = request.Uri, AllowFallbackOnHostPage = allowFallbackOnHostPage };
        var bRet = _provider.PlatformWebViewResourceRequested(this, requestWrapper, out var response);
        if (!bRet)
            return;

        if (response is null)
            return;

        using var ms = new MemoryStream();
        response.Content.CopyTo(ms);
        ms.Position = 0;
        var responseContentType = response.Headers.TryGetValue(QueryStringHelper.ContentTypeKey, out var headerString)
            ? headerString
            : "application/octet-stream";
        FinishSchemeRequest(request, responseContentType, ms);
    }

    private void OnProxyRequestMessage(WebViewRequestEventArgs args)
    {
        _callBack.PlatformProxyRequestReceived(args).GetAwaiter().GetResult();
    }

    private void HandleAssetRequest(Uri uri, WebViewRequestEventArgs e)
    {
        if (string.IsNullOrEmpty(_creationProperties.AssetRootFolder))
            return;

        var req = uri.LocalPath;
        var filePath = Path.Combine(_creationProperties.AssetRootFolder!, Path.Combine(req.Split('/')));
        var fileExtension = Path.GetExtension(filePath);
        if (!MimeTypes.TryGetValue(fileExtension, out var mimeType))
            return;

        e.ResponseContentType = mimeType;
        if (File.Exists(filePath))
        {
            e.ResponseStream = new MemoryStream(File.ReadAllBytes(filePath));
            return;
        }

        var assembly = _creationProperties.ResourceAssembly;
        if (assembly is null)
            return;

        filePath = $"{assembly.GetName().Name}.{_creationProperties.AssetRootFolder}{req.Replace('/', '.')}";
        e.ResponseStream = assembly.GetManifestResourceStream(filePath);
    }

    private static void FinishSchemeRequest(URISchemeRequest request, string contentType, Stream content)
    {
        using var memory = new MemoryStream();
        content.CopyTo(memory);
        var pBuffer = GtkApi.MarshalToGLibInputStream(memory.ToArray(), memory.Length);
        using var inputStream = new GLib.InputStream(pBuffer);
        request.Finish(inputStream, memory.Length, contentType);
    }
}
