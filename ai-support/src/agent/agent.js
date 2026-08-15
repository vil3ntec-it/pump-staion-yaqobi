// ═══════════════════════════════════════════════════════════════════════════
//  عاملِ پشتیبانی — حلقهٔ اصلی
//
//  ترتیبِ کار عمداً «RAG اول، ابزار بعد» است:
//
//     بازیابی → مدل → (اگر لازم شد) ابزار → مدل → جواب
//
//  چرا نه «اول از مدل بپرس چه ابزاری لازم است»: آن روش برای هر سؤال دستِ‌کم دو
//  فراخوانیِ مدل لازم دارد. با بازیابیِ اولیه — که ۱ میلی‌ثانیه است — اکثر
//  سؤال‌ها با **یک** فراخوانی تمام می‌شوند. روی کامپیوترِ خانگی این یعنی نصف
//  شدنِ زمان و نصف شدنِ مصرف.
//
//  و اگر مدل اصلاً در دسترس نباشد یا سیستم شلوغ باشد، همان بازیابیِ اولیه به
//  پاسخِ استخراجی تبدیل می‌شود. یعنی هیچ مسیری به «جواب ندادن» ختم نمی‌شود.
// ═══════════════════════════════════════════════════════════════════════════

import config from '../config.js';
import { getProvider } from '../llm/provider.js';
import { llmPool, OverloadedError } from '../llm/pool.js';
import { answerCache } from '../llm/cache.js';
import { packContext, Retriever } from '../rag/retriever.js';
import { systemMessage, contextMessage } from './prompt.js';
import { callTool } from './tools/router.js';
import { toolSchemasFor } from './tools/registry.js';
import { inspectUserInput, sanitizeUntrusted } from '../security/injection.js';
import { redactText } from '../security/redact.js';
import { audit } from '../security/audit.js';

const NO_ANSWER = 'اطلاعات کافی برای پاسخ دقیق به این سؤال پیدا نکردم.';

export class SupportAgent {
  /**
   * @param {{retriever: Retriever, index: object, appVersion?: string}} deps
   */
  constructor({ retriever, index, appVersion = '' }) {
    this.retriever = retriever;
    this.index = index;
    this.appVersion = appVersion;
    this.kbVersion = index?.builtAt || '0';
  }

  /**
   * @param {{question: string, level: string, user: string, history?: object[], memory?: object[], signal?: AbortSignal}} req
   */
  async answer({ question, level = 'PUBLIC', user = 'anon', history = [], memory = [], signal }) {
    const started = Date.now();
    const cleanQuestion = redactText(String(question || '').trim());
    if (!cleanQuestion) {
      return this._reply({ text: 'سؤالی ننوشتید. چه چیزی دربارهٔ برنامه می‌خواهید بدانید؟', mode: 'empty', started });
    }

    const inj = inspectUserInput(cleanQuestion);
    if (inj.flagged) {
      audit({ kind: 'injection', user, where: 'user_input', matches: inj.matches });
    }

    // ── کش ───────────────────────────────────────────────────────────────
    const cached = answerCache.get(cleanQuestion, level, this.kbVersion);
    if (cached) return { ...cached, cached: true, ms: Date.now() - started };

    const provider = getProvider();
    const modelUp = await provider.available();

    // ── بازیابیِ اولیه ───────────────────────────────────────────────────
    let { results } = await this.retriever.search(cleanQuestion, { level, signal });

    // ── اگر مدل نیست: پاسخِ استخراجی ─────────────────────────────────────
    if (!modelUp) {
      return this._extractive(results, cleanQuestion, {
        started, level,
        why: 'مدلِ زبانی در دسترس نیست (سرور خاموش است یا Ollama بالا نیامده).',
      });
    }

    // ── مدل هست: تولید با کنترلِ بار ─────────────────────────────────────
    try {
      const out = await llmPool.run(() => this._generate({
        question: cleanQuestion, results, level, user, history, memory, signal, flagged: inj.flagged,
      }));

      // مدل دادهٔ دستگاه خواست → جوابِ نهایی نیست، درخواستِ داده است
      if (out.needsClient) {
        return {
          mode: 'need-client-data',
          needsClient: out.needsClient,
          state: { messages: out.messages, level, user },
          sources: [],
          ms: Date.now() - started,
        };
      }

      const reply = this._reply({ ...out, started, level });
      // ⚠️ جوابی که روی دادهٔ زندهٔ دستگاه ساخته شده **کش نمی‌شود**: هم مالِ همان
      //    شخص است و به دیگری ربطی ندارد، هم فردا عددش عوض می‌شود.
      if (reply.text && reply.text !== NO_ANSWER && !out.usedClientData) {
        answerCache.set(cleanQuestion, level, this.kbVersion, { ...reply, ms: undefined });
      }
      return reply;

    } catch (e) {
      if (e instanceof OverloadedError || e.degrade) {
        audit({ kind: 'degraded', user, reason: e.message });
        return this._extractive(results, cleanQuestion, {
          started, level,
          why: `${e.message} — برای اینکه کامپیوتر کند نشود، جواب را مستقیم از مستندات می‌دهم.`,
        });
      }
      if (e.code === 'NO_MODEL' || e.code === 'MODEL_MISSING') {
        return this._extractive(results, cleanQuestion, { started, level, why: 'مدلِ تنظیم‌شده روی سرور نصب نیست.' });
      }
      audit({ kind: 'error', user, where: 'generate', msg: e.message });
      return this._extractive(results, cleanQuestion, { started, level, why: 'در تولیدِ پاسخ خطایی پیش آمد.' });
    }
  }

