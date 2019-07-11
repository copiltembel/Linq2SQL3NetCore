using Db.DataAccess.DataSet;
using LinqToSQL3NetCore.Example.DataAccess;
using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Linq;
using System.Text;

namespace LinqToSQL3NetCore.Example
{
    public class TestMultiCommand
    {
        public static void TestInsertUpdateDelete(DbContext dbContext)
        {
            // insert an entity that will be modified afterwards
            var updateEntity = new TestTable1()
            {
                Dummy = "1",
                Dummy2 = 1
            };

            dbContext.InsertOnSubmit(updateEntity);
            dbContext.SubmitChanges();

            // insert an entity that will be deleted in the batch
            var deleteEntity = new TestTable1()
            {
                Dummy = "99",
                Dummy2 = 99
            };

            dbContext.InsertOnSubmit(deleteEntity);
            dbContext.SubmitChanges();

            // do insert update delete
            var testTable1 = new TestTable1()
            {
                Dummy = "0",
                Dummy2 = 0
            };

            var testTable2 = new TestTable2()
            {
                Id = new DbId<TestTable2, Guid>(Guid.NewGuid()),
                Dummy1 = "00",
                Dummy2 = 1,
                TestTable1 = testTable1

            };

            var testTable3 = new TestTable3()
            {
                TestTable1 = testTable1
            };

            dbContext.InsertOnSubmit(testTable1);
            updateEntity.Dummy = "2";
            updateEntity.Dummy2 = 2;

            var deleteEntity2 = dbContext.TestTable1s.Single(tt1 => tt1.Id == deleteEntity.Id);

            dbContext.DeleteOnSubmit(deleteEntity2);

            dbContext.SubmitChanges();
        }
    }
}
