using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TypableMap;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.TypableMap
{
    public interface ITypableMapStatusRepository
    {
        void SetSize(Int32 size);
        String Add(Status status);
        Boolean TryGetValue(String typableMapId, out Status status);
    }
    
    public class TypableMapStatusMemoryRepository : ITypableMapStatusRepository
    {
        private TypableMap<Status> _typableMap;
        public TypableMapStatusMemoryRepository(Int32 size)
        {
            _typableMap = new TypableMap<Status>(size);
        }

        #region ITypableMapStatusRepository メンバ
        public void SetSize(int size)
        {
            _typableMap = new TypableMap<Status>(size);
        }

        public String Add(Status status)
        {
            return _typableMap.Add(status);
        }

        public Boolean TryGetValue(String typableMapId, out Status status)
        {
            return _typableMap.TryGetValue(typableMapId, out status);
        }
        #endregion
    }
}
