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
            HighRamGb = 32,   // target 32GB — BuildGenerator scales down to 16GB for tight budgets
            MaxCpuScore = 84,
            MaxCpuBudgetRatio = 0.22m,
            RequiredGpuBrand = null,
            MinGpuScore = 0,
            MinGpuVram = 0,
            MinimumBudget = 500m,
            BudgetFlexibility = 60m   // can go up to $60 over to get meaningfully better parts
        };

        public static WorkloadProfile VideoEditing => new WorkloadProfile
        {
            Name = "Video Editing",
            GpuBudgetRatio = 0.35m,
            CpuBudgetRatio = 0.45m,
            ValueRamGb = 32,
            BalancedRamGb = 32,
            HighRamGb = 32,
            MaxCpuScore = 0,
            MaxCpuBudgetRatio = 0.25m,
            RequiredGpuBrand = null,
            MinGpuScore = 0,
            MinGpuVram = 0,
            MinimumBudget = 700m,
            BudgetFlexibility = 75m   // slightly more flexibility for editing builds
        };

        public static WorkloadProfile AI => new WorkloadProfile
        {
            Name = "AI",
            GpuBudgetRatio = 0.75m,
            CpuBudgetRatio = 0.25m,
            ValueRamGb = 32,
            BalancedRamGb = 32,
            HighRamGb = 32,
            MaxCpuScore = 0,
            MaxCpuBudgetRatio = 0.15m,
            RequiredGpuBrand = "NVIDIA",
            MinGpuScore = 70,
            MinGpuVram = 12,
            MinimumBudget = 900m,
            BudgetFlexibility = 75m   // worth going over to get a better VRAM tier
        };
    }
}