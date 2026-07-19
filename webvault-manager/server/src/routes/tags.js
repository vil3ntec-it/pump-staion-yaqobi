// ============================================================================
//  سیستم تگ — قابل استفاده برای همهٔ موجودیت‌ها
// ============================================================================
import { db } from '../db.js';

export function tagsFor(entityType, entityId) {
  return db
    .prepare(
      `SELECT t.name FROM tags t
       JOIN entity_tags et ON et.tag_id = t.id
       WHERE et.entity_type = ? AND et.entity_id = ?
       ORDER BY t.name`
    )
    .all(entityType, entityId)
    .map((r) => r.name);
}

/** لیست تگ‌ها را (آرایه‌ای از رشته) برای یک موجودیت تنظیم می‌کند. */
export function setTags(entityType, entityId, tagNames) {
  if (!Array.isArray(tagNames)) return;
  const clean = [...new Set(tagNames.map((t) => String(t).trim().replace(/^#/, '')).filter(Boolean))];
  db.prepare('DELETE FROM entity_tags WHERE entity_type = ? AND entity_id = ?').run(entityType, entityId);
  const insTag = db.prepare('INSERT INTO tags(name) VALUES(?) ON CONFLICT(name) DO NOTHING');
  const getTag = db.prepare('SELECT id FROM tags WHERE name = ?');
  const link = db.prepare(
    'INSERT INTO entity_tags(tag_id, entity_type, entity_id) VALUES(?, ?, ?) ON CONFLICT DO NOTHING'
  );
  for (const name of clean) {
    insTag.run(name);
    const t = getTag.get(name);
    if (t) link.run(t.id, entityType, entityId);
  }
}

export function register(router) {
  // همهٔ تگ‌ها + تعداد استفاده
  router.get('/api/tags', () =>
    db
      .prepare(
        `SELECT t.name, COUNT(et.tag_id) AS count
         FROM tags t LEFT JOIN entity_tags et ON et.tag_id = t.id
         GROUP BY t.id ORDER BY count DESC, t.name`
      )
      .all()
  );

  // موجودیت‌های دارای یک تگ خاص
  router.get('/api/tags/:name/items', ({ params }) =>
    db
      .prepare(
        `SELECT et.entity_type, et.entity_id
         FROM entity_tags et JOIN tags t ON t.id = et.tag_id
         WHERE t.name = ?`
      )
      .all(params.name)
  );
}
