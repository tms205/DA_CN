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
      await tester.pumpWidget(const SizedBox());
    },
  );

  testWidgets('Từ chối GPS vẫn tìm địa chỉ và nhập chi tiết được', (
    tester,
  ) async {
    DiaChiNhan giaTri = const DiaChiNhan();
    await mo(
      tester,
      DiaChiGiao(
        giaTri: giaTri,
        onChanged: (v) => giaTri = v,
        layGps: () async => throw StateError('Chưa được cấp quyền vị trí.'),
        timDiaChi: (_) async => timDuoc(),
      ),
    );
    await tester.tap(find.text('Dùng vị trí hiện tại'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Chưa được cấp quyền vị trí'), findsOneWidget);
    await tester.enterText(
      find.byType(TextField).at(0),
      '140 Lê Trọng Tấn, TP Hồ Chí Minh',
    );
    await tester.ensureVisible(find.text('Tìm địa chỉ'));
    await tester.tap(find.text('Tìm địa chỉ'));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Chạm để xác nhận điểm nhận này'));
    await tester.tap(find.text('Chạm để xác nhận điểm nhận này'));
    await tester.pumpAndSettle();
    expect(giaTri.daChon, isTrue);
    await tester.ensureVisible(find.byType(TextField).at(1));
    await tester.enterText(find.byType(TextField).at(1), 'Tầng 2, phòng 201');
    expect(giaTri.chiTiet, 'Tầng 2, phòng 201');
    expect(giaTri.viDo, 10.7);
    await tester.enterText(find.byType(TextField).first, 'Địa chỉ mới');
    expect(giaTri.daChon, isFalse);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('Kết quả địa chỉ cũ về muộn không ghi đè chữ khách vừa sửa', (
    tester,
  ) async {
    final doi = Completer<KetQuaApi>();
    await mo(
      tester,
      DiaChiGiao(
        giaTri: const DiaChiNhan(diaChi: 'Địa chỉ đủ để tìm'),
        onChanged: (_) {},
        timDiaChi: (_) => doi.future,
      ),
    );
    await tester.tap(find.text('Tìm địa chỉ'));
    await tester.pump();
    await tester.enterText(
      find.byType(TextField).first,
      'Khách vừa đổi sang địa chỉ khác',
    );
    doi.complete(timDuoc());
    await tester.pumpAndSettle();
    expect(find.text('Chạm để xác nhận điểm nhận này'), findsNothing);
    expect(find.text('Khách vừa đổi sang địa chỉ khác'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('GPS sai lệch lớn không tự chấp nhận, có hướng nhập tay', (
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
    expect(soLanTim, 0);
    await tester.pumpWidget(const SizedBox());
  });
}
