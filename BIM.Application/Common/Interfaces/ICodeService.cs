namespace BIM.Application.Common.Interfaces
{
    public interface ICodeService
    {
        bool CodeContainsGS(string code);
        bool VerifyMeatCodes(string firstDbCode, string scannedCode,
            int gsFirstPos, int gsSecondPos);
        bool VerifyCodes(string firstCode, string labelStarCode,
            int gsPosition);
        string FormatCodesForReport(string code, int codeLength);
    }
}