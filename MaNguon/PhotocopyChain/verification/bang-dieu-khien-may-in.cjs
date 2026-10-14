// Chặn mọi API và mọi thao tác ghi. Không thanh toán/in hoặc sửa dữ liệu thật.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

async function main() {
  const base = process.env.PHOTOCOPY_QA_BASE || 'http://localhost:5066';
  const output = process.env.PHOTOCOPY_QA_OUTPUT;
  assert.ok(output); fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const results = [];
  try {
    for (const width of [1440, 768, 390]) {
      const context = await browser.newContext({ viewport: { width, height: 1000 } });
      await context.tracing.start({ screenshots: true, snapshots: true });
      const page = await context.newPage(), errors = [], writes = [];
      page.on('pageerror', e => errors.push(e.message));
      const nd = { maNguoiDung: 3, hoTen: 'Nhân viên kiểm thử', tenVaiTro: 'STAFF', maChiNhanh: 1, tenChiNhanh: 'Chi nhánh 1' };
      const machines = [
        { maMayIn: 1, tenMay: 'Ricoh MP 2001L', tenChiNhanh: 'Chi nhánh 1', khoGiay: 'A3', tocDo: 20, soTrangDaIn: 199, in2Mat: true },
        { maMayIn: 2, tenMay: 'Canon mạng (fixture)', tenChiNhanh: 'Chi nhánh 1', khoGiay: 'A3', tocDo: 25, soTrangDaIn: 100, in2Mat: true, inMau: true }
      ];
      const states = [
        { maMayIn: 1, cheDo: 'AO', trangThai: 'DANG_IN', maDonHangDangIn: 6, tongTrang: 100, trangDaIn: 25, soGiayConLai: 200, tenFileDangIn: 'Tài liệu thử.pdf', quyCach: 'A4 · Một mặt · 1 bản', tamDung: false },
        { maMayIn: 2, cheDo: 'THAT', tenHangDoi: 'Canon LAN', trangThai: 'CHO_XAC_NHAN', maDonHangDangIn: 7, tongTrang: 8, trangDaIn: null, thongDiep: 'Đã gửi PDF; đối chiếu bản giấy.', tamDung: false }
      ];
      let fail = false;
      await context.addInitScript(nd => localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'fixture-not-real', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: nd })), nd);
      await context.route('**/*', async route => {
        const req = route.request(), url = new URL(req.url());
        if (!url.pathname.startsWith('/api/') && !url.pathname.startsWith('/hubs/')) return route.continue();
        const tra = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } });
        if (req.method() === 'OPTIONS') return tra({});
        if (url.pathname === '/api/auth/me') return tra(nd);
        if (url.pathname === '/api/may-in-ao/danh-sach') return tra(machines);
        if (url.pathname === '/api/may-in-ao/trang-thai') return tra(states, fail ? 503 : 200);
        if (req.method() === 'POST' && /\/api\/may-in-ao\/\d+\/dieu-khien$/.test(url.pathname)) {
          const body = req.postDataJSON(), id = Number(url.pathname.split('/')[3]);
          const state = states.find(x => x.maMayIn === id);
          writes.push({ id, ...body });
          if (body.thaoTac === 'TAM_DUNG') { state.tamDung = true; state.trangThai = 'TAM_DUNG'; }
          else if (body.thaoTac === 'TIEP_TUC') { state.tamDung = false; state.trangThai = 'DANG_IN'; }
          else if (body.thaoTac === 'KHOI_PHUC') state.trangThai = 'DANG_IN';
          else if (body.thaoTac === 'XAC_NHAN_BAN_GIAY') { assert.equal(body.maDonHang, 7); state.trangThai = 'SAN_SANG'; state.maDonHangDangIn = null; }
          else state.trangThai = body.thaoTac;
          return tra({ message: 'Đã gửi fixture.' });
        }
        // Không request ghi nào được ra ngoài fixture.
        assert.equal(req.method(), 'GET', 'Request ghi ngoài phạm vi kiểm thử!');
        return tra([]);
      });
      try {
        await page.goto(base + '/may-in');
        await expect(page.getByRole('heading', { name: 'Bảng điều khiển máy in', exact: true })).toBeVisible();
        const console = page.getByRole('region', { name: 'Điều khiển máy đã chọn' });
        await expect(console).toContainText('25 / 100 trang');
        await console.getByRole('button', { name: 'Tạm dừng', exact: true }).click();
        await expect(console.getByRole('button', { name: 'Tiếp tục', exact: true })).toBeEnabled({ timeout: 10000 });
        await console.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
        await expect(console.getByRole('button', { name: 'Tạm dừng', exact: true })).toBeEnabled({ timeout: 10000 });
        await console.getByRole('button', { name: 'Kẹt giấy', exact: true }).click();
        await expect(console.getByLabel('Màn hình trạng thái')).toContainText('Kẹt giấy', { timeout: 10000 });
        await console.getByRole('button', { name: 'Nạp giấy / khôi phục', exact: true }).click();
        await expect(console.getByLabel('Màn hình trạng thái')).toContainText('Đang xử lý / in', { timeout: 10000 });
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
        await page.screenshot({ fullPage: true, path: path.join(output, `may-ao-${width}.png`) });
        await page.getByRole('button', { name: /Canon mạng/ }).click();
        await expect(console.getByRole('button', { name: 'Kẹt giấy', exact: true })).toHaveCount(0);
        const confirm = console.getByRole('button', { name: 'Xác nhận đã kiểm tra bản giấy', exact: true });
        await expect(confirm).toBeDisabled();
        await console.getByRole('checkbox', { name: /Tôi đã kiểm tra toàn bộ bản giấy/ }).check();
        await expect(confirm).toBeEnabled();
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
        await page.screenshot({ fullPage: true, path: path.join(output, `may-that-${width}.png`) });
        await confirm.click();
        await expect(console.getByRole('button', { name: 'Xác nhận đã kiểm tra bản giấy', exact: true })).toHaveCount(0, { timeout: 10000 });
        assert.ok(writes.some(x => x.thaoTac === 'XAC_NHAN_BAN_GIAY' && x.maDonHang === 7));
        fail = true;
        await page.getByRole('button', { name: 'Làm mới trạng thái' }).click();
        await expect(page.getByRole('alert')).toContainText('Không tải được trạng thái');
        await expect(console.getByRole('button', { name: 'Tạm dừng', exact: true })).toBeDisabled();
        assert.deepEqual(errors, []);
        results.push({ width, result: 'PASS', writes: writes.length });
      } catch (e) {
        await page.screenshot({ fullPage: true, path: path.join(output, `failure-${width}.png`) }); throw e;
      } finally {
        await context.tracing.stop({ path: path.join(output, `trace-${width}.zip`) });
        await context.close();
      }
    }
  } finally { await browser.close(); }
  console.log(JSON.stringify(results));
}
main().catch(e => { console.error(e.stack); process.exitCode = 1; });
