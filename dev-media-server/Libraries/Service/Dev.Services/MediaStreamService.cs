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

        #endregion

        public MediaStreamHelper(Dev.Data.Mongo.Media.MediaServer mediaServer)
        {
            _bufferPath = mediaServer.Path;
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
                                count = await sfs.ReadAsync(buffer, 0, Math.Min((int) remainingBytes, ReadStreamBufferSize));
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