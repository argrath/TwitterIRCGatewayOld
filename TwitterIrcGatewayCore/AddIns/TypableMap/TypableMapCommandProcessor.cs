using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Misuzilla.Net.Irc;
using TypableMap;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.TypableMap
{
    public class TypableMapCommandProcessor
    {
        public Session Session { get; private set; }
        public TypableMap<Status> TypableMap { get; private set; }
        private Int32 _typableMapKeySize;
        public Int32 TypableMapKeySize
        {
            get
            {
                return _typableMapKeySize;
            }
            set
            {
                if (value < 1)
                    value = 1;

                if (_typableMapKeySize != value)
                {
                    _typableMapKeySize = value;
                    TypableMap = new TypableMap<Status>(_typableMapKeySize);
                }
            }
        }

        private Dictionary<String, ITypableMapCommand> _commands;
        private Regex _matchRE;

        public TypableMapCommandProcessor(TwitterService twitter, Session session, Int32 typableMapKeySize)
        {
            Session = session;
            TypableMap = new TypableMap<Status>(typableMapKeySize);

            _commands = new Dictionary<string, ITypableMapCommand>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var t in typeof(TypableMapCommandProcessor).GetNestedTypes())
            {
                if (typeof(ITypableMapCommand).IsAssignableFrom(t) && t.IsClass)
                {
                    var cmd = Activator.CreateInstance(t) as ITypableMapCommand;
                    AddCommand(cmd);
                }
            }

            UpdateRegex();
        }

        public ITypableMapCommand AddCommand(ITypableMapCommand command)
        {
            _commands[command.CommandName] = command;
            UpdateRegex();
            return command;
        }
        public Boolean RemoveCommand(ITypableMapCommand command)
        {
            Boolean retVal = _commands.Remove(command.CommandName);
            if (_commands.Count != 0)
            {
                UpdateRegex();
            }
            return retVal;
        }

        private void UpdateRegex()
        {
            List<String> keys = new List<string>();
            foreach (var key in _commands.Keys)
                keys.Add(Regex.Escape(key));

            _matchRE = new Regex(@"^\s*(?<cmd>" + (String.Join("|", keys.ToArray())) + @")\s+(?<tid>([aiueokgsztdnhbpmyrwjvlq][aiueo])+)(\s*|\s+(?<args>.*))$", RegexOptions.IgnoreCase);
        }

        public Boolean Process(PrivMsgMessage message)
        {
            if (_commands.Count == 0)
                return false;

            Match m = _matchRE.Match(message.Content);
            if (m.Success)
            {
                Status status;
                if (TypableMap.TryGetValue(m.Groups["tid"].Value, out status))
                {
                    return _commands[m.Groups["cmd"].Value].Process(this, message, status, m.Groups["args"].Value);
                }
            }
            return false;
        }

        public interface ITypableMapCommand
        {
            String CommandName { get; }
            Boolean Process(TypableMapCommandProcessor processor, PrivMsgMessage msg, Status status, String args);
        }

        public class FavCommand : ITypableMapCommand
        {
            public virtual String CommandName { get { return "fav"; } }
            public Boolean Process(TypableMapCommandProcessor processor, PrivMsgMessage msg, Status status, String args)
            {
                Boolean isUnfav = (String.Compare(CommandName, "unfav", true) == 0);
                processor.Session.RunCheck(() =>
                                               {
                                                   Status favStatus = (isUnfav
                                                                           ? processor.Session.TwitterService.DestroyFavorite(
                                                                                 status.Id)
                                                                           : processor.Session.TwitterService.CreateFavorite(
                                                                                 status.Id));
                                                   processor.Session.SendServer(new NoticeMessage
                                                                                    {
                                                                                        Receiver = msg.Receiver,
                                                                                        Content =
                                                                                            String.Format(
                                                                                            "ユーザ {0} のステータス \"{1}\"をFavorites{2}しました。",
                                                                                            favStatus.User.ScreenName,
                                                                                            favStatus.Text,
                                                                                            (isUnfav ? "から削除" : "に追加"))
                                                                                    });
                                               });
                return true;
            }
        }

        public class UnfavCommand : FavCommand
        {
            public override string CommandName
            {
                get
                {
                    return "unfav";
                }
            }
        }

        public class ReCommand : ITypableMapCommand
        {
            #region ITypableMapCommand メンバ

            public string CommandName
            {
                get { return "re"; }
            }

            public Boolean Process(TypableMapCommandProcessor processor, PrivMsgMessage msg, Status status, string args)
            {
                processor.Session.RunCheck(() =>
                                               {
                                                   String replyMsg = String.Format("@{0} {1}", status.User.ScreenName, args);
                                                   processor.Session.TwitterService.UpdateStatus(replyMsg, status.Id);
                                               });
                return true;
            }

            #endregion
        }

    }

}
