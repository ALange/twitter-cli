// XCliSharp - A C# CLI for Twitter/X
// Mirrors twitter_cli/cli.py

using System.CommandLine;
using XCliSharp;

// Global output options
var jsonOpt = new Option<bool>("--json", description: "Output as JSON");
var yamlOpt = new Option<bool>("--yaml", description: "Output as YAML");

// Root command
var root = new RootCommand("xcli — A C# CLI for Twitter/X");

// Helper: create authenticated client
static (TwitterClient client, AppConfig config) GetClient()
{
    var cfg = ConfigLoader.LoadConfig();
    var creds = Auth.Resolve();
    var client = new TwitterClient(creds.AuthToken, creds.Ct0, cfg.RateLimit, creds.CookieString);
    return (client, cfg);
}

// Helper: run a command with error handling
static async Task<int> RunAsync(Func<Task<int>> action)
{
    try { return await action(); }
    catch (AuthenticationException ex)
    {
        Spectre.Console.AnsiConsole.MarkupLine($"[red]❌ Authentication error:[/] {ex.Message}");
        return 1;
    }
    catch (RateLimitException)
    {
        Spectre.Console.AnsiConsole.MarkupLine("[red]❌ Rate limited by Twitter. Please wait and retry.[/]");
        return 1;
    }
    catch (NetworkException ex)
    {
        Spectre.Console.AnsiConsole.MarkupLine($"[red]❌ Network error: {ex.Message}[/]");
        return 1;
    }
    catch (TwitterException ex)
    {
        Spectre.Console.AnsiConsole.MarkupLine($"[red]❌ {ex.Message}[/]");
        return 1;
    }
    catch (Exception ex)
    {
        Spectre.Console.AnsiConsole.MarkupLine($"[red]❌ Unexpected error: {ex.Message}[/]");
        return 1;
    }
}

// ── FEED ──────────────────────────────────────────────────────────────────
{
    var feedCmd = new Command("feed", "Fetch home timeline");
    var feedType = new Option<string>(
        "--type",
        description: "Feed type: for-you | following",
        getDefaultValue: () => "for-you");
    feedType.AddAlias("-t");
    feedType.FromAmong("for-you", "following");
    var maxOpt = new Option<int?>("--max", description: "Max tweets to fetch");
    maxOpt.AddAlias("-n");
    feedCmd.AddOption(feedType);
    feedCmd.AddOption(maxOpt);
    feedCmd.AddOption(jsonOpt);
    feedCmd.AddOption(yamlOpt);

    feedCmd.SetHandler(async (ctx) =>
    {
        var type = ctx.ParseResult.GetValueForOption(feedType) ?? "for-you";
        var max = ctx.ParseResult.GetValueForOption(maxOpt);
        var asJson = ctx.ParseResult.GetValueForOption(jsonOpt);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlOpt);

        ctx.ExitCode = await RunAsync(async () =>
        {
            var (client, cfg) = GetClient();
            using (client)
            {
                var count = max ?? cfg.Fetch.Count;
                Spectre.Console.AnsiConsole.MarkupLine("[dim]Fetching feed...[/]");
                var tweets = type == "following"
                    ? await client.FetchFollowingFeedAsync(count)
                    : await client.FetchHomeTimelineAsync(count);

                tweets = TweetFilter.FilterTweets(tweets, cfg.Filter.ToFilterConfig());
                TweetCache.SaveTweetCache(tweets);

                if (Output.EmitStructured(
                    tweets.Select(t => (object)Serialization.TweetToDict(t)).ToList(),
                    asJson, asYaml)) return 0;

                Formatter.PrintTweetTable(tweets, title: $"📱 Feed ({type}) — {tweets.Count} tweets");
            }
            return 0;
        });
    });

    root.AddCommand(feedCmd);
}

