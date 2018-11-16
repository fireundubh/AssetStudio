using System.ComponentModel;
using System.Windows.Forms;

namespace AssetStudio.Extensions
{
    internal static class UIExtensions
    {
        public static void InvokeIfRequired(this ISynchronizeInvoke obj, MethodInvoker action)
        {
            if (!obj.InvokeRequired)
            {
                action();
                return;
            }

            var args = new object[0];
            obj.Invoke(action, args);
        }

        public static void AsyncInvokeIfRequired(this ISynchronizeInvoke obj, MethodInvoker action)
        {
            if (!obj.InvokeRequired)
            {
                action();
                return;
            }

            var args = new object[0];
            obj.BeginInvoke(action, args);
        }
    }
}