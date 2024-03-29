using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Text;
using System.Reflection;
using System.Linq;
using System.Security.Permissions;
using System.Security;

namespace System.Data.Linq
{
	using System.Data.Linq.Mapping;
	using Linq;
	using System.Diagnostics.CodeAnalysis;
    using LinqToSQL3NetCore.ChangeManagement;

    /// <summary>
    /// Implementation of ChangeDirector which calls user code if possible 
    /// and othewise falls back to creating SQL for 'INSERT', 'UPDATE' and 'DELETE'.
    /// </summary>
    internal class StandardChangeDirector : ChangeDirector
	{
		#region Enums
		internal enum UpdateType { Insert, Update, Delete };
		internal enum AutoSyncBehavior { ApplyNewAutoSync, RollbackSavedValues }
		#endregion

		#region Member Declarations
		private DataContext _context;
		[SuppressMessage("Microsoft.MSInternal", "CA908:AvoidTypesThatRequireJitCompilationInPrecompiledAssemblies", Justification = "[....]: FxCop bug Dev10:423110 -- List<KeyValuePair<object, object>> is not supposed to be flagged as a violation.")]
		private List<KeyValuePair<TrackedObject, object[]>> _syncRollbackItems;
		#endregion

		internal StandardChangeDirector(DataContext context)
		{
			this._context = context;
		}

		[SuppressMessage("Microsoft.MSInternal", "CA908:AvoidTypesThatRequireJitCompilationInPrecompiledAssemblies", Justification = "[....]: FxCop bug Dev10:423110 -- List<KeyValuePair<object, object>> is not supposed to be flagged as a violation.")]
		private List<KeyValuePair<TrackedObject, object[]>> SyncRollbackItems
		{
			get
			{
				if(_syncRollbackItems == null)
				{
					_syncRollbackItems = new List<KeyValuePair<TrackedObject, object[]>>();
				}
				return _syncRollbackItems;
			}
		}

        public static bool MetTypeCache { get; private set; }

        internal override int Insert(TrackedObject item)
		{
			if(item.Type.Table.InsertMethod != null)
			{
				try
				{
					item.Type.Table.InsertMethod.Invoke(this._context, new object[] { item.Current });
				}
				catch(TargetInvocationException tie)
				{
					if(tie.InnerException != null)
					{
						throw tie.InnerException;
					}
					throw;
				}
				return 1;
			}
			else
			{
				return DynamicInsert(item);
			}
		}

		internal override int DynamicInsert(TrackedObject item)
		{
			Expression cmd = this.GetInsertCommand(item);
			if(cmd.Type == typeof(int))
			{
				return (int)this._context.Provider.Execute(cmd).ReturnValue;
			}
			else
			{
				IEnumerable<object> facts = (IEnumerable<object>)this._context.Provider.Execute(cmd).ReturnValue;
				object[] syncResults = (object[])facts.FirstOrDefault();
				if(syncResults != null)
				{
					// [....] any auto gen or computed members
					AutoSyncMembers(syncResults, item, UpdateType.Insert, AutoSyncBehavior.ApplyNewAutoSync);
					return 1;
				}
				else
				{
					throw Error.InsertAutoSyncFailure();
				}
			}
		}

		internal override void AppendInsertText(TrackedObject item, StringBuilder appendTo)
		{
			if(item.Type.Table.InsertMethod != null)
			{
				appendTo.Append(Strings.InsertCallbackComment);
			}
			else
			{
				Expression cmd = this.GetInsertCommand(item);
				appendTo.Append(this._context.Provider.GetQueryText(cmd));
				appendTo.AppendLine();
			}
		}

		/// <summary>
		/// Update the item, returning 0 if the update fails, 1 if it succeeds.
		/// </summary>        
		internal override int Update(TrackedObject item)
		{
			if(item.Type.Table.UpdateMethod != null)
			{
				// create a copy - don't allow the override to modify our
				// internal original values
				try
				{
					item.Type.Table.UpdateMethod.Invoke(this._context, new object[] { item.Current });
				}
				catch(TargetInvocationException tie)
				{
					if(tie.InnerException != null)
					{
						throw tie.InnerException;
					}
					throw;
				}
				return 1;
			}
			else
			{
				return DynamicUpdate(item);
			}
		}

		internal override int DynamicUpdate(TrackedObject item)
		{
			Expression cmd = this.GetUpdateCommand(item);
			if(cmd.Type == typeof(int))
			{
				return (int)this._context.Provider.Execute(cmd).ReturnValue;
			}
			else
			{
				IEnumerable<object> facts = (IEnumerable<object>)this._context.Provider.Execute(cmd).ReturnValue;
				object[] syncResults = (object[])facts.FirstOrDefault();
				if(syncResults != null)
				{
					// [....] any auto gen or computed members
					AutoSyncMembers(syncResults, item, UpdateType.Update, AutoSyncBehavior.ApplyNewAutoSync);
					return 1;
				}
				else
				{
					return 0;
				}
			}
		}

