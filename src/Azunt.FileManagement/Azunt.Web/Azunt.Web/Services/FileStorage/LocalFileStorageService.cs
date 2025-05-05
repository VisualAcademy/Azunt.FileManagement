using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Azunt.FileManagement.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly string _rootPath;
        private readonly ILogger<LocalFileStorageService> _logger;

        public LocalFileStorageService(IWebHostEnvironment env, ILogger<LocalFileStorageService> logger)
        {
            _logger = logger;
            _rootPath = Path.Combine(env.WebRootPath, "files", "Files");

            if (!System.IO.Directory.Exists(_rootPath))
            {
                System.IO.Directory.CreateDirectory(_rootPath);
            }
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName)
        {
            // 파일명 중복 방지
            string safeFileName = GetUniqueFileName(fileName);
            string fullPath = Path.Combine(_rootPath, safeFileName);

            using (var file = System.IO.File.Create(fullPath))
            {
                await fileStream.CopyToAsync(file);
            }

            // 웹에서 접근 가능한 상대 경로 반환
            return $"/files/Files/{safeFileName}";
        }

        private string GetUniqueFileName(string fileName)
        {
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            string newFileName = fileName;
            int count = 1;

            while (System.IO.File.Exists(Path.Combine(_rootPath, newFileName)))
            {
                newFileName = $"{baseName}({count}){extension}";
                count++;
            }

            return newFileName;
        }

        public Task<Stream> DownloadAsync(string fileName)
        {
            string fullPath = Path.Combine(_rootPath, fileName);

            if (!System.IO.File.Exists(fullPath))
                throw new FileNotFoundException($"File not found: {fileName}");

            var stream = System.IO.File.OpenRead(fullPath);
            return Task.FromResult<Stream>(stream);
        }

        public Task DeleteAsync(string fileName)
        {
            string fullPath = Path.Combine(_rootPath, fileName);

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }

            return Task.CompletedTask;
        }
    }
}
