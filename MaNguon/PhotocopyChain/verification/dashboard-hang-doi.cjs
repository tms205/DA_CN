// Kiểm tra UI thật với API fixture; chặn toàn bộ request tới backend thật.
// Dùng bản dotnet publish (thư mục wwwroot) truyền qua đối số đầu tiên.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { createDanhMucFixture, kiemTraDanhMuc } = require('./quan-ly-danh-muc.cjs');
const { kiemTraGoogle } = require('./google-auth.cjs');
const { kiemTraQuyCach } = require('./quy-cach-tai-quay.cjs');

const webRoot = path.resolve(process.argv[2] || '');
assert.ok(fs.existsSync(path.join(webRoot, 'index.html')), 'Truyền thư mục wwwroot của bản publish.');
const output = path.resolve(process.argv[3] || path.join(require('node:os').tmpdir(), 'PhotocopyDashboardScreenshots'));
fs.mkdirSync(output, { recursive: true });
const mime = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.css': 'text/css', '.wasm': 'application/wasm', '.png': 'image/png', '.svg': 'image/svg+xml', '.dat': 'application/octet-stream' };
const server = http.createServer((req, res) => {
  const relative = decodeURIComponent(new URL(req.url, 'http://test.local').pathname).replace(/^\/+/, '');
  let file = path.resolve(webRoot, relative || 'index.html');
  if (!file.startsWith(webRoot + path.sep) && file !== path.join(webRoot, 'index.html')) { res.writeHead(403).end(); return; }
  if (!fs.existsSync(file) || !fs.statSync(file).isFile()) {
    if (path.extname(file)) { res.writeHead(404).end(); return; }
    file = path.join(webRoot, 'index.html');
  }
  res.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
  fs.createReadStream(file).pipe(res);
});

