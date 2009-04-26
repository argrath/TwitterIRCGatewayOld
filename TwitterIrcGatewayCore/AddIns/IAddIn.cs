using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// カスタム設定情報であることを示すインターフェースです。
    /// </summary>
    /// <remarks>
    /// 設定情報クラスはこのインターフェースを実装する必要があります。
    /// </remarks>
    public interface ICustomConfiguration : IConfiguration
    {
        ICollection<ConfigurationPropertyInfo> GetConfigurationPropertyInfo();
        void SetValue(String Name, Object value);
        Object GetValue(String Name);
    }
    /// <summary>
    /// 設定可能なパラメータを表すクラスです。
    /// </summary>
    public class ConfigurationPropertyInfo
    {
        /// <summary>
        /// 設定名
        /// </summary>
        public String Name { get; set; }
        /// <summary>
        /// 設定の説明
        /// </summary>
        public String Description { get; set; }
        /// <summary>
        /// 設定の型
        /// </summary>
        public Type Type { get; set; }
        /// <summary>
        /// 値を取得がプロパティまたはフィールド経由の場合はPropertyInfoまたはFieldInfoを指定します
        /// </summary>
        public MemberInfo MemberInfo { get; set; }
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

        #region IAddIn メンバ

        public void Initialize(Server server, Session session)
        {
            CurrentServer = server;
            CurrentSession = session;

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
        }

        #endregion
    }
}
