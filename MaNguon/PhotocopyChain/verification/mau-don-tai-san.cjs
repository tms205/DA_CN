// UI Blazor thật + PDF thật từ thư viện; chặn toàn bộ API bằng fixture, không tạo đơn/thu tiền thật.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');

async function main() {
  const base = process.env.PHOTOCOPY_WEB_URL || 'http://localhost:5064';
  const output = process.env.PHOTOCOPY_QA_OUTPUT;
  assert.ok(output, 'Cần PHOTOCOPY_QA_OUTPUT trỏ vào thư mục kiểm thử.');
  fs.mkdirSync(output, { recursive: true });
  const folder = path.resolve(__dirname, '../PhotocopyApi/TaiNguyen/MauDon');
  const catalog = JSON.parse(fs.readFileSync(path.join(folder, 'catalog.json'), 'utf8'));
  const templates = catalog.map(item => Object.fromEntries(Object.entries(item).map(([k, v]) => [k[0].toLowerCase() + k.slice(1), v])));
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const results = [];
  try {
    for (const width of [390, 768, 1366]) {
      const context = await browser.newContext({ viewport: { width, height: 1000 } });
      const page = await context.newPage(), errors = [], selections = [];
      page.on('pageerror', error => errors.push(error.message));
      const nd = { maNguoiDung: 2, hoTen: 'Quản lý kiểm thử', tenVaiTro: 'BRANCH_ADMIN', maChiNhanh: 1, tenChiNhanh: 'Chi nhánh thử' };
      await context.addInitScript(nd => {
        // Chỉ gắn phiên fixture ở trang chính, không chạm localStorage của PDF/data iframe.
        if (window !== window.top) return;
        localStorage.setItem('photocopychain.phien', JSON.stringify({
          accessToken: 'fixture-not-real', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: nd
        }));
      }, nd);
      await context.route('**/*', async route => {
        const request = route.request(), url = new URL(request.url());
        if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) {
          const tra = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } });
          if (request.method() === 'OPTIONS') return tra({});
          if (url.pathname === '/api/don-hang/khach-tai-quay') return tra({ maKhachHang: 20, hoTen: 'Khách thử' });
          if (url.pathname === '/api/file-in/mau') return tra(templates);
          const preview = url.pathname.match(/^\/api\/file-in\/mau\/([A-Z0-9_]+)\/trang\/(\d+)$/);
          if (preview) {
            const template = catalog.find(m => m.MaMau === preview[1]);
            const file = template.TenFile.replace('.pdf', `-trang-${preview[2]}.png`);
            return route.fulfill({ contentType: 'image/png', body: fs.readFileSync(path.join(folder, 'XemTruoc', file)), headers: { 'Access-Control-Allow-Origin': '*' } });
          }
          const selection = url.pathname.match(/^\/api\/file-in\/mau\/([A-Z0-9_]+)$/);
          if (selection && request.method() === 'POST') {
            assert.equal(url.searchParams.get('maKhachHang'), '20');
            const template = catalog.find(m => m.MaMau === selection[1]);
            selections.push(template.MaMau);
            return tra({ maFileIn: 10, tenFileGoc: template.TenMau + '.pdf', soTrang: template.SoTrang, trangThaiFile: 'SAN_SANG' });
          }
          if (url.pathname === '/api/bao-gia') return tra({ tienIn: 600, tongTien: 600, tongSoTrang: 2, ngayHenTra: '2026-10-14T18:30:00', khuyenMaiHopLe: true });
          assert.ok(request.method() === 'GET' || url.pathname.startsWith('/hubs/'), 'Không cho gửi yêu cầu ghi ngoài fixture.');
          if (url.pathname === '/api/danh-muc/bang-gia') return tra([{ maDichVu: 1, tenDichVu: 'In A4', loaiDichVu: 'IN_AN', khoGiay: 'A4', loaiGiay: '70gsm', matIn: '1_MAT', mauSac: 'DEN_TRANG', donGia: 300 }]);
          if (url.pathname === '/api/auth/me') return tra(nd);
          if (url.pathname === '/api/danh-muc/chi-nhanh') return tra([{ maChiNhanh: 1, tenChiNhanh: 'Chi nhánh thử' }]);
          if (url.pathname === '/api/ca/dang-mo') return tra({ maCa: 1, maNhanVien: 2, maChiNhanh: 1, trangThai: 'DANG_MO' });
          if (url.pathname === '/api/quan-tri/bao-cao/hien-tai') return tra({ soPhieuChoDuyet: 0, soHoanTienChoDuyet: 0 });
          if (url.pathname.startsWith('/hubs/')) return tra({}, 503);
          return tra([]);
        }
        if (url.origin !== base && !['https://fonts.googleapis.com', 'https://fonts.gstatic.com'].includes(url.origin)) return route.abort();
        return route.continue();
      });
      try {
        await page.goto(base + '/lap-don-tai-quay');
        await page.getByPlaceholder('Nguyễn Văn An').fill('Khách thử');
        await page.getByRole('button', { name: 'Tìm / tạo khách hàng', exact: true }).click();
        await page.getByLabel('Dịch vụ đặt in').selectOption('MAU_DON');
        await expect(page.getByRole('article')).toHaveCount(Math.min(6, catalog.length));
        const library = page.getByRole('region', { name: 'Thư viện mẫu đơn', exact: true });
        const list = library.getByRole('region', { name: 'Danh sách mẫu đơn', exact: true });
        const pagination = library.getByRole('navigation', { name: 'Phân trang thư viện mẫu' });
        await expect(pagination).toContainText(`Trang 1 / ${Math.ceil(catalog.length / 6)}`);
        await expect(pagination.getByRole('button', { name: 'Trang mẫu trước', exact: true })).toBeDisabled();
        const names = new Set(await page.getByRole('article').getByRole('heading', { level: 3 }).allTextContents());
        for (let i = 2; i <= Math.ceil(catalog.length / 6); i++) {
          await pagination.getByRole('button', { name: 'Trang mẫu sau', exact: true }).click();
          await expect(pagination).toContainText(`Trang ${i} / ${Math.ceil(catalog.length / 6)}`);
          (await page.getByRole('article').getByRole('heading', { level: 3 }).allTextContents()).forEach(n => names.add(n));
        }
        assert.equal(names.size, catalog.length, 'Mẫu ở các trang sau vẫn phải truy cập được, không cắt bỏ danh mục.');
        await expect(pagination.getByRole('button', { name: 'Trang mẫu sau', exact: true })).toBeDisabled();
        const sizeLibrary = await list.boundingBox();
        assert.ok(sizeLibrary.height <= 442, 'Danh sách mẫu phải cuộn trong khung giới hạn, không kéo dài trang.');
        await list.focus();
        await expect(list).toBeFocused();
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
        await page.getByLabel('Nhóm mẫu').selectOption('Học vụ');
        await expect(pagination).toContainText('Trang 1 /');
        await page.getByLabel('Tìm mẫu đơn').fill('khong-co-mau');
        await expect(page.getByRole('article')).toHaveCount(0);
        await expect(pagination.getByRole('button', { name: 'Trang mẫu sau', exact: true })).toBeDisabled();
        await library.getByRole('button', { name: 'Xóa bộ lọc mẫu', exact: true }).click();
        await expect(page.getByRole('article')).toHaveCount(6);
        await expect(page.getByLabel('Nhóm mẫu')).toHaveValue('');
        const listWidth = await list.evaluate(element => element.clientWidth);
        const cardSize = await page.getByRole('article').first().boundingBox();
        assert.ok(cardSize.width >= Math.min(280, listWidth - 8),
          'Thẻ mẫu phải theo bề rộng vùng tài liệu, không ép hai cột hẹp khiến tên xuống dòng từng chữ.');
        await library.scrollIntoViewIfNeeded();
        await page.screenshot({ path: path.join(output, `thu-vien-gon-${width}.png`) });
        await page.getByLabel('Nhóm mẫu').selectOption('Việc làm');
        await expect(page.getByRole('article')).toHaveCount(2);
        await page.getByLabel('Tìm mẫu đơn').fill('khong-co-mau');
        await expect(page.getByText('Không có mẫu phù hợp.', { exact: false })).toBeVisible();
        await page.getByLabel('Tìm mẫu đơn').fill('');
        await page.getByRole('article').filter({ hasText: 'Đơn xin việc' }).getByRole('button', { name: 'Xem mẫu', exact: true }).click();
        await expect(page.getByRole('dialog', { name: 'Đơn xin việc · 1 trang' })).toBeVisible();
        const preview = page.locator('dialog[aria-label="Bản xem trước mẫu đơn"]');
        const dialogSize = await preview.boundingBox();
        assert.ok(Math.abs(dialogSize.x) < 1 && Math.abs(dialogSize.y) < 1
          && Math.abs(dialogSize.width - width) < 1 && Math.abs(dialogSize.height - 1000) < 1,
          'Cửa sổ xem mẫu phải phủ toàn bộ màn hình trình duyệt.');
        const image = page.getByRole('img', { name: 'Đơn xin việc, trang 1', exact: true });
        await expect(image).toHaveAttribute('src', /^data:image\/png;base64,/);
        await expect.poll(() => image.evaluate(i => i.complete && i.naturalWidth > 0)).toBeTruthy();
        const size = await image.evaluate(i => ({
          image: i.getBoundingClientRect().height,
          frame: i.parentElement.clientHeight,
        }));
        assert.ok(Math.abs(size.image / size.frame - 1.5) < .01, 'Bản xem trước phải lớn hơn mặc định cũ 50%.');
        assert.deepEqual(selections, [], 'Chỉ xem chưa được tạo file.');
        await preview.screenshot({ path: path.join(output, `xem-truoc-${width}.png`) });
        await page.getByRole('button', { name: 'Đóng bản xem trước' }).focus();
        await page.keyboard.press('Shift+Tab');
        await expect(page.getByRole('button', { name: 'Chọn mẫu này để in' })).toBeFocused();
        await page.keyboard.press('Escape');
        await expect(preview).not.toBeVisible();
        await page.getByRole('article').filter({ hasText: 'Đơn xin việc' }).getByRole('button', { name: 'Xem mẫu', exact: true }).click();
        await expect(preview).toBeVisible();
        await page.getByRole('button', { name: 'Đóng bản xem trước' }).click();
        await page.getByLabel('Nhóm mẫu').selectOption('Học vụ');
        await page.getByLabel('Tìm mẫu đơn').fill('nghỉ học có thời hạn');
        await page.getByRole('article').getByRole('button', { name: 'Xem mẫu', exact: true }).click();
        await expect(page.getByRole('dialog', { name: 'Đơn xin nghỉ học có thời hạn · 2 trang' })).toBeVisible();
        await page.getByRole('button', { name: 'Trang sau', exact: true }).click();
        await expect(preview).toContainText('Trang 2 / 2');
        await expect(page.getByRole('img', { name: 'Đơn xin nghỉ học có thời hạn, trang 2', exact: true })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Trang sau', exact: true })).toBeDisabled();
        await page.getByRole('button', { name: 'Phóng to', exact: true }).click();
        await page.getByRole('button', { name: 'Thu nhỏ', exact: true }).click();
        await page.getByRole('button', { name: 'Chọn mẫu này để in', exact: true }).click();
        await expect(page.getByText('2 trang PDF', { exact: true })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Lập đơn & hiện mã QR' })).toBeEnabled();
        assert.deepEqual(selections, ['NGHI_HOC_HDU']);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
        assert.deepEqual(errors, []);
        await page.screenshot({ path: path.join(output, `mau-don-${width}.png`), fullPage: true });
        results.push({ width, result: 'PASS', note: 'Blazor thật, PDF thật, API fixture không ghi CSDL' });
      } catch (error) {
        await page.screenshot({ path: path.join(output, `failure-${width}.png`), fullPage: true });
        throw error;
      } finally { await context.close(); }
    }
  } finally { await browser.close(); }
  console.log(JSON.stringify(results));
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
