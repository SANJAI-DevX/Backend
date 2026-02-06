using Microsoft.EntityFrameworkCore;
using URLShortener.API.Data;
using URLShortener.API.DTOs;
using URLShortener.API.Models;
using System.Security.Cryptography;
using System.Text;

namespace URLShortener.API.Services
{
    public interface IUrlService
    {
        Task<UrlResponse> CreateShortUrlAsync(CreateUrlRequest request, string baseUrl, int? userId = null);
        Task<UrlMapping?> GetByShortCodeAsync(string shortCode);
        Task<UrlStatisticsResponse?> GetStatisticsAsync(string shortCode);
        Task LogClickAsync(int urlMappingId, string? ipAddress, string? userAgent);
        Task<List<UrlMapping>> GetUserUrlsAsync(int userId);
        Task<bool> DeleteUrlAsync(string shortCode, int userId);
    }

    public class UrlService : IUrlService
    {
        private readonly ApplicationDbContext _context;
        private readonly IGeoLocationService _geoLocationService;
        private const string Characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public UrlService(ApplicationDbContext context, IGeoLocationService geoLocationService)
        {
            _context = context;
            _geoLocationService = geoLocationService;
        }

        public async Task<UrlResponse> CreateShortUrlAsync(CreateUrlRequest request, string baseUrl, int? userId = null)
        {
            var normalizedUrl = NormalizeUrl(request.OriginalUrl);

            string shortCode;
            if (!string.IsNullOrWhiteSpace(request.CustomCode))
            {
                shortCode = request.CustomCode.Trim();
                
                if (!IsValidShortCode(shortCode))
                {
                    throw new ArgumentException("Custom code must be 3-20 alphanumeric characters");
                }

                if (await _context.UrlMappings.AnyAsync(u => u.ShortCode == shortCode))
                {
                    throw new ArgumentException("This custom code is already taken");
                }
            }
            else
            {
                var existing = await _context.UrlMappings
                    .FirstOrDefaultAsync(u => u.OriginalUrl == normalizedUrl && u.UserId == userId);

                if (existing != null)
                {
                    return MapToResponse(existing, baseUrl);
                }

                shortCode = await GenerateUniqueShortCodeAsync();
            }

            var urlMapping = new UrlMapping
            {
                OriginalUrl = normalizedUrl,
                ShortCode = shortCode,
                CreatedAt = DateTime.UtcNow,
                UserId = userId
            };

            _context.UrlMappings.Add(urlMapping);
            await _context.SaveChangesAsync();

            return MapToResponse(urlMapping, baseUrl);
        }

        public async Task<List<UrlMapping>> GetUserUrlsAsync(int userId)
        {
            return await _context.UrlMappings
                .Where(u => u.UserId == userId)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> DeleteUrlAsync(string shortCode, int userId)
        {
            var urlMapping = await _context.UrlMappings
                .FirstOrDefaultAsync(u => u.ShortCode == shortCode && u.UserId == userId);

            if (urlMapping == null)
            {
                return false;
            }

            _context.UrlMappings.Remove(urlMapping);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<UrlMapping?> GetByShortCodeAsync(string shortCode)
        {
            return await _context.UrlMappings
                .FirstOrDefaultAsync(u => u.ShortCode == shortCode);
        }

        public async Task<UrlStatisticsResponse?> GetStatisticsAsync(string shortCode)
        {
            var urlMapping = await _context.UrlMappings
                .Include(u => u.ClickLogs.OrderByDescending(c => c.ClickedAt).Take(50))
                .FirstOrDefaultAsync(u => u.ShortCode == shortCode);

            if (urlMapping == null)
                return null;

            var clicksByCountry = await _context.ClickLogs
                .Where(c => c.UrlMappingId == urlMapping.Id && c.Country != null)
                .GroupBy(c => c.Country)
                .Select(g => new { Country = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToDictionaryAsync(x => x.Country, x => x.Count);

            return new UrlStatisticsResponse
            {
                Id = urlMapping.Id,
                OriginalUrl = urlMapping.OriginalUrl,
                ShortCode = urlMapping.ShortCode,
                TotalClicks = urlMapping.ClickCount,
                CreatedAt = urlMapping.CreatedAt,
                LastAccessedAt = urlMapping.LastAccessedAt,
                RecentClicks = urlMapping.ClickLogs.Select(c => new ClickLogDto
                {
                    ClickedAt = c.ClickedAt,
                    IpAddress = c.IpAddress,
                    Country = c.Country,
                    City = c.City,
                    UserAgent = c.UserAgent
                }).ToList(),
                ClicksByCountry = clicksByCountry
            };
        }

        public async Task LogClickAsync(int urlMappingId, string? ipAddress, string? userAgent)
        {
            var urlMapping = await _context.UrlMappings.FindAsync(urlMappingId);
            if (urlMapping == null)
                return;

            var (country, city) = await _geoLocationService.GetLocationAsync(ipAddress);

            var clickLog = new ClickLog
            {
                UrlMappingId = urlMappingId,
                ClickedAt = DateTime.UtcNow,
                IpAddress = ipAddress,
                Country = country,
                City = city,
                UserAgent = userAgent
            };

            _context.ClickLogs.Add(clickLog);

            urlMapping.ClickCount++;
            urlMapping.LastAccessedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        private async Task<string> GenerateUniqueShortCodeAsync()
        {
            const int maxAttempts = 10;
            const int codeLength = 7;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var shortCode = GenerateRandomCode(codeLength);
                
                if (!await _context.UrlMappings.AnyAsync(u => u.ShortCode == shortCode))
                {
                    return shortCode;
                }
            }

            return GenerateRandomCode(codeLength + 1);
        }

        private string GenerateRandomCode(int length)
        {
            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            var result = new StringBuilder(length);
            foreach (var b in bytes)
            {
                result.Append(Characters[b % Characters.Length]);
            }

            return result.ToString();
        }

        private bool IsValidShortCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length < 3 || code.Length > 20)
                return false;

            return code.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
        }

        private string NormalizeUrl(string url)
        {
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            return url.Trim();
        }

        private UrlResponse MapToResponse(UrlMapping mapping, string baseUrl)
        {
            return new UrlResponse
            {
                Id = mapping.Id,
                OriginalUrl = mapping.OriginalUrl,
                ShortCode = mapping.ShortCode,
                ShortUrl = $"{baseUrl}/{mapping.ShortCode}",
                CreatedAt = mapping.CreatedAt,
                ClickCount = mapping.ClickCount,
                LastAccessedAt = mapping.LastAccessedAt
            };
        }
    }
}