using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Diagnostics;

namespace System.Data.Linq
{
	using System.Data.Linq.Mapping;
	using Linq;

	/// <summary>
	/// Class which performs actions on changes of entity instances tracked by the controlling change tracker 
	/// </summary>
	internal class ChangeProcessor
	{
		#region Member Declarations
		private CommonDataServices _services;
		private DataContext _context;
		private ChangeTracker _tracker;
		private ChangeDirector _changeDirector;
		private EdgeMap _currentParentEdges;
		private EdgeMap _originalChildEdges;
		private ReferenceMap _originalChildReferences;
		#endregion

		#region Private classes

		private class EdgeMap
		{
			Dictionary<MetaAssociation, Dictionary<TrackedObject, TrackedObject>> associations;

			internal EdgeMap()
			{
				this.associations = new Dictionary<MetaAssociation, Dictionary<TrackedObject, TrackedObject>>();
			}

			internal void Add(MetaAssociation assoc, TrackedObject from, TrackedObject to)
			{
				Dictionary<TrackedObject, TrackedObject> pairs;
				if(!associations.TryGetValue(assoc, out pairs))
				{
					pairs = new Dictionary<TrackedObject, TrackedObject>();
					associations.Add(assoc, pairs);
				}
				pairs.Add(from, to);
			}

			internal TrackedObject this[MetaAssociation assoc, TrackedObject from]
			{
				get
				{
					Dictionary<TrackedObject, TrackedObject> pairs;
					if(associations.TryGetValue(assoc, out pairs))
					{
						TrackedObject to;
						if(pairs.TryGetValue(from, out to))
						{
							return to;
						}
					}
					return null;
				}
			}
			internal void Clear()
			{
				this.associations.Clear();
			}
		}

		private class ReferenceMap
		{
			Dictionary<TrackedObject, List<TrackedObject>> references;

			internal ReferenceMap()
			{
				this.references = new Dictionary<TrackedObject, List<TrackedObject>>();
			}

			internal void Add(TrackedObject from, TrackedObject to)
			{
				List<TrackedObject> refs;
				if(!references.TryGetValue(from, out refs))
				{
					refs = new List<TrackedObject>();
					references.Add(from, refs);
				}
				if(!refs.Contains(to))
					refs.Add(to);
			}

			internal IEnumerable<TrackedObject> this[TrackedObject from]
			{
				get
				{
					List<TrackedObject> refs;
					if(references.TryGetValue(from, out refs))
					{
						return refs;
					}
					return Empty;
				}
			}

			internal void Clear()
			{
				this.references.Clear();
			}

			private static TrackedObject[] Empty = new TrackedObject[] { };
		}
		#endregion


		internal ChangeProcessor(CommonDataServices services, DataContext context)
		{
			this._services = services;
			this._context = context;
			this._tracker = services.ChangeTracker;
			this._changeDirector = services.ChangeDirector;
			this._currentParentEdges = new EdgeMap();
			this._originalChildEdges = new EdgeMap();
			this._originalChildReferences = new ReferenceMap();
		}


