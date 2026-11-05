select * from customers
-- ---
select c.id, c.name, o.total from customers c inner join orders o on o.customer_id = c.id
-- ---
select c.name, o.id from customers c left join orders o on o.customer_id = c.id where o.id is null
-- ---
select c.name, o.id from customers c right join orders o on o.customer_id = c.id
-- ---
select c.id, i.sku from customers c cross join order_items i
-- ---
select o.id, i.sku from orders o join order_items i on i.order_id = o.id
-- ---
select * from orders natural join order_items
-- ---
select * from orders straight_join order_items on order_items.order_id = orders.id
-- ---
select name, (select count(*) from orders o where o.customer_id = c.id) as n from customers c
-- ---
select * from customers where id in (select customer_id from orders where total > 10)
-- ---
select * from customers c where exists (select 1 from orders o where o.customer_id = c.id)
-- ---
select * from (select customer_id, sum(total) as s from orders group by customer_id) t where t.s > 5
-- ---
with recent as (select * from orders where placed_on > current_date - interval 30 day) select count(*) from recent
-- ---
select distinct customer_id from orders order by customer_id
-- ---
select * from customers order by id limit 1, 2
-- ---
select customer_id, count(*), sum(total) from orders group by customer_id having sum(total) > 1 order by 2 desc limit 5 offset 1
-- ---
select id from customers except select customer_id from orders
-- ---
select id from customers intersect select customer_id from orders
-- ---
select id from customers union all select customer_id from orders
-- ---
select id from customers union select customer_id from orders
-- ---
select id, lag(total) over w, lead(total) over w from orders window w as (order by id)
-- ---
select id, sum(total) over (order by id rows between unbounded preceding and current row) from orders
-- ---
select id, total, row_number() over (partition by customer_id order by total desc) as rn from orders
-- ---
with recursive n(i) as (select 1 union all select i + 1 from n where i < 10) select sum(i) from n
-- ---
select `id`, `name` as `n` from `customers`
-- ---
select `c`.`id` from `customers` `c` where `c`.`name` = "a" and `c`.`email` = 'b'
-- ---
select case when total > 100 then 'big' else 'small' end from orders
-- ---
select cast(total as signed), cast(placed_on as char), convert(total, decimal(10, 2)) from orders
-- ---
select ifnull(email, 'none'), if(id > 1, 'a', 'b'), concat(name, '!'), group_concat(name separator ',') from customers group by email, id, name
-- ---
select j -> '$.a', j ->> '$.a', json_extract(j, '$.a') from (select cast('{"a": 1}' as json) as j) t
-- ---
select name from customers where name like 'a%' or name regexp '^c' or name rlike 'd$' or name not like '%z'
-- ---
select id from orders where placed_on between '2020-01-01' and '2020-12-31' and total is not null and customer_id <=> 3
-- ---
select date_add(placed_on, interval 1 day), date_format(placed_on, '%Y-%m'), now(), year(placed_on) from orders
-- ---
select 'it''s', 'a\nb', 0x41, b'101', x'41', 1e3, .5
-- ---
select * from customers where id = 1 for update
-- ---
select * from customers where id = 1 lock in share mode
-- ---
select sql_calc_found_rows * from customers limit 1
-- ---
select name from customers # hash comment
where id > 0 -- dash comment
/* block comment */
-- ---
insert into customers (name, email) values ('a', 'a@x.y'), ('b', 'b@x.y')
-- ---
insert into customers (name) select name from customers
-- ---
insert into customers (name, email) values ('z', 'z@x.y') on duplicate key update name = values(name)
-- ---
insert ignore into customers (name, email) values ('z', 'z@x.y')
-- ---
insert into customers set name = 'a', email = 'q@x.y'
-- ---
replace into customers (id, name) values (1, 'r')
-- ---
update orders o join customers c on c.id = o.customer_id set o.total = o.total + 1 where c.name = 'a'
-- ---
update customers set name = upper(name) where id in (select customer_id from orders) order by id limit 5
-- ---
delete o from orders o join customers c on c.id = o.customer_id where c.name = 'a'
-- ---
delete from order_items where order_id not in (select id from orders) limit 10
-- ---
create table corpus_t (id int auto_increment primary key, name varchar(50) not null default '', created timestamp default current_timestamp, unique key uq_name (name)) engine = innodb default charset = utf8mb4
-- ---
create temporary table corpus_tmp (a int, b text)
-- ---
create index corpus_idx on customers (name(10))
-- ---
create view corpus_v as select id, name from customers
-- ---
alter table customers add column corpus_c int default 0 not null
-- ---
alter table customers drop column email
-- ---
drop table if exists corpus_gone
-- ---
create function corpus_f(a int) returns int deterministic return a + 1
-- ---
truncate table no_pk
-- ---
explain select * from customers
-- ---
set @x = 1
-- ---
select @x := 2, @@version, @@session.sql_mode
-- ---
delimiter //
create procedure corpus_p(in a int)
begin
    select a;
    select a + 1;
end //
delimiter ;
-- statements: 1
-- ---
select 1; select 2
-- statements: 2
-- ---
start transaction; select 1; commit
-- statements: 3
