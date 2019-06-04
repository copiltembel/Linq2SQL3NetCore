using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace LinqToSQL3.Example.DataAccess
{
    [DataContract(IsReference = true)]
    [Serializable]
    public abstract partial class DbEntity<TDbEntity, TIdStorage> : DbEntityBase, INotifyPropertyChanging, INotifyPropertyChanged where TDbEntity : DbEntity<TDbEntity, TIdStorage> where TIdStorage : IComparable, IComparable<TIdStorage>, IEquatable<TIdStorage>, IFormattable
    {
        #region Property Change Event Handling
        private static PropertyChangingEventArgs emptyChangingEventArgs = new PropertyChangingEventArgs(String.Empty);

        public event PropertyChangingEventHandler PropertyChanging;
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void SendPropertyChanging()
        {
            if ((this.PropertyChanging != null))
            {
                this.PropertyChanging(this, emptyChangingEventArgs);
            }
        }

        protected virtual void SendPropertyChanged(string propertyName)
        {
            if ((this.PropertyChanged != null))
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion

        public void CopyTo(DbEntity<TDbEntity, TIdStorage> otherDbEntity, bool includeDbId = true)
        {
            foreach (PropertyInfo property in otherDbEntity.GetType().GetProperties())
            {
                if (property.CanWrite)
                {
                    if (IsSimple(property.PropertyType) || (includeDbId && IsDbId(property.PropertyType)))
                    {
                        property.SetValue(otherDbEntity, property.GetValue(this, null), null);
                    }
                }
            }
        }

        private bool IsDbId(Type type)
        {
            if (IsNotNullableDbId(type))
            {
                return true;
            }
            if (type == typeof(Nullable<>))
            {
                return IsNotNullableDbId(type.GenericTypeArguments[0]);
            }
            return false;
        }

        private bool IsNotNullableDbId(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(DbId<,>);
        }

        private bool IsSimple(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // nullable type, check if the nested type is simple.
                return IsSimple(type.GenericTypeArguments[0]);
            }
            return type.IsPrimitive
              || type.IsEnum
              || type.Equals(typeof(string))
              || type.Equals(typeof(decimal));
        }
    }
}
