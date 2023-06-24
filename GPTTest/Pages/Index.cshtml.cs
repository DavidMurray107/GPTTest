using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenAI.Interfaces;
using OpenAI.ObjectModels.RequestModels;


namespace GPTTest.Pages;

public class Index : PageModel
{
    private readonly IOpenAIService _openAiService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Index> _logger;
    
    //TODO Sort out how to get the Date to be accurate when using 'tomorrow' as an option.
    //Current Date seems to be entirely unknown to the system.
    public Index(IOpenAIService openAiService, IHttpClientFactory httpClientFactory, ILogger<Index> logger)
    {
        _openAiService = openAiService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [Display(Name = "Message")]
    public string Message { get; set; } = "M";

    [Display(Name = "Response")]
    public string APIResponse { get; set; } = "R";

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPost([FromForm] string Message)
    {
        _logger.LogInformation("Post received " + Message);
        FunctionDefinitionBuilder builder = new FunctionDefinitionBuilder("CheckAppointmentAvailability",
            "Checks whether the requested appointment is available").AddParameter("aptDate", "string",
            "the requested UTC Appointment Time using the ISO 8601 Format ", required: true);
         
        FunctionDefinition def = builder.Build();
        List<FunctionDefinition> functions = new List<FunctionDefinition>() { def };
        var completionResult = await _openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromUser(Message),
            },
            Model = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo_0613,
            MaxTokens = 50, //optional
            Functions = functions
        });
        if (completionResult.Successful)
        {
            _logger.LogInformation("Completion Result Successful!");
            _logger.LogInformation("Finish Reason "+ completionResult.Choices.First().FinishReason);
            _logger.LogInformation("Message Content "+ completionResult.Choices.First().Message.Content);
            _logger.LogInformation("Message Function Call? Name "+ completionResult.Choices.First().Message.FunctionCall?.Name);
            _logger.LogInformation("Message Function Call? Arguments "+ completionResult.Choices.First().Message.FunctionCall?.Arguments);


            if (completionResult.Choices.First().FinishReason == "function_call") 
            {
                _logger.LogInformation($"ChatGPT Wants you to call a Function {completionResult.Choices.First().Message.FunctionCall?.Name} with the following Parameters {completionResult.Choices.First().Message.FunctionCall?.Arguments}");
                var functionExecution = await ExecutionRegisteredFunction(completionResult.Choices.First().Message.FunctionCall?.Name, completionResult.Choices.First().Message.FunctionCall?.ParseArguments());
                var completionResult2 = await _openAiService.ChatCompletion.CreateCompletion(
                    new ChatCompletionCreateRequest
                    {
                        Messages = new List<ChatMessage>
                        {
                            ChatMessage.FromUser(Message),
                            ChatMessage.FromFunction(functionExecution, completionResult.Choices.First().Message.FunctionCall?.Name)
                        },
                        Model = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo_0613,
                        MaxTokens = 50, //optional
                        Functions = functions
                    });
                if (completionResult2.Successful)
                {_logger.LogInformation("Completion Result 2 Successful!");
                    _logger.LogInformation("Finish Reason "+ completionResult2.Choices.First().FinishReason);
                    _logger.LogInformation("Message Content "+ completionResult2.Choices.First().Message.Content);
                    _logger.LogInformation("Message Function Call? Name "+ completionResult2.Choices.First().Message.FunctionCall?.Name);
                    _logger.LogInformation("Message Function Call? Arguments "+ completionResult2.Choices.First().Message.FunctionCall?.Arguments);
                    APIResponse = completionResult2.Choices.First().Message.Content;
                }
                else
                {
                    if (completionResult2.Error == null)
                    {
                        _logger.LogError("Unknown Error");
                        throw new Exception();
                    }

                    _logger.LogError($"{completionResult2.Error.Code}: {completionResult2.Error.Message}");
                }
            }
        }
        else
        {
            if (completionResult.Error == null)
            {
                _logger.LogError("Unknown Error");
                throw new Exception();
            }

            _logger.LogError($"{completionResult.Error.Code}: {completionResult.Error.Message}");
        }
        
        this.Message= Message;
        
        return Page();
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
        string APIEndpoint = $"{this.Request.Scheme}://{this.Request.Host}/API/Appointment/AppointmentAvailable?date={aDate:O}";
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
}