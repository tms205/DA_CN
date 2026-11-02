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
  const page=await browser.newPage({viewport:{width:1366,height:950},timezoneId:'Asia/Bangkok'}),errors=[];page.on('pageerror',e=>errors.push(e.message));
  let reports=0;const requests=[];
  const order=(id,status,home=false)=>({maDonHang:id,maDonHangHienThi:'DH'+String(id).padStart(5,'0'),maChiNhanh:1,tenChiNhanh:'Cơ sở thử',tenKhachHang:'Khách thử '+id,
    soDienThoaiKhach:'0901234567',sdtNhan:'0901234567',hinhThucNhan:home?'GIAO_HANG':'TAI_QUAY',trangThaiDonHang:status,
    trangThaiThanhToan:'DA_THANH_TOAN',phuongThucThanhToan:'VIETQR',tongTien:39000,phiShip:19000,ngayDat:'2026-11-01T09:00:00',soFile:1,tongSoTrang:1});
  const delivery=(id,state,shipping)=>({...order(id,state,true),diaChiGiao:'123 Đường thử',maGiaoHang:id,soLanGiao:shipping==='THAT_BAI'?3:1,lanGiao:shipping==='THAT_BAI'?3:1,
    tenShipper:'Shipper thử',trangThaiGiao:shipping,trangThaiDoiChieu:shipping==='THAT_BAI'?'DA_XU_LY':null,soPhanHoiChoDoiChieu:0});
  const groups={CHO_GIAO:[{...delivery(91,'DA_IN',null),soLanGiao:0}],DANG_GIAO:[delivery(92,'DANG_GIAO','DANG_GIAO')],THAT_BAI:[delivery(93,'GIAO_THAT_BAI','THAT_BAI')],THANH_CONG:[delivery(94,'HOAN_THANH','THANH_CONG')]};
  await page.addInitScript(()=>sessionStorage.setItem('photocopychain.phien',JSON.stringify({accessToken:'fixture-only',hetHanLuc:'2099-01-01T00:00:00Z',nguoiDung:{maNguoiDung:1,tenVaiTro:'SUPER_ADMIN',hoTen:'CEO thử',maChiNhanh:1,tenChiNhanh:'Cơ sở thử'}})));
  await page.route('http://localhost:5268/**',async route=>{
   const req=route.request(),u=new URL(req.url()),p=u.pathname,headers={'access-control-allow-origin':'*','access-control-allow-headers':'*','access-control-allow-methods':'*'};
   if(req.method()==='OPTIONS')return route.fulfill({status:204,headers});
   if(p.startsWith('/hubs/'))return route.fulfill({status:503,headers,body:''});
   assert.equal(req.method(),'GET','Bài kiểm thử điều hướng chỉ đọc dữ liệu: '+p);
   let data=[];
   if(p==='/api/bao-tri/trang-thai')data={dangBaoTri:false,tinhNangKhoa:[]};
   else if(p==='/api/bao-tri/phien')data={hopLe:true};
   else if(p==='/api/ca/cho-tong-ket')return route.fulfill({status:204,headers});
   else if(p==='/api/quan-tri/bao-cao/doanh-thu'){reports++;data=[];}
   else if(p==='/api/quan-tri/bao-cao/hien-tai')data={soCaDangMo:1,tuNgayKiemDem:'2026-11-01'};
   else if(p==='/api/giao-hang/phan-bo')data=Object.keys(groups).map(nhom=>({nhom,soDon:1}));
   else if(p==='/api/giao-hang'){requests.push(u.searchParams.get('nhom'));data=groups[u.searchParams.get('nhom')]||[];}
   else if(p==='/api/don-hang/hang-doi')data=[order(1,'CHO_XAC_NHAN'),order(2,'DA_XAC_NHAN'),order(3,'DANG_IN'),order(4,'DA_IN'),...Object.values(groups).flat()];
   await route.fulfill({status:200,headers,json:data});
  });
  const base='http://127.0.0.1:'+server.address().port;
  await page.goto(base+'/tong-quan');await page.getByLabel('Khoảng thời gian').waitFor();
  await page.getByRole('heading',{name:'Doanh thu kỳ đã chọn',exact:true}).waitFor();
  assert.equal(await page.getByLabel('Khoảng thời gian').locator('option:not([disabled])').count(),5);
  assert.ok(!(await page.locator('main').innerText()).includes('Tùy chọn ngày'));
  const n=reports;await page.getByLabel('Từ ngày',{exact:true}).fill('31/02/2026');await page.getByLabel('Đến ngày',{exact:true}).fill('01/03/2026');
  await page.getByRole('button',{name:'Xem tổng quan',exact:true}).click();await page.getByText('Từ ngày phải là ngày có thật, đúng định dạng dd/MM/yyyy.',{exact:true}).waitFor();assert.equal(reports,n);
  await page.getByLabel('Từ ngày',{exact:true}).fill('02/11/2026');await page.getByLabel('Đến ngày',{exact:true}).fill('01/11/2026');
  await page.getByRole('button',{name:'Xem tổng quan',exact:true}).click();await page.getByText('Từ ngày phải nhỏ hơn hoặc bằng Đến ngày.',{exact:true}).waitFor();assert.equal(reports,n);
  await page.getByLabel('Khoảng thời gian').selectOption('thang-truoc');
  await page.getByRole('heading',{name:'Giá trị trung bình/đơn',exact:true}).waitFor();
  const expected=await page.evaluate(()=>{const d=new Date(),f=x=>String(x.getDate()).padStart(2,'0')+'/'+String(x.getMonth()+1).padStart(2,'0')+'/'+x.getFullYear();return [f(new Date(d.getFullYear(),d.getMonth()-1,1)),f(new Date(d.getFullYear(),d.getMonth(),0))];});
  assert.equal(await page.getByLabel('Từ ngày',{exact:true}).inputValue(),expected[0]);assert.equal(await page.getByLabel('Đến ngày',{exact:true}).inputValue(),expected[1]);
  await page.screenshot({path:path.join(out,'tong-quan-thang-truoc.png')});
  for(const [name,group,id] of [['Giao thành công','THANH_CONG',94],['Giao thất bại','THAT_BAI',93],['Đang giao','DANG_GIAO',92],['Chờ giao','CHO_GIAO',91]]){
   await page.goto(base+'/tong-quan');await page.getByRole('tab',{name:'Vận hành',exact:true}).click();
   const link=page.getByRole('link',{name:new RegExp('^'+name+':')});await link.click();
   await page.waitForURL('**/giao-hang?nhom='+group);await page.getByRole('button',{name,exact:true}).waitFor();
   assert.equal(await page.getByRole('button',{name,exact:true}).getAttribute('aria-pressed'),'true');
   await page.getByText('DH'+String(id).padStart(5,'0'),{exact:true}).waitFor();assert.equal(requests.at(-1),group);
   if(group==='CHO_GIAO')assert.equal(await page.getByText('DH00093',{exact:true}).count(),0);
   if(group==='THAT_BAI')assert.equal(await page.getByRole('button',{name:'Phân công giao lại',exact:true}).count(),0); // Đủ ba lượt.
  }
  await page.getByRole('button',{name:'Giao thành công',exact:true}).click();await page.waitForURL('**nhom=THANH_CONG');await page.getByText('DH00094',{exact:true}).waitFor();
  await page.goto(base+'/tong-quan');await page.getByRole('tab',{name:'Vận hành',exact:true}).click();
  await page.getByRole('link',{name:/^Chờ tiếp nhận:/}).click();await page.waitForURL('**/?trangThai=CHO_XAC_NHAN');
  await page.locator('tbody').getByText('DH00001',{exact:true}).waitFor();assert.equal(await page.locator('tbody').getByText('DH00002',{exact:true}).count(),0);
  assert.deepEqual(errors,[]);console.log('PASS: five presets/manual date validation, four delivery tabs and dashboard links, reconciled failure excluded from waiting, queue deep link; fixture only.');
 }finally{await browser.close();server.close();}
})().catch(e=>{console.error(e);server.close();process.exitCode=1;});
