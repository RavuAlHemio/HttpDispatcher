using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using RavuAlHemio.HttpDispatcher.Matching;

namespace RavuAlHemio.HttpDispatcher.Generic
{
    /// <summary>
    /// An HTTP listener that forwards requests to the registered responders' endpoint-handling methods.
    /// </summary>
    public abstract class GenericDistributingHttpServer<TContext> : IDisposable
    {
        protected static readonly HashSet<char> SimpleAlphanumeric = new HashSet<char>("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789");

        protected readonly CancellationTokenSource CancellerSource;
        protected readonly List<object> Responders;
        protected readonly List<UriHandler> Handlers;
        protected readonly Dictionary<string, Regex> RegexCache;

        private bool _disposed;

        /// <summary>
        /// Occurs when a request has been received, before responders are searched
        /// for a matching endpoint.
        /// </summary>
        public event EventHandler<HttpServerEventArgs<TContext>> RequestReceived;

        /// <summary>
        /// Occurs when a URL string is about to be parsed into a value.
        /// </summary>
        public event EventHandler<ParseValueEventArgs<TContext>> ParseValue;

        /// <summary>
        /// Occurs after a matching endpoint has been found and immediately before
        /// it is called.
        /// </summary>
        public event EventHandler<EndpointEventArgs<TContext>> CallingEndpoint;

        /// <summary>
        /// Occurs after a matching endpoint has been found and called, if an exception
        /// was thrown during its execution.
        /// </summary>
        public event EventHandler<ResponderExceptionEventArgs<TContext>> ResponderException;

        /// <summary>
        /// Occurs if an exception is thrown during distribution (but not while invoking
        /// the endpoint).
        /// </summary>
        public event EventHandler<DistributionExceptionEventArgs<TContext>> DistributionException;

        /// <summary>
        /// Occurs if no matching endpoint was found for a request.
        /// </summary>
        public event EventHandler<UnhandledRequestEventArgs<TContext>> UnhandledRequest;

        #region event plumbing
        protected virtual void OnRequestReceived(HttpServerEventArgs<TContext> e)
        {
            RequestReceived?.Invoke(this, e);
        }

        protected virtual void OnParseValue(ParseValueEventArgs<TContext> e)
        {
            ParseValue?.Invoke(this, e);
        }

        protected virtual void OnCallingEndpoint(EndpointEventArgs<TContext> e)
        {
            CallingEndpoint?.Invoke(this, e);
        }

        protected virtual void OnResponderException(ResponderExceptionEventArgs<TContext> e)
        {
            ResponderException?.Invoke(this, e);
        }

        protected virtual void OnDistributionException(DistributionExceptionEventArgs<TContext> e)
        {
            DistributionException?.Invoke(this, e);
        }

        protected virtual void OnUnhandledRequest(UnhandledRequestEventArgs<TContext> e)
        {
            UnhandledRequest?.Invoke(this, e);
        }
        #endregion

        /// <summary>
        /// Initializes a distributing HTTP listener on the given URI prefix.
        /// </summary>
        protected GenericDistributingHttpServer()
        {
            CancellerSource = new CancellationTokenSource();
            Responders = new List<object>();
            Handlers = new List<UriHandler>();
            RegexCache = new Dictionary<string, Regex>();
            _disposed = false;
        }

