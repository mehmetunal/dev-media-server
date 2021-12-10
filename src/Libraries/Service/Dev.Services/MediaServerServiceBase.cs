namespace Dev.Services
{
    public class MediaServerServiceBase
    {

        private async Task FileTypeProsess(out string duration, string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                var mimeType = MimeKit.MimeTypes.GetMimeType(filePath);
                await IsVideoAsync(filePath, mimeType, out duration);
            }
        }
    }
}