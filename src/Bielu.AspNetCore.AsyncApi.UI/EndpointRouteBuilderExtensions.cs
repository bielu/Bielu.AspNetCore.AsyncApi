using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapAsyncApiUI(
        this IEndpointRouteBuilder endpoints,
        string path = "/async-api")
    {
        var basePath = path.TrimEnd('/');
        var fileProvider = new EmbeddedFileProvider(
            typeof(EndpointRouteBuilderExtensions).Assembly,
            "Bielu.AspNetCore.AsyncApi.UI.assets");

        // Serve static files (JS, CSS, etc.)
        endpoints.MapGet($"{basePath}/{{**route}}", HandleStaticFiles)
            .ExcludeFromDescription();

        // Serve index.html for root path
        endpoints.MapGet(basePath, HandleAsyncApiUI)
            .ExcludeFromDescription();

        return endpoints;

        async Task HandleAsyncApiUI(HttpContext context)
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(await GetHtmlContent(context));
        }

        async Task<string> GetHtmlContent(HttpContext context)
        {
            var fileInfo = fileProvider.GetFileInfo("index.html");

            if (!fileInfo.Exists)
                throw new FileNotFoundException("index.html not found");

            using var stream = fileInfo.CreateReadStream();
            using var reader = new StreamReader(stream);
            var htmlContent = await reader.ReadToEndAsync();

            // Extract document name from route values
            var documentName = context.Request.RouteValues["document"]?.ToString() ?? "v1";
            var asyncApiDocumentUrl = $"{context.Request.PathBase}/asyncapi/{documentName}.json";
    
            // Replace template variables in HTML
            htmlContent = htmlContent.Replace("{asyncApiDocumentUrl}", asyncApiDocumentUrl);
            var scheme = context.Request.Scheme;
            var host = context.Request.Host;
            var asyncApiUiUrl = $"{scheme}://{host}{context.Request.PathBase}{basePath}";

            htmlContent = htmlContent.Replace("{asyncApiUiUrl}", asyncApiUiUrl);
            htmlContent = htmlContent.Replace("{title}",$"{documentName} AsyncAPI Documentation");

            return htmlContent;
        }

        async Task HandleStaticFiles(HttpContext context, string route)
        {
            var fileInfo = fileProvider.GetFileInfo(route);

            if (!fileInfo.Exists || fileInfo.IsDirectory)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = GetContentType(route);
            await using var stream = fileInfo.CreateReadStream();
            await stream.CopyToAsync(context.Response.Body);
        }
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
