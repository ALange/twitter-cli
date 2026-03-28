"""Tests for the MCP server tools (twitter_cli/mcp_server.py).

All tests monkeypatch `_make_client` so no real network calls are made.
"""

from __future__ import annotations

import json
from typing import Any, List, Optional
from unittest.mock import MagicMock, patch

import pytest
from click.testing import CliRunner

pytest.importorskip("mcp", reason="mcp extra not installed; run: uv sync --extra mcp")

from twitter_cli.models import Author, Metrics, Tweet, UserProfile


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _make_tweet(tweet_id: str = "1", text: str = "hello", **kwargs: Any) -> Tweet:
    return Tweet(
        id=tweet_id,
        text=text,
        author=Author(id="u1", name="Alice", screen_name="alice"),
        metrics=Metrics(likes=10, retweets=2, replies=1, quotes=0, views=100, bookmarks=0),
        created_at="Sat Mar 01 12:00:00 +0000 2025",
        **kwargs,
    )


def _make_profile(screen_name: str = "alice") -> UserProfile:
    return UserProfile(
        id="u1",
        name="Alice",
        screen_name=screen_name,
        bio="Test bio",
        followers_count=500,
        following_count=100,
        tweets_count=200,
    )


def _fake_client(
    search_tweets: Optional[List[Tweet]] = None,
    timeline_tweets: Optional[List[Tweet]] = None,
    user_tweets: Optional[List[Tweet]] = None,
    profile: Optional[UserProfile] = None,
) -> MagicMock:
    client = MagicMock()
    client.fetch_search.return_value = search_tweets or []
    client.fetch_home_timeline.return_value = timeline_tweets or []
    client.fetch_user.return_value = profile or _make_profile()
    client.fetch_user_tweets.return_value = user_tweets or []
    return client


# ---------------------------------------------------------------------------
# search_tweets tests
# ---------------------------------------------------------------------------


