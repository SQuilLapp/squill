select * from sample.customers
-- ---
select c.id, c.name, o.total from sample.customers c inner join sample.orders o on o.customer_id = c.id
-- ---
select c.name, o.id from sample.customers c left join sample.orders o on o.customer_id = c.id where o.id is null
-- ---
select c.name, o.id from sample.customers c right join sample.orders o on o.customer_id = c.id
-- ---
select c.name, o.id from sample.customers c full outer join sample.orders o on o.customer_id = c.id
-- ---
select c.id, i.sku from sample.customers c cross join sample.order_items i
-- ---
select * from sample.orders natural join sample.order_items
-- ---
select o.id, i.sku from sample.orders o join sample.order_items i on i.order_id = o.id
-- ---
select name, (select count(*) from sample.orders o where o.customer_id = c.id) as n from sample.customers c
-- ---
select * from sample.customers where id in (select customer_id from sample.orders where total > 10)
-- ---
select * from sample.customers c where exists (select 1 from sample.orders o where o.customer_id = c.id)
-- ---
select * from (select customer_id, sum(total) as s from sample.orders group by customer_id) t where t.s > 5
-- ---
with recent as (select * from sample.orders where placed_on > current_date - 30) select count(*) from recent
-- ---
with recursive n(i) as (select 1 union all select i + 1 from n where i < 10) select sum(i) from n
-- ---
with a as (select id from sample.customers), b as (select customer_id from sample.orders) select * from a join b on b.customer_id = a.id
-- ---
select id, total, row_number() over (partition by customer_id order by total desc) as rn from sample.orders
-- ---
select id, sum(total) over (order by id rows between unbounded preceding and current row) from sample.orders
-- ---
select id, lag(total) over w, lead(total) over w from sample.orders window w as (order by id)
-- ---
select id from sample.customers union select customer_id from sample.orders
-- ---
select id from sample.customers union all select customer_id from sample.orders
-- ---
select id from sample.customers intersect select customer_id from sample.orders
-- ---
select id from sample.customers except select customer_id from sample.orders
-- ---
select customer_id, count(*), sum(total) from sample.orders group by customer_id having sum(total) > 1 order by 2 desc limit 5 offset 1
-- ---
select distinct customer_id from sample.orders order by customer_id
-- ---
select distinct on (customer_id) customer_id, placed_on from sample.orders order by customer_id, placed_on desc
-- ---
select case when total > 100 then 'big' when total > 10 then 'mid' else 'small' end from sample.orders
-- ---
select id::text, total::int, cast(placed_on as text) from sample.orders
-- ---
select 1::bigint + 2::smallint, '2020-01-01'::date, now()::timestamp
-- ---
select generate_series(1, 10)
-- ---
select * from generate_series(1, 3) as g(i)
-- ---
select array[1, 2, 3], (array[1, 2, 3])[2], array_agg(id) from sample.customers
-- ---
select '{"a": 1}'::jsonb ->> 'a', '{"a": {"b": 2}}'::jsonb -> 'a' -> 'b', '{"a": 1}'::jsonb @> '{"a": 1}'::jsonb
-- ---
select count(*) filter (where total > 10), sum(total) filter (where total < 5) from sample.orders
-- ---
select c.id, x.s from sample.customers c, lateral (select sum(total) as s from sample.orders o where o.customer_id = c.id) x
-- ---
select c.id, x.s from sample.customers c left join lateral (select sum(total) as s from sample.orders o where o.customer_id = c.id) x on true
-- ---
select name from sample.customers where name like 'a%' or name ilike 'B%' or name ~ '^c' or name similar to '%d%'
-- ---
select id from sample.orders where placed_on between '2020-01-01' and '2020-12-31' and total is not null and customer_id is distinct from 3
-- ---
select coalesce(email, 'none'), nullif(name, ''), greatest(1, 2), least(1, 2) from sample.customers
-- ---
select id from sample.orders where total = any (array[1, 2, 3]) and customer_id <> all (array[4, 5])
-- ---
select date_trunc('month', placed_on), extract(year from placed_on), placed_on + interval '1 day' from sample.orders
-- ---
select "id", "name" as "n" from "sample"."customers"
-- ---
select 'it''s', e'a\nb', $$dollar quoted$$, $tag$tag quoted$tag$
-- ---
select * from sample.customers order by name collate "C" nulls last
-- ---
select * from sample.customers fetch first 3 rows only
-- ---
select * from sample.customers for update
-- ---
insert into sample.customers (name, email) values ('a', 'a@x.y'), ('b', 'b@x.y')
-- ---
insert into sample.customers (name) select name from sample.customers returning id, name
-- ---
insert into sample.customers (name, email) values ('z', 'z@x.y') on conflict (email) do update set name = excluded.name returning id
-- ---
insert into sample.customers (name, email) values ('z', 'z@x.y') on conflict do nothing
-- ---
update sample.orders o set total = o.total + 1 from sample.customers c where c.id = o.customer_id and c.name = 'a' returning o.id
-- ---
update sample.customers set name = upper(name) where id in (select customer_id from sample.orders)
-- ---
delete from sample.orders o using sample.customers c where c.id = o.customer_id and c.name = 'a' returning o.id
-- ---
delete from sample.order_items where order_id not in (select id from sample.orders)
-- ---
create table sample.corpus_t (id serial primary key, name text not null default '', created timestamptz default now(), unique (name))
-- ---
create temp table corpus_tmp (a int, b text)
-- ---
create index corpus_idx on sample.customers (lower(name)) where email is not null
-- ---
create view sample.corpus_v as select id, name from sample.customers
-- ---
alter table sample.customers add column corpus_c integer default 0 not null
-- ---
alter table sample.customers drop column email
-- ---
drop table sample.no_pk
-- ---
create or replace function sample.corpus_f(a integer) returns integer language plpgsql as $$
begin
    return a + 1;
end;
$$
-- ---
create function sample.corpus_g() returns void language plpgsql as $body$
declare x int := 1;
begin
    perform x;
end
$body$
-- ---
do $$ begin raise notice 'hi; there'; end $$
-- ---
truncate sample.no_pk
-- ---
explain select * from sample.customers
-- ---
select 1; select 2
-- statements: 2
-- ---
begin; select 1; commit
-- statements: 3
-- ---
-- a leading comment
select /* inline ; comment */ 1 -- trailing comment
