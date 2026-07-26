import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ExternalLink,
  FolderPlus,
  HardDrive,
  Play,
  Plus,
  RefreshCw,
  RotateCw,
  ScrollText,
  Search,
  Server,
  Settings2,
  Square,
  Trash2,
} from 'lucide-react';
import { useApp } from '../app-context';
import { api, ApiError } from '../api';
import type { Site } from '../types';
import { Badge, Card, ConfirmDialog, Empty, Field, Loading, Modal, StatusDot, toast } from '../components/ui';
import { bytes, dateTime } from '../format';

type Discovered = {
  path: string;
  name: string;
  kind: string;
  port: number | null;
  startCommand: string | null;
  alreadyAdded: boolean;
};

export default function Sites() {
  const { t, lang, sites: liveSites } = useApp();
  const [sites, setSites] = useState<Site[] | null>(null);
  const [sitesRoot, setSitesRoot] = useState('');
  const [busyId, setBusyId] = useState<number | null>(null);
  const [addOpen, setAddOpen] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [discoverOpen, setDiscoverOpen] = useState(false);
  const [logsFor, setLogsFor] = useState<Site | null>(null);
  const [settingsFor, setSettingsFor] = useState<Site | null>(null);
  const [deleteFor, setDeleteFor] = useState<Site | null>(null);

  const load = useCallback(async () => {
    const res = await api<{ sites: Site[]; sitesRoot: string }>('/api/sites');
    setSites(res.sites);
    setSitesRoot(res.sitesRoot);
  }, []);

  useEffect(() => {
    load().catch(() => setSites([]));
  }, [load]);

  // بروزرسانی زندهٔ وضعیت از سوکت
  useEffect(() => {
    if (!liveSites.length) return;
    setSites((prev) => {
      if (!prev) return prev;
      const map = new Map(liveSites.map((s) => [s.id, s]));
      return prev.map((s) => {
        const live = map.get(s.id);
        return live ? { ...s, online: live.online, managed: live.managed, process: live.process } : s;
      });
    });
  }, [liveSites]);

  async function act(site: Site, action: 'start' | 'stop' | 'restart') {
    setBusyId(site.id);
    try {
      await api(`/api/sites/${site.id}/${action}`, { method: 'POST' });
      await load();
      toast(`${site.name} — ${t(action === 'start' ? 'start' : action === 'stop' ? 'stop' : 'restart')}`);
    } catch (e) {
      toast(e instanceof ApiError ? e.code : t('error'), 'bad');
    } finally {
      setBusyId(null);
    }
  }

  if (!sites) return <Loading />;

  return (
    <div className="mx-auto flex max-w-7xl flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h1 className="text-base font-semibold">{t('websites')}</h1>
          <p className="text-xs text-ink-muted">{t('ownFolderNote')}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button className="btn btn-sm" onClick={() => load()}>
            <RefreshCw className="h-3.5 w-3.5" />
            {t('refresh')}
          </button>
          <button className="btn btn-sm" onClick={() => setDiscoverOpen(true)}>
            <Search className="h-3.5 w-3.5" />
            {t('discover')}
          </button>
          <button className="btn btn-sm" onClick={() => setCreateOpen(true)}>
            <FolderPlus className="h-3.5 w-3.5" />
            {t('newSite')}
          </button>
          <button className="btn btn-primary btn-sm" onClick={() => setAddOpen(true)}>
            <Plus className="h-3.5 w-3.5" />
            {t('addSite')}
          </button>
        </div>
      </div>

      {!sites.length ? (
        <Card>
          <Empty icon={<Server className="h-8 w-8" />} title={t('noSites')} hint={`${t('sitesRoot')}: ${sitesRoot}`} />
        </Card>
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          {sites.map((site) => (
            <Card key={site.id} className="flex flex-col">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <h2 className="truncate text-sm font-semibold">{site.name}</h2>
                  <p className="truncate text-[11px] text-ink-muted" title={site.path}>
                    {site.path}
                  </p>
                </div>
                <StatusDot online={site.online} label={site.online ? t('online') : t('offline')} />
              </div>

              <dl className="mt-3 grid grid-cols-2 gap-x-3 gap-y-2 text-xs">
                <Row label={t('domain')} value={site.domain || '—'} />
                <Row label={t('port')} value={site.port ? String(site.port) : '—'} mono />
                <Row label={t('size')} value={bytes(site.sizeBytes)} />
                <Row label={t('lastUpdate')} value={dateTime(site.lastModified, lang)} />
              </dl>

              <div className="mt-3 flex flex-wrap items-center gap-1.5">
                {site.kind && <Badge tone="info">{site.kind}</Badge>}
                {site.managed && <Badge tone="good">{t('running')}</Badge>}
                {!site.pathExists && <Badge tone="bad">{t('pathMissing')}</Badge>}
                {site.errorCount > 0 && <Badge tone="warn">{`${site.errorCount} ${t('errors')}`}</Badge>}
              </div>

              <div className="mt-4 flex flex-wrap gap-1.5 border-t border-line pt-3">
                <a
                  className="btn btn-sm"
                  href={site.port ? `http://${location.hostname}:${site.port}` : `http://${site.domain}`}
                  target="_blank"
                  rel="noreferrer"
                  aria-disabled={!site.port && !site.domain}
                >
                  <ExternalLink className="h-3.5 w-3.5" />
                  {t('open')}
                </a>
                <button className="btn btn-sm" disabled={busyId === site.id} onClick={() => act(site, 'start')}>
                  <Play className="h-3.5 w-3.5" />
                  {t('start')}
                </button>
                <button className="btn btn-sm" disabled={busyId === site.id} onClick={() => act(site, 'stop')}>
                  <Square className="h-3.5 w-3.5" />
                  {t('stop')}
                </button>
                <button className="btn btn-sm" disabled={busyId === site.id} onClick={() => act(site, 'restart')}>
                  <RotateCw className="h-3.5 w-3.5" />
                  {t('restart')}
                </button>
                <button className="btn btn-sm" onClick={() => setLogsFor(site)}>
                  <ScrollText className="h-3.5 w-3.5" />
                  {t('logs')}
                </button>
                <button className="btn btn-sm" onClick={() => setSettingsFor(site)}>
                  <Settings2 className="h-3.5 w-3.5" />
                  {t('settings')}
                </button>
              </div>
            </Card>
          ))}
        </div>
      )}

      <AddSiteModal open={addOpen} onClose={() => setAddOpen(false)} onDone={load} />
      <CreateSiteModal open={createOpen} onClose={() => setCreateOpen(false)} onDone={load} sitesRoot={sitesRoot} />
      <DiscoverModal open={discoverOpen} onClose={() => setDiscoverOpen(false)} onDone={load} />
      {logsFor && <LogsModal site={logsFor} onClose={() => setLogsFor(null)} />}
      {settingsFor && (
        <SiteSettingsModal
          site={settingsFor}
          onClose={() => setSettingsFor(null)}
          onDone={load}
          onDelete={() => {
            setDeleteFor(settingsFor);
            setSettingsFor(null);
          }}
        />
      )}
      <ConfirmDialog
        open={Boolean(deleteFor)}
        danger
        title={t('deleteSite')}
        message={t('deleteSiteConfirm')}
        onCancel={() => setDeleteFor(null)}
        onConfirm={async () => {
          if (!deleteFor) return;
          await api(`/api/sites/${deleteFor.id}`, { method: 'DELETE' });
          setDeleteFor(null);
          await load();
        }}
      />
    </div>
  );
}

