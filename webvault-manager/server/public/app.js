// ============================================================================
//  WebVault Manager — رابط کاربری (SPA بدون فریم‌ورک، بدون بیلد)
// ============================================================================

// --------------------------- وضعیت و ابزار پایه ---------------------------
const state = {
  token: sessionStorage.getItem('wv_token') || '',
  autoLockMin: 15,
  route: 'dashboard',
  tagFilter: null,
};

const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const money = (n) => (n == null || n === '' ? '—' : Number(n).toLocaleString('fa-IR') + ' تومان');
const fmtDate = (d) => (!d ? '—' : new Date(d).toLocaleDateString('fa-IR'));

const STATUS = { active: 'فعال', sold: 'فروخته‌شده', developing: 'در حال توسعه', archived: 'آرشیو' };
const PAYSTATUS = { unpaid: 'پرداخت‌نشده', partial: 'بخشی', paid: 'پرداخت‌شده' };
const CREDTYPE = { password: 'رمز عبور', ssh: 'SSH Key', api: 'API Key', ftp: 'FTP', db: 'رمز دیتابیس', admin: 'ورود ادمین' };
const FILECATS = { Contracts: 'قراردادها', Documents: 'مدارک', Images: 'تصاویر', Credentials: 'دسترسی‌ها', Backup: 'بکاپ' };

// --------------------------- API ---------------------------
async function api(method, path, body, opts = {}) {
  const headers = { ...(state.token ? { Authorization: `Bearer ${state.token}` } : {}) };
  let payload = body;
  if (body && !opts.raw) { headers['Content-Type'] = 'application/json'; payload = JSON.stringify(body); }
  const res = await fetch('/api' + path, { method, headers, body: payload });
  if (res.status === 401) { doLock(true); throw new Error('نشست منقضی شد — دوباره وارد شوید'); }
  const ct = res.headers.get('content-type') || '';
  const data = ct.includes('application/json') ? await res.json() : await res.text();
  if (!res.ok) throw new Error((data && data.error) || 'خطای سرور');
  resetAutoLock();
  return data;
}
const GET = (p) => api('GET', p);
const POST = (p, b) => api('POST', p, b);
const PUT = (p, b) => api('PUT', p, b);
const DEL = (p) => api('DELETE', p);

// --------------------------- توست ---------------------------
function toast(msg, kind = 'ok') {
  const t = document.createElement('div');
  t.className = `toast ${kind}`;
  t.textContent = msg;
  $('#toasts').appendChild(t);
  setTimeout(() => { t.style.opacity = '0'; setTimeout(() => t.remove(), 300); }, 2800);
}

// --------------------------- قفل خودکار ---------------------------
let lockTimer = null;
function resetAutoLock() {
  if (!state.token) return;
  clearTimeout(lockTimer);
  lockTimer = setTimeout(() => doLock(false, 'به‌دلیل بی‌فعالیتی قفل شد'), state.autoLockMin * 60 * 1000);
}
['click', 'keydown', 'mousemove', 'touchstart'].forEach((e) =>
  document.addEventListener(e, () => { if (state.token) resetAutoLock(); }, { passive: true }));

// --------------------------- صفحهٔ قفل ---------------------------
let isSetup = false;
async function initLock() {
  try {
    const st = await GET('/status');
    state.autoLockMin = st.autoLockMinutes || 15;
    isSetup = !st.initialized;
    $('#lockSubtitle').textContent = isSetup
      ? 'برای شروع، یک «رمز اصلی» قوی تعیین کنید'
      : 'برای دسترسی، رمز اصلی را وارد کنید';
    $('#confirmField').style.display = isSetup ? 'block' : 'none';
    $('#lockBtn').textContent = isSetup ? 'ساخت صندوق' : 'باز کردن';
  } catch {
    $('#lockSubtitle').textContent = 'اتصال به سرور برقرار نشد';
  }
}

$('#lockForm').addEventListener('submit', async (e) => {
  e.preventDefault();
  $('#lockError').textContent = '';
  const pass = $('#masterPass').value;
  if (isSetup && pass !== $('#masterPass2').value) { $('#lockError').textContent = 'دو رمز یکسان نیستند'; return; }
  try {
    const r = isSetup ? await POST('/setup', { password: pass }) : await POST('/unlock', { password: pass });
    state.token = r.token;
    state.autoLockMin = r.autoLockMinutes || 15;
    sessionStorage.setItem('wv_token', state.token);
    $('#masterPass').value = ''; $('#masterPass2').value = '';
    enterApp();
  } catch (err) {
    $('#lockError').textContent = err.message;
  }
});

async function doLock(silent, msg) {
  clearTimeout(lockTimer);
  const had = state.token;
  if (had) { try { await POST('/lock'); } catch {} }
  state.token = '';
  sessionStorage.removeItem('wv_token');
  $('#app').classList.remove('show');
  $('#lockScreen').style.display = 'grid';
  await initLock();
  if (msg) $('#lockError').textContent = msg;
}
$('#lockNowBtn').addEventListener('click', () => doLock());

function enterApp() {
  $('#lockScreen').style.display = 'none';
  $('#app').classList.add('show');
  resetAutoLock();
  navigate(location.hash.replace('#/', '') || 'dashboard');
}

// --------------------------- ناوبری ---------------------------
const PAGE_TITLES = {
  dashboard: 'داشبورد', websites: 'سایت‌ها', domains: 'دامنه‌ها', servers: 'سرورها',
  passwords: 'صندوق رمزها', backups: 'بکاپ‌ها', clients: 'مشتری‌ها', files: 'فایل‌ها', settings: 'تنظیمات',
};
const PAGES = {};

