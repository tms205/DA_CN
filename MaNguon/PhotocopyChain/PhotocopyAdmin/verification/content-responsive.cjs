// Kiểm tra CSS thật với bản ghi mẫu; không kết nối API, không dùng tài khoản hay sửa dữ liệu.
// Chạy: node verification/content-responsive.cjs (cần Playwright và Chrome).
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const { chromium } = require(process.env.PHOTOCOPY_PLAYWRIGHT || "playwright");

const root = path.resolve(__dirname, "..");
const css = ["app.css", "app-ui-v2.css"]
    .map(name => fs.readFileSync(path.join(root, "wwwroot/css", name), "utf8")).join("\n");
const pages = fs.readdirSync(path.join(root, "Pages")).filter(name => name.endsWith(".razor"));
const fixtures = [];

for (const name of pages) {
    const source = fs.readFileSync(path.join(root, "Pages", name), "utf8");
    for (const match of source.matchAll(/<table\b[\s\S]*?<\/table>/g)) {
        const table = match[0];
        const headers = [...table.matchAll(/<th(?:\s[^>]*|)>([\s\S]*?)<\/th>/g)]
            .map(m => m[1].startsWith("@") ? "Kỳ / nhân viên" : m[1].trim() || "Thao tác");
        const row = [...table.matchAll(/<tr\b[^>]*>([\s\S]*?)<\/tr>/g)]
            .find(m => m[1].includes("<td"));
        const cells = [...row[1].matchAll(/<td\b[^>]*>/g)].map(m => m[0]);
        assert.equal(cells.length, headers.length, name + ": số cột phải khớp");
        const body = cells.map((open, i) => {
            // Biểu thức Razor trong nhãn được thay bằng giá trị mẫu chỉ trong bài kiểm tra.
            open = open.replace(/data-nhan="@(\([^\n]*?\)|\w+\(\))"/g, 'data-nhan="Kỳ / nhân viên"');
            const value = headers[i].includes("Thao tác")
                ? '<div style="display:flex;gap:8px"><button class="nut-nho">Kiểm tra &amp; xác nhận</button><button class="nut-nho">Sửa thông tin</button></div>'
                : i === 0 ? '<b>DH000009</b>'
                : '<div>Nguyễn Trọng Mạnh · Cơ sở 1 – Lê Trọng Tấn</div><span class="nhan-mau nhan-xanh">Đã thanh toán thành công</span>';
            return open + value + "</td>";
        }).join("");
        const prefix = source.slice(0, match.index);
        const headerSource = prefix.slice(prefix.lastIndexOf('<div class="bang-tieu-de'));
        const title = headerSource.match(/<h2>([\s\S]*?)<\/h2>/)?.[1] || "Danh sách dữ liệu";
        const description = headerSource.match(/<p class="bang-mo-ta">([^<]*)<\/p>/)?.[1]
            || "Theo dõi và đối chiếu dữ liệu trong phạm vi chi nhánh.";
        const panelHeader = '<div class="bang-tieu-de"><div><h2>'
            + (title.startsWith("@") ? "Ca làm việc trong 7 ngày gần đây" : title)
            + '</h2><p class="bang-mo-ta">' + description
            + '</p></div><span class="bang-dem">30 bản ghi</span></div>';
        fixtures.push({
            name: name + " / bảng " + fixtures.length,
            html: '<div class="the the-bang">' + panelHeader
                + '<div class="bang-cong-cu"><div class="o-nhap nho o-tim"><input aria-label="Tìm kiếm" placeholder="Tìm tên, mã đơn hoặc số điện thoại"></div><button class="nut-nho">Làm mới</button></div>'
                + '<div class="bang-cuon"><table class="bang"><thead><tr>'
                + headers.map(h => "<th>" + h + "</th>").join("")
                + "</tr></thead><tbody><tr>" + body + "</tr></tbody></table></div></div>"
        });
    }
}

