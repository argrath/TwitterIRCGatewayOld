using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Reflection;
using System.Threading;
using System.ComponentModel;

using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Utilities;

namespace TwitterIrcGatewayCLI
{
    class Program
    {
        static CommandLineParser<CommandLineOptions> CommandLineParser = new CommandLineParser<CommandLineOptions>();
        static void Main(string[] args)
        {
            IPAddress bindAddress = IPAddress.Loopback;
            Encoding encoding = Encoding.GetEncoding("ISO-2022-JP");
            IWebProxy proxy = WebProxy.GetDefaultProxy();

            CommandLineOptions options;
            if (CommandLineParser.TryParse(args, out options))
            {
                // Encoding
                if (String.Compare(options.Encoding, "UTF-8", true) == 0)
                    encoding = new UTF8Encoding(false);
                else
                    encoding = Encoding.GetEncoding(options.Encoding);

                // Listening IP
                if (!IPAddress.TryParse(options.BindAddress, out bindAddress))
                {
                    ShowUsage();
                    return;
                }

                // Proxy
                try
                {
                    if (!String.IsNullOrEmpty(options.Proxy))
                        proxy = new WebProxy(options.Proxy);
                }
                catch (UriFormatException)
                {
                    ShowUsage();
                    return;
                }
            }
            else
            {
                ShowUsage();
                return;
            }

            Server _server = new Server();
            _server.EnableTrace = options.EnableTrace;
            _server.IgnoreWatchError = options.IgnoreWatchError;
            _server.Interval = options.Interval;
            _server.ResolveTinyUrl = options.ResolveTinyurl;
            _server.EnableDropProtection = options.EnableDropProtection;
            _server.Encoding = encoding;
            _server.SetTopicOnStatusChanged = options.SetTopicOnstatuschanged;
            _server.IntervalDirectMessage = options.IntervalDirectmessage;
            _server.CookieLoginMode = options.CookieLoginMode;
            _server.ChannelName = "#"+options.ChannelName;
            _server.EnableRepliesCheck = options.EnableRepliesCheck;
            _server.IntervalReplies = options.IntervalReplies;
            _server.DisableUserList = options.DisableUserlist;
            _server.BroadcastUpdate = options.BroadcastUpdate;
            _server.ClientMessageWait = options.ClientMessageWait;
            _server.BroadcastUpdateMessageIsNotice = options.BroadcastUpdateMessageIsNotice;
            _server.SessionStartedRecieved += new EventHandler<SessionStartedEventArgs>(_server_SessionStartedRecieved);
            _server.Proxy = proxy;

            Console.WriteLine("Start TwitterIrcGateway Server v{0}", typeof(Server).Assembly.GetName().Version);
            Console.WriteLine("[Configuration] BindAddress: {0}, Port: {1}", bindAddress, options.Port);
            Console.WriteLine("[Configuration] EnableTrace: {0}", _server.EnableTrace);
            Console.WriteLine("[Configuration] IgnoreWatchError: {0}", _server.IgnoreWatchError);
            Console.WriteLine("[Configuration] Interval: {0}", _server.Interval);
            Console.WriteLine("[Configuration] ResolveTinyUrl: {0}", _server.ResolveTinyUrl);
            Console.WriteLine("[Configuration] Encoding: {0}", _server.Encoding.EncodingName);
            Console.WriteLine("[Configuration] SetTopicOnStatusChanged: {0}", _server.SetTopicOnStatusChanged);
            Console.WriteLine("[Configuration] EnableDropProtection: {0}", _server.EnableDropProtection);
            Console.WriteLine("[Configuration] IntervalDirectMessage: {0}", _server.IntervalDirectMessage);
            Console.WriteLine("[Configuration] CookieLoginMode: {0}", _server.CookieLoginMode);
            Console.WriteLine("[Configuration] ChannelName: {0}", _server.ChannelName);
            Console.WriteLine("[Configuration] EnableRepliesCheck: {0}", _server.EnableRepliesCheck);
            Console.WriteLine("[Configuration] IntervalReplies: {0}", _server.IntervalReplies);
            Console.WriteLine("[Configuration] DisableUserList: {0}", _server.DisableUserList);
            Console.WriteLine("[Configuration] BroadcastUpdate: {0}", _server.BroadcastUpdate);
            Console.WriteLine("[Configuration] ClientMessageWait: {0}", _server.ClientMessageWait);
            Console.WriteLine("[Configuration] BroadcatUpdateMessageIsNotice: {0}", _server.BroadcastUpdateMessageIsNotice);
            Console.WriteLine("[Configuration] Proxy: {0}", options.Proxy);

            _server.Start(bindAddress, options.Port);

            while (true)
                Thread.Sleep(1000);
        }

        private static void ShowUsage() 
        {
            Console.WriteLine("TwitterIrcGateway Server v{0}", typeof(Server).Assembly.GetName().Version);
            Console.WriteLine(@"Usage:");
            CommandLineParser.ShowHelp();
        }

        static void _server_SessionStartedRecieved(object sender, SessionStartedEventArgs e)
        {
            //Console.WriteLine("[Connect] User: {0}", e.UserName);
        }
    }

    class CommandLineOptions
    {
        [DefaultValue(16668)]
        [Description("IRC server listen port")]
        public Int32 Port { get; set; }

        [DefaultValue("127.0.0.1")]
        [Description("IRC server bind IP address")]
        public String BindAddress { get; set; }

        [DefaultValue(90)]
        [Description("interval of checking Timeline")]
        public Int32 Interval { get; set; }

        [DefaultValue(true)]
        [Description("enable TinyURL resolver")]
        public Boolean ResolveTinyurl { get; set; }

        [DefaultValue("ISO-2022-JP")]
        [Description("IRC message text character encoding")]
        public String Encoding { get; set; }

        [DefaultValue(false)]
        [Description("ignore API error messages")]
        public Boolean IgnoreWatchError { get; set; }

        [DefaultValue(true)]
        [Description("enable drop protection")]
        public Boolean EnableDropProtection { get; set; }

        [DefaultValue(false)]
        [Description("set status as topic on status changed")]
        public Boolean SetTopicOnstatuschanged { get; set; }

        [DefaultValue(false)]
        [Description("enable trace")]
        public Boolean EnableTrace { get; set; }

        [DefaultValue(180)]
        [Description("interval of checking directmessage")]
        public Int32 IntervalDirectmessage { get; set; }

        [DefaultValue(false)]
        [Description("enable cookie-login mode")]
        public Boolean CookieLoginMode { get; set; }

        [DefaultValue("Twitter")]
        [Description("channel name of Twitter timeline")]
        public String ChannelName { get; set; }

        [DefaultValue(false)]
        [Description("enable replies check")]
        public Boolean EnableRepliesCheck { get; set; }

        [DefaultValue(300)]
        [Description("interval of checking Replies")]
        public Int32 IntervalReplies { get; set; }

        [DefaultValue(false)]
        [Description("disable nick/user (following) list")]
        public Boolean DisableUserlist { get; set; }

        [DefaultValue(false)]
        [Description("broadcast status message on updated")]
        public Boolean BroadcastUpdate { get; set; }

        [DefaultValue(0)]
        [Description("wait of send messages to client (milliseconds)")]
        public Int32 ClientMessageWait { get; set; }

        [DefaultValue(false)]
        [Description("broadcast status message type is NOTICE")]
        public Boolean BroadcastUpdateMessageIsNotice { get; set; }

        [DefaultValue("")]
        [Description("HTTP proxy server URL (http://host:port)")]
        public String Proxy { get; set; }
    }
}
