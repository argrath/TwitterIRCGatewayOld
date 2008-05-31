using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Misuzilla.Net.Irc;
using System.IO;
using System.Xml;

namespace Misuzilla.Applications.TwitterIrcGateway.Filter
{
    [XmlInclude(typeof(Drop))]
    [XmlInclude(typeof(Redirect))]
    [XmlInclude(typeof(RewriteContent))]
    public class Filters
    {
        public Filters()
        {
            _items = new List<FilterItem>();
        }
        
        private static Object _syncObject = new object();
        private static XmlSerializer _serializer = null;
        static Filters()
        {
            lock (_syncObject)
            {
                if (_serializer == null)
                {
                    _serializer = new XmlSerializer(typeof(Filters));
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

        public void Add(FilterItem item)
        {
            _items.Add(item);
        }

        private List<FilterItem> _items;
        public FilterItem[] Items
        {
            get { return _items.ToArray(); }
            set { _items.AddRange(value); }
        }

        /// <summary>
        /// メッセージをフィルタします
        /// </summary>
        /// <param name="user"></param>
        /// <param name="message"></param>
        /// <returns>メッセージを捨てるかどうか</returns>
        public Boolean ExecuteFilters(FilterArgs args)
        {
            Trace.WriteLine(String.Format("Filter: User: {0} / Message: {1}",args.User.ScreenName, args.Content.Replace('\n', ' ')));
            foreach (FilterItem item in _items)
            {
                if (!item.Enabled)
                    continue;

                item.Execute(args);

                if (args.Drop)
                {
                    Trace.WriteLine(String.Format("  => DROP", item.GetType().Name, args.User.ScreenName, args.Content.Replace('\n', ' ')));
                    return false;
                }
                Trace.WriteLine(String.Format("  => {0} / User: {1} / Message: {2}", item.GetType().Name, args.User.ScreenName, args.Content.Replace('\n', ' ')));
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static Filters Load(String path)
        {
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
                                foreach (FilterItem item in filters.Items)
                                {
                                    Trace.WriteLine(String.Format(" - Filter:{0}", item.ToString()));
                                }
                                return filters;
                            }
                        }
                        catch (XmlException xe) { Trace.WriteLine(xe.Message); }
                        catch (InvalidOperationException ioe) { Trace.WriteLine(ioe.Message); }
                    }
                }
                catch (IOException ie)
                {
                    Trace.WriteLine(ie.Message);
                    throw;
                }
            }
            return new Filters();
        }
    }

    public abstract class FilterItem
    {
        private Boolean _enabled = true;
        [XmlAttribute]
        public Boolean Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        public abstract void Execute(FilterArgs args);
    }

    public class FilterArgs
    {
        public String Content;
        public User User;
        public String IRCMessageType;
        public Boolean Drop;
        public Session Session;

        public FilterArgs(Session session, String content, User user, String ircMessageType, Boolean drop)
        {
            this.Session = session;
            this.Content = content;
            this.User = user;
            this.IRCMessageType = ircMessageType;
            this.Drop = drop;
        }
    }

    public class Drop : FilterItem
    {
        private String _matchPattern = "";
        public String MatchPattern
        {
            get { return _matchPattern; }
            set { _matchPattern = value; }
        }
        
        private String _userMatchPattern = "";
        public String UserMatchPattern
        {
            get { return _userMatchPattern; }
            set { _userMatchPattern = value; }
        }

        public override void Execute(FilterArgs args)
        {
            if (!String.IsNullOrEmpty(_matchPattern))
            {
                args.Drop =
                    Regex.IsMatch(args.Content, _matchPattern, RegexOptions.IgnoreCase) &&
                    ((String.IsNullOrEmpty(_userMatchPattern)) ? true : Regex.IsMatch(args.User.ScreenName, _userMatchPattern));
            }
        }
        public override string ToString()
        {
            return "Drop:"
                + ((Enabled) ? "" : "[DISABLED]")
                + ((String.IsNullOrEmpty(_userMatchPattern)) ? "" : String.Format(" UserMatchPattern={0}", _userMatchPattern))
                + ((String.IsNullOrEmpty(_matchPattern)) ? "" : String.Format(" MatchPattern={0}", _matchPattern))
            ;
        }
    }

