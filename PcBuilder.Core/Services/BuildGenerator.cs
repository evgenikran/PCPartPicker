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
            // simple gaming split
            decimal gpuBudget = budget * profile.GpuBudgetRatio;
            decimal cpuBudget = budget * profile.CpuBudgetRatio;
            decimal motherboardBudget = budget * 0.15m;



            var gpus = _repository.GetByType("GPU").ToList();
            var cpus = _repository.GetByType("CPU").ToList();
            var rams = _repository.GetByType("RAM").ToList();
            var motherboards = _repository.GetByType("Motherboard").ToList();
            var storages = _repository.GetByType("Storage").ToList();
            var psus = _repository.GetByType("PSU").ToList();






            var results = new List<BuildResult>();

            // VALUE build: best performance per price
            var valueGpu = gpus
                .Where(g => g.Price <= gpuBudget)
                .OrderByDescending(g => g.PerformanceScore / g.Price)
                .FirstOrDefault();

            var valueCpu = cpus
                .Where(c => c.Price <= cpuBudget)
                .OrderByDescending(c => c.PerformanceScore / c.Price)
                .FirstOrDefault();


            var valueMb = FindCompatibleMotherboard(
                valueCpu,
                motherboards,
                motherboardBudget);

            var valueRam = FindCompatibleRam(
                rams,
                valueMb,
                profile.ValueRamGb);

            

            // 🔽 THEN SELECT PSU
            var valuePsu = FindCompatiblePsu(
                psus,
                CalculateRequiredWattage(valueCpu,valueGpu));

            var valueStorage = FindStorage(storages, 512);


            if (valueGpu != null && valueCpu != null && valueMb != null && valueRam != null && valueStorage != null && valuePsu != null)
            {
                var valueBuild = CreateBuild(
                    "Value",
                    valueCpu,
                    valueGpu,
                    valueRam,
                    valueMb,
                    valueStorage,
                    valuePsu,
                    budget);

                if (valueBuild != null)
                    results.Add(valueBuild);
            }


            // BALANCED build: middle of the pack
            var filteredGpus = gpus
             .Where(g => g.Price <= gpuBudget)
             .OrderBy(g => g.Price)
             .ToList();

            var balancedGpu = filteredGpus
                .Skip(filteredGpus.Count / 2)
                .FirstOrDefault();


            var balancedCpu = cpus
                .Where(c => c.Price <= cpuBudget)
                .OrderBy(c => c.Price)
                .Skip(cpus.Count / 2)
                .FirstOrDefault();

            var balancedMb = FindCompatibleMotherboard(
                valueCpu,
                motherboards,
                motherboardBudget);

            var balancedRam = FindCompatibleRam(
                rams,
                balancedMb,
                profile.HighRamGb);
            
            var balancedPsu = FindCompatiblePsu(
                psus,
                CalculateRequiredWattage(balancedCpu, balancedGpu));

            var balancedStorage = FindStorage(storages, 512);


            if (balancedGpu != null && balancedCpu != null && balancedMb != null && balancedRam != null && balancedStorage!= null && balancedPsu != null)
            {
                var balancedBuild = CreateBuild(
                   "Balanced",
                   balancedCpu,
                   balancedGpu,
                   balancedRam,
                   balancedMb,
                   balancedStorage,
                   balancedPsu,
                   budget);

                if (balancedBuild != null)
                    results.Add(balancedBuild);


            }

            // PERFORMANCE build: strongest parts within budget
            var performanceGpu = gpus
                .Where(g => g.Price <= gpuBudget)
                .OrderByDescending(g => g.PerformanceScore)
                .FirstOrDefault();

            var performanceCpu = cpus
                .Where(c => c.Price <= cpuBudget)
                .OrderByDescending(c => c.PerformanceScore)
                .FirstOrDefault();
            var performanceMb = FindCompatibleMotherboard(
                valueCpu,
                motherboards,
                motherboardBudget);

            var performanceRam = FindCompatibleRam(
                rams,
                performanceMb,
                profile.HighRamGb);

            var performancePsu = FindCompatiblePsu(
                psus,
                CalculateRequiredWattage(performanceCpu, performanceGpu));

            var performanceStorage = FindStorage(storages, 512);

            if (performanceGpu != null && performanceCpu != null && performanceMb != null && performanceRam != null && performanceStorage != null && performancePsu != null)
            {

                var performanceBuild = CreateBuild(
                  "Performance",
                  performanceCpu,
                  performanceGpu,
                  performanceRam,
                  performanceMb,
                  performanceStorage,
                  performancePsu,
                  budget);

                if (performanceBuild != null)
                    results.Add(performanceBuild);


            }

            return results;
        }



        private BuildResult? CreateBuild(
            string type,
            Part cpu,
            Part gpu,
            Part ram,
            Part motherboard,
            Part storage,
            Part psu,
            decimal totalBudget)

        {
            decimal totalPrice = cpu.Price + gpu.Price + ram.Price + motherboard.Price + storage.Price + psu.Price;


            if (totalPrice > totalBudget) 
                    return null;

                return new BuildResult
                {
                    BuildType = type,
                    Parts = new List<Part> { cpu, gpu, ram , motherboard, storage,psu},
                  
                    TotalPrice = totalPrice
                };


        }

        private Part? FindCompatibleMotherboard(
        Part cpu,
        IEnumerable<Part> motherboards,
        decimal maxPrice)
        {
            return motherboards
                .Where(m => m.Socket == cpu.Socket && m.Price <= maxPrice)
                .OrderByDescending(m => m.PerformanceScore)
                .FirstOrDefault();
        }

        private Part? FindCompatibleRam(
            IEnumerable<Part> rams,
            Part motherboard,
            int requiredSizeGb)
                {
                    return rams
                        .Where(r =>
                            r.RamType == motherboard.RamType &&
                            r.SizeGb >= requiredSizeGb)
                        .OrderBy(r => r.Price)
                        .FirstOrDefault();
                }

        private Part? FindStorage(
            IEnumerable<Part> storages,
            int requiredCapacityGb)
                {
                   return storages
                       .Where(s => s.CapacityGb >= requiredCapacityGb)
                       .OrderBy(s => s.Price)
                       .FirstOrDefault();
                }


        private Part? FindCompatiblePsu(
        IEnumerable<Part> psus,
        int requiredWattage)
            {
                return psus
                .Where(p => p.Wattage >= requiredWattage)
                .OrderBy(p => p.Wattage)
                .FirstOrDefault();
        }

        private int CalculateRequiredWattage(Part cpu, Part gpu)
        {
            return (int)Math.Ceiling((decimal)((cpu.Wattage + gpu.Wattage) * 1.4));
        }


    }

}