function navigate(route) {
  if (!PAGES[route]) route = 'dashboard';
  state.route = route;
  state.tagFilter = null;
  location.hash = '#/' + route;
  $$('.nav-item').forEach((n) => n.classList.toggle('active', n.dataset.route === route));
  $('#pageTitle').textContent = PAGE_TITLES[route];
  $('#sidebar').classList.remove('open');
  $('#content').innerHTML = '<div class="empty"><div class="big">⏳</div>در حال بارگذاری…</div>';
  PAGES[route]().catch((e) => { $('#content').innerHTML = `<div class="empty"><div class="big">⚠️</div>${esc(e.message)}</div>`; });
}
$$('.nav-item').forEach((n) => n.addEventListener('click', () => navigate(n.dataset.route)));
$('#menuToggle').addEventListener('click', () => $('#sidebar').classList.toggle('open'));
window.addEventListener('hashchange', () => {
  const r = location.hash.replace('#/', '');
  if (r && r !== state.route && state.token) navigate(r);
});

// --------------------------- مودال و فرم عمومی ---------------------------
function closeModal() { $('#modalBack').classList.remove('show'); $('#modal').innerHTML = ''; }
$('#modalBack').addEventListener('click', (e) => { if (e.target.id === 'modalBack') closeModal(); });

function openModal(title, bodyHtml, { wide, footer } = {}) {
  $('#modal').className = 'modal' + (wide ? ' wide' : '');
  $('#modal').innerHTML = `
    <div class="modal-head"><h3>${esc(title)}</h3><button class="close-x" data-x>×</button></div>
    <div class="modal-body">${bodyHtml}</div>
    ${footer ? `<div class="modal-foot">${footer}</div>` : ''}`;
  $('#modalBack').classList.add('show');
  $('#modal [data-x]').addEventListener('click', closeModal);
}

// spec: [{name,label,type,options,section,placeholder,full,value}]
function formHtml(fields, data = {}) {
  let html = '';
  let openGrid = false;
  const closeGrid = () => { if (openGrid) { html += '</div>'; openGrid = false; } };
  for (const f of fields) {
    if (f.section) { closeGrid(); html += `<div class="section-title">${esc(f.section)}</div>`; continue; }
    const val = data[f.name] ?? f.value ?? '';
    let input;
    if (f.type === 'select') {
      input = `<select name="${f.name}">${f.options.map((o) => `<option value="${esc(o.v)}" ${String(val) === String(o.v) ? 'selected' : ''}>${esc(o.t)}</option>`).join('')}</select>`;
    } else if (f.type === 'textarea') {
      input = `<textarea name="${f.name}" placeholder="${esc(f.placeholder || '')}">${esc(val)}</textarea>`;
    } else if (f.type === 'password') {
      input = `<div class="pw-row"><input type="text" name="${f.name}" value="${esc(val)}" placeholder="${esc(f.placeholder || '')}" class="mono" />
        <button type="button" class="btn sm ghost" data-gen="${f.name}">تولید</button></div>`;
    } else {
      input = `<input type="${f.type || 'text'}" name="${f.name}" value="${esc(val)}" placeholder="${esc(f.placeholder || '')}" ${f.type === 'number' ? 'step="any"' : ''} ${f.mono ? 'class="mono"' : ''} />`;
    }
    const field = `<div class="field"><label>${esc(f.label)}</label>${input}</div>`;
    if (f.full) { closeGrid(); html += field; }
    else { if (!openGrid) { html += '<div class="grid-2">'; openGrid = true; } html += field; }
  }
  closeGrid();
  return html;
}

function readForm(fields) {
  const out = {};
  for (const f of fields) {
    if (f.section) continue;
    const elm = $(`[name="${f.name}"]`, $('#modal'));
    if (!elm) continue;
    let v = elm.value.trim();
    if (f.type === 'number') v = v === '' ? null : Number(v);
    // انتخاب خالی (مثل «بدون مشتری») باید null باشد تا کلید خارجی نقض نشود
    else if (f.type === 'select' && v === '') v = null;
    out[f.name] = v;
  }
  return out;
}

// فرم استاندارد ساخت/ویرایش
function entityForm({ title, fields, data = {}, onSave, wide }) {
  openModal(title, formHtml(fields, data), {
    wide,
    footer: `<button class="btn primary" data-save>ذخیره</button><button class="btn ghost" data-cancel>انصراف</button>`,
  });
  $('#modal [data-cancel]').addEventListener('click', closeModal);
  $$('#modal [data-gen]').forEach((b) => b.addEventListener('click', async () => {
    const r = await POST('/generate-password', { length: 20 });
    $(`[name="${b.dataset.gen}"]`, $('#modal')).value = r.password;
  }));
  $('#modal [data-save]').addEventListener('click', async () => {
    const payload = readForm(fields);
    if (data.tags) payload._existingTags = data.tags;
    try {
      await onSave(payload);
      closeModal();
      navigate(state.route);
    } catch (e) { toast(e.message, 'err'); }
  });
}

function tagsField(value) {
  return { name: 'tags_str', label: 'تگ‌ها (با کاما جدا کنید)', full: true, value: (value || []).join(', '), placeholder: 'wordpress, sold, urgent' };
}
const parseTags = (p) => (p.tags_str ? p.tags_str.split(',').map((t) => t.trim()).filter(Boolean) : []);

function confirmDel(msg, fn) {
  openModal('تأیید حذف', `<p style="font-size:14px">${esc(msg)}</p>`, {
    footer: `<button class="btn danger" data-yes>حذف کن</button><button class="btn ghost" data-no>انصراف</button>`,
  });
  $('#modal [data-no]').addEventListener('click', closeModal);
  $('#modal [data-yes]').addEventListener('click', async () => {
    try { await fn(); closeModal(); toast('حذف شد'); navigate(state.route); }
    catch (e) { toast(e.message, 'err'); }
  });
}

function badge(kind, map) { return `<span class="badge ${kind}">${map[kind] || kind}</span>`; }
function tagsHtml(tags) { return (tags || []).map((t) => `<span class="tag">#${esc(t)}</span>`).join(''); }
function emptyState(text, icon = '📭') { return `<div class="empty"><div class="big">${icon}</div>${esc(text)}</div>`; }

