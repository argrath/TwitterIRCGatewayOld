import clr
import System

from System.Diagnostics import Trace
import Misuzilla.Applications.TwitterIrcGateway
import Misuzilla.Applications.TwitterIrcGateway.AddIns
import Misuzilla.Applications.TwitterIrcGateway.AddIns.Console

from Misuzilla.Applications.TwitterIrcGateway.AddIns.Console import Context
from Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration import DLRContext

class TestContext(Context):
	def Initialize(self):
		pass

	def hauhau(self, args):
		self.Console.NotifyMessage(("Hauhau: %s" % (args)))

console = Misuzilla.Applications.TwitterIrcGateway.AddIns.Console.Console()
console.Attach("#TestContext", Server, Session, DLRContext[TestContext].GetProxyType("Test", TestContext))

def OnBeforeUnload(sender, e):
	console.Detatch()