// ── BOOKMARKS ─────────────────────────────────────────────────────────────
{
    var bookmarksCmd = new Command("bookmarks", "Fetch bookmarked tweets");
    var maxOpt = new Option<int?>("--max", description: "Max tweets to fetch");
    maxOpt.AddAlias("-n");
    bookmarksCmd.AddOption(maxOpt);
    bookmarksCmd.AddOption(jsonOpt);
    bookmarksCmd.AddOption(yamlOpt);

    bookmarksCmd.SetHandler(async (ctx) =>
    {
        var max = ctx.ParseResult.GetValueForOption(maxOpt);
        var asJson = ctx.ParseResult.GetValueForOption(jsonOpt);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlOpt);

        ctx.ExitCode = await RunAsync(async () =>
        {
            var (client, cfg) = GetClient();
            using (client)
            {
                var count = max ?? cfg.Fetch.Count;
                Spectre.Console.AnsiConsole.MarkupLine("[dim]Fetching bookmarks...[/]");
                var tweets = await client.FetchBookmarksAsync(count);
                TweetCache.SaveTweetCache(tweets);

                if (Output.EmitStructured(
                    tweets.Select(t => (object)Serialization.TweetToDict(t)).ToList(),
                    asJson, asYaml)) return 0;

                Formatter.PrintTweetTable(tweets, title: $"🔖 Bookmarks — {tweets.Count} tweets");
            }
            return 0;
        });
    });

    root.AddCommand(bookmarksCmd);
}

// ── SEARCH ────────────────────────────────────────────────────────────────
{
    var searchCmd = new Command("search", "Search tweets");
    var queryArg = new Argument<string>("query", description: "Search query");
    var fromOpt = new Option<string?>("--from", description: "Only from this user");
    var toOpt = new Option<string?>("--to", description: "Only to this user");
    var langOpt = new Option<string?>("--lang", description: "Language filter (ISO code)");
    var sinceOpt = new Option<string?>("--since", description: "Start date YYYY-MM-DD");
    var untilOpt = new Option<string?>("--until", description: "End date YYYY-MM-DD");
    var maxOpt = new Option<int?>("--max", description: "Max results");
    maxOpt.AddAlias("-n");
    var productOpt = new Option<string>(
        "--product",
        description: "Top | Latest | Photos | Videos",
        getDefaultValue: () => "Top");
    productOpt.FromAmong("Top", "Latest", "Photos", "Videos");
    var minLikesOpt = new Option<int?>("--min-likes", description: "Min likes");
    var minRetweetsOpt = new Option<int?>("--min-retweets", description: "Min retweets");

    searchCmd.AddArgument(queryArg);
    searchCmd.AddOption(fromOpt);
    searchCmd.AddOption(toOpt);
    searchCmd.AddOption(langOpt);
    searchCmd.AddOption(sinceOpt);
    searchCmd.AddOption(untilOpt);
    searchCmd.AddOption(maxOpt);
    searchCmd.AddOption(productOpt);
    searchCmd.AddOption(minLikesOpt);
    searchCmd.AddOption(minRetweetsOpt);
    searchCmd.AddOption(jsonOpt);
    searchCmd.AddOption(yamlOpt);

    searchCmd.SetHandler(async (ctx) =>
    {
        var query = ctx.ParseResult.GetValueForArgument(queryArg);
        var from = ctx.ParseResult.GetValueForOption(fromOpt);
        var to = ctx.ParseResult.GetValueForOption(toOpt);
        var lang = ctx.ParseResult.GetValueForOption(langOpt);
        var since = ctx.ParseResult.GetValueForOption(sinceOpt);
        var until = ctx.ParseResult.GetValueForOption(untilOpt);
        var max = ctx.ParseResult.GetValueForOption(maxOpt);
        var product = ctx.ParseResult.GetValueForOption(productOpt) ?? "Top";
        var minLikes = ctx.ParseResult.GetValueForOption(minLikesOpt);
        var minRetweets = ctx.ParseResult.GetValueForOption(minRetweetsOpt);
        var asJson = ctx.ParseResult.GetValueForOption(jsonOpt);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlOpt);

        ctx.ExitCode = await RunAsync(async () =>
        {
            var builtQuery = SearchBuilder.BuildSearchQuery(
                query, fromUser: from, toUser: to, lang: lang,
                since: since, until: until,
                minLikes: minLikes, minRetweets: minRetweets);

            var (client, cfg) = GetClient();
            using (client)
            {
                var count = max ?? cfg.Fetch.Count;
                Spectre.Console.AnsiConsole.MarkupLine($"[dim]Searching: {builtQuery}[/]");
                var tweets = await client.FetchSearchAsync(builtQuery, count, product);
                TweetCache.SaveTweetCache(tweets);

                if (Output.EmitStructured(
                    tweets.Select(t => (object)Serialization.TweetToDict(t)).ToList(),
                    asJson, asYaml)) return 0;

                Formatter.PrintTweetTable(tweets, title: $"🔍 Search: \"{builtQuery}\" — {tweets.Count} results");
            }
            return 0;
        });
    });

    root.AddCommand(searchCmd);
}

