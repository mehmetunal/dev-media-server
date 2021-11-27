

using System;
using System.Threading.Tasks;
using Dev.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

        [HttpGet("GetFileById/{id}")]
        public async Task<object> GetFileById(string id)
        {
            var result = await _mediaServerService.GetByIdAsync(id);
            return result;
        }

        [HttpGet("GetVideoById/{id}")]
        public async Task GetVideoById(string id)
        {
            Response.Headers["Accept-Ranges"] = "bytes";
            Response.ContentType = "application/octet-stream";

            MediaStreamHelper mediaStream = new MediaStreamHelper(id, _mediaServerService);

            long end = 0;
            int start = 0;
            var CHUNK_SIZE = 1024 * 10;


            if (!String.IsNullOrEmpty(Request.Headers["Range"]))
            {
                string[] range = Request.Headers["Range"].ToString().Split(new char[] {'=', '-'});
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
            var result = await _mediaServerService.AddAsync(fileName);
            return result.Id.ToString();
        }

        [HttpPost("uploadComplete/{id}")]
        public async Task<string> UploadComplete(string fileName, string id)
        {
            var mediaServer = await _mediaServerService.UpdateAsync(fileName, id);
            if (mediaServer == null)
                throw new ArgumentNullException($"{fileName} is null");

            return mediaServer.Id.ToString();
        }

        [HttpDelete("{id}")]
        public async Task<bool> Delete(string id)
        {
            var mediaServer = await _mediaServerService.DeleteAsync(id);
            if (mediaServer == null)
                throw new ArgumentNullException($"{id} is null");

            return mediaServer.Id != null;
        }

        #endregion
    }
}