# P18-16 - Core: role and privilege model

Status: done
Depends on: none

## Goal
The engine-neutral-looking but PostgreSQL-shaped model behind M11: roles, memberships, object privileges (direct and inherited) and the pending change set. Recipe 1.

## Design decisions (final)
- `src/Squill.Core/Security/`: `RoleInfo(string Name, bool CanLogin, bool IsSuperuser, bool Inherits, IReadOnlyList<string> MemberOf, IReadOnlyList<string> Members)` (a "group role" = a role with members or without `LOGIN`, as the list of M11 splits `Rôles de groupe` and `Utilisateurs`; the split rule is `RoleInfo.IsGroup`); `PrivilegeKind` `Select | Insert | Update | Delete | Truncate | References | Trigger` (the seven columns of M11); `ObjectPrivileges(string Schema, string Name, ObjectKind Kind /* Table, View, MaterializedView */, IReadOnlyDictionary<PrivilegeKind, PrivilegeState>)`; `PrivilegeState` `None | Granted | Inherited(string FromRole)` (inherited: granted through a role the subject is a member of, with the first source role for the tooltip `Hérité de lecture_seule . GRANT SELECT ON ALL TABLES`; the tooltip text is built by the Presentation card from `FromRole`).
- `PrivilegeChange(PrivilegeChangeKind Grant | Revoke, string Schema, string ObjectName, ObjectKind, PrivilegeKind, string Role)` and `PrivilegeChangeSet` (`Toggle(objectKey, kind, currentState)` rules: toggling a `None` adds a `Grant`, toggling a `Granted` adds a `Revoke`, toggling a pending change removes it, toggling an `Inherited` cell does nothing and reports `InheritedCannotRevoke` (the hint of M11 says it can only be revoked at the source role); `ToggleRow(objectKey)` for `Maj+Espace`; `Count`).
- Pure types, no I/O, XML doc, equality by value.

## Out of scope
- Reading from a server (P18-17), SQL text (P18-18), `WITH GRANT OPTION`, column privileges, default privileges, other engines.

## Tasks
- [x] Records, `RoleInfo.IsGroup`, `PrivilegeChangeSet`.
- [x] Tests in `tests/Squill.Core.Tests/Security/`: toggle rules (none, granted, pending grant, pending revoke, inherited), row toggle, group / user split, `Count`.

## Acceptance criteria
- Common acceptance (recipe 7), no head touched.

## Suggested commit message
Add the role and privilege model
