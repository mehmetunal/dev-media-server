using Dev.Core.Infrastructure;
using Dev.Core.IO;
using Dev.Core.IO.Model;
using Dev.Data.Mongo;
using Dev.Dto.Mongo;
using Dev.Mongo.Repository;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;
using Xabe.FFmpeg;

namespace Dev.Services
{
    public class MediaServerService : IMediaServerService
    {
        #region Properties
        private string path = "";
        private readonly string _root;
        private readonly string _tempFolder;
        private readonly MediaSetting _mediaSetting;
        private readonly IFilesManager _filesManager;
        private readonly IDevFileProvider _devFileProvider;
        private readonly IGridFsRepository _gridFsRepository;
        private readonly IMongoRepository<DevMedia> _mongoRepository;

        #endregion

        #region Ctor

        public MediaServerService(
            MediaSetting mediaSetting,
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
            _mediaSetting = mediaSetting;
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

            var mediaDto = new MediaDto();

            if (_mediaSetting.FilesStoredIntoDatabase)
                mediaDto = await GetFileStoredIntoDatabaseById(objid);
            else
                mediaDto = await GetFileStoredIntoFolderById(objid);

            return mediaDto;
        }

        public virtual async Task<string> GetImageByIdAsync(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId objid))
                throw new ArgumentException($"{objid} not equils type ObjectId");

            byte[] image = Array.Empty<byte>();
            if (_mediaSetting.FilesStoredIntoDatabase)
            {
                image = await _gridFsRepository.DownloadAsBytesAsync(objid);
                if (image == null)
                    throw new ArgumentNullException();
            }
            else
            {
                var result = await _mongoRepository.FindByIdAsync(id);
                if (result != null)
                {
                    image = _devFileProvider.ReadAllBytes(result.Path);
                }
            }


            return Convert.ToBase64String(image);
        }

        public virtual async Task<string> AddAsync(string fileName)
        {
            var newFilePath = FileMove(fileName);
            var stream = new FileStream(path: newFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileInfo info = new(newFilePath);

            if (_mediaSetting.FilesStoredIntoDatabase)
            {
                var fileInfo = await _gridFsRepository.UploadFileAsync(stream, info.Name);
                if (fileInfo == null)
                    throw new ArgumentNullException(nameof(fileInfo));

                return fileInfo;
            }
            var devMedia = GetDevMedia(info);
            if (devMedia == null)
                throw new ArgumentNullException(nameof(devMedia));
            if (devMedia != null)
            {
                var result = await _mongoRepository.AddAsync(devMedia);
                return result.Id.ToString();
            }
            return null;
        }

        public async Task<(byte[] fileByte, string fileName)> FileDownload(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId objid))
                throw new ArgumentException($"{objid} not equils type ObjectId");

            byte[]? resultByte = Array.Empty<byte>();
            if (_mediaSetting.FilesStoredIntoDatabase)
            {
                var gridFsData = (await _gridFsRepository.GetFileById(objid)).FirstOrDefault();
                if (gridFsData == null)
                    throw new ArgumentNullException(nameof(gridFsData));

                resultByte = await _gridFsRepository.DownloadAsBytesAsync(objid);
                if (resultByte == null)
                    throw new ArgumentNullException(nameof(resultByte));

                return (resultByte, gridFsData.Filename);
            }

            var file = await _mongoRepository.FindByIdAsync(id);
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            resultByte = _devFileProvider.ReadAllBytes(file.Path);

            return (resultByte, file.Name);
        }

        public async Task<Stream> GetFileByIdAsync(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId objid))
                throw new ArgumentException($"{objid} not equils type ObjectId");

            var stream = new MemoryStream();
            if (_mediaSetting.FilesStoredIntoDatabase)
            {
                await _gridFsRepository.DownloadToStreamAsync(objid, stream);
            }
            else
            {
                var file = await _mongoRepository.FindByIdAsync(objid);
                using var fs = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                fs.CopyTo(stream);
                return stream;
            }

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

            if (_mediaSetting.FilesStoredIntoDatabase)
            {
                var fileId = await _gridFsRepository.UploadFileAsync(stream, fileInfo.Name);
                if (fileId == null)
                    throw new ArgumentNullException(nameof(fileId));

                return fileId;

            }
            var devMedia = GetDevMedia(fileInfo);
            devMedia.Id = objid;

            if (devMedia == null)
                throw new ArgumentNullException(nameof(devMedia));
            if (devMedia != null)
            {
                var result = await _mongoRepository.AddAsync(devMedia);
                return result.Id.ToString();
            }

            return null;
        }

        public virtual async Task<bool> DeleteAsync(string id)
        {
            try
            {
                if (!ObjectId.TryParse(id, out ObjectId objid))
                    throw new ArgumentException($"{objid} not equils type ObjectId");

                var file = await GetByIdAsync(id);
                if (file != null)
                {
                    var fullPath = Path.Combine(_root, file.Name);
                    _filesManager.Delete(fullPath);
                }

                if (_mediaSetting.FilesStoredIntoDatabase)
                {
                    await _gridFsRepository.DeleteAsync(objid);
                }
                else
                {
                    await _mongoRepository.DeleteAsync(objid);
                }

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

        private async Task<MediaDto> GetFileStoredIntoFolderById(ObjectId objid)
        {
            var mediaDto = new MediaDto();
            var file = await _mongoRepository.FindByIdAsync(objid);
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            FileInfo fileInfo = new(file.Path);
            var devMedia = GetDevMedia(fileInfo);
            if (mediaDto != null)
            {
                mediaDto.Id = devMedia.Id.ToString();
                mediaDto.Name = devMedia.Name;
                mediaDto.Length = devMedia.Length;
                mediaDto.Extensions = devMedia.Extensions;
                mediaDto.Size = devMedia.Size;
            }
            return mediaDto;
        }

        private async Task<MediaDto> GetFileStoredIntoDatabaseById(ObjectId objid)
        {
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

            FileTypeProsess(gridFsData.Filename, out string duration);
            mediaDto.Duration = duration;
            return mediaDto;
        }

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

        private DevMedia GetDevMedia(FileInfo fileInfo)
        {
            var devMedia = new DevMedia()
            {
                Name = fileInfo.Name,
                Path = fileInfo.FullName,
                Length = fileInfo.Length,
                Size = _filesManager.FormatFileSize(fileInfo.Length),
                Extensions = Path.GetExtension(fileInfo.FullName),
            };

            string duration;
            FileTypeProsess(fileInfo.FullName, out duration);
            devMedia.Duration = duration;

            return devMedia;
        }

        private void FileTypeProsess(string filePath, out string duration)
        {
            duration = null;
            if (!string.IsNullOrEmpty(filePath))
            {
                var mimeType = MimeKit.MimeTypes.GetMimeType(filePath);
                IsVideo(filePath, mimeType, out duration);
            }
        }

        private void IsVideo(string filePath, string? mimeType, out string duration)
        {
            duration = null;
            if (!string.IsNullOrEmpty(mimeType) && mimeType.StartsWith("video"))
            {
                var getDuration = GetVideoDuration(filePath);
                if (getDuration != null)
                {
                    duration = getDuration.ToString();
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private TimeSpan? GetVideoDuration(string filePath)
        {
            var mediaInfo = MediaInfo.Get(filePath).GetAwaiter().GetResult();
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