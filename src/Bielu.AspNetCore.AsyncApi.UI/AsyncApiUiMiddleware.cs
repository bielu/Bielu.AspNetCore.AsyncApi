using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bielu.AspNetCore.AsyncApi.UI
{
    internal class AsyncApiUiMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly AsyncApiMiddlewareOptions _middlewareOptions;
        private readonly IServiceProvider _serviceProvider;
        private readonly StaticFileMiddleware _staticFiles;
        private readonly Dictionary<string, StaticFileMiddleware> _namedStaticFiles;

        public AsyncApiUiMiddleware(RequestDelegate next, IServiceProvider serviceProvider, IWebHostEnvironment env, ILoggerFactory loggerFactory, IOptions<AsyncApiMiddlewareOptions> options)
        {
            _next = next;
            _middlewareOptions = options.Value;
            _serviceProvider = serviceProvider;

            var fileProvider = new EmbeddedFileProvider(GetType().Assembly, GetType().Namespace);
            var staticFileOptions = new StaticFileOptions
            {
                RequestPath = _middlewareOptions.UiBaseRoute.TrimEnd('/'),
                FileProvider = fileProvider,
            };

            _staticFiles = new StaticFileMiddleware(
                _ => Task.CompletedTask,
                env,
                Options.Create(staticFileOptions),
                loggerFactory);

            _namedStaticFiles = new Dictionary<string, StaticFileMiddleware>();

            var allAsyncApiOptions = serviceProvider.GetKeyedServices<AsyncApiOptions>(KeyedService.AnyKey);

         

            foreach (var documentName in allAsyncApiOptions)
            {
                var namedStaticFileOptions = new StaticFileOptions
                {
                    RequestPath = documentName.DocumentRoutePattern.Replace("{document}", documentName.DocumentName),
                    FileProvider = fileProvider,
                };

                _namedStaticFiles.Add(documentName.DocumentName, new StaticFileMiddleware(
                    _ => Task.CompletedTask,
                    env,
                    Options.Create(namedStaticFileOptions),
                    loggerFactory));
            }
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var requestPath = context.Request.Path.Value ?? "";
            var baseRoute = _middlewareOptions.UiBaseRoute.TrimEnd('/');

            // Handle base route with trailing slash - redirect to index.html
            if (HttpMethods.IsGet(context.Request.Method) && 
                (requestPath == baseRoute || requestPath == baseRoute + "/"))
            {
                context.Response.StatusCode = (int)HttpStatusCode.MovedPermanently;

                if (TryGetDocument(context, out var document))
                {
                    context.Response.Headers["Location"] = GetUiIndexFullRoute(context.Request).Replace("{document}", document);
                }
                else
                {
                    context.Response.Headers["Location"] = GetUiIndexFullRoute(context.Request);
                }
                return;
            }

            if (IsRequestingAsyncApiUi(context.Request))
            {
                if (TryGetDocument(context, out var document))
                {
                    await RespondWithAsyncApiHtml(context.Response, GetDocumentFullRoute(context.Request).Replace("{document}", document));
                }
                else
                {
                    await RespondWithAsyncApiHtml(context.Response, GetDocumentFullRoute(context.Request));
                }
                return;
            }

            if (!TryGetDocument(context, out var documentName))
            {
                await _staticFiles.Invoke(context);
            }
            else
            {
                if (_namedStaticFiles.TryGetValue(documentName, out var files))
                {
                    await files.Invoke(context);
                }
                else
                {
                    await _staticFiles.Invoke(context);
                }
            }

            await _next(context);
        }

        private async Task RespondWithAsyncApiHtml(HttpResponse response, string route)
        {
            var fileProvider = new EmbeddedFileProvider(typeof(EndpointRouteBuilderExtensions).Assembly, "Bielu.AspNetCore.AsyncApi.UI.wwwroot");
            using var stream =fileProvider.GetFileInfo("index.html").CreateReadStream();

            using var reader = new StreamReader(stream);

            var indexHtml = new StringBuilder(await reader.ReadToEndAsync());

            var allOptions = _serviceProvider.GetKeyedServices<AsyncApiOptions>(null).FirstOrDefault();

            indexHtml.Replace("{{title}}", allOptions?.DocumentName ?? "AsyncAPI UI");
            indexHtml.Replace("{{asyncApiDocumentUrl}}", route);

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = MediaTypeNames.Text.Html;
            await response.WriteAsync(indexHtml.ToString(), Encoding.UTF8);
        }

        private bool IsRequestingUiBase(HttpRequest request)
        {
            return HttpMethods.IsGet(request.Method) && request.Path.Value == _middlewareOptions.UiBaseRoute.TrimEnd('/');
        }

        private bool IsRequestingAsyncApiUi(HttpRequest request)
        {
            return HttpMethods.IsGet(request.Method) && request.Path.Value.EndsWith("index.html");
        }

        private string GetUiIndexFullRoute(HttpRequest request)
        {
            var basePath = _middlewareOptions.UiBaseRoute.TrimEnd('/');
            if (request.PathBase.HasValue)
            {
                return request.PathBase + basePath + "/index.html";
            }
            return basePath + "/index.html";
        }

        private string GetDocumentFullRoute(HttpRequest request)
        {
            if (TryGetDocument(request.HttpContext, out var documentName))
            {
                return _middlewareOptions.UiBaseRoute + documentName;
            }
            return _middlewareOptions.UiBaseRoute;
        }

        private bool TryGetDocument(HttpContext context, out string? documentName)
        {
            documentName = context.Request.RouteValues["document"]?.ToString();
            return !string.IsNullOrWhiteSpace(documentName);
        }
    }
}