class TestSearchTweets:
    def test_basic_keyword_search(self, monkeypatch: pytest.MonkeyPatch) -> None:
        tweets = [_make_tweet("1", "python is great"), _make_tweet("2", "asyncio rocks")]
        monkeypatch.setattr("twitter_cli.mcp_server._make_client", lambda: _fake_client(search_tweets=tweets))

        from twitter_cli.mcp_server import search_tweets

        result = search_tweets(query="python")
        data = json.loads(result)
        assert isinstance(data, list)
        assert len(data) == 2
        assert data[0]["id"] == "1"
        assert "@alice" == data[0]["author"]

    def test_returns_compact_fields(self, monkeypatch: pytest.MonkeyPatch) -> None:
        tweets = [_make_tweet("42", "compact test")]
        monkeypatch.setattr("twitter_cli.mcp_server._make_client", lambda: _fake_client(search_tweets=tweets))

        from twitter_cli.mcp_server import search_tweets

        result = search_tweets(query="compact")
        data = json.loads(result)
        assert len(data) == 1
        item = data[0]
        for key in ("id", "author", "text", "likes", "rts", "time"):
            assert key in item, "missing key: %s" % key

    def test_empty_query_and_no_filters_returns_error(self, monkeypatch: pytest.MonkeyPatch) -> None:
        monkeypatch.setattr("twitter_cli.mcp_server._make_client", lambda: _fake_client())

        from twitter_cli.mcp_server import search_tweets

        result = search_tweets(query="")
        data = json.loads(result)
        assert "error" in data

    def test_invalid_product_returns_error(self, monkeypatch: pytest.MonkeyPatch) -> None:
        monkeypatch.setattr("twitter_cli.mcp_server._make_client", lambda: _fake_client())

        from twitter_cli.mcp_server import search_tweets

        result = search_tweets(query="test", product="Invalid")
        data = json.loads(result)
        assert "error" in data

    def test_advanced_filters_passed_to_client(self, monkeypatch: pytest.MonkeyPatch) -> None:
        fake = _fake_client(search_tweets=[_make_tweet("1")])
        monkeypatch.setattr("twitter_cli.mcp_server._make_client", lambda: fake)

        from twitter_cli.mcp_server import search_tweets

        search_tweets(
            query="AI",
            from_user="elonmusk",
            lang="en",
            since="2026-01-01",
            until="2026-12-31",
            min_likes=100,
        )
        # Verify the composed query was passed to fetch_search
        call_args = fake.fetch_search.call_args
        assert call_args is not None
        composed = call_args[0][0]
        assert "AI" in composed
        assert "from:elonmusk" in composed
        assert "lang:en" in composed
        assert "since:2026-01-01" in composed
        assert "until:2026-12-31" in composed
        assert "min_faves:100" in composed

    def test_max_results_respected(self, monkeypatch: pytest.MonkeyPatch) -> None:
        many_tweets = [_make_tweet(str(i)) for i in range(50)]
        monkeypatch.setattr(
            "twitter_cli.mcp_server._make_client",
            lambda: _fake_client(search_tweets=many_tweets),
        )

        from twitter_cli.mcp_server import search_tweets

        result = search_tweets(query="test", max_results=5)
        data = json.loads(result)
        assert len(data) == 5

    def test_max_results_capped_at_200(self, monkeypatch: pytest.MonkeyPatch) -> None:
        fake = _fake_client(search_tweets=[])
        monkeypatch.setattr("twitter_cli.mcp_server._make_client", lambda: fake)

        from twitter_cli.mcp_server import search_tweets

        search_tweets(query="test", max_results=9999)
        count_arg = fake.fetch_search.call_args[0][1]
        assert count_arg == 200

    def test_client_error_returns_json_error(self, monkeypatch: pytest.MonkeyPatch) -> None:
        fake = MagicMock()
        fake.fetch_search.side_effect = RuntimeError("network down")
        monkeypatch.setattr("twitter_cli.mcp_server._make_client", lambda: fake)

        from twitter_cli.mcp_server import search_tweets

        result = search_tweets(query="fail")
        data = json.loads(result)
        assert "error" in data
        assert "network down" in data["error"]

    def test_invalid_date_returns_error(self, monkeypatch: pytest.MonkeyPatch) -> None:
        monkeypatch.setattr("twitter_cli.mcp_server._make_client", lambda: _fake_client())

        from twitter_cli.mcp_server import search_tweets

        result = search_tweets(query="test", since="not-a-date")
        data = json.loads(result)
        assert "error" in data


# ---------------------------------------------------------------------------
# get_home_timeline tests
# ---------------------------------------------------------------------------


class TestGetHomeTimeline:
    def test_returns_tweets(self, monkeypatch: pytest.MonkeyPatch) -> None:
        tweets = [_make_tweet("10", "timeline tweet")]
        monkeypatch.setattr(
            "twitter_cli.mcp_server._make_client",
            lambda: _fake_client(timeline_tweets=tweets),
        )

        from twitter_cli.mcp_server import get_home_timeline

        result = get_home_timeline(max_results=10)
        data = json.loads(result)
        assert len(data) == 1
        assert data[0]["id"] == "10"

    def test_client_error_returns_json_error(self, monkeypatch: pytest.MonkeyPatch) -> None:
        fake = MagicMock()
        fake.fetch_home_timeline.side_effect = RuntimeError("auth failed")
        monkeypatch.setattr("twitter_cli.mcp_server._make_client", lambda: fake)

        from twitter_cli.mcp_server import get_home_timeline

        result = get_home_timeline()
        data = json.loads(result)
        assert "error" in data


# ---------------------------------------------------------------------------
# get_user_timeline tests
# ---------------------------------------------------------------------------


