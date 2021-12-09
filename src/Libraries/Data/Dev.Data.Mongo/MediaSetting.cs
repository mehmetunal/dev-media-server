namespace Dev.Data.Mongo
{
    public class MediaSetting : ISetting
    {
        /// <summary>
        /// Resimlerin veritabanında mı yoksa dosya sisteminde mi saklandığını belirten bir değer.
        /// </summary>
        public bool FilesStoredIntoDatabase { get; set; }
    }
}
