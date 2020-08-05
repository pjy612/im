﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace BiliEntity
{
    /// <summary>礼物监控表</summary>
    [Serializable]
    [DataObject]
    [Description("礼物监控表")]
    [BindIndex("PRIMARY", true, "id")]
    [BindIndex("idx_raffle_id", false, "raffle_id")]
    [BindIndex("idx_end_time", false, "end_time")]
    [BindIndex("idx_create_at", false, "create_at")]
    [BindIndex("idx_raffle_id_sort", false, "raffle_id_sort")]
    [BindIndex("idx_raffle_type", false, "raffle_type")]
    [BindTable("raffle_list", Description = "礼物监控表", ConnName = "BiliCenter", DbType = DatabaseType.MySql)]
    public partial class RaffleList<TEntity> : IRaffleList
    {
        #region 属性
        private Int32 _Id;
        /// <summary>礼物Id</summary>
        [DisplayName("礼物Id")]
        [Description("礼物Id")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("id", "礼物Id", "int(20)")]
        public virtual Int32 Id
        {
            get { return _Id; }
            set { if (OnPropertyChanging(__.Id, value)) { _Id = value; OnPropertyChanged(__.Id); } }
        }

        private Int64 _RaffleID;
        /// <summary>礼物Id</summary>
        [DisplayName("礼物Id")]
        [Description("礼物Id")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("raffle_id", "礼物Id", "bigint(20)")]
        public virtual Int64 RaffleID
        {
            get { return _RaffleID; }
            set { if (OnPropertyChanging(__.RaffleID, value)) { _RaffleID = value; OnPropertyChanged(__.RaffleID); } }
        }

        private String _RaffleType;
        /// <summary>礼物类型</summary>
        [DisplayName("礼物类型")]
        [Description("礼物类型")]
        [DataObjectField(false, false, false, 100)]
        [BindColumn("raffle_type", "礼物类型", "varchar(100)")]
        public virtual String RaffleType
        {
            get { return _RaffleType; }
            set { if (OnPropertyChanging(__.RaffleType, value)) { _RaffleType = value; OnPropertyChanged(__.RaffleType); } }
        }

        private Int64 _RaffleIDSort;
        /// <summary>礼物Id 排序</summary>
        [DisplayName("礼物Id排序")]
        [Description("礼物Id 排序")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("raffle_id_sort", "礼物Id 排序", "bigint(20)")]
        public virtual Int64 RaffleIDSort
        {
            get { return _RaffleIDSort; }
            set { if (OnPropertyChanging(__.RaffleIDSort, value)) { _RaffleIDSort = value; OnPropertyChanged(__.RaffleIDSort); } }
        }

        private String _TvType;
        /// <summary>小电视类型</summary>
        [DisplayName("小电视类型")]
        [Description("小电视类型")]
        [DataObjectField(false, false, false, 100)]
        [BindColumn("tv_type", "小电视类型", "varchar(100)")]
        public virtual String TvType
        {
            get { return _TvType; }
            set { if (OnPropertyChanging(__.TvType, value)) { _TvType = value; OnPropertyChanged(__.TvType); } }
        }

        private String _GuardType;
        /// <summary>舰队类型</summary>
        [DisplayName("舰队类型")]
        [Description("舰队类型")]
        [DataObjectField(false, false, false, 100)]
        [BindColumn("guard_type", "舰队类型", "varchar(100)")]
        public virtual String GuardType
        {
            get { return _GuardType; }
            set { if (OnPropertyChanging(__.GuardType, value)) { _GuardType = value; OnPropertyChanged(__.GuardType); } }
        }

        private Int64 _RoomID;
        /// <summary>来源房间号</summary>
        [DisplayName("来源房间号")]
        [Description("来源房间号")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("room_id", "来源房间号", "bigint(20)")]
        public virtual Int64 RoomID
        {
            get { return _RoomID; }
            set { if (OnPropertyChanging(__.RoomID, value)) { _RoomID = value; OnPropertyChanged(__.RoomID); } }
        }

        private DateTime _EndTime;
        /// <summary>领取结束时间</summary>
        [DisplayName("领取结束时间")]
        [Description("领取结束时间")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("end_time", "领取结束时间", "timestamp")]
        public virtual DateTime EndTime
        {
            get { return _EndTime; }
            set { if (OnPropertyChanging(__.EndTime, value)) { _EndTime = value; OnPropertyChanged(__.EndTime); } }
        }

        private String _Data;
        /// <summary>礼物信息</summary>
        [DisplayName("礼物信息")]
        [Description("礼物信息")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Data", "礼物信息", "varchar(100)")]
        public virtual String Data
        {
            get { return _Data; }
            set { if (OnPropertyChanging(__.Data, value)) { _Data = value; OnPropertyChanged(__.Data); } }
        }

        private DateTime _CreateAt;
        /// <summary>创建时间</summary>
        [DisplayName("创建时间")]
        [Description("创建时间")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("create_at", "创建时间", "timestamp")]
        public virtual DateTime CreateAt
        {
            get { return _CreateAt; }
            set { if (OnPropertyChanging(__.CreateAt, value)) { _CreateAt = value; OnPropertyChanged(__.CreateAt); } }
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
                    case __.Id : return _Id;
                    case __.RaffleID : return _RaffleID;
                    case __.RaffleType : return _RaffleType;
                    case __.RaffleIDSort : return _RaffleIDSort;
                    case __.TvType : return _TvType;
                    case __.GuardType : return _GuardType;
                    case __.RoomID : return _RoomID;
                    case __.EndTime : return _EndTime;
                    case __.Data : return _Data;
                    case __.CreateAt : return _CreateAt;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case __.Id : _Id = Convert.ToInt32(value); break;
                    case __.RaffleID : _RaffleID = Convert.ToInt64(value); break;
                    case __.RaffleType : _RaffleType = Convert.ToString(value); break;
                    case __.RaffleIDSort : _RaffleIDSort = Convert.ToInt64(value); break;
                    case __.TvType : _TvType = Convert.ToString(value); break;
                    case __.GuardType : _GuardType = Convert.ToString(value); break;
                    case __.RoomID : _RoomID = Convert.ToInt64(value); break;
                    case __.EndTime : _EndTime = Convert.ToDateTime(value); break;
                    case __.Data : _Data = Convert.ToString(value); break;
                    case __.CreateAt : _CreateAt = Convert.ToDateTime(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得礼物监控表字段信息的快捷方式</summary>
        public partial class _
        {
            ///<summary>礼物Id</summary>
            public static readonly Field Id = FindByName(__.Id);

            ///<summary>礼物Id</summary>
            public static readonly Field RaffleID = FindByName(__.RaffleID);

            ///<summary>礼物类型</summary>
            public static readonly Field RaffleType = FindByName(__.RaffleType);

            ///<summary>礼物Id 排序</summary>
            public static readonly Field RaffleIDSort = FindByName(__.RaffleIDSort);

            ///<summary>小电视类型</summary>
            public static readonly Field TvType = FindByName(__.TvType);

            ///<summary>舰队类型</summary>
            public static readonly Field GuardType = FindByName(__.GuardType);

            ///<summary>来源房间号</summary>
            public static readonly Field RoomID = FindByName(__.RoomID);

            ///<summary>领取结束时间</summary>
            public static readonly Field EndTime = FindByName(__.EndTime);

            ///<summary>礼物信息</summary>
            public static readonly Field Data = FindByName(__.Data);

            ///<summary>创建时间</summary>
            public static readonly Field CreateAt = FindByName(__.CreateAt);

            static Field FindByName(String name) { return Meta.Table.FindByName(name); }
        }

        /// <summary>取得礼物监控表字段名称的快捷方式</summary>
        partial class __
        {
            ///<summary>礼物Id</summary>
            public const String Id = "Id";

            ///<summary>礼物Id</summary>
            public const String RaffleID = "RaffleID";

            ///<summary>礼物类型</summary>
            public const String RaffleType = "RaffleType";

            ///<summary>礼物Id 排序</summary>
            public const String RaffleIDSort = "RaffleIDSort";

            ///<summary>小电视类型</summary>
            public const String TvType = "TvType";

            ///<summary>舰队类型</summary>
            public const String GuardType = "GuardType";

            ///<summary>来源房间号</summary>
            public const String RoomID = "RoomID";

            ///<summary>领取结束时间</summary>
            public const String EndTime = "EndTime";

            ///<summary>礼物信息</summary>
            public const String Data = "Data";

            ///<summary>创建时间</summary>
            public const String CreateAt = "CreateAt";

        }
        #endregion
    }

    /// <summary>礼物监控表接口</summary>
    public partial interface IRaffleList
    {
        #region 属性
        /// <summary>礼物Id</summary>
        Int32 Id { get; set; }

        /// <summary>礼物Id</summary>
        Int64 RaffleID { get; set; }

        /// <summary>礼物类型</summary>
        String RaffleType { get; set; }

        /// <summary>礼物Id 排序</summary>
        Int64 RaffleIDSort { get; set; }

        /// <summary>小电视类型</summary>
        String TvType { get; set; }

        /// <summary>舰队类型</summary>
        String GuardType { get; set; }

        /// <summary>来源房间号</summary>
        Int64 RoomID { get; set; }

        /// <summary>领取结束时间</summary>
        DateTime EndTime { get; set; }

        /// <summary>礼物信息</summary>
        String Data { get; set; }

        /// <summary>创建时间</summary>
        DateTime CreateAt { get; set; }
        #endregion

        #region 获取/设置 字段值
        /// <summary>获取/设置 字段值。</summary>
        /// <param name="name">字段名</param>
        /// <returns></returns>
        Object this[String name] { get; set; }
        #endregion
    }
}