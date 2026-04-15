using AutoMapper;
using AutoMapper.QueryableExtensions;
using BIM.Application.Common.Interfaces;
using BIM.Application.Features.Products.DTO;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BIM.Application.Features.Products.Queries
{
    public class FindGTINProductQuery : IRequest<ProductDTO>
    {
        public string GTIN { get; set; }
    }

    public class FindGTINProductQueryHandler : IRequestHandler<FindGTINProductQuery, ProductDTO>
    {
        private readonly IAppDbContext _context;
        private readonly ILoggerService _loggerService;
        private readonly IMapper _mapper;

        public FindGTINProductQueryHandler(IAppDbContext context, ILoggerService loggerService, IMapper mapper)
        {
            _context = context;
            _loggerService = loggerService;
            _mapper = mapper;
        }

        public async Task<ProductDTO> Handle(FindGTINProductQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var product = await _context.Products
                                            .Where(q => q.GTIN == request.GTIN)
                                            .ProjectTo<ProductDTO>(_mapper.ConfigurationProvider)
                                            .SingleOrDefaultAsync();
                return product!;
            }
            catch(Exception ex)
            {
                _loggerService.LogError($"{ex.Source} -> {ex.Message}\n{ex.StackTrace}");
                return null!;
            }
        }
    }
}