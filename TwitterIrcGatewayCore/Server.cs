using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public class Server
    {
        private TcpListener _tcpListener;
        private List<Session> _sessions;
        private Encoding _encoding = Encoding.GetEncoding("ISO-2022-JP");
        
        /// <summary>
        /// チェックする間隔
        /// </summary>
        public Int32 Interval = 60;

        /// <summary>
        /// ダイレクトメッセージをチェックする間隔
        /// </summary>
        public Int32 IntervalDirectMessage = 60 * 5;

        /// <summary>
        /// Repliesをチェックするかどうか
        /// </summary>
        public Boolean EnableRepliesCheck = false;
        
        /// <summary>
        /// Repliesチェックする間隔
        /// </summary>
        public Int32 IntervalReplies = 60 * 5;

        /// <summary>
        /// エラーを無視するかどうか
        /// </summary>
        public Boolean IgnoreWatchError = false;

        /// <summary>
        /// TinyURLを展開するかどうか
        /// </summary>
        public Boolean ResolveTinyUrl = true;

        /// <summary>
        /// 取りこぼし防止を利用するかどうか
        /// </summary>
        public Boolean EnableDropProtection = true;

        /// <summary>
        /// ステータスを更新したときにトピックを変更するかどうか
        /// </summary>
        public Boolean SetTopicOnStatusChanged = false;

        /// <summary>
        /// トレースを有効にするかどうか
        /// </summary>
        public Boolean EnableTrace = false;

        /// <summary>
        /// Cookie ログインでタイムラインを取得するかどうか
        /// </summary>
        public Boolean CookieLoginMode = false;

        /// <summary>
        /// Twitterのステータスが流れるチャンネル名
        /// </summary>
        public String ChannelName = "#twitter";

        /// <summary>
        /// ユーザ一覧を取得するかどうか
        /// </summary>
        public Boolean DisableUserList = false;

        /// <summary>
        /// アップデートをすべてのチャンネルに投げるかどうか
        /// </summary>
        public Boolean BroadcastUpdate = false;

        /// <summary>
        /// クライアントにメッセージを送信するときのウェイト
        /// </summary>
        public Int32 ClientMessageWait = 0;

        /// <summary>
        /// アップデートをすべてのチャンネルに投げるときNOTICEにするかどうか
        /// </summary>
        public Boolean BroadcastUpdateMessageIsNotice = false;

        /// <summary>
        /// APIアクセスに利用するプロクシサーバの設定
        /// </summary>
        public IWebProxy Proxy = null;

        public const String ServerName = "localhost";
        public const String ServerNick = "$TwitterIrcGatewayServer$";

        public event EventHandler<SessionStartedEventArgs> SessionStartedRecieved;

        void AcceptHandled(IAsyncResult ar)
        {
            if (_tcpListener != null && ar.IsCompleted)
            {
                TcpClient tcpClient = _tcpListener.EndAcceptTcpClient(ar);
                _tcpListener.BeginAcceptTcpClient(AcceptHandled, this);

                Trace.WriteLine(String.Format("Client Connected: RemoteEndPoint={0}", tcpClient.Client.RemoteEndPoint));
                Session session = new Session(this, tcpClient);
                lock (_sessions)
                {
                    _sessions.Add(session);
                }
                session.SessionStarted += new EventHandler<SessionStartedEventArgs>(session_SessionStartedRecieved);
                session.SessionEnded += new EventHandler(session_SessionEnded);
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

        void session_SessionStartedRecieved(object sender, SessionStartedEventArgs e)
        {
            // 中継
            if (SessionStartedRecieved != null)
            {
                SessionStartedRecieved(sender, e);
            }
        }

        public Encoding Encoding
        {
            get { return _encoding; }
            set { _encoding = value; }
        }
        public Boolean IsRunning
        {
            get { return _tcpListener != null; }
        }

        public void Start(IPAddress ipAddr, Int32 port)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException();
            }

            _sessions = new List<Session>();

            Trace.WriteLine(String.Format("Starting IRC Server: IPAddress = {0}, port = {1}", ipAddr, port));
            _tcpListener = new TcpListener(ipAddr, port);
            _tcpListener.Start();
            _tcpListener.BeginAcceptTcpClient(AcceptHandled, this);
        }

        public void Stop()
        {
            lock (_sessions)
            {
                foreach (Session session in _sessions)
                {
                    session.Close();
                }
            }
            if (_tcpListener != null)
            {
                _tcpListener.Stop();
                _tcpListener = null;
            }
        }
    }
}
