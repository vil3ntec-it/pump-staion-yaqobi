// ============================================================================
//  خروجی‌گیری (Export) — JSON و CSV  + بکاپ کامل برنامه
//  مقادیر محرمانه فقط با پرچم صریح withSecrets=1 رمزگشایی می‌شوند (و لاگ می‌گردد).
// ============================================================================
import { db } from '../db.js';
import { log } from '../activity.js';
import { decField } from '../vault.js';

const TABLES = ['websites', 'domains', 'servers', 'clients', 'backups', 'credentials', 'contracts'];

function toCsv(rows) {
  if (!rows.length) return '';
  const cols = Object.keys(rows[0]);
  const esc = (v) => {
    if (v == null) return '';
    const s = String(v);
    return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  return [cols.join(','), ...rows.map((r) => cols.map((c) => esc(r[c])).join(','))].join('\n');
}

export function register(router) {
  // خروجی CSV یک جدول
  router.get('/api/export/csv/:table', ({ params, res }) => {
    const table = TABLES.includes(params.table) ? params.table : null;
    if (!table) { res.writeHead(400); res.end('جدول نامعتبر'); return; }
    let rows = db.prepare(`SELECT * FROM ${table}`).all();
    if (table === 'credentials') rows = rows.map(({ secret_enc, ...r }) => ({ ...r, secret: '***' }));
    if (table === 'servers') rows = rows.map(({ ssh_key_enc, ...r }) => ({ ...r, ssh_key: '***' }));
    const csv = '﻿' + toCsv(rows); // BOM برای اکسل/فارسی
    res.writeHead(200, {
      'Content-Type': 'text/csv; charset=utf-8',
      'Content-Disposition': `attachment; filename="${table}.csv"`,
    });
    res.end(csv);
    log('export', table, null, 'CSV');
  });

  // خروجی JSON کامل (بکاپ برنامه)
  router.get('/api/export/json', ({ query, vk, res }) => {
    const withSecrets = query.withSecrets === '1';
    const data = {};
    for (const t of TABLES) data[t] = db.prepare(`SELECT * FROM ${t}`).all();
    if (withSecrets) {
      data.credentials = data.credentials.map((r) => ({ ...r, secret: decField(vk, r.secret_enc) }));
      data.servers = data.servers.map((r) => ({ ...r, ssh_key: decField(vk, r.ssh_key_enc) }));
      log('export', 'vault', null, 'JSON با مقادیر محرمانه');
    } else {
      data.credentials = data.credentials.map(({ secret_enc, ...r }) => r);
      data.servers = data.servers.map(({ ssh_key_enc, ...r }) => r);
      log('export', 'vault', null, 'JSON');
    }
    data.exported_at = new Date().toISOString();
    res.writeHead(200, {
      'Content-Type': 'application/json; charset=utf-8',
      'Content-Disposition': `attachment; filename="webvault-export-${Date.now()}.json"`,
    });
    res.end(JSON.stringify(data, null, 2));
  });
}
