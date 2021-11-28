using Dev.Core.IO.Model;
using Dev.Core.IoC;
using Dev.Dto.Mongo;
using Microsoft.AspNetCore.Http;

namespace Dev.Services
{
    public interface IMediaServerService : IService
    {
        FileResponseModel FileUpload(IFormFile fileToUpload, string file, int num);
        Task<(byte[] fileByte, string fileName)> FileDownload(string id);
        Task<MediaDto> GetByIdAsync(string id);
        Task<string> GetImageByIdAsync(string id);
        Task<Stream> GetFileByIdAsync(string id);
        Task<string> AddAsync(string fileName);
        Task<string> UpdateAsync(string id, string fileName);
        Task<bool> DeleteAsync(string id);
    }
}