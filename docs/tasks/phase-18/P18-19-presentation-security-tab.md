# P18-19 - Presentation: the security tab (risky)

Status: todo
Depends on: P18-16, P18-17, P18-18, P18-02

## Goal
The M11 screen as a workspace tab for PostgreSQL connections: role list, privilege matrix, generated SQL, apply. Recipe 1, 2, 4.

## Design decisions (final)
- `src/Squill.Presentation/Security/SecurityViewModel.cs`, tab kind `Sécurité . <connection>`; command + palette action + menu item `Sécurité et rôles...` + connection context item (recipe 2), disabled with a reason when the session has no `ISecurityReader` (every engine except PostgreSQL) or is not connected.
- Left list: `Rôles de groupe n` and `Utilisateurs n` (`RoleInfo.IsGroup`), row = glyph + mono name + `n membres` or the badges `LOGIN` / `SUPERUSER`, filter box `Filtrer les rôles`. The `+` (new role) button of the mockup is **not drawn**. Selecting a role loads its privileges (worker, cancel on change, stale result dropped).
- Header: role name, badges (`NOLOGIN`, `INHERIT`, `SUPERUSER` as the role says), subtitle `Rôle de groupe . membre de x . 2 membres : a, b`. Tabs: only `Privilèges sur les objets` is drawn (recipe 1); `Général`, `Appartenances`, `Privilèges par défaut`, `Dupliquer`, `Supprimer le rôle...` are not.
- Matrix: rows = tables, views and materialized views of the selected schemas (selector `Schéma : public, analytics`, multi-select from `ListSchemasAsync`, default = the default schema), columns the 7 kinds, cells `PrivilegeState` + pending change; legend `Accordé`, `Hérité`, `À accorder`, `À révoquer`; check `Afficher les droits hérités` (off = inherited cells look empty and stay non-toggleable). Cell toggle through `PrivilegeChangeSet.Toggle`; `Espace` toggles, arrows navigate, `Maj+Espace` toggles the row (key handling is in the heads, the view-model exposes `ToggleCell(row, col)` and `ToggleRow(row)`). Inherited cell tooltip `Hérité de <role> . <GRANT text>`; the hint text under the grid, as in M11.
- Right panel `SQL GÉNÉRÉ` with `n changements`: `PostgresPrivilegeScript.Build(...)`, `Copier`, `Annuler` (drops the pending changes), `Appliquer` (`Ctrl+S`). The `Effet sur les membres` block and `Appliquer aussi aux objets futurs` are **not drawn**.
- Apply: statements run one by one through the statement runner inside the session transaction rules (savepoint inside a user transaction, short transaction otherwise, as the grid commit does since P15-09), with the **Production confirmation** (typed word because `REVOKE` is destructive for access) on Production; on success reload the privileges and clear the changes; a failure rolls back and keeps the pending changes with the engine message. Changing the role or schema with pending changes asks to apply or discard (reuse the pending-edits prompt wording of the grid, new `UiText` key).
- Status bar: `n changements non appliqués`, `<user>@<connection> / <database>`, version. Keys `UiText` `security.*` (fr + en).
- Everything read-only until `Appliquer`.

## Out of scope
- Heads (P18-20, P18-21), role creation or deletion, other engines.

## Tasks
- [ ] View-model, row and cell view-models, workspace command, palette action, catalog keys.
- [ ] Tests in `tests/Squill.Presentation.Tests/Security/` with a fake reader and runner: list split and filter, load per role with stale result dropped, toggles and pending counts, inherited cell refuses, script text follows changes, apply success reloads, apply failure keeps changes, Production confirmation required and nothing runs when refused, unsupported engine disables the command, switching role with pending changes asks.
- [ ] A PostgreSQL Docker-gated scenario in `tests/Shared` or the Postgres test project: through the view-model, revoke a grant and grant another, apply, reload: state matches.

## Acceptance criteria
- Common acceptance (recipe 7), no head touched. Riskiest: apply must never bypass the production confirmation; a test proves it.

## Suggested commit message
Add the security view-model and tab
