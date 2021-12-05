using Dev.Dto.Mongo;
using Dev.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
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
            if (!string.IsNullOrEmpty(Request.Headers["Range"]))
            {
                var fileInfo = await _mediaServerService.GetByIdAsync(id);
                var fileStream = (await _mediaServerService.GetFileByIdAsync(id));

                Response.Headers["Accept-Ranges"] = "bytes";
                Response.ContentType = MimeKit.MimeTypes.GetMimeType(fileInfo.Extensions);
                string[] range = Request.Headers["Range"].ToString().Split(new char[] { '=', '-' });

                long start = long.Parse(range[1]);

                long end = Math.Min(start + (5000000), fileStream.Length - 1);

                var mediaStreamHelper = new MediaStreamHelper
                {
                    fis = fileStream
                };

                Response.StatusCode = 206;

                #region OLD
                //long rangeStart = long.Parse(range[1]);
                //long? rangeEnd = null;
                //if (range.Length > 3)
                //{
                //    if (long.TryParse(range[2].ToString(), out long rEnd))
                //    {
                //        rangeEnd = rEnd;
                //    }
                //}
                //mediaStreamHelper.TryReadRangeItem(rangeStart, rangeEnd, fileStream.Length, out long start, out long end);
                //Response.ContentLength = end - start + 1;
                #endregion

                var contentRange = new ContentRangeHeaderValue(start, end, fileStream.Length);

                Response.Headers["Content-Range"] = contentRange.ToString();

                var outputStream = this.Response.Body;

                await mediaStreamHelper.CreatePartialContentAsync(outputStream, start, end);

                Response.Clear();
            }
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