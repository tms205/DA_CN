USE PhotocopyChain;
GO

CREATE TABLE ChiNhanh (
    MaChiNhanh   INT IDENTITY(1,1) PRIMARY KEY,
    TenChiNhanh  NVARCHAR(100) NOT NULL,
    DiaChi       NVARCHAR(255) NOT NULL,
    SoDienThoai  VARCHAR(15),
    ViDo         DECIMAL(9,6),
    KinhDo       DECIMAL(9,6),
    GioMoCua     TIME DEFAULT '09:00',
    GioDongCua   TIME DEFAULT '21:00',
    BanKinhGiao  DECIMAL(5,2) DEFAULT 10, 
    TrangThai    NVARCHAR(20) DEFAULT N'HOAT_DONG'
);
GO

CREATE TABLE NguoiDung (
    MaNguoiDung  INT IDENTITY(1,1) PRIMARY KEY,
    HoTen        NVARCHAR(100) NOT NULL,
    Email        VARCHAR(100) NOT NULL UNIQUE,
    SoDienThoai  VARCHAR(15),
    MatKhau      VARCHAR(255),
    VaiTro       VARCHAR(20) NOT NULL
                 CHECK (VaiTro IN ('SUPER_ADMIN','BRANCH_ADMIN','STAFF','SHIPPER','CUSTOMER')),
    MaChiNhanh   INT REFERENCES ChiNhanh(MaChiNhanh),   -- NULL voi khach hang va Super Admin
    DiaChi       NVARCHAR(255),
    AnhDaiDien   NVARCHAR(255),
    TrangThai    NVARCHAR(20) DEFAULT N'HOAT_DONG',       -- HOAT_DONG / KHOA
    NgayTao      DATETIME DEFAULT GETDATE()
);
GO

-- Google không lưu mật khẩu, liên kết về tài khoản NguoiDung hiện có.
CREATE TABLE dbo.LienKetTaiKhoanGoogle (
    GoogleSubject VARCHAR(255) COLLATE Latin1_General_100_BIN2 NOT NULL,
    MaNguoiDung INT NOT NULL,
    NgayLienKet DATETIME2 NOT NULL CONSTRAINT DF_LienKetGoogle_Ngay DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_LienKetTaiKhoanGoogle PRIMARY KEY (GoogleSubject),
    CONSTRAINT UQ_LienKetGoogle_NguoiDung UNIQUE (MaNguoiDung),
    CONSTRAINT FK_LienKetGoogle_NguoiDung FOREIGN KEY (MaNguoiDung) REFERENCES dbo.NguoiDung(MaNguoiDung)
);
GO

CREATE TABLE MayIn (
    MaMayIn      INT IDENTITY(1,1) PRIMARY KEY,
    MaChiNhanh   INT NOT NULL REFERENCES ChiNhanh(MaChiNhanh),
    TenMay       NVARCHAR(100) NOT NULL,
    KhoGiay      VARCHAR(5) DEFAULT 'A4',       -- kho giay lon nhat in duoc
    InMau        BIT DEFAULT 0,
    In2Mat       BIT DEFAULT 0,
    TocDo        INT DEFAULT 20,                -- trang/phut
    SoTrangDaIn  INT DEFAULT 0,                 -- chi so dem trang tren may
    TrangThai    NVARCHAR(20) DEFAULT N'HOAT_DONG'   -- HOAT_DONG / BAO_TRI / HONG
);
GO

CREATE TABLE VatTu (
    MaVatTu      INT IDENTITY(1,1) PRIMARY KEY,
    TenVatTu     NVARCHAR(100) NOT NULL,
    DonViTinh    NVARCHAR(20),
    DonGia       DECIMAL(12,0) DEFAULT 0
);
GO

CREATE TABLE DichVu (
    MaDichVu     INT IDENTITY(1,1) PRIMARY KEY,
    TenDichVu    NVARCHAR(100) NOT NULL,
    LoaiDichVu   VARCHAR(10) NOT NULL CHECK (LoaiDichVu IN ('IN_AN','GIA_CONG')),
    KhoGiay      VARCHAR(5),
    LoaiGiay     VARCHAR(20),
    MatIn        VARCHAR(10),                 
    MauSac       VARCHAR(10),                   
    NhomGiaCong  VARCHAR(20),                   
    KichThuocAnh VARCHAR(10),                   -- NULL: tai lieu; anh the tinh tien theo anh
    DonGia       DECIMAL(12,0) NOT NULL,
    MaVatTu      INT REFERENCES VatTu(MaVatTu),
    TrangThai    NVARCHAR(20) DEFAULT N'HIEN',
    CONSTRAINT CK_DichVu_AnhThe CHECK (KichThuocAnh IS NULL OR
      (KichThuocAnh IN ('2X3','3X4','4X6','35X45','5X5') AND LoaiDichVu='IN_AN'
       AND KhoGiay IS NOT NULL AND KhoGiay='A4' AND LoaiGiay IS NOT NULL AND LoaiGiay='GIAY_ANH'
       AND MatIn IS NOT NULL AND MatIn='1_MAT' AND MauSac IS NOT NULL AND MauSac='MAU'))
);
GO

