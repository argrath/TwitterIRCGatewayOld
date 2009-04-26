using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// Twitterへの接続と操作を提供します。
    /// </summary>
    public class TwitterService : IDisposable
    {
        //private WebClient _webClient;
        private CredentialCache _credential;
        private IWebProxy _proxy = WebRequest.DefaultWebProxy;
        private String _userName;
        private Boolean _cookieLoginMode = false;
        private Boolean _enableDropProtection = true;

        private Timer _timer;
        private Timer _timerDirectMessage;
        private Timer _timerReplies;
        
        private DateTime _lastAccessTimeline = new DateTime();
        private DateTime _lastAccessReplies = new DateTime();
        private DateTime _lastAccessDirectMessage = DateTime.Now;
        private Int32 _lastAccessDirectMessageId = 0;
        private Boolean _isFirstTime = true;
        private Boolean _isFirstTimeReplies = true;
        private Boolean _isFirstTimeDirectMessage = true;

        private Int32 _bufferSize = 250;
        private LinkedList<Status> _statusBuffer;
        private LinkedList<Status> _repliesBuffer;

        #region Events
        /// <summary>
        /// 更新チェック時にエラーが発生した場合に発生します。
        /// </summary>
        public event EventHandler<ErrorEventArgs> CheckError;
        /// <summary>
        /// タイムラインステータスの更新があった場合に発生します。
        /// </summary>
        public event EventHandler<StatusesUpdatedEventArgs> TimelineStatusesReceived;
        /// <summary>
        /// Repliesの更新があった場合に発生します。
        /// </summary>
        public event EventHandler<StatusesUpdatedEventArgs> RepliesReceived;
        /// <summary>
        /// ダイレクトメッセージの更新があった場合に発生します。
        /// </summary>
        public event EventHandler<DirectMessageEventArgs> DirectMessageReceived;
        #endregion

        #region Fields
        /// <summary>
        /// Twitter APIのエンドポイントURLのプレフィックスを取得・設定します。
        /// </summary>
        public String ServiceServerPrefix = "http://twitter.com";
        /// <summary>
        /// リクエストのRefererを取得・設定します。
        /// </summary>
        public String Referer = "http://twitter.com/home";
        /// <summary>
        /// リクエストのクライアント名を取得・設定します。この値はsourceパラメータとして利用されます。
        /// </summary>
        public String ClientName = "TwitterIrcGateway";
        public String ClientUrl = "http://www.misuzilla.org/dist/net/twitterircgateway/";
        public String ClientVersion = typeof(TwitterService).Assembly.GetName().Version.ToString();
        #endregion

        /// <summary>
        /// TwitterService クラスのインスタンスをユーザ名とパスワードで初期化します。
        /// </summary>
        /// <param name="userName">ユーザー名</param>
        /// <param name="password">パスワード</param>
        public TwitterService(String userName, String password)
        {
            CredentialCache credCache = new CredentialCache();
            credCache.Add(new Uri(ServiceServerPrefix), "Basic", new NetworkCredential(userName, password));
            _credential = credCache;

            _userName = userName;
            
            _timer = new Timer(new TimerCallback(OnTimerCallback), null, Timeout.Infinite, Timeout.Infinite);
            _timerDirectMessage = new Timer(new TimerCallback(OnTimerCallbackDirectMessage), null, Timeout.Infinite, Timeout.Infinite);
            _timerReplies = new Timer(new TimerCallback(OnTimerCallbackReplies), null, Timeout.Infinite, Timeout.Infinite);
            
            _statusBuffer = new LinkedList<Status>();
            _repliesBuffer = new LinkedList<Status>();

            //_webClient = new PreAuthenticatedWebClient();
            //_webClient = new WebClient();
            //_webClient.Credentials = _credential;

            Interval = 90;
            IntervalDirectMessage = 360;
            IntervalReplies = 120;
        
            POSTFetchMode = false;
        }

        /// <summary>
        /// 接続に利用するプロキシを設定します。
        /// </summary>
        public IWebProxy Proxy
        {
            get
            {
                return _proxy;
                //return _webClient.Proxy;
            }
            set
            {
                _proxy = value;
                //_webClient.Proxy = value;
            }
        }
        
        /// <summary>
        /// Cookieを利用してログインしてデータにアクセスします。
        /// </summary>
        [Obsolete("Cookieログインによるデータ取得は制限されました。POSTFetchModeを利用してください。")]
        public Boolean CookieLoginMode
        {
            get { return _cookieLoginMode; }
            set { _cookieLoginMode = value; }
        }
         
        /// <summary>
        /// POSTを利用してログインしてデータにアクセスします。
        /// </summary>
        [Obsolete("POSTによる取得は廃止されました。")]
        public Boolean POSTFetchMode
        {
            get { return false; }
            set { }
        }
       
        /// <summary>
        /// 取りこぼし防止を有効にするかどうかを指定します。
        /// </summary>
        public Boolean EnableDropProtection
        {
            get { return _enableDropProtection; }
            set { _enableDropProtection = value; }
        }

        /// <summary>
        /// タイムラインをチェックする間隔を指定します。
        /// </summary>
        public Int32 Interval
        {
            get;
            set;
        }

        /// <summary>
        /// ダイレクトメッセージをチェックする間隔を指定します。
        /// </summary>
        public Int32 IntervalDirectMessage
        {
            get;
            set;
        }

        /// <summary>
        /// Repliesをチェックする間隔を指定します。
        /// </summary>
        public Int32 IntervalReplies
        {
            get;
            set;
        }

        /// <summary>
        /// Repliesのチェックを実行するかどうかを指定します。
        /// </summary>
        public Boolean EnableRepliesCheck
        {
            get;
            set;
        }

        /// <summary>
        /// タイムラインの一回の取得につき何件取得するかを指定します。
        /// </summary>
        public Int32 FetchCount
        {
            get;
            set;
        }

        /// <summary>
        /// 認証情報を問い合わせます。
        /// </summary>
        /// <return cref="User">ユーザー情報</returns>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public User VerifyCredential()
        {
            return ExecuteRequest<User>(() =>
            {
                String responseBody = GET("/account/verify_credentials.xml", false);
                if (NilClasses.CanDeserialize(responseBody))
                {
                    return null;
                }
                else
                {
                    User user = User.Serializer.Deserialize(new StringReader(responseBody)) as User;
                    return user;
                }
            });
        }
        /// <summary>
        /// ステータスを更新します。
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public Status UpdateStatus(String message)
        {
            return UpdateStatus(message, 0);
        }
        
        /// <summary>
        /// ステータスを更新します。
        /// </summary>
        /// <param name="message"></param>
        /// <param name="inReplyToStatusId"></param>
        public Status UpdateStatus(String message, Int32 inReplyToStatusId)
        {
            String encodedMessage = TwitterService.EncodeMessage(message);
            return ExecuteRequest<Status>(() =>
            {
                String responseBody = POST(String.Format("/statuses/update.xml?status={0}&source={1}{2}", encodedMessage, ClientName, (inReplyToStatusId != 0 ? "&in_reply_to_status_id="+inReplyToStatusId : "")), new byte[] {});
                if (NilClasses.CanDeserialize(responseBody))
                {
                    return null;
                }
                else
                {
                    Status status = Status.Serializer.Deserialize(new StringReader(responseBody)) as Status;
                    return status;
                }
            });
        }

        /// <summary>
        /// 指定されたユーザにダイレクトメッセージを送信します。
        /// </summary>
        /// <param name="targetId"></param>
        /// <param name="message"></param>
        public void SendDirectMessage(String targetId, String message)
        {
            String encodedMessage = TwitterService.EncodeMessage(message);
            ExecuteRequest(() =>
            {
                String responseBody = POST(String.Format("/direct_messages/new.xml?user={0}&text={1}", targetId, encodedMessage), new Byte[0]);
            });
        }

        /// <summary>
        /// friendsを取得します。
        /// </summary>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public User[] GetFriends()
        {
            return GetFriends(1);
        }

        /// <summary>
        /// friendsを取得します。
        /// </summary>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public User[] GetFriends(Int32 maxPage)
        {
            List<User> usersList = new List<User>();
            Int32 page = 0;
            return ExecuteRequest<User[]>(() =>
            {
                while (page++ != maxPage)
                {
                    String responseBody = GET(String.Format("/statuses/friends.xml?page={0}&lite=true", page));
                    if (NilClasses.CanDeserialize(responseBody))
                    {
                        return usersList.ToArray();
                    }
                    else
                    {
                        Users users = Users.Serializer.Deserialize(new StringReader(responseBody)) as Users;
                        if (users == null || users.User == null || users.User.Length == 0)
                        {
                            return usersList.ToArray();
                        }
                        else
                        {
                            usersList.AddRange(users.User);
                            if (users.User.Length < 100)
                                break;
                        }
                    }
                }
                // あまりに多い場合はそこまで。
                return usersList.ToArray();
            });
        }

        /// <summary>
        /// userを取得します。
        /// </summary>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public User GetUser(String id)
        {
            return ExecuteRequest<User>(() =>
            {
                String responseBody = GET(String.Format("/users/show/{0}.xml", id), false);
                if (NilClasses.CanDeserialize(responseBody))
                {
                    return null;
                }
                else
                {
                    User user = User.Serializer.Deserialize(new StringReader(responseBody)) as User;
                    return user;
                }
            });
        }

        /// <summary>
        /// timeline を取得します。
        /// </summary>
        /// <param name="since">最終更新日時</param>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public Statuses GetTimeline(DateTime since)
        {
            return GetTimeline(since, FetchCount);
        }
        /// <summary>
        /// timeline を取得します。
        /// </summary>
        /// <param name="since">最終更新日時</param>
        /// <param name="count">取得数</param>
        /// <returns></returns>
        public Statuses GetTimeline(DateTime since, Int32 count)
        {
            return ExecuteRequest<Statuses>(() =>
            {
                String responseBody = GET(String.Format("/statuses/friends_timeline.xml?since={0}&count={1}", Utility.UrlEncode(since.ToUniversalTime().ToString("r")), count));
                Statuses statuses;
                if (NilClasses.CanDeserialize(responseBody))
                {
                    statuses = new Statuses();
                    statuses.Status = new Status[0];
                }
                else
                {
                    statuses = Statuses.Serializer.Deserialize(new StringReader(responseBody)) as Statuses;
                    if (statuses == null || statuses.Status == null)
                    {
                        statuses = new Statuses();
                        statuses.Status = new Status[0];
                    }
                }

                return statuses;
            });
        }

        /// <summary>
        /// replies を取得します。
        /// </summary>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        [Obsolete]
        public Statuses GetReplies()
        {
            return GetMentions();
        }
        /// <summary>
        /// mentions を取得します。
        /// </summary>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public Statuses GetMentions()
        {
            return ExecuteRequest<Statuses>(() =>
            {
                String responseBody = GET("/statuses/replies.xml");
                Statuses statuses;
                if (NilClasses.CanDeserialize(responseBody))
                {
                    statuses = new Statuses();
                    statuses.Status = new Status[0];
                }
                else
                {
                    statuses = Statuses.Serializer.Deserialize(new StringReader(responseBody)) as Statuses;
                    if (statuses == null || statuses.Status == null)
                    {
                        statuses = new Statuses();
                        statuses.Status = new Status[0];
                    }
                }

                return statuses;
            });
        }

        /// <summary>
        /// direct messages を取得します。
        /// </summary>
        /// <param name="since">最終更新日時</param>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public DirectMessages GetDirectMessages(DateTime since)
        {
            return ExecuteRequest<DirectMessages>(() =>
            {
                // Cookie ではダメ
                String responseBody = GET(String.Format("/direct_messages.xml?since={0}", Utility.UrlEncode(since.ToUniversalTime().ToString("r"))), false);
                DirectMessages directMessages;
                if (NilClasses.CanDeserialize(responseBody))
                {
                    // 空
                    directMessages = new DirectMessages();
                    directMessages.DirectMessage = new DirectMessage[0];
                }
                else
                {
                    directMessages = DirectMessages.Serializer.Deserialize(new StringReader(responseBody)) as DirectMessages;
                    if (directMessages == null || directMessages.DirectMessage == null)
                    {
                        directMessages = new DirectMessages();
                        directMessages.DirectMessage = new DirectMessage[0];
                    }
                }

                return directMessages;
            });
        }

        /// <summary>
        /// 指定したユーザの timeline を取得します。
        /// </summary>

        /// <summary>
        /// direct messages を取得します。
        /// </summary>
        /// <param name="sinceId">最後に取得したID</param>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public DirectMessages GetDirectMessages(Int32 sinceId)
        {
            return ExecuteRequest<DirectMessages>(() =>
            {
                // Cookie ではダメ
                String responseBody = GET(String.Format("/direct_messages.xml?since_id={0}", sinceId), false);
                DirectMessages directMessages;
                if (NilClasses.CanDeserialize(responseBody))
                {
                    // 空
                    directMessages = new DirectMessages();
                    directMessages.DirectMessage = new DirectMessage[0];
                }
                else
                {
                    directMessages = DirectMessages.Serializer.Deserialize(new StringReader(responseBody)) as DirectMessages;
                    if (directMessages == null || directMessages.DirectMessage == null)
                    {
                        directMessages = new DirectMessages();
                        directMessages.DirectMessage = new DirectMessage[0];
                    }
                }

                return directMessages;
            });
        }

        /// <param name="screenName">スクリーンネーム</param>
        /// <param name="since">最終更新日時</param>
        /// <param name="count"></param>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public Statuses GetTimelineByScreenName(String screenName, DateTime since, Int32 count)
        {
            return ExecuteRequest<Statuses>(() =>
            {
                StringBuilder sb = new StringBuilder();
                if (since != new DateTime())
                    sb.Append("since=").Append(Utility.UrlEncode(since.ToUniversalTime().ToString("r"))).Append("&");
                if (count > 0)
                    sb.Append("count=").Append(count).Append("&");

                String responseBody = GET(String.Format("/statuses/user_timeline/{0}.xml?{1}", screenName, sb.ToString()));
                Statuses statuses;
                if (NilClasses.CanDeserialize(responseBody))
                {
                    statuses = new Statuses();
                    statuses.Status = new Status[0];
                }
                else
                {
                    statuses = Statuses.Serializer.Deserialize(new StringReader(responseBody)) as Statuses;
                    if (statuses == null || statuses.Status == null)
                    {
                        statuses = new Statuses();
                        statuses.Status = new Status[0];
                    }
                }

                return statuses;
            });
        }

        /// <summary>
        /// 指定したユーザの favorites を取得します。
        /// </summary>
        /// <param name="screenName">スクリーンネーム</param>
        /// <param name="page">ページ</param>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public Statuses GetFavoritesByScreenName(String screenName, Int32 page)
        {
            return ExecuteRequest<Statuses>(() =>
            {
                StringBuilder sb = new StringBuilder();
                if (page > 0)
                    sb.Append("page=").Append(page).Append("&");

                String responseBody = GET(String.Format("/favorites/{0}.xml?{1}", screenName, sb.ToString()));
                Statuses statuses;
                if (NilClasses.CanDeserialize(responseBody))
                {
                    statuses = new Statuses();
                    statuses.Status = new Status[0];
                }
                else
                {
                    statuses = Statuses.Serializer.Deserialize(new StringReader(responseBody)) as Statuses;
                    if (statuses == null || statuses.Status == null)
                    {
                        statuses = new Statuses();
                        statuses.Status = new Status[0];
                    }
                }

                return statuses;
            });
        }
        
        /// <summary>
        /// メッセージをfavoritesに追加します。
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Status CreateFavorite(Int32 id)
        {
            return ExecuteRequest<Status>(() =>
            {
                String responseBody = POST(String.Format("/favorites/create/{0}.xml", id), new byte[0]);
                Status status;
                if (NilClasses.CanDeserialize(responseBody))
                {
                    return null;
                }
                else
                {
                    status = Status.Serializer.Deserialize(new StringReader(responseBody)) as Status;
                    return status;
                }
            });
        }

        /// <summary>
        /// メッセージをfavoritesから削除します。
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Status DestroyFavorite(Int32 id)
        {
            return ExecuteRequest<Status>(() =>
            {
                String responseBody = POST(String.Format("/favorites/destroy/{0}.xml", id), new byte[0]);
                Status status;
                if (NilClasses.CanDeserialize(responseBody))
                {
                    return null;
                }
                else
                {
                    status = Status.Serializer.Deserialize(new StringReader(responseBody)) as Status;
                    return status;
                }
            });
        }

        /// <summary>
        /// メッセージを削除します。
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Status DestroyStatus(Int32 id)
        {
            return ExecuteRequest<Status>(() =>
            {
                String responseBody = POST(String.Format("/statuses/destroy/{0}.xml", id), new byte[0]);
                Status status;
                if (NilClasses.CanDeserialize(responseBody))
                {
                    return null;
                }
                else
                {
                    status = Status.Serializer.Deserialize(new StringReader(responseBody)) as Status;
                    return status;
                }
            });
        }
        /// <summary>
        /// ユーザをfollowします。
        /// </summary>
        /// <param name="screenName"></param>
        /// <returns></returns>
        public User CreateFriendship(String screenName)
        {
            return ExecuteRequest<User>(() =>
            {
                String responseBody = POST(String.Format("/friendships/create/{0}.xml", screenName), new byte[0]);
                if (NilClasses.CanDeserialize(responseBody))
                {
                    return null;
                }
                else
                {
                    return User.Serializer.Deserialize(new StringReader(responseBody)) as User;
                }
            });
        }

        /// <summary>
        /// ユーザをremoveします。
        /// </summary>
        /// <param name="screenName"></param>
        /// <returns></returns>
        public User DestroyFriendship(String screenName)
        {
            return ExecuteRequest<User>(() =>
            {
                String responseBody = POST(String.Format("/friendships/destroy/{0}.xml", screenName), new byte[0]);
                if (NilClasses.CanDeserialize(responseBody))
                {
                    return null;
                }
                else
                {
                    return User.Serializer.Deserialize(new StringReader(responseBody)) as User;
                }
            });
        }

        /// <summary>
        /// ユーザをblockします。
        /// </summary>
        /// <param name="screenName"></param>
        /// <returns></returns>
        public User CreateBlock(String screenName)
        {
            return ExecuteRequest<User>(() =>
            {
                String responseBody = POST(String.Format("/blocks/create/{0}.xml", screenName), new byte[0]);
                if (NilClasses.CanDeserialize(responseBody))
                {
                    return null;
                }
                else
                {
                    return User.Serializer.Deserialize(new StringReader(responseBody)) as User;
                }
            });
        }

        /// <summary>
        /// ユーザへのblockを解除します。
        /// </summary>
        /// <param name="screenName"></param>
        /// <returns></returns>
        public User DestroyBlock(String screenName)
        {
            return ExecuteRequest<User>(() =>
            {
                String responseBody = POST(String.Format("/blocks/destroy/{0}.xml", screenName), new byte[0]);
                if (NilClasses.CanDeserialize(responseBody))
                {
                    return null;
                }
                else
                {
                    return User.Serializer.Deserialize(new StringReader(responseBody)) as User;
                }
            });
        }
        #region 内部タイマーイベント
        /// <summary>
        /// Twitter のタイムラインの受信を開始します。
        /// </summary>
        public void Start()
        {
            _timer.Change(0, Interval * 1000);
            _timerDirectMessage.Change(0, IntervalDirectMessage * 1000);
            if (EnableRepliesCheck)
            {
                _timerReplies.Change(0, IntervalReplies * 1000);
            }
        }

        /// <summary>
        /// Twitter のタイムラインの受信を停止します。
        /// </summary>
        public void Stop()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _timerDirectMessage.Change(Timeout.Infinite, Timeout.Infinite);
            _timerReplies.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stateObject"></param>
        private void OnTimerCallback(Object stateObject)
        {
            RunCallback(_timer, CheckNewTimeLine);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stateObject"></param>
        private void OnTimerCallbackDirectMessage(Object stateObject)
        {
            RunCallback(_timerDirectMessage, CheckDirectMessage);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stateObject"></param>
        private void OnTimerCallbackReplies(Object stateObject)
        {
            RunCallback(_timerReplies, CheckNewReplies);
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
            if (_enableDropProtection)
            {
                lock (statusBuffer)
                {
                    if (statusBuffer.Contains(status))
                        return false;

                    statusBuffer.AddLast(status);
                    if (statusBuffer.Count > _bufferSize)
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
            }

            return true;
        }
        /// <summary>
        /// ステータスがすでに流されたかどうかをチェックして、流されていない場合に指定されたアクションを実行します。
        /// </summary>
        /// <param name="status"></param>
        /// <param name="action"></param>
        public void ProcessStatus(Status status, Action<Status> action)
        {
            if (ProcessDropProtection(_statusBuffer, status))
            {
                action(status);

                // 最終更新時刻
                if (_enableDropProtection)
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
            }
        }
        /// <summary>
        /// ステータスがすでに流されたかどうかをチェックして、流されていない場合に指定されたアクションを実行します。
        /// </summary>
        /// <param name="statuses"></param>
        /// <param name="action"></param>
        public void ProcessStatuses(Statuses statuses, Action<Statuses> action)
        {
            Statuses tmpStatuses = new Statuses();
            List<Status> statusList = new List<Status>();
            foreach (Status status in statuses.Status)
            {
                ProcessStatus(status, s =>
                {
                    statusList.Add(status);
                    // 最終更新時刻
                    if (_enableDropProtection)
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
                });
            }

            if (statusList.Count == 0)
                return;
            tmpStatuses.Status = statusList.ToArray();
            action(tmpStatuses);
        }
        /// <summary>
        /// Repliesステータスがすでに流されたかどうかをチェックして、流されていない場合に指定されたアクションを実行します。
        /// </summary>
        /// <param name="statuses"></param>
        /// <param name="action"></param>
        public void ProcessRepliesStatus(Statuses statuses, Action<Statuses> action)
        {
            Statuses tmpStatuses = new Statuses();
            List<Status> statusList = new List<Status>();
            foreach (Status status in statuses.Status)
            {
                if (status.CreatedAt < _lastAccessReplies)
                    continue;

                if (ProcessDropProtection(_repliesBuffer, status) && ProcessDropProtection(_statusBuffer, status))
                {
                    statusList.Add(status);

                    // 最終更新時刻
                    if (_enableDropProtection)
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
                }
            }

            if (statusList.Count == 0)
                return;
            tmpStatuses.Status = statusList.ToArray();
            action(tmpStatuses);
        }

        private void CheckNewTimeLine()
        {
            Boolean friendsCheckRequired = false;
            RunCheck(delegate
            {
                Statuses statuses = GetTimeline(_lastAccessTimeline);
                Array.Reverse(statuses.Status);
                // 差分チェック
                ProcessStatuses(statuses, (s) =>
                {
                    OnTimelineStatusesReceived(new StatusesUpdatedEventArgs(s, _isFirstTime, friendsCheckRequired));
                });

                if (_isFirstTime && _enableDropProtection)
                {
                    _lastAccessTimeline = DateTime.Now;
                }
                _isFirstTime = false;
            });
        }

        private void CheckDirectMessage()
        {
            RunCheck(delegate
            {
                DirectMessages directMessages = (_lastAccessDirectMessageId == 0) ? GetDirectMessages(_lastAccessDirectMessage) : GetDirectMessages(_lastAccessDirectMessageId);
                Array.Reverse(directMessages.DirectMessage);
                foreach (DirectMessage message in directMessages.DirectMessage)
                {
                    // チェック
                    if (message == null || String.IsNullOrEmpty(message.SenderScreenName))
                    {
                        continue;
                    }
                    
                    OnDirectMessageReceived(new DirectMessageEventArgs(message, _isFirstTimeDirectMessage));
                    
                    // 最終更新時刻
                    if (_lastAccessDirectMessageId == 0 || message.CreatedAt > _lastAccessDirectMessage)
                    {
                        _lastAccessDirectMessage = message.CreatedAt;
                        _lastAccessDirectMessageId = message.Id;
                    }
                }
                _isFirstTimeDirectMessage = false;
            });
        }

        private void CheckNewReplies()
        {
            Boolean friendsCheckRequired = false;
            RunCheck(delegate
            {
                Statuses statuses = GetReplies();
                Array.Reverse(statuses.Status);
                
                // 差分チェック
                ProcessRepliesStatus(statuses, (s) =>
                {
                    // Here I pass dummy, because no matter how the replier flags
                    // friendsCheckRequired, we cannot receive his or her info
                    // through get_friends.
                    OnRepliesReceived(new StatusesUpdatedEventArgs(s, _isFirstTimeReplies, friendsCheckRequired));
                });

                if (_isFirstTimeReplies && _enableDropProtection)
                {
                    _lastAccessReplies = DateTime.Now;
                }
                _isFirstTimeReplies = false;
            });
        }
        #endregion

        #region イベント
        protected virtual void OnCheckError(ErrorEventArgs e)
        {
            FireEvent<ErrorEventArgs>(CheckError, e);
        }
        protected virtual void OnTimelineStatusesReceived(StatusesUpdatedEventArgs e)
        {
            FireEvent<StatusesUpdatedEventArgs>(TimelineStatusesReceived, e);
        }
        protected virtual void OnRepliesReceived(StatusesUpdatedEventArgs e)
        {
            FireEvent<StatusesUpdatedEventArgs>(RepliesReceived, e);
        }
        protected virtual void OnDirectMessageReceived(DirectMessageEventArgs e)
        {
            FireEvent<DirectMessageEventArgs>(DirectMessageReceived, e);
        }
        private void FireEvent<T>(EventHandler<T> eventHandler, T e) where T : EventArgs
        {
            if (eventHandler != null)
                eventHandler(this, e);
        }
        #endregion

        #region ユーティリティメソッド
        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static String EncodeMessage(String s)
        {
            return Utility.UrlEncode(s);
        }
        #endregion

        #region Helper Delegate
        private delegate T ExecuteRequestProcedure<T>();
        private delegate void Procedure();

        private T ExecuteRequest<T>(ExecuteRequestProcedure<T> execProc)
        {
            try
            {
                return execProc();
            }
            catch (WebException)
            {
                throw;
            }
            catch (InvalidOperationException ioe)
            {
                // XmlSerializer
                throw new TwitterServiceException(ioe);
            }
            catch (XmlException xe)
            {
                throw new TwitterServiceException(xe);
            }
            catch (IOException ie)
            {
                throw new TwitterServiceException(ie);
            }
        }
        private void ExecuteRequest(Procedure execProc)
        {
            try
            {
                execProc();
            }
            catch (WebException)
            {
                throw;
            }
            catch (InvalidOperationException ioe)
            {
                // XmlSerializer
                throw new TwitterServiceException(ioe);
            }
            catch (XmlException xe)
            {
                throw new TwitterServiceException(xe);
            }
            catch (IOException ie)
            {
                throw new TwitterServiceException(ie);
            }
        }

        /// <summary>
        /// チェックを実行します。例外が発生した場合には自動的にメッセージを送信します。
        /// </summary>
        /// <param name="proc">実行するチェック処理</param>
        /// <returns></returns>
        private Boolean RunCheck(Procedure proc)
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
                    OnCheckError(new ErrorEventArgs(ex));
                    return false;
                }
            }
            catch (TwitterServiceException ex2)
            {
                OnCheckError(new ErrorEventArgs(ex2));
                return false;
            }
            catch (Exception ex3)
            {
                OnCheckError(new ErrorEventArgs(ex3));
                Trace.WriteLine("RunCheck(Unhandled Exception): "+ex3.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// タイマーコールバックの処理を実行します。
        /// </summary>
        /// <param name="timer"></param>
        /// <param name="callbackProcedure"></param>
        private void RunCallback(Timer timer, Procedure callbackProcedure)
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
        #endregion

        #region IDisposable メンバ

        public void Dispose()
        {
            //if (_webClient != null)
            //{
            //    _webClient.Dispose();
            //    _webClient = null;
            //}

            Stop();

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
        }

        #endregion
        
        internal class PreAuthenticatedWebClient : WebClient
        {
            private TwitterService _twitterService;
            public PreAuthenticatedWebClient(TwitterService twitterService)
            {
                _twitterService = twitterService;
            }
            protected override WebRequest GetWebRequest(Uri address)
            {
                // このアプリケーションで HttpWebReqeust 以外がくることはない
                HttpWebRequest webRequest = base.GetWebRequest(address) as HttpWebRequest;
                webRequest.PreAuthenticate = true;
                webRequest.Accept = "text/xml, application/xml";
                webRequest.UserAgent = String.Format("{0}/{1}", _twitterService.ClientName, GetType().Assembly.GetName().Version);
                //webRequest.Referer = TwitterService.Referer;
                webRequest.Headers["X-Twitter-Client"] = _twitterService.ClientName;
                webRequest.Headers["X-Twitter-Client-Version"] = _twitterService.ClientVersion;
                webRequest.Headers["X-Twitter-Client-URL"] = _twitterService.ClientUrl;

                return webRequest;
            }
        }

        /// <summary>
        /// 指定されたURLからデータを取得し文字列として返します。CookieLoginModeが有効なときは自動的にCookieログイン状態で取得します。
        /// </summary>
        /// <param name="url">データを取得するURL</param>
        /// <returns></returns>
        public String GET(String url)
        {
            return GET(url, POSTFetchMode);
        }

        /// <summary>
        /// 指定されたURLからデータを取得し文字列として返します。
        /// </summary>
        /// <param name="url">データを取得するURL</param>
        /// <param name="postFetchMode">POSTで取得するかどうか</param>
        /// <returns></returns>
        public String GET(String url, Boolean postFetchMode)
        {
            if (postFetchMode)
            {
                return POST(url, new Byte[0]);
            }
            else
            {
                url = ServiceServerPrefix + url;
                System.Diagnostics.Trace.WriteLine("GET: " + url);
                HttpWebRequest webRequest = CreateHttpWebRequest(url, "GET");
                HttpWebResponse webResponse = webRequest.GetResponse() as HttpWebResponse;
                using (StreamReader sr = new StreamReader(webResponse.GetResponseStream()))
                    return sr.ReadToEnd();
            }
        }

        public String POST(String url, Byte[] postData)
        {
            url = ServiceServerPrefix + url;
            System.Diagnostics.Trace.WriteLine("POST: " + url);
            HttpWebRequest webRequest = CreateHttpWebRequest(url, "POST");
            using (Stream stream = webRequest.GetRequestStream())
            {
                stream.Write(postData, 0, postData.Length);
            }
            HttpWebResponse webResponse = webRequest.GetResponse() as HttpWebResponse;
            using (StreamReader sr = new StreamReader(webResponse.GetResponseStream()))
                return sr.ReadToEnd();
        }

        protected virtual HttpWebRequest CreateHttpWebRequest(String url, String method)
        {
            HttpWebRequest webRequest = HttpWebRequest.Create(url) as HttpWebRequest;
            //webRequest.Credentials = _credential;
            //webRequest.PreAuthenticate = true;
            webRequest.Proxy = _proxy;
            webRequest.Method = method;
            webRequest.Accept = "text/xml, application/xml, text/html;q=0.5";
            webRequest.UserAgent = String.Format("{0}/{1}", ClientName, ClientVersion);
            //webRequest.Referer = TwitterService.Referer;
            webRequest.Headers["X-Twitter-Client"] = ClientName;
            webRequest.Headers["X-Twitter-Client-Version"] = ClientVersion;
            webRequest.Headers["X-Twitter-Client-URL"] = ClientUrl;

            Uri uri = new Uri(url);

            NetworkCredential cred = _credential.GetCredential(uri, "Basic");
            webRequest.Headers["Authorization"] = String.Format("Basic {0}", Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", cred.UserName, cred.Password))));

            return webRequest as HttpWebRequest;
        }

        #region Cookie アクセス

        private CookieCollection _cookies = null;
        //[Obsolete("Cookieによる認証はサポートされません。代わりにGET(POST)を利用してください。")]
        public String GETWithCookie(String url)
        {
            Boolean isRetry = false;
            url = ServiceServerPrefix + url;
        Retry:
            try
            {
                System.Diagnostics.Trace.WriteLine(String.Format("GET(Cookie): {0}", url));
                return DownloadString(url);
            }
            catch (WebException we)
            {
                HttpWebResponse wResponse = we.Response as HttpWebResponse;
                if (wResponse == null || wResponse.StatusCode != HttpStatusCode.Unauthorized || isRetry)
                    throw;

                _cookies = CookieLogin();

                isRetry = true;
                goto Retry;
            }
        }

        public CookieCollection CookieLogin()
        {
            System.Diagnostics.Trace.WriteLine(String.Format("Cookie Login: {0}", _userName));

            HttpWebRequest request = CreateWebRequest("http://twitter.com/account/verify_credentials.xml") as HttpWebRequest;
            request.AllowAutoRedirect = false;
            request.Method = "GET";

            NetworkCredential cred = _credential.GetCredential(new Uri("http://twitter.com/account/verify_credentials.xml"), "Basic");
            request.Headers["Authorization"] = String.Format("Basic {0}", Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", cred.UserName, cred.Password))));

            HttpWebResponse response = request.GetResponse() as HttpWebResponse;
            using (StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                String responseBody = sr.ReadToEnd();

                if (response.Cookies.Count == 0)
                {
                    throw new ApplicationException("ログインに失敗しました。ユーザ名またはパスワードが間違っている可能性があります。");
                }

                foreach (Cookie cookie in response.Cookies)
                {
                    cookie.Domain = "twitter.com";
                }

                _cookies = response.Cookies;
                
                return response.Cookies;
            }
        }

        WebRequest CreateWebRequest(String uri)
        {
            WebRequest request = WebRequest.Create(uri);
            if (request is HttpWebRequest)
            {
                HttpWebRequest httpRequest = request as HttpWebRequest;
                httpRequest.UserAgent = "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 5.1)";
                httpRequest.Referer = Referer;
                httpRequest.PreAuthenticate = false;
                httpRequest.Accept = "*/*";
                httpRequest.CookieContainer = new CookieContainer();
                httpRequest.Proxy = _proxy;
                if (_cookies != null)
                {
                    httpRequest.CookieContainer.Add(_cookies);
                }
            }
            return request;
        }
           
#if FALSE
        private CookieCollection Login(String userNameOrEmail, String password)
        {
            System.Diagnostics.Trace.WriteLine(String.Format("Cookie Login: {0}", userNameOrEmail));
            using (CookieEnabledWebClient webClient = new CookieEnabledWebClient())
            {
                Byte[] data = webClient.UploadData("https://twitter.com/sessions", Encoding.UTF8.GetBytes(
                    String.Format("username_or_email={0}&password={1}&remember_me=1&commit=Sign%20In", userNameOrEmail, password)
                ));

                String responseBody = Encoding.UTF8.GetString(data);

                if (webClient.Cookies == null)
                {
                    throw new ApplicationException("ログインに失敗しました。ユーザ名またはパスワードが間違っている可能性があります。");
                }

                // XXX: .twitter.com となっていると twitter.com に送られないので書き換える
                foreach (Cookie cookie in webClient.Cookies)
                {
                    cookie.Domain = "twitter.com";
                }

                return webClient.Cookies;
            }
        }
        class CookieEnabledWebClient : WebClient
        {
            public CookieEnabledWebClient()
                : base()
            {
            }
            public CookieEnabledWebClient(CookieCollection cookies)
                : base()
            {
                _cookies = cookies;
            }
            private CookieCollection _cookies;
            public CookieCollection Cookies
            {
                get
                {
                    return _cookies;
                }
                set
                {
                    _cookies = value;
                }
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                if (request is HttpWebRequest)
                {
                    HttpWebRequest httpRequest = request as HttpWebRequest;
                    httpRequest.UserAgent = "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 5.1)";
                    httpRequest.Referer = Referer;
                    httpRequest.PreAuthenticate = false;
                    httpRequest.Accept = "*/*";
                    httpRequest.CookieContainer = new CookieContainer();
                    if (_cookies != null)
                    {
                        httpRequest.CookieContainer.Add(_cookies);
                    }
                }
                return request;
            }

            protected override WebResponse GetWebResponse(WebRequest request)
            {
                WebResponse response = base.GetWebResponse(request);
                if (response is HttpWebResponse)
                {
                    HttpWebResponse httpResponse = response as HttpWebResponse;
                    _cookies = httpResponse.Cookies;
                }
                return response;
            }
        }
#endif
        
        String DownloadString(String url)
        {
            WebRequest request = CreateWebRequest(url);
            WebResponse response = null;
            try
            {
                response = request.GetResponse();
                using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                {
                    return sr.ReadToEnd();
                }
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
            }
#if FALSE
            using (CookieEnabledWebClient webClient = new CookieEnabledWebClient(_cookies))
            {
                return webClient.DownloadString(url);
            }
#endif
        }
        #endregion
    }

    /// <summary>
    /// エラー発生時のイベントのデータを提供します。
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
        public ErrorEventArgs(Exception ex)
        {
            this.Exception = ex;
        }
    }

    /// <summary>
    /// ステータスが更新時のイベントのデータを提供します。
    /// </summary>
    public class StatusesUpdatedEventArgs : EventArgs
    {
        public Statuses Statuses { get; set; }
        public Boolean IsFirstTime { get; set; }
        public Boolean FriendsCheckRequired { get; set; }
        public StatusesUpdatedEventArgs(Statuses statuses)
        {
            this.Statuses = statuses;
        }
        public StatusesUpdatedEventArgs(Statuses statuses, Boolean isFirstTime, Boolean friendsCheckRequired)
            : this(statuses)
        {
            this.IsFirstTime = isFirstTime;
            this.FriendsCheckRequired = friendsCheckRequired;
        }
    }
    /// <summary>
    /// ダイレクトメッセージを受信時のイベントのデータを提供します。
    /// </summary>
    public class DirectMessageEventArgs : EventArgs
    {
        public DirectMessage DirectMessage { get; set; }
        public Boolean IsFirstTime { get; set; }
        public DirectMessageEventArgs(DirectMessage directMessage)
        {
            this.DirectMessage = directMessage;
        }
        public DirectMessageEventArgs(DirectMessage directMessage, Boolean isFirstTime)
            : this(directMessage)
        {
            this.IsFirstTime = isFirstTime;
        }
    }
    /// <summary>
    /// Twitterにアクセスを試みた際にスローされる例外。
    /// </summary>
    public class TwitterServiceException : ApplicationException
    {
        public TwitterServiceException(String message)
            : base(message)
        {
        }
        public TwitterServiceException(Exception innerException)
            : base(innerException.Message, innerException)
        {
        }
        public TwitterServiceException(String message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// データが空を表します。
    /// </summary>
    [XmlRoot("nilclasses")]
    public class NilClasses
    {
        private static Object _syncObject = new object();
        private static XmlSerializer _serializer = null;
        static NilClasses()
        {
            lock (_syncObject)
            {
                if (_serializer == null)
                {
                    _serializer = new XmlSerializer(typeof(NilClasses));
                }
            }
        }
        public static XmlSerializer Serializer
        {
            get
            {
                return _serializer;
            }
        }

        public static Boolean CanDeserialize(String xml)
        {
            return NilClasses.Serializer.CanDeserialize(new XmlTextReader(new StringReader(xml)));
        }
    }

    /// <summary>
    /// DirectMessage のセットを格納します。
    /// </summary>
    [XmlRoot("direct-messages")]
    public class DirectMessages
    {
        [XmlElement("direct_message")]
        public DirectMessage[] DirectMessage;

        private static Object _syncObject = new object();
        private static XmlSerializer _serializer = null;
        static DirectMessages()
        {
            lock (_syncObject)
            {
                if (_serializer == null)
                {
                    _serializer = new XmlSerializer(typeof(DirectMessages));
                }
            }
        }
        public static XmlSerializer Serializer
        {
            get
            {
                return _serializer;
            }
        }    
    }

    /// <summary>
    /// ダイレクトメッセージの情報を表します。
    /// </summary>
    [XmlType("DirectMessage")]
    public class DirectMessage
    {
        [XmlElement("id")]
        public Int32 Id;
        [XmlElement("text")]
        public String _text;
        [XmlElement("sender_id")]
        public String SenderId;
        [XmlElement("recipient_id")]
        public String RecipientId;
        [XmlElement("created_at")]
        public String _createdAt;
        [XmlElement("sender_screen_name")]
        public String SenderScreenName;
        [XmlElement("recipient_screen_name")]
        public String RecipientScreenName;

        [XmlIgnore]
        public String Text
        {
            get
            {
                if (String.IsNullOrEmpty(_text))
                    return String.Empty;

                return Utility.UnescapeCharReference(_text);
            }
        }
        [XmlIgnore]
        public DateTime CreatedAt
        {
            get
            {
                return Utility.ParseDateTime(_createdAt);
            }
        }

        public override string ToString()
        {
            return String.Format("DirectMessage: {0} (ID:{1})", Text, Id.ToString());
        }
    }

    /// <summary>
    /// Statusのセットを格納します。
    /// </summary>
    [XmlType("statuses")]
    public class Statuses
    {
        [XmlElement("status")]
        public Status[] Status;

        private static Object _syncObject = new object();
        private static XmlSerializer _serializer = null;
        static Statuses()
        {
            lock (_syncObject)
            {
                if (_serializer == null)
                {
                    _serializer = new XmlSerializer(typeof(Statuses));
                }
            }
        }

        public static XmlSerializer Serializer
        {
            get
            {
                return _serializer;
            }
        }
    }

    /// <summary>
    /// Userのセットを格納します。
    /// </summary>
    [XmlType("users")]
    public class Users
    {
        [XmlElement("user")]
        public User[] User;

        private static Object _syncObject = new object();
        private static XmlSerializer _serializer = null;
        static Users()
        {
            lock (_syncObject)
            {
                if (_serializer == null)
                {
                    _serializer = new XmlSerializer(typeof(Users));
                }
            }
        }
        public static XmlSerializer Serializer
        {
            get
            {
                return _serializer;
            }
        }
    }

    [XmlType("user")]
    public class User
    {
        [XmlElement("id")]
        public Int32 Id;
        [XmlElement("name")]
        public String Name;
        [XmlElement("screen_name")]
        public String ScreenName;
        [XmlElement("location")]
        public String Location;
        [XmlElement("description")]
        public String Description;
        [XmlElement("profile_image_url")]
        public String ProfileImageUrl;
        [XmlElement("url")]
        public String Url;
        [XmlElement("protected")]
        public Boolean Protected;
        [XmlElement("status")]
        public Status Status;

        private static Object _syncObject = new object();
        private static XmlSerializer _serializer = null;
        static User()
        {
            lock (_syncObject)
            {
                if (_serializer == null)
                {
                    _serializer = new XmlSerializer(typeof(User));
                }
            }
        }
        public static XmlSerializer Serializer
        {
            get
            {
                return _serializer;
            }
        }

        public override string ToString()
        {
            return String.Format("User: {0} / {1} (ID:{2})", ScreenName, Name, Id.ToString());
        }
    }

    /// <summary>
    /// ステータスを表します。
    /// </summary>
    [XmlType("status")]
    public class Status
    {
        [XmlElement("created_at")]
        public String _createdAtOriginal;
        [XmlElement("id")]
        public Int32 Id;
        [XmlElement("in_reply_to_status_id")]
        public String InReplyToStatusId;
        [XmlElement("in_reply_to_user_id")]
        public String InReplyToUserId;
        [XmlElement("text")]
        public String _textOriginal;
        [XmlElement("user")]
        public User User;
        [XmlElement("source")]
        public String Source;
        [XmlElement("favorited")]
        public String Favorited;
        [XmlElement("truncated")]
        public Boolean Truncated;

        [XmlIgnore]
        private String _text;
        [XmlIgnore]
        private DateTime _createdAt;
        
        [XmlIgnore]
        public String Text
        {
            get
            {
                if (!String.IsNullOrEmpty(_textOriginal) && _text == null)
                {
                    _text = Utility.UnescapeCharReference(_textOriginal);
                }

                return _text;
            }
            set
            {
                _text = value;
            }
        }
        [XmlIgnore]
        public DateTime CreatedAt
        {
            get
            {
                if (!String.IsNullOrEmpty(_createdAtOriginal) && _createdAt == DateTime.MinValue)
                {
                    _createdAt = Utility.ParseDateTime(_createdAtOriginal);
                }
                return _createdAt;
            }
            set
            {
                _createdAt = value;
            }
        }

        private static Object _syncObject = new object();
        private static XmlSerializer _serializer = null;
        static Status()
        {
            lock (_syncObject)
            {
                if (_serializer == null)
                {
                    _serializer = new XmlSerializer(typeof(Status));
                }
            }
        }
        public static XmlSerializer Serializer
        {
            get
            {
                return _serializer;
            }
        }

        public override int GetHashCode()
        {
            return this.Id;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Status))
                return false;

            Status status = obj as Status;
            return (status.Id == this.Id) && (status.Text == this.Text);
        }

        public override string ToString()
        {
            return String.Format("Status: {0} (ID:{1})", Text, Id.ToString());
        }
    }
}
