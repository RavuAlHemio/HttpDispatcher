using System.Collections.Generic;

namespace RavuAlHemio.HttpDispatcher.Generic
{
    /// <summary>
    /// Provides data for the <see cref="GenericDistributingHttpServer{TContext}.UnhandledRequest"/> event.
    /// </summary>
    public class UnhandledRequestEventArgs<TContext> : HttpServerEventArgs<TContext>
    {
        /// <summary>
        /// If at least one handler was found for this path, but the HTTP method of the handler didn't match the HTTP
        /// method of the request, <see cref="AvailableMethodsForPath"/> will contain a list of HTTP methods for which
        /// a handler exists for this path. If no handlers were found for this path, this list will be empty.
        /// </summary>
        public IList<string> AvailableMethodsForPath { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnhandledRequestEventArgs"/> class.
        /// </summary>
        /// <param name="context">The <typeparamref name="TContext"/> containing
        /// information pertaining to the active request.</param>
        public UnhandledRequestEventArgs(TContext context)
            : base(context)
        {
        }
    }
}
