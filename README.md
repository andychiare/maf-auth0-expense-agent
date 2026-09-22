# ExpenseAgent

ExpenseAgent is a sample Blazor Web App that implements an AI-powered expense-approval chat assistant. Authenticated managers can chat with an agent to review expense reports submitted by their direct reports.

This project is the companion sample for the Auth0 blog post series "Building Secure AI Agents with Microsoft Agent Framework and Auth0":

- Part 1: [User Authentication](https://auth0.com/blog/building-secure-ai-agents-with-microsoft-agent-framework-and-auth0-part-1-user-authentication/) — scaffolds a Blazor Web App with Auth0 authentication and wires it up to an AI agent built with the Microsoft Agent Framework and Azure OpenAI.
- Part 2: [RAG Filtering](https://auth0.com/blog/building-secure-ai-agents-microsoft-agent-framework-auth0-part-2-rag-filtering/) — replaces the hardcoded expense data with a real vector store, adds semantic search over expense reports, and uses Auth0 Fine-Grained Authorization (FGA) to pre-filter search results so the agent only ever sees expenses the logged-in manager is authorized to read.

Expense reports are now embedded and indexed in an in-memory vector store (via `CommunityToolkit.VectorData.InMemory`), and the `GetExpenseReports` tool performs a semantic search filtered to the IDs returned by an OpenFGA `ListObjects` authorization check for the current manager — permissions are applied *before* retrieval, not after, so unauthorized records never reach the LLM's context.

## Minimum Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An [Auth0](https://auth0.com/) account with a Regular Web Application registered (free tier is sufficient)
- An [Auth0 FGA](https://dashboard.fga.dev/) store with an authorization model defining an `expense` type and a `can_read` relation to `user`, plus permission tuples mapping managers to the expense IDs they're allowed to read
- An Azure subscription with an Azure OpenAI / Azure AI Foundry resource, with a chat model (e.g. `gpt-4.1-mini`) and an embedding model (e.g. `text-embedding-3-small`) deployed

## Setup

1. **Register an Auth0 application**
   - Create a Regular Web Application in your Auth0 tenant.
   - Set **Allowed Callback URLs** to `https://localhost:7099/callback`.
   - Set **Allowed Logout URLs** to `https://localhost:7099/`.
   - Note the application's **Domain** and **Client ID**.

2. **Deploy Azure OpenAI models**
   - In Azure AI Foundry / Azure OpenAI, deploy a chat model (e.g. `gpt-4.1-mini`) and an embedding model (e.g. `text-embedding-3-small`).
   - Note the resource **Endpoint**, **API Key**, and both **Deployment names**.

3. **Create an Auth0 FGA store**
   - Create a store at the [FGA dashboard](https://dashboard.fga.dev/) and define an authorization model with an `expense` type and a `can_read` relation to `user`.
   - Write tuples granting each manager `can_read` on the specific expense IDs their direct reports own.
   - Create an FGA API client and note the **Store ID**, **Client ID**, **Client Secret**, **API URL**, **API Token Issuer**, and **API Audience**.

4. **Configure the app**

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
       "ChatDeployment": "gpt-4.1-mini",
       "EmbeddingDeployment": "text-embedding-3-small"
     },
     "Fga": {
       "StoreId": "YOUR_FGA_STORE_ID",
       "ClientId": "YOUR_FGA_CLIENT_ID",
       "ClientSecret": "YOUR_FGA_CLIENT_SECRET",
       "ApiUrl": "YOUR_FGA_API_URL",
       "ApiTokenIssuer": "YOUR_API_TOKEN_ISSUER",
       "ApiAudience": "YOUR_API_AUDIENCE"
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
