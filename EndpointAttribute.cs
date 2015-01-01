using System;

namespace RavuAlHemio.HttpDispatcher
{
    /// <summary>
    /// Marks a method that responds to HTTP requests at a specific path.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    class EndpointAttribute : Attribute
    {
        private readonly string _path;

        /// <summary>
        /// The path to respond to, with placeholders for arguments in curly braces.
        /// </summary>
        public string Path { get { return _path; } }

        /// <summary>
        /// The method to respond to, or null if the method doesn't matter.
        /// </summary>
        public string Method { get; set; }

        public EndpointAttribute(string path)
        {
            _path = path;
            Method = null;
        }
    }
}
