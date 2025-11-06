namespace InventoryManagement.API.DTOs;

public class CreateInventoryDto
{
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsPublic { get; set; } = true;
    public string Tags { get; set; } = string.Empty;
}

public class UpdateInventoryDto
{
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsPublic { get; set; }
    public string Tags { get; set; } = string.Empty;
    public string? CustomIdFormat { get; set; }
}

public class InventoryDto
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsPublic { get; set; }
    public string Tags { get; set; } = string.Empty;
    public int OwnerId { get; set; }
    public required string OwnerUsername { get; set; }
    public string? CustomIdFormat { get; set; }
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int ItemCount { get; set; }
    public bool CanEdit { get; set; }
}

public class InventoryDetailDto : InventoryDto
{
    public List<CustomFieldDto> CustomFields { get; set; } = new();
    public List<UserAccessDto> SharedWith { get; set; } = new();
}

public class CustomFieldDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Type { get; set; }
    public bool ShowInTable { get; set; }
    public int DisplayOrder { get; set; }
}

public class UserAccessDto
{
    public int UserId { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public DateTime GrantedAt { get; set; }
}
