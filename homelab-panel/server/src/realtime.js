// ---------------------------------------------------------------------------
// اطلاعات لحظه‌ای با Socket.IO — نمودارها و لاگ‌ها بدون Refresh بروز می‌شوند
// ---------------------------------------------------------------------------
import { Server } from 'socket.io';
import { verifyToken } from './auth.js';
import { processEvents } from './sites/process.js';
import { getHistory } from './metrics/index.js';
import { listSites } from './sites/registry.js';
import { getSiteSync } from './state.js';

export function attachRealtime(httpServer) {
  const io = new Server(httpServer, {
    cors: { origin: true, credentials: true },
    serveClient: false,
  });

  io.use((socket, next) => {
    const token = socket.handshake.auth?.token || socket.handshake.query?.token;
    const user = token ? verifyToken(String(token)) : null;
    if (!user) return next(new Error('unauthorized'));
    socket.data.user = user;
    next();
  });

  io.on('connection', (socket) => {
    socket.emit('history', getHistory());

    socket.on('watch:site', (slug) => {
      if (typeof slug !== 'string') return;
      for (const room of socket.rooms) {
        if (room.startsWith('site:')) socket.leave(room);
      }
      socket.join(`site:${slug}`);
    });

    socket.on('unwatch:site', (slug) => {
      if (typeof slug === 'string') socket.leave(`site:${slug}`);
    });
  });

  // لاگ زندهٔ هر سایت فقط به کسانی که همان سایت را باز کرده‌اند
  processEvents.on('log', (entry) => {
    io.to(`site:${entry.slug}`).emit('site:log', entry);
  });

  processEvents.on('status', async (entry) => {
    io.emit('site:status', entry);
  });

  return io;
}

// در هر چرخهٔ جمع‌آوری معیارها فراخوانی می‌شود
export async function broadcastMetrics(io, snapshot) {
  if (!io) return;
  io.emit('metrics', snapshot);

  // خلاصهٔ سبک از سایت‌ها و سرور سایت (هر ۵ چرخه یک‌بار کافی است)
  broadcastMetrics.tick = (broadcastMetrics.tick || 0) + 1;
  if (broadcastMetrics.tick % 5 === 0) {
    try {
      const sites = await listSites({ withSize: false });
      const sync = getSiteSync();
      io.emit('sites', {
        sites,
        siteSync: sync ? sync.snapshot() : null,
      });
    } catch { /* یک چرخهٔ ناموفق مهم نیست */ }
  }
}
