using Dev.Data.Mongo.Attributes;
using MongoDB.Bson;

namespace Dev.Data.Mongo.Media
{
    [BsonCollection("media_server_binary")]
    public class MediaServerBinary : BaseEntity, IPrimaryKey<ObjectId>
    {
        public virtual ObjectId MediaServerId { get; set; }
        public virtual BsonBinaryData BinaryData { get; set; }
    }
}