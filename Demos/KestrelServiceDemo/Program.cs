using System.ServiceProcess;

namespace RavuAlHemio.HttpDispatcher.Demos.Kestrel.ServiceDemo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var servicesToRun = new ServiceBase[]
            {
                new KestrelDemoService()
            };
            ServiceBase.Run(servicesToRun);
        }
    }
}
