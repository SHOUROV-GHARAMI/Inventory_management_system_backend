using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.API.Data;
using InventoryManagement.API.Models;
using System.Security.Claims;

namespace InventoryManagement.API.Controllers;

[ApiController]
[Route("api/inventories/{inventoryId}/comments")]
public class CommentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CommentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<CommentDto>>> GetComments(
        int inventoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] DateTime? since = null)
    {
        var inventory = await _context.Inventories.FindAsync(inventoryId);
        if (inventory == null)
        {
            return NotFound();
        }

        var query = _context.Comments
            .Include(c => c.User)
            .Where(c => c.InventoryId == inventoryId);

        // Support polling - get comments since last check
        if (since.HasValue)
        {
            query = query.Where(c => c.CreatedAt > since.Value);
        }

        var total = await query.CountAsync();
        var comments = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CommentDto
            {
                Id = c.Id,
                Content = c.Content,
                UserId = c.UserId,
                Username = c.User.Username,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", total.ToString());
        return Ok(comments);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<CommentDto>> CreateComment(int inventoryId, CreateCommentDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        var inventory = await _context.Inventories.FindAsync(inventoryId);
        if (inventory == null)
        {
            return NotFound();
        }

        // Check if user can view inventory (and thus comment)
        if (!inventory.IsPublic)
        {
            var hasAccess = inventory.OwnerId == userId ||
                           await _context.UserInventoryAccesses.AnyAsync(ua => ua.InventoryId == inventoryId && ua.UserId == userId) ||
                           User.IsInRole("Admin");

            if (!hasAccess)
            {
                return Forbid();
            }
        }

        var comment = new Comment
        {
            InventoryId = inventoryId,
            UserId = userId,
            Content = dto.Content
        };

        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        var user = await _context.Users.FindAsync(userId);

        var commentDto = new CommentDto
        {
            Id = comment.Id,
            Content = comment.Content,
            UserId = userId,
            Username = user!.Username,
            CreatedAt = comment.CreatedAt
        };

        return CreatedAtAction(nameof(GetComments), new { inventoryId }, commentDto);
    }

    [HttpDelete("{commentId}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(int inventoryId, int commentId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var isAdmin = User.IsInRole("Admin");

        var comment = await _context.Comments
            .Include(c => c.Inventory)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.InventoryId == inventoryId);

        if (comment == null)
        {
            return NotFound();
        }

        // Only comment owner, inventory owner, or admin can delete
        if (!isAdmin && comment.UserId != userId && comment.Inventory.OwnerId != userId)
        {
            return Forbid();
        }

        _context.Comments.Remove(comment);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class CreateCommentDto
{
    public required string Content { get; set; }
}

public class CommentDto
{
    public int Id { get; set; }
    public required string Content { get; set; }
    public int UserId { get; set; }
    public required string Username { get; set; }
    public DateTime CreatedAt { get; set; }
}
