using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// TwitterIrcGatewayの接続サーバ機能を提供します。
    /// </summary>
    public class Server : MarshalByRefObject
    {
        private static List<Server> _runningServers = new List<Server>();
        
        private TcpListener _tcpListener;
        private List<Session> _sessions;
        private Encoding _encoding = Encoding.GetEncoding("ISO-2022-JP");

        /// <summary>
        /// APIアクセスに利用するプロクシサーバの設定
        /// </summary>
        public IWebProxy Proxy = null;

        public const String ServerName = "localhost";
        public const String ServerNick = "$TwitterIrcGatewayServer$";

        /// <summary>
        /// 新たなセッションが開始されたイベント
        /// </summary>
        public event EventHandler<SessionStartedEventArgs> SessionStartedReceived;

        /// <summary>
        /// 文字エンコーディングを取得・設定します
        /// </summary>
        public Encoding Encoding
        {
            get { return _encoding; }
            set { _encoding = value; }
        }
        /// <summary>
        /// サーバが現在動作中かどうかを取得します
        /// </summary>
        public Boolean IsRunning
        {
            get { return _tcpListener != null; }
        }

        /// <summary>
        /// 現在存在しているセッション情報のコレクションを取得します。
        /// </summary>
        public IList<Session> Sessions
        {
            get { return _sessions.AsReadOnly(); }
        }

        /// <summary>
        /// 現在実行しているサーバのコレクションを取得します。
        /// </summary>
        public static IList<Server> RunningServers
        {
            get { return _runningServers.AsReadOnly(); }
        }
        
        /// <summary>
        /// 指定したIPアドレスとポートでクライアントからの接続待ち受けを開始します
        /// </summary>
        /// <param name="ipAddr">接続を待ち受けるIPアドレス</param>
        /// <param name="port">接続を待ち受けるポート</param>
        public void Start(IPAddress ipAddr, Int32 port)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException();
            }
            
            lock (_runningServers)
            {
                _runningServers.Add(this);
            }

            _sessions = new List<Session>();

            Trace.WriteLine(String.Format("Starting IRC Server: IPAddress = {0}, port = {1}", ipAddr, port));
            _tcpListener = new TcpListener(ipAddr, port);
            _tcpListener.Start();
            _tcpListener.BeginAcceptTcpClient(AcceptHandled, this);
        }
        
        /// <summary>
        /// クライアントからの接続待ち受けを停止します
        /// </summary>
        public void Stop()
        {
            lock (this)
            {
                if (_tcpListener != null)
                {
                    _tcpListener.Stop();
                    _tcpListener = null;
                }
                lock (_runningServers)
                {
                    _runningServers.Remove(this);
                }
                lock (_sessions)
                {
                    foreach (Session session in _sessions.ToArray())
                    {
                        session.Close();
                    }
                }
            }
        }

        public override string ToString()
        {
            return String.Format("Server: LocalEndPoint={0}", _tcpListener.LocalEndpoint);
        }

        #region Internal Implementation
        void AcceptHandled(IAsyncResult ar)
        {
            TcpClient tcpClient = null;

            if (_tcpListener != null)
            {
                lock (this)
                    lock (_tcpListener)
                    {
                        if (_tcpListener != null && ar.IsCompleted)
                        {
                            tcpClient = _tcpListener.EndAcceptTcpClient(ar);
                            _tcpListener.BeginAcceptTcpClient(AcceptHandled, this);
                        }
                    }
            }

            if (tcpClient != null && tcpClient.Connected)
            {
                Trace.WriteLine(String.Format("Client Connected: RemoteEndPoint={0}", tcpClient.Client.RemoteEndPoint));
                Session session = new Session(this, tcpClient);
                lock (_sessions)
                {
                    _sessions.Add(session);
                }
                session.SessionStarted += new EventHandler<SessionStartedEventArgs>(session_SessionStartedReceived);
                session.SessionEnded += new EventHandler<EventArgs>(session_SessionEnded);
                session.Start();
            }
        }

        void session_SessionEnded(object sender, EventArgs e)
        {
            lock (_sessions)
            {
                _sessions.Remove(sender as Session);
            }
        }

        void session_SessionStartedReceived(object sender, SessionStartedEventArgs e)
        {
            // 中継
            if (SessionStartedReceived != null)
            {
                SessionStartedReceived(sender, e);
            }
        }
        #endregion
    }
}
