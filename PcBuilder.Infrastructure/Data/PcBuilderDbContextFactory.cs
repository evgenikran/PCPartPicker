using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PcBuilder.Infrastructure.Data
{
    public class PcBuilderDbContextFactory
        : IDesignTimeDbContextFactory<PcBuilderDbContext>
    {
        public PcBuilderDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PcBuilderDbContext>();

            optionsBuilder.UseSqlServer(
                "Server=DESKTOP-EQN3H9P\\MSSQLSERVER01;Database=PcBuilderDb;Trusted_Connection=True;TrustServerCertificate=True;"
            );

            return new PcBuilderDbContext(optionsBuilder.Options);
        }
    }
}
