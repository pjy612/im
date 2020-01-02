﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace BiliEntity
{
    /// <summary>房间状态表</summary>
    [Serializable]
    [DataObject]
    [Description("房间状态表")]
    [BindIndex("idx_room_id", false, "room_id")]
    [BindIndex("idx_last_update_time", false, "last_update_time")]
    [BindTable("room_init_list", Description = "房间状态表", ConnName = "BiliCenter", DbType = DatabaseType.MySql)]
    public partial class RoomInitList<TEntity> : IRoomInitList
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

        private String _Message;
        /// <summary>房间返回值</summary>
        [DisplayName("房间返回值")]
        [Description("房间返回值")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Message", "房间返回值", "longtext")]
        public virtual String Message
        {
            get { return _Message; }
            set { if (OnPropertyChanging(__.Message, value)) { _Message = value; OnPropertyChanged(__.Message); } }
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
                    case __.Message : return _Message;
                    case __.LastUpdateTime : return _LastUpdateTime;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case __.RoomID : _RoomID = Convert.ToInt64(value); break;
                    case __.Message : _Message = Convert.ToString(value); break;
                    case __.LastUpdateTime : _LastUpdateTime = Convert.ToDateTime(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得房间状态表字段信息的快捷方式</summary>
        public partial class _
        {
            ///<summary>房间号</summary>
            public static readonly Field RoomID = FindByName(__.RoomID);

            ///<summary>房间返回值</summary>
            public static readonly Field Message = FindByName(__.Message);

            ///<summary>最后更新时间</summary>
            public static readonly Field LastUpdateTime = FindByName(__.LastUpdateTime);

            static Field FindByName(String name) { return Meta.Table.FindByName(name); }
        }

        /// <summary>取得房间状态表字段名称的快捷方式</summary>
        partial class __
        {
            ///<summary>房间号</summary>
            public const String RoomID = "RoomID";

            ///<summary>房间返回值</summary>
            public const String Message = "Message";

            ///<summary>最后更新时间</summary>
            public const String LastUpdateTime = "LastUpdateTime";

        }
        #endregion
    }

    /// <summary>房间状态表接口</summary>
    public partial interface IRoomInitList
    {
        #region 属性
        /// <summary>房间号</summary>
        Int64 RoomID { get; set; }

        /// <summary>房间返回值</summary>
        String Message { get; set; }

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