# P21-99 - Oracle: wallet folder in the connection editors

Status: todo
Depends on: P21-08

## Goal
`ConnectionProfile.WalletPath` (the `TNS_ADMIN` folder with `cwallet.sso`) exists and is honoured by the bridge and the `sqlplus` terminal (P21-08), but neither connection editor can set it.

## Design decisions (final)
- A text field with a folder picker in both editors, shown only for Oracle, bound to `ConnectionEditorViewModel.WalletPath`; a one-line hint that `Require` and `VerifyFull` still need a certificate the wallet or the system store trusts.
- All texts through `UiText`, both catalogs.

## Out of scope
- Auto-login wallets for the password (the user still types it unless the wallet stores it).

## Tasks
- [ ] WinUI editor field, then the Avalonia twin; view-model tests already cover the round trip.
- [ ] Head checks of recipe 3 (AOT publish, drive on the published exe, dark and light captures).

## Acceptance criteria
- Recipe 2 and recipe 3.

## Suggested commit message
Add the Oracle wallet folder to the connection editors
