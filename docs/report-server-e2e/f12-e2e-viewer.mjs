// Fáze 12 PASS 3 — viewer1 role edge case (scenario 5) + empty states (scenario 6).
import { chromium } from 'playwright-core';
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
const WEB_URL = process.env.WEB_URL, OUT = process.env.OUT, CHROME = process.env.CHROME;
const KC_USER = process.env.KC_USER ?? 'viewer1', KC_PASS = process.env.KC_PASS ?? 'Pass123!';
const tid = (id) => `[data-testid='${id}']`;
const sees = async (page, id) => (await page.locator(tid(id)).count()) > 0;
let circuitUp = false;
async function ensureInteractive(page){ for(let i=0;i<80&&!circuitUp;i++) await page.waitForTimeout(250); await page.waitForTimeout(1200); }
const result = { user: KC_USER, scenarios: {}, errors: [] };
const shot = async (page,n)=>{ await page.screenshot({path:join(OUT,n),fullPage:true}).catch(()=>{}); };
const browser = await chromium.launch({ headless: true, executablePath: CHROME });
const context = await browser.newContext({ ignoreHTTPSErrors: true });
await context.route('**/*.wasm', r => r.abort());
const page = await context.newPage();
page.on('websocket', ws => { if (ws.url().includes('_blazor')) circuitUp = true; });
page.on('pageerror', e => result.errors.push(String(e)));
try {
  await page.goto(`${WEB_URL}/reports`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForSelector('#username', { timeout: 60000 });
  await page.fill('#username', KC_USER); await page.fill('#password', KC_PASS); await page.click('#kc-login');
  await page.waitForSelector(tid('f12-explorer-page'), { timeout: 60000 });
  await ensureInteractive(page);
  const s5 = { name: 'viewer1-role-gating' };
  s5.newReportOpenAbsent = !(await sees(page, 'new-report-open'));
  s5.navReports = await sees(page, 'nav-reports');
  s5.navFavorites = await sees(page, 'nav-favorites');
  s5.navHistory = await sees(page, 'nav-history');
  s5.navDesignerAbsent = !(await sees(page, 'nav-designer'));
  s5.navSchedulesAbsent = !(await sees(page, 'nav-schedules'));
  s5.signedInUser = (await page.locator(tid('signed-in-user')).textContent().catch(()=> '')).trim();
  await shot(page, 'f12-s5-viewer1-shell.png');
  s5.pass = s5.newReportOpenAbsent && s5.navReports && s5.navFavorites && s5.navHistory && s5.navDesignerAbsent;
  result.scenarios.s5 = s5;
  // Scenario 6: empty states for a user with no favorites / no runs.
  const s6 = { name: 'empty-states' };
  await page.click(tid('nav-favorites'));
  await page.waitForSelector(tid('f12-favorites-page'), { timeout: 30000 });
  await page.waitForTimeout(1000);
  s6.favoritesEmpty = await sees(page, 'favorites-empty');
  await shot(page, 'f12-s6-viewer1-favorites-empty.png');
  await page.click(tid('nav-history'));
  await page.waitForSelector(tid('run-history-page'), { timeout: 30000 });
  await page.waitForTimeout(1000);
  s6.historyEmpty = await sees(page, 'run-history-empty');
  await shot(page, 'f12-s6-viewer1-history-empty.png');
  s6.pass = s6.favoritesEmpty && s6.historyEmpty;
  result.scenarios.s6 = s6;
} catch (e) { result.failure = String(e); await shot(page, 'f12-viewer1-FATAL.png'); }
finally {
  result.errorCount = result.errors.length; result.errors = [...new Set(result.errors)].slice(0,5);
  writeFileSync(join(OUT,'f12-viewer1-result.json'), JSON.stringify(result,null,2));
  await browser.close();
}
console.log(JSON.stringify(result.scenarios, null, 2));