// ── USER ──────────────────────────────────────────────────────────────────
{
    var userCmd = new Command("user", "Show user profile");
    var handleArg = new Argument<string>("handle", description: "Twitter handle (without @)");
    userCmd.AddArgument(handleArg);
    userCmd.AddOption(jsonOpt);
    userCmd.AddOption(yamlOpt);

    userCmd.SetHandler(async (ctx) =>
    {
        var handle = ctx.ParseResult.GetValueForArgument(handleArg).TrimStart('@');
        var asJson = ctx.ParseResult.GetValueForOption(jsonOpt);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlOpt);

        ctx.ExitCode = await RunAsync(async () =>
        {
            var (client, _) = GetClient();
            using (client)
            {
                Spectre.Console.AnsiConsole.MarkupLine($"[dim]Fetching user @{handle}...[/]");
                var user = await client.FetchUserAsync(handle);
                if (user is null) { Spectre.Console.AnsiConsole.MarkupLine("[red]User not found[/]"); return 1; }

                if (Output.EmitStructured(Serialization.UserProfileToDict(user), asJson, asYaml)) return 0;
                Formatter.PrintUserProfile(user);
            }
            return 0;
        });
    });

    root.AddCommand(userCmd);
}

// ── USER-POSTS ────────────────────────────────────────────────────────────
{
    var userPostsCmd = new Command("user-posts", "Show user tweets");
    var handleArg = new Argument<string>("handle", description: "Twitter handle");
    var maxOpt = new Option<int?>("--max", description: "Max tweets");
    maxOpt.AddAlias("-n");
    userPostsCmd.AddArgument(handleArg);
    userPostsCmd.AddOption(maxOpt);
    userPostsCmd.AddOption(jsonOpt);
    userPostsCmd.AddOption(yamlOpt);

    userPostsCmd.SetHandler(async (ctx) =>
    {
        var handle = ctx.ParseResult.GetValueForArgument(handleArg).TrimStart('@');
        var max = ctx.ParseResult.GetValueForOption(maxOpt);
        var asJson = ctx.ParseResult.GetValueForOption(jsonOpt);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlOpt);

        ctx.ExitCode = await RunAsync(async () =>
        {
            var (client, cfg) = GetClient();
            using (client)
            {
                var count = max ?? cfg.Fetch.Count;
                Spectre.Console.AnsiConsole.MarkupLine($"[dim]Fetching @{handle} tweets...[/]");
                var user = await client.FetchUserAsync(handle);
                if (user is null) { Spectre.Console.AnsiConsole.MarkupLine("[red]User not found[/]"); return 1; }
                var tweets = await client.FetchUserTweetsAsync(user.Id, count);
                TweetCache.SaveTweetCache(tweets);

                if (Output.EmitStructured(
                    tweets.Select(t => (object)Serialization.TweetToDict(t)).ToList(),
                    asJson, asYaml)) return 0;

                Formatter.PrintTweetTable(tweets, title: $"📝 @{handle} — {tweets.Count} tweets");
            }
            return 0;
        });
    });

    root.AddCommand(userPostsCmd);
}

