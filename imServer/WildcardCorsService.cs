using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace aspCore.Extensions
{
    //自己的扩展
    /// <summary>
    /// 自定义跨域处理服务，增加通配符二级域名策略  ICorsService
    /// 想要覆盖之前的ICorsService,需要在AddMvc()之后，替换,例如下：
    /// services.Add(ServiceDescriptor.Transient<ICorsService, WildcardCorsService>());
    /// services.Configure<CorsOptions>(options => options.AddPolicy(
    /// "AllowSameDomain",
    /// builder => builder.WithOrigins("*.test.com")));
    /// </summary>
    public class WildcardCorsService : CorsService
    {
        public WildcardCorsService(IOptions<CorsOptions> options)
            : base(options)
        {
        }

        #region 在默认处理域名策略之前，提前拦截,用自己的策略
        public override void EvaluateRequest(HttpContext context, CorsPolicy policy, CorsResult result)
        {
            var origin = context.Request.Headers[CorsConstants.Origin];
            //拦截
            //Orings为策略(*.test.com)(该策略可多个)    origin为跨域请求的域名
            EvaluateOriginForWildcard(policy.Origins, origin);
            //策略根据通配符替换完成
            base.EvaluateRequest(context, policy, result);
        }

        public override void EvaluatePreflightRequest(HttpContext context, CorsPolicy policy, CorsResult result)
        {
            var origin = context.Request.Headers[CorsConstants.Origin];
            //拦截
            EvaluateOriginForWildcard(policy.Origins, origin);
            //策略根据通配符替换完成
            base.EvaluatePreflightRequest(context, policy, result);
        }
        #endregion
        private void EvaluateOriginForWildcard(IList<string> origins, string origin)
        {
            //只在没有匹配的origin的情况下进行操作
            if (!origins.Contains(origin))
            {
                //查询所有以星号开头的origin （如果有多个通配符域名策略，每个都设置）
                var wildcardDomains = origins.Where(o => o.StartsWith("*"));
                if (wildcardDomains.Any())
                {
                    //遍历以星号开头的origin 
                    foreach (var wildcardDomain in wildcardDomains)
                    {
                        //如果以.test.com结尾
                        if (origin.EndsWith(wildcardDomain.Substring(1))
                            //或者以//test.com结尾，针对http://test.com
                            || origin.EndsWith("//" + wildcardDomain.Substring(2)))
                        {
                            //将http://www.cnblogs.com添加至origins
                            origins.Add(origin);
                            break;
                        }
                    }
                }
            }
        }
    }
}