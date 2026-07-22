// Fáze 12 (A4) PASS 3 live E2E — author1 scenarios 1-4.
import { chromium } from 'playwright-core';
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';

const WEB_URL = process.env.WEB_URL ?? 'https://localhost:7150';
const OUT = process.env.OUT;
const CHROME = process.env.CHROME;
const KC_USER = process.env.KC_USER ?? 'author1';
const KC_PASS = process.env.KC_PASS ?? 'Pass123!';
const FOLDER_ID = process.env.FOLDER_ID ?? 'finance';
const BROKEN_JSON = process.env.BROKEN_JSON;
const PARAM_JSON = process.env.PARAM_JSON;

const result = { user: KC_USER, webUrl: WEB_URL, scenarios: {}, errors: [] };
const shot = async (page, name) => { await page.screenshot({ path: join(OUT, name), fullPage: true }).catch(() => {}); };
const tid = (id) => `[data-testid='${id}']`;
const sees = async (page, id) => (await page.locator(tid(id)).count()) > 0;
const txt = async (page, id) => (await page.locator(tid(id)).first().textContent().catch(() => '') ?? '').trim();

let circuitUp = false;

// After a full navigation, wait until the Blazor Server circuit websocket is open, then let it settle.
async function ensureInteractive(page) {
  for (let i = 0; i < 80 && !circuitUp; i++) await page.waitForTimeout(250);
  await page.waitForTimeout(1200);
}

// Click a trigger and wait for a target testid to appear, retrying (early clicks can be dropped
// before the circuit binds handlers). Idempotent triggers only.
async function clickUntil(page, triggerId, targetId, tries = 20) {
  for (let i = 0; i < tries; i++) {
    try {
      await page.click(tid(triggerId), { timeout: 3000 });
    } catch { /* element may be mid-render */ }
    try {
      await page.waitForSelector(tid(targetId), { timeout: 2000 });
      return true;
    } catch { /* not yet interactive; retry */ }
  }
  await page.waitForSelector(tid(targetId), { timeout: 3000 });
  return true;
}

