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
            You are an expense approval assistant. You help managers review expense reports
            submitted by their direct reports. When asked, retrieve and summarize the reports.
            Be concise and professional. Address the manager by their first name when you know it.
            Format the expenses as an unordered list.
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