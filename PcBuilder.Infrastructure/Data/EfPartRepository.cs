using System.Collections.Generic;
using System.Linq;
using PcBuilder.Core.Models;
using PcBuilder.Core.Repositories;
using PcBuilder.Infrastructure.Data;

namespace PcBuilder.Infrastructure.Repositories
{
    public class EfPartRepository : IPartRepository
    {
        private readonly PcBuilderDbContext _context;

        public EfPartRepository(PcBuilderDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Part> GetByType(string type)
        {
            return _context.Parts
                           .Where(p => p.Type == type)
                           .ToList();
        }

        public IEnumerable<Part> GetAll()
        {
            return _context.Parts.ToList();
        }
    }
}
