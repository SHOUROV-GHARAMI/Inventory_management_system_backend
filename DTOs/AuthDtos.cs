namespace InventoryManagement.API.DTOs;

public class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class RegisterRequest
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class SocialLoginRequest
{
    public required string Provider { get; set; } // "Google" or "Facebook"
    public required string IdToken { get; set; }
    public required string Email { get; set; }
    public required string Username { get; set; }
    public required string ProviderId { get; set; }
}

public class AuthResponse
{
    public required string Token { get; set; }
    public required UserDto User { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsBlocked { get; set; }
    public required string PreferredLanguage { get; set; }
    public required string PreferredTheme { get; set; }
}
