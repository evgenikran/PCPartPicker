using System.Text;
using Newtonsoft.Json;
using PcBuilder.Core.Models;

namespace PcBuilder.Api.Services
{
    public class AiReviewService
    {
        private readonly HttpClient _httpClient;

        public AiReviewService(string apiKey)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }

        public async Task<string> ReviewBuildAsync(BuildResult build, string workload, decimal budget)
        {
            var partsList = string.Join("\n", build.Parts.Select(p =>
                $"- {p.Type}: {p.Name} (${p.Price:F2})"));

            var prompt = $"""
                You are a friendly PC hardware expert explaining a build to someone who may not know much about PC components.

                This is a {workload} PC build with a ${budget:F0} budget:

                {partsList}

                Respond in exactly this format with no extra text:

                OVERVIEW: [2 enthusiastic sentences about what this build is great at and what experience the user can expect]

                CPU: [1 sentence — what this CPU does well and why it suits {workload}]
                GPU: [1 sentence — what this GPU delivers and what performance to expect]
                RAM: [1 sentence — why this amount of RAM is right for {workload}]
                MOTHERBOARD: [1 sentence — what platform this is and why it works well]
                STORAGE: [1 sentence — what kind of drive this is and what speed/capacity benefits it brings]
                PSU: [1 sentence — why this wattage and efficiency rating is appropriate]

                BEST FOR: [One line listing 3-4 specific tasks or games/software this build handles well]
                """;

            var requestBody = new
            {
                model = "claude-sonnet-4-5",
                max_tokens = 400,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return $"AI review error: {response.StatusCode} - {errorBody}";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            dynamic result = JsonConvert.DeserializeObject(responseJson)!;
            return result.content[0].text.ToString();
        }
    }
}