// ── LIKES ─────────────────────────────────────────────────────────────────
{
    var likesCmd = new Command("likes", "Show user's liked tweets");
    var handleArg = new Argument<string>("handle");
    var maxOpt = new Option<int?>("--max", description: "Max tweets");
    maxOpt.AddAlias("-n");
    likesCmd.AddArgument(handleArg);
    likesCmd.AddOption(maxOpt);
    likesCmd.AddOption(jsonOpt);
    likesCmd.AddOption(yamlOpt);

    likesCmd.SetHandler(async (ctx) =>
    {
        var handle = ctx.ParseResult.GetValueForArgument(handleArg).TrimStart('@');
        var max = ctx.ParseResult.GetValueForOption(maxOpt);
        var asJson = ctx.ParseResult.GetValueForOption(jsonOpt);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlOpt);

        ctx.ExitCode = await RunAsync(async () =>
        {
            var (client, cfg) = GetClient();
            using (client)
            {
                Spectre.Console.AnsiConsole.MarkupLine($"[dim]Fetching @{handle} likes...[/]");
                var user = await client.FetchUserAsync(handle);
                if (user is null) { Spectre.Console.AnsiConsole.MarkupLine("[red]User not found[/]"); return 1; }
                var tweets = await client.FetchUserLikesAsync(user.Id, max ?? cfg.Fetch.Count);
                TweetCache.SaveTweetCache(tweets);

                if (Output.EmitStructured(
                    tweets.Select(t => (object)Serialization.TweetToDict(t)).ToList(),
                    asJson, asYaml)) return 0;

                Formatter.PrintTweetTable(tweets, title: $"❤️ @{handle} likes — {tweets.Count} tweets");
            }
            return 0;
        });
    });

    root.AddCommand(likesCmd);
}

// ── TWEET DETAIL ──────────────────────────────────────────────────────────
{
    var tweetCmd = new Command("tweet", "Show tweet detail and replies");
    var idArg = new Argument<string>("id", description: "Tweet ID or cache index");
    tweetCmd.AddArgument(idArg);
    tweetCmd.AddOption(jsonOpt);
    tweetCmd.AddOption(yamlOpt);

    tweetCmd.SetHandler(async (ctx) =>
    {
        var idInput = ctx.ParseResult.GetValueForArgument(idArg);
        var asJson = ctx.ParseResult.GetValueForOption(jsonOpt);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlOpt);

        ctx.ExitCode = await RunAsync(async () =>
        {
            // Try to resolve as a cache index
            string tweetId = idInput;
            if (int.TryParse(idInput, out var idx))
            {
                var (cachedId, _) = TweetCache.ResolveCachedTweet(idx);
                if (cachedId is not null) tweetId = cachedId;
            }

            var (client, _) = GetClient();
            using (client)
            {
                Spectre.Console.AnsiConsole.MarkupLine($"[dim]Fetching tweet {tweetId}...[/]");
                var (tweet, replies) = await client.FetchTweetDetailAsync(tweetId);
                if (tweet is null) { Spectre.Console.AnsiConsole.MarkupLine("[red]Tweet not found[/]"); return 1; }

                if (Output.EmitStructured(new Dictionary<string, object>
                {
                    ["tweet"] = Serialization.TweetToDict(tweet),
                    ["replies"] = replies.Select(t => (object)Serialization.TweetToDict(t)).ToList(),
                }, asJson, asYaml)) return 0;

                Formatter.PrintTweetDetail(tweet);
                if (replies.Count > 0)
                    Formatter.PrintTweetTable(replies, title: $"💬 Replies ({replies.Count})");
            }
            return 0;
        });
    });

    root.AddCommand(tweetCmd);
}

// ── FOLLOWERS ─────────────────────────────────────────────────────────────
{
    var followersCmd = new Command("followers", "List user's followers");
    var handleArg = new Argument<string>("handle");
    var maxOpt = new Option<int?>("--max", description: "Max users");
    maxOpt.AddAlias("-n");
    followersCmd.AddArgument(handleArg);
    followersCmd.AddOption(maxOpt);
    followersCmd.AddOption(jsonOpt);
    followersCmd.AddOption(yamlOpt);

    followersCmd.SetHandler(async (ctx) =>
    {
        var handle = ctx.ParseResult.GetValueForArgument(handleArg).TrimStart('@');
        var max = ctx.ParseResult.GetValueForOption(maxOpt);
        var asJson = ctx.ParseResult.GetValueForOption(jsonOpt);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlOpt);

        ctx.ExitCode = await RunAsync(async () =>
        {
            var (client, _) = GetClient();
            using (client)
            {
                Spectre.Console.AnsiConsole.MarkupLine($"[dim]Fetching @{handle} followers...[/]");
                var user = await client.FetchUserAsync(handle);
                if (user is null) { Spectre.Console.AnsiConsole.MarkupLine("[red]User not found[/]"); return 1; }
                var users = await client.FetchFollowersAsync(user.Id, max ?? 20);

                if (Output.EmitStructured(
                    users.Select(u => (object)Serialization.UserProfileToDict(u)).ToList(),
                    asJson, asYaml)) return 0;

                Formatter.PrintUserTable(users, title: $"👥 Followers of @{handle}");
            }
            return 0;
        });
    });

    root.AddCommand(followersCmd);
}

