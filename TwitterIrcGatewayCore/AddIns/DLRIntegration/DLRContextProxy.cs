using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;
using System.ComponentModel;
using Microsoft.Scripting.Hosting;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration
{
    public static class DLRContextHelper
    {
        public static Type Wrap(String contextName, Object dlrContextType)
        {
            Type genCtxType = typeof(DLRContextBase<>).MakeGenericType(dlrContextType.GetType());
            return genCtxType.InvokeMember("GetProxyType", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod, null, null, new Object[] {contextName, dlrContextType}) as Type;
        }
    }

    [Obsolete("DLRContextHelperを利用してください。")]
    public class DLRContext<T> : Context where T : class
    {
        [Obsolete("DLRContextHelper.Wrap を利用してください。")]
        public static Type GetProxyType(String contextName, Object scriptType)
        {
            return DLRContextBase<T>.GetProxyType(contextName, scriptType);
        }
    }
    
    public class DLRContextBase<T> : Context where T : class
    {
        private DLRIntegrationAddIn _dlrAddIn;
        private ScriptRuntime _scriptRuntime;
        private Context _site;
        private static Object _scriptType;
        private static String _contextName;

        public override string ContextName { get { return _contextName; } }
        
        internal static Type GetProxyType(String contextName, Object scriptType)
        {
            _scriptType = scriptType;
            _contextName = contextName;
            return typeof(DLRContextBase<T>);
        }
        
        public override void Initialize()
        {
            _dlrAddIn = Session.AddInManager.GetAddIn<DLRIntegrationAddIn>();
            _scriptRuntime = _dlrAddIn.ScriptRuntime;
            _site = _scriptRuntime.Operations.CreateInstance(_scriptType) as Context;
            if (_site == null)
                throw new ArgumentException("指定された型はContext クラスを継承していないためインスタンス化できません。");
            
            _site.Server = Server;
            _site.Session = Session;
            _site.Console = Console;
            _site.Initialize();

            base.Initialize();
        }

        private Func<Object, Object> _func;

        public override IDictionary<string, string> GetCommands()
        {
            var commands =_site.GetCommands();
            // いくつか削除する
            commands.Remove("OnConfigurationChanged");
            commands.Remove("Equals");
            commands.Remove("MemberwiseClone");
            commands.Remove("ToString");
            commands.Remove("GetHashCode");
            return commands;
        }

        public override bool OnCallMissingCommand(string commandName, string rawInputLine)
        {
            return _site.OnCallMissingCommand(commandName, rawInputLine);
        }

        public override void Dispose()
        {
            _site.Dispose();
        }
        
        public override MethodInfo GetCommand(string commandName)
        {
            try
            {
                var commandNameNormalized = commandName;
                var memberNames = _scriptRuntime.Operations.GetMemberNames(_site);
                // なぜかGetMemberのIgnoreCaseがきかないのでがんばる
                foreach (var memberName in memberNames)
                {
                    if (String.Compare(memberName, commandName, true) == 0)
                        commandNameNormalized = memberName;
                }
                var func = _scriptRuntime.Operations.GetMember<Func<Object, Object>>(_site, commandNameNormalized, true);
                
                if (func != null)
                {
                    _func = func;
                    return MethodInfo;
                }
            }
            catch (Exception e)
            {
            }
            return base.GetCommand(commandName);
        }

        public MethodInfo MethodInfo
        {
            get { return this.GetType().GetMethod("__WrapMethod__"); }
        }

        [Browsable(false)]
        public Object @__WrapMethod__(String args)
        {
            return _func(args);
        }
    }
}
