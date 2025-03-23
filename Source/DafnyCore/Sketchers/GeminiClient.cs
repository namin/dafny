using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Dafny
{
    class GeminiClient : IClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _url;
        private readonly string _model;

        public GeminiClient()
        {
            _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("Missing GEMINI_API_KEY. Set it in your environment variables.");
            }

            _url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key="+_apiKey;
            _model = "gemini-2.0-flash";

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public string ClientSummary => "Claude";

        public async Task<string> Complete(string prompt)
        {
            var requestBody = new
            {
                contents = new []
                {
                    new {
                        parts = new []
                        {
                            new {
                                text = prompt
                            }
                        }
                    }
                }
            };

            var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(_url, jsonContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return $"Error {response.StatusCode}: {responseContent}";
                }

                var responseData = JsonConvert.DeserializeObject<dynamic>(responseContent);
                string extractedText = responseData?.candidates[0]?.content?.parts[0]?.text?.ToString().Trim() ?? "No response received.";
                return extractedText;
            }
            catch (Exception ex)
            {
                return $"Error generating response: {ex.Message}";
            }
        }
    }
}