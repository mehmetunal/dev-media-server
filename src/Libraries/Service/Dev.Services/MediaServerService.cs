using Dev.Core.Infrastructure;
using Dev.Core.IO;
using Dev.Core.IO.Model;
using Dev.Data.Mongo;
using Dev.Dto.Mongo;
using Dev.Mongo.Repository;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver;
using Xabe.FFmpeg;

namespace Dev.Services
{
    public class MediaServerService : IMediaServerService
    {
        #region Properties

        private readonly string _tempFolder;
        private readonly string _root;
        private string path = "";
        private readonly IFilesManager _filesManager;
        private readonly IDevFileProvider _devFileProvider;
        private readonly IGridFsRepository _gridFsRepository;
        private readonly IMongoRepository<DevMedia> _mongoRepository;

        #endregion

        #region Ctor

        public MediaServerService(
            IFilesManager filesManager,
            IDevFileProvider devFileProvider,
            IGridFsRepository gridFsRepository,
            IMongoRepository<DevMedia> mongoRepository)
        {
            _mongoRepository = mongoRepository;
            path = RemoteIp.Replace(".", "-").Replace(":", "--").Replace(";", "--");
            _filesManager = filesManager;
            _devFileProvider = devFileProvider;
            _root = $"{_devFileProvider.GetAbsolutePath("/")}/{path}";
            _tempFolder = $"{_root}/Temp";
            _gridFsRepository = gridFsRepository;
        }

        #endregion

        #region Method

        public FileResponseModel FileUpload(IFormFile fileToUpload, string file, int num)
        {
            var fileName = file + num;
            _filesManager.FolderCreat(_tempFolder);
            var fileInfo = _filesManager.FilesCreat(_tempFolder, fileToUpload, fileName);

            if (fileInfo == null)
                throw new ArgumentNullException(nameof(fileInfo));

            return fileInfo;
        }
        public virtual async Task<MediaDto> GetByIdAsync(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId objid))
                throw new ArgumentException($"{objid} not equils type ObjectId");

            var gridFsData = (await _gridFsRepository.GetFileById(objid)).FirstOrDefault();

            if (gridFsData == null)
                throw new ArgumentNullException(nameof(gridFsData));

            var mediaDto = new MediaDto
            {
                Id = gridFsData.Id.ToString(),
                Name = gridFsData.Filename.Replace(Path.GetExtension(gridFsData.Filename), ""),
                Length = gridFsData.Length,
                Extensions = Path.GetExtension(gridFsData.Filename),
                Size = _filesManager.FormatFileSize(gridFsData.Length),
            };

            var mimeType = MimeKit.MimeTypes.GetMimeType(gridFsData.Filename);
            if (mimeType.StartsWith("video"))
            {
                var videoFilePath = Path.Combine(_root, gridFsData.Filename);
                var mediaInfo = await MediaInfo.Get(videoFilePath);

                var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
                if (videoStream != null)
                {
                    var videoDuration = videoStream?.Duration;
                    mediaDto.Duration = videoDuration.ToString();
                }
            }

