using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace RavuAlHemio.HttpDispatcher
{
    /// <summary>
    /// An HTTP listener that forwards requests to the registered responders' endpoint-handling methods.
    /// </summary>
    public class DistributingHttpListener
    {
        private static readonly HashSet<char> SimpleAlphanumeric = new HashSet<char>("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789");

        private volatile bool _stopNow;
        private readonly Thread _acceptorThread;
        protected readonly HttpListener Listener;
        protected readonly List<object> Responders;
        protected readonly Dictionary<string, Regex> RegexCache;

        /// <summary>
        /// Occurs when a request has been received, before responders are searched
        /// for a matching endpoint.
        /// </summary>
        public event EventHandler<ListenerEventArgs> RequestReceived;

        /// <summary>
        /// Occurs when a URL string is about to be parsed into a value.
        /// </summary>
        public event EventHandler<ParseValueEventArgs> ParseValue;

        /// <summary>
        /// Occurs after a matching endpoint has been found and immediately before
        /// it is called.
        /// </summary>
        public event EventHandler<EndpointEventArgs> CallingEndpoint;

        /// <summary>
        /// Occurs after a matching endpoint has been found and called, and it has
        /// returned without throwing an exception.
        /// </summary>
        public event EventHandler<EndpointEventArgs> EndpointCalled;

        /// <summary>
        /// Occurs after a matching endpoint has been found and called, if an exception
        /// was thrown during its execution.
        /// </summary>
        public event EventHandler<ResponderExceptionEventArgs> ResponderException;

        /// <summary>
        /// Occurs if an exception is thrown during distribution (but not while invoking
        /// the endpoint).
        /// </summary>
        public event EventHandler<DistributionExceptionEventArgs> DistributionException;

        /// <summary>
        /// Occurs if no matching endpoint was found for a request.
        /// </summary>
        public event EventHandler<ListenerEventArgs> UnhandledRequest;

        #region event plumbing
        protected virtual void OnRequestReceived(ListenerEventArgs e)
        {
            var handler = RequestReceived;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnParseValue(ParseValueEventArgs e)
        {
            var handler = ParseValue;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnCallingEndpoint(EndpointEventArgs e)
        {
            var handler = CallingEndpoint;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnEndpointCalled(EndpointEventArgs e)
        {
            var handler = EndpointCalled;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnResponderException(ResponderExceptionEventArgs e)
        {
            var handler = ResponderException;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnDistributionException(DistributionExceptionEventArgs e)
        {
            var handler = DistributionException;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnUnhandledRequest(ListenerEventArgs e)
        {
            var handler = UnhandledRequest;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        #endregion

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

            var lea = new ListenerEventArgs(context);
            OnRequestReceived(lea);
            if (lea.Responded)
            {
                return;
            }

            // find a responder
            foreach (var responder in Responders)
            {
                var endpointPrefixes = new HashSet<string>();
                foreach (
                    var responderAttribute in
                        responder.GetType()
                            .GetCustomAttributes(typeof (ResponderAttribute), true)
                            .Select(a => (ResponderAttribute) a))
                {
                    var pathPrefix = responderAttribute.Path;
                    if (pathPrefix == null)
                    {
                        endpointPrefixes.Add("");
                        continue;
                    }
                    while (pathPrefix.EndsWith("/"))
                    {
                        pathPrefix = pathPrefix.Substring(0, pathPrefix.Length - 1);
                    }
                    if (!pathPrefix.StartsWith("/"))
                    {
                        pathPrefix = "/" + pathPrefix;
                    }

                    endpointPrefixes.Add(pathPrefix);
                }

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

                        Match match = null;
                        foreach (var endpointPrefix in endpointPrefixes)
                        {
                            var regexString = "^" + Regex.Escape(endpointPrefix) + string.Join("[/]", newBits) + "$";
                            var regex = RegexCache.ContainsKey(regexString)
                                ? RegexCache[regexString]
                                : (RegexCache[regexString] = new Regex(regexString));

                            match = regex.Match(path);
                            if (match.Success)
                            {
                                // the path handled by this endpoint matches the request's path
                                break;
                            }
                        }
                        if (match == null || !match.Success)
                        {
                            // this endpoint doesn't match the path
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
                        if (parameters[0].ParameterType != typeof(HttpListenerContext))
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

                            var str = HttpDispatcherUtil.UrlDecodeUtf8(argumentStrings[argument.Name]);

                            var e = new ParseValueEventArgs(context, responder, method, argument.Name, argument.ParameterType, str);
                            OnParseValue(e);
                            if (e.Responded)
                            {
                                return;
                            }

                            if (e.Parsed)
                            {
                                argValues.Add(str);
                            }
                            // some defaults
                            else if (argument.ParameterType == typeof(string))
                            {
                                argValues.Add(str);
                            }
                            else if (argument.ParameterType == typeof(int))
                            {
                                var value = HttpDispatcherUtil.ParseIntOrNull(str);
                                if (!value.HasValue)
                                {
                                    failed = true;
                                    break;
                                }
                                argValues.Add(value.Value);
                            }
                            else if (argument.ParameterType == typeof(long))
                            {
                                var value = HttpDispatcherUtil.ParseLongOrNull(str);
                                if (!value.HasValue)
                                {
                                    failed = true;
                                    break;
                                }
                                argValues.Add(value.Value);
                            }
                            else if (argument.ParameterType == typeof(double))
                            {
                                var value = HttpDispatcherUtil.ParseDoubleOrNull(str);
                                if (!value.HasValue)
                                {
                                    failed = true;
                                    break;
                                }
                                argValues.Add(value.Value);
                            }
                            else if (argument.ParameterType == typeof(decimal))
                            {
                                var value = HttpDispatcherUtil.ParseDecimalOrNull(str);
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
                        var eea = new EndpointEventArgs(context, responder, method);
                        OnCallingEndpoint(eea);
                        if (eea.Responded)
                        {
                            return;
                        }

                        try
                        {
                            method.Invoke(responder, argValues.ToArray());
                        }
                        catch (TargetInvocationException exc)
                        {
                            var reea = new ResponderExceptionEventArgs(context, responder, method, exc.InnerException);
                            OnResponderException(reea);
                            if (reea.Responded)
                            {
                                return;
                            }
                        }
                        return;
                    }
                }
            }

            // call the unhandled request handler
            OnUnhandledRequest(lea);
            if (lea.Responded)
            {
                return;
            }

            // call the default unhandled request handler
            SendJson404(context);
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
                        var deea = new DistributionExceptionEventArgs(context, ex);
                        OnDistributionException(deea);
                        if (deea.Responded)
                        {
                            return;
                        }
                        SendJson500Exception(context, ex);
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
            var jsonBytes = HttpDispatcherUtil.Utf8NoBom.GetBytes(jsonString);
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
            var jsonBytes = HttpDispatcherUtil.Utf8NoBom.GetBytes(jsonString);
            context.Response.StatusCode = 500;
            context.Response.StatusDescription = "Internal Server Error";
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = jsonBytes.LongLength;
            context.Response.Close(jsonBytes, true);
        }
    }
}
