using System;
using RavuAlHemio.HttpDispatcher.Kestrel;

namespace RavuAlHemio.HttpDispatcher.Demos.Kestrel.CLIDemo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var responder = new KestrelResponder();
            using (var listener = new DistributingKestrelServer("http://localhost:8080/"))
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
