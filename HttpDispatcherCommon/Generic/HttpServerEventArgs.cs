using System;

namespace RavuAlHemio.HttpDispatcher.Generic
{
    /// <summary>
    /// Provides data for the events of <see cref="GenericDistributingHttpServer{TContext}"/>.
    /// </summary>
    public class HttpServerEventArgs<TContext> : EventArgs
    {
        /// <summary>
        /// Gets the context of the underlying server, which
        /// may be used to inspect the request and send a response to the client.
        /// </summary>
        public TContext Context { get; protected set; }

        /// <summary>
        /// Gets or sets a value indicating whether an event handler has already
        /// responded to a request and whether further processing should
        /// therefore be terminated.
        /// </summary>
        public bool Responded { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpServerEventArgs{TContext}"/> class.
        /// </summary>
        /// <param name="context">The <typeparamref name="TContext"/> containing
        /// information pertaining to the active request.</param>
        public HttpServerEventArgs(TContext context)
        {
            Context = context;
            Responded = false;
        }
    }
}
