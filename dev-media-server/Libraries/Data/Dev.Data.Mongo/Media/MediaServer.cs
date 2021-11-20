using Dev.Data.Mongo.Attributes;
using MongoDB.Bson;

namespace Dev.Data.Mongo.Media
{
    [BsonCollection("media_server")]
    public class MediaServer : BaseEntity, IPrimaryKey<ObjectId>
    {
        public virtual string FileName { get; set; }
        public virtual string Path { get; set; }
        public virtual int FileType { get; set; }
        public virtual int FileExtensions { get; set; }
        public virtual string Size { get; set; }
    }
}