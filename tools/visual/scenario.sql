-- Extra SQL of New-VisualProfile.ps1 -Scenario Full: one block per database, started by a "-- @db <file name without .db>" line.
-- cmp_source and cmp_target start from seed.sql; every other block is run on an empty database.
-- Differences cmp_source -> cmp_target: 1 table only in the source (archive_2023), 1 table only in the target (avoirs),
-- 1 extra column (produits.poids_kg), 1 index only in the target (ix_factures_numero), 1 view that differs (v_ca_par_client).

-- @db cmp_source
CREATE TABLE archive_2023 (
    id INTEGER PRIMARY KEY,
    libelle TEXT NOT NULL,
    cloture_le TEXT
);
INSERT INTO archive_2023 (libelle, cloture_le) VALUES ('Exercice 2023', '2023-12-31');

-- @db cmp_target
CREATE TABLE avoirs (
    id INTEGER PRIMARY KEY,
    commande_id INTEGER NOT NULL REFERENCES commandes(id),
    montant NUMERIC NOT NULL,
    emis_le TEXT
);
ALTER TABLE produits ADD COLUMN poids_kg NUMERIC;
CREATE INDEX ix_factures_numero ON factures(numero);
DROP VIEW v_ca_par_client;
CREATE VIEW v_ca_par_client AS
SELECT c.id, c.raison_sociale, count(o.id) AS nb_commandes
FROM clients c JOIN commandes o ON o.client_id = c.id
GROUP BY c.id, c.raison_sociale;

-- @db import_target
CREATE TABLE people (
    id INTEGER PRIMARY KEY,
    nom TEXT NOT NULL,
    naissance DATE,
    montant NUMERIC
);
