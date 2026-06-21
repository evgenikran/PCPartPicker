namespace PcBuilder.Core.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsAdmin { get; set; } = false;

        public ICollection<SavedBuild> SavedBuilds { get; set; } = new List<SavedBuild>();
    }
}