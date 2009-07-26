﻿#pragma warning disable 1591
//------------------------------------------------------------------------------
// <auto-generated>
//     このコードはツールによって生成されました。
//     ランタイム バージョン:2.0.50727.4918
//
//     このファイルへの変更は、以下の状況下で不正な動作の原因になったり、
//     コードが再生成されるときに損失したりします。
// </auto-generated>
//------------------------------------------------------------------------------

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.SqlServerDataStore
{
	using System.Data.Linq;
	using System.Data.Linq.Mapping;
	using System.Data;
	using System.Collections.Generic;
	using System.Reflection;
	using System.Linq;
	using System.Linq.Expressions;
	using System.ComponentModel;
	using System;
	
	
	[System.Data.Linq.Mapping.DatabaseAttribute(Name="Database")]
	public partial class TwitterIrcGatewayDataContext : System.Data.Linq.DataContext
	{
		
		private static System.Data.Linq.Mapping.MappingSource mappingSource = new AttributeMappingSource();
		
    #region Extensibility Method Definitions
    partial void OnCreated();
    partial void InsertUser(User instance);
    partial void UpdateUser(User instance);
    partial void DeleteUser(User instance);
    partial void InsertStatus(Status instance);
    partial void UpdateStatus(Status instance);
    partial void DeleteStatus(Status instance);
    partial void InsertGroup(Group instance);
    partial void UpdateGroup(Group instance);
    partial void DeleteGroup(Group instance);
    partial void InsertTimeline(Timeline instance);
    partial void UpdateTimeline(Timeline instance);
    partial void DeleteTimeline(Timeline instance);
    partial void InsertAuthUser(AuthUser instance);
    partial void UpdateAuthUser(AuthUser instance);
    partial void DeleteAuthUser(AuthUser instance);
    #endregion
		
		public TwitterIrcGatewayDataContext() : 
				base(global::Misuzilla.Applications.TwitterIrcGateway.AddIns.SqlServerDataStore.Properties.Settings.Default.DatabaseConnectionString, mappingSource)
		{
			OnCreated();
		}
		
		public TwitterIrcGatewayDataContext(string connection) : 
				base(connection, mappingSource)
		{
			OnCreated();
		}
		
		public TwitterIrcGatewayDataContext(System.Data.IDbConnection connection) : 
				base(connection, mappingSource)
		{
			OnCreated();
		}
		
		public TwitterIrcGatewayDataContext(string connection, System.Data.Linq.Mapping.MappingSource mappingSource) : 
				base(connection, mappingSource)
		{
			OnCreated();
		}
		
		public TwitterIrcGatewayDataContext(System.Data.IDbConnection connection, System.Data.Linq.Mapping.MappingSource mappingSource) : 
				base(connection, mappingSource)
		{
			OnCreated();
		}
		
		public System.Data.Linq.Table<User> User
		{
			get
			{
				return this.GetTable<User>();
			}
		}
		
		public System.Data.Linq.Table<Status> Status
		{
			get
			{
				return this.GetTable<Status>();
			}
		}
		
		public System.Data.Linq.Table<Group> Group
		{
			get
			{
				return this.GetTable<Group>();
			}
		}
		
		public System.Data.Linq.Table<Timeline> Timeline
		{
			get
			{
				return this.GetTable<Timeline>();
			}
		}
		
		public System.Data.Linq.Table<AuthUser> AuthUser
		{
			get
			{
				return this.GetTable<AuthUser>();
			}
		}
	}
	
	[Table(Name="dbo.[User]")]
	public partial class User : INotifyPropertyChanging, INotifyPropertyChanged
	{
		
		private static PropertyChangingEventArgs emptyChangingEventArgs = new PropertyChangingEventArgs(String.Empty);
		
		private int _Id;
		
		private string _ScreenName;
		
		private string _Name;
		
		private string _ProfileImageSmall;
		
		private bool _IsProtected;
		
		private EntitySet<Status> _Status;
		
		private EntitySet<Timeline> _Timeline;
		
    #region Extensibility Method Definitions
    partial void OnLoaded();
    partial void OnValidate(System.Data.Linq.ChangeAction action);
    partial void OnCreated();
    partial void OnIdChanging(int value);
    partial void OnIdChanged();
    partial void OnScreenNameChanging(string value);
    partial void OnScreenNameChanged();
    partial void OnNameChanging(string value);
    partial void OnNameChanged();
    partial void OnProfileImageUrlChanging(string value);
    partial void OnProfileImageUrlChanged();
    partial void OnIsProtectedChanging(bool value);
    partial void OnIsProtectedChanged();
    #endregion
		
		public User()
		{
			this._Status = new EntitySet<Status>(new Action<Status>(this.attach_Status), new Action<Status>(this.detach_Status));
			this._Timeline = new EntitySet<Timeline>(new Action<Timeline>(this.attach_Timeline), new Action<Timeline>(this.detach_Timeline));
			OnCreated();
		}
		
		[Column(Storage="_Id", AutoSync=AutoSync.OnInsert, DbType="Int NOT NULL IDENTITY", IsPrimaryKey=true)]
		public int Id
		{
			get
			{
				return this._Id;
			}
			set
			{
				if ((this._Id != value))
				{
					this.OnIdChanging(value);
					this.SendPropertyChanging();
					this._Id = value;
					this.SendPropertyChanged("Id");
					this.OnIdChanged();
				}
			}
		}
		
		[Column(Storage="_ScreenName", DbType="NVarChar(50) NOT NULL", CanBeNull=false)]
		public string ScreenName
		{
			get
			{
				return this._ScreenName;
			}
			set
			{
				if ((this._ScreenName != value))
				{
					this.OnScreenNameChanging(value);
					this.SendPropertyChanging();
					this._ScreenName = value;
					this.SendPropertyChanged("ScreenName");
					this.OnScreenNameChanged();
				}
			}
		}
		
		[Column(Storage="_Name", DbType="NVarChar(MAX)")]
		public string Name
		{
			get
			{
				return this._Name;
			}
			set
			{
				if ((this._Name != value))
				{
					this.OnNameChanging(value);
					this.SendPropertyChanging();
					this._Name = value;
					this.SendPropertyChanged("Name");
					this.OnNameChanged();
				}
			}
		}
		
		[Column(Storage="_ProfileImageSmall", DbType="NVarChar(MAX)")]
		public string ProfileImageUrl
		{
			get
			{
				return this._ProfileImageSmall;
			}
			set
			{
				if ((this._ProfileImageSmall != value))
				{
					this.OnProfileImageUrlChanging(value);
					this.SendPropertyChanging();
					this._ProfileImageSmall = value;
					this.SendPropertyChanged("ProfileImageUrl");
					this.OnProfileImageUrlChanged();
				}
			}
		}
		
		[Column(Storage="_IsProtected", DbType="Bit NOT NULL")]
		public bool IsProtected
		{
			get
			{
				return this._IsProtected;
			}
			set
			{
				if ((this._IsProtected != value))
				{
					this.OnIsProtectedChanging(value);
					this.SendPropertyChanging();
					this._IsProtected = value;
					this.SendPropertyChanged("IsProtected");
					this.OnIsProtectedChanged();
				}
			}
		}
		
		[Association(Name="User_Status", Storage="_Status", ThisKey="Id", OtherKey="UserId")]
		public EntitySet<Status> Status
		{
			get
			{
				return this._Status;
			}
			set
			{
				this._Status.Assign(value);
			}
		}
		
		[Association(Name="User_Timeline", Storage="_Timeline", ThisKey="Id", OtherKey="UserId")]
		public EntitySet<Timeline> Timeline
		{
			get
			{
				return this._Timeline;
			}
			set
			{
				this._Timeline.Assign(value);
			}
		}
		
		public event PropertyChangingEventHandler PropertyChanging;
		
		public event PropertyChangedEventHandler PropertyChanged;
		
		protected virtual void SendPropertyChanging()
		{
			if ((this.PropertyChanging != null))
			{
				this.PropertyChanging(this, emptyChangingEventArgs);
			}
		}
		
		protected virtual void SendPropertyChanged(String propertyName)
		{
			if ((this.PropertyChanged != null))
			{
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
		
		private void attach_Status(Status entity)
		{
			this.SendPropertyChanging();
			entity.User = this;
		}
		
		private void detach_Status(Status entity)
		{
			this.SendPropertyChanging();
			entity.User = null;
		}
		
		private void attach_Timeline(Timeline entity)
		{
			this.SendPropertyChanging();
			entity.User = this;
		}
		
		private void detach_Timeline(Timeline entity)
		{
			this.SendPropertyChanging();
			entity.User = null;
		}
	}
	
	[Table(Name="dbo.Status")]
	public partial class Status : INotifyPropertyChanging, INotifyPropertyChanged
	{
		
		private static PropertyChangingEventArgs emptyChangingEventArgs = new PropertyChangingEventArgs(String.Empty);
		
		private long _Id;
		
		private string _ScreenName;
		
		private System.Nullable<int> _UserId;
		
		private string _Text;
		
		private System.Nullable<int> _InReplyToId;
		
		private System.DateTime _CreatedAt;
		
		private EntitySet<Timeline> _Timeline;
		
		private EntityRef<User> _User;
		
    #region Extensibility Method Definitions
    partial void OnLoaded();
    partial void OnValidate(System.Data.Linq.ChangeAction action);
    partial void OnCreated();
    partial void OnIdChanging(long value);
    partial void OnIdChanged();
    partial void OnScreenNameChanging(string value);
    partial void OnScreenNameChanged();
    partial void OnUserIdChanging(System.Nullable<int> value);
    partial void OnUserIdChanged();
    partial void OnTextChanging(string value);
    partial void OnTextChanged();
    partial void OnInReplyToIdChanging(System.Nullable<int> value);
    partial void OnInReplyToIdChanged();
    partial void OnCreatedAtChanging(System.DateTime value);
    partial void OnCreatedAtChanged();
    #endregion
		
		public Status()
		{
			this._Timeline = new EntitySet<Timeline>(new Action<Timeline>(this.attach_Timeline), new Action<Timeline>(this.detach_Timeline));
			this._User = default(EntityRef<User>);
			OnCreated();
		}
		
		[Column(Storage="_Id", DbType="BigInt NOT NULL", IsPrimaryKey=true)]
		public long Id
		{
			get
			{
				return this._Id;
			}
			set
			{
				if ((this._Id != value))
				{
					this.OnIdChanging(value);
					this.SendPropertyChanging();
					this._Id = value;
					this.SendPropertyChanged("Id");
					this.OnIdChanged();
				}
			}
		}
		
		[Column(Storage="_ScreenName", DbType="NVarChar(50) NOT NULL", CanBeNull=false)]
		public string ScreenName
		{
			get
			{
				return this._ScreenName;
			}
			set
			{
				if ((this._ScreenName != value))
				{
					this.OnScreenNameChanging(value);
					this.SendPropertyChanging();
					this._ScreenName = value;
					this.SendPropertyChanged("ScreenName");
					this.OnScreenNameChanged();
				}
			}
		}
		
		[Column(Storage="_UserId", DbType="Int")]
		public System.Nullable<int> UserId
		{
			get
			{
				return this._UserId;
			}
			set
			{
				if ((this._UserId != value))
				{
					if (this._User.HasLoadedOrAssignedValue)
					{
						throw new System.Data.Linq.ForeignKeyReferenceAlreadyHasValueException();
					}
					this.OnUserIdChanging(value);
					this.SendPropertyChanging();
					this._UserId = value;
					this.SendPropertyChanged("UserId");
					this.OnUserIdChanged();
				}
			}
		}
		
		[Column(Storage="_Text", DbType="Text NOT NULL", CanBeNull=false, UpdateCheck=UpdateCheck.Never)]
		public string Text
		{
			get
			{
				return this._Text;
			}
			set
			{
				if ((this._Text != value))
				{
					this.OnTextChanging(value);
					this.SendPropertyChanging();
					this._Text = value;
					this.SendPropertyChanged("Text");
					this.OnTextChanged();
				}
			}
		}
		
		[Column(Storage="_InReplyToId", DbType="Int")]
		public System.Nullable<int> InReplyToId
		{
			get
			{
				return this._InReplyToId;
			}
			set
			{
				if ((this._InReplyToId != value))
				{
					this.OnInReplyToIdChanging(value);
					this.SendPropertyChanging();
					this._InReplyToId = value;
					this.SendPropertyChanged("InReplyToId");
					this.OnInReplyToIdChanged();
				}
			}
		}
		
		[Column(Storage="_CreatedAt", DbType="DateTime NOT NULL")]
		public System.DateTime CreatedAt
		{
			get
			{
				return this._CreatedAt;
			}
			set
			{
				if ((this._CreatedAt != value))
				{
					this.OnCreatedAtChanging(value);
					this.SendPropertyChanging();
					this._CreatedAt = value;
					this.SendPropertyChanged("CreatedAt");
					this.OnCreatedAtChanged();
				}
			}
		}
		
		[Association(Name="Status_Timeline", Storage="_Timeline", ThisKey="Id", OtherKey="StatusId")]
		public EntitySet<Timeline> Timeline
		{
			get
			{
				return this._Timeline;
			}
			set
			{
				this._Timeline.Assign(value);
			}
		}
		
		[Association(Name="User_Status", Storage="_User", ThisKey="UserId", OtherKey="Id", IsForeignKey=true)]
		public User User
		{
			get
			{
				return this._User.Entity;
			}
			set
			{
				User previousValue = this._User.Entity;
				if (((previousValue != value) 
							|| (this._User.HasLoadedOrAssignedValue == false)))
				{
					this.SendPropertyChanging();
					if ((previousValue != null))
					{
						this._User.Entity = null;
						previousValue.Status.Remove(this);
					}
					this._User.Entity = value;
					if ((value != null))
					{
						value.Status.Add(this);
						this._UserId = value.Id;
					}
					else
					{
						this._UserId = default(Nullable<int>);
					}
					this.SendPropertyChanged("User");
				}
			}
		}
		
		public event PropertyChangingEventHandler PropertyChanging;
		
		public event PropertyChangedEventHandler PropertyChanged;
		
		protected virtual void SendPropertyChanging()
		{
			if ((this.PropertyChanging != null))
			{
				this.PropertyChanging(this, emptyChangingEventArgs);
			}
		}
		
		protected virtual void SendPropertyChanged(String propertyName)
		{
			if ((this.PropertyChanged != null))
			{
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
		
		private void attach_Timeline(Timeline entity)
		{
			this.SendPropertyChanging();
			entity.Status = this;
		}
		
		private void detach_Timeline(Timeline entity)
		{
			this.SendPropertyChanging();
			entity.Status = null;
		}
	}
	
	[Table(Name="dbo.[Group]")]
	public partial class Group : INotifyPropertyChanging, INotifyPropertyChanged
	{
		
		private static PropertyChangingEventArgs emptyChangingEventArgs = new PropertyChangingEventArgs(String.Empty);
		
		private int _Id;
		
		private int _UserId;
		
		private string _Name;
		
		private EntitySet<Timeline> _Timeline;
		
    #region Extensibility Method Definitions
    partial void OnLoaded();
    partial void OnValidate(System.Data.Linq.ChangeAction action);
    partial void OnCreated();
    partial void OnIdChanging(int value);
    partial void OnIdChanged();
    partial void OnUserIdChanging(int value);
    partial void OnUserIdChanged();
    partial void OnNameChanging(string value);
    partial void OnNameChanged();
    #endregion
		
		public Group()
		{
			this._Timeline = new EntitySet<Timeline>(new Action<Timeline>(this.attach_Timeline), new Action<Timeline>(this.detach_Timeline));
			OnCreated();
		}
		
		[Column(Storage="_Id", AutoSync=AutoSync.OnInsert, DbType="Int NOT NULL IDENTITY", IsPrimaryKey=true, IsDbGenerated=true)]
		public int Id
		{
			get
			{
				return this._Id;
			}
			set
			{
				if ((this._Id != value))
				{
					this.OnIdChanging(value);
					this.SendPropertyChanging();
					this._Id = value;
					this.SendPropertyChanged("Id");
					this.OnIdChanged();
				}
			}
		}
		
		[Column(Storage="_UserId", DbType="Int NOT NULL")]
		public int UserId
		{
			get
			{
				return this._UserId;
			}
			set
			{
				if ((this._UserId != value))
				{
					this.OnUserIdChanging(value);
					this.SendPropertyChanging();
					this._UserId = value;
					this.SendPropertyChanged("UserId");
					this.OnUserIdChanged();
				}
			}
		}
		
		[Column(Storage="_Name", DbType="NVarChar(MAX) NOT NULL", CanBeNull=false)]
		public string Name
		{
			get
			{
				return this._Name;
			}
			set
			{
				if ((this._Name != value))
				{
					this.OnNameChanging(value);
					this.SendPropertyChanging();
					this._Name = value;
					this.SendPropertyChanged("Name");
					this.OnNameChanged();
				}
			}
		}
		
		[Association(Name="Group_Timeline", Storage="_Timeline", ThisKey="Id", OtherKey="GroupId")]
		public EntitySet<Timeline> Timeline
		{
			get
			{
				return this._Timeline;
			}
			set
			{
				this._Timeline.Assign(value);
			}
		}
		
		public event PropertyChangingEventHandler PropertyChanging;
		
		public event PropertyChangedEventHandler PropertyChanged;
		
		protected virtual void SendPropertyChanging()
		{
			if ((this.PropertyChanging != null))
			{
				this.PropertyChanging(this, emptyChangingEventArgs);
			}
		}
		
		protected virtual void SendPropertyChanged(String propertyName)
		{
			if ((this.PropertyChanged != null))
			{
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
		
		private void attach_Timeline(Timeline entity)
		{
			this.SendPropertyChanging();
			entity.Group = this;
		}
		
		private void detach_Timeline(Timeline entity)
		{
			this.SendPropertyChanging();
			entity.Group = null;
		}
	}
	
	[Table(Name="dbo.Timeline")]
	public partial class Timeline : INotifyPropertyChanging, INotifyPropertyChanged
	{
		
		private static PropertyChangingEventArgs emptyChangingEventArgs = new PropertyChangingEventArgs(String.Empty);
		
		private int _UserId;
		
		private long _StatusId;
		
		private int _GroupId;
		
		private EntityRef<User> _User;
		
		private EntityRef<Status> _Status;
		
		private EntityRef<Group> _Group;
		
    #region Extensibility Method Definitions
    partial void OnLoaded();
    partial void OnValidate(System.Data.Linq.ChangeAction action);
    partial void OnCreated();
    partial void OnUserIdChanging(int value);
    partial void OnUserIdChanged();
    partial void OnStatusIdChanging(long value);
    partial void OnStatusIdChanged();
    partial void OnGroupIdChanging(int value);
    partial void OnGroupIdChanged();
    #endregion
		
		public Timeline()
		{
			this._User = default(EntityRef<User>);
			this._Status = default(EntityRef<Status>);
			this._Group = default(EntityRef<Group>);
			OnCreated();
		}
		
		[Column(Storage="_UserId", DbType="Int NOT NULL", IsPrimaryKey=true)]
		public int UserId
		{
			get
			{
				return this._UserId;
			}
			set
			{
				if ((this._UserId != value))
				{
					if (this._User.HasLoadedOrAssignedValue)
					{
						throw new System.Data.Linq.ForeignKeyReferenceAlreadyHasValueException();
					}
					this.OnUserIdChanging(value);
					this.SendPropertyChanging();
					this._UserId = value;
					this.SendPropertyChanged("UserId");
					this.OnUserIdChanged();
				}
			}
		}
		
		[Column(Storage="_StatusId", DbType="BigInt NOT NULL", IsPrimaryKey=true)]
		public long StatusId
		{
			get
			{
				return this._StatusId;
			}
			set
			{
				if ((this._StatusId != value))
				{
					if (this._Status.HasLoadedOrAssignedValue)
					{
						throw new System.Data.Linq.ForeignKeyReferenceAlreadyHasValueException();
					}
					this.OnStatusIdChanging(value);
					this.SendPropertyChanging();
					this._StatusId = value;
					this.SendPropertyChanged("StatusId");
					this.OnStatusIdChanged();
				}
			}
		}
		
		[Column(Storage="_GroupId", DbType="Int NOT NULL", IsPrimaryKey=true)]
		public int GroupId
		{
			get
			{
				return this._GroupId;
			}
			set
			{
				if ((this._GroupId != value))
				{
					if (this._Group.HasLoadedOrAssignedValue)
					{
						throw new System.Data.Linq.ForeignKeyReferenceAlreadyHasValueException();
					}
					this.OnGroupIdChanging(value);
					this.SendPropertyChanging();
					this._GroupId = value;
					this.SendPropertyChanged("GroupId");
					this.OnGroupIdChanged();
				}
			}
		}
		
		[Association(Name="User_Timeline", Storage="_User", ThisKey="UserId", OtherKey="Id", IsForeignKey=true)]
		public User User
		{
			get
			{
				return this._User.Entity;
			}
			set
			{
				User previousValue = this._User.Entity;
				if (((previousValue != value) 
							|| (this._User.HasLoadedOrAssignedValue == false)))
				{
					this.SendPropertyChanging();
					if ((previousValue != null))
					{
						this._User.Entity = null;
						previousValue.Timeline.Remove(this);
					}
					this._User.Entity = value;
					if ((value != null))
					{
						value.Timeline.Add(this);
						this._UserId = value.Id;
					}
					else
					{
						this._UserId = default(int);
					}
					this.SendPropertyChanged("User");
				}
			}
		}
		
		[Association(Name="Status_Timeline", Storage="_Status", ThisKey="StatusId", OtherKey="Id", IsForeignKey=true)]
		public Status Status
		{
			get
			{
				return this._Status.Entity;
			}
			set
			{
				Status previousValue = this._Status.Entity;
				if (((previousValue != value) 
							|| (this._Status.HasLoadedOrAssignedValue == false)))
				{
					this.SendPropertyChanging();
					if ((previousValue != null))
					{
						this._Status.Entity = null;
						previousValue.Timeline.Remove(this);
					}
					this._Status.Entity = value;
					if ((value != null))
					{
						value.Timeline.Add(this);
						this._StatusId = value.Id;
					}
					else
					{
						this._StatusId = default(long);
					}
					this.SendPropertyChanged("Status");
				}
			}
		}
		
		[Association(Name="Group_Timeline", Storage="_Group", ThisKey="GroupId", OtherKey="Id", IsForeignKey=true)]
		public Group Group
		{
			get
			{
				return this._Group.Entity;
			}
			set
			{
				Group previousValue = this._Group.Entity;
				if (((previousValue != value) 
							|| (this._Group.HasLoadedOrAssignedValue == false)))
				{
					this.SendPropertyChanging();
					if ((previousValue != null))
					{
						this._Group.Entity = null;
						previousValue.Timeline.Remove(this);
					}
					this._Group.Entity = value;
					if ((value != null))
					{
						value.Timeline.Add(this);
						this._GroupId = value.Id;
					}
					else
					{
						this._GroupId = default(int);
					}
					this.SendPropertyChanged("Group");
				}
			}
		}
		
		public event PropertyChangingEventHandler PropertyChanging;
		
		public event PropertyChangedEventHandler PropertyChanged;
		
		protected virtual void SendPropertyChanging()
		{
			if ((this.PropertyChanging != null))
			{
				this.PropertyChanging(this, emptyChangingEventArgs);
			}
		}
		
		protected virtual void SendPropertyChanged(String propertyName)
		{
			if ((this.PropertyChanged != null))
			{
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}
	
	[Table(Name="dbo.AuthUser")]
	public partial class AuthUser : INotifyPropertyChanging, INotifyPropertyChanged
	{
		
		private static PropertyChangingEventArgs emptyChangingEventArgs = new PropertyChangingEventArgs(String.Empty);
		
		private int _UserId;
		
		private string _Token;
		
		private string _TokenSecret;
		
		private string _PasswordHash;
		
    #region Extensibility Method Definitions
    partial void OnLoaded();
    partial void OnValidate(System.Data.Linq.ChangeAction action);
    partial void OnCreated();
    partial void OnUserIdChanging(int value);
    partial void OnUserIdChanged();
    partial void OnTokenChanging(string value);
    partial void OnTokenChanged();
    partial void OnTokenSecretChanging(string value);
    partial void OnTokenSecretChanged();
    partial void OnPasswordHashChanging(string value);
    partial void OnPasswordHashChanged();
    #endregion
		
		public AuthUser()
		{
			OnCreated();
		}
		
		[Column(Storage="_UserId", DbType="Int NOT NULL", IsPrimaryKey=true)]
		public int UserId
		{
			get
			{
				return this._UserId;
			}
			set
			{
				if ((this._UserId != value))
				{
					this.OnUserIdChanging(value);
					this.SendPropertyChanging();
					this._UserId = value;
					this.SendPropertyChanged("UserId");
					this.OnUserIdChanged();
				}
			}
		}
		
		[Column(Storage="_Token", DbType="NVarChar(MAX) NOT NULL", CanBeNull=false)]
		public string Token
		{
			get
			{
				return this._Token;
			}
			set
			{
				if ((this._Token != value))
				{
					this.OnTokenChanging(value);
					this.SendPropertyChanging();
					this._Token = value;
					this.SendPropertyChanged("Token");
					this.OnTokenChanged();
				}
			}
		}
		
		[Column(Storage="_TokenSecret", DbType="NVarChar(MAX) NOT NULL", CanBeNull=false)]
		public string TokenSecret
		{
			get
			{
				return this._TokenSecret;
			}
			set
			{
				if ((this._TokenSecret != value))
				{
					this.OnTokenSecretChanging(value);
					this.SendPropertyChanging();
					this._TokenSecret = value;
					this.SendPropertyChanged("TokenSecret");
					this.OnTokenSecretChanged();
				}
			}
		}
		
		[Column(Storage="_PasswordHash", DbType="NVarChar(MAX) NOT NULL", CanBeNull=false)]
		public string PasswordHash
		{
			get
			{
				return this._PasswordHash;
			}
			set
			{
				if ((this._PasswordHash != value))
				{
					this.OnPasswordHashChanging(value);
					this.SendPropertyChanging();
					this._PasswordHash = value;
					this.SendPropertyChanged("PasswordHash");
					this.OnPasswordHashChanged();
				}
			}
		}
		
		public event PropertyChangingEventHandler PropertyChanging;
		
		public event PropertyChangedEventHandler PropertyChanged;
		
		protected virtual void SendPropertyChanging()
		{
			if ((this.PropertyChanging != null))
			{
				this.PropertyChanging(this, emptyChangingEventArgs);
			}
		}
		
		protected virtual void SendPropertyChanged(String propertyName)
		{
			if ((this.PropertyChanged != null))
			{
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}
}
#pragma warning restore 1591
