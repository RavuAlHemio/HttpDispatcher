using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;

namespace RavuAlHemio.HttpDispatcher
{
    /// <summary>
    /// An HTTP listener that forwards requests to the registered responders' endpoint-handling methods.
    /// </summary>
    public class DistributingHttpListener
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly HashSet<char> SimpleAlphanumeric = new HashSet<char>("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789");

        private volatile bool _stopNow;
        private readonly Thread _acceptorThread;
        protected readonly HttpListener Listener;
        protected readonly List<object> Responders;
        protected readonly Dictionary<string, Regex> RegexCache;

        /// <summary>
        /// Gets or sets the request handler which is called if no responder's endpoint
        /// matches the request's path.
        /// </summary>
        /// <remarks>The default handler is <see cref="SendJson404"/>.</remarks>
        /// <value>The unhandled request handler.</value>
        public Action<HttpListenerContext> UnhandledRequestHandler { get; set; }

        /// <summary>
        /// Gets or sets the request handler which is called if a responder throws an
        /// exception.
        /// </summary>
        /// <remarks>The default handler is <see cref="SendJson500Exception"/>.</remarks>
        /// <value>The exception handler.</value>
        public Action<HttpListenerContext, Exception> ExceptionHandler { get; set; }

        /// <summary>
        /// Initializes a distributing HTTP listener on the given URI prefix.
        /// </summary>
        /// <param name="uriPrefix">URI prefix.</param>
        public DistributingHttpListener(string uriPrefix)
        {
            Responders = new List<object>();
            _acceptorThread = new Thread(Proc) {Name = "DistributingHttpListener acceptor"};
            RegexCache = new Dictionary<string, Regex>();

            Listener = new HttpListener();
            Listener.Prefixes.Add(uriPrefix);

            UnhandledRequestHandler = SendJson404;
            ExceptionHandler = SendJson500Exception;
        }

        /// <summary>
        /// Adds a new responder to the responder chain.
        /// </summary>
        /// <param name="newResponder">The responder to add.</param>
        public void AddResponder(object newResponder)
        {
            if (newResponder == null)
            {
                throw new ArgumentNullException("newResponder");
            }

            if (Responders.Contains(newResponder))
            {
                return;
            }

            if (!newResponder.GetType().GetCustomAttributes(typeof (ResponderAttribute), true).Any())
            {
                throw new ArgumentException("the added responder does not have a ResponderAttribute", "newResponder");
            }

            Responders.Add(newResponder);
        }

        /// <summary>
        /// Removes the responder from the responder chain.
        /// </summary>
        /// <returns><c>true</c> if the responder has been removed, <c>false</c> if it was not contained
        /// in the responder chain in the first place.</returns>
        /// <param name="responder">The responder to remove.</param>
        public bool RemoveResponder(object responder)
        {
            if (responder == null)
            {
                throw new ArgumentNullException("responder");
            }

            return Responders.Remove(responder);
        }

        /// <summary>
        /// Starts listening to HTTP requests.
        /// </summary>
        public void Start()
        {
            _stopNow = false;
            Listener.Start();
            _acceptorThread.Start();
        }

        /// <summary>
        /// Stops listening to HTTP requests.
        /// </summary>
        public void Stop()
        {
            _stopNow = true;
            Listener.Stop();
            _acceptorThread.Join();
        }

        /// <summary>
        /// Returns whether the string is a placeholder in a path string.
        /// </summary>
        /// <returns><c>true</c> if the string is a placeholder; otherwise, <c>false</c>.</returns>
        /// <param name="s">The string.</param>
        public static bool IsPlaceholder(string s)
        {
            if (s.Length < 3)
            {
                return false;
            }
            if (s[0] != '{' || s[s.Length - 1] != '}')
            {
                return false;
            }
            if (!s.Substring(1, s.Length - 2).All(c => SimpleAlphanumeric.Contains(c)))
            {
                return false;
            }
            return true;
        }

