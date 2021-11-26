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
        readonly string _bufferPath;
        public string id { get; set; }
        public long FileSize { get; set; }
        public string FileType { get; set; }
        public string FileExt { get; set; }
        public FileStream fis { get; set; }

        #endregion
      
        public  MediaStreamHelper(string id, IMediaServerService _mediaServerService)
        {
            var mediaServer = _mediaServerService.GetByIdAsync(id).GetAwaiter().GetResult();
            if (mediaServer != null)
            {
                FileSize = mediaServer.Size;
                FileType = mediaServer.FileExtensions.Replace(".", "");
                FileExt = mediaServer.FileExtensions;
                _bufferPath = mediaServer.Path;
            }

            if (File.Exists(_bufferPath))
            {
                fis = new FileStream(path: _bufferPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                FileSize = fis.Length;
            }
        }

        public void SetPosition(long position)
        {
            fis.Seek(position, SeekOrigin.Begin);
        }

        /// <summary>
        /// Load Media From SQL Server FileStream in the form of chunked parts
        /// </summary>
        /// <param name="outputStream">If exists in local buffer (ram disk || fast tempropy disk) it won't go to fetch data from SQL Server</param>
        /// <param name="start">Start range byte of media to play</param>
        /// <param name="end">End range byte of media to play</param>
        /// <param name="id">Id of media to find</param>
        /// <returns></returns>
        public async Task CreatePartialContent(Stream outputStream, long start, long end, bool isCancellationRequested)
        {
            try
            {
                int length;
                long videSize = fis.Length;
                byte[] buffer = new Byte[ReadStreamBufferSize];

                long remainingBytes = end - start + 1;

                while (videSize > 0)
                {
                    // Verify that the client is connected.
                    if (isCancellationRequested == false)
                    {
                        // Read the data in buffer.
                        length = await fis.ReadAsync(buffer, 0, Math.Min((int)remainingBytes, ReadStreamBufferSize));

                        // Write the data to the current output stream.
                        await outputStream.WriteAsync(buffer, 0, length);
                        // Flush the data to the HTML output.
                        outputStream.Flush();

                        buffer = new Byte[buffer.Length];
                        videSize = videSize - buffer.Length;
                    }
                    else
                    {
                        //prevent infinite loop if user disconnects
                        videSize = -1;
                    }
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                if (fis != null)
                {
                    //Close the file.
                    fis.Close();
                }
            }
        }

    }
}