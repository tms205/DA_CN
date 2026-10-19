// Chỉ tương tác Web với API fixture; không sửa dữ liệu kho thật.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const output = path.join(require('node:os').tmpdir(), 'PhotocopySuaKhoVatTu');

async function run() {
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  try {
    for (const width of [1440, 1024, 390, 320]) {
      for (const role of ['SUPER_ADMIN', 'BRANCH_ADMIN']) {
        const context = await browser.newContext({ viewport: { width, height: 900 } });
        const page = await context.newPage(), errors = [], writes = [];
        const user = { maNguoiDung: 1, hoTen: 'Người kiểm thử', tenVaiTro: role, maChiNhanh: role === 'SUPER_ADMIN' ? null : 1 };
        const branches = [1, 2].map(ma => ({ maChiNhanh: ma, tenChiNhanh: 'Chi nhánh ' + ma, trangThai: 'HOAT_DONG' }));
        const rows = branches.map(cn => ({ ...cn, maVatTu: 1, tenVatTu: 'Giấy thử', donViTinh: 'Tờ', soLuong: 20, mucToiThieu: 5, donGia: 120, duocDoiDonVi: false, canhBao: false }));
        rows.push({ ...branches[0], maVatTu: 2, tenVatTu: 'Giấy chưa dùng', donViTinh: 'Tờ', soLuong: 0, mucToiThieu: 0, donGia: 0, duocDoiDonVi: true, canhBao: true });
        await context.addInitScript(user => {
          localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'fixture-only', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: user }));
          if (user.tenVaiTro !== 'SUPER_ADMIN') localStorage.setItem('photocopychain.pham-vi-chi-nhanh', '1');
          sessionStorage.setItem('photocopychain.ca-tu-dong', '1:1');
        }, user);
        await context.tracing.start({ screenshots: true, snapshots: true });
        page.on('pageerror', e => errors.push(e.message));
        await context.route('**/*', async route => {
          const req = route.request(), p = new URL(req.url()).pathname;
          if (!p.startsWith('/api/') && !p.startsWith('/hubs/')) return route.continue();
          let status = req.method() === 'OPTIONS' ? 204 : 200, data = [];
          if (req.method() === 'PUT') {
            const body = req.postDataJSON();
            writes.push({ p, body });
            if (p === '/api/quan-tri/danh-muc-vat-tu/1') {
              if (body.tenVatTu === 'Giấy trùng') { status = 409; data = { message: 'Tên vật tư đã tồn tại.' }; }
              else {
                rows.filter(x => x.maVatTu === 1).forEach(x => Object.assign(x, body));
                data = { message: 'Đã sửa thông tin vật tư.' };
              }
            } else if (p === '/api/quan-tri/kho/1/1/muc-canh-bao') {
              rows[0].mucToiThieu = body.mucToiThieu;
              rows[0].canhBao = rows[0].soLuong <= body.mucToiThieu;
              data = { message: 'Đã cập nhật mức cảnh báo.' };
            } else { status = 400; errors.push('PUT ngoài dự kiến: ' + p); }
          } else if (!['GET', 'OPTIONS'].includes(req.method())) {
            status = 400; errors.push('Ghi ngoài dự kiến: ' + p);
          } else {
            data = ({
              '/api/auth/me': user, '/api/danh-muc/chi-nhanh': branches,
              '/api/ca/dang-mo': { maCa: 1, maNhanVien: 1, maChiNhanh: 1, trangThai: 'DANG_MO', tienDauCa: 200000 },
              '/api/quan-tri/kho': role === 'SUPER_ADMIN' ? rows : rows.filter(x => x.maChiNhanh === 1),
              '/api/quan-tri/phieu-nhap': [],
            })[p] || [];
          }
          return route.fulfill({ status, contentType: 'application/json', body: status === 204 ? '' : JSON.stringify(data), headers: { 'Access-Control-Allow-Origin': '*' } });
        });
        try {
          await page.goto('http://localhost:5064/kho-vat-tu', { waitUntil: 'domcontentloaded' });
          const stockTable = page.getByRole('table').first();
          const row1 = () => stockTable.getByRole('row').filter({ hasText: 'Chi nhánh 1' }).filter({ hasText: role === 'SUPER_ADMIN' ? 'Giấy đã sửa' : 'Giấy thử' });
          await expect(stockTable.getByRole('button', { name: 'Sửa mức cảnh báo', exact: true }).first()).toBeVisible();
          if (role === 'SUPER_ADMIN') {
            const rowUsed = stockTable.getByRole('row').filter({ hasText: 'Chi nhánh 1' }).filter({ hasText: 'Giấy thử' });
            await rowUsed.getByRole('button', { name: 'Sửa thông tin', exact: true }).click();
            const dialog = page.getByRole('dialog', { name: 'Sửa thông tin vật tư', exact: true });
            await expect(dialog.getByLabel('Đơn vị tính', { exact: true })).toBeDisabled();
            await dialog.getByLabel('Tên vật tư', { exact: true }).fill('Giấy trùng');
            await dialog.getByRole('button', { name: 'Lưu thông tin', exact: true }).click();
            await expect(dialog.getByRole('alert')).toContainText('Tên vật tư đã tồn tại.');
            await dialog.getByLabel('Tên vật tư', { exact: true }).fill('Giấy đã sửa');
            await dialog.getByLabel('Giá nhập tham khảo (₫/đơn vị)').fill('150');
            await dialog.getByRole('button', { name: 'Lưu thông tin', exact: true }).click();
            await expect(dialog).not.toBeVisible();
            await expect(stockTable.getByText('Giấy đã sửa', { exact: true })).toHaveCount(2);
            assert.deepEqual(writes[1].body, { tenVatTu: 'Giấy đã sửa', donViTinh: 'Tờ', donGia: 150 });
            const unused = stockTable.getByRole('row').filter({ hasText: 'Giấy chưa dùng' });
            await unused.getByRole('button', { name: 'Sửa thông tin', exact: true }).click();
            await expect(dialog.getByLabel('Đơn vị tính', { exact: true })).toBeEnabled();
            await page.keyboard.press('Escape');
            await expect(dialog).not.toBeVisible();
          } else {
            await expect(page.getByRole('button', { name: 'Sửa thông tin', exact: true })).toHaveCount(0);
          }
          await row1().getByRole('button', { name: 'Sửa mức cảnh báo', exact: true }).click();
          const warning = page.getByRole('dialog', { name: 'Sửa mức cảnh báo tồn kho', exact: true });
          await expect(warning).toContainText('Chi nhánh 1');
          await expect(warning.getByRole('spinbutton')).toHaveCount(1);
          await warning.getByLabel('Mức cảnh báo tối thiểu', { exact: true }).fill('30');
          assert.ok(await warning.evaluate(el => el.scrollWidth <= el.clientWidth + 1));
          await page.screenshot({ path: path.join(output, `${role}-${width}-popup.png`), fullPage: true });
          await warning.getByRole('button', { name: 'Lưu mức cảnh báo', exact: true }).click();
          await expect(warning).not.toBeVisible();
          await expect(row1()).toContainText('Sắp hết hàng');
          assert.deepEqual(writes.at(-1), { p: '/api/quan-tri/kho/1/1/muc-canh-bao', body: { mucToiThieu: 30 } });
          assert.equal(rows[1].mucToiThieu, 5);
          assert.equal(rows[0].soLuong, 20);
          assert.equal(rows[1].soLuong, 20);
          assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
          assert.deepEqual(errors, []);
          console.log(`PASS ${role} ${width}px: quyền, popup lưu, khóa đơn vị, không tràn; chỉ ghi fixture.`);
        } catch (e) {
          await page.screenshot({ path: path.join(output, `${role}-${width}-error.png`), fullPage: true });
          throw e;
        } finally {
          await context.tracing.stop({ path: path.join(output, `${role}-${width}.zip`) });
          await context.close();
        }
      }
    }
  } finally { await browser.close(); }
}
run().catch(e => { console.error(e); process.exitCode = 1; });
