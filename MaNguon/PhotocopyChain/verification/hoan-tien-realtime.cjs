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
 try {
  const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[],sockets=new Set();let pending=false,reads=0,approvalReads=0;
  page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error')console.error('UI',m.text());});
  await page.addInitScript(()=>sessionStorage.setItem('photocopychain.phien',JSON.stringify({accessToken:'fixture-only',hetHanLuc:'2099-01-01T00:00:00Z',nguoiDung:{maNguoiDung:1,tenVaiTro:'SUPER_ADMIN',hoTen:'CEO thử'}})));
  await page.routeWebSocket('ws://localhost:5268/hubs/cap-nhat**',ws=>{
   sockets.add(ws);ws.onClose(()=>sockets.delete(ws));
   ws.onMessage(message=>{for(const part of String(message).split(String.fromCharCode(30))){if(!part)continue;try{const x=JSON.parse(part);if(x.protocol)ws.send('{}'+String.fromCharCode(30));}catch{}}});
  });
  const signal=()=>{for(const ws of sockets)ws.send(JSON.stringify({type:1,target:'RefundChanged',arguments:[{maDonHang:9002,maChiNhanh:1}]})+String.fromCharCode(30));};
  const headers={'access-control-allow-origin':'*','access-control-allow-headers':'*','access-control-allow-methods':'*'};
  await page.route('http://localhost:5268/**',async route=>{
   const req=route.request(),p=new URL(req.url()).pathname;
   if(req.method()==='OPTIONS')return route.fulfill({status:204,headers});
   if(p==='/hubs/cap-nhat/negotiate')return route.fulfill({status:200,headers,json:{negotiateVersion:1,connectionId:'fixture',connectionToken:'fixture',availableTransports:[{transport:'WebSockets',transferFormats:['Text','Binary']}]}});
   if(p.startsWith('/hubs/'))return route.fulfill({status:503,headers});
   assert.equal(req.method(),'GET','No business mutation is allowed in realtime UI test');
   let data=[];
   if(p==='/api/bao-tri/trang-thai')data={dangBaoTri:false,tinhNangKhoa:[]};
   else if(p==='/api/bao-tri/phien')data={hopLe:true};
   else if(p==='/api/ca/cho-tong-ket')return route.fulfill({status:204,headers});
   else if(p==='/api/ca/dang-mo')data={maCa:1,trangThai:'DANG_MO'};
   else if(p==='/api/danh-muc/chi-nhanh')data=[{maChiNhanh:1,tenChiNhanh:'Cơ sở thử',trangThai:'HOAT_DONG'}];
   else if(p==='/api/quan-tri/bao-cao/yeu-cau-hoan-tien'){reads++;data=pending?[{maDonHang:9002,tenKhachHang:'Khách hủy trong 2 phút',tenChiNhanh:'Cơ sở thử',soTien:18000,loai:'HUY_DON',trangThai:'CHAP_THUAN',guiLuc:'2026-11-04T02:00:00Z'}]:[];}
   else if(p==='/api/quan-tri/bao-cao/hien-tai')data={soHoanTienChoDuyet:pending?1:0,soPhieuChoDuyet:0};
   else if(p==='/api/quan-tri/bao-cao/phan-tich')data={dichVu:[],thanhToan:[],gioCaoDiem:[],hieuQua:{},danhGia:{}};
   else if(p==='/api/quan-tri/hoan-tien'){approvalReads++;data=pending?[{maDonHang:9002,maDonHangHienThi:'DH09002',tenKhachHang:'Khách hủy trong 2 phút',tenChiNhanh:'Cơ sở thử',soTien:18000,kenhDatDon:'APP',guiLuc:'2026-11-04T02:00:00Z',ghiChu:'Lý do hoàn tiền: Không cần in nữa\nNgân hàng: Ngân hàng thử\nSố tài khoản: 123456789'}]:[];}
   await route.fulfill({status:200,headers,json:data});
  });
  const base=process.argv[5]||('http://127.0.0.1:'+server.address().port);
  await page.goto(base+'/tong-quan');await page.getByRole('heading',{name:'Tổng quan',exact:true}).waitFor();
  const alert=page.getByRole('region',{name:'Yêu cầu hoàn tiền cần xử lý'});
  await page.waitForFunction(()=>document.body.innerText.includes('Xu hướng doanh thu'));
  const deadline=Date.now()+5000;while(!sockets.size&&Date.now()<deadline)await page.waitForTimeout(100);
  assert.ok(sockets.size>0,'Realtime connection must be established');
  await page.waitForTimeout(350);assert.equal(await alert.count(),0);
  pending=true;const before=reads;signal();signal();signal();
  try{await alert.getByText('DH09002 · Khách hủy trong 2 phút').waitFor({timeout:4000});}catch(e){console.error('reads',reads,'before',before,'sockets',sockets.size,'errors',errors,'body',(await page.locator('body').innerText()).slice(0,2200));throw e;}
  assert.ok(reads-before<=2,'Burst notifications must coalesce');
  await alert.getByText('Chờ chuyển hoàn',{exact:true}).waitFor();
  await page.screenshot({path:path.join(out,'refund-live-dashboard.png'),fullPage:true});
  pending=false;for(const socket of Array.from(sockets))socket.close({code:1001,reason:'fixture reconnect'});
  await alert.waitFor({state:'hidden',timeout:7000});
  pending=true;signal();await alert.waitFor({timeout:4000});
  await alert.getByRole('link',{name:'Xử lý hoàn tiền →'}).click();
  const popup=page.getByRole('dialog').filter({hasText:'Hoàn tiền đơn đã hủy'});
  await popup.getByText('Ngân hàng thử',{exact:true}).waitFor();
  assert.ok(await popup.getByText('123456789',{exact:true}).isVisible());
  await page.waitForTimeout(400);
  await page.screenshot({path:path.join(out,'refund-deep-link.png'),fullPage:true});
  await page.setViewportSize({width:390,height:844});await page.waitForTimeout(400);
  assert.ok(await popup.evaluate(x=>x.scrollWidth<=x.clientWidth+1),'Refund form must not scroll horizontally');
  await page.screenshot({path:path.join(out,'refund-deep-link-390.png'),fullPage:true});
  await page.setViewportSize({width:1440,height:1000});
  await popup.locator('#phe-ma-chuyen').fill('DRAFT-KEEP');
  await popup.locator('#phe-ghi-chu-hoan').fill('Nội dung đang đối soát chưa gửi');
  const readBefore=approvalReads;signal();
  const until=Date.now()+4000;while(approvalReads===readBefore&&Date.now()<until)await page.waitForTimeout(100);
  assert.ok(approvalReads>readBefore);assert.equal(await popup.locator('#phe-ma-chuyen').inputValue(),'DRAFT-KEEP');
  pending=false;signal();
  await popup.waitFor({state:'hidden',timeout:4000});
  await page.getByText('Không có đơn hàng nào chờ hoàn tiền.').waitFor({timeout:4000});
  assert.deepEqual(errors,[]);
  console.log('PASS websocket refund arrival -> prominent dashboard -> exact refund popup; remote completion removes row; no real writes');
 }finally{await browser.close();server.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
