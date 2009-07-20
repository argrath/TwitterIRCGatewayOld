using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.TypableMap
{
    public interface ITypableMapStatusRepositoryFactory
    {
        ITypableMapStatusRepository Create(Int32 size);
    }

    public class TypableMapStatusMemoryRepositoryFactory : ITypableMapStatusRepositoryFactory
    {
        #region ITypableMapStatusRepositoryFactory メンバ

        public ITypableMapStatusRepository Create(int size)
        {
            return new TypableMapStatusMemoryRepository(size);
        }

        #endregion
    }
}
