// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Pipelines;
using System.Reflection;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Saunter2.Extensions;
using Saunter2.Services.Schemas;
using Saunter2.Transformers;

namespace Saunter2.Services;

internal sealed class AsyncApiDocumentService(
    [Microsoft.Extensions.DependencyInjection.ServiceKey] string documentName,
    IApiDescriptionGroupCollectionProvider apiDescriptionGroupCollectionProvider,
    IHostEnvironment hostEnvironment,
    IOptionsMonitor<AsyncApiOptions> optionsMonitor,
    IServiceProvider serviceProvider,
    IServer? server = null) : IAsyncApiDocumentProvider
{
    private readonly AsyncApiOptions _options = optionsMonitor.Get(documentName);
    private readonly AsyncApiJsonSchemaService _componentService = serviceProvider.GetRequiredKeyedService<AsyncApiJsonSchemaService>(documentName);

    /// <summary>
    /// Cache of <see cref="AsyncApiOperationTransformerContext"/> instances keyed by the
    /// `ApiDescription.ActionDescriptor.Id` of the associated operation. ActionDescriptor IDs
    /// are unique within the lifetime of an application and serve as helpful associators between
    /// operations, API descriptions, and their respective transformer contexts.
    /// </summary>
    private readonly ConcurrentDictionary<string, AsyncApiOperationTransformerContext> _operationTransformerContextCache = new();
    private static readonly ApiResponseType _defaultApiResponseType = new() { StatusCode = StatusCodes.Status200OK };

    private static readonly FrozenSet<string> _disallowedHeaderParameters = new[] { HeaderNames.Accept, HeaderNames.Authorization, HeaderNames.ContentType }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    internal bool TryGetCachedOperationTransformerContext(string descriptionId, [NotNullWhen(true)] out AsyncApiOperationTransformerContext? context)
        => _operationTransformerContextCache.TryGetValue(descriptionId, out context);

    public async Task<AsyncApiDocument> GetAsyncApiDocumentAsync(IServiceProvider scopedServiceProvider, HttpRequest? httpRequest = null, CancellationToken cancellationToken = default)
    {
        // Schema and operation transformers are scoped per-request and can be
        // pre-allocated to hold the same number of transformers as the associated
        // options object.
        var schemaTransformers = _options.SchemaTransformers.Count > 0
            ? new IAsyncApiSchemaTransformer[_options.SchemaTransformers.Count]
            : [];
        var operationTransformers = _options.OperationTransformers.Count > 0 ?
            new IAsyncApiOperationTransformer[_options.OperationTransformers.Count]
            : [];
        InitializeTransformers(scopedServiceProvider, schemaTransformers, operationTransformers);
        var document = new AsyncApiDocument
        {
            Info = GetAsyncApiInfo(),
         //   Servers = GetAsyncApiServers(httpRequest)
        };
       // document.Paths = await GetAsyncApiPathsAsync(document, scopedServiceProvider, operationTransformers, schemaTransformers, cancellationToken);
        try
        {
            await ApplyTransformersAsync(document, scopedServiceProvider, schemaTransformers, cancellationToken);
        }

        finally
        {
            await FinalizeTransformers(schemaTransformers, operationTransformers);
        }
        // Call register components to support
        // // resolution of references in the document.
        // document.Workspace ??= new();
        // document.Workspace.RegisterComponents(document);
        if (document.Components?.Schemas is not null)
        {
            // Sort schemas by key name for better readability and consistency
            // This works around an API change in AsyncApi.NET
            document.Components.Schemas = new Dictionary<string, AsyncApiMultiFormatSchema>(
                document.Components.Schemas.OrderBy(kvp => kvp.Key),
                StringComparer.Ordinal);
        }
        return document;
    }

    private async Task ApplyTransformersAsync(AsyncApiDocument document, IServiceProvider scopedServiceProvider, IAsyncApiSchemaTransformer[] schemaTransformers, CancellationToken cancellationToken)
    {
        var documentTransformerContext = new AsyncApiDocumentTransformerContext
        {
            DocumentName = documentName,
            ApplicationServices = scopedServiceProvider,
            DescriptionGroups = apiDescriptionGroupCollectionProvider.ApiDescriptionGroups.Items,
            Document = document,
            SchemaTransformers = schemaTransformers
        };
        // Use index-based for loop to avoid allocating an enumerator with a foreach.
        for (var i = 0; i < _options.DocumentTransformers.Count; i++)
        {
            var transformer = _options.DocumentTransformers[i];
            await transformer.TransformAsync(document, documentTransformerContext, cancellationToken);
        }
    }

    internal void InitializeTransformers(IServiceProvider scopedServiceProvider, IAsyncApiSchemaTransformer[] schemaTransformers, IAsyncApiOperationTransformer[] operationTransformers)
    {
        for (var i = 0; i < _options.SchemaTransformers.Count; i++)
        {
            var schemaTransformer = _options.SchemaTransformers[i];
            if (schemaTransformer is TypeBasedAsyncApiSchemaTransformer typeBasedTransformer)
            {
                schemaTransformers[i] = typeBasedTransformer.InitializeTransformer(scopedServiceProvider);
            }
            else
            {
                schemaTransformers[i] = schemaTransformer;
            }
        }

        for (var i = 0; i < _options.OperationTransformers.Count; i++)
        {
            var operationTransformer = _options.OperationTransformers[i];
            if (operationTransformer is TypeBasedAsyncApiOperationTransformer typeBasedTransformer)
            {
                operationTransformers[i] = typeBasedTransformer.InitializeTransformer(scopedServiceProvider);
            }
            else
            {
                operationTransformers[i] = operationTransformer;
            }
        }
    }

    internal static async Task FinalizeTransformers(IAsyncApiSchemaTransformer[] schemaTransformers, IAsyncApiOperationTransformer[] operationTransformers)
    {
        for (var i = 0; i < schemaTransformers.Length; i++)
        {
            await schemaTransformers[i].FinalizeTransformer();
        }
        for (var i = 0; i < operationTransformers.Length; i++)
        {
            await operationTransformers[i].FinalizeTransformer();
        }
    }

  

    // Note: Internal for testing.
    internal AsyncApiInfo GetAsyncApiInfo()
    {
        return new AsyncApiInfo
        {
            Title = $"{hostEnvironment.ApplicationName} | {documentName}",
            Version = AsyncApiGeneratorConstants.DefaultAsyncApiVersion
        };
    }

    // Resolve server URL from the request to handle reverse proxies.
    // If there is active request object, assume a development environment and use the server addresses.
    internal List<AsyncApiServer> GetAsyncApiServers(HttpRequest? httpRequest = null)
    {
        if (httpRequest is not null)
        {
            var serverUrl = UriHelper.BuildAbsolute(httpRequest.Scheme, httpRequest.Host, httpRequest.PathBase);
            // Remove trailing slash when pathBase is empty to align with AsyncApi specification.
            // Keep the trailing slash if pathBase explicitly contains "/" to preserve intentional path structure.
            if (serverUrl.EndsWith('/') && !httpRequest.PathBase.HasValue)
            {
                serverUrl = serverUrl.TrimEnd('/');
            }
            return [new AsyncApiServer { Host = serverUrl }];
        }
        else
        {
            return GetDevelopmentAsyncApiServers();
        }
    }
    private List<AsyncApiServer> GetDevelopmentAsyncApiServers()
    {
        if (hostEnvironment.IsDevelopment() &&
            server?.Features.Get<IServerAddressesFeature>()?.Addresses is { Count: > 0 } addresses)
        {
            return [.. addresses.Select(address => new AsyncApiServer { Host = address })];
        }
        return [];
    }



    private static string? GetSummary(ApiDescription description)
        => description.ActionDescriptor.EndpointMetadata.OfType<IEndpointSummaryMetadata>().LastOrDefault()?.Summary;

    private static string? GetDescription(ApiDescription description)
        => description.ActionDescriptor.EndpointMetadata.OfType<IEndpointDescriptionMetadata>().LastOrDefault()?.Description;

    private static string? GetOperationId(ApiDescription description)
        => description.ActionDescriptor.AttributeRouteInfo?.Name ??
            description.ActionDescriptor.EndpointMetadata.OfType<IEndpointNameMetadata>().LastOrDefault()?.EndpointName;

    // private static HashSet<AsyncApiTag> GetTags(ApiDescription description, AsyncApiDocument document)
    // {
    //     var actionDescriptor = description.ActionDescriptor;
    //     if (actionDescriptor.EndpointMetadata?.OfType<ITagsMetadata>().LastOrDefault() is { } tagsMetadata)
    //     {
    //         HashSet<AsyncApiTagReference> tags = [];
    //         foreach (var tag in tagsMetadata.Tags)
    //         {
    //             document.Tags ??= new HashSet<AsyncApiTag>();
    //             document.Tags.Add(new AsyncApiTag { Name = tag });
    //             tags.Add(new AsyncApiTagReference(tag, document));
    //
    //         }
    //         return tags;
    //     }
    //     // If no tags are specified, use the controller name as the tag. This effectively
    //     // allows us to group endpoints by the "resource" concept (e.g. users, todos, etc.)
    //     var controllerName = description.ActionDescriptor.RouteValues["controller"];
    //     document.Tags ??= new HashSet<AsyncApiTag>();
    //     document.Tags.Add(new AsyncApiTag { Name = controllerName });
    //     return controllerName is not null ? [new(controllerName, document)] : [];
    // }

   

    private static bool IsRequired(ApiParameterDescription parameter)
    {
        var hasRequiredAttribute = parameter.ParameterDescriptor is IParameterInfoParameterDescriptor parameterInfoDescriptor &&
            parameterInfoDescriptor.ParameterInfo.GetCustomAttributes(inherit: true).Any(attr => attr is RequiredAttribute);
        // Per the AsyncApi specification, parameters that are sourced from the path
        // are always required, regardless of the requiredness status of the parameter.
        return parameter.Source == BindingSource.Path || parameter.IsRequired || hasRequiredAttribute;
    }

    // Apply [Description] attributes on the parameter to the top-level AsyncApiParameter object and not the schema.
    private static string? GetParameterDescriptionFromAttribute(ApiParameterDescription parameter)
    {
        if (parameter.ParameterDescriptor is IParameterInfoParameterDescriptor { ParameterInfo: { } parameterInfo } &&
            parameterInfo.GetCustomAttributes<DescriptionAttribute>().LastOrDefault() is { } parameterDescription)
        {
            return parameterDescription.Description;
        }

        if (parameter.ModelMetadata is DefaultModelMetadata { Attributes.PropertyAttributes.Count: > 0 } metadata &&
            metadata.Attributes.PropertyAttributes.OfType<DescriptionAttribute>().LastOrDefault() is { } propertyDescription)
        {
            return propertyDescription.Description;
        }

        return null;
    }

   

    /// <remarks>
    /// This method is used to determine the target type for a given parameter. The target type
    /// is the actual type that should be used to generate the schema for the parameter. This is
    /// necessary because MVC's ModelMetadata layer will set ApiParameterDescription.Type to string
    /// when the parameter is a parsable or convertible type. In this case, we want to use the actual
    /// model type to generate the schema instead of the string type.
    /// </remarks>
    /// <remarks>
    /// This method will also check if no target type was resolved from the <see cref="ApiParameterDescription"/>
    /// and default to a string schema. This will happen if we are dealing with an inert route parameter
    /// that does not define a specific parameter type in the route handler or in the response.
    /// </remarks>
    private static Type GetTargetType(ApiDescription description, ApiParameterDescription parameter)
    {
        var bindingMetadata = description.ActionDescriptor.EndpointMetadata
            .OfType<IParameterBindingMetadata>()
            .SingleOrDefault(metadata => metadata.Name == parameter.Name);
        var parameterType = parameter.Type is not null
            ? Nullable.GetUnderlyingType(parameter.Type) ?? parameter.Type
            : parameter.Type;

        // parameter.Type = typeof(string)
        // parameter.ModelMetadata.Type = typeof(TEnum)
        var requiresModelMetadataFallbackForEnum = parameterType == typeof(string)
            && parameter.ModelMetadata.ModelType != parameter.Type
            && parameter.ModelMetadata.ModelType.IsEnum;
        // Enums are exempt because we want to set the IAsyncApiSchema.Enum field when feasible.
        // parameter.Type = typeof(TEnum), typeof(TypeWithTryParse)
        // parameter.ModelMetadata.Type = typeof(string)
        var hasTryParse = bindingMetadata?.HasTryParse == true && parameterType is not null && !parameterType.IsEnum;
        var targetType = requiresModelMetadataFallbackForEnum || hasTryParse
            ? parameter.ModelMetadata.ModelType
            : parameter.Type;
        targetType ??= typeof(string);
        return targetType;
    }

    /// <inheritdoc />
    public Task<AsyncApiDocument> GetAsyncApiDocumentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return GetAsyncApiDocumentAsync(serviceProvider, httpRequest: null, cancellationToken);
    }
}
