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
        Task<DevMediaServer> GetByIdAsync(string id);
        Task<DevMediaServer> AddAsync(string fileName);
        Task<DevMediaServer> UpdateAsync(string id, string fileName);
        Task<DevMediaServer> DeleteAsync(string id);
    }
}