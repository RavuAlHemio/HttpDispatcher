using System;
using System.Net;
using System.Reflection;

namespace RavuAlHemio.HttpDispatcher
{
    /// <summary>
    /// Provides data for the <see cref="DistributingHttpListener.DistributionException"/> event.
    /// </summary>
    public class DistributionExceptionEventArgs : ListenerEventArgs
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
        /// <param name="context">The <see cref="HttpListenerContext"/> containing
        /// information pertaining to the active request.</param>
        /// <param name="exception">The exception that has been thrown by the responder.</param>
        public DistributionExceptionEventArgs(HttpListenerContext context, Exception exception)
            : base(context)
        {
            Exception = exception;
        }
    }
}
