﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;
using System.ComponentModel;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    public class ConsoleAddIn : AddInBase
    {
        public Context CurrentContext { get; set; }
        public Stack<Context> ContextStack { get; set; }
        public String ConsoleChannelName { get { return "#Console";  } }
        public GeneralConfig Config { get; private set; }

        internal List<Type> Contexts { get; private set; }

        public override void Initialize()
        {
            Session.PreMessageReceived += new EventHandler<MessageReceivedEventArgs>(Session_PreMessageReceived);
            Session.PostMessageReceived += new EventHandler<MessageReceivedEventArgs>(Session_PostMessageReceived);

            // Default Context
            CurrentContext = this.GetContext<RootContext>(Server, Session);
            ContextStack = new Stack<Context>();
            Config = Session.AddInManager.GetConfig<GeneralConfig>();
            Contexts = new List<Type>();

            RegisterContext<RootContext>();
            RegisterContext<ConfigContext>();
            RegisterContext<FilterContext>();
            RegisterContext<GroupContext>();
        }

        /// <summary>
        /// IRCメッセージを受け取ってTIG本体に処理が渡る前の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Session_PreMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            PrivMsgMessage privMsg = e.Message as PrivMsgMessage;
            if (privMsg == null || String.Compare(privMsg.Receiver, ConsoleChannelName, true) != 0)
                return;

            ProcessMessage(privMsg);

            // 後続のAddIn,TIG本体には渡さない
            e.Cancel = true;
        }
        
        /// <summary>
        /// IRCメッセージを受け取ってTIG本体が処理を終えた後の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Session_PostMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            JoinMessage joinMsg = e.Message as JoinMessage;
            if (joinMsg == null || String.Compare(joinMsg.Channel, ConsoleChannelName, true) != 0)
                return;
            
            // ここに来るのは初回#Consoleを作成してJOINしたときのみ。
            // 二回目以降はサーバ側がJOINを送り出すのでこない。

            // IsSpecial を True にすることでチャンネルにタイムラインが流れないようにする
            Session.Groups[ConsoleChannelName].IsSpecial = true;

            ShowCommandsAsUsers();
        }
    
        void ProcessMessage(PrivMsgMessage privMsg)
        {
            String msgText = privMsg.Content.Trim();
            String[] args = Regex.Split(msgText, @"(?<!\\)\s");
            if (args.Length == 0)
                return;

            // コンテキスト
            foreach (var ctx in CurrentContext.Contexts)
            {
                if (ctx == typeof(RootContext))
                    continue;

                if (String.Compare(ctx.Name.Replace("Context", ""), args[0], true) == 0)
                {
                    this.PushContext(this.GetContext(ctx, Server, Session));
                    return;
                }
            }
            
            // コマンドを探す
            MethodInfo methodInfo = CurrentContext.GetType().GetMethod(args[0].Replace(":", ""),
                                                                         BindingFlags.Instance | BindingFlags.Public |
                                                                         BindingFlags.IgnoreCase);

            Object[] attrs = (methodInfo == null) ? new object[0] : methodInfo.GetCustomAttributes(typeof(BrowsableAttribute), true);

            if (methodInfo == null || methodInfo.IsFinal || methodInfo.IsConstructor || methodInfo.IsSpecialName || (attrs.Length != 0 && !((BrowsableAttribute)attrs[0]).Browsable))
            {
                NotifyMessage("指定された名前はこのコンテキストのコマンド、またはサブコンテキストにも見つかりません。");
                return;
            }

            try
            {
                ParameterInfo[] paramInfo = methodInfo.GetParameters();
                if (paramInfo.Length == 1 && paramInfo[0].ParameterType == typeof(String))
                {
                    methodInfo.Invoke(CurrentContext, new [] { msgText.Substring(args[0].Length).Trim() });
                }
                else if (paramInfo.Length == 1 && paramInfo[0].ParameterType == typeof(String[]))
                {
                    String[] shiftedArgs = new string[args.Length - 1];
                    Array.Copy(args, 1, shiftedArgs, 0, shiftedArgs.Length);
                    
                    methodInfo.Invoke(CurrentContext, (shiftedArgs.Length == 0 ? null : new [] { shiftedArgs }));
                }
                else
                {
                    List<Object> convertedArgs = new List<object>();
                    Int32 i = 1;
                    foreach (var pi in paramInfo)
                    {
                        if (i == args.Length)
                            break;
                        
                        TypeConverter typeConv = TypeDescriptor.GetConverter(pi.ParameterType);
                        convertedArgs.Add(typeConv.ConvertFromString(args[i++]));
                    }
                    methodInfo.Invoke(CurrentContext, ((convertedArgs.Count != 0) ? convertedArgs.ToArray() : null));
                }
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                    ex = ex.InnerException;
                
                NotifyMessage("コマンドを実行時にエラーが発生しました:");
                foreach (var line in ex.Message.Split('\n'))
                {
                    NotifyMessage(line);
                }
            }
        }

        /// <summary>
        /// クライアントにメッセージをコンテキスト名からのNOTICEで送信します。
        /// </summary>
        /// <param name="message">メッセージ</param>
        public void NotifyMessage(String message)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Context ctx in ContextStack)
                sb.Insert(0, ctx.GetType().Name.Replace("Context", "") + "\\");

            sb.Append(CurrentContext.GetType().Name.Replace("Context", ""));

            NotifyMessage(sb.ToString(), message);
        }
        /// <summary>
        /// クライアントにメッセージをNOTICEで送信します。
        /// </summary>
        /// <param name="senderNick">送信者のニックネーム</param>
        /// <param name="message">メッセージ</param>
        public void NotifyMessage(String senderNick, String message)
        {
            Session.Send(new NoticeMessage(ConsoleChannelName, message)
                             {SenderHost = "twitter@" + Server.ServerName, SenderNick = senderNick});
        }

        /// <summary>
        /// クライアントに対しコマンドをユーザとしてみせます
        /// </summary>
        public void ShowCommandsAsUsers()
        {
            MethodInfo[] methodInfoArr = CurrentContext.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
            Type t = typeof(Context);
            List<String> users = new List<string>();

            foreach (var methodInfo in methodInfoArr)
            {
                if (t.IsAssignableFrom(methodInfo.DeclaringType) && !methodInfo.IsConstructor && !methodInfo.IsFinal && !methodInfo.IsSpecialName)
                {
                    Object[] attrs = methodInfo.GetCustomAttributes(typeof(BrowsableAttribute), true);
                    if (attrs.Length != 0 && !((BrowsableAttribute)attrs[0]).Browsable)
                        continue;

                    users.Add(methodInfo.Name);
                }
            }

            Session.SendNumericReply(NumericReply.RPL_NAMREPLY, "=", ConsoleChannelName, "@"+Session.Nick+" "+ String.Join(" ", users.ToArray()));
            Session.SendNumericReply(NumericReply.RPL_ENDOFNAMES, ConsoleChannelName, "End of NAMES list");
        }
        
        /// <summary>
        /// コンテキストを追加します。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void RegisterContext<T>() where T : Context, new()
        {
            RegisterContext(typeof(T));
        }
        public void RegisterContext(Type contextType)
        {
            if (!Contexts.Contains(contextType))
                Contexts.Add(contextType);
        }

        public Context GetContext<T>(Server server, Session session) where T : Context, new()
        {
            Context ctx = new T { Server = server, Session = session };
            ctx.Initialize();
            return ctx;
        }
        
        public Context GetContext(Type t, Server server, Session session)
        {
            Context ctx = Activator.CreateInstance(t) as Context;
            ctx.Server = server;
            ctx.Session = session;
            ctx.Initialize();
            return ctx;
        }
        #region Context Helpers
        public void ChangeContext(Context ctx)
        {
            ContextStack.Clear();
            CurrentContext = ctx;
            NotifyMessage("コンテキストを変更しました。");
            ShowCommandsAsUsers();
        }

        public void PushContext(Context ctx)
        {
            ContextStack.Push(CurrentContext);
            CurrentContext = ctx;
            NotifyMessage("コンテキストを変更しました。");
            ShowCommandsAsUsers();
        }

        public void PopContext()
        {
            if (ContextStack.Count > 0)
            {
                CurrentContext = ContextStack.Pop();
                NotifyMessage("コンテキストを変更しました。");
                ShowCommandsAsUsers();
            }
        }
        #endregion
    }

}