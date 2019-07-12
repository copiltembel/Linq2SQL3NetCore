using System;
using System.Collections.Generic;
using System.Data.Linq.Mapping;
using System.Text;
using System.Data.Linq;
using System.Collections.Concurrent;
using System.Linq;

namespace LinqToSQL3NetCore.ChangeManagement
{
    public class MetaTypeCache
    {
        static Dictionary<StandardChangeDirector.UpdateType, ConcurrentDictionary<MetaType, IReadOnlyList<MetaDataMember>>> MetaDicByUpdateType = 
            new Dictionary<StandardChangeDirector.UpdateType, ConcurrentDictionary<MetaType, IReadOnlyList<MetaDataMember>>>(capacity: 2);

        static MetaTypeCache()
        {
            var insertDic = new ConcurrentDictionary<MetaType, IReadOnlyList<MetaDataMember>>();
            var updateDic = new ConcurrentDictionary<MetaType, IReadOnlyList<MetaDataMember>>();
            MetaDicByUpdateType.Add(StandardChangeDirector.UpdateType.Insert, insertDic);
            MetaDicByUpdateType.Add(StandardChangeDirector.UpdateType.Update, updateDic);
        }

        internal static bool TryGetMetaDataMembers(StandardChangeDirector.UpdateType updateType, MetaType metaType, out IReadOnlyList<MetaDataMember> result)
        {
            var metaDataMembersByMetaType = MetaDicByUpdateType[updateType];
            if (metaDataMembersByMetaType.TryGetValue(metaType, out result))
            {
                return true;
            }
            result = null;
            return false;
        }

        internal static bool TrySetMetaDataMembers(StandardChangeDirector.UpdateType updateType, MetaType metaType, IReadOnlyList<MetaDataMember> metaDataMembers)
        {
            var metaDataMembersByMetaType = MetaDicByUpdateType[updateType];
            return metaDataMembersByMetaType.TryAdd(metaType, metaDataMembers);
        }
    }
}
