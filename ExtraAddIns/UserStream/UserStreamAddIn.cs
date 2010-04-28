﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Threading;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;
using System.Runtime.Serialization.Json;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.UserStream
{
    public class UserStreamAddIn : AddInBase
    {
        [ThreadStatic]
        private static IPEndPoint _localIPEndpoint;

        private HashSet<Int64> _friendIds;

        private Thread _workerThread;
        private Boolean _isRunning;
        private HttpWebRequest _webRequest;

        public UserStreamConfig Config { get; set; }

        public override void Initialize()
        {
            Session.AddInsLoadCompleted += (sender, e) =>
                                               {
                                                   Session.AddInManager.GetAddIn<ConsoleAddIn>().RegisterContext<UserStreamContext>();
                                                   Config = Session.AddInManager.GetConfig<UserStreamConfig>();
                                                   Setup(Config.Enabled);
                                               };
        }
        public override void Uninitialize()
        {
            Setup(false);
        }
    
        internal void Setup(Boolean isStart)
        {
            if (_workerThread != null)
            {
                _isRunning = false;
                
                if (_webRequest != null)
                {
                    _webRequest.Abort();
                    _webRequest = null;
                }

                _workerThread.Abort();
                _workerThread.Join(200);
                _workerThread = null;
            }

            if (isStart)
            {
                _friendIds = new HashSet<Int64>();
                _workerThread = new Thread(WorkerProcedure);
                _workerThread.Start();
                _isRunning = true;
            }
        }
    
        private void WorkerProcedure()
        {
            try
            {
                String ipEndpoint = Config.IPEndPoint;
                _localIPEndpoint = (ipEndpoint == null) ? null : new IPEndPoint(IPAddress.Parse(ipEndpoint), 0);

                FieldInfo fieldInfo = typeof(TwitterService).GetField("_credential", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);

                CredentialCache credentials = fieldInfo.GetValue(CurrentSession.TwitterService) as CredentialCache;
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(_Status));
                DataContractJsonSerializer serializer2 = new DataContractJsonSerializer(typeof(_FriendsObject));
                DataContractJsonSerializer serializer3 = new DataContractJsonSerializer(typeof(_EventObject));

                _webRequest = WebRequest.Create("http://betastream.twitter.com/2b/user.json") as HttpWebRequest;
                _webRequest.Credentials = credentials.GetCredential(new Uri(CurrentSession.TwitterService.ServiceServerPrefix), "Basic");
                _webRequest.PreAuthenticate = true;
                _webRequest.ServicePoint.ConnectionLimit = 1000;
                _webRequest.ServicePoint.BindIPEndPointDelegate = (servicePoint, remoteEndPoint, retryCount) => { return _localIPEndpoint; };
                using (var response = _webRequest.GetResponse())
                using (var stream = response.GetResponseStream())
                {
                    stream.ReadTimeout = 30*1000;

                    StreamReader sr = new StreamReader(stream, Encoding.UTF8);
                    Boolean isFirstLine = true;
                    while (!sr.EndOfStream && _isRunning)
                    {
                        var line = sr.ReadLine();
                        if (String.IsNullOrEmpty(line))
                            continue;

                        _Status statusJson = null;
                        try
                        {
                            // XXX: これはてぬき
                            if (isFirstLine)
                            {
                                isFirstLine = false;
                                _FriendsObject streamObject =
                                    serializer2.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(line))) as
                                    _FriendsObject;
                                if (streamObject != null && streamObject.friends != null)
                                {
                                    _friendIds.UnionWith(streamObject.friends);
                                }
                            }
                            else if (line.IndexOf("\"event\":") > -1)
                            {
                                _EventObject eventObj =
                                    serializer3.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(line))) as _EventObject;

                                if (eventObj.Event == "follow" && eventObj.source.id == CurrentSession.TwitterUser.Id)
                                    _friendIds.Add(eventObj.target.id);
                            }
                            else
                            {
                                statusJson =
                                    serializer.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(line))) as _Status;
                            }

                        }
                        catch
                        {
                            //CurrentSession.SendServerErrorMessage("UserStream(Deserialize): " + line);
                            continue;
                        }

                        if (statusJson == null || statusJson.id == 0)
                            continue;

                        if (Config.IsThroughMyPostFromUserStream && (statusJson.user.id == CurrentSession.TwitterUser.Id))
                            continue;

                        Status status = new Status()
                                            {
                                                CreatedAt = statusJson.CreatedAt,
                                                _textOriginal = statusJson.text,
                                                Source = statusJson.source,
                                                Id = statusJson.id,
                                                InReplyToUserId =
                                                    statusJson.in_reply_to_user_id.HasValue
                                                        ? statusJson.in_reply_to_user_id.Value.ToString()
                                                        : null
                                            };
                        User user = new User()
                                        {
                                            Id = (Int32) statusJson.user.id,
                                            Protected = statusJson.user.Protected,
                                            ProfileImageUrl = statusJson.user.profile_image_url,
                                            ScreenName = statusJson.user.screen_name
                                        };
                        status.User = user;
                        Boolean friendCheckRequired = false;
                        if (Config.AllAtMode ||
                            (statusJson.in_reply_to_user_id.HasValue == false) ||
                            (statusJson.in_reply_to_user_id.HasValue && _friendIds.Contains(statusJson.in_reply_to_user_id.Value)))
                        {
                            CurrentSession.TwitterService.ProcessStatus(status,
                                                                        (s) =>
                                                                        CurrentSession.ProcessTimelineStatus(s,
                                                                                                             ref friendCheckRequired,
                                                                                                             false,
                                                                                                             false));
                        }
                    }
                }
            }
            catch (ThreadAbortException)
            {}
            catch (Exception e)
            {
                CurrentSession.SendServerErrorMessage("UserStream: " + e.ToString());
            }
            finally
            {
                _isRunning = false;
            }
        }
    }

    [Description("User Stream設定コンテキストに切り替えます")]
    public class UserStreamContext : Context
    {
        public override IConfiguration[] Configurations
        {
            get
            {
                return new[] { CurrentSession.AddInManager.GetAddIn<UserStreamAddIn>().Config };
            }
        }

        [Description("User Stream を有効にします")]
        public void Enable()
        {
            var config = CurrentSession.AddInManager.GetConfig<UserStreamConfig>();
            config.Enabled = true;
            CurrentSession.AddInManager.SaveConfig(config);
            CurrentSession.AddInManager.GetAddIn<UserStreamAddIn>().Setup(config.Enabled);
            Console.NotifyMessage("User Stream を有効にしました。");
        }
        [Description("User Stream を無効にします")]
        public void Disable()
        {
            var config = CurrentSession.AddInManager.GetConfig<UserStreamConfig>();
            config.Enabled = false;
            CurrentSession.AddInManager.SaveConfig(config);
            CurrentSession.AddInManager.GetAddIn<UserStreamAddIn>().Setup(config.Enabled);
            Console.NotifyMessage("User Stream を無効にしました。");
        }
    }
    
    public class UserStreamConfig : IConfiguration
    {
        [Browsable(false)]
        public Boolean Enabled { get; set; }

        [Browsable(false)]
        public String IPEndPoint { get; set; }

        [Description("all@と同じ挙動になるかどうかを指定します。")]
        public Boolean AllAtMode { get; set; }

        [Description("自分のポストをUser Streamから拾わないようにするかどうかを指定します。")]
        public Boolean IsThroughMyPostFromUserStream { get; set; }

//        [Description("切断された際に自動的に再接続を試みるかどうかを指定します。")]
//        public Boolean AutoRestart { get; set; }
    }

    [DataContract]
    class _Status
    {
        [DataMember]
        public Int64 id { get; set; }
        [DataMember]
        public String text { get; set; }
        [DataMember]
        public String created_at { get; set; }
        [DataMember]
        public String source { get; set; }
        [DataMember]
        public _User user { get; set; }

        public DateTime CreatedAt { get { return DateTime.ParseExact(created_at, "ddd MMM dd HH:mm:ss zz00 yyyy", CultureInfo.InvariantCulture.DateTimeFormat); } }

        [DataMember]
        public Int64? in_reply_to { get; set; }

        [DataMember]
        public Int64? in_reply_to_user_id { get; set; }

    }

    [DataContract]
    class _EventTarget
    {
        [DataMember]
        public Int64 id { get; set; }
    }

    [DataContract]
    class _User
    {
        [DataMember]
        public Int64 id { get; set; }
        [DataMember]
        public String screen_name { get; set; }
        [DataMember]
        public String profile_image_url { get; set; }
        [DataMember(Name = "protected")]
        public Boolean Protected { get; set; }
    }

    [DataContract]
    class _FriendsObject
    {
        [DataMember]
        public List<Int64> friends { get; set; }
    }

    [DataContract]
    class _EventObject
    {
        [DataMember(Name = "event")]
        public String Event { get; set; }
        [DataMember]
        public _EventTarget target { get; set; }
        [DataMember]
        public _EventTarget source { get; set; }
    }
}
