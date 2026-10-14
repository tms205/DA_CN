// Chỉ chạy preview riêng và fixture API; không gọi API thật, không có secret.
// node kiem_thu_giao_hang_web.cjs <thu-muc-wwwroot-publish> <thu-muc-playwright>
const { chromium } = require(process.argv[3]);
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(process.argv[2]);
const mime = { '.html':'text/html', '.js':'text/javascript', '.json':'application/json', '.css':'text/css', '.wasm':'application/wasm', '.svg':'image/svg+xml' };
const server = http.createServer((req, res) => {
  let file = path.resolve(root, '.' + new URL(req.url, 'http://fixture').pathname);
  if (!file.startsWith(root + path.sep)) { res.writeHead(403).end(); return; }
  if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) file = path.join(root, 'index.html');
  res.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
  fs.createReadStream(file).pipe(res);
});
(async () => {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const browser = await chromium.launch({ headless: true, channel: 'chrome' });
  try {
    const page = await browser.newPage();
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    let assigned = false, posts = 0, unavailable = false;
    const order = {
      maDonHang:15, maDonHangHienThi:'DH00015', maChiNhanh:1, tenChiNhanh:'Cơ sở 1',
      diaChiChiNhanh:'140 Lê Trọng Tấn', tenKhachHang:'Khách thử <script>', sdtNhan:'0901234567',
      diaChiGiao:'123 Lê Lợi · Chi tiết: Tầng 2 · Ghi chú giao: Gọi trước',
      phiShip:19000, tongTien:39000, trangThaiDonHang:'DA_IN', trangThaiThanhToan:'DA_THANH_TOAN',
      soLanGiao:0, ngayDat:'2026-10-14T09:00:00',
    };
    await page.addInitScript(() => sessionStorage.setItem('photocopychain.phien', JSON.stringify({
      accessToken:'fixture-not-real', hetHanLuc:'2099-01-01T00:00:00Z',
      nguoiDung:{ maNguoiDung:1, hoTen:'Quản lý thử nghiệm', email:'fixture@example.invalid',
        tenVaiTro:'BRANCH_ADMIN', maChiNhanh:1, tenChiNhanh:'Cơ sở 1' },
    })));
    await page.route('http://localhost:5268/**', async route => {
      const url = new URL(route.request().url());
      const headers = { 'access-control-allow-origin':'*', 'access-control-allow-headers':'*', 'access-control-allow-methods':'*' };
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status:204, headers });
      if (url.pathname.startsWith('/hubs/')) return route.fulfill({ status:503, headers, body:'' });
      let data = [];
      if (url.pathname.endsWith('/phan-cong')) {
        posts++; assert.equal(route.request().postDataJSON().maShipper, 7);
        assigned = true; data = { message:'Đã bàn giao' };
      } else if (url.pathname === '/api/giao-hang/shipper') data = [{ maShipper:7, maChiNhanh:1, hoTen:'Nguyễn Văn Giao' }];
      else if (url.pathname === '/api/giao-hang') {
        if (unavailable) return route.fulfill({ status:500, headers, json:{ message:'Fixture: chưa tải được giao hàng.' } });
        data = url.searchParams.get('nhom') === 'CHO_GIAO' ? (assigned ? [] : [order])
          : url.searchParams.get('nhom') === 'DANG_GIAO' && assigned ? [{ ...order, trangThaiDonHang:'DANG_GIAO', soLanGiao:1, tenShipper:'Nguyễn Văn Giao' }] : [];
      } else if (url.pathname.includes('/hien-tai')) data = {};
      await route.fulfill({ status:200, headers, json:data });
    });
    await page.goto('http://127.0.0.1:' + server.address().port + '/giao-hang');
    await page.getByText('DH00015', { exact:true }).waitFor({ timeout:60000 });
    assert.equal(await page.locator('tbody script').count(), 0);
    for (const width of [1366, 1024, 768, 390, 320]) {
      await page.setViewportSize({ width, height:900 });
      await page.waitForTimeout(150);
      const dims = await page.evaluate(() => ({ full:document.documentElement.scrollWidth, view:innerWidth }));
      assert.ok(dims.full <= dims.view + 1, 'Tràn ngang ở ' + width + ': ' + JSON.stringify(dims));
      console.log('Responsive PASS ' + width + 'px');
    }
    await page.setViewportSize({ width:1366, height:900 });
    await page.locator('#shipper-15').selectOption('7');
    await page.getByRole('button', { name:'Bàn giao đi giao', exact:true }).click();
    assert.equal(posts, 0);
    await page.getByRole('button', { name:'Xác nhận bàn giao', exact:true }).click();
    await page.getByRole('status').filter({ hasText:'Đã bàn giao DH00015' }).waitFor();
    assert.equal(posts, 1);
    await page.getByRole('button', { name:'Đang giao', exact:true }).click();
    await page.getByText('Nguyễn Văn Giao', { exact:true }).waitFor();
    await page.getByRole('textbox', { name:'Tìm đơn giao' }).fill('không có');
    await page.getByText('Không có đơn/lượt phù hợp.', { exact:true }).waitFor();
    await page.getByRole('textbox', { name:'Tìm đơn giao' }).fill('');
    unavailable = true;
    await page.getByRole('button', { name:'Làm mới', exact:true }).click();
    await page.getByRole('alert').filter({ hasText:'Fixture: chưa tải được' }).waitFor();
    unavailable = false;
    await page.getByRole('button', { name:'Làm mới', exact:true }).click();
    await page.getByText('DH00015', { exact:true }).waitFor();
    await page.getByRole('button', { name:'Lịch sử', exact:true }).click();
    await page.getByText('Không có đơn/lượt phù hợp.', { exact:true }).waitFor();
    assert.deepEqual(errors, []);
    console.log('PASS: xác nhận hai bước, nhóm/tìm kiếm, lỗi/thử lại, HTML escape, không lỗi JavaScript.');
  } finally { await browser.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(err => { console.error(err.message); process.exitCode = 1; server.close(); });
