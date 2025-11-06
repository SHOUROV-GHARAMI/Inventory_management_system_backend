using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.API.Data;
using InventoryManagement.API.Models;
using InventoryManagement.API.DTOs;
using System.Security.Claims;

namespace InventoryManagement.API.Controllers;

[ApiController]
[Route("api/inventories/{inventoryId}/fields")]
[Authorize]
public class CustomFieldsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CustomFieldsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<CustomFieldDto>>> GetFields(int inventoryId)
    {
        var inventory = await _context.Inventories
            .Include(i => i.CustomFields)
            .FirstOrDefaultAsync(i => i.Id == inventoryId);

        if (inventory == null)
        {
            return NotFound();
        }

        // Check access for private inventories
        if (!inventory.IsPublic && !User.Identity!.IsAuthenticated)
        {
            return Unauthorized();
        }

        var fields = inventory.CustomFields
            .OrderBy(cf => cf.DisplayOrder)
            .Select(cf => new CustomFieldDto
            {
                Id = cf.Id,
                Name = cf.Name,
                Description = cf.Description,
                Type = cf.Type.ToString(),
                ShowInTable = cf.ShowInTable,
                DisplayOrder = cf.DisplayOrder
            })
            .ToList();

        return Ok(fields);
    }

    [HttpPost]
    public async Task<ActionResult<CustomFieldDto>> CreateField(int inventoryId, CreateCustomFieldDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var isAdmin = User.IsInRole("Admin");

        var inventory = await _context.Inventories
            .Include(i => i.CustomFields)
            .FirstOrDefaultAsync(i => i.Id == inventoryId);

        if (inventory == null)
        {
            return NotFound();
        }

        // Check if user can edit
        if (!isAdmin && inventory.OwnerId != userId)
        {
            return Forbid();
        }

        // Parse field type
        if (!Enum.TryParse<FieldType>(dto.Type, true, out var fieldType))
        {
            return BadRequest(new { message = "Invalid field type" });
        }

        // Enforce 3-per-type limit
        var countOfType = inventory.CustomFields.Count(cf => cf.Type == fieldType);
        if (countOfType >= 3)
        {
            return BadRequest(new { message = $"Maximum 3 fields of type {dto.Type} allowed" });
        }

        // Get next display order
        var maxOrder = inventory.CustomFields.Any() 
            ? inventory.CustomFields.Max(cf => cf.DisplayOrder) 
            : -1;

        var field = new CustomField
        {
            InventoryId = inventoryId,
            Name = dto.Name,
            Description = dto.Description,
            Type = fieldType,
            ShowInTable = dto.ShowInTable,
            DisplayOrder = maxOrder + 1
        };

        _context.CustomFields.Add(field);
        await _context.SaveChangesAsync();

        var fieldDto = new CustomFieldDto
        {
            Id = field.Id,
            Name = field.Name,
            Description = field.Description,
            Type = field.Type.ToString(),
            ShowInTable = field.ShowInTable,
            DisplayOrder = field.DisplayOrder
        };

        return CreatedAtAction(nameof(GetFields), new { inventoryId }, fieldDto);
    }

    [HttpPut("{fieldId}")]
    public async Task<IActionResult> UpdateField(int inventoryId, int fieldId, UpdateCustomFieldDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var isAdmin = User.IsInRole("Admin");

        var field = await _context.CustomFields
            .Include(cf => cf.Inventory)
            .FirstOrDefaultAsync(cf => cf.Id == fieldId && cf.InventoryId == inventoryId);

        if (field == null)
        {
            return NotFound();
        }

        // Check if user can edit
        if (!isAdmin && field.Inventory.OwnerId != userId)
        {
            return Forbid();
        }

        field.Name = dto.Name;
        field.Description = dto.Description;
        field.ShowInTable = dto.ShowInTable;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{fieldId}")]
    public async Task<IActionResult> DeleteField(int inventoryId, int fieldId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var isAdmin = User.IsInRole("Admin");

        var field = await _context.CustomFields
            .Include(cf => cf.Inventory)
            .FirstOrDefaultAsync(cf => cf.Id == fieldId && cf.InventoryId == inventoryId);

        if (field == null)
        {
            return NotFound();
        }

        // Check if user can edit
        if (!isAdmin && field.Inventory.OwnerId != userId)
        {
            return Forbid();
        }

        _context.CustomFields.Remove(field);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> ReorderFields(int inventoryId, [FromBody] ReorderFieldsDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var isAdmin = User.IsInRole("Admin");

        var inventory = await _context.Inventories
            .Include(i => i.CustomFields)
            .FirstOrDefaultAsync(i => i.Id == inventoryId);

        if (inventory == null)
        {
            return NotFound();
        }

        // Check if user can edit
        if (!isAdmin && inventory.OwnerId != userId)
        {
            return Forbid();
        }

        // Update display orders
        foreach (var order in dto.FieldOrders)
        {
            var field = inventory.CustomFields.FirstOrDefault(cf => cf.Id == order.FieldId);
            if (field != null)
            {
                field.DisplayOrder = order.Order;
            }
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class CreateCustomFieldDto
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Type { get; set; } // "Text", "MultilineText", "Number", "Link", "Boolean"
    public bool ShowInTable { get; set; } = false;
}

public class UpdateCustomFieldDto
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool ShowInTable { get; set; }
}

public class ReorderFieldsDto
{
    public required List<FieldOrderDto> FieldOrders { get; set; }
}

public class FieldOrderDto
{
    public int FieldId { get; set; }
    public int Order { get; set; }
}
