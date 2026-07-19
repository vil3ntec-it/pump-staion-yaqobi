// ============================================================================
//  WebVault Manager — لایهٔ دیتابیس (SQLite داخلی Node — بدون npm install)
//  از node:sqlite استفاده می‌کند (Node 22+). فایل دیتابیس در data/vault.db
// ============================================================================
import { DatabaseSync } from 'node:sqlite';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const DATA_DIR = process.env.WV_DATA_DIR || path.join(__dirname, '..', 'data');
fs.mkdirSync(DATA_DIR, { recursive: true });
export const FILES_DIR = path.join(DATA_DIR, 'files');
fs.mkdirSync(FILES_DIR, { recursive: true });

const DB_PATH = path.join(DATA_DIR, 'vault.db');
export const db = new DatabaseSync(DB_PATH);

db.exec('PRAGMA journal_mode = WAL;');
db.exec('PRAGMA foreign_keys = ON;');

// --- طرح دیتابیس (Schema) ---------------------------------------------------
db.exec(`
CREATE TABLE IF NOT EXISTS meta (
  key   TEXT PRIMARY KEY,
  value TEXT
);

CREATE TABLE IF NOT EXISTS clients (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  name           TEXT NOT NULL,
  email          TEXT,
  phone          TEXT,
  delivery_date  TEXT,
  amount         REAL,
  payment_status TEXT DEFAULT 'unpaid',   -- unpaid | partial | paid
  notes          TEXT,
  created_at     TEXT DEFAULT (datetime('now')),
  updated_at     TEXT DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS servers (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  name        TEXT NOT NULL,
  provider    TEXT,
  ip          TEXT,
  ssh_user    TEXT,
  ssh_port    INTEGER DEFAULT 22,
  server_type TEXT,                        -- VPS | Dedicated | Home | Shared
  os          TEXT,
  cpu         TEXT,
  ram         TEXT,
  storage     TEXT,
  ssh_key_enc TEXT,                        -- رمزنگاری‌شده
  notes       TEXT,
  created_at  TEXT DEFAULT (datetime('now')),
  updated_at  TEXT DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS websites (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  name           TEXT NOT NULL,
  url            TEXT,
  status         TEXT DEFAULT 'active',    -- active | sold | developing | archived
  build_date     TEXT,
  sale_date      TEXT,
  sale_price     REAL,
  client_id      INTEGER REFERENCES clients(id) ON DELETE SET NULL,
  description    TEXT,
  cms            TEXT,                      -- WordPress | Laravel | Custom ...
  language       TEXT,                      -- PHP | Node | ...
  hosting        TEXT,
  server_id      INTEGER REFERENCES servers(id) ON DELETE SET NULL,
  server_ip      TEXT,
  ports          TEXT,
  runtime_version TEXT,                     -- PHP/Node version
  database_name  TEXT,
  created_at     TEXT DEFAULT (datetime('now')),
  updated_at     TEXT DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS domains (
  id            INTEGER PRIMARY KEY AUTOINCREMENT,
  website_id    INTEGER REFERENCES websites(id) ON DELETE SET NULL,
  domain_name   TEXT NOT NULL,
  registrar     TEXT,
  purchase_date TEXT,
  expiry_date   TEXT,
  dns_provider  TEXT,
  nameservers   TEXT,
  cloudflare    TEXT,
  auto_renew    INTEGER DEFAULT 0,
  notes         TEXT,
  created_at    TEXT DEFAULT (datetime('now')),
  updated_at    TEXT DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS credentials (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  website_id INTEGER REFERENCES websites(id) ON DELETE SET NULL,
  server_id  INTEGER REFERENCES servers(id) ON DELETE SET NULL,
  title      TEXT NOT NULL,
  type       TEXT DEFAULT 'password',   -- password | ssh | api | ftp | db | admin
  username   TEXT,
  secret_enc TEXT,                       -- رمزنگاری‌شده (AES-256-GCM)
  url        TEXT,
  notes      TEXT,
  created_at TEXT DEFAULT (datetime('now')),
  updated_at TEXT DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS backups (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  website_id  INTEGER REFERENCES websites(id) ON DELETE CASCADE,
  location    TEXT,                       -- NAS path | disk | cloud ...
  backup_date TEXT,
  type        TEXT DEFAULT 'full',        -- database | files | full
  size        TEXT,
  status      TEXT DEFAULT 'ok',          -- ok | failed | running
  notes       TEXT,
  created_at  TEXT DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS contracts (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  website_id  INTEGER REFERENCES websites(id) ON DELETE SET NULL,
  client_id   INTEGER REFERENCES clients(id) ON DELETE SET NULL,
  filename    TEXT NOT NULL,
  stored_name TEXT NOT NULL,
  mime        TEXT,
  size        INTEGER,
  category    TEXT DEFAULT 'Documents',   -- Contracts | Documents | Images | Credentials | Backup
  uploaded_at TEXT DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS tags (
  id   INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT UNIQUE NOT NULL
);

CREATE TABLE IF NOT EXISTS entity_tags (
  tag_id      INTEGER NOT NULL REFERENCES tags(id) ON DELETE CASCADE,
  entity_type TEXT NOT NULL,               -- website | domain | server | client | credential
  entity_id   INTEGER NOT NULL,
  PRIMARY KEY (tag_id, entity_type, entity_id)
);

CREATE TABLE IF NOT EXISTS activity_log (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  ts          TEXT DEFAULT (datetime('now')),
  action      TEXT,                        -- create | update | delete | login | unlock | export ...
  entity_type TEXT,
  entity_id   INTEGER,
  detail      TEXT,
  actor       TEXT DEFAULT 'owner'
);

CREATE INDEX IF NOT EXISTS idx_domains_expiry   ON domains(expiry_date);
CREATE INDEX IF NOT EXISTS idx_backups_website  ON backups(website_id);
CREATE INDEX IF NOT EXISTS idx_cred_website     ON credentials(website_id);
CREATE INDEX IF NOT EXISTS idx_entity_tags      ON entity_tags(entity_type, entity_id);
`);

// --- کمکی‌های ساده ----------------------------------------------------------
export const meta = {
  get(key) {
    const row = db.prepare('SELECT value FROM meta WHERE key = ?').get(key);
    return row ? row.value : null;
  },
  set(key, value) {
    db.prepare(
      'INSERT INTO meta(key, value) VALUES(?, ?) ON CONFLICT(key) DO UPDATE SET value = excluded.value'
    ).run(key, value);
  },
};

/** ستون‌های مجاز برای هر جدول (جلوگیری از تزریق نام ستون). */
export function insertRow(table, cols, data) {
  const keys = cols.filter((c) => data[c] !== undefined);
  const placeholders = keys.map(() => '?').join(', ');
  const sql = `INSERT INTO ${table} (${keys.join(', ')}) VALUES (${placeholders})`;
  const info = db.prepare(sql).run(...keys.map((k) => data[k]));
  return db.prepare(`SELECT * FROM ${table} WHERE id = ?`).get(info.lastInsertRowid);
}

export function updateRow(table, cols, id, data) {
  const keys = cols.filter((c) => data[c] !== undefined);
  if (keys.length === 0) return db.prepare(`SELECT * FROM ${table} WHERE id = ?`).get(id);
  const setClause = keys.map((k) => `${k} = ?`).join(', ');
  const sql = `UPDATE ${table} SET ${setClause}, updated_at = datetime('now') WHERE id = ?`;
  db.prepare(sql).run(...keys.map((k) => data[k]), id);
  return db.prepare(`SELECT * FROM ${table} WHERE id = ?`).get(id);
}
