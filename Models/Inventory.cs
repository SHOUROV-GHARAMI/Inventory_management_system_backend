using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagement.API.Models;

public class Inventory
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty; // Markdown

    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsPublic { get; set; } = true;

    [MaxLength(1000)]
    public string Tags { get; set; } = string.Empty; // Comma-separated

    public int OwnerId { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public User Owner { get; set; } = null!;

    [MaxLength(500)]
    public string? CustomIdFormat { get; set; }

    public int ViewCount { get; set; } = 0;

    [Timestamp]
    public byte[]? RowVersion { get; set; } // For optimistic locking

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<CustomField> CustomFields { get; set; } = new List<CustomField>();
    public ICollection<Item> Items { get; set; } = new List<Item>();
    public ICollection<UserInventoryAccess> UserAccesses { get; set; } = new List<UserInventoryAccess>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