		/// <summary>
		/// Submits the changes to the database: executes Insert, Update and Delete queries based on the changes found in the tracked entities.
		/// </summary>
		/// <param name="failureMode">The failure mode.</param>
		internal void SubmitChanges(ConflictMode failureMode)
		{
			this.TrackUntrackedObjects();
			// Must apply inferred deletions only after any untracked objects
			// are tracked
			this.ApplyInferredDeletions();
			this.BuildEdgeMaps();

			var list = this.GetOrderedList();

			ValidateAll(list);

			int numUpdatesAttempted = 0;
			ChangeConflictSession conflictSession = new ChangeConflictSession(this._context);
			List<ObjectChangeConflict> conflicts = new List<ObjectChangeConflict>();
			List<TrackedObject> deletedItems = new List<TrackedObject>();
			List<TrackedObject> insertedItems = new List<TrackedObject>();
			List<TrackedObject> syncDependentItems = new List<TrackedObject>();

			for(int i = 0; i < list.Count; i++)
			{
				try
				{
#if BULK_INSERT
                    if(list[i].IsNew)
					{
                        var bulkInsertList = new List<TrackedObject>();
                        while (i < list.Count && list[i].IsNew)
                        {
                            bulkInsertList.Add(list[i]);

                            if (list[i].SynchDependentData())
                            {
                                syncDependentItems.Add(list[i]);
                            }
                            i++;
                        }
						_changeDirector.Insert(bulkInsertList);
						// store all inserted items for post processing
						insertedItems.AddRange(bulkInsertList);
					}
#else
                    if (list[i].IsNew)
                    {
                        if (list[i].SynchDependentData())
                        {
                            syncDependentItems.Add(list[i]);
                        }
                        _changeDirector.Insert(list[i]);
                        // store all inserted items for post processing
                        insertedItems.Add(list[i]);
                    }
#endif
                    else if(list[i].IsDeleted)
					{
                        var bulkDeleteList = new List<TrackedObject>();
                        while(i<list.Count && list[i].IsDeleted)
                        {
                            bulkDeleteList.Add(list[i]);
                            i++;
                        }

                        if (bulkDeleteList.Count == 1)
                        {
						    // Delete returns 1 if the delete was successfull, 0 if the row exists
						    // but wasn't deleted due to an OC conflict, or -1 if the row was
						    // deleted by another context (no OC conflict in this case)
						    numUpdatesAttempted++;
						    int ret = _changeDirector.Delete(list[i]);
						    if(ret == 0)
						    {
							    conflicts.Add(new ObjectChangeConflict(conflictSession, list[i], false));
						    }
						    else
						    {
							    // store all deleted items for post processing
							    deletedItems.Add(list[i]);
						    }
                        }
                        else
                        {
                            // Delete returns 1 if the delete was successfull, 0 if the row exists
                            // but wasn't deleted due to an OC conflict, or -1 if the row was
                            // deleted by another context (no OC conflict in this case)
                            numUpdatesAttempted++;
                            int ret = _changeDirector.Delete(bulkDeleteList);
                            if (ret == 0)
                            {
                                throw new NotImplementedException();
                                //conflicts.Add(new ObjectChangeConflict(conflictSession, bulkDeleteList, false));
                            }
                            else
                            {
                                // store all deleted items for post processing
                                deletedItems.AddRange(bulkDeleteList);
                            }
                        }
					}
					else if(list[i].IsPossiblyModified)
					{
						if(list[i].SynchDependentData())
						{
							syncDependentItems.Add(list[i]);
						}
						if(list[i].IsModified)
						{
							CheckForInvalidChanges(list[i]);
							numUpdatesAttempted++;
							if(_changeDirector.Update(list[i]) <= 0)
							{
								conflicts.Add(new ObjectChangeConflict(conflictSession, list[i]));
							}
						}
					}
				}
				catch(ChangeConflictException)
				{
					conflicts.Add(new ObjectChangeConflict(conflictSession, list[i]));
				}
				if(conflicts.Count > 0 && failureMode == ConflictMode.FailOnFirstConflict)
				{
					break;
				}
			}

			// if we have accumulated any failed updates, throw the exception now
			if(conflicts.Count > 0)
			{
				// First we need to rollback any value that have already been auto-[....]'d, since the values are no longer valid on the server
				_changeDirector.RollbackAutoSync();
				// Also rollback any dependent items that were [....]'d, since their parent values may have been rolled back
				foreach(TrackedObject syncDependentItem in syncDependentItems)
				{
					Debug.Assert(syncDependentItem.IsNew || syncDependentItem.IsPossiblyModified, "SynchDependent data should only be rolled back for new and modified objects.");
					syncDependentItem.SynchDependentData();
				}
				this._context.ChangeConflicts.Fill(conflicts);
				throw CreateChangeConflictException(numUpdatesAttempted, conflicts.Count);
			}
			else
			{
				// No conflicts occurred, so we don't need to save the rollback values anymore
				_changeDirector.ClearAutoSyncRollback();
			}

			// Only after all updates have been sucessfully processed do we want to make
			// post processing modifications to the objects and/or cache state.
			PostProcessUpdates(insertedItems, deletedItems);
		}

		private void PostProcessUpdates(List<TrackedObject> insertedItems, List<TrackedObject> deletedItems)
		{
			// perform post delete processing
			foreach(TrackedObject deletedItem in deletedItems)
			{
				// remove deleted item from identity cache
				this._services.RemoveCachedObjectLike(deletedItem.Type, deletedItem.Original);
				ClearForeignKeyReferences(deletedItem);
			}

			// perform post insert processing
			foreach(TrackedObject insertedItem in insertedItems)
			{
				object lookup = this._services.InsertLookupCachedObject(insertedItem.Type, insertedItem.Current);
				if(lookup != insertedItem.Current)
				{
					throw new DuplicateKeyException(insertedItem.Current, Strings.DatabaseGeneratedAlreadyExistingKey);
				}
				insertedItem.InitializeDeferredLoaders();
			}
		}

