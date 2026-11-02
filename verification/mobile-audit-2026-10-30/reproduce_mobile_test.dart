// Kiểm thử tái hiện hiện trạng, không gửi yêu cầu đến máy chủ thật.
import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:photocopy_mobile/api.dart';
import 'package:photocopy_mobile/theme.dart';
import 'package:photocopy_mobile/models.dart';
import 'package:photocopy_mobile/screens/don_hang/tien_do_screen.dart';
import 'package:photocopy_mobile/screens/don_hang/don_hang_screen.dart';
import 'package:photocopy_mobile/screens/dat_in/chon_chi_nhanh_screen.dart';
import 'package:photocopy_mobile/screens/dat_in/thanh_toan_screen.dart';

http.Response jsonResponse(Object data) => http.Response(jsonEncode(data), 200,
    headers: {'content-type':'application/json; charset=utf-8'});
Map<String,dynamic> order(String status) => {
 'maDonHang':57,'maDonHangHienThi':'DH00057','maChiNhanh':1,
 'tenChiNhanh':'Chi nhánh test','hinhThucNhan':'TAI_QUAY',
 'trangThaiDonHang':status,'ngayDat':'2026-10-30T08:00:00',
 'phuongThucThanhToan':'VIETQR','trangThaiThanhToan':'DA_THANH_TOAN',
 'tienHang':5000,'tongTien':5000,'dongIn':[],'tienDo':[],
};