  // ───────────────────────────────────────────────────────────────────────
  //  تولید با مدل
  // ───────────────────────────────────────────────────────────────────────
  async _generate({ question, results, level, user, history, memory, signal, flagged }) {
    const provider = getProvider();
    // ابزار فقط وقتی به مدل داده می‌شود که سؤال واقعاً دربارهٔ **داده** باشد.
    // روی CPU، هر دورِ ابزار ده‌ها ثانیه است؛ برای «چطور پی‌دی‌اف بگیرم؟» که
    // جوابش در متنِ بازیابی‌شده هست، دادنِ ابزار فقط وقت تلف کردن است.
    const wantsData = needsDataTools(question);
    const tools = wantsData ? toolSchemasFor(level, config.tools.whitelist) : [];
    // سؤالِ داده‌ای دستِ‌کم یک دورِ ابزار لازم دارد، حتی اگر پیکربندی صفر باشد.
    // (پیش‌فرضِ صفر برای سرعتِ سؤال‌های مستنداتی است، نه برای بستنِ ابزار.)
    const maxSteps = wantsData ? Math.max(1, config.agent.maxSteps) : 0;
    const usedTools = [];
    let sources = results;

    /* بسطِ پرسش با مدل حذف شد.
       قبلاً وقتی بازیابیِ اول ضعیف بود، یک فراخوانیِ اضافه به مدل می‌رفت تا
       عبارت‌های جست‌وجوی بهتری پیشنهاد دهد. روی کامپیوترِ بدونِ کارتِ گرافیک
       همان یک فراخوانی ۲۰ تا ۴۰ ثانیه بود — یعنی برای سؤالی که جوابش پیدا
       نشده، دو برابر انتظار و آخرش همان «نمی‌دانم». مترادف‌هایی که در سربرگِ
       اسناد نوشته شده‌اند همان کار را رایگان انجام می‌دهند. */

    const messages = [
      systemMessage({ level, appVersion: this.appVersion, memory, flagged }),
      ...history.slice(-config.agent.historyTurns),
      contextMessage(packContext(results)),
      { role: 'user', content: question },
    ];

    let final = null;

    // سقفِ دقیقِ فراخوانیِ مدل: maxSteps + 1. دورِ آخر عمداً **بدونِ ابزار**
    // اجرا می‌شود تا مدل مجبور شود جوابِ نهایی بنویسد — وگرنه یک مدلِ کوچک
    // می‌تواند بی‌نهایت ابزار صدا بزند و سرور را مشغول نگه دارد.
    for (let step = 0; step <= maxSteps; step++) {
      const lastRound = step === maxSteps;
      const out = await provider.chat({ messages, tools: lastRound ? [] : tools, signal });

      if (lastRound || !out.toolCalls?.length) { final = out; break; }

      messages.push({
        role: 'assistant',
        content: out.text || '',
        tool_calls: out.toolCalls.map(t => ({ function: { name: t.name, arguments: t.args } })),
      });

      const deferred = [];

      for (const call of out.toolCalls.slice(0, 3)) {
        const res = await callTool(call.name, call.args, {
          level, user, index: this.index, retriever: this.retriever,
        });

        // ابزارِ سمتِ دستگاه — سرور جوابش را ندارد و نباید داشته باشد
        if (res.deferred) {
          deferred.push({ name: res.tool, args: res.args });
          continue;
        }

        usedTools.push({ name: call.name, ok: res.ok });

        if (res.ok && Array.isArray(res.result)) {
          sources = mergeSources(sources, res.result);
        }
        messages.push({
          role: 'tool',
          name: call.name,
          content: JSON.stringify(res.ok ? res.result : { error: res.error }).slice(0, 6000),
        });
      }

      // اگر مدل دادهٔ دستگاه خواست، همین‌جا می‌ایستیم و توپ را به مرورگر می‌دهیم.
      // گفت‌وگو نیمه‌کاره نگه داشته می‌شود تا با رسیدنِ نتیجه ادامه پیدا کند.
      if (deferred.length) {
        return { needsClient: deferred, messages, sources, usedTools };
      }
    }

    return {
      text: (final?.text || '').trim() || NO_ANSWER,
      mode: 'model',
      model: provider.name,
      usedTools,
      sources,
      usage: final?.usage,
    };
  }

