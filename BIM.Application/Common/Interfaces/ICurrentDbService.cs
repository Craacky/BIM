using BIM.Application.Features.Databases.DTO;
using BIM.Application.Features.Products.DTO;

namespace BIM.Application.Common.Interfaces
{
    public interface ICurrentDbService
    {
        DatabaseListDTO CurrentDb { get; set; }
        ProductDTO CurrentProduct { get; set; }

        void AddNewDb(out bool isAdded);
        Task<(bool, string)> VerifyProduct();
        Task<int> VerifyStage1Db();
        (int, string) VerifyStage2Db(string scannedCode);
        void FinishPrint();
        void StartPrint();
        void RePrint();
    }
}
