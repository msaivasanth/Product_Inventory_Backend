using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SampleProject.Models.Chats;

namespace SampleProject.Services
{
    public class ChatDbService
    {
        private readonly IConfiguration _config;
        private readonly IMongoDatabase _db;


        public ChatDbService(IConfiguration config)
        {
            _config = config;

            var connectionString = _config.GetConnectionString("DbConnection");
            var mongoUrl = MongoUrl.Create(connectionString);
            var mongoClient = new MongoClient(mongoUrl);

            _db = mongoClient.GetDatabase(mongoUrl.DatabaseName);
        }


        public IMongoCollection<Users> Users => _db.GetCollection<Users>("users");

        public IMongoCollection<Chat> Chat => _db.GetCollection<Chat>("chats");

        public IMongoCollection<Message> Messages => _db.GetCollection<Message>("messages");



        public async Task<List<Users>> GetUsers()
        {
            return await Users.Find(new BsonDocument()).ToListAsync();
        }

        public async Task<List<Users>> FilterUsers(string name)
        {
            return await Users.Find(x => x.Name.ToLower().Contains(name.ToLower())).ToListAsync();
        }

        public async Task<Users> GetSingleUser(string id)
        {
            var user = await Users.Find(x => x.Id == id).ToListAsync();
            return user[0];
        }

        public async Task<Users> GetUserByEmail(string email)
        {
            var user = await Users.Find(x => x.Email == email).ToListAsync();
            return user[0];
        }
        public async Task<Users> AddUser(Users user)
        {
            await Users.InsertOneAsync(user);
            return user;
        }


        public async Task<Chat> GetOrAddChat(string userId, string senderId)

        {

            var isUser = await Users.Find(x => x.Id == userId).ToListAsync();
            if (isUser == null) { return null; }
            var User = await Users.Find(x => x.Id == senderId).ToListAsync();
            var Name = User[0].Name;

            var isChat = await Chat.Find(x => x.Users.Contains(userId) && x.Users.Contains(senderId)).ToListAsync();

            if (isChat.Count == 0)
            {

                var newChat = new Chat
                {
                    Users = [userId, senderId],

                };

                await Chat.InsertOneAsync(newChat);
                return newChat;
            }
            else
            {
                return isChat[0];
            }

        }

        public async Task<Message> SendMessage([FromBody] Message message)
        {
            if (message == null) return null;

            await Messages.InsertOneAsync(message);

            return message;
        }

        public async Task<List<Message>> FetchAllMessages(string chatId)
        {
            var messages = await Messages.Find(x => x.chat.Equals(chatId)).ToListAsync();
            return messages;
        }

        public async Task<List<List<string>>> GetAllChats(string id)
        {
            var res = await Chat.Find(x => x.Users.Contains(id)).ToListAsync();
            List<List<string>> result = new List<List<string>>();

            for (int i = 0; i < res.Count; i++)
            {
                var chat = res[i];
                List<string> lis = new List<string>();

                var user1 = await GetSingleUser(chat.Users[0]);

                var user2 = await GetSingleUser(chat.Users[1]);

                if (chat.Users[0] != id)
                {
                    lis.Add(chat.Id);
                    lis.Add(user1.Name);
                    lis.Add(chat.Users[0]);
                }
                else
                {
                    lis.Add(chat.Id);
                    lis.Add(user2.Name);
                    lis.Add(chat.Users[1]);
                }

                result.Add(lis);
            }

            return result;
        }
    }
}