  /**
   * پاسخِ **پخشِ زنده** — همان منطقِ answer، ولی متن تکه‌تکه بیرون می‌آید.
   *
   * عمداً ساده‌تر از answer است: ابزار ندارد و فقط یک بار مدل را صدا می‌زند.
   * دلیلش سرعت است — روی CPU هر دورِ اضافه ده‌ها ثانیه می‌شود، و کاربر برای
   * سؤالِ مستنداتی به ابزار نیازی ندارد چون متنِ لازم از قبل بازیابی شده.
   * سؤال‌های داده‌ای (الباقی، مخزن) خودشان به مسیرِ answer می‌روند.
   *
   * @yields {{type:'delta'|'done'|'meta', ...}}
   */
  async *answerStream({ question, level = 'PUBLIC', user = 'anon', history = [], memory = [], signal }) {
    const started = Date.now();
    const cleanQuestion = redactText(String(question || '').trim());
    if (!cleanQuestion) {
      yield { type: 'done', text: 'سؤالی ننوشتید.', mode: 'empty', sources: [], ms: 0 };
      return;
    }

    const inj = inspectUserInput(cleanQuestion);
    if (inj.flagged) audit({ kind: 'injection', user, where: 'user_input', matches: inj.matches });

    // کش: جوابِ آماده را یک‌جا می‌فرستیم (بدونِ انتظار)
    const cached = answerCache.get(cleanQuestion, level, this.kbVersion);
    if (cached) {
      yield { type: 'done', ...cached, cached: true, ms: Date.now() - started };
      return;
    }

    const provider = getProvider();
    const { results } = await this.retriever.search(cleanQuestion, { level, signal });

    // منابع را زودتر می‌فرستیم تا ویجت بتواند «دارم از این سند می‌خوانم» را نشان دهد
    yield { type: 'meta', sources: results.map(r => ({ title: r.title, section: r.heading, version: r.version })) };

    const modelUp = typeof provider.chatStream === 'function' && await provider.available();
    if (!modelUp) {
      const out = this._extractive(results, cleanQuestion, {
        started, level,
        why: 'مدلِ زبانی در دسترس نیست.',
      });
      yield { type: 'done', ...out };
      return;
    }

    const messages = [
      systemMessage({ level, appVersion: this.appVersion, memory, flagged: inj.flagged }),
      ...history.slice(-config.agent.historyTurns),
      contextMessage(packContext(results)),
      { role: 'user', content: cleanQuestion },
    ];

    let text = '';
    let release = null;
    try {
      // نوبت را دستی می‌گیریم چون از داخلِ callback نمی‌شود yield کرد
      release = await llmPool.acquire();
      for await (const piece of provider.chatStream({ messages, signal })) {
        text += piece;
        yield { type: 'delta', text: piece };
      }
    } catch (e) {
      if (e instanceof OverloadedError || e.degrade || e.code === 'NO_MODEL' || e.code === 'MODEL_MISSING') {
        const out = this._extractive(results, cleanQuestion, { started, level, why: e.message });
        yield { type: 'done', ...out };
        return;
      }
      audit({ kind: 'error', user, where: 'stream', msg: e.message });
      const out = this._extractive(results, cleanQuestion, { started, level, why: 'در تولیدِ پاسخ خطایی پیش آمد.' });
      yield { type: 'done', ...out };
      return;
    } finally {
      // اگر کاربر وسطِ کار تب را ببندد، این‌جا هم اجرا می‌شود و نوبت آزاد می‌شود.
      // بدونِ این، یک قطعِ ناگهانی صف را برای همیشه قفل می‌کرد.
      release?.();
    }

    const final = this._reply({
      text: text.trim() || NO_ANSWER,
      mode: 'model', model: provider.name, sources: results, started, level,
    });
    if (final.text && final.text !== NO_ANSWER) {
      answerCache.set(cleanQuestion, level, this.kbVersion, { ...final, ms: undefined });
    }
    yield { type: 'done', ...final };
  }

