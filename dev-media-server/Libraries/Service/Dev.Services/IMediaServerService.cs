using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dev.Core.IO.Model;
using Dev.Core.IoC;
using Dev.Data.Mongo.Media;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;

namespace Dev.Services
{
    public interface IMediaServerService : IService
    {
        FileResponseModel FileUpload(IFormFile fileToUpload, string file, int num);
        Task<MediaServer> GetByIdAsync(string id);
        Task<MediaServer> AddAsync(string fileName);
        Task<MediaServer> UpdateAsync(string id, string fileName);
        Task<MediaServer> DeleteAsync(string id);
    }
}