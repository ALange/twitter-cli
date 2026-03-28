---
name: twitter-cli
description: Use twitter-cli for ALL Twitter/X operations — reading tweets, posting, replying, quoting, liking, retweeting, following, searching, user lookups. Invoke whenever user requests any Twitter interaction.
author: jackwener
version: "0.8.0"
tags:
  - twitter
  - x
  - social-media
  - terminal
  - cli
---

# twitter-cli — Twitter/X CLI Tool

**Binary:** `twitter`
**Credentials:** browser cookies (auto-extracted) or env vars

## Setup

```bash
# Install (requires Python 3.8+)
uv tool install twitter-cli
# Or: pipx install twitter-cli

# Upgrade to latest (recommended to avoid API errors)
uv tool upgrade twitter-cli
# Or: pipx upgrade twitter-cli
```

## Authentication

**IMPORTANT FOR AGENTS**: Before executing ANY twitter-cli command, you MUST first check if credentials exist. If not, you MUST proactively guide the user through the authentication process. Do NOT assume credentials are configured.

**CRITICAL**: Write operations (posting tweets, replying, quoting) REQUIRE full browser cookies. Only providing `auth_token` + `ct0` via env vars may result in **226 error** ("looks like automated behavior"). For best results, use browser cookie extraction.

### Step 0: Check if already authenticated

```bash
twx status --yaml >/dev/null && echo "AUTH_OK" || echo "AUTH_NEEDED"
```

