"""MCP server for twitter-cli.

Exposes Twitter/X operations as MCP tools so that LLMs can search tweets,
read timelines, look up user profiles, and more.

Usage (stdio transport, for use with Claude Desktop or any MCP client):
    twitter-mcp

Usage (network/HTTP transport, for remote or multi-client access):
    twitter-mcp --mcp-port 8000

The server reads authentication credentials from the browser just like the
main CLI does (via browser-cookie3).
"""

from __future__ import annotations

import json
import logging
from typing import List, Optional

import click

try:
    from mcp.server.fastmcp import FastMCP
except ImportError as _mcp_missing:
    raise ImportError(
        "The 'mcp' package is required to run the MCP server. "
        "Install it with: uv tool install 'twitter-cli[mcp]' or pip install 'twitter-cli[mcp]'"
    ) from _mcp_missing

from .auth import get_cookies
from .client import TwitterClient
from .config import load_config
from .models import Tweet
from .search import build_search_query
from .serialization import tweet_to_compact_dict, user_profile_to_dict

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Server setup
# ---------------------------------------------------------------------------

mcp = FastMCP(
    "twitter-cli",
    instructions=(
        "Access Twitter/X via this server. "
        "Use search_tweets to find tweets by keyword or advanced filters. "
        "Use get_home_timeline or get_user_timeline to read feeds. "
        "Use get_user_profile to look up a user's details."
    ),
)

# ---------------------------------------------------------------------------
# Internal helpers
# ---------------------------------------------------------------------------


def _make_client() -> TwitterClient:
    """Create an authenticated TwitterClient from browser cookies."""
    cookies = get_cookies()
    config = load_config()
    rate_limit_config = config.get("rateLimit")
    return TwitterClient(
        cookies["auth_token"],
        cookies["ct0"],
        rate_limit_config,
        cookie_string=cookies.get("cookie_string"),
    )


def _tweets_to_result(tweets: List[Tweet]) -> str:
    """Serialize tweets to a compact JSON string suitable for LLM consumption."""
    return json.dumps(
        [tweet_to_compact_dict(t) for t in tweets],
        ensure_ascii=False,
        indent=2,
    )


# ---------------------------------------------------------------------------
# Tools
# ---------------------------------------------------------------------------


@mcp.tool()
def search_tweets(
    query: str = "",
    product: str = "Top",
    from_user: Optional[str] = None,
    to_user: Optional[str] = None,
    lang: Optional[str] = None,
    since: Optional[str] = None,
    until: Optional[str] = None,
    has: Optional[List[str]] = None,
    exclude: Optional[List[str]] = None,
    min_likes: Optional[int] = None,
    min_retweets: Optional[int] = None,
    max_results: int = 20,
) -> str:
    """Search Twitter/X tweets and return a JSON list of matching tweets.

    Args:
        query: Base search keywords (e.g. "python asyncio").
        product: Search tab — one of "Top", "Latest", "Photos", "Videos".
        from_user: Only tweets from this user (screen name, without @).
        to_user: Only tweets directed at this user.
        lang: ISO 639-1 language code (e.g. "en", "fr", "ja").
        since: Start date in YYYY-MM-DD format (inclusive).
        until: End date in YYYY-MM-DD format (inclusive).
        has: Require content types — any of "links", "images", "videos", "media".
        exclude: Exclude content types — any of "retweets", "replies", "links".
        min_likes: Minimum number of likes.
        min_retweets: Minimum number of retweets.
        max_results: Maximum number of tweets to return (default 20, max 200).

    Returns:
        JSON array of tweets, each with keys: id, author, text, likes, rts, time.
    """
    try:
        composed_query = build_search_query(
            query,
            from_user=from_user,
            to_user=to_user,
            lang=lang,
            since=since,
            until=until,
            has=has,
            exclude=exclude,
            min_likes=min_likes,
            min_retweets=min_retweets,
        )
    except ValueError as exc:
        return json.dumps({"error": str(exc)})

    if not composed_query:
        return json.dumps(
            {"error": "Provide a query or at least one filter (e.g. from_user, lang)."}
        )

    valid_products = {"Top", "Latest", "Photos", "Videos"}
    if product not in valid_products:
        return json.dumps({"error": "product must be one of: %s" % ", ".join(sorted(valid_products))})

    count = min(max(1, max_results), 200)

    try:
        client = _make_client()
        tweets = client.fetch_search(composed_query, count, product)
        return _tweets_to_result(tweets[:count])
    except Exception as exc:
        logger.exception("search_tweets failed")
        return json.dumps({"error": str(exc)})


@mcp.tool()
def get_home_timeline(max_results: int = 20) -> str:
    """Fetch the authenticated user's home timeline (For You feed).

    Args:
        max_results: Maximum number of tweets to return (default 20, max 200).

    Returns:
        JSON array of tweets, each with keys: id, author, text, likes, rts, time.
    """
    count = min(max(1, max_results), 200)
    try:
        client = _make_client()
        tweets = client.fetch_home_timeline(count)
        return _tweets_to_result(tweets[:count])
    except Exception as exc:
        logger.exception("get_home_timeline failed")
        return json.dumps({"error": str(exc)})


@mcp.tool()
def get_user_timeline(screen_name: str, max_results: int = 20) -> str:
    """Fetch tweets posted by a specific Twitter/X user.

    Args:
        screen_name: The user's @handle (without the leading @).
        max_results: Maximum number of tweets to return (default 20, max 200).

    Returns:
        JSON array of tweets, each with keys: id, author, text, likes, rts, time.
    """
    screen_name = screen_name.lstrip("@")
    count = min(max(1, max_results), 200)
    try:
        client = _make_client()
        profile = client.fetch_user(screen_name)
        tweets = client.fetch_user_tweets(profile.id, count)
        return _tweets_to_result(tweets[:count])
    except Exception as exc:
        logger.exception("get_user_timeline failed")
        return json.dumps({"error": str(exc)})


@mcp.tool()
def get_user_profile(screen_name: str) -> str:
    """Fetch profile information for a Twitter/X user.

    Args:
        screen_name: The user's @handle (without the leading @).

    Returns:
        JSON object with user profile fields: id, name, screenName, bio,
        location, followers, following, tweets, verified, etc.
    """
    screen_name = screen_name.lstrip("@")
    try:
        client = _make_client()
        profile = client.fetch_user(screen_name)
        return json.dumps(user_profile_to_dict(profile), ensure_ascii=False, indent=2)
    except Exception as exc:
        logger.exception("get_user_profile failed")
        return json.dumps({"error": str(exc)})


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------


@click.command()
@click.option(
    "--mcp-port",
    type=int,
    default=None,
    help=(
        "Port to listen on for network (HTTP) transport. "
        "If omitted, the server communicates over stdio. "
        "WARNING: binds to 0.0.0.0 — ensure the port is firewalled or "
        "access-controlled before exposing it on a shared network."
    ),
)
def main(mcp_port: Optional[int]) -> None:
    """Run the MCP server (stdio by default, HTTP if --mcp-port is given)."""
    if mcp_port is not None:
        mcp.settings.host = "0.0.0.0"
        mcp.settings.port = mcp_port
        mcp.run(transport="streamable-http")
    else:
        mcp.run(transport="stdio")


if __name__ == "__main__":
    main()
