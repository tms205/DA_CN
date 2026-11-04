// Dữ liệu giả, không kết nối CSDL hoặc thay đổi đơn thật.
const {chromium}=require(process.argv[3]);
const http=require('node:http'),fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const root=path.resolve(process.argv[2]),out=path.resolve(process.argv[4]);fs.mkdirSync(out,{recursive:true});
const mime={'.html':'text/html','.js':'text/javascript','.json':'application/json','.css':'text/css','.wasm':'application/wasm'};
const server=http.createServer((req,res)=>{
 let file=path.resolve(root,'.'+new URL(req.url,'http://fixture').pathname);
 if(file!==root&&!file.startsWith(root+path.sep))return res.writeHead(403).end();
 if(!fs.existsSync(file)||fs.statSync(file).isDirectory())file=path.join(root,'index.html');
 res.setHeader('Content-Type',mime[path.extname(file)]||'application/octet-stream');fs.createReadStream(file).pipe(res);
});
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));const browser=await chromium.launch({headless:true,channel:'chrome'});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[],writes=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.addInitScript(()=>{sessionStorage.setItem('photocopychain.phien',JSON.stringify({accessToken:'fixture-only',hetHanLuc:'2099-01-01T00:00:00Z',nguoiDung:{maNguoiDung:1,tenVaiTro:'SUPER_ADMIN',hoTen:'CEO thử'}}));localStorage.setItem('photocopychain.chi-nhanh','1');});
  const hoSo={maDonHang:9001,tenKhachHang:'Khách kiểm thử',tenChiNhanh:'Cơ sở thử',soTien:25000,guiLuc:'2026-11-03T08:00:00Z',trangThai:'CHO_DUYET',moTa:'Sản phẩm thiếu trang và chữ bị nhòe cần đối chiếu',nganHang:'Ngân hàng thử',soTaiKhoan:'123456789',chuTaiKhoan:'Khách kiểm thử',anh:[],lichSu:[]};
  const billPng=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4////fwAJ+wP9KobjigAAAABJRU5ErkJggg==','base64'),uploads=[];let canceledRefunded=false;
  let remaining=2,orderState='CHO_XAC_NHAN',detailReads=0;
  const don=()=>({maDonHang:9001,maDonHangHienThi:'DH09001',maChiNhanh:1,tenChiNhanh:'Cơ sở thử',maKhachHang:10,tenKhachHang:'Khách kiểm thử',ngayDat:'2026-11-03T08:00:00',hinhThucNhan:'TAI_QUAY',kenhDatDon:'APP',giayChoTiepNhan:remaining,choHuyKhach:remaining>0,phuongThucThanhToan:'VIETQR',trangThaiThanhToan:'DA_THANH_TOAN',trangThaiDonHang:orderState,tongTien:25000,dongIn:[],tienDo:[]});
  await page.route('http://localhost:5268/**',async route=>{
   const req=route.request(),p=new URL(req.url()).pathname,headers={'access-control-allow-origin':'*','access-control-allow-headers':'*','access-control-allow-methods':'*'};
   if(req.method()==='OPTIONS')return route.fulfill({status:204,headers});
   if(p.startsWith('/hubs/'))return route.fulfill({status:503,headers,body:''});
   let data=[];
   if(p==='/api/bao-tri/trang-thai')data={dangBaoTri:false,tinhNangKhoa:[]};
   else if(p==='/api/bao-tri/phien')data={hopLe:true};
   else if(p==='/api/ca/cho-tong-ket')return route.fulfill({status:204,headers});
   else if(p==='/api/ca/dang-mo')data={maCa:1,trangThai:'DANG_MO'};
   else if(p==='/api/danh-muc/chi-nhanh')data=[{maChiNhanh:1,tenChiNhanh:'Cơ sở thử',trangThai:'HOAT_DONG'}];
   else if(p==='/api/quan-tri/hoan-tien')data=canceledRefunded?[]:[{maDonHang:9002,maDonHangHienThi:'DH09002',tenKhachHang:'Khách hủy thử',tenChiNhanh:'Cơ sở thử',soTien:18000,ghiChu:'Lý do hoàn tiền: Không cần in nữa\nNgân hàng: Ngân hàng thử\nSố tài khoản: 123456789'}];
   else if(/^\/api\/don-hang\/900[12]\/bien-lai-hoan-tien$/.test(p)) {
    assert.ok(req.postDataBuffer().includes(Buffer.from('bill.png')));uploads.push(p);data={maBangChung:'a'.repeat(32)};
   }
   else if(p.includes('/bien-lai-hoan-tien/'))return route.fulfill({status:200,headers:{...headers,'content-type':'image/png'},body:billPng});
   else if(p==='/api/don-hang/9002/hoan-tien') {
    const body=req.postDataJSON();assert.equal(body.maBienLai,'a'.repeat(32));assert.ok(body.lyDo.trim().length>10);writes.push(body);canceledRefunded=true;data={message:'Đã lưu'};
   }
   else if(p==='/api/don-hang/khieu-nai-hoan-tien')data=[hoSo];
   else if(p==='/api/don-hang/9001/khieu-nai-hoan-tien/quyet-dinh'){
    const body=req.postDataJSON();writes.push(body);hoSo.trangThai=body.trangThai;hoSo.lichSu.push({...body,luc:'2026-11-03T08:30:00Z',bienLai:body.trangThai==='DA_HOAN_TIEN'?{anh:{maAnh:'a'.repeat(32)},maChuyenKhoan:body.maChuyenKhoan,luc:'2026-11-03T08:30:00Z'}:null});data={message:'Đã lưu'};
   }
   else if(p==='/api/don-hang/hang-doi')data=[don()];
   else if(p==='/api/don-hang/9001') { data=don();detailReads++;if(detailReads===1)setTimeout(()=>remaining=0,500); }
   else if(p==='/api/don-hang/9001/tiep-nhan'){assert.equal(remaining,0);writes.push({accept:true});orderState='DA_XAC_NHAN';data={message:'Đã tiếp nhận'};}
   await route.fulfill({status:200,headers,json:data});
  });
  const base=process.argv[5] || ('http://127.0.0.1:'+server.address().port);
  await page.goto(base+'/phe-duyet');
  await page.getByRole('heading',{name:'Hoàn tiền',exact:true}).waitFor();
  assert.equal(await page.getByRole('heading',{name:'Hoàn tiền',exact:true}).count(),1);
  assert.equal(await page.getByRole('heading',{name:'Đơn hàng chờ xác nhận hoàn tiền'}).count(),0);
  await page.getByText('DH09002',{exact:true}).waitFor();
  assert.equal(await page.getByRole('button',{name:'Xem hồ sơ'}).count(),0);
  await page.getByRole('button',{name:'Sản phẩm',exact:false}).click();
  assert.equal(await page.getByText('DH09002',{exact:true}).isVisible(),false);
  await page.getByRole('button',{name:'Đơn đã hủy',exact:false}).click();
  assert.equal(await page.getByRole('button',{name:'Xem hồ sơ'}).count(),0);
  await page.getByRole('button',{name:'Sản phẩm',exact:false}).click();
  await page.setViewportSize({width:390,height:844});
  await page.waitForTimeout(400);
  await page.getByRole('button',{name:'Đơn đã hủy',exact:false}).click();
  await page.getByRole('button',{name:'Sản phẩm',exact:false}).click();
  await page.locator('.phe-hoan-tien').scrollIntoViewIfNeeded();
  await page.waitForTimeout(200);
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  await page.screenshot({path:path.join(out,'hoan-tien-hai-tab-390.png'),fullPage:true});
  await page.setViewportSize({width:1440,height:1000});
  await page.getByRole('button',{name:'Xem hồ sơ'}).click();
  const popup=page.getByRole('dialog').filter({hasText:'Xem xét yêu cầu hoàn tiền'});
  await popup.locator('.ht-ngan-hang').getByText('Khách kiểm thử',{exact:true}).waitFor();
  await page.waitForTimeout(350);
  await page.screenshot({path:path.join(out,'hoan-tien-popup.png'),fullPage:true});
  await popup.locator('textarea').fill('Đã đối chiếu bằng chứng và chấp thuận hoàn toàn bộ');
  await popup.getByRole('button',{name:'Chấp thuận hoàn toàn bộ'}).click();
  await page.getByRole('dialog').getByRole('button',{name:'Xác nhận',exact:true}).click();
  await page.getByText('Chấp thuận · Chờ chuyển tiền',{exact:false}).first().waitFor();
  await page.getByRole('button',{name:'Xem hồ sơ'}).click();
  await popup.locator('textarea').fill('Đã chuyển hoàn và có mã đối soát');
  await popup.getByRole('button',{name:'Xác nhận đã chuyển hoàn'}).click();
  assert.equal(writes.length,1,'Missing reference must not submit');
  await popup.locator('#ht-ma-chuyen').fill('TEST-BANK-ONLY');
  await popup.getByRole('button',{name:'Xác nhận đã chuyển hoàn'}).click();
  assert.equal(writes.length,1,'Missing receipt must not submit');
  await popup.locator('input[type=file]').setInputFiles({name:'bill.png',mimeType:'image/png',buffer:billPng});
  await popup.getByAltText('Biên lai chuyển khoản đang chọn').waitFor();
  await popup.getByRole('button',{name:'Xác nhận đã chuyển hoàn'}).click();
  await page.getByRole('dialog').getByRole('button',{name:'Xác nhận',exact:true}).click();
  await popup.waitFor({state:'hidden'});
  await page.locator('#phe-san-pham').getByText('Đã chuyển hoàn',{exact:false}).first().waitFor();
  assert.equal(writes.length,2);assert.equal(writes[1].maChuyenKhoan,'TEST-BANK-ONLY');
  await page.screenshot({path:path.join(out,'hoan-tien-ceo.png'),fullPage:true});
  await page.getByRole('button',{name:'Xem hồ sơ'}).click();
  await popup.getByRole('button',{name:'Xem biên lai chuyển khoản'}).click();
  await popup.getByAltText('Biên lai chuyển khoản hoàn tiền của CEO').waitFor();
  await page.screenshot({path:path.join(out,'bien-lai-san-pham.png'),fullPage:true});
  await popup.getByRole('button',{name:'Đóng',exact:true}).click();
  await page.getByRole('button',{name:'Đơn đã hủy',exact:false}).click();
  await page.locator('#phe-don-huy').getByRole('button',{name:'Xử lý hoàn tiền',exact:true}).click();
  const cancelPopup=page.getByRole('dialog').filter({hasText:'Hoàn tiền đơn đã hủy'});
  await cancelPopup.locator('textarea').fill('1234567890');
  await cancelPopup.locator('#phe-ma-chuyen').fill('TEST-CANCEL-ONLY');
  await cancelPopup.getByRole('button',{name:'Xác nhận đã chuyển hoàn'}).click();
  assert.equal(writes.length,2,'Ten-character reason must not submit');
  await cancelPopup.locator('textarea').fill('12345678901');
  await cancelPopup.getByRole('button',{name:'Xác nhận đã chuyển hoàn'}).click();
  assert.equal(writes.length,2,'Canceled order also requires receipt');
  await cancelPopup.locator('input[type=file]').setInputFiles({name:'bill.png',mimeType:'image/png',buffer:billPng});
  await cancelPopup.getByAltText('Biên lai chuyển khoản đang chọn').waitFor();
  await page.screenshot({path:path.join(out,'bien-lai-don-huy.png'),fullPage:true});
  await cancelPopup.getByRole('button',{name:'Xác nhận đã chuyển hoàn'}).click();
  await page.getByRole('dialog').filter({hasText:'Xác nhận hoàn tiền'}).getByRole('button',{name:'Đã chuyển hoàn',exact:true}).click();
  await page.getByText('Đã xác nhận hoàn tiền DH09002.').waitFor();
  assert.equal(writes.length,3);assert.equal(uploads.length,2);

  await page.goto(base+'/');
  await page.getByText('DH09001',{exact:true}).click();
  await page.getByText('Đơn app đang chờ xác nhận',{exact:false}).waitFor();
  assert.equal(await page.getByRole('button',{name:'Tiếp nhận đơn',exact:true}).count(),0);
  await page.getByRole('button',{name:'Tiếp nhận đơn',exact:true}).waitFor({timeout:8000});
  assert.ok(detailReads<=3,'Countdown must not request detail every second');
  await page.getByRole('button',{name:'Tiếp nhận đơn',exact:true}).click();
  await page.getByRole('dialog').getByRole('button',{name:'Tiếp nhận',exact:true}).click();
  await page.getByRole('button',{name:'Chọn máy & bắt đầu in'}).waitFor();
  assert.equal(await page.getByText('Điều kiện bắt đầu in tự động').count(),0);
  await page.screenshot({path:path.join(out,'don-app-tiep-nhan-thu-cong.png'),fullPage:true});
  assert.deepEqual(errors,[]);console.log('PASS CEO approval -> bank-reference validation -> transferred; APP countdown -> manual receive -> manual printer; no automatic retry requests');
 }finally{await browser.close();server.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