        /// <summary>
        /// Adds a new responder to the responder chain.
        /// </summary>
        /// <param name="newResponder">The responder to add.</param>
        public virtual void AddResponder(object newResponder)
        {
            if (newResponder == null)
            {
                throw new ArgumentNullException(nameof(newResponder));
            }

            if (Responders.Contains(newResponder))
            {
                return;
            }

            List<ResponderAttribute> responderAttributes = newResponder
                .GetType()
                .GetTypeInfo()
                .GetCustomAttributes<ResponderAttribute>(true)
                .ToList();
            if (!responderAttributes.Any())
            {
                throw new ArgumentException("the added responder does not have a ResponderAttribute", nameof(newResponder));
            }

            Responders.Add(newResponder);

            // obtain the endpoint prefixes of this responder
            var endpointPrefixes = new List<string>();
            foreach (ResponderAttribute responderAttribute in responderAttributes)
            {
                string pathPrefix = responderAttribute.Path;
                if (pathPrefix == null)
                {
                    endpointPrefixes.Add("");
                    continue;
                }

                pathPrefix = pathPrefix.TrimEnd('/');
                if (!pathPrefix.StartsWith("/"))
                {
                    pathPrefix = "/" + pathPrefix;
                }

                endpointPrefixes.Add(pathPrefix);
            }

            // obtain information about the endpoints (public, non-static methods)
            foreach (var method in newResponder.GetType().GetTypeInfo().GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                // for each endpoint attribute this method has
                foreach (var endpointAttribute in method.GetCustomAttributes<EndpointAttribute>())
                {
                    // method has an endpoint attribute; check if its first argument is a context as we expect it
                    ParameterInfo[] methodParams = method.GetParameters();
                    if (methodParams.Length < 1)
                    {
                        throw new ArgumentException($"responder method {method} has Endpoint attribute but no arguments (requires at least {typeof(TContext).Name})");
                    }
                    if (methodParams[0].ParameterType != typeof(TContext))
                    {
                        throw new ArgumentException($"responder method {method} has Endpoint attribute but the type of its first argument is not {typeof(TContext).Name}");
                    }

                    // construct a regular expression pattern to match the path of the endpoint
                    var newGroupNames = new HashSet<string>();
                    var newBits = new List<string>();
                    foreach (string pathBit in endpointAttribute.Path.Split('/'))
                    {
                        if (IsPlaceholder(pathBit))
                        {
                            string placeholderText = pathBit.Substring(1, pathBit.Length - 2);
                            if (newGroupNames.Contains(placeholderText))
                            {
                                throw new ArgumentException($"multiple placeholders named '{placeholderText}' in path endpoint '{endpointAttribute.Path}'");
                            }
                            newGroupNames.Add(placeholderText);
                            newBits.Add($"(?<{placeholderText}>[^/]+)");
                        }
                        else
                        {
                            newBits.Add(Regex.Escape(pathBit));
                        }
                    }

                    string regexString = string.Join("/", newBits);

                    // verify if all arguments have explicit or default values
                    foreach (ParameterInfo param in methodParams.Skip(1))
                    {
                        if (!newGroupNames.Contains(param.Name) && !param.HasDefaultValue)
                        {
                            throw new ArgumentException($"method {method} endpoint '{endpointAttribute.Path}' does not handle argument {param.Name}");
                        }
                    }

                    // apply each endpoint prefix in turn
                    foreach (string endpointPrefix in endpointPrefixes)
                    {
                        string fullRegexString = $"^{Regex.Escape(endpointPrefix)}{regexString}$";
                        Regex matcher = ObtainRegex(fullRegexString);
                        Handlers.Add(new UriHandler(matcher, newGroupNames, newResponder, method,
                            endpointAttribute));
                    }
                }
            }
        }

        /// <summary>
        /// Removes the responder from the responder chain.
        /// </summary>
        /// <returns><c>true</c> if the responder has been removed, <c>false</c> if it was not contained
        /// in the responder chain in the first place.</returns>
        /// <param name="responder">The responder to remove.</param>
        public virtual bool RemoveResponder(object responder)
        {
            if (responder == null)
            {
                throw new ArgumentNullException(nameof(responder));
            }

            bool removed = Responders.Remove(responder);
            if (!removed)
            {
                return false;
            }

            // also remove all handlers with this responder
            int numRemoved = Handlers.RemoveAll(h => h.Responder == responder);
            return (numRemoved > 0);
        }

        /// <summary>
        /// Starts listening to HTTP requests.
        /// </summary>
        public virtual void Start()
        {
        }

