using Microsoft.AspNetCore.Mvc;
using PcBuilder.Api.Services;
using PcBuilder.Core.Models;
using PcBuilder.Core.Profiles;
using PcBuilder.Core.Services;


namespace PcBuilder.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BuildController : ControllerBase
    {
        private readonly IBuildGenerator _generator;
        private readonly AiReviewService _aiReview;

        public BuildController(IBuildGenerator generator, IConfiguration config)
        {
            _generator = generator;
            _aiReview = new AiReviewService(config["AnthropicApiKey"]!);
        }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] BuildRequest request)
        {
            if (request.Budget <= 0)
                return BadRequest("Budget must be greater than 0.");

            WorkloadProfile profile = request.Workload switch
            {
                "Gaming" => WorkloadProfiles.Gaming,
                "Video Editing" => WorkloadProfiles.VideoEditing,
                "AI" => WorkloadProfiles.AI,
                _ => WorkloadProfiles.Gaming
            };

            if (request.Budget < profile.MinimumBudget)
                return BadRequest(new
                {
                    error = $"Minimum budget for {profile.Name} is ${profile.MinimumBudget:F0}."
                });

            var builds = _generator.Generate(request.Budget, profile);

            if (builds.Count == 0)
                return NotFound(new
                {
                    error = $"No build found for ${request.Budget:F0} ({profile.Name}). Try increasing your budget."
                });

            var build = builds[0];

            // Get AI review
            var review = await _aiReview.ReviewBuildAsync(build, profile.Name, request.Budget);

            var response = new
            {
                buildType = build.BuildType,
                totalPrice = build.TotalPrice,
                aiReview = review,
                parts = build.Parts.Select(p => new
                {
                    type = p.Type,
                    name = p.Name,
                    price = p.Price
                })
            };

            return Ok(response);
        }
    }

    public class BuildRequest
    {
        public decimal Budget { get; set; }
        public string Workload { get; set; } = "Gaming";
    }
}