// ── FOLLOWING ─────────────────────────────────────────────────────────────
{
    var followingCmd = new Command("following", "List user's following");
    var handleArg = new Argument<string>("handle");
    var maxOpt = new Option<int?>("--max", description: "Max users");
    maxOpt.AddAlias("-n");
    followingCmd.AddArgument(handleArg);
    followingCmd.AddOption(maxOpt);
    followingCmd.AddOption(jsonOpt);
    followingCmd.AddOption(yamlOpt);

    followingCmd.SetHandler(async (ctx) =>
    {
        var handle = ctx.ParseResult.GetValueForArgument(handleArg).TrimStart('@');
        var max = ctx.ParseResult.GetValueForOption(maxOpt);
        var asJson = ctx.ParseResult.GetValueForOption(jsonOpt);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlOpt);

        ctx.ExitCode = await RunAsync(async () =>
        {
            var (client, _) = GetClient();
            using (client)
            {
                Spectre.Console.AnsiConsole.MarkupLine($"[dim]Fetching @{handle} following...[/]");
                var user = await client.FetchUserAsync(handle);
                if (user is null) { Spectre.Console.AnsiConsole.MarkupLine("[red]User not found[/]"); return 1; }
                var users = await client.FetchFollowingAsync(user.Id, max ?? 20);

                if (Output.EmitStructured(
                    users.Select(u => (object)Serialization.UserProfileToDict(u)).ToList(),
                    asJson, asYaml)) return 0;

                Formatter.PrintUserTable(users, title: $"👤 Following of @{handle}");
            }
            return 0;
        });
    });

    root.AddCommand(followingCmd);
}

// ── POST ──────────────────────────────────────────────────────────────────
{
    var postCmd = new Command("post", "Post a new tweet");
    var textArg = new Argument<string>("text", "Tweet text");
    postCmd.AddArgument(textArg);
    postCmd.AddOption(jsonOpt);
    postCmd.AddOption(yamlOpt);

    postCmd.SetHandler(async (ctx) =>
    {
        var text = ctx.ParseResult.GetValueForArgument(textArg);
        var asJson = ctx.ParseResult.GetValueForOption(jsonOpt);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlOpt);

        ctx.ExitCode = await RunAsync(async () =>
        {
            var (client, _) = GetClient();
            using (client)
            {
                Spectre.Console.AnsiConsole.MarkupLine("[dim]Posting tweet...[/]");
                var tweet = await client.CreateTweetAsync(text);
                if (tweet is null) { Spectre.Console.AnsiConsole.MarkupLine("[red]Failed to post tweet[/]"); return 1; }

                if (Output.EmitStructured(Serialization.TweetToDict(tweet), asJson, asYaml)) return 0;
                Spectre.Console.AnsiConsole.MarkupLine($"[green]✅ Posted: x.com/{tweet.Author.ScreenName}/status/{tweet.Id}[/]");
                Formatter.PrintTweetDetail(tweet);
            }
            return 0;
        });
    });

    root.AddCommand(postCmd);
}

