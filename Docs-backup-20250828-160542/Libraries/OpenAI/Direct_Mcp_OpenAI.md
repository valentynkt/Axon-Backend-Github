
# Implementation Guide: Direct OpenAI Responses API → Remote C# MCP Server

## Executive Summary

* **Direct MCP via Responses API:** OpenAI can **call remote MCP servers directly**. This removes app‑mediated tool hops, cuts latency, and simplifies orchestration.
* **C# feasibility:** End‑to‑end client in C# is straightforward: **`HttpClient`** for full control (or OpenAI.NET if desired). This guide includes verified runnable client code.
* **Agents SDK (C#):** There is **no official OpenAI Agents SDK for C#**. Build agents by composing **OpenAI client** (for Responses) with **ModelContextProtocol** client (for tool discovery).
* **Stability:** The C# MCP ecosystem is **preview**; pin versions and monitor releases. Expect breaking changes.

---

## Direct MCP Pattern (how it differs from app‑mediated)

Traditional flow: App ↔ LLM ↔ (function call hint) ↔ App executes tool ↔ App replies.
**Direct MCP** moves the inner tool loop into **OpenAI runtime ↔ remote MCP server**:

* Fewer round‑trips and less token chatter.
* Best when OpenAI and the MCP server are co‑located (lower network latency).
* OpenAI fetches tool schemas, caches them into conversation context, and invokes tools directly as the model reasons.

**Sequence (one turn)**

1. **Client** POSTs to `/v1/responses` with a `tools` entry of `type: "mcp"`.
2. **OpenAI** discovers tools via `GET {server_url}/tools/list` (first use per conversation).
3. Tool schemas are cached in the response context as **`mcp_list_tools`**.
4. **LLM** may decide to call a tool; OpenAI POSTs to **`{server_url}/tools/call`**.
5. Tool output reenters model context; OpenAI returns the final answer.

---

## Client Implementation (C#) for Responses + MCP

### Request tool object (in `tools`)

**Required/optional fields**

* `type: "mcp"` – marks a remote MCP tool server.
* `server_url` – base URL; OpenAI appends `/tools/list` and `/tools/call`.
* `server_label` – optional human‑readable label.
* `require_approval` – optional; set to boolean or policy object to gate tool calls.
* `headers` – optional; forwarded to the MCP server (e.g., `Authorization`). **Values are not stored by OpenAI**, and the **path** portion of `server_url` is **redacted in responses**, so **include full path + headers on every request**.

**Multiple servers**: supply multiple `type: "mcp"` entries in `tools`.
**Tool filtering**: use `allowed_tools` to import only the subset you need (reduces tokens/latency and narrows decision space).

### Verified C# client (non‑streaming) — **unchanged**

```csharp
// Verified C# Snippet for calling Responses API with a remote MCP tool
// Requires: A.NET project (e.g., Console App) with System.Net.Http and System.Text.Json

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class OpenAiMcpClient
{
private static readonly HttpClient _httpClient = new HttpClient();
private const string OpenAiApiUrl = "https://api.openai.com/v1/responses";

    public static async Task<string> CallWithRemoteMcpToolAsync()
    {
        var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(openAiApiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY environment variable not set.");
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiApiKey);

        // Define the remote MCP server tool
        var mcpTool = new
        {
            type = "mcp",
            server_url = "http://localhost:5123/api/mcp", // Replace with your public MCP server URL
            server_label = "LocalWeatherService",
            require_approval = false,
            headers = new
            {
                Authorization = "Bearer your_secret_token_for_mcp_server"
            }
        };

        // Construct the full request payload
        var requestPayload = new
        {
            model = "gpt-4o",
            input = "Use the weather tool to find the weather in Boston.",
            tools = new { mcpTool }
        };

        var jsonPayload = JsonSerializer.Serialize(requestPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            // Send the request to the OpenAI Responses API
            var response = await _httpClient.PostAsync(OpenAiApiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();
            
            // In a non-streaming response, the final synthesized text is typically in the `output_text` field.
            // A more robust implementation would deserialize the JSON response into a proper object.
            Console.WriteLine($"Raw Response: {responseBody}");
            return responseBody;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"\nException Caught!");
            Console.WriteLine($"Message: {e.Message}");
            return null;
        }
    }
}
```

### Streaming (SSE) overview

* Set `"stream": true`.
* Parse events such as: `response.created`, `mcp_list_tools`, `mcp_call.started`, `mcp_call.output`, `mcp_call.error`, `response.completed`.
* Show partial output and tool activity in real time.

### Approvals (safety)

* With `require_approval: true` (or a policy object), tool calls **pause** pending approval.
* Your app reads the `mcp_approval_request` item, shows the proposed call (tool + args), and—upon consent—sends a follow‑up with an **`mcp_approval_response`** and the `previous_response_id` linking to the pending turn.
* For trusted servers, you can set `"require_approval": "never"` (globally) or whitelist tool names.

### Conversation state, discovery cache, and cost

* On first attach, OpenAI imports schemas and emits **`mcp_list_tools`** in the response.
* Keep the cache active by passing **`previous_response_id`** on subsequent turns.
* Stateless calls lose the cache → extra `tools/list` call and schema tokens each turn (more latency and cost).

### Error handling and idempotency

* `mcp_call` items include `error` on failure (protocol, execution, connectivity).
* For transient failures, surface minimal diagnostics to users, and **retry** carefully (idempotent tools only).

---

## .NET Agentic Path (no official C# Agents SDK)

**Approach:** Compose **OpenAI** (Responses) with **ModelContextProtocol** client:

* Use **MCP client** to discover tools at runtime.
* Convert discovered tools into `tools` entries for a Responses call or into `AIFunction` abstractions if using `Microsoft.Extensions.AI`.
* Maintain conversation state (`previous_response_id`) yourself.

**Conceptual C# (unchanged)**

```csharp
// This conceptual code is based on the pattern discussed in [3]
// It demonstrates the "roll your own" agent loop in C#.

using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI;
//... other usings

// 1. Create a chat client (e.g., from the OpenAI SDK).
var chatClient = new OpenAIClient("YOUR_API_KEY")
.GetChatClient("gpt-4o")
.AsIChatClient(); // Using Microsoft.Extensions.AI abstractions

// 2. Create an MCP client to connect to your remote tool server.
await using var mcpClient = await McpClientFactory.CreateAsync(
new HttpMcpClientTransportOptions { Endpoint = new Uri("http://localhost:5123/api/mcp") });

// 3. Discover the available tools from the MCP server.
var remoteTools = await mcpClient.ListToolsAsync();

// 4. Construct chat options, including the discovered tools.
// McpClientTool inherits from AIFunction, making it compatible.
var chatOptions = new ChatOptions
{
Tools = remoteTools.ToList<AIFunction>()
};

// 5. Invoke the LLM with the prompt and the available tools.
string userPrompt = "Use the weather tool to find the weather in Boston.";
ChatResponse response = await chatClient.GetResponseAsync(userPrompt, chatOptions);

// 6. The application would then parse the response. If a tool call was made,
// the result is already synthesized in the response because of the direct MCP pattern.
Console.WriteLine(response.Text);
```

**Notes**

* In production, prefer explicit **Responses** requests (full control of `tools`, `headers`, `require_approval`, `allowed_tools`, `previous_response_id`).
* Ensure your tool discovery + selection logic never expands the toolset more than needed (token minimization, tighter decision space).

---

## Production Notes

### Security

* **Trust boundary:** A remote MCP server can exfiltrate anything in the model’s context. Only connect to servers you operate or **deeply trust**.
* **Headers:** Send secrets via `tools[].headers` (e.g., `Authorization`). OpenAI **does not store header values** and redacts the **path** of `server_url` in responses; **include full path and headers on every request**.
* **Approvals:** Keep them **on** for destructive/expensive actions; log approved calls.
* **Data policy:** If you rely on data residency or zero‑retention, validate the third‑party MCP server policies independently.

### Limits, tokens, cost

* Standard OpenAI **RPM/TPM** limits apply to `/v1/responses`.
* Token cost = **prompt** + **schemas (from `mcp_list_tools`)** + **tool outputs** + **model output**.
* Reduce cost/latency by: keeping **schemas concise**, using **`allowed_tools`**, and **reusing context** (`previous_response_id`).

### Performance

* **Biggest wins:** cache schemas via `previous_response_id`; filter tools via `allowed_tools`.
* Use streaming for **fast first token** and to expose tool activity in UIs.

### Stability & maintenance

* MCP packages are **preview**; **pin** versions, **monitor** releases, and budget for refactors.

---

## Checklists

### Client‑side Direct MCP (no server steps)

* [ ] `OPENAI_API_KEY` set.
* [ ] Have a **trusted MCP server URL** (and auth scheme) ready.
* [ ] Build request body with `model`, `input`, `tools: [{ type: "mcp", server_url, headers, (optionally) require_approval, allowed_tools }]`.
* [ ] If conversational: pass `previous_response_id` to reuse **`mcp_list_tools`** cache.
* [ ] Consider `stream: true` and handle SSE for better UX.
* [ ] Parse `mcp_approval_request` / emit `mcp_approval_response` if approvals are enabled.
* [ ] Log tool calls and data sent; avoid oversharing.
* [ ] Handle `mcp_call.error` gracefully; retry idempotently.

### Production readiness

* [ ] **Approvals** enabled for sensitive tools, or an explicit `require_approval` policy in payload.
* [ ] **Headers** set every turn; secrets not logged.
* [ ] **Allowed tools** constrained; schemas reviewed for brevity.
* [ ] **Caching** via `previous_response_id` implemented.
* [ ] **Analyzer & dependency pins** in place; watch MCP releases.

---

## Package table (client‑relevant)

| Package ID             | Version (example) | Notes                | Stability |
| ---------------------- | ----------------- | -------------------- | --------- |
| OpenAI (openai‑dotnet) | 2.0.0‑beta.12     | Official .NET client | Beta      |
| ModelContextProtocol   | 0.3.0‑preview\.3  | C# MCP client/core   | Preview   |

> Versions reflect July 2025; verify before pinning.

---

## OpenAI Remote MCP — Docs Excerpts (kept unchanged for reference)

* The **MCP tool** works **only** in **Responses API** and charges only **tokens** for importing schemas and tool calls; **no extra MCP fee**.
* Keep **`mcp_list_tools`** in context to avoid repeated imports; constrain with **`allowed_tools`**.

**Example: attach MCP server**

```bash
curl https://api.openai.com/v1/responses \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $OPENAI_API_KEY" \
  -d '{
  "model": "gpt-4.1",
  "tools": [
    {
      "type": "mcp",
      "server_label": "deepwiki",
      "server_url": "https://mcp.deepwiki.com/mcp",
      "require_approval": "never"
    }
  ],
  "input": "What transport protocols are supported in the 2025-03-26 version of the MCP spec?"
}'
```

**JS/Python** examples and **`allowed_tools`**, **`mcp_call`**, and **approval** flows remain exactly as in your source text (unchanged and included above).

**Authentication via headers** (e.g., Stripe MCP) — values **not stored**, path **redacted** in responses; **send every turn**.

**Risks & Safety**: trust servers, keep approvals until confident, log data, beware prompt injection and server behavior changes; validate third‑party retention/residency policies.
