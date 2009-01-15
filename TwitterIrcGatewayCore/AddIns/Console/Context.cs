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

        public virtual Type[] Contexts { get { return new Type[0]; } }
        
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
            ConsoleAddIn.NotifyMessage("[Contexts]");
            foreach (var ctx in this.Contexts)
            {
                if (IsBrowsable(ctx))
                    ConsoleAddIn.NotifyMessage(String.Format("{0} - {1}", ctx.Name.Replace("Context", ""), GetDescription(ctx)));
            }
            
            ConsoleAddIn.NotifyMessage("[Commands]");
            MethodInfo[] methodInfoArr = this.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
            Type t = typeof(Context);
            foreach (var methodInfo in methodInfoArr)
            {
                if (t.IsAssignableFrom(methodInfo.DeclaringType) && !methodInfo.IsConstructor && !methodInfo.IsFinal && !methodInfo.IsSpecialName)
                {
                    if (IsBrowsable(methodInfo))
                        ConsoleAddIn.NotifyMessage(String.Format("{0} - {1}", methodInfo.Name, GetDescription(methodInfo)));
                }
            }
        }

        [Description("コンテキストを一つ前のものに戻します")]
        public void Exit()
        {
            ConsoleAddIn.PopContext();
        }

        #region Context Helpers
        private Boolean IsBrowsable(Type t)
        {
            Object[] attrs = t.GetCustomAttributes(typeof(BrowsableAttribute), true);
            return !(attrs.Length != 0 && !((BrowsableAttribute)attrs[0]).Browsable);
        }
        private Boolean IsBrowsable(MethodInfo mi)
        {
            Object[] attrs = mi.GetCustomAttributes(typeof(BrowsableAttribute), true);
            return !(attrs.Length != 0 && !((BrowsableAttribute)attrs[0]).Browsable);
        }
        private String GetDescription(Type t)
        {
            Object[] attrs = t.GetCustomAttributes(typeof(DescriptionAttribute), true);
            return (attrs.Length == 0) ? "" : ((DescriptionAttribute)attrs[0]).Description;
        }
        private String GetDescription(MethodInfo mi)
        {
            Object[] attrs = mi.GetCustomAttributes(typeof(DescriptionAttribute), true);
            return (attrs.Length == 0) ? "" : ((DescriptionAttribute)attrs[0]).Description;
        }
        #endregion

        #region IDisposable メンバ
        [Browsable(false)]
        public virtual void Dispose()
        {
        }
        #endregion
    }

}
