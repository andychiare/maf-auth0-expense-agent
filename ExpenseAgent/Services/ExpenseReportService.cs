using ExpenseAgent.Models;
using CommunityToolkit.VectorData.InMemory;
using Microsoft.Extensions.VectorData;
using System.ClientModel;
using Azure.AI.OpenAI;
using OpenAI.Embeddings;

namespace ExpenseAgent.Services;

public class ExpenseReportService
{
    private readonly EmbeddingClient _embeddingClient;
    private readonly VectorStoreCollection<string, ExpenseReport> _collection;

    public ExpenseReportService(IConfiguration config)
    {
        var endpoint = new Uri(config["AzureOpenAI:Endpoint"]!);
        var apiKey = new ApiKeyCredential(config["AzureOpenAI:ApiKey"]!);
        var embeddingDeployment = config["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-3-small";

        _embeddingClient = new AzureOpenAIClient(endpoint, apiKey)
            .GetEmbeddingClient(embeddingDeployment);

        var vectorStore = new InMemoryVectorStore();
        _collection = vectorStore.GetCollection<string, ExpenseReport>("expense-reports");
    }

    public async Task EnsureCollectionAsync()
        => await _collection.EnsureCollectionExistsAsync();

    public async Task IngestAsync(ExpenseReport report)
    {
        var text = BuildSearchText(report);
        var result = await _embeddingClient.GenerateEmbeddingAsync(text);
        report.Embedding = result.Value.ToFloats();
        await _collection.UpsertAsync(report);
    }

    public async Task<IReadOnlyList<ExpenseReport>> SearchAsync(
        string query,
        IReadOnlyList<string> authorizedIds,
        int maxResults = 5)
    {
        if (authorizedIds.Count == 0)
        {
            return Array.Empty<ExpenseReport>();
        }

        var queryEmbedding = await _embeddingClient.GenerateEmbeddingAsync(query);
        var queryVector = queryEmbedding.Value.ToFloats();

        var options = new VectorSearchOptions<ExpenseReport>
        {
            Filter = r => authorizedIds.Contains(r.Id)
        };

        var authorized = new List<ExpenseReport>();
        await foreach (var result in _collection.SearchAsync(queryVector, top: maxResults, options))
        {
            authorized.Add(result.Record);
        }

        return authorized;
    }

    private static string BuildSearchText(ExpenseReport r) =>
        $"Expense {r.Id}. Employee: {r.EmployeeEmail}. " +
        $"Amount: {r.Amount:C}. Merchant: {r.Merchant}. " +
        $"Justification: {r.Justification}. Submitted: {r.SubmittedAt}.";
}