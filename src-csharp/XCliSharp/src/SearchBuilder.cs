// Advanced search query builder for XCliSharp.
// Mirrors twitter_cli/search.py

using System.Text.RegularExpressions;

namespace XCliSharp;

public static class SearchBuilder
{
    private static readonly Regex LangPattern = new(@"^[A-Za-z][A-Za-z\-]{1,14}$");

    public static string BuildSearchQuery(
        string query = "",
        string? fromUser = null,
        string? toUser = null,
        string? lang = null,
        string? since = null,
        string? until = null,
        IEnumerable<string>? has = null,
        IEnumerable<string>? exclude = null,
        int? minLikes = null,
        int? minRetweets = null)
    {
        var parts = new List<string>();

        var queryText = query.Trim();
        fromUser = NormalizeHandle(fromUser);
        toUser = NormalizeHandle(toUser);
        lang = NormalizeLang(lang);
        since = NormalizeDate("--since", since);
        until = NormalizeDate("--until", until);

        if (minLikes.HasValue && minLikes.Value < 0)
            throw new InvalidInputException("--min-likes must be >= 0");
        if (minRetweets.HasValue && minRetweets.Value < 0)
            throw new InvalidInputException("--min-retweets must be >= 0");
        if (since is not null && until is not null && string.Compare(since, until, StringComparison.Ordinal) > 0)
            throw new InvalidInputException("--since must be on or before --until");

        if (!string.IsNullOrEmpty(queryText)) parts.Add(queryText);
        if (fromUser is not null) parts.Add($"from:{fromUser}");
        if (toUser is not null) parts.Add($"to:{toUser}");
        if (lang is not null) parts.Add($"lang:{lang}");
        if (since is not null) parts.Add($"since:{since}");
        if (until is not null) parts.Add($"until:{until}");

        if (has is not null)
            foreach (var item in has)
                parts.Add($"filter:{item.ToLowerInvariant()}");

        if (exclude is not null)
            foreach (var item in exclude)
            {
                var lower = item.ToLowerInvariant();
                parts.Add(lower is "retweets" or "replies" or "links"
                    ? $"-filter:{lower}"
                    : $"-filter:{lower}");
            }

        if (minLikes.HasValue) parts.Add($"min_faves:{minLikes.Value}");
        if (minRetweets.HasValue) parts.Add($"min_retweets:{minRetweets.Value}");

        return string.Join(" ", parts);
    }

    private static string? NormalizeHandle(string? value)
    {
        if (value is null) return null;
        var text = value.Trim().TrimStart('@');
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string? NormalizeLang(string? value)
    {
        if (value is null) return null;
        var text = value.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(text)) return null;
        if (!LangPattern.IsMatch(text))
            throw new InvalidInputException("--lang must be an ISO language code like en or zh-cn");
        return text;
    }

    private static string? NormalizeDate(string flagName, string? value)
    {
        if (value is null) return null;
        var text = value.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", out _))
            throw new InvalidInputException($"{flagName} must be in YYYY-MM-DD format");
        return text;
    }
}
