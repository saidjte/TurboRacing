/**
 * Unity Personal License Robot (GameCI-compatible).
 * Flow: login on Unity web -> call genesis activation API from page context -> save .ulf
 * Leaves debug screenshots at every step for diagnosis.
 */
const puppeteer = require('puppeteer');
const fs = require('fs');

const EMAIL = process.env.UNITY_EMAIL;
const PASSWORD = process.env.UNITY_PASSWORD;
const ALF_PATH = process.env.ALF_PATH || '.github/alf/Unity_v6000.0.84f1.alf';
const ULF_OUT = process.env.ULF_OUT || 'Unity_v6000.0.84f1.ulf';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function describe(page, name) {
  try {
    const url = page.url();
    await page.screenshot({ path: `shot_${name}.png`, fullPage: true });
    const info = await page.evaluate(() => {
      const els = [...document.querySelectorAll('input, button, a[href], form')]
        .slice(0, 80)
        .map((e) => ({
          tag: e.tagName,
          type: e.type || null,
          id: e.id || null,
          name: e.name || null,
          action: e.action || null,
          placeholder: e.placeholder || null,
          text: (e.innerText || e.value || '').slice(0, 60).replace(/\n/g, ' '),
          visible: !!(e.offsetParent || e.getClientRects().length),
        }));
      return { title: document.title, els };
    });
    console.log(`\n=== STEP ${name} | URL: ${url} | TITLE: ${info.title}`);
    for (const e of info.els) console.log(JSON.stringify(e));
  } catch (e) {
    console.log(`describe(${name}) failed: ${e.message}`);
  }
}

async function findInput(page, candidates, timeoutEach = 5000) {
  for (const sel of candidates) {
    try {
      const el = await page.waitForSelector(sel, { visible: true, timeout: timeoutEach });
      if (el) { console.log('found input:', sel); return el; }
    } catch { /* try next */ }
  }
  return null;
}

async function clickSubmit(page) {
  const clicked = await page.evaluate(() => {
    const btns = [...document.querySelectorAll('button, input[type=submit], a[role=button]')];
    for (const b of btns) {
      const t = (b.innerText || b.value || '').toLowerCase();
      if (/(continue|next|sign ?in|log ?in|submit)/.test(t) &&
          (b.offsetParent || b.getClientRects().length)) {
        b.click();
        return b.innerText || b.value;
      }
    }
    const f = document.querySelector('form');
    if (f) { f.submit(); return 'form.submit()'; }
    return null;
  });
  console.log('clicked submit:', clicked);
  if (!clicked) throw new Error('no submit button found');
}

(async () => {
  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-blink-features=AutomationControlled'],
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1280, height: 900 });
  page.setDefaultTimeout(60000);
  await page.setUserAgent(
    'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36');

  try {
    console.log('=== opening license.unity3d.com/manual');
    await page.goto('https://license.unity3d.com/manual', { waitUntil: 'networkidle2', timeout: 90000 });
    await sleep(3000);
    await describe(page, '0_landing');

    // ---- LOGIN (email-first flow aware) ----
    const emailEl = await findInput(page, [
      'input[type=email]',
      'input[name*="email" i]',
      'input[id*="email" i]',
      '#conversations_create_session_form_email',
      'input[type=text][name*="user" i]',
    ]);
    if (!emailEl) throw new Error('email input not found');
    await emailEl.type(EMAIL, { delay: 25 });
    await describe(page, '1_email_filled');
    await clickSubmit(page);
    await sleep(4000);
    await describe(page, '2_after_email');

    const passEl = await findInput(page, ['input[type=password]']);
    if (!passEl) throw new Error('password input not found (maybe verification page?)');
    await passEl.type(PASSWORD, { delay: 25 });
    await describe(page, '3_password_filled');
    await clickSubmit(page);
    await sleep(6000);
    await describe(page, '4_after_login');

    const bodyText = await page.evaluate(() => document.body.innerText.slice(0, 2500));
    console.log('BODY TEXT AFTER LOGIN:\n', bodyText);
    if (/verification code|two-factor|2fa|verify your identity/i.test(bodyText)) {
      throw new Error('account verification page detected - manual step needed');
    }

    // make sure we are on license.unity3d.com origin
    if (!page.url().includes('license.unity3d.com')) {
      console.log('not on license.unity3d.com, navigating back...');
      await page.goto('https://license.unity3d.com/manual', { waitUntil: 'networkidle2', timeout: 90000 });
      await sleep(3000);
      await describe(page, '4b_back_to_license');
    }

    // ---- GENESIS API (same-origin cookies) ----
    const alf = fs.readFileSync(ALF_PATH, 'utf8').replace(/^\uFEFF/, '');

    const res1 = await page.evaluate(async (xml) => {
      const r = await fetch('/genesis/activation/create-transaction', {
        method: 'POST', headers: { 'Content-Type': 'text/xml' }, body: xml,
      });
      return { status: r.status, text: (await r.text()).slice(0, 800) };
    }, alf);
    console.log('create-transaction:', JSON.stringify(res1));
    if (res1.status >= 400) throw new Error('create-transaction failed: ' + JSON.stringify(res1));

    const res2 = await page.evaluate(async () => {
      const r = await fetch('/genesis/activation/update-transaction', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ transaction: { serial: { type: 'personal' } } }),
      });
      return { status: r.status, text: (await r.text()).slice(0, 800) };
    });
    console.log('update-transaction:', JSON.stringify(res2));
    if (res2.status >= 400) throw new Error('update-transaction failed: ' + JSON.stringify(res2));

    const res3 = await page.evaluate(async () => {
      const r = await fetch('/genesis/activation/download-license', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}',
      });
      return { status: r.status, text: await r.text() };
    });
    console.log('download-license status:', res3.status, 'len:', res3.text.length);
    if (res3.status >= 400) {
      throw new Error('download-license failed: ' + JSON.stringify({ ...res3, text: res3.text.slice(0, 500) }));
    }

    const json = JSON.parse(res3.text);
    if (!json.xml) throw new Error('no xml field in response: ' + res3.text.slice(0, 300));
    fs.writeFileSync(ULF_OUT, json.xml);
    console.log(`ULF saved: ${ULF_OUT} (${json.xml.length} chars)`);
    console.log('SUCCESS');
  } catch (e) {
    console.error('ROBOT ERROR:', e.message);
    try { await describe(page, 'ERROR'); } catch { /* ignore */ }
    process.exitCode = 1;
  } finally {
    await browser.close();
  }
})();
