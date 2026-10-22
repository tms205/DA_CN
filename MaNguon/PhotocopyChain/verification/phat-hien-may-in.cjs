// Mọi API đều được chặn/mock: KHÔNG liên kết hay in trên hệ thống thật.
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
    for (const [width, role] of [[1440, 'SUPER_ADMIN'], [768, 'SUPER_ADMIN'], [390, 'SUPER_ADMIN'], [390, 'STAFF']]) {
      const context = await browser.newContext({ viewport: { width, height: 1000 } });
      await context.tracing.start({ screenshots: true, snapshots: true });
      const page = await context.newPage(), errors = [], writes = [];
      page.on('pageerror', e => errors.push(e.message));
      const nd = { maNguoiDung: 1, hoTen: 'Kiểm thử máy in', tenVaiTro: role, maChiNhanh: 1, tenChiNhanh: 'Chi nhánh kiểm thử' };
      await context.addInitScript(nd => localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'fixture-only', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: nd })), nd);
      let fail = false;
      const connector = { connectorId: 'a'.repeat(32), tenMayTinh: 'PC-CUA-HANG fixture', trucTuyen: true, lanCuoiPhatHien: new Date().toISOString(), maChiNhanh: 1, boInPdfSanSang: true, maMayCauHinh: [1], lienKet: [], hangDoi: [
        { ten: 'HP USB fixture', driver: 'HP driver', cong: 'USB001', trangThaiDriver: 0, soJob: 0, inMau: false, inHaiMat: true, laMayAo: false },
        { ten: 'Microsoft Print to PDF', driver: 'Microsoft', cong: 'PORTPROMPT:', laMayAo: true }
      ] };
      await context.route('**/*', async route => {
        const req = route.request(), url = new URL(req.url());
        if (!url.pathname.startsWith('/api/') && !url.pathname.startsWith('/hubs/')) return route.continue();
        const tra = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } });
        if (req.method() === 'OPTIONS') return tra({});
        if (url.pathname === '/api/auth/me') return tra(nd);
        if (url.pathname === '/api/may-in-ao/danh-sach') return tra([{ maMayIn: 1, maChiNhanh: 1, tenMay: 'Ricoh fixture', tenChiNhanh: nd.tenChiNhanh, khoGiay: 'A3', tocDo: 20 }]);
        if (url.pathname === '/api/may-in-ao/trang-thai') return tra([{ maMayIn: 1, cheDo: 'AO', trangThai: 'SAN_SANG' }]);
        if (url.pathname === '/api/may-in-ao/phat-hien') return tra([connector], fail ? 503 : 200);
        if (url.pathname === '/api/may-in-ao/phat-hien/1/lien-ket' && req.method() === 'POST') {
          assert.equal(role, 'SUPER_ADMIN'); const body = req.postDataJSON();
          assert.equal(body.tenHangDoi, 'HP USB fixture'); assert.equal(body.daKiemTraThietBi, true);
          writes.push(body); return tra({ message: 'Fixture saved' });
        }
        assert.equal(req.method(), 'GET', `Blocked unexpected write: ${url.pathname}`);
        return tra([]);
      });
      try {
        await page.goto((process.env.PHOTOCOPY_QA_BASE || 'http://localhost:5066') + '/may-in');
        await expect(page.getByRole('heading', { name: 'Thiết bị Windows tại cửa hàng' })).toBeVisible();
        await page.getByText('PC-CUA-HANG fixture', { exact: true }).click();
        await expect(page.getByText('Microsoft Print to PDF', { exact: true })).toBeVisible();
        const link = page.getByRole('button', { name: 'Liên kết máy đang chọn', exact: true });
        if (role === 'SUPER_ADMIN') {
          await link.click();
          const popup = page.getByRole('dialog', { name: 'Liên kết máy in Windows' });
          await expect(popup).toBeVisible();
          await popup.getByLabel('Chế độ xử lý').selectOption('THAT');
          await expect(popup.getByLabel('Queue Windows').getByRole('option', { name: /Microsoft Print/ })).toBeDisabled();
          await popup.getByLabel('Queue Windows').selectOption('HP USB fixture');
          const save = popup.getByRole('button', { name: 'Lưu liên kết', exact: true });
          await expect(save).toBeDisabled();
          await popup.getByRole('checkbox').check(); await expect(save).toBeEnabled();
          assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
          await page.screenshot({ path: path.join(output, `popup-${width}.png`), fullPage: true });
          await save.click(); await expect(popup).not.toBeVisible();
          assert.equal(writes.length, 1);
        } else await expect(link).toHaveCount(0);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
        await page.screenshot({ path: path.join(output, `discovery-${role}-${width}.png`), fullPage: true });
        fail = true;
        await page.getByRole('button', { name: 'Cập nhật danh sách', exact: true }).click();
        await expect(page.getByRole('alert')).toContainText('Chưa tải được danh sách phát hiện');
        await expect(page.getByRole('button', { name: 'Tạm dừng', exact: true })).toBeEnabled();
        assert.deepEqual(errors, []); results.push({ width, role, result: 'PASS' });
      } catch (error) {
        await page.screenshot({ path: path.join(output, `failure-${role}-${width}.png`), fullPage: true }); throw error;
      } finally {
        await context.tracing.stop({ path: path.join(output, `trace-${role}-${width}.zip`) }); await context.close();
      }
    }
  } finally { await browser.close(); }
  console.log(JSON.stringify(results));
})().catch(e => { console.error(e.stack); process.exitCode = 1; });
