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
            HighRamGb = 32
        };

        public static WorkloadProfile VideoEditing => new WorkloadProfile
        {
            Name = "Video Editing",
            GpuBudgetRatio = 0.3m,
            CpuBudgetRatio = 0.5m,
            ValueRamGb = 32,
            HighRamGb = 64
        };

        public static WorkloadProfile AI => new WorkloadProfile
        {
            Name = "AI",
            GpuBudgetRatio = 0.7m,
            CpuBudgetRatio = 0.3m,
            ValueRamGb = 32,
            HighRamGb = 64
        };
    }
}
