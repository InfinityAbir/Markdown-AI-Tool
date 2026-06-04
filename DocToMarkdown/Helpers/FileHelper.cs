using Microsoft.AspNetCore.Http;

namespace DocToMarkdown.Helpers
{
    public static class FileHelper
    {
        // 📁 Ensure directory exists
        public static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        // 🆔 Generate unique file name
        public static string GetUniqueFileName(string fileName)
        {
            return $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        }

        // 💾 Save uploaded file
        public static async Task<string> SaveFileAsync(IFormFile file, string uploadPath)
        {
            EnsureDirectory(uploadPath);

            var uniqueName = GetUniqueFileName(file.FileName);
            var fullPath = Path.Combine(uploadPath, uniqueName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return fullPath;
        }

        // 📄 Create temp file path
        public static string CreateTempFile(string folderPath, string extension = ".txt")
        {
            EnsureDirectory(folderPath);

            return Path.Combine(folderPath, $"{Guid.NewGuid()}{extension}");
        }

        // ❌ Safe delete file
        public static void SafeDelete(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // ignore silently (avoid crash)
            }
        }

        // 🧹 Cleanup multiple temp files
        public static void CleanupFiles(IEnumerable<string> filePaths)
        {
            foreach (var file in filePaths)
            {
                SafeDelete(file);
            }
        }
    }
}