        protected virtual void HandleRequest(HttpListenerContext context)
        {
            var httpMethod = context.Request.HttpMethod;
            var path = context.Request.Url.AbsolutePath;

            // find a responder
            foreach (var responder in Responders)
            {
                // find a public, non-static method that would respond to this request
                foreach (var method in responder.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
                {
                    // for each endpoint attribute this method has
                    foreach (var endpoint in method.GetCustomAttributes(typeof (EndpointAttribute), true).Select(a => (EndpointAttribute)a))
                    {
                        if (endpoint.Method != null && endpoint.Method != httpMethod)
                        {
                            // endpoint's HTTP method doesn't match request's HTTP method
                            continue;
                        }

                        // construct a regular expression pattern to match the path of the endpoint
                        var groupNames = new HashSet<string>();
                        var newBits = endpoint.Path.Split('/').Select(p =>
                        {
                            if (IsPlaceholder(p))
                            {
                                var placeholderText = p.Substring(1, p.Length - 2);
                                if (groupNames.Contains(placeholderText))
                                {
                                    throw new ArgumentException(string.Format("multiple placeholders named '{0}' in path endpoint", placeholderText));
                                }
                                groupNames.Add(placeholderText);
                                return string.Format("(?<{0}>[^/]+)", placeholderText);
                            }
                            else
                            {
                                return Regex.Escape(p);
                            }
                        });

                        var regexString = string.Join("[/]", newBits);
                        var regex = RegexCache.ContainsKey(regexString)
                            ? RegexCache[regexString]
                            : (RegexCache[regexString] = new Regex(regexString));

                        var match = regex.Match(path);
                        if (!match.Success)
                        {
                            // the path handled by this endpoint doesn't match the request's path
                            continue;
                        }

                        var argumentStrings = new Dictionary<string, string>();
                        foreach (var groupName in groupNames)
                        {
                            argumentStrings[groupName] = match.Groups[groupName].Value;
                        }

                        // try matching the arguments
                        var argValues = new List<object>();
                        var parameters = method.GetParameters();
                        if (parameters.Length < 1)
                        {
                            // needs at least one parameter (context)
                            continue;
                        }
                        if (parameters[0].ParameterType != typeof (HttpListenerContext))
                        {
                            // first parameter isn't the context
                            throw new ArgumentException(string.Format("handler method {0}'s first argument is not an HttpListenerContext", method.Name));
                        }

                        // add the context to the beginning
                        argValues.Add(context);
                        
                        var failed = false;
                        foreach (var argument in method.GetParameters().Skip(1))
                        {
                            if (!groupNames.Contains(argument.Name))
                            {
                                if (argument.HasDefaultValue)
                                {
                                    // this argument isn't specified in the endpoint attribute; give it the default value
                                    argValues.Add(argument.DefaultValue);
                                    continue;
                                }
                                throw new ArgumentException(string.Format("argument '{0}' is unmatched and has no default value", argument.Name));
                            }

                            var str = Util.UrlDecodeUtf8(argumentStrings[argument.Name]);
                            if (argument.ParameterType == typeof (string))
                            {
                                argValues.Add(str);
                            }
                            else if (argument.ParameterType == typeof (int))
                            {
                                var value = Util.ParseIntOrNull(str);
                                if (!value.HasValue)
                                {
                                    failed = true;
                                    break;
                                }
                                argValues.Add(value.Value);
                            }
                            else if (argument.ParameterType == typeof (long))
                            {
                                var value = Util.ParseLongOrNull(str);
                                if (!value.HasValue)
                                {
                                    failed = true;
                                    break;
                                }
                                argValues.Add(value.Value);
                            }
                            else if (argument.ParameterType == typeof (double))
                            {
                                var value = Util.ParseDoubleOrNull(str);
                                if (!value.HasValue)
                                {
                                    failed = true;
                                    break;
                                }
                                argValues.Add(value.Value);
                            }
                            else if (argument.ParameterType == typeof (decimal))
                            {
                                var value = Util.ParseDecimalOrNull(str);
                                if (!value.HasValue)
                                {
                                    failed = true;
                                    break;
                                }
                                argValues.Add(value.Value);
                            }
                            else
                            {
                                throw new ArgumentException(string.Format("argument '{0}' has unknown type {1}", argument.Name, argument.ParameterType));
                            }
                        }

                        if (failed)
                        {
                            // unmatched argument; continue
                            continue;
                        }

                        // if we got this far, dispatch it
                        method.Invoke(responder, argValues.ToArray());
                        return;
                    }
                }
            }

            // call the unhandled request handler
            UnhandledRequestHandler(context);
        }

        /// <summary>
        /// Handles requests.
        /// </summary>
        protected void Proc()
        {
            while (!_stopNow)
            {
                var context = Listener.GetContext();

                ThreadPool.QueueUserWorkItem(c =>
                {
                    var ctx = (HttpListenerContext) c;
                    try
                    {
                        HandleRequest(ctx);
                    }
                    catch (Exception ex)
                    {
                        ExceptionHandler(ctx, ex);
                    }
                }, context);
            }
        }

        /// <summary>
        /// Sends a JSON 404 response. Default unhandled request handler.
        /// </summary>
        /// <param name="context">The context to respond with.</param>
        public static void SendJson404(HttpListenerContext context)
        {
            context.Response.Headers[HttpResponseHeader.ContentType] = "application/json";
            context.Response.StatusCode = 404;
            const string jsonString = "{\"status\":\"error\",\"error\":\"not found\"}";
            var jsonBytes = Util.Utf8NoBom.GetBytes(jsonString);
            context.Response.ContentLength64 = jsonBytes.LongLength;
            context.Response.Close(jsonBytes, true);
        }

        /// <summary>
        /// Sends a JSON 500 response. Default exception handler.
        /// </summary>
        /// <param name="context">The context to respond with.</param>
        /// <param name="exception">The exception that was thrown.</param>
        public static void SendJson500Exception(HttpListenerContext context, Exception exception)
        {
            const string jsonString = "{\"status\":\"error\",\"error\":\"exception thrown\",\"errorType\":\"EXCEPTION\"}";
            var jsonBytes = Util.Utf8NoBom.GetBytes(jsonString);
            Logger.Error("handling HTTP request", exception);
            context.Response.StatusCode = 500;
            context.Response.StatusDescription = "Internal Server Error";
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = jsonBytes.LongLength;
            context.Response.Close(jsonBytes, true);
        }
    }
}
