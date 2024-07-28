using Microsoft.VisualBasic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SampleProject.Models.Chats
{
    public class Users
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("username"), BsonRepresentation(BsonType.String)]
        public string Name { get; set; }

        [BsonElement("email"), BsonRepresentation(BsonType.String)]
        public string Email { get; set; }

        public DateTime timestamp { get; set; } = DateAndTime.Now;
    }
}
