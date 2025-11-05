using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagement.API.Models;

public class Item
{
    [Key]
    public int Id { get; set; }

    public int InventoryId { get; set; }

    [ForeignKey(nameof(InventoryId))]
    public Inventory Inventory { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string CustomId { get; set; } = string.Empty;

    public int CreatedByUserId { get; set; }

    [ForeignKey(nameof(CreatedByUserId))]
    public User CreatedByUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<ItemFieldValue> FieldValues { get; set; } = new List<ItemFieldValue>();
    public ICollection<ItemLike> Likes { get; set; } = new List<ItemLike>();
}
