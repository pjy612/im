﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BiliAccount;
using BiliAccount.Linq;
using CSRedis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
using NewLife.Reflection;
using NewLife.Serialization;

namespace web.Controllers
{
    [Route("login")]
    public class LoginController : Controller
    {
        public string Ip => this.Request.Headers["X-Real-IP"].FirstOrDefault() ?? this.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString();
        [HttpGet("v1/login")]
        public object Login(string username, string password)
        {
            var (b, accountVo) = LoginBase(username, password, $"account:{username}:{Ip}");
            if (b)
            {
                return new { code = 0, data = accountVo };
            }
            (b, accountVo) = LoginBase(username, password, $"account:{username}");
            if (b)
            {
                return new { code = 0, data = accountVo };
            }
            return new { code = -101 };
        }

        private (bool, AccountVO) LoginBase(string username, string password, string cacheKey)
        {
            var account = RedisHelper.Get<AccountVO>(cacheKey);
            try
            {
                if (account == null)
                {
                    var loginVo = ByPassword.LoginByPassword(username, password);
                    if (loginVo != null && loginVo.LoginStatus == Account.LoginStatusEnum.ByPassword)
                        account = ConvertTo(loginVo);
                    else
                        return (false, null);
                }
                else
                {
                    if (account.Password != password)
                    {
                        var loginVo = ByPassword.LoginByPassword(username, password);
                        if (loginVo != null && loginVo.LoginStatus == Account.LoginStatusEnum.ByPassword)
                            account = ConvertTo(loginVo);
                        else
                            return (false, null);
                    }
                    else
                    {
                        var needRefresh = true;

                        if (ByPassword.IsTokenAvailable(account.AccessToken))
                        {
                            if (account.Expires_AccessToken > DateTime.Now.AddDays(-1))
                                needRefresh = false;
                        }
                        if (needRefresh)
                        {
                            Account tmp = new Account();
                            var refresh = ByPassword.RefreshToken(account.AccessToken, account.RefreshToken, ref tmp);
                            if (refresh)
                            {
                                account = ConvertTo(account, tmp);
                            }
                            else
                            {
                                var loginVo = ByPassword.LoginByPassword(username, password);
                                if (loginVo != null && loginVo.LoginStatus == Account.LoginStatusEnum.ByPassword)
                                    account = ConvertTo(loginVo);
                                else
                                    return (false, null);
                            }
                        }
                    }
                }
                RedisHelper.Set(cacheKey, account);
            }
            finally
            {

            }
            return (true, account);
        }

        [HttpGet("v1/loginex")]
        public object LoginEx(string username, string password)
        {
            var (b, accountVo) = LoginBase(username, password, $"account:{username}");
            if (b)
            {
                return new { code = 0, data = accountVo };
            }
            return new { code = -101 };
        }

        private AccountVO ConvertTo(Account src)
        {
            var target = new AccountVO();
            var fieldInfos = src.GetType().GetFields();
            foreach (var propertyInfo in typeof(AccountVO).GetProperties())
            {
                var info = fieldInfos.FirstOrDefault(r => r.Name == propertyInfo.Name && r.FieldType == propertyInfo.PropertyType);
                if (info != null)
                    if (propertyInfo.CanWrite)
                        propertyInfo.SetValue(target, info.GetValue(src));
            }
            return target;
        }

        private AccountVO ConvertTo(AccountVO target, Account src)
        {
            var fieldInfos = src.GetType().GetFields();
            foreach (var propertyInfo in typeof(AccountVO).GetProperties())
            {
                var info = fieldInfos.FirstOrDefault(r => r.Name == propertyInfo.Name && r.FieldType == propertyInfo.PropertyType);
                if (info != null)
                {
                    if (propertyInfo.CanWrite)
                    {
                        var value = propertyInfo.GetValue(target);
                        if (info.GetValue(src) != null && info.GetValue(src) != value)
                        {
                            propertyInfo.SetValue(target, info.GetValue(src));
                        }
                    }
                }
            }
            return target;
        }

        private List<Cookie> ConvertTo(CookieCollection src)
        {
            var target = new List<Cookie>();
            if (src != null)
            {
                foreach (Cookie srcCookie in src) target.Add(srcCookie);
            }
            return target;
        }
    }

    public class AccountVO
    {
        /// <summary>Access_Token（使用二维码登录时此项为空）</summary>
        public string AccessToken { get; set; }

        /// <summary>Buvid/local_id</summary>
        public string Buvid { get; set; }

        /// <summary>csrf_token</summary>
        public string CsrfToken { get; set; }

        /// <summary>设备标识</summary>
        public string DeviceGuid { get; set; }

        /// <summary>device_id/bili_local_id</summary>
        public string DeviceId { get; set; }

        /// <summary>Access_Token有效期（使用二维码登录时此项为空）</summary>
        public DateTime Expires_AccessToken { get; set; }

        /// <summary>Cookies有效期</summary>
        public DateTime Expires_Cookies { get; set; }

        /// <summary>密码（使用二维码登录时此项为空）</summary>
        public string Password { get; set; }

        /// <summary>Refresh_Token（使用二维码登录时此项为空）</summary>
        public string RefreshToken { get; set; }

        /// <summary>Cookies字符串</summary>
        public string strCookies { get; set; }

        /// <summary>手机号（仅当需要手机验证的时候有值）</summary>
        public string Tel { get; set; }

        /// <summary>用户数字id</summary>
        public string Uid { get; set; }

        /// <summary>手机验证链接（仅当需要手机验证的时候有值）</summary>
        public string Url { get; set; }

        /// <summary>用户名（使用二维码登录时此项为空）</summary>
        public string UserName { get; set; }

        public List<Cookie> Cookies
        {
            get
            {
                List<Cookie> tmp = new List<Cookie>();
                string[] strArray1 = strCookies.Trim().Split(';');
                foreach (var s in strArray1)
                {
                    string[] strArray2 = s.Trim().Split('=');
                    tmp.Add(new Cookie(strArray2[0], strArray2[1])
                    {
                        Domain = ".bilibili.com"
                    });
                }
                return tmp;
            }
        }
    }
}