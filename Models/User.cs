using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.API.Models;

public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? PasswordHash { get; set; }

    [MaxLength(50)]
    public string? Provider { get; set; } // "Google", "Facebook", "Local"

    [MaxLength(255)]
    public string? ProviderId { get; set; }

    public bool IsAdmin { get; set; } = false;

    public bool IsBlocked { get; set; } = false;

    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "en";

    [MaxLength(10)]
    public string PreferredTheme { get; set; } = "light";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public ICollection<Inventory> OwnedInventories { get; set; } = new List<Inventory>();
    public ICollection<UserInventoryAccess> InventoryAccesses { get; set; } = new List<UserInventoryAccess>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<ItemLike> ItemLikes { get; set; } = new List<ItemLike>();
}
