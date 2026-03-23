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

            // SMART PICK — best combined performance per dollar
            var smartBuild = TryBuildWithStrategy(
                budget, profile, gpus, eligibleCpus, rams, motherboards, storages, psus,
                pairSelector: pairs => pairs
                    .OrderByDescending(p => (double)(p.Cpu.PerformanceScore + p.Gpu.PerformanceScore)
                                           / (double)(p.Cpu.Price + p.Gpu.Price))
                    .FirstOrDefault(),
                buildType: "Smart Pick",
                requiredRamGb: profile.ValueRamGb
            );
            if (smartBuild != null) results.Add(smartBuild);

            // TOP PICK — highest combined performance score within budget
            var topBuild = TryBuildWithStrategy(
                budget, profile, gpus, eligibleCpus, rams, motherboards, storages, psus,
                pairSelector: pairs => pairs
                    .OrderByDescending(p => p.Cpu.PerformanceScore + p.Gpu.PerformanceScore)
                    .FirstOrDefault(),
                buildType: "Top Pick",
                requiredRamGb: profile.HighRamGb
            );
            if (topBuild != null) results.Add(topBuild);

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
            Func<List<CpuGpuPair>, CpuGpuPair?> pairSelector,
            string buildType,
            int requiredRamGb)
        {
            // CPU+GPU get 80% of budget max — leaves 20% for supporting parts
            // 20% of $800 = $160 — enough for cheap MB+RAM+SSD+PSU
            // 20% of $1500 = $300 — enough for decent supporting parts
            decimal maxPrimarySpend = budget * 0.80m;

            var pairs = (
                from c in eligibleCpus
                from g in gpus
                where c.Price + g.Price <= maxPrimarySpend
                   && IsBalanced(c, g)
                select new CpuGpuPair(c, g)
            ).ToList();

            if (pairs.Count == 0) return null;

            // Strategy picks the ideal pair — but if supporting parts don't fit,
            // walk down to next best until one assembles successfully
            var orderedPairs = new List<CpuGpuPair> { pairSelector(pairs)! }
                .Concat(pairs.OrderByDescending(p => p.Cpu.PerformanceScore + p.Gpu.PerformanceScore))
                .Where(p => p != null)
                .Distinct()
                .ToList();

            foreach (var candidate in orderedPairs)
            {
                var build = TryAssemble(
                    candidate.Cpu, candidate.Gpu,
                    budget, profile,
                    eligibleCpus, rams, motherboards, storages, psus,
                    requiredRamGb, buildType);

                if (build != null) return build;
            }

            return null;
        }

        private BuildResult? TryAssemble(
            Part cpu,
            Part gpu,
            decimal budget,
            WorkloadProfile profile,
            List<Part> eligibleCpus,
            List<Part> rams,
            List<Part> motherboards,
            List<Part> storages,
            List<Part> psus,
            int requiredRamGb,
            string buildType)
        {
            decimal remaining = budget - cpu.Price - gpu.Price;

            // Supporting part budgets — proportional slices of remaining budget
            // with hard floors so cheap parts are always reachable
            decimal mbBudget = Math.Max(Math.Min(remaining * 0.38m, 220m), 65m);
            decimal storageBudget = Math.Max(Math.Min(remaining * 0.20m, 120m), 55m);
            decimal psuBudget = Math.Max(Math.Min(remaining * 0.32m, 200m), 90m);

            var mb = FindCompatibleMotherboard(cpu, motherboards, mbBudget);
            if (mb == null) return null;

            var storage = FindStorage(storages, 500, storageBudget);
            var psu = FindCompatiblePsu(psus, CalculateRequiredWattage(cpu, gpu), psuBudget);
            if (storage == null || psu == null) return null;

            // Try to find RAM at the required size — if it doesn't fit the budget,
            // fall back to the next lower tier (64→32→16) so a good CPU+GPU pair
            // isn't thrown away just because 64GB is too expensive at this budget
            Part? ram = null;
            int actualRamGb = requiredRamGb;
            foreach (int ramGb in new[] { requiredRamGb, 32, 16 })
            {
                decimal ramBudget = Math.Max(Math.Min(remaining * 0.35m, GetRamCeiling(ramGb)), GetRamFloor(ramGb));
                ram = FindCompatibleRam(rams, mb, ramGb, ramBudget);
                if (ram != null) { actualRamGb = ramGb; break; }
            }
            if (ram == null) return null;

            decimal total = cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price;
            if (total > budget) return null;

            // Spend leftover upgrading primary parts first
            decimal leftover = budget - total;

            if (profile.GpuBudgetRatio >= profile.CpuBudgetRatio)
            {
                // Gaming / AI: GPU first, then CPU
                gpu = UpgradeBalancedGpu(gpu, cpu, gpus: _repository.GetByType("GPU").ToList(), leftover) ?? gpu;
                leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

                var betterCpu = TryUpgradeCpu(cpu, eligibleCpus, motherboards, mbBudget, leftover);
                if (betterCpu != null && IsBalanced(betterCpu, gpu))
                {
                    cpu = betterCpu;
                    mb = FindCompatibleMotherboard(cpu, motherboards, mbBudget) ?? mb;
                    ram = FindCompatibleRam(rams, mb, actualRamGb, GetRamCeiling(actualRamGb)) ?? ram;
                }
                leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);
            }
            else
            {
                // Video Editing: CPU first, then GPU
                var betterCpu = TryUpgradeCpu(cpu, eligibleCpus, motherboards, mbBudget, leftover);
                if (betterCpu != null && IsBalanced(betterCpu, gpu))
                {
                    cpu = betterCpu;
                    mb = FindCompatibleMotherboard(cpu, motherboards, mbBudget) ?? mb;
                    ram = FindCompatibleRam(rams, mb, actualRamGb, GetRamCeiling(actualRamGb)) ?? ram;
                }
                leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

                gpu = UpgradeBalancedGpu(gpu, cpu, gpus: _repository.GetByType("GPU").ToList(), leftover) ?? gpu;
                leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);
            }

            // Upgrade secondary parts with remaining leftover
            decimal ramUpgradeBudget = Math.Max(Math.Min(remaining * 0.35m, GetRamCeiling(requiredRamGb)), GetRamFloor(requiredRamGb));
            ram = UpgradeRam(ram, mb, rams, Math.Min(leftover, ramUpgradeBudget - ram.Price)) ?? ram;
            leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

            storage = UpgradeStorage(storage, storages, Math.Min(leftover, storageBudget - storage.Price)) ?? storage;
            leftover = budget - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

            psu = FindCompatiblePsu(psus, CalculateRequiredWattage(cpu, gpu),
                      Math.Min(psu.Price + leftover, psuBudget)) ?? psu;

            total = cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price;
            if (total > budget) return null;

            return new BuildResult
            {
                BuildType = buildType,
                Parts = new List<Part> { cpu, gpu, ram, mb, storage, psu },
                TotalPrice = total
            };
        }

        // GPU price must be 0.5x–3.0x CPU price
        // Tighter upper bound prevents weak CPUs being paired with high-end GPUs
        // Examples:
        //   $90  i3  → GPU $45–$270  (blocks RTX 3070 at $340) ✓
        //   $150 R5  → GPU $75–$450  (allows RTX 3070) ✓
        //   $190 i5  → GPU $95–$570  (allows RTX 3080) ✓
        //   $270 R9  → GPU $135–$810 (allows RTX 4080) ✓
        private bool IsBalanced(Part cpu, Part gpu)
        {
            return gpu.Price >= cpu.Price * 0.5m
                && gpu.Price <= cpu.Price * 3.0m;
        }

        private decimal GetRamCeiling(int gb) => gb >= 64 ? 280m : gb >= 32 ? 160m : 100m;
        private decimal GetRamFloor(int gb) => gb >= 64 ? 200m : gb >= 32 ? 85m : 40m;

        // Only upgrade CPU if a compatible MB exists within budget for the new socket
        private Part? TryUpgradeCpu(Part current, List<Part> eligibleCpus, List<Part> motherboards, decimal mbBudget, decimal leftover)
        {
            if (leftover <= 0) return null;
            decimal maxPrice = current.Price + leftover;

            return eligibleCpus
                .Where(c => c.Price > current.Price && c.Price <= maxPrice)
                .OrderByDescending(c => c.PerformanceScore)
                .FirstOrDefault(c => motherboards.Any(m => m.Socket == c.Socket && m.Price <= mbBudget));
        }

        private Part? UpgradeBalancedGpu(Part current, Part cpu, List<Part> gpus, decimal leftover)
        {
            if (leftover <= 0) return current;
            decimal maxPrice = current.Price + leftover;
            return gpus
                .Where(g => g.Price > current.Price && g.Price <= maxPrice && IsBalanced(cpu, g))
                .OrderByDescending(g => g.PerformanceScore)
                .FirstOrDefault() ?? current;
        }

        private Part? UpgradeRam(Part current, Part motherboard, List<Part> rams, decimal leftover)
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

        private Part? UpgradeStorage(Part current, List<Part> storages, decimal leftover)
        {
            if (leftover <= 0) return current;
            decimal maxPrice = current.Price + leftover;

            var candidates = storages
                .Where(s => s.CapacityGb >= current.CapacityGb
                         && s.Price > current.Price
                         && s.Price <= maxPrice)
                .ToList();

            // Prefer NVMe over SATA
            return candidates
                .Where(s => s.Name.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.PerformanceScore)
                .FirstOrDefault()
                ?? candidates.OrderByDescending(s => s.PerformanceScore).FirstOrDefault()
                ?? current;
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

        private record CpuGpuPair(Part Cpu, Part Gpu);
    }
}