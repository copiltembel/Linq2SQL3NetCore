using System.Data.Linq.Provider.Common;
using System.Data.Linq.Provider.Interfaces;
using System.Linq.Expressions;

namespace System.Data.Linq.Provider.Common
{
	internal class AdoCompiledQuery : ICompiledQuery
	{
		DataLoadOptions originalShape;
		Expression query;
		QueryInfo queryInfo;
		IObjectReaderFactory factory;
		ICompiledSubQuery[] subQueries;

		internal AdoCompiledQuery(IReaderProvider provider, Expression query, QueryInfo queryInfo, IObjectReaderFactory factory, ICompiledSubQuery[] subQueries)
		{
			this.originalShape = provider.Services.Context.LoadOptions;
			this.query = query;
			this.queryInfo = queryInfo;
			this.factory = factory;
			this.subQueries = subQueries;
		}

		public IExecuteResult Execute(IProvider provider, object[] arguments)
		{
			if(provider == null)
			{
				throw Error.ArgumentNull("provider");
			}

			var readerProvider = provider as IReaderProvider;
			if(readerProvider == null)
			{
				throw Error.ArgumentTypeMismatch("provider");
			}

			// verify shape is compatibile with original.
			if(!AreEquivalentShapes(this.originalShape, readerProvider.Services.Context.LoadOptions))
			{
				throw Error.CompiledQueryAgainstMultipleShapesNotSupported();
			}

			// execute query (only last query produces results)
			return provider.ExecuteAll(this.query, this.queryInfo, this.factory, arguments, subQueries);
		}

		private static bool AreEquivalentShapes(DataLoadOptions shape1, DataLoadOptions shape2)
		{
			if(shape1 == shape2)
			{
				return true;
			}
			if(shape1 == null)
			{
				return shape2.IsEmpty;
			}
			if(shape2 == null)
			{
				return shape1.IsEmpty;
			}
			if(shape1.IsEmpty && shape2.IsEmpty)
			{
				return true;
			}
			return false;
		}
	}
}