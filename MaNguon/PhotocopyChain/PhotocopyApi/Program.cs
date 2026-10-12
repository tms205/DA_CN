using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using PhotocopyApi.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PhotocopyApi;
using PhotocopyApi.Hubs;
using PhotocopyApi.MayInAo;
using PhotocopyApi.ThanhToan;
using PhotocopyShared.Constants;
using PayOS;
using PhotocopyChain.CauHinh;

var builder = WebApplication.CreateBuilder(args);

// File riêng dùng chung cho API/máy in, đọc trước khi tạo dịch vụ.
string fileEnv = DocFileEnv.LayDuongDan(Environment.GetEnvironmentVariable("PHOTOCOPY_ENV_FILE"), builder.Environment.ContentRootPath);
builder.Configuration.AddInMemoryCollection(DocFileEnv.Doc(fileEnv));

// Đọc thông tin kênh thanh toán payOS từ cấu hình chung.
string payOsClientId = builder.Configuration["PayOS:ClientId"]
    ?? throw new InvalidOperationException("Thiếu cấu hình PayOS:ClientId.");

string payOsApiKey = builder.Configuration["PayOS:ApiKey"]
    ?? throw new InvalidOperationException("Thiếu cấu hình PayOS:ApiKey.");

string payOsChecksumKey = builder.Configuration["PayOS:ChecksumKey"]
    ?? throw new InvalidOperationException("Thiếu cấu hình PayOS:ChecksumKey.");

// Dùng chung một payOS client trong toàn bộ API
builder.Services.AddSingleton(
    new PayOSClient(payOsClientId, payOsApiKey, payOsChecksumKey));
// ================== ĐĂNG KÝ DỊCH VỤ ==================

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Dữ liệu gửi lên sai (thiếu email, mật khẩu yếu...) thì trả về { message: "câu báo lỗi đầu tiên" }
        // giống hệt các lỗi khác, để Web và App chỉ cần đọc "message" là hiện được
        options.InvalidModelStateResponseFactory = context =>
        {
            string loi = "Dữ liệu gửi lên không hợp lệ.";
            foreach (var truong in context.ModelState.Values)
            {
                if (truong.Errors.Count > 0 && truong.Errors[0].ErrorMessage != "")
                {
                    loi = truong.Errors[0].ErrorMessage;
                    break;
                }
            }
            return new BadRequestObjectResult(new { message = loi });
        };
    });

// Bộ nhớ tạm của API: giữ mã OTP, đếm số lần nhập sai mật khẩu, quãng đường đã hỏi bản đồ (tắt API là mất)
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<OtpDatLaiStore>();

// Dùng chung hạn mức cho gửi mã và đặt lại mật khẩu.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("khoi-phuc-mat-khau", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "khong-ro-ip",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.OnRejected = async (context, dung) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Quá nhiều yêu cầu khôi phục mật khẩu. Vui lòng thử lại sau." }, dung);
    };
});

// F-32: kênh real-time SignalR (Hubs/CapNhatHub.cs)
builder.Services.AddSignalR();

// Máy in mô phỏng và máy in vật lý sau này dùng chung luồng tự động này.
builder.Services.AddSingleton<TrangThaiMayInAoStore>();
builder.Services.AddSingleton(new QuyenNhanLenhInStore(
    builder.Configuration["PrinterConnector:ThuMucLenh"] ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhotocopyChain", "PrinterClaims")));
builder.Services.AddScoped<TuDongInService>();
builder.Services.AddHostedService<XepLaiDonChoInJob>();

// F-10 / F-31: mỗi phút hủy đơn VietQR quá hạn và xóa file của các đơn đã kết thúc
builder.Services.AddHostedService<DonQuaHanJob>();

// Đăng nhập bằng JWT: client gửi kèm header "Authorization: Bearer <token>"
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // giữ nguyên tên claim mình đặt (ma_nguoi_dung, vai_tro...)
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = TenClaim.HoTen,
            RoleClaimType = TenClaim.VaiTro   // để [Authorize(Roles = "...")] đọc đúng vai trò trong token
        };

        // Chưa đăng nhập / token hết hạn / không đủ quyền → cũng trả { message } tiếng Việt
        options.Events = new JwtBearerEvents
        {
            // SignalR chạy trên WebSocket — trình duyệt KHÔNG cho gửi header "Authorization" khi mở WebSocket,
            // nên client gửi token trong địa chỉ: /hubs/cap-nhat?access_token=... Đoạn này đọc token từ đó ra.
            OnMessageReceived = context =>
            {
                string token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = token;
                return Task.CompletedTask;
            },

            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { message = "Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Vui lòng đăng nhập lại." });
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { message = "Bạn không có quyền thực hiện thao tác này." });
            }
        };
    });
builder.Services.AddAuthorization();

// Web quản trị (Blazor) chạy khác cổng với API nên phải cho phép CORS
builder.Services.AddCors(options => options.AddPolicy("WebAdmin", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>())
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// Swagger: trang thử API tại /swagger, có nút "Authorize" để dán token
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PhotocopyChain API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Dán accessToken lấy từ /api/auth/login hoặc /api/auth/admin/login (không cần gõ chữ Bearer)."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            new string[] { }
        }
    });
});

var app = builder.Build();

// ================== XỬ LÝ REQUEST ==================

// Lỗi không lường trước (mất kết nối CSDL...) → vẫn trả { message } để Web / App hiện được.
// Chi tiết lỗi được in ra cửa sổ console của API.
app.UseExceptionHandler(loi => loi.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new { message = "Máy chủ gặp sự cố, vui lòng thử lại sau." });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Máy dev KHÔNG ép https: App trên điện thoại gọi http qua "adb reverse"
    app.UseHttpsRedirection();
}

app.UseCors("WebAdmin");
app.UseAuthentication();
// Sau khi đọc JWT, áp dụng chi nhánh CEO đã chọn trước khi kiểm [Authorize].
app.UseMiddleware<PhamViChiNhanhMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

// F-32: địa chỉ của kênh real-time. Web / App nối vào đây để nhận đơn mới, đổi trạng thái, báo đã nhận tiền.
app.MapHub<CapNhatHub>("/hubs/cap-nhat");

// Chạy một lần sau khi đổi Quick Tunnel:
// dotnet run -- --dang-ky-webhook-payos
// Cần một tiến trình API khác đang chạy để payOS gửi request kiểm tra webhook.
if (args.Contains("--dang-ky-webhook-payos"))
{
    string diaChiGoc = QuyTacPayOS.ChuanHoaDiaChiGoc(builder.Configuration["PayOS:PublicBaseUrl"]);
    if (diaChiGoc == null)
        throw new InvalidOperationException("Thiếu PayOS:PublicBaseUrl là địa chỉ HTTPS công khai.");

    string webhookUrl = diaChiGoc + "/api/thanh-toan/webhook";
    PayOSClient payOs = app.Services.GetRequiredService<PayOSClient>();
    await payOs.Webhooks.ConfirmAsync(webhookUrl);
    Console.WriteLine("Đã đăng ký webhook payOS: " + webhookUrl);
    return;
}

app.Run();
