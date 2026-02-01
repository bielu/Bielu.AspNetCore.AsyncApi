using System;
using System.Collections.Generic;

namespace Saunter.Options.Filters;

public class DocumentFilterContext
{
    public DocumentFilterContext(IEnumerable<Type> asyncApiTypes)
    {
        AsyncApiTypes = asyncApiTypes;
    }

    public IEnumerable<Type> AsyncApiTypes { get; }
}