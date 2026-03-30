// Tests for Output helpers - mirrors test_output.py
using XCliSharp;
using Xunit;

namespace XCliSharp.Tests;

public class OutputTests
{
    [Fact]
    public void ResolveFormat_AsJson_ReturnsJson()
    {
        var fmt = Output.ResolveFormat(asJson: true, asYaml: false);
        Assert.Equal(OutputFormat.Json, fmt);
    }

    [Fact]
    public void ResolveFormat_AsYaml_ReturnsYaml()
    {
        var fmt = Output.ResolveFormat(asJson: false, asYaml: true);
        Assert.Equal(OutputFormat.Yaml, fmt);
    }

    [Fact]
    public void ResolveFormat_BothJsonAndYaml_ThrowsInvalidInputException()
    {
        Assert.Throws<InvalidInputException>(() =>
            Output.ResolveFormat(asJson: true, asYaml: true));
    }

    [Fact]
    public void ResolveFormat_OUTPUTEnvJson_ReturnsJson()
    {
        Environment.SetEnvironmentVariable("OUTPUT", "json");
        try
        {
            var fmt = Output.ResolveFormat(asJson: false, asYaml: false);
            Assert.Equal(OutputFormat.Json, fmt);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OUTPUT", null);
        }
    }

    [Fact]
    public void ResolveFormat_OUTPUTEnvYaml_ReturnsYaml()
    {
        Environment.SetEnvironmentVariable("OUTPUT", "yaml");
        try
        {
            var fmt = Output.ResolveFormat(asJson: false, asYaml: false);
            Assert.Equal(OutputFormat.Yaml, fmt);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OUTPUT", null);
        }
    }

    [Fact]
    public void WrapSuccess_CreatesCorrectPayload()
    {
        var data = new { foo = "bar" };
        var payload = Output.WrapSuccess(data);

        Assert.True((bool)payload["ok"]!);
        Assert.Equal("1", payload["schema_version"]?.ToString());
        Assert.Same(data, payload["data"]);
    }

    [Fact]
    public void WrapError_CreatesCorrectPayload()
    {
        var payload = Output.WrapError("not_found", "User not found");

        Assert.False((bool)payload["ok"]!);
        Assert.Equal("1", payload["schema_version"]?.ToString());

        var error = (Dictionary<string, object?>)payload["error"]!;
        Assert.Equal("not_found", error["code"]?.ToString());
        Assert.Equal("User not found", error["message"]?.ToString());
    }

    [Fact]
    public void WrapError_WithDetails_IncludesDetails()
    {
        var payload = Output.WrapError("error", "msg", details: new { id = "123" });
        var error = (Dictionary<string, object?>)payload["error"]!;
        Assert.NotNull(error["details"]);
    }
}
