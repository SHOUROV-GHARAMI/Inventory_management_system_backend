using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace InventoryManagement.API.Services;

public interface ICustomIdService
{
    string GenerateId(string format, int inventoryId);
    string PreviewId(string format);
    bool ValidateFormat(string format);
}

public class CustomIdService : ICustomIdService
{
    private static readonly Random _random = new Random();

    public string GenerateId(string format, int inventoryId)
    {
        if (string.IsNullOrEmpty(format))
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
        }

        return ParseFormat(format, inventoryId);
    }

    public string PreviewId(string format)
    {
        if (string.IsNullOrEmpty(format))
        {
            return "XXXXXXXX";
        }

        return ParseFormat(format, 1);
    }

    public bool ValidateFormat(string format)
    {
        if (string.IsNullOrEmpty(format))
        {
            return true;
        }

        try
        {
            ParseFormat(format, 1);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string ParseFormat(string format, int inventoryId)
    {
        var result = format;

        // Replace placeholders with actual values
        result = Regex.Replace(result, @"\{TEXT:([^\}]+)\}", m => m.Groups[1].Value);
        result = Regex.Replace(result, @"\{RANDOM6\}", m => GenerateRandomNumber(6));
        result = Regex.Replace(result, @"\{RANDOM9\}", m => GenerateRandomNumber(9));
        result = Regex.Replace(result, @"\{RANDOM20\}", m => GenerateRandomNumber(20));
        result = Regex.Replace(result, @"\{RANDOM32\}", m => GenerateRandomNumber(32));
        result = Regex.Replace(result, @"\{GUID\}", m => Guid.NewGuid().ToString("N").ToUpper());
        result = Regex.Replace(result, @"\{GUID8\}", m => Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper());
        result = Regex.Replace(result, @"\{DATE:([^\}]+)\}", m => DateTime.UtcNow.ToString(m.Groups[1].Value));
        result = Regex.Replace(result, @"\{DATE\}", m => DateTime.UtcNow.ToString("yyyyMMdd"));
        result = Regex.Replace(result, @"\{TIME\}", m => DateTime.UtcNow.ToString("HHmmss"));
        result = Regex.Replace(result, @"\{YEAR\}", m => DateTime.UtcNow.Year.ToString());
        result = Regex.Replace(result, @"\{MONTH\}", m => DateTime.UtcNow.ToString("MM"));
        result = Regex.Replace(result, @"\{DAY\}", m => DateTime.UtcNow.ToString("dd"));

        // Sequence number is handled separately per inventory
        // For now, we'll use a placeholder - the actual implementation should query the database
        result = Regex.Replace(result, @"\{SEQ:(\d+)\}", m =>
        {
            var padding = int.Parse(m.Groups[1].Value);
            // This should be replaced with actual sequence logic
            return "0".PadLeft(padding, '0') + "1";
        });
        result = Regex.Replace(result, @"\{SEQ\}", m => "001");

        return result;
    }

    private string GenerateRandomNumber(int bits)
    {
        return bits switch
        {
            6 => _random.Next(0, 1000000).ToString("D6"),
            9 => _random.Next(0, 1000000000).ToString("D9"),
            20 => GenerateRandomString(20),
            32 => GenerateRandomString(32),
            _ => _random.Next(0, 100000).ToString("D5")
        };
    }

    private string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }
}

// DTOs for Custom ID
public class CustomIdFormatDto
{
    public required string Format { get; set; }
}

public class CustomIdPreviewDto
{
    public required string Format { get; set; }
    public required string Preview { get; set; }
}
