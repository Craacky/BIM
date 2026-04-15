using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BIM.Infrastructure.Perseverance
{
    public class ContextFactory<IContext> : IDbContextFactory<IContext> where IContext : DbContext
    {
        private readonly IServiceProvider provider;

        public ContextFactory(IServiceProvider _provider)
        {
            provider = _provider;
        }

        public IContext CreateDbContext()
        {
            if (provider == null)
                throw new InvalidOperationException("Please configure an instance of IServiceProvider");
            return ActivatorUtilities.CreateInstance<IContext>(provider);
        }
    }
}