// ── REPLY ─────────────────────────────────────────────────────────────────
{
    var replyCmd = new Command("reply", "Reply to a tweet");
    var idArg = new Argument<string>("id", "Tweet ID to reply to");
    var textArg = new Argument<string>("text", "Reply text");
    replyCmd.AddArgument(idArg);
    replyCmd.AddArgument(textArg);
    replyCmd.AddOption(jsonOpt);
    replyCmd.AddOption(yamlOpt);

    replyCmd.SetHandler(async (ctx) =>
    {
        var id = ctx.ParseResult.GetValueForArgument(idArg);
        var text = ctx.ParseResult.GetValueForArgument(textArg);
        var asJson = ctx.ParseResult.GetValueForOption(jsonOpt);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlOpt);

        ctx.ExitCode = await RunAsync(async () =>
        {
            var (client, _) = GetClient();
            using (client)
            {
                Spectre.Console.AnsiConsole.MarkupLine("[dim]Posting reply...[/]");
                var tweet = await client.CreateTweetAsync(text, replyToId: id);
                if (tweet is null) { Spectre.Console.AnsiConsole.MarkupLine("[red]Failed to post reply[/]"); return 1; }

                if (Output.EmitStructured(Serialization.TweetToDict(tweet), asJson, asYaml)) return 0;
                Spectre.Console.AnsiConsole.MarkupLine($"[green]✅ Replied: x.com/{tweet.Author.ScreenName}/status/{tweet.Id}[/]");
            }
            return 0;
        });
    });

    root.AddCommand(replyCmd);
}

// ── DELETE ────────────────────────────────────────────────────────────────
{
    var deleteCmd = new Command("delete", "Delete a tweet");
    var idArg = new Argument<string>("id", "Tweet ID to delete");
    deleteCmd.AddArgument(idArg);
    deleteCmd.AddOption(jsonOpt);
    deleteCmd.AddOption(yamlOpt);

    deleteCmd.SetHandler(async (ctx) =>
    {
        var id = ctx.ParseResult.GetValueForArgument(idArg);
        var asJson = ctx.ParseResult.GetValueForOption(jsonOpt);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlOpt);

        ctx.ExitCode = await RunAsync(async () =>
        {
            var (client, _) = GetClient();
            using (client)
            {
                await client.DeleteTweetAsync(id);
                if (Output.EmitStructured(new Dictionary<string, object> { ["deleted"] = true, ["id"] = id }, asJson, asYaml)) return 0;
                Spectre.Console.AnsiConsole.MarkupLine($"[green]✅ Deleted tweet {id}[/]");
            }
            return 0;
        });
    });

    root.AddCommand(deleteCmd);
}

// ── LIKE / UNLIKE ─────────────────────────────────────────────────────────
static Command MakeLikeCmd(string name, string desc, bool like, Option<bool> jsonO, Option<bool> yamlO)
{
    var cmd = new Command(name, desc);
    var idArg = new Argument<string>("id", "Tweet ID");
    cmd.AddArgument(idArg);
    cmd.AddOption(jsonO);
    cmd.AddOption(yamlO);
    cmd.SetHandler(async (ctx) =>
    {
        var id = ctx.ParseResult.GetValueForArgument(idArg);
        var asJson = ctx.ParseResult.GetValueForOption(jsonO);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlO);
        ctx.ExitCode = await RunAsync(async () =>
        {
            var (client, _) = GetClient();
            using (client)
            {
                bool ok = like ? await client.LikeTweetAsync(id) : await client.UnlikeTweetAsync(id);
                if (Output.EmitStructured(new Dictionary<string, object> { ["ok"] = ok, ["id"] = id }, asJson, asYaml)) return 0;
                Spectre.Console.AnsiConsole.MarkupLine($"[green]✅ {(like ? "Liked" : "Unliked")} tweet {id}[/]");
            }
            return 0;
        });
    });
    return cmd;
}
root.AddCommand(MakeLikeCmd("like", "Like a tweet", true, jsonOpt, yamlOpt));
root.AddCommand(MakeLikeCmd("unlike", "Unlike a tweet", false, jsonOpt, yamlOpt));

