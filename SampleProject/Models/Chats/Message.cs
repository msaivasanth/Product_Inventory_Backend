using Microsoft.VisualBasic;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace SampleProject.Models.Chats
{
    public class Message
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string sender { get; set; }

        public string content { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string chat { get; set; }

        public DateTime timestamp { get; set; } = DateAndTime.Now;
    }
}
