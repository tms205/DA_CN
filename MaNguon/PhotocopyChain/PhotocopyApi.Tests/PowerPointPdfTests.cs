using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PhotocopyApi.Controllers;
using PhotocopyApi.DichVu;
using UglyToad.PdfPig;
using Xunit;

namespace PhotocopyApi.Tests;

[CollectionDefinition("PowerPoint COM", DisableParallelization = true)]
public class PowerPointComCollection { }

[Collection("PowerPoint COM")]
public class PowerPointPdfTests
{
    private sealed class OfficeFact : FactAttribute
    {
        public OfficeFact()
        {
            if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("PHOTOCOPY_PPT_TEST") != "1")
                Skip = "Opt-in: chuyển PowerPoint thật, không chạy cùng phiên PowerPoint người dùng.";
        }
    }

    [OfficeFact]
    public void ChuyenFileBaoVe_Thanh17Trang_VaKhongDongPhienCoSan()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        string input = Environment.GetEnvironmentVariable("PHOTOCOPY_PPT_TEST_FILE")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "BaoVe_KhoaLuan_PhotocopyChain.pptx");
        Assert.True(File.Exists(input));
        var existingProcesses = Process.GetProcessesByName("POWERPNT").Select(p => p.Id).ToArray();
        string copy = Path.Combine(Path.GetTempPath(), "photocopy-upload-test-" + Guid.NewGuid().ToString("N") + ".pptx");
        File.Copy(input, copy);
        input = copy;
        string output = Path.Combine(Path.GetTempPath(), "photocopy-ppt-fixed-" + Guid.NewGuid().ToString("N") + ".pdf");
        var controller = new FileInController(new ConfigurationBuilder().Build(), NullLogger<FileInController>.Instance);
        typeof(FileInController).GetMethod("DoiBangMicrosoftOffice", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(controller, new object[] { input, output, "PowerPoint" });
        using (var pdf = PdfDocument.Open(output)) Assert.Equal(17, pdf.NumberOfPages);

        Exception failure = null;
        var thread = new Thread(() =>
        {
            if (!OperatingSystem.IsWindows()) return;
            dynamic app = null, doc = null;
            string ownFile = Path.Combine(Path.GetTempPath(), "ppt-session-test-" + Guid.NewGuid().ToString("N") + ".pptx");
            var own = new TienTrinhOffice("PowerPoint");
            try
            {
                app = Activator.CreateInstance(Type.GetTypeFromProgID("PowerPoint.Application")!);
                doc = app.Presentations.Add(-1);
                doc.SaveAs(ownFile, 24);
                own.NhanCuaSoTheoTieuDe(Path.GetFileNameWithoutExtension(ownFile));
                var conversion = new TienTrinhOffice("PowerPoint");
                int security = (int)app.AutomationSecurity, alerts = (int)app.DisplayAlerts;
                typeof(FileInController).GetMethod("XuatPdfBangOffice", BindingFlags.NonPublic | BindingFlags.Static)!
                    .Invoke(null, new object[] { input, output + ".second.pdf", "PowerPoint", conversion });
                Assert.Equal(security, (int)app.AutomationSecurity);
                Assert.Equal(alerts, (int)app.DisplayAlerts);
                using (var pdf = PdfDocument.Open(output + ".second.pdf")) Assert.Equal(17, pdf.NumberOfPages);
                // The test-owned presentation existed before the conversion and must stay open.
                Assert.True((int)doc.Windows.Count > 0);
                Assert.True((int)app.Presentations.Count > 0);
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                if (doc != null) { try { doc.Saved = -1; doc.Close(); } finally { Marshal.FinalReleaseComObject(doc); } }
                if (app != null) { try { if (own.SoHuu) app.Quit(); } finally { Marshal.ReleaseComObject(app); } }
                File.Delete(ownFile);
            }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "Kiểm tra bảo vệ phiên Office quá thời gian.");
        Assert.Null(failure);
        foreach (int pid in existingProcesses) Assert.False(Process.GetProcessById(pid).HasExited);
        File.Delete(copy);
    }
}
