// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ByteBard.AsyncAPI.Models;

namespace Saunter2.Schemas;

[JsonSerializable(typeof(AsyncApiJsonSchema))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(JsonNode))]
internal sealed partial class AsyncApiJsonSchemaContext : JsonSerializerContext { }
