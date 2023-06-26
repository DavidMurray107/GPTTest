using GPTTest.Contracts;
using GPTTest.Helpers;
using Microsoft.AspNetCore.SignalR;
namespace GPTTest.Hubs;

public class ChatHub : Hub
{
    private readonly IChatGptHandler _chatGptHandler;

    public ChatHub(IChatGptHandler chatGptHandler)
    {
        _chatGptHandler = chatGptHandler;
    }

    public async Task SendMessage(string user, string message)
    {
        //Need to put Message history into ChatGPT Readable format. Add to session based on ConnectionId.
        
        var connectionId =  Context.ConnectionId;
        await Clients.Caller.SendAsync("ReceiveMessage", user, message);
        string chatGPTMessage =  await _chatGptHandler.SendChatMessage(message, connectionId, ChatGptMessageRoles.User);
        await Clients.Caller.SendAsync("ReceiveMessage", "Chat", chatGPTMessage);
    }
}