using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhotocopyPrinterSimulator;
using PhotocopyChain.CauHinh;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
string fileEnv = DocFileEnv.LayDuongDan(Environment.GetEnvironmentVariable("PHOTOCOPY_ENV_FILE"), AppContext.BaseDirectory);
builder.Configuration.AddInMemoryCollection(DocFileEnv.Doc(fileEnv));

string khoaKetNoi = builder.Configuration["PrinterConnector:ApiKey"]
    ?? throw new InvalidOperationException(
        "Thiếu PrinterConnector:ApiKey. Hãy kiểm tra file .env riêng.");

builder.Services.Configure<CauHinhSimulator>(builder.Configuration.GetSection("Simulator"));
string diaChiApi = builder.Configuration["Simulator:ApiBaseUrl"]
    ?? throw new InvalidOperationException("Thiếu Simulator:ApiBaseUrl.");
var httpMayIn = new HttpClient
{
    BaseAddress = new Uri(diaChiApi),
    Timeout = TimeSpan.FromSeconds(30)
};
httpMayIn.DefaultRequestHeaders.Add("X-Printer-Api-Key", khoaKetNoi);
builder.Services.AddSingleton(httpMayIn);
builder.Services.AddHostedService<MayInSimulator>();

await builder.Build().RunAsync();
