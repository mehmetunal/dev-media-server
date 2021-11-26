﻿using Dev.Data.Mongo.Attributes;
using MongoDB.Bson;

namespace Dev.Data.Mongo.Media
{
    [BsonCollection("dev_media_server")]
    public class DevMediaServer : BaseEntity, IPrimaryKey<ObjectId>
    {
        public string RequestDomain { get; set; }
        public virtual string FileName { get; set; }
        public virtual string Path { get; set; }
        public virtual string FileExtensions { get; set; }
        public virtual long Size { get; set; }
    }
}