-- Danh muc dich vu dung chung, moi chi nhanh co gia va trang thai ban rieng.
CREATE TABLE BangGiaChiNhanh (
    MaChiNhanh   INT NOT NULL REFERENCES ChiNhanh(MaChiNhanh),
    MaDichVu     INT NOT NULL REFERENCES DichVu(MaDichVu),
    DonGia       DECIMAL(12,0) NOT NULL CHECK (DonGia >= 0),
    TrangThai    NVARCHAR(20) NOT NULL DEFAULT N'HIEN'
                 CHECK (TrangThai IN (N'HIEN',N'AN')),
    PRIMARY KEY (MaChiNhanh, MaDichVu)
);
GO

CREATE TABLE TonKho (
    MaChiNhanh   INT REFERENCES ChiNhanh(MaChiNhanh),
    MaVatTu      INT REFERENCES VatTu(MaVatTu),
    SoLuong      INT DEFAULT 0 CHECK (SoLuong >= 0),
    MucToiThieu  INT DEFAULT 0,
    PRIMARY KEY (MaChiNhanh, MaVatTu)
);
GO

CREATE TABLE KhuyenMai (
    MaKhuyenMai  INT IDENTITY(1,1) PRIMARY KEY,
    MaChiNhanh   INT NOT NULL REFERENCES ChiNhanh(MaChiNhanh),
    MaCode       VARCHAR(30) NOT NULL,
    TenKhuyenMai NVARCHAR(150),
    PhanTramGiam INT NOT NULL CHECK (PhanTramGiam BETWEEN 1 AND 100),
    GiamToiDa    DECIMAL(12,0),
    DonToiThieu  DECIMAL(12,0) DEFAULT 0,
    NgayBatDau   DATE,
    NgayKetThuc  DATE,
    SoLuong      INT DEFAULT 0,
    DaDung       INT DEFAULT 0,
    TrangThai    NVARCHAR(20) DEFAULT N'HOAT_DONG',
    CONSTRAINT UQ_KhuyenMai_ChiNhanh_MaCode UNIQUE (MaChiNhanh, MaCode)
);
GO

CREATE TABLE FileIn (
    MaFile       INT IDENTITY(1,1) PRIMARY KEY,
    MaKhachHang  INT NOT NULL REFERENCES NguoiDung(MaNguoiDung),
    TenFile      NVARCHAR(255) NOT NULL,
    DinhDang     VARCHAR(10),
    DungLuong    INT,
    DuongDan     NVARCHAR(500),
    SoTrang      INT,
    KichThuocAnh VARCHAR(10),
    SoLuongAnh   INT,
    TrangThai    NVARCHAR(20) DEFAULT N'SAN_SANG',   
    NgayTaiLen   DATETIME DEFAULT GETDATE(),
    CONSTRAINT CK_FileIn_AnhThe CHECK ((KichThuocAnh IS NULL AND SoLuongAnh IS NULL) OR
      (KichThuocAnh IS NOT NULL AND KichThuocAnh IN ('2X3','3X4','4X6','35X45','5X5')
       AND SoLuongAnh IS NOT NULL AND SoLuongAnh BETWEEN 1 AND 100))
);
GO

