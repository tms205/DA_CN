import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/api.dart';

void main() {
  final dangKy = <String, dynamic>{
    'hoTen': 'Nguyễn Văn A',
    'email': 'a@gmail.com',
    'soDienThoai': '0912345678',
    'matKhau': 'Abcd1234',
  };
  final don = <String, dynamic>{
    'maChiNhanh': 1,
    'hinhThucNhan': 'TAI_QUAY',
    'fileIn': [
      {
        'maFileIn': 1,
        'maDichVu': 1,
        'soBanIn': 1,
        'dsGiaCong': <int>[],
        'huongGiay': 'DUNG',
      },
    ],
  };
  final sai = <(String, Map<String, dynamic>)>[
    ('/api/auth/register/otp', {...dangKy, 'email': 'sv1@huit.edu.vn'}),
    ('/api/auth/register/otp', {...dangKy, 'email': 'a@gmail.com.evil.com'}),
    ('/api/auth/register/otp', {...dangKy, 'email': 'a..b@gmail.com'}),
    ('/api/auth/register/otp', {...dangKy, 'soDienThoai': '091234567'}),
    ('/api/auth/register/otp', {...dangKy, 'soDienThoai': '09123456789'}),
    ('/api/auth/register/otp', {...dangKy, 'hoTen': 'A' * 101}),
    ('/api/auth/register/otp', {...dangKy, 'matKhau': 'Aa1${'á' * 40}'}),
    ('/api/auth/register', {'email': 'a@gmail.com', 'maOtp': '12345x'}),
    ('/api/auth/forgot-password', {'email': 'a@@gmail.com'}),
    (
      '/api/auth/reset-password',
      {'email': 'sv1@huit.edu.vn', 'maOtp': '123456', 'matKhauMoi': 'weak'},
    ),
    ('/api/auth/me', {'hoTen': 'A', 'soDienThoai': '+84912345678'}),
    ('/api/auth/me', {'hoTen': 'A', 'diaChiMacDinh': 'A' * 256}),
    ('/api/auth/me/password', {'matKhauCu': '', 'matKhauMoi': 'Abcd1234'}),
    ('/api/don-hang', {...don, 'maChiNhanh': 0}),
    ('/api/don-hang', {...don, 'hinhThucNhan': 'NO'}),
    (
      '/api/don-hang',
      {...don, 'hinhThucNhan': 'GIAO_HANG', 'diaChiGiao': '140 Lê Trọng Tấn'},
    ),
    (
      '/api/don-hang',
      {...don, 'hinhThucNhan': 'GIAO_HANG', 'sdtNhan': '0912345678'},
    ),
    ('/api/don-hang', {...don, 'viDoGiao': 91, 'kinhDoGiao': 106}),
    ('/api/don-hang', {...don, 'viDoGiao': 10}),
    ('/api/don-hang', {...don, 'ghiChu': 'A' * 256}),
    ('/api/don-hang', {...don, 'maCodeKhuyenMai': 'ABC!'}),
    ('/api/don-hang', {...don, 'fileIn': null}),
    ('/api/don-hang', {...don, 'fileIn': <dynamic>[]}),
    (
      '/api/don-hang',
      {
        ...don,
        'fileIn': [null],
      },
    ),
    (
      '/api/don-hang',
      {
        ...don,
        'fileIn': [
          {'maFileIn': 1, 'maDichVu': 1, 'soBanIn': 0},
        ],
      },
    ),
    (
      '/api/don-hang',
      {
        ...don,
        'fileIn': [
          {'maFileIn': 1, 'maDichVu': 1, 'soBanIn': 1.5},
        ],
      },
    ),
    (
      '/api/don-hang',
      {
        ...don,
        'fileIn': [
          {
            'maFileIn': 1,
            'maDichVu': 1,
            'soBanIn': 1,
            'dsGiaCong': [2, 2],
          },
        ],
      },
    ),
    (
      '/api/don-hang/chi-nhanh-phu-hop',
      {
        'dsMaDichVu': [0],
      },
    ),
    ('/api/don-hang/1/huy', {'lyDo': '   '}),
    ('/api/don-hang/1/danh-gia', {'soSao': 6}),
    ('/api/don-hang/1/danh-gia', {'soSao': 5, 'nhanXet': 'A' * 501}),
    ('/api/auth/register/otp', {...dangKy, 'email': 'a@gmail.com\n'}),
    ('/api/auth/register/otp', {...dangKy, 'soDienThoai': '0912345678\n'}),
    ('/api/auth/register/otp', {...dangKy, 'matKhau': 'Aa12345\n'}),
    ('/api/don-hang', {...don, 'hinhThucNhan': null}),
    ('/api/don-hang', {...don, 'maChiNhanh': 2147483648}),
    (
      '/api/don-hang',
      {
        ...don,
        'fileIn': [
          {'maFileIn': 1, 'maDichVu': 1, 'soBanIn': 2147483648},
        ],
      },
    ),
    (
      '/api/don-hang',
      {
        ...don,
        'fileIn': [
          {'maFileIn': 1, 'maDichVu': 1},
          {'maFileIn': 1, 'maDichVu': 1},
        ],
      },
    ),
  ];

  for (var i = 0; i < sai.length; i++) {
    test('Chặn dữ liệu sai trước gửi API: $i ${sai[i].$1}', () async {
      var soRequest = 0;
      await http.runWithClient(
        () async {
          final kq =
              sai[i].$1 == '/api/auth/me' ||
                  sai[i].$1 == '/api/auth/me/password'
              ? await Api.put(sai[i].$1, sai[i].$2)
              : await Api.post(sai[i].$1, sai[i].$2);
          expect(kq.thanhCong, isFalse);
          expect(kq.loi, isNotEmpty);
          expect(soRequest, 0);
        },
        () => MockClient((_) async {
          soRequest++;
          return http.Response('{}', 200);
        }),
      );
    });
  }

  test('Tài khoản cũ và đơn hợp lệ vẫn gửi API bình thường', () async {
    await http.runWithClient(() async {
      expect(
        (await Api.post('/api/auth/login', {
          'taiKhoan': 'sv1@huit.edu.vn',
          'matKhau': 'old',
        })).thanhCong,
        isTrue,
      );
      expect(
        (await Api.post('/api/auth/forgot-password', {
          'email': 'sv1@huit.edu.vn',
        })).thanhCong,
        isTrue,
      );
      expect(
        (await Api.post('/api/auth/register/otp', {
          ...dangKy,
          'email': 'User.Name@GMAIL.COM',
        })).thanhCong,
        isTrue,
      );
      expect(
        (await Api.post('/api/auth/register/otp', {
          ...dangKy,
          'email': 'user.name+doan@gmail.com',
        })).thanhCong,
        isTrue,
      );
      expect(
        (await Api.post('/api/don-hang', {
          ...don,
          'hinhThucNhan': 'GIAO_HANG',
          'diaChiGiao': '140 Lê Trọng Tấn',
          'sdtNhan': '0912345678',
        })).thanhCong,
        isTrue,
      );
      expect((await Api.post('/api/bao-gia', don)).thanhCong, isTrue);
    }, () => MockClient((_) async => http.Response('{}', 200)));
  });

  test('Tên file quá dài không bị báo nhầm thành lỗi kết nối', () async {
    final kq = await Api.taiFile('file-khong-ton-tai.pdf', '${'A' * 252}.pdf');
    expect(kq.thanhCong, isFalse);
    expect(kq.loi, contains('Tên file'));
  });
}
