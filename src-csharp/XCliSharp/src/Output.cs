// Structured output helpers for XCliSharp.
// Mirrors twitter_cli/output.py

using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Serialization;

namespace XCliSharp;

public enum OutputFormat { Rich, Json, Yaml }

public static class Output
{
    private const string SchemaVersion = "1";

    public static OutputFormat ResolveFormat(bool asJson, bool asYaml)
    {
        if (asJson && asYaml)
            throw new InvalidInputException("Use only one of --json or --yaml.");
        if (asYaml) return OutputFormat.Yaml;
        if (asJson) return OutputFormat.Json;

        var env = Environment.GetEnvironmentVariable("OUTPUT")?.Trim().ToLowerInvariant() ?? "auto";
        return env switch
        {
            "yaml" => OutputFormat.Yaml,
            "json" => OutputFormat.Json,
            "rich" => OutputFormat.Rich,
            _ => Console.IsOutputRedirected ? OutputFormat.Yaml : OutputFormat.Rich,
        };
    }

    public static bool EmitStructured(object data, bool asJson, bool asYaml)
    {
        var fmt = ResolveFormat(asJson, asYaml);
        if (fmt == OutputFormat.Rich) return false;

        var payload = WrapSuccess(data);
        EmitPayload(payload, fmt);
        return true;
    }

    public static void EmitError(string code, string message, bool asJson = false, bool asYaml = false, object? details = null)
    {
        var fmt = ResolveFormat(asJson, asYaml);
        if (fmt == OutputFormat.Rich)
        {
            Console.Error.WriteLine($"Error [{code}]: {message}");
            return;
        }
        var payload = WrapError(code, message, details);
        EmitPayload(payload, fmt);
    }

    public static Dictionary<string, object?> WrapSuccess(object data) => new()
    {
        ["ok"] = true,
        ["schema_version"] = SchemaVersion,
        ["data"] = data,
    };

    public static Dictionary<string, object?> WrapError(string code, string message, object? details = null)
    {
        var error = new Dictionary<string, object?> { ["code"] = code, ["message"] = message };
        if (details is not null) error["details"] = details;
        return new Dictionary<string, object?> { ["ok"] = false, ["schema_version"] = SchemaVersion, ["error"] = error };
    }

    private static void EmitPayload(object payload, OutputFormat fmt)
    {
        if (fmt == OutputFormat.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }));
        }
        else
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .Build();
            Console.Write(serializer.Serialize(payload));
        }
    }
}
