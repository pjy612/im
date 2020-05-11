using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using FreeSql;
using FreeSql.Aop;
using FreeSql.DataAnnotations;
using XCode;
using XCode.DataAccessLayer;

namespace ImCore
{
    public static class FreeSqlHelper
    {
        private static ConcurrentDictionary<(string, DataType), IFreeSql> _fsDict = new ConcurrentDictionary<(string, DataType), IFreeSql>();

        public const string DB = "BiliCenter";

        public static void Init()
        {
            DAL.AddConnStr(DB, "Server=127.0.0.1;port=3306;Database=im;Uid=poster;Pwd=;SslMode=none;Convert Zero Datetime=True;Allow Zero Datetime=True;Allow User Variables=True;", null, "mysql");
        }

        public static IFreeSql BaseDb => GetFreeSql(DAL.Create(DB).Db.ConnectionString, DataType.MySql);

        public static IFreeSql GetFreeSql(string conStr, DataType dbType, bool autoSyncStructure = false)
        {
            return _fsDict.GetOrAdd((conStr, dbType), (t) =>
            {
                IFreeSql freeSql = new FreeSqlBuilder()
                    .UseConnectionString(dbType, conStr)
                    .UseMonitorCommand(executing: command =>
                    {
                        //XTrace.WriteLine(command.CommandText);
                    }, executed: (command, trace) =>
                    {
                        //XTrace.WriteLine(trace);
                    })
                    .UseNoneCommandParameter(true)
                    .UseAutoSyncStructure(autoSyncStructure)
                    .Build();
                freeSql.Aop.ConfigEntity         += FsqlAopConfigEntity;
                freeSql.Aop.ConfigEntityProperty += FsqlAopConfigEntityProperty;
                return freeSql;
            });
        }
        private static void FsqlAopConfigEntity(object s, ConfigEntityEventArgs e)
        {
            if ((typeof(EntityBase)).IsAssignableFrom(e.EntityType))
            {
                Type baseType = e.EntityType.BaseType;
                if (baseType != null)
                {
                    BindTableAttribute bindTable = baseType.GetCustomAttribute<BindTableAttribute>();
                    if (bindTable != null)
                    {
                        e.ModifyResult.Name = bindTable.Name;
                    }
                }
            }
        }

        private static void FsqlAopConfigEntityProperty(object s, ConfigEntityPropertyEventArgs e)
        {
            if ((typeof(EntityBase)).IsAssignableFrom(e.EntityType))
            {
                PropertyInfo info = e.Property;
                ColumnAttribute columnInfo = e.ModifyResult;
                BindColumnAttribute bindColumn = info.GetCustomAttribute<BindColumnAttribute>();
                if (bindColumn != null)
                {
                    columnInfo.Name = bindColumn.Name;
                    columnInfo.DbType = bindColumn.RawType;
                    DataObjectFieldAttribute dataObjectField = info.GetCustomAttribute<DataObjectFieldAttribute>();
                    if (dataObjectField != null)
                    {
                        columnInfo.IsPrimary  = dataObjectField.PrimaryKey;
                        columnInfo.IsIdentity = dataObjectField.IsIdentity;
                        columnInfo.IsNullable = dataObjectField.IsNullable;
                    }
                }
                else
                {
                    columnInfo.IsIgnore = true;
                }
            }
        }
    }
}
