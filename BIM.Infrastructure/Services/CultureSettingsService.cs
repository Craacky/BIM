using BIM.Application.Common.Interfaces;
using System.Globalization;
using System.Runtime.InteropServices;

namespace BIM.Infrastructure.Services
{
    public class CultureSettingsService : ICultureSettingsService
    {
        //dll
        [DllImport("user32.dll")]
        static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        //variables
        private CultureInfo _currentLanguage;

        //properties
        public string CurrentLanguage { get; set; } = string.Empty;
        public string CapsLock { get; set; } = string.Empty;

        private CultureInfo GetCurrentCulture()
        {
            var l = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero));
            return new CultureInfo((short)l.ToInt64());
        }

        public void HandleCurrentLanguage()
        {
            var currentCulture = GetCurrentCulture();
            if (_currentLanguage == null || _currentLanguage.LCID != currentCulture.LCID)
            {
                _currentLanguage = currentCulture;
                CurrentLanguage = _currentLanguage.Name == "en-US" ? "On" : "Off";
            }
        }

        public void HandleCapsLock() => CapsLock = Console.CapsLock ? "On" : "Off";
    }
}