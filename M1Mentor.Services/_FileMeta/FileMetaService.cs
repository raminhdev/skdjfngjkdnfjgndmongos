using M1Mentor.Utilities.Services.Contracts;
using M1Mentor.Domain.Collections;
using M1Mentor.Domain.Repositories.Contracts;
using M1Mentor.Services._FileMeta.Contracts;
using M1Mentor.Services._FileMeta.DTOs.Results;
using M1Mentor.Services._FileMeta.DTOs.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using System.Security.Cryptography;
using Utilities.Exceptions.Common;
using Utilities.Extensions;
using Utilities.MongoDatabase.Extensions;
using Utilities.MongoDatabase.Filter;
using Utilities.Services;
using Utilities.Services.Contracts;
using static Utilities.Constants.RegisterMode;

namespace M1Mentor.Services._FileMeta
{
    public class FileMetaService(
        IFileMetaRepository _fileMetaRepository,
        IFileReferenceService _fileReferenceService,
        IRandomService _randomService,
        IFileStorageProvider _storageProvider,
        ILogger<FileMetaService> _logger,
        FileSettings _fileSettings)
        : IScopedDependency, IFileMetaService, IFileMetaSchedulerService, IFileMetaInternalService
    {
        // Images at or below this size are re-encoded (EXIF stripped, recompressed). Larger images
        // and all non-image files are streamed straight through with bounded memory.
        private const long ImageReencodeMaxBytes = 50 * 1024 * 1024; // 50 MB

        public async Task SyncEntityFilesAsync(string entityId, EntityType type, IEnumerable<string> currentStoredNames)
        {
            var currentNames = currentStoredNames?.ToHashSet() ?? [];

            var existingReferences = await _fileReferenceService.GetByEntityAsync(entityId, type);

            var existingFileMetaIds = existingReferences
                .Select(r => r.FileMetaId)
                .ToHashSet();

            var incomingFileMetas = currentNames.Count > 0
                ? await _fileMetaRepository.AsQueryable()
                    .Where(f => currentNames.Contains(f.StoredName))
                    .ToListAsync()
                : [];

            var incomingFileMetaIds = incomingFileMetas
                .Select(f => f.FileMetaId)
                .ToHashSet();

            var toAdd = incomingFileMetas
                .Where(f => !existingFileMetaIds.Contains(f.FileMetaId))
                .ToList();

            var toRemove = existingReferences
                .Where(r => !incomingFileMetaIds.Contains(r.FileMetaId))
                .ToList();

            foreach (var fileMeta in toAdd)
            {
                await _fileReferenceService.CreateAsync(fileMeta.FileMetaId, type, entityId);
                await _fileMetaRepository.FindOneAndUpdateAsync(q => q.FileMetaId == fileMeta.FileMetaId,
                    Builders<FileMeta>.Update.Inc(q => q.ReferenceCount, 1));
            }

            foreach (var reference in toRemove)
            {
                await _fileReferenceService.DeleteAsync(reference.ReferenceId);
                await _fileMetaRepository.FindOneAndUpdateAsync(q => q.FileMetaId == reference.FileMetaId,
                    Builders<FileMeta>.Update.Inc(q => q.ReferenceCount, -1));
            }
        }

        public async Task<string> UploadFileAsync(IFormFile file, EntityType type, string uploadedBy = null)
        {
            // Validation (size/extension/name) is performed up-front so we never touch storage for
            // an invalid file. The hash is computed by streaming the upload (no full buffering).
            ValidateFile(file);
            ValidateMaxSize(file.Length);
            var extension = GetExtension(file.FileName);
            ValidateExtension(extension);

            var hash = await CalculateHashAsync(file);

            var existingFile = await GetFileMetaByHashWithOutExceptionAsync(hash);

            if (existingFile != null)
            {
                // Content-addressed de-duplication: identical bytes are stored once.
                existingFile.ReferenceCount++;

                await _fileMetaRepository.FindOneAndUpdateAsync(q => q.FileMetaId == existingFile.FileMetaId,
                    Builders<FileMeta>.Update.Set(q => q.ReferenceCount, existingFile.ReferenceCount));
                await _fileReferenceService.CreateAsync(existingFile.FileMetaId, type);

                return existingFile.StoredName;
            }

            var fileMeta = CreateFileMetaInstance(file, hash, extension);
            fileMeta.StoragePath = GenerateRelativePath(fileMeta.StoredName, type);
            fileMeta.StorageProvider = _storageProvider.Name;
            fileMeta.BucketName = _storageProvider.BucketName;
            fileMeta.UploadedBy = uploadedBy;

            // Persist metadata first; if the physical write fails the orphan record is reclaimed by
            // the cleanup scheduler (ReferenceCount == 0).
            await _fileMetaRepository.InsertOneAsync(fileMeta);

            fileMeta.Size = await SaveFileAsync(file, extension, fileMeta.StoragePath);

            await _fileMetaRepository.FindOneAndUpdateAsync(
                q => q.FileMetaId == fileMeta.FileMetaId,
                Builders<FileMeta>.Update.Set(q => q.Size, fileMeta.Size));

            return fileMeta.StoredName;
        }

        public async Task<FileDataResult> DownloadFileAsync(string storedFileName)
        {
            var fileMeta =
                await GetFileMetaByStoredNameWithOutExceptionAsync(storedFileName.RemoveSpaces())
                ?? throw new NotFoundException("File Not Found");

            var stored = await _storageProvider.OpenReadAsync(fileMeta.StoragePath);

            return new FileDataResult
            {
                ContentType = !string.IsNullOrWhiteSpace(fileMeta.MimeType)
                    ? fileMeta.MimeType
                    : FileTypeExtensions.GetMimeTypeFromFileExtension("." +
                                                                      (fileMeta.Extension ?? string.Empty)
                                                                      .ToLowerInvariant()),
                FileName = fileMeta.OriginalName ?? fileMeta.StoredName,
                Stream = stored.Stream
            };
        }

        public async Task<MonjoFilteredResult<FileMetaResult>> GetAllFilesAsync(MonjoQuery monjoQuery)
        {
            monjoQuery.WithBase<FileMetaResult>();

            return await _fileMetaRepository.AsQueryable()
                .Apply(monjoQuery.Where)
                .Apply(monjoQuery.Order)
                .Select(fileMeta => new FileMetaResult()
                {
                    CreatedMoment = fileMeta.CreatedMoment,
                    Extension = fileMeta.Extension,
                    FileMetaId = fileMeta.FileMetaId,
                    OriginalName = fileMeta.OriginalName,
                    ReferenceCount = fileMeta.ReferenceCount,
                    Size = fileMeta.Size,
                    StoredName = fileMeta.StoredName,
                    CreatedByInfo = fileMeta.CreatedByInfo,
                    ModifiedByInfo = fileMeta.ModifiedByInfo,
                    ModifiedMoment = fileMeta.ModifiedMoment
                })
                .ExecuteAsync(monjoQuery);
        }

        public async Task<bool> DeleteFileAsync(string fileMetaId)
        {
            var fileMeta = await GetFileMetaByIdWithOutExceptionAsync(fileMetaId)
                           ?? throw new NotFoundException("File Not Found");

            if (fileMeta.ReferenceCount != 0)
                throw new BadRequestException("Cannot Delete Used File");

            if (!string.IsNullOrWhiteSpace(fileMeta.StoragePath))
                await _storageProvider.DeleteAsync(fileMeta.StoragePath);

            await _fileMetaRepository.DeleteOneAsync(q => q.FileMetaId == fileMeta.FileMetaId);

            return true;
        }

        public async Task MarkFilesForDeletionAsync()
        {
            _logger.LogInformation("MarkFilesForDeletion started at {Time}", DateTime.UtcNow);

            var cutoff = DateTime.UtcNow.AddHours(-_fileSettings.SafetyWindowHours);

            var candidates = await _fileMetaRepository.AsQueryable()
                .Where(f => f.ReferenceCount == 0
                            && f.MarkedForDeletionAt == null
                            && f.ModifiedMoment < cutoff)
                .ToListAsync();

            if (candidates.Count == 0)
            {
                _logger.LogInformation("MarkFilesForDeletion — no eligible files");
                return;
            }

            await _fileMetaRepository.UpdateManyAsync(q =>
                    candidates
                        .Select(meta => meta.FileMetaId)
                        .ToList()
                        .Contains(q.FileMetaId),
                Builders<FileMeta>.Update.Set(q => q.MarkedForDeletionAt, DateTime.UtcNow));

            foreach (var file in candidates)
            {
                _logger.LogInformation("Marked for deletion: {StoredName} ({Id})", file.StoredName, file.FileMetaId);
            }

            _logger.LogInformation("MarkFilesForDeletion finished — marked {Count} file(s)", candidates.Count);
        }

        public async Task CleanupMarkedFilesAsync()
        {
            _logger.LogInformation("CleanupMarkedFiles started at {Time}", DateTime.UtcNow);

            var cutoff = DateTime.UtcNow.AddHours(-_fileSettings.QuarantinePeriodHours);

            var candidates = await _fileMetaRepository.AsQueryable()
                .Where(f => f.ReferenceCount == 0
                            && f.MarkedForDeletionAt != null
                            && f.MarkedForDeletionAt < cutoff)
                .ToListAsync();

            if (candidates.Count == 0)
            {
                _logger.LogInformation("CleanupMarkedFiles — no eligible files");
                return;
            }

            int deleted = 0;
            int skipped = 0;

            foreach (var file in candidates)
            {
                var safe = await VerifyNoLiveReferencesAsync(file);

                if (!safe)
                {
                    skipped++;
                    continue;
                }

                var success = await DeleteFileAndMetaAsync(file);

                if (success) deleted++;
                else skipped++;
            }

            _logger.LogInformation("CleanupMarkedFiles finished — deleted: {Deleted}, skipped: {Skipped}",
                deleted, skipped);
        }


        #region Private Methods

        private async Task<FileMeta> GetFileMetaByIdWithOutExceptionAsync(string fileMetaId)
            => await _fileMetaRepository.AsQueryable().FirstOrDefaultAsync(q => q.FileMetaId == fileMetaId);

        private async Task<FileMeta> GetFileMetaByHashWithOutExceptionAsync(string hash)
            => await _fileMetaRepository.AsQueryable().FirstOrDefaultAsync(q => q.Hash == hash);

        private async Task<FileMeta> GetFileMetaByStoredNameWithOutExceptionAsync(string storedName)
            => await _fileMetaRepository.AsQueryable().FirstOrDefaultAsync(q => q.StoredName == storedName);

        public async Task<FileMeta> GetFileMetaByStoragePathWithOutExceptionAsync(string storagePath)
            => await _fileMetaRepository.AsQueryable().FirstOrDefaultAsync(q => q.StoragePath == storagePath);

        private FileMeta CreateFileMetaInstance(IFormFile file, string hash, string extension)
        {
            var storedName = GenerateFileName(file.FileName, extension);

            return new FileMeta
            {
                Hash = hash,
                OriginalName = file.FileName,
                Size = file.Length,
                StoredName = storedName,
                Extension = extension,
                MimeType = FileTypeExtensions.GetMimeTypeFromFileExtension("." + extension.ToLowerInvariant()),
                ReferenceCount = 0
            };
        }

        private async Task<bool> VerifyNoLiveReferencesAsync(FileMeta file)
        {
            var liveCount = await _fileReferenceService.GetFileLiveCountAsync(file);

            if (liveCount == 0) return true;

            _logger.LogWarning(
                "File {StoredName} has {Count} live reference(s) despite ReferenceCount=0 — correcting and skipping",
                file.StoredName, liveCount);

            await _fileMetaRepository.FindOneAndUpdateAsync(
                q => q.FileMetaId == file.FileMetaId,
                Builders<FileMeta>.Update
                    .Set(q => q.ReferenceCount, (int)liveCount)
                    .Set(q => q.MarkedForDeletionAt, null));

            return false;
        }

        private async Task<bool> DeleteFileAndMetaAsync(FileMeta file)
        {
            if (!string.IsNullOrWhiteSpace(file.StoragePath))
            {
                try
                {
                    var removed = await _storageProvider.DeleteAsync(file.StoragePath);
                    if (removed)
                        _logger.LogInformation("Deleted from storage: {Path}", file.StoragePath);
                    else
                        _logger.LogWarning("File missing from storage: {Path} — removing orphan DB record",
                            file.StoragePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Storage delete failed: {Path} — skipping DB delete to allow retry",
                        file.StoragePath);
                    return false;
                }
            }

            await _fileMetaRepository.DeleteOneAsync(q => q.FileMetaId == file.FileMetaId);
            _logger.LogInformation("Deleted FileMeta record: {FileMetaId}", file.FileMetaId);

            return true;
        }

        /// <summary>
        /// Computes a SHA-256 content hash by streaming the upload — never loads the file into memory.
        /// </summary>
        private static async Task<string> CalculateHashAsync(IFormFile formFile)
        {
            using var sha256 = SHA256.Create();
            await using var stream = formFile.OpenReadStream();
            var hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hash);
        }

