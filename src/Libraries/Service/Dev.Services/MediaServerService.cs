using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dev.Core.Infrastructure;
using Dev.Core.IO;
using Dev.Core.IO.Model;
using Dev.Data.Mongo.Media;
using Dev.Mongo.Repository;
using Microsoft.AspNetCore.Http;

namespace Dev.Services
{
    public class MediaServerService : IMediaServerService
    {
        #region Properties

        private string _tempFolder;
        private readonly string _root;
        private string path = "";
        private readonly IFilesManager _filesManager;
        private readonly IDevFileProvider _devFileProvider;
        private readonly IMongoRepository<DevMediaServer> _repository;

        #endregion

        #region Ctor

        public MediaServerService(
            IFilesManager filesManager,
            IDevFileProvider devFileProvider,
            IMongoRepository<DevMediaServer> repository)
        {
            path = RemoteIp.Replace(".", "").Replace(":", "").Replace(";", "");
            _filesManager = filesManager;
            _devFileProvider = devFileProvider;
            _root = $"{_devFileProvider.GetAbsolutePath("/")}/{path}";
            _tempFolder = $"{_root}/Temp";
            _repository = repository;
        }

        #endregion

        #region Method

        public FileResponseModel FileUpload(IFormFile fileToUpload, string file, int num)
        {
            var fileName = file + num;
            _filesManager.FolderCreat(_tempFolder);
            var fileInfo = _filesManager.FilesCreat(_tempFolder, fileToUpload, fileName);
            return fileInfo;
        }

        public virtual async Task<DevMediaServer> GetByIdAsync(string id)
        {
            var result = await _repository.FindByIdAsync(id);
            return result;
        }

        public virtual async Task<DevMediaServer> AddAsync(string fileName)
        {
            var newFilePath = FileMove(fileName);
            var mediaServer = LoadMediaServer(newFilePath);
            var result = await _repository.AddAsync(mediaServer);
            return result;
        }

        public virtual async Task<DevMediaServer> UpdateAsync(string id, string fileName)
        {
            var resultMediaServer = await _repository.FindByIdAsync(id);
            _filesManager.Delete(resultMediaServer.Path);
            var newFilePath = FileMove(fileName);
            var mediaServer = LoadMediaServer(newFilePath);
            mediaServer.Id = resultMediaServer.Id;
            mediaServer.CreatedDate = resultMediaServer.CreatedDate;
            mediaServer.CreatorIP = resultMediaServer.CreatorIP;
            var result = await _repository.UpdateAsync(mediaServer);
            return result;
        }

        public virtual async Task<DevMediaServer> DeleteAsync(string id)
        {
            var resultMediaServer = await _repository.FindByIdAsync(id);
            _filesManager.Delete(resultMediaServer.Path);
            var result = await _repository.DeleteAsync(resultMediaServer);
            return result;
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

            _devFileProvider.FileMove(Path.Combine(tempPath, fileName), Path.Combine(_root, fileName));

            var newFilePath = Path.Combine(_root, fileName);

            return newFilePath;
        }

        private DevMediaServer LoadMediaServer(string newFilePath)
        {
            FileInfo info = new(newFilePath);
            var mediaServer = new DevMediaServer();
            mediaServer.Path = $"{path}/{info.Name}";
            mediaServer.Size = info.Length;
            mediaServer.FileExtensions = info.Extension;
            mediaServer.FileName = info.Name;
            mediaServer.CreatorIP = RemoteIp;
            return mediaServer;
        }

        #endregion
    }
}