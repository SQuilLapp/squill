-- SQuilL sample schema for SQLite. Idempotent: safe to run twice.
drop trigger if exists orders_touch;
drop view if exists order_totals;
drop table if exists all_types;
drop table if exists no_pk;
drop table if exists rowid_only;
drop table if exists wr_lookup;
drop table if exists fk_actions;
drop table if exists fk_targets;
drop table if exists order_items;
drop table if exists orders;
drop table if exists customers;

create table customers (
    id integer primary key autoincrement,
    name text not null,
    email text unique,
    created_at datetime not null default current_timestamp
);

create table orders (
    id integer primary key autoincrement,
    customer_id integer not null references customers (id) on delete cascade on update cascade,
    placed_on datetime not null default current_timestamp,
    total numeric(10,2) not null default 0,
    touched integer not null default 0
);
create index orders_customer_placed on orders (customer_id, placed_on);

create table order_items (
    order_id integer not null references orders (id) on delete cascade,
    line_no integer not null,
    sku text not null,
    quantity integer not null default 1,
    primary key (order_id, line_no)
);

-- One foreign key per referential action.
create table fk_targets (id integer primary key);
create table fk_actions (
    id integer primary key,
    a integer references fk_targets (id) on delete cascade,
    b integer references fk_targets (id) on delete set null,
    c integer references fk_targets (id) on delete restrict,
    d integer references fk_targets (id) on delete no action,
    e integer default 0 references fk_targets (id) on delete set default
);

create table no_pk (label text, amount integer);
create table wr_lookup (code text primary key, label text) without rowid;

create view order_totals as
    select c.id as customer_id, c.name, count(o.id) as orders, coalesce(sum(o.total), 0) as total
    from customers c left join orders o on o.customer_id = c.id
    group by c.id, c.name;

create table all_types (
    c_id INTEGER PRIMARY KEY AUTOINCREMENT,
    c_integer INTEGER,
    c_numeric NUMERIC(10,2),
    c_real REAL,
    c_text TEXT,
    c_blob BLOB,
    c_datetime DATETIME,
    c_boolean BOOLEAN,
    c_untyped
);

create trigger orders_touch after update on orders
begin
    update orders set touched = touched + 1 where id = new.id;
end;
