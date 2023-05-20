﻿using Avalonia.WebView.iOS.Handlers;

namespace Avalonia.WebView.iOS.Core;

partial class IosWebViewCore
{
    IosWebViewCore IPlatformWebView<IosWebViewCore>.PlatformView => this;

    public nint NativeHandler { get; private set; }

    bool IPlatformWebView.IsInitialized => IsInitialized;

    object? IPlatformWebView.PlatformViewContext => this;

    bool IWebViewControl.IsCanGoForward => WebView.CanGoForward;

    bool IWebViewControl.IsCanGoBack => WebView.CanGoBack;

    async Task<bool> IPlatformWebView.Initialize(IVirtualWebViewProvider? virtualProvider)
    {
        if (IsInitialized)
            return true;

        _provider = virtualProvider;
        _config.Preferences.SetValueForKey(NSObject.FromObject(true), new NSString("developerExtrasEnabled"));
        _config.UserContentController.AddScriptMessageHandler(new WebViewScriptMessageHandler(MessageReceived), "webwindowinterop");
        //config.UserContentController.AddUserScript(new WKUserScript( new NSString(BlazorInitScript), WKUserScriptInjectionTime.AtDocumentEnd, true));

       await PrepareBlazorWebViewStarting(virtualProvider);



        return true;
    }

    async Task<string?> IWebViewControl.ExecuteScriptAsync(string javaScript)
    {
        if (WebView is null)
            return default;

        if (string.IsNullOrWhiteSpace(javaScript))
            return default;

        var ret = await WebView.EvaluateJavaScriptAsync(javaScript);
        return CFString.FromHandle(ret.Handle) ?? string.Empty;
    }

    bool IWebViewControl.GoBack()
    {
        if (WebView is null)
            return false;

        if (!WebView.CanGoBack)
            return false;

        return WebView.GoBack() == null ? false : true;
    }

    bool IWebViewControl.GoForward()
    {
        if (WebView is null)
            return false;

        if (!WebView.CanGoForward)
            return false;

        return WebView.GoForward() == null ? false : true;
    }

    bool IWebViewControl.Navigate(Uri? uri)
    {
        if (uri is null)
            return false;

        using var nsUrl = new NSUrl(uri.AbsoluteUri);
        using var request = new NSUrlRequest(nsUrl);

        return WebView.LoadRequest(request) == null ? false : true;
    }

    bool IWebViewControl.NavigateToString(string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
            return false;

        return WebView.LoadHtmlString(htmlContent, default!) == null ? false : true;
    }

    bool IWebViewControl.OpenDevToolsWindow()
    {
        throw new NotImplementedException();
    }

    bool IWebViewControl.PostWebMessageAsJson(string webMessageAsJson)
    {
        throw new NotImplementedException();
    }

    bool IWebViewControl.PostWebMessageAsString(string webMessageAsString)
    {
        if (string.IsNullOrWhiteSpace(webMessageAsString))
            return false;

        return true;
    }

    bool IWebViewControl.Reload()
    {
        if (WebView is null)
            return false;

        WebView.Reload();
        return true;
    }

    bool IWebViewControl.Stop()
    {
        if (WebView is null)
            return false;

        WebView.StopLoading();
        return true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
               
            }

            ClearBlazorWebViewCompleted();
            UnregisterEvents();
            WebView.Dispose();
            WebView = default!;

            IsDisposed = true;
        }
    }

    void IDisposable.Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        ((IDisposable)this)?.Dispose();
        return new ValueTask();
    }
}

