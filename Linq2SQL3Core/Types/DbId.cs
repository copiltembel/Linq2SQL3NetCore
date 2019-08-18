namespace System.Data.Linq
{
    public partial struct DbId<TDbEntity, TIdStorage> : IConvertible, IComparable, IComparable<DbId<TDbEntity, TIdStorage>>, IEquatable<DbId<TDbEntity, TIdStorage>>, IEquatable<TIdStorage> where TDbEntity : DbEntityBase where TIdStorage: struct, IComparable, IComparable<TIdStorage>, IEquatable<TIdStorage>, IFormattable
    {
        private TIdStorage _Id;

        public DbId(TIdStorage value) : this()
        {
            _Id = value;
        }

        public bool Equals(DbId<TDbEntity, TIdStorage> other)
        {
            return other._Id.Equals(_Id);
        }

        public override bool Equals(object obj)
        {
            return obj is DbId<TDbEntity, TIdStorage> && Equals((DbId<TDbEntity, TIdStorage>)obj);
        }

        public override int GetHashCode()
        {
            return _Id.GetHashCode();
        }

        public override string ToString()
        {
            return _Id.ToString();
        }

        public static bool operator ==(DbId<TDbEntity, TIdStorage> x, DbId<TDbEntity, TIdStorage> y) => x._Id.Equals(y._Id);

        public static bool operator !=(DbId<TDbEntity, TIdStorage> x, DbId<TDbEntity, TIdStorage> y) => !x._Id.Equals(y._Id);

        public static bool operator >(DbId<TDbEntity, TIdStorage> x, DbId<TDbEntity, TIdStorage> y) => x._Id.CompareTo(y._Id) > 0;

        public static bool operator <(DbId<TDbEntity, TIdStorage> x, DbId<TDbEntity, TIdStorage> y) => x._Id.CompareTo(y._Id) < 0;

        public static bool operator >=(DbId<TDbEntity, TIdStorage> x, DbId<TDbEntity, TIdStorage> y) => x._Id.CompareTo(y._Id) >= 0;

        public static bool operator <=(DbId<TDbEntity, TIdStorage> x, DbId<TDbEntity, TIdStorage> y) => x._Id.CompareTo(y._Id) <= 0;

        public int CompareTo(object other)
        {
            if (!(other is DbId<TDbEntity, TIdStorage>))
            {
                throw new ArgumentException($"'other' must be of type {nameof(DbId<TDbEntity, TIdStorage>)}");
            }

            if (other == null)
            {
                return 1;
            }

            var otherId = ((DbId<TDbEntity, TIdStorage>)other)._Id;
            return _Id.CompareTo(otherId);
        }

        public bool Equals(int other)
        {
            return _Id.Equals(other);
        }

        public int CompareTo(DbId<TDbEntity, TIdStorage> other)
        {
            return _Id.CompareTo(other._Id);
        }

        //public TypeCode GetTypeCode()
        //{
        //    return _Id.GetTypeCode();
        //}

        public bool ToBoolean(IFormatProvider provider)
        {
            return ((IConvertible)_Id).ToBoolean(provider);
        }

        public char ToChar(IFormatProvider provider)
        {
            return ((IConvertible)_Id).ToChar(provider);
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            return ((IConvertible)_Id).ToSByte(provider);
        }

        public byte ToByte(IFormatProvider provider)
        {
            return ((IConvertible)_Id).ToByte(provider);
        }

        public short ToInt16(IFormatProvider provider)
        {
            return ((IConvertible)_Id).ToInt16(provider);
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            return ((IConvertible)_Id).ToUInt16(provider);
        }

        public int ToInt32(IFormatProvider provider)
        {
            return ((IConvertible)_Id).ToInt32(provider);
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            return ((IConvertible)_Id).ToUInt32(provider);
        }

        public long ToInt64(IFormatProvider provider)
        {
            return ((IConvertible)_Id).ToInt64(provider);
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            return ((IConvertible)_Id).ToUInt64(provider);
        }

        public float ToSingle(IFormatProvider provider)
        {
            return ((IConvertible)_Id).ToSingle(provider);
        }

        public double ToDouble(IFormatProvider provider)
        {
            return ((IConvertible)_Id).ToDouble(provider);
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            return ((IConvertible)_Id).ToDecimal(provider);
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            return ((IConvertible)_Id).ToDateTime(provider);
        }

        public string ToString(IFormatProvider provider)
        {
            return _Id.ToString();
        }

        public object ToType(Type conversionType, IFormatProvider provider)
        {
            return _Id;
        }

        public bool Equals(TIdStorage other)
        {
            throw new NotImplementedException();
        }

        public TypeCode GetTypeCode()
        {
            return Type.GetTypeCode(typeof(TIdStorage));
        }

        public TIdStorage SimpleValue
        {
            get
            {
                return _Id;
            }
        }
    }
}
