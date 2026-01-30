// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Saunter2.Extensions;
using Saunter2.Transformers;

namespace Saunter2.Services;

/// <summary>
/// Options to support the construction of AsyncApi documents.
/// </summary>
public sealed class AsyncApiOptions
{
    internal readonly List<IAsyncApiDocumentTransformer> DocumentTransformers = [];
    internal readonly List<IAsyncApiOperationTransformer> OperationTransformers = [];
    internal readonly List<IAsyncApiSchemaTransformer> SchemaTransformers = [];

    /// <summary>
    /// A default implementation for creating a schema reference ID for a given <see cref="JsonTypeInfo"/>.
    /// </summary>
    /// <param name="jsonTypeInfo">The <see cref="JsonTypeInfo"/> associated with the schema we are generating a reference ID for.</param>
    /// <returns>The reference ID to use for the schema or <see langword="null"/> if the schema should always be inlined.</returns>
    public static string? CreateDefaultSchemaReferenceId(JsonTypeInfo jsonTypeInfo) => jsonTypeInfo.GetSchemaReferenceId();

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncApiOptions"/> class
    /// with the default <see cref="ShouldInclude"/> predicate.
    /// </summary>
    public AsyncApiOptions()
    {
        ShouldInclude = (description) => description.GroupName == null || string.Equals(description.GroupName, DocumentName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The version of the AsyncApi specification to use. Defaults to <see cref="AsyncApiSpecVersion.AsyncApi3_1"/>.
    /// </summary>
    public AsyncApiVersion AsyncApiVersion { get; set; } = AsyncApiVersion.AsyncApi3_0;

    /// <summary>
    /// The name of the AsyncApi document this <see cref="AsyncApiOptions"/> instance is associated with.
    /// </summary>
    public string DocumentName { get; internal set; } = AsyncApiGeneratorConstants.DefaultDocumentName;

    /// <summary>
    /// A delegate to determine whether a given <see cref="ApiDescription"/> should be included in the given AsyncApi document.
    /// </summary>
    public Func<ApiDescription, bool> ShouldInclude { get; set; }

    /// <summary>
    /// A delegate to determine how reference IDs should be created for schemas associated with types in the given AsyncApi document.
    /// </summary>
    /// <remarks>
    /// The default implementation uses the <see cref="CreateDefaultSchemaReferenceId"/> method to generate reference IDs. When
    /// the provided delegate returns <see langword="null"/>, the schema associated with the <see cref="JsonTypeInfo"/> will always be inlined.
    /// </remarks>
    public Func<JsonTypeInfo, string?> CreateSchemaReferenceId { get; set; } = CreateDefaultSchemaReferenceId;

    /// <summary>
    /// Registers a new document transformer on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <typeparam name="TTransformerType">The type of the <see cref="IAsyncApiDocumentTransformer"/> to instantiate.</typeparam>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddDocumentTransformer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTransformerType>()
        where TTransformerType : IAsyncApiDocumentTransformer
    {
        DocumentTransformers.Add(new TypeBasedAsyncApiDocumentTransformer(typeof(TTransformerType)));
        return this;
    }

    /// <summary>
    /// Registers a given instance of <see cref="IAsyncApiDocumentTransformer"/> on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <param name="transformer">The <see cref="IAsyncApiDocumentTransformer"/> instance to use.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddDocumentTransformer(IAsyncApiDocumentTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        DocumentTransformers.Add(transformer);
        return this;
    }

    /// <summary>
    /// Registers a given delegate as a document transformer on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <param name="transformer">The delegate representing the document transformer.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddDocumentTransformer(Func<AsyncApiDocument, AsyncApiDocumentTransformerContext, CancellationToken, Task> transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        DocumentTransformers.Add(new DelegateAsyncApiDocumentTransformer(transformer));
        return this;
    }

    /// <summary>
    /// Registers a new operation transformer on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <typeparam name="TTransformerType">The type of the <see cref="IAsyncApiOperationTransformer"/> to instantiate.</typeparam>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddOperationTransformer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTransformerType>()
        where TTransformerType : IAsyncApiOperationTransformer
    {
        OperationTransformers.Add(new TypeBasedAsyncApiOperationTransformer(typeof(TTransformerType)));
        return this;
    }

    /// <summary>
    /// Registers a given instance of <see cref="IAsyncApiOperationTransformer"/> on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <param name="transformer">The <see cref="IAsyncApiOperationTransformer"/> instance to use.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddOperationTransformer(IAsyncApiOperationTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        OperationTransformers.Add(transformer);
        return this;
    }

    /// <summary>
    /// Registers a given delegate as an operation transformer on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <param name="transformer">The delegate representing the operation transformer.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddOperationTransformer(Func<AsyncApiOperation, AsyncApiOperationTransformerContext, CancellationToken, Task> transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        OperationTransformers.Add(new DelegateAsyncApiOperationTransformer(transformer));
        return this;
    }

    /// <summary>
    /// Registers a new schema transformer on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <typeparam name="TTransformerType">The type of the <see cref="IAsyncApiSchemaTransformer"/> to instantiate.</typeparam>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddSchemaTransformer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTransformerType>()
        where TTransformerType : IAsyncApiSchemaTransformer
    {
        SchemaTransformers.Add(new TypeBasedAsyncApiSchemaTransformer(typeof(TTransformerType)));
        return this;
    }

    /// <summary>
    /// Registers a given instance of <see cref="IAsyncApiOperationTransformer"/> on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <param name="transformer">The <see cref="IAsyncApiOperationTransformer"/> instance to use.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddSchemaTransformer(IAsyncApiSchemaTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        SchemaTransformers.Add(transformer);
        return this;
    }

    /// <summary>
    /// Registers a given delegate as a schema transformer on the current <see cref="AsyncApiOptions"/> instance.
    /// </summary>
    /// <param name="transformer">The delegate representing the schema transformer.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public AsyncApiOptions AddSchemaTransformer(Func<AsyncApiJsonSchema, AsyncApiJsonSchemaTransformerContext, CancellationToken, Task> transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        SchemaTransformers.Add(new DelegateAsyncApiSchemaTransformer(transformer));
        return this;
    }
}
