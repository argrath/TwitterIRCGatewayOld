using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public class Connection : ConnectionBase
    {
        private User _twitterUser = null;

        public Connection(Server server, TcpClient tcpClient) : base(server, tcpClient)
        {
        }

        protected override AuthenticateResult OnAuthenticate(UserInfo userInfo)
        {
            // ニックネームとパスワードのチェック
            if (String.IsNullOrEmpty(userInfo.Nick))
            {
                return new AuthenticateResult(ErrorReply.ERR_NONICKNAMEGIVEN, "No nickname given");
            }
            if (String.IsNullOrEmpty(userInfo.Password))
            {
                return new AuthenticateResult(ErrorReply.ERR_PASSWDMISMATCH, "Password Incorrect");
            }

            // ログインチェック
            // この段階でTwitterServiceは作っておく
            SendGatewayServerMessage("* アカウント認証を確認しています...");
            TwitterService twitter = new TwitterService(userInfo.UserName, userInfo.Password);
            _twitterUser = null;
            try
            {
                _twitterUser = twitter.VerifyCredential();
            }
            catch (WebException we)
            {
                // Twitter の接続に失敗
                SendGatewayServerMessage("* アカウント認証に失敗しました。ユーザ名またはパスワードを確認してください。");
                return new AuthenticateResult(ErrorReply.ERR_PASSWDMISMATCH, "Password Incorrect");
            }
            catch (Exception ex)
            {
                // Twitter の接続に失敗
                SendGatewayServerMessage("* アカウント認証に失敗しました。ユーザ名またはパスワードを確認してください。内部的なエラーが発生しました。");
                Trace.WriteLine(ex.ToString());
                return new AuthenticateResult(ErrorReply.ERR_PASSWDMISMATCH, "Password Incorrect");
            }
            SendGatewayServerMessage(String.Format("* アカウント: {0} (ID:{1})", _twitterUser.ScreenName, _twitterUser.Id));

            return new TwitterAuthenticateResult(_twitterUser); // 成功
        }

        protected override void OnAuthenticateSucceeded()
        {
            Session session = CurrentServer.GetOrCreateSession(_twitterUser);
            session.Attach(this);
        }

        protected override void OnAuthenticateFailed(AuthenticateResult authenticateResult)
        {
        }
    }
}
