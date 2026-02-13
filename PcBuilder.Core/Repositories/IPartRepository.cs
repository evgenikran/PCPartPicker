using System.Collections.Generic;
using PcBuilder.Core.Models;

namespace PcBuilder.Core.Repositories
{
    public interface IPartRepository
    {
        IEnumerable<Part> GetAll();
        IEnumerable<Part> GetByType(string type);
    }
}
