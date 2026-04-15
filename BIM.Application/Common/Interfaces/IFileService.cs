namespace BIM.Application.Common.Interfaces
{
    public interface IFileService
    {
        string FileName { get; set; }
        string FilePath { get; set; }
        IEnumerable<string> FileText { get; set; }
        string LastPrintedCode { get; set; }

        void CopyFileToLabelStarFolder();
        void MoveFileToWorkFolder(string selectedFilePath);
        void MoveFileToArchive();
        void MoveFileToDuplicates();
        void DeleteBackupFiles();
        void SaveValidatedFileToGoodCodesFolder();
        bool IsFileContainsDupes(IEnumerable<string> lines);
        bool IsLastCodeFounded(string code);
        //void TakeCodesForReprint(string code);
        //IEnumerable<string> GetInfoFromLastLogFile();

        //(bool,int) WriteReportFile(string verifyCode);
    }
}
