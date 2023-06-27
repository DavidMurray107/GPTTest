using GPTTest.Contracts;
using GPTTest.Helpers;
using Microsoft.Extensions.Caching.Memory;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.SharedModels;

namespace GPTTest.Handlers;

public class ChatGptHandler : IChatGptHandler
{
    private readonly IOpenAIService _openAiService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatGptHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private HttpRequest? _request;
    private readonly IMemoryCache _memoryCache;
    public ChatGptHandler(IOpenAIService openAiService, IHttpClientFactory httpClientFactory,
        ILogger<ChatGptHandler> logger, IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
    {
        _openAiService = openAiService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _memoryCache = memoryCache;
        _request = _httpContextAccessor.HttpContext?.Request;
    }

    private List<FunctionDefinition> GetAvailableFunctions()
    {
        FunctionDefinitionBuilder builder = new FunctionDefinitionBuilder("CheckAppointmentAvailability",
                "Checks whether the requested appointment is available")
            .AddParameter("aptDate", "string", "the requested UTC Appointment Time using the ISO 8601 Format ",
                required: true);

        FunctionDefinition def = builder.Build();
        List<FunctionDefinition> functions = new() { def };
        return functions;
    }

    private async Task<string> ExecutionRegisteredFunction(string functionName, Dictionary<string, object> arguments)
    {
        string functionResult = "";
        switch (functionName)
        {
            case "CheckAppointmentAvailability":

                functionResult = await CheckAppointmentAvailability(arguments["aptDate"].ToString());
                break;
            default:
                break;
        }

        return functionResult;
    }

    private async Task<string> CheckAppointmentAvailability(string aptDate)
    {
        _logger.LogInformation("Checking Appointment Availability for " + aptDate);
        if (!DateTime.TryParse(aptDate, out DateTime aDate))
            return "Date Invalid";
        using HttpClient client = _httpClientFactory.CreateClient();
        string APIEndpoint =
            $"{_request?.Scheme}://{_request?.Host}/API/Appointment/AppointmentAvailable?date={aDate:O}";
        try
        {
            // string result = await client.GetFromJsonAsync<string>( APIEndpoint,
            //     new JsonSerializerOptions(JsonSerializerDefaults.Web));
            string result = await client.GetStringAsync(APIEndpoint);
            _logger.LogInformation("Appointment Availability Response: " + result);
            return result ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: " + ex.ToString());
            return "Error: " + ex.ToString();
        }

        return "Unavailable";
    }

    private List<ChatMessage> GetChatHistory(string connectionId)
    {
        if(!_memoryCache.TryGetValue(connectionId, out List<ChatMessage> chatHistory))
        {
            chatHistory = new()
            {
                ChatMessage.FromAssistant($"Today Date is {DateTime.UtcNow:O}")
            };
        }
        if (chatHistory == null)
            chatHistory = new List<ChatMessage>();
        return chatHistory;
    }

    private void AddToChatHistory(List<ChatMessage> chatHistory, string message, string connectionId, ChatGptMessageRoles messageRoles, string? name = null)
    {
        _logger.LogInformation($"Add {message} to History as {messageRoles.ToString()}");
        switch (messageRoles)
        {
            case ChatGptMessageRoles.System:
                chatHistory.Add(ChatMessage.FromSystem(message, name));
                break;
            case ChatGptMessageRoles.User:
                chatHistory.Add(ChatMessage.FromUser(message, name));
                break;
            case ChatGptMessageRoles.Assistant:
                chatHistory.Add(ChatMessage.FromAssistant(message, name));
                break;
            case ChatGptMessageRoles.Function:
                chatHistory.Add(ChatMessage.FromFunction(message, name));
                break;
        }
        _memoryCache.Set(connectionId, chatHistory);
    }
    public async Task<string> SendChatMessage(string message, string connectionId,ChatGptMessageRoles messageRole, string? name = null)
    {
        _logger.LogInformation("Send Message" + message);
        string ChatResponse = "";

        List<ChatMessage> chatHistory = GetChatHistory(connectionId);

        foreach (var c in chatHistory)
        {
            _logger.LogInformation( $"{c.Role} Said {c.Content}");
        }

        AddToChatHistory(chatHistory, message, connectionId, messageRole, name);
        
        var functions = GetAvailableFunctions();
        var completionResult = await _openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = chatHistory,
            Model = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo_0613,
            MaxTokens = 50, //optional
            Functions = functions
        });
        if (completionResult.Successful)
        {
            _logger.LogInformation("Completion Result Successful!");
            _logger.LogInformation("Finish Reason "+ completionResult.Choices.First().FinishReason);
            _logger.LogInformation("Message Content "+ completionResult.Choices.First().Message.Content);
            if (completionResult.Choices.First().FinishReason == "function_call")
            {
                _logger.LogInformation($"ChatGPT Wants you to call a Function {completionResult.Choices.First().Message.FunctionCall?.Name} with the following Parameters {completionResult.Choices.First().Message.FunctionCall?.Arguments}");
                var functionExecution = await ExecutionRegisteredFunction(completionResult.Choices?.First()?.Message?.FunctionCall?.Name ?? "", completionResult?.Choices?.First()?.Message?.FunctionCall?.ParseArguments() ?? new());
                return await SendChatMessage(functionExecution, connectionId, ChatGptMessageRoles.Function, completionResult?.Choices?.First()?.Message?.FunctionCall?.Name ?? "");
            }
            else
            {
                //Only add a response if the system says something.
                ChatResponse = completionResult.Choices.First().Message.Content;
                AddToChatHistory(chatHistory, ChatResponse, connectionId, ChatGptMessageRoles.System, name);
            }
            
           

        }
        else
        {
            if (completionResult.Error == null)
            {
                _logger.LogError("Unknown Error");
                //throw new Exception();
            }
            
            _logger.LogError($"{completionResult?.Error?.Code}: {completionResult?.Error?.Message}");
            ChatResponse = "I'm sorry I am experiencing technical difficulties please try again in a moment.";
        }
        
        return ChatResponse;
    }
}
