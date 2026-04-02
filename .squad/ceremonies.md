# Ceremonies

> Team meetings that happen before or after work. Each squad configures their own.

## Design Review

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | before |
| **Condition** | multi-agent task involving 2+ agents modifying shared systems |
| **Facilitator** | lead |
| **Participants** | all-relevant |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. Review the task and requirements
2. Agree on interfaces and contracts between components
3. Identify risks and edge cases
4. Assign action items

---

## Retrospective

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | after |
| **Condition** | build failure, test failure, or reviewer rejection |
| **Facilitator** | lead |
| **Participants** | all-involved |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. What happened? (facts only)
2. Root cause analysis
3. What should change?
4. Action items for next iteration

---

## Optimization Pre-Review

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | before |
| **Condition** | coding task with a plan from Geralt that will modify C# or TypeScript/Vue code |
| **Facilitator** | lead |
| **Participants** | Arthur (for C#/.NET), Dutch (for TS/Vue), or both for cross-stack |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. Review the plan for optimization opportunities
2. Flag any patterns that should use modern language features
3. Recommend approach adjustments before implementation begins

---

## Optimization Post-Review

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | after |
| **Condition** | coding implementation completes (Marcus or Kratos finished work) |
| **Facilitator** | lead |
| **Participants** | Arthur (for C#/.NET changes), Dutch (for TS/Vue changes), or both |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. Review implemented code for modern language features and patterns
2. Identify simplification and performance opportunities
3. Suggest concrete improvements (with before/after examples)
