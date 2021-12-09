using Dev.Core.Infrastructure;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Enums;

namespace Dev.Services
{
    public class MediaStreamHelper
    {
        #region Property

        private readonly IBackgroundQueueService _queue = EngineContext.Current.Resolve<IBackgroundQueueService>();
        public Stream? Fis { get; set; }

        #endregion

        public async Task CreatePartialContentAsync(Stream outputStream, long start, long end)
        {
            var ReadStreamBufferSize = 256 * 1024;
            int count = 0;
            long remainingBytes = end - start + 1;
            long position = start;
            byte[] buffer = new byte[ReadStreamBufferSize];
            try
            {
                Fis.Position = start;
                do
                {
                    try
                    {
                        count = await Fis.ReadAsync(buffer, 0, Math.Min((int)remainingBytes, ReadStreamBufferSize));
                        if (count <= 0) break;
                        await outputStream.WriteAsync(buffer, 0, count);

                    }
                    catch (Exception)
                    {
                        return;
                    }
                    position = Fis.Position;
                    remainingBytes = end - position + 1;
                } while (position <= end);
            }
            finally
            {
                await outputStream.FlushAsync();
                outputStream.Close();
            }

        }

        public void TrimVideo(double start, double end, string inputPath, string outputPath)
        {
            _queue.QueueTask(async token =>
            {
                await ConvertVideo(start, end, inputPath, outputPath, token);
            });

        }

        public async Task<bool> ConvertVideo(double start, double end, string inputPath, string outputPath, CancellationToken ct)
        {
            try
            {
                var startSpan = TimeSpan.FromSeconds(start);
                var endSpan = TimeSpan.FromSeconds(end);
                var duration = endSpan - startSpan;

                var info = await MediaInfo.Get(inputPath);

                var videoStream = info.VideoStreams.First()
                    .SetCodec(VideoCodec.H264)
                    .SetSize(VideoSize.Hd480)
                    .Split(startSpan, duration);

                await Conversion.New()
                    .AddStream(videoStream)
                    .SetOutput(outputPath)
                    .Start(ct);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return true;
        }

        public bool TryReadRangeItem(long? rangeStart, long? rangeEnd,
            long contentLength, out long start, out long end)
        {
            if (rangeStart != null)
            {
                start = rangeStart.Value;
                if (rangeEnd != null)
                    end = rangeEnd.Value;
                else
                    end = contentLength - 1;
            }
            else
            {
                end = contentLength - 1;
                if (rangeEnd != null)
                    start = contentLength - rangeEnd.Value;
                else
                    start = 0;
            }
            return (start < contentLength && end < contentLength);
        }
    }
}