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
  const page=await browser.newPage({viewport:{width:1366,height:1000}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.addInitScript(()=>sessionStorage.setItem('photocopychain.phien',JSON.stringify({accessToken:'fixture-only',hetHanLuc:'2099-01-01T00:00:00Z',nguoiDung:{maNguoiDung:1,tenVaiTro:'SUPER_ADMIN',hoTen:'CEO thử',maChiNhanh:1,tenChiNhanh:'Cơ sở thử'}})));
  let next=0, exports=[], jobs=new Map();
  await page.route('http://localhost:5268/**',async route=>{
   const req=route.request(),p=new URL(req.url()).pathname,headers={'access-control-allow-origin':'*','access-control-allow-headers':'*','access-control-allow-methods':'*'};
   if(req.method()==='OPTIONS')return route.fulfill({status:204,headers});
   if(p.startsWith('/hubs/'))return route.fulfill({status:503,headers,body:''});
   let data=[];
   if(p==='/api/bao-tri/trang-thai')data={dangBaoTri:false,tinhNangKhoa:[]};
   else if(p==='/api/bao-tri/phien')data={hopLe:true};
   else if(p==='/api/ca/cho-tong-ket')return route.fulfill({status:204,headers});
   else if(p==='/api/ocr/tac-vu'){
    assert.equal(req.method(),'POST');const id='00000000-0000-0000-0000-'+String(++next).padStart(12,'0');jobs.set(id,next);
    data={id,state:'CHO_XU_LY',position:2};
   }else if(p.startsWith('/api/ocr/tac-vu/')){
    const id=p.split('/').pop();data={id,state:'HOAN_TAT',position:0,text:'Văn bản ảnh '+jobs.get(id),truncated:false};
   }else if(p==='/api/ocr/xuat-word'){
    exports.push(req.postDataJSON());return route.fulfill({status:200,headers:{...headers,'content-type':'application/vnd.openxmlformats-officedocument.wordprocessingml.document'},body:Buffer.from('PK fixture')});
   }
   await route.fulfill({status:200,headers,json:data});
  });
  await page.goto('http://127.0.0.1:'+server.address().port+'/nhan-dang-chu');
  await page.locator('#ocr-file').waitFor();
  const img=fs.readFileSync(process.argv[5]);
  await page.locator('#ocr-file').setInputFiles([{name:'dau.jpg',mimeType:'image/jpeg',buffer:img},{name:'sau.jpg',mimeType:'image/jpeg',buffer:img}]);
  await page.getByText('1. dau.jpg',{exact:true}).waitFor();
  await page.getByRole('button',{name:'Đưa ảnh lên trước',exact:true}).nth(1).click();
  await page.getByText('1. sau.jpg',{exact:true}).waitFor();
  await page.getByRole('button',{name:'Nhận dạng ảnh chưa xong / bị lỗi',exact:true}).click();
  await page.getByText('Ảnh 1/2 · Đang chờ · thứ tự 2',{exact:true}).waitFor();
  await page.getByRole('button',{name:'Xuất tất cả ra Word',exact:true}).waitFor({state:'visible'});
  await page.waitForFunction(()=>document.querySelector('#ocr-text')?.value==='Văn bản ảnh 2');
  await page.locator('#ocr-text').fill('Ảnh hai đã hiệu chỉnh');
  await page.locator('.ocr-file-name').nth(0).click();
  assert.equal(await page.locator('#ocr-text').inputValue(),'Văn bản ảnh 1');
  await page.locator('#ocr-text').fill('Ảnh một đã hiệu chỉnh');
  const download=page.waitForEvent('download');
  await page.getByRole('button',{name:'Xuất tất cả ra Word',exact:true}).click();
  await (await download).saveAs(path.join(out,'fixture.docx'));
  assert.deepEqual(exports,[{pages:['Ảnh một đã hiệu chỉnh','Ảnh hai đã hiệu chỉnh']}]);
  await page.screenshot({path:path.join(out,'web-desktop.png'),fullPage:true});
  for(const width of [390,768]){
   await page.setViewportSize({width,height:900});
   const close=page.getByRole('button',{name:'Đóng menu',exact:true});if(await close.isVisible())await close.click();
   await page.locator('main').evaluate(e=>e.scrollTop=0);
   assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Không được tràn ngang ở '+width);
   await page.screenshot({path:path.join(out,'web-'+width+'.png'),fullPage:true});
  }
  // Đổi lần chọn file không làm mất nội dung các ảnh đã chọn trước.
  await page.locator('#ocr-file').setInputFiles([{name:'them.jpg',mimeType:'image/jpeg',buffer:img}]);
  await page.getByText('3. them.jpg',{exact:true}).waitFor();
  await page.locator('.ocr-file-name').nth(0).click();assert.equal(await page.locator('#ocr-text').inputValue(),'Ảnh một đã hiệu chỉnh');
  assert.equal(await page.getByRole('button',{name:'Xuất tất cả ra Word',exact:true}).isDisabled(),true);
  await page.getByRole('button',{name:'Xóa ảnh',exact:true}).nth(2).click();
  assert.equal(await page.getByRole('button',{name:'Xuất tất cả ra Word',exact:true}).isDisabled(),false);
  assert.deepEqual(errors,[]);console.log('PASS OCR web: multi image, queue, ordering, edited Word, preserve previous selection, responsive390/768');
 }finally{await browser.close();server.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
