
using Utilities.Exceptions.Common;

namespace Utilities.Extensions
{
    public static class FileTypeExtensions
    {
        public static string GetMimeTypeFromFileExtension(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".zip" => "application/zip",
                ".rar" => "application/vnd.rar",
                _ => "application/octet-stream"
            };
        }


        public static string ToS3ValidatedPath(this string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new BadRequestException("Path cannot be null or empty.", nameof(fullPath));

            fullPath = fullPath.Trim();
            fullPath = fullPath.Replace(":", "");
            fullPath = fullPath.Replace("\\", "/").TrimStart('/');

            return fullPath.RemoveSpaces();
        }
    }
}
