using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagement.API.Models;

public enum FieldType
{
    Text,
    MultilineText,
    Number,
    Link,
    Boolean
}

public class CustomField
{
    [Key]
    public int Id { get; set; }

    public int InventoryId { get; set; }

    [ForeignKey(nameof(InventoryId))]
    public Inventory Inventory { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public FieldType Type { get; set; }

    public bool ShowInTable { get; set; } = false;

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<ItemFieldValue> ItemFieldValues { get; set; } = new List<ItemFieldValue>();
}
