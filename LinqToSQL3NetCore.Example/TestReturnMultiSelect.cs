using LinqToSQL3NetCore.Example.DataAccess;
using System;
using System.Data;
using System.Data.SqlClient;

namespace LinqToSQL3NetCore.Example
{
    public class TestReturnMultiSelect
    {
        static string queryString = @"begin tran

DECLARE @p0 nchar(10);
DECLARE @p1 int;

SET @p0 = N'0         ';
SET @p1 = 0;

INSERT INTO [dbo].[TestTable1]([Dummy], [Dummy2])
VALUES (@p0, @p1)

DECLARE @value1 int;
SET @value1 = SCOPE_IDENTITY();

SELECT @value1;

DECLARE @p2 uniqueidentifier;
SET @p2 = 'D14F304B-F90B-4524-93EB-CF76682C593C';
DECLARE @p3 nchar(10);
SET @p3 = N'00        ';
DECLARE @p4 int;
SET @p4 = 1;

INSERT INTO [dbo].[TestTable2]([Id], [Dummy1], [Dummy2], [TestTable1Id])
VALUES (@p2, @p3, @p4, @value1)

INSERT INTO [dbo].[TestTable3]([TestTable1Id])
VALUES (@value1)

SET @value1 = SCOPE_IDENTITY();

SELECT @value1;

rollback";


        public static void Test(DbContext dbContext)
        {
            using (var connection = new SqlConnection(dbContext.Connection.ConnectionString))
            {
                using (SqlCommand command = new SqlCommand(queryString, connection))
                {
                    using (SqlDataAdapter sda = new SqlDataAdapter())
                    {
                        sda.SelectCommand = command;
                        //command.Parameters.AddWithValue("@tPatSName", "Your-Parm-Value");
                        connection.Open();
                        //SqlDataReader reader = command.ExecuteReader();
                        using (var ds = new DataSet())
                        {
                            sda.Fill(ds);
                            //Console.WriteLine(ds.)
                        }
                    }
                }
            }
        }
    }
}

