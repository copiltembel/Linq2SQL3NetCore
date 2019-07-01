using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace LinqToSQL3NetCore.QueryableExtensions
{
    public static class IQueryableExtensions
    {
        private static MethodInfo s_LoadWith_TSource;
        public static MethodInfo LoadWith_TSource(Type TSource, Type TTable) =>
            (s_LoadWith_TSource ??
            (s_LoadWith_TSource = new Func<IQueryable<object>, Expression<Func<object, object>>, IQueryable<object>>(IQueryableExtensions.LoadWith).GetMethodInfo().GetGenericMethodDefinition()))
            .MakeGenericMethod(TSource, TTable);

        public static IQueryable<TSource> LoadWith<TSource, TTable>(this IQueryable<TSource> source, Expression<Func<TSource, TTable>> predicate)
        {
            return source.Provider.CreateQuery<TSource>(
                Expression.Call(
                    null,
                    LoadWith_TSource(typeof(TSource), typeof(TTable)),
                    source.Expression, 
                    Expression.Quote(predicate)
                    ));
            //            return source.Provider.CreateQuery<TSource>(Expression.Call(null, LoadWith_TSource, ;
        }
    }
}
