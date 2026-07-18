-- ساختار پایگاه‌دادهٔ سرور شخصی پمپ یعقوبی
-- این فایل یک‌بار هنگام نصب اجرا می‌شود (راهنما در README-fa.md).
--
-- کل «درخت» realtime (همان چیزی که فایربیس نگه می‌داشت) در حافظهٔ سرور است و
-- برای پایداری، هر شاخهٔ سطح‌اول (stations / chat / backups / ...) به‌صورت یک
-- ردیف JSONB این‌جا ذخیره می‌شود. با هر بار روشن شدن سرور، از این‌جا بازخوانی می‌شود.

CREATE TABLE IF NOT EXISTS fb_store (
  k          TEXT PRIMARY KEY,           -- کلید شاخهٔ سطح‌اول: 'stations' | 'chat' | 'backups' | 'backupsIndex' | ...
  v          JSONB NOT NULL,             -- کل محتوای آن شاخه به‌صورت JSON
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ایندکس زمان به‌روزرسانی (برای گزارش/نگهداری اختیاری)
CREATE INDEX IF NOT EXISTS fb_store_updated_idx ON fb_store (updated_at);
