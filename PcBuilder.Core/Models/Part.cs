using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PcBuilder.Core.Models
{
    public class Part
    {
        public int Id { get; set; }  // Use int for DB identity

        public string Type { get; set; } = null!;
        public string Name { get; set; } = null!;

        public decimal Price { get; set; }

        public int PerformanceScore { get; set; }

        // Compatibility / specs
        public string? Socket { get; set; }

        public string? RamType { get; set; }

        public int? SizeGb { get; set; }

        public int? CapacityGb { get; set; }

        public int? Wattage { get; set; }
    }

}