const filters = '<div class="ca-loc-luoi">'
    + Array.from({ length: 6 }, () => '<label class="truong">Bộ lọc<input class="ca-o-loc" value="Chi nhánh"></label>').join("")
    + '</div><div class="luoi-hai-cot"><div class="the"><div class="o-nhap"><input placeholder="Tên tài liệu"></div></div>'
    + '<div class="the"><dl class="bang-thong-tin"><dt>Tài liệu</dt><dd>'
    + "Tài_liệu_không_có_khoảng_trắng_".repeat(8)
    + '</dd></dl><input type="file"></div></div>'
    + '<div class="luoi-hai-cot"><div class="the"><div class="ca-loc-luoi ca-loc-staff ca-xu-ly-lech">'
    + '<label class="truong">Tiền kiểm đếm<input class="ca-o-loc" value="7400"></label>'
    + '<label class="truong ca-ghi-chu-lech">Ghi chú<textarea class="ca-o-loc">Đối chiếu tiền trong ca</textarea></label></div>'
    + '<div class="ca-loc-hanh-dong"><span>Đối chiếu ca làm việc và tiền đã nhận</span><div>'
    + '<button class="nut-nho">Kiểm tra &amp; xác nhận</button><button class="nut-nho">Xuất báo cáo</button></div></div></div>'
    + '<div class="the"><div class="bg-luoi-phat-sinh">'
    + '<label class="truong bg-truong-file">Chọn tài liệu<input type="file"></label>'
    + '<label class="truong">Số lượng<input class="ca-o-loc" value="120"></label></div></div></div>';

function documentFor(content, queue = false, rail = false) {
    const panel = '<aside class="hd-phai"><div class="hd-chi-tiet"><h2>Chi tiết đơn</h2>'
        + '<dl class="bang-thong-tin"><dt>Tên tài liệu</dt><dd>' + "Tai_lieu_dai_".repeat(12)
        + '</dd></dl><input type="file"><button class="nut-chinh">Bắt đầu in và xác nhận bàn giao</button></div></aside>';
    return '<!doctype html><html lang="vi"><meta charset="utf-8"><style>' + css + '</style><body>'
        + '<div class="khung' + (rail ? " menu-thu-gon" : "") + '"><aside class="thanh-ben">'
        + '<div class="tb-logo"><div class="tb-logo-icon">P</div><div class="tb-logo-chu"><div class="tb-logo-ten">PhotocopyChain</div><div class="tb-logo-phu">Quản trị chuỗi chi nhánh</div></div></div>'
        + '<div class="tb-chi-nhanh"><div class="tb-chi-nhanh-chu"><div class="tb-chi-nhanh-ten">Cơ sở 1 – Lê Trọng Tấn</div><div class="tb-chi-nhanh-dc">Không gian quản trị</div></div></div>'
        + '<nav class="tb-menu">' + ["Vận hành", "Ca &amp; tiền", "Quản lý"].map((group, i) =>
            '<section class="tb-nhom"><button class="tb-nhom-tieu-de" aria-expanded="true"><span>' + group
            + '</span><span class="tb-nhom-mui-ten"></span></button><div class="tb-nhom-noi-dung">'
            + [["Hàng đợi đơn", "Lập đơn tại quầy", "Bàn giao tại quầy", "Kho file in"],
                ["Ca làm việc &amp; két tiền", "Lịch sử đơn theo ca"],
                ["Kho vật tư", "Bảng giá in ấn", "Khuyến mãi", "Nhân viên", "Đánh giá khách hàng", "Phê duyệt", "Báo cáo doanh thu", "Chi nhánh &amp; máy in"]][i]
                .map(label => '<a class="tb-muc"><span>' + label + '</span></a>').join("")
            + '</div></section>').join("") + '</nav>'
        + '<div class="tb-nguoi-dung"><div class="tb-nd-lien-ket"><div class="tb-avatar">PT</div><div class="tb-nd-chu"><div class="tb-nd-ten">Phạm Văn Thọ</div><div class="tb-nd-vai-tro">Nhân viên in ấn · Hồ sơ</div></div></div></div></aside>'
        + '<section class="vung-lam-viec"><header class="thanh-tren">Hệ thống vận hành</header><main class="noi-dung">'
        + '<div class="tieu-de-trang"><div class="tdt-chu"><h1>Hàng đợi và quản lý</h1></div></div><div class="than-trang">'
        + (queue ? '<div class="hd-khung"><div class="hd-trai">' + content + '</div>' + panel + '</div>' : content)
        + '</div></main></section></div></body></html>';
}

