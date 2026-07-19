// ============================================================================
//  WebVault Manager — لایهٔ رمزنگاری (بدون هیچ وابستگی خارجی)
//  از ماژول داخلی node:crypto استفاده می‌کند: AES-256-GCM + scrypt
// ============================================================================
import crypto from 'node:crypto';

// --- پارامترهای مشتق‌سازی کلید از رمز اصلی (Master Password) ---------------
const SCRYPT = { N: 16384, r: 8, p: 1, keylen: 32 };

/** کلید رمزگذاری (KEK) را از رمز اصلی و نمک می‌سازد. */
export function deriveKek(password, saltB64) {
  const salt = Buffer.from(saltB64, 'base64');
  return crypto.scryptSync(String(password), salt, SCRYPT.keylen, {
    N: SCRYPT.N, r: SCRYPT.r, p: SCRYPT.p, maxmem: 64 * 1024 * 1024,
  });
}

/** نمک تصادفی base64 برای مشتق‌سازی کلید. */
export function newSalt() {
  return crypto.randomBytes(16).toString('base64');
}

/** یک کلید صندوق (Vault Key) تصادفی ۲۵۶ بیتی می‌سازد. */
export function newVaultKey() {
  return crypto.randomBytes(32);
}

/**
 * رمزگذاری AES-256-GCM.
 * خروجی: رشتهٔ base64 از  iv(12) | tag(16) | ciphertext
 */
export function encrypt(key, plaintext) {
  if (plaintext == null || plaintext === '') return null;
  const iv = crypto.randomBytes(12);
  const cipher = crypto.createCipheriv('aes-256-gcm', key, iv);
  const enc = Buffer.concat([cipher.update(String(plaintext), 'utf8'), cipher.final()]);
  const tag = cipher.getAuthTag();
  return Buffer.concat([iv, tag, enc]).toString('base64');
}

/** رمزگشایی مقداری که با encrypt ساخته شده. اگر کلید اشتباه باشد استثنا می‌دهد. */
export function decrypt(key, blobB64) {
  if (blobB64 == null || blobB64 === '') return null;
  const buf = Buffer.from(blobB64, 'base64');
  const iv = buf.subarray(0, 12);
  const tag = buf.subarray(12, 28);
  const data = buf.subarray(28);
  const decipher = crypto.createDecipheriv('aes-256-gcm', key, iv);
  decipher.setAuthTag(tag);
  return Buffer.concat([decipher.update(data), decipher.final()]).toString('utf8');
}

/** توکن نشست تصادفی و امن. */
export function randomToken(bytes = 32) {
  return crypto.randomBytes(bytes).toString('base64url');
}

/** تولید رمز قوی. */
export function generatePassword(opts = {}) {
  const {
    length = 20, upper = true, lower = true, digits = true, symbols = true,
  } = opts;
  let pool = '';
  if (lower) pool += 'abcdefghijkmnopqrstuvwxyz';
  if (upper) pool += 'ABCDEFGHJKLMNPQRSTUVWXYZ';
  if (digits) pool += '23456789';
  if (symbols) pool += '!@#$%^&*()-_=+[]{};:,.?';
  if (!pool) pool = 'abcdefghijkmnopqrstuvwxyz';
  const out = [];
  for (let i = 0; i < length; i++) {
    out.push(pool[crypto.randomInt(pool.length)]);
  }
  return out.join('');
}
