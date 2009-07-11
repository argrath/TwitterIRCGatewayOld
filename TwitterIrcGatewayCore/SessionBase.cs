using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public abstract class SessionBase : MarshalByRefObject, IIrcMessageSendable
    {
        private Server _server;

        private List<ConnectionBase> _connections = new List<ConnectionBase>();

        public Boolean IsKeepAlive { get; set; }
        public Int32 Id { get; private set; }
        public String CurrentNick { get; set; }
        public IList<ConnectionBase> Connections { get { return _connections.AsReadOnly(); } }

        public event EventHandler<ConnectionAttachEventArgs> ConnectionAttached;
        public event EventHandler<ConnectionAttachEventArgs> ConnectionDetached;

        public SessionBase(Int32 id, Server server)
        {
            Id = id;
            _server = server;
            Trace.WriteLine("Session Started");
        }

        public void Attach(ConnectionBase connection)
        {
            lock (_server.Sessions)
                lock (_connections)
                {
                    _connections.Add(connection);
                    connection.ConnectionEnded += ConnectionEnded;
                    connection.MessageReceived += MessageReceived;
                    SendGatewayServerMessage("Connection Attached: " + connection.ToString());

                    // ニックネームを合わせる
                    if (String.IsNullOrEmpty(CurrentNick))
                    {
                        CurrentNick = connection.UserInfo.Nick;
                    }
                    else
                    {
                        connection.SendServer(new NickMessage() {NewNick = CurrentNick});
                        connection.UserInfo.Nick = CurrentNick;
                    }

                    OnAttached(connection);
                    OnConnectionAttached(new ConnectionAttachEventArgs {Connection = connection});
                }
        }

        public void Detach(ConnectionBase connection)
        {
            lock (_server.Sessions)
                lock (_connections)
                {
                    connection.ConnectionEnded -= ConnectionEnded;
                    connection.MessageReceived -= MessageReceived;
                    _connections.Remove(connection);
                    SendGatewayServerMessage("Connection Detached: " + connection.ToString());

                    OnDetached(connection);
                    OnConnectionDetached(new ConnectionAttachEventArgs {Connection = connection});

                    // 接続が0になったらセッション終了
                    if (_connections.Count == 0 && !IsKeepAlive)
                    {
                        Close();
                    }
                }
        }

        #region オーバーライドして使うメソッド
        protected abstract void OnAttached(ConnectionBase connection);
        protected abstract void OnDetached(ConnectionBase connection);
        protected abstract void OnMessageReceivedFromClient(MessageReceivedEventArgs e);
        #endregion

        #region イベントハンドラ
        private void MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            // クライアントからきた PRIVMSG/NOTICE は他のクライアントにも投げる
            if (e.Message is PrivMsgMessage || e.Message is NoticeMessage)
            {
                lock (_connections)
                {
                    foreach (ConnectionBase connection in _connections)
                    {
                        // 送信元には送らない
                        if (connection != sender)
                            connection.SendServer(e.Message);
                    }
                }
            }
            
            // セッションの方で処理する
            OnMessageReceivedFromClient(e);
        }
        private void ConnectionEnded(object sender, EventArgs e)
        {
            Detach((ConnectionBase)sender);
        }
        #endregion
        
        protected virtual void OnConnectionAttached(ConnectionAttachEventArgs e)
        {
            if (ConnectionAttached != null)
                ConnectionAttached(this, e);
        }
        protected virtual void OnConnectionDetached(ConnectionAttachEventArgs e)
        {
            if (ConnectionDetached != null)
                ConnectionDetached(this, e);
        }
            
        public virtual void Close()
        {
            lock (_server.Sessions)
            {
                Trace.WriteLine("Session Closing");
                lock (_connections)
                {
                    List<ConnectionBase> connections = new List<ConnectionBase>(_connections);
                    foreach (ConnectionBase connection in connections)
                    {
                        Detach(connection);
                        connection.Close();
                    }
                }

                _server.Sessions.Remove(Id);
            }
        }

        #region IRC メッセージ処理
        /// <summary>
        /// IRCメッセージを送信します
        /// </summary>
        /// <param name="msg"></param>
        public void Send(IRCMessage msg)
        {
            lock (_connections)
                foreach (ConnectionBase connection in _connections)
                    connection.Send(msg);
        }

        /// <summary>
        /// JOIN などクライアントに返すメッセージを送信します
        /// </summary>
        /// <param name="msg"></param>
        public void SendServer(IRCMessage msg)
        {
            lock (_connections)
                foreach (ConnectionBase connection in _connections)
                    connection.SendServer(msg);
        }

        /// <summary>
        /// IRCサーバからのメッセージを送信します
        /// </summary>
        /// <param name="msg"></param>
        public void SendServerMessage(IRCMessage msg)
        {
            lock (_connections)
                foreach (ConnectionBase connection in _connections)
                    connection.SendServerMessage(msg);
        }

        /// <summary>
        /// Gatewayからのメッセージを送信します
        /// </summary>
        /// <param name="message"></param>
        public void SendGatewayServerMessage(String message)
        {
            lock (_connections)
                foreach (ConnectionBase connection in _connections)
                    connection.SendGatewayServerMessage(message);
        }

        /// <summary>
        /// サーバのエラーメッセージを送信します
        /// </summary>
        /// <param name="message"></param>
        public void SendServerErrorMessage(String message)
        {
//            if (!_config.IgnoreWatchError)
            {
                SendGatewayServerMessage("エラー: " + message);
            }
        }

        /// <summary>
        /// サーバからクライアントにエラーリプライを返します。
        /// </summary>
        /// <param name="errorNum">エラーリプライ番号</param>
        /// <param name="commandParams">リプライコマンドパラメータ</param>
        public void SendErrorReply(ErrorReply errorNum, params String[] commandParams)
        {
            SendNumericReply((NumericReply)errorNum, commandParams);
        }

        /// <summary>
        /// サーバからクライアントにニュメリックリプライを返します。
        /// </summary>
        /// <param name="numReply">リプライ番号</param>
        /// <param name="commandParams">リプライコマンドパラメータ</param>
        public void SendNumericReply(NumericReply numReply, params String[] commandParams)
        {
            foreach (ConnectionBase connection in _connections)
                connection.SendNumericReply(numReply, commandParams);
        }
        #endregion
    }
}
