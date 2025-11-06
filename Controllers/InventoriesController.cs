using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.API.Data;
using InventoryManagement.API.Models;
using InventoryManagement.API.DTOs;
using InventoryManagement.API.Services;
using System.Security.Claims;

namespace InventoryManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICloudStorageService _cloudStorage;

    public InventoriesController(ApplicationDbContext context, ICloudStorageService cloudStorage)
    {
        _context = context;
        _cloudStorage = cloudStorage;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryDto>>> GetInventories(
        [FromQuery] string? category = null,
        [FromQuery] string? tag = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = User.Identity?.IsAuthenticated == true 
            ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!) 
            : (int?)null;

        var query = _context.Inventories
            .Include(i => i.Owner)
            .Include(i => i.Items)
            .AsQueryable();

        // Non-authenticated users can only see public inventories
        if (userId == null)
        {
            query = query.Where(i => i.IsPublic);
        }
        else
        {
            // Authenticated users can see public inventories and their own
            query = query.Where(i => i.IsPublic || i.OwnerId == userId || i.UserAccesses.Any(ua => ua.UserId == userId));
        }

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(i => i.Category == category);
        }

        if (!string.IsNullOrEmpty(tag))
        {
            query = query.Where(i => i.Tags.Contains(tag));
        }

        var total = await query.CountAsync();
        var inventories = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => MapToInventoryDto(i, userId))
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", total.ToString());
        return Ok(inventories);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<InventoryDetailDto>> GetInventory(int id)
    {
        var userId = User.Identity?.IsAuthenticated == true 
            ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!) 
            : (int?)null;

        var inventory = await _context.Inventories
            .Include(i => i.Owner)
            .Include(i => i.Items)
            .Include(i => i.CustomFields)
            .Include(i => i.UserAccesses)
                .ThenInclude(ua => ua.User)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (inventory == null)
        {
            return NotFound();
        }

        // Check access
        if (!inventory.IsPublic && userId == null)
        {
            return Unauthorized();
        }

        if (!inventory.IsPublic && 
            userId != inventory.OwnerId && 
            !inventory.UserAccesses.Any(ua => ua.UserId == userId))
        {
            return Forbid();
        }

        // Increment view count
        inventory.ViewCount++;
        await _context.SaveChangesAsync();

        var dto = MapToInventoryDetailDto(inventory, userId);
        return Ok(dto);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<InventoryDto>> CreateInventory(CreateInventoryDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        var inventory = new Inventory
        {
            Title = dto.Title,
            Description = dto.Description,
            Category = dto.Category,
            ImageUrl = dto.ImageUrl,
            IsPublic = dto.IsPublic,
            Tags = dto.Tags,
            OwnerId = userId
        };

        _context.Inventories.Add(inventory);
        await _context.SaveChangesAsync();

        var createdInventory = await _context.Inventories
            .Include(i => i.Owner)
            .Include(i => i.Items)
            .FirstAsync(i => i.Id == inventory.Id);

        return CreatedAtAction(nameof(GetInventory), new { id = inventory.Id }, MapToInventoryDto(createdInventory, userId));
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateInventory(int id, UpdateInventoryDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var isAdmin = User.IsInRole("Admin");

        var inventory = await _context.Inventories.FindAsync(id);
        if (inventory == null)
        {
            return NotFound();
        }

        // Check if user can edit
        if (!isAdmin && inventory.OwnerId != userId && 
            !await _context.UserInventoryAccesses.AnyAsync(ua => ua.InventoryId == id && ua.UserId == userId))
        {
            return Forbid();
        }

        inventory.Title = dto.Title;
        inventory.Description = dto.Description;
        inventory.Category = dto.Category;
        inventory.ImageUrl = dto.ImageUrl;
        inventory.IsPublic = dto.IsPublic;
        inventory.Tags = dto.Tags;
        inventory.CustomIdFormat = dto.CustomIdFormat;
        inventory.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The inventory was modified by another user. Please refresh and try again." });
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteInventory(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var isAdmin = User.IsInRole("Admin");

        var inventory = await _context.Inventories.FindAsync(id);
        if (inventory == null)
        {
            return NotFound();
        }

        // Only owner or admin can delete
        if (!isAdmin && inventory.OwnerId != userId)
        {
            return Forbid();
        }

        // Delete image from cloud storage if exists
        if (!string.IsNullOrEmpty(inventory.ImageUrl))
        {
            await _cloudStorage.DeleteImageAsync(inventory.ImageUrl);
        }

        _context.Inventories.Remove(inventory);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id}/upload-image")]
    [Authorize]
    public async Task<ActionResult<string>> UploadImage(int id, IFormFile file)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var isAdmin = User.IsInRole("Admin");

        var inventory = await _context.Inventories.FindAsync(id);
        if (inventory == null)
        {
            return NotFound();
        }

        // Check if user can edit
        if (!isAdmin && inventory.OwnerId != userId)
        {
            return Forbid();
        }

        // Validate file
        if (file.Length > 5 * 1024 * 1024) // 5MB limit
        {
            return BadRequest(new { message = "File size must be less than 5MB" });
        }

        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
        if (!allowedTypes.Contains(file.ContentType))
        {
            return BadRequest(new { message = "Only image files are allowed (jpg, png, gif)" });
        }

        // Delete old image if exists
        if (!string.IsNullOrEmpty(inventory.ImageUrl))
        {
            await _cloudStorage.DeleteImageAsync(inventory.ImageUrl);
        }

        // Upload new image
        var fileName = $"inventory-{id}-{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var imageUrl = await _cloudStorage.UploadImageAsync(file, fileName);

        inventory.ImageUrl = imageUrl;
        await _context.SaveChangesAsync();

        return Ok(new { imageUrl });
    }

    [HttpGet("my")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<InventoryDto>>> GetMyInventories()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        var inventories = await _context.Inventories
            .Include(i => i.Owner)
            .Include(i => i.Items)
            .Where(i => i.OwnerId == userId)
            .OrderByDescending(i => i.UpdatedAt)
            .Select(i => MapToInventoryDto(i, userId))
            .ToListAsync();

        return Ok(inventories);
    }

    [HttpGet("shared")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<InventoryDto>>> GetSharedInventories()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        var inventories = await _context.UserInventoryAccesses
            .Where(ua => ua.UserId == userId)
            .Include(ua => ua.Inventory)
                .ThenInclude(i => i.Owner)
            .Include(ua => ua.Inventory)
                .ThenInclude(i => i.Items)
            .Select(ua => MapToInventoryDto(ua.Inventory, userId))
            .ToListAsync();

        return Ok(inventories);
    }

    [HttpGet("latest")]
    public async Task<ActionResult<IEnumerable<InventoryDto>>> GetLatestInventories([FromQuery] int count = 10)
    {
        var userId = User.Identity?.IsAuthenticated == true 
            ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!) 
            : (int?)null;

        var inventories = await _context.Inventories
            .Include(i => i.Owner)
            .Include(i => i.Items)
            .Where(i => i.IsPublic)
            .OrderByDescending(i => i.CreatedAt)
            .Take(count)
            .Select(i => MapToInventoryDto(i, userId))
            .ToListAsync();

        return Ok(inventories);
    }

    [HttpGet("popular")]
    public async Task<ActionResult<IEnumerable<InventoryDto>>> GetPopularInventories([FromQuery] int count = 5)
    {
        var userId = User.Identity?.IsAuthenticated == true 
            ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!) 
            : (int?)null;

        var inventories = await _context.Inventories
            .Include(i => i.Owner)
            .Include(i => i.Items)
            .Where(i => i.IsPublic)
            .OrderByDescending(i => i.ViewCount)
            .Take(count)
            .Select(i => MapToInventoryDto(i, userId))
            .ToListAsync();

        return Ok(inventories);
    }

    [HttpGet("{id}/statistics")]
    public async Task<ActionResult<InventoryStatisticsDto>> GetInventoryStatistics(int id)
    {
        var userId = User.Identity?.IsAuthenticated == true 
            ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!) 
            : (int?)null;

        var inventory = await _context.Inventories
            .Include(i => i.Items)
                .ThenInclude(item => item.FieldValues)
            .Include(i => i.CustomFields)
            .Include(i => i.UserAccesses)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (inventory == null)
        {
            return NotFound();
        }

        // Check access
        if (!inventory.IsPublic && userId == null)
        {
            return Unauthorized();
        }

        if (!inventory.IsPublic && 
            userId != inventory.OwnerId && 
            !inventory.UserAccesses.Any(ua => ua.UserId == userId))
        {
            return Forbid();
        }

        // Calculate statistics
        var totalLikes = await _context.ItemLikes
            .Where(il => _context.Items.Any(i => i.Id == il.ItemId && i.InventoryId == id))
            .CountAsync();

        var totalComments = await _context.Comments
            .Where(c => c.InventoryId == id)
            .CountAsync();

        var fieldStatistics = new List<FieldStatistics>();
        
        foreach (var field in inventory.CustomFields)
        {
            var fieldStat = new FieldStatistics
            {
                FieldId = field.Id,
                FieldName = field.Name,
                FieldType = field.Type.ToString()
            };

            var fieldValues = inventory.Items
                .SelectMany(i => i.FieldValues)
                .Where(fv => fv.CustomFieldId == field.Id)
                .ToList();

            var totalItems = inventory.Items.Count;
            fieldStat.FilledCount = fieldValues.Count;
            fieldStat.EmptyCount = totalItems - fieldValues.Count;

            // Calculate statistics based on field type
            switch (field.Type)
            {
                case FieldType.Number:
                    var numericValues = fieldValues
                        .Where(fv => fv.NumberValue.HasValue)
                        .Select(fv => fv.NumberValue!.Value)
                        .ToList();
                    
                    if (numericValues.Any())
                    {
                        fieldStat.Average = (double)numericValues.Average();
                        fieldStat.Min = (double)numericValues.Min();
                        fieldStat.Max = (double)numericValues.Max();
                        fieldStat.Sum = (double)numericValues.Sum();
                    }
                    break;

                case FieldType.Text:
                case FieldType.MultilineText:
                    var textValues = fieldValues
                        .Where(fv => !string.IsNullOrEmpty(fv.TextValue))
                        .GroupBy(fv => fv.TextValue)
                        .Select(g => new ValueFrequency
                        {
                            Value = g.Key!,
                            Count = g.Count(),
                            Percentage = (double)g.Count() / totalItems * 100
                        })
                        .OrderByDescending(vf => vf.Count)
                        .Take(5)
                        .ToList();
                    
                    fieldStat.TopValues = textValues;
                    break;

                case FieldType.Boolean:
                    var boolValues = fieldValues
                        .Where(fv => fv.BooleanValue.HasValue)
                        .GroupBy(fv => fv.BooleanValue!.Value ? "Yes" : "No")
                        .Select(g => new ValueFrequency
                        {
                            Value = g.Key,
                            Count = g.Count(),
                            Percentage = (double)g.Count() / totalItems * 100
                        })
                        .ToList();
                    
                    fieldStat.TopValues = boolValues;
                    break;
            }

            fieldStatistics.Add(fieldStat);
        }

        var statistics = new InventoryStatisticsDto
        {
            TotalItems = inventory.Items.Count,
            TotalLikes = totalLikes,
            TotalComments = totalComments,
            ViewCount = inventory.ViewCount,
            FieldStatistics = fieldStatistics
        };

        return Ok(statistics);
    }

    [HttpPost("{id}/share")]
    [Authorize]
    public async Task<IActionResult> GrantAccess(int id, [FromBody] int userId)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var isAdmin = User.IsInRole("Admin");

        var inventory = await _context.Inventories.FindAsync(id);
        if (inventory == null)
        {
            return NotFound();
        }

        // Only owner or admin can grant access
        if (!isAdmin && inventory.OwnerId != currentUserId)
        {
            return Forbid();
        }

        // Check if user exists
        var userToShare = await _context.Users.FindAsync(userId);
        if (userToShare == null)
        {
            return BadRequest(new { message = "User not found" });
        }

        // Check if already has access
        var existingAccess = await _context.UserInventoryAccesses
            .FirstOrDefaultAsync(ua => ua.InventoryId == id && ua.UserId == userId);

        if (existingAccess != null)
        {
            return BadRequest(new { message = "User already has access to this inventory" });
        }

        // Grant access
        var access = new UserInventoryAccess
        {
            InventoryId = id,
            UserId = userId,
            GrantedAt = DateTime.UtcNow
        };

        _context.UserInventoryAccesses.Add(access);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Access granted successfully" });
    }

    [HttpDelete("{id}/share/{userId}")]
    [Authorize]
    public async Task<IActionResult> RevokeAccess(int id, int userId)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var isAdmin = User.IsInRole("Admin");

        var inventory = await _context.Inventories.FindAsync(id);
        if (inventory == null)
        {
            return NotFound();
        }

        // Only owner or admin can revoke access
        if (!isAdmin && inventory.OwnerId != currentUserId)
        {
            return Forbid();
        }

        var access = await _context.UserInventoryAccesses
            .FirstOrDefaultAsync(ua => ua.InventoryId == id && ua.UserId == userId);

        if (access == null)
        {
            return NotFound(new { message = "Access not found" });
        }

        _context.UserInventoryAccesses.Remove(access);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Access revoked successfully" });
    }

    private static InventoryDto MapToInventoryDto(Inventory inventory, int? currentUserId)
    {
        var canEdit = currentUserId.HasValue && 
            (inventory.OwnerId == currentUserId || 
             inventory.UserAccesses.Any(ua => ua.UserId == currentUserId));

        return new InventoryDto
        {
            Id = inventory.Id,
            Title = inventory.Title,
            Description = inventory.Description,
            Category = inventory.Category,
            ImageUrl = inventory.ImageUrl,
            IsPublic = inventory.IsPublic,
            Tags = inventory.Tags,
            OwnerId = inventory.OwnerId,
            OwnerUsername = inventory.Owner.Username,
            CustomIdFormat = inventory.CustomIdFormat,
            ViewCount = inventory.ViewCount,
            CreatedAt = inventory.CreatedAt,
            UpdatedAt = inventory.UpdatedAt,
            ItemCount = inventory.Items.Count,
            CanEdit = canEdit
        };
    }

    private static InventoryDetailDto MapToInventoryDetailDto(Inventory inventory, int? currentUserId)
    {
        var canEdit = currentUserId.HasValue && 
            (inventory.OwnerId == currentUserId || 
             inventory.UserAccesses.Any(ua => ua.UserId == currentUserId));

        return new InventoryDetailDto
        {
            Id = inventory.Id,
            Title = inventory.Title,
            Description = inventory.Description,
            Category = inventory.Category,
            ImageUrl = inventory.ImageUrl,
            IsPublic = inventory.IsPublic,
            Tags = inventory.Tags,
            OwnerId = inventory.OwnerId,
            OwnerUsername = inventory.Owner.Username,
            CustomIdFormat = inventory.CustomIdFormat,
            ViewCount = inventory.ViewCount,
            CreatedAt = inventory.CreatedAt,
            UpdatedAt = inventory.UpdatedAt,
            ItemCount = inventory.Items.Count,
            CanEdit = canEdit,
            CustomFields = inventory.CustomFields.OrderBy(cf => cf.DisplayOrder).Select(cf => new CustomFieldDto
            {
                Id = cf.Id,
                Name = cf.Name,
                Description = cf.Description,
                Type = cf.Type.ToString(),
                ShowInTable = cf.ShowInTable,
                DisplayOrder = cf.DisplayOrder
            }).ToList(),
            SharedWith = inventory.UserAccesses.Select(ua => new UserAccessDto
            {
                UserId = ua.UserId,
                Username = ua.User.Username,
                Email = ua.User.Email,
                GrantedAt = ua.GrantedAt
            }).ToList()
        };
    }
}
