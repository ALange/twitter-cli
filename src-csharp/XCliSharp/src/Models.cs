// Data models for XCliSharp.
// Mirrors twitter_cli/models.py

namespace XCliSharp;

/// <summary>Tweet author information.</summary>
public record Author(
    string Id,
    string Name,
    string ScreenName,
    string ProfileImageUrl = "",
    bool Verified = false
);

/// <summary>Engagement metrics for a tweet.</summary>
public record Metrics(
    int Likes = 0,
    int Retweets = 0,
    int Replies = 0,
    int Quotes = 0,
    int Views = 0,
    int Bookmarks = 0
);

/// <summary>Media attached to a tweet.</summary>
public record TweetMedia(
    string Type, // "photo" | "video" | "animated_gif"
    string Url,
    int? Width = null,
    int? Height = null
);

/// <summary>A single tweet.</summary>
public record Tweet(
    string Id,
    string Text,
    Author Author,
    Metrics Metrics,
    string CreatedAt,
    List<TweetMedia>? Media = null,
    List<string>? Urls = null,
    bool IsRetweet = false,
    string Lang = "",
    string? RetweetedBy = null,
    Tweet? QuotedTweet = null,
    double? Score = null,
    string? ArticleTitle = null,
    string? ArticleText = null,
    bool IsSubscriberOnly = false
);

/// <summary>A bookmark folder.</summary>
public record BookmarkFolder(string Id, string Name);

/// <summary>A Twitter user profile.</summary>
public record UserProfile(
    string Id,
    string Name,
    string ScreenName,
    string Bio = "",
    string Location = "",
    string Url = "",
    int FollowersCount = 0,
    int FollowingCount = 0,
    int TweetsCount = 0,
    int LikesCount = 0,
    bool Verified = false,
    string ProfileImageUrl = "",
    string CreatedAt = ""
);
