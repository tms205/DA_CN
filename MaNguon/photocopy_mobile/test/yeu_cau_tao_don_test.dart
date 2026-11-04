import 'package:flutter_test/flutter_test.dart';
import 'package:photocopy_mobile/yeu_cau_tao_don.dart';

void main() {
  test('Retry giữ UUID; sửa nội dung đổi UUID; không sửa map nguồn', () {
    final request = YeuCauTaoDon();
    final data = <String, dynamic>{
      'maChiNhanh': 1,
      'fileIn': [
        {'id': 3},
      ],
    };
    final first = request.ganMa(data)['maYeuCauTao'];
    expect(
      first,
      matches(
        RegExp(
          r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
        ),
      ),
    );
    expect(request.ganMa(data)['maYeuCauTao'], first);
    expect(data.containsKey('maYeuCauTao'), false);
    expect(
      request.ganMa({...data, 'maChiNhanh': 2})['maYeuCauTao'],
      isNot(first),
    );
    expect(YeuCauTaoDon().ganMa(data)['maYeuCauTao'], isNot(first));
  });
}
