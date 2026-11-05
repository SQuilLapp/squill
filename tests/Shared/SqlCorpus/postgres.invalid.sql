-- expect: sql.unknownTable
select * from sample.nowhere
-- ---
-- expect: sql.unknownColumn
select nope from sample.customers
-- ---
-- expect: sql.unknownColumn
select c.nope from sample.customers c
-- ---
-- expect: sql.expectedTableName
select * from
-- ---
-- expect: sql.expectedExpression
select 1 +
-- ---
-- expect: sql.expectedExpression
select * from sample.customers where
-- ---
-- expect: sql.expectedChar
select (1 + 2 from sample.customers
-- ---
-- expect: sql.expectedValuesOrQuery
insert into sample.customers (name)
-- ---
-- expect: sql.expectedTableName
delete from
-- ---
-- expect: sql.expectedCteName
with as (select 1) select 1
-- ---
-- expect: sql.expectedSelectItem
select from sample.customers