            return mediaDto;
        }

        public virtual async Task<string> GetImageByIdAsync(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId objid))
                throw new ArgumentException($"{objid} not equils type ObjectId");

            var image = await _gridFsRepository.DownloadAsBytesAsync(objid);
            if (image == null)
                throw new ArgumentNullException();

            return Convert.ToBase64String(image);
        }

        public virtual async Task<string> AddAsync(string fileName)
        {
            var newFilePath = FileMove(fileName);
            var stream = new FileStream(path: newFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileInfo info = new(newFilePath);

            var fileInfo = await _gridFsRepository.UploadFileAsync(stream, info.Name);
            if (fileInfo == null)
                throw new ArgumentNullException(nameof(fileInfo));

            return fileInfo;
        }

        public async Task<(byte[] fileByte, string fileName)> FileDownload(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId objid))
                throw new ArgumentException($"{objid} not equils type ObjectId");

            var gridFsData = (await _gridFsRepository.GetFileById(objid)).FirstOrDefault();
            if (gridFsData == null)
                throw new ArgumentNullException(nameof(gridFsData));

            var resultByte = await _gridFsRepository.DownloadAsBytesAsync(objid);
            if (resultByte == null)
                throw new ArgumentNullException(nameof(resultByte));

            return (resultByte, gridFsData.Filename);
        }


        public async Task<Stream> GetFileByIdAsync(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId objid))
                throw new ArgumentException($"{objid} not equils type ObjectId");

            var stream = new MemoryStream();
            await _gridFsRepository.DownloadToStreamAsync(objid, stream);

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            return stream;
        }

        public virtual async Task<string> UpdateAsync(string id, string fileName)
        {
            if (!ObjectId.TryParse(id, out ObjectId objid))
                throw new ArgumentException($"{objid} not equils type ObjectId");

            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));

            await DeleteAsync(id);

            var newFilePath = FileMove(fileName);

            var stream = new FileStream(path: newFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileInfo fileInfo = new(newFilePath);
            if (fileInfo == null)
                throw new ArgumentNullException(nameof(fileInfo));

            var fileId = await _gridFsRepository.UploadFileAsync(stream, fileInfo.Name);
            if (fileId == null)
                throw new ArgumentNullException(nameof(fileId));

            return fileId;
        }

        public virtual async Task<bool> DeleteAsync(string id)
        {
            try
            {
                if (!ObjectId.TryParse(id, out ObjectId objid))
                    throw new ArgumentException($"{objid} not equils type ObjectId");

                var file = await GetByIdAsync(id);

                var fullPath = Path.Combine(_root, file.Name);
                _filesManager.Delete(fullPath);

                await _gridFsRepository.DeleteAsync(objid);

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        protected virtual string RemoteIp => EngineContext.Current.Resolve<IHttpContextAccessor>().HttpContext.Connection.RemoteIpAddress.ToString();

        #region Private

        private string FileMove(string fileName)
        {
            string tempPath = _tempFolder;
            string newPath = Path.Combine(tempPath, fileName);
            string[] filePaths = _devFileProvider.GetFiles(tempPath).Where(p => p.Contains(fileName)).OrderBy(p => Int32.Parse(p.Replace(fileName, "$").Split('$')[1])).ToArray();
            foreach (string filePath in filePaths)
            {
                _filesManager.MergeChunks(newPath, filePath);
            }

            var fileInfo = _filesManager.FileRandomName(Path.Combine(tempPath, fileName));
            var newFilePath = Path.Combine(_root, fileInfo.Name);
            _devFileProvider.FileMove(fileInfo.FullName, newFilePath);

            return newFilePath;
        }


        private async Task<DevMedia> GetDevMediaAsync(FileInfo fileInfo)
        {
            var devMedia = new DevMedia()
            {
                Name = fileInfo.Name,
                Path = fileInfo.FullName,
                Length = fileInfo.Length,
                Size = _filesManager.FormatFileSize(fileInfo.Length),
                Extensions = Path.GetExtension(fileInfo.FullName),
            };
            if (fileInfo != null && !string.IsNullOrEmpty(fileInfo.FullName))
            {
                var mimeType = MimeKit.MimeTypes.GetMimeType(fileInfo.FullName);
                await IsVideo(fileInfo, devMedia, mimeType);
            }

            return devMedia;
        }

        private static async Task IsVideo(FileInfo fileInfo, DevMedia devMedia, string mimeType)
        {
            if (mimeType.StartsWith("video"))
            {
                var duration = await GetDuration(fileInfo);
                if (duration != null)
                {
                    devMedia.Duration = duration.ToString();
                }
            }
        }

        private static async Task<TimeSpan?> GetDuration(FileInfo fileInfo)
        {
            var mediaInfo = await MediaInfo.Get(fileInfo.FullName);
            if (mediaInfo != null)
            {
                var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
                if (videoStream != null)
                {
                    var videoDuration = videoStream?.Duration;
                    return videoDuration;
                }
            }
            return null;
        }

        #endregion
    }
}