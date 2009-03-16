using System;
using System.Collections.Generic;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    public interface IAddIn
    {
        void Initialize(Server server, Session session);
        void Uninitialize();
    }

    public interface IConfiguration
    {
    }

    public abstract class AddInBase : MarshalByRefObject, IAddIn
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

        public virtual void Uninitialize()
        {
        }

        #endregion
    }
}
