// UI thật, API giả lập hoàn toàn: không gửi đơn, thanh toán, in hay ghi CSDL thật.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const web = process.env.PHOTOCOPY_WEB_URL || 'http://localhost:5078';
const output = process.env.PHOTOCOPY_QA_OUTPUT || path.join(require('node:os').tmpdir(), 'PhotocopyPopupAdminBrowser');
const now = '2026-10-16T10:00:00';

async function run() {
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  try {
    for (const width of JSON.parse(process.env.PHOTOCOPY_POPUP_WIDTHS || '[1440,768,390,320]')) {
      const height = width < 600 ? 740 : 900;
      const context = await browser.newContext({ viewport: { width, height } });
      await context.tracing.start({ screenshots: true, snapshots: true });
      const page = await context.newPage(), errors = [], writes = [];
      let failNext = false, holdNext = false, release;
      const user = { maNguoiDung: 1, hoTen: 'CEO kiểm thử', email: 'ceotest@gmail.com', soDienThoai: '0901234567', tenVaiTro: 'SUPER_ADMIN', maChiNhanh: null };
      const cn = { maChiNhanh: 1, tenChiNhanh: 'Chi nhánh thử', diaChi: '140 Lê Trọng Tấn', soDienThoai: '0901234567', gioMoCua: '09:00:00', gioDongCua: '21:00:00', banKinhGiao: 10, trangThai: 'HOAT_DONG' };
      const dv = { maDichVu: 1, maChiNhanh: 1, tenChiNhanh: cn.tenChiNhanh, tenDichVu: 'In A4 thử', loaiDichVu: 'IN_AN', khoGiay: 'A4', loaiGiay: '70gsm', matIn: '1_MAT', mauSac: 'DEN_TRANG', donGia: 300, trangThai: 'HIEN' };
      const may = { maMayIn: 1, maChiNhanh: 1, tenMay: 'Ricoh thử', tenChiNhanh: cn.tenChiNhanh, khoGiay: 'A3', tocDo: 20, soTrangDaIn: 100, trangThai: 'HOAT_DONG', in2Mat: true };
      const ca = { maCa: 1, maNhanVien: 1, maChiNhanh: 1, tenNhanVien: user.hoTen, tenChiNhanh: cn.tenChiNhanh, gioMoCa: now, tienDauCa: 200000, doanhThuTrongCa: 5000, doanhThuVietQr: 10000, trangThai: 'DANG_MO' };
      const phieu = { maPhieuNhap: 1, maChiNhanh: 1, tenChiNhanh: cn.tenChiNhanh, maVatTu: 1, tenVatTu: 'Giấy A4', nhaCungCap: 'Nhà cung cấp thử', soLuong: 10, donGia: 50000, tenNguoiLap: user.hoTen, ngayLap: now, trangThai: 'CHO_DUYET' };
      const don = { maDonHang: 1, maDonHangHienThi: 'DH00001', maKhachHang: 5, tenKhachHang: 'Khách kiểm thử', soDienThoaiKhach: '0901234567', maChiNhanh: 1, tenChiNhanh: cn.tenChiNhanh, ngayDat: now, trangThaiDonHang: 'DA_IN', trangThaiThanhToan: 'DA_THANH_TOAN', phuongThucThanhToan: 'COD', hinhThucNhan: 'TAI_QUAY', tongTien: 300, tienHang: 300, tongSoTrang: 1, soFile: 1, quyCach: 'A4 · 1 mặt', dongIn: [{ maChiTiet: 1, maFile: 1, maDichVu: 1, tenFile: 'Test.pdf', tongSoTrangFile: 1, soTrang: 1, soBan: 1, tuTrang: 1, denTrang: 1, khoGiay: 'A4', matIn: '1_MAT', mauSac: 'DEN_TRANG', trangThaiFile: 'SAN_SANG', donGia: 300, thanhTien: 300 }], tienDo: [] };
      const lists = {
        '/api/danh-muc/chi-nhanh': [cn], '/api/quan-tri/chi-nhanh': [cn], '/api/quan-tri/may-in': [may],
        '/api/quan-tri/dich-vu': [dv], '/api/danh-muc/bang-gia': [dv],
        '/api/quan-tri/danh-muc-dich-vu': [dv], '/api/quan-tri/danh-muc-vat-tu': [{ maVatTu: 1, tenVatTu: 'Giấy A4' }],
        '/api/quan-tri/bao-cao/hien-tai': {},
        '/api/quan-tri/khuyen-mai': [{ maKhuyenMai: 1, maChiNhanh: 1, tenChiNhanh: cn.tenChiNhanh, maCode: 'TEST10', tenKhuyenMai: 'Mã thử', phanTramGiam: 10, ngayBatDau: now, ngayKetThuc: '2026-12-31T00:00:00', soLuong: 100, trangThai: 'HOAT_DONG' }],
        '/api/nhan-vien': [{ maNguoiDung: 2, hoTen: 'Nhân viên thử', email: 'test@gmail.com', tenVaiTro: 'STAFF', maChiNhanh: 1, tenChiNhanh: cn.tenChiNhanh, trangThai: 'HOAT_DONG', ngayTao: now, duocCapLai: true }],
        '/api/quan-tri/khach-hang': [{ maNguoiDung: 5, hoTen: 'Khách kiểm thử', email: 'khach@gmail.com', soDienThoai: '0901234567', diaChi: '123 Đường thử', trangThai: 'HOAT_DONG', ngayTao: now }],
        '/api/quan-tri/kho': [{ maChiNhanh: 1, tenChiNhanh: cn.tenChiNhanh, maVatTu: 1, tenVatTu: 'Giấy A4', donViTinh: 'ram', soLuong: 20, mucToiThieu: 5, donGia: 50000 }],
        '/api/quan-tri/phieu-nhap': [phieu, { ...phieu, maPhieuNhap: 2, trangThai: 'DA_DUYET' }],
        '/api/quan-tri/hoan-tien': [{ maDonHang: 3, maDonHangHienThi: 'DH00003', tenKhachHang: 'Khách thử', tenChiNhanh: cn.tenChiNhanh, soTien: 3000 }],
        '/api/don-hang/danh-gia': [{ maDanhGia: 1, maDonHangHienThi: 'DH00001', tenKhachHang: 'Khách thử', tenChiNhanh: cn.tenChiNhanh, soSao: 4, nhanXet: 'Tốt', ngayDanhGia: now, trangThai: 'HIEN' }],
        '/api/ca': [ca], '/api/ca/cho-xac-nhan': [{ ...ca, maCa: 2, maNhanVien: 2, tenNhanVien: 'Nhân viên thử', trangThai: 'CHO_XAC_NHAN', gioDongCa: now }],
        '/api/giao-hang': [{ ...don, trangThaiThanhToan: 'DA_THANH_TOAN', diaChiGiao: '123 Lê Lợi', sdtNhan: '0901234567', diaChiChiNhanh: cn.diaChi }],
        '/api/giao-hang/shipper': [{ maShipper: 3, maChiNhanh: 1, hoTen: 'Shipper thử' }],
        '/api/don-hang/hang-doi': [don], '/api/don-hang/1': don,
        '/api/don-hang/1/may-in': [{ ...may, dapUngDuoc: true }],
      };
      page.on('pageerror', e => errors.push(e.message));
      page.on('console', m => { if (m.type() === 'error' && /Unhandled exception|crit: Microsoft/i.test(m.text())) errors.push(m.text()); });
      await context.addInitScript(user => {
        const role = localStorage.getItem('popup-qa-role');
        if (role) { user.tenVaiTro = role; user.maChiNhanh = 1; }
        localStorage.setItem('photocopychain.phien', JSON.stringify({ accessToken: 'fixture-only', hetHanLuc: '2099-01-01T00:00:00Z', nguoiDung: user }));
        localStorage.setItem('photocopychain.pham-vi-chi-nhanh', '1');
        sessionStorage.setItem('photocopychain.ca-tu-dong', '1:1');
      }, user);
      await context.route('**/*', async route => {
        const req = route.request(), url = new URL(req.url()), p = url.pathname;
        if (!p.startsWith('/api/') && !p.startsWith('/hubs/')) return route.continue();
        const send = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } });
        if (req.method() === 'OPTIONS') return send({});
        if (p.startsWith('/hubs/')) return send({}, 503);
        if (req.method() === 'GET') {
          if (p === '/api/auth/me') return send(user);
          if (p === '/api/ca/dang-mo') return send(ca);
          return send(lists[p] || []);
        }
        writes.push({ path: p, method: req.method(), body: req.postData() });
        if (failNext) { failNext = false; return send({ message: 'Lỗi lưu kiểm thử — giữ nguyên dữ liệu nhập.' }, 503); }
        if (holdNext) { holdNext = false; await new Promise(resolve => { release = resolve; }); }
        if (p === '/api/auth/me') return send({ ...user, ...req.postDataJSON() });
        if (p === '/api/quan-tri/khach-hang/5') {
          const body = req.postDataJSON();
          Object.assign(lists['/api/quan-tri/khach-hang'][0], { hoTen: body.hoTen, soDienThoai: body.soDienThoai, diaChi: body.diaChiMacDinh });
          return send({ message: 'Đã cập nhật khách hàng kiểm thử.' });
        }
        if (p === '/api/don-hang/khach-tai-quay') return send({ maKhachHang: 5, hoTen: 'Khách kiểm thử', soDienThoai: '0901234567' });
        if (p === '/api/file-in') return send({ maFileIn: 1, tenFileGoc: 'Test.pdf', soTrang: 1, trangThai: 'SAN_SANG' });
        if (p === '/api/bao-gia') return send({ tienIn: 300, tongTien: 300, ngayHenTra: now, khuyenMaiHopLe: true });
        if (p === '/api/don-hang/tai-quay') return send({ maDonHang: 10, maDonHangHienThi: 'DH00010', phuongThucThanhToan: 'COD', tongTien: 300, message: 'Đã lập đơn kiểm thử' });
        return send({ message: 'Đã lưu kiểm thử.' });
      });
      const visit = async route => { await page.goto(web + route, { waitUntil: 'domcontentloaded' }); await expect(page.getByRole('heading', { level: 1 })).toBeVisible(); };
      const check = async popup => {
        await expect(popup).toBeVisible();
        const box = await popup.boundingBox();
        assert.ok(box && box.x >= 0 && box.y >= 0 && box.x + box.width <= width + 1 && box.y + box.height <= height + 1, `Popup phải vừa viewport ${width}`);
        assert.ok(await popup.evaluate(el => el.scrollWidth <= el.clientWidth + 1), `Popup không tràn ngang ${width}`);
        const close = popup.getByRole('button', { name: 'Đóng hộp thoại', exact: true });
        const cb = await close.boundingBox();
        assert.ok(cb && cb.y >= 0 && cb.y + cb.height <= height, 'Nút đóng luôn nhìn thấy');
        for (const button of await popup.locator(':scope > footer').getByRole('button').all()) {
          const bb = await button.boundingBox();
          assert.ok(bb && bb.y >= 0 && bb.y + bb.height <= height + 1, 'Nút ở chân popup không bị che khuất');
        }
      };
      const openCancel = async (route, button, title) => {
        await visit(route);
        const count = writes.length;
        await page.getByRole('button', { name: button, exact: true }).first().click();
        const popup = page.getByRole('dialog', { name: title, exact: true });
        await check(popup);
        await popup.getByRole('button', { name: 'Đóng hộp thoại', exact: true }).focus();
        await page.keyboard.press('Shift+Tab');
        assert.ok(await popup.evaluate(el => el.contains(document.activeElement)), 'Shift+Tab không rời popup');
        await page.keyboard.press('Escape');
        await expect(popup).not.toBeVisible();
        assert.equal(writes.length, count, `Mở/Hủy ${button} không được ghi API`);
        if (route === '/danh-gia' || route === '/thong-tin-ca-nhan') console.log(`Đã kiểm popup ${route} (${width}px)`);
      };
      try {
        let modal;
        if (process.env.PHOTOCOPY_POPUP_ORDER_ONLY !== '1') {
        for (const [route, button, title] of [
          ['/dich-vu', 'Thêm dịch vụ', 'Thêm dịch vụ / quy cách mới'],
          ['/dich-vu', 'Sửa dịch vụ', 'Sửa danh mục dịch vụ #1'],
          ['/bang-gia', 'Sửa giá', 'Sửa giá: In A4 thử'],
          ['/khuyen-mai', 'Thêm khuyến mãi', 'Tạo mã khuyến mãi mới'],
          ['/quan-tri', 'Thêm chi nhánh', 'Thêm chi nhánh mới'],
          ['/quan-tri', 'Sửa chi nhánh', 'Sửa chi nhánh'],
          ['/quan-tri', 'Thêm máy in', 'Thêm máy in mới'],
          ['/quan-tri', 'Sửa chỉ số', 'Sửa chỉ số trang in'],
          ['/quan-tri', 'Bảo trì', 'Trạng thái máy in'],
          ['/kho-vat-tu', 'Lập phiếu nhập', 'Lập và duyệt phiếu nhập vật tư'],
          ['/kho-vat-tu', 'Nhận đủ 10', 'Nhận vật tư vào kho'],
          ['/nhan-vien', 'Thêm nhân viên', 'Tạo tài khoản nhân viên mới'],
          ['/nhan-vien', 'Khóa', 'Trạng thái nhân viên'],
          ['/khach-hang', 'Chi tiết', 'Chi tiết khách hàng'],
          ['/khach-hang', 'Sửa', 'Sửa khách hàng'],
          ['/khach-hang', 'Khóa', 'Xác nhận trạng thái tài khoản'],
          ['/phe-duyet', 'Duyệt', 'Phê duyệt phiếu nhập'],
          ['/phe-duyet', 'Từ chối', 'Phê duyệt phiếu nhập'],
          ['/phe-duyet', 'Đã chuyển hoàn', 'Xác nhận hoàn tiền'],
          ['/danh-gia', 'Ẩn đánh giá', 'Ẩn đánh giá'],
          ['/ca-lam-viec', 'Đóng ca & kiểm đếm', 'Đóng ca — kiểm đếm tiền'],
          ['/ca-lam-viec', 'Kiểm tra & xác nhận', 'Kiểm đếm & xác nhận ca'],
          ['/ban-giao', 'In lại do lỗi quầy', 'In lại do lỗi quầy'],
          ['/ban-giao', 'Khách mua thêm', 'Khách mua thêm'],
          ['/ban-giao', 'Bàn giao', 'Bàn giao tại quầy'],
          ['/giao-hang', 'Bàn giao đi giao', 'Phân công & bàn giao shipper'],
          ['/thong-tin-ca-nhan', 'Sửa hồ sơ', 'Sửa hồ sơ cá nhân'],
          ['/thong-tin-ca-nhan', 'Đổi mật khẩu', 'Đổi mật khẩu'],
        ]) await openCancel(route, button, title);

        // Nút submit ở footer ngoài form vẫn chạy đúng form; lỗi không làm mất dữ liệu.
        await visit('/bang-gia');
        await page.getByRole('button', { name: 'Sửa giá', exact: true }).click();
        const edit = page.getByRole('dialog', { name: 'Sửa giá: In A4 thử', exact: true });
        await edit.getByLabel('Đơn giá (₫)').fill('450');
        const before = writes.length;
        failNext = true;
        await edit.getByRole('button', { name: 'Lưu giá', exact: true }).click();
        await expect(edit.getByRole('alert')).toContainText('Lỗi lưu kiểm thử');
        await expect(edit.getByLabel('Đơn giá (₫)')).toHaveValue('450');
        holdNext = true;
        await edit.getByRole('button', { name: 'Lưu giá', exact: true }).click();
        await expect(edit.getByRole('button', { name: 'Đang lưu…', exact: true })).toBeDisabled();
        await expect(edit.getByLabel('Đơn giá (₫)')).toBeDisabled();
        await page.keyboard.press('Escape');
        await expect(edit).toBeVisible();
        assert.ok(release); release();
        await expect(edit).not.toBeVisible();
        assert.equal(writes.length, before + 2);
        assert.equal(JSON.parse(writes.at(-1).body).donGia, 450);

        await visit('/quan-tri');
        await page.getByRole('button', { name: 'Sửa chỉ số', exact: true }).click();
        modal = page.getByRole('dialog', { name: 'Sửa chỉ số trang in', exact: true });
        await modal.getByLabel('Chỉ số trang in', { exact: true }).fill('777');
        await modal.getByRole('button', { name: 'Hủy', exact: true }).click();
        await page.getByRole('button', { name: 'Sửa chỉ số', exact: true }).click();
        await expect(modal.getByLabel('Chỉ số trang in', { exact: true })).toHaveValue('100');
        await modal.getByLabel('Chỉ số trang in', { exact: true }).fill('777');
        await modal.getByRole('button', { name: 'Lưu chỉ số', exact: true }).click();
        await expect(modal).not.toBeVisible();
        assert.equal(JSON.parse(writes.at(-1).body).soTrangDaIn, 777);

        await visit('/kho-vat-tu');
        await page.getByRole('button', { name: 'Lập phiếu nhập', exact: true }).click();
        modal = page.getByRole('dialog', { name: 'Lập và duyệt phiếu nhập vật tư', exact: true });
        await modal.getByPlaceholder('Tên đơn vị cung ứng').fill('NCC kiểm thử');
        await modal.getByRole('button', { name: 'Lập phiếu & duyệt trực tiếp', exact: true }).click();
        await expect(modal).not.toBeVisible();
        assert.equal(writes.at(-1).path, '/api/quan-tri/phieu-nhap');

        await visit('/thong-tin-ca-nhan');
        await page.getByRole('button', { name: 'Sửa hồ sơ', exact: true }).click();
        modal = page.getByRole('dialog', { name: 'Sửa hồ sơ cá nhân', exact: true });
        await modal.getByLabel('Họ và tên', { exact: true }).fill('Hồ sơ kiểm thử');
        await modal.getByRole('button', { name: 'Lưu thay đổi', exact: true }).click();
        await expect(modal).not.toBeVisible();
        assert.equal(JSON.parse(writes.at(-1).body).hoTen, 'Hồ sơ kiểm thử');

        // Sửa từng khách: hủy không ghi; lỗi giữ form; thành công tải lại đúng hàng.
        await visit('/khach-hang');
        const customerWrites = writes.length;
        await page.getByRole('button', { name: 'Sửa', exact: true }).click();
        modal = page.getByRole('dialog', { name: 'Sửa khách hàng', exact: true });
        await expect(modal.getByLabel('Email đăng nhập', { exact: true })).toHaveAttribute('readonly', '');
        await modal.getByLabel('Họ và tên', { exact: true }).fill('Tên chưa lưu');
        await modal.getByRole('button', { name: 'Hủy', exact: true }).click();
        assert.equal(writes.length, customerWrites);
        await page.getByRole('button', { name: 'Sửa', exact: true }).click();
        await expect(modal.getByLabel('Họ và tên', { exact: true })).toHaveValue('Khách kiểm thử');
        await modal.getByLabel('Số điện thoại', { exact: true }).fill('123');
        await modal.getByRole('button', { name: 'Lưu thay đổi', exact: true }).click();
        await expect(modal).toContainText('Số điện thoại phải gồm 10 chữ số');
        assert.equal(writes.length, customerWrites);
        await modal.getByLabel('Số điện thoại', { exact: true }).fill('0901234567');
        await modal.getByLabel('Họ và tên', { exact: true }).fill('Khách đã sửa');
        failNext = true;
        await modal.getByRole('button', { name: 'Lưu thay đổi', exact: true }).click();
        await expect(modal.getByRole('alert')).toContainText('Lỗi lưu kiểm thử');
        await expect(modal.getByLabel('Họ và tên', { exact: true })).toHaveValue('Khách đã sửa');
        await modal.getByRole('button', { name: 'Lưu thay đổi', exact: true }).click();
        await expect(modal).not.toBeVisible();
        await expect(page.getByRole('table')).toContainText('Khách đã sửa');
        assert.equal(writes.at(-1).path, '/api/quan-tri/khach-hang/5');
        assert.deepEqual(Object.keys(JSON.parse(writes.at(-1).body)).sort(), ['diaChiMacDinh','hoTen','soDienThoai']);

        // Mỗi mắt chỉ đổi kiểu của một ô, không gửi form và không làm mất nội dung.
        await visit('/thong-tin-ca-nhan');
        await page.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).click();
        modal = page.getByRole('dialog', { name: 'Đổi mật khẩu', exact: true });
        await check(modal);
        const passwordWrites = writes.length;
        const fields = ['Mật khẩu hiện tại', 'Mật khẩu mới', 'Nhập lại mật khẩu mới'];
        const eyes = ['mật khẩu hiện tại', 'mật khẩu mới', 'mật khẩu nhập lại'];
        for (let i = 0; i < fields.length; i++) await modal.getByLabel(fields[i], { exact: true }).fill(i ? 'MoiKiemThu123' : 'CuKiemThu123');
        for (let i = 0; i < fields.length; i++) {
          await modal.getByRole('button', { name: 'Hiện ' + eyes[i], exact: true }).click();
          for (let j = 0; j < fields.length; j++) await expect(modal.getByLabel(fields[j], { exact: true })).toHaveAttribute('type', i === j ? 'text' : 'password');
          await expect(modal.getByLabel(fields[i], { exact: true })).toHaveValue(i ? 'MoiKiemThu123' : 'CuKiemThu123');
          await modal.getByRole('button', { name: 'Ẩn ' + eyes[i], exact: true }).click();
        }
        assert.equal(writes.length, passwordWrites);
        const repeatBox = await modal.getByLabel('Nhập lại mật khẩu mới', { exact: true }).boundingBox();
        const ruleBox = await modal.getByRole('list', { name: 'Yêu cầu mật khẩu mới' }).boundingBox();
        assert.ok(ruleBox.y > repeatBox.y + repeatBox.height);
        await modal.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill('KhongKhop123');
        await modal.getByRole('button', { name: 'Lưu mật khẩu mới', exact: true }).click();
        await expect(modal).toContainText('Mật khẩu nhập lại không khớp');
        assert.equal(writes.length, passwordWrites);
        await modal.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill('MoiKiemThu123');
        await modal.getByLabel('Nhập lại mật khẩu mới', { exact: true }).press('Tab');
        await page.screenshot({ path: path.join(output, 'mat-khau-' + width + '.png'), fullPage: true });
        await modal.getByRole('button', { name: 'Lưu mật khẩu mới', exact: true }).click();
        await expect(modal).not.toBeVisible();
        assert.equal(writes.at(-1).path, '/api/auth/me/password');

        await visit('/giao-hang');
        await page.getByRole('button', { name: 'Bàn giao đi giao', exact: true }).click();
        modal = page.getByRole('dialog', { name: 'Phân công & bàn giao shipper', exact: true });
        await expect(modal.getByRole('button', { name: 'Xác nhận bàn giao', exact: true })).toBeDisabled();
        await modal.getByLabel('Shipper thuộc chi nhánh này').selectOption('3');
        await modal.getByRole('button', { name: 'Xác nhận bàn giao', exact: true }).click();
        await expect(modal).not.toBeVisible();
        assert.equal(JSON.parse(writes.at(-1).body).maShipper, 3);

        // Trang nhân viên không còn cấp mật khẩu tạm; khôi phục qua OTP ở trang đăng nhập.
        await visit('/nhan-vien');
        await expect(page.getByRole('button', { name: 'Cấp lại mật khẩu', exact: true })).toHaveCount(0);
        await expect(page.getByRole('dialog', { name: 'Mật khẩu tạm', exact: true })).toHaveCount(0);
        }

        // Lập đơn trên trang như cũ; các popup CRUD khác vẫn giữ nguyên.
        await visit('/lap-don-tai-quay');
        const order = page.getByRole('main');
        await expect(page.getByRole('dialog', { name: 'Lập đơn tại quầy', exact: true })).toHaveCount(0);
        await expect(order.getByRole('heading', { name: '1. Khách hàng', exact: true })).toBeVisible();
        await expect(order.getByRole('heading', { name: '2. Tài liệu', exact: true })).toBeVisible();
        await order.getByPlaceholder('Nguyễn Văn An').fill('Khách kiểm thử');
        await order.getByRole('button', { name: 'Tìm / tạo khách hàng', exact: true }).click();
        await order.locator('input[type=file]').setInputFiles({ name: 'Test.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4\nUI fixture') });
        await expect(order.getByRole('button', { name: 'Tính lại giá', exact: true })).toBeVisible();
        await order.getByLabel('Thanh toán', { exact: true }).selectOption('COD');
        await order.getByRole('button', { name: 'Tính lại giá', exact: true }).click();
        await expect(order.getByRole('button', { name: 'Lập đơn COD', exact: true })).toBeEnabled();
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1 && document.querySelector('main').scrollWidth <= document.querySelector('main').clientWidth + 1), `Trang lập đơn không tràn ngang ${width}px`);
        await page.screenshot({ path: path.join(output, `lap-don-${width}.png`) });
        await order.getByRole('button', { name: 'Lập đơn COD', exact: true }).click();
        await expect(order.getByText(/Đã lập đơn kiểm thử/)).toBeVisible();
        await expect(order.getByRole('button', { name: 'Tạm đóng · giữ nội dung', exact: true })).toHaveCount(0);
        await expect(order.getByPlaceholder('Nguyễn Văn An')).toHaveValue('Khách kiểm thử');
        assert.equal(writes.filter(x => x.path === '/api/don-hang/tai-quay').length, 1);
        if (process.env.PHOTOCOPY_POPUP_ORDER_ONLY === '1') {
          assert.deepEqual(errors, []);
          console.log(`PASS ${width}px: lập đơn trên trang (không popup), tạo COD, không tràn ngang.`);
          continue;
        }

        await visit('/');
        await page.getByRole('row').filter({ hasText: 'DH00001' }).click();
        await page.getByRole('button', { name: 'Hủy đơn hàng', exact: true }).click();
        modal = page.getByRole('dialog', { name: 'Hủy đơn hàng', exact: true });
        await check(modal);
        await modal.getByLabel('Lý do hủy').fill('Khách đổi yêu cầu — kiểm thử');
        await modal.getByRole('button', { name: 'Xác nhận hủy đơn', exact: true }).click();
        await expect(modal).not.toBeVisible();
        assert.equal(writes.at(-1).path, '/api/don-hang/1/huy');
        assert.match(JSON.parse(writes.at(-1).body).lyDo, /Khách đổi/);

        don.trangThaiDonHang = 'DA_XAC_NHAN';
        await visit('/');
        await page.getByRole('row').filter({ hasText: 'DH00001' }).click();
        await page.getByRole('button', { name: 'Chọn máy & bắt đầu in', exact: true }).click();
        modal = page.getByRole('dialog', { name: 'Chọn máy & bắt đầu in', exact: true });
        await check(modal);
        await modal.getByRole('radio').check();
        await modal.getByRole('button', { name: 'Bắt đầu in', exact: true }).click();
        await expect(modal).not.toBeVisible();
        assert.equal(JSON.parse(writes.at(-1).body).maMayIn, 1);

        don.phuongThucThanhToan = 'VIETQR';
        await visit('/');
        await page.getByRole('row').filter({ hasText: 'DH00001' }).click();
        await page.getByRole('button', { name: 'Phục hồi tài liệu', exact: true }).click();
        modal = page.getByRole('dialog', { name: 'Phục hồi tài liệu', exact: true });
        await check(modal);
        await modal.locator('input[type=file]').setInputFiles({ name: 'Test.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4\nUI fixture') });
        await expect(modal).not.toBeVisible();
        assert.equal(writes.at(-1).path, '/api/don-hang/1/chi-tiet/1/file');

        if (width === 1440) {
          user.tenVaiTro = 'BRANCH_ADMIN'; user.maChiNhanh = 1;
          await page.evaluate(() => localStorage.setItem('popup-qa-role', 'BRANCH_ADMIN'));
          await visit('/dich-vu');
          await expect(page.getByRole('button', { name: 'Thêm dịch vụ', exact: true })).toHaveCount(0);
          await expect(page.getByRole('button', { name: 'Sửa dịch vụ', exact: true })).toHaveCount(0);
          await visit('/quan-tri');
          await expect(page.getByRole('button', { name: 'Thêm chi nhánh', exact: true })).toHaveCount(0);
          user.tenVaiTro = 'STAFF';
          await page.evaluate(() => localStorage.setItem('popup-qa-role', 'STAFF'));
          for (const route of ['/nhan-vien', '/phe-duyet', '/quan-tri']) {
            await visit(route);
            await expect(page.getByRole('heading', { name: 'Không có quyền truy cập', exact: true })).toBeVisible();
          }
        }
        assert.deepEqual(errors, []);
        console.log(`PASS ${width}px: popup CRUD, bàn phím/Hủy/busy/lỗi, lưu giá/chỉ số/kho/hồ sơ, giao hàng, lập đơn COD, hủy/phân máy/phục hồi và quyền.`);
      } catch (e) {
        await page.screenshot({ path: path.join(output, `error-${width}.png`) });
        throw e;
      } finally {
        await context.tracing.stop({ path: path.join(output, `trace-${width}.zip`) });
        await context.close();
      }
    }
  } finally { await browser.close(); }
}
run().catch(e => { console.error(e); process.exitCode = 1; });
