// Kiểm tra Web thật, chặn toàn bộ API bằng fixture; không ghi dữ liệu thật.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const web = process.env.PHOTOCOPY_WEB_URL || 'http://localhost:5064';
const output = path.join(require('node:os').tmpdir(), 'PhotocopyAlignmentAdmin');

async function run() {
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  try {
    for (const width of [1440, 1024, 390, 320]) {
      const context = await browser.newContext({ viewport: { width, height: 900 } });
      const page = await context.newPage(), errors = [], writes = [];
      await context.tracing.start({ screenshots: true, snapshots: true });
      page.on('pageerror', e => errors.push(e.message));
      const user = { maNguoiDung: 1, hoTen: 'CEO thử', email: 'ceo@example.test', tenVaiTro: 'SUPER_ADMIN', maChiNhanh: null };
      const branch = { maChiNhanh: 1, tenChiNhanh: 'Chi nhánh thử', trangThai: 'HOAT_DONG' };
      const fixtures = {
        '/api/auth/me': user,
        '/api/danh-muc/chi-nhanh': [branch],
        '/api/ca/dang-mo': { maCa: 1, maNhanVien: 1, maChiNhanh: 1, trangThai: 'DANG_MO', tienDauCa: 200000 },
        '/api/quan-tri/danh-muc-dich-vu': [{ maDichVu: 1, tenDichVu: 'In A4 thử', loaiDichVu: 'IN_AN', khoGiay: 'A4', loaiGiay: '70gsm', matIn: '1_MAT', mauSac: 'DEN_TRANG', donGia: 300, trangThai: 'HIEN' }],
        '/api/quan-tri/danh-muc-vat-tu': [],
        '/api/nhan-vien': [{ maNguoiDung: 2, hoTen: 'Nhân viên thử', email: 'staff@example.test', tenVaiTro: 'STAFF', trangThai: 'HOAT_DONG', duocCapLai: true, ngayTao: '2026-10-19T07:30:00' }],
        '/api/don-hang/danh-gia': [{ maDanhGia: 1, maDonHangHienThi: 'DH00001', tenKhachHang: 'Khách thử', tenChiNhanh: 'Chi nhánh thử', soSao: 5, nhanXet: 'In rõ nét', trangThai: 'HIEN', ngayDanhGia: '2026-10-19T09:00:00' }],
        '/api/quan-tri/mau-don': [],
      };
      await context.addInitScript(user => {
        localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'fixture-only', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: user }));
        localStorage.setItem('photocopychain.pham-vi-chi-nhanh', '1');
        sessionStorage.setItem('photocopychain.ca-tu-dong', '1:1');
      }, user);
      await context.route('**/*', async route => {
        const req = route.request(), p = new URL(req.url()).pathname;
        if (!p.startsWith('/api/') && !p.startsWith('/hubs/')) return route.continue();
        if (!['GET', 'OPTIONS'].includes(req.method())) writes.push(p);
        return route.fulfill({ status: req.method() === 'OPTIONS' ? 204 : 200,
          contentType: 'application/json', body: req.method() === 'OPTIONS' ? '' : JSON.stringify(fixtures[p] || []),
          headers: { 'Access-Control-Allow-Origin': '*' } });
      });
      try {
        for (const [url, label, button, alignment] of [
          ['/dich-vu', 'Thao tác', 'Sửa dịch vụ', 'left'],
          ['/nhan-vien', 'Thao tác', 'Khóa', 'left'],
          ['/danh-gia', 'Trạng thái / Thao tác', 'Ẩn đánh giá', 'center'],
        ]) {
          await page.goto(web + url, { waitUntil: 'domcontentloaded' });
          const action = page.getByRole('button', { name: button, exact: true });
          await expect(action).toBeVisible();
          await action.click({ trial: true });
          const header = page.getByRole('columnheader', { name: label, exact: true, includeHidden: true });
          const cell = page.locator('[data-nhan="' + label + '"]');
          assert.equal(await cell.evaluate(el => getComputedStyle(el).textAlign), alignment);
          // Trên mobile, thead được giữ cho trình đọc màn hình nhưng bị clip 1px.
          // Chỉ so vị trí với header khi còn bố cục bảng; ở thẻ dùng nhãn data-nhan.
          if (await header.evaluate(el => getComputedStyle(el.closest('table')).display === 'table')) {
            const textBox = await header.evaluate(el => { const r = document.createRange(); r.selectNodeContents(el); const b = r.getBoundingClientRect(); return { x: b.x, width: b.width }; });
            const buttonBox = await action.boundingBox();
            const offset = alignment === 'left' ? buttonBox.x - textBox.x : buttonBox.x + buttonBox.width / 2 - textBox.x - textBox.width / 2;
            assert.ok(Math.abs(offset) < 2, `${url}: lệch tiêu đề ${offset}px`);
          }
          if (url === '/nhan-vien') await expect(page.getByRole('button', { name: 'Cấp lại mật khẩu', exact: true })).toHaveCount(0);
          assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
          await page.screenshot({ path: path.join(output, url.slice(1) + '-' + width + '.png'), fullPage: true });
        }
        await page.goto(web + '/mau-don', { waitUntil: 'domcontentloaded' });
        await expect(page.getByRole('button', { name: 'Làm mới thư viện', exact: true })).toHaveAttribute('title', 'Tải lại danh sách mẫu từ máy chủ, không xóa hay tạo lại thư viện.');
        await page.getByRole('button', { name: 'Làm mới thư viện', exact: true }).click();
        assert.deepEqual(writes, []);
        assert.deepEqual(errors, []);
        console.log(`PASS ${width}px: căn cột dịch vụ/nhân viên/đánh giá, không tràn ngang, không cấp mật khẩu, làm mới chỉ đọc.`);
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
