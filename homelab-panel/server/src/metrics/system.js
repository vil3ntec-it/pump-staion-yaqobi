// ---------------------------------------------------------------------------
// خواندن وضعیت واقعی سیستم: CPU، RAM، دیسک، دما، مدت روشن بودن
// همه‌چیز از خودِ سیستم‌عامل خوانده می‌شود — هیچ داده‌ی نمونه‌ای در کار نیست.
// ---------------------------------------------------------------------------
import os from 'node:os';
import fs from 'node:fs';
import fsp from 'node:fs/promises';
import { run, powershell } from '../lib/exec.js';

const isLinux = process.platform === 'linux';
const isWin = process.platform === 'win32';
const isMac = process.platform === 'darwin';

// ---------------------------------------------------------------------------
// CPU — از اختلاف زمان‌های تجمعی هسته‌ها
// ---------------------------------------------------------------------------
function cpuSnapshot() {
  return os.cpus().map((c) => {
    const t = c.times;
    const idle = t.idle;
    const total = t.user + t.nice + t.sys + t.irq + t.idle;
    return { idle, total };
  });
}

let prevCpu = cpuSnapshot();
let prevCpuAt = Date.now();

export function readCpu() {
  const now = cpuSnapshot();
  const cores = [];
  let idleDelta = 0;
  let totalDelta = 0;

  for (let i = 0; i < now.length; i++) {
    const p = prevCpu[i] || now[i];
    const dIdle = now[i].idle - p.idle;
    const dTotal = now[i].total - p.total;
    idleDelta += dIdle;
    totalDelta += dTotal;
    cores.push(dTotal > 0 ? Math.min(100, Math.max(0, ((dTotal - dIdle) / dTotal) * 100)) : 0);
  }

  prevCpu = now;
  prevCpuAt = Date.now();

  const usage = totalDelta > 0 ? ((totalDelta - idleDelta) / totalDelta) * 100 : 0;
  const list = os.cpus();

  return {
    usage: Math.round(Math.min(100, Math.max(0, usage)) * 10) / 10,
    cores: cores.map((c) => Math.round(c * 10) / 10),
    count: list.length,
    model: list[0]?.model?.trim() || null,
    speedMhz: list[0]?.speed || null,
    loadAvg: os.loadavg().map((n) => Math.round(n * 100) / 100),
  };
}

// ---------------------------------------------------------------------------
// حافظه (RAM)
// ---------------------------------------------------------------------------
export async function readMemory() {
  const total = os.totalmem();
  let free = os.freemem();
  let available = free;
  let swapTotal = null;
  let swapUsed = null;

  if (isLinux) {
    try {
      const txt = await fsp.readFile('/proc/meminfo', 'utf8');
      const kv = {};
      for (const line of txt.split('\n')) {
        const m = line.match(/^(\w+):\s+(\d+)\s*kB/);
        if (m) kv[m[1]] = Number(m[2]) * 1024;
      }
      if (kv.MemAvailable != null) available = kv.MemAvailable;
      if (kv.MemFree != null) free = kv.MemFree;
      if (kv.SwapTotal != null) {
        swapTotal = kv.SwapTotal;
        swapUsed = kv.SwapTotal - (kv.SwapFree || 0);
      }
    } catch { /* از مقادیر os استفاده می‌کنیم */ }
  }

  const used = total - available;
  return {
    total,
    free,
    available,
    used,
    usage: total > 0 ? Math.round((used / total) * 1000) / 10 : 0,
    swapTotal,
    swapUsed,
  };
}

// ---------------------------------------------------------------------------
// دیسک‌ها
// ---------------------------------------------------------------------------
async function linuxMounts() {
  const out = [];
  try {
    const txt = await fsp.readFile('/proc/mounts', 'utf8');
    const seen = new Set();
    for (const line of txt.split('\n')) {
      const [dev, mount, type] = line.split(/\s+/);
      if (!dev || !mount) continue;
      if (!dev.startsWith('/dev/')) continue;
      if (['squashfs', 'iso9660'].includes(type)) continue;
      const mp = mount.replace(/\\040/g, ' ');
      if (seen.has(mp)) continue;
      seen.add(mp);
      out.push({ device: dev, mount: mp, fs: type });
    }
  } catch { /* بی‌خیال */ }
  if (!out.length) out.push({ device: null, mount: '/', fs: null });
  return out;
}

