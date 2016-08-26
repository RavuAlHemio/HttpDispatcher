using System;

namespace RavuAlHemio.HttpDispatcher.Generic
{
    /// <summary>
    /// Provides data for the <see cref="GenericDistributingHttpServer{TContext}.DistributionException"/> event.
    /// </summary>
    public class DistributionExceptionEventArgs<TContext> : HttpServerEventArgs<TContext>
    {
        /// <summary>
        /// Gets or sets a value indicating whether an event handler has already
        /// responded to a request and whether further processing should
        /// therefore be terminated.
        /// </summary>
        public Exception Exception { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ResponderExceptionEventArgs"/> class.
        /// </summary>
        /// <param name="context">The <typeparamref name="TContext"/> containing
        /// information pertaining to the active request.</param>
        /// <param name="exception">The exception that has been thrown by the responder.</param>
        public DistributionExceptionEventArgs(TContext context, Exception exception)
            : base(context)
        {
            Exception = exception;
        }
    }
}
