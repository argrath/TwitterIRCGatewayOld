using System;
using System.Reflection;
using Microsoft;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration
{
    public class DLRContextProxy<T> : Context where T : class
    {
        private DLRIntegrationAddIn _dlrAddIn;
        private Object _site;
        private Type _siteType;
        private static Object _scriptType;
        
        public static void RegisterScriptType(Object scriptType)
        {
            _scriptType = scriptType;
        }
        
        public override void Initialize()
        {
            _dlrAddIn = Session.AddInManager.GetAddIn<DLRIntegrationAddIn>();
            _siteType = typeof(T);
            _site = _dlrAddIn.ScriptRuntime.Operations.CreateInstance(_scriptType);
            base.Initialize();
        }
        
        public override MethodInfo GetCommand(string commandName)
        {
            var func = _dlrAddIn.ScriptRuntime.Operations.GetMember<Func<Object, Object>>(_site, commandName);
            if (func != null)
            {
                return func.Method;
            }
            return base.GetCommand(commandName);
        }
    }
}
