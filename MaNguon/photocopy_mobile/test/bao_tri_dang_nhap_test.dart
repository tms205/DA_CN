import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/screens/xac_thuc/dang_ky_screen.dart';
import 'package:photocopy_mobile/screens/xac_thuc/dang_nhap_screen.dart';
import 'package:photocopy_mobile/screens/xac_thuc/quen_mat_khau_screen.dart';
import 'package:photocopy_mobile/theme.dart';
import 'package:photocopy_mobile/widgets/widgets.dart';

http.Response json(Object data, [int status = 200]) => http.Response(
  jsonEncode(data), status, headers: {'content-type': 'application/json; charset=utf-8'},
);

void main() {
  setUp(() => AppTheme.dungGoogleFonts = false);

  Future<void> mo(WidgetTester tester) async {
    tester.view.physicalSize = const Size(1170, 2532);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(const MaterialApp(home: DangNhapScreen()));
    await tester.pumpAndSettle();
  }

  Future<void> bam(WidgetTester tester, String label) async {
    await tester.ensureVisible(find.text(label));
    await tester.tap(find.text(label));
    await tester.pumpAndSettle();
  }

  for (final full in [true, false]) {
    testWidgets('Chỉ một thông báo khi bấm nhiều thao tác; bảo trì toàn bộ=$full', (tester) async {
      final requests = <String>[];
      await http.runWithClient(() async {
        await mo(tester);
        for (final label in ['Đăng nhập','Quên mật khẩu?','Đăng ký ngay','Tiếp tục với Google','Đăng nhập']) {
          await bam(tester,label);
          expect(find.byType(HopThongBao),findsOneWidget);
          expect(find.textContaining('Nâng cấp hệ thống thử nghiệm'),findsOneWidget);
          expect(find.byType(DangNhapScreen),findsOneWidget);
          expect(find.byType(DangKyScreen),findsNothing);
          expect(find.byType(QuenMatKhauScreen),findsNothing);
        }
        expect(requests.every((x)=>x=='/api/bao-tri/trang-thai'),isTrue);
        expect(tester.takeException(),isNull);
      },()=>MockClient((req) async {
        requests.add(req.url.path);
        return json({'dangBaoTri':full,'tinhNangKhoa':full?[]:['DANG_NHAP'],
          'loiNhan':'Nâng cấp hệ thống thử nghiệm. Dự kiến mở lại lúc 18:00.'});
      }));
    });
  }

  testWidgets('Kết thúc bảo trì bỏ thông báo cũ và mở Quên mật khẩu bình thường', (tester) async {
    bool active=true;
    await http.runWithClient(() async {
      await mo(tester);
      expect(find.byType(HopThongBao),findsOneWidget);
      active=false;
      await bam(tester,'Quên mật khẩu?');
      expect(find.byType(QuenMatKhauScreen),findsOneWidget);
      Navigator.of(tester.element(find.byType(QuenMatKhauScreen))).pop();
      await tester.pumpAndSettle();
      expect(find.byType(HopThongBao),findsNothing);
    },()=>MockClient((_) async => json({'dangBaoTri':active,'loiNhan':'Thông báo cũ','tinhNangKhoa':[]})));
  });

  for(final google in [false,true]) {
    testWidgets('Bật bảo trì giữa lúc gửi ${google?'Google':'mật khẩu'} không tạo lỗi thứ hai', (tester) async {
      bool active=false;
      await http.runWithClient(() async {
        await mo(tester);
        if(!google) {
          await tester.enterText(find.byType(TextField).at(0),'test@gmail.com');
          await tester.enterText(find.byType(TextField).at(1),'Abcdef12');
        }
        await bam(tester,google?'Tiếp tục với Google':'Đăng nhập');
        expect(active,isTrue);
        expect(find.byType(HopThongBao),findsOneWidget);
        expect(find.textContaining('CEO đang nâng cấp'),findsOneWidget);
      },()=>MockClient((req) async {
        if(req.url.path=='/api/bao-tri/trang-thai') return json({'dangBaoTri':active,'loiNhan':'CEO đang nâng cấp','tinhNangKhoa':[]});
        active=true;
        return json({'dangBaoTri':true,'message':'Hệ thống đang bảo trì.\nCEO đang nâng cấp'},503);
      }));
    });
  }

  testWidgets('Bật bảo trì xóa lỗi Google cũ; tắt bảo trì không hiện lại lỗi cũ', (tester) async {
    bool active=false;
    await http.runWithClient(() async {
      await mo(tester);
      await bam(tester,'Tiếp tục với Google');
      expect(find.text('Lỗi Google trước đó'),findsOneWidget);
      active=true;
      await bam(tester,'Quên mật khẩu?');
      expect(find.byType(HopThongBao),findsOneWidget);
      expect(find.text('Lỗi Google trước đó'),findsNothing);
      active=false;
      await bam(tester,'Quên mật khẩu?');
      Navigator.of(tester.element(find.byType(QuenMatKhauScreen))).pop();
      await tester.pumpAndSettle();
      expect(find.byType(HopThongBao),findsNothing);
    },()=>MockClient((req) async => req.url.path=='/api/bao-tri/trang-thai'
        ?json({'dangBaoTri':active,'loiNhan':'Nâng cấp hệ thống','tinhNangKhoa':[]})
        :json({'message':'Lỗi Google trước đó'},400)));
  });

  testWidgets('Lỗi mật khẩu bình thường vẫn hiển thị', (tester) async {
    await http.runWithClient(() async {
      await mo(tester);
      await tester.enterText(find.byType(TextField).at(0),'test@gmail.com');
      await tester.enterText(find.byType(TextField).at(1),'Abcdef12');
      await bam(tester,'Đăng nhập');
      expect(find.text('Tài khoản hoặc mật khẩu không đúng.'),findsOneWidget);
      expect(find.byType(HopThongBao),findsOneWidget);
    },()=>MockClient((req) async => req.url.path=='/api/bao-tri/trang-thai'
        ?json({'dangBaoTri':false,'loiNhan':'','tinhNangKhoa':[]})
        :json({'message':'Tài khoản hoặc mật khẩu không đúng.'},401)));
  });
}
