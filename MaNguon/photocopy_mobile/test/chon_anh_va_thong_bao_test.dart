import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/widgets/chon_anh_bang_chung.dart';
import 'package:photocopy_mobile/screens/thong_bao/thong_bao_screen.dart';
import 'package:photocopy_mobile/screens/don_hang/tien_do_screen.dart';
import 'package:photocopy_mobile/theme.dart';
import 'bang_chung_giao_test.dart' show AnhThu;
import 'huy_don_test.dart' show don;

void main() {
  late Directory dir;
  setUp(() => dir = Directory.systemTemp.createTempSync('multi-evidence-'));
  tearDown(() => dir.deleteSync(recursive: true));
  AnhThu image(String name) {
    final f = File('${dir.path}/$name')..writeAsBytesSync([1, 2, 3]);
    return AnhThu(name, f.path);
  }

  test('One picker call can return all three images', () async {
    final files = [image('a.png'), image('b.jpg'), image('c.jpeg')];
    int calls = 0;
    final result = await chonAnhBangChung(
      daChon: [],
      chonNhieu: () async {
        calls++;
        return files;
      },
    );
    expect(result.length, 3);
    expect(calls, 1);
  });
  test(
    'Total limit includes previous images, duplicate selection does not duplicate',
    () async {
      final a = image('a.png'),
          b = image('b.png'),
          c = image('c.png'),
          d = image('d.png');
      final result = await chonAnhBangChung(
        daChon: [a.path!],
        chonNhieu: () async => [a, b, b, c],
      );
      expect(result.length, 2);
      await expectLater(
        chonAnhBangChung(daChon: [a.path!], chonNhieu: () async => [b, c, d]),
        throwsA(isA<FormatException>()),
      );
    },
  );
  test('Invalid and oversized evidence is rejected before appending', () async {
    await expectLater(
      chonAnhBangChung(daChon: [], chonNhieu: () async => [image('a.pdf')]),
      throwsA(isA<FormatException>()),
    );
    final a = image('big.png');
    File(a.path!).writeAsBytesSync(List<int>.filled(5 * 1024 * 1024 + 1, 0));
    await expectLater(
      chonAnhBangChung(daChon: [], chonNhieu: () async => [a]),
      throwsA(isA<FormatException>()),
    );
  });
  testWidgets(
    'Processed notification stays bold after read and opens exact order',
    (t) async {
      AppTheme.dungGoogleFonts = false;
      final calls = <String>[];
      http.Response json(Object body) => http.Response(
        jsonEncode(body),
        200,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
      await http.runWithClient(
        () async {
          await t.pumpWidget(const MaterialApp(home: ThongBaoScreen()));
          await t.pumpAndSettle();
          final status = find.text('ĐÃ XỬ LÝ · Xem kết quả');
          expect(status, findsOneWidget);
          expect(t.widget<Text>(status).style!.fontWeight, FontWeight.w800);
          await t.tap(find.text('CEO đã xử lý yêu cầu hoàn tiền'));
          await t.pumpAndSettle();
          expect(find.byType(TienDoScreen), findsOneWidget);
          expect(calls, contains('/api/don-hang/15'));
          await t.pumpWidget(const SizedBox());
        },
        () => MockClient((r) async {
          calls.add(r.url.path);
          if (r.url.path == '/api/thong-bao')
            return json({
              'soChuaDoc': 0,
              'danhSach': [
                {
                  'maThongBao': 5,
                  'maDonHang': 15,
                  'maDonHangHienThi': 'DH00015',
                  'tieuDe': 'CEO đã xử lý yêu cầu hoàn tiền',
                  'noiDung': 'Mở đơn để xem kết quả.',
                  'loaiThongBao': 'DON_HANG',
                  'trangThaiXuLy': 'DA_XU_LY',
                  'daDoc': true,
                  'ngayTao': '2026-11-03T10:00:00',
                },
              ],
            });
          if (r.url.path == '/api/don-hang/15') return json(don('DA_IN'));
          throw StateError('Unexpected path ${r.url.path}');
        }),
      );
    },
  );
}
