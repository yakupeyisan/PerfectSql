using System.Data.SqlClient;

namespace PerfectSql;

public static class ConnectionStore
{
    private static SqlConnection _connection;
    private static string _connectionString;
    public static SqlConnection SqlConnection => _connection ??= new(_connectionString);
    public static SqlConnection MasterSqlConnection => new(_masterConnectionString);
    internal static object SqlConnectionLock = new object();
    internal static bool IsLoaded { get; private set; }
    internal static void RepairSqlConnection()
    {
        _connection = new(_connectionString);
    }
    public static void SetConnectionString(string connectionString)
    {
        IsLoaded = true;
        _connectionString = connectionString;
    }
    public static void CheckDatabaseConnection()
    {
        using (SqlConnection sqlConnection = ConnectionStore.MasterSqlConnection)
        {
            sqlConnection.Open();
            string query = $@"SELECT 1";
            using (SqlCommand command = new(query, sqlConnection))
            {
                command.ExecuteNonQuery();
            }
            sqlConnection.Close();
        }
    }
    private static string _masterConnectionString => string.Join(";", _connectionString.Split(";").Select(x =>
    {
        if (x.StartsWith("Database"))
        {
            x = "Database=master";
        }
        return x;
    }));
    public static string DatabaseName => _connectionString.Split(";").Where(x => x.StartsWith("Database")).First().Replace("Database=", "");
}
