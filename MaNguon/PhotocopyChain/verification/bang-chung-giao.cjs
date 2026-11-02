// Fixture cục bộ, không gọi API/database thật. node <script> <wwwroot publish> <playwright> <QA output>
const { chromium } = require(process.argv[3]);
const http = require('node:http'), fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const root = path.resolve(process.argv[2]), out = path.resolve(process.argv[4]);
fs.mkdirSync(out, {recursive:true});
const mime = {'.html':'text/html','.js':'text/javascript','.json':'application/json','.css':'text/css','.wasm':'application/wasm','.svg':'image/svg+xml'};
const server = http.createServer((req,res) => {
  let file = path.resolve(root, '.' + new URL(req.url,'http://fixture').pathname);
  if (!file.startsWith(root + path.sep)) { res.writeHead(403).end(); return; }
  if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) file = path.join(root,'index.html');
  res.setHeader('Content-Type',mime[path.extname(file)] || 'application/octet-stream'); fs.createReadStream(file).pipe(res);
});
(async () => {
  await new Promise(resolve => server.listen(0,'127.0.0.1',resolve));
  const browser = await chromium.launch({headless:true,channel:'chrome'});
  try {
    for (const role of ['BRANCH_ADMIN','SUPER_ADMIN','STAFF']) {
      const page = await browser.newPage({viewport:{width:1366,height:900}});
      const errors=[]; page.on('pageerror',e=>errors.push(e.message));
      let done=false;
      await page.addInitScript(role => sessionStorage.setItem('photocopychain.phien',JSON.stringify({
        accessToken:'fixture-not-real',hetHanLuc:'2099-01-01T00:00:00Z',nguoiDung:{maNguoiDung:1,hoTen:'Nhân sự thử',
          tenVaiTro:role,maChiNhanh:1,tenChiNhanh:'Cơ sở thử'}})),role);
      await page.route('http://localhost:5268/**',async route=>{
        const u=new URL(route.request().url());
        const headers={'access-control-allow-origin':'*','access-control-allow-headers':'*','access-control-allow-methods':'*'};
        if (route.request().method()==='OPTIONS') return route.fulfill({status:204,headers});
        if (u.pathname.startsWith('/hubs/')) return route.fulfill({status:503,headers,body:''});
        let data=[];
        if (u.pathname==='/api/giao-hang') data=[{maDonHang:15,maDonHangHienThi:'DH00015',maGiaoHang:77,maChiNhanh:1,
          tenChiNhanh:'Cơ sở thử',tenKhachHang:'Khách thử',sdtNhan:'0901234567',diaChiGiao:'123 Lê Lợi',
          tongTien:39000,phiShip:19000,trangThaiDonHang:'GIAO_THAT_BAI',trangThaiThanhToan:'DA_THANH_TOAN',
          trangThaiGiao:'THAT_BAI',trangThaiDoiChieu:'CHO_DOI_CHIEU',soPhanHoiChoDoiChieu:done?1:2,lanGiao:3,soLanGiao:3,tenShipper:'Shipper thử',lyDo:'Khách không nghe máy',ngayDat:'2026-11-01T09:00:00'}];
        else if(u.pathname==='/api/giao-hang/don/15/lich-su') data=[1,2,3].map(n=>({
          maDonHang:15,maGiaoHang:76+n,lanGiao:n,tenShipper:'Shipper lượt '+n,trangThai:'THAT_BAI',
          trangThaiDoiChieu:n===2?null:n===1&&done?'DA_XU_LY':'CHO_DOI_CHIEU',ngayNhan:'2026-11-01T09:00:00',ngayGiao:'2026-11-01T09:30:00'}));
        else if(u.pathname.endsWith('/doi-chieu/xu-ly')) {
          assert.equal(route.request().method(),'PUT'); assert.ok(u.pathname.includes('/luot/77/'));
          assert.equal(route.request().postDataJSON().ketLuan,'Đã đối chiếu và thống nhất giao lại với khách.');
          done=true; data={message:'Đã lưu kết luận'};
        }
        else if(u.pathname.endsWith('/doi-chieu')) { const n=Number(u.pathname.split('/')[4])-76;
          data={maDonHang:15,maGiaoHang:76+n,lanGiao:n,phanHoi:n===2?null:{
          maKhachHang:5,noiDung:'Tôi không thấy cuộc gọi <script>alert(1)</script>',guiLuc:'2026-11-01T02:32:00Z',
          trangThai:n===1&&done?'DA_XU_LY':'CHO_DOI_CHIEU',anh:[{maAnh:'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',loaiNoiDung:'image/png'}],
          ketLuan:n===1&&done?'Đã đối chiếu và thống nhất giao lại với khách.':null,tenNguoiXuLy:'Quản lý thử',xuLyLuc:'2026-11-01T03:00:00Z'}}; }
        else if (u.pathname.endsWith('/bang-chung')) { const n=Number(u.pathname.split('/')[4])-76;
          data={maDonHang:15,maGiaoHang:76+n,lanGiao:n,tenShipper:'Shipper thử',
          lyDo:'Khách không nghe máy <script>alert(1)</script>',viDo:10.7,kinhDo:106.7,doChinhXac:12,diaChi:'123 Lê Lợi, TP.HCM',
          thoiDiemGps:'2026-11-01T02:30:00Z',thoiDiemBaoCao:'2026-11-01T02:31:00Z',anh:[{maAnh:'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',loaiNoiDung:'image/png'}]}; }
        else if (u.pathname.includes('/bang-chung/anh/') || u.pathname.includes('/doi-chieu/anh/')) return route.fulfill({status:200,headers:{...headers,'content-type':'image/png'},
          body:Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=','base64')});
        else if (u.pathname.includes('bao-tri/') || u.pathname.endsWith('/hien-tai')) data={dangBaoTri:false};
        await route.fulfill({status:200,headers,json:data});
      });
      await page.goto('http://127.0.0.1:'+server.address().port+'/giao-hang?nhom=THAT_BAI');
      await page.getByText('DH00015',{exact:true}).waitFor({timeout:60000});
      for (const width of [1366,1024,768,390,320]) {
        await page.setViewportSize({width,height:950});
        await page.screenshot({path:path.join(out,`giao-hang-${role}-${width}.png`),fullPage:true});
        const size=await page.evaluate(()=>({scroll:document.documentElement.scrollWidth,client:document.documentElement.clientWidth}));
        assert.ok(size.scroll<=size.client+1,'Trang giao hàng tràn ngang '+width+': '+JSON.stringify(size));
      }
      if (role==='STAFF') { assert.equal(await page.getByRole('button',{name:'Lịch sử & đối chiếu',exact:true}).count(),0); }
      else {
        await page.getByText('2 phản hồi chờ đối chiếu',{exact:true}).waitFor();
        await page.getByRole('button',{name:'Lịch sử & đối chiếu',exact:true}).click();
        const modal=page.locator('dialog[open]');
        const attempts=modal.getByRole('group',{name:'Chọn lượt giao để xem bằng chứng và đối chiếu'});
        assert.equal(await attempts.getByRole('button').count(),3);
        await modal.getByRole('heading',{name:'Ý kiến của khách hàng'}).waitFor();
        assert.ok((await modal.innerText()).includes('Shipper lượt 1'));
        for (const width of [1366,390]) {
          await page.setViewportSize({width,height:900});
          await modal.locator('.qt-popup-than').evaluate(el=>{el.scrollTop=0;});
          await page.screenshot({path:path.join(out,`lich-su-${width}.png`)});
        }
        await attempts.getByRole('button',{name:/Lượt 2/}).click();
        await modal.getByRole('button',{name:'Phản hồi khách hàng',exact:true}).click();
        await modal.getByText('Khách chưa gửi bằng chứng đối chiếu cho lượt giao này.',{exact:true}).waitFor();
        await attempts.getByRole('button',{name:/Lượt 3/}).click();
        await modal.getByRole('heading',{name:'Ý kiến của khách hàng'}).waitFor();
        assert.ok((await modal.innerText()).includes('Lượt 3'));
        await attempts.getByRole('button',{name:/Lượt 1/}).click();
        await modal.getByRole('heading',{name:'Ý kiến của khách hàng'}).waitFor();
        await modal.getByRole('button',{name:'Bằng chứng shipper',exact:true}).click();
        await modal.getByRole('heading',{name:'Lý do giao thất bại'}).waitFor();
        await modal.locator('img').waitFor();
        await modal.evaluate(el=>Promise.all(el.getAnimations().map(a=>a.finished)));
        assert.equal(await modal.locator('script').count(),0);
        assert.ok((await modal.innerText()).includes('Lượt 1/3'));
        for (const width of [1366,768,390,320]) {
          await page.setViewportSize({width,height:900});
          await page.screenshot({path:path.join(out,`bang-chung-${width}.png`)});
          const d=await modal.evaluate(el=>({scroll:el.scrollWidth,client:el.clientWidth}));
          assert.ok(d.scroll<=d.client+1,'Popup tràn ngang '+width+': '+JSON.stringify(d));
        }
        await modal.getByRole('button',{name:'Phản hồi khách hàng',exact:true}).click();
        await modal.getByRole('heading',{name:'Ý kiến của khách hàng'}).waitFor();
        await modal.locator('img').waitFor();
        assert.equal(await modal.locator('script').count(),0);
        for (const width of [1366,768,390,320]) {
          await page.setViewportSize({width,height:900});
          await modal.locator('textarea').scrollIntoViewIfNeeded();
          await page.screenshot({path:path.join(out,`doi-chieu-${width}.png`)});
          const d=await modal.evaluate(el=>({scroll:el.scrollWidth,client:el.clientWidth}));
          assert.ok(d.scroll<=d.client+1,'Đối chiếu tràn ngang '+width+': '+JSON.stringify(d));
        }
        await modal.getByLabel('Nội dung trả lời khách').fill('Đã đối chiếu và thống nhất giao lại với khách.');
        await modal.getByRole('button',{name:'Lưu kết luận và thông báo khách',exact:true}).click();
        await modal.getByRole('heading',{name:'Kết quả đối chiếu'}).waitFor();
        assert.equal(await modal.locator('textarea').count(),0);
        assert.equal(done,true);
        await modal.getByText('1 phản hồi chờ đối chiếu',{exact:true}).waitFor();
        assert.ok((await attempts.getByRole('button',{name:/Lượt 1/}).innerText()).includes('Đã đối chiếu'));
        assert.ok((await attempts.getByRole('button',{name:/Lượt 3/}).innerText()).includes('Chờ đối chiếu'));
        await modal.getByRole('button',{name:'Đóng',exact:true}).click();
        assert.equal(await page.locator('dialog[open]').count(),0);
        await page.getByText('1 phản hồi chờ đối chiếu',{exact:true}).waitFor();
        await page.getByRole('button',{name:'Lịch sử & đối chiếu',exact:true}).click();
        await page.locator('dialog[open]').getByRole('heading',{name:'Ý kiến của khách hàng'}).waitFor();
        assert.ok((await page.locator('dialog[open]').innerText()).includes('Shipper lượt 3'));
      }
      assert.deepEqual(errors,[]); await page.close();
      console.log('PASS role '+role);
    }
  } finally { await browser.close(); server.close(); }
})().catch(error=>{console.error(error);server.close();process.exitCode=1;});
