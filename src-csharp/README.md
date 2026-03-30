# XCliSharp — C# port of twitter-cli

A faithful C# port of [twitter-cli](../README.md), built on .NET 8+ with:

- **System.CommandLine** — CLI framework (mirrors Click)
- **System.Text.Json** — JSON parsing
- **YamlDotNet** — YAML output
- **Spectre.Console** — Rich terminal tables and panels (mirrors Rich)
- **xUnit** — Unit tests

## Commands

All commands from the Python CLI are supported:

### Read Commands
```
xcli feed                          # Home timeline (For You)
xcli feed -t following             # Following feed
xcli bookmarks                     # Bookmarked tweets
xcli search "query"                # Search tweets
xcli search "query" --from user    # Advanced search
xcli user elonmusk                 # User profile
xcli user-posts elonmusk           # User tweets
xcli likes elonmusk                # User likes
xcli tweet <id>                    # Tweet detail + replies
xcli show <n>                      # Show tweet by cache index
xcli followers <handle>            # Followers list
xcli following <handle>            # Following list
```

### Write Commands
```
xcli post "text"                   # Post a tweet
xcli reply <id> "text"             # Reply to a tweet
xcli delete <id>                   # Delete a tweet
xcli like <id>                     # Like a tweet
xcli unlike <id>                   # Unlike a tweet
xcli retweet <id>                  # Retweet
xcli unretweet <id>                # Remove retweet
xcli bookmark <id>                 # Bookmark a tweet
xcli unbookmark <id>               # Remove bookmark
xcli follow <handle>               # Follow a user
xcli unfollow <handle>             # Unfollow a user
```

### Output Options
All commands support `--json` and `--yaml` for machine-readable output.

## Authentication

Set environment variables:
```bash
export TWITTER_AUTH_TOKEN=<your auth_token cookie>
export TWITTER_CT0=<your ct0 cookie>
```

Find these in your browser's DevTools → Application → Cookies → https://x.com

## Build and Run

```bash
cd src-csharp

# Build
dotnet build

# Run
dotnet run --project XCliSharp -- feed
dotnet run --project XCliSharp -- search "AI"

# Run tests
dotnet test

# Publish as single executable
dotnet publish XCliSharp -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

## Architecture

| File | Purpose |
|------|---------|
| `XCliSharp/src/Models.cs` | Data records: Tweet, Author, Metrics, etc. |
| `XCliSharp/src/Exceptions.cs` | Exception hierarchy |
| `XCliSharp/src/Constants.cs` | Bearer token, User-Agent |
| `XCliSharp/src/TimeUtil.cs` | Twitter timestamp formatting |
| `XCliSharp/src/SearchBuilder.cs` | Advanced search query builder |
| `XCliSharp/src/TweetFilter.cs` | Tweet scoring and filtering |
| `XCliSharp/src/Config.cs` | YAML config loading |
| `XCliSharp/src/TweetCache.cs` | Short-lived tweet index cache |
| `XCliSharp/src/Serialization.cs` | JSON serialization |
| `XCliSharp/src/Parser.cs` | GraphQL response parser |
| `XCliSharp/src/Auth.cs` | Cookie authentication (env vars) |
| `XCliSharp/src/TwitterClient.cs` | Twitter GraphQL API client |
| `XCliSharp/src/Formatter.cs` | Rich terminal output (Spectre.Console) |
| `XCliSharp/src/Output.cs` | Structured JSON/YAML output |
| `XCliSharp/Program.cs` | CLI entry point (System.CommandLine) |
