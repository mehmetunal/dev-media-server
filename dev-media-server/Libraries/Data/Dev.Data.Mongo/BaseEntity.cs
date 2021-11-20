using System;
using System.Runtime.Serialization;
using Dev.Core.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dev.Data.Mongo
{
    public abstract class BaseEntity : Data.BaseEntity, IBaseEntity, IEntity
    {
        protected BaseEntity()
        {
            _id = ObjectId.GenerateNewId();
            CreatedDate = DateTime.UtcNow;
        }

        [BsonId]
        [DataMember]
        private ObjectId _id;

        [DataMember]
        public ObjectId Id
        {
            get => _id;
            set => _id = value;
        }
        public DateTime CreatedDate { get; set; }
        [DataMember]
        public string CreatorIP { get; set; }
        [DataMember]
        public DateTime? ModifiedDate { get; set; }
        [DataMember]
        public string ModifierIP { get; set; }
    }
}