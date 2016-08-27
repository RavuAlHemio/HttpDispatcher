using System.ServiceProcess;
using RavuAlHemio.HttpDispatcher.Kestrel;

namespace RavuAlHemio.HttpDispatcher.Demos.Kestrel.ServiceDemo
{
    public class KestrelDemoService : ServiceBase
    {
        private DistributingKestrelServer _listener;

        public KestrelDemoService()
            : base()
        {
            ServiceName = "KestrelDemoService";
        }

        protected override void OnStart(string[] args)
        {
            var responder = new KestrelResponder();
            _listener = new DistributingKestrelServer("http://localhost:8080/");
            _listener.AddResponder(responder);
            _listener.Start();
        }

        protected override void OnStop()
        {
            _listener.Stop();
            _listener.Dispose();
        }
    }
}
