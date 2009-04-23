import clr
import System

from System.Diagnostics import Trace
import Misuzilla.Applications.TwitterIrcGateway
import Misuzilla.Applications.TwitterIrcGateway.AddIns
import Misuzilla.Applications.TwitterIrcGateway.AddIns.Console

from Misuzilla.Applications.TwitterIrcGateway.AddIns.Console import ConsoleAddIn, Console, Context
from Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration import DLRIntegrationAddIn, DLRContext

class TestContext(Context):
	def Initialize(self):
		pass

	def hauhau(self, args):
		self.Console.NotifyMessage(("Hauhau: %s" % (args)))

def onBeforeUnload(sender, e):
	Session.AddInManager.GetAddIn[ConsoleAddIn]().UnregisterContext(DLRContext[TestContext].GetProxyType("DLRTest", TestContext))
	console.Detatch()

console = Misuzilla.Applications.TwitterIrcGateway.AddIns.Console.Console()
console.Attach("#TestContext", Server, Session, DLRContext[TestContext].GetProxyType("Test", TestContext))

Session.AddInManager.GetAddIn[DLRIntegrationAddIn]().BeforeUnload += onBeforeUnload
Session.AddInManager.GetAddIn[ConsoleAddIn]().RegisterContext(DLRContext[TestContext].GetProxyType("DLRTest", TestContext), "DLRTest", "Context DLR implementation sample")