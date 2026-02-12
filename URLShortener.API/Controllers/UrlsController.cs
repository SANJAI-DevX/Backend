using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using URLShortener.API.DTOs;
using URLShortener.API.Services;
using System.Security.Claims;

namespace URLShortener.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UrlsController : ControllerBase
    {
        private readonly IUrlService _urlService;
        private readonly IQrCodeService _qrCodeService;
        private readonly ILogger<UrlsController> _logger;

        public UrlsController(
            IUrlService urlService,
            IQrCodeService qrCodeService,
            ILogger<UrlsController> logger)
        {
            _urlService = urlService;
            _qrCodeService = qrCodeService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<UrlResponse>> CreateShortUrl([FromBody] CreateUrlRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                int? userId = null;

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                // SAFE parsing (prevents crash when logged in)
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var parsedId))
                {
                    userId = parsedId;
                }

                var baseUrl = $"{Request.Scheme}://{Request.Host}";

                var result = await _urlService.CreateShortUrlAsync(request, baseUrl, userId);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating short URL");
                return StatusCode(500, new { error = ex.Message }); // show real error temporarily
            }
        }

        [HttpGet("my-urls")]
        [Authorize]
        public async Task<ActionResult<List<UrlResponse>>> GetMyUrls()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var urls = await _urlService.GetUserUrlsAsync(userId);

            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var response = urls.Select(u => new UrlResponse
            {
                Id = u.Id,
                OriginalUrl = u.OriginalUrl,
                ShortCode = u.ShortCode,
                ShortUrl = $"{baseUrl}/{u.ShortCode}",
                CreatedAt = u.CreatedAt,
                ClickCount = u.ClickCount,
                LastAccessedAt = u.LastAccessedAt
            }).ToList();

            return Ok(response);
        }

        [HttpDelete("{shortCode}")]
        [Authorize]
        public async Task<ActionResult> DeleteUrl(string shortCode)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var success = await _urlService.DeleteUrlAsync(shortCode, userId);

            if (!success)
            {
                return NotFound(new { error = "URL not found or you don't have permission to delete it" });
            }

            return Ok(new { message = "URL deleted successfully" });
        }

        [HttpGet("{shortCode}/stats")]
        public async Task<ActionResult<UrlStatisticsResponse>> GetStatistics(string shortCode)
        {
            var stats = await _urlService.GetStatisticsAsync(shortCode);

            if (stats == null)
                return NotFound(new { error = "Short URL not found" });

            return Ok(stats);
        }

        [HttpPost("qr-code")]
        public ActionResult GenerateQrCode([FromBody] QrCodeRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var qrCodeBytes = _qrCodeService.GenerateQrCode(request);
                return File(qrCodeBytes, "image/png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating QR code");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    [ApiController]
    [Route("")]
    public class RedirectController : ControllerBase
    {
        private readonly IUrlService _urlService;
        private readonly ILogger<RedirectController> _logger;

        public RedirectController(IUrlService urlService, ILogger<RedirectController> logger)
        {
            _urlService = urlService;
            _logger = logger;
        }

        [HttpGet("{shortCode}")]
        public async Task<IActionResult> RedirectToOriginalUrl(string shortCode)
        {
            var urlMapping = await _urlService.GetByShortCodeAsync(shortCode);

            if (urlMapping == null)
                return NotFound("Short URL not found");

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();

            _ = Task.Run(async () =>
            {
                try
                {
                    await _urlService.LogClickAsync(urlMapping.Id, ipAddress, userAgent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error logging click for {ShortCode}", shortCode);
                }
            });

            return Redirect(urlMapping.OriginalUrl);
        }
    }
}
