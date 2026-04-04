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
            if (budget < profile.MinimumBudget)
                return new List<BuildResult>();

            var gpus = _repository.GetByType("GPU").ToList();
            var cpus = _repository.GetByType("CPU").ToList();
            var rams = _repository.GetByType("RAM").ToList();
            var motherboards = _repository.GetByType("Motherboard").ToList();
            var storages = _repository.GetByType("Storage").ToList();
            var psus = _repository.GetByType("PSU").ToList();

            var eligibleCpus = cpus
                .Where(c => profile.MaxCpuScore == 0 || c.PerformanceScore <= profile.MaxCpuScore)
                .Where(c => c.Price <= budget * profile.MaxCpuBudgetRatio)
                .ToList();

            var eligibleGpus = gpus
                .Where(g => profile.RequiredGpuBrand == null ||
                            g.Name.Contains(profile.RequiredGpuBrand, StringComparison.OrdinalIgnoreCase))
                .Where(g => g.PerformanceScore >= profile.MinGpuScore)
                .Where(g => profile.MinGpuVram == 0 || (g.SizeGb ?? 0) >= profile.MinGpuVram)
                .ToList();

            var results = new List<BuildResult>();

            // Try strict budget first, then with flexibility as fallback
            var build = TryBuild(budget, budget, profile, eligibleGpus, eligibleCpus, rams, motherboards, storages, psus)
                     ?? TryBuild(budget, budget + profile.BudgetFlexibility, profile, eligibleGpus, eligibleCpus, rams, motherboards, storages, psus);

            if (build != null) results.Add(build);
            return results;
        }

        private BuildResult? TryBuild(
            decimal userBudget,      // original budget — never changes, used for display
            decimal spendingLimit,   // actual ceiling for part selection and assembly
            WorkloadProfile profile,
            List<Part> gpus,
            List<Part> eligibleCpus,
            List<Part> rams,
            List<Part> motherboards,
            List<Part> storages,
            List<Part> psus)
        {
            // Pair selection always uses userBudget for the floor
            // but spendingLimit for the ceiling
            decimal minPrimarySpend = userBudget <= 700m ? userBudget * 0.42m : userBudget * 0.50m;
            decimal maxPrimarySpend = spendingLimit * 0.78m;

            var pairs = (
                from c in eligibleCpus
                from g in gpus
                where c.Price + g.Price >= minPrimarySpend
                   && c.Price + g.Price <= maxPrimarySpend
                   && IsBalanced(c, g)
                select new CpuGpuPair(c, g)
            ).ToList();

            if (pairs.Count == 0)
            {
                pairs = (
                    from c in eligibleCpus
                    from g in gpus
                    where c.Price + g.Price >= userBudget * 0.35m
                       && c.Price + g.Price <= maxPrimarySpend
                       && IsBalanced(c, g)
                    select new CpuGpuPair(c, g)
                ).ToList();
            }

            if (pairs.Count == 0) return null;

            CpuGpuPair? bestPair = profile.GpuBudgetRatio >= profile.CpuBudgetRatio
                ? pairs.OrderByDescending(p => p.Gpu.PerformanceScore * 2 + p.Cpu.PerformanceScore).FirstOrDefault()
                : pairs.OrderByDescending(p => p.Cpu.PerformanceScore * 2 + p.Gpu.PerformanceScore).FirstOrDefault();

            if (bestPair == null) return null;

            var orderedPairs = new List<CpuGpuPair> { bestPair }
                .Concat(pairs.OrderByDescending(p => p.Cpu.PerformanceScore + p.Gpu.PerformanceScore))
                .Distinct()
                .ToList();

            foreach (var candidate in orderedPairs)
            {
                var build = TryAssemble(candidate.Cpu, candidate.Gpu,
                    userBudget, spendingLimit, profile,
                    eligibleCpus, gpus, rams, motherboards, storages, psus);
                if (build != null) return build;
            }

            return null;
        }

        private BuildResult? TryAssemble(
            Part cpu,
            Part gpu,
            decimal userBudget,    // shown in output, used for budget-aware decisions
            decimal spendingLimit, // hard ceiling for all spending
            WorkloadProfile profile,
            List<Part> eligibleCpus,
            List<Part> eligibleGpus,
            List<Part> rams,
            List<Part> motherboards,
            List<Part> storages,
            List<Part> psus)
        {
            decimal remaining = spendingLimit - cpu.Price - gpu.Price;
            if (remaining <= 0) return null;

            bool isLowBudget = userBudget < 900m;

            decimal mbBudget = isLowBudget
                ? Math.Max(Math.Min(remaining * 0.30m, 130m), 60m)
                : Math.Max(Math.Min(remaining * 0.38m, 220m), 65m);

            decimal psuBudget = Math.Max(Math.Min(remaining * 0.35m, 220m), 90m);
            int minStorageGb = userBudget >= 900m ? 1000 : 512;
            decimal storageBudget = Math.Max(Math.Min(remaining * 0.20m, 130m), userBudget >= 900m ? 70m : 45m);

            var mb = FindCompatibleMotherboard(cpu, motherboards, mbBudget);
            if (mb == null) return null;

            var storage = FindStorage(storages, minStorageGb, storageBudget);
            var psu = FindCompatiblePsu(psus, CalculateRequiredWattage(cpu, gpu), psuBudget);
            if (storage == null || psu == null) return null;

            // Budget-aware RAM — gaming scales with budget, others use profile target
            int targetRamGb = profile.HighRamGb;
            if (profile.Name == "Gaming")
                targetRamGb = userBudget >= 700m ? 32 : 16;

            int requiredRamGb = targetRamGb;
            int actualRamGb = requiredRamGb;
            Part? ram = null;

            foreach (int ramGb in new[] { requiredRamGb, 32, 16 })
            {
                decimal ramRatio = isLowBudget ? 0.25m : 0.35m;
                decimal ramBudget = Math.Max(Math.Min(remaining * ramRatio, GetRamCeiling(ramGb)), GetRamFloor(ramGb));
                ram = FindCompatibleRam(rams, mb, ramGb, ramBudget);
                if (ram != null) { actualRamGb = ramGb; break; }
            }
            if (ram == null) return null;

            decimal total = cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price;
            if (total > spendingLimit) return null;

            // Spend leftover upgrading parts — leftover calculated against spendingLimit
            decimal leftover = spendingLimit - total;

            if (profile.GpuBudgetRatio >= profile.CpuBudgetRatio)
            {
                gpu = UpgradeBalancedGpu(gpu, cpu, eligibleGpus, leftover) ?? gpu;
                leftover = spendingLimit - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

                var betterCpu = TryUpgradeCpu(cpu, eligibleCpus, motherboards, mbBudget, leftover);
                if (betterCpu != null && IsBalanced(betterCpu, gpu))
                {
                    cpu = betterCpu;
                    mb = FindCompatibleMotherboard(cpu, motherboards, mbBudget) ?? mb;
                    ram = FindCompatibleRam(rams, mb, actualRamGb, GetRamCeiling(actualRamGb)) ?? ram;
                }
                leftover = spendingLimit - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);
            }
            else
            {
                var betterCpu = TryUpgradeCpu(cpu, eligibleCpus, motherboards, mbBudget, leftover);
                if (betterCpu != null && IsBalanced(betterCpu, gpu))
                {
                    cpu = betterCpu;
                    mb = FindCompatibleMotherboard(cpu, motherboards, mbBudget) ?? mb;
                    ram = FindCompatibleRam(rams, mb, actualRamGb, GetRamCeiling(actualRamGb)) ?? ram;
                }
                leftover = spendingLimit - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

                gpu = UpgradeBalancedGpu(gpu, cpu, eligibleGpus, leftover) ?? gpu;
                leftover = spendingLimit - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);
            }

            decimal ramRatioCap = isLowBudget ? 0.25m : 0.35m;
            decimal ramUpgradeCap = Math.Max(Math.Min(remaining * ramRatioCap, GetRamCeiling(requiredRamGb)), GetRamFloor(requiredRamGb));
            ram = UpgradeRam(ram, mb, rams, Math.Min(leftover, ramUpgradeCap - ram.Price)) ?? ram;
            leftover = spendingLimit - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

            storage = UpgradeStorage(storage, storages, Math.Min(leftover, storageBudget - storage.Price)) ?? storage;
            leftover = spendingLimit - (cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price);

            psu = FindCompatiblePsu(psus, CalculateRequiredWattage(cpu, gpu),
                      Math.Min(psu.Price + leftover, psuBudget)) ?? psu;

            total = cpu.Price + gpu.Price + mb.Price + ram.Price + storage.Price + psu.Price;
            if (total > spendingLimit) return null;

            return new BuildResult
            {
                BuildType = "Recommended Build",
                Parts = new List<Part> { cpu, gpu, ram, mb, storage, psu },
                TotalPrice = total
            };
        }

        private bool IsBalanced(Part cpu, Part gpu)
        {
            return gpu.Price >= cpu.Price * 0.5m
                && gpu.Price <= cpu.Price * 3.0m;
        }

        private decimal GetRamCeiling(int gb) => gb >= 64 ? 280m : gb >= 32 ? 160m : 100m;
        private decimal GetRamFloor(int gb) => gb >= 64 ? 200m : gb >= 32 ? 85m : 40m;

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