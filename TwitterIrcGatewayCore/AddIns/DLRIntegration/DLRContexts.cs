using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting.Shell;
using Microsoft.Scripting.Runtime;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration
{
    [Description("DLR統合 コンテキストに切り替えます")]
    public class DLRContext : Context
    {
        public override ICollection<ContextInfo> Contexts { get { return (IsEvalEnabled ? new ContextInfo[] { new ContextInfo(typeof(IpyContext)) } : new ContextInfo[0]); } }
        public Boolean IsEvalEnabled { get { return File.Exists(Path.Combine(Session.UserConfigDirectory, "EnableDLRDebug")); } }

        [Description("読み込まれているスクリプトを一覧表示します")]
        public void List()
        {
            DLRIntegrationAddIn addIn = Session.AddInManager.GetAddIn<DLRIntegrationAddIn>();
            if (addIn.ScriptScopes.Keys.Count == 0)
            {
                Console.NotifyMessage("スクリプトは現在読み込まれていません。");
                return;
            }

            foreach (var key in addIn.ScriptScopes.Keys)
            {
                Console.NotifyMessage(key);
            }
        }

        [Description("スクリプトを再読み込みします")]
        public void Reload()
        {
            Console.NotifyMessage("スクリプトを再読み込みします。");
            Session.AddInManager.GetAddIn<DLRIntegrationAddIn>().ReloadScripts((fileName, ex) =>
            {
                Console.NotifyMessage("ファイル " + fileName + " を読み込みました。");
                if (ex != null)
                {
                    Console.NotifyMessage("実行時にエラーが発生しました:");
                    Console.NotifyMessage(ex.Message);
                }
            });
            Console.NotifyMessage("スクリプトを再読み込みしました。");
        }

        [Description("現在のスクリプトスコープでスクリプトを評価します")]
        public void Eval([Description("言語名またはスクリプトエンジンの名前")]
                         String languageName,
                         [Description("評価する式")]
                         String expression)
        {
            if (IsEvalEnabled)
            {
                Object retVal = Session.AddInManager.GetAddIn<DLRIntegrationAddIn>().Eval(languageName, expression);
                Console.NotifyMessage(retVal == null ? "(null)" : retVal.ToString());
            }
            else
            {
                Console.NotifyMessage("Eval コマンドは現在無効化されています。");
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
            _virtualConsole = new VirtualConsole(Session, this.Console);
            addIn.ScriptRuntime.IO.SetOutput(MemoryStream.Null, _virtualConsole.Output);
            addIn.ScriptRuntime.IO.SetErrorOutput(MemoryStream.Null, _virtualConsole.Output);
            _pythonCommandLine = new PythonCommandLine2(Server, Session);
            PythonConsoleOptions consoleOptions = new PythonConsoleOptions();

            _consoleThread = new Thread(t =>
            {
                _pythonCommandLine.Run(addIn.ScriptRuntime.GetEngine("py"), _virtualConsole, consoleOptions);
            });
            _consoleThread.Start();

            base.Initialize();
        }

        public override bool OnPreProcessInput(string inputLine)
        {
            if (inputLine.Trim().ToLower() == "exit")
                return false;

            _virtualConsole.SetLine((inputLine == " ") ? "" : inputLine);
            return true;
        }

        [Browsable(false)]
        public override void Help(string commandName)
        {
        }

        [Description("IronPython コンソールを終了します")]
        public new void Exit()
        {
            base.Exit();
        }

        public override void Dispose()
        {
            if (_consoleThread != null)
            {
                _consoleThread.Abort();
                _consoleThread = null;
            }
            base.Dispose();
        }

        private class VirtualWriter : TextWriter
        {
            private Console.Console _console;
            public override Encoding Encoding { get { return Encoding.UTF8; } }
            public override void Write(string value)
            {
                if (!String.IsNullOrEmpty(value.Trim()))
                    _console.NotifyMessage(value);
            }

            public VirtualWriter(Console.Console console)
            {
                _console = console;
            }
        }

        /// <summary>
        /// PythonCommandLine.cs の Initialize で Console.Out/Error に OutputWriter を設定しており、
        /// それはもともとの System.Console.Out などをみている
        /// </summary>
        class PythonCommandLine2 : PythonCommandLine
        {
            private Server _server;
            private Session _session;
            public PythonCommandLine2(Server server, Session session) : base()
            {
                _server = server;
                _session = session;
            }
            protected override Scope CreateScope()
            {
                Scope scope = base.CreateScope();
                scope.SetObjectName("Session", _session);
                scope.SetObjectName("Server", _server);
                scope.SetObjectName("CurrentSession", _session);
                scope.SetObjectName("CurrentServer", _server);

                return scope;
            }
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
            private Console.Console _console;
            private String _line;
            private ManualResetEvent _resetEvent = new ManualResetEvent(false);
            private VirtualWriter _writer;

            public VirtualConsole(Session session, Console.Console console)
            {
                _writer = new VirtualWriter(console);
                _session = session;
                _console = console;
            }

            public void SetLine(String line)
            {
                _line = line;
                _resetEvent.Set();
            }

            #region IConsole メンバ

            public TextWriter ErrorOutput
            {
                get { return _writer; }
                set { }
            }

            public TextWriter Output
            {
                get { return _writer; }
                set { }
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
                _console.NotifyMessage(text);
            }

            public void WriteLine()
            {
                _console.NotifyMessage(" ");
            }

            public void WriteLine(string text, Style style)
            {
                // TODO: style
                _console.NotifyMessage(text);
            }

            #endregion
        }
    }
}
