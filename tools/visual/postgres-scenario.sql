-- PostgreSQL data of the visual verification passes (P20-03). Idempotent: recreates its own schemas and roles.
drop schema if exists ventes cascade;
drop schema if exists archive cascade;
-- Roles are cluster-wide: drop what a previous run granted, then recreate.
do $$
declare r text;
begin
    foreach r in array array['app_read', 'app_write', 'report', 'user', 'equipe', 'membre_unique'] loop
        if exists (select 1 from pg_roles where rolname = r) then
            execute format('drop owned by %I', r);
            execute format('drop role %I', r);
        end if;
    end loop;
end $$;

create schema ventes;
create schema archive;

create table ventes.clients (
    id integer primary key,
    raison_sociale text not null,
    ville text,
    email text,
    cree_le date not null
);
create table ventes.produits (
    id integer primary key,
    reference text not null unique,
    libelle text not null,
    prix_ht numeric(12, 2) not null
);
create table ventes.stocks (
    produit_id integer primary key references ventes.produits (id),
    quantite integer not null,
    entrepot text
);
create table ventes.commandes (
    id integer primary key,
    client_id integer not null references ventes.clients (id),
    statut text not null,
    montant_ttc numeric(14, 2) not null,
    cree_le date not null,
    note text
);
create table ventes.lignes_commande (
    id integer generated always as identity primary key,
    commande_id integer not null references ventes.commandes (id),
    produit_id integer not null references ventes.produits (id),
    quantite integer not null
);
create table ventes.factures (
    id integer generated always as identity primary key,
    commande_id integer not null references ventes.commandes (id),
    numero text not null,
    emise_le date not null,
    payee boolean not null default false
);
create index ix_commandes_client_id on ventes.commandes (client_id);
create index ix_commandes_cree_le on ventes.commandes (cree_le);

insert into ventes.clients values
 (10482, 'Atelier Lumen SARL', 'Lyon', 'contact@lumen.example', '2023-01-12'),
 (10017, 'Brasserie du Port', 'Marseille', 'port@brasserie.example', '2022-11-03'),
 (11230, 'Coopérative Vallée Verte', null, null, '2023-04-21'),
 (10955, 'Dumas & Fils', 'Nantes', 'dumas@fils.example', '2023-02-09'),
 (12001, 'Éditions Marelle', 'Paris', 'edition@marelle.example', '2023-06-30'),
 (10344, 'Ferronnerie Arnaud', 'Lille', null, '2022-09-15'),
 (11876, 'Groupe Hélios', 'Bordeaux', 'helios@groupe.example', '2023-05-05'),
 (10561, 'Imprimerie Castel', 'Rennes', 'castel@imprimerie.example', '2022-12-01');
insert into ventes.produits values
 (1, 'PRD-001', 'Lampe d''atelier', 49.90), (2, 'PRD-002', 'Fût de bière 30 L', 112.00),
 (3, 'PRD-003', 'Cahier A5 recyclé', 3.20), (4, 'PRD-004', 'Rosier grimpant', 14.50),
 (5, 'PRD-005', 'Poignée en laiton', 8.75), (6, 'PRD-006', 'Ramette 500 feuilles', 5.60);
insert into ventes.stocks values (1, 120, 'Lyon'), (2, 40, 'Marseille'), (3, 2500, 'Paris'), (4, 300, null), (5, 860, 'Lille'), (6, 1900, 'Paris');
insert into ventes.commandes values
 (1, 10482, 'expediee', 412870.50, '2024-03-02', null),
 (2, 10482, 'livree', 1250.00, '2024-03-10', 'Livraison express'),
 (3, 10017, 'expediee', 298114.00, '2024-03-05', null),
 (4, 10017, 'annulee', 450.20, '2024-03-06', 'Annulée par le client'),
 (5, 11230, 'livree', 254006.75, '2024-03-08', null),
 (6, 10955, 'expediee', 231440.10, '2024-03-09', null),
 (7, 12001, 'en_attente', 187902.00, '2024-03-11', null),
 (8, 10344, 'livree', 176330.40, '2024-03-12', 'Facture à part');
insert into ventes.lignes_commande (commande_id, produit_id, quantite) values
 (1, 1, 20), (1, 5, 100), (2, 3, 400), (3, 2, 12), (4, 2, 1), (5, 4, 80), (6, 1, 5), (7, 3, 900), (8, 5, 60);
insert into ventes.factures (commande_id, numero, emise_le, payee) values
 (1, 'F-2024-0001', '2024-03-03', true), (2, 'F-2024-0002', '2024-03-11', true),
 (3, 'F-2024-0003', '2024-03-06', false), (5, 'F-2024-0004', '2024-03-09', true);

create view ventes.v_ca_par_client as
select c.id, c.raison_sociale, c.ville, count(o.id) as nb_commandes, sum(o.montant_ttc) as ca_ttc
from ventes.clients c join ventes.commandes o on o.client_id = c.id
where o.statut <> 'annulee'
group by c.id, c.raison_sociale, c.ville;

create function ventes.ttc(ht numeric) returns numeric language sql as 'select ht * 1.2';
create procedure ventes.purger_brouillons() language sql as 'delete from ventes.commandes where statut = ''brouillon''';

-- 450 empty tables: the privilege matrix cuts its rows at 400 (P19-04).
do $$
begin
    for i in 1..450 loop
        execute format('create table archive.%I (id integer primary key)', 'bulk_' || lpad(i::text, 3, '0'));
    end loop;
end $$;

-- Roles: app_write is a member of app_read; "user" is a reserved word (P19-01);
-- equipe has two members, membre_unique exactly one (singular label, P19-18).
create role app_read;
create role app_write in role app_read;
create role report;
create role "user";
create role equipe;
create role membre_unique;
grant equipe to app_write, report;
grant membre_unique to "user";

-- Mix of granted / not granted / partial for the 7 table privileges.
grant usage on schema ventes, archive to app_read, app_write, report, "user";
grant select on all tables in schema ventes to app_read;
grant insert, update on ventes.clients, ventes.commandes to app_write;
grant delete, truncate on ventes.factures to app_write;
grant references, trigger on ventes.produits, ventes.stocks to report;
grant select (id, raison_sociale), update (ville) on ventes.clients to report;
grant all on ventes.lignes_commande to "user";
grant select, insert, update, delete, truncate, references, trigger on archive.bulk_001 to equipe;
grant select on archive.bulk_002 to membre_unique;
grant execute on function ventes.ttc(numeric) to app_read;
