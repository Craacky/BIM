namespace BIM_Control.Services
{
    public static class ThreadExtension
    {
        public static void SafeInvoke(this Control uiElement, Action updater, bool forceSynchronous)
        {
            if (uiElement == null)
                throw new ArgumentNullException("uiElement");

            if (uiElement.InvokeRequired)
            {
                if (forceSynchronous)
                    uiElement.Invoke((Action)delegate { SafeInvoke(uiElement, updater, forceSynchronous); });
                else
                    uiElement.BeginInvoke((Action)delegate { SafeInvoke(uiElement, updater, forceSynchronous); });
            }
            else
            {
                if (uiElement.IsDisposed)
                    throw new ObjectDisposedException("Control is already disposed.");
                updater();
            }
        }
    }
}
