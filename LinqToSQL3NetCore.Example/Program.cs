using Db.DataAccess.DataSet;
using LinqToSQL3NetCore.Example.DataAccess;
using Newtonsoft.Json;
using System;
using System.Data.Linq;
using System.IO;
using System.Linq;

namespace LinqToSQL3NetCore.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            var connectionString = GetConnectionString("connectionstrings.json");

            DbContext dbContext = new DbContext(connectionString);
            RunTest(dbContext);
        }

        private static void RunTest(DbContext dbContext)
        {
            var x = new TestInsertUpdateDelete(dbContext);
            x.RunTest2();
        }

        static string GetConnectionString(string jsonFileName)
        {
            var connectionSettings = JsonConvert.DeserializeObject<ConnectionSettings>(File.ReadAllText(jsonFileName));
            var connectionStrings = connectionSettings.DbConnectionStrings.Db1;
            return connectionStrings;
        }
    }
}
