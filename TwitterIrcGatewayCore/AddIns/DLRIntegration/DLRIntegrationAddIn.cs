using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using IronRuby.Builtins;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration
{
    public class DLRIntegrationAddIn : AddInBase
    {
        private ScriptRuntime _scriptRuntime;
        private Dictionary<String, ScriptScope> _scriptScopes;

        public event EventHandler BeforeUnload;

        internal IDictionary<String, ScriptScope> ScriptScopes { get { return _scriptScopes; } }
        internal ScriptRuntime ScriptRuntime { get { return _scriptRuntime; } }

        public override void Initialize()
        {
            CurrentSession.AddInsLoadCompleted += (sender, e) =>
            {
                CurrentSession.AddInManager.GetAddIn<ConsoleAddIn>().RegisterContext<DLRContext>();
                ReloadScripts((fileName, ex) => { Trace.WriteLine("Script Executed: " + fileName); if (ex != null) { Trace.WriteLine(ex.ToString()); } });
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
                {
                    // アンロード時に出るExceptionはとりあえず全部握りつぶす
                    foreach (EventHandler handler in BeforeUnload.GetInvocationList())
                    {
                        try
                        {
                            handler.Invoke(this, EventArgs.Empty);
                        }
                        catch (Exception e)
                        {
                            Trace.WriteLine("Exception at BeforeUnload(Ignore): "+e.Message);
                        }
                    }
                }

                _scriptRuntime.Shutdown();
                _scriptRuntime = null;
                BeforeUnload = null;
            }
        }
        
        public Object Eval(String languageName, String expression)
        {
            ScriptEngine engine = _scriptRuntime.GetEngine(languageName);
            return engine.CreateScriptSourceFromString(expression, SourceCodeKind.Statements).Execute(_scriptScopes["*Eval*"]);
        }
    
        public void ReloadScripts(ScriptExecutionCallback scriptExecutionCallback)
        {
            SessionProxy.RemoveAllEvents();
            ServerProxy.RemoveAllEvents();
            
            Shutdown();

            ScriptRuntimeSetup scriptRuntimeSetup = new ScriptRuntimeSetup();
            scriptRuntimeSetup.LanguageSetups.Add(new LanguageSetup("IronPython.Runtime.PythonContext, IronPython", "IronPython 2.6 Alpha", new[] { "IronPython", "Python", "py" }, new[] { ".py" }));
            scriptRuntimeSetup.LanguageSetups.Add(new LanguageSetup("IronRuby.Runtime.RubyContext, IronRuby", "IronRuby 1.0 Alpha", new[] { "IronRuby", "Ruby", "rb" }, new[] { ".rb" }));
            scriptRuntimeSetup.LanguageSetups[0].Options.Add("PythonLibraryPaths", @"Libraries\IronPython".Split(';'));
            scriptRuntimeSetup.LanguageSetups[1].Options.Add("LibraryPaths", @"Libraries\IronRuby\IronRuby;Libraries\IronRuby\ruby;Libraries\IronRuby\ruby\site_ruby;Libraries\IronRuby\ruby\site_ruby\1.8;Libraries\IronRuby\ruby\1.8".Split(';'));
            scriptRuntimeSetup.LanguageSetups[1].Options["KCode"] = RubyEncoding.KCodeUTF8;
            scriptRuntimeSetup.LanguageSetups[1].ExceptionDetail = true;
            _scriptRuntime = ScriptRuntime.CreateRemote(AppDomain.CurrentDomain, scriptRuntimeSetup);
            
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                _scriptRuntime.LoadAssembly(asm);
            
            _scriptScopes = new Dictionary<string, ScriptScope>();
            PrepareScriptScopeByPath("*Eval*");
            _scriptRuntime.Globals.SetVariable("Session", CurrentSession);
            _scriptRuntime.Globals.SetVariable("Server", CurrentServer);
            _scriptRuntime.Globals.SetVariable("CurrentSession", CurrentSession);
            _scriptRuntime.Globals.SetVariable("CurrentServer", CurrentServer);

            // 共通のスクリプトを読む
            LoadScriptsFromDirectory(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "GlobalScripts"), scriptExecutionCallback);

            // ユーザごとのスクリプトを読む
            LoadScriptsFromDirectory(Path.Combine(CurrentSession.UserConfigDirectory, "Scripts"), scriptExecutionCallback);
        }
        
        /// <summary>
        /// 指定したディレクトリ以下のスクリプトを読み込む
        /// </summary>
        /// <param name="rootDir"></param>
        /// <param name="scriptExecutionCallback"></param>
        private void LoadScriptsFromDirectory(String rootDir, ScriptExecutionCallback scriptExecutionCallback)
        {
            if (Directory.Exists(rootDir))
            {
                foreach (var path in Directory.GetFiles(rootDir, "*.*", SearchOption.AllDirectories))
                {
                    ScriptEngine engine;
                    if (_scriptRuntime.TryGetEngineByFileExtension(Path.GetExtension(path), out engine))
                    {
                        try
                        {
                            String expression = File.ReadAllText(path, Encoding.UTF8);
                            ScriptScope scriptScope = PrepareScriptScopeByPath(path);
                            engine.Execute(expression, scriptScope);
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
        
        private ScriptScope PrepareScriptScopeByPath(String path)
        {
            ScriptScope scriptScope = _scriptRuntime.CreateScope();
            scriptScope.SetVariable("Session", CurrentSession);
            scriptScope.SetVariable("Server", CurrentServer);
            scriptScope.SetVariable("CurrentSession", CurrentSession);
            scriptScope.SetVariable("CurrentServer", CurrentServer);

            return _scriptScopes[path] = scriptScope;
        }

        public delegate void ScriptExecutionCallback(String fileName, Exception e);
    }
}
