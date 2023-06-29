using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GPTTest.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OpenAI.ObjectModels.RequestModels;

namespace GPTTest.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiagnosticsController : ControllerBase
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<DiagnosticsController> _logger;
        private readonly GptTestContext _context;
        public DiagnosticsController(IMemoryCache memoryCache, ILogger<DiagnosticsController> logger, GptTestContext context)
        {
            _memoryCache = memoryCache;
            _logger = logger;
            _context = context;
        }
        
        [HttpGet("GetChatHistory/{connectionId}"), 
         EndpointDescription("Gets the Chat history from the DB for an old conversation by the connection Id that was used for that conversation")]
        public IActionResult GetChatHistory(string connectionId)
        {
            var history = _context.ChatHistories.Where(ch => ch.ConversationId == connectionId).ToList();
            if (history is not null)
            {
                history.ForEach(h => _logger.LogDebug($" {h.SentBy} Said {h.Message}"));
            }

            return Ok(history);
        }

        [HttpGet("GetActiveChatHistory/{connectionId}"),
         EndpointDescription("Gets the Chat history from the memory cache for an active conversation by the connection Id that is in use for that conversation")]
        public IActionResult GetActiveChatHistory(string connectionId)
        {
            _memoryCache.TryGetValue(connectionId, out List<ChatMessage> history);
            if (history is not null)
            {
                history.ForEach(h => _logger.LogDebug($" {h.Role} Said {h.Content}"));
            }

            return Ok(history);
        }
    }
}