If `AUTH_OK`, skip to [Command Reference](#command-reference).
If `AUTH_NEEDED`, proceed to guide the user:

### Step 1: Guide user to authenticate

**Method A: Browser cookie extraction (recommended)**

Ensure user is logged into x.com in one of: Arc, Chrome, Edge, Firefox, Brave. twitter-cli auto-extracts cookies.
All Chrome profiles are scanned automatically. To specify a profile: `TWITTER_CHROME_PROFILE="Profile 2" twx feed`.
To prioritize a specific browser: `TWITTER_BROWSER=chrome twx feed` (supported: arc, chrome, edge, firefox, brave).

```bash
twx whoami
```

**Method B: Environment variables**

```bash
export TWITTER_AUTH_TOKEN="<auth_token from browser>"
export TWITTER_CT0="<ct0 from browser>"
twx whoami
```

**Method C: Full cookie string (for cloud/remote agents)**

Tell the user:

> 我需要你的 Twitter 登录凭证。请按以下步骤获取：
>
> 1. 用 Chrome/Edge/Firefox 打开 https://x.com（确保已登录）
> 2. 按 `F12` 打开开发者工具 → **Network** 标签
> 3. 在页面上刷新，点击任意 `x.com` 请求
> 4. 找到 **Request Headers** → **Cookie:** 这一行，右键 → 复制值
> 5. 把完整 Cookie 字符串发给我
>
> ⚠️ Cookie 包含登录信息，请不要分享给其他人。

Then extract and set env vars:

```bash
FULL_COOKIE="<user's cookie string>"
export TWITTER_AUTH_TOKEN=$(echo "$FULL_COOKIE" | grep -oE 'auth_token=[a-f0-9]+' | cut -d= -f2)
export TWITTER_CT0=$(echo "$FULL_COOKIE" | grep -oE 'ct0=[a-f0-9]+' | cut -d= -f2)
twx whoami
```

### Step 2: Handle common auth issues

| Symptom | Agent action |
|---------|-------------|
| `No Twitter cookies found` | Guide user to login to x.com in browser, or set env vars |
| Read works, write returns 226 | Full cookies missing — use browser cookie extraction instead of env vars |
| `Cookie expired (401/403)` | Ask user to re-login to x.com and retry |
| User changed password | All old cookies invalidated — re-extract |

## Output Format

### Default: Rich table (human-readable)

```bash
twx feed                          # Pretty table output
```

### YAML / JSON: structured output

Non-TTY stdout defaults to YAML automatically. Use `OUTPUT=yaml|json|rich|auto` to override.

```bash
twx feed --yaml
twx feed --json | jq '.[0].text'
```

All machine-readable output uses the envelope documented in [SCHEMA.md](./SCHEMA.md).
Tweet and user payloads now live under `.data`.

### Full text: `--full-text` flag (rich tables only)

Use `--full-text` when the user wants complete post bodies in terminal tables.
It affects rich table list views such as `feed`, `bookmarks`, `search`, `user-posts`, `likes`, `list`, and reply tables in `tweet`.
It does **not** change `--json`, `--yaml`, or `-c` compact output.

```bash
twx feed --full-text
twx search "AI agent" --full-text
twx user-posts elonmusk --max 20 --full-text
twx tweet 1234567890 --full-text
```

### Compact: `-c` flag (minimal tokens for LLM)

```bash
twx -c feed --max 10              # Minimal fields, great for LLM context
twx -c search "AI" --max 20       # ~80% fewer tokens than --json
```

**Compact fields (per tweet):** `id`, `author` (@handle), `text` (truncated 140 chars), `likes`, `rts`, `time` (short format)

## Command Reference

### Read Operations

```bash
twx status                         # Quick auth check
twx status --yaml                  # Structured auth status
twx whoami                         # Current authenticated user
twx whoami --yaml                  # YAML output
twx whoami --json                  # JSON output
twx user elonmusk                  # User profile
twx user elonmusk --json           # JSON output
twx feed                           # Home timeline (For You)
twx feed -t following              # Following timeline
twx feed --max 50                  # Limit count
twx feed --full-text               # Show full post body in table
twx feed --filter                  # Enable ranking filter
twx feed --yaml > tweets.yaml      # Export as YAML
twx feed --input tweets.json       # Read from local JSON file
twx bookmarks                      # Bookmarked tweets
twx bookmarks --full-text          # Full text in bookmarks table
twx bookmarks --max 30 --yaml
twx search "keyword"               # Search tweets
twx search "AI agent" -t Latest --max 50
twx search "AI agent" --full-text  # Full text in search results
twx search "topic" -o results.json # Save to file
twx tweet 1234567890               # Tweet detail + replies
twx tweet 1234567890 --full-text   # Full text in reply table
twx tweet https://x.com/user/status/12345  # Accepts URL
twx show 2                         # Open tweet #2 from last feed/search list
twx show 2 --full-text             # Full text in reply table
twx show 2 --json                  # Structured output
twx list 1539453138322673664       # List timeline
twx list 1539453138322673664 --full-text
twx user-posts elonmusk --max 20   # User's tweets
twx user-posts elonmusk --full-text
twx likes elonmusk --max 30        # User's likes (own only, see note)
twx likes elonmusk --full-text
twx followers elonmusk --max 50    # Followers
twx following elonmusk --max 50    # Following
```

### Write Operations

```bash
twx post "Hello from twitter-cli!"              # Post tweet
twx post "Hello!" --image photo.jpg              # Post with image
twx post "Gallery" -i a.png -i b.jpg             # Up to 4 images
twx reply 1234567890 "Great tweet!"              # Reply (standalone)
twx reply 1234567890 "Nice!" -i pic.png          # Reply with image
twx post "reply text" --reply-to 1234567890      # Reply (via post)
twx quote 1234567890 "Interesting take"          # Quote-tweet
twx quote 1234567890 "Look" -i chart.png         # Quote with image
twx delete 1234567890                            # Delete tweet
twx like 1234567890                              # Like
twx unlike 1234567890                            # Unlike
twx retweet 1234567890                           # Retweet
twx unretweet 1234567890                         # Unretweet
twx bookmark 1234567890                          # Bookmark
twx unbookmark 1234567890                        # Unbookmark
twx follow elonmusk                              # Follow user
twx unfollow elonmusk                            # Unfollow user
```

**Image upload notes:**
- Supported formats: JPEG, PNG, GIF, WebP
- Max file size: 5 MB per image
- Max 4 images per tweet
- Use `--image` / `-i` (repeatable)

## Agent Workflows

### Post and verify

```bash
twx post "My tweet text" 2>/dev/null
# Output includes tweet URL: 🔗 https://x.com/i/status/<id>
```

### Post with images

```bash
# Single image
twx post "Check this out!" --image /path/to/photo.jpg

# Multiple images
twx post "Photo gallery" -i img1.png -i img2.jpg -i img3.webp
```

### Reply to someone's latest tweet

```bash
TWEET_ID=$(twx user-posts targetuser --max 1 --json | jq -r '.data[0].id')
twx reply "$TWEET_ID" "Nice post!"
```

### Create a thread

```bash
# Post first tweet, capture output for tweet ID
twx post "Thread 1/3: First point"
# Note the tweet ID from output, then:
twx reply <first_tweet_id> "2/3: Second point"
twx reply <second_tweet_id> "3/3: Final point"
```

### Quote-tweet with commentary

```bash
TWEET_ID=$(twx search "interesting topic" --max 1 --json | jq -r '.data[0].id')
twx quote "$TWEET_ID" "This is a great insight!"
```

### Like all search results

```bash
twx search "interesting topic" --max 5 --json | jq -r '.data[].id' | while read id; do
  twx like "$id"
done
```

### Get user info then follow

```bash
twx user targethandle --json | jq '.data | {username, followers, bio}'
twx follow targethandle
```

### Find most popular tweets from a user

```bash
twx user-posts elonmusk --max 20 --json | jq '.data | sort_by(.metrics.likes) | reverse | .[:3] | .[] | {id, text: .text[:80], likes: .metrics.likes}'
```

### Check follower relationship

```bash
MY_NAME=$(twx whoami --json | jq -r '.data.user.username')
twx followers "$MY_NAME" --max 200 --json | jq -r '.data[].username' | grep -q "targetuser" && echo "Yes" || echo "No"
```

### Daily reading workflow

```bash
# Compact mode for token-efficient LLM context
twx -c feed -t following --max 30
twx -c bookmarks --max 20

# Rich table with complete post bodies
twx feed -t following --max 20 --full-text
twx search "AI agent" --max 20 --full-text

# Full JSON for analysis
twx feed -t following --max 30 -o following.json
twx bookmarks --max 20 -o bookmarks.json
```

### Search with jq filtering

```bash
# Tweets with > 100 likes
twx search "AI safety" --max 20 --json | jq '[.data[] | select(.metrics.likes > 100)]'

# Extract just text and author
twx search "rust lang" --max 10 --json | jq '.data[] | {author: .author.screenName, text: .text[:100]}'

# Most engaged tweets
twx search "topic" --max 20 --json | jq '.data | sort_by(.metrics.likes) | reverse | .[:5] | .[].id'
```

## Ranking Filter

Filtering is opt-in. Enable with `--filter`:

```bash
twx feed --filter
twx bookmarks --filter
```

## Error Reference

| Error | Cause | Fix |
|-------|-------|-----|
| `No Twitter cookies found` | Not authenticated | Login to x.com in browser, or set env vars |
| HTTP 226 | Automated detection | Use browser cookie extraction (not env vars) |
| HTTP 401/403 | Cookie expired | Re-login to x.com and retry |
| HTTP 404 | QueryId rotation | Retry (auto-fallback built in) |
| HTTP 429 | Rate limited | Wait 15+ minutes, then retry |
| Error 187 | Duplicate tweet | Change text content |
| Error 186 | Tweet too long | Keep under 280 chars |

## Limitations

- **Images only** — video/GIF animation upload not yet supported (image upload supports JPEG/PNG/GIF/WebP)
- **No DMs** — no direct messaging
- **No notifications** — can't read notifications
- **No polls** — can't create polls
- **Single account** — one set of credentials at a time
- **Likes are private** — Twitter/X made all likes private since June 2024. `twx likes` only works for your own account

## Safety Notes

- Write operations have built-in random delays (1.5–4s) to avoid rate limits.
- TLS fingerprint and User-Agent are automatically matched to the Chrome version used.
- Do not ask users to share raw cookie values in chat logs.
- Prefer local browser cookie extraction over manual secret copy/paste.
- Agent should treat cookie values as secrets (do not echo to stdout unnecessarily).
