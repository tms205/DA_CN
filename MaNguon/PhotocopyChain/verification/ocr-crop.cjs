// Kiểm tra quy đổi tọa độ kéo cắt, không gọi API/model.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
(async () => {
 const source = fs.readFileSync(path.join(__dirname, '../PhotocopyAdmin/wwwroot/js/ocr-tools.js'), 'utf8');
 const { normalizeRect } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
 assert.deepEqual(normalizeRect([.8,.9],[.2,.1]), [.2,.1,.6000000000000001,.8]);
 assert.deepEqual(normalizeRect([-1,-2],[2,3]), [0,0,1,1]);
 assert.equal(normalizeRect([.2,.2],[.2,.2]), null);
 console.log('Crop coordinate checks: PASS');
})().catch(e=>{console.error(e);process.exit(1)});
