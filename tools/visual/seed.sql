-- SQLite sample data (French) used to compare the app with the mockups M03, M05, M07 and M08.
PRAGMA foreign_keys = ON;

CREATE TABLE clients (
    id INTEGER PRIMARY KEY,
    raison_sociale TEXT NOT NULL,
    ville TEXT,
    email TEXT,
    cree_le TEXT NOT NULL
);
CREATE TABLE produits (
    id INTEGER PRIMARY KEY,
    reference TEXT NOT NULL UNIQUE,
    libelle TEXT NOT NULL,
    prix_ht NUMERIC NOT NULL
);
CREATE TABLE stocks (
    produit_id INTEGER PRIMARY KEY REFERENCES produits(id),
    quantite INTEGER NOT NULL,
    entrepot TEXT
);
CREATE TABLE commandes (
    id INTEGER PRIMARY KEY,
    client_id INTEGER NOT NULL REFERENCES clients(id),
    statut TEXT NOT NULL,
    montant_ttc NUMERIC NOT NULL,
    cree_le TEXT NOT NULL,
    note TEXT
);
CREATE TABLE lignes_commande (
    id INTEGER PRIMARY KEY,
    commande_id INTEGER NOT NULL REFERENCES commandes(id),
    produit_id INTEGER NOT NULL REFERENCES produits(id),
    quantite INTEGER NOT NULL
);
CREATE TABLE factures (
    id INTEGER PRIMARY KEY,
    commande_id INTEGER NOT NULL REFERENCES commandes(id),
    numero TEXT NOT NULL,
    emise_le TEXT NOT NULL,
    payee INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX ix_commandes_client_id ON commandes(client_id);
CREATE INDEX ix_commandes_cree_le ON commandes(cree_le);

INSERT INTO clients (id, raison_sociale, ville, email, cree_le) VALUES
 (10482, 'Atelier Lumen SARL', 'Lyon', 'contact@lumen.example', '2023-01-12'),
 (10017, 'Brasserie du Port', 'Marseille', 'port@brasserie.example', '2022-11-03'),
 (11230, 'Coopérative Vallée Verte', NULL, NULL, '2023-04-21'),
 (10955, 'Dumas & Fils', 'Nantes', 'dumas@fils.example', '2023-02-09'),
 (12001, 'Éditions Marelle', 'Paris', 'edition@marelle.example', '2023-06-30'),
 (10344, 'Ferronnerie Arnaud', 'Lille', NULL, '2022-09-15'),
 (11876, 'Groupe Hélios', 'Bordeaux', 'helios@groupe.example', '2023-05-05'),
 (10561, 'Imprimerie Castel', 'Rennes', 'castel@imprimerie.example', '2022-12-01'),
 (12210, 'Jardins de Provence', 'Avignon', 'jardins@provence.example', '2023-07-18'),
 (10093, 'Kerlan Distribution', 'Brest', NULL, '2022-10-27'),
 (10110, 'Librairie du Midi', 'Toulouse', 'midi@librairie.example', '2023-03-14'),
 (10222, 'Menuiserie Vasseur', 'Lyon', 'vasseur@menuiserie.example', '2023-08-02');

INSERT INTO produits (id, reference, libelle, prix_ht) VALUES
 (1, 'PRD-001', 'Lampe d''atelier', 49.90),
 (2, 'PRD-002', 'Fût de bière 30 L', 112.00),
 (3, 'PRD-003', 'Cahier A5 recyclé', 3.20),
 (4, 'PRD-004', 'Rosier grimpant', 14.50),
 (5, 'PRD-005', 'Poignée en laiton', 8.75),
 (6, 'PRD-006', 'Ramette 500 feuilles', 5.60);

INSERT INTO stocks (produit_id, quantite, entrepot) VALUES
 (1, 120, 'Lyon'), (2, 40, 'Marseille'), (3, 2500, 'Paris'),
 (4, 300, NULL), (5, 860, 'Lille'), (6, 1900, 'Paris');

INSERT INTO commandes (id, client_id, statut, montant_ttc, cree_le, note) VALUES
 (1, 10482, 'expediee', 412870.50, '2024-03-02', NULL),
 (2, 10482, 'livree', 1250.00, '2024-03-10', 'Livraison express'),
 (3, 10017, 'expediee', 298114.00, '2024-03-05', NULL),
 (4, 10017, 'annulee', 450.20, '2024-03-06', 'Annulée par le client'),
 (5, 11230, 'livree', 254006.75, '2024-03-08', NULL),
 (6, 10955, 'expediee', 231440.10, '2024-03-09', NULL),
 (7, 12001, 'en_attente', 187902.00, '2024-03-11', NULL),
 (8, 10344, 'livree', 176330.40, '2024-03-12', 'Facture à part'),
 (9, 11876, 'expediee', 162018.95, '2024-03-13', NULL),
 (10, 10561, 'en_attente', 149776.20, '2024-03-14', NULL),
 (11, 12210, 'livree', 138905.00, '2024-03-15', NULL),
 (12, 10093, 'expediee', 131240.60, '2024-03-16', NULL),
 (13, 10110, 'livree', 980.00, '2024-03-17', NULL),
 (14, 10222, 'annulee', 75.00, '2024-03-18', NULL);

INSERT INTO lignes_commande (commande_id, produit_id, quantite) VALUES
 (1, 1, 20), (1, 5, 100), (2, 3, 400), (3, 2, 12), (4, 2, 1), (5, 4, 80),
 (6, 1, 5), (7, 3, 900), (8, 5, 60), (9, 6, 700), (10, 6, 300), (11, 4, 150),
 (12, 1, 33), (13, 3, 120), (14, 5, 4);

INSERT INTO factures (commande_id, numero, emise_le, payee) VALUES
 (1, 'F-2024-0001', '2024-03-03', 1), (2, 'F-2024-0002', '2024-03-11', 1),
 (3, 'F-2024-0003', '2024-03-06', 0), (5, 'F-2024-0004', '2024-03-09', 1),
 (6, 'F-2024-0005', '2024-03-10', 0), (8, 'F-2024-0006', '2024-03-13', 1),
 (9, 'F-2024-0007', '2024-03-14', 0), (11, 'F-2024-0008', '2024-03-16', 1);

CREATE VIEW v_ca_par_client AS
SELECT c.id, c.raison_sociale, c.ville, count(o.id) AS nb_commandes, sum(o.montant_ttc) AS ca_ttc
FROM clients c JOIN commandes o ON o.client_id = c.id
WHERE o.statut <> 'annulee'
GROUP BY c.id, c.raison_sociale, c.ville;
