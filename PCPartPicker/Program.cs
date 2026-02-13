using System;
using PcBuilder.Core.Repositories;
using PcBuilder.Core.Services;
using PcBuilder.Core.Profiles;
using PcBuilder.Core.Models;

namespace PCBuilder.App
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== PC Builder ===");
            Console.WriteLine();

            decimal budget = ReadBudget();
            WorkloadProfile profile = ReadWorkload();

            IPartRepository repository =
                new JsonPartRepository("Data/parts.json");

            IBuildGenerator generator =
                new BuildGenerator(repository);

            var builds = generator.Generate(budget, profile);

            Console.WriteLine();
            Console.WriteLine("=== Recommended Builds ===");
            Console.WriteLine();

            foreach (var build in builds)
            {
                Console.WriteLine($"Build type: {build.BuildType}");
                Console.WriteLine($"Total price: {build.TotalPrice}");

                foreach (var part in build.Parts)
                {
                    Console.WriteLine(
                        $"- {part.Type}: {part.Name} (${part.Price})");
                }

                Console.WriteLine();
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static decimal ReadBudget()
        {
            while (true)
            {
                Console.Write("Enter your budget: ");

                if (decimal.TryParse(Console.ReadLine(), out decimal budget)
                    && budget > 0)
                {
                    return budget;
                }

                Console.WriteLine("Invalid budget. Try again.");
            }
        }

        static WorkloadProfile ReadWorkload()
        {
            Console.WriteLine();
            Console.WriteLine("Select workload:");
            Console.WriteLine("1 - Gaming");
            Console.WriteLine("2 - Video Editing");
            Console.WriteLine("3 - AI");

            while (true)
            {
                Console.Write("Choice: ");
                string input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        return WorkloadProfiles.Gaming;
                    case "2":
                        return WorkloadProfiles.VideoEditing;
                    case "3":
                        return WorkloadProfiles.AI;
                }

                Console.WriteLine("Invalid choice. Try again.");
            }
        }
    }
}
