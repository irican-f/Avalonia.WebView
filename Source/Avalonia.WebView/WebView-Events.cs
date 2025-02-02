using System.Diagnostics;

namespace AvaloniaWebView;

partial class WebView
{
    public event EventHandler<WebViewCreatingEventArgs>? WebViewCreating;
    public event EventHandler<WebViewCreatedEventArgs>? WebViewCreated;
    public event EventHandler<WebViewUrlLoadingEventArg>? NavigationStarting;
    public event EventHandler<WebViewUrlLoadedEventArg>? NavigationCompleted;
    public event EventHandler<WebViewMessageReceivedEventArgs>? WebMessageReceived;
    public event EventHandler<WebViewNewWindowEventArgs>? WebViewNewWindowRequested;
    public event EventHandler<WebViewRequestEventArgs>? WebResourceRequestReceived;
    public event Func<WebViewRequestEventArgs, Task>? ProxyRequestReceived;
    public event EventHandler<List<string>>? WebViewFilesDropped;

}
