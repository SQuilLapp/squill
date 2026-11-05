select * from sample.customers
-- ---
select c.id, c.name, o.total from sample.customers c inner join sample.orders o on o.customer_id = c.id
-- ---
select c.name, o.id from sample.customers c left join sample.orders o on o.customer_id = c.id where o.id is null
-- ---
select c.name, o.id from sample.customers c right outer join sample.orders o on o.customer_id = c.id
-- ---
select c.name, o.id from sample.customers c full outer join sample.orders o on o.customer_id = c.id
-- ---
select c.id, i.sku from sample.customers c cross join sample.order_items i
-- ---
select c.id, x.total from sample.customers c cross apply (select top 1 total from sample.orders o where o.customer_id = c.id order by total desc) x
-- ---
select c.id, x.total from sample.customers c outer apply (select top 1 total from sample.orders o where o.customer_id = c.id) x
-- ---
select name, (select count(*) from sample.orders o where o.customer_id = c.id) as n from sample.customers c
-- ---
select * from sample.customers where id in (select customer_id from sample.orders where total > 10)
-- ---
select * from sample.customers c where exists (select 1 from sample.orders o where o.customer_id = c.id)
-- ---
select * from (select customer_id, sum(total) as s from sample.orders group by customer_id) t where t.s > 5
-- ---
with recent as (select * from sample.orders where placed_on > dateadd(day, -30, getdate())) select count(*) from recent
-- ---
with n(i) as (select 1 union all select i + 1 from n where i < 10) select sum(i) from n option (maxrecursion 100)
-- ---
select id, total, row_number() over (partition by customer_id order by total desc) as rn from sample.orders
-- ---
select id, sum(total) over (order by id rows between unbounded preceding and current row) from sample.orders
-- ---
select id, lag(total) over (order by id), lead(total) over (order by id) from sample.orders
-- ---
select id from sample.customers union select customer_id from sample.orders
-- ---
select id from sample.customers union all select customer_id from sample.orders
-- ---
select id from sample.customers intersect select customer_id from sample.orders
-- ---
select id from sample.customers except select customer_id from sample.orders
-- ---
select top 5 * from sample.customers order by id
-- ---
select top (5) percent * from sample.customers
-- ---
select top 3 with ties * from sample.orders order by customer_id
-- ---
select * from sample.customers order by id offset 1 rows fetch next 2 rows only
-- ---
select customer_id, count(*), sum(total) from sample.orders group by customer_id having sum(total) > 1 order by 2 desc
-- ---
select distinct customer_id from sample.orders order by customer_id
-- ---
select [id], [name] as [n] from [sample].[customers]
-- ---
select "id", "name" from "sample"."customers"
-- ---
select case when total > 100 then 'big' else 'small' end from sample.orders
-- ---
select cast(total as int), convert(varchar(10), placed_on, 120), try_cast('1' as int), try_convert(int, 'x') from sample.orders
-- ---
select isnull(email, 'none'), iif(id > 1, 'a', 'b'), concat(name, '!'), len(name), string_agg(name, ',') from sample.customers group by name, email, id
-- ---
select name from sample.customers where name like 'a%' or name not like '%z' or name like '[abc]%'
-- ---
select id from sample.orders where placed_on between '2020-01-01' and '2020-12-31' and total is not null
-- ---
select dateadd(day, 1, placed_on), datediff(day, placed_on, getdate()), year(placed_on), getdate(), sysdatetime() from sample.orders
-- ---
select N'unicode', 'it''s', 0x41, 1e3, $5.00
-- ---
select * from sample.customers with (nolock) where id = 1
-- ---
select * from sample.customers c with (updlock, rowlock) where c.id = 1
-- ---
select * into #corpus_tmp from sample.customers
-- ---
select * from sample.customers -- trailing comment
where id > 0 /* block */
-- ---
insert into sample.customers (name, email) values ('a', 'a@x.y'), ('b', 'b@x.y')
-- ---
insert into sample.customers (name) select name from sample.customers
-- ---
insert into sample.customers (name, email) output inserted.id, inserted.name values ('z', 'z@x.y')
-- ---
update o set o.total = o.total + 1 from sample.orders o join sample.customers c on c.id = o.customer_id where c.name = 'a'
-- ---
update sample.customers set name = upper(name) output deleted.name, inserted.name where id in (select customer_id from sample.orders)
-- ---
update top (5) sample.orders set total = 0
-- ---
delete o from sample.orders o join sample.customers c on c.id = o.customer_id where c.name = 'a'
-- ---
delete from sample.order_items output deleted.order_id where order_id not in (select id from sample.orders)
-- ---
delete top (5) from sample.order_items
-- ---
merge sample.customers as t using (select 'a@x.y' as email, 'a' as name) as s on t.email = s.email when matched then update set t.name = s.name when not matched then insert (name, email) values (s.name, s.email) output $action;
-- ---
create table sample.corpus_t (id int identity(1, 1) primary key, name nvarchar(50) not null default '', created datetime2 default sysdatetime(), constraint uq_corpus_name unique (name))
-- ---
create table #corpus_tmp2 (a int, b nvarchar(max))
-- ---
create index corpus_idx on sample.customers (name) include (email) where email is not null
-- ---
create view sample.corpus_v as select id, name from sample.customers
-- ---
alter table sample.customers add corpus_c int default 0 not null
-- ---
drop table sample.no_pk
-- ---
create procedure sample.corpus_p @a int as begin select @a; select @a + 1; end
-- ---
declare @x int = 1; select @x
-- statements: 2
-- ---
declare @t table (id int); insert into @t values (1); select * from @t
-- statements: 3
-- ---
truncate table sample.no_pk
-- ---
select 1
go
select 2
-- statements: 2
-- ---
begin tran; select 1; commit
-- statements: 3
