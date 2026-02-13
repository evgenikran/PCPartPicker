using System.Collections.Generic;
using PcBuilder.Core.Models;

namespace PcBuilder.Core.Repositories
{
    public class InMemoryPartRepository : IPartRepository
    {
        public IEnumerable<Part> GetAll()
        {
            return new List<Part>
            {
                new Part
                {
                    Id = 1,
                    Type = "CPU",
                    Name = "Test CPU",
                    Price = 250,
                    PerformanceScore = 70
                },
                new Part
                {
                    Id = 2,
                    Type = "GPU",
                    Name = "Test GPU",
                    Price = 500,
                    PerformanceScore = 85
                }
            };
        }

        public IEnumerable<Part> GetByType(string type)
        {
            foreach (var part in GetAll())
            {
                if (part.Type == type)
                    yield return part;
            }
        }
    }
}