class TestGetUserTimeline:
    def test_strips_at_sign(self, monkeypatch: pytest.MonkeyPatch) -> None:
        fake = _fake_client(user_tweets=[_make_tweet("5")])
        monkeypatch.setattr("twitter_cli.mcp_server._make_client", lambda: fake)

        from twitter_cli.mcp_server import get_user_timeline

        get_user_timeline("@alice")
        fake.fetch_user.assert_called_once_with("alice")

    def test_returns_user_tweets(self, monkeypatch: pytest.MonkeyPatch) -> None:
        tweets = [_make_tweet("7", "user post")]
        monkeypatch.setattr(
            "twitter_cli.mcp_server._make_client",
            lambda: _fake_client(user_tweets=tweets),
        )

        from twitter_cli.mcp_server import get_user_timeline

        result = get_user_timeline("alice")
        data = json.loads(result)
        assert len(data) == 1
        assert data[0]["id"] == "7"

    def test_client_error_returns_json_error(self, monkeypatch: pytest.MonkeyPatch) -> None:
        fake = MagicMock()
        fake.fetch_user.side_effect = RuntimeError("user not found")
        monkeypatch.setattr("twitter_cli.mcp_server._make_client", lambda: fake)

        from twitter_cli.mcp_server import get_user_timeline

        result = get_user_timeline("ghost")
        data = json.loads(result)
        assert "error" in data


# ---------------------------------------------------------------------------
# get_user_profile tests
# ---------------------------------------------------------------------------


class TestGetUserProfile:
    def test_returns_profile_fields(self, monkeypatch: pytest.MonkeyPatch) -> None:
        profile = _make_profile("bob")
        monkeypatch.setattr(
            "twitter_cli.mcp_server._make_client",
            lambda: _fake_client(profile=profile),
        )

        from twitter_cli.mcp_server import get_user_profile

        result = get_user_profile("bob")
        data = json.loads(result)
        assert data["screenName"] == "bob"
        assert data["name"] == "Alice"
        assert data["bio"] == "Test bio"
        assert data["followers"] == 500

    def test_strips_at_sign(self, monkeypatch: pytest.MonkeyPatch) -> None:
        fake = _fake_client()
        monkeypatch.setattr("twitter_cli.mcp_server._make_client", lambda: fake)

        from twitter_cli.mcp_server import get_user_profile

        get_user_profile("@charlie")
        fake.fetch_user.assert_called_once_with("charlie")

    def test_client_error_returns_json_error(self, monkeypatch: pytest.MonkeyPatch) -> None:
        fake = MagicMock()
        fake.fetch_user.side_effect = RuntimeError("not found")
        monkeypatch.setattr("twitter_cli.mcp_server._make_client", lambda: fake)

        from twitter_cli.mcp_server import get_user_profile

        result = get_user_profile("nobody")
        data = json.loads(result)
        assert "error" in data

# ---------------------------------------------------------------------------
# main() entry-point tests
# ---------------------------------------------------------------------------


class TestMain:
    def test_stdio_transport_by_default(self) -> None:
        from twitter_cli.mcp_server import main, mcp

        with patch.object(mcp, "run") as mock_run:
            runner = CliRunner()
            result = runner.invoke(main, [])
            assert result.exit_code == 0
            mock_run.assert_called_once_with(transport="stdio")

    def test_network_transport_with_mcp_port(self) -> None:
        from twitter_cli.mcp_server import main, mcp

        original_host = mcp.settings.host
        original_port = mcp.settings.port
        original_transport_security = mcp.settings.transport_security
        try:
            with patch.object(mcp, "run") as mock_run:
                runner = CliRunner()
                result = runner.invoke(main, ["--mcp-port", "9000"])
                assert result.exit_code == 0
                assert mcp.settings.host == "0.0.0.0"
                assert mcp.settings.port == 9000
                # DNS rebinding protection must be disabled so clients connecting
                # via the server's real IP/hostname are not rejected with 421.
                assert mcp.settings.transport_security is None
                mock_run.assert_called_once_with(transport="streamable-http")
        finally:
            mcp.settings.host = original_host
            mcp.settings.port = original_port
            mcp.settings.transport_security = original_transport_security

    def test_invalid_port_exits_with_error(self) -> None:
        from twitter_cli.mcp_server import main

        runner = CliRunner()
        result = runner.invoke(main, ["--mcp-port", "not-a-port"])
        assert result.exit_code != 0
