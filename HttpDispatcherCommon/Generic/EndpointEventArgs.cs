using System.Net;
using System.Reflection;

namespace RavuAlHemio.HttpDispatcher.Generic
{
    /// <summary>
    /// Provides data for the <see cref="GenericDistributingHttpServer{TContext}.CallingEndpoint"/> event.
    /// </summary>
    public class EndpointEventArgs<TContext> : HttpServerEventArgs<TContext>
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
        /// <see cref="GenericEndpointEventArgs{TContext}"/> class.
        /// </summary>
        /// <param name="context">The <typeparamref name="TContext"/> containing
        /// information pertaining to the active request.</param>
        /// <param name="responder">The responder whose endpoint is being called.</param>
        /// <param name="endpoint">The endpoint that is being called.</param>
        public EndpointEventArgs(TContext context, object responder, MethodInfo endpoint)
            : base(context)
        {
            Responder = responder;
            Endpoint = endpoint;
        }
    }
}