async function statfsInfo(mount) {
  try {
    const s = await fsp.statfs(mount);
    const total = s.blocks * s.bsize;
    const freeForUser = s.bavail * s.bsize;
    const used = total - s.bfree * s.bsize;
    if (!total) return null;
    return {
      total,
      free: freeForUser,
      used,
      usage: Math.round((used / total) * 1000) / 10,
    };
  } catch {
    return null;
  }
}

export async function readDisks() {
  const disks = [];

  if (isWin) {
    const r = await powershell(
      "Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' | Select-Object DeviceID,Size,FreeSpace,VolumeName,FileSystem | ConvertTo-Json -Compress"
    );
    if (r.ok && r.stdout.trim()) {
      try {
        let parsed = JSON.parse(r.stdout);
        if (!Array.isArray(parsed)) parsed = [parsed];
        for (const d of parsed) {
          const total = Number(d.Size || 0);
          const free = Number(d.FreeSpace || 0);
          if (!total) continue;
          disks.push({
            mount: d.DeviceID,
            device: d.DeviceID,
            fs: d.FileSystem || null,
            label: d.VolumeName || null,
            total,
            free,
            used: total - free,
            usage: Math.round(((total - free) / total) * 1000) / 10,
          });
        }
      } catch { /* پارس نشد */ }
    }
  } else {
    const mounts = isLinux ? await linuxMounts() : [{ device: null, mount: '/', fs: null }];
    if (isMac) {
      const r = await run('df', ['-kP']);
      if (r.ok) {
        for (const line of r.stdout.split('\n').slice(1)) {
          const parts = line.trim().split(/\s+/);
          if (parts.length < 6) continue;
          const mount = parts.slice(5).join(' ');
          if (!mount.startsWith('/')) continue;
          if (!parts[0].startsWith('/dev/')) continue;
          const total = Number(parts[1]) * 1024;
          const used = Number(parts[2]) * 1024;
          const free = Number(parts[3]) * 1024;
          if (!total) continue;
          disks.push({
            mount,
            device: parts[0],
            fs: null,
            label: null,
            total,
            free,
            used,
            usage: Math.round((used / total) * 1000) / 10,
          });
        }
      }
    } else {
      for (const m of mounts) {
        const info = await statfsInfo(m.mount);
        if (!info) continue;
        disks.push({ mount: m.mount, device: m.device, fs: m.fs, label: null, ...info });
      }
    }
  }

  // حذف تکراری‌ها (bind mount ها) بر اساس اندازه + فضای آزاد + دستگاه
  const uniq = [];
  const seen = new Set();
  for (const d of disks) {
    const key = `${d.device || ''}|${d.total}|${d.free}`;
    if (seen.has(key)) continue;
    seen.add(key);
    uniq.push(d);
  }
  uniq.sort((a, b) => b.total - a.total);

  const totals = uniq.reduce(
    (acc, d) => ({ total: acc.total + d.total, used: acc.used + d.used, free: acc.free + d.free }),
    { total: 0, used: 0, free: 0 }
  );

  return {
    disks: uniq,
    total: totals.total,
    used: totals.used,
    free: totals.free,
    usage: totals.total > 0 ? Math.round((totals.used / totals.total) * 1000) / 10 : 0,
  };
}

// ---------------------------------------------------------------------------
// دما — اگر سیستم پشتیبانی نکند null برمی‌گردد (چیزی از خودمان نمی‌سازیم)
// ---------------------------------------------------------------------------
async function linuxTemps() {
  const out = [];
  // thermal zones
  try {
    for (const entry of await fsp.readdir('/sys/class/thermal')) {
      if (!entry.startsWith('thermal_zone')) continue;
      try {
        const raw = await fsp.readFile(`/sys/class/thermal/${entry}/temp`, 'utf8');
        const value = Number(raw.trim()) / 1000;
        if (!Number.isFinite(value) || value <= 0 || value > 150) continue;
        let label = entry;
        try {
          label = (await fsp.readFile(`/sys/class/thermal/${entry}/type`, 'utf8')).trim() || entry;
        } catch { /* بدون برچسب */ }
        out.push({ label, celsius: Math.round(value * 10) / 10 });
      } catch { /* این zone خوانده نشد */ }
    }
  } catch { /* thermal وجود ندارد */ }

  // hwmon (بیشتر روی سرورها و لپ‌تاپ‌ها)
  if (!out.length) {
    try {
      for (const dir of await fsp.readdir('/sys/class/hwmon')) {
        const base = `/sys/class/hwmon/${dir}`;
        let chip = dir;
        try {
          chip = (await fsp.readFile(`${base}/name`, 'utf8')).trim() || dir;
        } catch { /* بدون نام */ }
        let files = [];
        try {
          files = await fsp.readdir(base);
        } catch { continue; }
        for (const f of files) {
          if (!/^temp\d+_input$/.test(f)) continue;
          try {
            const value = Number((await fsp.readFile(`${base}/${f}`, 'utf8')).trim()) / 1000;
            if (!Number.isFinite(value) || value <= 0 || value > 150) continue;
            let label = `${chip}/${f.replace('_input', '')}`;
            try {
              const l = await fsp.readFile(`${base}/${f.replace('_input', '_label')}`, 'utf8');
              if (l.trim()) label = `${chip} — ${l.trim()}`;
            } catch { /* بدون برچسب */ }
            out.push({ label, celsius: Math.round(value * 10) / 10 });
          } catch { /* رد شو */ }
        }
      }
    } catch { /* hwmon وجود ندارد */ }
  }
  return out;
}

