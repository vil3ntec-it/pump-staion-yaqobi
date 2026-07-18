// تست دودِ سرور: set/get/update/remove/push و رویدادهای بلادرنگ + onDisconnect + limitToLast
// اجرا: node test-client.mjs   (سرور باید بالا باشد)
import { WebSocket } from 'ws';

const URL = process.env.WS_URL || 'ws://localhost:8787';
const TOKEN = process.env.AUTH_TOKEN || '';
let idSeq = 0, subSeq = 0;
const results = [];
function ok(name, cond) { results.push([name, !!cond]); console.log((cond ? '✅' : '❌') + ' ' + name); }
const sleep = ms => new Promise(r => setTimeout(r, ms));

function mkClient() {
  const ws = new WebSocket(URL + (TOKEN ? '?token=' + encodeURIComponent(TOKEN) : ''));
  const pending = new Map();
  const subs = new Map(); // subId -> events[]
  let connectedResolve;
  const connected = new Promise(r => (connectedResolve = r));
  ws.on('message', d => {
    const m = JSON.parse(d.toString());
    if (m.op === 'connected') connectedResolve();
    else if (m.op === 'ack' || m.op === 'result') { const p = pending.get(m.id); if (p) { pending.delete(m.id); p(m); } }
    else if (m.op === 'event') { if (!subs.has(m.subId)) subs.set(m.subId, []); subs.get(m.subId).push(m); }
  });
  const call = (obj) => new Promise(res => { const id = ++idSeq; pending.set(id, res); ws.send(JSON.stringify({ ...obj, id })); });
  return {
    ws, connected, subs,
    set: (path, value) => call({ op: 'set', path, value }),
    get: (path) => call({ op: 'get', path }),
    update: (path, value) => call({ op: 'update', path, value }),
    remove: (path) => call({ op: 'remove', path }),
    push: (path, value) => call({ op: 'push', path, value }),
    sub: (path, event, limitToLast) => { const subId = ++subSeq; subs.set(subId, []); ws.send(JSON.stringify({ op: 'sub', path, event, subId, limitToLast })); return subId; },
    onDisc: (path, action, value) => ws.send(JSON.stringify({ op: 'onDisc', path, action, value })),
    close: () => ws.close(),
  };
}

async function main() {
  const A = mkClient();
  await A.connected;
  ok('اتصال و احراز هویت', true);

  // set / get
  await A.set('stations/pump1', { hello: 'world', n: 5 });
  const r1 = await A.get('stations/pump1');
  ok('set و get مقدار کامل', r1.value && r1.value.hello === 'world' && r1.value.n === 5);

  // update (merge)
  await A.update('stations/pump1', { n: 9, extra: true });
  const r2 = await A.get('stations/pump1');
  ok('update فقط کلیدهای داده‌شده را عوض می‌کند', r2.value.hello === 'world' && r2.value.n === 9 && r2.value.extra === true);

  // value listener روی کلاینت دوم
  const B = mkClient();
  await B.connected;
  const vSub = B.sub('stations/pump1', 'value');
  await sleep(150);
  const initial = B.subs.get(vSub);
  ok('value: وضعیت اولیه فرستاده شد', initial.length === 1 && initial[0].value.n === 9);
  await A.set('stations/pump1/n', 42);
  await sleep(200);
  const afterChange = B.subs.get(vSub);
  ok('value: با تغییر، رویداد جدید آمد', afterChange.length === 2 && afterChange[1].value.n === 42);

  // push + child_added + limitToLast
  const CSub = B.sub('chat/room1/group', 'child_added', 3);
  await sleep(100);
  for (let i = 1; i <= 5; i++) { await A.push('chat/room1/group', { t: 'msg' + i, ts: Date.now() + i }); await sleep(5); }
  await sleep(250);
  const childEvents = B.subs.get(CSub);
  const texts = childEvents.map(e => e.value.t);
  ok('push: شناسه ساخته شد و پیام‌ها آمدند', childEvents.length === 5 && texts[texts.length - 1] === 'msg5');
  ok('child_added با ترتیب زمانی درست', texts.slice(-3).join(',') === 'msg3,msg4,msg5');

  // child_changed
  const changeSub = B.sub('chat/room1/group', 'child_changed');
  await sleep(100);
  const firstKey = childEvents[0].key;
  await A.set('chat/room1/group/' + firstKey + '/t', 'edited');
  await sleep(200);
  const chEvents = B.subs.get(changeSub);
  ok('child_changed هنگام ویرایش یک فرزند', chEvents.length >= 1 && chEvents[chEvents.length - 1].key === firstKey);

  // child_removed
  const remSub = B.sub('chat/room1/group', 'child_removed');
  await sleep(100);
  await A.remove('chat/room1/group/' + firstKey);
  await sleep(200);
  const remEvents = B.subs.get(remSub);
  ok('child_removed هنگام حذف یک فرزند', remEvents.length === 1 && remEvents[0].key === firstKey);

  // onDisconnect (presence)
  await A.set('chat/room1/presence/staff', { ts: Date.now() });
  A.onDisc('chat/room1/presence/staff', 'remove');
  const pre = await B.get('chat/room1/presence/staff');
  ok('presence قبل از قطع اتصال موجود است', pre.value && typeof pre.value.ts === 'number');
  A.close();
  await sleep(300);
  const post = await B.get('chat/room1/presence/staff');
  ok('onDisconnect: با قطع اتصال، presence پاک شد', post.value === null);

  // remove کل شاخه
  await B.remove('chat/room1/group');
  const g = await B.get('chat/room1/group');
  ok('remove کل شاخه', g.value === null);

  B.close();
  await sleep(100);
  const passed = results.filter(r => r[1]).length;
  console.log(`\nنتیجه: ${passed}/${results.length} تست موفق`);
  process.exit(passed === results.length ? 0 : 1);
}

main().catch(e => { console.error('خطا در تست:', e); process.exit(1); });