async function assertFits(page, name) {
    const result = await page.evaluate(() => {
        const main = document.querySelector("main");
        const bounds = main.getBoundingClientRect();
        const errors = [];
        for (const node of [main, ...main.querySelectorAll("*")]) {
            const style = getComputedStyle(node);
            const rect = node.getBoundingClientRect();
            if (!rect.width || !rect.height || style.display === "none"
                || node.closest("thead") && getComputedStyle(node.closest("thead")).position === "absolute") continue;
            if (rect.left < bounds.left - 1 || rect.right > bounds.right + 1)
                errors.push(node.tagName + "." + node.className + ": vượt vùng main");
            if (!["INPUT", "SELECT", "TEXTAREA"].includes(node.tagName) && node.scrollWidth > node.clientWidth + 1)
                errors.push(node.tagName + "." + node.className + ": nội dung bị cuộn/cắt ngang");
        }
        return errors.slice(0, 6);
    });
    assert.deepEqual(result, [], name);
    const labels = await page.evaluate(() => {
        const cells = [...document.querySelectorAll("td")];
        return cells.every(cell => getComputedStyle(cell).display === "table-cell"
            || cell.hasAttribute("data-nhan") && getComputedStyle(cell, "::before").content !== "none");
    });
    assert(labels, name + ": mọi ô dạng thẻ phải có nhãn");
    const desktopTables = await page.evaluate(() => [...document.querySelectorAll("table")].every(table => {
        const area = table.closest(".bang-cuon");
        return area.clientWidth <= 600 || getComputedStyle(table).display === "table";
    }));
    assert(desktopTables, name + ": vùng đủ rộng phải giữ bố cục bảng, không chuyển thành thẻ");
    const sidebar = await page.evaluate(() => {
        const expanded = !document.querySelector(".khung").classList.contains("menu-thu-gon");
        const side = document.querySelector(".thanh-ben");
        return {
            desktop: innerWidth >= 1024 && expanded,
            width: side.getBoundingClientRect().width,
            fontSize: getComputedStyle(side.querySelector(".tb-muc")).fontSize
        };
    });
    if (sidebar.desktop) assert.equal(sidebar.width, 361, name + ": sidebar phải rộng 361px");
    assert.equal(sidebar.fontSize, "17.5px", name + ": chữ mục menu phải tăng thêm 1px");
}

(async () => {
    const browser = await chromium.launch({
        executablePath: process.env.PHOTOCOPY_CHROME || "C:/Program Files/Google/Chrome/Application/chrome.exe",
        headless: true
    });
    let total = 0;
    try {
        for (const width of [1920, 1536, 1440, 1366, 1280, 1024, 768, 390, 320]) {
            const context = await browser.newContext({ viewport: { width, height: 1000 } });
            // Các trang mẫu không được phép gửi bất kỳ yêu cầu mạng nào.
            await context.route("**/*", route => route.abort());
            const page = await context.newPage();
            for (const fixture of fixtures) {
                await page.setContent(documentFor(fixture.html));
                await assertFits(page, width + "px: " + fixture.name);
                const actions = page.getByRole("main").getByRole("button");
                for (let i = 0; i < await actions.count(); i++) await actions.nth(i).click();
                total++;
            }
            await page.setContent(documentFor(filters));
            await assertFits(page, width + "px: form và bộ lọc");
            total++;
            for (const rail of [false, true]) {
                await page.setContent(documentFor(fixtures.find(f => f.name.startsWith("HangDoiDon")).html, true, rail));
                await assertFits(page, width + "px: hàng đợi / sidebar " + (rail ? "thu gọn" : "đầy đủ"));
                if (!rail && [1440, 390].includes(width) && process.env.PHOTOCOPY_SCREENSHOTS) {
                    await page.screenshot({ path: path.join(process.env.PHOTOCOPY_SCREENSHOTS, "content-" + width + ".png") });
                }
                total++;
            }
            await context.close();
            console.log("PASS " + width + "px");
        }
        console.log("PASS " + total + " bố cục mẫu dùng CSS thật; không kiểm thử API/nghiệp vụ.");
    } finally {
        await browser.close();
    }
})().catch(error => {
    console.error(error.message);
    process.exitCode = 1;
});
