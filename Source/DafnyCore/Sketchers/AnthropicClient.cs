using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Dafny
{
    class AnthropicClient : IClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _model;

        public AnthropicClient()
        {
            _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("Missing ANTHROPIC_API_KEY. Set it in your environment variables.");
            }

            _baseUrl = "https://api.anthropic.com/v1/messages";
            _model = "claude-3-5-sonnet-20241022";

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public string ClientSummary => "Claude";

        public async Task<string> Complete(string prompt)
        {
            var requestBody = new
            {
                model = _model,
                max_tokens = 1024,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(_baseUrl, jsonContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return $"Error {response.StatusCode}: {responseContent}";
                }

                var responseData = JsonConvert.DeserializeObject<dynamic>(responseContent);
                string extractedText = responseData?.content[0]?.text?.ToString().Trim() ?? "No response received.";
                return extractedText;
            }
            catch (Exception ex)
            {
                return $"Error generating response: {ex.Message}";
            }
        }
    }
}