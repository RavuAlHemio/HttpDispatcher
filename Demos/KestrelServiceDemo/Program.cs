using DasMulli.Win32.ServiceUtils;

namespace RavuAlHemio.HttpDispatcher.Demos.Kestrel.ServiceDemo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var service = new KestrelDemoService();
            var serviceHost = new Win32ServiceHost(service);
            serviceHost.Run();
        }
    }
}