		/// <summary>
		/// Clears out the foreign key values and parent object references for deleted objects on the child side of a relationship.
		/// For bi-directional relationships, also performs the following fixup:
		///   - for 1:N we remove the deleted entity from the opposite EntitySet or collection
		///   - for 1:1 we null out the back reference
		/// </summary>
		private void ClearForeignKeyReferences(TrackedObject to)
		{
			Debug.Assert(to.IsDeleted, "Foreign key reference cleanup should only happen on Deleted objects.");
			foreach(MetaAssociation assoc in to.Type.Associations)
			{
				if(assoc.IsForeignKey)
				{
					// If there is a member on the other side referring back to us (i.e. this is a bi-directional relationship),
					// we want to do a cache lookup to find the other side, then will remove ourselves from that collection.
					// This cache lookup is only possible if the other key is the primary key, since that is the only way items can be found in the cache.
					if(assoc.OtherMember != null && assoc.OtherKeyIsPrimaryKey)
					{
						Debug.Assert(assoc.OtherMember.IsAssociation, "OtherMember of the association is expected to also be an association.");
						// Search the cache for the target of the association, since
						// it might not be loaded on the object being deleted, and we
						// don't want to force a load.
						object[] keyValues = CommonDataServices.GetForeignKeyValues(assoc, to.Current);
						object cached = this._services.IdentityManager.Find(assoc.OtherType, keyValues);

						if(cached != null)
						{
							if(assoc.OtherMember.Association.IsMany)
							{
								// Note that going through the IList interface handles 
								// EntitySet as well as POCO collections that implement IList 
								// and are not FixedSize.
								System.Collections.IList collection = assoc.OtherMember.MemberAccessor.GetBoxedValue(cached) as System.Collections.IList;
								if(collection != null && !collection.IsFixedSize)
								{
									collection.Remove(to.Current);
									// Explicitly clear the foreign key values and parent object reference
									ClearForeignKeysHelper(assoc, to.Current);
								}
							}
							else
							{
								// Null out the other association.  Since this is a 1:1 association,
								// we're not concerned here with causing a deferred load, since the
								// target is already cached (since we're deleting it).
								assoc.OtherMember.MemberAccessor.SetBoxedValue(ref cached, null);
								// Explicitly clear the foreign key values and parent object reference
								ClearForeignKeysHelper(assoc, to.Current);
							}
						}
						// else the item was not found in the cache, so there is no fixup that has to be done
						// We are explicitly not calling ClearForeignKeysHelper because it breaks existing shipped behavior and we want to maintain backward compatibility
					}
					else
					{
						// This is a unidirectional relationship or we have no way to look up the other side in the cache, so just clear our own side
						ClearForeignKeysHelper(assoc, to.Current);
					}
				}
				// else this is not the 1-side (foreign key) of the relationship, so there is nothing for us to do
			}
		}

		// Ensure the the member and foreign keys are nulled so that after trackedInstance is deleted,
		// the object does not appear to be associated with the other side anymore. This prevents the deleted object
		// from referencing objects still in the cache, but also will prevent the related object from being implicitly loaded
		private static void ClearForeignKeysHelper(MetaAssociation assoc, object trackedInstance)
		{
			Debug.Assert(assoc.IsForeignKey, "Foreign key clearing should only happen on foreign key side of the association.");
			Debug.Assert(assoc.ThisMember.IsAssociation, "Expected ThisMember of an association to always be an association.");

			// If this member is one of our deferred loaders, and it does not already have a value, explicitly set the deferred source to
			// null so that when we set the association member itself to null later, it doesn't trigger an implicit load.
			// This is only necessary if the value has not already been assigned or set, because otherwise we won't implicitly load anyway when the member is accessed.
			MetaDataMember thisMember = assoc.ThisMember;

			if(thisMember.IsDeferred &&
				!(thisMember.StorageAccessor.HasAssignedValue(trackedInstance) || thisMember.StorageAccessor.HasLoadedValue(trackedInstance)))
			{
				// If this is a deferred member, set the value directly in the deferred accessor instead of going 
				// through the normal member accessor, so that we don't trigger an implicit load.                                            
				thisMember.DeferredSourceAccessor.SetBoxedValue(ref trackedInstance, null);
			}

			// Notify the object that the relationship should be considered deleted.
			// This allows the object to do its own fixup even when we can't do it automatically.
			thisMember.MemberAccessor.SetBoxedValue(ref trackedInstance, null);

			// Also set the foreign key values to null if possible
			for(int i = 0, n = assoc.ThisKey.Count; i < n; i++)
			{
				MetaDataMember thisKey = assoc.ThisKey[i];
				if(thisKey.CanBeNull)
				{
					thisKey.StorageAccessor.SetBoxedValue(ref trackedInstance, null);
				}
			}
		}

