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
using System.Threading;
using System.Threading.Tasks;
using Dev.Core.IO;
using Dev.Core.IO.Model;
using Dev.Framework.Exceptions;
using Dev.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RangeItemHeaderValue = Microsoft.Net.Http.Headers.RangeItemHeaderValue;

namespace Devfreco.MediaServer.Controllers
{
    [Route("api/v{version:apiVersion}/media-server")]
    public class MediaServerController : DevBaseController
    {
        #region Properties

        private readonly IMediaServerService _mediaServerService;

        // We have a read-only dictionary for mapping file extensions and MIME names. 
        private static IReadOnlyDictionary<string, string> _mimeNames;
        private static ObjectCache cache = MemoryCache.Default;

        #region Constructors

        #endregion

        #endregion

        #region Ctor

        public MediaServerController(IFilesManager filesManager,
            IDevFileProvider devFileProvider,
            IMediaServerService mediaServerService)
        {
            _mediaServerService = mediaServerService;
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

            _mimeNames = new ReadOnlyDictionary<string, string>(mimeNames);
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
        public HttpResponseMessage GetVideoById(string id)
        {
            var mediaServer = new Dev.Data.Mongo.Media.MediaServer();
            MediaStreamHelper streamline;
            if (cache.Contains("MediaStream-" + id))
                streamline = (MediaStreamHelper)cache.Get("MediaStream-" + id);
            else
            {
                mediaServer = _mediaServerService.GetByIdAsync(id).GetAwaiter().GetResult();
                streamline = new MediaStreamHelper(mediaServer);
                cache.Set("MediaStream-" + id, streamline, DateTime.Now.AddSeconds(30.0));
            }

            if (mediaServer.Size == 0)
                throw new NotFoundException();

            var rangeHeader = Request.GetTypedHeaders().Range;
            var response = new HttpResponseMessage();

            response.Headers.AcceptRanges.Add("bytes");

            // The request will be treated as normal request if there is no Range header.
            if (rangeHeader == null || !rangeHeader.Ranges.Any())
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new PushStreamContent(async (outputStream, httpContent, transpContext)
                        =>
                    {
                        using (outputStream)
                            await streamline.CreatePartialContent(outputStream, 0, mediaServer.Size, id);
                    }
                    , GetMimeNameFromExt(mediaServer.FileExtensions));

                response.Content.Headers.ContentLength = mediaServer.Size;
                response.Content.Headers.ContentType = GetMimeNameFromExt(mediaServer.FileExtensions);

                return response;
            }

            long start = 0, end = 0;
            // 1. If the unit is not 'bytes'.
            // 2. If there are multiple ranges in header value.
            // 3. If start or end position is greater than file length.
            if (rangeHeader.Unit != "bytes" || rangeHeader.Ranges.Count > 1 ||
                !TryReadRangeItem(rangeHeader.Ranges.First(), mediaServer.Size, out start, out end))
            {
                response.StatusCode = HttpStatusCode.RequestedRangeNotSatisfiable;
                response.Content = new StreamContent(Stream.Null); // No content for this status.
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(mediaServer.Size);
                response.Content.Headers.ContentType = GetMimeNameFromExt(mediaServer.FileExtensions);

                return response;
            }

            var contentRange = new ContentRangeHeaderValue(start, end, mediaServer.Size);

            // We are now ready to produce partial content.
            response.StatusCode = HttpStatusCode.PartialContent;

            response.Content = new PushStreamContent(async (outputStream, httpContent, transpContext)
                =>
            {
                using (outputStream) // Copy the file to output stream in indicated range.
                    await streamline.CreatePartialContent(outputStream, start, end, id);
            }, GetMimeNameFromExt(mediaServer.FileExtensions));
            response.Content.Headers.ContentRange = contentRange;

            return response;
        }


        [HttpPost("upload")]
        public FileResponseModel Upload(IFormFile fileToUpload, string file, int num)
        {
            var result = _mediaServerService.FileUpload(fileToUpload, file, num);
            return result;
        }

        [HttpPost("uploadComplete")]
        public async Task<Dev.Data.Mongo.Media.MediaServer> UploadComplete(string fileName)
        {
            return await _mediaServerService.AddAsync(fileName);
        }

        [HttpPost("uploadComplete/{id}")]
        public async Task<Dev.Data.Mongo.Media.MediaServer> UploadComplete(string fileName, string id)
        {
            return await _mediaServerService.UpdateAsync(fileName, id);
        }

        [HttpDelete("{id}")]
        public async Task<Dev.Data.Mongo.Media.MediaServer> Delete(string id)
        {
            return await _mediaServerService.DeleteAsync(id);
        }

        #endregion

        #region Others

        private static MediaTypeHeaderValue GetMimeNameFromExt(string ext)
        {
            string value;

            if (_mimeNames.TryGetValue(ext.ToLowerInvariant(), out value))
                return new MediaTypeHeaderValue(value);
            else
                return new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
        }

        private static bool TryReadRangeItem(RangeItemHeaderValue range, long contentLength,
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