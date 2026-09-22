using Microsoft.Extensions.AI;
using System.ClientModel;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Caching.Distributed;
using Azure.AI.OpenAI;

namespace ExpenseAgent.Agent;

public class ExpenseAgentService
{
    private readonly IConfiguration _config;
    private readonly IDistributedCache _cache;
    private AIAgent? _agent;

    public ExpenseAgentService(IConfiguration config, IDistributedCache cache)
    {
        _config = config;
        _cache = cache;
    }

    private AIAgent GetOrCreateAgent(ExpenseAgentTools tools)
    {
        if (_agent is not null) return _agent;

        var endpoint = new Uri(_config["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("Missing configuration: AzureOpenAI:Endpoint. See the README for details."));
        var apiKey = new ApiKeyCredential(_config["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("Missing configuration: AzureOpenAI:ApiKey. See the README for details."));
        var deployment = _config["AzureOpenAI:ChatDeployment"] ?? "gpt-4.1-mini";

        var systemPrompt = """
            You are an expense approval assistant. You help managers review and process expense reports from their direct reports.

            For each expense report you review, check whether it includes all three required fields:
            1. Amount (must be greater than zero)
            2. Merchant name (must not be empty)
            3. Justification (must not be empty)

            If any required field is missing:
            - Call SendMissingDataEmail with the employee's email, the expense ID, and
            a comma-separated list of the missing field names.
            - Tell the manager what you did and which fields were missing.

            If all required fields are present:
            - Call RequestManagerApproval with the expense ID and a brief one-line summary.
            - The manager will receive a push notification on their phone.
            - Wait for the result and tell the manager the outcome.

            Be concise and professional. Address the manager by name when you know it.
            Process one report at a time. Do not trigger multiple approval requests simultaneously.
            """;

        IChatClient chatClient = new AzureOpenAIClient(endpoint, apiKey)
            .GetChatClient(deployment)
            .AsIChatClient();

        _agent = chatClient.AsAIAgent(
            instructions: systemPrompt,
            name: "ExpenseAgent",
            tools: [AIFunctionFactory.Create(tools.GetExpenseReports)]);

        return _agent;
    }

    public async Task<string> ChatAsync(
        string userId,
        string userMessage,
        string? userName,
        ExpenseAgentTools tools)
    {
        var agent = GetOrCreateAgent(tools);

        var sessionKey = $"agent-session:{userId}";
        var serializedSession = await _cache.GetStringAsync(sessionKey);

        AgentSession session = serializedSession is not null
            ? await agent.DeserializeSessionAsync(JsonDocument.Parse(serializedSession).RootElement)
            : await agent.CreateSessionAsync();

        var response = await agent.RunAsync(userMessage, session);

        var serializedState = await agent.SerializeSessionAsync(session);
        await _cache.SetStringAsync(sessionKey, serializedState.GetRawText(),
            new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromHours(1)
            });

        return response.Text;
    }
}