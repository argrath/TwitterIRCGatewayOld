using System;
using System.Collections.Generic;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    /// <summary>
    /// アドインのインターフェース型。
    /// </summary>
    /// <remarks>
    /// アドインは<see cref="System.MarshalByRefObject" />を継承して、このインターフェースを実装する必要があります。
    /// 特別な理由がない限りは<see cref="AddInBase" />を継承してください。
    /// </remarks>
    public interface IAddIn
    {
        /// <summary>
        /// アドインが読み込まれ初期化されるためにアドインマネージャから呼び出されます。
        /// </summary>
        /// <param name="server">サーバのインスタンス</param>
        /// <param name="session">現在の接続のセッション情報</param>
        void Initialize(Server server, Session session);
        
        /// <summary>
        /// アドインが破棄される直前に呼び出されます。
        /// </summary>
        void Uninitialize();
    }

    /// <summary>
    /// 設定情報であることを示す、マーカーインターフェースです。
    /// </summary>
    /// <remarks>
    /// 設定情報クラスはこのインターフェースを実装する必要があります。
    /// </remarks>
    public interface IConfiguration
    {
    }

    /// <summary>
    /// アドインのベースとなる基本的な機能を持ったクラスです。
    /// </summary>
    public abstract class AddInBase : MarshalByRefObject, IAddIn
    {
        /// <summary>
        /// 関連づけられているサーバのインスタンスを取得します。
        /// このプロパティは古い形式です。
        /// </summary>
        [Obsolete("このプロパティは古い形式です。CurrentServer プロパティを利用してください。")]
        protected Server Server { get { return CurrentServer; } }
        /// <summary>
        /// 関連づけられているセッション情報のインスタンスを取得します。
        /// このプロパティは古い形式です。
        /// </summary>
        [Obsolete("このプロパティは古い形式です。CurrentSession プロパティを利用してください。")]
        protected Session Session { get { return CurrentSession; } }
        /// <summary>
        /// 関連づけられているサーバのインスタンスを取得します。
        /// </summary>
        protected Server CurrentServer { get; private set; }
        /// <summary>
        /// 関連づけられているセッション情報のインスタンスを取得します。
        /// </summary>
        protected Session CurrentSession { get; private set; }
        
        /// <summary>
        /// サーバインスタンスへのプロキシを取得・設定します。
        /// </summary>
        /// <remarks>
        /// このプロキシは接続されているイベントを管理して、アドインが破棄されるときにすべてのイベントを解除できるようにします。
        /// 特別な理由がない限り、<see cref="Session" />を利用してください。
        /// </remarks>
        protected EventMangedProxy<Session> SessionProxy;
        /// <summary>
        /// セッション情報インスタンスへのプロキシを取得・設定します。
        ///</summary>
        /// <remarks>
        /// このプロキシは接続されているイベントを管理して、アドインが破棄されるときにすべてのイベントを解除できるようにします。
        /// 特別な理由がない限り、<see cref="Server" />を利用してください。
        /// </remarks>
        protected EventMangedProxy<Server> ServerProxy;
        
        #region IAddIn メンバ

        public void Initialize(Server server, Session session)
        {
            SessionProxy = new EventMangedProxy<Session>(session);
            ServerProxy = new EventMangedProxy<Server>(server);

            CurrentServer = (Server)ServerProxy.GetTransparentProxy();
            CurrentSession = (Session)SessionProxy.GetTransparentProxy();

            Initialize();
        }

        /// <summary>
        /// アドインが初期化されるときに呼び出されます。このメソッドをオーバーライドして実装します。
        /// </summary>
        public virtual void Initialize()
        {
        }

        /// <summary>
        /// アドインが破棄されるときに呼び出されます。既定ではイベントをすべて解除します。
        /// </summary>
        /// <remarks>
        /// このメソッドをオーバーライドして処理を行うことができますが、必ずベースクラスのUninitializeを呼び出してください。
        /// </remarks>
        public virtual void Uninitialize()
        {
            SessionProxy.RemoveAllEvents();
            ServerProxy.RemoveAllEvents();
        }

        #endregion
    }
}
