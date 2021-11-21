using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Runtime.Caching;
using RangeItemHeaderValue = Microsoft.Net.Http.Headers.RangeItemHeaderValue;

namespace Devfreco.MediaServer.Web.Controllers
{
    public class MediaServerController : Controller
    {
        #region Properties

        // This will be used in copying input stream to output stream.
        public const int ReadStreamBufferSize = 1024 * 1024;
        // We have a read-only dictionary for mapping file extensions and MIME names. 
        public IReadOnlyDictionary<string, string> MimeNames;
        // We will discuss this later.
        public IReadOnlyCollection<char> InvalidFileNameChars;
        // Where are your videos located at? Change the value to any folder you want.
        public static readonly string InitialDirectory;

        //private readonly IMediaServerService _mediaServerService;
        private static ObjectCache cache = MemoryCache.Default;
        #endregion

        #region Ctor

        public MediaServerController()
        {
            var mimeNames = new Dictionary<string, string>
            {
                {".mp3", "audio/mpeg"}, // List all supported media types; 
                {".mp4", "video/mp4"},
                {".ogg", "application/ogg"},
                {".ogv", "video/ogg"},
                {".oga", "audio/ogg"},
                {".wav", "audio/x-wav"},
                {".webm", "video/webm"}
            };

            MimeNames = new ReadOnlyDictionary<string, string>(mimeNames);
            InvalidFileNameChars = Array.AsReadOnly(Path.GetInvalidFileNameChars());
            //_mediaServerService = mediaServerService;
        }

        #endregion

        #region Method

        //[HttpGet("GetFileById/{id}")]
        //public async Task<object> GetFileById(string id)
        //{
        //    var result = await _mediaServerService.GetByIdAsync(id);
        //    return result;
        //}

        public HttpResponseMessage GetVideoById(string id)
        {
            Dev.Services.MediaStreamHelper streamline;
            if (cache.Contains("MediaStream-" + id))
                streamline = (Dev.Services.MediaStreamHelper)cache.Get("MediaStream-" + id);
            else
            {
                streamline = new Dev.Services.MediaStreamHelper(id);
                cache.Set("MediaStream-" + id, streamline, DateTime.Now.AddSeconds(30.0));
            }
            if (streamline.FileSize == 0)
                throw new Exception();

            var rangeHeader = Request.GetTypedHeaders().Range;
            HttpResponseMessage response = new HttpResponseMessage();

            response.Headers.AcceptRanges.Add("bytes");

            // The request will be treated as normal request if there is no Range header.
            if (rangeHeader == null || !rangeHeader.Ranges.Any())
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new PushStreamContent(async (outputStream, httpContent, transpContext)
                =>
                {
                    using (outputStream)
                        await streamline.CreatePartialContent(outputStream, 0, streamline.FileSize, id);
                }
                , GetMimeNameFromExt(streamline.FileExt));

                response.Content.Headers.ContentLength = streamline.FileSize;
                response.Content.Headers.ContentType = GetMimeNameFromExt(streamline.FileExt);

                return response;
            }
            long start = 0, end = 0;
            // 1. If the unit is not 'bytes'.
            // 2. If there are multiple ranges in header value.
            // 3. If start or end position is greater than file length.
            if (rangeHeader.Unit != "bytes" || rangeHeader.Ranges.Count > 1 ||
                !TryReadRangeItem(rangeHeader.Ranges.First(), streamline.FileSize, out start, out end))
            {
                response.StatusCode = HttpStatusCode.RequestedRangeNotSatisfiable;
                response.Content = new StreamContent(Stream.Null);  // No content for this status.
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(streamline.FileSize);
                response.Content.Headers.ContentType = GetMimeNameFromExt(streamline.FileExt);

                return response;
            }

            var contentRange = new ContentRangeHeaderValue(start, end, streamline.FileSize);

            // We are now ready to produce partial content.
            response.StatusCode = HttpStatusCode.PartialContent;

            response.Content = new PushStreamContent(async (outputStream, httpContent, transpContext) =>
            {
                using (outputStream) // Copy the file to output stream in indicated range.
                    await streamline.CreatePartialContent(outputStream, start, end, id);

            }, GetMimeNameFromExt(streamline.FileExt));
            response.Content.Headers.ContentRange = contentRange;

            return response;

        }

        //[HttpPost("upload")]
        //public FileResponseModel Upload(IFormFile fileToUpload, string file, int num)
        //{
        //    var result = _mediaServerService.FileUpload(fileToUpload, file, num);
        //    return result;
        //}

        //[HttpPost("uploadComplete")]
        //public async Task<Dev.Data.Mongo.Media.MediaServer> UploadComplete(string fileName)
        //{
        //    return await _mediaServerService.AddAsync(fileName);
        //}

        //[HttpPost("uploadComplete/{id}")]
        //public async Task<Dev.Data.Mongo.Media.MediaServer> UploadComplete(string fileName, string id)
        //{
        //    return await _mediaServerService.UpdateAsync(fileName, id);
        //}

        //[HttpDelete("{id}")]
        //public async Task<Dev.Data.Mongo.Media.MediaServer> Delete(string id)
        //{
        //    return await _mediaServerService.DeleteAsync(id);
        //}

        #endregion
        #region Others
        private MediaTypeHeaderValue GetMimeNameFromExt(string ext)
        {
            string value;

            if (MimeNames.TryGetValue(ext.ToLowerInvariant(), out value))
                return new MediaTypeHeaderValue(value);
            else
                return new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
        }

        private bool TryReadRangeItem(RangeItemHeaderValue range, long contentLength,
           out long start, out long end)
        {
            if (range.From != null)
            {
                start = range.From.Value;
                if (range.To != null)
                    end = range.To.Value;
                else
                    end = contentLength - 1;
            }
            else
            {
                end = contentLength - 1;
                if (range.To != null)
                    start = contentLength - range.To.Value;
                else
                    start = 0;
            }
            return (start < contentLength && end < contentLength);
        }
        #endregion
    }
}
