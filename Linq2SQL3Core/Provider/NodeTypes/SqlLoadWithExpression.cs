namespace System.Data.Linq.Provider.NodeTypes
{
	internal class SqlLoadWithExpression {
		private SqlExpression expression;

		internal SqlLoadWithExpression(SqlExpression expr) {
			this.Expression = expr;
		}

		internal SqlExpression Expression {
			get { return this.expression; }
			set {
				if (value == null)
					throw Error.ArgumentNull("value");
				if (this.expression != null && !this.expression.ClrType.IsAssignableFrom(value.ClrType))
					throw Error.ArgumentWrongType("value", this.expression.ClrType, value.ClrType);
				this.expression = value;
			}
		}

		private static SqlColumn UnwrapColumn(SqlExpression expr) {
			System.Diagnostics.Debug.Assert(expr != null);

			SqlUnary exprAsUnary = expr as SqlUnary;
			if (exprAsUnary != null) {
				expr = exprAsUnary.Operand;
			}

			SqlColumn exprAsColumn = expr as SqlColumn;
			if (exprAsColumn != null) {
				return exprAsColumn;
			}

			SqlColumnRef exprAsColumnRef = expr as SqlColumnRef;
			if (exprAsColumnRef != null) {
				return exprAsColumnRef.GetRootColumn();
			}
			//
			// For all other types return null to revert to default behavior for Equals()
			// and GetHashCode()
			//
			return null;
		}
	}
}