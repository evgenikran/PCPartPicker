namespace PcBuilder.Core.Models
{
    public class WorkloadProfile
    {
        public string Name { get; set; } = null!;

        // Budget split (0–1 range)
        public decimal GpuBudgetRatio { get; set; }
        public decimal CpuBudgetRatio { get; set; }

        // RAM targets
        public int ValueRamGb { get; set; }
        public int BalancedRamGb { get; set; }
        public int HighRamGb { get; set; }

        // CPU score cap — 0 = no cap
        public int MaxCpuScore { get; set; } = 0;

        // CPU price cap as fraction of total budget
        public decimal MaxCpuBudgetRatio { get; set; } = 0.30m;

        // GPU brand filter — null = any, "NVIDIA" = NVIDIA only
        public string? RequiredGpuBrand { get; set; } = null;

        // Minimum GPU performance score
        public int MinGpuScore { get; set; } = 0;

        // Minimum GPU VRAM in GB — critical for AI (needs 12GB+)
        public int MinGpuVram { get; set; } = 0;

        // Minimum budget for this workload
        public decimal MinimumBudget { get; set; } = 400m;

        // How much over budget the algorithm can go to find better parts
        // The output shows the real total so the user always knows actual cost
        public decimal BudgetFlexibility { get; set; } = 50m;
    }
}