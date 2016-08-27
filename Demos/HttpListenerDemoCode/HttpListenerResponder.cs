using System.Globalization;
using System.Net;
using System.Text;

namespace RavuAlHemio.HttpDispatcher.Demos.HttpListener
{
    [Responder]
    public class HttpListenerResponder
    {
        [Endpoint("/hello-world")]
        public void HelloWorld(HttpListenerContext ctx)
        {
            RespondWithText(ctx, "Hello, world!");
        }

        [Endpoint("/sum/{left}/{right}")]
        public void Sum(HttpListenerContext ctx, long left, long right)
        {
            long sum = left + right;
            RespondWithText(ctx, sum.ToString(CultureInfo.InvariantCulture));
        }

        protected static void RespondWithText(HttpListenerContext ctx, string response)
        {
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            var bytes = Encoding.UTF8.GetBytes(response);
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.Close(bytes, false);
        }
    }
}
