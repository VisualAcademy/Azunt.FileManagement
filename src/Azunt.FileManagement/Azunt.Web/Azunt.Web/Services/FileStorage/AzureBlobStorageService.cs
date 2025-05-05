using Azunt.FileManagement;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading.Tasks;

namespace Azunt.Web.Services.FileStorage
{
    public class AzureBlobStorageService : IFileStorageService
    {
        private readonly BlobContainerClient _containerClient;

        public AzureBlobStorageService(IConfiguration config)
        {
            var connStr = config["AzureBlobStorage:Default:ConnectionString"];
            var containerName = config["AzureBlobStorage:Default:ContainerName"];

            _containerClient = new BlobContainerClient(connStr, containerName);
            _containerClient.CreateIfNotExists();
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName)
        {
            // 파일명 중복 방지 처리
            string safeFileName = await GetUniqueFileNameAsync(fileName);
            var blobClient = _containerClient.GetBlobClient(safeFileName);

            // 파일 업로드
            await blobClient.UploadAsync(fileStream, overwrite: true);

            return blobClient.Uri.ToString(); // 전체 URL 반환
        }

        private async Task<string> GetUniqueFileNameAsync(string fileName)
        {
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            string newFileName = fileName;
            int count = 1;

            // Blob Storage에서 파일이 이미 존재하는지 체크
            while (await _containerClient.GetBlobClient(newFileName).ExistsAsync())
            {
                newFileName = $"{baseName}({count}){extension}";
                count++;
            }

            return newFileName;
        }

        public async Task<Stream> DownloadAsync(string fileName)
        {
            var blobClient = _containerClient.GetBlobClient(fileName);

            if (!await blobClient.ExistsAsync())
                throw new FileNotFoundException($"File not found: {fileName}");

            var response = await blobClient.DownloadAsync();
            return response.Value.Content;
        }

        public Task DeleteAsync(string fileName)
        {
            var blobClient = _containerClient.GetBlobClient(fileName);
            return blobClient.DeleteIfExistsAsync();
        }
    }
}
