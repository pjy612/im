﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
 using System.ComponentModel.DataAnnotations;
 using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace BiliEntity
{
    /// <summary>主播舰队</summary>
    [Serializable]
    [DataObject]
    [Description("主播舰队")]
    [BindIndex("PRIMARY", true, "uid")]
    [BindIndex("idx_uid", false, "uid")]
    [BindIndex("idx_last_update_time", false, "last_update_time")]
    [BindTable("guard_top", Description = "主播舰队", ConnName = "BiliCenter", DbType = DatabaseType.MySql)]
    public partial class GuardTop<TEntity> : IGuardTop
    {
        #region 属性
        private Int64 _Uid;
        /// <summary>主播id</summary>
        [DisplayName("主播id")]
        [Description("主播id")]
        [DataObjectField(true, false, false, 0)]
        [BindColumn("uid", "主播id", "bigint(20)")]
        public virtual Int64 Uid
        {
            get { return _Uid; }
            set { if (OnPropertyChanging(__.Uid, value)) { _Uid = value; OnPropertyChanged(__.Uid); } }
        }

        private Int64 _Num;
        /// <summary>舰队数量</summary>
        [DisplayName("舰队数量")]
        [Description("舰队数量")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("num", "舰队数量", "bigint(20)")]
        public virtual Int64 Num
        {
            get { return _Num; }
            set { if (OnPropertyChanging(__.Num, value)) { _Num = value; OnPropertyChanged(__.Num); } }
        }

        private String _Data;
        /// <summary>返回值</summary>
        [DisplayName("返回值")]
        [Description("返回值")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("data", "返回值", "longtext")]
        [MaxLength(-1)]
        public virtual String Data
        {
            get { return _Data; }
            set { if (OnPropertyChanging(__.Data, value)) { _Data = value; OnPropertyChanged(__.Data); } }
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
                    case __.Uid : return _Uid;
                    case __.Num : return _Num;
                    case __.Data : return _Data;
                    case __.LastUpdateTime : return _LastUpdateTime;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case __.Uid : _Uid = Convert.ToInt64(value); break;
                    case __.Num : _Num = Convert.ToInt64(value); break;
                    case __.Data : _Data = Convert.ToString(value); break;
                    case __.LastUpdateTime : _LastUpdateTime = Convert.ToDateTime(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得主播舰队字段信息的快捷方式</summary>
        public partial class _
        {
            ///<summary>主播id</summary>
            public static readonly Field Uid = FindByName(__.Uid);

            ///<summary>舰队数量</summary>
            public static readonly Field Num = FindByName(__.Num);

            ///<summary>返回值</summary>
            public static readonly Field Data = FindByName(__.Data);

            ///<summary>最后更新时间</summary>
            public static readonly Field LastUpdateTime = FindByName(__.LastUpdateTime);

            static Field FindByName(String name) { return Meta.Table.FindByName(name); }
        }

        /// <summary>取得主播舰队字段名称的快捷方式</summary>
        partial class __
        {
            ///<summary>主播id</summary>
            public const String Uid = "Uid";

            ///<summary>舰队数量</summary>
            public const String Num = "Num";

            ///<summary>返回值</summary>
            public const String Data = "Data";

            ///<summary>最后更新时间</summary>
            public const String LastUpdateTime = "LastUpdateTime";

        }
        #endregion
    }

    /// <summary>主播舰队接口</summary>
    public partial interface IGuardTop
    {
        #region 属性
        /// <summary>主播id</summary>
        Int64 Uid { get; set; }

        /// <summary>舰队数量</summary>
        Int64 Num { get; set; }

        /// <summary>返回值</summary>
        String Data { get; set; }

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