void main(){
 TestWidgetsFlutterBinding.ensureInitialized();
 setUpAll(()async{
  var sdk=File(Platform.resolvedExecutable).parent;
  while(!Directory('${sdk.path}/bin/cache/artifacts/material_fonts').existsSync()){
   if(sdk.parent.path==sdk.path)throw StateError('SDK font missing');
   sdk=sdk.parent;
  }
  final bytes=await File('${sdk.path}/bin/cache/artifacts/material_fonts/roboto-regular.ttf').readAsBytes();
  for(final name in ['Roboto','monospace'])await(FontLoader(name)..addFont(Future.value(ByteData.sublistView(bytes)))).load();
 });
 setUp(()=>AppTheme.dungGoogleFonts=false);

 test('R01: HTTP lỗi dạng text bị đổi thành lỗi mạng và mất mã HTTP',()async{
  await http.runWithClient(()async{
   final result=await Api.get('/api/bao-tri/phien');
   expect(result.maHttp,isNull);
   expect(result.loi,contains('Không kết nối được máy chủ'));
  },()=>MockClient((_)async=>http.Response('Unauthorized',401)));
 });

 testWidgets('R02: phản hồi danh sách cũ đến sau ghi đè trạng thái mới',(tester)async{
  int calls=0;final old=Completer<void>();
  await http.runWithClient(()async{
   await tester.pumpWidget(const MaterialApp(home:DonHangScreen()));
   await tester.pumpAndSettle();
   final refresh=tester.widget<RefreshIndicator>(find.byType(RefreshIndicator)).onRefresh;
   final older=refresh();await tester.pump();
   final newer=refresh();await tester.pump();await newer;await tester.pumpAndSettle();
   expect(find.text('Đang in ấn'),findsOneWidget);
   old.complete();await older;await tester.pumpAndSettle();
   expect(find.text('Chờ tiếp nhận'),findsOneWidget);
   expect(calls,3);
   await tester.pumpWidget(const SizedBox());
  },()=>MockClient((_)async{
   final n=++calls;if(n==2)await old.future;
   return jsonResponse([order(n==3?'DANG_IN':'CHO_XAC_NHAN')]);
  }));
 });

 testWidgets('R03: màn đơn đang chờ không tự cập nhật sau 25 giây khi không có SignalR',(tester)async{
  int calls=0;
  await http.runWithClient(()async{
   await tester.pumpWidget(const MaterialApp(home:TienDoScreen(maDonHang:57)));
   await tester.pumpAndSettle();
   await tester.pump(const Duration(seconds:25));
   expect(calls,1);
   await tester.pumpWidget(const SizedBox());
  },()=>MockClient((_)async{calls++;return jsonResponse(order('CHO_XAC_NHAN'));}));
 });

 testWidgets('R04: vẫn đổi chi nhánh được trong lúc yêu cầu đặt đơn cũ đang chờ',(tester)async{
  final pending=Completer<KetQuaApi>();Map<String,dynamic>? sent;final quoted=<int>[];
  Future<KetQuaApi> post(String route,Map<String,dynamic> body)async{
   if(route.endsWith('/chi-nhanh-phu-hop'))return KetQuaApi()..thanhCong=true..duLieu=[
    {'maChiNhanh':1,'tenChiNhanh':'Chi nhánh Một','diaChi':'Địa chỉ một'},
    {'maChiNhanh':2,'tenChiNhanh':'Chi nhánh Hai','diaChi':'Địa chỉ hai'}];
   if(route=='/api/bao-gia'){quoted.add(body['maChiNhanh'] as int);return KetQuaApi()..thanhCong=true..duLieu={'tienIn':5000,'tongTien':5000};}
   sent=Map.from(body);return pending.future;
  }
  await tester.pumpWidget(MaterialApp(home:ChonChiNhanhScreen(
   file:FileIn.fromJson({'maFileIn':1,'tenFileGoc':'test.pdf','soTrang':1,'trangThaiFile':'SAN_SANG'}),
   maDichVu:1,tenCauHinh:'A4',soBan:1,dsGiaCong:const[],hoaToc:false,guiYeuCau:post)));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Đặt đơn và chuyển khoản'));await tester.pump();
  expect(sent?['maChiNhanh'],1);
  await tester.ensureVisible(find.text('Chi nhánh Hai'));
  await tester.tap(find.text('Chi nhánh Hai'));await tester.pump(const Duration(milliseconds:400));
  await tester.pump();
  // Giá được gọi lại theo nhánh 2 trong khi body gửi trước đó là nhánh 1.
  expect(quoted.last,2);
  expect(sent?['maChiNhanh'],1);
  pending.complete(KetQuaApi()..loi='Fixture kết thúc yêu cầu');
  await tester.pumpAndSettle();
  expect(tester.takeException(),isNull);
  await tester.pumpWidget(const SizedBox());
 });

 testWidgets('R05: hộp đánh giá 5 sao tràn ngang ở màn rộng 320dp',(tester)async{
  tester.view.physicalSize=const Size(320,844);tester.view.devicePixelRatio=1;
  addTearDown(tester.view.reset);
  await http.runWithClient(()async{
   await tester.pumpWidget(const MaterialApp(home:TienDoScreen(maDonHang:57)));
   await tester.pumpAndSettle();
   await tester.scrollUntilVisible(find.text('Đánh giá dịch vụ'),250);
   await tester.tap(find.text('Đánh giá dịch vụ'));await tester.pumpAndSettle();
   final error=tester.takeException();
   expect(error,isNotNull);
   expect(error.toString(),contains('overflowed'));
   await tester.pumpWidget(const SizedBox());
  },()=>MockClient((_)async=>jsonResponse(order('HOAN_THANH'))));
 });

 testWidgets('R06: màn thanh toán đã trả không đối chiếu lại sau khi resume',(tester)async{
  int reads=0;bool canceled=false;
  await http.runWithClient(()async{
   await tester.pumpWidget(const MaterialApp(home:ThanhToanScreen(maDonHang:57)));
   await tester.pumpAndSettle();
   expect(reads,1);
   tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.paused);
   canceled=true;
   tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
   await tester.pump(const Duration(seconds:6));
   expect(reads,1); // API không được đọc lại dù dữ liệu mới đã đổi ở máy chủ giả.
   await tester.pumpWidget(const SizedBox());
  },()=>MockClient((_)async{
   reads++;return jsonResponse({'maDonHang':57,'maDonHangHienThi':'DH00057',
    'phuongThuc':'VIETQR','trangThai':'DA_THANH_TOAN','soTien':5000,
    'trangThaiDonHang':canceled?'DA_HUY':'DA_XAC_NHAN',
    'hanThanhToan':DateTime.now().add(const Duration(minutes:10)).toIso8601String()});
  }));
 });
}