function Row({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="min-w-0">
      <dt className="text-[11px] text-ink-muted">{label}</dt>
      <dd className={`truncate ${mono ? 'tnum font-mono' : ''}`}>{value}</dd>
    </div>
  );
}

/* ------------------------------ افزودن با مسیر ------------------------------ */
function AddSiteModal({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: () => Promise<void> }) {
  const { t } = useApp();
  const [path, setPath] = useState('');
  const [name, setName] = useState('');
  const [domain, setDomain] = useState('');
  const [port, setPort] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      await api('/api/sites', { body: { path, name: name || undefined, domain: domain || undefined, port: port || undefined } });
      await onDone();
      setPath('');
      setName('');
      setDomain('');
      setPort('');
      onClose();
      toast(t('saved'));
    } catch (e) {
      setError(e instanceof ApiError ? e.code : t('error'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={t('addSite')}
      footer={
        <>
          <button className="btn" onClick={onClose}>
            {t('cancel')}
          </button>
          <button className="btn btn-primary" disabled={busy || !path.trim()} onClick={submit}>
            {t('add')}
          </button>
        </>
      }
    >
      <Field label={t('addByPath')}>
        <input className="input font-mono" value={path} onChange={(e) => setPath(e.target.value)} placeholder={t('pathPlaceholder')} />
      </Field>
      <Field label={t('name')}>
        <input className="input" value={name} onChange={(e) => setName(e.target.value)} />
      </Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label={t('domain')}>
          <input className="input" value={domain} onChange={(e) => setDomain(e.target.value)} />
        </Field>
        <Field label={t('port')}>
          <input className="input tnum" value={port} onChange={(e) => setPort(e.target.value)} inputMode="numeric" />
        </Field>
      </div>
      {error && <p className="text-xs" style={{ color: 'var(--status-critical)' }}>{error}</p>}
    </Modal>
  );
}

/* ------------------------------ ساخت سایت نو ------------------------------ */
function CreateSiteModal({
  open,
  onClose,
  onDone,
  sitesRoot,
}: {
  open: boolean;
  onClose: () => void;
  onDone: () => Promise<void>;
  sitesRoot: string;
}) {
  const { t } = useApp();
  const [name, setName] = useState('');
  const [domain, setDomain] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={t('newSite')}
      footer={
        <>
          <button className="btn" onClick={onClose}>
            {t('cancel')}
          </button>
          <button
            className="btn btn-primary"
            disabled={busy || !name.trim()}
            onClick={async () => {
              setBusy(true);
              setError(null);
              try {
                await api('/api/sites/create', { body: { name, domain: domain || undefined, kind: 'static' } });
                await onDone();
                setName('');
                setDomain('');
                onClose();
              } catch (e) {
                setError(e instanceof ApiError ? e.code : t('error'));
              } finally {
                setBusy(false);
              }
            }}
          >
            {t('add')}
          </button>
        </>
      }
    >
      <Field label={t('name')} hint={`${sitesRoot}/<${t('name')}>`}>
        <input className="input" value={name} onChange={(e) => setName(e.target.value)} autoFocus />
      </Field>
      <Field label={t('domain')}>
        <input className="input" value={domain} onChange={(e) => setDomain(e.target.value)} />
      </Field>
      <p className="text-xs text-ink-muted">{t('ownFolderNote')}</p>
      {error && <p className="mt-2 text-xs" style={{ color: 'var(--status-critical)' }}>{error}</p>}
    </Modal>
  );
}

