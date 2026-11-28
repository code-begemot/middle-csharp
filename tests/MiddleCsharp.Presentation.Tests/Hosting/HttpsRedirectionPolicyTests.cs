using Microsoft.Extensions.Configuration;
using MiddleCsharp.Presentation.Hosting;

namespace MiddleCsharp.Presentation.Tests.Hosting;

public sealed class HttpsRedirectionPolicyTests
{
    [Fact]
    public void IsEnabled_MissingSetting_DefaultsToTrue()
    {
        // Arrange
        var configuration = BuildConfiguration();

        // Act
        var isEnabled = HttpsRedirectionPolicy.IsEnabled(configuration);

        // Assert
        Assert.True(isEnabled);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void IsEnabled_ExplicitSetting_ReturnsConfiguredValue(string value, bool expected)
    {
        // Arrange
        var configuration = BuildConfiguration(value);

        // Act
        var isEnabled = HttpsRedirectionPolicy.IsEnabled(configuration);

        // Assert
        Assert.Equal(expected, isEnabled);
    }

    private static IConfiguration BuildConfiguration(string? value = null)
    {
        var values = new Dictionary<string, string?>();

        if (value is not null)
        {
            values["HttpsRedirection:Enabled"] = value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
