import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/screens/dat_in/chon_file_screen.dart';
import 'package:photocopy_mobile/theme.dart';
import 'package:photocopy_mobile/api.dart';
import 'package:photocopy_mobile/phien.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(() async {
    var sdk = File(Platform.resolvedExecutable).parent;
    while (!Directory(
      '${sdk.path}/bin/cache/artifacts/material_fonts',
    ).existsSync()) {
      if (sdk.parent.path == sdk.path) {
        throw StateError('Không tìm thấy font Flutter.');
      }
      sdk = sdk.parent;
    }
    final font = await File(
      '${sdk.path}/bin/cache/artifacts/material_fonts/roboto-regular.ttf',
    ).readAsBytes();
    await (FontLoader(
      'Roboto',
    )..addFont(Future.value(ByteData.sublistView(font)))).load();
  });
  setUp(() => AppTheme.dungGoogleFonts = false);
  test(
    'Ảnh xem trước dùng bearer header và chặn dữ liệu không phải PNG',
    () async {
      final oldToken = Phien.token;
      Phien.token = 'fixture-not-real';
      addTearDown(() => Phien.token = oldToken);
      await http.runWithClient(
        () async {
          final res = await Api.getAnhMau('DON_XIN_VIEC', 1);
          expect(res.thanhCong, isFalse);
          expect(res.loi, contains('không hợp lệ'));
        },
        () => MockClient((request) async {
          expect(request.headers['Authorization'], 'Bearer fixture-not-real');
          expect(request.url.path, '/api/file-in/mau/DON_XIN_VIEC/trang/1');
          expect(request.url.query, isEmpty);
          return http.Response(
            'not-a-png',
            200,
            headers: {'content-type': 'image/png'},
          );
        }),
      );
    },
  );
  for (final width in [320.0, 390.0]) {
    testWidgets(
      'Xem mẫu trước, quay lại không tạo file; chọn mới gửi POST, rộng $width',
      (tester) async {
        tester.view.physicalSize = Size(width * 3, 2532);
        tester.view.devicePixelRatio = 3;
        addTearDown(tester.view.reset);
        final image = File(
          '../PhotocopyChain/PhotocopyApi/TaiNguyen/MauDon/XemTruoc/xin-viec-trang-1.png',
        ).readAsBytesSync();
        int post = 0;
        bool imageError = true;
        final pages = <int>[];
        await http.runWithClient(
          () async {
            await tester.pumpWidget(
              MaterialApp(
                builder: (context, child) => MediaQuery(
                  data: MediaQuery.of(
                    context,
                  ).copyWith(textScaler: const TextScaler.linear(1.3)),
                  child: child!,
                ),
                home: const ChonFileScreen(),
              ),
            );
            await tester.tap(
              find.byType(DropdownButtonFormField<String>).first,
            );
            await tester.pumpAndSettle();
            await tester.tap(find.text('In mẫu đơn trống').last);
            await tester.pumpAndSettle();
            await tester.ensureVisible(find.text('Đơn xin việc'));
            await tester.tap(find.text('Đơn xin việc'));
            await tester.pumpAndSettle();
            expect(post, 0);
            expect(find.text('Xem mẫu đơn'), findsOneWidget);
            expect(find.text('Chọn để in'), findsOneWidget);
            expect(
              tester
                  .widget<FilledButton>(
                    find.widgetWithText(FilledButton, 'Chọn để in'),
                  )
                  .onPressed,
              isNull,
            );
            imageError = false;
            await tester.tap(find.text('Thử lại'));
            await tester.pumpAndSettle();
            expect(find.byType(Image), findsOneWidget);
            expect(find.text('Trang 1 / 2'), findsOneWidget);
            await tester.tap(find.byTooltip('Trang sau'));
            await tester.pumpAndSettle();
            expect(find.text('Trang 2 / 2'), findsOneWidget);
            expect(pages, [1, 1, 2]);
            await tester.tap(find.byTooltip('Phóng to'));
            await tester.pumpAndSettle();
            expect(tester.takeException(), isNull);
            await tester.pageBack();
            await tester.pumpAndSettle();
            expect(post, 0);
            await tester.tap(find.text('Đơn xin việc'));
            await tester.pumpAndSettle();
            await tester.tap(find.text('Chọn để in'));
            await tester.pumpAndSettle();
            expect(post, 1);
            expect(find.text('Kiểm thử không tạo file thật.'), findsOneWidget);
            expect(tester.takeException(), isNull);
            await tester.pumpWidget(const SizedBox());
          },
          () => MockClient((request) async {
            if (request.method == 'POST') {
              expect(request.url.path, '/api/file-in/mau/DON_XIN_VIEC');
              post++;
              return http.Response(
                jsonEncode({'message': 'Kiểm thử không tạo file thật.'}),
                400,
                headers: {'content-type': 'application/json; charset=utf-8'},
              );
            }
            if (request.url.path == '/api/file-in/mau') {
              return http.Response(
                jsonEncode([
                  {
                    'maMau': 'DON_XIN_VIEC',
                    'tenMau': 'Đơn xin việc',
                    'moTa': 'Bản trống',
                    'soTrang': 2,
                    'nhom': 'Việc làm',
                    'nguon': 'Nguồn kiểm thử',
                    'phamVi': 'Mẫu tham khảo; kiểm tra yêu cầu nơi nhận.',
                  },
                ]),
                200,
                headers: {'content-type': 'application/json; charset=utf-8'},
              );
            }
            if (request.url.path.contains('/trang/')) {
              pages.add(int.parse(request.url.pathSegments.last));
              if (imageError) {
                return http.Response(
                  jsonEncode({'message': 'Chưa tải được ảnh.'}),
                  503,
                  headers: {'content-type': 'application/json; charset=utf-8'},
                );
              }
              return http.Response.bytes(
                image,
                200,
                headers: {'content-type': 'image/png'},
              );
            }
            throw StateError('API ngoài dự kiến: ${request.url.path}');
          }),
        );
      },
    );
  }
}
