namespace GPTTest.Models;

public class ChatHistory
{
    public int Id { get; set; }
    public string Message { get; set; } = null!;
    public string SentBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string ConversationId { get; set; } = null!;
}