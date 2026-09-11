using DuckDB.NET.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BigaFund.Tests;

[TestClass]
public class DuckDbSmokeTest
{
    [TestMethod]
    public void DuckDb_CanCreateMemoryDatabaseAndQuery()
    {
        using var connection = new DuckDBConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE test_funds (code VARCHAR PRIMARY KEY, name VARCHAR, nav DOUBLE);";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "INSERT INTO test_funds VALUES ('000001', '华夏成长', 1.283);";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT name, nav FROM test_funds WHERE code = '000001';";
        using var reader = cmd.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual("华夏成长", reader.GetString(0));
        Assert.AreEqual(1.283, reader.GetDouble(1), 0.0001);
    }
}
