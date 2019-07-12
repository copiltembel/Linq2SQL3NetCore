using Db.DataAccess.DataSet;
using System.Collections.Generic;
using System.Data.Linq;

namespace LinqToSQL3NetCore.Example.DataAccess
{
    public class DbContext : DbDataContext
    {
        public DbContext(string connection) :
            base(connection)
        {
        }

        public virtual void DeleteOnSubmit<TDbEntity>(IReadOnlyList<TDbEntity> dbEntities) where TDbEntity : DbEntityBase
        {
            this.GetTable<TDbEntity>().DeleteAllOnSubmit(dbEntities);
        }

        public virtual void InsertOnSubmit<TDbEntity>(IReadOnlyList<TDbEntity> dbEntities) where TDbEntity : DbEntityBase
        {
            this.GetTable<TDbEntity>().InsertAllOnSubmit(dbEntities);
        }

        public virtual void InsertOnSubmit<TDbEntity>(TDbEntity dbEntity) where TDbEntity : DbEntityBase
        {
            this.GetTable<TDbEntity>().InsertOnSubmit(dbEntity);
        }

        public virtual void DeleteOnSubmit<TDbEntity>(TDbEntity dbEntity) where TDbEntity : DbEntityBase
        {
            this.GetTable<TDbEntity>().DeleteOnSubmit(dbEntity);
        }

        public virtual void DeleteAllOnSubmit<TDbEntity>(IEnumerable<TDbEntity> dbEntities) where TDbEntity : DbEntityBase
        {
            this.GetTable<TDbEntity>().DeleteAllOnSubmit(dbEntities);
        }
    }
}
