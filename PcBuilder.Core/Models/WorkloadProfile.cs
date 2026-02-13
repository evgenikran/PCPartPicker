namespace PcBuilder.Core.Models
{
    public class WorkloadProfile
    {
        public string Name { get; set; }

        // Budget split (0–1 range)
        public decimal GpuBudgetRatio { get; set; }
        public decimal CpuBudgetRatio { get; set; }

        // RAM rules
        public int ValueRamGb { get; set; }
        public int HighRamGb { get; set; }
    }
}
