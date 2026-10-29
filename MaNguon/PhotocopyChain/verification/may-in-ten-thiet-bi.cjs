// Chặn mọi API/SignalR: không sửa liên kết, không tạo đơn và không in giấy thật.
const { chromium } = require('playwright');
const { expect } = require('playwright/test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

(async () => {
  const output = process.env.PHOTOCOPY_QA_OUTPUT;
  assert.ok(output); fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const results = [];
  try {
    for (const role of ['SUPER_ADMIN', 'BRANCH_ADMIN', 'STAFF']) {
      for (const width of [1440, 390]) {
        const context = await browser.newContext({ viewport: { width, height: 1000 } });
        await context.tracing.start({ screenshots: true, snapshots: true });
        const page = await context.newPage(), errors = [];
        page.on('pageerror', e => errors.push(e.message));
        const user = { maNguoiDung: 1, hoTen: 'Nhân sự fixture', tenVaiTro: role, maChiNhanh: 1, tenChiNhanh: 'Chi nhánh fixture' };
        const queue = { ten: 'HP LaserJet MFP M236sdw fixture', driver: 'Driver fixture', cong: 'WSD-fixture', laMayAo: false, inMau: false, inHaiMat: true, khoGiayHoTro: ['A4'] };
        const ketNoi = { cheDo: 'THAT', tenHangDoi: queue.ten, trucTuyen: true, hangDoi: queue };
        const machine = { maMayIn: 1, tenMay: 'Ricoh danh mục cũ', tenChiNhanh: user.tenChiNhanh, maChiNhanh: 1, khoGiay: 'A3', inMau: false, in2Mat: true, tocDo: 20, dapUngDuoc: true, ketNoi };
        const order = { maDonHang: 55, maDonHangHienThi: 'DH00055', tenKhachHang: 'Khách fixture', maChiNhanh: 1, ngayDat: '2026-10-22T09:00:00',
          hinhThucNhan: 'TAI_QUAY', phuongThucThanhToan: 'COD', trangThaiThanhToan: 'CHUA_THANH_TOAN', trangThaiDonHang: 'DA_XAC_NHAN', dongIn: [], tongTien: 1000 };
        let a3 = false;
        await context.addInitScript(user => localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'fixture-only', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: user })), user);
        await context.route('**/*', async route => {
          const req = route.request(), url = new URL(req.url());
          if (!url.pathname.startsWith('/api/') && !url.pathname.startsWith('/hubs/')) return route.continue();
          const tra = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } });
          if (req.method() === 'OPTIONS') return tra({});
          if (url.pathname.startsWith('/hubs/')) return tra({}, 503);
          assert.equal(req.method(), 'GET', `Chặn thao tác ghi/in: ${url.pathname}`);
          if (url.pathname === '/api/auth/me') return tra(user);
          if (url.pathname === '/api/danh-muc/chi-nhanh') return tra([{ maChiNhanh: 1, tenChiNhanh: user.tenChiNhanh }]);
          if (url.pathname === '/api/ca/dang-mo') return tra({ maCa: 1 });
          if (url.pathname === '/api/may-in-ao/danh-sach') return tra([machine]);
          if (url.pathname === '/api/may-in-ao/trang-thai') return tra([{ maMayIn: 1, cheDo: 'THAT', tenHangDoi: queue.ten, trangThai: 'SAN_SANG' }]);
          if (url.pathname === '/api/may-in-ao/phat-hien') return tra([{ connectorId: 'a'.repeat(32), tenMayTinh: 'PC fixture', maChiNhanh: 1, trucTuyen: true,
            lanCuoiPhatHien: new Date().toISOString(), boInPdfSanSang: true, maMayCauHinh: [1], hangDoi: [queue], lienKet: [{ maMayIn: 1, cheDo: 'THAT', tenHangDoi: queue.ten }] }]);
          if (url.pathname === '/api/don-hang/hang-doi') return tra([order]);
          if (url.pathname === '/api/don-hang/55') return tra(order);
          if (url.pathname === '/api/don-hang/55/may-in') return tra([{ ...machine, dapUngDuoc: !a3, lyDoKhongDapUng: a3 ? 'Máy in thật không hỗ trợ khổ A3; driver công bố: A4.' : null }]);
          return tra([]);
        });
        try {
          const base = process.env.PHOTOCOPY_QA_BASE || 'http://localhost:5064';
          await page.goto(base + '/may-in');
          const consolePanel = page.getByRole('region', { name: 'Điều khiển máy đã chọn' });
          await expect(consolePanel.getByRole('heading', { name: queue.ten, exact: true })).toBeVisible();
          await expect(consolePanel).toContainText('Máy thật · Windows');
          await expect(consolePanel).toContainText('Danh mục: Ricoh danh mục cũ');
          const discovery = page.getByRole('region', { name: 'Thiết bị Windows được phát hiện' });
          await expect(discovery.getByText(queue.ten, { exact: true })).toBeVisible(); // Tự mở khi đã liên kết.
          if (role !== 'SUPER_ADMIN') await expect(discovery.getByRole('button', { name: 'Liên kết máy đang chọn' })).toHaveCount(0);
          assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
          await page.screenshot({ path: path.join(output, `device-${role}-${width}.png`), fullPage: true });
          await page.goto(base + '/');
          await page.getByRole('row').filter({ hasText: 'DH00055' }).click();
          await page.getByRole('button', { name: 'Chọn máy & bắt đầu in', exact: true }).click();
          const dialog = page.getByRole('dialog', { name: 'Chọn máy & bắt đầu in' });
          await expect(dialog).toContainText(queue.ten);
          await expect(dialog).toContainText('Máy thật · Windows');
          await expect(dialog.getByRole('radio')).toBeEnabled();
          await dialog.getByRole('button', { name: 'Hủy', exact: true }).click();
          a3 = true;
          await page.getByRole('row').filter({ hasText: 'DH00055' }).click();
          await page.getByRole('button', { name: 'Chọn máy & bắt đầu in', exact: true }).click();
          await expect(dialog.getByRole('radio')).toBeDisabled();
          await expect(dialog).toContainText('không hỗ trợ khổ A3');
          await expect(dialog.getByRole('button', { name: 'Gửi lệnh tới máy in', exact: true })).toBeDisabled();
          assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
          await page.screenshot({ path: path.join(output, `selection-${role}-${width}.png`), fullPage: true });
          assert.deepEqual(errors, []); results.push({ role, width, result: 'PASS' });
        } finally {
          await context.tracing.stop({ path: path.join(output, `trace-${role}-${width}.zip`) });
          await context.close();
        }
      }
    }
  } finally { await browser.close(); }
  console.log(JSON.stringify(results));
})().catch(error => { console.error(error.stack); process.exitCode = 1; });