  /**
   * ادامهٔ گفت‌وگو بعد از اینکه مرورگر اعدادِ خواسته‌شده را حساب کرد و فرستاد.
   *
   * @param {{state: object, toolResults: object[], signal?: AbortSignal}} req
   */
  async continueWithClientData({ state, toolResults, signal }) {
    const started = Date.now();
    const messages = Array.isArray(state?.messages) ? [...state.messages] : [];
    const level = state?.level || 'PUBLIC';

    if (!messages.length) {
      return this._reply({ text: NO_ANSWER, mode: 'model', started, level });
    }

    for (const r of (toolResults || []).slice(0, 4)) {
      // نتیجه از مرورگر آمده، پس **نامعتمد** است: هم پاکسازیِ تزریق می‌خورد هم
      // redact. اگر عددی که برگشته کنارش متنِ دستوری داشته باشد، بی‌اثر می‌شود.
      const raw = JSON.stringify(r?.result ?? { error: 'نتیجه‌ای نیامد' }).slice(0, 4000);
      messages.push({
        role: 'tool',
        name: String(r?.name || 'client_tool').slice(0, 64),
        content: sanitizeUntrusted(redactText(raw)).text,
      });
    }

    messages.push({
      role: 'user',
      content: 'حالا با همین اعدادِ واقعی، جوابِ کوتاه و روشن به سؤالم بده. عددها را همان‌طور که آمده بگو و از خودت عددی نساز.',
    });

    try {
      const provider = getProvider();
      const out = await llmPool.run(() => provider.chat({ messages, tools: [], signal }));
      return this._reply({
        text: (out.text || '').trim() || NO_ANSWER,
        mode: 'model',
        model: provider.name,
        usedTools: (toolResults || []).map(r => ({ name: r?.name, ok: true })),
        usedClientData: true,
        usage: out.usage,
        started, level,
      });
    } catch (e) {
      if (e instanceof OverloadedError || e.degrade) {
        // مدل نتوانست جمله‌بندی کند — عددها که هست، خامش را نشان بده
        return this._reply({
          text: 'الان نتوانستم جمله‌بندی کنم، ولی این چیزی است که از حسابِ خودت درآمد:\n\n' + rawClientText(toolResults),
          mode: 'extractive', degraded: true, usedClientData: true, started, level,
        });
      }
      audit({ kind: 'error', where: 'continue', msg: e.message });
      return this._reply({
        text: 'در تولیدِ پاسخ خطایی پیش آمد، ولی عددها این‌هاست:\n\n' + rawClientText(toolResults),
        mode: 'extractive', degraded: true, usedClientData: true, started, level,
      });
    }
  }

