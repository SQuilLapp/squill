-- SQuilL sample schema for SQL Server 2022. Idempotent: safe to run twice.
-- Batches are separated by lines holding only GO (not a T-SQL statement: the loader of the tests splits on them).
drop view if exists sample.order_totals;
drop table if exists sample.all_types, sample.fk_actions, sample.fk_targets, sample.fk_targets_b, sample.fk_targets_c, sample.fk_targets_d, sample.no_pk, sample.order_items, sample.orders, sample.customers;
GO
drop schema if exists sample;
GO
create schema sample;
GO
create table sample.customers (
    id int identity(1, 1) primary key,
    name varchar(100) not null,
    email nvarchar(200) unique,
    created_at datetimeoffset not null default sysdatetimeoffset()
);

create table sample.orders (
    id int identity(1, 1) primary key,
    customer_id int not null,
    placed_on date not null default cast(getdate() as date),
    total decimal(18, 4) not null default 0,
    constraint fk_orders_customer foreign key (customer_id) references sample.customers (id) on delete cascade on update cascade
);
create index orders_customer_placed on sample.orders (customer_id, placed_on);

create table sample.order_items (
    order_id int not null,
    line_no int not null,
    sku varchar(30) not null,
    quantity int not null default 1,
    primary key (order_id, line_no),
    constraint fk_items_order foreign key (order_id) references sample.orders (id) on delete cascade
);

-- One foreign key per referential action (SQL Server has no RESTRICT: NO ACTION is the closest). Each cascading action needs its own parent table: two paths to one parent are refused.
create table sample.fk_targets (id int primary key);
create table sample.fk_targets_b (id int primary key);
create table sample.fk_targets_c (id int primary key);
create table sample.fk_targets_d (id int primary key);
create table sample.fk_actions (
    id int primary key,
    a int,
    b int,
    c int,
    d int default 0,
    constraint fk_act_a foreign key (a) references sample.fk_targets (id) on delete cascade,
    constraint fk_act_b foreign key (b) references sample.fk_targets_b (id) on delete set null,
    constraint fk_act_c foreign key (c) references sample.fk_targets_c (id) on delete no action,
    constraint fk_act_d foreign key (d) references sample.fk_targets_d (id) on delete set default
);

create table sample.no_pk (label nvarchar(max), amount int);
GO
create view sample.order_totals as
    select c.id as customer_id, c.name, count(o.id) as orders, coalesce(sum(o.total), 0) as total
    from sample.customers c left join sample.orders o on o.customer_id = c.id
    group by c.id, c.name;
GO
create table sample.all_types (
    c_tinyint tinyint,
    c_smallint smallint,
    c_int int,
    c_bigint bigint,
    c_decimal decimal(18, 4),
    c_real real,
    c_float float,
    c_bit bit,
    c_char char(3),
    c_nvarchar nvarchar(50),
    c_varchar_max varchar(max),
    c_varbinary_max varbinary(max),
    c_date date,
    c_time time,
    c_datetime2 datetime2(3),
    c_datetimeoffset datetimeoffset,
    c_smalldatetime smalldatetime,
    c_money money,
    c_guid uniqueidentifier,
    c_xml xml,
    c_rowversion rowversion,
    c_identity int identity(1, 1)
);