/* ------------------------------- کشف خودکار ------------------------------- */
function DiscoverModal({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: () => Promise<void> }) {
  const { t } = useApp();
  const [items, setItems] = useState<Discovered[] | null>(null);
  const [roots, setRoots] = useState<string[]>([]);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!open) return;
    setItems(null);
    api<{ roots: string[]; found: Discovered[] }>('/api/sites/discover', { method: 'POST' })
      .then((res) => {
        setItems(res.found);
        setRoots(res.roots);
        setSelected(new Set(res.found.filter((f) => !f.alreadyAdded).map((f) => f.path)));
      })
      .catch(() => setItems([]));
  }, [open]);

  const available = useMemo(() => (items || []).filter((i) => !i.alreadyAdded), [items]);

  return (
    <Modal
      open={open}
      onClose={onClose}
      wide
      title={t('discover')}
      footer={
        <>
          <button className="btn" onClick={onClose}>
            {t('cancel')}
          </button>
          <button
            className="btn btn-primary"
            disabled={busy || !selected.size}
            onClick={async () => {
              setBusy(true);
              try {
                await api('/api/sites/discover/import', {
                  body: { items: available.filter((i) => selected.has(i.path)) },
                });
                await onDone();
                onClose();
              } finally {
                setBusy(false);
              }
            }}
          >
            {t('importSelected')}
          </button>
        </>
      }
    >
      <p className="mb-3 text-xs text-ink-muted">{t('discoverHint')}</p>
      {roots.length > 0 && (
        <p className="mb-3 truncate font-mono text-[11px] text-ink-muted">{roots.join(' · ')}</p>
      )}
      {items === null ? (
        <Loading label={t('discovering')} />
      ) : !items.length ? (
        <Empty title={t('noData')} />
      ) : (
        <ul className="space-y-1.5">
          {items.map((item) => (
            <li
              key={item.path}
              className="flex items-center gap-3 rounded-xl border border-line p-2.5"
              style={{ background: 'var(--surface-0)' }}
            >
              <input
                type="checkbox"
                className="h-4 w-4 accent-[var(--series-1)]"
                disabled={item.alreadyAdded}
                checked={selected.has(item.path)}
                onChange={(e) => {
                  const next = new Set(selected);
                  if (e.target.checked) next.add(item.path);
                  else next.delete(item.path);
                  setSelected(next);
                }}
              />
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium">{item.name}</p>
                <p className="truncate font-mono text-[11px] text-ink-muted">{item.path}</p>
              </div>
              <Badge tone="info">{item.kind}</Badge>
              {item.alreadyAdded && <Badge>{t('alreadyAdded')}</Badge>}
            </li>
          ))}
        </ul>
      )}
    </Modal>
  );
}

