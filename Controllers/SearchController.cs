using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.API.Data;
using InventoryManagement.API.DTOs;
using System.Security.Claims;

namespace InventoryManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SearchController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<SearchResultsDto>> Search(
        [FromQuery] string q,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "Search query is required" });
        }

        var userId = User.Identity?.IsAuthenticated == true
            ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!)
            : (int?)null;

        var results = new SearchResultsDto
        {
            Query = q,
            Inventories = new List<InventorySearchResult>(),
            Items = new List<ItemSearchResult>()
        };

        // Search inventories if type is null or "inventory"
        if (type == null || type.Equals("inventory", StringComparison.OrdinalIgnoreCase))
        {
            results.Inventories = await SearchInventories(q, userId, page, pageSize);
        }

        // Search items if type is null or "item"
        if (type == null || type.Equals("item", StringComparison.OrdinalIgnoreCase))
        {
            results.Items = await SearchItems(q, userId, page, pageSize);
        }

        return Ok(results);
    }

    [HttpGet("inventories")]
    public async Task<ActionResult<IEnumerable<InventorySearchResult>>> SearchInventories(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "Search query is required" });
        }

        var userId = User.Identity?.IsAuthenticated == true
            ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!)
            : (int?)null;

        var results = await SearchInventories(q, userId, page, pageSize);
        return Ok(results);
    }

    [HttpGet("items")]
    public async Task<ActionResult<IEnumerable<ItemSearchResult>>> SearchItems(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "Search query is required" });
        }

        var userId = User.Identity?.IsAuthenticated == true
            ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!)
            : (int?)null;

        var results = await SearchItems(q, userId, page, pageSize);
        return Ok(results);
    }

    private async Task<List<InventorySearchResult>> SearchInventories(string query, int? userId, int page, int pageSize)
    {
        var searchTerm = query.ToLower();

        var inventoryQuery = _context.Inventories
            .Include(i => i.Owner)
            .Include(i => i.Items)
            .Where(i => i.IsPublic || (userId.HasValue && (i.OwnerId == userId || i.UserAccesses.Any(ua => ua.UserId == userId))))
            .Where(i =>
                i.Title.ToLower().Contains(searchTerm) ||
                i.Description.ToLower().Contains(searchTerm) ||
                i.Category.ToLower().Contains(searchTerm) ||
                i.Tags.ToLower().Contains(searchTerm));

        var total = await inventoryQuery.CountAsync();
        var inventories = await inventoryQuery
            .OrderByDescending(i => i.ViewCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InventorySearchResult
            {
                Id = i.Id,
                Title = i.Title,
                Description = i.Description.Length > 200 ? i.Description.Substring(0, 200) + "..." : i.Description,
                Category = i.Category,
                Tags = i.Tags,
                OwnerUsername = i.Owner.Username,
                ItemCount = i.Items.Count,
                ViewCount = i.ViewCount,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();

        return inventories;
    }

    private async Task<List<ItemSearchResult>> SearchItems(string query, int? userId, int page, int pageSize)
    {
        var searchTerm = query.ToLower();

        var itemQuery = _context.Items
            .Include(i => i.Inventory)
                .ThenInclude(inv => inv.Owner)
            .Include(i => i.CreatedByUser)
            .Include(i => i.FieldValues)
                .ThenInclude(fv => fv.CustomField)
            .Where(i => i.Inventory.IsPublic || (userId.HasValue && (i.Inventory.OwnerId == userId || i.Inventory.UserAccesses.Any(ua => ua.UserId == userId))))
            .Where(i =>
                i.CustomId.ToLower().Contains(searchTerm) ||
                i.FieldValues.Any(fv =>
                    (fv.TextValue != null && fv.TextValue.ToLower().Contains(searchTerm))));

        var total = await itemQuery.CountAsync();
        var items = await itemQuery
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new ItemSearchResult
            {
                Id = i.Id,
                CustomId = i.CustomId,
                InventoryId = i.InventoryId,
                InventoryTitle = i.Inventory.Title,
                CreatedByUsername = i.CreatedByUser.Username,
                CreatedAt = i.CreatedAt,
                MatchedFields = i.FieldValues
                    .Where(fv => fv.TextValue != null && fv.TextValue.ToLower().Contains(searchTerm))
                    .Select(fv => new FieldMatch
                    {
                        FieldName = fv.CustomField.Name,
                        Value = fv.TextValue!.Length > 100 ? fv.TextValue.Substring(0, 100) + "..." : fv.TextValue
                    })
                    .ToList()
            })
            .ToListAsync();

        return items;
    }
}

public class SearchResultsDto
{
    public required string Query { get; set; }
    public List<InventorySearchResult> Inventories { get; set; } = new();
    public List<ItemSearchResult> Items { get; set; } = new();
}

public class InventorySearchResult
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public required string Tags { get; set; }
    public required string OwnerUsername { get; set; }
    public int ItemCount { get; set; }
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ItemSearchResult
{
    public int Id { get; set; }
    public required string CustomId { get; set; }
    public int InventoryId { get; set; }
    public required string InventoryTitle { get; set; }
    public required string CreatedByUsername { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<FieldMatch> MatchedFields { get; set; } = new();
}

public class FieldMatch
{
    public required string FieldName { get; set; }
    public required string Value { get; set; }
}