// ============================================================================
//  صفحه: داشبورد
// ============================================================================
PAGES.dashboard = async function () {
  const d = await GET('/dashboard');
  const c = $('#content');
  const stat = (k, v, cls, icon) => `<div class="stat ${cls}"><div class="k"><span class="dot"></span>${icon} ${k}</div><div class="v">${v.toLocaleString('fa-IR')}</div></div>`;
  c.innerHTML = `
    <div class="cards">
      ${stat('کل سایت‌ها', d.websites.total, '', '🌐')}
      ${stat('فعال', d.websites.active, 'green', '✅')}
      ${stat('فروخته‌شده', d.websites.sold, 'violet', '💰')}
      ${stat('در حال توسعه', d.websites.developing, 'amber', '🛠️')}
      ${stat('دامنه‌ها', d.counts.domains, '', '🔗')}
      ${stat('سرورها', d.counts.servers, '', '🖥️')}
      ${stat('مشتری‌ها', d.counts.clients, '', '👤')}
      ${stat('رمزهای ذخیره‌شده', d.counts.credentials, '', '🗝️')}
    </div>
    <div class="two-col">
      <div>
        <div class="panel">
          <div class="panel-head"><h3>⏰ دامنه‌های نزدیک انقضا</h3></div>
          <div class="table-wrap">${d.expiringDomains.length ? `<table><thead><tr><th>دامنه</th><th>تاریخ انقضا</th><th>باقی‌مانده</th></tr></thead><tbody>${d.expiringDomains.map((x) => `<tr><td class="mono">${esc(x.domain_name)}</td><td>${fmtDate(x.expiry_date)}</td><td class="${x.days < 0 ? 'expiry-danger' : 'expiry-warn'}">${x.days < 0 ? 'منقضی شده' : x.days + ' روز'}</td></tr>`).join('')}</tbody></table>` : emptyState('دامنهٔ نزدیک انقضایی نیست', '✅')}</div>
        </div>
        <div class="panel">
          <div class="panel-head"><h3>💾 آخرین بکاپ‌ها</h3></div>
          <div class="table-wrap">${d.recentBackups.length ? `<table><thead><tr><th>سایت</th><th>نوع</th><th>تاریخ</th><th>وضعیت</th></tr></thead><tbody>${d.recentBackups.map((b) => `<tr><td>${esc(b.website_name || '—')}</td><td>${esc(b.type)}</td><td>${fmtDate(b.backup_date)}</td><td>${badge(b.status, { ok: 'سالم', failed: 'ناموفق', running: 'در حال اجرا' })}</td></tr>`).join('')}</tbody></table>` : emptyState('هنوز بکاپی ثبت نشده', '💾')}</div>
        </div>
      </div>
      <div>
        <div class="panel">
          <div class="panel-head"><h3>🛡️ هشدارهای امنیتی</h3></div>
          <div class="panel-body">${d.alerts.length ? d.alerts.map((a) => `<div class="alert ${a.level}">${a.level === 'danger' ? '🔴' : a.level === 'warning' ? '🟠' : '🔵'} ${esc(a.text)}</div>`).join('') : emptyState('همه‌چیز مرتب است', '🟢')}</div>
        </div>
        <div class="panel">
          <div class="panel-head"><h3>📜 فعالیت‌های اخیر</h3></div>
          <div class="panel-body" style="max-height:280px;overflow:auto">${d.recentActivity.map((a) => `<div style="font-size:12.5px;color:var(--muted);padding:6px 0;border-bottom:1px solid var(--border)"><b style="color:var(--text)">${esc(a.action)}</b> · ${esc(a.entity_type || '')} ${esc(a.detail || '')}<div style="font-size:11px;color:var(--muted-2)">${fmtDate(a.ts)} ${new Date(a.ts).toLocaleTimeString('fa-IR')}</div></div>`).join('') || emptyState('فعالیتی نیست')}</div>
        </div>
      </div>
    </div>`;
};

// ============================================================================
//  ابزار: سربرگ صفحه با دکمهٔ افزودن
// ============================================================================
function pageHead(title, btnLabel, onAdd) {
  const wrap = document.createElement('div');
  wrap.className = 'panel-head';
  wrap.style.cssText = 'border:1px solid var(--border);border-radius:14px;margin-bottom:16px;background:var(--panel)';
  wrap.innerHTML = `<h3>${esc(title)}</h3><button class="btn primary" data-add>➕ ${esc(btnLabel)}</button>`;
  wrap.querySelector('[data-add]').addEventListener('click', onAdd);
  return wrap;
}

// ============================================================================
//  صفحه: سایت‌ها
// ============================================================================
const websiteFields = (servers, clients) => [
  { section: 'اطلاعات عمومی' },
  { name: 'name', label: 'نام سایت *' }, { name: 'url', label: 'آدرس سایت', mono: true },
  { name: 'status', label: 'وضعیت', type: 'select', options: Object.entries(STATUS).map(([v, t]) => ({ v, t })) },
  { name: 'client_id', label: 'مشتری', type: 'select', options: [{ v: '', t: '— بدون مشتری —' }, ...clients.map((c) => ({ v: c.id, t: c.name }))] },
  { name: 'build_date', label: 'تاریخ ساخت', type: 'date' }, { name: 'sale_date', label: 'تاریخ فروش', type: 'date' },
  { name: 'sale_price', label: 'قیمت فروش (تومان)', type: 'number' },
  { name: 'description', label: 'توضیحات', type: 'textarea', full: true },
  { section: 'اطلاعات فنی' },
  { name: 'cms', label: 'CMS', placeholder: 'WordPress / Laravel / Custom' },
  { name: 'language', label: 'زبان برنامه‌نویسی', placeholder: 'PHP / Node / …' },
  { name: 'hosting', label: 'هاست' },
  { name: 'server_id', label: 'سرور', type: 'select', options: [{ v: '', t: '— انتخاب سرور —' }, ...servers.map((s) => ({ v: s.id, t: s.name }))] },
  { name: 'server_ip', label: 'IP سرور', mono: true }, { name: 'ports', label: 'پورت‌ها', mono: true, placeholder: '80, 443, 3306' },
  { name: 'runtime_version', label: 'نسخهٔ PHP/Node', mono: true }, { name: 'database_name', label: 'نام دیتابیس', mono: true },
];

