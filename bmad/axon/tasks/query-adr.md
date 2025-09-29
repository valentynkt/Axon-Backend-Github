# Query ADR Task

**Agent**: Axon Doc Oracle
**Purpose**: Query Architectural Decision Records (ADR) by topic or ID

---

```xml
<task id="bmad/axon/tasks/query-adr.md" name="Query ADR">
  <llm critical="true">
    <i>6 ADRs available: Modular Monolith, CQRS, Result Pattern, Strong IDs, PostgreSQL, FastEndpoints</i>
    <i>Support: list all, search by topic, get details, validate compliance</i>
  </llm>

  <flow>
    <step n="1" title="Parse Query">
      <action>Determine query type: list-all / search-topic / get-details / validate</action>
      <action>Extract search terms if applicable</action>
    </step>

    <step n="2" title="Search ADR Catalog">
      <action>ADR-001: Modular Monolith (architecture, modules)</action>
      <action>ADR-002: CQRS with MediatR (cqrs, commands, queries)</action>
      <action>ADR-003: Result Pattern (error-handling, result, exceptions)</action>
      <action>ADR-004: Strong IDs (strong-ids, type-safety)</action>
      <action>ADR-005: PostgreSQL (database, persistence)</action>
      <action>ADR-006: FastEndpoints (api, endpoints, rest)</action>
      <action>Match query to ADRs by topic keywords</action>
    </step>

    <step n="3" title="Retrieve ADR Content">
      <action>Read matching ADR files from Docs/ENGINEERING/guides/architecture/adrs/</action>
      <action>Extract: Context, Decision, Rationale, Consequences</action>
    </step>

    <step n="4" title="Output Query Results">
      <output format="markdown">
## ADR Query Results

**Query**: {query-text}
**Matches**: {count}

### ADR-{id}: {title}
- **File**: {file-path}
- **Decision**: {decision-summary}
- **Topics**: {topics}
- **Rationale**: {why}
- **Consequences**: {impacts}
      </output>
    </step>
  </flow>

  <adr-catalog>
    <i>ADR-001: Modular Monolith Architecture</i>
    <i>ADR-002: CQRS with MediatR</i>
    <i>ADR-003: Result Pattern for Error Handling</i>
    <i>ADR-004: Strong IDs</i>
    <i>ADR-005: PostgreSQL as Primary Database</i>
    <i>ADR-006: FastEndpoints for API Layer</i>
  </adr-catalog>

  <validation>
    <i>All 6 ADRs must be searchable</i>
    <i>Topic matching should be fuzzy (partial match)</i>
  </validation>

  <references>
    <i>ADR location: Docs/ENGINEERING/guides/architecture/adrs/</i>
    <i>ADR index: Docs/ENGINEERING/guides/architecture/adrs/00-INDEX.md</i>
  </references>
</task>
```
