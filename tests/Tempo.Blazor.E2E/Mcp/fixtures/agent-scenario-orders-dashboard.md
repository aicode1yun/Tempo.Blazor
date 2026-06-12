# Agent scenario — "Správa objednávek" (Orders management dashboard)

This is the design brief an LLM agent is given. The agent may use **only** the wireframe MCP
tools (`wireframe_list_components`, `wireframe_get_component_schema`, `wireframe_create_document`,
`wireframe_apply_operations`, `wireframe_validate_document`, `wireframe_get_implementation_brief`)
and must resolve any errors purely from the tools' JSON responses.

## Task

Design a wireframe for an **Orders management** page with:

- a **top header** spanning the page width (title + user menu area);
- a **left sidebar** with navigation;
- a row of **4 KPI cards** near the top of the content area;
- an **orders table** with filters and pagination in the main content area;
- **Detail** and **Cancel (Storno)** action buttons;
- a **navigation flow** from the orders table to an order-detail target.

## Acceptance criteria (asserted by the Mcp5 replay test against the implementation brief)

1. The document validates (`wireframe_validate_document` → `valid: true`).
2. The page has a **header** region and a **sidebar** region (from geometry).
3. The **content** region contains the KPI cards, the table and the action buttons.
4. There are **at least 4 KPI card** elements.
5. There is **at least one navigation flow** (connector) whose source is the orders table.
6. `componentsUsed` reports a table component and a button component.
7. Total element count is **>= 10** (header + sidebar + 4 KPIs + table + 2 buttons + filters).
