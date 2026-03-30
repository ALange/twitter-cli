// Shared constants for XCliSharp.
// Mirrors twitter_cli/constants.py

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace XCliSharp;

public static class Constants
{
    public const string BearerToken =
        "AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs" +
        "%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA";

    private static string _chromeVersion = "133";

    public static void SyncChromeVersion(string impersonateTarget)
    {
        var m = Regex.Match(impersonateTarget, @"(\d+)");
        if (m.Success) _chromeVersion = m.Groups[1].Value;
    }

    public static string GetUserAgent()
    {
        string platform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? "Macintosh; Intel Mac OS X 10_15_7"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Windows NT 10.0; Win64; x64"
                : "X11; Linux x86_64";
        return $"Mozilla/5.0 ({platform}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{_chromeVersion}.0.0.0 Safari/537.36";
    }

    public static string GetSecChUa() =>
        $"\"Chromium\";v=\"{_chromeVersion}\", \"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"{_chromeVersion}\"";

    public static string GetAcceptLanguage()
    {
        var tag = GetLocaleTag();
        var language = tag.Contains('-') ? tag.Split('-')[0] : tag;
        return $"{tag},{language};q=0.9,en;q=0.8";
    }

    public static string GetTwitterClientLanguage()
    {
        var tag = GetLocaleTag();
        return tag.Contains('-') ? tag.Split('-')[0] : tag;
    }

    public static string GetSecChUaPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "\"macOS\"";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "\"Windows\"";
        return "\"Linux\"";
    }

    private static string GetLocaleTag()
    {
        var raw = Environment.GetEnvironmentVariable("LC_ALL")
            ?? Environment.GetEnvironmentVariable("LC_MESSAGES")
            ?? Environment.GetEnvironmentVariable("LANG")
            ?? "en_US.UTF-8";
        var tag = raw.Split('.')[0].Replace('_', '-');
        return string.IsNullOrEmpty(tag) ? "en-US" : tag;
    }
}
