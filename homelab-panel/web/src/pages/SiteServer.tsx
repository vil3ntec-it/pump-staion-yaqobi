import { useCallback, useEffect, useState } from 'react';
import { Database, Eye, EyeOff, Info, Link2, Plug, RefreshCw, RotateCw, TriangleAlert } from 'lucide-react';
import { useApp } from '../app-context';
import { api } from '../api';
import type { SiteServerInfo } from '../types';
import { Badge, Card, ConfirmDialog, CopyButton, Empty, Loading, toast } from '../components/ui';
import { bytes, dateTime, ltr } from '../format';

/* ---------------------------------------------------------------------------
   «سرور سایت» — سرور برنامهٔ پمپ یعقوبی روی همین پنل و همین پورت اجرا می‌شود.
   آدرس و رمزی که سایت لازم دارد، اینجا پیدا می‌شود.
--------------------------------------------------------------------------- */
export default function SiteServer() {
  const { t, lang } = useApp();
  const [info, setInfo] = useState<SiteServerInfo | { enabled: false } | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [rotateOpen, setRotateOpen] = useState(false);

  const load = useCallback(async () => {
    try {
      setInfo(await api<SiteServerInfo>('/api/site-server'));
    } catch {
      setInfo({ enabled: false });
    }
  }, []);

  useEffect(() => {
    load();
    const timer = setInterval(load, 8000);
    return () => clearInterval(timer);
  }, [load]);

  if (!info) return <Loading />;
  if (!info.enabled) {
    return (
      <Card>
        <Empty icon={<Plug className="h-8 w-8" />} title={t('notSupported')} />
      </Card>
    );
  }

  const data = info as SiteServerInfo;

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h1 className="text-base font-semibold">{t('siteServerTitle')}</h1>
          <p className="mt-1 max-w-2xl text-xs text-ink-muted">{t('siteServerIntro')}</p>
        </div>
        <button className="btn btn-sm" onClick={load}>
          <RefreshCw className="h-3.5 w-3.5" />
          {t('refresh')}
        </button>
      </div>

      {/* مهم‌ترین نکته‌ای که کاربر باید بداند، نه پنهان در راهنما */}
      <Card>
        <div className="flex items-start gap-2.5">
          <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0" style={{ color: 'var(--status-warning)' }} />
          <div className="text-xs leading-relaxed text-ink-soft">
            <p className="mb-1 font-semibold text-ink">{t('lanOnlyTitle')}</p>
            <p>{t('lanOnlyBody')}</p>
            <p className="mt-1.5">{t('tunnelHint')}</p>
          </div>
        </div>
      </Card>

      <Card
        title={t('serverAddress')}
        icon={<Link2 className="h-4 w-4" />}
        action={<Badge tone="info">{t('whereToPut')}</Badge>}
      >
        <ul className="space-y-2">
          {data.addresses.map((a) => (
            <li
              key={a.ws}
              className="flex flex-wrap items-center gap-2 rounded-xl border border-line p-3"
              style={{ background: 'var(--surface-0)' }}
            >
              <div className="min-w-0 flex-1">
                <p className="text-[11px] text-ink-muted">{a.label}</p>
                <p className="truncate font-mono text-sm" dir="ltr">
                  {a.ws}
                </p>
              </div>
              <CopyButton value={a.ws} />
              <a className="btn btn-sm" href={`${a.http}/health`} target="_blank" rel="noreferrer">
                /health
              </a>
            </li>
          ))}
        </ul>
      </Card>

      <Card title={t('serverToken')} icon={<Eye className="h-4 w-4" />}>
        <div className="flex flex-wrap items-center gap-2">
          <code
            className="min-w-0 flex-1 truncate rounded-xl border border-line px-3 py-2.5 font-mono text-sm"
            style={{ background: 'var(--surface-0)' }}
            dir="ltr"
          >
            {token ?? data.tokenPreview ?? '—'}
          </code>
          {token ? (
            <button className="btn btn-sm" onClick={() => setToken(null)}>
              <EyeOff className="h-3.5 w-3.5" />
              {t('hideToken')}
            </button>
          ) : (
            <button
              className="btn btn-sm"
              onClick={async () => {
                try {
                  const res = await api<{ token: string }>('/api/site-server/token');
                  setToken(res.token);
                } catch {
                  toast(t('error'), 'bad');
                }
              }}
            >
              <Eye className="h-3.5 w-3.5" />
              {t('showToken')}
            </button>
          )}
          {token && <CopyButton value={token} />}
          <button className="btn btn-sm" onClick={() => setRotateOpen(true)}>
            <RotateCw className="h-3.5 w-3.5" />
            {t('rotateToken')}
          </button>
        </div>
        <p className="mt-3 flex items-start gap-1.5 text-[11px] text-ink-muted">
          <Info className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          {t('rotateWarn')}
        </p>
        {data.dedicatedPort && (
          <p className="mt-2 flex items-start gap-1.5 text-[11px] text-ink-muted">
            <Info className="mt-0.5 h-3.5 w-3.5 shrink-0" />
            {t('dedicatedPortNote', { port: data.dedicatedPort })}
          </p>
        )}
      </Card>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <Stat label={t('liveConnections')} value={String(data.stats.liveConnections)} />
        <Stat label={t('dataBranches')} value={String(data.stats.branches)} />
        <Stat label={t('reads')} value={String(data.stats.reads)} />
        <Stat label={t('writes')} value={String(data.stats.writes)} />
      </div>

      <Card title={t('dataBranches')} icon={<Database className="h-4 w-4" />}>
        {!data.branches.length ? (
          <Empty title={t('noData')} />
        ) : (
          <ul className="divide-y divide-line">
            {data.branches.map((b) => (
              <li key={b.key} className="flex items-center justify-between gap-3 py-2">
                <span className="truncate font-mono text-sm">{b.key}</span>
                <span className="tnum shrink-0 text-xs text-ink-soft">
                  {ltr(`${b.children} · ${bytes(b.bytes)}`)}
                </span>
              </li>
            ))}
          </ul>
        )}
        <p className="mt-3 truncate font-mono text-[11px] text-ink-muted" dir="ltr">
          {data.dataDir}
        </p>
      </Card>

      {data.stats.clients.length > 0 && (
        <Card title={t('liveConnections')} icon={<Plug className="h-4 w-4" />}>
          <ul className="divide-y divide-line">
            {data.stats.clients.map((c, i) => (
              <li key={i} className="flex items-center justify-between gap-3 py-2 text-xs">
                <span className="font-mono" dir="ltr">
                  {c.remote}
                </span>
                <span className="text-ink-soft">{dateTime(c.since, lang)}</span>
              </li>
            ))}
          </ul>
        </Card>
      )}

      <ConfirmDialog
        open={rotateOpen}
        danger
        title={t('rotateToken')}
        message={t('rotateWarn')}
        onCancel={() => setRotateOpen(false)}
        onConfirm={async () => {
          try {
            const res = await api<{ token: string }>('/api/site-server/rotate-token', { method: 'POST' });
            setToken(res.token);
            toast(t('saved'));
            load();
          } catch {
            toast(t('error'), 'bad');
          }
          setRotateOpen(false);
        }}
      />
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <Card>
      <p className="text-[11px] text-ink-muted">{label}</p>
      <p className="tnum mt-1 text-2xl font-semibold">{value}</p>
    </Card>
  );
}
