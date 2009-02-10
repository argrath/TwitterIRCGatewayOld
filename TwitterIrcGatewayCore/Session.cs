using System;
using System.Collections.Generic;
using System.ComponentModel;
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

using TypableMap;
using Misuzilla.Net.Irc;
using Misuzilla.Applications.TwitterIrcGateway.Filter;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;

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
        private TwitterIMService _twitterIm;
        private LinkedList<Int32> _lastStatusIdsFromGateway;
        private Groups _groups;
        private Filters _filter;
        private Config _config;
        private AddInManager _addinManager;

        private List<String> _nickNames = new List<string>();
        private Boolean _isFirstTime = true;

        private TraceListener _traceListener;

        private String _username;
        private String _password;
        private String _nick;
        
        #region Events
        public event EventHandler<MessageReceivedEventArgs> PreMessageReceived;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public event EventHandler<MessageReceivedEventArgs> PostMessageReceived;
        public event EventHandler<SessionStartedEventArgs> SessionStarted;
        public event EventHandler<EventArgs> SessionEnded;
        
        public event EventHandler<EventArgs> ConfigChanged;

        public event EventHandler<EventArgs> AddInsLoadCompleted;

        public event EventHandler<TimelineStatusesEventArgs> PreProcessTimelineStatuses;
        public event EventHandler<TimelineStatusEventArgs> PreProcessTimelineStatus;
        public event EventHandler<TimelineStatusEventArgs> PreFilterProcessTimelineStatus;
        public event EventHandler<TimelineStatusEventArgs> PostFilterProcessTimelineStatus;
        public event EventHandler<TimelineStatusEventArgs> PreSendMessageTimelineStatus;
        public event EventHandler<TimelineStatusEventArgs> PostSendMessageTimelineStatus;
        public event EventHandler<TimelineStatusEventArgs> PostProcessTimelineStatus;
        public event EventHandler<TimelineStatusesEventArgs> PostProcessTimelineStatuses;

        public event EventHandler<StatusUpdateEventArgs> PreSendUpdateStatus;
        #endregion

        private Boolean _requireIMReconnect = false;
        private Int32 _imReconnectCount = 0;

        public Session(Server server, TcpClient tcpClient)
        {
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_USER);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_NICK);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_PASS);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_QUIT);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_PRIVMSG);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_WHOIS);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_INVITE);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_JOIN);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_PART);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_KICK);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_LIST);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_TOPIC);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_MODE);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_PING);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_TIGIMENABLE);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_TIGIMDISABLE);

            _groups = new Groups();
            _filter = new Filters();
            _config = new Config();

            _server = server;
            _tcpClient = tcpClient;
            _lastStatusIdsFromGateway = new LinkedList<int>();
            
            _addinManager = new AddInManager();
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
            get { return _tcpClient; }
        }
        
        /// <summary>
        /// 
        /// </summary>
        public String Nick
        {
            get { return _nick; }
        }
        
        /// <summary>
        /// 
        /// </summary>
        public String ClientHost
        {
            get { return _clientHost; }
        }
        
        /// <summary>
        /// セッションに結びつけられたTwitterへのAPIアクセスのためのサービス
        /// </summary>
        public TwitterService TwitterService
        {
            get { return _twitter; }
        }
        
        /// <summary>
        /// セッションに結びつけられた設定
        /// </summary>
        public Config Config
        {
            get { return _config; }
        }
        
        /// <summary>
        /// セッションが持つグループのコレクション
        /// </summary>
        public Groups Groups
        {
            get { return _groups;  }
        }
        
        /// <summary>
        /// セッションが持つフィルタのコレクション
        /// </summary>
        public Filters Filters
        {
            get { return _filter;  }
        }
        
        /// <summary>
        /// セッションのアドインマネージャ
        /// </summary>
        public AddInManager AddInManager
        {
            get { return _addinManager;  }
        }        
        
        /// <summary>
        /// ユーザの設定が保存されているディレクトリのパス
        /// </summary>
        public String UserConfigDirectory
        {
            get { return Path.Combine(ConfigBasePath, _username); }
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
                            OnMessageReceived(msg);
                        }
                        catch (IRCException)
                        {}
                    }
                }
            }
            catch (IOException)
            {}
            catch (NullReferenceException)
            {}
            finally
            {
                OnSessionEnded();
                this.Close();
            }
        }

        #region イベント実行メソッド
        protected virtual void OnMessageReceived(IRCMessage msg)
        {
            Trace.WriteLine(msg.ToString());

            if (FireEvent(PreMessageReceived, new MessageReceivedEventArgs(msg, _writer, _tcpClient)))
            {
                if (FireEvent(MessageReceived, new MessageReceivedEventArgs(msg, _writer, _tcpClient)))
                {
                    FireEvent(PostMessageReceived, new MessageReceivedEventArgs(msg, _writer, _tcpClient));
                }
            }
        }
        protected virtual void OnSessionStarted(String username)
        {
            LoadConfig();
            OnConfigChanged();
            
            LoadGroups();
            LoadFilters();

            if (!String.IsNullOrEmpty(_config.IMServiceServerName))
            {
                ConnectToIMService(true);
            }

            _addinManager.Load(_server, this);
            FireEvent(AddInsLoadCompleted, EventArgs.Empty);
            
            FireEvent(SessionStarted, new SessionStartedEventArgs(username));
        }
        protected virtual void OnSessionEnded()
        {
            FireEvent(SessionEnded, EventArgs.Empty);
        }
        protected virtual void OnConfigChanged()
        {
            if (ConfigChanged != null)
                ConfigChanged(this, EventArgs.Empty);

            if (_traceListener == null && (_config.EnableTrace || _server.EnableTrace))
            {
                _traceListener = new IrcTraceListener(this);
                Trace.Listeners.Add(_traceListener);
            }
            else if ((_traceListener != null) && !_config.EnableTrace && !_server.EnableTrace)
            {
                Trace.Listeners.Remove(_traceListener);
                _traceListener = null;
            }

        }
        
        /// <summary>
        /// 
        /// </summary>
        public void LoadFilters()
        {
            // filters 読み取り
            String path = Path.Combine(ConfigBasePath, Path.Combine(_username, "Filters.xml"));
            try
            {
                _filter = Filters.Load(path);
            }
            catch (IOException ie)
            {
                SendTwitterGatewayServerMessage("エラー: " + ie.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void SaveFilters()
        {
            lock (_filter)
            {
                String path = Path.Combine(ConfigBasePath, Path.Combine(_username, "Filters.xml"));
                try
                {
                    _filter.Save(path);
                }
                catch (IOException ie)
                {
                    SendTwitterGatewayServerMessage("エラー: " + ie.Message);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void LoadGroups()
        {
            // group 読み取り
            lock (_groups)
            {
                String path = Path.Combine(ConfigBasePath, Path.Combine(_username, "Groups.xml"));
                try
                {
                    _groups = Groups.Load(path);

                    // 下位互換性FIX: グループに自分自身のNICKは存在しないようにします
                    foreach (Group g in _groups.Values)
                    {
                        g.Members.Remove(_nick);
                    }
                }
                catch (IOException ie)
                {
                    SendTwitterGatewayServerMessage("エラー: " + ie.Message);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void SaveGroups()
        {
            // group 読み取り
            lock (_groups)
            {
                String path = Path.Combine(ConfigBasePath, Path.Combine(_username, "Groups.xml"));
                try
                {
                    _groups.Save(path);
                }
                catch (IOException ie)
                {
                    SendTwitterGatewayServerMessage("エラー: " + ie.Message);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void LoadConfig()
        {
            lock (_config)
            {
                String path = Path.Combine(ConfigBasePath, Path.Combine(_username, "Config.xml"));
                try
                {
                    _config = Config.Load(path);
                }
                catch (IOException ie)
                {
                    SendTwitterGatewayServerMessage("エラー: " + ie.Message);
                }
            }
        }        
        /// <summary>
        /// 
        /// </summary>
        public void SaveConfig()
        {
            lock (_config)
            {
                String path = Path.Combine(ConfigBasePath, Path.Combine(_username, "Config.xml"));
                try
                {
                    _config.Save(path);
                    OnConfigChanged();
                }
                catch (IOException ie)
                {
                    SendTwitterGatewayServerMessage("エラー: " + ie.Message);
                }
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
        private void MessageReceived_JOIN(object sender, MessageReceivedEventArgs e)
        {
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
                    if (!String.IsNullOrEmpty(group.Topic))
                    {
                        Send(new TopicMessage(channelName, group.Topic));
                    }
                    else
                    {
                        SendNumericReply(NumericReply.RPL_NOTOPIC, channelName, "No topic is set");
                    }
                }
            }
       }

        private void MessageReceived_PART(object sender, MessageReceivedEventArgs e)
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
        private void MessageReceived_KICK(object sender, MessageReceivedEventArgs e)
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
        private void MessageReceived_LIST(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "LIST", true) != 0) return;
            foreach (Group group in _groups.Values)
            {
                SendNumericReply(NumericReply.RPL_LIST, group.Name, group.Members.Count.ToString(), "");
            }
            SendNumericReply(NumericReply.RPL_LISTEND, "End of LIST");
        }
        private void MessageReceived_INVITE(object sender, MessageReceivedEventArgs e)
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

        private void MessageReceived_USER(object sender, MessageReceivedEventArgs e)
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

            SendServer(joinMsg);
            Send(autoMsg);

            //
            // Twitte Service Setup
            //
            _twitter = new TwitterService(_username, _password);
            _twitter.CookieLoginMode = _server.CookieLoginMode;
            _twitter.Interval = _server.Interval;
            _twitter.IntervalDirectMessage = _server.IntervalDirectMessage;
            _twitter.IntervalReplies = _server.IntervalReplies;
            _twitter.EnableRepliesCheck = _server.EnableRepliesCheck;
            _twitter.POSTFetchMode = _server.POSTFetchMode;
            _twitter.RepliesReceived += new EventHandler<StatusesUpdatedEventArgs>(twitter_RepliesReceived);
            _twitter.TimelineStatusesReceived += new EventHandler<StatusesUpdatedEventArgs>(twitter_TimelineStatusesReceived);
            _twitter.CheckError += new EventHandler<ErrorEventArgs>(twitter_CheckError);
            _twitter.DirectMessageReceived += new EventHandler<DirectMessageEventArgs>(twitter_DirectMessageReceived);
            if (_server.Proxy != null)
                _twitter.Proxy = _server.Proxy;

            OnSessionStarted(_username);
            Trace.WriteLine(String.Format("SessionStarted: UserName={0}; Nickname={1}", _username, _nick));

            _twitter.Start();
        }

        void MessageReceived_NICK(object sender, MessageReceivedEventArgs e)
        {
            if (!(e.Message is NickMessage)) return;

            _nick = ((NickMessage)(e.Message)).NewNick;
        }

        void MessageReceived_PASS(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "PASS", true) != 0) return;

            if (e.Message.CommandParam.Length != 0)
            {
                _password = e.Message.CommandParam.Substring(1);
            }
        }

        void MessageReceived_QUIT(object sender, MessageReceivedEventArgs e)
        {
            if (!(e.Message is QuitMessage)) return;

            try
            {
                e.Client.Close();
            }
            catch { }
        }

        void MessageReceived_PRIVMSG(object sender, MessageReceivedEventArgs e)
        {
            PrivMsgMessage message = e.Message as PrivMsgMessage;
            if (message == null) return;

            StatusUpdateEventArgs eventArgs = new StatusUpdateEventArgs(message, message.Content);
            if (!FireEvent(PreSendUpdateStatus, eventArgs)) return;

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
                            if (group.IsJoined && !group.IsSpecial && !group.IgnoreEchoBack && String.Compare(message.Receiver, group.Name, true) != 0)
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

        void MessageReceived_WHOIS(object sender, MessageReceivedEventArgs e)
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

        void MessageReceived_TOPIC(object sender, MessageReceivedEventArgs e)
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
        
        void MessageReceived_MODE(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "MODE", true) != 0) return;
            ModeMessage modeMsg = e.Message as ModeMessage;

            // チャンネルターゲットかつタイムラインチャンネル以外のみ
            if (modeMsg.Target.StartsWith("#") && (String.Compare(modeMsg.Target, _server.ChannelName, true) != 0))
            {
                String channel = modeMsg.Target;
                String modeArgs = modeMsg.ModeArgs;
                Group group;
                if (!_groups.TryGetValue(channel, out group))
                {
                    SendErrorReply(ErrorReply.ERR_NOSUCHCHANNEL, "No such channel");
                    return;
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

        void MessageReceived_PING(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "PING", true) != 0) return;
            Send(IRCMessage.CreateMessage("PONG :" + e.Message.CommandParam));
        }
        
        void MessageReceived_TIGIMENABLE(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "TIGIMENABLE", true) != 0) return;
            if (String.IsNullOrEmpty(e.Message.CommandParams[3]))
            {
                SendTwitterGatewayServerMessage("TIGIMENABLE コマンドは4つの引数(ServiceServerName, ServerName, UserName, Password)が必要です。");
                return;
            }
            
            _config.IMServiceServerName = e.Message.CommandParams[0];
            _config.IMServerName = e.Message.CommandParams[1];
            _config.IMUserName = e.Message.CommandParams[2];
            _config.SetIMPassword(_password, e.Message.CommandParams[3]);
            SaveConfig();
            ConnectToIMService(true);
        }

        void MessageReceived_TIGIMDISABLE(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "TIGIMDISABLE", true) != 0) return;
            _config.IMServiceServerName = _config.IMServerName = _config.IMUserName = _config.IMEncryptoPassword = "";
            SaveConfig();
            DisconnectToIMService(false);
        }

        private void ConnectToIMService(Boolean initialConnect)
        {
            DisconnectToIMService(!initialConnect);
            SendTwitterGatewayServerMessage(String.Format("インスタントメッセージングサービス \"{0}\" (サーバ: {1}) にユーザ \"{2}\" でログインします。", _config.IMServerName, _config.IMServiceServerName, _config.IMUserName));

            _twitterIm = new TwitterIMService(_config.IMServiceServerName, _config.IMServerName, _config.IMUserName, _config.GetIMPassword(_password));
            _twitterIm.StatusUpdateReceived += new EventHandler<TwitterIMService.StatusUpdateReceivedEventArgs>(twitterIm_StatusUpdateReceived);
            _twitterIm.Logined += new EventHandler(twitterIm_Logined);
            _twitterIm.AuthErrored += new EventHandler(twitterIm_AuthErrored);
            _twitterIm.SocketErrorHandled += new EventHandler<TwitterIMService.ErrorEventArgs>(twitterIm_SocketErrorHandled);
            _twitterIm.Closed += new EventHandler(twitterIm_Closed);
            _twitterIm.Open();

            if (initialConnect)
            {
                _requireIMReconnect = true;
                _imReconnectCount = 0;
            }
        }
        #endregion

        #region Twitter IM Service
        private void DisconnectToIMService(Boolean requireIMReconnect)
        {
            if (_twitterIm != null)
            {
                //SendTwitterGatewayServerMessage("インスタントメッセージングサービスから切断します。");
                _requireIMReconnect = requireIMReconnect;
                _twitterIm.Close();
                _twitterIm = null;
            }
        }

        void twitterIm_Closed(object sender, EventArgs e)
        {
            if (_requireIMReconnect && _imReconnectCount++ < 10)
            {
                SendTwitterGatewayServerMessage(String.Format("インスタントメッセージングサービスから切断しました。再接続します({0}回目)", _imReconnectCount));
                ConnectToIMService(false);
            }
            else
            {
                SendTwitterGatewayServerMessage("インスタントメッセージングサービスから切断しました。");
            }
        }
        void twitterIm_SocketErrorHandled(object sender, TwitterIMService.ErrorEventArgs e)
        {
            if (_requireIMReconnect && _imReconnectCount++ < 10)
            {
                SendTwitterGatewayServerMessage(String.Format("インスタントメッセージングサービスの接続でエラーが発生しました: {0} / 再接続します。({1}回目)", e.Exception.Message, _imReconnectCount));
                ConnectToIMService(false);
            }
            else
            {
                SendTwitterGatewayServerMessage("インスタントメッセージングサービスの接続でエラーが発生しました: " + e.Exception.Message); 
            }
        
        }
        void twitterIm_Logined(object sender, EventArgs e)
        {
            SendTwitterGatewayServerMessage("インスタントメッセージングサービスにログインしました。");
        }
        void twitterIm_AuthErrored(object sender, EventArgs e)
        {
            SendTwitterGatewayServerMessage("インスタントメッセージングサービスのログインに失敗しました。ユーザ名とパスワードが正しくありません。");
        }
        void twitterIm_StatusUpdateReceived(object sender, TwitterIMService.StatusUpdateReceivedEventArgs e)
        {
            _isFirstTime = false; // IMが先にきてしまったらあきらめる
            _twitter.ProcessStatus(e.Status, (s) =>
            {
                Boolean friendsCheckRequired = false;
                ProcessTimelineStatus(e.Status, ref friendsCheckRequired);
            });
        }
        #endregion

        #region Twitter Service イベント
        void twitter_CheckError(object sender, ErrorEventArgs e)
        {
            SendServerErrorMessage(e.Exception.Message);
        }

        void twitter_DirectMessageReceived(object sender, DirectMessageEventArgs e)
        {
            DirectMessage message = e.DirectMessage;
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
        }

        void twitter_TimelineStatusesReceived(object sender, StatusesUpdatedEventArgs e)
        {
            SendPing();

            TimelineStatusesEventArgs eventArgs = new TimelineStatusesEventArgs(e.Statuses, _isFirstTime);
            if (!FireEvent(PreProcessTimelineStatuses, eventArgs)) return;

            // 初回だけは先にチェックしておかないとnamesが後から来てジャマ
            if (_isFirstTime)
            {
                CheckFriends();
            }
            
            Boolean friendsCheckRequired = e.FriendsCheckRequired;
            foreach (Status status in e.Statuses.Status)
            {
                ProcessTimelineStatus(status, ref friendsCheckRequired);
            }
            
            // Friendsをチェックするのは成功して、チェックが必要となったとき
            if (e.FriendsCheckRequired && !_server.DisableUserList)
            {
                CheckFriends();
            }

            if (!FireEvent(PostProcessTimelineStatuses, eventArgs)) return;
            
            _isFirstTime = false;
        }

        void twitter_RepliesReceived(object sender, StatusesUpdatedEventArgs e)
        {
            Boolean dummy = false;
            foreach (Status status in e.Statuses.Status)
            {
                ProcessTimelineStatus(status, ref dummy);
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
        /// <param name="message"></param>
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
                _nickNames = new List<string>(Array.ConvertAll<User, String>(friends, u => u.ScreenName));

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
                List<String> screenNames = new List<string>(Array.ConvertAll<User, String>(friends, u => u.ScreenName));

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

        public void ProcessTimelineStatus (Status status, ref Boolean friendsCheckRequired)
        {
            TimelineStatusEventArgs eventArgs = new TimelineStatusEventArgs(status, status.Text, "PRIVMSG");
            if (!FireEvent(PreProcessTimelineStatus, eventArgs)) return;
            
            // チェック
            // 自分がゲートウェイを通して発言したものは捨てる
            if (status.User == null || String.IsNullOrEmpty(status.User.ScreenName) || _lastStatusIdsFromGateway.Contains(status.Id))
            {
                return;
            }

            // friends チェックが必要かどうかを確かめる
            // まだないときは取ってくるフラグを立てる
            friendsCheckRequired |= !(_nickNames.Contains(status.User.ScreenName));
            
            // フィルタ
            if (!FireEvent(PreFilterProcessTimelineStatus, eventArgs)) return;
            FilterArgs filterArgs = new FilterArgs(this, eventArgs.Text, status.User, eventArgs.IRCMessageType, false, status);
            if (!_filter.ExecuteFilters(filterArgs))
            {
                // 捨てる
                return;
            }
            eventArgs.Text = filterArgs.Content;

            if (!FireEvent(PostFilterProcessTimelineStatus, eventArgs)) return;
            if (!FireEvent(PreSendMessageTimelineStatus, eventArgs)) return;
            
            String[] lines = eventArgs.Text.Split(new Char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
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
                    Send(CreateIRCMessageFromStatusAndType(status, eventArgs.IRCMessageType, _server.ChannelName, line));
                }

                // グループにも投げる
                foreach (Group group in _groups.Values)
                {
                    if (!group.IsJoined || group.IsSpecial)
                        continue;

                    Boolean isOrMatch = String.IsNullOrEmpty(group.Topic) ? false : group.Topic.StartsWith("|");
                    Boolean isMatched = String.IsNullOrEmpty(group.Topic) ? true : Regex.IsMatch(line, (isOrMatch ? group.Topic.Substring(1) : group.Topic));
                    Boolean isExistsInChannelOrNoMembers = (group.Exists(status.User.ScreenName) || group.Members.Count == 0);

                    // 0: self
                    // 1: member exists in channel && match regex
                    // 2: no members in channel(self only) && match regex
                    // 3: member exists in channel || match regex (StartsWith: "|")
                    // 4: no members in channel(self only) || match regex (StartsWith: "|")
                    if (isOrMatch ? (isExistsInChannelOrNoMembers || isMatched) : (isExistsInChannelOrNoMembers && isMatched))
                    {
                        if (_isFirstTime)
                        {
                            // 初回はNOTICE
                            Send(CreateIRCMessageFromStatusAndType(status, "NOTICE", group.Name, String.Format("{0}: {1}", status.CreatedAt.ToString("HH:mm"), line)));
                        }
                        else
                        {
                            Send(CreateIRCMessageFromStatusAndType(status, eventArgs.IRCMessageType, group.Name, line));
                        }
                    }
                }
            }

            if (!FireEvent(PostSendMessageTimelineStatus, eventArgs)) return;
            if (!FireEvent(PostProcessTimelineStatus, eventArgs)) return;
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

        public delegate void Procedure();
        /// <summary>
        /// チェックを実行します。例外が発生した場合には自動的にメッセージを送信します。
        /// </summary>
        /// <param name="proc">実行するチェック処理</param>
        /// <returns></returns>
        public Boolean RunCheck(Procedure proc)
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
                    twitter_CheckError(_twitter, new ErrorEventArgs(ex));
                    return false;
                }
            }
            catch (TwitterServiceException ex2)
            {
                twitter_CheckError(_twitter, new ErrorEventArgs(ex2));
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

        #region ヘルパーメソッド
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TEventArgs"></typeparam>
        /// <param name="hander"></param>
        /// <param name="e"></param>
        /// <returns>キャンセルされた場合にはfalseが返ります。</returns>
        private Boolean FireEvent<TEventArgs>(EventHandler<TEventArgs> hander, TEventArgs e) where TEventArgs:EventArgs
        {
            if (hander != null)
                hander(this, e);
            
            if (e is CancelableEventArgs)
            {
                return !((e as CancelableEventArgs).Cancel);
            }
            
            return true;
        }
        #endregion

        #region IDisposable メンバ
        private Boolean _isDisposed = false;

        public void Dispose()
        {
            if (!_isDisposed)
            {
                if (_server.EnableTrace)
                {
                    Trace.Listeners.Remove(_traceListener);
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
