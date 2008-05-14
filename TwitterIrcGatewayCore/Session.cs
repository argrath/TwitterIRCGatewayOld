using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Xml;

using Misuzilla.Net.Irc;
using Misuzilla.Applications.TwitterIrcGateway.Filter;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public class Session : IDisposable
    {
        private readonly static String ConfigBasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Configs");

        private Server _server;
        private TcpClient _tcpClient;
        private StreamWriter _writer;
        private String _clientHost;
        private TwitterService _twitter;
        private LinkedList<Int32> _lastStatusIdsFromGateway;
        private Timer _timer;
        private Timer _timerDirectMessage;
        private Timer _timerReplies;
        private Groups _groups;
        private Filters _filter;

        private DateTime _lastAccessTimeline = new DateTime();
        private DateTime _lastAccessReplies = new DateTime();
        private List<String> _nickNames = new List<string>();
        private Boolean _isFirstTime = true;
        private Boolean _isFirstTimeReplies = true;
        private DateTime _lastAccessDirectMessage = DateTime.Now;
        private LinkedList<Status> _statusBuffer;
        private LinkedList<Status> _repliesBuffer;
        private TraceListener _traceListeneer;

        private event EventHandler<MessageRecievedEventArgs> MessageRecieved;
        private String _username;
        private String _password;
        private String _nick;

        public event EventHandler<SessionStartedEventArgs> SessionStarted;
        public event EventHandler SessionEnded;

        public Session(Server server, TcpClient tcpClient)
        {
            MessageRecieved += new EventHandler<MessageRecievedEventArgs>(MessageRecieved_USER);
            MessageRecieved += new EventHandler<MessageRecievedEventArgs>(MessageRecieved_NICK);
            MessageRecieved += new EventHandler<MessageRecievedEventArgs>(MessageRecieved_PASS);
            MessageRecieved += new EventHandler<MessageRecievedEventArgs>(MessageRecieved_QUIT);
            MessageRecieved += new EventHandler<MessageRecievedEventArgs>(MessageRecieved_PRIVMSG);
            MessageRecieved += new EventHandler<MessageRecievedEventArgs>(MessageRecieved_WHOIS);
            MessageRecieved += new EventHandler<MessageRecievedEventArgs>(MessageRecieved_INVITE);
            MessageRecieved += new EventHandler<MessageRecievedEventArgs>(MessageRecieved_JOIN);
            MessageRecieved += new EventHandler<MessageRecievedEventArgs>(MessageRecieved_PART);
            MessageRecieved += new EventHandler<MessageRecievedEventArgs>(MessageRecieved_KICK);
            MessageRecieved += new EventHandler<MessageRecievedEventArgs>(MessageRecieved_LIST);
            MessageRecieved += new EventHandler<MessageRecievedEventArgs>(MessageRecieved_TIGGC);
            MessageRecieved += new EventHandler<MessageRecievedEventArgs>(MessageRecieved_TOPIC);
            MessageRecieved += new EventHandler<MessageRecievedEventArgs>(MessageRecieved_MODE);

            _groups = new Groups();
            _filter = new Filters();

            _server = server;
            _tcpClient = tcpClient;
            _statusBuffer = new LinkedList<Status>();
            _repliesBuffer = new LinkedList<Status>();
            _lastStatusIdsFromGateway = new LinkedList<int>();
            _timer = new Timer(new TimerCallback(OnTimerCallback), null, Timeout.Infinite, Timeout.Infinite);
            _timerDirectMessage = new Timer(new TimerCallback(OnTimerCallbackDirectMessage), null, Timeout.Infinite, Timeout.Infinite);
            _timerReplies = new Timer(new TimerCallback(OnTimerCallbackReplies), null, Timeout.Infinite, Timeout.Infinite);
        }

        ~Session()
        {
            this.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        public TcpClient TcpClient
        {
            get
            {
                return _tcpClient;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public String Nick
        {
            get
            {
                return _nick;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public String ClientHost
        {
            get
            {
                return _clientHost;
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="stateObject"></param>
        private void OnTimerCallback(Object stateObject)
        {
            RunCallback(_timer, delegate
            {
                SendPing();

                // 初回だけは先にチェックしておかないとnamesが後から来てジャマ
                if (_isFirstTime)
                {
                    CheckFriends();
                }

                // Friendsをチェックするのは成功して、チェックが必要となったとき
                Boolean friendsCheckRequired = false;
                if (CheckNewTimeLine(out friendsCheckRequired) && friendsCheckRequired && !_server.DisableUserList)
                {
                    CheckFriends();
                }
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stateObject"></param>
        private void OnTimerCallbackDirectMessage(Object stateObject)
        {
            RunCallback(_timerDirectMessage, delegate
            {
                CheckDirectMessage();
            });
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="stateObject"></param>
        private void OnTimerCallbackReplies(Object stateObject)
        {
            RunCallback(_timerReplies, delegate
            {
                CheckNewReplies();
            });
        }

        /// <summary>
        /// 
        /// </summary>
        private delegate void CallbackProcedure();

        /// <summary>
        /// タイマーコールバックの処理を実行します。
        /// </summary>
        /// <param name="timer"></param>
        /// <param name="callbackProcedure"></param>
        private void RunCallback(Timer timer, CallbackProcedure callbackProcedure)
        {
            // あまりに処理が遅れると二重になる可能性がある
            if (Monitor.TryEnter(timer))
            {
                try
                {
                    callbackProcedure();
                }
                finally
                {
                    Monitor.Exit(timer);
                }
            }
        }

        /// <summary>
        /// セッションを開始します。
        /// </summary>
        public void Start()
        {
            CheckDisposed();
            try
            {
                using (NetworkStream stream = _tcpClient.GetStream())
                using (StreamReader sr = new StreamReader(stream, _server.Encoding))
                using (StreamWriter sw = new StreamWriter(stream, _server.Encoding))
                {
                    _writer = sw;

                    String line;
                    while (_tcpClient.Connected && (line = sr.ReadLine()) != null)
                    {
                        try
                        {
                            IRCMessage msg = IRCMessage.CreateMessage(line);
                            OnMessageRecieved(msg);
                        }
                        catch (IRCException)
                        {}
                    }
                }
            }
            catch (IOException ie)
            {}
            catch (NullReferenceException ne)
            {}
            finally
            {
                OnSessionEnded();
                this.Close();
            }
        }

        #region イベント実行メソッド

        protected virtual void OnMessageRecieved(IRCMessage msg)
        {
            if (MessageRecieved != null)
            {
                MessageRecieved(this, new MessageRecievedEventArgs(msg, _writer, _tcpClient));
            }
        }

        protected virtual void OnSessionStarted(String username)
        {
            LoadGroups();
            LoadFilters();

            //
            if (SessionStarted != null)
            {
                SessionStarted(this, new SessionStartedEventArgs(username));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void LoadFilters()
        {
            // filters 読み取り
            String path = Path.Combine(ConfigBasePath, Path.Combine(_nick, "Filters.xml"));
            if (File.Exists(path))
            {
                Trace.WriteLine(String.Format("Load Filters: {0}", path));
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open))
                    {
                        try
                        {
                            Filters filters = Filters.Serializer.Deserialize(fs) as Filters;
                            if (filters != null)
                            {
                                _filter = filters;
                                foreach (FilterItem item in _filter.Items)
                                {
                                    Trace.WriteLine(String.Format(" - Filter:{0}", item.ToString()));
                                }
                            }
                        }
                        catch (XmlException xe) { Trace.WriteLine(xe.Message); }
                        catch (InvalidOperationException ioe) { Trace.WriteLine(ioe.Message); }
                    }
                }
                catch (IOException ie)
                {
                    SendTwitterGatewayServerMessage("エラー: " + ie.Message);
                    Trace.WriteLine(ie.Message);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void LoadGroups()
        {
           // group 読み取り
            String path = Path.Combine(ConfigBasePath, Path.Combine(_username, "Groups.xml"));
            if (File.Exists(path))
            {
                Trace.WriteLine(String.Format("Load Group: {0}", path));
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open))
                    {
                        try
                        {
                            Groups groups = Groups.Deserialize(fs);
                            _groups = groups;

                            // 下位互換性FIX: グループに自分自身のNICKは存在しないようにします
                            foreach (Group g in groups.Values)
                            {
                                g.Members.Remove(_nick);
                            }
                        }
                        catch (XmlException xe) { Trace.WriteLine(xe.Message); }
                        catch (InvalidOperationException ioe) { Trace.WriteLine(ioe.Message); }
                    }
                }
                catch (IOException ie)
                {
                    SendTwitterGatewayServerMessage("エラー: " + ie.Message);
                    Trace.WriteLine(ie.Message);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        private void SaveGroups()
        {
            // group 読み取り
            lock (_groups)
            {
                String dir = Path.Combine(ConfigBasePath, _username);
                String path = Path.Combine(dir, "Groups.xml");
                Trace.WriteLine(String.Format("Save Group: {0}", path));
                try
                {
                    Directory.CreateDirectory(dir);
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        try
                        {
                            _groups.Serialize(fs);
                        }
                        catch (XmlException xe) { Trace.WriteLine(xe.Message); }
                        catch (InvalidOperationException ioe) { Trace.WriteLine(ioe.Message); }
                    }
                }
                catch (IOException ie)
                {
                    SendTwitterGatewayServerMessage("エラー: " + ie.Message);
                    Trace.WriteLine(ie.Message);
                }
            }
        }

        protected virtual void OnSessionEnded()
        {
            if (SessionEnded != null)
            {
                SessionEnded(this, EventArgs.Empty);
            }
        }

        #endregion

        private Group GetGroupByChannelName(String channelName)
        {
            // グループを取得/作成
            Group group;
            if (!_groups.TryGetValue(channelName, out group))
            {
                group = new Group(channelName);
                _groups.Add(channelName, group);
            }
            return group;
        }

        #region メッセージ処理イベント
        private void MessageRecieved_JOIN(object sender, MessageRecievedEventArgs e)
        {
            Trace.WriteLine(e.Message.ToString());
            if (!(e.Message is JoinMessage)) return;
            if (String.IsNullOrEmpty(e.Message.CommandParams[0]))
            {
                SendErrorReply(ErrorReply.ERR_NEEDMOREPARAMS, "Not enough parameters");
                return;
            }

            JoinMessage joinMsg = e.Message as JoinMessage;
            Trace.WriteLine(String.Format("Join: {0} -> {1}", joinMsg.Sender, joinMsg.Channel));
            String[] channelNames = joinMsg.Channel.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (String channelName in channelNames)
            {
                if (!channelName.StartsWith("#") || channelName.Length < 3 || String.Compare(channelName, _server.ChannelName, true) == 0)
                {
                    Trace.WriteLine(String.Format("No nick/such channel: {0}", channelName));
                    SendErrorReply(ErrorReply.ERR_NOSUCHCHANNEL, "No such nick/channel");
                    continue;
                }

                // グループを取得/作成
                Group group = GetGroupByChannelName(channelName);
                if (!group.IsJoined)
                {
                    joinMsg = new JoinMessage(channelName, "");
                    SendServer(joinMsg);

                    SendNumericReply(NumericReply.RPL_NAMREPLY, "=", channelName, String.Format("@{0} ", _nick) + String.Join(" ", group.Members.ToArray()));
                    SendNumericReply(NumericReply.RPL_ENDOFNAMES, channelName, "End of NAMES list");
                    group.IsJoined = true;

                    // mode
                    foreach (ChannelMode mode in group.ChannelModes)
                    {
                        Send(new ModeMessage(channelName, mode.ToString()));
                    }
                    
                    // Set topic of client, if topic was set
                    Send(new TopicMessage(channelName, group.Topic));
                }
            }
       }

        private void MessageRecieved_PART(object sender, MessageRecievedEventArgs e)
        {
            if (!(e.Message is PartMessage)) return;
            if (String.IsNullOrEmpty(e.Message.CommandParams[0]))
            {
                SendErrorReply(ErrorReply.ERR_NEEDMOREPARAMS, "Not enough parameters");
                return;
            }

            PartMessage partMsg = e.Message as PartMessage;
            Trace.WriteLine(String.Format("Part: {0} -> {1}", partMsg.Sender, partMsg.Channel));
            String[] channelNames = partMsg.Channel.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (String channelName in channelNames)
            {
                if (!channelName.StartsWith("#") || channelName.Length < 3 || String.Compare(channelName, _server.ChannelName, true) == 0)
                {
                    SendErrorReply(ErrorReply.ERR_NOSUCHCHANNEL, "No such nick/channel");
                    continue;
                }

                // グループを取得/作成
                Group group;
                if (_groups.TryGetValue(channelName, out group))
                {
                    group.IsJoined = false;
                }
                else
                {
                    SendErrorReply(ErrorReply.ERR_NOTONCHANNEL, "You're not on that channel");
                    continue;
                }
                partMsg = new PartMessage(channelName, "");
                SendServer(partMsg);

                // もう捨てていい?
                if (group.Members.Count == 0)
                {
                    _groups.Remove(group.Name);
                    SendTwitterGatewayServerMessage("グループ \""+group.Name+"\" を削除しました。");
                }
            }
        }
        private void MessageRecieved_KICK(object sender, MessageRecievedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "KICK", true) != 0) return;

            String[] channels = e.Message.CommandParams[0].Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            String[] kickTargets = e.Message.CommandParams[1].Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (channels.Length == 0 || (channels.Length != 1 && channels.Length != kickTargets.Length))
            {
                SendErrorReply(ErrorReply.ERR_NEEDMOREPARAMS, "Not enough parameters");
                return;
            }

            if (channels.Length == 1)
            {
                // 一チャンネルから複数けりだす
                Group group;
                if (!_groups.TryGetValue(channels[0], out group))
                {
                    SendErrorReply(ErrorReply.ERR_NOTONCHANNEL, "You're not on that channel");
                    return;
                }
                foreach (String kickTarget in kickTargets)
                {
                    if (group.Exists(kickTarget))
                    {
                        group.Remove(kickTarget);

                        OtherMessage kickMsg = new OtherMessage("KICK");
                        kickMsg.Sender = e.Message.Sender;
                        kickMsg.CommandParams[0] = channels[0];
                        kickMsg.CommandParams[1] = kickTarget;
                        kickMsg.CommandParams[2] = e.Message.CommandParams[2];
                        Send(kickMsg);
                    }
                    else
                    {
                        SendErrorReply(ErrorReply.ERR_NOSUCHNICK, "No such nick/channel");
                        return;
                    }
                }
            }
            else
            {
                // 複数チャンネルからそれぞれ
                for (Int32 i = 0; i < channels.Length; i++)
                {
                    String channelName = channels[i];
                    Group group;
                    if (!_groups.TryGetValue(channelName, out group))
                    {
                        SendErrorReply(ErrorReply.ERR_NOTONCHANNEL, "You're not on that channel");
                        return;
                    }
                    if (group.Exists(kickTargets[i]))
                    {
                        group.Remove(kickTargets[i]);
                        
                        OtherMessage kickMsg = new OtherMessage("KICK");
                        kickMsg.Sender = e.Message.Sender;
                        kickMsg.CommandParams[0] = group.Name;
                        kickMsg.CommandParams[1] = kickTargets[i];
                        kickMsg.CommandParams[2] = e.Message.CommandParams[2];
                        Send(kickMsg);
                    }
                    else
                    {
                        SendErrorReply(ErrorReply.ERR_NOSUCHNICK, "No such nick/channel");
                        return;
                    }
                }
            }

            SaveGroups();
        }
        private void MessageRecieved_LIST(object sender, MessageRecievedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "LIST", true) != 0) return;
            foreach (Group group in _groups.Values)
            {
                SendNumericReply(NumericReply.RPL_LIST, group.Name, group.Members.Count.ToString(), "");
            }
            SendNumericReply(NumericReply.RPL_LISTEND, "End of LIST");
        }
        private void MessageRecieved_INVITE(object sender, MessageRecievedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "INVITE", true) != 0) return;
            if (String.IsNullOrEmpty(e.Message.CommandParams[0]))
            {
                SendErrorReply(ErrorReply.ERR_NONICKNAMEGIVEN, "No nickname given");
                return;
            }

            String userName = e.Message.CommandParams[0];
            String channelName = e.Message.CommandParams[1];
            Trace.WriteLine(String.Format("Invite: {0} -> {1}", userName, channelName));
            if (!channelName.StartsWith("#") || channelName.Length < 3 || String.Compare(channelName, _server.ChannelName, true) == 0)
            {
                SendErrorReply(ErrorReply.ERR_NOSUCHCHANNEL, "No such nick/channel");
                return;
            }

            // グループを取得、ユーザ追加
            Group group = GetGroupByChannelName(channelName);
            if (!group.Exists(userName))
            {
                group.Add(userName);
            }
            if (group.IsJoined)
            {
                JoinMessage joinMsg = new JoinMessage(channelName, "");
                joinMsg.SenderHost = "twitter@" + Server.ServerName;
                joinMsg.SenderNick = userName;
                Send(joinMsg);
            }

            SaveGroups();
        }

        private void MessageRecieved_USER(object sender, MessageRecievedEventArgs e)
        {
            if (!(e.Message is UserMessage)) return;

            if (String.IsNullOrEmpty(_nick))
            {
                SendErrorReply(ErrorReply.ERR_NONICKNAMEGIVEN, "No nickname given");
                return;
            }
            else if (String.IsNullOrEmpty(_password))
            {
                SendErrorReply(ErrorReply.ERR_PASSWDMISMATCH, "Password Incorrect");
                return;
            }

            //_username = e.Message.CommandParams[0]; // usernameがtwitterのIDとなる
            _username = e.Message.CommandParams[3];
            _twitter = new TwitterService(_username, _password);
            _twitter.CookieLoginMode = _server.CookieLoginMode;
            if (_server.Proxy != null)
                _twitter.Proxy = _server.Proxy;

            Type t = typeof(Server);
            _clientHost = String.Format("{0}!{1}@{2}", _nick, e.Message.CommandParams[0], ((IPEndPoint)(e.Client.Client.RemoteEndPoint)).Address);

            SendNumericReply(NumericReply.RPL_WELCOME
                , String.Format("Welcome to the Internet Relay Network {0}", _clientHost));
            SendNumericReply(NumericReply.RPL_YOURHOST
                , String.Format("Your host is {0}, running version {1}", t.FullName, t.Assembly.GetName().Version));
            SendNumericReply(NumericReply.RPL_CREATED
                , String.Format("This server was created {0}", DateTime.Now));
            SendNumericReply(NumericReply.RPL_MYINFO
                , String.Format("{0} {1}-{2} {3} {4}", Environment.MachineName, t.FullName, t.Assembly.GetName().Version, "", ""));

            JoinMessage joinMsg = new JoinMessage(_server.ChannelName, "");
            PrivMsgMessage autoMsg = new PrivMsgMessage();
            autoMsg.SenderNick = Server.ServerNick;
            autoMsg.SenderHost = "twitter@" + Server.ServerName;
            autoMsg.Receiver = _server.ChannelName;
            autoMsg.Content = "Twitter IRC Gateway Server Connected.";

            if (_server.EnableTrace)
            {
                _traceListeneer = new IrcTraceListener(this);
                Trace.Listeners.Add(_traceListeneer);
            }

            SendServer(joinMsg);
            Send(autoMsg);

            OnSessionStarted(_username);
            Trace.WriteLine(String.Format("SessionStarted: UserName={0}; Nickname={1}", _username, _nick));

            _timer.Change(0, _server.Interval * 1000);
            _timerDirectMessage.Change(0, _server.IntervalDirectMessage * 1000);
            if (_server.EnableRepliesCheck)
            {
                _timerReplies.Change(0, _server.IntervalReplies * 1000);
            }
        }

        void MessageRecieved_NICK(object sender, MessageRecievedEventArgs e)
        {
            if (!(e.Message is NickMessage)) return;

            _nick = ((NickMessage)(e.Message)).NewNick;
        }

        void MessageRecieved_PASS(object sender, MessageRecievedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "PASS", true) != 0) return;

            if (e.Message.CommandParam.Length != 0)
            {
                _password = e.Message.CommandParam.Substring(1);
            }
        }

        void MessageRecieved_QUIT(object sender, MessageRecievedEventArgs e)
        {
            if (!(e.Message is QuitMessage)) return;

            try
            {
                e.Client.Close();
            }
            catch { }
        }

        void MessageRecieved_PRIVMSG(object sender, MessageRecievedEventArgs e)
        {
            PrivMsgMessage message = e.Message as PrivMsgMessage;
            if (message == null) return;

            Boolean isRetry = false;
            Retry:
            try
            {
                // チャンネル宛は自分のメッセージを書き換え
                if ((String.Compare(message.Receiver, _server.ChannelName, true) == 0) || message.Receiver.StartsWith("#"))
                {
                    try
                    {
                        Status status = _twitter.UpdateStatus(message.Content);
                        if (status != null)
                        {
                            Trace.WriteLineIf(status != null, String.Format("Status Update: {0} (ID:{1}, CreatedAt:{2})", status.Text, status.Id.ToString(), status.CreatedAt.ToString()));

                            _lastStatusIdsFromGateway.AddLast(status.Id);
                            if (_lastStatusIdsFromGateway.Count > 100)
                            {
                                _lastStatusIdsFromGateway.RemoveFirst();
                            }
                        }
                    }
                    catch (TwitterServiceException tse)
                    {
                        SendTwitterGatewayServerMessage("エラー: メッセージは完了しましたが、レスポンスを正しく受信できませんでした。(" + tse.Message + ")");
                    }

                    // topic にする
                    if (_server.SetTopicOnStatusChanged)
                    {
                        TopicMessage topicMsg = new TopicMessage(_server.ChannelName, message.Content);
                        topicMsg.Sender = _clientHost;
                        Send(topicMsg);
                    }

                    // 他のチャンネルにも投げる
                    if (_server.BroadcastUpdate)
                    {
                        // #Twitter
                        if (String.Compare(message.Receiver, _server.ChannelName, true) != 0)
                        {
                            // XXX: 例によってIRCライブラリのバージョンアップでどうにかしたい
                            if (_server.BroadcastUpdateMessageIsNotice)
                            {
                                Send(new NoticeMessage()
                                {
                                    Sender = _clientHost,
                                    Receiver = _server.ChannelName,
                                    Content = message.Content
                                });
                            }
                            else
                            {
                                Send(new PrivMsgMessage()
                                {
                                    Sender = _clientHost,
                                    Receiver = _server.ChannelName,
                                    Content = message.Content
                                });
                            }
                        }
                        
                        // group
                        foreach (Group group in _groups.Values)
                        {
                            if (group.IsJoined && !group.IgnoreEchoBack && String.Compare(message.Receiver, group.Name, true) != 0)
                            {
                                if (_server.BroadcastUpdateMessageIsNotice)
                                {
                                    Send(new NoticeMessage()
                                    {
                                        Sender = _clientHost,
                                        Receiver = group.Name,
                                        Content = message.Content
                                    });
                                }
                                else
                                {
                                    Send(new PrivMsgMessage()
                                    {
                                        Sender = _clientHost,
                                        Receiver = group.Name,
                                        Content = message.Content
                                    });
                                }
                            }
                        }
                    }
                }
                else
                {
                    // 人に対する場合はDirect Message
                    _twitter.SendDirectMessage(message.Receiver, message.Content);
                }
                if (isRetry)
                {
                    NoticeMessage noticeMsg = new NoticeMessage();
                    noticeMsg.SenderNick = Server.ServerNick;
                    noticeMsg.SenderHost = Server.ServerName;
                    noticeMsg.Receiver = message.Receiver;
                    noticeMsg.Content = "メッセージ送信のリトライに成功しました。";
                    Send(noticeMsg);
                }
            }
            catch (WebException ex)
            {
                NoticeMessage noticeMsg = new NoticeMessage();
                noticeMsg.SenderNick = Server.ServerNick;
                noticeMsg.SenderHost = Server.ServerName;
                noticeMsg.Receiver = message.Receiver;
                noticeMsg.Content = String.Format("メッセージ送信に失敗しました({0})" + (!isRetry ? "/リトライします。" : ""), ex.Message.Replace("\n", " "));
                Send(noticeMsg);

                // 一回だけリトライするよ
                if (!isRetry)
                {
                    isRetry = true;
                    goto Retry;
                }
            }
        }

        void MessageRecieved_WHOIS(object sender, MessageRecievedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "WHOIS", true) != 0) return;

            // nick check
            if (String.IsNullOrEmpty(e.Message.CommandParams[0]))
            {
                SendErrorReply(ErrorReply.ERR_NONICKNAMEGIVEN, "No nickname given");
                return;
            }

            User user = null;
            try
            {
                user = _twitter.GetUser(e.Message.CommandParams[0]);
                if (user == null)
                {
                    SendErrorReply(ErrorReply.ERR_NOSUCHNICK, "No such nick/channel");
                }
            }
            catch (WebException we)
            {
                SendTwitterGatewayServerMessage("エラー: " + we.Message);
            }
            catch (TwitterServiceException tse)
            {
                SendTwitterGatewayServerMessage("エラー: " + tse.Message);
            }

            if (user == null)
                return;

            // ステータスをWHOIS replyとして返す
            SendNumericReply(NumericReply.RPL_WHOISUSER, user.ScreenName, user.Id.ToString(), "localhost", "*", user.Name + " - " + user.Description);
            SendNumericReply(NumericReply.RPL_WHOISSERVER, user.ScreenName, "WebSite", user.Url);
            if (user.Status != null)
            {
                SendNumericReply(NumericReply.RPL_AWAY, user.ScreenName, user.Status.Text.Replace('\n', ' '));
                SendNumericReply(NumericReply.RPL_WHOISIDLE
                    , user.ScreenName
                    , ((TimeSpan)(DateTime.Now - user.Status.CreatedAt)).TotalSeconds.ToString()
                    , "seconds idle");
            }
            SendNumericReply(NumericReply.RPL_ENDOFWHOIS, "End of /WHOIS list");
        }

        void MessageRecieved_TIGGC(object sender, MessageRecievedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "TIGGC", true) != 0) return;
            Int64 memUsage = GC.GetTotalMemory(false);
            GC.Collect();
            SendTwitterGatewayServerMessage(String.Format("Garbage Collect: {0:###,##0} bytes -> {1:###,##0} bytes", memUsage, GC.GetTotalMemory(false)));
        }

        void MessageRecieved_TOPIC(object sender, MessageRecievedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "TOPIC", true) != 0) return;
            TopicMessage topicMsg = e.Message as TopicMessage;

            // client -> server (TOPIC #Channel :Topic Msg) && channel name != server primary channel(ex.#Twitter)
            if (!String.IsNullOrEmpty(topicMsg.Topic) && (String.Compare(topicMsg.Channel, _server.ChannelName, true) != 0))
            {
                // Set channel topic
                Group group = GetGroupByChannelName(topicMsg.Channel);
                group.Topic = topicMsg.Topic;
                SaveGroups();
                
                // server -> client (set client topic)
                Send(new TopicMessage(topicMsg.Channel, topicMsg.Topic){
                    SenderNick = _nick
                });
            }
        }
        
        void MessageRecieved_MODE(object sender, MessageRecievedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "MODE", true) != 0) return;
            ModeMessage modeMsg = e.Message as ModeMessage;
            // チャンネルターゲットのみ
            if (modeMsg.CommandParams[0].StartsWith("#"))
            {
                String channel = modeMsg.CommandParams[0];
                String modeArgs = modeMsg.CommandParams[1];
                Group group;
                if (!_groups.TryGetValue(channel, out group))
                {
                    SendErrorReply(ErrorReply.ERR_NOSUCHCHANNEL);
                }
                
                foreach (ChannelMode mode in ChannelMode.Parse(modeArgs))
                {
                    foreach (ChannelMode mode2 in new List<ChannelMode>(group.ChannelModes))
                    {
                        if (mode2.Mode == mode.Mode && mode2.Parameter == mode.Parameter)
                        {
                            if (mode.IsRemove)
                            {
                                // すでにあって削除
                                group.ChannelModes.Remove(mode2);
                            }
                            else
                            {
                                // すでにある
                                goto NEXT;
                            }
                        }
                    }
                    
                    if (!mode.IsRemove)
                    {
                        group.ChannelModes.Add(mode);
                    }
                    SendServer(new ModeMessage(channel, mode.ToString()));
                    SaveGroups();
                NEXT:
                    ;
                }
            }
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg"></param>
        public void Send(IRCMessage msg)
        {
            CheckDisposed();
            lock (_writer)
            {
                if (_tcpClient != null && _tcpClient.Connected && _writer.BaseStream.CanWrite)
                {
                    _writer.WriteLine(msg.RawMessage);
                    _writer.Flush();
                }
            }
        }

        /// <summary>
        /// JOIN とかクライアントに返すやつ
        /// </summary>
        /// <param name="msg"></param>
        public void SendServer(IRCMessage msg)
        {
            msg.Sender = _clientHost;
            Send(msg);
        }

        /// <summary>
        /// IRCサーバメッセージ系
        /// </summary>
        /// <param name="msg"></param>
        public void SendServerMessage(IRCMessage msg)
        {
            msg.Prefix = Server.ServerName;
            Send(msg);
        }

        /// <summary>
        /// サーバメッセージ系
        /// </summary>
        /// <param name="msg"></param>
        public void SendTwitterGatewayServerMessage(String message)
        {
            NoticeMessage noticeMsg = new NoticeMessage();
            noticeMsg.Sender = "";
            noticeMsg.Receiver = _nick;
            noticeMsg.Content = message.Replace("\n", " ");
            Send(noticeMsg);
        }

        /// <summary>
        /// サーバエラーメッセージ系
        /// </summary>
        /// <param name="message"></param>
        public void SendServerErrorMessage(String message)
        {
            if (!_server.IgnoreWatchError)
            {
                SendTwitterGatewayServerMessage("エラー: " + message);
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
            if (commandParams.Length > 14 || commandParams.Length < 0)
                throw new ArgumentOutOfRangeException("commandParams");

            NumericReplyMessage numMsg = new NumericReplyMessage(numReply);
            numMsg.CommandParams[0] = _nick;
            for (Int32 i = 0; i < commandParams.Length; i++)
                numMsg.CommandParams[i+1] = commandParams[i];

            SendServerMessage(numMsg);
        }

        /// <summary>
        /// 
        /// </summary>
        void GetFriendNames()
        {
            RunCheck(delegate
            {
                User[] friends = _twitter.GetFriends();
                _nickNames = new List<string>(Array.ConvertAll<User, String>(friends, delegate(User u)
                {
                    return u.ScreenName;
                }));

                SendNumericReply(NumericReply.RPL_NAMREPLY, "=", _server.ChannelName, String.Join(" ", _nickNames.ToArray()));
                SendNumericReply(NumericReply.RPL_ENDOFNAMES, _server.ChannelName, "End of NAMES list");
            });
        }

        /// <summary>
        /// 
        /// </summary>
        private void SendPing()
        {
            Send(new OtherMessage(String.Format("PING :{0}", Server.ServerName)));
        }

        /// <summary>
        /// 
        /// </summary>
        private void CheckFriends()
        {
            if (_nickNames.Count == 0)
            {
                GetFriendNames();
                return;
            }

            RunCheck(delegate
            {
                User[] friends = _twitter.GetFriends();
                List<String> screenNames = new List<string>(Array.ConvertAll<User, String>(friends, delegate(User u)
                {
                    return u.ScreenName;
                }));

                // てきとうに。
                // 増えた分
                foreach (String screenName in screenNames)
                {
                    if (!_nickNames.Contains(screenName))
                    {
                        JoinMessage joinMsg = new JoinMessage(_server.ChannelName, "");
                        joinMsg.SenderNick = screenName;
                        joinMsg.SenderHost = String.Format("{0}@{1}", "twitter", Server.ServerName);
                        Send(joinMsg);
                    }
                }
                // 減った分
                foreach (String screenName in _nickNames)
                {
                    if (!screenNames.Contains(screenName))
                    {
                        PartMessage partMsg = new PartMessage(_server.ChannelName, "");
                        partMsg.SenderNick = screenName;
                        partMsg.SenderHost = String.Format("{0}@{1}", "twitter", Server.ServerName);
                        Send(partMsg);
                    }
                }

                _nickNames = screenNames;

            });
        }

        private Boolean CheckNewTimeLine(out Boolean friendsCheckRequired)
        {
            Boolean friendsCheckRequiredAnon = false;
            Boolean returnValue = RunCheck(delegate
            {
                Statuses statuses = _twitter.GetTimeline(_lastAccessTimeline);
                Array.Reverse(statuses.Status);
                foreach (Status status in statuses.Status)
                {
                    // 差分チェック
                    if (ProcessDropProtection(_statusBuffer, status))
                    {
                        ProcessTimelineStatus(status, ref friendsCheckRequiredAnon);
                    }
                }

                if (_isFirstTime && _server.EnableDropProtection)
                {
                    _lastAccessTimeline = DateTime.Now;
                }
                _isFirstTime = false;
            });
            
            friendsCheckRequired = friendsCheckRequiredAnon;
            return returnValue;
        }

        private Boolean CheckNewReplies()
        {
            Boolean friendsCheckRequired = false;
            return RunCheck(delegate
            {
                Statuses statuses = _twitter.GetReplies();
                Array.Reverse(statuses.Status);
                bool dummy = false;
                foreach (Status status in statuses.Status)
                {
                    if (status.CreatedAt < _lastAccessReplies)
                        continue;
                    // 差分チェック
                    if (ProcessDropProtection(_repliesBuffer, status) && ProcessDropProtection(_statusBuffer, status))
                    {
                        // Here I pass dummy, because no matter how the replier flags
                        // friendsCheckRequired, we cannot receive his or her info
                        // through get_friends.
                        ProcessTimelineStatus(status, ref dummy);
                    }
                }

                if (_isFirstTimeReplies && _server.EnableDropProtection)
                {
                    _lastAccessReplies = DateTime.Now;
                }
                _isFirstTimeReplies = false;
            });
        }
        
        /// <summary>
        /// 既に受信したstatusかどうかをチェックします。既に送信済みの場合falseを返します。
        /// </summary>
        /// <param name="statusBuffer"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        private Boolean ProcessDropProtection(LinkedList<Status> statusBuffer, Status status)
        {
            // 差分チェック
            if (_server.EnableDropProtection)
            {
                if (statusBuffer.Contains(status))
                    return false;

                statusBuffer.AddLast(status);
                if (statusBuffer.Count > 100)
                {
                    // 一番古いのを消す
                    //Status oldStatus = null;
                    //foreach (Status statTmp in _statusBuffer)
                    //{
                    //    if (oldStatus == null || oldStatus.CreatedAt > statTmp.CreatedAt)
                    //    {
                    //        oldStatus = statTmp;
                    //    }
                    //}
                    //_statusBuffer.Remove(oldStatus);
                    statusBuffer.RemoveFirst();
                }
            }
            
            return true;
        }
        
        private void ProcessTimelineStatus (Status status, ref Boolean friendsCheckRequired)
        {

            // チェック
            if (status.User == null || String.IsNullOrEmpty(status.User.ScreenName))
            {
                return;
            }

            // friends チェックが必要かどうかを確かめる
            // まだないときは取ってくるフラグを立てる
            friendsCheckRequired |= !(_nickNames.Contains(status.User.ScreenName));
            
            // フィルタ
            FilterArgs filterArgs = new FilterArgs(this, status.Text, status.User, "PRIVMSG", false);
            if (!_filter.ExecuteFilters(filterArgs))
            {
                // 捨てる
                return;
            }

            // 自分がゲートウェイを通して発言したものは捨てる
            if (_lastStatusIdsFromGateway.Contains(status.Id))
            {
                return;
            }

            // TinyURL
            String text = (_server.ResolveTinyUrl) ? Utility.ResolveTinyUrlInMessage(filterArgs.Content) : filterArgs.Content;

            String[] lines = text.Split(new Char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (String line in lines)
            {
                // 初回はrecentろぐっぽく
                if (_isFirstTime)
                {
                    Send(new NoticeMessage()
                    {
                        SenderNick = status.User.ScreenName,
                        SenderHost = "twitter@" + Server.ServerName,
                        Receiver   = _server.ChannelName,
                        Content    = String.Format("{0}: {1}", status.CreatedAt.ToString("HH:mm"), line)
                    });
                }
                else
                {
                    Send(CreateIRCMessageFromStatusAndType(status, filterArgs.IRCMessageType, _server.ChannelName, line));
                }

                // グループにも投げる
                foreach (Group group in _groups.Values)
                {
                    if (!group.IsJoined)
                        continue;

                    Boolean isMatched = String.IsNullOrEmpty(group.Topic) ? true : Regex.IsMatch(line, group.Topic);
                    
                    // 0: self
                    // 1: member exists in channel && match regex
                    // 2: no members in channel(self only) && match regex
                    if ((group.Exists(status.User.ScreenName) || group.Members.Count == 0) && isMatched)
                    {
                        if (_isFirstTime)
                        {
                            // 初回はNOTICE
                            Send(CreateIRCMessageFromStatusAndType(status, "NOTICE", group.Name, String.Format("{0}: {1}", status.CreatedAt.ToString("HH:mm"), line)));
                        }
                        else
                        {
                            Send(CreateIRCMessageFromStatusAndType(status, filterArgs.IRCMessageType, group.Name, line));
                        }
                    }
                }
            }

            // 最終更新時刻
            if (_server.EnableDropProtection)
            {
                // 取りこぼし防止しているときは一番古い日付
                if (status.CreatedAt < _lastAccessTimeline)
                {
                    _lastAccessTimeline = status.CreatedAt;
                }
            }
            else
            {
                if (status.CreatedAt > _lastAccessTimeline)
                {
                    _lastAccessTimeline = status.CreatedAt;
                }
            }

            // ウェイト
            if (_server.ClientMessageWait > 0)
                Thread.Sleep(_server.ClientMessageWait);
        }

        // XXX: IRCクライアントライブラリのアップデートで対応できるけどとりあえず...
        private IRCMessage CreateIRCMessageFromStatusAndType(Status status, String type, String receiver, String line)
        {
            IRCMessage msg;
            switch (type.ToUpperInvariant())
            {
                case "NOTICE":
                    msg = new NoticeMessage(receiver, line);
                    break;
                case "PRIVMSG":
                default:
                    msg = new PrivMsgMessage(receiver, line);
                    break;
            } 
            msg.SenderNick = status.User.ScreenName;
            msg.SenderHost = "twitter@" + Server.ServerName;

            return msg;
        }

        /// <summary>
        /// 
        /// </summary>
        private void CheckDirectMessage()
        {
            RunCheck(delegate
            {
                DirectMessages directMessages = _twitter.GetDirectMessages(_lastAccessDirectMessage);
                Array.Reverse(directMessages.DirectMessage);
                foreach (DirectMessage message in directMessages.DirectMessage)
                {
                    // チェック
                    if (message == null || String.IsNullOrEmpty(message.SenderScreenName))
                    {
                        continue;
                    }

                    String text = (_server.ResolveTinyUrl) ? Utility.ResolveTinyUrlInMessage(message.Text) : message.Text;
                    String[] lines = text.Split(new Char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (String line in lines)
                    {
                        PrivMsgMessage privMsg = new PrivMsgMessage();
                        privMsg.SenderNick = message.SenderScreenName;
                        privMsg.SenderHost = "twitter@" + Server.ServerName;
                        privMsg.Receiver = _nick;
                        //privMsg.Content = String.Format("{0}: {1}", screenName, text);
                        privMsg.Content = line;
                        Send(privMsg);
                    }
                    // 最終更新時刻
                    if (message.CreatedAt > _lastAccessDirectMessage)
                    {
                        _lastAccessDirectMessage = message.CreatedAt;
                    }
                }
            });
        }

        private delegate void CheckProcedure();
        /// <summary>
        /// チェックを実行します。例外が発生した場合には自動的にメッセージを送信します。
        /// </summary>
        /// <param name="proc">実行するチェック処理</param>
        /// <returns></returns>
        private Boolean RunCheck(CheckProcedure proc)
        {
            try
            {
                proc();
            }
            catch (WebException ex)
            {
                if (ex.Response == null || !(ex.Response is HttpWebResponse) || ((HttpWebResponse)(ex.Response)).StatusCode != HttpStatusCode.NotModified)
                {
                    // not-modified 以外
                    SendServerErrorMessage(ex.Message);
                    return false;
                }
            }
            catch (TwitterServiceException ex2)
            {
                SendServerErrorMessage(ex2.Message);
                return false;
            }

            return true;
        }
        

        /// <summary>
        /// 
        /// </summary>
        public void Close()
        {
            this.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        private void CheckDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(this.GetType().Name);
        }

        #region IDisposable メンバ
        private Boolean _isDisposed = false;

        public void Dispose()
        {
            if (!_isDisposed)
            {
                if (_server.EnableTrace)
                {
                    Trace.Listeners.Remove(_traceListeneer);
                }

                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                }
                if (_timerDirectMessage != null)
                {
                    _timerDirectMessage.Dispose();
                    _timerDirectMessage = null;
                }
                if (_timerReplies != null)
                {
                    _timerReplies.Dispose();
                    _timerReplies = null;
                }

                if (_twitter != null)
                {
                    _twitter.Dispose();
                    _twitter = null;
                }
                if (_tcpClient != null && _tcpClient.Connected)
                {
                    _tcpClient.Close();
                    _tcpClient = null;
                }
                GC.SuppressFinalize(this);
                _isDisposed = true;
            }
        }

        #endregion
    }
}
