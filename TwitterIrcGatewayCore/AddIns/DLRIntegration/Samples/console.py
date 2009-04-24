import clr
import System

from System.Diagnostics import Trace
import Misuzilla.Applications.TwitterIrcGateway
import Misuzilla.Applications.TwitterIrcGateway.AddIns
import Misuzilla.Applications.TwitterIrcGateway.AddIns.Console

from Misuzilla.Applications.TwitterIrcGateway.AddIns.Console import ConsoleAddIn, Console, Context
from Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration import DLRIntegrationAddIn, DLRContextHelper

class TestContext(Context):
	def Initialize(self):
		pass

	def GetCommands(self):
		dict = Context.GetCommands(self)
		dict["Hauhau"] = "Say Hauhau!"
		return dict

	def hauhau(self, args):
		self.Console.NotifyMessage(("Hauhau: %s" % (args)))

def onBeforeUnload(sender, e):
	Session.AddInManager.GetAddIn[ConsoleAddIn]().UnregisterContext(DLRContextHelper.Wrap("DLRTest", TestContext))
	console.Detatch()

console = Misuzilla.Applications.TwitterIrcGateway.AddIns.Console.Console()
console.Attach("#TestContext", Server, Session, DLRContextHelper.Wrap("Test", TestContext))

Session.AddInManager.GetAddIn[DLRIntegrationAddIn]().BeforeUnload += onBeforeUnload
Session.AddInManager.GetAddIn[ConsoleAddIn]().RegisterContext(DLRContextHelper.Wrap("DLRTest", TestContext), "DLRTest", "Context DLR implementation sample")