function date(day) {
  return `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;
}
function fixtureReport(url, branch) {
  const from = new Date(url.searchParams.get('tuNgay') + 'T12:00:00');
  const to = new Date(url.searchParams.get('denNgay') + 'T12:00:00');
  const rows = [];
  for (let day = new Date(from); day <= to; day.setDate(day.getDate() + 1)) {
    for (const id of branch ? [branch] : [1, 2, 3]) {
      rows.push({ ngay: date(day) + 'T00:00:00', maChiNhanh: id, tenChiNhanh: `Chi nhánh ${id}`, soDon: id, doanhThu: id * 100000 });
    }
  }
  return rows;
}
function fixtureQueue(branch) {
  return ['COD', 'VIETQR', 'VIETQR', 'COD', 'COD', 'COD', 'COD'].map((method, i) => ({
    maDonHang: i + 1, maDonHangHienThi: `DH${String(i + 1).padStart(5, '0')}`, maDonHangGoc: null, maDonHangGocHienThi: null, loaiPhatSinh: null,
    maChiNhanh: branch || 1, tenChiNhanh: `Chi nhánh ${branch || 1}`, tenKhachHang: `Khách ${i + 1}`, soDienThoaiKhach: null, hinhThucNhan: 'TAI_QUAY',
    inHoaToc: i === 0, trangThaiDonHang: i < 3 ? 'CHO_XAC_NHAN' : 'DA_XAC_NHAN', phuongThucThanhToan: method,
    trangThaiThanhToan: i === 2 ? 'DA_THANH_TOAN' : 'CHUA_THANH_TOAN', tongTien: 100000,
    ngayDat: `2026-10-13T09:${String(20 - i).padStart(2, '0')}:00`, ngayHenTra: null, soFile: 1, tongSoTrang: 20, quyCach: 'A4 · 1 mặt · Đen trắng', tenNhanVien: null, tenMayIn: null
  }));
}

async function run() {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const base = `http://127.0.0.1:${server.address().port}`;
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const results = [];
  try {
    for (const width of (process.env.VERIFY_GOOGLE_ONLY === '1' || process.env.VERIFY_FORM_ONLY === '1' ? [] : [390, 768, 1024, 1366, 1920])) {
      const context = await browser.newContext({ viewport: { width, height: 1000 }, locale: 'vi-VN', timezoneId: 'Asia/Ho_Chi_Minh' });
      let mode = 'normal';
      const danhMucFixture = createDanhMucFixture();
      const apiCalls = [], errors = [];
      await context.addInitScript(() => {
        if (!localStorage.getItem('fixture-anonymous') && !localStorage.getItem('photocopychain.phien')) localStorage.setItem('photocopychain.phien', JSON.stringify({
          accessToken: 'ui-fixture-not-a-real-token', hetHanLuc: '2099-01-01T00:00:00Z',
          nguoiDung: { maNguoiDung: 1, hoTen: 'CEO kiểm thử', tenVaiTro: 'SUPER_ADMIN', maChiNhanh: null, tenChiNhanh: null }
        }));
      });
      await context.addInitScript(() => {
        Object.defineProperty(navigator.clipboard, 'writeText', { configurable: true, value: async text => {
          if (window.fixtureClipboardError) throw new DOMException('Clipboard denied', 'NotAllowedError');
          window.fixtureCopiedText = text;
        } });
      });
      await context.route('**/*', async route => {
        const url = new URL(route.request().url());
        if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) {
          const selected = (route.request().headers().authorization || '').includes('branch-fixture') ? 1 : Number(route.request().headers()['x-ma-chi-nhanh'] || 0);
          const danhMuc = await danhMucFixture.response(route.request(), selected);
          if (danhMuc) return route.fulfill({ status: danhMuc.status, contentType: 'application/json', body: JSON.stringify(danhMuc.body), headers: { 'Access-Control-Allow-Origin': '*' } });
          if (url.pathname === '/api/ca/mo' && route.request().method() === 'POST') {
            assert.ok(selected > 0, 'CEO chỉ mở ca tại một chi nhánh cụ thể.');
            assert.equal(route.request().postDataJSON().tienDauCa, 200000);
            return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ maCa: 50, maNhanVien: 1, maChiNhanh: selected, tienDauCa: 200000, trangThai: 'DANG_MO' }), headers: { 'Access-Control-Allow-Origin': '*' } });
          }
          assert.equal(route.request().method(), url.pathname.startsWith('/hubs/') ? 'POST' : 'GET', 'Chỉ fixture danh mục được xử lý request ghi; không gọi backend thật.');
          apiCalls.push({ path: url.pathname, selected });
          let body;
          if (url.pathname === '/api/quan-tri/bao-cao/doanh-thu') {
            assert.equal(url.searchParams.get('nhomTheo'), 'ngay');
            if (mode === 'error') return route.fulfill({ status: 503, contentType: 'application/json', body: '{"message":"fixture unavailable"}', headers: { 'Access-Control-Allow-Origin': '*' } });
            body = mode === 'empty' || (mode === 'previous-zero' && new Date(url.searchParams.get('denNgay') + 'T23:59:59') < new Date(new Date().setHours(0, 0, 0, 0))) ? [] : fixtureReport(url, selected);
           } else if (url.pathname === '/api/quan-tri/bao-cao/phan-tich') {
            if (mode === 'analysis-error') return route.fulfill({ status: 503, headers: { 'Access-Control-Allow-Origin': '*' } });
            const ds = mode === 'empty' ? [] : fixtureReport(url, selected), total = ds.reduce((sum, x) => sum + x.doanhThu, 0);
            body = { tienHang: total, phiShip: 0, giamGia: 0, doanhThu: total,
              dichVu: total ? [{ ten: 'In đen trắng', soTien: total * .8, soDon: ds.length }, { ten: 'Đóng lò xo', soTien: total * .2, soDon: ds.length }] : [],
              thanhToan: total ? [{ ten: 'VietQR', soTien: total, soDon: ds.length }] : [],
              gioCaoDiem: total ? [{ thu: 0, gio: 9, soDon: 5 }] : [],
              hieuQua: { soDon: ds.length, soDonCoMoc: ds.length, phutXuLyTrungBinh: total ? 40 : null, soDonCoHen: ds.length, soDonDungHen: ds.length },
              danhGia: { soLuot: total ? 2 : 0, diemTrungBinh: total ? 4.5 : null } };
          } else if (url.pathname === '/api/quan-tri/bao-cao/hien-tai') body = {
            docLuc: new Date().toISOString(), tuNgayKiemDem: '2026-09-14T00:00:00', soCaDangMo: 2, soCaChoXacNhan: 1, soCaKiemDem: 0, soCaLech: 0, tienLech: 0, soPhieuChoDuyet: 1, soHoanTienChoDuyet: 1
          };
          else if (url.pathname === '/api/auth/google/config') body = { daBat: false, webClientId: '' };
          else if (url.pathname === '/api/danh-muc/chi-nhanh') body = [1, 2, 3].map(id => ({ maChiNhanh: id, tenChiNhanh: `Chi nhánh ${id}`, diaChi: 'Địa chỉ kiểm thử' }));
          else if (url.pathname === '/api/don-hang/hang-doi') body = mode === 'operations-empty' ? [] : fixtureQueue(selected);
          else if (url.pathname === '/api/quan-tri/may-in') body = (selected ? [selected] : [1, 2, 3]).map(id => ({ maMayIn: id, maChiNhanh: id, tenChiNhanh: `Chi nhánh ${id}`, tenMay: `Máy in ${id}`, khoGiay: 'A4', tocDo: 30, trangThai: 'HOAT_DONG' }));
          else if (url.pathname === '/api/may-in-ao/trang-thai') body = [1, 2].filter(id => !selected || id === selected).map(id => ({ maMayIn: id, trangThai: 'SAN_SANG', lanCuoiPhanHoi: new Date().toISOString() }));
          else if (url.pathname === '/api/quan-tri/kho') {
            if (mode === 'warehouse-error') return route.fulfill({ status: 503, headers: { 'Access-Control-Allow-Origin': '*' } });
            body = [{ maChiNhanh: selected || 1, tenChiNhanh: `Chi nhánh ${selected || 1}`, maVatTu: 1, tenVatTu: 'Giấy A4 thiếu', soLuong: 20, mucToiThieu: 50, donViTinh: 'tờ', canhBao: true }];
          }
          else if (url.pathname === '/api/quan-tri/phieu-nhap') body = [{ maPhieuNhap: 1, maChiNhanh: selected || 1, tenChiNhanh: `Chi nhánh ${selected || 1}`, tenVatTu: 'Giấy A4', soLuong: 500, trangThai: 'CHO_DUYET', tenNguoiLap: 'Quản lý thử', ngayLap: '2026-10-13T00:00:00' }];
          else if (url.pathname === '/api/ca/dang-mo') return route.fulfill({ status: 204, headers: { 'Access-Control-Allow-Origin': '*' } });
          else if (url.pathname.startsWith('/hubs/')) return route.fulfill({ status: 503, headers: { 'Access-Control-Allow-Origin': '*' } });
          else throw new Error(`API chưa khai báo fixture: ${url.pathname}`);
          return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } });
        }
        if (url.hostname === 'mail.google.com') return route.fulfill({ status: 200, contentType: 'text/html', body: '<h1>Gmail compose fixture</h1>' });
        if (url.origin !== base && !url.hostname.endsWith('googleapis.com') && !url.hostname.endsWith('gstatic.com')) return route.abort();
        return route.continue();
      });
      const page = await context.newPage();
      page.on('pageerror', error => errors.push(error.message));
      const consoleErrors = [];
      page.on('console', msg => { if (msg.type() === 'error') consoleErrors.push(msg.text()); });
      await page.clock.install();
      const noOverflow = async () => assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1 && document.querySelector('main').scrollWidth <= document.querySelector('main').clientWidth + 1), `Tràn ngang ở ${width}px`);
      try {
        await page.goto(base + '/tong-quan');
        const business = page.getByRole('tab', { name: 'Kinh doanh', exact: true });
        const operations = page.getByRole('tab', { name: 'Vận hành', exact: true });
        const apply = page.getByRole('button', { name: 'Xem tổng quan', exact: true });
        const detail = page.getByRole('region', { name: 'Chi tiết doanh thu', exact: true });
        await expect(business).toHaveAttribute('aria-selected', 'true');
        await expect(page.getByRole('heading', { name: 'Giá trị trung bình/đơn', exact: true })).toBeVisible();
        await expect(page.getByLabel('Từ ngày', { exact: true })).toHaveValue(/^[0-9]{2}\/[0-9]{2}\/[0-9]{4}$/);
        await expect(page.getByRole('heading', { name: 'Cơ cấu dịch vụ', exact: true })).toBeVisible();
        await expect.poll(async () => Math.round((await page.getByRole('complementary', { name: 'Điều hướng chính', exact: true }).boundingBox()).width)).toBe(325);
        const header = page.getByRole('banner', { name: 'Thanh điều hướng', exact: true });
        const footer = page.getByRole('contentinfo', { name: 'Thông tin hệ thống', exact: true });
        await expect(header).toHaveCount(1);
        await expect(header.getByText('Tổng quan', { exact: true })).toHaveCount(1);
        await expect(footer).not.toContainText('CNTT-KLCN031');
        await expect(footer.getByRole('link', { name: 'Email liên hệ: manh27062005@gmail.com', exact: true })).toHaveAttribute('href', 'https://mail.google.com/mail/?view=cm&fs=1&to=manh27062005%40gmail.com');
        await expect(footer.getByRole('button', { name: 'Điện thoại: 0769479813', exact: true })).toHaveAttribute('aria-haspopup', 'dialog');
        const gmailPromise = context.waitForEvent('page');
        await footer.getByRole('link', { name: 'Email liên hệ: manh27062005@gmail.com', exact: true }).click();
        const gmail = await gmailPromise;
        await expect(gmail.getByRole('heading', { name: 'Gmail compose fixture' })).toBeVisible();
        assert.equal(new URL(gmail.url()).searchParams.get('to'), 'manh27062005@gmail.com');
        await gmail.close();
        const phone = footer.getByRole('button', { name: 'Điện thoại: 0769479813', exact: true });
        await phone.click();
        const dialog = page.getByRole('dialog', { name: 'Liên hệ qua điện thoại' });
        await expect(dialog).toBeVisible();
        await expect(dialog.getByLabel('Số điện thoại')).toBeFocused();
        await expect(dialog.getByRole('link', { name: 'Gọi bằng ứng dụng' })).toHaveAttribute('href', 'tel:0769479813');
        await dialog.getByRole('button', { name: 'Sao chép số', exact: true }).click();
        await expect(dialog.getByRole('status')).toHaveText('Đã sao chép số điện thoại.');
        assert.equal(await page.evaluate(() => window.fixtureCopiedText), '0769479813');
        await page.evaluate(() => window.fixtureClipboardError = true);
        await dialog.getByRole('button', { name: 'Sao chép số', exact: true }).click();
        await expect(dialog.getByRole('status')).toContainText('sao chép thủ công');
        await page.evaluate(() => window.fixtureClipboardError = false);
        for (let i = 0; i < 6; i++) {
          await page.keyboard.press('Tab');
          // Chrome có thể chuyển focus tới thanh địa chỉ sau phần tử cuối.
          // Không được chuyển tới bất kỳ phần tử tương tác nào của trang nền.
          assert.ok(await dialog.evaluate(el => el.contains(document.activeElement) || document.activeElement === document.body), 'Không được focus vào trang nền của dialog.');
        }
        await page.keyboard.press('Escape');
        await expect(dialog).not.toBeVisible(); await expect(phone).toBeFocused();
        await phone.click(); await dialog.getByRole('button', { name: 'Đóng liên hệ' }).click();
        await expect(dialog).not.toBeVisible();
        await expect(footer).toContainText('In rõ nét — Nhận đúng hẹn.');
        await expect(footer).toBeVisible();
        const footerTop = (await footer.boundingBox()).y;
        await page.getByRole('main').evaluate(el => el.scrollTop = el.scrollHeight);
        assert.ok(Math.abs((await footer.boundingBox()).y - footerTop) < 1, 'Footer không được trượt theo vùng nội dung.');
        assert.ok(await footer.evaluate(el => {
          const work = el.parentElement, main = el.previousElementSibling;
          return getComputedStyle(el).position === 'static'
            && main.tagName === 'MAIN'
            && el.getBoundingClientRect().top >= main.getBoundingClientRect().bottom - 1
            && el.getBoundingClientRect().bottom <= work.getBoundingClientRect().bottom + 1
            && el.getBoundingClientRect().bottom <= innerHeight + 1;
        }), 'Footer phải cố định dưới vùng nội dung, không che thao tác.');
        await footer.screenshot({ animations: 'disabled', path: path.join(output, `footer-bar-${width}.png`) });
        await footer.getByRole('link', { name: 'Email liên hệ: manh27062005@gmail.com', exact: true }).focus();
        await page.keyboard.press('Tab');
        await expect(footer.getByRole('button', { name: 'Điện thoại: 0769479813', exact: true })).toBeFocused();
        if (width === 390) {
          await page.setViewportSize({ width, height: 640 });
          assert.ok(await footer.evaluate(el => el.getBoundingClientRect().bottom <= innerHeight + 1));
          assert.ok((await footer.boundingBox()).height <= 150, 'Footer điện thoại phải gọn, không chiếm quá nhiều chiều cao.');
          assert.ok(await page.getByRole('main').evaluate(el => el.clientHeight >= 250), 'Footer không được chiếm hết vùng làm việc.');
          await noOverflow();
          await page.screenshot({ animations: 'disabled', path: path.join(output, 'footer-390-compact.png') });
          await page.setViewportSize({ width, height: 1000 });
        }
        await noOverflow();
        await page.screenshot({ animations: 'disabled', path: path.join(output, `footer-${width}.png`) });
        const sidebar = page.getByRole('complementary', { name: 'Điều hướng chính', exact: true });
        if (width < 1024) await header.getByRole('button', { name: 'Mở menu điều hướng', exact: true }).click();
        else {
          const toggle = header.getByRole('button', { name: 'Thu gọn hoặc mở rộng menu', exact: true });
          await toggle.click(); await expect(toggle).toHaveAttribute('aria-expanded', 'false');
          await toggle.click(); await expect(toggle).toHaveAttribute('aria-expanded', 'true');
        }
        for (const name of ['Dashboard', 'Vận hành', 'Ca & tiền', 'Quản lý', 'Cấu hình']) {
          const group = sidebar.getByRole('button', { name, exact: true });
          if (await group.getAttribute('aria-expanded') !== 'true') await group.click();
        }
        for (const name of ['Dashboard', 'Vận hành', 'Ca & tiền', 'Quản lý', 'Cấu hình'])
          await expect(sidebar.getByRole('button', { name, exact: true })).toHaveAttribute('aria-expanded', 'true');
        const shapes = await sidebar.getByRole('navigation').locator('svg').evaluateAll(ds => ds.map(x => x.innerHTML));
        assert.equal(shapes.length, new Set(shapes).size, 'Mỗi nhóm/mục sidebar phải có hình icon riêng.');
        await sidebar.screenshot({ animations: 'disabled', path: path.join(output, `sidebar-${width}.png`) });
        if (width < 1024) await sidebar.getByRole('button', { name: 'Đóng menu', exact: true }).click();
        await page.getByRole('tab', { name: 'Kinh doanh', exact: true }).scrollIntoViewIfNeeded();
        const filterBox = await page.getByRole('region', { name: 'Bộ lọc tổng quan', exact: true }).boundingBox();
        const kpiBox = await page.getByLabel('Chỉ số doanh thu').boundingBox();
        assert.ok(filterBox.width >= kpiBox.width - 2);
        await noOverflow();
        await page.screenshot({ animations: 'disabled', path: path.join(output, `dashboard-${width}.png`) });
        await page.getByRole('heading', { name: 'Xu hướng doanh thu', exact: true }).scrollIntoViewIfNeeded();
        assert.ok(await page.getByRole('img', { name: 'Xu hướng doanh thu kỳ đã chọn so với cùng số ngày liền trước', exact: true }).evaluate(svg => {
          const box = svg.getBoundingClientRect();
          return [...svg.querySelectorAll('text')].every(text => { const r = text.getBoundingClientRect(); return r.left >= box.left - 1 && r.right <= box.right + 1; });
        }), 'Nhãn biểu đồ không được bị cắt.');
        await page.screenshot({ animations: 'disabled', path: path.join(output, `chart-${width}.png`) });
        await expect(page.getByRole('heading', { name: 'Giờ cao điểm', exact: true })).toHaveCount(0);
        await expect(page.getByRole('heading', { name: 'Hiệu quả & trải nghiệm khách hàng', exact: true })).toHaveCount(0);
        await expect(detail).toHaveCount(0);
        await page.getByRole('heading', { name: 'Cơ cấu dịch vụ', exact: true }).scrollIntoViewIfNeeded();
        await noOverflow();
        await page.screenshot({ animations: 'disabled', path: path.join(output, `analysis-${width}.png`) });
        // Preset tự áp dụng, không cần bấm nút lần nữa.
        await page.getByLabel('Khoảng thời gian', { exact: true }).selectOption('ngay');
        await expect(page.getByLabel('Chỉ số doanh thu').getByText('600.000₫', { exact: true })).toHaveCount(1);
        mode = 'previous-zero';
        await apply.click();
        await expect(page.getByText('Kỳ đối chiếu chưa có doanh thu', { exact: true })).toBeVisible();
        mode = 'normal';
        for (const group of ['tuan', 'thang', 'ngay']) {
          await page.getByLabel('Nhóm báo cáo', { exact: true }).selectOption(group);
          await apply.click();
          await expect(apply).toBeEnabled();
          await noOverflow();
        }
        const to = new Date(), from = new Date(); from.setDate(to.getDate() - 24);
        const viDate = day => date(day).split('-').reverse().join('/');
        await page.getByLabel('Từ ngày', { exact: true }).fill(viDate(from));
        await page.getByLabel('Đến ngày', { exact: true }).fill(viDate(to));
        await apply.click();
        await expect(apply).toBeEnabled();
        await expect(detail).toHaveCount(0);
        await expect(page.getByLabel('Số dòng báo cáo')).toHaveCount(0);
        await expect(page.getByRole('img', { name: 'Xu hướng doanh thu kỳ đã chọn so với cùng số ngày liền trước', exact: true })).toBeVisible();
        await page.getByLabel('Từ ngày', { exact: true }).fill('31/02/2026');
        const beforeBadDate = apiCalls.length;
        await apply.click();
        await expect(page.getByRole('alert')).toContainText('Nhập ngày đúng định dạng');
        assert.equal(apiCalls.length, beforeBadDate);
        await page.getByLabel('Khoảng thời gian', { exact: true }).selectOption('ngay');
        await expect(page.getByRole('heading', { name: 'Doanh thu kỳ đã chọn', exact: true })).toBeVisible();
        mode = 'analysis-error';
        await apply.click();
        await expect(page.getByRole('alert')).toContainText('Không tải được phân tích');
        await expect(page.getByLabel('Chỉ số doanh thu')).toBeVisible();
        mode = 'normal';
        await page.getByRole('button', { name: 'Thử lại phân tích', exact: true }).click();
        await expect(page.getByRole('heading', { name: 'Cơ cấu dịch vụ', exact: true })).toBeVisible();
        if (width >= 1366) {
          await page.getByLabel('Chọn phạm vi chi nhánh').selectOption('2');
          await expect.poll(() => apiCalls.some(x => x.path.includes('doanh-thu') && x.selected === 2)).toBe(true);
          await expect(page.getByRole('heading', { name: 'Doanh thu kỳ đã chọn', exact: true })).toBeVisible();
          await expect(page.getByRole('region', { name: 'So sánh doanh thu chi nhánh', exact: true })).toHaveCount(0);
          assert.ok(apiCalls.some(x => x.path.includes('doanh-thu') && x.selected === 2));
        }
        mode = 'empty'; await apply.click();
        await expect(page.getByText(/Chưa có đơn hoàn tất trong kỳ/)).toBeVisible();
        mode = 'error'; await apply.click();
        await expect(page.getByRole('alert')).toContainText('Không tải được tổng quan');
        await expect(page.getByLabel('Chỉ số doanh thu')).toHaveCount(0);
        mode = 'normal'; await apply.click();
        // Điều hướng tab bằng bàn phím.
        await business.focus(); await page.keyboard.press('ArrowRight');
        await expect(operations).toHaveAttribute('aria-selected', 'true');
        await expect(operations).toBeFocused();
        await expect(page.getByLabel('Chờ tiếp nhận: 3 đơn', { exact: true })).toBeVisible();
        await expect(page.getByRole('region', { name: 'Ca và phê duyệt', exact: true })).toContainText('Chưa có');
        await page.getByRole('heading', { name: 'Tình trạng máy in', exact: true }).scrollIntoViewIfNeeded();
        await noOverflow();
        await page.screenshot({ animations: 'disabled', path: path.join(output, `operations-${width}.png`) });
        mode = 'warehouse-error';
        await page.getByRole('button', { name: 'Làm mới vận hành', exact: true }).click();
        await expect(page.getByRole('region', { name: 'Cảnh báo vật tư', exact: true })).toContainText('Không tải được tồn kho');
        await expect(page.getByLabel('Chờ tiếp nhận: 3 đơn', { exact: true })).toBeVisible();
        mode = 'normal';
        await page.getByRole('button', { name: 'Làm mới vận hành', exact: true }).click();
        await expect(page.getByText('Giấy A4 thiếu', { exact: true })).toBeVisible();
        if (width === 1366) {
          const beforeTick = apiCalls.filter(x => x.path === '/api/don-hang/hang-doi').length;
          const stockCalls = apiCalls.filter(x => x.path === '/api/quan-tri/kho').length;
          await page.clock.fastForward(60000);
          await expect.poll(() => apiCalls.filter(x => x.path === '/api/don-hang/hang-doi').length).toBeGreaterThan(beforeTick);
          assert.equal(apiCalls.filter(x => x.path === '/api/quan-tri/kho').length, stockCalls, 'Không quét kho theo timer.');
        }
        mode = 'operations-empty';
        await page.getByRole('button', { name: 'Làm mới vận hành', exact: true }).click();
        await expect(page.getByText('Không có đơn cần ưu tiên.', { exact: true })).toBeVisible();
        await expect(page.getByRole('heading', { name: 'Ưu tiên xử lý', exact: true })).toHaveCount(0);
        mode = 'normal'; await business.click();
        const afterDispose = apiCalls.filter(x => x.path === '/api/don-hang/hang-doi').length;
        if (width === 1366) {
          await page.clock.fastForward(120000);
          assert.equal(apiCalls.filter(x => x.path === '/api/don-hang/hang-doi').length, afterDispose);
        }
        if (width < 1024) await header.getByRole('button', { name: 'Mở menu điều hướng', exact: true }).click();
        const operationMenu = sidebar.getByRole('button', { name: 'Vận hành', exact: true });
        if (await operationMenu.getAttribute('aria-expanded') !== 'true') await operationMenu.click();
        await sidebar.getByRole('link', { name: 'Hàng đợi đơn', exact: true }).click();
        await expect(header.getByText('Hàng đợi đơn hàng', { exact: true })).toHaveCount(1);
        await expect(page.getByRole('heading', { name: 'Đơn đang chờ tiếp nhận' })).toBeVisible();
        await page.getByPlaceholder('Tìm mã đơn, tên khách, SĐT…').fill('khong-co-don');
        await expect(page.getByText('Không có đơn nào phù hợp.')).toBeVisible();
        await page.getByRole('button', { name: 'Xem đơn chờ', exact: true }).click();
        await expect(page.getByRole('table').getByRole('row')).toHaveCount(4);
        await noOverflow();
        await kiemTraDanhMuc({ page, base, fixture: danhMucFixture, width, output, noOverflow });
        await page.evaluate(() => localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'branch-fixture', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: { maNguoiDung: 2, hoTen: 'Quản lý thử', tenVaiTro: 'BRANCH_ADMIN', maChiNhanh: 1, tenChiNhanh: 'Chi nhánh 1' } })));
        await page.goto(base + '/tong-quan');
        await expect(operations).toHaveAttribute('aria-selected', 'true');
        await expect(page.getByLabel('Chờ tiếp nhận: 3 đơn', { exact: true })).toBeVisible();
        await business.click();
        await expect(page.getByRole('region', { name: 'So sánh doanh thu chi nhánh', exact: true })).toHaveCount(0);
        if (width < 1024) await header.getByRole('button', { name: 'Mở menu điều hướng', exact: true }).click();
        if (await operationMenu.getAttribute('aria-expanded') !== 'true') await operationMenu.click();
        await sidebar.getByRole('link', { name: 'Hàng đợi đơn', exact: true }).click();
        await expect(header.getByText('Hàng đợi đơn hàng', { exact: true })).toHaveCount(1);
        await page.goto(base + '/quan-tri');
        await expect(page.getByRole('heading', { name: 'Mạng lưới chi nhánh & vùng phục vụ' })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Thêm chi nhánh', exact: true })).toHaveCount(0);
        await expect(page.getByRole('button', { name: 'Thêm máy in', exact: true })).toHaveCount(0);
        await page.goto(base + '/dich-vu');
        await expect(page.getByRole('heading', { name: 'Danh mục dịch vụ', exact: true })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Thêm dịch vụ', exact: true })).toHaveCount(0);
        await page.goto(base + '/khach-hang');
        await expect(page.getByRole('heading', { name: 'Không có quyền truy cập' })).toBeVisible();
        await page.evaluate(() => localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'ui-fixture-not-a-real-token', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: { maNguoiDung: 3, hoTen: 'Nhân viên thử', tenVaiTro: 'STAFF', maChiNhanh: 1, tenChiNhanh: 'Chi nhánh 1' } })));
        await page.goto(base + '/quan-tri');
        await expect(page.getByRole('button', { name: 'Thêm chi nhánh', exact: true })).toHaveCount(0);
        await expect(page.getByRole('button', { name: 'Thêm máy in', exact: true })).toHaveCount(0);
        await page.goto(base + '/dich-vu');
        await expect(page.getByRole('heading', { name: 'Không có quyền truy cập' })).toBeVisible();
        await page.goto(base + '/khach-hang');
        await expect(page.getByRole('heading', { name: 'Không có quyền truy cập' })).toBeVisible();
        const previousCount = apiCalls.filter(x => x.path.includes('bao-cao')).length;
        await page.goto(base + '/tong-quan');
        await expect(page.getByRole('heading', { name: 'Không có quyền truy cập' })).toBeVisible();
        assert.equal(apiCalls.filter(x => x.path.includes('bao-cao')).length, previousCount);
        await page.evaluate(() => {
          localStorage.setItem('fixture-anonymous', '1');
          localStorage.removeItem('photocopychain.phien'); sessionStorage.clear();
        });
        await page.goto(base + '/dang-nhap');
        await expect(page.getByRole('heading', { name: 'Chào mừng trở lại', exact: true })).toBeVisible();
        await expect(header).toHaveCount(0); await expect(footer).toHaveCount(0);
        await noOverflow();
        await page.goto(base + '/quen-mat-khau');
        await expect(page.getByRole('heading', { name: 'Quên mật khẩu', exact: true })).toBeVisible();
        await expect(header).toHaveCount(0); await expect(footer).toHaveCount(0);
        await noOverflow();
        assert.deepEqual(errors, []);
        assert.deepEqual(consoleErrors.filter(x => /Unhandled exception|crit: Microsoft/i.test(x)), []);
        results.push({ width, status: 'PASS', scenarios: 'CRUD danh mục fixture, Gmail popup/phone dialog/copy/focus, sidebar unique icons/independent groups/drawer/collapse, header SPA navigation/footer/auth layout, dashboard regression/roles/errors/keyboard/presets/chart/no revenue detail table, timer at 1366' });
      } catch (error) {
        await page.screenshot({ animations: 'disabled', path: path.join(output, `failure-${width}.png`), fullPage: true });
        console.error(consoleErrors.filter(x => /Unhandled|Exception|critical/i.test(x)).join('\n'));
        throw error;
      } finally { await context.close(); }
    }
    if (process.env.VERIFY_FORM_ONLY !== '1') results.push(...await kiemTraGoogle(browser, base, output));
    if (process.env.VERIFY_GOOGLE_ONLY !== '1') results.push(...await kiemTraQuyCach(browser, base, output));
    console.log(JSON.stringify({ results, screenshots: output, note: 'API/Google fixture only, no real DB/payment writes' }, null, 2));
  } finally { await browser.close(); }
}
run().catch(error => { console.error(error); process.exitCode = 1; }).finally(() => server.close());
