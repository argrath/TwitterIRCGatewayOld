using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using IronPython.Hosting;
using IronRuby.Builtins;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;
using System.IO;
using System.Security.Policy;
using Microsoft.Scripting.Hosting.Shell;

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
            Session.AddInsLoadCompleted += (sender, e) =>
            {
                Session.AddInManager.GetAddIn<ConsoleAddIn>().RegisterContext<DLRContext>();
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
            _scriptRuntime.Globals.SetVariable("Session", Session);
            _scriptRuntime.Globals.SetVariable("Server", Server);

            // 共通のスクリプトを読む
            LoadScriptsFromDirectory(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "GlobalScripts"), scriptExecutionCallback);

            // ユーザごとのスクリプトを読む
            LoadScriptsFromDirectory(Path.Combine(Session.UserConfigDirectory, "Scripts"), scriptExecutionCallback);
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
            scriptScope.SetVariable("Session", Session);
            scriptScope.SetVariable("Server", Server);

            return _scriptScopes[path] = scriptScope;
        }

        public delegate void ScriptExecutionCallback(String fileName, Exception e);
    }

    [Description("DLR統合 コンテキストに切り替えます")]
    public class DLRContext : Context
    {
        public override Type[] Contexts { get { return new Type[] {typeof (IpyContext)}; } }
        
        [Description("読み込まれているスクリプトを一覧表示します")]
        public void List()
        {
            DLRIntegrationAddIn addIn = Session.AddInManager.GetAddIn<DLRIntegrationAddIn>();
            if (addIn.ScriptScopes.Keys.Count == 0)
            {
                ConsoleAddIn.NotifyMessage("スクリプトは現在読み込まれていません。");
                return;
            }

            foreach (var key in addIn.ScriptScopes.Keys)
            {
                ConsoleAddIn.NotifyMessage(key);
            }
        }
        
        [Description("スクリプトを再読み込みします")]
        public void Reload()
        {
            ConsoleAddIn.NotifyMessage("スクリプトを再読み込みします。");
            Session.AddInManager.GetAddIn<DLRIntegrationAddIn>().ReloadScripts((fileName, ex) =>
                                                                               {
                                                                                   ConsoleAddIn.NotifyMessage("ファイル " + fileName + " を読み込みました。");
                                                                                   if (ex != null)
                                                                                   {
                                                                                        ConsoleAddIn.NotifyMessage("実行時にエラーが発生しました:");
                                                                                        ConsoleAddIn.NotifyMessage(ex.Message);
                                                                                   }
                                                                               });
            ConsoleAddIn.NotifyMessage("スクリプトを再読み込みしました。");
        }

        [Description("現在のスクリプトスコープでスクリプトを評価します")]
        public void Eval([Description("言語名またはスクリプトエンジンの名前")]
                         String languageName,
                         [Description("評価する式")]
                         String expression)
        {
            if (File.Exists(Path.Combine(Session.UserConfigDirectory, "EnableDLRDebug")))
            {
                Object retVal = Session.AddInManager.GetAddIn<DLRIntegrationAddIn>().Eval(languageName, expression);
                ConsoleAddIn.NotifyMessage(retVal == null ? "(null)" : retVal.ToString());
            }
            else
            {
                ConsoleAddIn.NotifyMessage("Eval コマンドは現在無効化されています。");
                //ConsoleAddIn.NotifyMessage("ユーザ設定ディレクトリに EnableDLRDebug ファイルを作成することで有効になります。");
            }
        }
    }
    
    [Description("IronPython コンソールコンテキストに切り替えます")]
    public class IpyContext : Context
    {
        private Thread _consoleThread;
        private PythonCommandLine _pythonCommandLine;
        private VirtualConsole _virtualConsole;
        
        public override void Initialize()
        {
            DLRIntegrationAddIn addIn = Session.AddInManager.GetAddIn<DLRIntegrationAddIn>();
            _pythonCommandLine = new PythonCommandLine2();
            _virtualConsole = new VirtualConsole(Session, ConsoleAddIn);
            PythonConsoleOptions consoleOptions = new PythonConsoleOptions();

            _consoleThread = new Thread(t =>
            {
                _pythonCommandLine.Run(addIn.ScriptRuntime.GetEngine("py"), _virtualConsole, consoleOptions);
            });
            _consoleThread.Start();

            base.Initialize();
        }

        public override bool OnCallMissingCommand(string commandName, string rawInputLine)
        {
            _virtualConsole.SetLine(rawInputLine);
            return true;
        }
        

        [Description("IronPython コンソールを終了します")]
        public new void Exit()
        {
            _consoleThread.Abort();
            base.Exit();
        }

        private class VirtualWriter : TextWriter
        {
            private ConsoleAddIn _consoleAddIn;
            public override Encoding Encoding { get { return Encoding.UTF8; } }
            public override void Write(string value)
            {
                if (!String.IsNullOrEmpty(value.Trim()))
                    _consoleAddIn.NotifyMessage(value);
            }

            public VirtualWriter(ConsoleAddIn consoleAddIn)
            {
                _consoleAddIn = consoleAddIn;
            }
        }
        
        /// <summary>
        /// PythonCommandLine.cs の Initialize で Console.Out/Error に OutputWriter を設定しており、
        /// それはもともとの System.Console.Out などをみている
        /// </summary>
        class PythonCommandLine2 : PythonCommandLine
        {
            protected override int Run()
            {
                Language.DomainManager.SharedIO.SetOutput(MemoryStream.Null, Console.Output);
                Language.DomainManager.SharedIO.SetErrorOutput(MemoryStream.Null, Console.Output);
                return base.Run();
            }
        }
        
        private class VirtualConsole : IConsole
        {
            private Session _session;
            private ConsoleAddIn _consoleAddIn;
            private String _line;
            private ManualResetEvent _resetEvent = new ManualResetEvent(false);
            private VirtualWriter _writer;
            
            public VirtualConsole(Session session, ConsoleAddIn consoleAddIn)
            {
                _writer = new VirtualWriter(consoleAddIn);
                _session = session;
                _consoleAddIn = consoleAddIn;
            }

            public void SetLine(String line)
            {
                _line = line;
                _resetEvent.Set();
            }
            
            #region IConsole メンバ

            public TextWriter ErrorOutput
            {
                get { return _writer;  }
                set {}
            }

            public TextWriter Output
            {
                get { return _writer; }
                set {}
            }

            public string ReadLine(int autoIndentSize)
            {
                _resetEvent.WaitOne();
                _resetEvent.Reset();
                return _line;
            }

            public void Write(string text, Style style)
            {
                // TODO: style
                _consoleAddIn.NotifyMessage(text);
            }

            public void WriteLine()
            {
                _consoleAddIn.NotifyMessage(" ");
            }

            public void WriteLine(string text, Style style)
            {
                // TODO: style
                _consoleAddIn.NotifyMessage(text);
            }

            #endregion
        }
    }
}
