namespace InventoryManagement.API.DTOs;

public class InventoryStatisticsDto
{
    public int TotalItems { get; set; }
    public int TotalLikes { get; set; }
    public int TotalComments { get; set; }
    public int ViewCount { get; set; }
    public List<FieldStatistics> FieldStatistics { get; set; } = new();
    public Dictionary<string, int> TagDistribution { get; set; } = new();
}

public class FieldStatistics
{
    public int FieldId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string FieldType { get; set; } = string.Empty;
    
    // For numeric fields
    public double? Average { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Sum { get; set; }
    
    // For all field types
    public int FilledCount { get; set; }
    public int EmptyCount { get; set; }
    
    // For text/boolean fields - most common values
    public List<ValueFrequency> TopValues { get; set; } = new();
}

public class ValueFrequency
{
    public string Value { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}
