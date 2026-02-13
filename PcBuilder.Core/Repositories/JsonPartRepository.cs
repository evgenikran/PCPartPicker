using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PcBuilder.Core.Models;

namespace PcBuilder.Core.Repositories
{
    public class JsonPartRepository : IPartRepository
    {
        private readonly List<Part> _parts;

        public JsonPartRepository(string filePath)
        {
            var json = File.ReadAllText(filePath);
            _parts = JsonSerializer.Deserialize<List<Part>>(json)
                     ?? new List<Part>();
        }

        public IEnumerable<Part> GetAll()
        {
            return _parts;
        }

        public IEnumerable<Part> GetByType(string type)
        {
            foreach (var part in _parts)
            {
                if (part.Type == type)
                    yield return part;
            }
        }
    }
}
