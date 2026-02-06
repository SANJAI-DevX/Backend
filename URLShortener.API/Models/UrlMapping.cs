using System.ComponentModel.DataAnnotations;

namespace URLShortener.API.Models
{
    public class UrlMapping
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(2048)]
        public string OriginalUrl { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string ShortCode { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int ClickCount { get; set; } = 0;

        public DateTime? LastAccessedAt { get; set; }

        // Foreign key to User
        public int? UserId { get; set; }

        // Navigation properties
        public virtual User? User { get; set; }
        public virtual ICollection<ClickLog> ClickLogs { get; set; } = new List<ClickLog>();
    }

    public class ClickLog
    {
        [Key]
        public int Id { get; set; }

        public int UrlMappingId { get; set; }

        public DateTime ClickedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        public virtual UrlMapping UrlMapping { get; set; } = null!;
    }
}