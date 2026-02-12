using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using URLShortener.API.Data;
using URLShortener.API.DTOs;
using URLShortener.API.Models;
using Google.Apis.Auth;

namespace URLShortener.API.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> GoogleLoginAsync(string idToken);
        Task<User?> GetUserByIdAsync(int userId);
    }

    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<AuthResponse> GoogleLoginAsync(string idToken)
        {
            try
            {
                var clientId = _configuration["Google:ClientId"];

                if (string.IsNullOrEmpty(clientId))
                {
                    throw new Exception("Google Client ID not configured.");
                }

                // Validate Google token with Audience
                var payload = await GoogleJsonWebSignature.ValidateAsync(
                    idToken,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = new[] { clientId }
                    });

                // Find existing user
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.GoogleId == payload.Subject);

                if (user == null)
                {
                    user = new User
                    {
                        GoogleId = payload.Subject,
                        Email = payload.Email,
                        Name = payload.Name,
                        ProfilePicture = payload.Picture,
                        CreatedAt = DateTime.UtcNow,
                        LastLoginAt = DateTime.UtcNow
                    };

                    _context.Users.Add(user);
                }
                else
                {
                    user.LastLoginAt = DateTime.UtcNow;
                    user.Name = payload.Name;
                    user.ProfilePicture = payload.Picture;
                }

                await _context.SaveChangesAsync();

                var token = GenerateJwtToken(user);

                return new AuthResponse
                {
                    Token = token,
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Name = user.Name,
                        ProfilePicture = user.ProfilePicture
                    }
                };
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("Invalid Google token", ex);
            }
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"];

            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new Exception("JWT Key not configured.");
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name ?? string.Empty)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"] ?? "URLShortener",
                audience: _configuration["Jwt:Audience"] ?? "URLShortener",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(30),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