CREATE TABLE DonHang (
    MaDonHang    INT IDENTITY(1,1) PRIMARY KEY,
    MaDonHangGoc INT,                                       -- don goc neu la in lai / mua them
    LoaiPhatSinh VARCHAR(20),
    MaNguoiTaoPhatSinh INT,
    LyDoPhatSinh NVARCHAR(255),
    MaKhachHang  INT NOT NULL REFERENCES NguoiDung(MaNguoiDung),
    MaChiNhanh   INT NOT NULL REFERENCES ChiNhanh(MaChiNhanh),
    MaNhanVien   INT REFERENCES NguoiDung(MaNguoiDung),     -- tho in
    MaMayIn      INT REFERENCES MayIn(MaMayIn),
    MaKhuyenMai  INT REFERENCES KhuyenMai(MaKhuyenMai),
    HinhThucNhan VARCHAR(20) NOT NULL CHECK (HinhThucNhan IN ('TAI_QUAY','GIAO_HANG')),
    DiaChiGiao   NVARCHAR(255),
    SdtNhan      VARCHAR(15),
    InGap        BIT DEFAULT 0,
    TienHang     DECIMAL(12,0) DEFAULT 0,
    PhiShip      DECIMAL(12,0) DEFAULT 0,
    GiamGia      DECIMAL(12,0) DEFAULT 0,
    TongTien     DECIMAL(12,0) DEFAULT 0,
    TrangThai    VARCHAR(20) DEFAULT 'CHO_XAC_NHAN'
                 CHECK (TrangThai IN ('CHO_XAC_NHAN','DA_XAC_NHAN','DANG_IN','DA_IN',
                                      'DANG_GIAO','DA_GIAO','GIAO_THAT_BAI','HOAN_THANH','DA_HUY')),
    NgayDat      DATETIME DEFAULT GETDATE(),
    NgayHenTra   DATETIME,
    NgayHoanThanh DATETIME,
    GhiChu       NVARCHAR(255),
    CONSTRAINT FK_DonHang_DonHangGoc FOREIGN KEY (MaDonHangGoc) REFERENCES DonHang(MaDonHang),
    CONSTRAINT FK_DonHang_NguoiTaoPhatSinh FOREIGN KEY (MaNguoiTaoPhatSinh) REFERENCES NguoiDung(MaNguoiDung),
    CONSTRAINT CK_DonHang_LoaiPhatSinh CHECK (LoaiPhatSinh IS NULL OR LoaiPhatSinh IN ('IN_LAI_LOI_QUAY','BO_SUNG'))
);
GO

-- Tra nhanh cac viec in lai / don bo sung cua mot don goc.
CREATE INDEX IX_DonHang_MaDonHangGoc
ON DonHang(MaDonHangGoc, LoaiPhatSinh, TrangThai);
GO

CREATE TABLE ChiTietDonHang (
    MaChiTiet    INT IDENTITY(1,1) PRIMARY KEY,
    MaDonHang    INT NOT NULL REFERENCES DonHang(MaDonHang),
    MaFile       INT NOT NULL REFERENCES FileIn(MaFile),
    MaDichVu     INT NOT NULL REFERENCES DichVu(MaDichVu),
    SoTrang      INT NOT NULL,
    TuTrang      INT,
    DenTrang     INT,
    SoBan        INT DEFAULT 1,
    HuongGiay    VARCHAR(5) NOT NULL CONSTRAINT DF_ChiTietDonHang_HuongGiay DEFAULT 'DUNG',
    DonGia       DECIMAL(12,0),
    ThanhTien    DECIMAL(12,0),         -- tien in + tien gia cong cua dong nay
    KichThuocAnh VARCHAR(10),           -- snapshot quy cach va so anh khi lap don
    SoLuongAnh   INT,
    CONSTRAINT CK_ChiTietDonHang_AnhThe CHECK ((KichThuocAnh IS NULL AND SoLuongAnh IS NULL) OR
      (KichThuocAnh IS NOT NULL AND KichThuocAnh IN ('2X3','3X4','4X6','35X45','5X5')
       AND SoLuongAnh IS NOT NULL AND SoLuongAnh BETWEEN 1 AND 100
       AND SoBan IS NOT NULL AND SoBan=1 AND HuongGiay IS NOT NULL AND HuongGiay='DUNG')),
    CONSTRAINT UQ_ChiTietDonHang_DonHang_File UNIQUE (MaDonHang, MaFile),
    CONSTRAINT CK_ChiTietDonHang_HuongGiay CHECK (
        (HuongGiay COLLATE Latin1_General_100_BIN2 = 'DUNG' AND DATALENGTH(HuongGiay) = 4) OR
        (HuongGiay COLLATE Latin1_General_100_BIN2 = 'NGANG' AND DATALENGTH(HuongGiay) = 5)),
    CONSTRAINT CK_ChiTietDonHang_PhamViTrang CHECK (
        (TuTrang IS NULL AND DenTrang IS NULL) OR
        (TuTrang >= 1 AND DenTrang >= TuTrang AND SoTrang = DenTrang - TuTrang + 1)
    )
);
GO

CREATE TABLE CaLamViec (
    MaCa         INT IDENTITY(1,1) PRIMARY KEY,
    MaChiNhanh   INT NOT NULL REFERENCES ChiNhanh(MaChiNhanh),
    MaNhanVien   INT NOT NULL REFERENCES NguoiDung(MaNguoiDung),
    GioMoCa      DATETIME DEFAULT GETDATE(),
    GioDongCa    DATETIME,
    TienDauCa    DECIMAL(12,0) DEFAULT 0,
    DoanhThuTrongCa    DECIMAL(12,0) DEFAULT 0,   
    TongDoanhThuCuoiCa DECIMAL(12,0),             -- so tien nhan vien tu dem va khai khi dong ca
    TienQuanLyDemCuoiCa DECIMAL(12,0),             -- so tien quan ly kiem dem doc lap khi duyet
    TienChi      DECIMAL(12,0) DEFAULT 0,      
    TienBu       DECIMAL(12,0) DEFAULT 0, 
    GhiChu       NVARCHAR(255),
    MaNguoiXacNhan INT REFERENCES NguoiDung(MaNguoiDung),
    TrangThai    VARCHAR(20) DEFAULT 'DANG_MO' 
);
GO

