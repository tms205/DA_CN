// UI Blazor thật; dữ liệu thử được chặn trong trình duyệt, không ghi lên API đang dùng.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { kiemTraQuyCach } = require('./quy-cach-tai-quay.cjs');
const webRoot = path.resolve(process.argv[2]);
const output = path.resolve(process.argv[3]);
fs.mkdirSync(output, { recursive: true });
const mime = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.wasm': 'application/wasm', '.png': 'image/png', '.svg': 'image/svg+xml' };
const server = http.createServer((req, res) => {
  const relative = decodeURIComponent(new URL(req.url, 'http://test.local').pathname).replace(/^\/+/, '');
  let file = path.resolve(webRoot, relative || 'index.html');
  if (!file.startsWith(webRoot + path.sep)) return res.writeHead(403).end();
  if (!fs.existsSync(file) || !fs.statSync(file).isFile()) {
    if (path.extname(file)) return res.writeHead(404).end();
    file = path.join(webRoot, 'index.html');
  }
  res.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
  fs.createReadStream(file).pipe(res);
});

async function run() {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const base = `http://127.0.0.1:${server.address().port}`;
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const orderResults = await kiemTraQuyCach(browser, base, output);
    for (const r of orderResults) console.log(`PASS quy cách ${r.width}px`);
    for (const width of [390, 768, 1366, 1920]) {
      const context = await browser.newContext({ viewport: { width, height: 1000 } });
      const page = await context.newPage(), errors = [];
      page.on('pageerror', error => errors.push(error.message));
      let pending = 1, failApproval = false;
      let releaseStaff;
      const initialStaff = new Promise(resolve => { releaseStaff = resolve; });
      const nd = { maNguoiDung: 1, hoTen: 'CEO kiểm thử', tenVaiTro: 'SUPER_ADMIN' };
      const staff = [
        { maNguoiDung: 2, hoTen: 'Trần Thị Quản', email: 'gg.tms205@gmail.com', soDienThoai: '0900000002', tenVaiTro: 'BRANCH_ADMIN', tenChiNhanh: 'Cơ sở 1 - Lê Trọng Tấn', trangThai: 'HOAT_DONG', ngayTao: '2026-10-02' },
        { maNguoiDung: 3, hoTen: 'Nguyễn Văn In', email: 'nhanvien@gmail.com', soDienThoai: '0900000003', tenVaiTro: 'STAFF', tenChiNhanh: 'Cơ sở 1 - Lê Trọng Tấn', trangThai: 'HOAT_DONG', ngayTao: '2026-10-02' },
        { maNguoiDung: 4, hoTen: 'Đỗ Văn Giao', email: 'shipper@gmail.com', soDienThoai: '0900000004', tenVaiTro: 'SHIPPER', tenChiNhanh: 'Cơ sở 1 - Lê Trọng Tấn', trangThai: 'KHOA', ngayTao: '2026-10-02' },
      ];
      await context.addInitScript(nd => localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'fixture-not-real', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: nd })), nd);
      await context.route('**/*', async route => {
        const req = route.request(), url = new URL(req.url());
        const tra = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } });
        if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) {
          assert.equal(req.method(), 'GET', 'Không được gửi thao tác ghi dữ liệu trong kiểm tra này.');
          if (url.pathname === '/api/auth/me') return tra(nd);
          if (url.pathname.startsWith('/api/bao-tri')) return tra({ dangBaoTri: false });
          if (url.pathname === '/api/nhan-vien') { await initialStaff; return tra(staff); }
          if (url.pathname === '/api/danh-muc/chi-nhanh') return tra([{ maChiNhanh: 1, tenChiNhanh: 'Cơ sở 1 - Lê Trọng Tấn' }]);
          if (url.pathname === '/api/quan-tri/bao-cao/hien-tai') return tra({ soPhieuChoDuyet: pending, soHoanTienChoDuyet: 0 });
          if (url.pathname === '/api/quan-tri/phieu-nhap') return failApproval ? tra({}, 503) : tra(pending ? [{ maPhieuNhap: 42, tenChiNhanh: 'Cơ sở 1 - Lê Trọng Tấn', tenVatTu: 'Giấy A4', donViTinh: 'tờ', soLuong: 1000, donGia: 100, tenNguoiLap: 'Trần Thị Quản', nhaCungCap: 'Nhà cung cấp thử', trangThai: 'CHO_DUYET' }] : []);
          if (url.pathname === '/api/quan-tri/hoan-tien') return tra([]);
          if (url.pathname.startsWith('/hubs/')) return tra({}, 503);
          return tra([]);
        }
        if (url.origin !== base && !['https://fonts.googleapis.com', 'https://fonts.gstatic.com'].includes(url.origin)) return route.abort();
        return route.continue();
      });
      try {
        await page.goto(base + '/nhan-vien');
        await expect(page.locator('.bang-dang-tai')).toBeVisible();
        assert.notEqual(await page.locator('.tai-khung').evaluate(el => getComputedStyle(el, '::after').animationName), 'none');
        await page.emulateMedia({ reducedMotion: 'reduce' });
        assert.equal(await page.locator('.tai-khung').evaluate(el => getComputedStyle(el, '::after').animationName), 'none');
        releaseStaff();
        await page.emulateMedia({ reducedMotion: 'no-preference' });
        await expect(page.getByRole('button', { name: 'Thêm nhân viên' })).toBeVisible();
        await expect(page.locator('tbody tr')).toHaveCount(3);
        await expect(page.locator('.bang-dang-tai')).toHaveCount(0);
        await page.getByLabel('Vai trò', { exact: true }).selectOption('STAFF');
        await expect(page.locator('tbody tr')).toHaveCount(1);
        await expect(page.locator('tbody')).toContainText('Nguyễn Văn In');
        await page.getByLabel('Vai trò', { exact: true }).selectOption('');
        await page.getByLabel('Trạng thái', { exact: true }).selectOption('KHOA');
        await expect(page.locator('tbody tr')).toHaveCount(1);
        await expect(page.getByRole('button', { name: 'Mở khóa', exact: true })).toBeVisible();
        await page.getByLabel('Trạng thái', { exact: true }).selectOption('');
        await page.getByLabel('Tìm nhân viên').fill('0900000002');
        await expect(page.locator('tbody tr')).toHaveCount(1);
        await page.getByLabel('Tìm nhân viên').fill('');
        if (width >= 1366) {
          const layout = await page.locator('.nv-danh-sach').evaluate(el => {
            const button = el.querySelector('.nv-them').getBoundingClientRect();
            const avatar = el.querySelector('.avatar-nho').getBoundingClientRect();
            const name = el.querySelector('.nv-ten').getBoundingClientRect();
            return { buttonWidth: button.width, panelWidth: el.clientWidth, horizontalIdentity: avatar.right <= name.left };
          });
          assert.ok(layout.buttonWidth < layout.panelWidth / 2, 'Nút thêm cần gọn trong thanh công cụ.');
          assert.ok(layout.horizontalIdentity, 'Avatar phải cùng hàng với thông tin nhân viên.');
        }
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
        if (width === 768) assert.equal(await page.locator('tbody tr').first().evaluate(el => getComputedStyle(el).display), 'grid');
        await expect(page.locator('.nv-danh-sach')).toHaveCSS('opacity', '1');
        await page.screenshot({ path: path.join(output, `nhan-vien-${width}.png`), fullPage: true });
        await page.getByRole('button', { name: 'Thêm nhân viên' }).click();
        await expect(page.locator('dialog[open]')).toBeVisible();
        await page.emulateMedia({ reducedMotion: 'reduce' });
        assert.equal(await page.locator('dialog[open]').evaluate(el => getComputedStyle(el).animationName), 'none');
        await page.keyboard.press('Escape');
        await expect(page.locator('dialog[open]')).toHaveCount(0);
        await page.emulateMedia({ reducedMotion: 'no-preference' });
        await page.goto(base + '/phe-duyet');
        const badge = page.locator('a[href="phe-duyet"] .tb-badge');
        await expect(badge).toHaveText('1');
        await expect(page.locator('tbody')).toContainText('PN0042');
        await page.screenshot({ path: path.join(output, `phe-duyet-${width}.png`), fullPage: true });
        pending = 0;
        await page.getByRole('button', { name: 'Làm mới danh sách', exact: true }).click();
        await expect(page.getByText('Không có phiếu nhập nào đang chờ duyệt.')).toBeVisible();
        await expect(badge).toHaveCount(0);
        await page.goto(base + '/nhan-vien');
        await expect(page.locator('tbody tr')).toHaveCount(3);
        await expect(badge).toHaveCount(0);
        await page.goto(base + '/phe-duyet');
        await expect(badge).toHaveCount(0);
        failApproval = true;
        await page.getByRole('button', { name: 'Làm mới danh sách', exact: true }).click();
        await expect(page.locator('.o-loi')).toContainText('Không tải được danh sách');
        await expect(badge).toHaveText('?');
        failApproval = false;
        await page.getByRole('button', { name: 'Làm mới danh sách', exact: true }).click();
        await expect(page.locator('.o-loi')).toHaveCount(0);
        await expect(badge).toHaveCount(0);
        assert.deepEqual(errors, []);
        console.log(`PASS nhân viên/phê duyệt ${width}px: bộ lọc, popup, badge 1→0, chuyển trang, lỗi/tải lại.`);
      } catch (error) {
        await page.screenshot({ path: path.join(output, `failure-${width}.png`), fullPage: true });
        throw error;
      } finally { await context.close(); }
    }
  } finally { await browser.close(); server.close(); }
}
run().catch(error => { console.error(error); server.close(); process.exitCode = 1; });
