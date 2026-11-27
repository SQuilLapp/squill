# P21-100 - Oracle: a killed bridge leaves the session looking connected

Status: todo
Depends on: P21-03

## Goal
Found by the P21-09 drive (WinUI published exe, Oracle Free 23ai). Cancelling `begin dbms_session.sleep(30); end;` works as documented in P21-03: the server does not service the break while the session sleeps, so the bridge is killed after the 5 s grace period. Afterwards the explorer still shows the green "connected" dot and the Manual toggle works, but every statement fails with "The Oracle bridge is no longer running." until the user disconnects and reconnects from the home card.

## Design decisions (final)
- When `BridgeClient` is broken (killed or exited), the session reconnects once on the next run (reopen the bridge with the stored profile and password) or, if that is refused, marks the connection as lost in the status bar and explorer so the user is told to reconnect. Pick the smaller of the two after reading how the other engines handle a dropped connection.
- A manual transaction open at that moment is lost: say so in the message.

## Out of scope
- Making `DBMS_SESSION.SLEEP` cancellable (a server limit).

## Tasks
- [ ] Failing test (fake bridge killed mid-request, then a run), then the fix; Docker test with a CPU-bound cancel followed by a run is already covered, add the sleep case.
- [ ] Both heads show the lost state (view-model test; drive on the published exe).

## Acceptance criteria
- Recipe 2 and recipe 3.

## Suggested commit message
Tell the user when the Oracle bridge was lost and reconnect
