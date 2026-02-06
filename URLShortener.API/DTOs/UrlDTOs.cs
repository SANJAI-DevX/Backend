using System.ComponentModel.DataAnnotations;

namespace URLShortener.API.DTOs
{
    public class CreateUrlRequest
    {
        [Required]
        [Url(ErrorMessage = "Please provide a valid URL")]
        [MaxLength(2048)]
        public string OriginalUrl { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? CustomCode { get; set; }
    }

    public class UrlResponse
    {
        public int Id { get; set; }
        public string OriginalUrl { get; set; } = string.Empty;
        public string ShortCode { get; set; } = string.Empty;
        public string ShortUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int ClickCount { get; set; }
        public DateTime? LastAccessedAt { get; set; }
    }

    public class QrCodeRequest
    {
        [Required]
        public string Url { get; set; } = string.Empty;

        [Range(100, 1000)]
        public int Size { get; set; } = 300;

        public QrCodeErrorCorrectionLevel ErrorCorrectionLevel { get; set; } = QrCodeErrorCorrectionLevel.M;
    }

    public enum QrCodeErrorCorrectionLevel
    {
        L = 0,
        M = 1,
        Q = 2,
        H = 3
    }

    public class UrlStatisticsResponse
    {
        public int Id { get; set; }
        public string OriginalUrl { get; set; } = string.Empty;
        public string ShortCode { get; set; } = string.Empty;
        public int TotalClicks { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastAccessedAt { get; set; }
        public List<ClickLogDto> RecentClicks { get; set; } = new();
        public Dictionary<string, int> ClicksByCountry { get; set; } = new();
    }

    public class ClickLogDto
    {
        public DateTime ClickedAt { get; set; }
        public string? IpAddress { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? UserAgent { get; set; }
    }
}