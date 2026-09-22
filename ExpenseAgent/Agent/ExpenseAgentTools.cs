using System.ComponentModel;
using System.Text.Json;
using ExpenseAgent.Services;

namespace ExpenseAgent.Agent;

public class ExpenseAgentTools
{
    private readonly string _managerId;
    private readonly ExpenseReportService _expenseService;
    private readonly FgaService _fgaService;

    public ExpenseAgentTools(string managerId, ExpenseReportService expenseService, FgaService fgaService)
    {
        _managerId = managerId;
        _expenseService = expenseService;
        _fgaService = fgaService;
    }

    [Description("Get the list of expense reports submitted by the manager's direct reports.")]
    public async Task<string> GetExpenseReports(
        [Description("Optional keyword to filter expense reports by description or merchant.")] string? query = null)
    {
        var authorizedIds = await _fgaService.GetAuthorizedExpensesAsync(_managerId);

        if (authorizedIds.Count == 0)
            return "No expense reports found for your direct reports.";

        var reports = await _expenseService.SearchAsync(
            query ?? "expense report", authorizedIds);
            
        return JsonSerializer.Serialize(reports);
    }
}