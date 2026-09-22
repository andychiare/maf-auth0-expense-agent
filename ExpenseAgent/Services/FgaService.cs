using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Configuration;

namespace ExpenseAgent.Services;

public class FgaService
{
    private readonly OpenFgaClient _client;

    public FgaService(IConfiguration config)
    {
        _client = new OpenFgaClient(new ClientConfiguration
        {
            ApiUrl = config["Fga:ApiUrl"]!,
            StoreId = config["Fga:StoreId"]!,
            Credentials = new Credentials
            {
                Method = CredentialsMethod.ClientCredentials,
                Config = new CredentialsConfig
                {
                    ClientId = config["Fga:ClientId"]!,
                    ClientSecret = config["Fga:ClientSecret"]!,
                    ApiAudience = config["Fga:ApiAudience"]!,
                    ApiTokenIssuer = config["Fga:ApiTokenIssuer"]!
                }
            }
        });
    }

    public async Task<IReadOnlyList<string>> GetAuthorizedExpensesAsync(string managerId)
    {
        var response = await _client.ListObjects(new ClientListObjectsRequest
        {
            User = $"user:{managerId}",
            Relation = "can_read",
            Type = "expense"
        });

        return response.Objects
            .Select(o => o.Replace("expense:", ""))
            .ToList();
    }
}