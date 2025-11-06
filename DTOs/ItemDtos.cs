namespace InventoryManagement.API.DTOs;

public class CreateItemDto
{
    public Dictionary<int, string> FieldValues { get; set; } = new();
}

public class UpdateItemDto
{
    public Dictionary<int, string> FieldValues { get; set; } = new();
}

public class ItemDto
{
    public int Id { get; set; }
    public required string CustomId { get; set; }
    public int InventoryId { get; set; }
    public int CreatedByUserId { get; set; }
    public required string CreatedByUsername { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int LikesCount { get; set; }
    public bool IsLikedByCurrentUser { get; set; }
    public Dictionary<int, ItemFieldValueDto> FieldValues { get; set; } = new();
}

public class ItemFieldValueDto
{
    public int FieldId { get; set; }
    public required string FieldName { get; set; }
    public required string FieldType { get; set; }
    public string? Value { get; set; }
}