		internal override void AppendUpdateText(TrackedObject item, StringBuilder appendTo)
		{
			if(item.Type.Table.UpdateMethod != null)
			{
				appendTo.Append(Strings.UpdateCallbackComment);
			}
			else
			{
				Expression cmd = this.GetUpdateCommand(item);
				appendTo.Append(this._context.Provider.GetQueryText(cmd));
				appendTo.AppendLine();
			}
		}

		internal override int Delete(TrackedObject item)
		{
			if(item.Type.Table.DeleteMethod != null)
			{
				try
				{
					item.Type.Table.DeleteMethod.Invoke(this._context, new object[] { item.Current });
				}
				catch(TargetInvocationException tie)
				{
					if(tie.InnerException != null)
					{
						throw tie.InnerException;
					}
					throw;
				}
				return 1;
			}
			else
			{
				return DynamicDelete(item);
			}
		}

		internal override int DynamicDelete(TrackedObject item)
		{
			Expression cmd = this.GetDeleteCommand(item);
			int ret = (int)this._context.Provider.Execute(cmd).ReturnValue;
			if(ret == 0)
			{
				// we don't yet know if the delete failed because the check constaint did not match
				// or item was already deleted.  Verify the item exists
				cmd = this.GetDeleteVerificationCommand(item);
				ret = ((int?)this._context.Provider.Execute(cmd).ReturnValue) ?? -1;
			}
			return ret;
		}

		internal override void AppendDeleteText(TrackedObject item, StringBuilder appendTo)
		{
			if(item.Type.Table.DeleteMethod != null)
			{
				appendTo.Append(Strings.DeleteCallbackComment);
			}
			else
			{
				Expression cmd = this.GetDeleteCommand(item);
				appendTo.Append(this._context.Provider.GetQueryText(cmd));
				appendTo.AppendLine();
			}
		}

		[SuppressMessage("Microsoft.MSInternal", "CA908:AvoidTypesThatRequireJitCompilationInPrecompiledAssemblies", Justification = "[....]: FxCop bug Dev10:423110 -- List<KeyValuePair<object, object>> is not supposed to be flagged as a violation.")]
		internal override void RollbackAutoSync()
		{
			// Rolls back any AutoSync values that may have been set already
			// Those values are no longer valid since the transaction will be rolled back on the server
			if(this._syncRollbackItems != null)
			{
				foreach(KeyValuePair<TrackedObject, object[]> rollbackItemPair in this.SyncRollbackItems)
				{
					TrackedObject rollbackItem = rollbackItemPair.Key;
					object[] rollbackValues = rollbackItemPair.Value;

					AutoSyncMembers(
						rollbackValues,
						rollbackItem,
						rollbackItem.IsNew ? UpdateType.Insert : UpdateType.Update,
						AutoSyncBehavior.RollbackSavedValues);
				}
			}
		}

		[SuppressMessage("Microsoft.MSInternal", "CA908:AvoidTypesThatRequireJitCompilationInPrecompiledAssemblies", Justification = "[....]: FxCop bug Dev10:423110 -- List<KeyValuePair<object, object>> is not supposed to be flagged as a violation.")]
		internal override void ClearAutoSyncRollback()
		{
			this._syncRollbackItems = null;
		}

		private Expression GetInsertCommand(TrackedObject item)
		{
			MetaType mt = item.Type;

			// bind to InsertFacts if there are any members to syncronize
			var membersToSync = GetAutoSyncMembers(mt, UpdateType.Insert);
			ParameterExpression p = Expression.Parameter(item.Type.Table.RowType.Type, "p");
			if(membersToSync.Count > 0)
			{
				Expression autoSync = this.CreateAutoSync(membersToSync, p);
				LambdaExpression resultSelector = Expression.Lambda(autoSync, p);
				return Expression.Call(typeof(DMLMethodPlaceholders), "Insert", new Type[] { item.Type.InheritanceRoot.Type, resultSelector.Body.Type }, Expression.Constant(item.Current), resultSelector);
			}
			else
			{
				return Expression.Call(typeof(DMLMethodPlaceholders), "Insert", new Type[] { item.Type.InheritanceRoot.Type }, Expression.Constant(item.Current));
			}
		}

