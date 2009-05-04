import clr
import re
import thread
import time

import Misuzilla.Applications.TwitterIrcGateway
import Misuzilla.Applications.TwitterIrcGateway.AddIns
import Misuzilla.Applications.TwitterIrcGateway.AddIns.Console

from System import *
from System.Threading import Thread, ThreadStart
from System.Diagnostics import Trace
from Misuzilla.Applications.TwitterIrcGateway import Status, Statuses, User, Users, Utility
from Misuzilla.Applications.TwitterIrcGateway.AddIns import IConfiguration
from Misuzilla.Applications.TwitterIrcGateway.AddIns.Console import ConsoleAddIn, Console, Context
from Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration import DLRIntegrationAddIn, DLRBasicConfiguration, DLRContextHelper

class Scraping(Object):
	def __init__(self):
		Session.AddInManager.GetAddIn[DLRIntegrationAddIn]().BeforeUnload += self.onBeforeUnload
		self.thread = None
		self.interval = 30
		self.re_source = re.compile(r"<span>from (.*?)</span>")
		self.re_statuses = re.compile(r"<li class=\"hentry status.*?</li>")
		self.re_content = re.compile(r"class=\"entry-content\">(.*?)</span>")
		self.re_user = re.compile(r"class=\"screen-name\" title=\"([^\"]+)\">(.*?)</a>")
		self.re_anchor = re.compile(r"<a href=\"(http://[^\"]*)\"[^>]*>.*?</a>")
		self.re_tag = re.compile(r"<[^>]*>")
		self.re_status_id = re.compile(r"id=\"status_(\d+)\"")
		
	def start(self):
		CurrentSession.TwitterService.CookieLogin()
		self.thread = Thread(ThreadStart(self.runProc))
		self.thread.Start()

	def runProc(self):
		while self.interval > 0:
			try:
				self.fetchHome()
			except Exception, ex:
				Trace.WriteLine(ex.ToString())
			Thread.Sleep(self.interval * 1000)

	def fetchHome(self):
		home = CurrentSession.TwitterService.GETWithCookie("/home")
		statuses = self.re_statuses.findall(home)
		statuses.reverse()
		for status in statuses:
			s = Status()
			# User
			match = self.re_user.search(status)
			s.User            = User()
			s.User.Id         = 0
			s.User.Name       = match.group(1)
			s.User.ScreenName = match.group(2)
			
			# Status
			s.Source    = self.re_source.search(status).group(1)
			s.Text      = Utility.UnescapeCharReference(self.re_tag.sub(r"", self.re_anchor.sub(r"\1", self.re_content.search(status).group(1))))
			s.Id        = int(self.re_status_id.search(status).group(1), 10)
			s.CreatedAt = DateTime.Now
			
			#Trace.WriteLine(s.ToString())
			CurrentSession.TwitterService.ProcessStatus(s, Action[Status](lambda s1: CurrentSession.ProcessTimelineStatus(s1, False, False)))

	def onBeforeUnload(self, sender, e):
		self.interval = 0
		self.thread.Join()

Scraping().start()

