using System.Net.Http.Headers;
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models.Ciba;
using Auth0.Core.Exceptions;

namespace ExpenseAgent.Services;

public class ManagerApprovalService
{
    private readonly AuthenticationApiClient _authApiClient;
    private readonly HttpClient _expenseApiClient;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _issuer;
    private readonly string _audience;

    public ManagerApprovalService(IConfiguration config, HttpClient expenseApiClient)
    {
        _issuer = $"https://{config["Auth0:Domain"]}/";
        _authApiClient = new AuthenticationApiClient(new Uri(_issuer));
        _expenseApiClient = expenseApiClient;
        _clientId = config["Auth0:ClientId"]!;
        _clientSecret = config["Auth0:ClientSecret"]!;
        _audience = config["ManagerApproval:Audience"]!;
    }

    public async Task<string> RequestApprovalAsync(string managerId, string expenseId, string summary)
    {
        // Trigger the CIBA request and send a push notification to the manager's device,
        // scoped to the Expense API with the approve:expenses permission
        var cibaResponse = await _authApiClient.ClientInitiatedBackchannelAuthorization(
            new ClientInitiatedBackchannelAuthorizationRequest
            {
                ClientId = _clientId,
                ClientSecret = _clientSecret,
                Audience = _audience,
                Scope = "openid approve:expenses",
                BindingMessage = SanitizeMessage($"Expense {expenseId}: {summary}"),
                LoginHint = new LoginHint
                {
                    Format = "iss_sub",
                    Issuer = _issuer,
                    Subject = managerId
                }
            });

        // Poll until the manager acts or the request expires
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));

            try
            {
                var tokenResponse = await _authApiClient.GetTokenAsync(
                    new ClientInitiatedBackchannelAuthorizationTokenRequest
                    {
                        AuthRequestId = cibaResponse.AuthRequestId,
                        ClientId = _clientId,
                        ClientSecret = _clientSecret
                    });

                // A successful token response means the manager approved. Record it on the Expense API,
                // using the CIBA-issued access token (scoped to approve:expenses) as the bearer credential
                using var request = new HttpRequestMessage(HttpMethod.Post, $"/expense/{expenseId}/status")
                {
                    Content = JsonContent.Create(new
                    {
                        Status = "Approved",
                        Timestamp = DateTimeOffset.UtcNow,
                        ManagerId = managerId
                    })
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

                await _expenseApiClient.SendAsync(request);

                return $"Expense {expenseId} approved by the manager.";
            }
            catch (ErrorApiException ex)
            {
                switch (ex.ApiError.Error)
                {
                    case "authorization_pending":
                        continue; // manager hasn't tapped yet. Keep waiting

                    case "access_denied":
                        return $"Expense {expenseId} rejected by the manager.";

                    case "expired_token":
                        return $"Approval request for expense {expenseId} timed out. No response was received.";

                    default:
                        throw;
                }
            }
        }
    }

   private string SanitizeMessage(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Replace unadmitted characters with space
        var sanitized = System.Text.RegularExpressions.Regex.Replace(input, @"[^a-zA-Z0-9+\-_.,:# ]", " ");

        // Truncate to 64 characters
        return sanitized.Length > 64 ? sanitized[..64] : sanitized;
    }
}