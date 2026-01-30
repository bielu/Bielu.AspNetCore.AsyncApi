// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using Saunter2.Services;

namespace Saunter2.Extensions;

internal static class AsyncApiDocumentExtensions
{
    /// <summary>
    /// Registers a <see cref="IAsyncApiSchema" /> into the top-level components store on the
    /// <see cref="AsyncApiDocument" /> and returns a resolvable reference to it.
    /// </summary>
    /// <param name="document">The <see cref="AsyncApiDocument"/> to register the schema onto.</param>
    /// <param name="schemaId">The ID that serves as the key for the schema in the schema store.</param>
    /// <param name="schema">The <see cref="IAsyncApiSchema" /> to register into the document.</param>
    /// <param name="schemaReference">An <see cref="IAsyncApiSchema"/> with a reference to the stored schema.</param>
    /// <returns>Whether the schema was added or already existed.</returns>
    public static bool AddAsyncApiJsonSchemaByReference(this AsyncApiDocument document, string schemaId, AsyncApiMultiFormatSchema schema, out AsyncApiJsonSchemaReference schemaReference)
    {
        // Make sure the document has a workspace,
        // AddComponent will add it to the workspace when adding the component.
        document.Workspace ??= new();
        // AddComponent will only add the schema if it doesn't already exist.
        if (!document.Components.Schemas.ContainsKey(schemaId))
        {
            document.Components.Schemas.Add(schemaId,schema);
        }
        object? description = null;
        object? example = null;
        object? defaultAnnotation = null;
        if (schema is AsyncApiJsonSchema { Metadata: not null } actualSchema)
        {
            actualSchema.Metadata.TryGetValue(AsyncApiConstants.DollarRef, out description);
            actualSchema.Metadata.TryGetValue(AsyncApiConstants.RefExampleAnnotation, out example);
            actualSchema.Metadata.TryGetValue(AsyncApiConstants.RefDefaultAnnotation, out defaultAnnotation);
        }

        schemaReference = new AsyncApiJsonSchemaReference(schemaId, document)
        {
            Description = description as string,
            Examples = example is JsonNode exampleJson ? [exampleJson] : null,
            Default = defaultAnnotation as JsonNode,
        };

        return schemaAdded;
    }
}
