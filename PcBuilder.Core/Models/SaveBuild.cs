namespace PcBuilder.Core.Models
{
    public class SavedBuild
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = "My Build";
        public string Workload { get; set; } = null!;
        public decimal Budget { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string PartsJson { get; set; } = null!;

        public User User { get; set; } = null!;
    }
}