/* --------------------------------- لاگ‌ها --------------------------------- */
function LogsModal({ site, onClose }: { site: Site; onClose: () => void }) {
  const { t, socket } = useApp();
  const [lines, setLines] = useState<{ at: string; level: string; text: string }[]>([]);
  const [filter, setFilter] = useState<'all' | 'error' | 'warn'>('all');

  useEffect(() => {
    api<{ lines: { at: string; level: string; text: string }[] }>(`/api/sites/${site.id}/logs`)
      .then((r) => setLines(r.lines))
      .catch(() => setLines([]));
  }, [site.id]);

  useEffect(() => {
    if (!socket) return;
    socket.emit('watch:site', site.slug);
    const onLog = (entry: { slug: string; level: string; text: string; at: number }) => {
      if (entry.slug !== site.slug) return;
      setLines((prev) => [...prev.slice(-400), { at: new Date(entry.at).toISOString(), level: entry.level, text: entry.text }]);
    };
    socket.on('site:log', onLog);
    return () => {
      socket.emit('unwatch:site', site.slug);
      socket.off('site:log', onLog);
    };
  }, [socket, site.slug]);

  const shown = lines.filter((l) => filter === 'all' || l.level === filter);

  return (
    <Modal
      open
      wide
      onClose={onClose}
      title={`${t('logs')} — ${site.name}`}
      footer={
        <>
          <button
            className="btn btn-danger"
            onClick={async () => {
              await api(`/api/sites/${site.id}/logs`, { method: 'DELETE' });
              setLines([]);
            }}
          >
            <Trash2 className="h-3.5 w-3.5" />
            {t('clearLogs')}
          </button>
          <button className="btn" onClick={onClose}>
            {t('close')}
          </button>
        </>
      }
    >
      <div className="mb-3 flex gap-1.5">
        {(['all', 'error', 'warn'] as const).map((f) => (
          <button
            key={f}
            className={`btn btn-sm ${filter === f ? 'btn-primary' : ''}`}
            onClick={() => setFilter(f)}
          >
            {f === 'all' ? t('all') : f === 'error' ? t('errors') : t('warnings')}
          </button>
        ))}
        <span className="ms-auto self-center text-[11px] text-ink-muted">{t('liveLogs')}</span>
      </div>
      {!shown.length ? (
        <Empty title={t('noLogs')} />
      ) : (
        <pre
          className="max-h-[55vh] overflow-auto rounded-xl border border-line p-3 font-mono text-[11px] leading-relaxed"
          style={{ background: 'var(--surface-0)' }}
          dir="ltr"
        >
          {shown.map((l, i) => (
            <div
              key={i}
              style={{
                color:
                  l.level === 'error'
                    ? 'var(--status-critical)'
                    : l.level === 'warn'
                      ? 'var(--status-warning)'
                      : 'var(--text-secondary)',
              }}
            >
              {l.at.replace('T', ' ').slice(0, 19)}  {l.text}
            </div>
          ))}
        </pre>
      )}
    </Modal>
  );
}