// ── RETWEET / UNRETWEET ───────────────────────────────────────────────────
static Command MakeRetweetCmd(string name, string desc, bool rt, Option<bool> jsonO, Option<bool> yamlO)
{
    var cmd = new Command(name, desc);
    var idArg = new Argument<string>("id", "Tweet ID");
    cmd.AddArgument(idArg);
    cmd.AddOption(jsonO);
    cmd.AddOption(yamlO);
    cmd.SetHandler(async (ctx) =>
    {
        var id = ctx.ParseResult.GetValueForArgument(idArg);
        var asJson = ctx.ParseResult.GetValueForOption(jsonO);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlO);
        ctx.ExitCode = await RunAsync(async () =>
        {
            var (client, _) = GetClient();
            using (client)
            {
                bool ok = rt ? await client.RetweetAsync(id) : await client.UnretweetAsync(id);
                if (Output.EmitStructured(new Dictionary<string, object> { ["ok"] = ok, ["id"] = id }, asJson, asYaml)) return 0;
                Spectre.Console.AnsiConsole.MarkupLine($"[green]✅ {(rt ? "Retweeted" : "Unretweeted")} tweet {id}[/]");
            }
            return 0;
        });
    });
    return cmd;
}
root.AddCommand(MakeRetweetCmd("retweet", "Retweet a tweet", true, jsonOpt, yamlOpt));
root.AddCommand(MakeRetweetCmd("unretweet", "Remove a retweet", false, jsonOpt, yamlOpt));

// ── BOOKMARK / UNBOOKMARK ─────────────────────────────────────────────────
static Command MakeBookmarkCmd(string name, string desc, bool bm, Option<bool> jsonO, Option<bool> yamlO)
{
    var cmd = new Command(name, desc);
    var idArg = new Argument<string>("id", "Tweet ID");
    cmd.AddArgument(idArg);
    cmd.AddOption(jsonO);
    cmd.AddOption(yamlO);
    cmd.SetHandler(async (ctx) =>
    {
        var id = ctx.ParseResult.GetValueForArgument(idArg);
        var asJson = ctx.ParseResult.GetValueForOption(jsonO);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlO);
        ctx.ExitCode = await RunAsync(async () =>
        {
            var (client, _) = GetClient();
            using (client)
            {
                bool ok = bm ? await client.BookmarkTweetAsync(id) : await client.UnbookmarkTweetAsync(id);
                if (Output.EmitStructured(new Dictionary<string, object> { ["ok"] = ok, ["id"] = id }, asJson, asYaml)) return 0;
                Spectre.Console.AnsiConsole.MarkupLine($"[green]✅ {(bm ? "Bookmarked" : "Removed bookmark from")} tweet {id}[/]");
            }
            return 0;
        });
    });
    return cmd;
}
root.AddCommand(MakeBookmarkCmd("bookmark", "Bookmark a tweet", true, jsonOpt, yamlOpt));
root.AddCommand(MakeBookmarkCmd("unbookmark", "Remove a bookmark", false, jsonOpt, yamlOpt));

// ── FOLLOW / UNFOLLOW ─────────────────────────────────────────────────────
static Command MakeFollowCmd(string name, string desc, bool follow, Option<bool> jsonO, Option<bool> yamlO)
{
    var cmd = new Command(name, desc);
    var handleArg = new Argument<string>("handle", "Twitter handle");
    cmd.AddArgument(handleArg);
    cmd.AddOption(jsonO);
    cmd.AddOption(yamlO);
    cmd.SetHandler(async (ctx) =>
    {
        var handle = ctx.ParseResult.GetValueForArgument(handleArg).TrimStart('@');
        var asJson = ctx.ParseResult.GetValueForOption(jsonO);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlO);
        ctx.ExitCode = await RunAsync(async () =>
        {
            var (client, _) = GetClient();
            using (client)
            {
                Spectre.Console.AnsiConsole.MarkupLine($"[dim]Looking up @{handle}...[/]");
                var user = await client.FetchUserAsync(handle);
                if (user is null) { Spectre.Console.AnsiConsole.MarkupLine("[red]User not found[/]"); return 1; }
                bool ok = follow ? await client.FollowUserAsync(user.Id) : await client.UnfollowUserAsync(user.Id);
                if (Output.EmitStructured(
                    new Dictionary<string, object> { ["ok"] = ok, ["screenName"] = handle, ["userId"] = user.Id },
                    asJson, asYaml)) return 0;
                Spectre.Console.AnsiConsole.MarkupLine($"[green]✅ {(follow ? "Followed" : "Unfollowed")} @{handle}[/]");
            }
            return 0;
        });
    });
    return cmd;
}
root.AddCommand(MakeFollowCmd("follow", "Follow a user", true, jsonOpt, yamlOpt));
root.AddCommand(MakeFollowCmd("unfollow", "Unfollow a user", false, jsonOpt, yamlOpt));

