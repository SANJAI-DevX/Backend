using System.ComponentModel.DataAnnotations;

namespace URLShortener.API.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string GoogleId { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? ProfilePicture { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual ICollection<UrlMapping> UrlMappings { get; set; } = new List<UrlMapping>();
    }
}