async function websiteModal(existing) {
  const [servers, clients] = await Promise.all([GET('/servers'), GET('/clients')]);
  const fields = [...websiteFields(servers, clients), tagsField(existing?.tags)];
  entityForm({
    title: existing ? 'ویرایش سایت' : 'سایت جدید', wide: true, fields, data: existing || {},
    onSave: async (p) => {
      p.tags = parseTags(p);
      if (existing) await PUT('/websites/' + existing.id, p); else await POST('/websites', p);
      toast('ذخیره شد');
    },
  });
}

PAGES.websites = async function () {
  const list = await GET('/websites');
  const c = $('#content'); c.innerHTML = '';
  c.appendChild(pageHead('مدیریت سایت‌ها', 'سایت جدید', () => websiteModal()));
  const panel = document.createElement('div'); panel.className = 'panel';
  panel.innerHTML = list.length ? `<div class="table-wrap"><table>
    <thead><tr><th>نام</th><th>آدرس</th><th>وضعیت</th><th>مشتری</th><th>CMS</th><th>قیمت فروش</th><th>تگ‌ها</th><th></th></tr></thead>
    <tbody>${list.map((w) => `<tr data-id="${w.id}">
      <td><b>${esc(w.name)}</b></td>
      <td class="mono">${w.url ? `<a href="${esc(w.url)}" target="_blank">${esc(w.url)}</a>` : '—'}</td>
      <td>${badge(w.status, STATUS)}</td>
      <td>${esc(w.client_name || '—')}</td>
      <td>${esc(w.cms || '—')}</td>
      <td>${w.status === 'sold' ? money(w.sale_price) : '—'}</td>
      <td class="wrap">${tagsHtml(w.tags)}</td>
      <td><div class="row-actions"><button class="btn sm ghost" data-edit>ویرایش</button><button class="btn sm danger" data-del>حذف</button></div></td>
    </tr>`).join('')}</tbody></table></div>` : emptyState('هنوز سایتی ثبت نشده — «سایت جدید» را بزنید', '🌐');
  c.appendChild(panel);
  $$('#content tr[data-id]').forEach((tr) => {
    const id = tr.dataset.id;
    tr.querySelector('[data-edit]')?.addEventListener('click', async () => websiteModal(await GET('/websites/' + id)));
    tr.querySelector('[data-del]')?.addEventListener('click', () => confirmDel('این سایت حذف شود؟', () => DEL('/websites/' + id)));
  });
};

// ============================================================================
//  صفحه: دامنه‌ها
// ============================================================================
async function domainModal(existing) {
  const websites = await GET('/websites');
  const fields = [
    { name: 'domain_name', label: 'نام دامنه *', mono: true }, { name: 'registrar', label: 'ثبت‌کننده (Registrar)' },
    { name: 'website_id', label: 'سایت مرتبط', type: 'select', options: [{ v: '', t: '— هیچ —' }, ...websites.map((w) => ({ v: w.id, t: w.name }))] },
    { name: 'dns_provider', label: 'ارائه‌دهندهٔ DNS' },
    { name: 'purchase_date', label: 'تاریخ خرید', type: 'date' }, { name: 'expiry_date', label: 'تاریخ انقضا', type: 'date' },
    { name: 'nameservers', label: 'Nameservers', type: 'textarea', full: true, mono: true },
    { name: 'cloudflare', label: 'اطلاعات Cloudflare', type: 'textarea', full: true },
    { name: 'notes', label: 'یادداشت', type: 'textarea', full: true },
    tagsField(existing?.tags),
  ];
  entityForm({
    title: existing ? 'ویرایش دامنه' : 'دامنهٔ جدید', wide: true, fields, data: existing || {},
    onSave: async (p) => { p.tags = parseTags(p); if (existing) await PUT('/domains/' + existing.id, p); else await POST('/domains', p); toast('ذخیره شد'); },
  });
}
PAGES.domains = async function () {
  const list = await GET('/domains');
  const c = $('#content'); c.innerHTML = '';
  c.appendChild(pageHead('مدیریت دامنه‌ها', 'دامنهٔ جدید', () => domainModal()));
  const panel = document.createElement('div'); panel.className = 'panel';
  panel.innerHTML = list.length ? `<div class="table-wrap"><table>
    <thead><tr><th>دامنه</th><th>ثبت‌کننده</th><th>سایت</th><th>انقضا</th><th>باقی‌مانده</th><th>تگ‌ها</th><th></th></tr></thead>
    <tbody>${list.map((d) => `<tr data-id="${d.id}">
      <td class="mono"><b>${esc(d.domain_name)}</b></td>
      <td>${esc(d.registrar || '—')}</td>
      <td>${esc(d.website_name || '—')}</td>
      <td>${fmtDate(d.expiry_date)}</td>
      <td>${d.days_to_expiry == null ? '—' : d.days_to_expiry < 0 ? '<span class="expiry-danger">منقضی</span>' : `<span class="${d.days_to_expiry <= 30 ? 'expiry-warn' : ''}">${d.days_to_expiry} روز</span>`}</td>
      <td class="wrap">${tagsHtml(d.tags)}</td>
      <td><div class="row-actions"><button class="btn sm ghost" data-edit>ویرایش</button><button class="btn sm danger" data-del>حذف</button></div></td>
    </tr>`).join('')}</tbody></table></div>` : emptyState('دامنه‌ای ثبت نشده', '🔗');
  c.appendChild(panel);
  $$('#content tr[data-id]').forEach((tr) => {
    const id = tr.dataset.id;
    tr.querySelector('[data-edit]')?.addEventListener('click', async () => domainModal(await GET('/domains/' + id)));
    tr.querySelector('[data-del]')?.addEventListener('click', () => confirmDel('این دامنه حذف شود؟', () => DEL('/domains/' + id)));
  });
};

