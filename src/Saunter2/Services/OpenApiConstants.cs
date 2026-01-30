// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Saunter2.Services;

internal static class AsyncApiGeneratorConstants
{
    internal const string DefaultDocumentName = "v1";
    internal const string DefaultOpenApiVersion = "1.0.0";
    internal const string DefaultOpenApiRoute = "/asyncapi/{documentName}.json";
    internal const string DescriptionId = "x-aspnetcore-id";
    internal const string SchemaId = "x-schema-id";
    internal const string RefDefaultAnnotation = "x-ref-default";
    internal const string RefDescriptionAnnotation = "x-ref-description";
    internal const string RefExampleAnnotation = "x-ref-example";
    internal const string RefPrefix = "#";
    internal const string NullableProperty = "x-is-nullable-property";
    internal const string DefaultOpenApiResponseKey = "default";
    
}
