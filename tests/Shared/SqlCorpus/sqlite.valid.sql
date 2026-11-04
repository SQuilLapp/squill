select * from customers
-- ---
select c.id, c.name, o.total from customers c inner join orders o on o.customer_id = c.id
-- ---
select c.name, o.id from customers c left join orders o on o.customer_id = c.id where o.id is null
-- ---
select c.name, o.id from customers c left outer join orders o on o.customer_id = c.id
-- ---
select c.name, o.id from customers c right join orders o on o.customer_id = c.id
-- ---
select c.name, o.id from customers c full outer join orders o on o.customer_id = c.id
-- ---
select c.id, i.sku from customers c cross join order_items i
-- ---
select * from orders natural join order_items
-- ---
select o.id, i.sku from orders o join order_items i on i.order_id = o.id
-- ---
select name, (select count(*) from orders o where o.customer_id = c.id) as n from customers c
-- ---
select * from customers where id in (select customer_id from orders where total > 10)
-- ---
select * from customers c where exists (select 1 from orders o where o.customer_id = c.id)
-- ---
select * from (select customer_id, sum(total) as s from orders group by customer_id) t where t.s > 5
-- ---
with recent as (select * from orders where placed_on > date('now', '-30 day')) select count(*) from recent
-- ---
with recursive n(i) as (select 1 union all select i + 1 from n where i < 10) select sum(i) from n
-- ---
select id, total, row_number() over (partition by customer_id order by total desc) as rn from orders
-- ---
select id, sum(total) over (order by id rows between unbounded preceding and current row) from orders
-- ---
select id, lag(total) over w, lead(total) over w from orders window w as (order by id)
-- ---
select id from customers union select customer_id from orders
-- ---
select id from customers union all select customer_id from orders
-- ---
select id from customers intersect select customer_id from orders
-- ---
select id from customers except select customer_id from orders
-- ---
select customer_id, count(*), sum(total) from orders group by customer_id having sum(total) > 1 order by 2 desc limit 5 offset 1
-- ---
select * from customers order by id limit 1, 2
-- ---
select distinct customer_id from orders order by customer_id
-- ---
select "id", "name" as "n" from "customers"
-- ---
select `id`, [name] from customers
-- ---
select case when total > 100 then 'big' else 'small' end from orders
-- ---
select cast(total as integer), cast(placed_on as text), typeof(total) from orders
-- ---
select ifnull(email, 'none'), coalesce(email, 'x'), nullif(name, ''), name || '!' from customers
-- ---
select group_concat(name, ','), total(id), count(distinct name) from customers
-- ---
select name from customers where name like 'a%' or name glob 'b*' or name not like '%z'
-- ---
select id from orders where placed_on between '2020-01-01' and '2020-12-31' and total is not null and customer_id is not 3
-- ---
select strftime('%Y-%m', placed_on), date('now'), datetime('now', 'localtime'), julianday('now') from orders
-- ---
select 'it''s', x'41', 1e3, .5, 0x1f
-- ---
select json_extract('{"a": 1}', '$.a'), '{"a": 1}' -> '$.a', '{"a": 1}' ->> '$.a'
-- ---
select * from customers indexed by sqlite_autoindex_customers_1 where email = 'a'
-- ---
select * from customers not indexed
-- ---
select name from customers -- dash comment
where id > 0 /* block comment */
-- ---
insert into customers (name, email) values ('a', 'a@x.y'), ('b', 'b@x.y')
-- ---
insert into customers (name) select name from customers
-- ---
insert into customers (name, email) values ('z', 'z@x.y') on conflict (email) do update set name = excluded.name returning id
-- ---
insert or replace into customers (id, name) values (1, 'r')
-- ---
insert or ignore into customers (name, email) values ('z', 'z@x.y')
-- ---
replace into customers (id, name) values (1, 'r')
-- ---
update orders set total = total + 1 where customer_id in (select id from customers where name = 'a')
-- ---
update orders set total = o2.total from orders o2 where o2.id = orders.id and orders.id > 0
-- ---
update or replace customers set name = 'x' where id = 1 returning id
-- ---
delete from orders where customer_id in (select id from customers where name = 'a') returning id
-- ---
delete from order_items where order_id not in (select id from orders)
-- ---
create table corpus_t (id integer primary key autoincrement, name text not null default '', created datetime default current_timestamp, unique (name))
-- ---
create table corpus_wr (code text primary key, label text) without rowid
-- ---
create table corpus_s (a integer, b integer) strict
-- ---
create temp table corpus_tmp (a int, b text)
-- ---
create index corpus_idx on customers (lower(name)) where email is not null
-- ---
create view corpus_v as select id, name from customers
-- ---
create trigger corpus_trg after insert on customers begin update customers set name = name where id = new.id; end
-- ---
create virtual table corpus_ft using fts5(body)
-- ---
alter table customers add column corpus_c integer default 0
-- ---
alter table customers rename to corpus_renamed
-- ---
drop table no_pk
-- ---
pragma foreign_keys = on
-- ---
pragma table_info(customers)
-- ---
pragma journal_mode
-- ---
explain query plan select * from customers
-- ---
begin; select 1; commit
-- statements: 3
-- ---
select 1; select 2
-- statements: 2