        private static void ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new BadRequestException("No file uploaded");

            if (string.IsNullOrWhiteSpace(file.FileName))
                throw new BadRequestException("File name is required");
        }

        private void ValidateMaxSize(long length)
        {
            var max = _fileSettings.MaxUploadSize;
            if (max > 0 && length > max)
                throw new BadRequestException($"File size exceeds the allowed limit of {max} bytes");
        }

        private static string GetExtension(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.TrimStart('.').ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(ext))
                throw new BadRequestException("File extension is missing");

            return ext;
        }

        private void ValidateExtension(string extension)
        {
            var allowed = _fileSettings.AllowedExtencions;
            if (!allowed.Contains(extension.ToUpper()))
                throw new BadRequestException($"File type {extension} is not supported");
        }

        private string GenerateFileName(string fileName, string extension)
        {
            var cleanName = Path.GetFileNameWithoutExtension(fileName);
            var timeStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff");
            var randomSuffix = _randomService.GetSecureNumericString(5);

            return $"{cleanName}-{timeStamp}{randomSuffix}.{extension}".RemoveSpaces();
        }

        // private string GenerateFileName(string fileName, string extension)
        // {
        //     // Sanitize to a safe, single path segment (path-traversal / illegal-char protection).
        //     var cleanName = Path.GetFileNameWithoutExtension(fileName);
        //     cleanName = string.Concat(cleanName.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        //     cleanName = cleanName.Replace("/", "").Replace("\\", "");
        //     if (string.IsNullOrWhiteSpace(cleanName)) cleanName = "file";
        //
        //     var timeStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff");
        //     var randomSuffix = _randomService.GetSecureNumericString(5);
        //
        //     return $"{cleanName}-{timeStamp}{randomSuffix}.{extension}".RemoveSpaces();
        // }

        /// <summary>
        /// Builds the provider-relative storage key: <c>{typeFolder}/{storedName}</c>. The path is
        /// always relative so it is portable across providers and never leaks an absolute location.
        /// </summary>
        private string GenerateRelativePath(string storedName, EntityType type)
        {
            string configured = type switch
            {
                EntityType.Type1 => _fileSettings.EntityType1UploadPath,
                EntityType.Type2 => _fileSettings.EntityType2UploadPath,
                EntityType.Type3 => _fileSettings.EntityType3UploadPath,
                _ => throw new BadRequestException($"Unknown folder name: {type}"),
            };

            // Accept either a relative subfolder ("EntityType1") or a legacy absolute path
            // ("C:\\New folder\\EntityType1Images") — only the safe leaf segment is used.
            var folder = string.IsNullOrWhiteSpace(configured)
                ? type.ToString()
                : configured.Replace('\\', '/').TrimEnd('/').Split('/').Last();

            if (string.IsNullOrWhiteSpace(folder)) folder = type.ToString();

            return $"{folder}/{storedName}";
        }

        /// <summary>
        /// Streams the upload into the configured storage provider. Small images are re-encoded
        /// (EXIF stripped, recompressed); everything else is copied through with bounded memory so
        /// multi-GB files transfer without large allocations.
        /// </summary>
        private async Task<long> SaveFileAsync(IFormFile file, string extension, string relativePath)
        {
            if (GetImageExtensions().Contains(extension) && file.Length <= ImageReencodeMaxBytes)
            {
                await using var processed = await ProcessImageToStreamAsync(file, extension);
                return await _storageProvider.SaveAsync(processed, relativePath);
            }

            await using var source = file.OpenReadStream();
            return await _storageProvider.SaveAsync(source, relativePath);
        }

        private static HashSet<string> GetImageExtensions() => ["PNG", "JPG", "JPEG", "WEBP"];

        private static async Task<Stream> ProcessImageToStreamAsync(IFormFile file, string extension)
        {
            await using var input = file.OpenReadStream();
            using var image = await Image.LoadAsync(input);
            image.Metadata.ExifProfile = null;

            var output = new MemoryStream();

            switch (extension)
            {
                case "JPG":
                case "JPEG":
                    await image.SaveAsync(output, new JpegEncoder { Quality = 80 });
                    break;
                case "PNG":
                    await image.SaveAsync(output, new PngEncoder { CompressionLevel = PngCompressionLevel.Level9 });
                    break;
                case "WEBP":
                    await image.SaveAsync(output, new WebpEncoder { Quality = 80 });
                    break;
                default:
                    throw new BadRequestException($"Unsupported image format: {extension}");
            }

            output.Position = 0;
            return output;
        }


        private async Task<FileDataResult> GetFileWithUriAsync(string storedName)
        {
            var fileMeta = await GetFileMetaByStoredNameWithOutExceptionAsync(storedName.RemoveSpaces())
                           ?? throw new NotFoundException("File Not Found");
            //if (fileMeta.IsUploadToS3)
            //{
            //    return await _s3FileService.DownloadFileAsync(fileMeta.FileUri);
            //}
            //else
            //{
            return await GetFileFromDiscAsync(fileMeta.StoragePath);
            //}
        }

        private static async Task<FileDataResult> GetFileFromDiscAsync(string fullPathFile)
        {
            if (fullPathFile == null)
                throw new BadRequestException("File name required");
            try
            {
                MemoryStream memory = new();
                await using (FileStream stream = new(fullPathFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    await stream.CopyToAsync(memory);

                memory.Position = 0;
                var fileName = Path.GetFileName(fullPathFile);

                return new FileDataResult
                {
                    ContentType = FileTypeExtensions.GetMimeTypeFromFileExtension(Path.GetExtension(fileName)),
                    FileName = fileName,
                    Stream = memory
                };
            }
            catch (BadRequestException ex)
            {
                throw new BadRequestException(ex.Message);
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }

        private FileMetaResult MapFileMetaResult(FileMeta fileMeta)
        {
            return new FileMetaResult
            {
                CreatedMoment = fileMeta.CreatedMoment,
                Extension = fileMeta.Extension,
                FileMetaId = fileMeta.FileMetaId,
                OriginalName = fileMeta.OriginalName,
                ReferenceCount = fileMeta.ReferenceCount,
                Size = fileMeta.Size,
                StoredName = fileMeta.StoredName,
                CreatedByInfo = fileMeta.CreatedByInfo,
                ModifiedByInfo = fileMeta.ModifiedByInfo,
                ModifiedMoment = fileMeta.ModifiedMoment
            };
        }

        #endregion
    }
}