    public class RewriteContent : FilterItem
    {
        private String _replacePattern = "";
        public String ReplacePattern
        {
            get { return _replacePattern; }
            set { _replacePattern = value; }
        }

        private String _matchPattern = "";
        public String MatchPattern
        {
            get { return _matchPattern; }
            set { _matchPattern = value; }
        }

        private String _userMatchPattern = "";
        public String UserMatchPattern
        {
            get { return _userMatchPattern; }
            set { _userMatchPattern = value; }
        }

        private String _messageType = "PRIVMSG";
        public String MessageType
        {
            get { return _messageType; }
            set { _messageType = value; }
        }

        public override void Execute(FilterArgs args)
        {
            if (!String.IsNullOrEmpty(_matchPattern))
            {
                if (Regex.IsMatch(args.Content, _matchPattern, RegexOptions.IgnoreCase) &&
                    ((String.IsNullOrEmpty(_userMatchPattern)) ? true : Regex.IsMatch(args.User.ScreenName, _userMatchPattern)))
                {
                    if (!String.IsNullOrEmpty(_replacePattern))
                    {
                        args.Content = Regex.Replace(args.Content, _matchPattern, _replacePattern, RegexOptions.IgnoreCase);
                    }

                    args.IRCMessageType = _messageType;
                }
            }
        }

        public override string ToString()
        {
            return "RewriteContent:"
                + ((Enabled) ? "" : "[DISABLED]")
                + ((String.IsNullOrEmpty(_userMatchPattern)) ? "" : String.Format(" UserMatchPattern={0}", _userMatchPattern))
                + ((String.IsNullOrEmpty(_messageType)) ? "" : String.Format(" MessageType={0}", _messageType))
                + ((String.IsNullOrEmpty(_matchPattern)) ? "" : String.Format(" MatchPattern={0}", _matchPattern))
                + ((String.IsNullOrEmpty(_replacePattern)) ? "" : String.Format(" ReplacePattern={0}", _replacePattern))
            ;
        }
    }
    
    public class Redirect : FilterItem
    {
        private String _matchPattern = "";
        public String MatchPattern
        {
            get { return _matchPattern; }
            set { _matchPattern = value; }
        }

        private String _userMatchPattern = "";
        public String UserMatchPattern
        {
            get { return _userMatchPattern; }
            set { _userMatchPattern = value; }
        }

        private String _channelName = "";
        public String ChannelName
        {
            get { return _channelName; }
            set { _channelName = value; }
        }

        private Boolean _duplicate = true;
        public Boolean Duplicate
        {
            get { return _duplicate; }
            set { _duplicate = value; }
        }

        public override void Execute(FilterArgs args)
        {
            if (String.IsNullOrEmpty(_channelName))
                return;

            if (!String.IsNullOrEmpty(_matchPattern))
            {
                Boolean rerouteRequired =
                    Regex.IsMatch(args.Content, _matchPattern, RegexOptions.IgnoreCase) &&
                    ((String.IsNullOrEmpty(_userMatchPattern)) ? true : Regex.IsMatch(args.User.ScreenName, _userMatchPattern));
                
                if (!rerouteRequired)
                    return;
                
                IRCMessage msg;
                switch (args.IRCMessageType.ToUpperInvariant())
                {
                    case "NOTICE":
                        msg = new NoticeMessage(_channelName, args.Content);
                        break;
                    case "PRIVMSG":
                    default:
                        msg = new PrivMsgMessage(_channelName, args.Content);
                        break;
                }
                msg.SenderNick = args.User.ScreenName;
                msg.SenderHost = "twitter@" + Server.ServerName;
                args.Session.Send(msg);
                
                if (!_duplicate)
                    args.Drop = true;
            }
        }
        public override string ToString()
        {
            return "Redirect:"
                + ((Enabled) ? "" : "[DISABLED]")
                + ((String.IsNullOrEmpty(_userMatchPattern)) ? "" : String.Format(" UserMatchPattern={0}", _userMatchPattern))
                + ((String.IsNullOrEmpty(_matchPattern)) ? "" : String.Format(" MatchPattern={0}", _matchPattern))
                + ((String.IsNullOrEmpty(_channelName)) ? "" : String.Format(" ChannelName={0}", _channelName))
                + ((_duplicate) ? " Duplicate" : "")
            ;
        }
    }

}