// ============================================================================
//  صفحه: سرورها
// ============================================================================
async function serverModal(existing) {
  const fields = [
    { section: 'مشخصات' },
    { name: 'name', label: 'نام سرور *' }, { name: 'provider', label: 'ارائه‌دهنده' },
    { name: 'ip', label: 'IP', mono: true }, { name: 'server_type', label: 'نوع', type: 'select', options: [{ v: 'Home', t: 'خانگی' }, { v: 'VPS', t: 'VPS' }, { v: 'Dedicated', t: 'اختصاصی' }, { v: 'Shared', t: 'اشتراکی' }] },
    { name: 'ssh_user', label: 'کاربر SSH', mono: true }, { name: 'ssh_port', label: 'پورت SSH', type: 'number', value: 22 },
    { section: 'سخت‌افزار / سیستم‌عامل' },
    { name: 'os', label: 'سیستم‌عامل' }, { name: 'cpu', label: 'CPU' },
    { name: 'ram', label: 'RAM' }, { name: 'storage', label: 'فضای ذخیره' },
    { section: 'دسترسی امن' },
    { name: 'ssh_key', label: 'SSH Key (رمزنگاری می‌شود)', type: 'textarea', full: true, mono: true, placeholder: existing?.has_ssh_key ? '••••••• (برای تغییر، مقدار جدید وارد کنید)' : '' },
    { name: 'notes', label: 'یادداشت', type: 'textarea', full: true },
    tagsField(existing?.tags),
  ];
  const data = { ...(existing || {}) }; delete data.ssh_key;
  entityForm({
    title: existing ? 'ویرایش سرور' : 'سرور جدید', wide: true, fields, data,
    onSave: async (p) => {
      p.tags = parseTags(p);
      if (existing && (p.ssh_key === '' || p.ssh_key == null)) delete p.ssh_key; // دست‌نخورده بماند
      if (existing) await PUT('/servers/' + existing.id, p); else await POST('/servers', p);
      toast('ذخیره شد');
    },
  });
}
PAGES.servers = async function () {
  const list = await GET('/servers');
  const c = $('#content'); c.innerHTML = '';
  c.appendChild(pageHead('مدیریت هاست و سرور', 'سرور جدید', () => serverModal()));
  const panel = document.createElement('div'); panel.className = 'panel';
  panel.innerHTML = list.length ? `<div class="table-wrap"><table>
    <thead><tr><th>نام</th><th>ارائه‌دهنده</th><th>IP</th><th>نوع</th><th>سیستم‌عامل</th><th>SSH</th><th>اتصال</th><th></th></tr></thead>
    <tbody>${list.map((s) => `<tr data-id="${s.id}">
      <td><b>${esc(s.name)}</b></td><td>${esc(s.provider || '—')}</td>
      <td class="mono">${esc(s.ip || '—')}</td><td>${esc(s.server_type || '—')}</td><td>${esc(s.os || '—')}</td>
      <td class="mono">${esc(s.ssh_user || '')}${s.ssh_port ? ':' + s.ssh_port : ''} ${s.has_ssh_key ? '🔑' : ''}</td>
      <td><button class="btn sm ghost" data-test>تست</button> <span data-testres style="font-size:12px"></span></td>
      <td><div class="row-actions"><button class="btn sm ghost" data-edit>ویرایش</button><button class="btn sm danger" data-del>حذف</button></div></td>
    </tr>`).join('')}</tbody></table></div>` : emptyState('سروری ثبت نشده', '🖥️');
  c.appendChild(panel);
  $$('#content tr[data-id]').forEach((tr) => {
    const id = tr.dataset.id;
    tr.querySelector('[data-edit]')?.addEventListener('click', async () => serverModal(await GET('/servers/' + id)));
    tr.querySelector('[data-del]')?.addEventListener('click', () => confirmDel('این سرور حذف شود؟', () => DEL('/servers/' + id)));
    tr.querySelector('[data-test]')?.addEventListener('click', async (e) => {
      const out = tr.querySelector('[data-testres]');
      e.target.disabled = true; out.textContent = '⏳';
      try {
        const r = await POST(`/servers/${id}/test`);
        out.innerHTML = r.reachable ? `<span style="color:var(--green)">✓ باز (${r.latencyMs}ms)</span>` : `<span style="color:var(--red)">✗ ${esc(r.error || 'بسته')}</span>`;
      } catch (err) { out.innerHTML = `<span style="color:var(--red)">${esc(err.message)}</span>`; }
      e.target.disabled = false;
    });
  });
};

