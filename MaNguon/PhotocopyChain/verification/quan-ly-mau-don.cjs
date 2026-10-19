// Blazor + CRUD/PDF/JSON thật trên API fixture. Chặn toàn bộ API nghiệp vụ thật.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

async function main() {
  const web = process.env.PHOTOCOPY_WEB_URL || 'http://localhost:5078';
  const api = process.env.PHOTOCOPY_MAU_TEST_API;
  const sample = process.env.PHOTOCOPY_MAU_SAMPLE;
  const output = process.env.PHOTOCOPY_QA_OUTPUT || path.join(require('node:os').tmpdir(), 'PhotocopyMauBrowser');
  assert.ok(api && sample, 'Chạy qua fixture QuanLyMauDonTests, không trỏ API thật.');
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  try {
    for (const width of [1440, 1024, 768, 390, 320]) {
      const context = await browser.newContext({ viewport: { width, height: width <= 390 ? 640 : 900 } });
      await context.tracing.start({ screenshots: true, snapshots: true });
      const page = await context.newPage(), errors = [];
      let failNextSave = false, holdNextSave = false, resumeSave;
      page.on('pageerror', e => errors.push(e.message));
      page.on('console', m => { if (m.type() === 'error' && /Unhandled exception|crit: Microsoft/i.test(m.text())) errors.push(m.text()); });
      const user = { maNguoiDung: 1, hoTen: 'CEO kiểm thử', tenVaiTro: 'SUPER_ADMIN', maChiNhanh: null };
      await context.addInitScript(user => localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'fixture-only', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: user })), user);
      await context.route('**/*', async route => {
        const req = route.request(), url = new URL(req.url());
        if (!url.pathname.startsWith('/api/') && !url.pathname.startsWith('/hubs/')) return route.continue();
        const tra = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } });
        if (req.method() === 'OPTIONS') return tra({});
        if (url.pathname.startsWith('/api/quan-tri/mau-don') || url.pathname.startsWith('/api/file-in/mau')) {
          if (url.pathname === '/api/quan-tri/mau-don' && req.method() === 'POST') {
            if (failNextSave) { failNextSave = false; return tra({ message: 'Lỗi lưu kiểm thử, dữ liệu vẫn được giữ.' }, 503); }
            if (holdNextSave) { holdNextSave = false; await new Promise(resolve => { resumeSave = resolve; }); }
          }
          const res = await route.fetch({ url: api + url.pathname + url.search, headers: { ...req.headers(), 'x-test-role': 'SUPER_ADMIN' } });
          return route.fulfill({ response: res, headers: { ...res.headers(), 'Access-Control-Allow-Origin': '*' } });
        }
        assert.ok(req.method() === 'GET' || url.pathname.startsWith('/hubs/'), 'Chặn mọi thao tác DB/thanh toán: ' + url.pathname);
        if (url.pathname.startsWith('/hubs/')) return tra({}, 503);
        if (url.pathname === '/api/auth/me') return tra(user);
        if (url.pathname === '/api/danh-muc/chi-nhanh') return tra([{ maChiNhanh: 1, tenChiNhanh: 'Chi nhánh thử' }]);
        return tra([]);
      });
      try {
        await page.goto(web + '/mau-don');
        await expect(page.getByRole('heading', { name: 'Mẫu đơn in sẵn', exact: true })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Xem', exact: true }).first()).toBeVisible();
        const checkWidth = async () => assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1 && document.querySelector('main').scrollWidth <= document.querySelector('main').clientWidth + 1), `Tràn nội dung ${width}px`);
        await checkWidth();
        await page.getByRole('button', { name: 'Tải mẫu lên', exact: true }).click();
        const editor = page.getByRole('dialog', { name: 'Thêm mẫu đơn', exact: true });
        await expect(editor).toBeVisible();
        const checkPopup = async () => {
          assert.ok(await editor.evaluate(el => el.scrollWidth <= el.clientWidth + 1), `Popup tràn ngang ${width}px`);
          for (const name of ['Hủy', 'Đóng form mẫu đơn']) {
            const box = await editor.getByRole('button', { name, exact: true }).boundingBox();
            assert.ok(box && box.y >= 0 && box.y + box.height <= (width <= 390 ? 640 : 900), `Nút ${name} phải ở trong màn hình ${width}px`);
          }
        };
        await checkPopup();
        await expect(editor.getByLabel('Tên mẫu đơn')).toBeFocused();
        await editor.getByLabel('Tên mẫu đơn').fill('Bản nháp chưa lưu');
        await page.keyboard.press('Escape');
        await expect(editor).not.toBeVisible();
        await expect(page.getByRole('button', { name: 'Tải mẫu lên', exact: true })).toBeFocused();
        await page.getByRole('button', { name: 'Tải mẫu lên', exact: true }).click();
        await expect(editor.getByLabel('Tên mẫu đơn')).toHaveValue('');
        await checkWidth();
        await page.getByLabel('Tên mẫu đơn').fill(`Mẫu QA ${width}`);
        await page.getByRole('form', { name: 'Thông tin mẫu đơn' }).getByLabel('Loại mẫu').fill('Mẫu kiểm thử');
        await page.getByLabel('File PDF').setInputFiles({ name: 'sai.txt', mimeType: 'text/plain', buffer: Buffer.from('not pdf') });
        await expect(editor.getByRole('alert')).toContainText('PDF');
        await page.getByLabel('File PDF').setInputFiles(sample);
        await expect(editor.getByText('xin-viec.pdf', { exact: true })).toBeVisible();
        await page.screenshot({ path: path.join(output, `form-${width}.png`) });
        await editor.getByRole('button', { name: 'Lưu mẫu đơn', exact: true }).focus();
        await page.keyboard.press('Tab');
        assert.ok(await editor.evaluate(el => el.contains(document.activeElement)), 'Tab phải ở lại trong popup.');
        failNextSave = true;
        await editor.getByRole('button', { name: 'Lưu mẫu đơn', exact: true }).click();
        await expect(editor.getByRole('alert')).toContainText('Lỗi lưu kiểm thử');
        await expect(editor.getByLabel('Tên mẫu đơn')).toHaveValue(`Mẫu QA ${width}`);
        holdNextSave = true;
        await page.getByRole('button', { name: 'Lưu mẫu đơn', exact: true }).click();
        await expect(editor.getByRole('button', { name: 'Đang kiểm tra PDF và lưu…', exact: true })).toBeDisabled();
        await checkPopup();
        await page.keyboard.press('Escape');
        await expect(editor).toBeVisible();
        await expect(editor.getByRole('button', { name: 'Đóng form mẫu đơn', exact: true })).toBeDisabled();
        assert.ok(resumeSave, 'Request lưu phải đang chờ ở fixture.'); resumeSave();
        await expect(page.getByRole('status')).toContainText('Đã lưu mẫu');
        await expect(editor).not.toBeVisible();
        await page.getByLabel('Tìm mẫu', { exact: true }).fill(`Mẫu QA ${width}`);
        let row = page.getByRole('row').filter({ hasText: `Mẫu QA ${width}` });
        await expect(row).toHaveCount(1);
        await row.getByRole('button', { name: 'Xem', exact: true }).click();
        const dialog = page.getByRole('dialog');
        await expect(dialog.getByRole('img')).toBeVisible();
        assert.ok((await dialog.boundingBox()).width >= width - 2, 'Xem trước toàn màn hình.');
        await page.screenshot({ path: path.join(output, `preview-${width}.png`) });
        await page.keyboard.press('Escape');
        await expect(dialog).not.toBeVisible();
        await row.getByRole('button', { name: 'Sửa', exact: true }).click();
        const edit = page.getByRole('dialog', { name: 'Sửa thông tin mẫu', exact: true });
        await expect(edit).toBeVisible();
        await expect(edit.getByLabel('Tên mẫu đơn')).toBeFocused();
        await edit.getByRole('button', { name: 'Hủy', exact: true }).click();
        await expect(edit).not.toBeVisible();
        await expect(row.getByRole('button', { name: 'Sửa', exact: true })).toBeFocused();
        await row.getByRole('button', { name: 'Sửa', exact: true }).click();
        await page.getByLabel('Tên mẫu đơn').fill(`Mẫu đã sửa ${width}`);
        await page.getByRole('button', { name: 'Lưu mẫu đơn', exact: true }).click();
        await expect(page.getByRole('status')).toContainText('Đã lưu mẫu');
        await page.getByLabel('Tìm mẫu', { exact: true }).fill(`Mẫu đã sửa ${width}`);
        row = page.getByRole('row').filter({ hasText: `Mẫu đã sửa ${width}` });
        await row.getByRole('button', { name: 'Thay file', exact: true }).click();
        await expect(page.getByRole('dialog', { name: 'Thay file mẫu đơn', exact: true })).toBeVisible();
        await page.getByLabel('File PDF').setInputFiles(sample);
        await page.getByRole('button', { name: 'Lưu file thay thế', exact: true }).click();
        await expect(page.getByRole('status')).toContainText('Đã lưu mẫu');
        await row.getByRole('button', { name: 'Ẩn', exact: true }).click();
        await page.getByRole('dialog', { name: 'Ẩn mẫu đơn', exact: true }).getByRole('button', { name: 'Ẩn', exact: true }).click();
        await expect(row.getByText('Đang ẩn', { exact: true })).toBeVisible();
        const customer = await context.request.get(api + '/api/file-in/mau', { headers: { 'x-test-role': 'CUSTOMER' } });
        assert.ok(!(await customer.json()).some(m => m.tenMau === `Mẫu đã sửa ${width}`));
        await row.getByRole('button', { name: 'Hiện', exact: true }).click();
        await page.getByRole('dialog', { name: 'Hiện mẫu đơn', exact: true }).getByRole('button', { name: 'Hiện', exact: true }).click();
        await expect(row.getByText('Đang hiện', { exact: true })).toBeVisible();
        await checkWidth();
        await page.screenshot({ path: path.join(output, `library-${width}.png`), fullPage: true });
        await row.getByRole('button', { name: /^Gỡ mẫu/ }).click();
        await page.getByRole('dialog', { name: 'Gỡ mẫu đơn', exact: true }).getByRole('button', { name: 'Gỡ mẫu', exact: true }).click();
        await expect(row).toHaveCount(0);
        await page.reload();
        await expect(page.getByRole('button', { name: 'Xem', exact: true }).first()).toBeVisible();
        assert.deepEqual(errors, []);
        console.log(`PASS ${width}px: popup thêm/sửa/thay, focus/Esc/Hủy, lỗi giữ dữ liệu, chặn đóng khi lưu, CRUD và không tràn ngang.`);
      } catch (e) {
        await page.screenshot({ path: path.join(output, `error-${width}.png`) });
        throw e;
      } finally {
        await context.tracing.stop({ path: path.join(output, `trace-${width}.zip`) });
        await context.close();
      }
    }
  } finally { await browser.close(); }
}
main().catch(e => { console.error(e); process.exitCode = 1; });
