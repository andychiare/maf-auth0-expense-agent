using System.ComponentModel;
using System.Text.Json;
using ExpenseAgent.Services;
using System.Net;
using System.Net.Http.Json;

namespace ExpenseAgent.Agent;

public class ExpenseAgentTools
{
    private readonly string _managerId;
    private readonly ExpenseReportService _expenseService;
    private readonly FgaService _fgaService;
    private readonly HttpClient _httpClient;
    private readonly string? _cookie;
    private readonly ManagerApprovalService _approvalService;

    public ExpenseAgentTools(
        string managerId,
        ExpenseReportService expenseService,
        FgaService fgaService,
        HttpClient httpClient,
        string? cookie,
        ManagerApprovalService approvalService)
    {
        _managerId = managerId;
        _expenseService = expenseService;
        _fgaService = fgaService;
        _httpClient = httpClient;
        _cookie = cookie;
        _approvalService = approvalService;
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

    [Description("Send an email to an employee asking them to provide missing information for their expense report.")]
    public async Task<string> SendMissingDataEmail(
        [Description("The employee's email address")] string employeeEmail,
        [Description("The expense report ID (e.g., exp-002)")] string expenseId,
        [Description("Comma-separated list of missing fields (e.g., 'amount, justification')")] string missingFields)
    {
        var subject = $"Action Required: Incomplete Expense Report {expenseId}";
        var body = $"""
            Hi,

            Your expense report {expenseId} is missing the following required fields: {missingFields}.

            Please update your submission with the missing information so it can be reviewed and approved.

            Thank you.
            """;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/send-missing-data-email")
        {
            Content = JsonContent.Create(new { employeeEmail, subject, body })
        };

        if (_cookie is not null)
        {
            request.Headers.Add("Cookie", _cookie);
        }

        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return "I can't send this yet — the manager still needs to authorize Gmail access.";
        }

        if (!response.IsSuccessStatusCode)
        {
            return $"Sending the email to {employeeEmail} failed. Please try again.";
        }

        return $"Email sent to {employeeEmail} requesting: {missingFields}.";
    }

    [Description("Send a push notification to the manager requesting approval or rejection of a complete expense report. Use only when the report has all required fields: amount, merchant, and justification.")]
    public Task<string> RequestManagerApproval(
        [Description("The expense report ID")] string expenseId,
        [Description("A brief one-line summary: amount, merchant, and justification (e.g. '$450 at Hotel for NYC client dinner')")] string summary)
    {
        return _approvalService.RequestApprovalAsync(_managerId, expenseId, summary);
    }    
}