namespace BIM.Application.Common.Constants
{
    public static class AppConstants
    {
        public static class Printer
        {
            public const int DefaultPort = 9100;
            public const int BackgroundMonitorIntervalMs = 2000;
            public const int ActiveMonitorIntervalMs = 1000;
            public const int ConnectionTimeoutMs = 2000;
            public const int PersistentConnectionTimeoutMs = 5000;
            // PATCH-BEGIN: SpoolerWin7Stability
            // Increased timeout to reduce false timeouts on Win7/slow spooler service operations.
            public const int SpoolerCleanupTimeoutMs = 30000;
            // PATCH-END: SpoolerWin7Stability

            public static readonly byte[] RebootCommand = { 0x1B, 0x21, 0x43 }; // ESC ! C
            public static readonly byte[] StatusCommand = { 0x1B, 0x21, 0x3F }; // ESC ! ?
        }

        public static class Camera
        {
            public const int DefaultPort = 2000;
            public const int ReconnectDelayMs = 5000;
            public const int MaxBufferLength = 10000;
            public const string TagStart = "<start>";
            public const string TagStop = "<stop>";
            public const string TagNext = "<next>";
            public const string FailMarker = "fail";
        }

        public static class UI
        {
            public const int PeriodicCheckIntervalMs = 3000;
            public const string ImageDeleteButton = "Images/delete-button.png";
            
            public static class Colors
            {
                // Can define common colors here if needed, but System.Drawing.Color is often enough
            }
        }
    }
}
