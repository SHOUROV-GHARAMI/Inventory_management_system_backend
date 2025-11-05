using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagement.API.Models;

public class ItemFieldValue
{
    [Key]
    public int Id { get; set; }

    public int ItemId { get; set; }

    [ForeignKey(nameof(ItemId))]
    public Item Item { get; set; } = null!;

    public int CustomFieldId { get; set; }

    [ForeignKey(nameof(CustomFieldId))]
    public CustomField CustomField { get; set; } = null!;

    public string? TextValue { get; set; }

    public decimal? NumberValue { get; set; }

    public bool? BooleanValue { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
