using System;
using System.IO;
using System.Threading.Tasks;

namespace Dev.Services
{
    public class MediaStreamHelper
    {
        #region Property

        // Chunk file size in byte
        public const int ReadStreamBufferSize = 256 * 1024;
        private readonly bool _inBuffer = false;
        readonly string _bufferPath;
        public string id { get; set; }
        public long FileSize { get; set; }
        public string FileType { get; set; }
        public string FileExt { get; set; }
        #endregion
        public class MediaServer
        {
            public virtual string FileName { get; set; }
            public virtual string Path { get; set; }
            public virtual string FileExtensions { get; set; }
            public virtual long Size { get; set; }
        }
        public MediaStreamHelper(string id)
        {
            var mediaServer = new MediaServer()
            {
                FileExtensions = ".webm",
                FileName = "Untitled_ Oct 6, 2021 4_30 PM.webm",
                Path = @"M:\Devfreco\Projeler\dev-media-server\dev-media-server\Presentation\Devfreco.MediaServer.Api\wwwroot\Untitled_ Oct 6, 2021 4_30 PM.webm",
                Size = 94805872

            }; //_mediaServerService.GetByIdAsync(id).GetAwaiter().GetResult();
            if (mediaServer != null)
            {
                FileSize = mediaServer.Size;
                FileType = mediaServer.FileExtensions.Replace(".", "");
                FileExt = mediaServer.FileExtensions;
                _bufferPath = mediaServer.Path;
            }

            if (System.IO.File.Exists(_bufferPath))
            {
                FileStream fis = null;
                try
                {
                    fis = System.IO.File.Open(_bufferPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    _inBuffer = true;
                }
                catch (Exception)
                {
                    _inBuffer = false;
                }

                try
                {
                    if (fis != null)
                        fis.Close();
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// Load Media From SQL Server FileStream in the form of chunked parts
        /// </summary>
        /// <param name="outputStream">If exists in local buffer (ram disk || fast tempropy disk) it won't go to fetch data from SQL Server</param>
        /// <param name="start">Start range byte of media to play</param>
        /// <param name="end">End range byte of media to play</param>
        /// <param name="id">Id of media to find</param>
        /// <returns></returns>
        public async Task CreatePartialContent(Stream outputStream, long start, long end, string id)
        {
            int count = 0;
            long remainingBytes = end - start + 1;
            long position = start;

            byte[] buffer = new byte[ReadStreamBufferSize];
            if (_inBuffer) //---------------------------(It's optional) LOAD FROM BUFFER----------------------------------------------
            {
                try
                {
                    using (FileStream sfs = new FileStream(_bufferPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, ReadStreamBufferSize, true))
                    {
                        sfs.Position = start;
                        do
                        {
                            try
                            {
                                count = await sfs.ReadAsync(buffer, 0, Math.Min((int)remainingBytes, ReadStreamBufferSize));
                                if (count <= 0) break;
                                await outputStream.WriteAsync(buffer, 0, count);
                            }
                            catch (Exception)
                            {
                                return;
                            }

                            position = sfs.Position;
                            remainingBytes = end - position + 1;
                        } while (position <= end);
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    await outputStream.FlushAsync();
                    outputStream.Close();
                }
            }
        }

    }
}