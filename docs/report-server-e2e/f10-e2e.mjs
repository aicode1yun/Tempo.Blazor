// Fáze 10 (A1) live E2E driver — proves the portal reflects the OIDC principal.
// Usage:
//   MODE=demo   WEB_URL=https://localhost:7150 OUT=<dir> node f10-e2e.mjs
//   MODE=author WEB_URL=... KC_USER=author1 KC_PASS=Pass123! OUT=<dir> node f10-e2e.mjs
//   MODE=viewer WEB_URL=... KC_USER=viewer1 KC_PASS=Pass123! OUT=<dir> node f10-e2e.mjs
// Requires playwright-core resolvable (build tests/Tempo.Blazor.E2E OR `npm i playwright-core`);
// pass CHROME=<path to chrome.exe> (chromium-1161 chrome-win/chrome.exe).
import { chromium } from 'playwright-core';
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';

const MODE = process.env.MODE ?? 'demo';
const WEB_URL = process.env.WEB_URL ?? 'https://localhost:7150';
const OUT = process.env.OUT ?? '.';
const CHROME = process.env.CHROME;
const NAV_IDS = ['nav-reports', 'nav-designer', 'nav-datasources', 'nav-schedules', 'nav-permissions', 'nav-revisions', 'nav-apikeys'];

const log = (m) => console.log(`[f10:${MODE}] ${m}`);

async function navMap(page) {
  const present = {};
  for (const id of NAV_IDS) present[id] = (await page.locator(`[data-testid='${id}']`).count()) > 0;
  return present;
}

async function main() {
  const browser = await chromium.launch({ headless: true, executablePath: CHROME });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  // Server-render mode: block only the wasm payload so the Server leg is exercised deterministically.
  await context.route('**/*.wasm', (r) => r.abort());
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(String(e)));
  const result = { mode: MODE, webUrl: WEB_URL, errors: [] };

  try {
    if (MODE === 'demo') {
      // OIDC OFF: the demo portal runs without an OIDC challenge. Demo login form → reports.
      await page.goto(`${WEB_URL}/login`, { waitUntil: 'networkidle', timeout: 60000 });
      await page.waitForSelector("[data-testid='login-interactive-ready']", { timeout: 60000 });
      await page.screenshot({ path: join(OUT, 'f10-demo-1-login.png'), fullPage: true });
      await page.click("[data-testid='login-submit']");
      await page.waitForSelector("[data-testid='report-server-shell']", { timeout: 60000 });
      await page.waitForTimeout(500);
      await page.screenshot({ path: join(OUT, 'f10-demo-2-reports.png'), fullPage: true });
      result.hasTenantSwitcher = (await page.locator("[data-testid='tenant-switcher']").count()) > 0;
      result.hasTenantDisplay = (await page.locator("[data-testid='tenant-display']").count()) > 0;
      result.signedInUser = (await page.locator("[data-testid='signed-in-user']").textContent().catch(() => '')).trim();
      result.nav = await navMap(page);
    } else {
      // OIDC ON: hitting a protected page redirects to the Keycloak challenge. Log in, land on /reports.
      await page.goto(`${WEB_URL}/reports`, { waitUntil: 'domcontentloaded', timeout: 60000 });
      // Keycloak login form.
      await page.waitForSelector('#username', { timeout: 60000 });
      await page.fill('#username', process.env.KC_USER);
      await page.fill('#password', process.env.KC_PASS);
      await page.screenshot({ path: join(OUT, `f10-${MODE}-1-kc-login.png`), fullPage: true });
      await page.click('#kc-login');
      await page.waitForSelector("[data-testid='report-server-shell']", { timeout: 60000 });
      await page.waitForTimeout(800);
      await page.screenshot({ path: join(OUT, `f10-${MODE}-2-reports.png`), fullPage: true });
      result.hasTenantSwitcher = (await page.locator("[data-testid='tenant-switcher']").count()) > 0;
      result.hasTenantDisplay = (await page.locator("[data-testid='tenant-display']").count()) > 0;
      result.tenantDisplay = (await page.locator("[data-testid='tenant-display']").textContent().catch(() => '')).trim();
      result.signedInUser = (await page.locator("[data-testid='signed-in-user']").textContent().catch(() => '')).trim();
      result.nav = await navMap(page);
    }
    result.errors = errors;
    log('OK ' + JSON.stringify(result));
  } catch (e) {
    result.failure = String(e);
    result.errors = errors;
    log('FAIL ' + String(e));
    await page.screenshot({ path: join(OUT, `f10-${MODE}-FAIL.png`), fullPage: true }).catch(() => {});
  } finally {
    writeFileSync(join(OUT, `f10-${MODE}-result.json`), JSON.stringify(result, null, 2));
    await browser.close();
  }
  if (result.failure) process.exit(1);
}
main();
