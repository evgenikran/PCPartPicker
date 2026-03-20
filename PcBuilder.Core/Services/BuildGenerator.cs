using System;
using System.Collections.Generic;
using System.Linq;
using PcBuilder.Core.Models;
using PcBuilder.Core.Repositories;

namespace PcBuilder.Core.Services
{
    public class BuildGenerator : IBuildGenerator
    {
        private readonly IPartRepository _repository;

        public BuildGenerator(IPartRepository repository)
        {
            _repository = repository;
        }

        public List<BuildResult> Generate(decimal budget, WorkloadProfile profile)
        {
            var gpus = _repository.GetByType("GPU").ToList();
            var cpus = _repository.GetByType("CPU").ToList();
            var rams = _repository.GetByType("RAM").ToList();
            var motherboards = _repository.GetByType("Motherboard").ToList();
            var storages = _repository.GetByType("Storage").ToList();
            var psus = _repository.GetByType("PSU").ToList();

            // Respect MaxCpuScore cap (e.g. no workstation CPUs for gaming)
            var eligibleCpus = profile.MaxCpuScore > 0
                ? cpus.Where(c => c.PerformanceScore <= profile.MaxCpuScore).ToList()
                : cpus;

            var results = new List<BuildResult>();

            // VALUE — best performance per dollar
            var valueBuild = TryBuildWithStrategy(
                budget, profile, gpus, eligibleCpus, rams, motherboards, storages, psus,
                gpuPicker: (list, cap) => list
                    .Where(g => g.Price <= cap)
                    .OrderByDescending(g => (double)g.PerformanceScore / (double)g.Price)
                    .FirstOrDefault(),
                cpuPicker: (list, cap) => list
                    .Where(c => c.Price <= cap)
                    .OrderByDescending(c => (double)c.PerformanceScore / (double)c.Price)
                    .FirstOrDefault(),
                buildType: "Value",
                requiredRamGb: profile.ValueRamGb
            );
            if (valueBuild != null) results.Add(valueBuild);

            // BALANCED — mid-tier parts
            var balancedBuild = TryBuildWithStrategy(
                budget, profile, gpus, eligibleCpus, rams, motherboards, storages, psus,
                gpuPicker: (list, cap) =>
                {
                    var filtered = list.Where(g => g.Price <= cap).OrderBy(g => g.Price).ToList();
                    return filtered.ElementAtOrDefault(filtered.Count / 2);
                },
                cpuPicker: (list, cap) =>
                {
                    var filtered = list.Where(c => c.Price <= cap).OrderBy(c => c.Price).ToList();
                    return filtered.ElementAtOrDefault(filtered.Count / 2);
                },
                buildType: "Balanced",
                requiredRamGb: profile.BalancedRamGb
            );
            if (balancedBuild != null) results.Add(balancedBuild);

            // PERFORMANCE — strongest parts within budget
            var performanceBuild = TryBuildWithStrategy(
                budget, profile, gpus, eligibleCpus, rams, motherboards, storages, psus,
                gpuPicker: (list, cap) => list
                    .Where(g => g.Price <= cap)
                    .OrderByDescending(g => g.PerformanceScore)
                    .FirstOrDefault(),
                cpuPicker: (list, cap) => list
                    .Where(c => c.Price <= cap)
                    .OrderByDescending(c => c.PerformanceScore)
                    .FirstOrDefault(),
                buildType: "Performance",
                requiredRamGb: profile.HighRamGb
            );
            if (performanceBuild != null) results.Add(performanceBuild);

            return results;
        }

        private BuildResult? TryBuildWithStrategy(
            decimal budget,
            WorkloadProfile profile,
            List<Part> gpus,
            List<Part> cpus,
            List<Part> rams,
            List<Part> motherboards,
            List<Part> storages,
            List<Part> psus,
            Func<List<Part>, decimal, Part?> gpuPicker,
            Func<List<Part>, decimal, Part?> cpuPicker,
            string buildType,
            int requiredRamGb)
        {
            decimal gpuBudget = budget * profile.GpuBudgetRatio;
            decimal cpuBudget = budget * profile.CpuBudgetRatio;
            decimal motherboardBudget = budget * 0.12m;

            // Step 1 — pick CPU first
            var cpu = cpuPicker(cpus, cpuBudget);
            if (cpu == null) return null;

            // Step 2 — pick GPU balanced against the CPU
            // Prevents mismatches like i3 + RTX 4090
            var gpu = gpus
                .Where(g => g.Price <= gpuBudget && IsBalanced(cpu, g))
                .OrderByDescending(g => (double)g.PerformanceScore / (double)g.Price)
                .FirstOrDefault();

            // Fallback: relax constraint, pick closest match by score
            if (gpu == null)
            {
                gpu = gpus
                    .Where(g => g.Price <= gpuBudget)
                    .OrderBy(g => Math.Abs(g.PerformanceScore - cpu.PerformanceScore))
                    .FirstOrDefault();
            }

            if (gpu == null) return null;

            // Step 3 — pick supporting parts
            var mb = FindCompatibleMotherboard(cpu, motherboards, motherboardBudget);
            if (mb == null) return null;

            var ram = FindCompatibleRam(rams, mb, requiredRamGb);
            var storage = FindStorage(storages, 512);
            var psu = FindCompatiblePsu(psus, CalculateRequiredWattage(cpu, gpu));
            if (ram == null || storage == null || psu == null) return null;

            // Step 4 — spend leftover budget upgrading parts
            decimal leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

            if (profile.GpuBudgetRatio >= profile.CpuBudgetRatio)
            {
                // Gaming/AI: GPU first
                gpu = UpgradeBalancedGpu(gpus, gpu, cpu, leftover) ?? gpu;
                leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

                cpu = UpgradePart(cpus, cpu, leftover) ?? cpu;
                leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);
            }
            else
            {
                // Video Editing: CPU first
                cpu = UpgradePart(cpus, cpu, leftover) ?? cpu;
                leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

                gpu = UpgradeBalancedGpu(gpus, gpu, cpu, leftover) ?? gpu;
                leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);
            }

