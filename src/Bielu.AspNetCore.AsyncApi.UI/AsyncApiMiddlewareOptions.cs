namespace Bielu.AspNetCore.AsyncApi.UI
{
    public class AsyncApiMiddlewareOptions
    {
        /// <summary>
        /// The base URL for the AsyncAPI UI
        /// </summary>
        public string UiBaseRoute { get; set; } = "/asyncapi/ui/";

        /// <summary>
        /// The title of page for AsyncAPI UI
        /// </summary>
        public string UiTitle { get; set; } = "AsyncAPI";
    }
}
