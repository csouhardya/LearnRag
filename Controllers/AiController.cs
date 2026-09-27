using LearnRag.Interfaces;
using LearnRag.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LearnRag.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AiController(IConfiguration config, 
        IIngestionService ingestionService, 
        IAskService askService,
        ILogger<AiController> logger) : ControllerBase
    {
        private readonly string uploadFolder = config["Constants:UploadFolderPath"]!;
        private readonly IIngestionService _ingestionService = ingestionService;
        private readonly IAskService _askService = askService;
        private readonly ILogger<AiController> _logger = logger;

        [HttpGet]
        [Route("ask")]
        public async Task<IActionResult> GetAsync(AskRequest request)
        {
            try
            {
                var resp = await _askService.AskAsync(request.userPrompt);
                return Ok(resp);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.InnerException?.ToString());
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("upload")]
        public async Task<IActionResult> UploadAsync(IFormFile file)
        {
            try
            {
                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }
                string filePath = Path.Combine(uploadFolder, file.FileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    await file.CopyToAsync(fileStream);
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.InnerException?.ToString());
                return BadRequest(ex.Message);
            }

            try
            {
                var resp =await _ingestionService.IngestDocumentAsync(file.FileName);
                return Ok(resp);
                //await _ingestionService.Dummy(file.FileName);
                //return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.InnerException?.ToString());
                return BadRequest(ex.Message);
            }
        }
    }
}
