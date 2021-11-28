using Dev.Core.Infrastructure;
using Dev.Core.IO;
using Dev.Core.IO.Model;
using Dev.Dto.Mongo;
using Dev.Mongo.Repository;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver;

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

        #endregion

        #region Ctor

        public MediaServerService(
            IFilesManager filesManager,
            IDevFileProvider devFileProvider,
            IGridFsRepository gridFsRepository)
        {
            path = RemoteIp.Replace(".", "").Replace(":", "").Replace(";", "");
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

        //public virtual async Task UpdateAsync(string id, string fileName)
        //{
        //    var resultMediaServer = await _gridFsRepository.DeleteAsync(id);
        //    _filesManager.Delete(resultMediaServer.Path);
        //    var newFilePath = FileMove(fileName);
        //    var mediaServer = LoadMediaServer(newFilePath);
        //    mediaServer.Id = resultMediaServer.Id;
        //    mediaServer.CreatedDate = resultMediaServer.CreatedDate;
        //    mediaServer.CreatorIP = resultMediaServer.CreatorIP;
        //    var result = await _repository.UpdateAsync(mediaServer);
        //    return result;
        //}

        public virtual async Task<string> UpdateAsync(string id, string fileName)
        {
            if (!ObjectId.TryParse(id, out ObjectId objid))
                throw new ArgumentException($"{objid} not equils type ObjectId");

            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));

            await DeleteAsync(id);
            //var file = await GetByIdAsync(id);
            //if (file == null)
            //    throw new ArgumentNullException(nameof(file));

            //var fullPath = Path.Combine(_root, file.Name);

            //_filesManager.Delete(fullPath);

            //await _gridFsRepository.DeleteAsync(objid);

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

        //public virtual async Task<DevMediaServer> DeleteAsync(string id)
        //{
        //    throw new NotImplementedException();
        //var resultMediaServer = await _repository.FindByIdAsync(id);
        //_filesManager.Delete(resultMediaServer.Path);
        //var result = await _repository.DeleteAsync(resultMediaServer);
        //return result;
        //}

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

            _devFileProvider.FileMove(Path.Combine(tempPath, fileName), Path.Combine(_root, fileName));

            var newFilePath = Path.Combine(_root, fileName);

            return newFilePath;
        }

        //private DevMediaServer LoadMediaServer(string newFilePath)
        //{
        //    FileInfo info = new(newFilePath);
        //    var mediaServer = new DevMediaServer();
        //    mediaServer.Path = $"{path}/{info.Name}";
        //    mediaServer.Size = info.Length;
        //    mediaServer.FileExtensions = info.Extension;
        //    mediaServer.FileName = info.Name;
        //    mediaServer.CreatorIP = RemoteIp;
        //    return mediaServer;
        //}

        #endregion
    }
}