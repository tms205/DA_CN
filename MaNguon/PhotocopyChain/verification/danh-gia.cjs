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
  const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.addInitScript(()=>sessionStorage.setItem('photocopychain.phien',JSON.stringify({accessToken:'fixture-only',hetHanLuc:'2099-01-01T00:00:00Z',nguoiDung:{maNguoiDung:1,tenVaiTro:'SUPER_ADMIN',hoTen:'CEO thử'}})));
  const reviews=[{maDanhGia:1,maDonHangHienThi:'DH00001',tenKhachHang:'Khách một',tenChiNhanh:'Cơ sở thử',soSao:5,nhanXet:'In đẹp',trangThai:'HIEN',ngayDanhGia:'2026-11-02T10:00:00'},{maDanhGia:2,maDonHangHienThi:'DH00002',tenKhachHang:'Khách hai',tenChiNhanh:'Cơ sở thử',soSao:3,nhanXet:'Phản hồi đang ẩn',trangThai:'AN',ngayDanhGia:'2026-11-01T10:00:00'}];
  const writes=[];
  await page.route('http://localhost:5268/**',async route=>{
   const req=route.request(),p=new URL(req.url()).pathname,headers={'access-control-allow-origin':'*','access-control-allow-headers':'*','access-control-allow-methods':'*'};
   if(req.method()==='OPTIONS')return route.fulfill({status:204,headers});
   if(p.startsWith('/hubs/'))return route.fulfill({status:503,headers,body:''});
   let data=[];
   if(p==='/api/bao-tri/trang-thai')data={dangBaoTri:false,tinhNangKhoa:[]};
   else if(p==='/api/bao-tri/phien')data={hopLe:true};
   else if(p==='/api/ca/cho-tong-ket')return route.fulfill({status:204,headers});
   else if(p==='/api/don-hang/danh-gia')data=reviews;
   else if(req.method()==='PUT'){
    assert.ok(p==='/api/don-hang/danh-gia/1/an'||p==='/api/don-hang/danh-gia/1/hien');writes.push(p);reviews[0].trangThai=p.endsWith('/an')?'AN':'HIEN';data={message:'Đã cập nhật'};
   }
   await route.fulfill({status:200,headers,json:data});
  });
  await page.goto('http://127.0.0.1:'+server.address().port+'/danh-gia');
  await page.getByRole('button',{name:'Ẩn đánh giá',exact:true}).click();
  await page.getByRole('dialog').getByRole('button',{name:'Ẩn đánh giá',exact:true}).click();
  await page.waitForFunction(()=>document.querySelectorAll('td.cot-hanh-dong button').length===2&&[...document.querySelectorAll('td.cot-hanh-dong button')].every(x=>x.textContent==='Hiện đánh giá'));
  await page.getByRole('button',{name:'Hiện đánh giá',exact:true}).first().click();
  await page.getByRole('dialog').getByRole('button',{name:'Hiện đánh giá',exact:true}).click();
  await page.getByRole('button',{name:'Ẩn đánh giá',exact:true}).waitFor();
  assert.deepEqual(writes,['/api/don-hang/danh-gia/1/an','/api/don-hang/danh-gia/1/hien']);
  assert.equal(await page.getByRole('button',{name:'Hiện đánh giá',exact:true}).count(),1);
  await page.screenshot({path:path.join(out,'reviews-web.png'),fullPage:true});
  assert.deepEqual(errors,[]);console.log('PASS Reviews web: hide, confirmation, restore, keep hidden review intact');
 }finally{await browser.close();server.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
