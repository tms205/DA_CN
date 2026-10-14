// Kiểm tra bố cục UI thật, chặn toàn bộ request ghi để không sửa giá/máy/CSDL thật.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const { createDanhMucFixture } = require('./quan-ly-danh-muc.cjs');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

async function main() {
  const base = 'http://localhost:5064', output = process.env.PHOTOCOPY_QA_OUTPUT;
  assert.ok(output); fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const results = [];
  try {
    for (const width of [1366, 1920, 768, 390]) {
      const context = await browser.newContext({ viewport: { width, height: 1000 } });
      const page = await context.newPage(), fixture = createDanhMucFixture(), errors = [];
      page.on('pageerror', e => errors.push(e.message));
      const nd = { maNguoiDung: 1, hoTen: 'CEO kiểm thử', tenVaiTro: 'SUPER_ADMIN', maChiNhanh: 1, tenChiNhanh: 'Chi nhánh 1' };
      await context.addInitScript(nd => {
        localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'fixture-not-real', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: nd }));
        localStorage.setItem('photocopychain.pham-vi-chi-nhanh', '1');
      }, nd);
      await context.route('**/*', async route => {
        const request = route.request(), url = new URL(request.url());
        if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) {
          const tra = (body, status = 200) => route.fulfill({ status, body: JSON.stringify(body), contentType: 'application/json', headers: { 'Access-Control-Allow-Origin': '*' } });
          if (request.method() === 'OPTIONS') return tra({});
          assert.equal(request.method(), 'GET', 'Chỉ kiểm tra UI, không ghi vào API thật.');
          if (url.pathname === '/api/auth/me') return tra(nd);
          if (url.pathname.startsWith('/hubs/')) return tra({}, 503);
          const res = await fixture.response(request, 1);
          if (res) return tra(res.body, res.status);
          if (url.pathname.endsWith('/hien-tai')) return tra({ soPhieuChoDuyet: 0, soHoanTienChoDuyet: 0 });
          return tra([]);
        }
        return route.continue();
      });
      try {
        for (const route of ['/bang-gia', '/quan-tri']) {
          await page.goto(base + route);
          await expect(page.getByRole('button', { name: route === '/bang-gia' ? 'Sửa giá' : 'Sửa chi nhánh', exact: true }).first()).toBeVisible();
          const alignment = await page.getByRole('table').evaluateAll(tables => tables.map(table => {
            const header = [...table.querySelectorAll('th')].find(th => th.textContent.trim() === 'Thao tác');
            const button = table.querySelector('td[data-nhan="Thao tác"] button');
            const range = document.createRange(); range.selectNodeContents(header);
            return { table: getComputedStyle(table).display, delta: button.getBoundingClientRect().left - range.getBoundingClientRect().left,
              cellAlign: getComputedStyle(button.parentElement).textAlign };
          }));
          for (const item of alignment) {
            assert.equal(item.cellAlign, 'left', 'Nút thao tác phải căn trái giống tiêu đề.');
            if (item.table !== 'block') assert.ok(Math.abs(item.delta) < 1, `Nút lệch tiêu đề ${item.delta}px`);
          }
          assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
          await page.screenshot({ path: path.join(output, `${route.slice(1)}-${width}.png`), fullPage: true });
          results.push({ width, route, result: 'PASS' });
        }
        assert.deepEqual(errors, []);
      } catch (e) {
        await page.screenshot({ path: path.join(output, `alignment-failure-${width}.png`) }); throw e;
      } finally { await context.close(); }
    }
  } finally { await browser.close(); }
  console.log(JSON.stringify(results));
}
main().catch(e => { console.error(e.message); process.exitCode = 1; });
