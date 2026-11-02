import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/api.dart';
import 'package:photocopy_mobile/screens/don_hang/don_hang_screen.dart';
import 'package:photocopy_mobile/theme.dart';

Map<String,dynamic> don(int id,String status)=>{
  'maDonHang':id,'maDonHangHienThi':'DH${id.toString().padLeft(5,'0')}',
  'tenChiNhanh':'Cơ sở thử','hinhThucNhan':'GIAO_HANG',
  'trangThaiDonHang':status,'trangThaiThanhToan':'DA_THANH_TOAN',
  'phuongThucThanhToan':'VIETQR','tongTien':25000,
  'ngayDat':'2026-11-02T08:00:00','soFile':1,'tongSoTrang':5,
};

MockClient api(List<Map<String,dynamic>> Function() data)=>MockClient((req) async {
  expect(req.url.path,'/api/don-hang/cua-toi');expect(req.method,'GET');
  return http.Response(jsonEncode(data()),200,headers:{'content-type':'application/json; charset=utf-8'});
});

void main(){
  setUp(()=>AppTheme.dungGoogleFonts=false);
  Future<void> mo(WidgetTester tester,{String nhom='TAT_CA',double width=390,Future<KetQuaApi> Function()? tai}) async {
    tester.view.physicalSize=Size(width,844);tester.view.devicePixelRatio=1;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(MaterialApp(home:DonHangScreen(nhomBanDau:nhom,taiDanhSach:tai)));
    await tester.pumpAndSettle();
  }
  Future<void> loc(WidgetTester tester,String label) async {
    final ngang=find.byWidgetPredicate((w)=>w is Scrollable && w.axisDirection==AxisDirection.right);
    tester.state<ScrollableState>(ngang).position.jumpTo(0);
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(find.text(label),140,scrollable:ngang);
    await tester.pumpAndSettle();
    await tester.tap(find.text(label));await tester.pumpAndSettle();
  }

  testWidgets('Đơn thất bại có tab và số đếm riêng, không nằm trong Đang giao',(tester) async {
    await http.runWithClient(() async {
      await mo(tester);
      await loc(tester,'Đang giao · 1');
      expect(find.text('DH00001'),findsOneWidget);expect(find.text('DH00002'),findsNothing);
      await loc(tester,'Giao thất bại · 1');
      expect(find.text('DH00002'),findsOneWidget);expect(find.text('DH00001'),findsNothing);
      await loc(tester,'Tất cả · 3');
      expect(find.text('DH00001'),findsOneWidget);expect(find.text('DH00002'),findsOneWidget);
      expect(tester.takeException(),isNull);
      await tester.pumpWidget(const SizedBox());
    },()=>api(()=>[don(1,'DANG_GIAO'),don(2,'GIAO_THAT_BAI'),don(3,'HOAN_THANH')]));
  });

  testWidgets('Tải trạng thái mới tự đổi nhóm; giao lại quay về Đang giao',(tester) async {
    String status='DANG_GIAO';
    await http.runWithClient(() async {
      await mo(tester,nhom:'DANG_GIAO',tai:() async=>KetQuaApi()..thanhCong=true..duLieu=[don(1,status)]);
      expect(find.text('DH00001'),findsOneWidget);
      status='GIAO_THAT_BAI';
      var reload=tester.state<RefreshIndicatorState>(find.byType(RefreshIndicator)).show();
      await tester.pumpAndSettle();await reload;
      expect(find.text('DH00001'),findsNothing);
      await loc(tester,'Giao thất bại · 1');expect(find.text('DH00001'),findsOneWidget);
      status='DANG_GIAO';
      reload=tester.state<RefreshIndicatorState>(find.byType(RefreshIndicator)).show();
      await tester.pumpAndSettle();await reload;
      expect(find.text('DH00001'),findsNothing);
      await loc(tester,'Đang giao · 1');expect(find.text('DH00001'),findsOneWidget);
      await tester.pumpWidget(const SizedBox());
    },()=>api(()=>[don(1,status)]));
  });

  testWidgets('Mở sẵn nhóm thất bại trên màn 320px không tràn',(tester) async {
    await http.runWithClient(() async {
      await mo(tester,nhom:'GIAO_THAT_BAI',width:320);
      expect(find.text('DH00002'),findsOneWidget);expect(find.text('DH00001'),findsNothing);
      expect(tester.takeException(),isNull);
      await tester.pumpWidget(const SizedBox());
    },()=>api(()=>[don(1,'DANG_GIAO'),don(2,'GIAO_THAT_BAI')]));
  });
}
