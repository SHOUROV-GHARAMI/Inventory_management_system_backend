using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.API.Data;
using InventoryManagement.API.Models;
using InventoryManagement.API.DTOs;
using InventoryManagement.API.Services;
using System.Security.Cryptography;
using System.Text;

namespace InventoryManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly TokenService _tokenService;

    public AuthController(ApplicationDbContext context, TokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        // Check if user already exists
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            return BadRequest(new { message = "User with this email already exists" });
        }

        // Hash password
        var passwordHash = HashPassword(request.Password);

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = passwordHash,
            Provider = "Local",
            IsAdmin = false,
            IsBlocked = false,
            PreferredLanguage = "en",
            PreferredTheme = "light",
            LastLoginAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = _tokenService.GenerateToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            User = MapToUserDto(user)
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || user.Provider != "Local")
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        if (user.IsBlocked)
        {
            return Forbid("Your account has been blocked");
        }

        // Verify password
        if (!VerifyPassword(request.Password, user.PasswordHash!))
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var token = _tokenService.GenerateToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            User = MapToUserDto(user)
        });
    }

    [HttpPost("social-login")]
    public async Task<ActionResult<AuthResponse>> SocialLogin(SocialLoginRequest request)
    {
        // In a real application, you would verify the IdToken with the provider
        // For now, we'll trust the request

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Provider == request.Provider && u.ProviderId == request.ProviderId);

        if (user == null)
        {
            // Create new user
            user = new User
            {
                Username = request.Username,
                Email = request.Email,
                Provider = request.Provider,
                ProviderId = request.ProviderId,
                IsAdmin = false,
                IsBlocked = false,
                PreferredLanguage = "en",
                PreferredTheme = "light",
                LastLoginAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        else
        {
            if (user.IsBlocked)
            {
                return Forbid("Your account has been blocked");
            }

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        var token = _tokenService.GenerateToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            User = MapToUserDto(user)
        });
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        var passwordHash = HashPassword(password);
        return passwordHash == hash;
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            IsAdmin = user.IsAdmin,
            IsBlocked = user.IsBlocked,
            PreferredLanguage = user.PreferredLanguage,
            PreferredTheme = user.PreferredTheme
        };
    }
}
