include Misuzilla::Applications::TwitterIrcGateway
$status_a = nil
class Scraping
	@@interval = 30
	@@user_cache = {}
	
	def self.interval=(val); @@interval = val; end
	def self.interval; @@interval; end
	def self.user_cache; @@user_cache; end
	
	def self.fetch_home
		home = Session.TwitterService.GETWithCookie("/home").to_s
		if statuses = home.match(%r{(<li class="hentry status.*?</li>)})
			statuses.to_a.reverse.each do |status|
				$status_a = status
				s = Status.new

				# User
				m = status.match(%r{class="screen-name" title="([^"]+)">(.*?)</a>})
				if @@user_cache[m[2]]
					s.User = @@user_cache[m[2]]
				else
					s.User = User.new
					s.User.Id = 0
					s.User.Name = m[1]
					s.User.ScreenName = m[2]
				end
				
				# Status
				s.Source = Utility::UnescapeCharReference(status.match(%r{<span>from (.*?)</span>})[1].to_s)
				s.Text = status.match(%r{class="entry-content">(.*?)</span>})[1].to_s.gsub(/<[^>]*>/, '')
				s.Id   = status.match(%r{id="status_(\d+)"})[1].to_i
				s.CreatedAt = Time.now
				
				# 送信
				Session.TwitterService.ProcessStatus(s, System::Action[Status].new{|s1| Session.ProcessTimelineStatus(s, false, false) })
			end
		end
	end
end

Thread.new do
	# ちゃんとThreadを終わらせないと大変残念なことになる
	dlr_addin = Session.AddInManager.GetAddIn(Misuzilla::Applications::TwitterIrcGateway::AddIns::DLRIntegration::DLRIntegrationAddIn.to_clr_type)
	dlr_addin.BeforeUnload do |sender, e|
		Scraping.interval = 0
	end
	
	# ユーザ情報をキャッシュする
	Session.pre_send_message_timeline_status do |sender, e|
		if e.status.user.Id != 0
			Scraping.user_cache[e.status.user.ScreenName] = e.status.user
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
end.run
