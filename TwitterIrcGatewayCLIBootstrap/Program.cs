using System.Reflection;

namespace TwitterIrcGatewayCLIBootstrap
{
    class Program
    {
        static void Main(string[] args)
        {
            Assembly asmTigCli = Assembly.Load("TwitterIrcGatewayCLI");
            asmTigCli.EntryPoint.Invoke(null, new object[]{ args });
        }
    }
}
