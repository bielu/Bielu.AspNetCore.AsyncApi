using System.IO;
using System.Net;
using System.Threading.Tasks;
using LEGO.AsyncAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Saunter.Options;

namespace Saunter.DocumentMiddleware
{
    internal class AsyncApiMiddleware(
        RequestDelegate next,
        IOptions<AsyncApiOptions> options,
        IAsyncApiDocumentProvider asyncApiDocumentProvider)
    {
        private readonly AsyncApiOptions _options = options.Value;

        public async Task Invoke(HttpContext context)
        {
            if (!IsRequestingAsyncApiSchema(context.Request))
            {
                await next(context);
                return;
            }

            if (context.TryGetDocument(out var documentName) && !_options.NamedApis.TryGetValue(documentName, out _))
            {
                await next(context);
                return;
            }

            var asyncApiSchema = asyncApiDocumentProvider.
                GetDocument(documentName, _options);

            await RespondWithAsyncApiSchemaJson(context.Response, asyncApiSchema);
        }

        private static async Task RespondWithAsyncApiSchemaJson(HttpResponse response, AsyncApiDocument asyncApiSchema)
        {
            var asyncApiSchemaJson = asyncApiSchema.SerializeAsJson(LEGO.AsyncAPI.AsyncApiVersion.AsyncApi2_0);
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json";

            await response.WriteAsync(asyncApiSchemaJson);
        }

        private bool IsRequestingAsyncApiSchema(HttpRequest request)
        {
            return HttpMethods.IsGet(request.Method) && request.Path.IsMatchingRoute(_options.Middleware.Route);
        }
    }
}
