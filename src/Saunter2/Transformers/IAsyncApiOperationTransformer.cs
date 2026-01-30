// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI.Models;

namespace Saunter2.Transformers;

/// <summary>
/// Represents a transformer that can be used to modify an AsyncApi operation.
/// </summary>
public interface IAsyncApiOperationTransformer
{
    /// <summary>
    /// Transforms the specified AsyncApi operation.
    /// </summary>
    /// <param name="operation">The <see cref="context"/> to modify.</param>
    /// <param name="context">The <see cref="cancellationToken"/> associated with the <see paramref="operation"/>.</param>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task TransformAsync(AsyncApiOperation operation, AsyncApiOperationTransformerContext context, CancellationToken cancellationToken);
}
