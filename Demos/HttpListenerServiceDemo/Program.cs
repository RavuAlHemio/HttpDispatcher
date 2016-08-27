﻿using System.ServiceProcess;

namespace HttpListenerServiceDemo
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            var servicesToRun = new ServiceBase[]
            {
                new HttpListenerDemoService()
            };
            ServiceBase.Run(servicesToRun);
        }
    }
}
