using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PcBuilder.Core.Models
{
    public class BuildResult
    {
        public string BuildType { get; set; } = null!;
        public List<Part> Parts { get; set; } = new List<Part>();
        public decimal TotalPrice { get; set; }
    }
}
