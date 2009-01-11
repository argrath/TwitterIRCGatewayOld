using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class Context : IDisposable
    {
        [Browsable(false)]
        public Session Session { get; set; }
        [Browsable(false)]
        public Server Server { get; set; }
        [Browsable(false)]
        public ConsoleAddIn ConsoleAddIn { get { return Session.AddInManager.GetAddIn<Console.ConsoleAddIn>(); } }

        public static Context GetContext<T>(Server server, Session session) where T : Context, new()
        {
            Context ctx = new T { Server = server, Session = session };
            ctx.Initialize();
            return ctx;
        }
        
        /// <summary>
        /// 
        /// </summary>
        [Browsable(false)]
        public virtual void Initialize()
        {
        }

        [Description("コマンドの一覧を表示します")]
        public void Help(String commandName)
        {
            MethodInfo[] methodInfoArr = this.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
            Type t = typeof(Context);
            foreach (var methodInfo in methodInfoArr)
            {
                if (t.IsAssignableFrom(methodInfo.DeclaringType) && !methodInfo.IsConstructor && !methodInfo.IsFinal && !methodInfo.IsSpecialName)
                {
                    Object[] attrs = methodInfo.GetCustomAttributes(typeof(BrowsableAttribute), true);
                    if (attrs.Length != 0 && !((BrowsableAttribute)attrs[0]).Browsable)
                        continue;

                    attrs = methodInfo.GetCustomAttributes(typeof(DescriptionAttribute), true);
                    String desc = (attrs.Length == 0) ? "" : ((DescriptionAttribute)attrs[0]).Description;

                    ConsoleAddIn.NotifyMessage(String.Format("{0} - {1}", methodInfo.Name, desc));
                }
            }
        }

        [Description("コンテキストを一つ前のものに戻します")]
        public void Exit()
        {
            ConsoleAddIn.PopContext();
        }

        #region Context Helpers

        #endregion

        #region IDisposable メンバ
        [Browsable(false)]
        public virtual void Dispose()
        {
        }
        #endregion
    }

}
