using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using static AssetStudio.Logging.LoggingHelper;

namespace AssetStudio
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

#if TRACE
            TraceListenerCollection listeners = Trace.Listeners;
            listeners.Add(new ConsoleTraceListener(true)
            {
                Name = "Console"
            });
            listeners.Add(new TextWriterTraceListener(new Uri(typeof(Program).Assembly.CodeBase).LocalPath + ".log")
            {
                Name = "LogFile"
            });
            Trace.AutoFlush = true;
#endif

            LogInfo("Started");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AssetStudioForm());
        }

        private static void OnUnhandledException(object appDom, UnhandledExceptionEventArgs unhandled)
        {
            if (unhandled.ExceptionObject is Exception exception)
            {
                LogException(exception, "Unhandled Exception");
            }
        }
    }
}