-- Một người chỉ có một ca đang mở; nhiều nhân viên cùng chi nhánh được mở ca riêng.
CREATE UNIQUE INDEX UX_CaLamViec_MotCaDangMoMoiNguoiDung
ON CaLamViec(MaNhanVien)
WHERE TrangThai = 'DANG_MO';
GO

-- Hỗ trợ sp_MoCa khóa/tra cứu các ca chưa kết thúc theo chi nhánh.
CREATE INDEX IX_CaLamViec_ChiNhanh_TrangThai_NhanVien
ON CaLamViec(MaChiNhanh, TrangThai, MaNhanVien);
GO

-- 12. Thanh toan
CREATE TABLE ThanhToan (
    MaThanhToan  INT IDENTITY(1,1) PRIMARY KEY,
    MaDonHang    INT NOT NULL UNIQUE REFERENCES DonHang(MaDonHang),
    PhuongThuc   VARCHAR(10) NOT NULL
        CONSTRAINT CK_ThanhToan_PhuongThuc CHECK (PhuongThuc IN ('VIETQR','COD')),
    SoTien       DECIMAL(12,0) NOT NULL,
    TrangThai    VARCHAR(20) DEFAULT 'CHUA_THANH_TOAN', 
    MaGiaoDich   VARCHAR(50),
    MaCa         INT REFERENCES CaLamViec(MaCa),
    NgayThanhToan DATETIME
);
GO

CREATE TABLE GiaoHang (
    MaGiaoHang   INT IDENTITY(1,1) PRIMARY KEY,
    MaDonHang    INT NOT NULL REFERENCES DonHang(MaDonHang),
    MaShipper    INT NOT NULL REFERENCES NguoiDung(MaNguoiDung),
    LanGiao      INT DEFAULT 1,
    TrangThai    VARCHAR(20) DEFAULT 'DANG_GIAO',  
    LyDo         NVARCHAR(255),
    TienThu      DECIMAL(12,0) DEFAULT 0,  
    DaNopTien    BIT DEFAULT 0,
    NgayNhan     DATETIME DEFAULT GETDATE(),
    NgayGiao     DATETIME
);
GO

CREATE TABLE DanhGia (
    MaDanhGia    INT IDENTITY(1,1) PRIMARY KEY,
    MaDonHang    INT NOT NULL UNIQUE REFERENCES DonHang(MaDonHang),
    SoSao        INT NOT NULL CHECK (SoSao BETWEEN 1 AND 5),
    NhanXet      NVARCHAR(500),
    TrangThai    NVARCHAR(10) DEFAULT N'HIEN',
    NgayDanhGia  DATETIME DEFAULT GETDATE()
);
GO

CREATE TABLE PhieuNhap (
    MaPhieuNhap  INT IDENTITY(1,1) PRIMARY KEY,
    MaChiNhanh   INT NOT NULL REFERENCES ChiNhanh(MaChiNhanh),
    MaVatTu      INT NOT NULL REFERENCES VatTu(MaVatTu),
    NhaCungCap   NVARCHAR(150),
    SoLuong      INT NOT NULL CHECK (SoLuong > 0),
    DonGia       DECIMAL(12,0) NOT NULL,
    MaNguoiLap   INT REFERENCES NguoiDung(MaNguoiDung),
    MaNguoiDuyet INT REFERENCES NguoiDung(MaNguoiDung),
    TrangThai    VARCHAR(20) DEFAULT 'CHO_DUYET',
    NgayLap      DATETIME DEFAULT GETDATE()
);
GO

CREATE TABLE ThongBao (
    MaThongBao   INT IDENTITY(1,1) PRIMARY KEY,
    MaNguoiDung  INT NOT NULL REFERENCES NguoiDung(MaNguoiDung),
    MaDonHang    INT REFERENCES DonHang(MaDonHang),
    TieuDe       NVARCHAR(150),
    NoiDung      NVARCHAR(500),
    DaDoc        BIT DEFAULT 0,
    NgayTao      DATETIME DEFAULT GETDATE()
);
GO

CREATE TABLE ChiTietGiaCong (
    MaChiTiet    INT NOT NULL REFERENCES ChiTietDonHang(MaChiTiet),
    MaDichVu     INT NOT NULL REFERENCES DichVu(MaDichVu),
    GiaGiaCong   DECIMAL(12,0) DEFAULT 0,
    PRIMARY KEY (MaChiTiet, MaDichVu)
);
GO
PRINT N'Da tao 17 bang';
GO
