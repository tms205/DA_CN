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
  await page.addInitScript(()=>{sessionStorage.setItem('photocopychain.phien',JSON.stringify({accessToken:'fixture-only',hetHanLuc:'2099-01-01T00:00:00Z',nguoiDung:{maNguoiDung:1,tenVaiTro:'SUPER_ADMIN',hoTen:'CEO thử',maChiNhanh:1,tenChiNhanh:'Cơ sở thử'}}));localStorage.setItem('photocopychain.pham-vi-chi-nhanh','1');});
  let jobCount=0,ready=false,jobDeletes=0,created=0,priceReads=0,files=0,paymentReads=0;let exports=[];
  await page.route('http://localhost:5268/**',async route=>{
   const req=route.request(),p=new URL(req.url()).pathname,headers={'access-control-allow-origin':'*','access-control-allow-headers':'*','access-control-allow-methods':'*'};
   if(req.method()==='OPTIONS')return route.fulfill({status:204,headers});
   if(p.startsWith('/hubs/'))return route.fulfill({status:503,headers,body:''});
   let data=[];
   if(p==='/api/bao-tri/trang-thai')data={dangBaoTri:false,tinhNangKhoa:[]};
   else if(p==='/api/bao-tri/phien')data={hopLe:true};
   else if(p==='/api/ca/cho-tong-ket')return route.fulfill({status:204,headers});
   else if(p==='/api/danh-muc/chi-nhanh')data=[{maChiNhanh:1,tenChiNhanh:'Cơ sở thử',diaChi:'Địa chỉ thử',trangThai:'HOAT_DONG'}];
   else if(p==='/api/danh-muc/bang-gia'){priceReads++;data=[{maDichVu:1,tenDichVu:'In A4',loaiDichVu:'IN_AN',khoGiay:'A4',loaiGiay:'70gsm',matIn:'1_MAT',mauSac:'DEN_TRANG',donGia:300,maVatTu:1},{maDichVu:13,tenDichVu:'Đóng bìa kiếng',loaiDichVu:'GIA_CONG',nhomGiaCong:'DONG_BIA_KIENG',donGia:5000}];}
   else if(p==='/api/don-hang/khach-tai-quay'){created++;data={maKhachHang:77,hoTen:'Khách bản nháp',soDienThoai:'0901234567',vuaTao:false};}
   else if(p==='/api/file-in'){files++;data={maFileIn:101,tenFileGoc:'tai-lieu.pdf',soTrang:3,trangThaiFile:'SAN_SANG',dinhDangGoc:'pdf',dungLuong:1000};}
   else if(p==='/api/bao-gia')data={tienIn:900,tienGiaCong:0,phuPhiGap:0,giamGia:0,tongTien:900};
   else if(p==='/api/ocr/tac-vu'){jobCount++;data={id:'00000000-0000-0000-0000-000000000001',state:'CHO_XU_LY',position:2};}
   else if(p.startsWith('/api/ocr/tac-vu/')){if(req.method()==='DELETE')jobDeletes++;data=ready?{state:'HOAN_TAT',text:'Văn bản OCR gốc',truncated:false}:{state:'DANG_XU_LY',position:0};}
   else if(p==='/api/ocr/xuat-word'){exports.push(req.postDataJSON());return route.fulfill({status:200,headers:{...headers,'content-type':'application/vnd.openxmlformats-officedocument.wordprocessingml.document'},body:Buffer.from('PK fixture')});}
   else if(p==='/api/thanh-toan/88'){paymentReads++;data={maDonHang:88,maDonHangHienThi:'DH00088',phuongThuc:'VIETQR',trangThai:'DA_THANH_TOAN'};}
   await route.fulfill({status:200,headers,json:data});
  });
  const base='http://127.0.0.1:'+server.address().port;
  const go=async target=>{await page.evaluate(t=>{const a=document.createElement('a');a.href=t;document.body.append(a);a.click();a.remove();},target);await page.waitForURL('**'+target);};
  const active=()=>page.locator('section.ban-nhap-tac-vu:not([hidden])');
  await page.goto(base+'/lap-don-tai-quay');
  await active().getByPlaceholder('Nguyễn Văn An').fill('Khách bản nháp');await active().getByPlaceholder('09xxxxxxxx').fill('0901234567');
  await go('/nhan-dang-chu');await active().locator('#ocr-file').waitFor();
  await go('/lap-don-tai-quay');assert.equal(await active().getByPlaceholder('Nguyễn Văn An').inputValue(),'Khách bản nháp');assert.equal(await active().getByPlaceholder('09xxxxxxxx').inputValue(),'0901234567');
  await active().getByRole('button',{name:'Tìm / tạo khách hàng',exact:true}).click();
  await active().locator('input[type=file]').setInputFiles({name:'tai-lieu.pdf',mimeType:'application/pdf',buffer:Buffer.from('%PDF-1.4 fixture')});
  await active().getByText('tai-lieu.pdf',{exact:true}).waitFor();
  await active().locator('#ldq-huong').selectOption('NGANG');await active().locator('#ldq-ban').fill('3');await active().locator('#ldq-ma-khuyen-mai').fill('TESTDRAFT');
  await active().getByPlaceholder('Ví dụ: bấm kim góc trái').fill('Ghi chú đang nhập');
  await go('/nhan-dang-chu');
  const img=fs.readFileSync(process.argv[5]);await active().locator('#ocr-file').setInputFiles({name:'chu.jpg',mimeType:'image/jpeg',buffer:img});
  await active().getByRole('button',{name:'Cắt vùng chữ',exact:true}).click();await page.getByRole('dialog').getByRole('button',{name:'Dùng vùng đã chọn',exact:true}).click();
  await active().getByRole('button',{name:'Nhận dạng ảnh chưa xong / bị lỗi',exact:true}).click();
  await active().getByText('Ảnh 1/1 · Đang chờ · thứ tự 2',{exact:true}).waitFor();
  await go('/lap-don-tai-quay');
  assert.equal(jobDeletes,0,'Chuyển menu không được hủy OCR');
  assert.equal(await active().locator('#ldq-huong').inputValue(),'NGANG');assert.equal(await active().locator('#ldq-ban').inputValue(),'3');assert.equal(await active().locator('#ldq-ma-khuyen-mai').inputValue(),'TESTDRAFT');assert.equal(await active().getByPlaceholder('Ví dụ: bấm kim góc trái').inputValue(),'Ghi chú đang nhập');
  assert.equal(created,1);assert.equal(files,1);assert.equal(priceReads,1,'Không khởi tạo lại màn hình quầy');
  ready=true;
  await page.waitForFunction(()=>document.querySelector('section[hidden] #ocr-text')?.value==='Văn bản OCR gốc');
  assert.match(await page.title(),/Lập đơn tại quầy/,'OCR ẩn không được đổi tiêu đề trang');
  await go('/nhan-dang-chu');await active().locator('#ocr-text').fill('Đã sửa và giữ nguyên qua đổi menu');
  assert.equal(await active().getByRole('button',{name:'Dùng ảnh gốc',exact:true}).count(),1,'Giữ vùng cắt');
  await go('/lap-don-tai-quay');await go('/nhan-dang-chu');assert.equal(await active().locator('#ocr-text').inputValue(),'Đã sửa và giữ nguyên qua đổi menu');assert.equal(jobCount,1);
  const download=page.waitForEvent('download');await active().getByRole('button',{name:'Xuất tất cả ra Word',exact:true}).click();await(await download).saveAs(path.join(out,'draft.docx'));assert.deepEqual(exports,[{pages:['Đã sửa và giữ nguyên qua đổi menu']}]);
  await go('/lap-don-tai-quay?maDonHang=88');await page.waitForFunction(()=>document.querySelector('main')?.textContent.includes('DH00088'));assert.equal(paymentReads,1,'Truy vấn payOS vẫn hoạt động trên trang được giữ');
  assert.equal(await active().locator('#ldq-ban').inputValue(),'3');
  await page.screenshot({path:path.join(out,'counter-draft.png'),fullPage:true});
  assert.deepEqual(errors,[]);console.log('PASS drafts: name/phone, uploaded file, print options, OCR crop/edit/background, title, Word, payOS query; no duplicate requests');
 }finally{await browser.close();server.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
