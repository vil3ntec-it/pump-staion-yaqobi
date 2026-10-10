/* ══ «حساب‌ها»ی رمزشده — سمتِ گوشی (شورای آمادگیِ عرضه، ۱۴۰۵/۰۷/۲۳) ══════════
 *
 *     node tools/check-kar-seal.mjs
 *
 * همان بردارِ مستقلِ ‎OwnerSealTests.cs‎ (پایتون/cryptography): رمزِ درست ⇒ باز،
 * رمزِ غلط ⇒ ‎null‎، و عکسِ کهنه (‎gate‎ِ پنج‌تکه) «رمزشده» شمرده نمی‌شود.
 */
import { createRequire } from 'node:module';
const app = createRequire(import.meta.url)('../kar/app.js');

const gate4 = 'pbkdf2$sha256$1000$AAECAwQFBgcICQoLDA0ODw==';
const seal = {
  v: 1, iv: 'BwcHBwcHBwcHBwcH',
  ct: 'HnBsr7v+hoHuc/3mWsltjg1igb+iB0cYsNWoK/noOXUN3rD5CRZkEEbFq4bi+eMTw4gIcf6DP3hjRXk78kpdsxzVF/FUO3XstTvSrkAe+iv9CDxcvUlJ94tXuUKvEc2h',
};
let fail = 0;
const ok = (name, c) => { console.log(`${c ? '✅' : '❌'} ${name}`); if (!c) fail++; };

ok('عکسِ رمزشده شناخته می‌شود', app.isSealed({ gate: gate4, seal }));
ok('عکسِ کهنه (gate پنج‌تکه) رمزشده نیست', !app.isSealed({ gate: gate4 + '$AAAA', sections: {} }));
const opened = await app.openSeal(await app.ownerSealKey('رمز-۱۲۳', gate4), seal);
ok('رمزِ درست ⇒ گاوصندوق باز شد', opened && opened.sections.safe.t === 'گاوصندوق');
ok('و چهار عددِ نوار', opened && opened.banner[0][0] === 'مفاد');
ok('رمزِ غلط ⇒ هیچ', (await app.openSeal(await app.ownerSealKey('غلط', gate4), seal)) === null);
ok('gateِ ناجور ⇒ کلیدی نیست', (await app.ownerSealKey('x', 'md5$1$2$3')) === null);
const sum = app.pumpSummary({ gate: gate4, seal, tank: {}, debtors: [] }, { gate: gate4, banner: [['مفاد', '1', '']] });
ok('«همهٔ پمپ‌ها» نوارِ بازشده را می‌گیرد', sum.banner && sum.banner[0][0] === 'مفاد');
ok('بی رمز نوار نیست', app.pumpSummary({ gate: gate4, seal, tank: {}, debtors: [] }, null).banner === null);
process.exit(fail ? 1 : 0);
