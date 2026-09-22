using Microsoft.Extensions.VectorData;

namespace ExpenseAgent.Models;

public class ExpenseReport
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    public string EmployeeEmail { get; set; } = string.Empty;

    [VectorStoreData]
    public decimal Amount { get; set; }

    [VectorStoreData]
    public string Merchant { get; set; } = string.Empty;

    [VectorStoreData]
    public string Justification { get; set; } = string.Empty;

    [VectorStoreData]
    public string SubmittedAt { get; set; } = string.Empty;

    [VectorStoreVector(dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}