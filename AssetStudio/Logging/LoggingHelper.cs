using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using JetBrains.Annotations;

namespace AssetStudio.Logging
{
    [PublicAPI]
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    [Discardable]
    public static class LoggingHelper
    {
        [Discardable, MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetPrefix()
        {
            // ReSharper disable once CommentTypo
            // if .NET 4.5, can use Ben.Demystifier to clean this crap up

            // stack frame count minus logger frames
            int fc = new StackTrace(0, false).FrameCount - 2;

            // thread id
            int tid = Thread.CurrentThread.ManagedThreadId;

            // stack frame above logger
            var sf = new StackFrame(2, true);

            MethodBase mb = sf.GetMethod();

            // method name
            string mn;

            if (mb.IsSpecialName)
            {
                mn = "(unknown)";
            }
            else
            {
                mn = mb.Name;
            }

            string fn = Path.GetFileName(sf.GetFileName());

            int ln = sf.GetFileLineNumber();

            int cn = sf.GetFileColumnNumber();

            // ReSharper disable once StringLiteralTypo
            string dt = DateTime.Now.ToString("HH:mm:ss:ffff");

            string tn = null;
            try
            {
                // can throw or be null
                Type t = mb.DeclaringType;
                tn = t?.Name;
                if (t != null && t.IsSpecialName)
                {
                    tn = null;
                }
            }
            catch
            {
                // dynamic, runtime or special method
            }

            if (tn != null)
            {
                return $"[{dt} #{tid}] {fc} {tn}.{mn} @ {fn}:{ln}:{cn} ";
            }

            return $"[{dt} #{tid}] {fc} {mn} @ {fn}:{ln}:{cn} ";
        }

        [Discardable, Conditional("TRACE"), MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogDebug(string msg)
        {
            Trace.WriteLine(GetPrefix() + msg);
        }

        [Discardable, Conditional("TRACE"), MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogInfo(string msg)
        {
            Trace.TraceInformation(GetPrefix() + msg);
        }

        [Discardable, Conditional("TRACE"), MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogWarn(string msg)
        {
            Trace.TraceWarning(GetPrefix() + msg);
        }

        [Discardable, Conditional("TRACE"), MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogError(string msg)
        {
            Trace.TraceError(GetPrefix() + msg);
        }

        [Discardable, Conditional("TRACE"), MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogException(Exception ex, string msg = null)
        {
            Trace.TraceError(GetPrefix() + (msg ?? "Exception follows"));
            Trace.TraceError("=== BEGIN EXCEPTION ===");
            var i = 1;
            do
            {
                Trace.TraceError($"{i++} {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");
                ex = ex.InnerException;
                Trace.WriteLineIf(ex != null, "");
            }
            while (ex != null);
            Trace.TraceError("=== END EXCEPTION ===");
        }
    }
}