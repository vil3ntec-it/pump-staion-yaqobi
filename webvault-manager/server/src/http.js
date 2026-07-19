// ============================================================================
//  WebVault Manager — چارچوب HTTP بسیار سبک (بدون Express، بدون وابستگی)
// ============================================================================
import { getSessionVk } from './vault.js';

const MAX_BODY = 60 * 1024 * 1024; // 60MB (برای آپلود فایل)

export function compilePath(pattern) {
  const names = [];
  const regex = new RegExp(
    '^' +
      pattern.replace(/:[A-Za-z_]+/g, (m) => {
        names.push(m.slice(1));
        return '([^/]+)';
      }) +
      '/?$'
  );
  return { regex, names };
}

export function json(res, status, data) {
  const body = JSON.stringify(data);
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Cache-Control': 'no-store',
  });
  res.end(body);
}

export function readJsonBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let size = 0;
    req.on('data', (c) => {
      size += c.length;
      if (size > MAX_BODY) {
        reject(new Error('حجم درخواست بیش از حد مجاز است'));
        req.destroy();
        return;
      }
      chunks.push(c);
    });
    req.on('end', () => {
      const raw = Buffer.concat(chunks);
      if (raw.length === 0) return resolve({});
      try {
        resolve(JSON.parse(raw.toString('utf8')));
      } catch {
        reject(new Error('بدنهٔ JSON نامعتبر است'));
      }
    });
    req.on('error', reject);
  });
}

export function readRawBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let size = 0;
    req.on('data', (c) => {
      size += c.length;
      if (size > MAX_BODY) {
        reject(new Error('حجم فایل بیش از حد مجاز است'));
        req.destroy();
        return;
      }
      chunks.push(c);
    });
    req.on('end', () => resolve(Buffer.concat(chunks)));
    req.on('error', reject);
  });
}

export function getToken(req) {
  const h = req.headers['authorization'];
  if (h && h.startsWith('Bearer ')) return h.slice(7);
  return req.headers['x-vault-token'] || null;
}

export class HttpError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

/** یک روتر ساده می‌سازد. */
export function createRouter() {
  const routes = [];
  const add = (method, path, handler, opts = {}) => {
    routes.push({ method, ...compilePath(path), handler, auth: opts.auth !== false, raw: !!opts.raw });
  };
  return {
    get: (p, h, o) => add('GET', p, h, o),
    post: (p, h, o) => add('POST', p, h, o),
    put: (p, h, o) => add('PUT', p, h, o),
    del: (p, h, o) => add('DELETE', p, h, o),
    match(method, pathname) {
      for (const r of routes) {
        if (r.method !== method) continue;
        const m = r.regex.exec(pathname);
        if (!m) continue;
        const params = {};
        r.names.forEach((n, i) => (params[n] = decodeURIComponent(m[i + 1])));
        return { route: r, params };
      }
      return null;
    },
    async dispatch(req, res, pathname, query) {
      const found = this.match(req.method, pathname);
      if (!found) return json(res, 404, { error: 'یافت نشد' });
      const { route, params } = found;

      let vk = null;
      if (route.auth) {
        const token = getToken(req);
        vk = token && getSessionVk(token);
        if (!vk) return json(res, 401, { error: 'قفل است یا نشست منقضی شده', locked: true });
      }

      try {
        const ctx = { params, query, vk, req, res };
        if (route.raw) {
          ctx.raw = await readRawBody(req);
        } else if (req.method !== 'GET' && req.method !== 'DELETE') {
          ctx.body = await readJsonBody(req);
        } else {
          ctx.body = {};
        }
        const result = await route.handler(ctx);
        // اگر هندلر خودش پاسخ را نوشته باشد (دانلود/CSV) کاری نمی‌کنیم.
        if (result !== undefined && !res.writableEnded) {
          json(res, 200, result);
        }
      } catch (err) {
        if (res.writableEnded) return;
        const status = err instanceof HttpError ? err.status : 400;
        json(res, status, { error: err.message || 'خطای سرور' });
      }
    },
  };
}
