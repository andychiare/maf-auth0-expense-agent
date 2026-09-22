using System.ComponentModel;
using System.Text.Json;

namespace ExpenseAgent.Agent;

public class ExpenseAgentTools
{
    private readonly string _managerId;

    public ExpenseAgentTools(string managerId)
    {
        _managerId = managerId;
    }

    [Description("Get the list of expense reports submitted by the manager's direct reports.")]
    public string GetExpenseReports(
        [Description("Optional keyword to filter expense reports by description or merchant")] string? filter = null)
    {
        // Hardcoded sample data
        var reports = new[]
        {
            new {
                Id = "exp-001",
                Employee = "alice@example.com",
                Amount = 450.00m,
                Merchant = "Marriott Hotels",
                Justification = "Client dinner during NYC conference",
                SubmittedAt = "2026-06-10"
            },
            new {
                Id = "exp-002",
                Employee = "bob@example.com",
                Amount = 0m,
                Merchant = "Delta Airlines",
                Justification = "",
                SubmittedAt = "2026-06-12"
            },
            new {
                Id = "exp-003",
                Employee = "carol@example.com",
                Amount = 120.50m,
                Merchant = "",
                Justification = "Team lunch",
                SubmittedAt = "2026-06-14"
            }
        };

        var filtered = filter is null
            ? reports
            : reports.Where(r =>
                r.Merchant.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                r.Justification.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();

        return JsonSerializer.Serialize(filtered);
    }
}