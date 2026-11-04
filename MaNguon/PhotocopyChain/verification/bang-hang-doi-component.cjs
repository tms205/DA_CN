const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const output = process.env.PHOTOCOPY_UI_FIXTURE;
const root = path.resolve(__dirname, '../PhotocopyAdmin/wwwroot');
let fixture = fs.readFileSync(path.join(output, 'hang-doi-component.html'), 'utf8');
const server = http.createServer((req,res) => {
  if (req.url === '/fixture') {res.setHeader('Content-Type','text/html; charset=utf-8');res.end(fixture);return;}
  const file = path.resolve(root, '.' + decodeURIComponent(req.url.split('?')[0]));
  if (!file.startsWith(root + path.sep) || !fs.existsSync(file) || !fs.statSync(file).isFile()) {res.writeHead(404);res.end();return;}
  res.setHeader('Content-Type', file.endsWith('.css') ? 'text/css' : file.endsWith('.woff2') ? 'font/woff2' : 'application/octet-stream');
  fs.createReadStream(file).pipe(res);
});
(async()=>{
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const base='http://127.0.0.1:'+server.address().port;
  fixture=fixture.replace('http://localhost:5064',base);
  const browser=await chromium.launch({headless:true, executablePath:process.env.PHOTOCOPY_BROWSER_EXE});
  try {
    const results=[];
    for(const width of [390,768,1366]){
      const page=await browser.newPage({viewport:{width,height:900}});
      await page.goto(base+'/fixture');await page.evaluate(()=>document.fonts.ready);
      assert.equal(await page.locator('tbody tr').count(),1);
      assert.equal(await page.locator('tbody td').count(),6);
      assert.equal(await page.locator('tbody tr').getAttribute('tabindex'),'0');
      await page.keyboard.press('Tab');
      assert.equal(await page.evaluate(()=>document.activeElement.tagName),'TR');
      const metrics=await page.evaluate(()=>({width:innerWidth,pageWidth:document.documentElement.scrollWidth,tableWidth:document.querySelector('table').getBoundingClientRect().width}));
      assert.ok(metrics.pageWidth <= width+1,'Page overflows at '+width);
      await page.screenshot({path:path.join(output,'queue-'+width+'.png'),fullPage:true});
      results.push(metrics);await page.close();
    }
    fs.writeFileSync(path.join(output,'layout-results.json'),JSON.stringify({mode:'Rendered component fixture, no API or business mutation',results},null,2));
    console.log('PASS controlled table layout: 390/768/1366; six cells; keyboard focus; no page overflow.');
  }finally{await browser.close();server.close();}
})().catch(error=>{console.error(error.message);server.close();process.exitCode=1;});
