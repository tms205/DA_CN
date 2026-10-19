import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/api.dart';
import 'package:photocopy_mobile/dich_vu_vi_tri.dart';
import 'package:photocopy_mobile/giao_hang.dart';
import 'package:photocopy_mobile/widgets/dia_chi_giao.dart';

void main() {
  Future<void> mo(WidgetTester tester, DiaChiGiao widget) => tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: Padding(padding: const EdgeInsets.all(16), child: widget),
        ),
      ),
    ),
  );
  KetQuaApi timDuoc() => KetQuaApi()
    ..thanhCong = true
    ..duLieu = [
      {
        'diaChi': '140 Lê Trọng Tấn, TP Hồ Chí Minh',
        'viDo': 10.7,
        'kinhDo': 106.7,
        'maViTriGiao': 'fixture-vi-tri',
      },
    ];

  testWidgets('Bỏ tìm bằng chữ, GPS tự điền và chọn điểm nhận từ API', (
    tester,
  ) async {
    DiaChiNhan giaTri = const DiaChiNhan();
    Map<String, dynamic>? daGui;
    await mo(
      tester,
      DiaChiGiao(
        giaTri: giaTri,
        onChanged: (v) => giaTri = v,
        layGps: () async => const DiemGps(10.71, 106.71, 10),
        timDiaChi: (req) async {
          daGui = req;
          return timDuoc();
        },
      ),
    );
    expect(find.text('Tìm địa chỉ'), findsNothing);
    expect(
      tester.widget<TextField>(find.byType(TextField).first).readOnly,
      isTrue,
    );
    expect(daGui, isNull);
    await tester.tap(find.text('Dùng vị trí hiện tại'));
    await tester.pumpAndSettle();
    expect(daGui, {'viDo': 10.71, 'kinhDo': 106.71});
    expect(
      tester.widget<TextField>(find.byType(TextField).first).controller!.text,
      '140 Lê Trọng Tấn, TP Hồ Chí Minh',
    );
    expect(giaTri.daChon, isTrue);
    expect(giaTri.viDo, 10.7);
    expect(giaTri.kinhDo, 106.7);
    expect(giaTri.maViTriGiao, 'fixture-vi-tri');
    expect(find.text('Chạm để xác nhận điểm nhận này'), findsNothing);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets(
    'Đổi sang GPS phải vô hiệu điểm nhận cũ ngay, không đặt bằng báo giá cũ',
    (tester) async {
      const cu = DiaChiNhan(
        diaChi: 'Địa chỉ cũ đầy đủ',
        viDo: 10,
        kinhDo: 106,
        maViTriGiao: 'fixture-cu',
      );
      DiaChiNhan giaTri = cu;
      final gps = Completer<DiemGps>();
      await mo(
        tester,
        DiaChiGiao(
          giaTri: cu,
          onChanged: (v) => giaTri = v,
          layGps: () => gps.future,
          timDiaChi: (_) async => timDuoc(),
        ),
      );
      await tester.tap(find.text('Dùng vị trí hiện tại'));
      await tester.pump();
      expect(giaTri.daChon, isFalse);
      gps.complete(const DiemGps(10.71, 106.71, 10));
      await tester.pumpAndSettle();
      expect(giaTri.daChon, isTrue);
      expect(giaTri.maViTriGiao, 'fixture-vi-tri');
      await tester.pumpWidget(const SizedBox());
    },
  );

  testWidgets('Từ chối GPS báo lỗi, thử lại được và vẫn sửa chi tiết', (
    tester,
  ) async {
    DiaChiNhan giaTri = const DiaChiNhan();
    bool tuChoi = true;
    int soLanApi = 0;
    await mo(
      tester,
      DiaChiGiao(
        giaTri: giaTri,
        onChanged: (v) => giaTri = v,
        layGps: () async {
          if (tuChoi) throw StateError('Chưa được cấp quyền vị trí.');
          return const DiemGps(10.71, 106.71, 10);
        },
        timDiaChi: (_) async {
          soLanApi++;
          return timDuoc();
        },
      ),
    );
    await tester.tap(find.text('Dùng vị trí hiện tại'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Chưa được cấp quyền vị trí'), findsOneWidget);
    expect(soLanApi, 0);
    expect(giaTri.daChon, isFalse);
    expect(find.text('Tìm địa chỉ'), findsNothing);
    expect(find.text('Mở cài đặt quyền vị trí'), findsOneWidget);
    tuChoi = false;
    await tester.tap(find.text('Dùng vị trí hiện tại'));
    await tester.pumpAndSettle();
    expect(giaTri.daChon, isTrue);
    await tester.ensureVisible(find.byType(TextField).at(1));
    await tester.enterText(find.byType(TextField).at(1), 'Tầng 2, phòng 201');
    expect(giaTri.chiTiet, 'Tầng 2, phòng 201');
    expect(giaTri.viDo, 10.7);
    expect(giaTri.daChon, isTrue);
    expect(giaTri.maViTriGiao, 'fixture-vi-tri');
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('Thoát khi chờ địa chỉ không cập nhật widget đã hủy', (
    tester,
  ) async {
    final doi = Completer<KetQuaApi>();
    int soLanThayDoi = 0;
    await mo(
      tester,
      DiaChiGiao(
        giaTri: const DiaChiNhan(diaChi: 'Địa chỉ đủ để tìm'),
        onChanged: (_) => soLanThayDoi++,
        layGps: () async => const DiemGps(10.7, 106.7, 10),
        timDiaChi: (_) => doi.future,
      ),
    );
    await tester.tap(find.text('Dùng vị trí hiện tại'));
    await tester.pump();
    expect(find.text('Đang tìm vị trí…'), findsOneWidget);
    final truocKhiThoat = soLanThayDoi;
    await tester.pumpWidget(const SizedBox());
    doi.complete(timDuoc());
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(soLanThayDoi, truocKhiThoat);
  });

  testWidgets('GPS sai lệch lớn không tự chấp nhận, hướng dẫn bật chính xác', (
    tester,
  ) async {
    int soLanTim = 0;
    await mo(
      tester,
      DiaChiGiao(
        giaTri: const DiaChiNhan(),
        onChanged: (_) {},
        layGps: () async => const DiemGps(10, 106, 3000),
        timDiaChi: (_) async {
          soLanTim++;
          return timDuoc();
        },
      ),
    );
    await tester.tap(find.text('Dùng vị trí hiện tại'));
    await tester.pumpAndSettle();
    expect(find.textContaining('sai lệch lớn'), findsOneWidget);
    expect(find.textContaining('nhập tay'), findsNothing);
    expect(soLanTim, 0);
    await tester.pumpWidget(const SizedBox());
  });

  for (final loi in ['rong', 'thieu-ma', 'toa-do-sai', 'mat-mang']) {
    testWidgets('Kết quả $loi không tự nhận địa chỉ hợp lệ', (tester) async {
      DiaChiNhan giaTri = const DiaChiNhan();
      await mo(
        tester,
        DiaChiGiao(
          giaTri: giaTri,
          onChanged: (v) => giaTri = v,
          layGps: () async => const DiemGps(10.7, 106.7, 10),
          timDiaChi: (_) async {
            if (loi == 'mat-mang') {
              return KetQuaApi()
                ..thanhCong = false
                ..loi = 'Không kết nối được máy chủ.';
            }
            var kq = timDuoc();
            if (loi == 'rong') kq.duLieu = [];
            if (loi == 'thieu-ma') kq.duLieu.first.remove('maViTriGiao');
            if (loi == 'toa-do-sai') kq.duLieu.first['viDo'] = 999;
            return kq;
          },
        ),
      );
      await tester.tap(find.text('Dùng vị trí hiện tại'));
      await tester.pumpAndSettle();
      expect(giaTri.daChon, isFalse);
      expect(find.text('Dùng vị trí hiện tại'), findsOneWidget);
      expect(find.text('Mở cài đặt quyền vị trí'), findsNothing);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
    });
  }

  testWidgets('Bấm liên tiếp chỉ lấy GPS một lần, giữ chi tiết đang nhập', (
    tester,
  ) async {
    int soLanGps = 0;
    DiaChiNhan giaTri = const DiaChiNhan();
    final gps = Completer<DiemGps>();
    await mo(
      tester,
      DiaChiGiao(
        giaTri: giaTri,
        onChanged: (v) => giaTri = v,
        layGps: () {
          soLanGps++;
          return gps.future;
        },
        timDiaChi: (_) async => timDuoc(),
      ),
    );
    await tester.tap(find.text('Dùng vị trí hiện tại'));
    await tester.tap(find.text('Dùng vị trí hiện tại'));
    await tester.pump();
    expect(soLanGps, 1);
    await tester.ensureVisible(find.byType(TextField).at(1));
    await tester.enterText(find.byType(TextField).at(1), 'Tầng 2, phòng 201');
    gps.complete(const DiemGps(10.7, 106.7, 10));
    await tester.pumpAndSettle();
    expect(giaTri.daChon, isTrue);
    expect(giaTri.chiTiet, 'Tầng 2, phòng 201');
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('Địa chỉ tự điền không tràn ở 320px và chữ lớn gấp đôi', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp(
        builder: (context, child) => MediaQuery(
          data: MediaQuery.of(
            context,
          ).copyWith(textScaler: TextScaler.linear(2)),
          child: child!,
        ),
        home: Scaffold(
          body: SingleChildScrollView(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: DiaChiGiao(
                giaTri: const DiaChiNhan(),
                onChanged: (_) {},
                layGps: () async => const DiemGps(10.7, 106.7, 10),
                timDiaChi: (_) async => timDuoc(),
              ),
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.text('Dùng vị trí hiện tại'));
    await tester.pumpAndSettle();
    expect(
      tester.widget<TextField>(find.byType(TextField).first).controller!.text,
      '140 Lê Trọng Tấn, TP Hồ Chí Minh',
    );
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
  });
}
