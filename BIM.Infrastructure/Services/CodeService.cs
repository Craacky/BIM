using BIM.Application.Common.Interfaces;
using System.Text;

namespace BIM.Infrastructure.Services
{
    public class CodeService : ICodeService
    {
        public bool CodeContainsGS(string code) => code.Contains("\u001d");
        private int FindFirstGSIndex(string code) => code.IndexOf((char)29);
        private int FindLastGSIndex(string code) => code.LastIndexOf((char)29);
        private string InfoAfterGS(string code, int start, int length) => code.Substring(start, length);
        private bool CodeContains93(string info) => info.Contains("93");
        private bool CodeContains92(string info) => info.Contains("92");
        private bool CodeContains91(string info) => info.Contains("91");
        private string InsertGSInCode(string code, int start) => code.Insert(start, "\u001d");

        public bool VerifyMeatCodes(string firstDbCode, string scannedCode, 
            int gsFirstPos, int gsSecondPos)
        {
            string cleanDb = firstDbCode.Replace("\u001d", "");
            string cleanScanned = scannedCode.Replace("\u001d", "");
            
            return cleanDb.Equals(cleanScanned, StringComparison.OrdinalIgnoreCase);

            string labelStarText = scannedCode;
            string infoAfterFirstGS = InfoAfterGS(firstDbCode, FindFirstGSIndex(firstDbCode), 3);
            string infoAfterLastGS = InfoAfterGS(firstDbCode, FindLastGSIndex(firstDbCode), 3);
            if (FindFirstGSIndex(firstDbCode) == gsFirstPos && CodeContains91(infoAfterFirstGS))
            {
                if (!CodeContainsGS(InfoAfterGS(scannedCode, gsFirstPos, 3)))
                    labelStarText = InsertGSInCode(labelStarText, gsFirstPos);
                if (FindLastGSIndex(firstDbCode) == gsSecondPos && CodeContains92(infoAfterLastGS))
                {
                    if (!CodeContainsGS(InfoAfterGS(labelStarText, gsSecondPos, 3)))
                        labelStarText = InsertGSInCode(labelStarText, gsSecondPos);
                    if (Equals(firstDbCode, labelStarText))
                        return true;
                }
            }
            return false;
        }

        public bool VerifyCodes(string firstDbCode, string scannedCode, int gsPosition)
        {
           
            string labelStarText = scannedCode;
            string infoAfterGS = InfoAfterGS(firstDbCode, FindFirstGSIndex(firstDbCode), 3);
            if (FindFirstGSIndex(firstDbCode) == gsPosition && CodeContains93(infoAfterGS))
            {
                if (!CodeContainsGS(scannedCode))
                    labelStarText = InsertGSInCode(scannedCode, gsPosition);
                if (Equals(firstDbCode, labelStarText))
                    return true;
            }
            return false;
        }

        private bool IsSequenceFound(string code, string elem, int pos,
            StringBuilder sb, int firstIndex, int secondIndex)
        {
            var findPos = code.IndexOf(elem);
            if (findPos == pos)
            {
                sb.Remove(firstIndex, 1);
                sb.Remove(secondIndex, 1);
                return true;
            }
            return false;
        }

        public string FormatCodesForReport(string code, int codeLength)
        {
            StringBuilder sb = new(code);
            if (IsSequenceFound(code, "(01)", 0, sb, 0, 2))
            {
                if (IsSequenceFound(code, "(21)", 18, sb, 16, 18))
                {
                    bool find93 = false;
                    switch (codeLength)
                    {
                        case 38:
                            find93 = IsSequenceFound(code, "(93)", 29, sb, 25, 37);
                            break;
                        case 40:
                            find93 = IsSequenceFound(code, "(93)", 31, sb, 27, 29);
                            break;
                    }
                    if (sb[^1].Equals(';'))
                        sb.Remove(sb.Length - 1, 1);
                }
            }
            return sb.ToString();
        }
    }
}