/* ----------------------------- تنظیمات هر سایت ----------------------------- */
function SiteSettingsModal({
  site,
  onClose,
  onDone,
  onDelete,
}: {
  site: Site;
  onClose: () => void;
  onDone: () => Promise<void>;
  onDelete: () => void;
}) {
  const { t } = useApp();
  const [name, setName] = useState(site.name);
  const [domain, setDomain] = useState(site.domain || '');
  const [port, setPort] = useState(site.port ? String(site.port) : '');
  const [startCommand, setStartCommand] = useState(site.startCommand || '');
  const [autostart, setAutostart] = useState(site.autostart);
  const [envText, setEnvText] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api<{ config: { env?: Record<string, string> } }>(`/api/sites/${site.id}`)
      .then((r) =>
        setEnvText(
          Object.entries(r.config?.env || {})
            .map(([k, v]) => `${k}=${v}`)
            .join('\n')
        )
      )
      .catch(() => setEnvText(''));
  }, [site.id]);

  async function save() {
    setBusy(true);
    try {
      await api(`/api/sites/${site.id}`, {
        method: 'PUT',
        body: { name, domain, port: port ? Number(port) : null, startCommand, autostart },
      });
      const env: Record<string, string> = {};
      for (const line of envText.split('\n')) {
        const idx = line.indexOf('=');
        if (idx > 0) env[line.slice(0, idx).trim()] = line.slice(idx + 1).trim();
      }
      await api(`/api/sites/${site.id}/config`, { method: 'PUT', body: { env } });
      await onDone();
      onClose();
      toast(t('saved'));
    } catch {
      toast(t('error'), 'bad');
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={`${t('settings')} — ${site.name}`}
      footer={
        <>
          <button className="btn btn-danger me-auto" onClick={onDelete}>
            <Trash2 className="h-3.5 w-3.5" />
            {t('delete')}
          </button>
          <button className="btn" onClick={onClose}>
            {t('cancel')}
          </button>
          <button className="btn btn-primary" disabled={busy} onClick={save}>
            {t('save')}
          </button>
        </>
      }
    >
      <Field label={t('name')}>
        <input className="input" value={name} onChange={(e) => setName(e.target.value)} />
      </Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label={t('domain')}>
          <input className="input" value={domain} onChange={(e) => setDomain(e.target.value)} />
        </Field>
        <Field label={t('port')}>
          <input className="input tnum" value={port} onChange={(e) => setPort(e.target.value)} inputMode="numeric" />
        </Field>
      </div>
      <Field label={t('startCommand')} hint="${PORT}">
        <input className="input font-mono text-xs" value={startCommand} onChange={(e) => setStartCommand(e.target.value)} />
      </Field>
      <Field label={t('envVars')} hint={t('onePerLine')}>
        <textarea
          className="input h-24 font-mono text-xs"
          value={envText}
          onChange={(e) => setEnvText(e.target.value)}
          dir="ltr"
        />
      </Field>
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          className="h-4 w-4 accent-[var(--series-1)]"
          checked={autostart}
          onChange={(e) => setAutostart(e.target.checked)}
        />
        {t('autostart')}
      </label>
      <p className="mt-4 flex items-center gap-1.5 truncate font-mono text-[11px] text-ink-muted">
        <HardDrive className="h-3.5 w-3.5 shrink-0" />
        {site.workspace}
      </p>
    </Modal>
  );
}
