namespace BIM.Application.Common.Interfaces
{
    public interface ILoggerService
    {
        void LogDebug(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogCritical(string message);
        void LogInformation(string message);
    }
}
