# ExpenseAgent

ExpenseAgent is a sample Blazor Web App that implements an AI-powered expense-approval chat assistant. Authenticated managers can chat with an agent to review expense reports submitted by their direct reports.

This project is the companion sample for the Auth0 blog post [Building Secure AI Agents with Microsoft Agent Framework and Auth0: User Authentication](https://auth0.com/blog/building-secure-ai-agents-with-microsoft-agent-framework-and-auth0-part-1-user-authentication/) (Part 1 of a series). It shows how to scaffold a Blazor Web App with Auth0 authentication and wire it up to an AI agent built with the Microsoft Agent Framework and Azure OpenAI.

The expense data used by the agent's tool is currently hardcoded sample data — there's no real database or per-manager authorization filtering yet. Those pieces are introduced in later parts of the blog series.

## Minimum Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An [Auth0](https://auth0.com/) account with a Regular Web Application registered (free tier is sufficient)
- An Azure subscription with an Azure OpenAI / Azure AI Foundry resource, with a chat model deployed (e.g. `gpt-4.1-mini`)

## Setup

1. **Register an Auth0 application**
   - Create a Regular Web Application in your Auth0 tenant.
   - Set **Allowed Callback URLs** to `https://localhost:7099/callback`.
   - Set **Allowed Logout URLs** to `https://localhost:7099/`.
   - Note the application's **Domain** and **Client ID**.

2. **Deploy an Azure OpenAI model**
   - In Azure AI Foundry / Azure OpenAI, deploy a chat model (e.g. `gpt-4.1-mini`).
   - Note the resource **Endpoint**, **API Key**, and **Deployment name**.

3. **Configure the app**

   Fill in `ExpenseAgent/appsettings.json` (or, better, use [`dotnet user-secrets`](https://learn.microsoft.com/aspnet/core/security/app-secrets) so real credentials aren't committed):

   ```json
   {
     "Auth0": {
       "Domain": "YOUR_AUTH0_DOMAIN",
       "ClientId": "YOUR_CLIENT_ID"
     },
     "AzureOpenAI": {
       "Endpoint": "https://YOUR-RESOURCE.openai.azure.com",
       "ApiKey": "YOUR_AZURE_OPENAI_API_KEY",
       "ChatDeployment": "gpt-4.1-mini"
     }
   }
   ```

## Running the App

```bash
cd ExpenseAgent
dotnet run
```

Then:

1. Browse to `https://localhost:7099`.
2. Log in via `/account/login` (redirects to Auth0's hosted login page).
3. Navigate to `/chat` and ask the assistant about expense reports, e.g. *"What expenses has Alice submitted?"*
4. Log out via `/account/logout`.