		private static void ValidateAll(IEnumerable<TrackedObject> list)
		{
			foreach(var item in list)
			{
				if(item.IsNew)
				{
					item.SynchDependentData();
					if(item.Type.HasAnyValidateMethod)
					{
						SendOnValidate(item.Type, item, ChangeAction.Insert);
					}
				}
				else if(item.IsDeleted)
				{
					if(item.Type.HasAnyValidateMethod)
					{
						SendOnValidate(item.Type, item, ChangeAction.Delete);
					}
				}
				else if(item.IsPossiblyModified)
				{
					item.SynchDependentData();
					if(item.IsModified && item.Type.HasAnyValidateMethod)
					{
						SendOnValidate(item.Type, item, ChangeAction.Update);
					}
				}
			}
		}

		private static void SendOnValidate(MetaType type, TrackedObject item, ChangeAction changeAction)
		{
			if(type != null)
			{
				SendOnValidate(type.InheritanceBase, item, changeAction);

				if(type.OnValidateMethod != null)
				{
					try
					{
						type.OnValidateMethod.Invoke(item.Current, new object[] { changeAction });
					}
					catch(TargetInvocationException tie)
					{
						if(tie.InnerException != null)
						{
							throw tie.InnerException;
						}

						throw;
					}
				}
			}
		}

		internal string GetChangeText()
		{
			this.ObserveUntrackedObjects();
			// Must apply inferred deletions only after any untracked objects
			// are tracked
			this.ApplyInferredDeletions();
			this.BuildEdgeMaps();

			// append change text only
			StringBuilder changeText = new StringBuilder();
			foreach(TrackedObject item in this.GetOrderedList())
			{
				if(item.IsNew)
				{
					item.SynchDependentData();
					_changeDirector.AppendInsertText(item, changeText);
				}
				else if(item.IsDeleted)
				{
					_changeDirector.AppendDeleteText(item, changeText);
				}
				else if(item.IsPossiblyModified)
				{
					item.SynchDependentData();
					if(item.IsModified)
					{
						_changeDirector.AppendUpdateText(item, changeText);
					}
				}
			}
			return changeText.ToString();
		}

		internal ChangeSet GetChangeSet()
		{
			List<object> newEntities = new List<object>();
			List<object> deletedEntities = new List<object>();
			List<object> changedEntities = new List<object>();

			this.ObserveUntrackedObjects();
			// Must apply inferred deletions only after any untracked objects
			// are tracked
			this.ApplyInferredDeletions();

			foreach(TrackedObject item in this._tracker.GetInterestingObjects())
			{
				if(item.IsNew)
				{
					item.SynchDependentData();
					newEntities.Add(item.Current);
				}
				else if(item.IsDeleted)
				{
					deletedEntities.Add(item.Current);
				}
				else if(item.IsPossiblyModified)
				{
					item.SynchDependentData();
					if(item.IsModified)
					{
						changedEntities.Add(item.Current);
					}
				}
			}

			return new ChangeSet(newEntities.AsReadOnly(), deletedEntities.AsReadOnly(), changedEntities.AsReadOnly());
		}

		// verify that primary key and db-generated values have not changed
		private static void CheckForInvalidChanges(TrackedObject tracked)
		{
			foreach(MetaDataMember mem in tracked.Type.PersistentDataMembers)
			{
				if(mem.IsPrimaryKey || mem.IsDbGenerated || mem.IsVersion)
				{
					if(tracked.HasChangedValue(mem))
					{
						if(mem.IsPrimaryKey)
						{
							throw Error.IdentityChangeNotAllowed(mem.Name, tracked.Type.Name);
						}
						else
						{
							throw Error.DbGeneratedChangeNotAllowed(mem.Name, tracked.Type.Name);
						}
					}
				}
			}
		}

