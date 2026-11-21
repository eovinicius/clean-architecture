# Agent Skills

Procedures that teach Claude Code this repository's conventions: vertical-slice use cases, custom command/query handlers (no MediatR), Result-based error handling, repositories + unit of work, controller actions, and full test coverage. Claude picks a skill automatically from its `description`; you can also invoke it explicitly.

## Template skills

| Skill | Invoke with | What it does |
| --- | --- | --- |
| **bootstrap** | `/bootstrap Acme.Billing` | Renames the solution to your app and strips the sample Todos feature (keeps Users + auth). |
| **ca-review** | `/ca-review` | Quick inline convention checklist. For the full pre-PR review, use `/review`. |

## Harness skills (pt-BR)

| Skill | Invoke with | What it does |
| --- | --- | --- |
| **implement-spec** | `/implement-spec SPEC-0001` | Implements an `Approved` spec AC by AC (red test → domain → slice → endpoint → migration), then verifies and closes the spec. |
| **dependency-upgrade** | `/dependency-upgrade EF Core` | Safe NuGet upgrades through Central Package Management, with real versions from the `nuget` MCP. |

The rest of the harness (rules, subagents, hooks, MCP) is documented in `docs/05-Engineering/Agent-Harness.md`.

Each skill is a plain Markdown file (`SKILL.md`, plus templates under `references/`). Changing a convention? Edit the skill once (and the matching rule in `.claude/rules/`), then run `bash scripts/agents/verify-harness.sh`.
