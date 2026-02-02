using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Writers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Tests that validate the generated AsyncAPI schema conforms to the AsyncAPI specification
/// using ByteBard.AsyncAPI.NET reader and writer.
/// </summary>
public class AsyncApiSchemaValidationTests
{
    private const string DefaultDocumentRoute = "/asyncapi/v1.json";

    [Fact]
    public async Task GeneratedDocument_CanBeSerializedAndDeserializedAsV3()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options =>
        {
            options.AsyncApiVersion = AsyncApiVersion.AsyncApi3_0;
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("Schema Validation Test", "1.0.0");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(DefaultDocumentRoute);
        var content = await response.Content.ReadAsStringAsync();

        // Parse with ByteBard reader
        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out var diagnostic);

        // Assert - Validate the document can be parsed
        document.ShouldNotBeNull();
        
        // Re-serialize to verify round-trip
        using var stringWriter = new StringWriter();
        var jsonWriter = new AsyncApiJsonWriter(stringWriter);
        document.SerializeV3(jsonWriter);
        var reserialized = stringWriter.ToString();

        // Parse again to ensure consistency
        var reReadDocument = reader.Read(reserialized, out _);
        reReadDocument.ShouldNotBeNull();
    }

    [Fact]
    public async Task GeneratedDocument_CanBeSerializedAndDeserializedAsV2()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options =>
        {
            options.AsyncApiVersion = AsyncApiVersion.AsyncApi2_0;
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("Schema Validation Test V2", "1.0.0");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(DefaultDocumentRoute);
        var content = await response.Content.ReadAsStringAsync();

        // Parse with ByteBard reader
        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out _);

        // Assert
        document.ShouldNotBeNull();
        
        // Re-serialize to V2
        using var stringWriter = new StringWriter();
        var jsonWriter = new AsyncApiJsonWriter(stringWriter);
        document.SerializeV2(jsonWriter);
        var reserialized = stringWriter.ToString();

        reserialized.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GeneratedDocument_HasValidAsyncApiVersion()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options =>
        {
            options.AsyncApiVersion = AsyncApiVersion.AsyncApi3_0;
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(DefaultDocumentRoute);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);

        // Assert
        var root = jsonDocument.RootElement;
        root.TryGetProperty("asyncapi", out var version).ShouldBeTrue();
        var versionString = version.GetString();
        versionString.ShouldNotBeNullOrEmpty();
        // AsyncAPI 3.0 should have version starting with "3."
        versionString!.ShouldStartWith("3.");
    }

    [Fact]
    public async Task GeneratedDocument_SchemaComponentsAreValid()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options =>
        {
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("Schema Components Test", "1.0.0");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(DefaultDocumentRoute);
        var content = await response.Content.ReadAsStringAsync();

        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out _);

        // Assert
        document.ShouldNotBeNull();
        
        // Components should be valid if present
        if (document.Components?.Schemas != null)
        {
            foreach (var schema in document.Components.Schemas)
            {
                schema.Key.ShouldNotBeNullOrEmpty();
                schema.Value.ShouldNotBeNull();
            }
        }
    }

    [Fact]
    public async Task GeneratedDocument_ServersAreValid()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options =>
        {
            options.AddServer("mqtt-server", "mqtt.example.com", "mqtt");
            options.AddServer("ws-server", "ws.example.com", "websocket");
            options.WithInfo("Multi-Server Test", "1.0.0");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(DefaultDocumentRoute);
        var content = await response.Content.ReadAsStringAsync();

        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out _);

        // Assert
        document.ShouldNotBeNull();
        document.Servers.ShouldNotBeNull();
        document.Servers.ShouldContainKey("mqtt-server");
        document.Servers.ShouldContainKey("ws-server");
        
        document.Servers["mqtt-server"].Host.ShouldBe("mqtt.example.com");
        document.Servers["mqtt-server"].Protocol.ShouldBe("mqtt");
        
        document.Servers["ws-server"].Host.ShouldBe("ws.example.com");
        document.Servers["ws-server"].Protocol.ShouldBe("websocket");
    }

    [Fact]
    public async Task GeneratedDocument_InfoIsValid()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options =>
        {
            options.WithInfo("Test API Title", "2.0.0");
            options.WithDescription("This is a test API description");
            options.WithLicense("MIT", "https://opensource.org/licenses/MIT");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(DefaultDocumentRoute);
        var content = await response.Content.ReadAsStringAsync();

        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out _);

        // Assert
        document.ShouldNotBeNull();
        document.Info.ShouldNotBeNull();
        document.Info.Title.ShouldContain("Test API Title");
        document.Info.Version.ShouldBe("2.0.0");
        document.Info.Description.ShouldBe("This is a test API description");
        document.Info.License.ShouldNotBeNull();
        document.Info.License!.Name.ShouldBe("MIT");
    }

    [Fact]
    public async Task GeneratedDocument_NoParsingErrors()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options =>
        {
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("No Errors Test", "1.0.0");
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(DefaultDocumentRoute);
        var content = await response.Content.ReadAsStringAsync();

        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out var diagnostic);

        // Assert - Check there are no parsing errors
        document.ShouldNotBeNull();
        
        var errorCount = diagnostic?.Errors?.Count() ?? 0;
        if (errorCount > 0)
        {
            var errors = string.Join(Environment.NewLine, 
                diagnostic!.Errors.Select(e => $"- {e.Message}"));
            Assert.Fail($"Document has {errorCount} parsing error(s):{Environment.NewLine}{errors}");
        }
    }

    private static async Task<IHost> CreateTestHostAsync(Action<AsyncApiOptions>? configureOptions = null)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers(); // Required for ApplicationPartManager
                    services.AddAsyncApi(options =>
                    {
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
                    });
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
