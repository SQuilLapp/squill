-- expect: sql.unknownTable
select * from nowhere
-- ---
-- expect: sql.unknownColumn
select nope from customers
-- ---
-- expect: sql.unknownColumn
select c.nope from customers c
-- ---
-- expect: sql.expectedTableName
select * from
-- ---
-- expect: sql.expectedExpression
select 1 +
-- ---
-- expect: sql.expectedExpression
select * from customers where
-- ---
-- expect: sql.expectedChar
select (1 + 2 from customers
-- ---
-- expect: sql.expectedValuesOrQuery
insert into customers (name)
-- ---
-- expect: sql.expectedTableName
delete from
-- ---
-- expect: sql.unknownTable
select * from "nowhere"
-- ---
-- expect: sql.expectedSelectItem
select from customers
