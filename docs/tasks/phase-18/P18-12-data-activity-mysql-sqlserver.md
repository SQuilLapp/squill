# P18-12 - Data: activity readers for MySQL and SQL Server

Status: done
Depends on: P18-11

## Goal
`IActivityReader` on the two other server engines, with the same model. Recipe 1, 4.

## Design decisions (final)
- MySQL (`Squill.Data.MySql`): sessions from `performance_schema.threads` joined to `information_schema.processlist` when available, else `information_schema.processlist` alone (columns `ID`, `USER`, `HOST`, `DB`, `COMMAND`, `TIME`, `STATE`, `INFO`; `Application` null, `IsSystem` = background threads / `system user`); `StateText` from `COMMAND` + `STATE`; idle = `Sleep`; transactions open from `information_schema.innodb_trx` (`trx_started`, `trx_mysql_thread_id`) -> `IdleInTransaction` when sleeping with an open trx; blocking edges from `performance_schema.data_lock_waits` joined to `data_locks` (MySQL 8) with `sys.innodb_lock_waits` as the MariaDB / 5.7 fallback; **stop rule**: if neither is readable with the profile's rights, `Blocking` is empty and a flag `BlockingUnavailable` is set on the snapshot (add it to P18-11's model here) so the UI says "not available" instead of "none". `MaxConnections` from `@@max_connections`, `ActiveConnections` from `Threads_connected`, `TransactionCounter` = `Com_commit + Com_rollback`. Cancel = `KILL QUERY <id>`, terminate = `KILL <id>` (numeric id validated, concatenated after parsing since `KILL` takes no parameter).
- SQL Server (`Squill.Data.SqlServer`): `sys.dm_exec_sessions` + `sys.dm_exec_requests` + `sys.dm_exec_connections` + `sys.dm_exec_sql_text` (`session_id`, `login_name`, `program_name`, `client_net_address`, `status`, `total_elapsed_time`, `wait_type`/`wait_resource`, `blocking_session_id`, text), `IsSystem` = `is_user_process = 0`; edges from `blocking_session_id`; open transactions from `sys.dm_tran_session_transactions` + `sys.dm_tran_active_transactions`; `LocksHeld` from `sys.dm_tran_locks` counts; `ActiveConnections` = user sessions, `MaxConnections` = `@@MAX_CONNECTIONS`, `TransactionCounter` from `sys.dm_os_performance_counters` (`Transactions/sec` is cumulative) or null if not permitted. Needs `VIEW SERVER STATE`: without it the reader throws a typed `ActivityUnavailableException` (message code `activity.permission`) that the UI shows; no partial data. Cancel: SQL Server has no per-session cancel from another session, so `CancelQueryAsync` is **not supported** (capability flag `SupportsCancelQuery` on the reader, false here); terminate = `KILL <id>`.
- Add `bool SupportsCancelQuery` to `IActivityReader` (true for PostgreSQL and MySQL); P18-11's reader sets true.

## Out of scope
- Oracle, SQLite (no capability), UI.

## Tasks
- [x] Both readers, model flag, exception type, provider hooks.
- [x] Docker-gated tests per engine like P18-11 (open transaction, blocked update, edges, cancel / terminate, permission failure on SQL Server with a login lacking `VIEW SERVER STATE`, MySQL `BlockingUnavailable` when the lock tables are denied if reproducible, else covered by a unit test over the parse of an empty result).

## Acceptance criteria
- Common acceptance (recipe 7); Docker-gated tests ran on MySQL and SQL Server (say which ran). If the lock tables of a MySQL version behave differently from the card, stop and report with a proposed fix.

## Suggested commit message
Add the activity readers for MySQL and SQL Server
