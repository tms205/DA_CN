using System.Reflection;
using System.Xml.Linq;
using Dapper;
using Microsoft.Data.SqlClient;
using PhotocopyApi.Controllers;
using PhotocopyShared.Dtos.DonHang;
using Xunit;

namespace PhotocopyApi.Tests;

// Chỉ SELECT và lấy execution plan; không bật PHOTOCOPY_TEST_DB, không ghi dữ liệu thật.
public sealed class TienDoTruyVanDocTests
{
    private static string Sql => (string)typeof(DonHangController).Assembly
        .GetType("PhotocopyApi.DuLieu.TruyVanSql+DonHang")!
        .GetField("DongInChiTiet", BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue();

    private static SqlConnection KetNoi()
    {
        var doc = typeof(DonHangController).Assembly.GetType("PhotocopyChain.CauHinh.DocFileEnv")!;
        var config = (Dictionary<string,string>)doc.GetMethod("Doc")!.Invoke(null,
            new object[]{Environment.GetEnvironmentVariable("PHOTOCOPY_DETAIL_READ_ENV_FILE")!});
        return new SqlConnection(config["ConnectionStrings:PhotocopyChain"]);
    }

    [SqlDocChiTiet]
    public async Task DongInNho_KhongCanHon2MbBoNhoTruyVan()
    {
        await using var db=KetNoi();
        await db.OpenAsync();
        int don=await db.ExecuteScalarAsync<int>("SELECT TOP(1) MaDonHang FROM ChiTietDonHang ORDER BY MaDonHang");
        Assert.True(don>0,"Cần ít nhất một đơn có dòng in để đo truy vấn.");
        await using var cmd=db.CreateCommand();
        cmd.CommandTimeout=10;
        cmd.CommandText="SET STATISTICS XML ON; "+Sql+"; SET STATISTICS XML OFF;";
        cmd.Parameters.AddWithValue("@MaDonHang",don);
        XDocument plan=null;
        await using(var reader=await cmd.ExecuteReaderAsync())
        {
            do
            {
                bool laPlan=reader.FieldCount==1 && reader.GetName(0).Contains("Showplan",StringComparison.OrdinalIgnoreCase);
                while(await reader.ReadAsync())
                    if(laPlan) plan=XDocument.Parse(reader.GetString(0));
            }while(await reader.NextResultAsync());
        }
        Assert.NotNull(plan);
        var grants=plan.Descendants().Where(x=>x.Name.LocalName=="MemoryGrantInfo").ToList();
        Assert.NotEmpty(grants);
        Assert.All(grants,x=>Assert.InRange((long?)x.Attribute("RequiredMemory")
            ?? (long?)x.Attribute("SerialRequiredMemory") ?? 0,0,2048));
    }

    [SqlDocChiTiet]
    public async Task DongIn_GiuThuTuTenGiaCongVaNullKhiKhongGiaCong()
    {
        await using var db=KetNoi();
        var ids=await db.QueryAsync<int>("SELECT DISTINCT TOP(20) MaDonHang FROM ChiTietDonHang ORDER BY MaDonHang");
        foreach(int don in ids)
        {
            var rows=(await db.QueryAsync<DongInChiTietDto>(Sql,new{MaDonHang=don})).ToList();
            var expected=(await db.QueryAsync<GiaCong>(@"SELECT ct.MaChiTiet,gc.TenDichVu
                FROM ChiTietDonHang ct LEFT JOIN ChiTietGiaCong g ON g.MaChiTiet=ct.MaChiTiet
                LEFT JOIN DichVu gc ON gc.MaDichVu=g.MaDichVu WHERE ct.MaDonHang=@don
                ORDER BY ct.MaChiTiet,gc.MaDichVu",new{don})).GroupBy(x=>x.MaChiTiet).ToList();
            Assert.Equal(expected.Count,rows.Count);
            foreach(var group in expected)
            {
                var names=group.Where(x=>x.TenDichVu!=null).Select(x=>x.TenDichVu).ToList();
                Assert.Equal(names.Count==0?null:string.Join(", ",names),rows.Single(x=>x.MaChiTiet==group.Key).TenGiaCong);
            }
        }
    }

    private sealed class GiaCong { public int MaChiTiet{get;set;} public string TenDichVu{get;set;} }
    private sealed class SqlDocChiTietAttribute:FactAttribute
    {
        public SqlDocChiTietAttribute()
        {
            if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PHOTOCOPY_DETAIL_READ_ENV_FILE")))
                Skip="Cần PHOTOCOPY_DETAIL_READ_ENV_FILE; chỉ đọc database và execution plan.";
        }
    }
}