		/// <summary>
		/// For the meta members specified, create an array initializer for each and bind to
		/// an output array.
		/// </summary>
		private Expression CreateAutoSync(IReadOnlyList<MetaDataMember> membersToSync, Expression source)
		{
			System.Diagnostics.Debug.Assert(membersToSync.Count > 0);
			int i = 0;
			Expression[] initializers = new Expression[membersToSync.Count];
			foreach(MetaDataMember mm in membersToSync)
			{
				initializers[i++] = Expression.Convert(this.GetMemberExpression(source, mm.Member), typeof(object));
			}
			return Expression.NewArrayInit(typeof(object), initializers);
		}

		private static IReadOnlyList<MetaDataMember> GetAutoSyncMembers(MetaType metaType, UpdateType updateType)
		{
            if (MetaTypeCache.TryGetMetaDataMembers(updateType, metaType, out var result))
            {
                return result;
            }
            var membersToSync = new List<MetaDataMember>();
			foreach(MetaDataMember metaMember in metaType.PersistentDataMembers.OrderBy(m => m.Ordinal))
			{
				// add all auto generated members for the specified update type to the auto-[....] list
				if((updateType == UpdateType.Insert && metaMember.AutoSync == AutoSync.OnInsert) ||
					(updateType == UpdateType.Update && metaMember.AutoSync == AutoSync.OnUpdate) ||
					 metaMember.AutoSync == AutoSync.Always)
				{
					membersToSync.Add(metaMember);
				}
			}
            MetaTypeCache.TrySetMetaDataMembers(updateType, metaType, membersToSync);
            return membersToSync;
		}

		/// <summary>
		/// Synchronize the specified item by copying in data from the specified results.
		/// Used to [....] members after successful insert or update, but also used to rollback to previous values if a failure
		/// occurs on other entities in the same SubmitChanges batch.
		/// </summary>
		/// <param name="autoSyncBehavior">
		/// If AutoSyncBehavior.ApplyNewAutoSync, the current value of the property is saved before the [....] occurs. This is used for normal synchronization after a successful update/insert.
		/// Otherwise, the current value is not saved. This is used for rollback operations when something in the SubmitChanges batch failed, rendering the previously-[....]'d values invalid.
		/// </param>
		[SuppressMessage("Microsoft.MSInternal", "CA908:AvoidTypesThatRequireJitCompilationInPrecompiledAssemblies", Justification = "[....]: FxCop bug Dev10:423110 -- List<KeyValuePair<object, object>> is not supposed to be flagged as a violation.")]
		private void AutoSyncMembers(object[] syncResults, TrackedObject item, UpdateType updateType, AutoSyncBehavior autoSyncBehavior)
		{
			System.Diagnostics.Debug.Assert(item != null);
			System.Diagnostics.Debug.Assert(item.IsNew || item.IsPossiblyModified, "AutoSyncMembers should only be called for new and modified objects.");
			object[] syncRollbackValues = null;
			if(syncResults != null)
			{
				int idx = 0;
				var membersToSync = GetAutoSyncMembers(item.Type, updateType);
				System.Diagnostics.Debug.Assert(syncResults.Length == membersToSync.Count);
				if(autoSyncBehavior == AutoSyncBehavior.ApplyNewAutoSync)
				{
					syncRollbackValues = new object[syncResults.Length];
				}
				foreach(MetaDataMember mm in membersToSync)
				{
					object value = syncResults[idx];
					object current = item.Current;
					MetaAccessor accessor =
						(mm.Member is PropertyInfo && ((PropertyInfo)mm.Member).CanWrite)
							? mm.MemberAccessor
							: mm.StorageAccessor;

					if(syncRollbackValues != null)
					{
						syncRollbackValues[idx] = accessor.GetBoxedValue(current);
					}
					accessor.SetBoxedValue(ref current, DBConvert.ChangeType(value, mm.Type));
					idx++;
				}
			}
			if(syncRollbackValues != null)
			{
				this.SyncRollbackItems.Add(new KeyValuePair<TrackedObject, object[]>(item, syncRollbackValues));
			}
		}

