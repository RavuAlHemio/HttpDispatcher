using System;
using System.Net;
using System.Reflection;

namespace RavuAlHemio.HttpDispatcher
{
    /// <summary>
    /// Provides data for the <see cref="DistributingHttpListener.ParseValue"/> event.
    /// </summary>
    public class ParseValueEventArgs : EndpointEventArgs
    {
        /// <summary>
        /// Gets or sets the name of the path argument whose value is
        /// being parsed.
        /// </summary>
        public string ArgumentName { get; protected set; }

        /// <summary>
        /// Gets or sets the type of the path argument whose value is
        /// being parsed.
        /// </summary>
        public Type ArgumentType { get; protected set; }

        /// <summary>
        /// Gets or sets the string value being parsed into an object.
        /// </summary>
        public string StringValue { get; protected set; }

        /// <summary>
        /// Gets or sets whether the object has been parsed into a value
        /// by an event handler.
        /// </summary>
        public bool Parsed { get; set; }

        /// <summary>
        /// Gets or sets the object value that has been determined by
        /// the event handler for the given string value.
        /// </summary>
        public object Value { get; set; }

        public ParseValueEventArgs(HttpListenerContext context, object responder, MethodInfo endpoint, string argumentName, Type argumentType, string stringValue)
            : base(context, responder, endpoint)
        {
            ArgumentName = argumentName;
            ArgumentType = argumentType;
            StringValue = stringValue;
            Parsed = false;
            Value = null;
        }
    }
}
