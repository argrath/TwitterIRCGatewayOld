#
# 別アカウントで取得するスクリプト
# $Id$
#
import clr
import re
import thread
import time

import Misuzilla.Applications.TwitterIrcGateway
import Misuzilla.Applications.TwitterIrcGateway.AddIns
import Misuzilla.Applications.TwitterIrcGateway.AddIns.Console

from System import *
from System.Threading import Thread, ThreadStart
from System.Collections.Generic import *
from System.Diagnostics import Trace
from Misuzilla.Applications.TwitterIrcGateway import Status, Statuses, User, Users, Utility, TwitterService
from Misuzilla.Applications.TwitterIrcGateway.AddIns import IConfiguration
from Misuzilla.Applications.TwitterIrcGateway.AddIns.Console import ConsoleAddIn, Console, Context
from Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration import DLRIntegrationAddIn, DLRBasicConfiguration, DLRContextHelper

class MultipleContext(Context):
	def Initialize(self):
		self.multiple = Multiple.instance()
		self.config = self.multiple.config
		pass

	def GetCommands(self):
		dict = Context.GetCommands(self)
		dict["Start"] = "取得を開始します。"
		dict["Stop"] = "取得を停止します。"
		return dict

	def OnUninitialize(self):
		pass

	def get_Configurations(self):
		return Array[IConfiguration]([ self.config ])

	def Start(self, args):
		if String.IsNullOrEmpty(self.config.GetValue("User")):
			self.Console.NotifyMessage("ユーザを指定してください。")
			return
		if String.IsNullOrEmpty(self.config.GetValue("Password")):
			self.Console.NotifyMessage("パスワードを指定してください。")
			return
		self.multiple.start()
		self.Console.NotifyMessage("ユーザ %s で接続を開始しました。" % self.config.GetValue("User"))

	def Stop(self, args):
		if not self.multiple.running:
			self.Console.NotifyMessage("現在取得は実行されていません。")
			return
		self.multiple.stop()
		self.Console.NotifyMessage("取得は停止しました。")

class Multiple(Object):
	@classmethod
	def instance(klass):
		if not hasattr(klass, 'instance_'):
			klass.instance_ = Multiple()
		return klass.instance_

	def __init__(self):
		self.running = False
		CurrentSession.AddInManager.GetAddIn[ConsoleAddIn]().RegisterContext(DLRContextHelper.Wrap(CurrentSession, "MultipleContext", MultipleContext), "Multiple", "別アカウント取得設定を行うコンテキストに切り替えます")
		Session.AddInManager.GetAddIn[DLRIntegrationAddIn]().BeforeUnload += self.onBeforeUnload
		self.config = DLRBasicConfiguration(CurrentSession, "MultipleContext", Dictionary[String,String]({ "User": "ユーザ名", "Password": "パスワード" }))
		if not String.IsNullOrEmpty(self.config.GetValue("User")) and not String.IsNullOrEmpty(self.config.GetValue("Password")):
			self.start()

	def start(self):
		# Setup TwitterService
		twitter = TwitterService(self.config.GetValue("User"), self.config.GetValue("Password"))
		twitter.Interval              = 60  # CurrentSession.Config.Interval
		twitter.IntervalDirectMessage = 360 # CurrentSession.Config.IntervalDirectMessage
		twitter.IntervalReplies       = 180 # CurrentSession.Config.IntervalReplies
		#twitter.BufferSize            = 250 #CurrentSession.Config.BufferSize;
		#twitter.EnableRepliesCheck    = False # CurrentSession.Config.EnableRepliesCheck;
		#twitter.FetchCount            = 50 # CurrentSession.Config.FetchCount;

		# Events
		twitter.RepliesReceived          += self.onStatusesReceived
		twitter.TimelineStatusesReceived += self.onStatusesReceived
		twitter.CheckError               += self.onCheckError
		#twitter.DirectMessageReceived   += ...

		twitter.Start()

		self.twitter = twitter
		self.running = True
		
	def stop(self):
		if self.running:
			self.twitter.Stop()
			self.running = False
		
	def onBeforeUnload(self, sender, e):
		self.stop()

	def onStatusesReceived(self, sender, e):
		CurrentSession.TwitterService.ProcessStatuses(e.Statuses, Action[Statuses](lambda s1: [CurrentSession.ProcessTimelineStatus(status, False, False) for status in s1.Status]))

	def onCheckError(self, sender, e):
		CurrentSession.SendServerErrorMessage(("(Multiple:%s) " % self.config.GetValue("User")) + e.Exception.Message)

multiple = Multiple.instance()
