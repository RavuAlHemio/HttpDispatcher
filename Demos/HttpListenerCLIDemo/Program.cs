using System;

namespace RavuAlHemio.HttpDispatcher.Demos.HttpListener.CLIDemo
{
    class Program
    {
        static void Main()
        {
            var responder = new HttpListenerResponder();
            using (var listener = new DistributingHttpListener("http://localhost:8080/"))
            {
                listener.AddResponder(responder);
                listener.Start();

                Console.WriteLine("Press Enter or Escape to stop.");
                for (;;)
                {
                    ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Escape)
                    {
                        break;
                    }
                }

                listener.Stop();
            }
        }
    }
}
