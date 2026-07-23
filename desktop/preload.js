// پوستهٔ دسکتاپ به دسترسیِ خاصی نیاز ندارد — برنامه کاملاً داخل صفحه اجرا می‌شود.
// این فایل عمداً خالی/کمینه است تا سطحِ امنیتی کوچک بماند (contextIsolation روشن).
const { contextBridge } = require('electron');
try {
  contextBridge.exposeInMainWorld('pumpDesktop', { isDesktop: true, platform: process.platform });
} catch (e) { /* بی‌خیال */ }
