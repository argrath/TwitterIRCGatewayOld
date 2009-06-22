import clr

from System import *
from System.Collections.Generic import *
from System.Diagnostics import Trace
import Misuzilla.Applications.TwitterIrcGateway
import Misuzilla.Applications.TwitterIrcGateway.AddIns
import Misuzilla.Applications.TwitterIrcGateway.AddIns.Console

from Misuzilla.Applications.TwitterIrcGateway.AddIns import IConfiguration
from Misuzilla.Applications.TwitterIrcGateway.AddIns.Console import ConsoleAddIn, Console, Context
from Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration import DLRIntegrationAddIn, DLRBasicConfiguration, DLRContextHelper

class AdminContext(Context):
	def Initialize(self):
		pass

	def GetCommands(self):
		dict = Context.GetCommands(self)
		dict["Users"] = "接続しているユーザの一覧を表示します。"
		return dict

	def OnUninitialize(self):
		pass

	def get_Configurations(self):
		return Array[IConfiguration]([ ])

	# Implementation
	def users(self, args):
		self.Console.NotifyMessage(("現在%d人のユーザが接続しています。" % (CurrentServer.Sessions.Count)))
		for session in CurrentServer.Sessions:
			self.Console.NotifyMessage(("%s" % (session)))

# #Console にコンテキストを追加する
Session.AddInManager.GetAddIn[ConsoleAddIn]().RegisterContext(DLRContextHelper.Wrap(CurrentSession, "AdminContext", AdminContext), "Admin", "管理用のコンテキストに切り替えます。")

# 後片付け
Session.AddInManager.GetAddIn[DLRIntegrationAddIn]().BeforeUnload += lambda sender, e: Session.AddInManager.GetAddIn[ConsoleAddIn]().UnregisterContext(DLRContextHelper.Wrap(CurrentSession, "AdminContext", AdminContext))
