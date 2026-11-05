-- SQuilL sample schema for MySQL 8. Idempotent: safe to run twice. MySQL has no schemas: the objects live in the connection's database.
set foreign_key_checks = 0;
drop view if exists order_totals;
drop function if exists add_numbers;
drop procedure if exists touch_customers;
drop trigger if exists orders_stamp;
drop table if exists all_types, fk_actions, fk_targets, no_pk, order_items, orders, customers;
set foreign_key_checks = 1;

create table customers (
    id int auto_increment primary key,
    name varchar(100) not null,
    email varchar(200) unique,
    created_at timestamp not null default current_timestamp
);

create table orders (
    id int auto_increment primary key,
    customer_id int not null,
    placed_on date not null default (current_date),
    total decimal(18, 4) not null default 0,
    constraint fk_orders_customer foreign key (customer_id) references customers (id) on delete cascade on update cascade,
    key orders_customer_placed (customer_id, placed_on)
);

create table order_items (
    order_id int not null,
    line_no int not null,
    sku varchar(30) not null,
    quantity int not null default 1,
    primary key (order_id, line_no),
    constraint fk_items_order foreign key (order_id) references orders (id) on delete cascade
);

-- One foreign key per referential action (MySQL has no SET DEFAULT: InnoDB rejects it).
create table fk_targets (id int primary key);
create table fk_actions (
    id int primary key,
    a int, b int, c int, d int,
    constraint fk_act_a foreign key (a) references fk_targets (id) on delete cascade,
    constraint fk_act_b foreign key (b) references fk_targets (id) on delete set null,
    constraint fk_act_c foreign key (c) references fk_targets (id) on delete restrict,
    constraint fk_act_d foreign key (d) references fk_targets (id) on delete no action
);

create table no_pk (label text, amount int);

create view order_totals as
    select c.id as customer_id, c.name, count(o.id) as orders, coalesce(sum(o.total), 0) as total
    from customers c left join orders o on o.customer_id = c.id
    group by c.id, c.name;

create function add_numbers(a int, b int) returns int deterministic return a + b;
create procedure touch_customers(in min_id int, in max_id int) update customers set name = concat(name, '') where id between min_id and max_id;
create trigger orders_stamp before insert on orders for each row set new.total = coalesce(new.total, 0);

create table all_types (
    c_tinyint tinyint,
    c_smallint smallint,
    c_int int,
    c_bigint bigint,
    c_decimal decimal(18, 4),
    c_float float,
    c_double double,
    c_bit bit(1),
    c_bool tinyint(1),
    c_char char(3),
    c_varchar varchar(50),
    c_text text,
    c_blob blob,
    c_date date,
    c_time time,
    c_datetime datetime(3),
    c_timestamp timestamp null,
    c_year year,
    c_json json,
    c_enum enum('sad', 'ok', 'happy'),
    c_set set('a', 'b', 'c'),
    c_auto int auto_increment unique
);
