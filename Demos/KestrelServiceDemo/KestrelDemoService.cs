using DasMulli.Win32.ServiceUtils;
using RavuAlHemio.HttpDispatcher.Kestrel;

namespace RavuAlHemio.HttpDispatcher.Demos.Kestrel.ServiceDemo
{
    public class KestrelDemoService : IWin32Service
    {
        private DistributingKestrelServer _listener;

        public string ServiceName => "KestrelDemoService";

        public void Start(string[] startupArguments, ServiceStoppedCallback serviceStoppedCallback)
        {
            var responder = new KestrelResponder();
            _listener = new DistributingKestrelServer(8080);
            _listener.AddResponder(responder);
            _listener.Start();
        }

        public void Stop()
        {
            _listener.Stop();
            _listener.Dispose();
        }
    }
}
