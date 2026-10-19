// Web thật + API fixture bị chặn toàn bộ; không thêm vật tư/phiếu/đơn vào DB thật.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const output = path.join(require('node:os').tmpdir(), 'PhotocopyVatTuMoi');

async function run() {
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  try {
    for (const width of [1440, 390, 320]) {
      const context = await browser.newContext({ viewport: { width, height: 900 } });
      const page = await context.newPage(), errors = [], writes = [];
      const user = { maNguoiDung: 1, hoTen: 'CEO kiểm thử', tenVaiTro: 'SUPER_ADMIN', maChiNhanh: null };
      const branch = { maChiNhanh: 1, tenChiNhanh: 'Chi nhánh thử', trangThai: 'HOAT_DONG' };
      const rows = [{ ...branch, maVatTu: 1, tenVatTu: 'Giấy A4 70gsm', donViTinh: 'Tờ', soLuong: 700, mucToiThieu: 50, donGia: 120 }];
      await context.addInitScript(user => {
        localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'fixture-only', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: user }));
        localStorage.setItem('photocopychain.pham-vi-chi-nhanh', '1');
        sessionStorage.setItem('photocopychain.ca-tu-dong', '1:1');
      }, user);
      await context.tracing.start({ screenshots: true, snapshots: true });
      page.on('pageerror', e => errors.push(e.message));
      await context.route('**/*', async route => {
        const req = route.request(), url = new URL(req.url()), p = url.pathname;
        if (!p.startsWith('/api/') && !p.startsWith('/hubs/')) return route.continue();
        let status = req.method() === 'OPTIONS' ? 204 : 200, data = [];
        if (req.method() === 'POST' && p === '/api/quan-tri/danh-muc-vat-tu') {
          const body = req.postDataJSON();
          writes.push(body);
          if (body.tenVatTu === rows[0].tenVatTu) {
            status = 409; data = { message: 'Tên vật tư đã tồn tại.' };
          } else {
            rows.push({ ...branch, ...body, maVatTu: 21, soLuong: 0, mucToiThieu: 0 });
            data = { maVatTu: 21, message: 'Đã thêm vật tư mới, tồn 0.' };
          }
        } else if (!['GET', 'OPTIONS'].includes(req.method())) {
          // Không request ghi ngoài dự kiến nào được đi ra API thật.
          status = 400; data = { message: 'Bài kiểm tra không cho thao tác ghi khác.' };
          errors.push('Ghi ngoài dự kiến: ' + p);
        } else {
          const fixtures = {
            '/api/auth/me': user,
            '/api/danh-muc/chi-nhanh': [branch],
            '/api/ca/dang-mo': { maCa: 1, maNhanVien: 1, maChiNhanh: 1, trangThai: 'DANG_MO', tienDauCa: 200000 },
            '/api/quan-tri/kho': rows,
            '/api/quan-tri/danh-muc-vat-tu': [...rows, { maVatTu: 30, tenVatTu: 'Vật tư chưa có đơn vị', donViTinh: null }],
            '/api/quan-tri/danh-muc-dich-vu': [],
            '/api/quan-tri/phieu-nhap': [],
          };
          data = fixtures[p] || [];
        }
        return route.fulfill({ status, contentType: 'application/json', body: status === 204 ? '' : JSON.stringify(data),
          headers: { 'Access-Control-Allow-Origin': '*' } });
      });
      try {
        await page.goto('http://localhost:5064/kho-vat-tu', { waitUntil: 'domcontentloaded' });
        await page.getByRole('button', { name: 'Thêm vật tư mới', exact: true }).click();
        const popup = page.getByRole('dialog', { name: 'Thêm vật tư mới', exact: true });
        await expect(popup).toBeVisible();
        await expect(popup.getByLabel('Đơn vị tính')).toHaveValue('Tờ');
        await popup.getByLabel('Tên vật tư', { exact: true }).fill('Giấy A4 70gsm');
        await popup.getByRole('button', { name: 'Thêm vật tư', exact: true }).click();
        await expect(popup.getByRole('alert')).toContainText('Tên vật tư đã tồn tại.');
        await popup.getByLabel('Tên vật tư', { exact: true }).fill('Giấy A4 100gsm');
        await popup.getByLabel('Giá nhập tham khảo (₫/đơn vị)').fill('140');
        await page.screenshot({ path: path.join(output, 'popup-' + width + '.png'), fullPage: true });
        assert.ok(await popup.evaluate(el => el.scrollWidth <= el.clientWidth + 1));
        await popup.getByRole('button', { name: 'Thêm vật tư', exact: true }).click();
        await expect(popup).not.toBeVisible();
        await expect(page.getByRole('row').filter({ hasText: 'Giấy A4 100gsm' })).toContainText('0');
        assert.equal(writes.length, 2);
        assert.deepEqual(writes[1], { tenVatTu: 'Giấy A4 100gsm', donViTinh: 'Tờ', donGia: 140 });
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));

        await page.goto('http://localhost:5064/dich-vu', { waitUntil: 'domcontentloaded' });
        await page.getByRole('button', { name: 'Thêm dịch vụ', exact: true }).click();
        const service = page.getByRole('dialog');
        const paper = service.getByLabel('Giấy in trong kho', { exact: true });
        await expect(paper).toContainText('Giấy A4 100gsm');
        await expect(paper).not.toContainText('Vật tư chưa có đơn vị');
        await paper.selectOption('21');
        await expect(paper).toHaveValue('21');
        await page.keyboard.press('Escape');
        await expect(service).not.toBeVisible();
        assert.equal(writes.length, 2);
        assert.deepEqual(errors, []);
        console.log(`PASS ${width}px: popup thêm/trùng tên, đọc lại kho, chọn giấy trong dịch vụ, không tràn/JS error; chỉ ghi fixture.`);
      } catch (e) {
        await page.screenshot({ path: path.join(output, 'error-' + width + '.png'), fullPage: true });
        throw e;
      } finally {
        await context.tracing.stop({ path: path.join(output, 'trace-' + width + '.zip') });
        await context.close();
      }
    }
  } finally { await browser.close(); }
}
run().catch(e => { console.error(e); process.exitCode = 1; });