let tempCache = { at: 0, value: null };

export async function readTemperature() {
  // دما کند خوانده می‌شود؛ هر ۵ ثانیه کافی است
  if (Date.now() - tempCache.at < 5000) return tempCache.value;

  let sensors = [];
  if (isLinux) {
    sensors = await linuxTemps();
  } else if (isWin) {
    const r = await powershell(
      "Get-CimInstance -Namespace root/wmi -ClassName MSAcpi_ThermalZoneTemperature -ErrorAction SilentlyContinue | Select-Object InstanceName,CurrentTemperature | ConvertTo-Json -Compress"
    );
    if (r.ok && r.stdout.trim()) {
      try {
        let parsed = JSON.parse(r.stdout);
        if (!Array.isArray(parsed)) parsed = [parsed];
        for (const z of parsed) {
          // مقدار به دهم کلوین است
          const celsius = Number(z.CurrentTemperature) / 10 - 273.15;
          if (!Number.isFinite(celsius) || celsius <= 0 || celsius > 150) continue;
          sensors.push({ label: String(z.InstanceName || 'ThermalZone'), celsius: Math.round(celsius * 10) / 10 });
        }
      } catch { /* پارس نشد */ }
    }
  }

  const value = sensors.length
    ? {
        supported: true,
        sensors,
        max: Math.max(...sensors.map((s) => s.celsius)),
      }
    : { supported: false, sensors: [], max: null };

  tempCache = { at: Date.now(), value };
  return value;
}

// ---------------------------------------------------------------------------
// اطلاعات میزبان
// ---------------------------------------------------------------------------
export function readHost() {
  return {
    hostname: os.hostname(),
    platform: process.platform,
    release: os.release(),
    type: os.type(),
    arch: os.arch(),
    uptimeSeconds: Math.floor(os.uptime()),
    processUptimeSeconds: Math.floor(process.uptime()),
    nodeVersion: process.version,
    bootTime: Date.now() - Math.floor(os.uptime()) * 1000,
  };
}

// اندازهٔ پوشه (واقعی) — برای «حجم فایل‌های سایت»
export async function dirSize(dir, { maxEntries = 200000 } = {}) {
  let total = 0;
  let files = 0;
  let newest = 0;
  const stack = [dir];
  while (stack.length) {
    const cur = stack.pop();
    let entries;
    try {
      entries = await fsp.readdir(cur, { withFileTypes: true });
    } catch {
      continue;
    }
    for (const e of entries) {
      if (files > maxEntries) return { bytes: total, files, newest, truncated: true };
      const full = `${cur}/${e.name}`;
      if (e.isDirectory()) {
        if (e.name === 'node_modules' || e.name === '.git') {
          // این پوشه‌ها هم شمرده می‌شوند ولی سریع‌تر: فقط اندازه، بدون پیمایش عمیق نمادها
        }
        stack.push(full);
      } else if (e.isFile()) {
        try {
          const st = await fsp.stat(full);
          total += st.size;
          files++;
          if (st.mtimeMs > newest) newest = st.mtimeMs;
        } catch { /* فایل ناپدید شد */ }
      }
    }
  }
  return { bytes: total, files, newest, truncated: false };
}

export function pathExists(p) {
  try {
    fs.accessSync(p);
    return true;
  } catch {
    return false;
  }
}
