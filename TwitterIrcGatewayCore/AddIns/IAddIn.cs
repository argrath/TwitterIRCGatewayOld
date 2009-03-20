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
        
        protected EventMangedProxy<Session> SessionProxy;
        protected EventMangedProxy<Server> ServerProxy;
        
        #region IAddIn メンバ

        public void Initialize(Server server, Session session)
        {
            SessionProxy = new EventMangedProxy<Session>(session);
            ServerProxy = new EventMangedProxy<Server>(server);

            Server = (Server)ServerProxy.GetTransparentProxy();
            Session = (Session)SessionProxy.GetTransparentProxy();

            Initialize();
        }

        public virtual void Initialize()
        {
        }

        public virtual void Uninitialize()
        {
            SessionProxy.RemoveAllEvents();
            ServerProxy.RemoveAllEvents();
        }

        #endregion
    }
}
