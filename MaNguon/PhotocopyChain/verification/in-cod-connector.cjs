// Cô lập mọi API: không ghi DB, không gửi PDF/thu tiền/in giấy thật.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

(async () => {
  const output = process.env.PHOTOCOPY_QA_OUTPUT;
  assert.ok(output); fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const results = [];
  try {
    for (const width of [1440, 768, 390]) {
      const context = await browser.newContext({ viewport: { width, height: 1000 } });
      await context.tracing.start({ screenshots: true, snapshots: true });
      const page = await context.newPage(), errors = [], writes = [];
      page.on('pageerror', e => errors.push(e.message));
      const nd = { maNguoiDung: 3, hoTen: 'Nhân viên fixture', tenVaiTro: 'STAFF', maChiNhanh: 1, tenChiNhanh: 'Chi nhánh fixture' };
      const don = { maDonHang: 52, maDonHangHienThi: 'DH00052', maChiNhanh: 1, tenChiNhanh: 'Chi nhánh fixture', tenKhachHang: 'Khách COD fixture', ngayDat: '2026-10-22T09:00:00',
        hinhThucNhan: 'TAI_QUAY', phuongThucThanhToan: 'COD', trangThaiThanhToan: 'CHUA_THANH_TOAN', trangThaiDonHang: 'DA_XAC_NHAN', tongTien: 12000, tienHang: 12000,
        tongSoTrang: 40, soFile: 1, dongIn: [{ tenFile: 'fixture.pdf', soTrang: 40, soBan: 1, khoGiay: 'A4', matIn: '1_MAT', huongGiay: 'DUNG', mauSac: 'DEN_TRANG', trangThaiFile: 'SAN_SANG' }] };
      let states = [{ maMayIn: 1, cheDo: 'AO', trangThai: 'SAN_SANG' }];
      await context.addInitScript(nd => localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'fixture-only', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: nd })), nd);
      await context.route('**/*', async route => {
        const req = route.request(), url = new URL(req.url());
        if (!url.pathname.startsWith('/api/') && !url.pathname.startsWith('/hubs/')) return route.continue();
        const tra = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } });
        if (req.method() === 'OPTIONS') return tra({});
        if (url.pathname.startsWith('/hubs/')) return tra({ message: 'SignalR bị cô lập, kiểm tra polling thay thế.' }, 503);
        if (url.pathname === '/api/auth/me') return tra(nd);
        if (url.pathname === '/api/don-hang/hang-doi') return tra([don]);
        if (url.pathname === '/api/don-hang/52') return tra(don);
        if (url.pathname === '/api/ca/dang-mo') return tra({ maCa: 3 });
        if (url.pathname === '/api/don-hang/52/may-in') return tra([
          { maMayIn: 1, tenMay: 'HP đang nối fixture', khoGiay: 'A4', dapUngDuoc: true },
          { maMayIn: 2, tenMay: 'Máy chưa kết nối fixture', khoGiay: 'A4', dapUngDuoc: true }
        ]);
        if (url.pathname === '/api/may-in-ao/trang-thai') return tra(states);
        if (req.method() === 'PUT' && url.pathname === '/api/don-hang/52/trang-thai-in') {
          const body = req.postDataJSON(); assert.equal(body.trangThaiMoi, 'DANG_IN'); assert.equal(body.maMayIn, 1);
          writes.push(body); don.maMayIn = 1; don.daGuiLenhIn = true; don.trangThaiDonHang = 'DANG_IN';
          states = [{ maMayIn: 1, maDonHangDangIn: 52, cheDo: 'AO', trangThai: 'DANG_IN', tongTrang: 40, trangDaIn: 10, soGiayConLai: 90 }];
          return tra({ message: 'Fixture gửi lệnh' });
        }
        assert.equal(req.method(), 'GET', `Chặn ghi ngoài fixture: ${url.pathname}`);
        return tra([]);
      });
      try {
        await page.goto((process.env.PHOTOCOPY_QA_BASE || 'http://localhost:5066') + '/');
        await page.getByRole('row').filter({ hasText: 'DH00052' }).click();
        await page.getByRole('button', { name: 'Chọn máy & bắt đầu in', exact: true }).click();
        const popup = page.getByRole('dialog', { name: 'Chọn máy & bắt đầu in' });
        await expect(popup.getByRole('radio').nth(1)).toBeDisabled();
        await popup.getByRole('button', { name: 'Gửi lệnh tới máy in', exact: true }).click();
        const progress = page.getByRole('region', { name: 'Tiến độ máy in' });
        await expect(progress).toContainText('10 / 40 trang');
        await expect(page.getByRole('button', { name: 'Xác nhận đã in xong', exact: true })).toHaveCount(0);
        assert.equal(writes.length, 1);
        states[0] = { maMayIn: 1, maDonHangDangIn: 52, cheDo: 'THAT', trangThai: 'DANG_IN', tongTrang: 40, trangDaIn: null };
        await expect(progress).toContainText('không hiển thị phần trăm giả', { timeout: 12000 });
        await expect(progress.getByRole('progressbar')).toHaveCount(0);
        states[0].trangThai = 'LOI'; states[0].loi = 'Printer unavailable fixture';
        await expect(progress).toContainText('Printer unavailable fixture', { timeout: 12000 });
        await progress.scrollIntoViewIfNeeded();
        assert.equal(don.trangThaiDonHang, 'DANG_IN');
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
        await page.screenshot({ path: path.join(output, `cod-progress-${width}.png`), fullPage: true });
        states = [{ maMayIn: 1, cheDo: 'THAT', trangThai: 'SAN_SANG' }]; don.trangThaiDonHang = 'DA_IN';
        await expect(page.getByRole('button', { name: 'Thu COD & bàn giao', exact: true })).toBeVisible({ timeout: 12000 });
        assert.equal(don.trangThaiThanhToan, 'CHUA_THANH_TOAN'); // In xong không tự thu tiền.
        // Đơn COD từ phiên bản thủ công cũ không tự in; phải xác nhận gửi, có thể hủy.
        don.trangThaiDonHang = 'DANG_IN'; don.daGuiLenhIn = false;
        await page.getByRole('row').filter({ hasText: 'DH00052' }).click();
        await page.getByRole('button', { name: 'Gửi đơn COD này tới máy đã chọn', exact: true }).click();
        const oldDialog = page.getByRole('dialog', { name: 'Gửi đơn COD cũ' });
        await expect(oldDialog).toContainText('không gửi nếu bản giấy đã in');
        assert.equal(writes.length, 1);
        await oldDialog.getByRole('button', { name: 'Hủy', exact: true }).click();
        assert.equal(writes.length, 1);
        assert.deepEqual(errors, []); results.push({ width, result: 'PASS', writes: writes.length });
      } catch (error) {
        await page.screenshot({ path: path.join(output, `failure-${width}.png`), fullPage: true }); throw error;
      } finally {
        await context.tracing.stop({ path: path.join(output, `trace-${width}.zip`) }); await context.close();
      }
    }
  } finally { await browser.close(); }
  console.log(JSON.stringify(results));
})().catch(e => { console.error(e.stack); process.exitCode = 1; });
