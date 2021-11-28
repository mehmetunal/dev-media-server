using Dev.Dto.Mongo;
using Dev.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Devfreco.MediaServer.Controllers
{
    [Route("api/v{version:apiVersion}/media-server")]
    public class MediaServerController : ControllerBase
    {
        #region Properties

        private readonly IMediaServerService _mediaServerService;

        #endregion

        #region Ctor

        public MediaServerController(IMediaServerService mediaServerService)
        {
            _mediaServerService = mediaServerService;
        }

        #endregion

        #region Method

        [HttpGet("GetImageById/{id}")]
        public async Task<string> GetImageById(string id)
        {
            var result = await _mediaServerService.GetImageByIdAsync(id);
            return result;
        }

        [HttpGet("GetFileById/{id}")]
        public async Task<MediaDto> GetFileById(string id)
        {
            var result = await _mediaServerService.GetByIdAsync(id);
            return result;
        }


        [HttpGet("FileDownload/{id}")]
        public async Task<FileResult> FileDownload(string id)
        {
            var (fileContents, fileName) = await _mediaServerService.FileDownload(id);
            string contentType = MimeKit.MimeTypes.GetMimeType(fileName);
            return File(fileContents, contentType);
        }

        [HttpGet("GetVideoById/{id}")]
        public async Task GetVideoById(string id)
        {
            Response.Headers["Accept-Ranges"] = "bytes";
            Response.ContentType = "application/octet-stream";

            var fileInfo = await _mediaServerService.GetByIdAsync(id);
            var fileStream = (await _mediaServerService.GetFileByIdAsync(id));

            var mediaStream = new MediaStreamHelper();
            mediaStream.FileSize = fileInfo.Length;
            mediaStream.FileExt = fileInfo.Extensions;
            mediaStream.FileType = fileInfo.Extensions.Replace(".", "");
            mediaStream.fis = fileStream;


            long end = 0;
            int start = 0;
            var CHUNK_SIZE = 1024 * 10;


            if (!String.IsNullOrEmpty(Request.Headers["Range"]))
            {
                string[] range = Request.Headers["Range"].ToString().Split(new char[] { '=', '-' });
                start = Int32.Parse(range[1]);
                mediaStream.SetPosition(start);
                Response.StatusCode = 206;
                end = Math.Min(start + CHUNK_SIZE, mediaStream.FileSize - 1);
                Response.Headers["Content-Range"] = String.Format(" bytes {0}-{1}/{2}", start, mediaStream.FileSize - 1, mediaStream.FileSize);
            }

            var outputStream = this.Response.Body;

            await mediaStream.CreatePartialContent(outputStream, start, end, HttpContext.RequestAborted.IsCancellationRequested);

            Response.Clear();
        }

        [HttpPost("upload")]
        public bool Upload(IFormFile fileToUpload, string file, int num)
        {
            var result = _mediaServerService.FileUpload(fileToUpload, file, num);
            return result.IsSuccess;
        }

        [HttpPost("uploadComplete")]
        public async Task<string> UploadComplete(string fileName)
        {
            var objId = await _mediaServerService.AddAsync(fileName);
            return objId;
        }

        [HttpPost("uploadComplete/{id}")]
        public async Task<string> UploadComplete(string fileName, string id)
        {
            var fileId = await _mediaServerService.UpdateAsync(fileName, id);
            if (fileId == null)
                throw new ArgumentNullException($"{fileId} is null");

            return fileId;
        }

        [HttpDelete("{id}")]
        public async Task<bool> Delete(string id)
        {
            var isSuccess = await _mediaServerService.DeleteAsync(id);
            return isSuccess;
        }

        #endregion
    }
}