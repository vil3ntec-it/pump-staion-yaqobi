// ---------------------------------------------------------------------------
// جمع‌آوری دوره‌ای معیارها + تاریخچهٔ کوتاه‌مدت (برای نمودارهای زنده)
// ---------------------------------------------------------------------------
import { readCpu, readMemory, readDisks, readTemperature, readHost } from './system.js';
import { readThroughput, readNetwork, readInterfaces, readPing, readGateway, readPublicIp } from './network.js';
import { config } from '../config.js';

const HISTORY = 180; // حدود ۶ دقیقه با بازهٔ ۲ ثانیه
const history = [];

let latest = null;

export async function collect() {
  const [memory, disk, temperature, throughput] = await Promise.all([
    readMemory(),
    readDisks(),
    readTemperature(),
    readThroughput(),
  ]);
  const cpu = readCpu();
  const host = readHost();

  const snapshot = {
    at: Date.now(),
    cpu,
    memory,
    disk,
    temperature,
    network: throughput,
    host,
  };

  latest = snapshot;
  history.push({
    at: snapshot.at,
    cpu: cpu.usage,
    memory: memory.usage,
    disk: disk.usage,
    rx: throughput.rxBytesPerSec,
    tx: throughput.txBytesPerSec,
    temp: temperature.max,
  });
  if (history.length > HISTORY) history.shift();

  return snapshot;
}

export function getLatest() {
  return latest;
}

export function getHistory() {
  return history.slice();
}

let timer = null;

export function startCollector(onTick) {
  if (timer) return;
  const tick = async () => {
    try {
      const snap = await collect();
      onTick?.(snap);
    } catch { /* یک چرخهٔ ناموفق نباید سرور را بخواباند */ }
  };
  tick();
  timer = setInterval(tick, config.metricsIntervalMs);
  timer.unref?.();
}

export function stopCollector() {
  if (timer) clearInterval(timer);
  timer = null;
}

export { readNetwork, readInterfaces, readPing, readGateway, readPublicIp };
