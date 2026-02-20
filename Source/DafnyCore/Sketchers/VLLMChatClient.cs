using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static Microsoft.Dafny.DafnyLogger;

namespace Microsoft.Dafny {
  public class VLLMChatClient {
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public VLLMChatClient() {
      _baseUrl = Environment.GetEnvironmentVariable("VLLM_BASE_URL");
      _model = Environment.GetEnvironmentVariable("VLLM_MODEL");

      if (string.IsNullOrEmpty(_baseUrl)) {
        throw new InvalidOperationException(
          "VLLM_BASE_URL environment variable is required (e.g. https://vllm.example.com)");
      }
      if (string.IsNullOrEmpty(_model)) {
        throw new InvalidOperationException(
          "VLLM_MODEL environment variable is required (e.g. qwen3-coder-flash)");
      }

      _httpClient = new HttpClient();
      _httpClient.Timeout = TimeSpan.FromSeconds(120);
    }

    public async Task<string> ChatComplete(string systemMessage, string userMessage) {
      var url = _baseUrl.TrimEnd('/') + "/v1/chat/completions";

      var messages = new List<object>();
      if (!string.IsNullOrEmpty(systemMessage)) {
        messages.Add(new { role = "system", content = systemMessage });
      }
      messages.Add(new { role = "user", content = userMessage });

      var requestBody = new {
        model = _model,
        messages = messages,
        max_tokens = 2048,
        temperature = 0.7
      };

      var json = JsonConvert.SerializeObject(requestBody);
      Log($"## VLLMChatClient ({_model} @ {_baseUrl})");
      Log("### System:\n" + systemMessage);
      Log("### User:\n" + userMessage);

      var content = new StringContent(json, Encoding.UTF8, "application/json");
      var response = await _httpClient.PostAsync(url, content);
      var responseText = await response.Content.ReadAsStringAsync();

      if (!response.IsSuccessStatusCode) {
        throw new Exception($"VLLM request failed ({response.StatusCode}): {responseText}");
      }

      var responseData = JsonConvert.DeserializeObject<dynamic>(responseText);
      string result = responseData?.choices[0]?.message?.content?.ToString()?.Trim()
                      ?? throw new Exception("No content in VLLM response");

      Log("### Response:\n" + result);
      return result;
    }
  }
}