            // Re-check MB after CPU upgrade (socket may have changed)
            mb = FindCompatibleMotherboard(cpu, motherboards, mb.Price + leftover) ?? mb;
            leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

            ram = UpgradeRam(rams, ram, mb, leftover) ?? ram;
            leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

            storage = UpgradeStorage(storages, storage, leftover) ?? storage;
            leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

            // Recalculate PSU requirements after upgrades
            psu = UpgradePsu(psus, psu, CalculateRequiredWattage(cpu, gpu), leftover) ?? psu;

            decimal totalPrice = cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price;
            if (totalPrice > budget) return null;

            return new BuildResult
            {
                BuildType = buildType,
                Parts = new List<Part> { cpu, gpu, ram, mb, storage, psu },
                TotalPrice = totalPrice
            };
        }

        // GPU/CPU score ratio must be between 0.6 and 1.8
        // Prevents extreme mismatches like i3 + RTX 4090
        private bool IsBalanced(Part cpu, Part gpu)
        {
            double ratio = (double)gpu.PerformanceScore / cpu.PerformanceScore;
            return ratio <= 1.8 && ratio >= 0.6;
        }

        // Upgrade GPU while keeping balance against CPU
        private Part? UpgradeBalancedGpu(List<Part> gpus, Part current, Part cpu, decimal leftover)
        {
            decimal maxPrice = current.Price + leftover;
            return gpus
                .Where(g => g.Price > current.Price
                         && g.Price <= maxPrice
                         && IsBalanced(cpu, g))
                .OrderByDescending(g => g.PerformanceScore)
                .FirstOrDefault() ?? current;
        }

        private Part? UpgradePart(List<Part> parts, Part current, decimal leftover)
        {
            decimal maxPrice = current.Price + leftover;
            return parts
                .Where(p => p.Price > current.Price && p.Price <= maxPrice)
                .OrderByDescending(p => p.PerformanceScore)
                .FirstOrDefault() ?? current;
        }

        private Part? UpgradeRam(List<Part> rams, Part current, Part motherboard, decimal leftover)
        {
            decimal maxPrice = current.Price + leftover;
            return rams
                .Where(r => r.RamType == motherboard.RamType
                         && r.SizeGb >= current.SizeGb
                         && r.Price > current.Price
                         && r.Price <= maxPrice)
                .OrderByDescending(r => r.PerformanceScore)
                .FirstOrDefault() ?? current;
        }

        private Part? UpgradeStorage(List<Part> storages, Part current, decimal leftover)
        {
            decimal maxPrice = current.Price + leftover;
            return storages
                .Where(s => s.CapacityGb >= current.CapacityGb
                         && s.Price > current.Price
                         && s.Price <= maxPrice)
                .OrderByDescending(s => s.PerformanceScore)
                .FirstOrDefault() ?? current;
        }

        private Part? UpgradePsu(List<Part> psus, Part current, int requiredWattage, decimal leftover)
        {
            decimal maxPrice = current.Price + leftover;
            return psus
                .Where(p => p.Wattage >= requiredWattage
                         && p.Price > current.Price
                         && p.Price <= maxPrice)
                .OrderByDescending(p => p.PerformanceScore)
                .FirstOrDefault() ?? current;
        }

        private Part? FindCompatibleMotherboard(Part cpu, IEnumerable<Part> motherboards, decimal maxPrice)
        {
            return motherboards
                .Where(m => m.Socket == cpu.Socket && m.Price <= maxPrice)
                .OrderByDescending(m => m.PerformanceScore)
                .FirstOrDefault();
        }

        private Part? FindCompatibleRam(IEnumerable<Part> rams, Part motherboard, int requiredSizeGb)
        {
            return rams
                .Where(r => r.RamType == motherboard.RamType && r.SizeGb >= requiredSizeGb)
                .OrderBy(r => r.Price)
                .FirstOrDefault();
        }

        private Part? FindStorage(IEnumerable<Part> storages, int requiredCapacityGb)
        {
            return storages
                .Where(s => s.CapacityGb >= requiredCapacityGb)
                .OrderBy(s => s.Price)
                .FirstOrDefault();
        }

        private Part? FindCompatiblePsu(IEnumerable<Part> psus, int requiredWattage)
        {
            // Extra 25% headroom on top of the already conservative wattage calculation
            int withHeadroom = (int)(requiredWattage * 1.25);
            return psus
                .Where(p => p.Wattage >= withHeadroom)
                .OrderBy(p => p.Wattage)
                .FirstOrDefault();
        }

        private int CalculateRequiredWattage(Part cpu, Part gpu)
        {
            // 1.8x accounts for real peak draw vs TDP + system overhead
            int calculated = (int)Math.Ceiling((decimal)((cpu.Wattage + gpu.Wattage) * 1.8));
            // Hard floor — no discrete GPU build ever gets less than 650W
            return Math.Max(calculated, 650);
        }
    }
}
