const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const assert = require('node:assert/strict');
const path = require('node:path');

async function kiemTraQuyCach(browser, base, output) {
  const results = [];
  for (const width of [390, 768, 1366, 1920]) {
    const context = await browser.newContext({ viewport: { width, height: 1000 } });
    const page = await context.newPage(), errors = [], baoGia = [];
    page.on('pageerror', e => errors.push(e.message));
    const nd = { maNguoiDung: 2, hoTen: 'Quản lý kiểm thử', tenVaiTro: 'BRANCH_ADMIN', maChiNhanh: 1, tenChiNhanh: 'Chi nhánh thử' };
    await context.addInitScript(nd => localStorage.setItem('photocopychain.phien', JSON.stringify({
      accessToken: 'fixture-not-real', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: nd
    })), nd);
    const ds = [
      { maDichVu: 1, tenDichVu: 'In A4', loaiDichVu: 'IN_AN', khoGiay: 'A4', loaiGiay: '70gsm', matIn: '1_MAT', mauSac: 'DEN_TRANG', donGia: 300 },
      { maDichVu: 13, tenDichVu: 'Đóng bìa kiếng', loaiDichVu: 'GIA_CONG', nhomGiaCong: 'DONG_GAY', donGia: 5000 },
      { maDichVu: 14, tenDichVu: 'Đóng lò xo', loaiDichVu: 'GIA_CONG', nhomGiaCong: 'DONG_GAY', donGia: 10000 },
      { maDichVu: 15, tenDichVu: 'Ép plastic', loaiDichVu: 'GIA_CONG', donGia: 15000 },
      { maDichVu: 16, tenDichVu: 'Bấm kim góc', loaiDichVu: 'GIA_CONG', donGia: 1000 },
    ];
    await context.route('**/*', async route => {
      const req = route.request(), url = new URL(req.url());
      if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) {
        const tra = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } });
        if (url.pathname === '/api/don-hang/khach-tai-quay') return tra({ maKhachHang: 20, hoTen: 'Khách thử' });
        if (url.pathname === '/api/file-in' && req.method() === 'POST') return tra({ maFileIn: 10, tenFileGoc: 'test.pdf', soTrang: 8, trangThaiFile: 'SAN_SANG' });
        if (url.pathname === '/api/bao-gia') {
          const body = req.postDataJSON(); baoGia.push(body);
          return tra({ tienIn: 2400, tienGiaCong: 15000, tongTien: 17400, tongSoTrang: 8, ngayHenTra: '2026-10-13T18:30:00', khuyenMaiHopLe: true });
        }
        assert.ok(req.method() === 'GET' || url.pathname.startsWith('/hubs/'), 'Không lập đơn/thu tiền thật.');
        if (url.pathname === '/api/danh-muc/bang-gia') return tra(ds);
        if (url.pathname === '/api/auth/me') return tra(nd);
        if (url.pathname === '/api/danh-muc/chi-nhanh') return tra([{ maChiNhanh: 1, tenChiNhanh: 'Chi nhánh thử' }]);
        if (url.pathname === '/api/ca/dang-mo') return tra({ maCa: 1, maNhanVien: 2, maChiNhanh: 1, trangThai: 'DANG_MO' });
        if (url.pathname === '/api/quan-tri/bao-cao/hien-tai') return tra({ soPhieuChoDuyet: 0, soHoanTienChoDuyet: 0 });
        if (url.pathname.startsWith('/hubs/')) return tra({}, 503);
        return tra([]);
      }
      if (url.origin !== base && !['https://fonts.googleapis.com', 'https://fonts.gstatic.com'].includes(url.origin)) return route.abort();
      return route.continue();
    });
    try {
      await page.goto(base + '/lap-don-tai-quay');
      await page.getByPlaceholder('Nguyễn Văn An').fill('Khách thử');
      await page.getByRole('button', { name: 'Tìm / tạo khách hàng', exact: true }).click();
      await page.locator('input[type=file]').setInputFiles({ name: 'test.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4\nfixture') });
      await expect(page.getByLabel('Thanh toán', { exact: true })).toBeVisible();
      const label = name => page.getByText(new RegExp(name + ' ·'));
      const checked = name => label(name).locator('input[type=checkbox]');
      await label('Đóng bìa kiếng').click();
      await label('Đóng lò xo').click();
      await expect(checked('Đóng bìa kiếng')).toBeChecked();
      await expect(checked('Đóng lò xo')).toBeChecked();
      await page.getByRole('button', { name: 'Tính lại giá', exact: true }).click();
      await expect(page.getByRole('button', { name: 'Lập đơn & hiện mã QR', exact: true })).toBeEnabled();
      assert.deepEqual(baoGia.at(-1).fileIn[0].dsGiaCong.sort(), [13, 14]);
      await label('Bấm kim góc').click();
      await expect(checked('Đóng lò xo')).not.toBeChecked();
      await expect(checked('Đóng bìa kiếng')).toBeChecked();
      await expect(checked('Bấm kim góc')).toBeChecked();
      await page.getByLabel('Tốc độ xử lý', { exact: true }).selectOption('HOA_TOC');
      await expect(page.getByRole('button', { name: 'Lập đơn & hiện mã QR', exact: true })).toBeEnabled();
      assert.equal(baoGia.at(-1).inGap, true);
      const sizes = await page.evaluate(() => {
        const ids = ['ldq-thanh-toan', 'ldq-ma-khuyen-mai', 'ldq-toc-do'];
        const fields = ids.map(id => { const e = document.getElementById(id), b = e.getBoundingClientRect(); return { x: b.x, y: b.y, width: b.width }; });
        return { fields, full: document.getElementById(ids[0]).parentElement.parentElement.clientWidth, overflow: document.documentElement.scrollWidth > innerWidth };
      });
      assert.equal(sizes.overflow, false);
      if (width > 1100) {
        assert.ok(sizes.fields.every(f => Math.abs(f.y - sizes.fields[0].y) < 1), 'Ba ô phải cùng hàng desktop.');
        assert.ok(sizes.fields[0].width < sizes.full * .36 && sizes.fields[1].width < sizes.full * .2, 'Thanh toán/mã giảm khoảng một nửa độ rộng cũ.');
      }
      await label('Đóng lò xo').click();
      await expect(checked('Đóng bìa kiếng')).toBeChecked();
      await expect(checked('Đóng lò xo')).toBeChecked();
      await expect(checked('Bấm kim góc')).not.toBeChecked();
      await page.getByRole('button', { name: 'Tính lại giá', exact: true }).click();
      await expect(page.getByRole('button', { name: 'Lập đơn & hiện mã QR', exact: true })).toBeEnabled();
      await page.getByLabel('Thanh toán', { exact: true }).scrollIntoViewIfNeeded();
      await page.screenshot({ path: path.join(output, `quy-cach-${width}.png`), fullPage: true });
      assert.deepEqual(errors, []);
      results.push({ width, status: 'PASS', sizes, note: 'Real Blazor UI, API fixture; no production writes' });
    } catch (error) {
      await page.screenshot({ path: path.join(output, `quy-cach-failure-${width}.png`), fullPage: true });
      throw error;
    } finally { await context.close(); }
  }
  return results;
}
module.exports = { kiemTraQuyCach };
