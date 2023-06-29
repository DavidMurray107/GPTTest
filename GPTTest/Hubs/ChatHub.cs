using GPTTest.Contracts;
using GPTTest.Helpers;
using Microsoft.AspNetCore.SignalR;
namespace GPTTest.Hubs;

public class ChatHub : Hub
{
    private readonly IChatGptHandler _chatGptHandler;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IChatGptHandler chatGptHandler, ILogger<ChatHub> logger)
    {
        _chatGptHandler = chatGptHandler;
        _logger = logger;
    }
    
    public async Task SendMessage(string user, string message)
    {
        //Need to put Message history into ChatGPT Readable format. Add to session based on ConnectionId.
        try
        {
            var connectionId = Context.ConnectionId;
            await Clients.Caller.SendAsync("ReceiveMessage", user, message);
            string chatGPTMessage =
                await _chatGptHandler.SendChatMessage(message, connectionId, ChatGptMessageRoles.User, user);
            await Clients.Caller.SendAsync("ReceiveMessage", "Chat", chatGPTMessage);
        }
        catch(Exception ex)
        {
            _logger.LogError("Exception when trying to send message: " + ex.ToString());                     
        }
    }
}