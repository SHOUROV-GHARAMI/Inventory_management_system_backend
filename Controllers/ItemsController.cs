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
[Route("api")]
public class ItemsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICustomIdService _customIdService;

    public ItemsController(ApplicationDbContext context, ICustomIdService customIdService)
    {
        _context = context;
        _customIdService = customIdService;
    }

    [HttpGet("inventories/{inventoryId}/items")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ItemDto>>> GetItems(
        int inventoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userId = User.Identity?.IsAuthenticated == true
            ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!)
            : (int?)null;

        var inventory = await _context.Inventories
            .Include(i => i.CustomFields)
            .FirstOrDefaultAsync(i => i.Id == inventoryId);

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
            !await _context.UserInventoryAccesses.AnyAsync(ua => ua.InventoryId == inventoryId && ua.UserId == userId))
        {
            return Forbid();
        }

        var query = _context.Items
            .Include(i => i.CreatedByUser)
            .Include(i => i.FieldValues)
                .ThenInclude(fv => fv.CustomField)
            .Include(i => i.Likes)
            .Where(i => i.InventoryId == inventoryId);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var itemDtos = items.Select(i => MapToItemDto(i, userId, inventory.CustomFields.ToList())).ToList();

        Response.Headers.Append("X-Total-Count", total.ToString());
        return Ok(itemDtos);
    }

    [HttpGet("items/{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ItemDto>> GetItem(int id)
    {
        var userId = User.Identity?.IsAuthenticated == true
            ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!)
            : (int?)null;

        var item = await _context.Items
            .Include(i => i.Inventory)
            .Include(i => i.CreatedByUser)
            .Include(i => i.FieldValues)
                .ThenInclude(fv => fv.CustomField)
            .Include(i => i.Likes)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
        {
            return NotFound();
        }

        // Check access
        if (!item.Inventory.IsPublic && userId == null)
        {
            return Unauthorized();
        }

        if (!item.Inventory.IsPublic && 
            userId != item.Inventory.OwnerId && 
            !await _context.UserInventoryAccesses.AnyAsync(ua => ua.InventoryId == item.InventoryId && ua.UserId == userId))
        {
            return Forbid();
        }

        var fields = await _context.CustomFields
            .Where(cf => cf.InventoryId == item.InventoryId)
            .ToListAsync();

        var itemDto = MapToItemDto(item, userId, fields);
        return Ok(itemDto);
    }

    [HttpPost("inventories/{inventoryId}/items")]
    [Authorize]
    public async Task<ActionResult<ItemDto>> CreateItem(int inventoryId, CreateItemDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var isAdmin = User.IsInRole("Admin");

        var inventory = await _context.Inventories
            .Include(i => i.CustomFields)
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == inventoryId);

        if (inventory == null)
        {
            return NotFound();
        }

        // Check if user can add items
        var canEdit = isAdmin || 
                     inventory.OwnerId == userId || 
                     (inventory.IsPublic && User.Identity!.IsAuthenticated) ||
                     await _context.UserInventoryAccesses.AnyAsync(ua => ua.InventoryId == inventoryId && ua.UserId == userId);

        if (!canEdit)
        {
            return Forbid();
        }

        // Generate custom ID
        var customId = _customIdService.GenerateId(inventory.CustomIdFormat ?? "", inventoryId);
        
        // Ensure uniqueness
        var attempts = 0;
        while (await _context.Items.AnyAsync(i => i.InventoryId == inventoryId && i.CustomId == customId) && attempts < 10)
        {
            customId = _customIdService.GenerateId(inventory.CustomIdFormat ?? "", inventoryId);
            attempts++;
        }

        if (attempts >= 10)
        {
            return BadRequest(new { message = "Failed to generate unique ID" });
        }

        var item = new Item
        {
            InventoryId = inventoryId,
            CustomId = customId,
            CreatedByUserId = userId
        };

        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        // Add field values
        foreach (var kvp in dto.FieldValues)
        {
            var field = inventory.CustomFields.FirstOrDefault(cf => cf.Id == kvp.Key);
            if (field == null) continue;

            var fieldValue = new ItemFieldValue
            {
                ItemId = item.Id,
                CustomFieldId = kvp.Key
            };

            // Parse value based on field type
            switch (field.Type)
            {
                case FieldType.Text:
                case FieldType.MultilineText:
                case FieldType.Link:
                    fieldValue.TextValue = kvp.Value;
                    break;
                case FieldType.Number:
                    if (decimal.TryParse(kvp.Value, out var numValue))
                    {
                        fieldValue.NumberValue = numValue;
                    }
                    break;
                case FieldType.Boolean:
                    if (bool.TryParse(kvp.Value, out var boolValue))
                    {
                        fieldValue.BooleanValue = boolValue;
                    }
                    break;
            }

            _context.ItemFieldValues.Add(fieldValue);
        }

        await _context.SaveChangesAsync();

        // Reload item with all relations
        var createdItem = await _context.Items
            .Include(i => i.CreatedByUser)
            .Include(i => i.FieldValues)
                .ThenInclude(fv => fv.CustomField)
            .Include(i => i.Likes)
            .FirstAsync(i => i.Id == item.Id);

        var itemDto = MapToItemDto(createdItem, userId, inventory.CustomFields.ToList());
        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, itemDto);
    }

    [HttpPut("items/{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateItem(int id, UpdateItemDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var isAdmin = User.IsInRole("Admin");

        var item = await _context.Items
            .Include(i => i.Inventory)
                .ThenInclude(inv => inv.CustomFields)
            .Include(i => i.FieldValues)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
        {
            return NotFound();
        }

        // Check if user can edit
        var canEdit = isAdmin || 
                     item.Inventory.OwnerId == userId || 
                     item.CreatedByUserId == userId ||
                     await _context.UserInventoryAccesses.AnyAsync(ua => ua.InventoryId == item.InventoryId && ua.UserId == userId);

        if (!canEdit)
        {
            return Forbid();
        }

        // Update field values
        foreach (var kvp in dto.FieldValues)
        {
            var field = item.Inventory.CustomFields.FirstOrDefault(cf => cf.Id == kvp.Key);
            if (field == null) continue;

            var existingValue = item.FieldValues.FirstOrDefault(fv => fv.CustomFieldId == kvp.Key);
            
            if (existingValue == null)
            {
                existingValue = new ItemFieldValue
                {
                    ItemId = item.Id,
                    CustomFieldId = kvp.Key
                };
                _context.ItemFieldValues.Add(existingValue);
            }

            // Clear all values first
            existingValue.TextValue = null;
            existingValue.NumberValue = null;
            existingValue.BooleanValue = null;

            // Set new value based on field type
            switch (field.Type)
            {
                case FieldType.Text:
                case FieldType.MultilineText:
                case FieldType.Link:
                    existingValue.TextValue = kvp.Value;
                    break;
                case FieldType.Number:
                    if (decimal.TryParse(kvp.Value, out var numValue))
                    {
                        existingValue.NumberValue = numValue;
                    }
                    break;
                case FieldType.Boolean:
                    if (bool.TryParse(kvp.Value, out var boolValue))
                    {
                        existingValue.BooleanValue = boolValue;
                    }
                    break;
            }
        }

        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("items/{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteItem(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var isAdmin = User.IsInRole("Admin");

        var item = await _context.Items
            .Include(i => i.Inventory)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
        {
            return NotFound();
        }

        // Check if user can delete
        var canDelete = isAdmin || 
                       item.Inventory.OwnerId == userId || 
                       item.CreatedByUserId == userId;

        if (!canDelete)
        {
            return Forbid();
        }

        _context.Items.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("items/{id}/like")]
    [Authorize]
    public async Task<IActionResult> LikeItem(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        var item = await _context.Items
            .Include(i => i.Likes)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
        {
            return NotFound();
        }

        // Check if already liked
        if (item.Likes.Any(l => l.UserId == userId))
        {
            return BadRequest(new { message = "Already liked" });
        }

        var like = new ItemLike
        {
            ItemId = id,
            UserId = userId
        };

        _context.ItemLikes.Add(like);
        await _context.SaveChangesAsync();

        return Ok(new { likesCount = item.Likes.Count + 1 });
    }

    [HttpDelete("items/{id}/like")]
    [Authorize]
    public async Task<IActionResult> UnlikeItem(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        var like = await _context.ItemLikes
            .FirstOrDefaultAsync(l => l.ItemId == id && l.UserId == userId);

        if (like == null)
        {
            return NotFound();
        }

        _context.ItemLikes.Remove(like);
        await _context.SaveChangesAsync();

        var likesCount = await _context.ItemLikes.CountAsync(l => l.ItemId == id);
        return Ok(new { likesCount });
    }

    private static ItemDto MapToItemDto(Item item, int? currentUserId, List<CustomField> allFields)
    {
        var fieldValues = new Dictionary<int, ItemFieldValueDto>();

        foreach (var field in allFields)
        {
            var value = item.FieldValues.FirstOrDefault(fv => fv.CustomFieldId == field.Id);
            
            string? valueStr = null;
            if (value != null)
            {
                valueStr = field.Type switch
                {
                    FieldType.Text => value.TextValue,
                    FieldType.MultilineText => value.TextValue,
                    FieldType.Link => value.TextValue,
                    FieldType.Number => value.NumberValue?.ToString(),
                    FieldType.Boolean => value.BooleanValue?.ToString(),
                    _ => null
                };
            }

            fieldValues[field.Id] = new ItemFieldValueDto
            {
                FieldId = field.Id,
                FieldName = field.Name,
                FieldType = field.Type.ToString(),
                Value = valueStr
            };
        }

        return new ItemDto
        {
            Id = item.Id,
            CustomId = item.CustomId,
            InventoryId = item.InventoryId,
            CreatedByUserId = item.CreatedByUserId,
            CreatedByUsername = item.CreatedByUser.Username,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            LikesCount = item.Likes.Count,
            IsLikedByCurrentUser = currentUserId.HasValue && item.Likes.Any(l => l.UserId == currentUserId),
            FieldValues = fieldValues
        };
    }
}
