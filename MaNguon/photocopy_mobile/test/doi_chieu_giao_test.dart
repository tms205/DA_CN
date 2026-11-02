import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/api.dart';
import 'package:photocopy_mobile/screens/don_hang/gui_doi_chieu_screen.dart';
import 'package:photocopy_mobile/widgets/doi_chieu_giao_hang.dart';
import 'bang_chung_giao_test.dart' show AnhThu;

KetQuaApi ok(dynamic data) => KetQuaApi()
  ..thanhCong = true
  ..duLieu = data;
Map<String, dynamic> luot([Map<String, dynamic>? report]) => {
  'maGiaoHang': 77,
  'maDonHang': 15,
  'lanGiao': 2,
  'lyDoGiaoThatBai': 'Khách không nghe máy',
  'phanHoi': report,
};

void main() {
  testWidgets('Mở phản hồi đúng lượt; không có lượt lỗi thì ẩn', (t) async {
    int? id, lan;
    await t.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: DoiChieuGiaoHang(
            maDonHang: 15,
            tai: () async => ok([luot()]),
            moGui: (_, x, y) async {
              id = x;
              lan = y;
              return false;
            },
          ),
        ),
      ),
    );
    await t.pumpAndSettle();
    await t.tap(find.text('Gửi bằng chứng đối chiếu'));
    await t.pumpAndSettle();
    expect(id, 77);
    expect(lan, 2);
    await t.pumpWidget(const SizedBox());
    await t.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: DoiChieuGiaoHang(maDonHang: 16, tai: () async => ok([])),
        ),
      ),
    );
    await t.pumpAndSettle();
    expect(find.text('Đối chiếu giao hàng'), findsNothing);
    await t.pumpWidget(const SizedBox());
  });

  testWidgets('Đã gửi thì chỉ xem; kết luận được hiển thị cho khách', (
    t,
  ) async {
    await t.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: DoiChieuGiaoHang(
            maDonHang: 15,
            tai: () async => ok([
              luot({
                'trangThai': 'DA_XU_LY',
                'noiDung': 'Tôi có nghe cuộc gọi',
                'guiLuc': '2026-11-01T02:00:00Z',
                'anh': [],
                'ketLuan': 'Cửa hàng đã liên hệ và thống nhất giao lại.',
                'tenNguoiXuLy': 'Quản lý thử',
                'xuLyLuc': '2026-11-01T02:30:00Z',
              }),
            ]),
          ),
        ),
      ),
    );
    await t.pumpAndSettle();
    expect(find.text('Đã xử lý'), findsOneWidget);
    expect(
      find.text('Cửa hàng đã liên hệ và thống nhất giao lại.'),
      findsOneWidget,
    );
    expect(find.text('Gửi bằng chứng đối chiếu'), findsNothing);
    expect(t.takeException(), isNull);
    await t.pumpWidget(const SizedBox());
  });

  testWidgets('Lỗi tải được thử lại, không kẹt màn hình', (t) async {
    int calls = 0;
    await t.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: DoiChieuGiaoHang(
            maDonHang: 15,
            tai: () async {
              if (++calls == 1) throw StateError('Mất mạng');
              return ok([luot()]);
            },
          ),
        ),
      ),
    );
    await t.pumpAndSettle();
    await t.tap(find.text('Thử lại'));
    await t.pumpAndSettle();
    expect(find.text('Gửi bằng chứng đối chiếu'), findsOneWidget);
    expect(calls, 2);
    await t.pumpWidget(const SizedBox());
  });

  testWidgets('Màn 320px chữ lớn; bắt buộc ảnh và nội dung; lỗi giữ bản nhập', (
    t,
  ) async {
    t.view.physicalSize = const Size(320, 800);
    t.view.devicePixelRatio = 1;
    addTearDown(t.view.resetPhysicalSize);
    addTearDown(t.view.resetDevicePixelRatio);
    final dir = Directory.systemTemp.createTempSync(
      'photocopy-customer-evidence-',
    );
    final file = File('${dir.path}/cuoc-goi.png')
      ..writeAsBytesSync(
        base64Decode(
          'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=',
        ),
      );
    addTearDown(() => dir.deleteSync(recursive: true));
    int calls = 0;
    String? text;
    List<Map<String, String>>? files;
    await t.pumpWidget(
      MaterialApp(
        home: MediaQuery(
          data: const MediaQueryData(textScaler: TextScaler.linear(1.5)),
          child: GuiDoiChieuScreen(
            maGiao: 77,
            lanGiao: 2,
            chonAnh: () async => AnhThu('cuoc-goi.png', file.path),
            gui: (body, anh) async {
              calls++;
              text = body;
              files = anh;
              return KetQuaApi()..loi = 'Mất mạng, hãy thử lại.';
            },
          ),
        ),
      ),
    );
    await t.pumpAndSettle();
    Future<void> toi(String text) async {
      await t.scrollUntilVisible(
        find.text(text),
        300,
        scrollable: find.byType(Scrollable).first,
      );
      await t.ensureVisible(find.text(text));
      await t.pumpAndSettle();
    }

    await toi('Gửi bằng chứng');
    expect(
      t
          .widget<FilledButton>(
            find.widgetWithText(FilledButton, 'Gửi bằng chứng'),
          )
          .onPressed,
      isNull,
    );
    await t.scrollUntilVisible(
      find.byType(TextField),
      -300,
      scrollable: find.byType(Scrollable).first,
    );
    await t.enterText(
      find.byType(TextField),
      'Tôi đã nhận cuộc gọi của shipper',
    );
    t.testTextInput.hide();
    await t.pumpAndSettle();
    await toi('Thêm ảnh (0/3)');
    await t.runAsync(() async {
      await t.tap(find.text('Thêm ảnh (0/3)'));
      await Future<void>.delayed(const Duration(milliseconds: 100));
    });
    await t.pumpAndSettle();
    await toi('Gửi bằng chứng');
    await t.tap(find.text('Gửi bằng chứng'));
    await t.pumpAndSettle();
    expect(calls, 1);
    expect(text, 'Tôi đã nhận cuộc gọi của shipper');
    expect(
      File(files!.single['path']!).readAsBytesSync(),
      file.readAsBytesSync(),
    );
    expect(find.text('Mất mạng, hãy thử lại.'), findsOneWidget);
    await t.scrollUntilVisible(
      find.byType(TextField),
      -300,
      scrollable: find.byType(Scrollable).first,
    );
    expect(t.widget<TextField>(find.byType(TextField)).controller!.text, text);
    expect(t.takeException(), isNull);
    await t.pumpWidget(const SizedBox());
  });
}
