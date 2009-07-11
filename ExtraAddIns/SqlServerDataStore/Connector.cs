using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;
using System.Data.Linq;

namespace Misuzilla.Applicaitons.TwitterIrcGateway.AddIns.SqlServerDataStore
{
    public class Connector : AddInBase
    {
        private TwitterIrcGatewayDataContext _dataContext = new TwitterIrcGatewayDataContext(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), @"Data\Database.mdf"));
        private Dictionary<String, Group> _cacheGroup;
        public override void Initialize()
        {
            base.Initialize();
            CurrentSession.PreProcessTimelineStatuses += new EventHandler<TimelineStatusesEventArgs>(CurrentSession_PreProcessTimelineStatuses);
            CurrentSession.PostSendGroupMessageTimelineStatus += new EventHandler<TimelineStatusGroupEventArgs>(CurrentSession_PostSendGroupMessageTimelineStatus);
            CurrentSession.PostProcessTimelineStatuses += new EventHandler<TimelineStatusesEventArgs>(CurrentSession_PostProcessTimelineStatuses);

            lock (_dataContext)
            {
                _dataContext.Connection.Open();

                // メインチャンネル
                if ((from g1 in _dataContext.Group
                     where g1.UserId == CurrentSession.TwitterUser.Id && g1.Name == CurrentSession.Config.ChannelName
                     select g1).Count() == 0)
                {
                    Group g = new Group
                                  {Name = CurrentSession.Config.ChannelName, UserId = CurrentSession.TwitterUser.Id};
                    _dataContext.Group.InsertOnSubmit(g);
                    _dataContext.SubmitChanges();
                }

                UpdateGroupCache();
            }
        }
        
        private void UpdateGroupCache()
        {
            _cacheGroup = (from g in _dataContext.Group
                           where g.UserId == CurrentSession.TwitterUser.Id
                           select g).ToDictionary(v => v.Name, StringComparer.InvariantCultureIgnoreCase);
        }
        
        void CurrentSession_PreProcessTimelineStatuses(object sender, TimelineStatusesEventArgs e)
        {
            lock (_dataContext)
            {
                var notFoundCount = (from g in CurrentSession.Groups.Keys
                                     where !_cacheGroup.ContainsKey(g)
                                     select g).Count();
                if (notFoundCount > 0)
                {
                    var newGroups = from g in CurrentSession.Groups.Keys
                                    let count = (from g1 in _dataContext.Group
                                                 where g1.UserId == CurrentSession.TwitterUser.Id && g1.Name == g
                                                 select g1).Count()
                                    where count == 0
                                    select new Group() {Name = g, UserId = CurrentSession.TwitterUser.Id};

                    _dataContext.Group.InsertAllOnSubmit(newGroups);
                    _dataContext.SubmitChanges();
                    UpdateGroupCache();
                }

                var newTwitterUsers =
                    (from status in e.Statuses.Status where status.User.Id != 0 select status.User).Distinct();
                foreach (var user in newTwitterUsers)
                {
                    try
                    {
                        User newUser = new User
                                           {
                                               Id = user.Id,
                                               Name = user.Name,
                                               IsProtected = user.Protected,
                                               ProfileImageUrl = user.ProfileImageUrl,
                                               ScreenName = user.ScreenName
                                           };
                        if (!_dataContext.User.Contains(newUser))
                        {
                            _dataContext.User.InsertOnSubmit(newUser);
                            _dataContext.SubmitChanges();
                        }
                    }
                    catch (SqlException sqlE)
                    {
                        // キー制約
                        if (sqlE.Number == 2627)
                            continue;

                        throw;
                    }
                }

                var newStatuses = from status in e.Statuses.Status
                                  select
                                      new Status
                                          {
                                              Id = status.Id,
                                              CreatedAt = status.CreatedAt,
                                              ScreenName = status.User.ScreenName,
                                              Text = status.Text,
                                              UserId = (status.User.Id == 0) ? null : (Int32?) status.User.Id
                                          };
                foreach (Status status in newStatuses)
                {
                    try
                    {
                        if (!_dataContext.Status.Contains(status))
                        {
                            _dataContext.Status.InsertOnSubmit(status);
                            _dataContext.SubmitChanges();
                        }
                    }
                    catch (SqlException sqlE)
                    {
                        // キー制約
                        if (sqlE.Number == 2627)
                            continue;

                        throw;
                    }
                }
            }
        }

        void CurrentSession_PostSendGroupMessageTimelineStatus(object sender, TimelineStatusGroupEventArgs e)
        {
            lock (_dataContext)
            {
                Timeline timeline = new Timeline
                                        {
                                            GroupId = _cacheGroup[e.Group.Name].Id,
                                            StatusId = e.Status.Id,
                                            UserId = CurrentSession.TwitterUser.Id
                                        };
                if (_dataContext.Timeline.Contains(timeline))
                    return;
                _dataContext.Timeline.InsertOnSubmit(timeline);
            }
        }

        void CurrentSession_PostProcessTimelineStatuses(object sender, TimelineStatusesEventArgs e)
        {
            lock (_dataContext)
            {
                _dataContext.SubmitChanges();
            }
        }

        public override void Uninitialize()
        {
            _dataContext.Dispose();
            base.Uninitialize();
        }
    }
}
