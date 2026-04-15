namespace BIM.Application.Common.Interfaces
{
    public interface ICultureSettingsService
    {
        string CurrentLanguage { get; set; }
        string CapsLock { get; set; }

        void HandleCurrentLanguage();
        void HandleCapsLock();
    }
}
