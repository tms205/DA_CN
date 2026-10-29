// UI thật từ bản publish, mọi API/SignalR bị chặn bằng fixture. Không in, không ghi DB.
const { chromium } = require('playwright');
const { expect } = require('playwright/test');
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const web = path.resolve(process.argv[2] || '');
const output = path.resolve(process.argv[3] || path.join(require('node:os').tmpdir(), 'PhotocopyTransferUI'));
assert.ok(fs.existsSync(path.join(web, 'index.html')), 'Cần đường dẫn wwwroot của bản publish mới.');
fs.mkdirSync(output, { recursive: true });
const mime = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.css': 'text/css', '.wasm': 'application/wasm', '.svg': 'image/svg+xml', '.png': 'image/png' };
const server = http.createServer((req, res) => {
  const relative = decodeURIComponent(new URL(req.url, 'http://fixture.local').pathname).replace(/^\/+/, '');
  let file = path.resolve(web, relative || 'index.html');
  if (!file.startsWith(web + path.sep)) { res.writeHead(403).end(); return; }
  if (!fs.existsSync(file) || !fs.statSync(file).isFile()) file = path.join(web, 'index.html');
  res.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
  fs.createReadStream(file).pipe(res);
});

(async () => {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const base = `http://127.0.0.1:${server.address().port}`;
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const results = [];
  try {
    for (const role of ['SUPER_ADMIN', 'BRANCH_ADMIN', 'STAFF']) for (const width of [1440, 390]) {
      const context = await browser.newContext({ viewport: { width, height: 1000 }, locale: 'vi-VN', timezoneId: 'Asia/Ho_Chi_Minh', serviceWorkers: 'block' });
      await context.tracing.start({ screenshots: true, snapshots: true });
      const page = await context.newPage(), errors = [], writes = [];
      page.on('pageerror', e => errors.push(e.message));
      let phase = '', ack = false, machine = 1;
      const user = { maNguoiDung: 8, hoTen: 'Nhân sự fixture', tenVaiTro: role, maChiNhanh: 1, tenChiNhanh: 'Chi nhánh fixture' };
      const row = { maChiTiet: 1, maFile: 1, tenFile: 'fixture.pdf', khoGiay: 'A4', matIn: '1_MAT', mauSac: 'DEN_TRANG', huongGiay: 'DUNG', soTrang: 5, soBan: 1 };
      const order = () => ({ maDonHang: 57, maDonHangHienThi: 'DH00057', maChiNhanh: 1, maMayIn: machine, tenMayIn: 'Máy fixture ' + machine,
        tenKhachHang: 'Khách fixture', hinhThucNhan: 'TAI_QUAY', phuongThucThanhToan: 'COD', trangThaiThanhToan: 'CHUA_THANH_TOAN',
        trangThaiDonHang: 'DANG_IN', daGuiLenhIn: true, dongIn: [row], ngayDat: '2026-10-24T09:00:00', tongTien: 1500 });
      const machines = [1, 2, 3, 4].map(id => ({ maMayIn: id, tenMay: 'Máy fixture ' + id, maChiNhanh: 1, khoGiay: 'A4', in2Mat: true,
        dapUngDuoc: id !== 3, lyDoKhongDapUng: id === 3 ? 'Không đáp ứng quy cách fixture' : null }));
      await context.addInitScript(user => localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'fixture-only', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: user })), user);
      await context.route('**/*', async route => {
        const req = route.request(), url = new URL(req.url()), p = url.pathname;
        if (!p.startsWith('/api/') && !p.startsWith('/hubs/')) return route.continue();
        const tra = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body),
          headers: { 'Access-Control-Allow-Origin': '*', 'Access-Control-Allow-Headers': '*', 'Access-Control-Allow-Methods': '*' } });
        if (req.method() === 'OPTIONS') return tra({});
        if (p.startsWith('/hubs/')) return tra({}, 503);
        if (req.method() !== 'GET') {
          if (p === '/api/ca/mo') return tra({ maCa: 1 }); // Chỉ ca fixture, không tới server.
          writes.push({ path: p, body: req.postDataJSON() });
          if (p === '/api/don-hang/57/chuyen-may/dung') { phase = 'DANG_DUNG'; return tra({ message: 'Fixture yêu cầu dừng' }); }
          if (p === '/api/don-hang/57/chuyen-may') { phase = 'DA_CHUYEN'; machine = 2; return tra({ message: 'Fixture đã chuyển' }); }
          errors.push('Request ghi ngoài luồng fixture: ' + p); return tra({}, 400);
        }
        if (p === '/api/auth/me') return tra(user);
        if (p === '/api/danh-muc/chi-nhanh') return tra([{ maChiNhanh: 1, tenChiNhanh: user.tenChiNhanh }]);
        if (p === '/api/ca/dang-mo') return tra({ maCa: 1 });
        if (p === '/api/don-hang/hang-doi') return tra([order()]);
        if (p === '/api/don-hang/57') return tra(order());
        if (p === '/api/don-hang/57/may-in') return tra(machines);
        if (p === '/api/may-in-ao/trang-thai') return tra(machines.map(x => ({ maMayIn: x.maMayIn, trangThai: x.maMayIn === 1 ? 'LOI' : x.maMayIn === 4 ? 'DANG_IN' : 'SAN_SANG', cheDo: 'AO' })));
        if (p === '/api/don-hang/57/chuyen-may') return tra({ maDonHang: 57, maMayHienTai: machine, trangThaiDon: 'DANG_IN', phanDangIn: [row],
          lichSu: phase ? [{ maYeuCau: 'a'.repeat(32), maMayCu: 1, maMayMoi: phase === 'DA_CHUYEN' ? 2 : null,
            trangThai: phase === 'DANG_DUNG' && ack ? 'DA_DUNG' : phase, lyDo: 'Máy hết giấy', lucYeuCau: '2026-10-24T02:00:00Z', maNguoiYeuCau: 8 }] : [] });
        return tra([]);
      });
      try {
        await page.goto(base + '/');
        await page.getByRole('row').filter({ hasText: 'DH00057' }).click();
        await page.getByRole('button', { name: 'Chuyển máy khi gặp sự cố', exact: true }).click();
        const dialog = page.getByRole('dialog', { name: 'Chuyển máy khi gặp sự cố' });
        const submit = dialog.getByRole('button', { name: 'Chuyển phần còn lại', exact: true });
        await expect(submit).toBeDisabled();
        await dialog.getByLabel('Lý do cần chuyển máy').fill('Máy hết giấy');
        await dialog.getByRole('button', { name: 'Yêu cầu dừng lệnh cũ' }).click();
        await expect(dialog.getByText('Đang chờ connector dừng lệnh cũ', { exact: true })).toBeVisible();
        await expect(submit).toBeDisabled(); assert.equal(writes.length, 1);
        ack = true;
        await dialog.getByRole('button', { name: 'Kiểm tra lại trạng thái' }).click();
        await expect(dialog.getByText('Connector đã dừng lệnh cũ', { exact: true })).toBeVisible();
        await dialog.getByLabel('Kết quả đối chiếu bản giấy').selectOption('KHONG_RO');
        await expect(submit).toBeDisabled();
        await expect(dialog.getByText('Chưa xác định phần đã in:', { exact: false })).toBeVisible();
        await dialog.getByLabel('Kết quả đối chiếu bản giấy').selectOption('MOT_PHAN');
        const file = dialog.getByRole('checkbox', { name: 'fixture.pdf (In một mặt)', exact: true });
        await expect(file).not.toBeChecked();
        await file.check();
        await dialog.getByLabel('Từ trang', { exact: true }).fill('3');
        await dialog.getByLabel('Đến trang', { exact: true }).fill('5');
        await expect(dialog.getByLabel('Máy mới cùng chi nhánh')).toHaveValue('');
        await expect(dialog.getByRole('option', { name: /Máy fixture 3/ })).toBeDisabled();
        await expect(dialog.getByRole('option', { name: /Máy fixture 4/ })).toBeDisabled();
        await dialog.getByLabel('Máy mới cùng chi nhánh').selectOption('2');
        await dialog.getByRole('checkbox', { name: /Tôi đã kiểm tra máy cũ/ }).check();
        await expect(submit).toBeDisabled();
        await dialog.getByRole('checkbox', { name: /Tôi xác nhận các phạm vi/ }).check();
        await expect(submit).toBeEnabled();
        const overflow = await dialog.evaluate(el => el.scrollWidth > el.clientWidth + 1);
        assert.equal(overflow, false, 'Popup không được tràn ngang');
        await page.screenshot({ path: path.join(output, `transfer-${role}-${width}.png`), fullPage: true });
        await submit.click();
        await expect(dialog).not.toBeVisible();
        assert.equal(writes.length, 2);
        const body = writes[1].body;
        assert.equal(body.maMayMoi, 2); assert.equal(body.daKiemTraMayCu, true); assert.equal(body.daDoiChieuBanGiay, true);
        assert.deepEqual(body.dongIn, [{ maChiTiet: 1, tuTrang: 3, denTrang: 5, soBan: 1 }]);
        assert.deepEqual(errors, []);
        results.push({ role, width, result: 'PASS' });
      } catch (error) {
        await page.screenshot({ path: path.join(output, `failure-${role}-${width}.png`), fullPage: true });
        throw error;
      } finally {
        await context.tracing.stop({ path: path.join(output, `trace-${role}-${width}.zip`) });
        await context.close();
      }
    }
    console.log(JSON.stringify(results));
  } finally { await browser.close(); }
})().catch(error => { console.error(error.stack); process.exitCode = 1; }).finally(() => server.close());
