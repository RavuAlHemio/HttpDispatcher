using System;
using System.Net;
using System.Reflection;

namespace RavuAlHemio.HttpDispatcher
{
    /// <summary>
    /// Provides data for the <see cref="DistributingHttpListener.CallingEndpoint"/> event.
    /// </summary>
    public class EndpointEventArgs : ListenerEventArgs
    {
        /// <summary>
        /// Gets the responder whose endpoint method will be called next (unless <see cref="Responded"/>
        /// is set to <c>true</c>).
        /// </summary>
        public object Responder { get; protected set; }

        /// <summary>
        /// Gets the method which will be called on the responder (unless <see cref="Responded"/>
        /// is set to <c>true</c>).
        /// </summary>
        public MethodInfo Endpoint { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ResponderExceptionEventArgs"/> class.
        /// </summary>
        /// <param name="context">The <see cref="HttpListenerContext"/> containing
        /// information pertaining to the active request.</param>
        /// <param name="exception">The exception that has been thrown by the responder.</param>
        public EndpointEventArgs(HttpListenerContext context, object responder, MethodInfo endpoint)
            : base(context)
        {
            Responder = responder;
            Endpoint = endpoint;
        }
    }
}