async function login(page) {
  await page.goto(`${WEB_URL}/reports`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForSelector('#username', { timeout: 60000 });
  await page.fill('#username', KC_USER);
  await page.fill('#password', KC_PASS);
  await page.click('#kc-login');
  await page.waitForSelector(tid('f12-explorer-page'), { timeout: 60000 });
  await ensureInteractive(page);
}

async function main() {
  const browser = await chromium.launch({ headless: true, executablePath: CHROME });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  await context.route('**/*.wasm', (r) => r.abort()); // exercise Server render leg (WASM leg points at the wrong API)
  const page = await context.newPage();
  page.on('websocket', (ws) => { if (ws.url().includes('_blazor')) circuitUp = true; });
  page.on('pageerror', (e) => result.errors.push(String(e)));

  try {
    await login(page);

    // ---------- Scenario 1: create blank report via UI ----------
    const s1 = { name: 'create-blank-report' };
    try {
      await clickUntil(page, 'new-report-open', 'new-report-form');
      await page.fill(tid('new-report-name'), 'E2E Ledger');
      await page.selectOption(tid('new-report-folder'), FOLDER_ID);
      await page.waitForTimeout(300);
      await shot(page, 'f12-s1-form-filled.png');
      await page.click(tid('new-report-submit'));
      await page.waitForURL(/\/designer\//, { timeout: 30000 });
      s1.designerUrl = page.url();
      s1.reportId = decodeURIComponent(page.url().split('/designer/')[1] || '').split('?')[0];
      await page.waitForTimeout(1200);
      await shot(page, 'f12-s1-designer.png');
      s1.pass = /\/designer\//.test(page.url()) && !!s1.reportId;
    } catch (e) { s1.pass = false; s1.error = String(e); await shot(page, 'f12-s1-FAIL.png'); }
    result.scenarios.s1 = s1;

    // ---------- Scenario 2: upload edge cases ----------
    const s2 = { name: 'upload-json-edge-case' };
    try {
      await page.goto(`${WEB_URL}/reports`, { waitUntil: 'domcontentloaded', timeout: 60000 });
      await page.waitForSelector(tid('f12-explorer-page'), { timeout: 60000 });
      await ensureInteractive(page);
      await clickUntil(page, 'new-report-open', 'new-report-form');
      await page.click(tid('new-report-source-upload'));
      await page.waitForSelector(tid('new-report-file'), { timeout: 10000 });
      await page.setInputFiles(tid('new-report-file'), BROKEN_JSON);
      await page.waitForSelector(tid('new-report-file-error'), { timeout: 10000 });
      s2.brokenError = await txt(page, 'new-report-file-error');
      await page.fill(tid('new-report-name'), 'Should Not Create');
      await page.selectOption(tid('new-report-folder'), FOLDER_ID);
      await shot(page, 'f12-s2-upload-error.png');
      await page.click(tid('new-report-submit'));
      await page.waitForTimeout(1500);
      s2.blockedStillOnForm = await sees(page, 'new-report-form');
      s2.blockedNotDesigner = !/\/designer\//.test(page.url());

      // Valid minimal parametric ReportDefinition uploads and creates.
      await page.setInputFiles(tid('new-report-file'), PARAM_JSON);
      await page.waitForSelector(tid('new-report-file-ok'), { timeout: 10000 });
      s2.validOk = await txt(page, 'new-report-file-ok');
      await page.fill(tid('new-report-name'), 'E2E Param Report');
      await page.selectOption(tid('new-report-folder'), FOLDER_ID);
      await shot(page, 'f12-s2-upload-valid.png');
      await page.click(tid('new-report-submit'));
      await page.waitForURL(/\/designer\//, { timeout: 30000 });
      s2.paramReportId = decodeURIComponent(page.url().split('/designer/')[1] || '').split('?')[0];
      s2.pass = !!s2.brokenError && s2.blockedStillOnForm && s2.blockedNotDesigner && !!s2.paramReportId;
    } catch (e) { s2.pass = false; s2.error = String(e); await shot(page, 'f12-s2-FAIL.png'); }
    result.scenarios.s2 = s2;

    const paramReportId = s2.paramReportId;
    const viewerPath = paramReportId ? `${WEB_URL}/reports/Finance/${paramReportId}` : null;

    // ---------- Scenario 3a: favorite on viewer + round-trip ----------
    const s3 = { name: 'favorites', reportId: paramReportId };
    try {
      if (!viewerPath) throw new Error('no paramReportId from s2');
      await page.goto(viewerPath, { waitUntil: 'domcontentloaded', timeout: 60000 });
      await page.waitForSelector(tid('f12-viewer-page'), { timeout: 60000 });
      await ensureInteractive(page);
      s3.notFound = await sees(page, 'report-not-found');
      s3.hasToggle = await sees(page, 'favorite-toggle');
      s3.pressedBefore = await page.locator(tid('favorite-toggle')).getAttribute('aria-pressed').catch(() => null);
      await page.click(tid('favorite-toggle'));
      await page.waitForFunction(
        () => document.querySelector("[data-testid='favorite-toggle']")?.getAttribute('aria-pressed') === 'true',
        { timeout: 15000 });
      s3.pressedAfter = await page.locator(tid('favorite-toggle')).getAttribute('aria-pressed').catch(() => null);
      await shot(page, 'f12-s3-viewer-favorited.png');

      await page.click(tid('nav-favorites'));
      await page.waitForSelector(tid('f12-favorites-page'), { timeout: 30000 });
      await page.waitForTimeout(1500);
      s3.hasList = await sees(page, 'favorites-list');
      s3.itemCount = await page.locator(tid('favorite-item')).count();
      s3.itemText = (await page.locator(tid('favorite-item')).first().innerText().catch(() => '')).replace(/\s+/g, ' ').trim();
      await shot(page, 'f12-s3-favorites-list.png');

      await page.click(tid('favorite-item'));
      await page.waitForSelector(tid('f12-viewer-page'), { timeout: 30000 });
      await ensureInteractive(page);
      s3.roundTripNotFound = await sees(page, 'report-not-found');
      s3.roundTripResolved = (await sees(page, 'favorite-toggle')) && !s3.roundTripNotFound;
      await shot(page, 'f12-s3-favorites-resolved-viewer.png');
      s3.pass = !s3.notFound && s3.hasToggle && s3.pressedBefore === 'false' && s3.pressedAfter === 'true'
        && s3.hasList && s3.itemCount >= 1 && s3.roundTripResolved;
    } catch (e) { s3.pass = false; s3.error = String(e); await shot(page, 'f12-s3-FAIL.png'); }
    result.scenarios.s3 = s3;

    // ---------- Scenario 4: parametric render -> run history ----------
    const s4 = { name: 'parametric-render-run-history', reportId: paramReportId };
    try {
      if (!viewerPath) throw new Error('no paramReportId from s2');
      if (!(await sees(page, 'viewer-param-form'))) {
        await page.goto(viewerPath, { waitUntil: 'domcontentloaded', timeout: 60000 });
        await page.waitForSelector(tid('f12-viewer-page'), { timeout: 60000 });
        await ensureInteractive(page);
      }
      await page.waitForSelector(tid('viewer-param-form'), { timeout: 20000 });
      s4.hasParamInput = await sees(page, 'param-input-AsOfDate');
      await page.fill(tid('param-input-AsOfDate'), '2026-07-19');
      await page.selectOption(tid('run-format'), 'Pdf');
      await page.waitForTimeout(300);
      await page.click(tid('run-report'));
      await page.waitForSelector(tid('run-report-status'), { timeout: 30000 });
      s4.runStatus = await txt(page, 'run-report-status');
      await shot(page, 'f12-s4-viewer-run.png');

      await page.click(tid('nav-history'));
      await page.waitForSelector(tid('run-history-page'), { timeout: 30000 });
      await page.waitForTimeout(1500);
      s4.hasTable = await sees(page, 'run-history-table');
      s4.rowCount = await page.locator(tid('run-history-row')).count();
      s4.firstRow = (await page.locator(tid('run-history-row')).first().innerText().catch(() => '')).replace(/\s+/g, ' ').trim();
      await shot(page, 'f12-s4-run-history.png');
      s4.pass = s4.hasParamInput && !!s4.runStatus && s4.hasTable && s4.rowCount >= 1 && /E2E Param Report/.test(s4.firstRow);
    } catch (e) { s4.pass = false; s4.error = String(e); await shot(page, 'f12-s4-FAIL.png'); }
    result.scenarios.s4 = s4;

    // ---------- Scenario 3b: un-favorite -> empty state ----------
    const s3b = { name: 'unfavorite-empty-state', reportId: paramReportId };
    try {
      if (!viewerPath) throw new Error('no paramReportId from s2');
      await page.goto(viewerPath, { waitUntil: 'domcontentloaded', timeout: 60000 });
      await page.waitForSelector(tid('f12-viewer-page'), { timeout: 60000 });
      await ensureInteractive(page);
      s3b.pressedBefore = await page.locator(tid('favorite-toggle')).getAttribute('aria-pressed').catch(() => null);
      await page.click(tid('favorite-toggle'));
      await page.waitForFunction(
        () => document.querySelector("[data-testid='favorite-toggle']")?.getAttribute('aria-pressed') === 'false',
        { timeout: 15000 });
      s3b.pressedAfter = await page.locator(tid('favorite-toggle')).getAttribute('aria-pressed').catch(() => null);
      await page.click(tid('nav-favorites'));
      await page.waitForSelector(tid('f12-favorites-page'), { timeout: 30000 });
      await page.waitForTimeout(1200);
      s3b.emptyState = await sees(page, 'favorites-empty');
      s3b.itemCount = await page.locator(tid('favorite-item')).count();
      await shot(page, 'f12-s3b-favorites-empty.png');
      s3b.pass = s3b.pressedBefore === 'true' && s3b.pressedAfter === 'false' && s3b.emptyState && s3b.itemCount === 0;
    } catch (e) { s3b.pass = false; s3b.error = String(e); await shot(page, 'f12-s3b-FAIL.png'); }
    result.scenarios.s3b = s3b;

  } catch (e) {
    result.failure = String(e);
    await shot(page, 'f12-author-FATAL.png');
  } finally {
    result.errorCount = result.errors.length;
    result.errors = [...new Set(result.errors)].slice(0, 5);
    writeFileSync(join(OUT, 'f12-author-result.json'), JSON.stringify(result, null, 2));
    await browser.close();
  }
  console.log(JSON.stringify(result.scenarios, null, 2));
}
main();
