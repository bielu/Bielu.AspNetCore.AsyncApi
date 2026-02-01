using System;

namespace Saunter.SharedKernel.Interfaces
{
    public interface IAsyncApiSchemaGenerator
    {
        GeneratedSchemas? Generate(Type? type);
    }
}
