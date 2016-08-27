using System;
using System.Net;
using System.Text;
using System.Threading;
using RavuAlHemio.HttpDispatcher.Generic;

namespace RavuAlHemio.HttpDispatcher
{
    /// <summary>
    /// An HTTP listener that forwards requests to the registered responders' endpoint-handling methods.
    /// </summary>
    public class DistributingHttpListener : GenericDistributingHttpServer<HttpListenerContext>
    {
        protected readonly HttpListener Listener;
        protected readonly Thread AcceptorThread;

        private bool _disposed;

        /// <summary>
        /// Initializes a distributing HTTP listener on the given URI prefix.
        /// </summary>
        /// <param name="uriPrefix">URI prefix.</param>
        public DistributingHttpListener(string uriPrefix)
            : base(uriPrefix)
        {
            Listener = new HttpListener();
            Listener.Prefixes.Add(uriPrefix);
            AcceptorThread = new Thread(Proc) { Name = "DistributingHttpListener acceptor" };
            _disposed = false;
        }

        /// <summary>
        /// Starts listening to HTTP requests.
        /// </summary>
        public override void Start()
        {
            base.Start();
            Listener.Start();
            AcceptorThread.Start();
        }

        /// <summary>
        /// Stops listening to HTTP requests.
        /// </summary>
        public override void Stop()
        {
            Listener.Stop();
            base.Stop();
            AcceptorThread.Join();
        }

        /// <summary>
        /// Handles requests.
        /// </summary>
        protected void Proc()
        {
            var cancelToken = CancellerSource.Token;
            while (!cancelToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = Listener.GetContext();
                }
                catch (HttpListenerException ex)
                {
                    if (cancelToken.IsCancellationRequested && ex.ErrorCode == HttpDispatcherUtil.ErrorOperationAborted)
                    {
                        // we expect this to happen
                        break;
                    }
                    throw;
                }

                ThreadPool.QueueUserWorkItem(c =>
                {
                    var ctx = (HttpListenerContext) c;
                    using (ctx.Response)
                    {
                        try
                        {
                            HandleRequest(ctx);
                        }
                        catch (Exception ex)
                        {
                            var deea = new DistributionExceptionEventArgs<HttpListenerContext>(context, ex);
                            OnDistributionException(deea);
                            if (deea.Responded)
                            {
                                return;
                            }
                            SendJson500Exception(context, ex);
                        }
                    }
                }, context);
            }
        }

        protected override Uri RequestUriFromContext(HttpListenerContext context)
        {
            var rawUrl = new StringBuilder(context.Request.RawUrl);

            // strip off multiple initial slashes
            while (rawUrl.Length > 1 && rawUrl[0] == '/' && rawUrl[1] == '/')
            {
                rawUrl.Remove(0, 1);
            }

            return new Uri(string.Format(
                "{0}://{1}{2}",
                context.Request.IsSecureConnection ? "https" : "http",
                context.Request.UserHostName,
                rawUrl
            ));
        }

        protected override string RequestHttpMethodFromContext(HttpListenerContext context)
        {
            return context.Request.HttpMethod;
        }

        /// <summary>
        /// Sends a JSON 404 response. Default unhandled request handler.
        /// </summary>
        /// <param name="context">The context to respond with.</param>
        protected override void SendJson404(HttpListenerContext context)
        {
            try
            {
                context.Response.Headers[HttpResponseHeader.ContentType] = "application/json";
                context.Response.StatusCode = 404;
                const string jsonString = "{\"status\":\"error\",\"error\":\"not found\"}";
                var jsonBytes = HttpDispatcherUtil.Utf8NoBom.GetBytes(jsonString);
                context.Response.ContentLength64 = jsonBytes.LongLength;
                context.Response.Close(jsonBytes, true);
            }
            catch (Exception)
            {
                // there's nothing we can do...
            }
        }

        /// <summary>
        /// Sends a JSON 500 response. Default exception handler.
        /// </summary>
        /// <param name="context">The context to respond with.</param>
        /// <param name="exception">The exception that was thrown.</param>
        protected override void SendJson500Exception(HttpListenerContext context, Exception exception)
        {
            try
            {
                const string jsonString = "{\"status\":\"error\",\"error\":\"exception thrown\",\"errorType\":\"EXCEPTION\"}";
                var jsonBytes = HttpDispatcherUtil.Utf8NoBom.GetBytes(jsonString);
                context.Response.StatusCode = 500;
                context.Response.StatusDescription = "Internal Server Error";
                context.Response.ContentType = "text/plain";
                context.Response.ContentLength64 = jsonBytes.LongLength;
                context.Response.Close(jsonBytes, true);
            }
            catch (Exception)
            {
                // there's nothing we can do...
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Listener.Close();
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
