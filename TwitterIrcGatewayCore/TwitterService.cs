using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using System.Reflection;
using System.IO;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Globalization;
using System.Threading;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public class TwitterService : IDisposable
    {
        //private WebClient _webClient;
        private CredentialCache _credential;
        private IWebProxy _proxy = WebProxy.GetDefaultProxy();
        private String _userName;
        private Boolean _cookieLoginMode = false;

        public static readonly String ServiceServerPrefix = "http://twitter.com";
        public static readonly String Referer = "http://twitter.com/home";
        public static readonly String ClientUrl = "http://www.misuzilla.org/dist/net/twitterircgateway/";
        public static readonly String ClientVersion = typeof(TwitterService).Assembly.GetName().Version.ToString();
        public static readonly String ClientName = "TwitterIrcGateway";

        public TwitterService(String userName, String password)
        {
            CredentialCache credCache = new CredentialCache();
            credCache.Add(new Uri(TwitterService.ServiceServerPrefix), "Basic", new NetworkCredential(userName, password));
            _credential = credCache;

            _userName = userName;

            //_webClient = new PreAuthenticatedWebClient();
            //_webClient = new WebClient();
            //_webClient.Credentials = _credential;
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
        public Boolean CookieLoginMode
        {
            get { return _cookieLoginMode; }
            set { _cookieLoginMode = value; }
        }

        /// <summary>
        /// ステータスを更新します。
        /// </summary>
        /// <param name="message"></param>
        public Status UpdateStatus(String message)
        {
            String encodedMessage = TwitterService.EncodeMessage(message);
            try
            {
                String responseBody = POST(String.Format("/statuses/update.xml?status={0}&source={1}", encodedMessage, TwitterService.ClientName), Encoding.Default.GetBytes("1"));
                if (NilClasses.CanDeserialize(responseBody))
                {
                    return null;
                }
                else
                {
                    Status status = Status.Serializer.Deserialize(new StringReader(responseBody)) as Status;
                    return status;
                }
            }
            catch (WebException we)
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
        /// 指定されたユーザにダイレクトメッセージを送信します。
        /// </summary>
        /// <param name="targetId"></param>
        /// <param name="message"></param>
        public void SendDirectMessage(String targetId, String message)
        {
            String encodedMessage = TwitterService.EncodeMessage(message);
            try
            {
                String responseBody = POST(String.Format("/direct_messages/new.xml?user={0}&text={1}", targetId, encodedMessage), new Byte[0]);
            }
            catch (WebException we)
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
        /// friendsを取得します。
        /// </summary>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public User[] GetFriends()
        {
            try
            {
                String responseBody = GET(String.Format("/statuses/friends.xml", _userName));
                if (NilClasses.CanDeserialize(responseBody))
                {
                    return new User[0];
                }
                else
                {
                    Users users = Users.Serializer.Deserialize(new StringReader(responseBody)) as Users;
                    return (users == null || users.User == null)
                        ? new User[0]
                        : users.User;
                }
            }
            catch (WebException we)
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
        /// userを取得します。
        /// </summary>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public User GetUser(String id)
        {
            try
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
            }
            catch (WebException we)
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
        /// timeline を取得します。
        /// </summary>
        /// <param name="since">最終更新日時</param>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public Statuses GetTimeline(DateTime since)
        {
            try
            {
                String responseBody = GET(String.Format("/statuses/friends_timeline.xml?since={0}", Utility.UrlEncode(since.ToUniversalTime().ToString("r"))));
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
            }
            catch (WebException we)
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
        /// replies を取得します。
        /// </summary>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public Statuses GetReplies()
        {
            try
            {
                String responseBody = GET("/statuses/replies.xml", false);
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
            }
            catch (WebException we)
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
        /// direct messages を取得します。
        /// </summary>
        /// <param name="since">最終更新日時</param>
        /// <exception cref="WebException"></exception>
        /// <exception cref="TwitterServiceException"></exception>
        public DirectMessages GetDirectMessages(DateTime since)
        {
            try
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
            }
            catch (WebException we)
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
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static String EncodeMessage(String s)
        {
            return Utility.UrlEncode(s);
        }

        #region IDisposable メンバ

        public void Dispose()
        {
            //if (_webClient != null)
            //{
            //    _webClient.Dispose();
            //    _webClient = null;
            //}
        }

        #endregion

        internal class PreAuthenticatedWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                // このアプリケーションで HttpWebReqeust 以外がくることはない
                HttpWebRequest webRequest = base.GetWebRequest(address) as HttpWebRequest;
                webRequest.PreAuthenticate = true;
                webRequest.Accept = "text/xml, application/xml";
                webRequest.UserAgent = String.Format("{0}/{1}", TwitterService.ClientName, GetType().Assembly.GetName().Version);
                //webRequest.Referer = TwitterService.Referer;
                webRequest.Headers["X-Twitter-Client"] = TwitterService.ClientName;
                webRequest.Headers["X-Twitter-Client-Version"] = TwitterService.ClientVersion;
                webRequest.Headers["X-Twitter-Client-URL"] = TwitterService.ClientUrl;

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
            return GET(url, CookieLoginMode);
        }

        /// <summary>
        /// 指定されたURLからデータを取得し文字列として返します。
        /// </summary>
        /// <param name="url">データを取得するURL</param>
        /// <param name="cookieLoginMode">Cookieログインで取得するかどうか</param>
        /// <returns></returns>
        public String GET(String url, Boolean cookieLoginMode)
        {
            if (cookieLoginMode)
            {
                return GETWithCookie(url);
            }
            else
            {
                url = TwitterService.ServiceServerPrefix + url;
                System.Diagnostics.Trace.WriteLine("GET: " + url);
                HttpWebRequest webRequest = CreateHttpWebRequest(url, "GET");
                HttpWebResponse webResponse = webRequest.GetResponse() as HttpWebResponse;
                using (StreamReader sr = new StreamReader(webResponse.GetResponseStream()))
                    return sr.ReadToEnd();
            }
        }

        public String POST(String url, Byte[] postData)
        {
            url = TwitterService.ServiceServerPrefix + url;
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
            webRequest.Accept = "text/xml, application/xml";
            webRequest.UserAgent = String.Format("{0}/{1}", TwitterService.ClientName, TwitterService.ClientVersion);
            //webRequest.Referer = TwitterService.Referer;
            webRequest.Headers["X-Twitter-Client"] = TwitterService.ClientName;
            webRequest.Headers["X-Twitter-Client-Version"] = TwitterService.ClientVersion;
            webRequest.Headers["X-Twitter-Client-URL"] = TwitterService.ClientUrl;

            Uri uri = new Uri(url);

            NetworkCredential cred = _credential.GetCredential(uri, "Basic");
            webRequest.Headers["Authorization"] = String.Format("Basic {0}", Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", cred.UserName, cred.Password))));

            return webRequest as HttpWebRequest;
        }

        #region Cookie アクセス

        private CookieCollection _cookies = null;
        public String GETWithCookie(String url)
        {
            Boolean isRetry = false;
            url = TwitterService.ServiceServerPrefix + url;
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

                _cookies = Login(_userName, _credential.GetCredential(new Uri("http://twitter.com"), "Basic").Password);

                isRetry = true;
                goto Retry;
            }
        }

        private CookieCollection Login(String userNameOrEmail, String password)
        {
            System.Diagnostics.Trace.WriteLine(String.Format("Cookie Login: {0}", userNameOrEmail));

            HttpWebRequest request = CreateWebRequest("http://twitter.com/sessions") as HttpWebRequest;
            request.AllowAutoRedirect = false;
            request.Method = "POST";
            using (StreamWriter sw = new StreamWriter(request.GetRequestStream()))
            {
                sw.Write("username_or_email={0}&password={1}&remember_me=1&commit=Sign%20In", userNameOrEmail, password);
            }
            
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

    [XmlRoot("nil-classes")]
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
    }

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
    }

    [XmlType("status")]
    public class Status
    {
        [XmlElement("created_at")]
        public String _createdAt;
        [XmlElement("id")]
        public Int32 Id;
        [XmlElement("text")]
        public String _text;
        [XmlElement("user")]
        public User User;

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
    }
}
