namespace Server.EntityFramework
{
    public class DatabaseOptions
    {
        public DatabaseProvider DatabaseType { get; set; }

        public string ConnectionString { get; set; }
    }
}