// ── SHOW (resolve from cache) ─────────────────────────────────────────────
{
    var showCmd = new Command("show", "Show a tweet by cache index");
    var idxArg = new Argument<int>("index", "Cache index (1-based from last feed/search)");
    showCmd.AddArgument(idxArg);
    showCmd.AddOption(jsonOpt);
    showCmd.AddOption(yamlOpt);

    showCmd.SetHandler(async (ctx) =>
    {
        var idx = ctx.ParseResult.GetValueForArgument(idxArg);
        var asJson = ctx.ParseResult.GetValueForOption(jsonOpt);
        var asYaml = ctx.ParseResult.GetValueForOption(yamlOpt);

        ctx.ExitCode = await RunAsync(async () =>
        {
            var (tweetId, cacheSize) = TweetCache.ResolveCachedTweet(idx);
            if (tweetId is null)
            {
                Spectre.Console.AnsiConsole.MarkupLine(cacheSize == 0
                    ? "[red]No cached results. Run 'feed' or 'search' first.[/]"
                    : $"[red]Index {idx} out of range (cache has {cacheSize} tweets)[/]");
                return 1;
            }

            var (client, _) = GetClient();
            using (client)
            {
                var (tweet, replies) = await client.FetchTweetDetailAsync(tweetId);
                if (tweet is null) { Spectre.Console.AnsiConsole.MarkupLine("[red]Tweet not found[/]"); return 1; }

                if (Output.EmitStructured(new Dictionary<string, object>
                {
                    ["tweet"] = Serialization.TweetToDict(tweet),
                    ["replies"] = replies.Select(t => (object)Serialization.TweetToDict(t)).ToList(),
                }, asJson, asYaml)) return 0;

                Formatter.PrintTweetDetail(tweet);
                if (replies.Count > 0)
                    Formatter.PrintTweetTable(replies, title: $"💬 Replies ({replies.Count})");
            }
            return 0;
        });
    });

    root.AddCommand(showCmd);
}

// ── MCP SERVER ────────────────────────────────────────────────────────────
{
    var mcpCmd = new Command("mcp-server", "Run as an MCP server for LLM clients");
    var hostOpt = new Option<string>(
        "--host",
        description: "Host/IP to bind to",
        getDefaultValue: () => "localhost");
    var portOpt = new Option<int>(
        "--port",
        description: "TCP port to listen on",
        getDefaultValue: () => 3001);
    var stdioOpt = new Option<bool>(
        "--stdio",
        description: "Use stdio transport instead of HTTP (for Claude Desktop, etc.)");

    mcpCmd.AddOption(hostOpt);
    mcpCmd.AddOption(portOpt);
    mcpCmd.AddOption(stdioOpt);

    mcpCmd.SetHandler(async (ctx) =>
    {
        var host = ctx.ParseResult.GetValueForOption(hostOpt) ?? "localhost";
        var port = ctx.ParseResult.GetValueForOption(portOpt);
        var useStdio = ctx.ParseResult.GetValueForOption(stdioOpt);

        ctx.ExitCode = await McpServer.RunAsync(host, port, useStdio);
    });

    root.AddCommand(mcpCmd);
}

// ── VERSION ───────────────────────────────────────────────────────────────
{
    var versionCmd = new Command("version", "Show version information");
    versionCmd.SetHandler(() =>
    {
        Spectre.Console.AnsiConsole.MarkupLine("[bold]xcli (XCliSharp) v1.0.0[/] — A C# port of twitter-cli");
        Spectre.Console.AnsiConsole.MarkupLine("  Runtime: [cyan].NET " + Environment.Version + "[/]");
    });
    root.AddCommand(versionCmd);
}

return await root.InvokeAsync(args);
