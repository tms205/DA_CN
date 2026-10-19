// Blazor thật + API scan thật từ fixture SaoChupHttpTests. Mọi thao tác DB/thanh toán bị chặn.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

async function main() {
  const web = process.env.PHOTOCOPY_WEB_URL || 'http://localhost:5064';
  const api = process.env.PHOTOCOPY_SCAN_TEST_API;
  const output = process.env.PHOTOCOPY_QA_OUTPUT;
  assert.ok(api && output, 'Chạy qua SaoChupHttpTests với API fixture và thư mục QA riêng.');
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  try {
    for (const width of [1440, 768, 390]) {
      const context = await browser.newContext({ viewport: { width, height: 1000 } });
      await context.tracing.start({ screenshots: true, snapshots: true });
      const page = await context.newPage(), errors = [], uploads = [], prices = [];
      let scanCount = 0, failScan = false;
      page.on('pageerror', e => errors.push(e.message));
      const nd = { maNguoiDung: 2, hoTen: 'Nhân viên thử', tenVaiTro: 'STAFF', maChiNhanh: 1, tenChiNhanh: 'Chi nhánh thử' };
      await context.addInitScript(nd => {
        if (window !== window.top) return;
        localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'fixture-only', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: nd }));
      }, nd);
      await context.route('**/*', async route => {
        const req = route.request(), url = new URL(req.url());
        if (!url.pathname.startsWith('/api/') && !url.pathname.startsWith('/hubs/')) return route.continue();
        const tra = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } });
        if (req.method() === 'OPTIONS') return tra({});
        if (url.pathname.startsWith('/api/may-quet/')) {
          if (url.pathname.endsWith('/quet')) {
            scanCount++;
            if (failScan) { failScan = false; return tra({ message: 'Lỗi kết nối scan kiểm thử.' }, 503); }
          }
          const res = await route.fetch({ url: api + url.pathname + url.search, headers: { ...req.headers(), 'x-test-role': 'STAFF' } });
          return route.fulfill({ response: res, headers: { ...res.headers(), 'Access-Control-Allow-Origin': '*' } });
        }
        if (url.pathname === '/api/don-hang/khach-tai-quay') return tra({ maKhachHang: 20, hoTen: 'Khách thử' });
        if (url.pathname === '/api/file-in' && req.method() === 'POST') {
          assert.equal(url.searchParams.get('maKhachHang'), '20');
          const body = req.postDataBuffer();
          assert.ok(body.includes(Buffer.from('%PDF-')), 'Đưa đúng PDF đã ghép, không tải hai ảnh thành hai trang.');
          uploads.push('PDF A4');
          return tra({ maFileIn: 25, tenFileGoc: 'Sao_chup_the_hai_mat.pdf', soTrang: 1, trangThaiFile: 'SAN_SANG' });
        }
        if (url.pathname === '/api/bao-gia') {
          prices.push(req.postDataJSON());
          return tra({ tienIn: 300, tongTien: 300, tongSoTrang: 1, ngayHenTra: '2026-10-15T18:30:00' });
        }
        if (url.pathname === '/api/file-in/25' && req.method() === 'DELETE') return tra({});
        assert.ok(req.method() === 'GET' || url.pathname.startsWith('/hubs/'), 'Chặn mọi API ghi không nằm trong fixture: ' + url.pathname);
        if (url.pathname === '/api/danh-muc/bang-gia') return tra([
          { maDichVu: 1, tenDichVu: 'In A3', loaiDichVu: 'IN_AN', khoGiay: 'A3', loaiGiay: '70gsm', matIn: '1_MAT', mauSac: 'DEN_TRANG', donGia: 500 },
          { maDichVu: 2, tenDichVu: 'A4 hai mặt', loaiDichVu: 'IN_AN', khoGiay: 'A4', loaiGiay: '70gsm', matIn: '2_MAT', mauSac: 'DEN_TRANG', donGia: 250 },
          { maDichVu: 3, tenDichVu: 'A4 một mặt', loaiDichVu: 'IN_AN', khoGiay: 'A4', loaiGiay: '70gsm', matIn: '1_MAT', mauSac: 'DEN_TRANG', donGia: 300 },
          { maDichVu: 4, tenDichVu: 'A4 màu', loaiDichVu: 'IN_AN', khoGiay: 'A4', loaiGiay: '70gsm', matIn: '1_MAT', mauSac: 'MAU', donGia: 3000 }
        ]);
        if (url.pathname === '/api/auth/me') return tra(nd);
        if (url.pathname === '/api/danh-muc/chi-nhanh') return tra([{ maChiNhanh: 1, tenChiNhanh: 'Chi nhánh thử' }]);
        if (url.pathname === '/api/ca/dang-mo') return tra({ maCa: 1, maNhanVien: 2, maChiNhanh: 1, trangThai: 'DANG_MO' });
        if (url.pathname.startsWith('/hubs/')) return tra({}, 503);
        return tra([]);
      });
      try {
        await page.goto(web + '/lap-don-tai-quay');
        await page.getByPlaceholder('Nguyễn Văn An').fill('Khách thử');
        await page.getByRole('button', { name: 'Tìm / tạo khách hàng', exact: true }).click();
        await page.getByLabel('Dịch vụ đặt in').selectOption('SAO_CHUP');
        const machine = page.getByRole('region', { name: 'Máy quét ảo', exact: true });
        if (width >= 1400) {
          await expect(machine).toBeVisible();
          assert.ok((await machine.boundingBox()).width > 700, 'Bảng máy quét phải dùng bề rộng nội dung, không bị nhét trong nửa cột tài liệu.');
        }
        const front = machine.getByRole('group', { name: 'Mặt trước', exact: true });
        const back = machine.getByRole('group', { name: 'Mặt sau', exact: true });
        const merge = machine.getByRole('button', { name: 'Ghép & xem trước A4', exact: true });
        await expect(merge).toBeDisabled();
        await front.getByLabel('Ảnh nguồn mặt trước', { exact: true }).setInputFiles({ name: 'sai.svg', mimeType: 'image/svg+xml', buffer: Buffer.from('<svg/>') });
        await expect(machine.getByRole('alert')).toContainText('JPG/PNG');
        await expect(front.getByRole('button', { name: 'Quét mặt trước', exact: true })).toBeDisabled();
        for (const [group, side] of [[front, 'trước'], [back, 'sau']]) {
          await group.getByRole('button', { name: `Ảnh minh họa mặt ${side}`, exact: true }).click();
          await expect(group.getByRole('button', { name: `Quét mặt ${side}`, exact: true })).toBeEnabled();
          assert.equal(await group.getByLabel(`Ảnh nguồn mặt ${side}`, { exact: true }).evaluate(e => e.files.length), 0,
            'Đổi sang ảnh minh họa phải xóa tên file cũ khỏi ô chọn tệp.');
        }
        const sample = await context.request.get(api + '/api/may-quet/anh-minh-hoa/TRUOC', { headers: { 'x-test-role': 'STAFF' } });
        assert.equal(sample.status(), 200);
        await front.getByLabel('Ảnh nguồn mặt trước', { exact: true }).setInputFiles({ name: 'the-test.png', mimeType: 'image/png', buffer: await sample.body() });
        await expect(front.getByText('the-test.png', { exact: true })).toBeVisible();
        const first = front.getByRole('button', { name: 'Quét mặt trước', exact: true });
        await first.evaluate(e => { e.click(); e.click(); });
        await expect(front.getByText(/^Đã quét ·/)).toBeVisible();
        assert.equal(scanCount, 1, 'Nhấp hai lần chỉ gửi một lệnh quét.');
        await back.getByRole('button', { name: 'Quét mặt sau', exact: true }).click();
        await expect(back.getByText(/^Đã quét ·/)).toBeVisible();
        await merge.click();
        await expect(machine.getByRole('img', { name: 'Bản ghép hai mặt trên cùng một mặt A4', exact: true })).toBeVisible();
        assert.deepEqual(uploads, [], 'Xem trước chưa được lưu FileIn hay tạo đơn.');
        await machine.getByRole('button', { name: 'Xem toàn màn hình', exact: true }).click();
        const dialog = page.getByRole('dialog', { name: 'Bản sao chụp A4', exact: true });
        await expect(dialog).toBeVisible();
        const box = await dialog.boundingBox();
        assert.ok(Math.abs(box.width - width) < 1 && Math.abs(box.height - 1000) < 1);
        await page.getByRole('button', { name: 'Đóng bản sao chụp', exact: true }).focus();
        await page.keyboard.press('Escape');
        await expect(dialog).not.toBeVisible();
        await machine.getByText('Thử sự cố máy ảo', { exact: true }).click();
        await machine.getByRole('button', { name: 'Mô phỏng mất kết nối', exact: true }).click();
        await expect(machine.getByRole('status')).toContainText('Mất kết nối');
        await expect(merge).toBeDisabled();
        await expect(machine.getByRole('button', { name: 'Dùng bản sao chụp để lập đơn', exact: true })).toHaveCount(0);
        await machine.getByRole('button', { name: 'Khôi phục máy ảo', exact: true }).click();
        await expect(merge).toBeDisabled();
        await front.getByText('Cắt viền mặt trước (tùy chọn)', { exact: true }).click();
        await front.getByLabel('Trái (%)', { exact: true }).fill('50');
        await front.getByLabel('Rộng (%)', { exact: true }).fill('60');
        await first.click();
        await expect(machine.getByRole('alert')).toContainText('Không quét được');
        await front.getByLabel('Trái (%)', { exact: true }).fill('0');
        await front.getByLabel('Rộng (%)', { exact: true }).fill('100');
        await first.click();
        await expect(front.getByText(/^Đã quét ·/)).toBeVisible();
        await back.getByRole('button', { name: 'Quét mặt sau', exact: true }).click();
        await expect(back.getByText(/^Đã quét ·/)).toBeVisible();
        await merge.click();
        await expect(machine.getByRole('button', { name: 'Dùng bản sao chụp để lập đơn', exact: true })).toBeEnabled();
        await front.getByRole('button', { name: /^Xoay mặt trước/ }).click();
        await expect(merge).toBeDisabled();
        await expect(machine.getByRole('button', { name: 'Dùng bản sao chụp để lập đơn', exact: true })).toHaveCount(0);
        for (let i = 0; i < 3; i++) await front.getByRole('button', { name: /^Xoay mặt trước/ }).click();
        failScan = true;
        await first.click();
        await expect(machine.getByRole('alert')).toContainText('Lỗi kết nối scan kiểm thử.');
        await expect(merge).toBeDisabled();
        await first.click();
        await expect(front.getByText(/^Đã quét ·/)).toBeVisible();
        await merge.click();
        await expect(machine.getByRole('button', { name: 'Dùng bản sao chụp để lập đơn', exact: true })).toBeEnabled();
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
        await machine.scrollIntoViewIfNeeded();
        await page.screenshot({ path: path.join(output, `may-quet-${width}.png`), fullPage: true });
        await machine.getByRole('button', { name: 'Dùng bản sao chụp để lập đơn', exact: true }).click();
        await expect(page.getByText('1 trang PDF', { exact: true })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Lập đơn & hiện mã QR', exact: true })).toBeEnabled();
        assert.deepEqual(uploads, ['PDF A4']);
        assert.equal(prices.at(-1).fileIn[0].maDichVu, 3);
        assert.equal(prices.at(-1).fileIn[0].huongGiay, 'DUNG');
        assert.equal(prices.at(-1).fileIn[0].soBanIn, 1);
        await expect(page.getByLabel('Dịch vụ đặt in')).toBeDisabled();
        await expect(first).toBeDisabled();
        await page.getByRole('button', { name: 'Xóa', exact: true }).click();
        await page.getByRole('dialog', { name: 'Xóa tài liệu', exact: true }).getByRole('button', { name: 'Xóa file', exact: true }).click();
        await expect(page.getByLabel('Dịch vụ đặt in')).toBeEnabled();
        await expect(merge).toBeDisabled();
        await expect(front.getByText('Chưa chọn ảnh', { exact: false })).toBeVisible();
        assert.equal(await front.getByLabel('Ảnh nguồn mặt trước', { exact: true }).evaluate(e => e.files.length), 0);
        assert.deepEqual(errors, []);
        console.log(JSON.stringify({ width, result: 'PASS', scanCount, uploaded: uploads.length, note: 'API scan thật; các API DB là fixture' }));
      } catch (e) {
        await page.screenshot({ path: path.join(output, `failure-${width}.png`), fullPage: true });
        throw e;
      } finally {
        await context.tracing.stop({ path: path.join(output, `trace-${width}.zip`) });
        await context.close();
      }
    }
  } finally { await browser.close(); }
}
main().catch(e => { console.error(e.stack); process.exitCode = 1; });
