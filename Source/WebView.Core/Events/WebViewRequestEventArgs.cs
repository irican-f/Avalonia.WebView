using WebViewCore.Helpers;

namespace WebViewCore.Events;

public class WebViewRequestEventArgs : EventArgs
{
    public WebViewRequestEventArgs(string fullUrl)
    {
        Url = fullUrl;
        QueryParams = QueryStringHelper.GetKeyValuePairs(fullUrl);
    }

    /// <summary>
    /// The full request URL.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// Query string values extracted from the request URL.
    /// </summary>
    public IDictionary<string, string> QueryParams { get; }

    /// <summary>
    /// The response content type.
    /// </summary>

    public string? ResponseContentType { get; set; } = "text/plain";

    /// <summary>
    /// The response stream to be used to respond to the request.
    /// </summary>
    public Stream? ResponseStream { get; set; } = null;
}
