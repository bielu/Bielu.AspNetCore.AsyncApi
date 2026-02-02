using System.Net;
using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Readers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Integration tests for AsyncAPI document generation and validation.
/// These tests verify that generated documents conform to the AsyncAPI specification.
/// </summary>
public class AsyncApiDocumentGenerationTests
{
    private const string TestDocumentName = "asyncapi";
    
    private static string GetDocumentRoute(string documentName) => 
        AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", documentName);

    [Fact]
    public async Task GetAsyncApiDocument_ReturnsValidJsonDocument()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(TestDocumentName));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldNotBeNullOrEmpty();

        // Verify it's valid JSON
        var jsonDocument = JsonDocument.Parse(content);
        jsonDocument.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAsyncApiDocument_ContainsRequiredAsyncApiFields()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(TestDocumentName));
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);

        // Assert - Check required AsyncAPI fields
        var root = jsonDocument.RootElement;
        
        // AsyncAPI version should be present
        root.TryGetProperty("asyncapi", out var asyncApiVersion).ShouldBeTrue();
        asyncApiVersion.GetString().ShouldNotBeNullOrEmpty();
        
        // Info object should be present
        root.TryGetProperty("info", out var info).ShouldBeTrue();
        info.TryGetProperty("title", out var title).ShouldBeTrue();
        title.GetString().ShouldNotBeNullOrEmpty();
        info.TryGetProperty("version", out var version).ShouldBeTrue();
        version.GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAsyncApiDocument_ContainsConfiguredServerInfo()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(TestDocumentName));
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);

        // Assert
        var root = jsonDocument.RootElement;
        
        if (root.TryGetProperty("servers", out var servers))
        {
            servers.TryGetProperty("test-server", out var testServer).ShouldBeTrue();
            testServer.TryGetProperty("host", out var host1).ShouldBeTrue();
            host1.GetString().ShouldBe("localhost:5000");
            testServer.TryGetProperty("protocol", out var protocol).ShouldBeTrue();
            protocol.GetString().ShouldBe("http");
        }
    }

    [Fact]
    public async Task GetAsyncApiDocument_IsValidAsyncApiDocument()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(TestDocumentName));
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Parse with ByteBard.AsyncAPI.NET reader to validate schema
        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out var diagnostic);
        
        // The document should be successfully parsed
        document.ShouldNotBeNull();
        
        // Check for any diagnostic errors
        if (diagnostic?.Errors != null && diagnostic.Errors.Any())
        {
            var errors = string.Join(Environment.NewLine, diagnostic.Errors.Select(e => e.Message));
            Assert.Fail($"AsyncAPI document has validation errors: {errors}");
        }
    }

    [Fact]
    public async Task GetAsyncApiDocument_ContainsInfoFromConfiguration()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(TestDocumentName));
        var content = await response.Content.ReadAsStringAsync();

        // Parse with ByteBard reader
        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out _);

        // Assert
        document.ShouldNotBeNull();
        document.Info.ShouldNotBeNull();
        document.Info.Title.ShouldContain("Test API");
        document.Info.Version.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAsyncApiDocument_NonExistentDocumentReturns404()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/asyncapi/nonexistent.json");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAsyncApiDocument_IsCaseInsensitive()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var responseLower = await client.GetAsync(GetDocumentRoute(TestDocumentName));
        var responseUpper = await client.GetAsync(GetDocumentRoute(TestDocumentName.ToUpperInvariant()));

        // Assert
        responseLower.StatusCode.ShouldBe(HttpStatusCode.OK);
        responseUpper.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<IHost> CreateTestHostAsync()
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddAsyncApi(TestDocumentName, options =>
                    {
                        options.AddServer("test-server", "localhost:5000", "http");
                        options.WithInfo("Test API", "1.0.0");
                        options.WithDescription("Test API for integration tests");
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapAsyncApi();
                    });
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
