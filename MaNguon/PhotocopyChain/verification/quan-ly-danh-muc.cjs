// Fixture lưu trong bộ nhớ. Mọi POST/PUT ở đây đều được chặn, không gửi vào database thật.
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const assert = require('node:assert/strict');
const path = require('node:path');

function createDanhMucFixture() {
  const branches = [1, 2, 3].map(id => ({ maChiNhanh: id, tenChiNhanh: 'Chi nhánh ' + id, diaChi: 'Địa chỉ kiểm thử', gioMoCua: '09:00:00', gioDongCua: '21:00:00', banKinhGiao: 10, trangThai: 'HOAT_DONG' }));
  const machines = branches.map(c => ({ maMayIn: c.maChiNhanh, maChiNhanh: c.maChiNhanh, tenChiNhanh: c.tenChiNhanh, tenMay: 'Máy in ' + c.maChiNhanh, khoGiay: 'A4', tocDo: 30, soTrangDaIn: 0, trangThai: 'HOAT_DONG' }));
  const customers = Array.from({ length: 12 }, (_, i) => ({ maNguoiDung: i + 20, hoTen: 'Khách thử ' + (i + 1), email: 'khach' + (i + 1) + '@gmail.com', soDienThoai: '0912345678', diaChi: 'Địa chỉ khách', soDon: 2, tongChiTieu: 50000, ngayTao: '2026-10-01T10:00:00', trangThai: 'HOAT_DONG' }));
  const services = Array.from({ length: 12 }, (_, i) => ({ maDichVu: i + 1, tenDichVu: 'In A4 thử ' + (i + 1), loaiDichVu: 'IN_AN', khoGiay: 'A4', loaiGiay: '70GSM', matIn: '1_MAT', mauSac: 'DEN_TRANG', donGia: 500, trangThai: 'HIEN', maVatTu: null, kichThuocAnh: null, nhomGiaCong: null }));
  const prices = branches.flatMap(c => services.map(d => ({ ...d, maChiNhanh: c.maChiNhanh, tenChiNhanh: c.tenChiNhanh })));
  const writes = [];
  return {
    writes, branches, machines, customers, services, prices,
    async response(request, selected) {
      const pathname = new URL(request.url()).pathname, method = request.method();
      const routes = {
        '/api/quan-tri/chi-nhanh': branches.filter(c => !selected || c.maChiNhanh === selected),
        '/api/danh-muc/chi-nhanh': branches.filter(c => c.trangThai === 'HOAT_DONG'),
        '/api/quan-tri/may-in': machines.filter(m => !selected || m.maChiNhanh === selected),
        '/api/quan-tri/khach-hang': customers,
        '/api/quan-tri/danh-muc-dich-vu': services,
        '/api/quan-tri/danh-muc-vat-tu': [{ maVatTu: 1, tenVatTu: 'Giấy A4', donViTinh: 'tờ' }],
        '/api/quan-tri/dich-vu': prices.filter(p => !selected || p.maChiNhanh === selected),
      };
      if (method === 'GET' && routes[pathname]) return { status: 200, body: routes[pathname] };
      if (method !== 'POST' && method !== 'PUT') return null;
      if (!pathname.startsWith('/api/quan-tri/')) return null;
      const body = request.postDataJSON();
      if (method === 'POST' && pathname === '/api/quan-tri/chi-nhanh') {
        const c = { ...body, maChiNhanh: branches.length + 1 }; branches.push(c);
        prices.push(...services.map(d => ({ ...d, maChiNhanh: c.maChiNhanh, tenChiNhanh: c.tenChiNhanh })));
      } else if (method === 'POST' && pathname === '/api/quan-tri/may-in') {
        assert.ok(!selected || selected === body.maChiNhanh);
        machines.push({ ...body, maMayIn: machines.length + 1, tenChiNhanh: branches.find(c => c.maChiNhanh === body.maChiNhanh).tenChiNhanh });
      } else if (method === 'POST' && pathname === '/api/quan-tri/dich-vu') {
        const d = { ...body, maDichVu: services.length + 1 }; services.push(d);
        prices.push(...branches.map(c => ({ ...d, maChiNhanh: c.maChiNhanh, tenChiNhanh: c.tenChiNhanh })));
      } else if (method === 'PUT' && /^\/api\/quan-tri\/khach-hang\/\d+\/trang-thai$/.test(pathname)) {
        Object.assign(customers.find(c => c.maNguoiDung === Number(pathname.split('/')[4])), body);
      } else if (method === 'PUT' && /^\/api\/quan-tri\/danh-muc-dich-vu\/\d+$/.test(pathname)) {
        Object.assign(services.find(d => d.maDichVu === Number(pathname.split('/').pop())), body);
      } else if (method === 'PUT' && /^\/api\/quan-tri\/bang-gia\/\d+$/.test(pathname)) {
        assert.ok(selected);
        Object.assign(prices.find(p => p.maChiNhanh === selected && p.maDichVu === Number(pathname.split('/').pop())), body);
      } else return null;
      writes.push({ pathname, method, body, selected });
      return { status: 200, body: { message: 'Đã lưu fixture.' } };
    }
  };
}

