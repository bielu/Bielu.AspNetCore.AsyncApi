// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Bielu.AspNetCore.AsyncApi.UI;

public static class EndpointRouteBuilderExtensions
{
    
    public static IApplicationBuilder UseAsyncApiUi(this IApplicationBuilder app, Action<AsyncApiMiddlewareOptions>? configureOptions = null)
    {
        var middlewareOptions = new AsyncApiMiddlewareOptions();
        configureOptions?.Invoke(middlewareOptions);
        app.UseStaticFiles();
        app.MapAsyncApiUI(middlewareOptions);
        return app.UseMiddleware<AsyncApiUiMiddleware>(Options.Create(middlewareOptions));
    }
    public static IApplicationBuilder MapAsyncApiUI(
        this IApplicationBuilder app,
        AsyncApiMiddlewareOptions options)
    {
        var basePath = options.UiBaseRoute.TrimEnd('/');

        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? string.Empty;

            // Only handle requests within the AsyncApi UI path
            if (!path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            // Exclude index.html from being served here
            if (path.EndsWith("index.html", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            // Serve static files (JS, CSS, etc.)
            var fileProvider = new EmbeddedFileProvider(typeof(EndpointRouteBuilderExtensions).Assembly, "Bielu.AspNetCore.AsyncApi.UI.wwwroot");
            var relativePath = path.Substring(basePath.Length).TrimStart('/');
            var fileInfo = fileProvider.GetFileInfo(relativePath);

            if (fileInfo.Exists && !fileInfo.IsDirectory)
            {
                context.Response.ContentType = GetContentType(relativePath);
                await using var stream = fileInfo.CreateReadStream();
                await stream.CopyToAsync(context.Response.Body);
                return;
            }

            await next();
        });

        return app;
    }

    private static string GetContentType(string path)
    {
        return path switch
        {
            _ when path.EndsWith(".js") => "application/javascript",
            _ when path.EndsWith(".css") => "text/css",
            _ when path.EndsWith(".json") => "application/json",
            _ => "application/octet-stream"
        };
    }
}
