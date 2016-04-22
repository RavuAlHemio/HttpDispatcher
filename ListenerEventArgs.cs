using System;
using System.Net;

namespace RavuAlHemio.HttpDispatcher
{
    /// <summary>
    /// Provides data for the events of <see cref="DistributingHttpListener"/>.
    /// </summary>
    public class ListenerEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the context of the <see cref="HttpListener"/>, which
        /// may be used to inspect the request and send a response to the client.
        /// </summary>
        public HttpListenerContext Context { get; protected set; }

        /// <summary>
        /// Gets or sets a value indicating whether an event handler has already
        /// responded to a request and whether further processing should
        /// therefore be terminated.
        /// </summary>
        public bool Responded { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ListenerEventArgs"/> class.
        /// </summary>
        /// <param name="context">The <see cref="HttpListenerContext"/> containing
        /// information pertaining to the active request.</param>
        public ListenerEventArgs(HttpListenerContext context)
        {
            Context = context;
            Responded = false;
        }
    }
}
