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

            // BALANCED — 40th percentile parts
            var balancedBuild = TryBuildWithStrategy(
                budget, profile, gpus, eligibleCpus, rams, motherboards, storages, psus,
                gpuPicker: (list, cap) =>
                {
                    var filtered = list.Where(g => g.Price <= cap).OrderBy(g => g.Price).ToList();
                    return filtered.ElementAtOrDefault((int)(filtered.Count * 0.4));
                },
                cpuPicker: (list, cap) =>
                {
                    var filtered = list.Where(c => c.Price <= cap).OrderBy(c => c.Price).ToList();
                    return filtered.ElementAtOrDefault((int)(filtered.Count * 0.4));
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

            return results
                .GroupBy(r => string.Join(",", r.Parts.Select(p => p.Id).OrderBy(id => id)))
                .Select(g => g.First())
                .ToList();
        }

        private BuildResult? TryBuildWithStrategy(
            decimal budget,
            WorkloadProfile profile,
            List<Part> gpus,
            List<Part> eligibleCpus,
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

            // Flexible caps: scale with budget, no hard ceiling that kills low-budget builds
            // Uses Max() to guarantee a minimum floor so cheap parts are always reachable
            decimal motherboardBudget = Math.Max(budget * 0.12m, 70m);
            decimal maxRamBudget = Math.Max(budget * 0.09m, 60m);
            decimal maxStorageBudget = Math.Max(budget * 0.08m, 55m);
            decimal maxPsuBudget = Math.Max(budget * 0.12m, 100m);

            // High budget ceiling — prevent absurd picks on large budgets
            // e.g. $4000 budget: motherboard cap = max($480, $70) = $480 — reasonable
            // but we don't want a $550 flagship board on a $600 build
            if (budget >= 1500m)
            {
                motherboardBudget = Math.Min(motherboardBudget, 280m);
                maxRamBudget = Math.Min(maxRamBudget, 160m);
                maxStorageBudget = Math.Min(maxStorageBudget, 130m);
                maxPsuBudget = Math.Min(maxPsuBudget, 200m);
            }

            // Step 1 — pick CPU
            var cpu = cpuPicker(eligibleCpus, cpuBudget);
            if (cpu == null) return null;

            // Step 2 — filter GPUs by balance, then run the strategy picker
            var balancedGpus = gpus.Where(g => IsBalanced(cpu, g)).ToList();
            var gpu = gpuPicker(balancedGpus, gpuBudget);

            // Fallback: closest score match if no balanced GPU found
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

            var ram = FindCompatibleRam(rams, mb, requiredRamGb, maxRamBudget);
            var storage = FindStorage(storages, 240, maxStorageBudget);  // 240GB minimum
            var psu = FindCompatiblePsu(psus, CalculateRequiredWattage(cpu, gpu), maxPsuBudget);
            if (ram == null || storage == null || psu == null) return null;

            // Step 4 — spend leftover on primary parts
            decimal leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

            if (profile.GpuBudgetRatio >= profile.CpuBudgetRatio)
            {
                gpu = UpgradeBalancedGpu(gpus, gpu, cpu, leftover) ?? gpu;
                leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);
                cpu = UpgradePart(eligibleCpus, cpu, leftover) ?? cpu;
                leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);
            }
            else
            {
                cpu = UpgradePart(eligibleCpus, cpu, leftover) ?? cpu;
                leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);
                gpu = UpgradeBalancedGpu(gpus, gpu, cpu, leftover) ?? gpu;
                leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);
            }

            // Step 5 — re-pick MB after CPU upgrade
            mb = FindCompatibleMotherboard(cpu, motherboards, motherboardBudget) ?? mb;
            leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

            // Step 6 — upgrade secondary parts with remaining leftover
            ram = UpgradeRam(rams, ram, mb, Math.Min(leftover, maxRamBudget - ram.Price)) ?? ram;
            leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

            storage = UpgradeStorage(storages, storage, Math.Min(leftover, maxStorageBudget - storage.Price)) ?? storage;
            leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

            psu = FindCompatiblePsu(psus, CalculateRequiredWattage(cpu, gpu),
                      Math.Min(psu.Price + leftover, maxPsuBudget)) ?? psu;

            decimal totalPrice = cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price;
            if (totalPrice > budget) return null;

            return new BuildResult
            {
                BuildType = buildType,
                Parts = new List<Part> { cpu, gpu, ram, mb, storage, psu },
                TotalPrice = totalPrice
            };
        }

        private bool IsBalanced(Part cpu, Part gpu)
        {
            double ratio = (double)gpu.PerformanceScore / cpu.PerformanceScore;
            return ratio <= 1.6 && ratio >= 0.6;
        }

        private Part? UpgradeBalancedGpu(List<Part> gpus, Part current, Part cpu, decimal leftover)
        {
            if (leftover <= 0) return current;
            decimal maxPrice = current.Price + leftover;
            return gpus
                .Where(g => g.Price > current.Price && g.Price <= maxPrice && IsBalanced(cpu, g))
                .OrderByDescending(g => g.PerformanceScore)
                .FirstOrDefault() ?? current;
        }

        private Part? UpgradePart(List<Part> parts, Part current, decimal leftover)
        {
            if (leftover <= 0) return current;
            decimal maxPrice = current.Price + leftover;
            return parts
                .Where(p => p.Price > current.Price && p.Price <= maxPrice)
                .OrderByDescending(p => p.PerformanceScore)
                .FirstOrDefault() ?? current;
        }

        private Part? UpgradeRam(List<Part> rams, Part current, Part motherboard, decimal leftover)
        {
            if (leftover <= 0) return current;
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
            if (leftover <= 0) return current;
            decimal maxPrice = current.Price + leftover;
            return storages
                .Where(s => s.CapacityGb >= current.CapacityGb
                         && s.Price > current.Price
                         && s.Price <= maxPrice)
                .OrderByDescending(s => s.PerformanceScore)
                .FirstOrDefault() ?? current;
        }

        private Part? FindCompatibleMotherboard(Part cpu, IEnumerable<Part> motherboards, decimal maxPrice)
        {
            return motherboards
                .Where(m => m.Socket == cpu.Socket && m.Price <= maxPrice)
                .OrderByDescending(m => m.PerformanceScore)
                .FirstOrDefault();
        }

        private Part? FindCompatibleRam(IEnumerable<Part> rams, Part motherboard, int requiredSizeGb, decimal maxPrice)
        {
            return rams
                .Where(r => r.RamType == motherboard.RamType
                         && r.SizeGb >= requiredSizeGb
                         && r.Price <= maxPrice)
                .OrderBy(r => r.Price)
                .FirstOrDefault();
        }

        private Part? FindStorage(IEnumerable<Part> storages, int requiredCapacityGb, decimal maxPrice)
        {
            return storages
                .Where(s => s.CapacityGb >= requiredCapacityGb && s.Price <= maxPrice)
                .OrderBy(s => s.Price)
                .FirstOrDefault();
        }

        private Part? FindCompatiblePsu(IEnumerable<Part> psus, int requiredWattage, decimal maxPrice)
        {
            return psus
                .Where(p => p.Wattage >= requiredWattage && p.Price <= maxPrice)
                .OrderBy(p => p.Wattage)
                .FirstOrDefault();
        }

        private int CalculateRequiredWattage(Part cpu, Part gpu)
        {
            int cpuW = cpu.Wattage.GetValueOrDefault(65);
            int gpuW = gpu.Wattage.GetValueOrDefault(150);
            int calculated = (int)Math.Ceiling((cpuW + gpuW) * 1.8);
            return Math.Max(calculated, 650);
        }
    }
}