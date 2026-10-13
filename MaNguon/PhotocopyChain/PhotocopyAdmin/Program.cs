using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using PhotocopyAdmin;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Web chỉ gọi API, không bao giờ kết nối thẳng CSDL. Địa chỉ API ghi trong wwwroot/appsettings.json.
string diaChiApi = builder.Configuration["ApiBaseUrl"];
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(diaChiApi) });

// Thư viện giao diện MudBlazor
builder.Services.AddMudServices();

// Phiên đăng nhập: cho Blazor biết ai đang đăng nhập để [Authorize(Roles = ...)] trên từng trang hoạt động.
// Dòng thứ 3: khi Blazor hỏi AuthenticationStateProvider thì đưa đúng đối tượng PhienDangNhap ở dòng thứ 2.
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<PhamViChiNhanh>();
builder.Services.AddScoped<PhienDangNhap>();
builder.Services.AddScoped<ThongBaoPheDuyet>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<PhienDangNhap>());

await builder.Build().RunAsync();
