using Microsoft.VisualBasic;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace SampleProject.Models.Chats
{
    public class Chat
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public List<string> Users { get; set; }


        public DateAndTime? timestamp { get; set; }
    }
}
