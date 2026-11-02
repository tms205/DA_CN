// Blazor thật, mọi API được chặn bằng fixture; không ghi người dùng/ca/tiền thật.
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const {expect}=require((process.env.PLAYWRIGHT_MODULE||'playwright')+'/test');
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),assert=require('node:assert/strict');
const root=path.resolve(process.argv[2]),out=path.resolve(process.argv[3]);
fs.mkdirSync(out,{recursive:true});
const mime={'.html':'text/html','.js':'text/javascript','.css':'text/css','.json':'application/json','.wasm':'application/wasm','.svg':'image/svg+xml','.png':'image/png'};
const server=http.createServer((req,res)=>{
 let file=path.resolve(root,decodeURIComponent(new URL(req.url,'http://local.test').pathname).replace(/^\/+/,''));
 if(!file.startsWith(root+path.sep))return res.writeHead(403).end();
 if(!fs.existsSync(file)||!fs.statSync(file).isFile())file=path.join(root,'index.html');
 res.setHeader('Content-Type',mime[path.extname(file)]||'application/octet-stream');fs.createReadStream(file).pipe(res);
});
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));
 const base=`http://127.0.0.1:${server.address().port}`,browser=await chromium.launch({channel:'msedge',headless:true});
 try{
  for(const width of [390,1366]){
   const context=await browser.newContext({viewport:{width,height:1000}}),page=await context.newPage();
   const errors=[],writes=[];let pending=true,revoked=false;
   const user={maNguoiDung:10,hoTen:'Nhân sự kiểm thử',email:'fixture@gmail.com',soDienThoai:'0901234567',tenVaiTro:width===1366?'SUPER_ADMIN':'STAFF',maChiNhanh:width===1366?null:1};
   const ca={maCa:5,maNhanVien:10,tenNhanVien:user.hoTen,maChiNhanh:1,tenChiNhanh:'Chi nhánh kiểm thử',gioMoCa:'2026-10-29T07:30:00',gioDongCa:'2026-10-29T12:30:00',trangThai:'CHO_TONG_KET',tienDauCa:200000,doanhThuTrongCa:0,doanhThuVietQr:30000};
   await context.addInitScript(user=>{if(!sessionStorage.getItem('fixture-started')){sessionStorage.setItem('fixture-started','1');localStorage.setItem('photocopychain.pham-vi-chi-nhanh','1');localStorage.setItem('photocopychain.phien',JSON.stringify({accessToken:'fixture-only',hetHanLuc:'2099-01-01T00:00:00Z',nguoiDung:user}));}},user);
   page.on('pageerror',e=>errors.push(e.message));
   page.on('console',m=>{if(m.type()==='error'&&/Unhandled exception|crit: Microsoft/i.test(m.text()))errors.push(m.text());});
   await context.route('**/*',async route=>{
    const req=route.request(),url=new URL(req.url()),p=url.pathname;
    if(!p.startsWith('/api/')&&!p.startsWith('/hubs/'))return route.continue();
    const send=(data,status=200)=>route.fulfill({status,contentType:'application/json',body:JSON.stringify(data),headers:{'Access-Control-Allow-Origin':'*'}});
    const empty=()=>route.fulfill({status:204,headers:{'Access-Control-Allow-Origin':'*'}});
    if(req.method()==='OPTIONS')return empty();
    if(p.startsWith('/hubs/'))return send({},503);
    if(p==='/api/bao-tri/trang-thai')return send({dangBaoTri:false});
    if(p==='/api/bao-tri/phien')return send({hopLe:!revoked},revoked?401:200);
    if(p==='/api/auth/google/config')return send({daBat:false,webClientId:''});
    if(p==='/api/auth/me/password'&&req.method()==='PUT'){
     writes.push(req.postDataJSON());revoked=true;return send({message:'Đã đổi mật khẩu. Vui lòng đăng nhập lại.'});
    }
    if(p==='/api/ca/dong'&&req.method()==='POST'){
     const body=req.postDataJSON();writes.push(body);
     assert.equal(body.maCa,5);assert.equal(body.tongDoanhThuCuoiCa,199900);assert.equal(body.ghiChu,'Thiếu 100đ, đang đối chiếu.');
     pending=false;ca.trangThai='CHO_XAC_NHAN';ca.tongDoanhThuCuoiCa=199900;return send({message:'Đã gửi tổng kết ca, chờ quản lý kiểm đếm.'});
    }
    assert.equal(req.method(),'GET','API ghi chưa khai báo: '+p);
    if(p==='/api/auth/me')return send(user);
    if(p==='/api/danh-muc/chi-nhanh')return send([{maChiNhanh:1,tenChiNhanh:ca.tenChiNhanh}]);
    if(p==='/api/ca/dang-mo')return empty();
    if(p==='/api/ca/cho-tong-ket')return pending?send(ca):empty();
    if(p==='/api/ca')return send([ca]);
    if(/\/api\/ca\/5\/don-hang/.test(p))return send([]);
    if(p==='/api/quan-tri/bao-cao/hien-tai')return send({soPhieuChoDuyet:0,soHoanTienChoDuyet:0});
    return send([]);
   });
   await page.goto(base+'/ca-lam-viec');
   const dialog=page.getByRole('dialog',{name:'Đóng ca — kiểm đếm tiền',exact:true});
   await expect(dialog).toBeVisible();
   if(width===1366){
    const poll=page.waitForRequest(r=>new URL(r.url()).pathname==='/api/ca/dang-mo',{timeout:15000});
    await dialog.getByRole('button',{name:'Đóng hộp thoại',exact:true}).click();
    await poll;await expect(dialog).not.toBeVisible();
    await page.getByRole('button',{name:'Đóng ca & kiểm đếm',exact:true}).click();
    await expect(dialog).toBeVisible();
   }
   const money=dialog.locator('input[type=number]');await expect(money).toHaveValue('');
   await money.fill('199900');await dialog.getByRole('button',{name:'Xác nhận đóng ca',exact:true}).click();
   await expect(dialog.getByRole('alert')).toContainText('lý do chênh lệch');assert.equal(writes.length,0);
   await dialog.locator('#ca-ly-do').fill('Thiếu 100đ, đang đối chiếu.');
   await page.screenshot({path:path.join(out,`tong-ket-ca-${width}.png`),fullPage:true});
   assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Không được trượt ngang');
   await dialog.getByRole('button',{name:'Xác nhận đóng ca',exact:true}).click();await expect(dialog).not.toBeVisible();
   assert.equal(writes.length,1);
   await page.goto(base+'/thong-tin-ca-nhan');
   await page.getByRole('button',{name:'Đổi mật khẩu',exact:true}).click();
   await page.locator('#mk-cu').fill('OldTest123');await page.locator('#mk-moi').fill('NewTest123');await page.locator('#mk-lai').fill('NewTest123');
   await page.getByRole('button',{name:'Lưu mật khẩu mới',exact:true}).click();
   await expect(page).toHaveURL(/dang-nhap\?mat-khau-da-doi=1/);
   await expect(page.getByRole('status')).toContainText('Vui lòng đăng nhập bằng mật khẩu mới');
   assert.equal(await page.evaluate(()=>localStorage.getItem('photocopychain.phien')),null);
   assert.equal(writes.length,2);assert.deepEqual(errors,[]);
   console.log(`PASS ${width}px ${user.tenVaiTro}: tiền để trống, bắt lý do lệch, gửi đúng ca, đổi mật khẩu xóa phiên và về đăng nhập.`);
   await context.close();
  }
 }finally{await browser.close();server.close();}
})().catch(e=>{console.error(e);server.close();process.exitCode=1;});
