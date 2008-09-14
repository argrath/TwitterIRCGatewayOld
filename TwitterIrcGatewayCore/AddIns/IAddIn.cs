using System;
using System.Collections.Generic;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    public interface IAddIn
    {
        void Initialize(Server server, Session session);
    }

    public abstract class AddInBase : IAddIn
    {
        protected Server Server { get; private set; }
        protected Session Session { get; private set; }
        
        #region IAddIn メンバ

        public void Initialize(Server server, Session session)
        {
            Server = server;
            Session = session;

            Initialize();
        }

        public virtual void Initialize()
        {
        }
        #endregion
    }

}
