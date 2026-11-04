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
  for(const role of ['SUPER_ADMIN','BRANCH_ADMIN']){
   const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
   await page.addInitScript(role=>sessionStorage.setItem('photocopychain.phien',JSON.stringify({accessToken:'fixture-only',hetHanLuc:'2099-01-01T00:00:00Z',nguoiDung:{maNguoiDung:role==='SUPER_ADMIN'?1:2,tenVaiTro:role,hoTen:'Người thử',maChiNhanh:role==='SUPER_ADMIN'?null:1,tenChiNhanh:'Cơ sở thử'}})),role);
   const order=(state,id)=>({maDonHang:id,maDonHangHienThi:'DH'+String(id).padStart(5,'0'),maChiNhanh:1,tenChiNhanh:'Cơ sở thử',maKhachHang:10,tenKhachHang:'Khách thử',ngayDat:'2026-11-03T08:00:00',hinhThucNhan:'TAI_QUAY',kenhDatDon:'APP',giayChoTiepNhan:0,choHuyKhach:false,phuongThucThanhToan:'VIETQR',trangThaiThanhToan:'DA_THANH_TOAN',trangThaiDonHang:state,tongTien:25000,dongIn:[],tienDo:[]});
   await page.route('http://localhost:5268/**',async route=>{
    const req=route.request(),p=new URL(req.url()).pathname,headers={'access-control-allow-origin':'*','access-control-allow-headers':'*','access-control-allow-methods':'*'};
    if(req.method()==='OPTIONS')return route.fulfill({status:204,headers});
    if(p.startsWith('/hubs/'))return route.fulfill({status:503,headers,body:''});
    assert.equal(req.method(),'GET','UI verification must not submit real operations');let data=[];
    if(p==='/api/bao-tri/trang-thai')data={dangBaoTri:false,tinhNangKhoa:[]};
    else if(p==='/api/bao-tri/phien')data={hopLe:true};
    else if(p==='/api/ca/cho-tong-ket')return route.fulfill({status:204,headers});
    else if(p==='/api/ca/dang-mo')data={maCa:1,trangThai:'DANG_MO'};
    else if(p==='/api/danh-muc/chi-nhanh')data=[{maChiNhanh:1,tenChiNhanh:'Cơ sở thử',trangThai:'HOAT_DONG'}];
    else if(p==='/api/quan-tri/bao-cao/yeu-cau-hoan-tien')data=[{maDonHang:9001,tenKhachHang:'Khách cần hoàn',tenChiNhanh:'Cơ sở thử',soTien:25000,loai:'HUY_DON',trangThai:'CHAP_THUAN',guiLuc:'2026-11-03T08:00:00Z'}];
    else if(p==='/api/quan-tri/bao-cao/hien-tai')data={soHoanTienChoDuyet:1,soPhieuChoDuyet:0,docLuc:'2026-11-03T08:00:00',tuNgayKiemDem:'2026-10-03T00:00:00'};
    else if(p==='/api/quan-tri/bao-cao/phan-tich')data={dichVu:[],thanhToan:[],gioCaoDiem:[],hieuQua:{},danhGia:{}};
    else if(p==='/api/don-hang/hang-doi')data=['CHO_XAC_NHAN','DA_XAC_NHAN','DANG_IN','DA_IN'].map((x,i)=>order(x,9001+i));
    else if(p==='/api/don-hang/9001')data=order('DA_HUY',9001);
    await route.fulfill({status:200,headers,json:data});
   });
   const base=process.argv[5]||('http://127.0.0.1:'+server.address().port);
   await page.goto(base+'/tong-quan');const alert=page.getByRole('region',{name:'Yêu cầu hoàn tiền cần xử lý'});
   await alert.getByRole('heading',{name:'1 yêu cầu đang chờ · 25.000₫'}).waitFor();
   assert.ok(await alert.getByText('DH09001 · Khách cần hoàn').isVisible());
   if(role==='SUPER_ADMIN'){
    assert.equal(await page.locator('#menu-van-hanh a[href="phe-duyet"]').count(),1);
    assert.equal(await page.locator('#menu-quan-ly a[href="phe-duyet"]').count(),0);
    assert.equal(await alert.getByRole('link',{name:'Xử lý hoàn tiền →'}).getAttribute('href'),'phe-duyet?hoan=don-huy&don=9001#hoan-tien');
   }else{
    assert.equal(await page.locator('a[href="phe-duyet"]').count(),0);
    await alert.getByRole('link',{name:'Xem đơn →'}).click();
    await page.waitForURL('**/?xemDon=9001');
    await page.getByText('Đã hủy',{exact:true}).first().waitFor();
   }
   await page.goto(base+'/');await page.locator('.thanh-loc .chip').first().waitFor();
   const colors=await page.locator('.thanh-loc .chip').evaluateAll(nodes=>nodes.map(x=>getComputedStyle(x).backgroundColor));
   assert.equal(new Set(colors).size,5,'All tab fills must be distinct, including All');
   const waiting=page.locator('.thanh-loc .hd-tab-cho');assert.equal(await waiting.evaluate(x=>getComputedStyle(x).fontWeight),'800');
   await page.screenshot({path:path.join(out,'queue-colors-'+role+'.png'),fullPage:true});
   await page.goto(base+'/tong-quan');await alert.waitFor();await page.screenshot({path:path.join(out,'refund-alert-'+role+'.png'),fullPage:true});
   await page.setViewportSize({width:390,height:844});await page.screenshot({path:path.join(out,'refund-alert-small-'+role+'.png'),fullPage:true});
   assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth+1),'No page horizontal overflow');
   assert.deepEqual(errors,[]);await page.close();
  }
  console.log('PASS dashboard refund alerts for CEO and branch manager, scope-appropriate links, sidebar move, distinct queue colors and 390px layout');
 }finally{await browser.close();server.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
