using Dev.Data.Mongo.Attributes;

namespace Dev.Data.Mongo
{
    [BsonCollection("dev-media")]
    public partial class DevMedia : BaseEntity
    {
        public string? Path { get; set; }
        public string? Name { get; set; }
        public string? Extensions { get; set; }
        public long Length { get; set; }
        public string? Size { get; set; }
        public string? Duration { get; set; }
    }
}
