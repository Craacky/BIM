using BIM.Application.Common.Interfaces;
using BIM.Application.Common.Mapping;
using BIM.Application.Features.Databases.DTO;
using BIM.Application.Models;
using BIM.Domain.Enums;
using MediatR;

namespace BIM.Application.Features.Databases.Commands
{
    public class ChangeDbStatusCommand : IMapFrom<DatabaseListDTO>, IRequest<Result>
    {
        public int Id { get; set; }
        public DbStatus DbStatus { get; set; }
    }

    public class ChangeDbStatusCommandHandler : IRequestHandler<ChangeDbStatusCommand, Result>
    {
        private readonly IAppDbContext _context;
        private readonly ILoggerService _loggerService;

        public ChangeDbStatusCommandHandler(IAppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        public async Task<Result> Handle(ChangeDbStatusCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var db = _context.DatabaseLists.SingleOrDefault(q => q.Id == request.Id) ?? throw new Exception($"Report {request.Id} not found");
                db.Status = request.DbStatus;
                _context.DatabaseLists.Update(db);
                await _context.SaveChangesAsync(cancellationToken);
                return await Result.SuccessAsync();
            }
            catch(Exception ex)
            {
                _loggerService.LogError($"{ex.Source} -> {ex.Message}\n{ex.StackTrace}");
                return await Result.FailAsync(null!);
            }
        }
    }
}
