using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;
using System.IO;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration
{
    public class DLRIntegrationAddIn : AddInBase
    {
        private ScriptRuntime _scriptRuntime;
        private ScriptScope _scriptScope;

        public event EventHandler BeforeUnload;

        public override void Initialize()
        {
            Session.AddInsLoadCompleted += (sender, e) =>
            {
                Session.AddInManager.GetAddIn<ConsoleAddIn>().RegisterContext<DLRContext>();
                ReloadScripts((fileName, ex) => { Trace.WriteLine("Script Execute: " + fileName); if (ex != null) { Trace.WriteLine(ex.ToString()); } });
            };
        }

        public override void Uninitialize()
        {
            Shutdown();
        }
        
        private void Shutdown()
        {
            if (_scriptRuntime != null)
            {
                if (BeforeUnload != null)
                    BeforeUnload(this, EventArgs.Empty);

                _scriptRuntime.Shutdown();
                _scriptRuntime = null;
                BeforeUnload = null;
            }
        }
        
        public Object Eval(String languageName, String expression)
        {
            ScriptEngine engine = _scriptRuntime.GetEngine(languageName);
            return engine.CreateScriptSourceFromString(expression, SourceCodeKind.Statements).Execute(_scriptScope);
        }
    
        public void ReloadScripts(ScriptExecutionCallback scriptExecutionCallback)
        {
            SessionProxy.RemoveAllEvents();
            ServerProxy.RemoveAllEvents();
            
            Shutdown();

            ScriptRuntimeSetup scriptRuntimeSetup = new ScriptRuntimeSetup();
            scriptRuntimeSetup.LanguageSetups.Add(new LanguageSetup("IronPython.Runtime.PythonContext, IronPython", "IronPython 2.0", new[] { "IronPython", "Python", "py" }, new[] { ".py" }));
            scriptRuntimeSetup.LanguageSetups.Add(new LanguageSetup("IronRuby.Runtime.RubyContext, IronRuby", "IronRuby 1.0 Alpha", new[] { "IronRuby", "Ruby", "rb" }, new[] { ".rb" }));
            _scriptRuntime = ScriptRuntime.CreateRemote(AppDomain.CurrentDomain, scriptRuntimeSetup);
            
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                _scriptRuntime.LoadAssembly(asm);
            
            _scriptScope = _scriptRuntime.CreateScope();
            _scriptScope.SetVariable("Session", Session);
            _scriptScope.SetVariable("Server", Server);
            _scriptRuntime.Globals.SetVariable("Session", Session);
            _scriptRuntime.Globals.SetVariable("Server", Server);

            if (Directory.Exists(Path.Combine(Session.UserConfigDirectory, "Scripts")))
            {
                foreach (var path in Directory.GetFiles(Path.Combine(Session.UserConfigDirectory, "Scripts"), "*.*", SearchOption.AllDirectories))
                {
                    ScriptEngine engine;
                    if (_scriptRuntime.TryGetEngineByFileExtension(Path.GetExtension(path), out engine))
                    {
                        try
                        {
                            engine.ExecuteFile(path, _scriptScope);
                            scriptExecutionCallback(path, null);
                        }
                        catch (Exception ex)
                        {
                            scriptExecutionCallback(path, ex);
                        }
                    }
                }
            }
        }

        public delegate void ScriptExecutionCallback(String fileName, Exception e);
    }
    
    [Description("DLR統合 コンテキストに切り替えます")]
    public class DLRContext : Context
    {
        [Description("スクリプトを再読み込みします")]
        public void Reload()
        {
            Session.AddInManager.GetAddIn<DLRIntegrationAddIn>().ReloadScripts((fileName, ex) =>
                                                                               {
                                                                                   ConsoleAddIn.NotifyMessage("ファイル " + fileName + " を読み込みました。");
                                                                                   if (ex != null)
                                                                                   {
                                                                                        ConsoleAddIn.NotifyMessage("実行時にエラーが発生しました:");
                                                                                        ConsoleAddIn.NotifyMessage(ex.Message);
                                                                                   }
                                                                               });
        }
#if DEBUG
        [Description("現在のスクリプトスコープでスクリプトを評価します")]
        public void Eval([Description("言語名またはスクリプトエンジンの名前")]
                         String languageName,
                         [Description("評価する式")]
                         String expression)
        {
            Object retVal = Session.AddInManager.GetAddIn<DLRIntegrationAddIn>().Eval(languageName, expression);
            ConsoleAddIn.NotifyMessage(retVal == null ? "(null)" : retVal.ToString());
        }
#endif
    }
}
