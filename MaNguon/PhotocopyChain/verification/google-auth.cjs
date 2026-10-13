// Kiểm UI thật; thay Google SDK và API bằng fixture, không gửi token/ghi CSDL thật.
const { expect } = require((process.env.PLAYWRIGHT_MODULE || 'playwright') + '/test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { createDanhMucFixture } = require('./quan-ly-danh-muc.cjs');

async function kiemTraGoogle(browser, base, output) {
  const results = [];
  for (const width of [390, 1366]) {
    const context = await browser.newContext({ viewport: { width, height: 1000 } });
    const page = await context.newPage(), errors = [], posts = [];
    const danhMuc = createDanhMucFixture();
    let enabled = false, sdkLoi = false, choPhep = false;
    page.on('pageerror', error => errors.push(error.message));
    await context.route('**/*', async route => {
      const req = route.request(), url = new URL(req.url());
      if (url.origin === 'https://accounts.google.com') {
        if (sdkLoi) return route.abort();
        return route.fulfill({ contentType: 'text/javascript', body: `
          window.google = { accounts: { id: {
            initialize: cfg => { window.fixtureGoogleCallback = cfg.callback; },
            renderButton: (el, cfg) => {
              const btn = document.createElement('button');
              btn.textContent = 'Tiếp tục với Google'; btn.style.cssText = 'height:44px;width:' + cfg.width + 'px';
              btn.onclick = () => window.fixtureGoogleCallback({ credential: 'fixture-google-credential-not-real' });
              el.replaceChildren(btn);
            }
          } } };` });
      }
      if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) {
        const tra = (status, body) => route.fulfill({ status, contentType: 'application/json',
          headers: { 'Access-Control-Allow-Origin': '*' }, body: JSON.stringify(body) });
        if (url.pathname === '/api/auth/google/config')
          return tra(200, { daBat: enabled, webClientId: enabled ? 'fixture-client.apps.googleusercontent.com' : '' });
        if (url.pathname === '/api/auth/admin/google') {
          assert.equal(req.method(), 'POST');
          const body = req.postDataJSON();
          assert.deepEqual(Object.keys(body).sort(), ['ghiNho', 'idToken']);
          assert.equal(body.idToken, 'fixture-google-credential-not-real'); posts.push(body);
          return choPhep ? tra(200, { accessToken: 'fixture-system-jwt-not-real', hetHanLuc: '2099-01-01T00:00:00Z',
            nguoiDung: { maNguoiDung: 1, hoTen: 'CEO kiểm thử', tenVaiTro: 'SUPER_ADMIN' } })
            : tra(403, { message: 'Email Google này chưa được cấp tài khoản nhân sự.' });
        }
        assert.ok(req.method() === 'GET' || url.pathname.startsWith('/hubs/'), 'Không ghi backend thật.');
        const fixture = await danhMuc.response(req, 0);
        if (fixture) return tra(fixture.status, fixture.body);
        if (url.pathname === '/api/quan-tri/bao-cao/hien-tai') return tra(200, { soPhieuChoDuyet: 0, soHoanTienChoDuyet: 0 });
        if (url.pathname.startsWith('/hubs/')) return tra(503, {});
        return tra(200, []);
      }
      // Chỉ static app được ra mạng, ngăn fixture gọi nhầm server thật.
      if (url.origin !== base && url.origin !== 'https://fonts.googleapis.com' && url.origin !== 'https://fonts.gstatic.com') return route.abort();
      return route.continue();
    });
    try {
      await page.goto(base + '/dang-nhap');
      await expect(page.getByText('Chưa cấu hình Google. Bạn vẫn có thể đăng nhập bằng mật khẩu.')).toBeVisible();
      await expect(page.getByRole('button', { name: 'Tiếp tục với Google' })).toBeDisabled();
      enabled = true; sdkLoi = true;
      await page.reload();
      await expect(page.getByRole('alert')).toContainText('Không mở được Google');
      sdkLoi = false;
      await page.getByRole('button', { name: 'Thử lại Google' }).click();
      await expect(page.getByRole('button', { name: 'Tiếp tục với Google' })).toBeVisible();
      await page.getByRole('button', { name: 'Tiếp tục với Google' }).focus();
      await page.keyboard.press('Enter');
      await expect(page.getByRole('alert')).toContainText('chưa được cấp tài khoản nhân sự');
      assert.equal(posts.length, 1);
      assert.equal(await page.evaluate(() => localStorage.getItem('photocopychain.phien')), null);
      await page.getByRole('button', { name: 'Thử lại Google' }).click();
      await expect(page.getByRole('button', { name: 'Tiếp tục với Google' })).toBeVisible();
      await page.screenshot({ path: path.join(output, `google-login-${width}.png`), fullPage: true });
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Login không tràn ngang.');

      // Callback muộn sau khi rời login không gửi request nữa.
      await page.getByRole('link', { name: 'Quên mật khẩu?' }).click();
      await expect(page.getByRole('heading', { name: 'Quên mật khẩu', exact: true })).toBeVisible();
      await page.evaluate(() => window.fixtureGoogleCallback({ credential: 'fixture-late-not-real' }));
      assert.equal(posts.length, 1);

      choPhep = true;
      await page.goto(base + '/dang-nhap?returnUrl=khach-hang');
      await expect(page.getByRole('button', { name: 'Tiếp tục với Google' })).toBeVisible();
      await page.getByText('Ghi nhớ trong 8 giờ', { exact: true }).click();
      await expect(page.getByRole('checkbox', { name: 'Ghi nhớ trong 8 giờ' })).not.toBeChecked();
      await page.getByRole('button', { name: 'Tiếp tục với Google' }).click();
      await page.waitForURL(base + '/khach-hang');
      await expect(page.getByRole('heading', { name: 'Khách hàng', exact: true })).toBeVisible();
      const phien = await page.evaluate(() => ({ local: localStorage.getItem('photocopychain.phien'), session: sessionStorage.getItem('photocopychain.phien') }));
      assert.equal(phien.local, null); assert.ok(phien.session);
      assert.equal(JSON.parse(phien.session).accessToken, 'fixture-system-jwt-not-real');
      assert.equal(phien.session.includes('fixture-google-credential'), false);
      assert.equal(posts[1].ghiNho, false);
      assert.deepEqual(errors, []);
      results.push({ width, status: 'PASS', scenarios: 'Google disabled/SDK failure/retry/API denied/keyboard/dispose/session/returnUrl/layout; SDK+API fixture only' });
    } catch (error) {
      await page.screenshot({ path: path.join(output, `google-failure-${width}.png`), fullPage: true });
      throw error;
    } finally { await context.close(); }
  }
  return results;
}
module.exports = { kiemTraGoogle };
