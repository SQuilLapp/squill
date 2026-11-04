# Column types: what each engine returns and what the grid shows

Written from `ColumnTypeReadTests` (one per driver test project, against `all_types` of the sample schema plus a few extra `select` expressions). "Text" is `CellFormatter.Format` (grid copy, export, editing).

| Engine | Native type | CLR type | Text sample |
|---|---|---|---|
| SQLite | integer / untyped integer | `Int64` | `42` |
| SQLite | real, numeric, untyped real | `Double` | `12.5` |
| SQLite | text, datetime (stored as text) | `String` | `2024-05-06 07:08:09` |
| SQLite | blob | `Byte[]` | `0xcafe` |
| PostgreSQL | smallint / integer / bigint | `Int16` / `Int32` / `Int64` | `7` |
| PostgreSQL | numeric(18,4) | `Decimal` | `1234.5000` |
| PostgreSQL | real / double precision | `Single` / `Double` | `1.5`, `NaN`, `Infinity` |
| PostgreSQL | boolean | `Boolean` | `true` |
| PostgreSQL | text, varchar, char(n) | `String` | `ab ` (char is blank padded) |
| PostgreSQL | bytea | `Byte[]` | `0xcafe` (cut after 32 bytes) |
| PostgreSQL | date / time | `DateOnly` / `TimeOnly` | `2024-05-06` / `07:08:09` |
| PostgreSQL | timestamp / timestamptz | `DateTime` (timestamptz in UTC) | `2024-05-06 05:08:09` |
| PostgreSQL | interval (no months) | `TimeSpan` | `-1.02:03:04.5` |
| PostgreSQL | interval with months | `String` | `<interval>` (not readable with the AOT-safe data source) |
| PostgreSQL | uuid | `Guid` | `a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11` |
| PostgreSQL | json, jsonb | `String` | `{"b": [1, 2]}` |
| PostgreSQL | inet, cidr, macaddr | `IPAddress`, `IPNetwork`, `PhysicalAddress` | `192.168.1.5` (inet drops the mask), `10.0.0.0/8`, `08002B010203` |
| PostgreSQL | integer[], text[] | `Int32[]`, `String[]` | `{1,2,3}`, `{a,b c}` |
| PostgreSQL | int4range | `NpgsqlRange<int>` | `[1,5)` |
| PostgreSQL | record (composite) | `Object[]` | `{1,x}` |
| PostgreSQL | enum | `String` | `<unsupported type>` (needs `EnableUnmappedTypes`, which is not AOT-safe) |
| MySQL | tinyint / smallint / int / bigint | `SByte` / `Int16` / `Int32` / `Int64` | `1`, `70000` |
| MySQL | decimal(18,4) | `Decimal` | `1234.5000` |
| MySQL | float / double | `Single` / `Double` | `1.5` |
| MySQL | bit(1) | `Boolean` | `true` |
| MySQL | bit(n), n > 1 | `Byte[]` | `0xaa` |
| MySQL | tinyint(1) | `Boolean` | `true` |
| MySQL | char, varchar, text, json, enum, set | `String` | `ok`, `a,c` |
| MySQL | blob | `Byte[]` | `0xcafe` |
| MySQL | date | `DateTime` | `2024-05-06` |
| MySQL | datetime(3), timestamp | `DateTime` | `2024-05-06 07:08:09.123` |
| MySQL | time | `TimeSpan` | `-12:03:04` |
| MySQL | year | `Int32` | `2024` |
| MySQL | zero date / datetime | `DateTime.MinValue` (`ConvertZeroDateTime=true`) | `0000-00-00` / `0000-00-00 00:00:00` |
| SQL Server | tinyint / smallint / int / bigint | `Byte` / `Int16` / `Int32` / `Int64` | `7` |
| SQL Server | decimal, money | `Decimal` | `1234.5000`, `12.3456` |
| SQL Server | real / float | `Single` / `Double` | `1.5` |
| SQL Server | bit | `Boolean` | `true` |
| SQL Server | char, nvarchar, varchar(max), xml | `String` | `héllo`, `<a x="1" />` |
| SQL Server | varbinary(max), rowversion | `Byte[]` | `0xcafe`, `0x0000000000000fa1` |
| SQL Server | date | `DateTime` | `2024-05-06` |
| SQL Server | datetime2, smalldatetime | `DateTime` | `2024-05-06 07:08:09.123` |
| SQL Server | datetimeoffset | `DateTimeOffset` | `2024-05-06 07:08:09.123+02:00` |
| SQL Server | time | `TimeSpan` | `07:08:09.123` |
| SQL Server | uniqueidentifier | `Guid` | `a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11` |
| SQL Server | sql_variant | the inner type (`Decimal`) | `1.5` |
| SQL Server | hierarchyid, geography, geometry | `Byte[]` (serialized form; `Microsoft.SqlServer.Types` is not AOT-safe) | `0x5b40` |

## Limits

- Binary values are read whole (a 1 MB `bytea` or `varbinary(max)` reads without freezing) and only the first 32 bytes are displayed (`CellFormatter.MaxHexBytes`); copy and export use the same text.
- A value whose formatting throws shows as `<unsupported: TypeName>`.
- The `DateTime.MinValue` to `0000-00-00` mapping applies to every engine; no other engine stores year 1 in a `date`/`datetime` column that SQuilL reads.
