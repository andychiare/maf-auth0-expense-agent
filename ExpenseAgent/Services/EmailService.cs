using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ExpenseAgent.Services;

public class EmailService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public EmailService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string body,
        string gmailToken)
    {
        var rawMessage =
            $"To: {toEmail}\r\n" +
            $"Subject: {subject}\r\n" +
            $"Content-Type: text/plain; charset=utf-8\r\n\r\n" +
            $"{body}";

        // Gmail API requires base64url encoding
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawMessage))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", gmailToken);

        var response = await httpClient.PostAsync(
            "https://gmail.googleapis.com/gmail/v1/users/me/messages/send",
            new StringContent(
                JsonSerializer.Serialize(new { raw = encoded }),
                Encoding.UTF8,
                "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Gmail send failed ({response.StatusCode}): {error}");
        }
    }
}