using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace InventoryManagement.API.Services;

public interface ICloudStorageService
{
    Task<string> UploadImageAsync(IFormFile file, string fileName);
    Task<bool> DeleteImageAsync(string fileUrl);
}

public class CloudStorageService : ICloudStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public CloudStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureStorage:ConnectionString"];
        _containerName = configuration["AzureStorage:ContainerName"] ?? "inventory-images";
        
        if (!string.IsNullOrEmpty(connectionString) && connectionString != "YOUR_AZURE_STORAGE_CONNECTION_STRING")
        {
            _blobServiceClient = new BlobServiceClient(connectionString);
        }
        else
        {
            // Mock service for development without Azure Storage
            _blobServiceClient = null!;
        }
    }

    public async Task<string> UploadImageAsync(IFormFile file, string fileName)
    {
        if (_blobServiceClient == null)
        {
            // Return a mock URL for development
            return $"https://mock-storage.blob.core.windows.net/{_containerName}/{fileName}";
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var blobClient = containerClient.GetBlobClient(fileName);

        using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });

        return blobClient.Uri.ToString();
    }

    public async Task<bool> DeleteImageAsync(string fileUrl)
    {
        if (_blobServiceClient == null || string.IsNullOrEmpty(fileUrl))
        {
            return true; // Mock delete for development
        }

        try
        {
            var uri = new Uri(fileUrl);
            var fileName = Path.GetFileName(uri.LocalPath);

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(fileName);

            await blobClient.DeleteIfExistsAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
