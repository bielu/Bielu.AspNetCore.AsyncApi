using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

namespace Saunter.Options.Filters
{
    public class OperationFilterContext
    {
        public OperationFilterContext(MethodInfo method, OperationAttribute operation)
        {
            Method = method;
            Operation = operation;
        }

        public MethodInfo Method { get; }

        public OperationAttribute Operation { get; }
    }
}
