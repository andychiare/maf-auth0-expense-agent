# ExpenseAgent

ExpenseAgent is a sample Blazor Web App that implements an AI-powered expense-approval chat assistant. Authenticated managers can chat with an agent to review expense reports submitted by their direct reports.

This project is the companion sample for the Auth0 blog post series "Building Secure AI Agents with Microsoft Agent Framework and Auth0":

- Part 1: [User Authentication](https://auth0.com/blog/building-secure-ai-agents-with-microsoft-agent-framework-and-auth0-part-1-user-authentication/) — scaffolds a Blazor Web App with Auth0 authentication and wires it up to an AI agent built with the Microsoft Agent Framework and Azure OpenAI.
- Part 2: [RAG Filtering](https://auth0.com/blog/building-secure-ai-agents-microsoft-agent-framework-auth0-part-2-rag-filtering/) — replaces the hardcoded expense data with a real vector store, adds semantic search over expense reports, and uses Auth0 Fine-Grained Authorization (FGA) to pre-filter search results so the agent only ever sees expenses the logged-in manager is authorized to read.
- Part 3: [Sending Email with Token Vault](https://auth0.com/blog/building-secure-ai-agents-microsoft-agent-framework-auth0-part-3-sending-email-with-token-vault/) — adds a `SendMissingDataEmail` tool that emails direct reports about incomplete expense reports via Gmail, using Auth0 Token Vault to obtain Google access tokens on demand so the app (and the LLM) never stores or sees the user's Gmail credentials.
- Part 4: [Human-in-the-Loop Approval](https://auth0.com/blog/building-secure-ai-agents-microsoft-agent-framework-auth0-part-4-human-in-the-loop-approval/) — adds Client-Initiated Backchannel Authentication (CIBA) so complete expense reports require an out-of-band approval or rejection from the manager's Auth0 Guardian-enrolled device before the decision is recorded by a protected Expense API.

Expense reports are now embedded and indexed in an in-memory vector store (via `CommunityToolkit.VectorData.InMemory`), and the `GetExpenseReports` tool performs a semantic search filtered to the IDs returned by an OpenFGA `ListObjects` authorization check for the current manager — permissions are applied *before* retrieval, not after, so unauthorized records never reach the LLM's context.

The agent can also send email on the manager's behalf. When a manager asks about expense reports missing required data, the `SendMissingDataEmail` tool uses Auth0 Token Vault to silently exchange the manager's Auth0 session for a fresh Google access token, then calls the Gmail API (`gmail.send` scope) to notify the report's owner. The access token is fetched just-in-time inside the tool implementation and discarded after use — it's injected via constructor rather than passed as a tool parameter, so it never appears in the tool schema the LLM sees and can never leak into the model's context. Managers who didn't originally log in with Google are prompted to link a Google account post-login via Auth0's Connected Accounts flow (the My Account API), after which Token Vault can mint Gmail tokens for them too.

Complete reports now follow a separate human-in-the-loop path. The `RequestManagerApproval` tool starts a CIBA request with the `approve:expenses` scope and a short binding message describing the expense. Auth0 sends a push notification through Guardian, and `ManagerApprovalService` polls until the manager approves, rejects, or the request expires. On approval, the CIBA-issued access token is sent to the protected `ExpenseApi`, which validates the scope and records the decision outside the chat conversation.

## Minimum Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An [Auth0](https://auth0.com/) account with a Regular Web Application registered (free tier is sufficient)
- An [Auth0 FGA](https://dashboard.fga.dev/) store with an authorization model defining an `expense` type and a `can_read` relation to `user`, plus permission tuples mapping managers to the expense IDs they're allowed to read
- An Azure subscription with an Azure OpenAI / Azure AI Foundry resource, with a chat model (e.g. `gpt-4.1-mini`) and an embedding model (e.g. `text-embedding-3-small`) deployed
- A Google Cloud project with an OAuth 2.0 client (Web application type) and the Gmail API enabled, requesting the `gmail.send` scope
- The `ExpenseApi` project in this solution, registered in Auth0 as an API with the `https://expenseapi.com` audience
- Auth0 CIBA support (available with an Enterprise plan or add-on; a new tenant can be used for limited testing), plus Auth0 Guardian push notifications and an enrolled manager device
- [Auth0 Token Vault](https://auth0.com/docs/secure/tokens/token-vault) enabled on your tenant's Google social connection, with **Offline Access** turned on so refresh tokens can be exchanged for fresh Google access tokens
- The [Auth0 My Account API](https://auth0.com/docs/api/myaccount) activated, for linking a Google account post-login when a manager didn't originally sign in with Google
- The `Auth0.AspNetCore.Authentication` NuGet package at v1.9.0 or later, plus the `Auth0.MyAccountApi` (beta) SDK

## Setup

1. **Register an Auth0 application**
   - Create a Regular Web Application in your Auth0 tenant.
   - Set **Allowed Callback URLs** to `https://localhost:7099/callback,https://localhost:7099/Account/ConnectGoogleCallback`.
   - Set **Allowed Logout URLs** to `https://localhost:7099/`.
   - Note the application's **Domain**, **Client ID**, and **Client Secret**.

2. **Deploy Azure OpenAI models**
   - In Azure AI Foundry / Azure OpenAI, deploy a chat model (e.g. `gpt-4.1-mini`) and an embedding model (e.g. `text-embedding-3-small`).
   - Note the resource **Endpoint**, **API Key**, and both **Deployment names**.

3. **Create an Auth0 FGA store**
   - Create a store at the [FGA dashboard](https://dashboard.fga.dev/) and define an authorization model with an `expense` type and a `can_read` relation to `user`.
   - Write tuples granting each manager `can_read` on the specific expense IDs their direct reports own.
   - Create an FGA API client and note the **Store ID**, **Client ID**, **Client Secret**, **API URL**, **API Token Issuer**, and **API Audience**.

4. **Create a Google OAuth client and enable the Gmail API**
   - In the [Google Cloud Console](https://console.cloud.google.com/), create (or reuse) a project, enable the **Gmail API**, and configure the OAuth consent screen.
   - Create an OAuth 2.0 **Client ID** of type **Web application**, and add Auth0's social-connection callback URL (`https://YOUR_AUTH0_DOMAIN/login/callback`) to its **Authorized redirect URIs**.
   - Note the **Client ID** and **Client Secret**.

5. **Enable Token Vault on the Google connection**
   - In your Auth0 tenant, open the **Google** social connection and enter the Client ID/Secret from the previous step.
   - Turn on **Offline Access** so Auth0 can request and store a refresh token for the user.
   - Under **Token Vault**, enable it for this connection and add the `https://www.googleapis.com/auth/gmail.send` scope so Token Vault is allowed to mint Gmail-sending access tokens.
   - On your Auth0 application, enable the **Token Vault** grant type.

6. **Activate the My Account API and configure MRRT**
   - Activate the [My Account API](https://auth0.com/docs/api/myaccount) for your tenant.
   - Create a Client Grant for your application authorizing the `create:me:connected_accounts` scope against the My Account API, so the app can drive the Connected Accounts flow for managers who didn't log in with Google.
   - Configure a **Multi-Resource Refresh Token (MRRT)** policy so the manager's session can be silently exchanged for a Google access token via Token Vault without an extra consent prompt.

7. **Configure the app**

   Fill in `ExpenseAgent/appsettings.json` (or, better, use [`dotnet user-secrets`](https://learn.microsoft.com/aspnet/core/security/app-secrets) so real credentials aren't committed):

   ```json
   {
     "Auth0": {
       "Domain": "YOUR_AUTH0_DOMAIN",
       "ClientId": "YOUR_CLIENT_ID",
       "ClientSecret": "YOUR_CLIENT_SECRET"
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
       },
       "ManagerApproval": {
          "Audience": "https://expenseapi.com",
          "ApiBaseUrl": "https://localhost:7015"
     }
   }
   ```

    `ManagerApproval:Audience` must match the Expense API identifier in Auth0. `ManagerApproval:ApiBaseUrl` must match the HTTPS URL used by the `ExpenseApi` launch profile.

8. **Configure the Expense API and CIBA**
    - Add the API project to the solution if needed: `dotnet sln add ExpenseApi/ExpenseApi.csproj`.
    - Register an Auth0 API named `ExpenseApi` with the identifier `https://expenseapi.com`.
    - Add the `approve:expenses` permission, enable **RBAC**, and enable **Add Permissions in the Access Token**.
    - In the Expense Agent application's **Advanced Settings > Grant Types**, enable **CIBA**. The Expense Agent must remain a confidential Regular Web Application because CIBA requires a client secret.
    - In the application's **API Access** tab, grant the `approve:expenses` permission for the ExpenseApi when the agent acts on the user's behalf.
    - Enable **Push Notifications** under **Security > Multi-factor Auth**, set the MFA policy to **Always**, and enroll the manager's device in Auth0 Guardian.

    The agent uses the `Auth0.AuthenticationApi` package for the CIBA `/bc-authorize` and `/token` calls. It is already referenced by this project; add it manually to a new checkout with `dotnet add ExpenseAgent/ExpenseAgent.csproj package Auth0.AuthenticationApi` if required.

## Running the App

Start the approval API in one terminal:

```bash
dotnet run --project ExpenseApi
```

Start the agent in another terminal:

```bash
cd ExpenseAgent
dotnet run
```

Then:

1. Browse to `https://localhost:7099`.
2. Log in via `/account/login` (redirects to Auth0's hosted login page).
3. If you didn't log in with Google, you'll see a banner prompting you to link a Google account — this drives the Connected Accounts flow through `/Account/ConnectGoogleCallback` so Token Vault can mint Gmail access tokens for you too.
4. Navigate to `/chat` and ask the assistant about expense reports, e.g. *"What expenses has Alice submitted?"*
5. Ask the assistant to review complete reports, e.g. *"Review all my pending expense reports and take action on each one."* — complete reports trigger `RequestManagerApproval`, while incomplete reports still trigger `SendMissingDataEmail`.
6. Approve or deny the push notification in Auth0 Guardian. The agent waits for the CIBA result and reports whether the expense was approved, rejected, or timed out.
7. After approving `exp-001`, verify that the separate API recorded the decision:

   ```bash
   curl -k https://localhost:7015/expense/exp-001/status
   ```

   The response contains the status, timestamp, and manager ID. A denied request returns a rejection message in chat and does not create a status record in the current sample.
8. Log out via `/account/logout`.
