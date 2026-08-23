---
"bielu-aspnetcore-asyncapi": patch
---

Fix `AsyncApiJsonSchemaService` throwing when a documented message type has a property that produces the JSON Schema `"enum"` keyword — most commonly an enum serialized with `JsonStringEnumConverter`. `System.Text.Json.Schema.JsonSchemaExporter` emits that keyword as a plain array of values, but the generated schema's `Enum` property is typed `IList<AsyncApiAny>`, and no `JsonConverter` was registered for `AsyncApiAny` anywhere in the schema-deserialization pipeline used by `AsyncApiJsonSchemaService`, so `JsonSerializer.Deserialize` threw `JsonException` the moment that keyword appeared — for any consumer whose documented AsyncAPI message contained an enum property. Adds `AsyncApiAnyJsonConverter` and registers it on the `JsonSerializerOptions` used to build `AsyncApiJsonSchemaContext`.
