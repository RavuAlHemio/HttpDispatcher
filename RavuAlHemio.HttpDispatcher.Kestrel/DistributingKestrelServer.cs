﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using RavuAlHemio.HttpDispatcher.Generic;

namespace RavuAlHemio.HttpDispatcher.Kestrel
{
    public class DistributingKestrelServer : GenericDistributingHttpServer<HttpContext>
    {
        protected readonly IWebHost WebHost;

        public DistributingKestrelServer(string uriPrefix)
            : base(uriPrefix)
        {
            WebHost = new WebHostBuilder()
                .UseKestrel()
                .UseUrls(uriPrefix)
                .Configure(app =>
                {
                    app.Use(async (ctx, next) =>
                    {
                        await Task.Run(() => HandleRequest(ctx));
                        await next();
                    });
                })
                .Build();

            WebHost.Start();
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
    }
}