// ============================================================================
//  صفحه: صندوق رمزها
// ============================================================================
async function credentialModal(existing) {
  const [websites, servers] = await Promise.all([GET('/websites'), GET('/servers')]);
  const fields = [
    { name: 'title', label: 'عنوان *' },
    { name: 'type', label: 'نوع', type: 'select', options: Object.entries(CREDTYPE).map(([v, t]) => ({ v, t })) },
    { name: 'username', label: 'نام کاربری', mono: true },
    { name: 'secret', label: 'مقدار محرمانه (رمز/کلید)', type: 'password', placeholder: existing?.has_secret ? '••••••• (برای تغییر مقدار جدید وارد کنید)' : '' },
    { name: 'website_id', label: 'سایت مرتبط', type: 'select', options: [{ v: '', t: '— هیچ —' }, ...websites.map((w) => ({ v: w.id, t: w.name }))] },
    { name: 'server_id', label: 'سرور مرتبط', type: 'select', options: [{ v: '', t: '— هیچ —' }, ...servers.map((s) => ({ v: s.id, t: s.name }))] },
    { name: 'url', label: 'آدرس ورود', mono: true, full: true },
    { name: 'notes', label: 'یادداشت', type: 'textarea', full: true },
    tagsField(existing?.tags),
  ];
  const data = { ...(existing || {}) };
  entityForm({
    title: existing ? 'ویرایش رمز' : 'رمز جدید', wide: true, fields, data,
    onSave: async (p) => {
      p.tags = parseTags(p);
      if (existing && (p.secret === '' || p.secret == null)) delete p.secret;
      if (existing) await PUT('/credentials/' + existing.id, p); else await POST('/credentials', p);
      toast('ذخیره شد');
    },
  });
}
async function copySecret(id) {
  try {
    const r = await GET(`/credentials/${id}/reveal`);
    await navigator.clipboard.writeText(r.secret || '');
    toast('در کلیپ‌بورد کپی شد 📋');
  } catch (e) { toast(e.message, 'err'); }
}
PAGES.passwords = async function () {
  const list = await GET('/credentials');
  const c = $('#content'); c.innerHTML = '';
  c.appendChild(pageHead('صندوق رمزها', 'رمز جدید', () => credentialModal()));
  const panel = document.createElement('div'); panel.className = 'panel';
  panel.innerHTML = list.length ? `<div class="table-wrap"><table>
    <thead><tr><th>عنوان</th><th>نوع</th><th>نام کاربری</th><th>مقدار</th><th>تگ‌ها</th><th></th></tr></thead>
    <tbody>${list.map((k) => `<tr data-id="${k.id}">
      <td><b>${esc(k.title)}</b></td><td>${esc(CREDTYPE[k.type] || k.type)}</td>
      <td class="mono">${esc(k.username || '—')}</td>
      <td>${k.has_secret ? `<span class="mono">••••••••</span> <button class="btn sm ghost" data-copy>📋 کپی</button>` : '—'}</td>
      <td class="wrap">${tagsHtml(k.tags)}</td>
      <td><div class="row-actions"><button class="btn sm ghost" data-edit>ویرایش</button><button class="btn sm danger" data-del>حذف</button></div></td>
    </tr>`).join('')}</tbody></table></div>` : emptyState('رمزی ذخیره نشده — همه‌چیز با AES-256 رمز می‌شود', '🗝️');
  c.appendChild(panel);
  $$('#content tr[data-id]').forEach((tr) => {
    const id = tr.dataset.id;
    const item = list.find((x) => x.id == id);
    tr.querySelector('[data-copy]')?.addEventListener('click', () => copySecret(id));
    tr.querySelector('[data-edit]')?.addEventListener('click', () => credentialModal(item));
    tr.querySelector('[data-del]')?.addEventListener('click', () => confirmDel('این رمز حذف شود؟', () => DEL('/credentials/' + id)));
  });
};

// ============================================================================
//  صفحه: بکاپ‌ها
// ============================================================================
async function backupModal(existing) {
  const websites = await GET('/websites');
  const fields = [
    { name: 'website_id', label: 'سایت *', type: 'select', options: websites.map((w) => ({ v: w.id, t: w.name })) },
    { name: 'type', label: 'نوع بکاپ', type: 'select', options: [{ v: 'full', t: 'کامل (Full)' }, { v: 'database', t: 'دیتابیس' }, { v: 'files', t: 'فایل‌ها' }] },
    { name: 'backup_date', label: 'تاریخ', type: 'date', value: new Date().toISOString().slice(0, 10) },
    { name: 'status', label: 'وضعیت', type: 'select', options: [{ v: 'ok', t: 'سالم' }, { v: 'failed', t: 'ناموفق' }, { v: 'running', t: 'در حال اجرا' }] },
    { name: 'location', label: 'محل بکاپ', full: true, placeholder: '/nas/backups یا آدرس هارد شبکه', mono: true },
    { name: 'size', label: 'حجم', placeholder: '2.3 GB' },
    { name: 'notes', label: 'یادداشت', type: 'textarea', full: true },
  ];
  entityForm({
    title: existing ? 'ویرایش بکاپ' : 'ثبت بکاپ', wide: true, fields, data: existing || {},
    onSave: async (p) => { if (existing) await PUT('/backups/' + existing.id, p); else await POST('/backups', p); toast('ذخیره شد'); },
  });
}
PAGES.backups = async function () {
  const list = await GET('/backups');
  const c = $('#content'); c.innerHTML = '';
  c.appendChild(pageHead('مدیریت بکاپ‌ها', 'ثبت بکاپ', () => backupModal()));
  const panel = document.createElement('div'); panel.className = 'panel';
  panel.innerHTML = list.length ? `<div class="table-wrap"><table>
    <thead><tr><th>سایت</th><th>نوع</th><th>تاریخ</th><th>محل</th><th>حجم</th><th>وضعیت</th><th></th></tr></thead>
    <tbody>${list.map((b) => `<tr data-id="${b.id}">
      <td><b>${esc(b.website_name || '—')}</b></td>
      <td>${({ full: 'کامل', database: 'دیتابیس', files: 'فایل‌ها' })[b.type] || esc(b.type)}</td>
      <td>${fmtDate(b.backup_date)}</td><td class="mono">${esc(b.location || '—')}</td><td>${esc(b.size || '—')}</td>
      <td>${badge(b.status, { ok: 'سالم', failed: 'ناموفق', running: 'در حال اجرا' })}</td>
      <td><div class="row-actions"><button class="btn sm ghost" data-edit>ویرایش</button><button class="btn sm danger" data-del>حذف</button></div></td>
    </tr>`).join('')}</tbody></table></div>` : emptyState('بکاپی ثبت نشده', '💾');
  c.appendChild(panel);
  $$('#content tr[data-id]').forEach((tr) => {
    const id = tr.dataset.id;
    tr.querySelector('[data-edit]')?.addEventListener('click', async () => {
      const all = await GET('/backups'); backupModal(all.find((x) => x.id == id));
    });
    tr.querySelector('[data-del]')?.addEventListener('click', () => confirmDel('این بکاپ حذف شود؟', () => DEL('/backups/' + id)));
  });
};

