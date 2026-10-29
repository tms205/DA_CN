// Thử UI và tương tác bằng dữ liệu fixture; không gọi backend hay ghi dữ liệu thật.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const http = require('node:http'), fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { kiemTraQuyCach } = require('./quy-cach-tai-quay.cjs');
const root = path.resolve(process.argv[2]), output = path.resolve(process.argv[3]);
fs.mkdirSync(output, { recursive: true });
const mime = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.wasm': 'application/wasm', '.svg': 'image/svg+xml', '.png': 'image/png' };
const server = http.createServer((req, res) => {
  let file = path.resolve(root, decodeURIComponent(new URL(req.url, 'http://test.local').pathname).replace(/^\/+/, '') || 'index.html');
  if (!file.startsWith(root + path.sep)) return res.writeHead(403).end();
  if (!fs.existsSync(file) || !fs.statSync(file).isFile()) {
    if (path.extname(file)) return res.writeHead(404).end();
    file = path.join(root, 'index.html');
  }
  res.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
  fs.createReadStream(file).pipe(res);
});

async function run() {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const base = `http://127.0.0.1:${server.address().port}`;
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    for (const width of (process.env.VERIFY_FORM_ONLY === '1' ? [] : [390, 768, 1366, 1920])) {
      const context = await browser.newContext({ viewport: { width, height: 1000 }, locale: 'vi-VN' });
      const page = await context.newPage(), errors = [];
      let reports = 0, empty = false;
      page.on('pageerror', error => errors.push(error.message));
      const user = { maNguoiDung: 1, hoTen: 'CEO kiểm thử', tenVaiTro: 'SUPER_ADMIN' };
      await context.addInitScript(user => localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'fixture-only', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: user })), user);
      await context.route('**/*', async route => {
        const req = route.request(), url = new URL(req.url());
        const tra = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } });
        if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) {
          if (url.pathname.startsWith('/hubs/')) return tra({}, 503);
          assert.equal(req.method(), 'GET');
          if (url.pathname === '/api/auth/me') return tra(user);
          if (url.pathname.startsWith('/api/bao-tri')) return tra({ dangBaoTri: false });
          if (url.pathname === '/api/quan-tri/bao-cao/doanh-thu') {
            reports++;
            const rows = [];
            for (let day = new Date(url.searchParams.get('tuNgay') + 'T12:00:00'); day <= new Date(url.searchParams.get('denNgay') + 'T12:00:00'); day.setDate(day.getDate() + 1)) {
              rows.push({ ngay: `${day.getFullYear()}-${String(day.getMonth()+1).padStart(2,'0')}-${String(day.getDate()).padStart(2,'0')}T00:00:00`, maChiNhanh: 1, tenChiNhanh: 'Chi nhánh thử', soDon: 2, doanhThu: day.getDate() % 3 ? 120000 : 0 });
            }
            return tra(empty ? [] : rows);
          }
          if (url.pathname === '/api/quan-tri/bao-cao/phan-tich') return tra({ doanhThu: 300000, tienHang: 300000, dichVu: empty ? [] : [
            { ten: 'In đen trắng', soTien: 150000 }, { ten: 'In màu', soTien: 100000 }, { ten: 'Đóng lò xo', soTien: 50000 }
          ], thanhToan: [], hieuQua: {}, danhGia: {} });
          if (url.pathname === '/api/quan-tri/bao-cao/hien-tai') return tra({ soPhieuChoDuyet: 0, soHoanTienChoDuyet: 0 });
          return tra([]);
        }
        if (url.origin !== base && !['https://fonts.googleapis.com', 'https://fonts.gstatic.com'].includes(url.origin)) return route.abort();
        return route.continue();
      });
      try {
        await page.goto(base + '/tong-quan');
        const bars = page.locator('.cd-cot'), chart = page.locator('.cd-xu-huong');
        await expect(bars.first()).toBeVisible();
        await expect(page.locator('.cc-cung')).toHaveCount(3);
        for (const text of ['Doanh thu đơn hoàn tất, không tính in lại lỗi quầy', 'Đơn giá gia công lấy từ dữ liệu đã chốt', 'Cách tính và kỳ đối chiếu', 'so với kỳ đối chiếu']) await expect(page.getByText(text, { exact: false })).toHaveCount(0);
        await bars.nth(1).hover();
        await expect(page.locator('.cd-tooltip')).toBeVisible();
        await bars.nth(1).click();
        await page.getByRole('heading', { name: 'Xu hướng doanh thu', exact: true }).hover();
        await expect(bars.nth(1)).toHaveAttribute('aria-pressed', 'true');
        await bars.nth(1).focus();
        await page.keyboard.press('ArrowRight');
        await expect(bars.nth(2)).toBeFocused();
        await page.keyboard.press('Escape');
        await expect(page.locator('.cd-cot.ghim')).toHaveCount(0);
        const callsBeforeGroup = reports;
        for (const group of ['Tuần', 'Tháng', 'Ngày']) {
          await page.getByRole('group', { name: 'Nhóm báo cáo', exact: true }).getByRole('button', { name: group, exact: true }).click();
          await expect(page.getByRole('group', { name: 'Nhóm báo cáo', exact: true }).getByRole('button', { name: group, exact: true })).toHaveAttribute('aria-pressed', 'true');
        }
        assert.equal(reports, callsBeforeGroup, 'Đổi nhóm dùng dữ liệu đã tải.');
        assert.equal(await chart.evaluate(el => el.scrollWidth > el.clientWidth), false);
        await chart.screenshot({ path: path.join(output, `xu-huong-${width}.png`), animations: 'disabled' });
        const legend = page.locator('.cc-muc');
        await legend.nth(1).hover();
        await expect(page.locator('.cc-cung').nth(1)).toHaveClass(/noi-bat/);
        await expect.poll(() => page.locator('.cc-cung').nth(1).evaluate(el => getComputedStyle(el).transform)).not.toBe('none');
        await expect(page.locator('.cc-thong-tin')).toContainText('In màu');
        const sum = await legend.evaluateAll(items => items.reduce((total, item) => total + Number(item.dataset.phanTram), 0));
        assert.ok(Math.abs(sum - 100) < .001);
        await page.locator('.cc-dich-vu').screenshot({ path: path.join(output, `co-cau-${width}.png`), animations: 'disabled' });
        await legend.nth(2).focus();
        await expect(page.locator('.cc-cung').nth(2)).toHaveClass(/noi-bat/);
        await page.emulateMedia({ reducedMotion: 'reduce' });
        assert.equal(await page.locator('.cc-cung').nth(2).evaluate(el => getComputedStyle(el).transitionDuration), '0s');
        empty = true;
        await page.getByRole('button', { name: 'Xem tổng quan', exact: true }).click();
        await expect(page.getByText('Chưa có dữ liệu dịch vụ trong kỳ.', { exact: true })).toBeVisible();
        await expect(page.locator('.cc-cung')).toHaveCount(0);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
        assert.deepEqual(errors, []);
        console.log(`PASS ${width}px: cột/tooltip/ghim/bàn phím/nhóm, vòng tròn/%, hover/focus, rỗng, giảm chuyển động.`);
      } catch (error) {
        await page.screenshot({ path: path.join(output, `failure-${width}.png`), fullPage: true });
        throw error;
      } finally { await context.close(); }
    }
    for (const result of await kiemTraQuyCach(browser, base, output)) console.log(`PASS tự cuộn sau tải file và quy cách ${result.width}px`);
  } finally { await browser.close(); server.close(); }
}
run().catch(error => { console.error(error); server.close(); process.exitCode = 1; });
