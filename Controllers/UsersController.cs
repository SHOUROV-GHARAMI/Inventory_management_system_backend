using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.API.Data;
using InventoryManagement.API.Models;
using InventoryManagement.API.DTOs;
using System.Security.Claims;

namespace InventoryManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public UsersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
    {
        var users = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                IsAdmin = u.IsAdmin,
                IsBlocked = u.IsBlocked,
                PreferredLanguage = u.PreferredLanguage,
                PreferredTheme = u.PreferredTheme
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            return NotFound();
        }

        return Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            IsAdmin = user.IsAdmin,
            IsBlocked = user.IsBlocked,
            PreferredLanguage = user.PreferredLanguage,
            PreferredTheme = user.PreferredTheme
        });
    }

    [HttpPut("me/preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UserPreferencesDto preferences)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            return NotFound();
        }

        user.PreferredLanguage = preferences.Language;
        user.PreferredTheme = preferences.Theme;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id}/block")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> BlockUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        user.IsBlocked = true;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id}/unblock")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UnblockUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        user.IsBlocked = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id}/grant-admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GrantAdmin(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        user.IsAdmin = true;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id}/revoke-admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RevokeAdmin(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        user.IsAdmin = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<UserSearchDto>>> SearchUsers(string query)
    {
        var users = await _context.Users
            .Where(u => u.Username.Contains(query) || u.Email.Contains(query))
            .Take(10)
            .Select(u => new UserSearchDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email
            })
            .ToListAsync();

        return Ok(users);
    }
}

public class UserPreferencesDto
{
    public required string Language { get; set; }
    public required string Theme { get; set; }
}

public class UserSearchDto
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
}
