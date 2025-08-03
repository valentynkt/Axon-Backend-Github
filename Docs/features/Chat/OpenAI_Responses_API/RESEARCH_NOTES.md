---
id: AXON-20250802-chat-openai-responses-api-RESEARCH_NOTES
title: OpenAI Responses API: Research Notes
module: Chat
feature: OpenAI_Responses_API
gate: G1
owner: docs-grounder
status: draft
relates_to: []
source_of_truth: doc
created: 2025-08-02
updated: 2025-08-02
version: 1
---

# Questions
- What is the correct structure/schema for OpenAI Responses API responses?
- How should the "output" field be properly parsed?
- Is this a new API format or a parsing issue in our current implementation?
- Should we use built-in OpenAI library response types instead of custom ResponsesApiResponse?

# Findings (evidence-backed)
- **Schema Mismatch Confirmed**: Current `ResponsesApiResponse` model expects flat structure (`Id`, `OutputText`, `McpCalls`) but actual API returns nested "output" array structure — [local_analysis]
- **Output Array Structure**: Real API returns `{"output": [{"type": "mcp_list_tools", "content": {...}}, {"type": "message", "content": [{"text": "..."}]}]}` — [local_analysis, openai_memory]
- **MCP Integration Confirmed**: OpenAI officially added MCP support to Responses API in May 2025, production-ready with 100% reliability — [openai_memory]
- **Multiple Output Types**: API returns different object types in output array (mcp_list_tools, message) requiring type-based parsing — [local_analysis]
- **Current Parsing Failure**: `DeserializeFromSnakeCase<ResponsesApiResponse>` returns empty/null because schema doesn't match actual response structure — [local_code]

# Apply vs Not-Apply (Axon-specific)
- **Apply**: Update ResponsesApiResponse model to match actual API schema with output array structure
- **Apply**: Implement type-based content extraction for "message" objects to get actual response text
- **Apply**: Consider using official OpenAI SDK response types if available (requires SDK investigation)
- **Apply**: Update ToolExecutionExtractor to handle new MCP data structure from output array
- **Not-Apply**: Don't assume API schema without verification - current mismatch proves need for actual API testing

# Assumptions & Risks
- **Assumption**: Current implementation was built against documentation or earlier API version that may have changed
- **Risk**: Schema changes are breaking and require careful migration of existing response parsing logic
- **Risk**: MCP tool execution extraction logic needs updating to work with new output array structure
- **Assumption**: OpenAI may have different response formats for different API versions or feature flags

# Contradictions / Gaps
- **Gap**: No official OpenAI .NET SDK response types found in current codebase to compare against
- **Gap**: Missing actual response samples to validate exact field names and structure
- **Contradiction**: Current code expects flat structure but API clearly returns nested output array

# Citations
- [local_analysis]: Analysis of current ResponsesApiResponse.cs and actual parsing failure in OpenAiClient.cs — /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Infrastructure/Ai/Models/ResponsesApiResponse.cs (type: local)
- [local_code]: OpenAI client parsing logic at line 187 using DeserializeFromSnakeCase — /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Infrastructure/Ai/OpenAiClient.cs (type: local)  
- [openai_memory]: OpenAI MCP + Responses API Research from memory showing confirmed MCP support — openai-mcp-responses-api-research-2025 (type: local)

# Confidence
**High** — Clear schema mismatch between expected flat structure and actual nested output array structure explains parsing failures. Need immediate model updates to match real API format.