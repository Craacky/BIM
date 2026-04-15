namespace BIM.Application.Common.Interfaces
{
    public interface ILicenseService
    {
        bool ValidateLicense(string token, out string errorMessage);
    }
}
