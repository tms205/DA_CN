using Dapper;
using Microsoft.Data.SqlClient;

namespace PhotocopyApi.DichVu;

public static class KhoaGiaoDichSql
{
    public const string Sql = @"DECLARE @result INT;
        EXEC @result=sys.sp_getapplock @Resource=@Resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
        SELECT @result;";

    public static async Task Lay(SqlConnection db, SqlTransaction tx, string resource, CancellationToken ct = default)
    {
        int result = await db.ExecuteScalarAsync<int>(new CommandDefinition(Sql, new { Resource = resource }, tx, cancellationToken: ct));
        if (result < 0) throw new InvalidOperationException("Yêu cầu đang được xử lý. Giữ nguyên thông tin và thử kiểm tra lại sau.");
    }

    public static Task File(SqlConnection db, SqlTransaction tx, int id, CancellationToken ct = default) =>
        Lay(db, tx, "PhotocopyChain:FileIn:" + id, ct);
}
