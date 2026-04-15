using AutoMapper;
using BIM.Application.Common.Interfaces;
using BIM.Application.Common.Mapping;
using BIM.Application.Features.Databases.DTO;
using BIM.Application.Models;
using BIM.Domain.Entities;
using MediatR;

namespace BIM.Application.Features.Databases.Commands
{
    public class AddDatabaseCommand : IMapFrom<DatabaseListDTO>, IRequest<Result<int>>
    {
        public DatabaseListDTO DatabaseListDTO { get; set; }
    }

    public class AddDatabaseCommandHandler : IRequestHandler<AddDatabaseCommand, Result<int>>
    {
        private readonly IAppDbContext _context;
        private readonly ILoggerService _loggerService;
        private readonly IMapper _mapper;

        public AddDatabaseCommandHandler(IAppDbContext context, ILoggerService loggerService, IMapper mapper)
        {
            _context = context;
            _loggerService = loggerService;
            _mapper = mapper;
        }

        public async Task<Result<int>> Handle(AddDatabaseCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var db = _mapper.Map<DatabaseList>(request.DatabaseListDTO);
                db.Id = 0;
                _context.DatabaseLists.Add(db);
                await _context.SaveChangesAsync(cancellationToken);
                return await Result<int>.SuccessAsync(db.Id);
            }
            catch(Exception ex)
            {
                _loggerService.LogError($"{ex.Source} -> {ex.Message}\n{ex.StackTrace}");
                return await Result<int>.FailAsync(null!);
            }
        }
    }
}