async function kiemTraDanhMuc({ page, base, fixture, width, output, noOverflow }) {
  await page.evaluate(() => localStorage.removeItem('photocopychain.pham-vi-chi-nhanh'));
  await page.goto(base + '/khach-hang');
  await expect(page.getByRole('heading', { name: 'Danh sách khách hàng', exact: true })).toBeVisible();
  await expect(page.getByRole('table').getByRole('row')).toHaveCount(11);
  await page.getByRole('button', { name: 'Sau', exact: true }).click();
  await expect(page.getByRole('table').getByRole('row')).toHaveCount(3);
  await page.getByLabel('Tìm khách hàng').fill('khach1@gmail.com');
  const row = page.getByRole('row').filter({ hasText: 'khach1@gmail.com' });
  await row.getByRole('button', { name: 'Chi tiết', exact: true }).click();
  await expect(page.getByRole('region', { name: 'Chi tiết khách hàng' })).toContainText('Địa chỉ khách');
  await row.getByRole('button', { name: 'Khóa', exact: true }).click();
  await page.getByRole('region', { name: 'Xác nhận trạng thái khách hàng' }).getByRole('button', { name: 'Xác nhận', exact: true }).click();
  await expect(row).toContainText('Bị khóa');
  await row.getByRole('button', { name: 'Mở khóa', exact: true }).click();
  await page.getByRole('button', { name: 'Xác nhận', exact: true }).click();
  await expect(row).toContainText('Hoạt động');
  await noOverflow();
  await page.screenshot({ animations: 'disabled', path: path.join(output, 'customers-' + width + '.png') });

  await page.goto(base + '/dich-vu');
  await expect(page.getByRole('heading', { name: 'Danh mục dịch vụ', exact: true })).toBeVisible();
  await expect(page.getByRole('table').getByRole('row')).toHaveCount(11);
  await page.getByLabel('Tìm dịch vụ').fill('khong-co');
  await expect(page.getByText('Không có dịch vụ phù hợp.')).toBeVisible();
  await page.getByLabel('Tìm dịch vụ').fill('');
  await page.getByRole('button', { name: 'Thêm dịch vụ', exact: true }).click();
  await page.getByLabel('Tên dịch vụ', { exact: true }).fill('In mới kiểm thử');
  await page.getByLabel('Khổ giấy', { exact: true }).selectOption('A4');
  await page.getByLabel('Mặt in', { exact: true }).selectOption('1_MAT');
  await page.getByLabel('Màu sắc', { exact: true }).selectOption('DEN_TRANG');
  await page.getByLabel(/Giá khởi tạo/).fill('800');
  await page.getByRole('button', { name: 'Lưu dịch vụ', exact: true }).click();
  await expect(page.getByText('Đã thêm dịch vụ và giá khởi tạo tại các chi nhánh.', { exact: true })).toBeVisible();
  assert.equal(fixture.services.at(-1).donGia, 800);
  await page.getByLabel('Tìm dịch vụ').fill('In A4 thử 1');
  await page.getByRole('row').filter({ hasText: 'In A4 thử 1' }).filter({ hasText: 'Mã DV: #1' }).first().getByRole('button', { name: 'Sửa dịch vụ' }).click();
  await page.getByLabel('Tên dịch vụ', { exact: true }).fill('Dịch vụ đổi tên');
  await page.getByLabel(/Giá khởi tạo/).fill('900');
  await page.getByRole('button', { name: 'Lưu dịch vụ', exact: true }).click();
  await expect(page.getByText('Đã sửa danh mục; giá chi nhánh không thay đổi.', { exact: true })).toBeVisible();
  assert.equal(fixture.prices.find(x => x.maChiNhanh === 1 && x.maDichVu === 1).donGia, 500);
  await page.getByLabel('Tìm dịch vụ').fill('Dịch vụ đổi tên');
  await noOverflow();
  await page.screenshot({ animations: 'disabled', path: path.join(output, 'services-' + width + '.png') });

  await page.goto(base + '/quan-tri');
  await expect(page.getByRole('heading', { name: 'Danh mục khách hàng' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Thêm chi nhánh', exact: true }).click();
  await page.getByLabel('Tên chi nhánh', { exact: true }).fill('Chi nhánh mới thử');
  await page.getByLabel('Địa chỉ chi nhánh', { exact: true }).fill('140 Lê Trọng Tấn');
  await page.getByLabel('Điện thoại hotline').fill('0912345678');
  await page.getByLabel('Giờ mở cửa').fill('08:00');
  await page.getByLabel('Giờ đóng cửa').fill('20:00');
  await page.getByRole('button', { name: 'Lưu chi nhánh', exact: true }).click();
  await expect(page.getByText('Đã thêm chi nhánh, tạo giá mặc định và kho trống. Bạn có thể chọn cơ sở mới ở thanh phạm vi.', { exact: true })).toBeVisible();
  await expect(page.getByRole('row').filter({ hasText: 'Chi nhánh mới thử' })).toContainText('08:00 – 20:00');
  await expect(page.getByLabel('Chọn phạm vi chi nhánh').getByRole('option', { name: 'Chi nhánh mới thử' })).toHaveCount(1);
  await page.getByRole('button', { name: 'Thêm máy in', exact: true }).click();
  await page.getByLabel('Chi nhánh quản lý').selectOption('4');
  await page.getByLabel('Tên máy / model').fill('Ricoh máy mới thử');
  await page.getByLabel('Tốc độ (trang/phút)').fill('45');
  await page.getByLabel('Khổ giấy lớn nhất').selectOption('A3');
  await page.getByLabel('Hỗ trợ in màu').check();
  await page.getByLabel('Hỗ trợ in hai mặt').check();
  await noOverflow();
  await page.screenshot({ animations: 'disabled', path: path.join(output, 'branch-printer-form-' + width + '.png') });
  await page.getByRole('button', { name: 'Lưu máy in', exact: true }).click();
  await expect(page.getByRole('row').filter({ hasText: 'Ricoh máy mới thử' })).toContainText('45');
  assert.equal(fixture.machines.at(-1).maChiNhanh, 4);
  await page.getByLabel('Chọn phạm vi chi nhánh').selectOption('1');
  await page.goto(base + '/bang-gia');
  await expect(page.getByRole('button', { name: 'Thêm dịch vụ' })).toHaveCount(0);
  await page.getByRole('row').filter({ hasText: 'Mã DV: #1' }).first().getByRole('button', { name: 'Sửa giá', exact: true }).click();
  await expect(page.getByLabel('Tên dịch vụ', { exact: true })).toHaveCount(0);
  await page.getByLabel('Đơn giá (₫)').fill('1200');
  await page.getByRole('button', { name: 'Lưu giá', exact: true }).click();
  await expect(page.getByText('Đã cập nhật giá tại chi nhánh đang chọn.', { exact: true })).toBeVisible();
  assert.equal(fixture.prices.find(x => x.maChiNhanh === 1 && x.maDichVu === 1).donGia, 1200);
  assert.equal(fixture.prices.find(x => x.maChiNhanh === 2 && x.maDichVu === 1).donGia, 500);
  assert.equal(fixture.services[0].donGia, 900);
  await noOverflow();
}

module.exports = { createDanhMucFixture, kiemTraDanhMuc };
