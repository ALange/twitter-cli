# XCliSharp — C# port of twitter-cli

A faithful C# port of [twitter-cli](../README.md), built on .NET 8+ with:

- **System.CommandLine** — CLI framework (mirrors Click)
- **System.Text.Json** — JSON parsing
- **YamlDotNet** — YAML output
- **Spectre.Console** — Rich terminal tables and panels (mirrors Rich)
- **ModelContextProtocol.AspNetCore** — MCP server (HTTP + stdio transports)
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

## MCP Server

XCliSharp includes a built-in [Model Context Protocol](https://modelcontextprotocol.io/) server,
allowing LLMs (Claude, GPT, etc.) to call Twitter/X tools directly.

### Start the server (HTTP transport)
```bash
# Default: localhost:3001
xcli mcp-server

# Custom host/port
xcli mcp-server --host 0.0.0.0 --port 8000
```

### Start the server (stdio transport — for Claude Desktop)
```bash
xcli mcp-server --stdio
```

### Exported MCP tools

| Tool | Description |
|------|-------------|
| `search_tweet` | Search tweets by keyword, user, date, language, engagement |
| `get_home_timeline` | Fetch the home timeline |
| `get_user_timeline` | Fetch a user's tweets |
| `get_user_profile` | Fetch a user's profile |

#### `search_tweet` parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `query` | string | Search keywords |
| `product` | string | `Top` \| `Latest` \| `Photos` \| `Videos` (default: `Top`) |
| `from_user` | string? | Only from this user (@handle without @) |
| `to_user` | string? | Only directed at this user |
| `lang` | string? | ISO 639-1 language code (e.g. `en`, `ja`) |
| `since` | string? | Start date YYYY-MM-DD |
| `until` | string? | End date YYYY-MM-DD |
| `min_likes` | int? | Minimum number of likes |
| `min_retweets` | int? | Minimum number of retweets |
| `max_results` | int | Maximum tweets to return (1–200, default 20) |

### Claude Desktop configuration

Add to your `claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "xcli": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/src-csharp/XCliSharp", "--", "mcp-server", "--stdio"],
      "env": {
        "TWITTER_AUTH_TOKEN": "your_auth_token",
        "TWITTER_CT0": "your_ct0"
      }
    }
  }
}
```

Or publish the app first for faster startup:
```json
{
  "mcpServers": {
    "xcli": {
      "command": "/path/to/xcli",
      "args": ["mcp-server", "--stdio"],
      "env": {
        "TWITTER_AUTH_TOKEN": "your_auth_token",
        "TWITTER_CT0": "your_ct0"
      }
    }
  }
}
```

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
dotnet run --project XCliSharp -- mcp-server --host localhost --port 3001

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
| `XCliSharp/src/McpTools.cs` | MCP tool definitions (`[McpServerToolType]`) |
| `XCliSharp/src/McpServer.cs` | MCP server runner (HTTP + stdio) |
| `XCliSharp/Program.cs` | CLI entry point (System.CommandLine) |
