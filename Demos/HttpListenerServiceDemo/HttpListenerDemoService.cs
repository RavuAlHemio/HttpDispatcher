using System.ServiceProcess;
using RavuAlHemio.HttpDispatcher;
using RavuAlHemio.HttpDispatcher.Demos.HttpListener;

namespace HttpListenerServiceDemo
{
    public partial class HttpListenerDemoService : ServiceBase
    {
        private DistributingHttpListener _listener;

        public HttpListenerDemoService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            var responder = new HttpListenerResponder();
            _listener = new DistributingHttpListener("http://localhost:8080/");
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
