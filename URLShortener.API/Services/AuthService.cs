public async Task<AuthResponse> GoogleLoginAsync(string idToken)
{
    try
    {
        var clientId = _configuration["Google:ClientId"];

        if (string.IsNullOrEmpty(clientId))
        {
            throw new Exception("Google Client ID not configured.");
        }

        var payload = await GoogleJsonWebSignature.ValidateAsync(
            idToken,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            });

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
