# P18-02 - Presentation: the home view-model

Status: todo
Depends on: P18-01

## Goal
Everything M01 shows and does, as a view-model both heads bind, plus the rule that decides when it is visible. Recipe 1, 3.

## Design decisions (final)
- `src/Squill.Presentation/Home/HomeViewModel.cs` (`public sealed partial class HomeViewModel : ViewModelBase`), owned by `WorkspaceViewModel` (`Home` property, `bool IsHomeVisible` = setting-driven at launch, then "no tab open and no active connection", recipe 3; recomputed when tabs or sessions change).
- `RecentCards`: up to 6 `HomeRecentCardViewModel` built from the recent store and the profile list (a recent id whose profile no longer exists is skipped and forgotten): name, dialect badge (two letters, as the explorer), environment pill text (`PROD`, `STAGING`, `DEV`, `LOCAL`: the short form of M01, new `UiText` keys `home.env.*`), subtitle `user@host:port/database` or the file path (no password, never), last opened as relative text (`il y a 12 min`, `hier, 18:42`, `lundi`, `21 oct.`: `RelativeTimeFormatter`, pure, fr + en, injected clock), `Shortcut` (`Ctrl+1` ... `Ctrl+6`), `OpenCommand` (connects through the existing connection flow of the workspace and touches the store).
- `QuickActions`: `Nouvelle connexion` (`Ctrl+N`, opens the connection editor), `Ouvrir un fichier` (`Ctrl+O`, only if the workspace already has an open-SQL-file or open-SQLite-file command; if neither exists, do not draw it and say so in the report), `Gérer les connexions` link.
- `SearchCommand` opens the palette (P17-14). `Ctrl+K` already does.
- Tips: `HomeTips` is a static list of 8 to 12 tips, each a `UiText` key (fr + en) describing a feature the app really has (run statement under cursor `Ctrl+Entrée`, palette, grid filter, manual transaction, Expliquer, ER diagram, history, snippets); `TipText`, `TipPosition` (`3 / 24` style, `n / count`), `NextTipCommand` (persists `HomeTipIndex`), `HideTipsCommand` (persists `HomeTipsHidden`, hides the card). Keyboard chips inside the tip text are plain markers `{kbd:Ctrl+Entrée}` that the heads render as chips (`TipSegments`: list of text / chip segments).
- `ShowAtStartup` two-way bound to the setting.
- `UiText` keys `home.*` (fr + en, parity green); the spec strings: `CONNEXIONS RÉCENTES`, `DÉMARRER`, `ASTUCE`, `Astuce suivante`, `Masquer`, `Afficher l'accueil au démarrage`, `Aucune connexion active`.
- The workspace touches the recent store whenever a session is opened (any path, not only the home cards).

## Out of scope
- Views (P18-03, P18-04), projects, import of other tools' connections, drivers count, ephemeral console.

## Tasks
- [ ] `HomeViewModel`, card view-model, `RelativeTimeFormatter`, tips, catalog keys, `WorkspaceViewModel` integration, `AppComposition` registration of the store.
- [ ] Tests in `tests/Squill.Presentation.Tests/Home/`: at most 6 cards newest first with shortcuts, a deleted profile is skipped and forgotten, subtitle never contains the password, relative text per range (minutes, yesterday with time, weekday, date) in fr and en, tip navigation wraps and persists, hiding persists, visibility rule (launch with setting on / off, tab opened hides, last tab closed with no connection shows), opening a card touches the store.

## Acceptance criteria
- Common acceptance (recipe 7), no head touched.

## Suggested commit message
Add the home view-model
