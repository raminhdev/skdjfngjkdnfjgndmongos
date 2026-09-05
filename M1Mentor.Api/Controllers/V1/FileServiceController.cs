using Asp.Versioning;
using M1Mentor.Domain.Collections;
using M1Mentor.Services._FileMeta.Contracts;
using M1Mentor.Services._FileMeta.DTOs.Results;
using Microsoft.AspNetCore.Mvc;
using Utilities.Api;
using Utilities.Attributes;
using Utilities.Filters;
using Utilities.MongoDatabase.Filter;

namespace M1Mentor.Api.Controllers.V1
{
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class FileServiceController(IFileMetaService _fileMetaService, IFileReferenceService _fileReferenceService) : ApiBaseController
    {
        // Range processing enabled => browsers/players can stream large video/audio and resume
        // interrupted downloads (HTTP 206 Partial Content). The stream is provider-backed and
        // disposed by the framework after the response is sent.
        [HttpGet("[action]")]
        [HttpPost("[action]")]
        [Authorize]
        [IgnoreSignature]
        public async Task<IActionResult> DownloadEntityType1FileAsync([FromQuery] string storedName)
        {
            var file = await _fileMetaService.DownloadFileAsync(storedName);

            return File(
                file.Stream,
                file.ContentType,
                file.FileName,
                enableRangeProcessing: true
            );
        }

        #region Upload

        // No hardcoded per-action cap: the upload size limit is configuration-driven
        // (FileStorage:MaxUploadSizeGB) and enforced globally by Kestrel/Form limits and by
        // FileMetaService. DisableRequestSizeLimit lets the global Kestrel limit be the single
        // source of truth so multi-GB uploads are accepted.
        [HttpPost("[action]")]
        [Authorize]
        [DisableRequestSizeLimit]
        [Security(disable: true)]
        public async Task<string> UploadEntityType1FileAsync(IFormFile file)
            => await _fileMetaService.UploadFileAsync(file, EntityType.Type1, PublicKey);

        [HttpPost("[action]")]
        [Authorize]
        [DisableRequestSizeLimit]
        [Security(disable: true)]
        public async Task<string> UploadEntityType2FileAsync(IFormFile file)
            => await _fileMetaService.UploadFileAsync(file, EntityType.Type2, PublicKey);

        [HttpPost("[action]")]
        [Authorize]
        [DisableRequestSizeLimit]
        [Security(disable: true)]
        public async Task<string> UploadEntityType3FileAsync(IFormFile file)
            => await _fileMetaService.UploadFileAsync(file, EntityType.Type3, PublicKey);

        #endregion

        [HttpPost("[action]")]
        [Authorize/* permission */]
        public async Task<MonjoFilteredResult<FileReferenceResult>> GetAllFileReferencesAsync(MonjoQuery monjoQuery)
            => await _fileReferenceService.GetAllAsync(monjoQuery);

        [HttpPost("[action]")]
        [Authorize/* permission */]
        public async Task<MonjoFilteredResult<FileMetaResult>> GetAllFilesAsync(MonjoQuery monjoQuery)
            => await _fileMetaService.GetAllFilesAsync(monjoQuery);

        [HttpDelete("[action]")]
        [Authorize/* permission */]
        public async Task<bool> DeleteFileMetaAsync([FromQuery] string fileMetaId)
            => await _fileMetaService.DeleteFileAsync(fileMetaId);
    }
}
