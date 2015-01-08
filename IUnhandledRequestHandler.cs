using System.Net;

namespace RavuAlHemio.HttpDispatcher
{
    public interface IUnhandledRequestHandler
    {
        void HandleUnhandledRequest(HttpListenerContext context);
    }
}

