namespace Azunt.FileManagement
{
    public interface IFileStorageService
    {
        Task<string> UploadAsync(Stream fileStream, string fileName);
        Task<Stream> DownloadAsync(string fileName);
        Task DeleteAsync(string fileName);
    }
}
