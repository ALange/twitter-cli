// MCP server runner for XCliSharp.
// Supports both HTTP (--host/--port) and stdio transports.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace XCliSharp;

public static class McpServer
{
    /// <summary>
    /// Run the MCP server.
    /// </summary>
    /// <param name="host">Host/IP to bind (HTTP transport). Default "localhost".</param>
    /// <param name="port">Port to listen on (HTTP transport). Default 3001.</param>
    /// <param name="useStdio">If true, use stdio transport instead of HTTP.</param>
    /// <returns>Exit code (0 = success).</returns>
    public static async Task<int> RunAsync(string host = "localhost", int port = 3001, bool useStdio = false)
    {
        try
        {
            if (useStdio)
                return await RunStdioAsync();
            return await RunHttpAsync(host, port);
        }
        catch (AuthenticationException ex)
        {
            Console.Error.WriteLine($"Authentication error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"MCP server error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunHttpAsync(string host, int port)
    {
        var url = $"http://{host}:{port}";
        Console.Error.WriteLine($"Starting xcli MCP server on {url}");
        Console.Error.WriteLine("Tools: search_tweet, get_home_timeline, get_user_timeline, get_user_profile");

        var builder = WebApplication.CreateBuilder();

        // Suppress verbose ASP.NET Core startup logs (keep stderr clean)
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithTools<McpTools>();

        var app = builder.Build();
        app.MapMcp();

        await app.RunAsync(url);
        return 0;
    }

    private static async Task<int> RunStdioAsync()
    {
        Console.Error.WriteLine("Starting xcli MCP server on stdio");
        Console.Error.WriteLine("Tools: search_tweet, get_home_timeline, get_user_timeline, get_user_profile");

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<McpTools>();

        await builder.Build().RunAsync();
        return 0;
    }
}
