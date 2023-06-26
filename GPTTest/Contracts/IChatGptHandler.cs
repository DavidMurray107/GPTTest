using GPTTest.Helpers;

namespace GPTTest.Contracts;

public interface IChatGptHandler
{
    public Task<string> SendChatMessage(string message, string connectionId, ChatGptMessageRoles messageRoles, string? name = null);
}