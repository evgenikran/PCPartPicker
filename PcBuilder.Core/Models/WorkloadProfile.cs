namespace PcBuilder.Core.Models
{
    public class WorkloadProfile
    {
        public string Name { get; set; } = null!;

        // Budget split (0–1 range)
        public decimal GpuBudgetRatio { get; set; }
        public decimal CpuBudgetRatio { get; set; }

        // RAM per build tier
        public int ValueRamGb { get; set; }  // Value build
        public int BalancedRamGb { get; set; }  // Balanced build
        public int HighRamGb { get; set; }  // Performance build

        // 0 = no cap, otherwise caps CPU PerformanceScore
        // Prevents workstation CPUs being picked for gaming
        public int MaxCpuScore { get; set; } = 0;
    }
}