        /// <summary>
        /// Stops listening to HTTP requests.
        /// </summary>
        public virtual void Stop()
        {
            CancellerSource.Cancel();
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

        protected virtual void HandleRequest(TContext context)
        {
            var httpMethod = RequestHttpMethodFromContext(context);
            var realUrl = RequestUriFromContext(context);
            var availableMethodsForPath = new List<string>();
            var path = realUrl.AbsolutePath;

            var lea = new HttpServerEventArgs<TContext>(context);
            OnRequestReceived(lea);
            if (lea.Responded)
            {
                return;
            }

            // find a handler
            foreach (var handler in Handlers)
            {
                Match match = handler.Matcher.Match(path);
                if (!match.Success)
                {
                    continue;
                }

                // we found a match
                var argumentStrings = new Dictionary<string, string>();
                foreach (string groupName in handler.ParameterNames)
                {
                    argumentStrings[groupName] = match.Groups[groupName].Value;
                }

                ParameterInfo[] parameters = handler.Endpoint.GetParameters();
                // this has been verified during adding
                Debug.Assert(parameters.Length > 0);
                Debug.Assert(parameters[0].ParameterType == typeof(TContext));

                // add the context as the first argument value
                var argValues = new List<object> {context};

                var failed = false;
                foreach (ParameterInfo argument in parameters.Skip(1))
                {
                    if (!handler.ParameterNames.Contains(argument.Name))
                    {
                        // this has been verified during adding
                        Debug.Assert(argument.HasDefaultValue);

                        argValues.Add(argument.DefaultValue);
                        continue;
                    }

                    string str = HttpDispatcherUtil.UrlDecodeUtf8(argumentStrings[argument.Name]);

                    var e = new ParseValueEventArgs<TContext>(context, handler.Responder, handler.Endpoint,
                        argument.Name, argument.ParameterType, str);
                    OnParseValue(e);
                    if (e.Responded)
                    {
                        return;
                    }

                    if (e.Parsed)
                    {
                        argValues.Add(str);
                        continue;
                    }

                    // some defaults
                    if (argument.ParameterType == typeof(string))
                    {
                        argValues.Add(str);
                    }
                    else if (argument.ParameterType == typeof(int)
                             || argument.ParameterType == typeof(int?))
                    {
                        int? value = HttpDispatcherUtil.ParseIntOrNull(str);
                        if (!value.HasValue)
                        {
                            failed = true;
                            break;
                        }
                        argValues.Add(value.Value);
                    }
                    else if (argument.ParameterType == typeof(long)
                             || argument.ParameterType == typeof(long?))
                    {
                        long? value = HttpDispatcherUtil.ParseLongOrNull(str);
                        if (!value.HasValue)
                        {
                            failed = true;
                            break;
                        }
                        argValues.Add(value.Value);
                    }
                    else if (argument.ParameterType == typeof(float)
                             || argument.ParameterType == typeof(float?))
                    {
                        double? value = HttpDispatcherUtil.ParseFloatOrNull(str);
                        if (!value.HasValue)
                        {
                            failed = true;
                            break;
                        }
                        argValues.Add((float)value.Value);
                    }
                    else if (argument.ParameterType == typeof(double)
                             || argument.ParameterType == typeof(double?))
                    {
                        double? value = HttpDispatcherUtil.ParseDoubleOrNull(str);
                        if (!value.HasValue)
                        {
                            failed = true;
                            break;
                        }
                        argValues.Add(value.Value);
                    }
                    else if (argument.ParameterType == typeof(decimal)
                             || argument.ParameterType == typeof(decimal?))
                    {
                        decimal? value = HttpDispatcherUtil.ParseDecimalOrNull(str);
                        if (!value.HasValue)
                        {
                            failed = true;
                            break;
                        }
                        argValues.Add(value.Value);
                    }
                    else
                    {
                        throw new ArgumentException($"don't know how to parse arguments of type {argument.ParameterType} after encountering argument {argument.Name} of {handler.Endpoint}");
                    }
                }

                if (failed)
                {
                    // unmatched argument; try next matcher
                    continue;
                }

                if (handler.EndpointAttribute.Method != null && handler.EndpointAttribute.Method != httpMethod)
                {
                    // endpoint's HTTP method doesn't match request's HTTP method
                    availableMethodsForPath.Add(handler.EndpointAttribute.Method);
                    continue;
                }

                // we're ready; dispatch it
                var eea = new EndpointEventArgs<TContext>(context, handler.Responder, handler.Endpoint);
                OnCallingEndpoint(eea);
                if (eea.Responded)
                {
                    return;
                }

                try
                {
                    handler.Endpoint.Invoke(handler.Responder, argValues.ToArray());
                }
                catch (TargetInvocationException exc)
                {
                    var reea = new ResponderExceptionEventArgs<TContext>(context, handler.Responder, handler.Endpoint,
                        exc.InnerException);
                    OnResponderException(reea);
                    if (reea.Responded)
                    {
                        return;
                    }
                    SendJson500Exception(context, exc);
                }
                return;
            }

            // call the unhandled request handler
            var urea = new UnhandledRequestEventArgs<TContext>(context)
            {
                AvailableMethodsForPath = availableMethodsForPath
            };
            OnUnhandledRequest(urea);
            if (urea.Responded)
            {
                return;
            }

            // call the default unhandled request handler
            SendJson404(context);
        }

        protected abstract void SendJson404(TContext context);
        protected abstract void SendJson500Exception(TContext context, Exception exc);

        protected Regex ObtainRegex(string regexString)
        {
            Regex regex;
            if (RegexCache.TryGetValue(regexString, out regex))
            {
                return regex;
            }

            regex = new Regex(regexString, RegexOptions.Compiled);
            RegexCache[regexString] = regex;
            return regex;
        }

        protected abstract string RequestHttpMethodFromContext(TContext context);
        protected abstract Uri RequestUriFromContext(TContext context);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                CancellerSource.Dispose();
            }

            _disposed = true;
        }
    }
}