		private Expression GetUpdateCommand(TrackedObject tracked)
		{
			object database = tracked.Original;
			MetaType rowType = tracked.Type.GetInheritanceType(database.GetType());
			MetaType rowTypeRoot = rowType.InheritanceRoot;

			ParameterExpression p = Expression.Parameter(rowTypeRoot.Type, "p");
			Expression pv = p;
			if(rowType != rowTypeRoot)
			{
				pv = Expression.Convert(p, rowType.Type);
			}

			Expression check = this.GetUpdateCheck(pv, tracked);
			if(check != null)
			{
				check = Expression.Lambda(check, p);
			}

			// bind to out array if there are any members to synchronize
			var membersToSync = GetAutoSyncMembers(rowType, UpdateType.Update);
			if(membersToSync.Count > 0)
			{
				Expression autoSync = this.CreateAutoSync(membersToSync, pv);
				LambdaExpression resultSelector = Expression.Lambda(autoSync, p);
				if(check != null)
				{
					return Expression.Call(typeof(DMLMethodPlaceholders), "Update", new Type[] { rowTypeRoot.Type, resultSelector.Body.Type }, Expression.Constant(tracked.Current), check, resultSelector);
				}
				else
				{
					return Expression.Call(typeof(DMLMethodPlaceholders), "Update", new Type[] { rowTypeRoot.Type, resultSelector.Body.Type }, Expression.Constant(tracked.Current), resultSelector);
				}
			}
			else if(check != null)
			{
				return Expression.Call(typeof(DMLMethodPlaceholders), "Update", new Type[] { rowTypeRoot.Type }, Expression.Constant(tracked.Current), check);
			}
			else
			{
				return Expression.Call(typeof(DMLMethodPlaceholders), "Update", new Type[] { rowTypeRoot.Type }, Expression.Constant(tracked.Current));
			}
		}

		private Expression GetUpdateCheck(Expression serverItem, TrackedObject tracked)
		{
			MetaType mt = tracked.Type;
			if(mt.VersionMember != null)
			{
				return Expression.Equal(
					this.GetMemberExpression(serverItem, mt.VersionMember.Member),
					this.GetMemberExpression(Expression.Constant(tracked.Current), mt.VersionMember.Member)
					);
			}
			else
			{
				Expression expr = null;
				foreach(MetaDataMember mm in mt.PersistentDataMembers)
				{
					if(!mm.IsPrimaryKey)
					{
						UpdateCheck check = mm.UpdateCheck;
						if(check == UpdateCheck.Always ||
							(check == UpdateCheck.WhenChanged && tracked.HasChangedValue(mm)))
						{
							object memberValue = mm.MemberAccessor.GetBoxedValue(tracked.Original);
							Expression eq =
								Expression.Equal(
									this.GetMemberExpression(serverItem, mm.Member),
									Expression.Constant(memberValue, mm.Type)
									);
							expr = (expr != null) ? Expression.And(expr, eq) : eq;
						}
					}
				}
				return expr;
			}
		}

		private Expression GetDeleteCommand(TrackedObject tracked)
		{
			MetaType rowType = tracked.Type;
			MetaType rowTypeRoot = rowType.InheritanceRoot;
			ParameterExpression p = Expression.Parameter(rowTypeRoot.Type, "p");
			Expression pv = p;
			if(rowType != rowTypeRoot)
			{
				pv = Expression.Convert(p, rowType.Type);
			}
			object original = tracked.CreateDataCopy(tracked.Original);
			Expression check = this.GetUpdateCheck(pv, tracked);
			if(check != null)
			{
				check = Expression.Lambda(check, p);
				return Expression.Call(typeof(DMLMethodPlaceholders), "Delete", new Type[] { rowTypeRoot.Type }, Expression.Constant(original), check);
			}
			else
			{
				return Expression.Call(typeof(DMLMethodPlaceholders), "Delete", new Type[] { rowTypeRoot.Type }, Expression.Constant(original));
			}
		}

		private Expression GetDeleteVerificationCommand(TrackedObject tracked)
		{
			ITable table = this._context.GetTable(tracked.Type.InheritanceRoot.Type);
			System.Diagnostics.Debug.Assert(table != null);
			ParameterExpression p = Expression.Parameter(table.ElementType, "p");
			Expression pred = Expression.Lambda(Expression.Equal(p, Expression.Constant(tracked.Current)), p);
			Expression where = Expression.Call(typeof(Queryable), "Where", new Type[] { table.ElementType }, table.Expression, pred);
			Expression selector = Expression.Lambda(Expression.Constant(0, typeof(int?)), p);
			Expression select = Expression.Call(typeof(Queryable), "Select", new Type[] { table.ElementType, typeof(int?) }, where, selector);
			Expression singleOrDefault = Expression.Call(typeof(Queryable), "SingleOrDefault", new Type[] { typeof(int?) }, select);
			return singleOrDefault;
		}

		[SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Unknown reason.")]
		private Expression GetMemberExpression(Expression exp, MemberInfo mi)
		{
			FieldInfo fi = mi as FieldInfo;
			if(fi != null)
				return Expression.Field(exp, fi);
			PropertyInfo pi = (PropertyInfo)mi;
			return Expression.Property(exp, pi);
		}
	}
}

