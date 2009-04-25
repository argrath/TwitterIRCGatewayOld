# $Id$
include Misuzilla::Applications::TwitterIrcGateway

class Scraping
	@@interval = 30
	@@user_cache = {}
	@@thread = nil
	
	def self.interval=(val); @@interval = val; end
	def self.interval; @@interval; end
	def self.user_cache; @@user_cache; end
	def self.thread=(val); @@thread = val; end
	def self.thread; @@thread; end
	
	def self.prepare_user_cache
#		begin
			Session.TwitterService.GetFriends(20).each do |u|
				@@user_cache[u.ScreenName.to_s] = u
			end
#		rescue
#		end
	end
	
	def self.fetch_home
		home = CurrentSession.TwitterService.GETWithCookie("/home").to_s
		if statuses = home.scan(%r{(<li class="hentry status.*?</li>)})
			statuses.reverse.each do |status1|
				status = status1[0]
				s = Status.new

				# User
				m = status.match(%r{class="screen-name" title="([^"]+)">(.*?)</a>})
				if @@user_cache[m[2]]
					s.User = @@user_cache[m[2]]
				else
					#begin
					#	s.User = CurrentSession.TwitterService.GetUser(m[2])
					#	@@user_cache[m[2]] = s.User
					#rescue
						s.User            = User.new
						s.User.Id         = 0
						s.User.Name       = m[1]
						s.User.ScreenName = m[2]
					#end
				end
				
				# Status
				s.Source    = status.match(%r{<span>from (.*?)</span>})[1].to_s
				s.Text      = Utility::UnescapeCharReference(status.match(%r{class="entry-content">(.*?)</span>})[1].to_s.gsub(%r{<a href="(http://[^"]*)"[^>]*>.*?</a>}, '\1').gsub(/<[^>]*>/, ''))
				s.Id        = status.match(%r{id="status_(\d+)"})[1].to_i
				s.CreatedAt = Time.now
				
				# 送信
				#System::Diagnostics::Trace::WriteLine(s)
				Session.TwitterService.ProcessStatus(s, System::Action[Status].new{|s1| CurrentSession.ProcessTimelineStatus(s, false, false) })
			end
		end
	end
end

Scraping.thread = Thread.new do
	# ちゃんとThreadを終わらせないと大変残念なことになる
	dlr_addin = CurrentSession.AddInManager.GetAddIn(Misuzilla::Applications::TwitterIrcGateway::AddIns::DLRIntegration::DLRIntegrationAddIn.to_clr_type)
	dlr_addin.BeforeUnload do |sender, e|
		Scraping.interval = 0
		Scraping.thread.join
	end
	
	# ユーザ情報をキャッシュする
	Session.pre_send_message_timeline_status do |sender, e|
		if e.status.user.Id != 0
			Scraping.user_cache[e.status.user.ScreenName.to_s] = e.status.user
		end
	end
    
	# ログインする
	Session.TwitterService.CookieLogin
    
	while Scraping.interval > 0 do
		begin
			Scraping.fetch_home
		rescue => e
			# なんもしないけど
			System::Diagnostics::Trace::WriteLine(e.to_s)
		end
		sleep(Scraping.interval)
	end
end

Scraping.thread.run
