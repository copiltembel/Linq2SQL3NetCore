using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace System.Data.Linq
{
    [DataContract(IsReference = true)]
    [Serializable]
    public abstract class DbEntityBase
    {

        public virtual IReadOnlyList<string> GetOnInsertValidationErrors()
        {
            return null;
        }

        public virtual IReadOnlyList<string> GetOnUpdateValidationErrors()
        {
            return null;
        }

        public virtual IReadOnlyList<string> GetOnDeleteValidationErrors()
        {
            return null;
        }
    }
}