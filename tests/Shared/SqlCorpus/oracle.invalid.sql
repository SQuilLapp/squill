-- expect: sql.unknownTable
select * from nowhere
-- ---
-- expect: sql.unknownColumn
select nope from customers
-- ---
-- expect: sql.expectedTableName
select * from
-- ---
-- expect: sql.expectedExpression
select 1 +
-- ---
-- expect: sql.expectedChar
select (1 + 2 from customers
