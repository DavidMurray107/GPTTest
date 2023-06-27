using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public DiagnosticsController(IMemoryCache memoryCache, ILogger<DiagnosticsController> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
        }

        [HttpGet("GetChatHistory/{connectionId}")]
        public IActionResult GetChatHistory(string connectionId)
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
