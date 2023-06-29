using GPTTest.Contracts;
using GPTTest.Helpers;
using GPTTest.Models;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using NuGet.Protocol;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using OpenAI.ObjectModels.SharedModels;

namespace GPTTest.Handlers;

public class ChatGptHandler : IChatGptHandler
{
    private readonly IOpenAIService _openAiService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatGptHandler> _logger;
    private readonly HttpRequest? _request;
    private readonly IMemoryCache _memoryCache;
    private readonly string BaseUrl;
    private readonly GptTestContext _context;
    private int RetryCount { get; set; } = 0;

    private int MaxRetries
    {
        get;
    } = 3;

    private int RateLimitRetries { get; set; } = 0;

    public ChatGptHandler(IOpenAIService openAiService, IHttpClientFactory httpClientFactory,
        ILogger<ChatGptHandler> logger, IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, GptTestContext context)
    {
        _openAiService = openAiService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _memoryCache = memoryCache;
        _context = context;
        _request = httpContextAccessor.HttpContext?.Request;
        BaseUrl = $"{_request?.Scheme}://{_request?.Host}";
    }

    private List<FunctionDefinition> GetAvailableFunctions()
    {
        FunctionDefinitionBuilder checkAvailability = new FunctionDefinitionBuilder("CheckAppointmentAvailability",
                "Checks whether the requested appointment is available")
            .AddParameter("aptDate", "string", "the requested Local to user Appointment Date and Time using the ISO 8601 Format",
                required: true);

        FunctionDefinition checkAvailabilityDefinition = checkAvailability.Build();

        FunctionDefinitionBuilder bookAppointment = new FunctionDefinitionBuilder("BookAppointment",
                $"Creates an appointment. The confirmation will be available at {BaseUrl}/BookingConfirmation/:id: where :id: is the ID of the appointment. Return this link as an html anchor")
            .AddParameter("firstName", "string", required: true)
            .AddParameter("lastName", "string", required: true)
            .AddParameter("aptDate", "string",
                description: "the requested Local to user Appointment Time using the ISO 8601 Format", required: true)
            .AddParameter("quantity", "number", description: "The number of people attending. Maximum 10",
                required: true);

        FunctionDefinition bookAppointmentDefinition = bookAppointment.Build();
        FunctionDefinitionBuilder editAppointment = new FunctionDefinitionBuilder("EditPreviouslyBookedAppointment",
                $"Edits an existing appointment. The confirmation will be available at {BaseUrl}/BookingConfirmation/:id: where :id: is the ID of the appointment. Return this link as an html anchor")
            .AddParameter("id", "number", required: true)
            .AddParameter("firstName", "string", required: true)
            .AddParameter("lastName", "string", required: true)
            .AddParameter("aptDate", "string",
                description: "the requested Local to user Appointment Time using the ISO 8601 Format", required: true)
            .AddParameter("quantity", "number", description: "The number of people attending. Maximum 10",
                required: true);

        FunctionDefinition editAppointmentDefinition = editAppointment.Build();

        List<FunctionDefinition> functions = new()
            { checkAvailabilityDefinition, bookAppointmentDefinition, editAppointmentDefinition };
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
            case "BookAppointment":
                functionResult = await BookAppointment(arguments["firstName"].ToString(),
                    arguments["lastName"].ToString(), arguments["aptDate"].ToString(),
                    Int32.Parse(arguments["quantity"].ToString()));
                break;
            case "EditPreviouslyBookedAppointment":
                functionResult = await EditPreviouslyBookedAppointment(Int32.Parse(arguments["id"].ToString()),
                    arguments["firstName"].ToString(),
                    arguments["lastName"].ToString(), arguments["aptDate"].ToString(),
                    Int32.Parse(arguments["quantity"].ToString()));
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
            $"{BaseUrl}/API/Appointment/AppointmentAvailable?date={aDate:O}";
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

    private async Task<string> BookAppointment(string firstName, string lastName, string aptDate, int quantity)
    {
        _logger.LogInformation(
            $"Booking Appointment for {firstName} {lastName} on {aptDate:G} for {quantity} attendees.");
        if (string.IsNullOrEmpty(firstName))
            return "First name Required";
        if (string.IsNullOrEmpty(lastName))
            return "Last name Required";
        if (quantity > 10)
            return "Too many people";
        if (!DateTime.TryParse(aptDate, out DateTime aDate))
            return "Date Invalid";
        Appointment appointment = new()
            { FirstName = firstName, LastName = lastName, Date = aDate, NumberOfPeople = quantity };

        using HttpClient client = _httpClientFactory.CreateClient();
        string APIEndpoint =
            $"{BaseUrl}/API/Appointment";
        try
        {
            var result = await client.PostAsJsonAsync(APIEndpoint, appointment);

            if (result.IsSuccessStatusCode)
            {
                return $"Success {result.Content.ReadAsStringAsync().Result}";
            }

            return $"Error:{result.Content.ReadAsStringAsync().Result}";
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: " + ex.ToString());
            return "Error: " + ex.ToString();
        }
    }

    private async Task<string> EditPreviouslyBookedAppointment(int id, string firstName, string lastName,
        string aptDate, int quantity)
    {
        _logger.LogInformation(
            $"Editing Previously Booking Appointment {id} for {firstName} {lastName} on {aptDate:G} for {quantity} attendees.");
        if (string.IsNullOrEmpty(firstName))
            return "First name Required";
        if (string.IsNullOrEmpty(lastName))
            return "Last name Required";
        if (quantity > 10)
            return "Too many people";
        if (!DateTime.TryParse(aptDate, out DateTime aDate))
            return "Date Invalid";
        Appointment appointment = new()
            { Id = id, FirstName = firstName, LastName = lastName, Date = aDate, NumberOfPeople = quantity };

        using HttpClient client = _httpClientFactory.CreateClient();
        string APIEndpoint =
            $"{BaseUrl}/API/Appointment/{id}";
        try
        {
            var result = await client.PutAsJsonAsync(APIEndpoint, appointment);

            if (result.IsSuccessStatusCode)
            {
                return $"Success {result.Content.ReadAsStringAsync().Result}";
            }

            return $"Error:{result.Content.ReadAsStringAsync().Result}";
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: " + ex.ToString());
            return "Error: " + ex.ToString();
        }
    }

    private List<ChatMessage> InitializeConversationPrompts()
    {
        return new()
        {
            ChatMessage.FromSystem(
                $"Answer questions as a receptionist that handles bookies at the office. Today's DateTime is {DateTime.UtcNow:O}. "
                + $"Don't make assumptions about what values to plug into functions. Ask for clarification. If you do not know the User's name or how many people are attending be sure to ask."
                + $"Before trying to book an appointment always display all parameters back to the user and ask for confirmation. "
                + $"You should always check for appointment availability before booking it. If the appointment is unavailable do not book the appointment. If you want to book an appointment you should always confirm the details to the user and send them an HTML Link. "
                + $"You cannot book any appointments in the past. " 
                + $"If clarifying dates use the ISO 8601 date format translated to the user's local time zone. "
                + $"The user's Timezone is {(TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now) ? TimeZoneInfo.Local.DaylightName : TimeZoneInfo.Local.StandardName)}. Always use the local time zone. "),
        };
    }

    private List<ChatMessage> GetChatHistory(string connectionId)
    {
        if (!_memoryCache.TryGetValue(connectionId, out List<ChatMessage> chatHistory))
        {
            chatHistory = InitializeConversationPrompts();
        }

        if (chatHistory == null)
            chatHistory = new List<ChatMessage>();
        return chatHistory;
    }

    private ChatMessage? AddToChatHistory(List<ChatMessage> chatHistory, string message, string connectionId,
        ChatGptMessageRoles messageRoles, string? name = null, FunctionCall? functionCall = null)
    {
        _logger.LogInformation($"Add {message} to History as {messageRoles.ToString()}");
        ChatMessage newMessage = null;
        switch (messageRoles)
        {
            case ChatGptMessageRoles.System:
                newMessage =ChatMessage.FromSystem(message);
                _context.ChatHistories.Add(new ChatHistory()
                    {
                        ConversationId = connectionId,
                        CreatedAt = DateTime.Now,
                        Message = message,
                        SentBy = "System"
                    }
                );
                break;
            case ChatGptMessageRoles.User:
                newMessage =ChatMessage.FromUser(message);
                _context.ChatHistories.Add(new ChatHistory()
                    {
                        ConversationId = connectionId,
                        CreatedAt = DateTime.Now,
                        Message = message,
                        SentBy = "User - " + name
                    }
                );
                break;
            case ChatGptMessageRoles.Assistant:
                var assistantChat = ChatMessage.FromAssistant(message);
                if (functionCall is not null)
                    assistantChat.FunctionCall = functionCall;
                newMessage = assistantChat;
                _context.ChatHistories.Add(new ChatHistory()
                    {
                        ConversationId = connectionId,
                        CreatedAt = DateTime.Now,
                        Message = message + (functionCall is not null ? $"Function: { functionCall.ToJson()}" : ""),
                        SentBy = "Assistant"
                    }
                );
                break;
            case ChatGptMessageRoles.Function:
                newMessage = ChatMessage.FromFunction(message, name);
                _context.ChatHistories.Add(new ChatHistory()
                    {
                        ConversationId = connectionId,
                        CreatedAt = DateTime.Now,
                        Message = message,
                        SentBy = "Function"
                    }
                );
                break;
            default:
                break;
        }
        chatHistory.Add(newMessage);
        _context.SaveChanges();
        _memoryCache.Set(connectionId, chatHistory);
        return newMessage;
    }

    public async Task<string> SendChatMessage(string message, string connectionId, ChatGptMessageRoles messageRole,
        string? name = null)
    {
        RateLimitRetries = 0;
        _logger.LogInformation("Send Message" + message);
        string ChatResponse = "";

        List<ChatMessage> chatHistory = GetChatHistory(connectionId);

        var currentMessage = AddToChatHistory(chatHistory, message, connectionId, messageRole, name);

        var functions = GetAvailableFunctions();
        ChatCompletionCreateResponse completionResult = null;
        bool processing = true;

        while (processing)
        {
            while (completionResult == null && RetryCount < MaxRetries)
            {
                try
                {
                    completionResult = await _openAiService.ChatCompletion.CreateCompletion(
                        new ChatCompletionCreateRequest
                        {
                            Messages = chatHistory,
                            Model = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo,
                            MaxTokens = 500, //optional
                            Functions = functions
                        });
                }
                catch (TimeoutException timeoutException)
                {
                    RetryCount++;
                    _logger.LogError("Connection Timeout: " + timeoutException.ToString());
                }
                catch (Exception exception)
                {
                    _logger.LogError("An issue occured when attempting to create the chat completion: " + exception.ToString());
                }
            }

            RetryCount = 0;

            if (completionResult is not null && completionResult.Successful)
            {
                _logger.LogInformation("Completion Result Successful!");
                _logger.LogInformation("Finish Reason " + completionResult.Choices.First().FinishReason);
                _logger.LogInformation("Message Content " + completionResult.Choices.First().Message.Content);

                /*
                 Available Finish Reasons.
                        stop: API returned complete message, or a message terminated by one of the stop sequences provided via the stop parameter
                        length: Incomplete model output due to max_tokens parameter or token limit
                        function_call: The model decided to call a function
                        content_filter: Omitted content due to a flag from our content filters
                        null: API response still in progress or incomplete
                */
                switch (completionResult.Choices.First().FinishReason)
                {
                    case "function_call":
                    {
                        _logger.LogInformation(
                            $"ChatGPT Wants you to call a Function {completionResult.Choices.First().Message.FunctionCall?.Name} with the following Parameters {completionResult.Choices.First().Message.FunctionCall?.Arguments}");

                        AddToChatHistory(chatHistory, ChatResponse, connectionId, ChatGptMessageRoles.Assistant, name,
                            completionResult.Choices.First().Message.FunctionCall);
                        var functionExecution = await ExecutionRegisteredFunction(
                            completionResult.Choices?.First()?.Message?.FunctionCall?.Name ?? "",
                            completionResult?.Choices?.First()?.Message?.FunctionCall?.ParseArguments() ?? new());
                        return await SendChatMessage(functionExecution, connectionId, ChatGptMessageRoles.Function,
                            completionResult?.Choices?.First()?.Message?.FunctionCall?.Name ?? "");
                    }
                    case "content_filter":
                    case "length":
                    case "stop":
                    default:
                        //Only add a response if the system says something.
                        if (!string.IsNullOrEmpty(completionResult.Choices.First().Message.Content))
                        {
                            ChatResponse = completionResult.Choices.First().Message.Content;
                            AddToChatHistory(chatHistory, ChatResponse, connectionId, ChatGptMessageRoles.Assistant,
                                name);
                        }

                        break;
                }

                processing = false;
            }
            else
            {
                if (completionResult?.Error == null)
                {
                    _logger.LogError("Unknown Error");
                    //throw new Exception();
                }

                //we experienced an error lets remove the last user response from the list and try again.
                if(currentMessage is not null)
                    chatHistory.Remove(currentMessage);
                
                _logger.LogError(
                    $"Code: {completionResult?.Error?.Code}{Environment.NewLine}Message:{completionResult?.Error?.Message}{Environment.NewLine}Param: {completionResult?.Error?.Param}{Environment.NewLine}Type: {completionResult?.Error?.Type}");

                //Rate limit hit, pause 20 seconds and try the request again.
                if (completionResult?.Error?.Type == "requests")
                {
                    //timeout for 30 sec then try again.
                    int fact = 1;
                    for (int x = 1; x <= RateLimitRetries; x++)
                    {
                        fact *= x;
                    }
                    int sleepTime = 20 * 1000 * fact; 
                    _logger.LogInformation("Rate Limit Pause starting. Will pause for " + sleepTime / 1000 + " seconds");
                    completionResult = null;
                    Thread.Sleep(sleepTime);
                    RateLimitRetries++;
                }
                ChatResponse = "I'm sorry I am experiencing technical difficulties please try again in a moment.";
            }
            
        }

        return ChatResponse;
    }
}