// ============================================================================
//  صفحه: مشتری‌ها
// ============================================================================
async function clientModal(existing) {
  const fields = [
    { name: 'name', label: 'نام مشتری *' }, { name: 'email', label: 'ایمیل', mono: true },
    { name: 'phone', label: 'شماره تماس', mono: true }, { name: 'delivery_date', label: 'تاریخ تحویل', type: 'date' },
    { name: 'amount', label: 'مبلغ (تومان)', type: 'number' },
    { name: 'payment_status', label: 'وضعیت پرداخت', type: 'select', options: Object.entries(PAYSTATUS).map(([v, t]) => ({ v, t })) },
    { name: 'notes', label: 'یادداشت', type: 'textarea', full: true },
    tagsField(existing?.tags),
  ];
  entityForm({
    title: existing ? 'ویرایش مشتری' : 'مشتری جدید', fields, data: existing || {},
    onSave: async (p) => { p.tags = parseTags(p); if (existing) await PUT('/clients/' + existing.id, p); else await POST('/clients', p); toast('ذخیره شد'); },
  });
}
PAGES.clients = async function () {
  const list = await GET('/clients');
  const c = $('#content'); c.innerHTML = '';
  c.appendChild(pageHead('مدیریت مشتری‌ها', 'مشتری جدید', () => clientModal()));
  const panel = document.createElement('div'); panel.className = 'panel';
  panel.innerHTML = list.length ? `<div class="table-wrap"><table>
    <thead><tr><th>نام</th><th>ایمیل</th><th>تماس</th><th>تحویل</th><th>مبلغ</th><th>پرداخت</th><th></th></tr></thead>
    <tbody>${list.map((c2) => `<tr data-id="${c2.id}">
      <td><b>${esc(c2.name)}</b></td><td class="mono">${esc(c2.email || '—')}</td><td class="mono">${esc(c2.phone || '—')}</td>
      <td>${fmtDate(c2.delivery_date)}</td><td>${money(c2.amount)}</td><td>${badge(c2.payment_status || 'unpaid', PAYSTATUS)}</td>
      <td><div class="row-actions"><button class="btn sm ghost" data-edit>ویرایش</button><button class="btn sm danger" data-del>حذف</button></div></td>
    </tr>`).join('')}</tbody></table></div>` : emptyState('مشتری‌ای ثبت نشده', '👤');
  c.appendChild(panel);
  $$('#content tr[data-id]').forEach((tr) => {
    const id = tr.dataset.id;
    tr.querySelector('[data-edit]')?.addEventListener('click', async () => clientModal(await GET('/clients/' + id)));
    tr.querySelector('[data-del]')?.addEventListener('click', () => confirmDel('این مشتری حذف شود؟', () => DEL('/clients/' + id)));
  });
};

// ============================================================================
//  صفحه: فایل‌ها
// ============================================================================
async function uploadModal() {
  const websites = await GET('/websites');
  openModal('آپلود فایل', `
    <div class="field"><label>سایت مرتبط</label><select id="upW"><option value="">— عمومی —</option>${websites.map((w) => `<option value="${w.id}">${esc(w.name)}</option>`).join('')}</select></div>
    <div class="field"><label>دسته</label><select id="upC">${Object.entries(FILECATS).map(([v, t]) => `<option value="${v}">${t}</option>`).join('')}</select></div>
    <div class="field"><label>فایل</label><input type="file" id="upF" /></div>`, {
    footer: `<button class="btn primary" id="upBtn">آپلود</button><button class="btn ghost" data-cancel>انصراف</button>`,
  });
  $('#modal [data-cancel]').addEventListener('click', closeModal);
  $('#upBtn').addEventListener('click', async () => {
    const f = $('#upF').files[0];
    if (!f) { toast('فایلی انتخاب نشده', 'err'); return; }
    const w = $('#upW').value, cat = $('#upC').value;
    const qs = new URLSearchParams({ filename: f.name, category: cat, mime: f.type || 'application/octet-stream', ...(w ? { website_id: w } : {}) });
    try {
      await api('POST', '/files?' + qs.toString(), await f.arrayBuffer(), { raw: true });
      closeModal(); toast('آپلود شد'); navigate('files');
    } catch (e) { toast(e.message, 'err'); }
  });
}
PAGES.files = async function () {
  const list = await GET('/files');
  const c = $('#content'); c.innerHTML = '';
  c.appendChild(pageHead('فایل‌ها و قراردادها', 'آپلود فایل', () => uploadModal()));
  const panel = document.createElement('div'); panel.className = 'panel';
  const fmtSize = (n) => (n > 1048576 ? (n / 1048576).toFixed(1) + ' MB' : (n / 1024).toFixed(0) + ' KB');
  panel.innerHTML = list.length ? `<div class="table-wrap"><table>
    <thead><tr><th>نام فایل</th><th>سایت</th><th>دسته</th><th>حجم</th><th>تاریخ</th><th></th></tr></thead>
    <tbody>${list.map((f) => `<tr data-id="${f.id}">
      <td><b>${esc(f.filename)}</b></td><td>${esc(f.website_name || 'عمومی')}</td>
      <td>${FILECATS[f.category] || esc(f.category)}</td><td>${fmtSize(f.size)}</td><td>${fmtDate(f.uploaded_at)}</td>
      <td><div class="row-actions"><a class="btn sm ghost" href="/api/files/${f.id}/download?token=1" data-dl>⬇️ دانلود</a><button class="btn sm danger" data-del>حذف</button></div></td>
    </tr>`).join('')}</tbody></table></div>` : emptyState('فایلی آپلود نشده', '📁');
  c.appendChild(panel);
  $$('#content tr[data-id]').forEach((tr) => {
    const id = tr.dataset.id;
    tr.querySelector('[data-dl]')?.addEventListener('click', async (e) => {
      e.preventDefault();
      // دانلود با هدر توکن → از fetch و blob استفاده می‌کنیم
      try {
        const res = await fetch(`/api/files/${id}/download`, { headers: { Authorization: `Bearer ${state.token}` } });
        if (!res.ok) throw new Error('دانلود ناموفق');
        const blob = await res.blob();
        const a = document.createElement('a'); a.href = URL.createObjectURL(blob);
        a.download = tr.querySelector('b').textContent; a.click(); URL.revokeObjectURL(a.href);
      } catch (err) { toast(err.message, 'err'); }
    });
    tr.querySelector('[data-del]')?.addEventListener('click', () => confirmDel('این فایل حذف شود؟', () => DEL('/files/' + id)));
  });
};

