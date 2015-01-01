using System;

namespace RavuAlHemio.HttpDispatcher
{
    /// <summary>
    /// Marks a class that is fitted to respond to HTTP requests.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    class ResponderAttribute : Attribute
    {
        /// <summary>
        /// A common path prefix for all the endpoints declared in this class, or <c>null</c> if
        /// no common prefix is desired.
        /// </summary>
        public string Path { get; set; }

        public ResponderAttribute()
        {
            Path = null;
        }
    }
}
