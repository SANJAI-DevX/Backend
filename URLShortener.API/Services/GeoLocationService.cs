namespace URLShortener.API.Services
{
    public interface IGeoLocationService
    {
        Task<(string? Country, string? City)> GetLocationAsync(string? ipAddress);
    }

    public class GeoLocationService : IGeoLocationService
    {
        private readonly ILogger<GeoLocationService> _logger;

        public GeoLocationService(ILogger<GeoLocationService> logger)
        {
            _logger = logger;
        }

        public async Task<(string? Country, string? City)> GetLocationAsync(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return (null, null);

            try
            {
                if (IsLocalOrPrivateIp(ipAddress))
                {
                    return ("Local", "Local");
                }

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                
                var response = await httpClient.GetAsync($"http://ip-api.com/json/{ipAddress}?fields=country,city");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = System.Text.Json.JsonSerializer.Deserialize<IpApiResponse>(json);
                    
                    if (data != null && data.Status != "fail")
                    {
                        return (data.Country, data.City);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get geolocation for IP: {IpAddress}", ipAddress);
            }

            return (null, null);
        }

        private bool IsLocalOrPrivateIp(string ipAddress)
        {
            if (ipAddress == "::1" || ipAddress == "127.0.0.1" || ipAddress.StartsWith("192.168.") || 
                ipAddress.StartsWith("10.") || ipAddress.StartsWith("172.16."))
            {
                return true;
            }

            return false;
        }

        private class IpApiResponse
        {
            public string? Status { get; set; }
            public string? Country { get; set; }
            public string? City { get; set; }
        }
    }
}