// ============================================================================
//  صفحه: تنظیمات
// ============================================================================
PAGES.settings = async function () {
  const c = $('#content');
  c.innerHTML = `
    <div class="panel"><div class="panel-head"><h3>🔒 امنیت</h3></div><div class="panel-body">
      <div class="grid-2">
        <div class="field"><label>قفل خودکار بعد از (دقیقه)</label>
          <div class="pw-row"><input type="number" id="setLock" value="${state.autoLockMin}" min="1" max="240" />
          <button class="btn sm primary" id="saveLock">ذخیره</button></div></div>
      </div>
      <div class="section-title">تغییر رمز اصلی</div>
      <div class="grid-2">
        <div class="field"><label>رمز فعلی</label><input type="password" id="oldPass" /></div>
        <div class="field"><label>رمز جدید</label><input type="password" id="newPass" /></div>
      </div>
      <button class="btn primary" id="changePass">تغییر رمز اصلی</button>
    </div></div>

    <div class="panel"><div class="panel-head"><h3>📤 خروجی و بکاپ برنامه</h3></div><div class="panel-body">
      <p style="color:var(--muted);font-size:13px">از داده‌ها خروجی بگیرید. خروجی JSON شامل مقادیر محرمانه <b>نمی‌شود</b> مگر گزینهٔ مربوطه را بزنید.</p>
      <div style="display:flex;gap:10px;flex-wrap:wrap">
        <button class="btn ghost" data-exp="json">JSON (بدون رمزها)</button>
        <button class="btn ghost" data-exp="json-secrets">JSON کامل (با رمزها) ⚠️</button>
        <button class="btn ghost" data-exp="csv/websites">CSV سایت‌ها</button>
        <button class="btn ghost" data-exp="csv/domains">CSV دامنه‌ها</button>
        <button class="btn ghost" data-exp="csv/clients">CSV مشتری‌ها</button>
      </div>
    </div></div>

    <div class="panel"><div class="panel-head"><h3>🏷️ تگ‌ها</h3></div><div class="panel-body" id="tagCloud"></div></div>

    <div class="panel"><div class="panel-head"><h3>ℹ️ درباره</h3></div><div class="panel-body" style="color:var(--muted);font-size:13px">
      WebVault Manager نسخهٔ ۱.۰ — سرور خانگی، داده‌ها روی همین سرور و رمزنگاری‌شده با AES-256-GCM ذخیره می‌شوند.
    </div></div>`;

  $('#saveLock').addEventListener('click', async () => {
    const m = Number($('#setLock').value);
    try { const r = await POST('/settings/auto-lock', { minutes: m }); state.autoLockMin = r.autoLockMinutes; resetAutoLock(); toast('ذخیره شد'); }
    catch (e) { toast(e.message, 'err'); }
  });
  $('#changePass').addEventListener('click', async () => {
    try {
      await POST('/change-master', { oldPassword: $('#oldPass').value, newPassword: $('#newPass').value });
      toast('رمز اصلی تغییر کرد'); $('#oldPass').value = ''; $('#newPass').value = '';
    } catch (e) { toast(e.message, 'err'); }
  });
  $$('[data-exp]').forEach((b) => b.addEventListener('click', async () => {
    const kind = b.dataset.exp;
    let path = kind === 'json' ? '/export/json' : kind === 'json-secrets' ? '/export/json?withSecrets=1' : '/export/' + kind;
    try {
      const res = await fetch('/api' + path, { headers: { Authorization: `Bearer ${state.token}` } });
      const blob = await res.blob();
      const a = document.createElement('a'); a.href = URL.createObjectURL(blob);
      a.download = (res.headers.get('content-disposition') || '').match(/filename="?([^"]+)"?/)?.[1] || 'export';
      a.click(); URL.revokeObjectURL(a.href); toast('خروجی آماده شد');
    } catch (e) { toast(e.message, 'err'); }
  }));
  try {
    const tags = await GET('/tags');
    $('#tagCloud').innerHTML = tags.length ? tags.map((t) => `<span class="tag on">#${esc(t.name)} <b>${t.count}</b></span>`).join('') : emptyState('تگی وجود ندارد');
  } catch {}
};

// ============================================================================
//  جستجوی سراسری
// ============================================================================
let searchTimer;
$('#globalSearch').addEventListener('input', (e) => {
  clearTimeout(searchTimer);
  const q = e.target.value.trim();
  const box = $('#searchResults');
  if (!q) { box.classList.remove('show'); return; }
  searchTimer = setTimeout(async () => {
    try {
      const r = await GET('/search?q=' + encodeURIComponent(q));
      const groups = [
        ['سایت‌ها', 'websites', r.websites, (x) => x.name, 'websites'],
        ['دامنه‌ها', 'domains', r.domains, (x) => x.domain_name, 'domains'],
        ['مشتری‌ها', 'clients', r.clients, (x) => x.name, 'clients'],
        ['سرورها', 'servers', r.servers, (x) => `${x.name} (${x.ip || ''})`, 'servers'],
        ['رمزها', 'credentials', r.credentials, (x) => x.title, 'passwords'],
      ];
      let html = '';
      for (const [label, , items, fmt, route] of groups) {
        if (!items.length) continue;
        html += `<div class="search-group-title">${label}</div>`;
        html += items.map((it) => `<div class="search-row" data-route="${route}">${esc(fmt(it))}<span style="color:var(--muted-2)">${label}</span></div>`).join('');
      }
      box.innerHTML = html || `<div class="search-row">نتیجه‌ای یافت نشد</div>`;
      box.classList.add('show');
      $$('.search-row[data-route]', box).forEach((row) => row.addEventListener('click', () => {
        box.classList.remove('show'); $('#globalSearch').value = ''; navigate(row.dataset.route);
      }));
    } catch {}
  }, 250);
});
document.addEventListener('click', (e) => { if (!e.target.closest('.search-box')) $('#searchResults').classList.remove('show'); });

// ============================================================================
//  شروع
// ============================================================================
(async function boot() {
  await initLock();
  if (state.token) {
    try { await GET('/dashboard'); enterApp(); }
    catch { doLock(true); }
  }
})();
