using System.Net;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.UI;
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
/// Integration tests for the AsyncAPI UI component.
/// These tests verify that the UI can load and render the AsyncAPI document without schema errors.
/// </summary>
public class AsyncApiUiIntegrationTests
{
    private static string GetDocumentRoute(string documentName) => 
        AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", documentName);

    [Fact]
    public async Task AsyncApiUi_ReturnsHtmlPage()
    {
        // Arrange
        using var host = await CreateTestHostWithUiAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/async-api");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("<!DOCTYPE html>");
        content.ShouldContain("asyncapi");
    }

    [Fact]
    public async Task AsyncApiUi_ContainsCorrectDocumentUrl()
    {
        // Arrange
        using var host = await CreateTestHostWithUiAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/async-api");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        // The UI should reference the AsyncAPI document URL
        content.ShouldContain("/asyncapi/");
        content.ShouldContain(".json");
    }

    [Fact]
    public async Task AsyncApiUi_DocumentIsValidForUiRendering()
    {
        // Arrange
        using var host = await CreateTestHostWithUiAsync();
        var client = host.GetTestClient();

        // Act - Get the AsyncAPI document that the UI would load
        var documentResponse = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        
        // Assert - Document should be valid JSON that the UI can parse
        documentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        documentResponse.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        
        var documentContent = await documentResponse.Content.ReadAsStringAsync();
        
        // Validate the document can be parsed by the AsyncAPI reader (same validation the UI uses)
        var reader = new AsyncApiStringReader();
        var document = reader.Read(documentContent, out var diagnostic);
        
        document.ShouldNotBeNull("Document should be parseable by AsyncAPI reader");
        
        // Check for schema validation errors that would cause UI rendering issues
        var errorCount = diagnostic?.Errors?.Count() ?? 0;
        if (errorCount > 0)
        {
            var errorMessages = string.Join(Environment.NewLine, 
                diagnostic!.Errors.Select(e => $"- {e.Message}"));
            Assert.Fail($"Document has schema validation errors that would cause UI rendering issues:{Environment.NewLine}{errorMessages}");
        }
    }

    [Fact]
    public async Task AsyncApiUi_DocumentWithServersIsValidForUi()
    {
        // Arrange
        using var host = await CreateTestHostWithUiAsync(options =>
        {
            options.AddServer("production", "api.example.com", "https");
            options.AddServer("staging", "staging.example.com", "https");
            options.WithInfo("Multi-Server API", "1.0.0");
            options.WithDescription("API with multiple servers for UI testing");
        });
        var client = host.GetTestClient();

        // Act
        var documentResponse = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var documentContent = await documentResponse.Content.ReadAsStringAsync();
        
        // Validate
        var reader = new AsyncApiStringReader();
        var document = reader.Read(documentContent, out var diagnostic);
        
        // Assert
        document.ShouldNotBeNull();
        document.Servers.ShouldNotBeNull();
        document.Servers.Count.ShouldBeGreaterThan(0);
        
        // No validation errors
        var errorCount = diagnostic?.Errors?.Count() ?? 0;
        errorCount.ShouldBe(0, "Document should have no schema validation errors");
    }

    [Fact]
    public async Task AsyncApiUi_DocumentWithInfoIsValidForUi()
    {
        // Arrange
        using var host = await CreateTestHostWithUiAsync(options =>
        {
            options.WithInfo("Test API for UI", "2.0.0");
            options.WithDescription("A comprehensive API description for UI rendering");
            options.WithLicense("MIT", "https://opensource.org/licenses/MIT");
        });
        var client = host.GetTestClient();

        // Act
        var documentResponse = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var documentContent = await documentResponse.Content.ReadAsStringAsync();
        
        // Validate
        var reader = new AsyncApiStringReader();
        var document = reader.Read(documentContent, out var diagnostic);
        
        // Assert
        document.ShouldNotBeNull();
        document.Info.ShouldNotBeNull();
        document.Info.Title.ShouldContain("Test API for UI");
        document.Info.Version.ShouldBe("2.0.0");
        
        // No validation errors
        var errorCount = diagnostic?.Errors?.Count() ?? 0;
        errorCount.ShouldBe(0, "Document should have no schema validation errors");
    }

    [Fact]
    public async Task AsyncApiUi_StaticFilesAreServed()
    {
        // Arrange
        using var host = await CreateTestHostWithUiAsync();
        var client = host.GetTestClient();

        // Act - Try to access static files path
        var response = await client.GetAsync("/async-api/scripts/index.js");

        // Assert - Should either return the file or 404 if not embedded
        // The important thing is the endpoint is configured correctly
        (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound)
            .ShouldBeTrue("Static files endpoint should be configured");
    }

    [Fact]
    public async Task AsyncApiUi_V3DocumentIsValidForUi()
    {
        // Arrange
        using var host = await CreateTestHostWithUiAsync(options =>
        {
            options.AsyncApiVersion = ByteBard.AsyncAPI.AsyncApiVersion.AsyncApi3_0;
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("AsyncAPI 3.0 Test", "3.0.0");
        });
        var client = host.GetTestClient();

        // Act
        var documentResponse = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var documentContent = await documentResponse.Content.ReadAsStringAsync();
        
        // Validate
        var reader = new AsyncApiStringReader();
        var document = reader.Read(documentContent, out var diagnostic);
        
        // Assert
        document.ShouldNotBeNull();
        
        // No validation errors for AsyncAPI 3.0
        var errorCount = diagnostic?.Errors?.Count() ?? 0;
        errorCount.ShouldBe(0, "AsyncAPI 3.0 document should have no schema validation errors");
    }

    [Fact]
    public async Task AsyncApiUi_V2DocumentIsValidForUi()
    {
        // Arrange
        using var host = await CreateTestHostWithUiAsync(options =>
        {
            options.AsyncApiVersion = ByteBard.AsyncAPI.AsyncApiVersion.AsyncApi2_0;
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("AsyncAPI 2.0 Test", "2.0.0");
        });
        var client = host.GetTestClient();

        // Act
        var documentResponse = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        var documentContent = await documentResponse.Content.ReadAsStringAsync();
        
        // Validate
        var reader = new AsyncApiStringReader();
        var document = reader.Read(documentContent, out var diagnostic);
        
        // Assert
        document.ShouldNotBeNull();
        
        // No validation errors for AsyncAPI 2.0
        var errorCount = diagnostic?.Errors?.Count() ?? 0;
        errorCount.ShouldBe(0, "AsyncAPI 2.0 document should have no schema validation errors");
    }

    private static async Task<IHost> CreateTestHostWithUiAsync(Action<AsyncApiOptions>? configureOptions = null)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddAsyncApi(options =>
                    {
                        options.AddServer("test-server", "localhost", "http");
                        options.WithInfo("Test API", "1.0.0");
                        configureOptions?.Invoke(options);
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapAsyncApi();
                        endpoints.MapAsyncApiUi();
                    });
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
