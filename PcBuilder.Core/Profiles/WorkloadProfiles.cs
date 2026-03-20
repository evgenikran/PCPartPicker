using PcBuilder.Core.Models;

namespace PcBuilder.Core.Profiles
{
    public static class WorkloadProfiles
    {
        public static WorkloadProfile Gaming => new WorkloadProfile
        {
            Name = "Gaming",
            GpuBudgetRatio = 0.6m,
            CpuBudgetRatio = 0.4m,
            ValueRamGb = 16,
            BalancedRamGb = 16,
            HighRamGb = 32,
            // Ryzen 7 5800X / i7-13700K level is plenty for gaming
            // Caps out workstation chips like 5900X, 5950X, i9s
            MaxCpuScore = 84
        };

        public static WorkloadProfile VideoEditing => new WorkloadProfile
        {
            Name = "Video Editing",
            GpuBudgetRatio = 0.3m,
            CpuBudgetRatio = 0.5m,
            ValueRamGb = 32,   // 16GB not enough for serious editing
            BalancedRamGb = 32,
            HighRamGb = 64,
            MaxCpuScore = 0     // no cap — more cores = better for editing
        };

        public static WorkloadProfile AI => new WorkloadProfile
        {
            Name = "AI",
            GpuBudgetRatio = 0.7m,
            CpuBudgetRatio = 0.3m,
            ValueRamGb = 32,
            BalancedRamGb = 32,
            HighRamGb = 64,
            MaxCpuScore = 0     // no cap — more cores help with AI workloads
        };
    }
}