  // ───────────────────────────────────────────────────────────────────────
  //  پاسخِ استخراجی — بدونِ مدل
  //
  //  این «حالتِ خرابی» نیست، یک حالتِ درجهٔ دومِ محترم است: متنِ واقعیِ سند را
  //  نشان می‌دهد و صادقانه می‌گوید که خودش ننوشته. برای «میانبرها چیست» عملاً
  //  همان‌قدر خوب است که جوابِ مدل.
  // ───────────────────────────────────────────────────────────────────────
  _extractive(results, question, { started, level, why }) {
    if (!results.length) {
      return this._reply({
        text: `${NO_ANSWER}\n\n(${why})`,
        mode: 'extractive', sources: [], started, level, degraded: true,
      });
    }

    const confident = Retriever.isConfident(results);
    const top = results.slice(0, confident ? 2 : 3);
    const body = top.map(r => {
      const head = r.heading ? `**${r.heading}**\n` : '';
      return `${head}${r.text.length > 700 ? r.text.slice(0, 700) + '…' : r.text}`;
    }).join('\n\n---\n\n');

    const preface = confident
      ? 'این را در مستندات پیدا کردم:'
      : 'جوابِ قطعی ندارم، ولی این بخش‌ها به سؤالتان نزدیک‌اند:';

    return this._reply({
      text: `${preface}\n\n${body}\n\n_(${why})_`,
      mode: 'extractive', sources: top, started, level, degraded: true,
    });
  }

  _reply({ text, mode, sources = [], model, usedTools = [], usage, started, level, degraded = false, usedClientData = false }) {
    return {
      text: redactText(text),
      mode,
      degraded,
      usedClientData,
      model: model || null,
      usedTools,
      // منابع همیشه برمی‌گردند تا کاربر بتواند خودش راستی‌آزمایی کند
      sources: sources.map(s => ({
        title: s.title,
        section: s.heading || s.section || '',
        version: s.version || '',
        updated: s.updated || '',
      })),
      usage,
      level,
      ms: Date.now() - started,
    };
  }
}

/**
 * آیا این سؤال به اعدادِ زندهٔ برنامه نیاز دارد؟
 *
 * تشخیصِ کلیدواژه‌ای عمدی است و «هوشمند» نیست: تنها کارش این است که تصمیم
 * بگیرد آیا ارزشِ **یک دورِ اضافهٔ مدل** را دارد یا نه. اگر اشتباه بگیرد،
 * بدترین اتفاق این است که مدل بدونِ ابزار جواب می‌دهد و می‌گوید نمی‌دانم —
 * نه اینکه چیزی خراب شود.
 */
function needsDataTools(q) {
  const t = String(q || '');
  return /الباقی|باقی|بردگی|رسید|قرض|حساب|مخزن|تیل|لیتر|موجودی|مصارف|مصرف|چند|چقدر|جمع|فیصدی|پیدا کن|بگرد/.test(t);
}

/** وقتی مدل نتوانست جمله بسازد، خودِ عددها را خوانا نشان بده — بهتر از هیچ */
function rawClientText(toolResults) {
  return (toolResults || []).map(r => {
    const v = r?.result;
    if (v && typeof v === 'object' && typeof v.text === 'string') return v.text;
    try { return JSON.stringify(v, null, 1); } catch { return ''; }
  }).filter(Boolean).join('\n\n') || 'چیزی پیدا نشد.';
}

/** منابعِ ابزار را به منابعِ بازیابی اضافه می‌کند، بدونِ تکرار */
function mergeSources(base, toolResults) {
  const out = [...base];
  const seen = new Set(base.map(s => `${s.title}|${s.heading || ''}`));
  for (const r of toolResults) {
    if (!r?.title) continue;
    const key = `${r.title}|${r.section || ''}`;
    if (seen.has(key)) continue;
    seen.add(key);
    out.push({ title: r.title, heading: r.section, version: r.version, updated: r.updated });
  }
  return out;
}

export { NO_ANSWER };
