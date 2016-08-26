using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RavuAlHemio.HttpDispatcher.Matching
{
    /// <summary>
    /// A matcher regular expression, a responder object and an endpoint method.
    /// </summary>
    public class UriHandler
    {
        public Regex Matcher { get; }
        public IReadOnlyList<string> ParameterNames { get; }
        public object Responder { get; }
        public MethodInfo Endpoint { get; }
        public EndpointAttribute EndpointAttribute { get; }

        public UriHandler(Regex matcher, IEnumerable<string> parameterNames, object responder, MethodInfo endpoint,
            EndpointAttribute endpointAttribute)
        {
            if (matcher == null)
            {
                throw new ArgumentNullException(nameof(matcher));
            }
            if (parameterNames == null)
            {
                throw new ArgumentNullException(nameof(parameterNames));
            }
            if (responder == null)
            {
                throw new ArgumentNullException(nameof(responder));
            }
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }
            if (endpointAttribute == null)
            {
                throw new ArgumentNullException(nameof(endpointAttribute));
            }

            Matcher = matcher;
            ParameterNames = new List<string>(parameterNames);
            Responder = responder;
            Endpoint = endpoint;
            EndpointAttribute = endpointAttribute;
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(Matcher != null);
            Contract.Invariant(Responder != null);
            Contract.Invariant(Responder.GetType().GetTypeInfo().GetCustomAttributes<ResponderAttribute>().Any());
            Contract.Invariant(Endpoint != null);
            Contract.Invariant(EndpointAttribute != null);
            Contract.Invariant(Endpoint.GetCustomAttributes<EndpointAttribute>().Contains(EndpointAttribute));
        }
    }
}