		/// <summary>
		/// Create an ChangeConflictException with the best message
		/// </summary>       
		static private ChangeConflictException CreateChangeConflictException(int totalUpdatesAttempted, int failedUpdates)
		{
			string msg = Strings.RowNotFoundOrChanged;
			if(totalUpdatesAttempted > 1)
			{
				msg = Strings.UpdatesFailedMessage(failedUpdates, totalUpdatesAttempted);
			}
			return new ChangeConflictException(msg);
		}

		internal void TrackUntrackedObjects()
		{
			Dictionary<object, object> visited = new Dictionary<object, object>();

			// search for untracked new objects
			List<TrackedObject> items = new List<TrackedObject>(this._tracker.GetInterestingObjects());
			foreach(TrackedObject item in items)
			{
				this.TrackUntrackedObjects(item.Type, item.Current, visited);
			}
		}

		internal void ApplyInferredDeletions()
		{
			foreach(TrackedObject item in this._tracker.GetInterestingObjects())
			{
				if(item.CanInferDelete())
				{
					// based on DeleteOnNull specifications on the item's associations,
					// a deletion can be inferred for this item.  The actual state transition
					// is dependent on the current item state.
					if(item.IsNew)
					{
						item.ConvertToRemoved();
					}
					else if(item.IsPossiblyModified || item.IsModified)
					{
						item.ConvertToDeleted();
					}
				}
			}
		}

		private void TrackUntrackedObjects(MetaType type, object item, Dictionary<object, object> visited)
		{
			if(!visited.ContainsKey(item))
			{
				visited.Add(item, item);
				TrackedObject tracked = this._tracker.GetTrackedObject(item);
				if(tracked == null)
				{
					tracked = this._tracker.Track(item);
					tracked.ConvertToNew();
				}
				else if(tracked.IsDead || tracked.IsRemoved)
				{
					// ignore
					return;
				}

				// search parents (objects we are dependent on)
				foreach(RelatedItem parent in this._services.GetParents(type, item))
				{
					this.TrackUntrackedObjects(parent.Type, parent.Item, visited);
				}

				// synch up primary key
				if(tracked.IsNew)
				{
					tracked.InitializeDeferredLoaders();

					if(!tracked.IsPendingGeneration(tracked.Type.IdentityMembers))
					{
						tracked.SynchDependentData();
						object cached = this._services.InsertLookupCachedObject(tracked.Type, item);
						if(cached != item)
						{
							TrackedObject cachedTracked = this._tracker.GetTrackedObject(cached);
							Debug.Assert(cachedTracked != null);
							if(cachedTracked.IsDeleted || cachedTracked.CanInferDelete())
							{
								// adding new object with same ID as object being deleted.. turn into modified
								tracked.ConvertToPossiblyModified(cachedTracked.Original);
								// turn deleted to dead...
								cachedTracked.ConvertToDead();

								this._services.RemoveCachedObjectLike(tracked.Type, item);
								this._services.InsertLookupCachedObject(tracked.Type, item);
							}
							else if(!cachedTracked.IsDead)
							{
								throw new DuplicateKeyException(item, Strings.CantAddAlreadyExistingKey);
							}
						}
					}
					else
					{
						// we may have a generated PK, however we set the PK on this new item to 
						// match a deleted item
						object cached = this._services.GetCachedObjectLike(tracked.Type, item);
						if(cached != null)
						{
							TrackedObject cachedTracked = this._tracker.GetTrackedObject(cached);
							Debug.Assert(cachedTracked != null);
							if(cachedTracked.IsDeleted || cachedTracked.CanInferDelete())
							{
								// adding new object with same ID as object being deleted.. turn into modified
								tracked.ConvertToPossiblyModified(cachedTracked.Original);
								// turn deleted to dead...
								cachedTracked.ConvertToDead();

								this._services.RemoveCachedObjectLike(tracked.Type, item);
								this._services.InsertLookupCachedObject(tracked.Type, item);
							}
						}
					}
				}

				// search children (objects that are dependent on us)
				foreach(RelatedItem child in this._services.GetChildren(type, item))
				{
					this.TrackUntrackedObjects(child.Type, child.Item, visited);
				}
			}
		}

		internal void ObserveUntrackedObjects()
		{
			Dictionary<object, object> visited = new Dictionary<object, object>();

			List<TrackedObject> items = new List<TrackedObject>(this._tracker.GetInterestingObjects());
			foreach(TrackedObject item in items)
			{
				this.ObserveUntrackedObjects(item.Type, item.Current, visited);
			}
		}

