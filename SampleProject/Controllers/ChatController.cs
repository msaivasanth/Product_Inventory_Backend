using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SampleProject.Models.Chats;
using SampleProject.Services;

namespace SampleProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly ChatDbService _service;
        public ChatController(ChatDbService context)
        {
            _service = context;
        }

        [HttpGet("users")]
        public async Task<List<Users>> Users()
        {
            return await _service.GetUsers();
        }

        [HttpPost("addUsers")]
        public async Task<Users> AddUser([FromBody] Users user)
        {
            return await _service.AddUser(user);
        }


        // Get chat with a single user.
        [HttpGet("chat/{id}/{senderId}")]
        public async Task<Chat> CreateOrAccessChat(string id, string senderId)
        {
            return await _service.GetOrAddChat(id, senderId);
        }

        // Send message to a user
        [HttpPost("sendMessage")]
        public async Task<Message> SendMessage([FromBody] Message message)
        {
            return await _service.SendMessage(message);
        }

        // Fetch all messages of a chat.
        [HttpGet("fetchMessages/{id}")]
        public async Task<List<Message>> FetchMessages(string id)
        {
            return await _service.FetchAllMessages(id);
        }

        // Fetch all the chats belong to a single user.
        [HttpGet("getAllChats/{id}")]
        public async Task<List<List<string>>> ChatsOfUser(string id)
        {
            return await _service.GetAllChats(id);
        }


        [HttpGet("login/{id}")]
        public async Task<Users> GetUser(string id)
        {
            return await _service.GetSingleUser(id);
        }

        [HttpGet("users/{name}")]
        public async Task<List<Users>> FilterUsersByString(string name)
        {
            return await _service.FilterUsers(name);
        }

        [HttpGet("user/{email}")]
        public async Task<Users> GetUserViaEmail(string email)
        {
            return await _service.GetUserByEmail(email);
        }
    }
}
