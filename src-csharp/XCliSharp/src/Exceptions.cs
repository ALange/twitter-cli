// Custom exception hierarchy for XCliSharp.
// Mirrors twitter_cli/exceptions.py

namespace XCliSharp;

/// <summary>Base exception for XCliSharp errors.</summary>
public class TwitterException : Exception
{
    public virtual string ErrorCode => "api_error";

    public TwitterException(string message) : base(message) { }
    public TwitterException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Raised when cookies are missing, expired, or invalid.</summary>
public class AuthenticationException : TwitterException
{
    public override string ErrorCode => "not_authenticated";
    public AuthenticationException(string message) : base(message) { }
}

/// <summary>Raised when Twitter rate-limits the request (HTTP 429).</summary>
public class RateLimitException : TwitterException
{
    public override string ErrorCode => "rate_limited";
    public RateLimitException(string message) : base(message) { }
}

/// <summary>Raised when a user or tweet is not found.</summary>
public class NotFoundException : TwitterException
{
    public override string ErrorCode => "not_found";
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Raised when upstream network requests fail.</summary>
public class NetworkException : TwitterException
{
    public override string ErrorCode => "network_error";
    public NetworkException(string message) : base(message) { }
    public NetworkException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Raised when media upload fails.</summary>
public class MediaUploadException : TwitterException
{
    public override string ErrorCode => "media_upload_error";
    public MediaUploadException(string message) : base(message) { }
}

/// <summary>Raised when user input is invalid.</summary>
public class InvalidInputException : TwitterException
{
    public override string ErrorCode => "invalid_input";
    public InvalidInputException(string message) : base(message) { }
}

/// <summary>Raised on non-OK Twitter API responses.</summary>
public class TwitterApiException : TwitterException
{
    public int StatusCode { get; }
    private readonly string _errorCode;
    public override string ErrorCode => _errorCode;

    public TwitterApiException(int statusCode, string message) : base($"Twitter API error (HTTP {statusCode}): {message}")
    {
        StatusCode = statusCode;
        _errorCode = statusCode switch
        {
            401 or 403 => "not_authenticated",
            429 => "rate_limited",
            404 => "not_found",
            _ => "api_error",
        };
    }
}
