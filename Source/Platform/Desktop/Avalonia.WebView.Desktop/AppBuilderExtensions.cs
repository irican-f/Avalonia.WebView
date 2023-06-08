﻿using System.Runtime.InteropServices;

namespace Avalonia.WebView.Desktop;

public static class AppBuilderExtensions
{
    public static AppBuilder UseDesktopWebView(this AppBuilder builder)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            builder.UseWindowWebView();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            builder.UseMacCatalystWebView();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            builder.UseLinuxWebView();

        return builder;
    }
}
