// Fixture cục bộ, không gọi API/database thật. node <script> <wwwroot publish> <playwright> <QA output>
const { chromium } = require(process.argv[3]);
const http = require('node:http'), fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const root = path.resolve(process.argv[2]), out = path.resolve(process.argv[4]);
fs.mkdirSync(out, {recursive:true});
const mime = {'.html':'text/html','.js':'text/javascript','.json':'application/json','.css':'text/css','.wasm':'application/wasm','.svg':'image/svg+xml'};
const server = http.createServer((req,res) => {
  let file = path.resolve(root, '.' + new URL(req.url,'http://fixture').pathname);
  if (file!==root && !file.startsWith(root + path.sep)) { res.writeHead(403).end(); return; }
  if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) file = path.join(root,'index.html');
  res.setHeader('Content-Type',mime[path.extname(file)] || 'application/octet-stream'); fs.createReadStream(file).pipe(res);
});
(async()=>{
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 const browser=await chromium.launch({headless:true,channel:'chrome'});
 try{
  const page=await browser.newPage({viewport:{width:1366,height:950}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
  let orderState='DA_IN',configSaved=false,assigned=false,cancelled=false,reportMaintenance=false,ocrRequests=0;
  let branchStatus='HOAT_DONG'; const branchUpdates=[];
  let state={dangBaoTri:false,loiNhan:'Bảo trì OCR',khoaWeb:true,khoaApp:true,khoaTinhNangWeb:[],khoaTinhNangApp:[]};
  const order=()=>({maDonHang:15,maDonHangHienThi:'DH00015',maChiNhanh:1,tenChiNhanh:'Cơ sở thử',tenKhachHang:'Khách thử',
   sdtNhan:'0901234567',soDienThoai:'0901234567',hinhThucNhan:'GIAO_HANG',diaChiGiao:'123 Lê Lợi',trangThaiDonHang:orderState,
   phuongThucThanhToan:'VIETQR',trangThaiThanhToan:'DA_THANH_TOAN',tongTien:39000,tienHang:20000,phiShip:19000,ngayDat:'2026-11-01T10:00:00',dongIn:[],soFile:1,tongSoTrang:1});
  await page.addInitScript(()=>sessionStorage.setItem('photocopychain.phien',JSON.stringify({accessToken:'fixture-not-real',hetHanLuc:'2099-01-01T00:00:00Z',
   nguoiDung:{maNguoiDung:1,hoTen:'CEO thử',tenVaiTro:'SUPER_ADMIN',maChiNhanh:1,tenChiNhanh:'Cơ sở thử'}})));
  await page.route('http://localhost:5268/**',async route=>{
   const req=route.request(),u=new URL(req.url()),p=u.pathname;
   const headers={'access-control-allow-origin':'*','access-control-allow-headers':'*','access-control-allow-methods':'*'};
   if(req.method()==='OPTIONS')return route.fulfill({status:204,headers});
   if(p.startsWith('/hubs/'))return route.fulfill({status:503,headers,body:''});
   let data=[];
   if(p==='/api/bao-tri'){
    if(req.method()==='PUT'){const body=req.postDataJSON();assert.equal(body.khoaWeb,false);assert.equal(body.khoaApp,false);assert.equal(body.soGio,2.5);assert.deepEqual(body.khoaTinhNangWeb,['OCR']);configSaved=true;state={...body,ketThucLuc:'2026-11-01T06:30:00Z'};}
    data={trangThai:state,gioHeThong:'2026-11-01T04:00:00Z'};
   }else if(p==='/api/bao-tri/trang-thai')data={dangBaoTri:false,loiNhan:reportMaintenance?'Nâng cấp báo cáo. Dự kiến mở lại lúc 18:00.':state.loiNhan,tinhNangKhoa:reportMaintenance?['BAO_CAO']:state.dangBaoTri?state.khoaTinhNangWeb:[]};
   else if(p==='/api/bao-tri/phien')data={hopLe:true};
   else if(p==='/api/ca/cho-tong-ket')return route.fulfill({status:204,headers});
   else if(p==='/api/ca/dang-mo')data={maCa:1};
   else if(p.startsWith('/api/quan-tri/bao-cao/')&&reportMaintenance)return route.fulfill({status:503,headers,json:{dangBaoTri:false,loiNhan:'Nâng cấp báo cáo',message:'Chức năng đang bảo trì.\nNâng cấp báo cáo. Dự kiến mở lại lúc 18:00.'}});
   else if(p.startsWith('/api/ocr/')){ocrRequests++;data=[];}
   else if(p==='/api/quan-tri/bao-cao/doanh-thu'){
    const rows=[{ngay:'2026-11-01',maChiNhanh:1,tenChiNhanh:'Cơ sở thử',trangThai:'HOAN_THANH',soDon:2,doanhThu:100000,giaTriDon:100000,soDonDoanhThu:2},
     {ngay:'2026-11-01',maChiNhanh:1,tenChiNhanh:'Cơ sở thử',trangThai:'DA_HUY',soDon:1,doanhThu:0,giaTriDon:50000,soDonDoanhThu:0}];
    data=rows.filter(r=>!u.searchParams.get('trangThai')||r.trangThai===u.searchParams.get('trangThai'));
   }else if(p==='/api/don-hang/hang-doi')data=[order()];
   else if(p==='/api/don-hang/15')data=order();
   else if(p==='/api/quan-tri/chi-nhanh')data=[{maChiNhanh:1,tenChiNhanh:'Chi nhánh thử trạng thái',diaChi:'123 Đường thử',soDienThoai:'0901234567',gioMoCua:'07:30:00',gioDongCua:'21:30:00',banKinhGiao:10,trangThai:branchStatus}];
   else if(p==='/api/quan-tri/chi-nhanh/1'&&req.method()==='PUT'){
    const body=req.postDataJSON();assert.ok(['DONG_CUA','HOAT_DONG'].includes(body.trangThai));
    assert.equal(body.tenChiNhanh,'Chi nhánh thử trạng thái');branchStatus=body.trangThai;branchUpdates.push(branchStatus);data={message:'Đã cập nhật chi nhánh'};
   }
   else if(p==='/api/giao-hang/shipper')data=[{maShipper:9,maChiNhanh:1,hoTen:'Shipper đúng chi nhánh',soDienThoai:'0901234567'},{maShipper:10,maChiNhanh:2,hoTen:'Shipper khác chi nhánh'}];
   else if(p==='/api/giao-hang/15/phan-cong'){assert.equal(req.postDataJSON().maShipper,9);assigned=true;orderState='DANG_GIAO';data={message:'Đã bàn giao'};}
   else if(p==='/api/don-hang/15/bang-chung-huy'&&req.method()==='POST')data={maBangChung:'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'};
   else if(p==='/api/don-hang/15/huy'){const body=req.postDataJSON();assert.equal(body.maBangChung,'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa');assert.equal(body.lyDo,'Khách yêu cầu hủy bằng tin nhắn.');cancelled=true;orderState='DA_HUY';data={message:'Đã hủy'};}
   await route.fulfill({status:200,headers,json:data});
  });
  const base='http://127.0.0.1:'+server.address().port;
  await page.goto(base+'/bao-tri');
  const appOptions=page.locator('fieldset').filter({has:page.locator('legend').filter({hasText:'App điện thoại'})});
  await appOptions.waitFor();
  assert.equal(await appOptions.getByLabel('Quản lý danh mục / nhân sự',{exact:true}).count(),0);
  assert.equal(await appOptions.getByLabel('Dashboard / báo cáo',{exact:true}).count(),0);
  assert.equal(await appOptions.locator('input[type=checkbox]').count(),7);
  await page.getByLabel('Khóa toàn bộ Web (đăng xuất nhân sự)').uncheck();
  await page.getByLabel('Khóa toàn bộ App (đăng xuất khách/shipper)').uncheck();
  await page.locator('fieldset').nth(0).getByLabel('Nhận dạng chữ OCR').check();
  await page.getByLabel('Thời lượng bảo trì (giờ)').fill('2.5');
  await page.getByLabel('Nội dung thông báo').fill('Bảo trì OCR, các dịch vụ khác hoạt động bình thường.');
  await page.getByRole('button',{name:'Bật bảo trì',exact:true}).click();
  await page.getByRole('button',{name:'Xác nhận bật',exact:true}).click();
  await page.getByText('Đã cập nhật chế độ bảo trì và lời nhắn.').waitFor();assert.ok(configSaved);
  for(const width of [1366,390]){await page.setViewportSize({width,height:950});await page.locator('#bt-status-title').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(out,`bao-tri-${width}.png`),fullPage:true});}
  await page.setViewportSize({width:1366,height:950});await page.goto(base+'/bao-cao');
  await page.locator('tbody tr').filter({hasText:'Đã hủy'}).waitFor();
  await page.getByLabel('Trạng thái đơn').selectOption('DA_HUY');await page.getByRole('button',{name:'Xem báo cáo',exact:true}).click();
  await page.waitForFunction(()=>document.querySelectorAll('tbody tr').length===1);
  assert.ok((await page.locator('tbody').innerText()).includes('Đã hủy'));
  await page.screenshot({path:path.join(out,'bao-cao-da-huy.png'),fullPage:true});
  await page.goto(base+'/');await page.getByText('DH00015',{exact:true}).first().click();
  await page.getByRole('button',{name:'Phân công giao hàng',exact:true}).click();
  let modal=page.locator('dialog[open]');await modal.getByLabel('Shipper nhận tài liệu').selectOption('9');
  assert.equal(await modal.getByLabel('Shipper nhận tài liệu').locator('option').count(),2);
  await page.setViewportSize({width:390,height:950});await modal.evaluate(el=>Promise.all(el.getAnimations({subtree:true}).map(a=>a.finished)));await page.screenshot({path:path.join(out,'phan-cong-giao-390.png')});
  await modal.getByRole('button',{name:'Xác nhận bàn giao',exact:true}).click();
  await page.waitForFunction(()=>document.querySelectorAll('dialog[open]').length===0);assert.ok(assigned);
  orderState='CHO_XAC_NHAN';await page.goto(base+'/');await page.getByText('DH00015',{exact:true}).first().click();
  await page.getByRole('button',{name:'Hủy đơn hàng',exact:true}).click();modal=page.locator('dialog[open]');
  await modal.getByLabel('Lý do hủy').fill('Khách yêu cầu hủy bằng tin nhắn.');
  await modal.locator('input[type=file]').setInputFiles({name:'proof.png',mimeType:'image/png',buffer:Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=','base64')});
  await page.screenshot({path:path.join(out,'huy-co-bang-chung-390.png')});
  await modal.getByRole('button',{name:'Xác nhận hủy đơn',exact:true}).click();
  await page.waitForFunction(()=>document.querySelectorAll('dialog[open]').length===0);assert.ok(cancelled);
  await page.goto(base+'/nhan-dang-chu');
  await page.getByText('Tạm dừng để bảo trì',{exact:true}).waitFor();assert.equal(ocrRequests,0);
  assert.equal(await page.locator('input[type=file]').count(),0);
  reportMaintenance=true;
  await page.goto(base+'/bao-cao');
  await page.getByText('Tạm dừng để bảo trì',{exact:true}).waitFor();
  assert.equal(await page.getByRole('button',{name:'Xem báo cáo',exact:true}).count(),0);
  await page.goto(base+'/tong-quan');
  await page.getByText('Tạm dừng để bảo trì',{exact:true}).waitFor();
  assert.ok(!(await page.locator('main').innerText()).includes('Không tải được tổng quan'));
  await page.getByRole('tab',{name:'Vận hành',exact:true}).click();
  await page.getByText('Chỉ số ca / phê duyệt tạm dừng',{exact:true}).waitFor();
  assert.ok(!(await page.locator('main').innerText()).includes('Không tải được chỉ số'));
  await page.getByText('Không có vật tư dưới mức tối thiểu.',{exact:true}).waitFor();
  for(const width of [1366,390]){await page.setViewportSize({width,height:950});await page.getByText('Chỉ số ca / phê duyệt tạm dừng',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:path.join(out,`van-hanh-bao-tri-${width}.png`),fullPage:true});}
  await page.goto(base+'/bao-cao');await page.getByText('Tạm dừng để bảo trì',{exact:true}).waitFor();
  reportMaintenance=false;
  await page.getByRole('button',{name:'Xem báo cáo',exact:true}).waitFor({timeout:15000});
  await page.goto(base+'/quan-tri');await page.getByRole('button',{name:'Sửa chi nhánh',exact:true}).click();
  modal=page.locator('dialog[open]');await modal.getByLabel('Trạng thái vận hành').selectOption('DONG_CUA');
  await modal.getByRole('button',{name:'Lưu chi nhánh',exact:true}).click();
  await page.waitForFunction(()=>document.querySelectorAll('dialog[open]').length===0);
  await page.locator('tbody tr').filter({hasText:'Chi nhánh thử trạng thái'}).getByText('Đóng cửa',{exact:true}).waitFor();
  await page.getByRole('button',{name:'Sửa chi nhánh',exact:true}).click();modal=page.locator('dialog[open]');
  assert.equal(await modal.getByLabel('Trạng thái vận hành').inputValue(),'DONG_CUA');
  await modal.evaluate(el=>Promise.all(el.getAnimations({subtree:true}).map(a=>a.finished)));
  await page.screenshot({path:path.join(out,'chi-nhanh-dong-cua.png')});
  await modal.getByLabel('Trạng thái vận hành').selectOption('HOAT_DONG');
  await modal.getByRole('button',{name:'Lưu chi nhánh',exact:true}).click();
  await page.waitForFunction(()=>document.querySelectorAll('dialog[open]').length===0);
  await page.locator('tbody tr').filter({hasText:'Chi nhánh thử trạng thái'}).getByText('Đang hoạt động',{exact:true}).waitFor();
  assert.deepEqual(branchUpdates,['DONG_CUA','HOAT_DONG']);
  assert.deepEqual(errors,[]);console.log('PASS: platform maintenance, dashboard sources/reopening, report filter, delivery, cancellation evidence, branch closing/reopening; fixture only.');
 }finally{await browser.close();server.close();}
})().catch(error=>{console.error(error);server.close();process.exitCode=1;});
