// Terminal output formatter for XCliSharp using Spectre.Console.
// Mirrors twitter_cli/formatter.py

using Spectre.Console;

namespace XCliSharp;

public static class Formatter
{
    public static string FormatNumber(int n) => n switch
    {
        >= 1_000_000 => $"{n / 1_000_000.0:F1}M",
        >= 1_000 => $"{n / 1_000.0:F1}K",
        _ => n.ToString(),
    };

    public static void PrintTweetTable(
        IEnumerable<Tweet> tweets,
        string? title = null,
        bool fullText = false)
    {
        var tweetList = tweets.ToList();
        title ??= $"[bold]Twitter — {tweetList.Count} tweets[/]";

        var table = new Table
        {
            Title = new TableTitle(title),
            ShowRowSeparators = true,
            Expand = true,
        };

        table.AddColumn(new TableColumn("[dim]#[/]").RightAligned().Width(3));
        table.AddColumn(new TableColumn("[cyan]Author[/]").Width(18));
        table.AddColumn(new TableColumn("Tweet"));
        table.AddColumn(new TableColumn("[green]Stats[/]").Width(24));
        table.AddColumn(new TableColumn("[yellow]Score[/]").RightAligned().Width(6));

        int i = 0;
        foreach (var tweet in tweetList)
        {
            i++;
            // Author
            var verified = tweet.Author.Verified ? " ✓" : "";
            var authorText = $"[cyan]@{EscapeMarkup(tweet.Author.ScreenName)}{verified}[/]";
            if (tweet.IsRetweet && tweet.RetweetedBy is not null)
                authorText += $"\n[dim]🔄 @{EscapeMarkup(tweet.RetweetedBy)}[/]";

            // Text
            var text = tweet.Text.Replace("\n", " ").Trim();
            if (!fullText && text.Length > 120)
                text = text[..117] + "...";

            // Media indicators
            if (tweet.Media?.Count > 0)
            {
                var icons = tweet.Media.Select(m => m.Type switch
                {
                    "photo" => "📷",
                    "video" => "📹",
                    _ => "🎞️",
                });
                text += " " + string.Join(" ", icons);
            }

            // Quoted tweet
            if (tweet.QuotedTweet is { } qt)
            {
                var qtText = qt.Text.Replace("\n", " ");
                if (!fullText && qtText.Length > 60) qtText = qtText[..57] + "...";
                text += $"\n[dim]┌ @{EscapeMarkup(qt.Author.ScreenName)}: {EscapeMarkup(qtText)}[/]";
            }

            text += $"\n[dim link]x.com/{EscapeMarkup(tweet.Author.ScreenName)}/status/{tweet.Id}[/]";

            // Stats
            var relTime = TimeUtil.FormatRelativeTime(tweet.CreatedAt);
            var stats =
                $"[red]❤ {FormatNumber(tweet.Metrics.Likes)}[/]  [green]🔄 {FormatNumber(tweet.Metrics.Retweets)}[/]\n" +
                $"[blue]💬 {FormatNumber(tweet.Metrics.Replies)}[/]  👁 {FormatNumber(tweet.Metrics.Views)}\n" +
                $"[dim]🕐 {relTime}[/]";

            var score = tweet.Score.HasValue ? $"{tweet.Score.Value:F1}" : "-";

            table.AddRow(
                i.ToString(),
                authorText,
                EscapeMarkup(text),
                stats,
                score
            );
        }

        AnsiConsole.Write(table);
    }

    public static void PrintTweetDetail(Tweet tweet)
    {
        var verified = tweet.Author.Verified ? " ✓" : "";
        var header = $"@{tweet.Author.ScreenName}{verified} ({tweet.Author.Name})";

        var sb = new System.Text.StringBuilder();

        if (tweet.IsRetweet && tweet.RetweetedBy is not null)
            sb.AppendLine($"🔄 Retweeted by @{tweet.RetweetedBy}\n");

        sb.AppendLine(tweet.Text);

        if (tweet.Media?.Count > 0)
        {
            sb.AppendLine();
            foreach (var m in tweet.Media)
            {
                var icon = m.Type == "photo" ? "📷" : (m.Type == "video" ? "📹" : "🎞️");
                sb.AppendLine($"{icon} {m.Type}: {m.Url}");
            }
        }

        if (tweet.Urls?.Count > 0)
        {
            sb.AppendLine();
            foreach (var u in tweet.Urls)
                sb.AppendLine($"🔗 {u}");
        }

        if (tweet.QuotedTweet is { } qt)
        {
            sb.AppendLine();
            sb.AppendLine($"┌── Quoted @{qt.Author.ScreenName} ──");
            sb.AppendLine(qt.Text);
        }

        sb.AppendLine();
        sb.AppendLine(
            $"❤️ {FormatNumber(tweet.Metrics.Likes)}  🔄 {FormatNumber(tweet.Metrics.Retweets)}" +
            $"  💬 {FormatNumber(tweet.Metrics.Replies)}  🔖 {FormatNumber(tweet.Metrics.Bookmarks)}" +
            $"  👁️ {FormatNumber(tweet.Metrics.Views)}");
        sb.AppendLine();
        sb.AppendLine($"🕐 {TimeUtil.FormatLocalTime(tweet.CreatedAt)}  |  🔗 x.com/{tweet.Author.ScreenName}/status/{tweet.Id}");

        AnsiConsole.Write(new Panel(sb.ToString().TrimEnd()) { Header = new PanelHeader(header) });
    }

    public static void PrintUserProfile(UserProfile user)
    {
        var verified = user.Verified ? " ✓" : "";
        var header = $"@{user.ScreenName}{verified} — {user.Name}";
        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(user.Bio)) sb.AppendLine(user.Bio);
        if (!string.IsNullOrEmpty(user.Location)) sb.AppendLine($"📍 {user.Location}");
        if (!string.IsNullOrEmpty(user.Url)) sb.AppendLine($"🔗 {user.Url}");
        sb.AppendLine();
        sb.AppendLine(
            $"👥 {FormatNumber(user.FollowersCount)} followers  " +
            $"👤 {FormatNumber(user.FollowingCount)} following  " +
            $"📝 {FormatNumber(user.TweetsCount)} tweets");

        AnsiConsole.Write(new Panel(sb.ToString().TrimEnd()) { Header = new PanelHeader(header) });
    }

    public static void PrintUserTable(IEnumerable<UserProfile> users, string? title = null)
    {
        var userList = users.ToList();
        title ??= $"Users ({userList.Count})";

        var table = new Table
        {
            Title = new TableTitle(title),
            ShowRowSeparators = true,
        };
        table.AddColumn(new TableColumn("[dim]#[/]").Width(3));
        table.AddColumn(new TableColumn("[cyan]Screen Name[/]").Width(20));
        table.AddColumn(new TableColumn("Name").Width(25));
        table.AddColumn(new TableColumn("[green]Followers[/]").Width(10));
        table.AddColumn(new TableColumn("Bio"));

        int i = 0;
        foreach (var user in userList)
        {
            i++;
            var verified = user.Verified ? " ✓" : "";
            table.AddRow(
                i.ToString(),
                $"[cyan]@{EscapeMarkup(user.ScreenName)}{verified}[/]",
                EscapeMarkup(user.Name),
                FormatNumber(user.FollowersCount),
                EscapeMarkup(user.Bio.Length > 80 ? user.Bio[..77] + "..." : user.Bio)
            );
        }

        AnsiConsole.Write(table);
    }

    private static string EscapeMarkup(string text) =>
        text.Replace("[", "[[").Replace("]", "]]");
}
