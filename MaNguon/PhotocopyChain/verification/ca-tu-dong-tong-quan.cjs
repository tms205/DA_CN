// CEO mở ca thủ công. Blazor thật, chặn mọi API bằng fixture; không ghi ca/tiền thật.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

async function main() {
  const base = process.env.PHOTOCOPY_WEB_URL || 'http://localhost:5064';
  const output = process.env.PHOTOCOPY_QA_OUTPUT || path.join(require('node:os').tmpdir(), 'PhotocopyCaTuDong');
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const results = [];
  try {
    for (const width of [1440, 768, 390]) {
      const context = await browser.newContext({ viewport: { width, height: 1000 } });
      const page = await context.newPage(), errors = [], writes = [], created = [];
      let current = null, logins = 0;
      const user = { maNguoiDung: 1, hoTen: 'CEO kiểm thử', tenVaiTro: 'SUPER_ADMIN', maChiNhanh: null };
      const login = { accessToken: 'fixture-only', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: user };
      await context.addInitScript(login => {
        if (!localStorage.getItem('fixture-started')) {
          localStorage.setItem('fixture-started', '1');
          localStorage.setItem('photocopychain.phien', JSON.stringify(login));
        }
      }, login);
      page.on('pageerror', e => errors.push(e.message));
      page.on('console', msg => { if (msg.type() === 'error' && /Unhandled exception|crit: Microsoft/i.test(msg.text())) errors.push(msg.text()); });
      await context.route('**/*', async route => {
        const req = route.request(), url = new URL(req.url());
        if (!url.pathname.startsWith('/api/') && !url.pathname.startsWith('/hubs/')) return route.continue();
        const tra = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } });
        if (req.method() === 'OPTIONS') return tra({});
        if (url.pathname.startsWith('/hubs/')) return tra({}, 503);
        const branch = Number(req.headers()['x-ma-chi-nhanh'] || 0);
        if (req.method() === 'POST' && url.pathname === '/api/auth/admin/login') {
          logins++;
          return tra(login);
        }
        if (req.method() === 'POST' && url.pathname === '/api/ca/mo') {
          assert.ok(branch > 0);
          assert.equal(req.headers().authorization, 'Bearer fixture-only');
          assert.equal(req.postDataJSON().tienDauCa, 200000);
          writes.push(branch);
          if (current && current.maChiNhanh !== branch) return tra({ message: 'Bạn đang có ca mở tại một chi nhánh khác.' }, 400);
          if (!current) {
            current = { maCa: created.length + 1, maNhanVien: 1, maChiNhanh: branch, tienDauCa: 200000, trangThai: 'DANG_MO' };
            created.push(branch);
          }
          return tra(current);
        }
        assert.equal(req.method(), 'GET', 'Chặn API ghi không khai báo: ' + url.pathname);
        if (url.pathname === '/api/auth/google/config') return tra({ daBat: false, webClientId: '' });
        if (url.pathname === '/api/danh-muc/chi-nhanh') return tra([1, 2].map(id => ({ maChiNhanh: id, tenChiNhanh: `Chi nhánh ${id}` })));
        if (url.pathname === '/api/quan-tri/bao-cao/doanh-thu') return tra([{ ngay: url.searchParams.get('tuNgay') + 'T00:00:00', maChiNhanh: branch || 1, tenChiNhanh: `Chi nhánh ${branch || 1}`, soDon: 1, doanhThu: 30000 }]);
        if (url.pathname === '/api/quan-tri/bao-cao/phan-tich') return tra({ doanhThu: 30000, tienHang: 30000, dichVu: [{ ten: 'In đen trắng', soTien: 30000, soDon: 1 }], thanhToan: [{ ten: 'VietQR', soTien: 30000, soDon: 1 }] });
        if (url.pathname === '/api/quan-tri/bao-cao/hien-tai') return tra({ docLuc: new Date().toISOString(), soPhieuChoDuyet: 0, soHoanTienChoDuyet: 0 });
        if (url.pathname === '/api/ca/dang-mo') return current && current.maChiNhanh === branch ? tra(current) : route.fulfill({ status: 204, headers: { 'Access-Control-Allow-Origin': '*' } });
        if (['/api/ca', '/api/ca/cho-xac-nhan'].includes(url.pathname)) return tra([]);
        if (['/api/quan-tri/phieu-nhap', '/api/quan-tri/hoan-tien'].includes(url.pathname)) return tra([]);
        throw new Error('API chưa khai báo fixture: ' + url.pathname);
      });
      const checkDashboard = async () => {
        await expect(page.getByRole('heading', { name: 'Doanh thu kỳ đã chọn', exact: true })).toBeVisible();
        await expect(page.getByRole('region', { name: 'Chi tiết doanh thu', exact: true })).toHaveCount(0);
        await expect(page.getByRole('link', { name: 'Báo cáo chi nhánh →', exact: true })).toHaveCount(1);
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1 && document.querySelector('main').scrollWidth <= document.querySelector('main').clientWidth + 1), `Tràn ngang ở ${width}px`);
      };
      const choose = async value => {
        await Promise.all([
          page.waitForEvent('domcontentloaded'),
          page.getByLabel('Chọn phạm vi chi nhánh').selectOption(value)
        ]);
        await checkDashboard();
      };
      const moThuCong = async () => {
        await page.goto(base + '/ca-lam-viec');
        await page.getByRole('button', { name: 'Mở ca làm việc', exact: true }).click();
        const dialog = page.getByRole('dialog', { name: 'Mở ca làm việc', exact: true });
        await expect(dialog.getByRole('spinbutton')).toHaveValue('200000');
        await dialog.getByRole('button', { name: 'Mở ca', exact: true }).click();
      };
      try {
        await page.goto(base + '/tong-quan');
        await checkDashboard();
        assert.deepEqual(writes, [], 'Toàn chuỗi không mở ca.');
        await choose('2');
        assert.deepEqual(writes, [], 'Chọn chi nhánh không tự mở ca.');
        await page.reload(); await checkDashboard();
        assert.deepEqual(writes, [], 'F5 không tự mở ca.');
        await moThuCong();
        await expect(page.getByRole('dialog', { name: 'Mở ca làm việc', exact: true })).not.toBeVisible();
        assert.deepEqual(created, [2]);
        current.tienDauCa = 350000; // Ca có sẵn không bị đặt lại tiền khi dùng lại.
        await page.goto(base + '/tong-quan'); await checkDashboard();
        assert.deepEqual(writes, [2], 'F5 cùng tab không mở thêm ca.');
        await choose(''); await choose('2');
        assert.deepEqual(writes, [2]);
        assert.deepEqual(created, [2], 'Dùng lại đúng ca, không tạo trùng.');
        assert.equal(current.tienDauCa, 350000);
        current = null; // Mô phỏng người dùng đã chủ động đóng ca.
        await page.reload(); await checkDashboard();
        assert.deepEqual(writes, [2], 'Không tự mở lại ca đã đóng khi F5.');
        if (width < 1024) await page.getByRole('button', { name: 'Mở menu điều hướng', exact: true }).click();
        await page.getByRole('button', { name: 'Đăng xuất', exact: true }).click();
        await expect(page.getByRole('heading', { name: 'Chào mừng trở lại', exact: true })).toBeVisible();
        await page.goto(base + '/dang-nhap?returnUrl=tong-quan');
        await page.getByLabel('Email hoặc số điện thoại').fill('ceo.test@gmail.com');
        await page.getByLabel('Mật khẩu', { exact: true }).fill('Test#1234');
        await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
        await checkDashboard();
        assert.equal(logins, 1);
        assert.deepEqual(writes, [2], 'Đăng nhập lại cũng không tự mở ca CEO.');
        await moThuCong();
        await expect(page.getByRole('dialog', { name: 'Mở ca làm việc', exact: true })).not.toBeVisible();
        await page.goto(base + '/tong-quan'); await checkDashboard();
        await choose('1');
        assert.deepEqual(writes, [2, 2], 'Đổi chi nhánh không tự mở hoặc đóng ca cũ.');
        await moThuCong();
        const dialog = page.getByRole('dialog', { name: 'Mở ca làm việc', exact: true });
        await expect(dialog.getByRole('alert')).toContainText('Bạn đang có ca mở tại một chi nhánh khác.');
        assert.deepEqual(created, [2, 2], 'Không tạo ca ở chi nhánh khác khi ca cũ chưa đóng.');
        current = null;
        await dialog.getByRole('button', { name: 'Mở ca', exact: true }).click();
        await expect(dialog).not.toBeVisible();
        assert.deepEqual(created, [2, 2, 1]);
        await page.goto(base + '/tong-quan'); await checkDashboard();
        await choose('');
        assert.deepEqual(writes, [2, 2, 1, 1]);
        await page.screenshot({ animations: 'disabled', path: path.join(output, `tong-quan-ca-tu-dong-${width}.png`) });
        assert.deepEqual(errors, []);
        results.push({ width, status: 'PASS', scenarios: 'Chọn chi nhánh/F5/đăng nhập lại không tự mở; bấm thủ công mới mở; giữ ca cũ và chặn ca khác chi nhánh; responsive' });
      } catch (error) {
        await page.screenshot({ path: path.join(output, `ca-tu-dong-failure-${width}.png`), fullPage: true });
        throw error;
      } finally { await context.close(); }
    }
    console.log(JSON.stringify({ results, note: 'UI và API fixture, không ghi database thật.' }, null, 2));
  } finally { await browser.close(); }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
