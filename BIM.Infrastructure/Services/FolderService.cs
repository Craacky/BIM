using BIM.Application.Common.Configs;
using BIM.Application.Common.Interfaces;

namespace BIM.Infrastructure.Services
{
    public class FolderService : IFolderService
    {
        private readonly FolderSettings _folderSettings;

        public static string folderPath = string.Empty;
        public static string goodFolder = string.Empty;

        public FolderService(FolderSettings folderSettings)
        {
            _folderSettings = folderSettings;

            folderPath = _folderSettings.Path;
            goodFolder = _folderSettings.Good;
        }

        public void VerifyAllFolders()
        {
            VerifyFolder(folderPath, string.Empty);
            VerifyFolder(folderPath, goodFolder);
            VerifyFolder(folderPath, _folderSettings.TemporaryStorage);
            VerifyFolder(folderPath, _folderSettings.Archive); // Add archive folder verification
            VerifyFolder(folderPath, _folderSettings.StatisticsOutput); // Add statistics folder verification
            VerifyFolder(folderPath, _folderSettings.CameraStatisticsOutput);
            VerifyFolder(folderPath, _folderSettings.Duplicates); // Add duplicates folder verification
        }

        private void VerifyFolder(string path, string folderName, string date = "")
        {
            string folderPath = Path.Combine(path, folderName, date);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }
    }
}
