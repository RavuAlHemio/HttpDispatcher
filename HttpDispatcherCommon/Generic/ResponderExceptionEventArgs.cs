﻿using System;
using System.Reflection;

namespace RavuAlHemio.HttpDispatcher.Generic
{
    /// <summary>
    /// Provides data for the <see cref="GenericDistributingHttpServer{TContext}.ResponderException"/> event.
    /// </summary>
    public class ResponderExceptionEventArgs<TContext> : EndpointEventArgs<TContext>
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
        /// <param name="responder">The responder whose endpoint method has thrown the exception.</param>
        /// <param name="endpoint">The endpoint method which has thrown the exception.</param>
        /// <param name="exception">The exception that has been thrown by the responder.</param>
        public ResponderExceptionEventArgs(TContext context, object responder, MethodInfo endpoint, Exception exception)
            : base(context, responder, endpoint)
        {
            Exception = exception;
        }
    }
}