		private void ObserveUntrackedObjects(MetaType type, object item, Dictionary<object, object> visited)
		{
			if(!visited.ContainsKey(item))
			{
				visited.Add(item, item);
				TrackedObject tracked = this._tracker.GetTrackedObject(item);
				if(tracked == null)
				{
					tracked = this._tracker.Track(item);
					tracked.ConvertToNew();
				}
				else if(tracked.IsDead || tracked.IsRemoved)
				{
					// ignore
					return;
				}

				// search parents (objects we are dependent on)
				foreach(RelatedItem parent in this._services.GetParents(type, item))
				{
					this.ObserveUntrackedObjects(parent.Type, parent.Item, visited);
				}

				// synch up primary key unless its generated.
				if(tracked.IsNew)
				{
					if(!tracked.IsPendingGeneration(tracked.Type.IdentityMembers))
					{
						tracked.SynchDependentData();
					}
				}

				// search children (objects that are dependent on us)
				foreach(RelatedItem child in this._services.GetChildren(type, item))
				{
					this.ObserveUntrackedObjects(child.Type, child.Item, visited);
				}
			}
		}

		private TrackedObject GetOtherItem(MetaAssociation assoc, object instance)
		{
			if(instance == null)
				return null;
			object other = null;
			// Don't load unloaded references
			if(assoc.ThisMember.StorageAccessor.HasAssignedValue(instance) ||
				assoc.ThisMember.StorageAccessor.HasLoadedValue(instance)
				)
			{
				other = assoc.ThisMember.MemberAccessor.GetBoxedValue(instance);
			}
			else if(assoc.OtherKeyIsPrimaryKey)
			{
				// Maybe it's in the cache, but not yet attached through reference.
				object[] foreignKeys = CommonDataServices.GetForeignKeyValues(assoc, instance);
				other = this._services.GetCachedObject(assoc.OtherType, foreignKeys);
			}
			// else the other key is not the primary key so there is no way to try to look it up
			return (other != null) ? this._tracker.GetTrackedObject(other) : null;
		}

		private bool HasAssociationChanged(MetaAssociation assoc, TrackedObject item)
		{
			if(item.Original != null && item.Current != null)
			{
				if(assoc.ThisMember.StorageAccessor.HasAssignedValue(item.Current) ||
					assoc.ThisMember.StorageAccessor.HasLoadedValue(item.Current)
					)
				{
					return this.GetOtherItem(assoc, item.Current) != this.GetOtherItem(assoc, item.Original);
				}
				else
				{
					object[] currentFKs = CommonDataServices.GetForeignKeyValues(assoc, item.Current);
					object[] originaFKs = CommonDataServices.GetForeignKeyValues(assoc, item.Original);
					for(int i = 0, n = currentFKs.Length; i < n; i++)
					{
						if(!object.Equals(currentFKs[i], originaFKs[i]))
							return true;
					}
				}
			}
			return false;
		}

		private void BuildEdgeMaps()
		{
			this._currentParentEdges.Clear();
			this._originalChildEdges.Clear();
			this._originalChildReferences.Clear();

			List<TrackedObject> list = new List<TrackedObject>(this._tracker.GetInterestingObjects());
			foreach(TrackedObject item in list)
			{
				bool isNew = item.IsNew;
				MetaType mt = item.Type;
				foreach(MetaAssociation assoc in mt.Associations)
				{
					if(assoc.IsForeignKey)
					{
						TrackedObject otherItem = this.GetOtherItem(assoc, item.Current);
						TrackedObject dbOtherItem = this.GetOtherItem(assoc, item.Original);
						bool pointsToDeleted = (otherItem != null && otherItem.IsDeleted) || (dbOtherItem != null && dbOtherItem.IsDeleted);
						bool pointsToNew = (otherItem != null && otherItem.IsNew);

						if(isNew || pointsToDeleted || pointsToNew || this.HasAssociationChanged(assoc, item))
						{
							if(otherItem != null)
							{
								this._currentParentEdges.Add(assoc, item, otherItem);
							}
							if(dbOtherItem != null)
							{
								if(assoc.IsUnique)
								{
									this._originalChildEdges.Add(assoc, dbOtherItem, item);
								}
								this._originalChildReferences.Add(dbOtherItem, item);
							}
						}
					}
				}
			}
		}

