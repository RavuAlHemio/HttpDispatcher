using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using RavuAlHemio.HttpDispatcher.Generic;

namespace RavuAlHemio.HttpDispatcher.Kestrel
{
    public class DistributingKestrelServer : GenericDistributingHttpServer<HttpContext>
    {
        protected readonly IWebHost WebHost;

        private bool _disposed;

        public DistributingKestrelServer(int port, IPAddress address = null, X509Certificate2 cert = null,
            Action<KestrelServerOptions> additionalKestrelConfigAction = null)
            : base()
        {
            if (address == null)
            {
                address = IPAddress.Any;
            }

            WebHost = new WebHostBuilder()
                .UseKestrel(opts =>
                {
                    opts.Listen(address, port, listenOpts =>
                    {
                        if (cert != null)
                        {
                            listenOpts.UseHttps(cert);
                        }
                    });

                    // don't limit maximum request body size
                    opts.Limits.MaxRequestBodySize = null;

                    if (additionalKestrelConfigAction != null)
                    {
                        additionalKestrelConfigAction.Invoke(opts);
                    }
                })
                .Configure(app =>
                {
                    app.Use(async (ctx, next) =>
                    {
                        await Task.Run(() => HandleRequest(ctx));
                        await next();
                    });
                })
                .Build();
        }

        public override void Start()
        {
            base.Start();
            WebHost.Start();
        }

        public override void Stop()
        {
            base.Stop();
            WebHost.Dispose();
        }

        protected override string RequestHttpMethodFromContext(HttpContext context)
        {
            return context.Request.Method;
        }

        protected override Uri RequestUriFromContext(HttpContext context)
        {
            return new Uri(context.Request.GetEncodedUrl());
        }

        protected override void SendJson404(HttpContext context)
        {
            try
            {
                context.Response.Headers["Content-Type"] = "application/json";
                context.Response.StatusCode = 404;
                const string jsonString = "{\"status\":\"error\",\"error\":\"not found\"}";
                var jsonBytes = HttpDispatcherUtil.Utf8NoBom.GetBytes(jsonString);
                context.Response.ContentLength = jsonBytes.Length;
                context.Response.Body.Write(jsonBytes, 0, jsonBytes.Length);
            }
            catch (Exception)
            {
                // there's nothing we can do...
            }
        }

        protected override void SendJson500Exception(HttpContext context, Exception exc)
        {
            try
            {
                const string jsonString = "{\"status\":\"error\",\"error\":\"exception thrown\",\"errorType\":\"EXCEPTION\"}";
                var jsonBytes = HttpDispatcherUtil.Utf8NoBom.GetBytes(jsonString);
                context.Response.StatusCode = 500;
                context.Response.ContentType = "text/plain";
                context.Response.ContentLength = jsonBytes.Length;
                context.Response.Body.Write(jsonBytes, 0, jsonBytes.Length);
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
                WebHost.Dispose();
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
