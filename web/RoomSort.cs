﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace BiliEntity
{
    /// <summary>房间统计排序表</summary>
    [Serializable]
    [DataObject]
    [Description("房间统计排序表")]
    [BindIndex("PRIMARY", true, "room_id")]
    [BindIndex("idx_room_id", false, "room_id")]
    [BindIndex("idx_uid", false, "uid")]
    [BindIndex("idx_last_update_time", false, "last_update_time")]
    [BindTable("room_sort", Description = "房间统计排序表", ConnName = "BiliCenter", DbType = DatabaseType.MySql)]
    public partial class RoomSort<TEntity> : IRoomSort
    {
        #region 属性
        private Int64 _RoomID;
        /// <summary>房间号</summary>
        [DisplayName("房间号")]
        [Description("房间号")]
        [DataObjectField(true, false, false, 0)]
        [BindColumn("room_id", "房间号", "bigint(20)")]
        public virtual Int64 RoomID
        {
            get { return _RoomID; }
            set { if (OnPropertyChanging(__.RoomID, value)) { _RoomID = value; OnPropertyChanged(__.RoomID); } }
        }

        private Int64 _Uid;
        /// <summary>用户Id</summary>
        [DisplayName("用户Id")]
        [Description("用户Id")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("uid", "用户Id", "bigint(20)")]
        public virtual Int64 Uid
        {
            get { return _Uid; }
            set { if (OnPropertyChanging(__.Uid, value)) { _Uid = value; OnPropertyChanged(__.Uid); } }
        }

        private Int64 _FansNum;
        /// <summary>粉丝牌量</summary>
        [DisplayName("粉丝牌量")]
        [Description("粉丝牌量")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("fans_num", "粉丝牌量", "bigint(20)")]
        public virtual Int64 FansNum
        {
            get { return _FansNum; }
            set { if (OnPropertyChanging(__.FansNum, value)) { _FansNum = value; OnPropertyChanged(__.FansNum); } }
        }

        private Int64 _FollowNum;
        /// <summary>关注数量</summary>
        [DisplayName("关注数量")]
        [Description("关注数量")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("follow_num", "关注数量", "bigint(20)")]
        public virtual Int64 FollowNum
        {
            get { return _FollowNum; }
            set { if (OnPropertyChanging(__.FollowNum, value)) { _FollowNum = value; OnPropertyChanged(__.FollowNum); } }
        }

        private Int64 _GuardNum;
        /// <summary>舰队数量</summary>
        [DisplayName("舰队数量")]
        [Description("舰队数量")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("guard_num", "舰队数量", "bigint(20)")]
        public virtual Int64 GuardNum
        {
            get { return _GuardNum; }
            set { if (OnPropertyChanging(__.GuardNum, value)) { _GuardNum = value; OnPropertyChanged(__.GuardNum); } }
        }

        private DateTime _LastUpdateTime;
        /// <summary>最后更新时间</summary>
        [DisplayName("最后更新时间")]
        [Description("最后更新时间")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("last_update_time", "最后更新时间", "timestamp")]
        public virtual DateTime LastUpdateTime
        {
            get { return _LastUpdateTime; }
            set { if (OnPropertyChanging(__.LastUpdateTime, value)) { _LastUpdateTime = value; OnPropertyChanged(__.LastUpdateTime); } }
        }
        #endregion

        #region 获取/设置 字段值
        /// <summary>
        /// 获取/设置 字段值。
        /// 一个索引，基类使用反射实现。
        /// 派生实体类可重写该索引，以避免反射带来的性能损耗
        /// </summary>
        /// <param name="name">字段名</param>
        /// <returns></returns>
        public override Object this[String name]
        {
            get
            {
                switch (name)
                {
                    case __.RoomID : return _RoomID;
                    case __.Uid : return _Uid;
                    case __.FansNum : return _FansNum;
                    case __.FollowNum : return _FollowNum;
                    case __.GuardNum : return _GuardNum;
                    case __.LastUpdateTime : return _LastUpdateTime;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case __.RoomID : _RoomID = Convert.ToInt64(value); break;
                    case __.Uid : _Uid = Convert.ToInt64(value); break;
                    case __.FansNum : _FansNum = Convert.ToInt64(value); break;
                    case __.FollowNum : _FollowNum = Convert.ToInt64(value); break;
                    case __.GuardNum : _GuardNum = Convert.ToInt64(value); break;
                    case __.LastUpdateTime : _LastUpdateTime = Convert.ToDateTime(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得房间统计排序表字段信息的快捷方式</summary>
        public partial class _
        {
            ///<summary>房间号</summary>
            public static readonly Field RoomID = FindByName(__.RoomID);

            ///<summary>用户Id</summary>
            public static readonly Field Uid = FindByName(__.Uid);

            ///<summary>粉丝牌量</summary>
            public static readonly Field FansNum = FindByName(__.FansNum);

            ///<summary>关注数量</summary>
            public static readonly Field FollowNum = FindByName(__.FollowNum);

            ///<summary>舰队数量</summary>
            public static readonly Field GuardNum = FindByName(__.GuardNum);

            ///<summary>最后更新时间</summary>
            public static readonly Field LastUpdateTime = FindByName(__.LastUpdateTime);

            static Field FindByName(String name) { return Meta.Table.FindByName(name); }
        }

        /// <summary>取得房间统计排序表字段名称的快捷方式</summary>
        partial class __
        {
            ///<summary>房间号</summary>
            public const String RoomID = "RoomID";

            ///<summary>用户Id</summary>
            public const String Uid = "Uid";

            ///<summary>粉丝牌量</summary>
            public const String FansNum = "FansNum";

            ///<summary>关注数量</summary>
            public const String FollowNum = "FollowNum";

            ///<summary>舰队数量</summary>
            public const String GuardNum = "GuardNum";

            ///<summary>最后更新时间</summary>
            public const String LastUpdateTime = "LastUpdateTime";

        }
        #endregion
    }

    /// <summary>房间统计排序表接口</summary>
    public partial interface IRoomSort
    {
        #region 属性
        /// <summary>房间号</summary>
        Int64 RoomID { get; set; }

        /// <summary>用户Id</summary>
        Int64 Uid { get; set; }

        /// <summary>粉丝牌量</summary>
        Int64 FansNum { get; set; }

        /// <summary>关注数量</summary>
        Int64 FollowNum { get; set; }

        /// <summary>舰队数量</summary>
        Int64 GuardNum { get; set; }

        /// <summary>最后更新时间</summary>
        DateTime LastUpdateTime { get; set; }
        #endregion

        #region 获取/设置 字段值
        /// <summary>获取/设置 字段值。</summary>
        /// <param name="name">字段名</param>
        /// <returns></returns>
        Object this[String name] { get; set; }
        #endregion
    }
}