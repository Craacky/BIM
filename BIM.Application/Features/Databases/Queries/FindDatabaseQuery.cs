using AutoMapper;
using AutoMapper.QueryableExtensions;
using BIM.Application.Common.Interfaces;
using BIM.Application.Features.Databases.DTO;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BIM.Application.Features.Databases.Queries
{
    public class FindDatabaseQuery : IRequest<DatabaseListDTO>
    {
        public string FirstCode { get; set; }
        public string Name { get; set; }
        public bool IsAnother { get; set; } = false;
    }

    public class FindDatabaseQueryHandler : IRequestHandler<FindDatabaseQuery, DatabaseListDTO>
    {
        private readonly IMapper _mapper;
        private readonly ILoggerService _loggerService;
        private readonly IAppDbContext _context;

        public FindDatabaseQueryHandler(IMapper mapper, ILoggerService loggerService, IAppDbContext context)
        {
            _mapper = mapper;
            _loggerService = loggerService;
            _context = context;
        }

        public async Task<DatabaseListDTO> Handle(FindDatabaseQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var database = new DatabaseListDTO();
                if (!request.IsAnother)
                    database = await _context.DatabaseLists
                                            .Where(q => q.FirstCode == request.FirstCode
                                                && q.Name == request.Name)
                                            .ProjectTo<DatabaseListDTO>(_mapper.ConfigurationProvider)
                                            .FirstOrDefaultAsync();
                else
                    database = await _context.DatabaseLists
                                            .Where(q => q.FirstCode == request.FirstCode
                                                && q.Name != request.Name)
                                            .ProjectTo<DatabaseListDTO>(_mapper.ConfigurationProvider)
                                            .FirstOrDefaultAsync();
                return database!;
            }
            catch(Exception ex)
            {
                _loggerService.LogError($"{ex.Source} -> {ex.Message}\n{ex.StackTrace}");
                return null!;
            }
            //var database = await context.DatabaseLists
            //                            .Where(q => q.FirstCode == request.FirstCode 
            //                                && q.Name == request.Name)
            //                            .ProjectTo<DatabaseListDTO>(mapper.ConfigurationProvider)
            //                            .FirstOrDefaultAsync();
            //return database!;
        }
    }
}
