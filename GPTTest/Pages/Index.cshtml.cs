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
            "Checks whether the requested appointment is available").AddParameter("aptDate", nameof(DateTime),
            "the requested appointment Time", required: true);
        
        FunctionDefinition def = builder.Build();
        List<FunctionDefinition> functions = new List<FunctionDefinition>() { def };
        var completionResult = await _openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromUser(Message),
            },
            Model = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo,
            MaxTokens = 50, //optional
            Functions = functions
        });
        if (completionResult.Successful)
        {
            _logger.LogInformation(completionResult.Choices.First().Message.Content);
            APIResponse =completionResult.Choices.First().Message.Content;
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

    private async Task<string> CheckAppointmentAvailability(DateTime aptDate)
    {
        using HttpClient client = _httpClientFactory.CreateClient();
        string APIEndpoint = $"{this.Request.Scheme}://{this.Request.Host}/API/Appointment/AppointmentAvailable?date={aptDate:O}";
        try
        {
            string result = await client.GetFromJsonAsync<string>( APIEndpoint,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return result ?? "";
        }
        catch (Exception ex)
        {
            return "Error: " + ex.ToString();
        }

        return "Unavailable";
    }
}