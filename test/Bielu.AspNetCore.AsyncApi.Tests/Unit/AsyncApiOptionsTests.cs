using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit;

/// <summary>
/// Unit tests for AsyncApiOptions configuration methods.
/// </summary>
public class AsyncApiOptionsTests
{
    [Fact]
    public void AddServer_ReturnsOptionsForChaining()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act
        var result = options.AddServer("mqtt-broker", "mqtt.example.com", "mqtt");

        // Assert
        result.ShouldBe(options);
    }

    [Fact]
    public void AddServer_WithConfigure_AllowsCustomization()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act
        var result = options.AddServer("amqp-broker", "rabbitmq.example.com", "amqp", server =>
        {
            server.Description = "RabbitMQ Production Server";
        });

        // Assert
        result.ShouldBe(options);
    }

    [Fact]
    public void AddServer_ThrowsForNullName()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => options.AddServer(null!, "localhost", "mqtt"));
    }

    [Fact]
    public void AddServer_ThrowsForNullUrl()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => options.AddServer("server", null!, "mqtt"));
    }

    [Fact]
    public void AddServer_ThrowsForNullProtocol()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => options.AddServer("server", "localhost", null!));
    }

    [Fact]
    public void WithDefaultContentType_SetsContentType()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act
        options.WithDefaultContentType("application/json");

        // Assert
        options.DefaultContentType.ShouldBe("application/json");
    }

    [Fact]
    public void WithDefaultContentType_ThrowsForNull()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => options.WithDefaultContentType(null!));
    }

    [Fact]
    public void WithInfo_SetsInfoProperties()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act
        options.WithInfo("My API", "1.0.0");

        // Assert
        options.Info.ShouldNotBeNull();
        options.Info!.Title.ShouldBe("My API");
        options.Info.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public void WithInfo_WithDelegate_AllowsCustomization()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act
        options.WithInfo(info =>
        {
            info.Title = "Custom API";
            info.Version = "2.0.0";
            info.Description = "A custom API description";
        });

        // Assert
        options.Info.ShouldNotBeNull();
        options.Info!.Title.ShouldBe("Custom API");
        options.Info.Version.ShouldBe("2.0.0");
        options.Info.Description.ShouldBe("A custom API description");
    }

    [Fact]
    public void WithDescription_SetsInfoDescription()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act
        options.WithDescription("API Description");

        // Assert
        options.Info.ShouldNotBeNull();
        options.Info!.Description.ShouldBe("API Description");
    }

    [Fact]
    public void WithLicense_SetsLicenseInfo()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act
        options.WithLicense("MIT", "https://opensource.org/licenses/MIT");

        // Assert
        options.Info.ShouldNotBeNull();
        options.Info!.License.ShouldNotBeNull();
        options.Info.License!.Name.ShouldBe("MIT");
        options.Info.License.Url!.ToString().ShouldBe("https://opensource.org/licenses/MIT");
    }

    [Fact]
    public void WithLicense_WithoutUrl_SetsOnlyName()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act
        options.WithLicense("Apache-2.0");

        // Assert
        options.Info!.License!.Name.ShouldBe("Apache-2.0");
        options.Info.License.Url.ShouldBeNull();
    }

    [Fact]
    public void AddChannelBinding_AddsBindingToCollection()
    {
        // Arrange
        var options = new AsyncApiOptions();
        var binding = new ByteBard.AsyncAPI.Bindings.AMQP.AMQPChannelBinding
        {
            Is = ByteBard.AsyncAPI.Bindings.AMQP.ChannelType.Queue
        };

        // Act
        options.AddChannelBinding("my-channel", binding);

        // Assert
        options.ChannelBindings.ShouldContainKey("my-channel");
        options.ChannelBindings["my-channel"].Count.ShouldBe(1);
    }

    [Fact]
    public void AddChannelBinding_AddsMultipleBindingsToSameChannel()
    {
        // Arrange
        var options = new AsyncApiOptions();
        var binding1 = new ByteBard.AsyncAPI.Bindings.AMQP.AMQPChannelBinding
        {
            Is = ByteBard.AsyncAPI.Bindings.AMQP.ChannelType.Queue
        };
        var binding2 = new ByteBard.AsyncAPI.Bindings.AMQP.AMQPChannelBinding
        {
            Is = ByteBard.AsyncAPI.Bindings.AMQP.ChannelType.RoutingKey
        };

        // Act
        options.AddChannelBinding("my-channel", binding1);
        options.AddChannelBinding("my-channel", binding2);

        // Assert
        options.ChannelBindings["my-channel"].Count.ShouldBe(2);
    }

    [Fact]
    public void AddOperationBinding_AddsBindingToCollection()
    {
        // Arrange
        var options = new AsyncApiOptions();
        var binding = new ByteBard.AsyncAPI.Bindings.Http.HttpOperationBinding
        {
            Method = "POST"
        };

        // Act
        options.AddOperationBinding("my-operation", binding);

        // Assert
        options.OperationBindings.ShouldContainKey("my-operation");
        options.OperationBindings["my-operation"].Count.ShouldBe(1);
    }

    [Fact]
    public void MethodChaining_AllowsFluentConfiguration()
    {
        // Arrange & Act
        var options = new AsyncApiOptions()
            .AddServer("server1", "localhost:5000", "http")
            .AddServer("server2", "localhost:5001", "ws")
            .WithDefaultContentType("application/json")
            .WithInfo("My API", "1.0.0")
            .WithDescription("API Description")
            .WithLicense("MIT");

        // Assert
        options.DefaultContentType.ShouldBe("application/json");
        options.Info!.Title.ShouldBe("My API");
        options.Info.Description.ShouldBe("API Description");
        options.Info.License!.Name.ShouldBe("MIT");
    }

    [Fact]
    public void AsyncApiVersion_DefaultsToAsyncApi3_0()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Assert
        options.AsyncApiVersion.ShouldBe(ByteBard.AsyncAPI.AsyncApiVersion.AsyncApi3_0);
    }

    [Fact]
    public void ShouldInclude_DefaultPredicateExists()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Assert - ShouldInclude delegate should be set by default
        options.ShouldInclude.ShouldNotBeNull();
    }
}
