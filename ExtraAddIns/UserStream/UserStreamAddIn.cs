using System;
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
        private Thread _workerThread;
        public override void Initialize()
        {
            Session.AddInsLoadCompleted += (sender, e) =>
                                               {
                                                   Session.AddInManager.GetAddIn<ConsoleAddIn>().RegisterContext<UserStreamContext>();
                                                   Setup(Session.AddInManager.GetConfig<UserStreamConfig>().Enabled);
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
                _workerThread.Abort();
                _workerThread.Join();
                _workerThread = null;
            }

            if (isStart)
            {
                _workerThread = new Thread(WorkerProcedure);
                _workerThread.Start();
            }
        }
    
        private void WorkerProcedure()
        {
            try
            {
                FieldInfo fieldInfo = typeof(TwitterService).GetField("_credential", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);

                CredentialCache credentials = fieldInfo.GetValue(CurrentSession.TwitterService) as CredentialCache;
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(_Status));
                HttpWebRequest webRequest = WebRequest.Create("http://betastream.twitter.com/2b/user.json") as HttpWebRequest;
                webRequest.Credentials = credentials.GetCredential(new Uri(CurrentSession.TwitterService.ServiceServerPrefix), "Basic");
                webRequest.PreAuthenticate = true;
                using (var response = webRequest.GetResponse())
                {
                    StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8);

                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine();
                        if (String.IsNullOrEmpty(line))
                            continue;

                        _Status statusJson;
                        try
                        {
                            statusJson = serializer.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(line))) as _Status;
                        }
                        catch
                        {
                            //CurrentSession.SendServerErrorMessage("UserStream(Deserialize): " + line);
                            continue;
                        }

                        if (statusJson == null || statusJson.id == 0)
                            continue;

                        Status status = new Status()
                                            {
                                                CreatedAt = statusJson.CreatedAt,
                                                _textOriginal = statusJson.text,
                                                Source = statusJson.source,
                                                Id = statusJson.id
                                            };
                        User user = new User()
                                        {
                                            Id = (Int32)statusJson.user.id,
                                            Protected = statusJson.user.Protected,
                                            ProfileImageUrl = statusJson.user.profile_image_url,
                                            ScreenName = statusJson.user.screen_name
                                        };
                        status.User = user;
                        Boolean friendCheckRequired = false;
                        CurrentSession.TwitterService.ProcessStatus(status, (s) => CurrentSession.ProcessTimelineStatus(s, ref friendCheckRequired, false, false));
                    }
                }
            }
            catch (Exception e)
            {
                CurrentSession.SendServerErrorMessage("UserStream: " + e.ToString());
            }
        }
    }

    [Description("User Stream設定コンテキストに切り替えます")]
    public class UserStreamContext : Context
    {
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
        public Boolean Enabled;
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
        //public Int64 in_reply_to { get; set; }
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
}
