﻿﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Xml.Serialization;
using NewLife.Log;
using NewLife.Web;
﻿using NewLife.Data;
using XCode;
using XCode.Configuration;
using XCode.Membership;

namespace BiliEntity
{
    /// <summary>房间统计排序表</summary>
    [ModelCheckMode(ModelCheckModes.CheckTableWhenFirstUse)]
    public class RoomSort : RoomSort<RoomSort> {
       
    }
    
    /// <summary>房间统计排序表</summary>
    public partial class RoomSort<TEntity> : Entity<TEntity> where TEntity : RoomSort<TEntity>, new()
    {
        #region 对象操作
            ﻿
        static RoomSort()
        {
            // 用于引发基类的静态构造函数，所有层次的泛型实体类都应该有一个
            TEntity entity = new TEntity();
        }

        /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew"></param>
        public override void OnValid(Boolean isNew)
        {
			// 如果没有脏数据，则不需要进行任何处理
			if (!HasDirty) return;

            // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
            //if (String.IsNullOrEmpty(Name)) throw new ArgumentNullException(_.Name, _.Name.DisplayName + "无效！");
            //if (!isNew && ID < 1) throw new ArgumentOutOfRangeException(_.ID, _.ID.DisplayName + "必须大于0！");

            // 建议先调用基类方法，基类方法会对唯一索引的数据进行验证
            base.OnValid(isNew);

            // 在新插入数据或者修改了指定字段时进行唯一性验证，CheckExist内部抛出参数异常
            //if (isNew || Dirtys[__.Name]) CheckExist(__.Name);
            
        }

        ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
        //[EditorBrowsable(EditorBrowsableState.Never)]
        //protected override void InitData()
        //{
        //    base.InitData();

        //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
        //    // Meta.Count是快速取得表记录数
        //    if (Meta.Count > 0) return;

        //    // 需要注意的是，如果该方法调用了其它实体类的首次数据库操作，目标实体类的数据初始化将会在同一个线程完成
        //    if (XTrace.Debug) XTrace.WriteLine("开始初始化{0}[{1}]数据……", typeof(TEntity).Name, Meta.Table.DataTable.DisplayName);

        //    var entity = new RoomSort();
        //    entity.RoomID = 0;
        //    entity.Uid = 0;
        //    entity.FansNum = 0;
        //    entity.FollowNum = 0;
        //    entity.GuardNum = 0;
        //    entity.LastUpdateTime = DateTime.Now;
        //    entity.Insert();

        //    if (XTrace.Debug) XTrace.WriteLine("完成初始化{0}[{1}]数据！", typeof(TEntity).Name, Meta.Table.DataTable.DisplayName);
        //}

        ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
        ///// <returns></returns>
        //public override Int32 Insert()
        //{
        //    return base.Insert();
        //}

        ///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>
        ///// <returns></returns>
        //protected override Int32 OnInsert()
        //{
        //    return base.OnInsert();
        //}

        #endregion

        #region 扩展属性
        #endregion

        #region 扩展查询

        public static IList<TEntity> FindAllByLastUpdateTimeLimit(int minutes)
        {
            return FindAll(_.LastUpdateTime <= DateTime.Now.AddMinutes(minutes));
        }
        /// <summary>根据房间号查找</summary>
        /// <param name="roomid">房间号</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity FindByRoomID(Int64 roomid)
        {
            if (Meta.Count >= 1000)
                return Find(__.RoomID, roomid);
            else // 实体缓存
                return Meta.Cache.Find(e => e.RoomID == roomid);
            // 单对象缓存
            //return Meta.SingleCache[roomid];
        }

        /// <summary>根据用户Id查找</summary>
        /// <param name="uid">用户Id</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static IList<TEntity> FindAllByUid(Int64 uid)
        {
            if (Meta.Count >= 1000)
                return FindAll(__.Uid, uid);
            else // 实体缓存
                return Meta.Cache.FindAll(e => e.Uid == uid);
        }

        /// <summary>根据最后更新时间查找</summary>
        /// <param name="lastupdatetime">最后更新时间</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static IList<TEntity> FindAllByLastUpdateTime(DateTime lastupdatetime)
        {
            if (Meta.Count >= 1000)
                return FindAll(__.LastUpdateTime, lastupdatetime);
            else // 实体缓存
                return Meta.Cache.FindAll(e => e.LastUpdateTime == lastupdatetime);
        }


        #endregion

        #region 高级查询
        // 以下为自定义高级查询的例子

        /// <summary>查询满足条件的记录集，分页、排序</summary>
        /// <param name="userid">用户编号</param>
        /// <param name="start">开始时间</param>
        /// <param name="end">结束时间</param>
        /// <param name="key">关键字</param>
        /// <param name="param">分页排序参数，同时返回满足条件的总记录数</param>
        /// <returns>实体集</returns>
        public static IList<TEntity> Search(Int32 userid, DateTime start, DateTime end, String key, PageParameter param)
        {
            // WhereExpression重载&和|运算符，作为And和Or的替代
            // SearchWhereByKeys系列方法用于构建针对字符串字段的模糊搜索，第二个参数可指定要搜索的字段
            var exp = SearchWhereByKeys(key, null, null);

            // 以下仅为演示，Field（继承自FieldItem）重载了==、!=、>、<、>=、<=等运算符
            //if (userid > 0) exp &= _.OperatorID == userid;
            //if (isSign != null) exp &= _.IsSign == isSign.Value;
            //exp &= _.OccurTime.Between(start, end); // 大于等于start，小于end，当start/end大于MinValue时有效

            return FindAll(exp, param);
        }
        #endregion

        #region 扩展操作
        #endregion

        #region 业务
        #endregion
    }
}