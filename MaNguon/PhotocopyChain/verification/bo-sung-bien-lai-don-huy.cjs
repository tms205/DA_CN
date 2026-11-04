// Toàn bộ API được chặn bằng dữ liệu giả; không gửi bill hoặc sửa đơn thật.
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
 try {
  const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[],writes=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.addInitScript(()=>{sessionStorage.setItem('photocopychain.phien',JSON.stringify({accessToken:'fixture-only',hetHanLuc:'2099-01-01T00:00:00Z',nguoiDung:{maNguoiDung:1,tenVaiTro:'SUPER_ADMIN',hoTen:'CEO thử'}}));localStorage.setItem('photocopychain.chi-nhanh','1');});
  const png=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4////fwAJ+wP9KobjigAAAABJRU5ErkJggg==','base64');
  const don={maDonHang:9091,maDonHangHienThi:'DH09091',maChiNhanh:1,tenChiNhanh:'Cơ sở thử',maKhachHang:10,tenKhachHang:'Khách kiểm thử',ngayDat:'2026-11-04T09:00:00',hinhThucNhan:'TAI_QUAY',kenhDatDon:'APP',phuongThucThanhToan:'VIETQR',trangThaiThanhToan:'DA_HOAN_TIEN',trangThaiDonHang:'DA_HUY',tongTien:34900,dongIn:[],tienDo:[]};
  const journal={loai:'HUY_TRUOC_TIEP_NHAN',guiLuc:'2026-11-04T09:01:00+07:00',maNguoiGui:10,lyDo:'Khách yêu cầu hủy trước tiếp nhận',anh:[]};
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
   else if(p==='/api/don-hang/hang-doi')data=[don];
   else if(p==='/api/don-hang/9091')data=don;
   else if(p==='/api/don-hang/9091/bang-chung-huy')data=journal;
   else if(p==='/api/don-hang/9091/bien-lai-hoan-tien'){
    assert.equal(req.method(),'POST');writes.push('upload');data={maBangChung:'a'.repeat(32)};
   }
   else if(p==='/api/don-hang/9091/bien-lai-hoan-tien/bo-sung'){
    const body=req.postDataJSON();assert.equal(body.maBienLai,'a'.repeat(32));assert.equal(body.maChuyenKhoan,'REF-TEST');assert.ok(body.lyDo.trim().length>10);
    writes.push('supplement');journal.bienLaiHoanTien={anh:{maAnh:'a'.repeat(32)},lyDo:body.lyDo,maChuyenKhoan:body.maChuyenKhoan,luc:'2026-11-04T09:10:00+07:00',maCeo:1};data={message:'Đã gửi biên lai và lời nhắn cho khách.'};
   }
   else if(p.includes('/bien-lai-hoan-tien/'))return route.fulfill({status:200,headers:{...headers,'content-type':'image/png'},body:png});
   else if(req.method()!=='GET')throw new Error('Unexpected mutation: '+p);
   await route.fulfill({status:200,headers,json:data});
  });
  const base=process.argv[5]||('http://127.0.0.1:'+server.address().port);
  await page.goto(base+'/');await page.getByText('DH09091',{exact:true}).click();
  await page.getByRole('button',{name:'Hồ sơ hủy & hoàn tiền',exact:true}).click();
  const popup=page.locator('dialog[open]');
  await popup.getByRole('button',{name:'Gửi biên lai và lời nhắn'}).click();assert.deepEqual(writes,[]);
  await popup.locator('#bill-cu-code').fill('REF-TEST');await popup.locator('#bill-cu-note').fill('CEO đã chuyển hoàn, bạn kiểm tra tài khoản giúp cửa hàng nhé.');
  await popup.getByRole('button',{name:'Gửi biên lai và lời nhắn'}).click();assert.deepEqual(writes,[],'Bill required even for historical record');
  await popup.locator('input[type=file]').setInputFiles({name:'bill.png',mimeType:'image/png',buffer:png});
  await popup.getByAltText('Biên lai chuyển khoản đang chọn').waitFor();
  await page.screenshot({path:path.join(out,'bo-sung-bien-lai-desktop.png'),fullPage:true});
  await page.setViewportSize({width:390,height:844});await page.waitForTimeout(250);
  assert.equal(await popup.evaluate(el=>el.scrollWidth>el.clientWidth+1),false,'Popup must fit mobile');
  await page.screenshot({path:path.join(out,'bo-sung-bien-lai-390.png'),fullPage:true});
  await popup.getByRole('button',{name:'Gửi biên lai và lời nhắn'}).click();
  await popup.getByRole('button',{name:'Xem biên lai chuyển khoản'}).waitFor();
  await popup.getByText('CEO đã chuyển hoàn, bạn kiểm tra tài khoản giúp cửa hàng nhé.',{exact:false}).waitFor();
  assert.deepEqual(writes,['upload','supplement'],'Must never call refund endpoint a second time');
  assert.equal(await popup.getByRole('button',{name:'Gửi biên lai và lời nhắn'}).count(),0);
  await popup.getByRole('button',{name:'Xem biên lai chuyển khoản'}).click();await popup.getByAltText('Biên lai chuyển khoản hoàn tiền của CEO').waitFor();
  assert.deepEqual(errors,[]);console.log('PASS historical canceled refund: required message/reference/photo; supplement only, no repeated refund; image view; desktop and 390px');
 }finally{await browser.close();server.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
