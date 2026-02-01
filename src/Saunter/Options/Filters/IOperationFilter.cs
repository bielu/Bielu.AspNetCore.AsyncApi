using LEGO.AsyncAPI.Models;

namespace Saunter.Options.Filters;

public interface IOperationFilter
{
    void Apply(AsyncApiOperation operation, OperationFilterContext context);
}