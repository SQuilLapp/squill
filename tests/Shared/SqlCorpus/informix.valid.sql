select first 3 tabname from systables order by tabid
-- ---
select skip 1 first 2 tabname from systables order by tabid
-- ---
select first 1 * from customers
-- ---
select * from customers order by id limit 5
-- ---
select unique tabtype from systables
-- ---
select first 2 distinct tabtype from systables
-- ---
select today, current year to second from systables where tabid = 1
-- ---
select nvl(null, 'x'), decode(1, 1, 'one', 'other') from systables where tabid = 1
-- ---
select mdy(11, 27, 2026), extend(current, year to day) from systables where tabid = 1
-- ---
select tabname from systables where tabname matches 'sys*' and tabid < 10
-- ---
select { a brace comment } 1 from systables where tabid = 1
-- ---
select c.name, count(o.id) from customers c left join orders o on o.customer_id = c.id group by c.name
-- ---
select cast(1 as decimal(10, 2)), cast('a' as varchar(10)), cast(1 as int8) from systables where tabid = 1
-- ---
select case when 1 = 1 then 'y' else 'n' end from systables where tabid = 1
-- ---
select * from order_totals where total >= 0
-- ---
select 'a' || 'b', length('abc'), substr('abc', 2, 1) from systables where tabid = 1
-- ---
create temp table scratch (id serial, label lvarchar(100), amount money(10, 2), at datetime year to second, flag boolean)
-- ---
create procedure proc_one(a integer) returning integer;
    define b integer;
    let b = a + 1;
    if b > 0 then
        return b;
    end if;
    return 0;
end procedure;
-- ---
-- statements: 2
create procedure proc_two() returning integer; return 2; end procedure;
select 1 from systables where tabid = 1;