		enum VisitState
		{
			Before,
			After
		}

		private List<TrackedObject> GetOrderedList()
		{
			var objects = this._tracker.GetInterestingObjects().ToList();

			// give list an initial order (most likely correct order) to avoid deadlocks in server
			var range = Enumerable.Range(0, objects.Count).ToList();
			range.Sort((int x, int y) => Compare(objects[x], x, objects[y], y));
			var ordered = range.Select(i => objects[i]).ToList();

			// permute order if constraint dependencies requires some changes to come before others
			var visited = new Dictionary<TrackedObject, VisitState>();
			var list = new List<TrackedObject>();
			foreach(TrackedObject item in ordered)
			{
				this.BuildDependencyOrderedList(item, list, visited);
			}
			return list;
		}

		private static int Compare(TrackedObject x, int xOrdinal, TrackedObject y, int yOrdinal)
		{
			// deal with possible nulls
			if(x == y)
			{
				return 0;
			}
			if(x == null)
			{
				return -1;
			}
			else if(y == null)
			{
				return 1;
			}
			// first order by action: Inserts first, Updates, Deletes last
			int xAction = x.IsNew ? 0 : x.IsDeleted ? 2 : 1;
			int yAction = y.IsNew ? 0 : y.IsDeleted ? 2 : 1;
			if(xAction < yAction)
			{
				return -1;
			}
			else if(xAction > yAction)
			{
				return 1;
			}
			// no need to order inserts (PK's may not even exist)
			if(x.IsNew)
			{
				// keep original order
				return xOrdinal.CompareTo(yOrdinal);
			}
			// second order by type
			if(x.Type != y.Type)
			{
				return string.CompareOrdinal(x.Type.Type.FullName, y.Type.Type.FullName);
			}
			// lastly, order by PK values
			int result = 0;
			foreach(MetaDataMember mm in x.Type.IdentityMembers)
			{
				object xValue = mm.StorageAccessor.GetBoxedValue(x.Current);
				object yValue = mm.StorageAccessor.GetBoxedValue(y.Current);
				if(xValue == null)
				{
					if(yValue != null)
					{
						return -1;
					}
				}
				else
				{
					IComparable xc = xValue as IComparable;
					if(xc != null)
					{
						result = xc.CompareTo(yValue);
						if(result != 0)
						{
							return result;
						}
					}
				}
			}
			// they are the same? leave in original order
			return xOrdinal.CompareTo(yOrdinal);
		}

		private void BuildDependencyOrderedList(TrackedObject item, List<TrackedObject> list, Dictionary<TrackedObject, VisitState> visited)
		{
			VisitState state;
			if(visited.TryGetValue(item, out state))
			{
				if(state == VisitState.Before)
				{
					throw Error.CycleDetected();
				}
				return;
			}

			visited[item] = VisitState.Before;

			if(item.IsInteresting)
			{
				if(item.IsDeleted)
				{
					// if 'item' is deleted
					//    all objects that used to refer to 'item' must be ordered before item
					foreach(TrackedObject other in this._originalChildReferences[item])
					{
						if(other != item)
						{
							this.BuildDependencyOrderedList(other, list, visited);
						}
					}
				}
				else
				{
					// if 'item' is new or changed
					//   for all objects 'other' that 'item' refers to along association 'assoc'
					//      if 'other' is new then 'other' must be ordered before 'item'
					//      if 'assoc' is pure one-to-one and some other item 'prevItem' used to refer to 'other'
					//         then 'prevItem' must be ordered before 'item'
					foreach(MetaAssociation assoc in item.Type.Associations)
					{
						if(assoc.IsForeignKey)
						{
							TrackedObject other = this._currentParentEdges[assoc, item];
							if(other != null)
							{
								if(other.IsNew)
								{
									// if other is new, visit other first (since item's FK depends on it)
									if(other != item || item.Type.DBGeneratedIdentityMember != null)
									{
										this.BuildDependencyOrderedList(other, list, visited);
									}
								}
								else if((assoc.IsUnique || assoc.ThisKeyIsPrimaryKey))
								{
									TrackedObject prevItem = this._originalChildEdges[assoc, other];
									if(prevItem != null && other != item)
									{
										this.BuildDependencyOrderedList(prevItem, list, visited);
									}
								}
							}
						}
					}
				}

				list.Add(item);
			}

			visited[item] = VisitState.After;
		}

	}
}
