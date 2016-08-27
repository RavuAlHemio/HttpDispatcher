using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace RavuAlHemio.HttpDispatcher.Demos.Kestrel
{
    [Responder]
    public class KestrelResponder
    {
        [Endpoint("/hello-world")]
        public void HelloWorld(HttpContext ctx)
        {
            RespondWithText(ctx, "Hello, world!");
        }

        [Endpoint("/sum/{left}/{right}")]
        public void Sum(HttpContext ctx, long left, long right)
        {
            long sum = left + right;
            RespondWithText(ctx, sum.ToString(CultureInfo.InvariantCulture));
        }

        protected static void RespondWithText(HttpContext ctx, string response)
        {
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            var bytes = Encoding.UTF8.GetBytes(response);
            ctx.Response.ContentLength = bytes.Length;
            ctx.Response.Body.Write(bytes, 0, bytes.Length);
        }
    }
}
