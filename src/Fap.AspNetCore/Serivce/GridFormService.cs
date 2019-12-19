﻿using Fap.AspNetCore.Model;
using Fap.AspNetCore.ViewModel;
using Fap.Core.DI;
using System;
using Fap.Core.Extensions;
using System.Collections.Generic;
using System.Text;
using Fap.Core.Infrastructure.Metadata;
using Fap.Core.Infrastructure.Domain;
using Fap.Core.DataAccess;
using System.Linq;
using Fap.Core.Rbac;
using Dapper;
using Fap.Core.Infrastructure.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Ardalis.GuardClauses;
using Fap.AspNetCore.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using System.Threading.Tasks;
using Fap.AspNetCore.Extensions;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Fap.AspNetCore.Serivce
{
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
    public class GridFormService : IGridFormService
    {
        private readonly IFapApplicationContext _applicationContext;
        private readonly IDbContext _dbContext;
        private readonly IRbacService _rbacService;
        private IAntiforgery _antiforgery;
        private readonly ILogger<GridFormService> _logger;
        public GridFormService(
            IFapApplicationContext fapApplicationContext,
            ILogger<GridFormService> logger,
            IDbContext dbContext,
            IRbacService rbacService,
            IAntiforgery antiforgery)
        {
            _dbContext = dbContext;
            _applicationContext = fapApplicationContext;
            _rbacService = rbacService;
            _antiforgery = antiforgery;
            _logger = logger;
        }
        public JqGridData QueryPageDataResultView(JqGridPostData jqGridPostData, Action<Pageable> actionSimpleQueryOption)
        {
            IEnumerable<FapColumn> fapColumns = _dbContext.Columns(jqGridPostData.QuerySet.TableName);
            Pageable queryOption = AnalysisPostData();
            //queryOption.Where = AnalysisWhere(queryOption.Where);
            PageDataResultView result = QueryPagedDynamicData(jqGridPostData.HasOperCol);
            return result.GetJqGridJsonData();
            Pageable AnalysisPostData()
            {
                jqGridPostData.Filters = jqGridPostData.Filters.IsPresent() ? jqGridPostData.Filters.Replace("query ", "select ") : "";
                //矫正当前页为0的情况
                if (jqGridPostData.Page < 0)
                {
                    jqGridPostData.Page = 1;
                }
                QuerySet qs = jqGridPostData.QuerySet;                
                Pageable queryOption = new Pageable(_dbContext) { TableName = qs.TableName, QueryCols = qs.QueryCols, HistoryTimePoint = jqGridPostData.TimePoint };
                //设置统计
                if (qs.Statsetlist != null && qs.Statsetlist.Any())
                {
                    queryOption.AddStatField(qs.Statsetlist);
                }
                if (qs.Parameters != null && qs.Parameters.Count > 0)
                {
                    foreach (var param in qs.Parameters)
                    {
                        queryOption.AddParameter(param.ParamKey, param.ParamValue);
                    }

                }
                //优先级高
                if (jqGridPostData.Sidx.IsPresent())
                {
                    string[] sidxs = jqGridPostData.Sidx.Split(',');
                    foreach (var sidx in sidxs)
                    {
                        if (sidx.IsPresent())
                        {
                            string[] odx = sidx.Trim().Split(' ');
                            if (odx != null)
                            {
                                var col = fapColumns.First(f => f.ColName == odx[0]);
                                string colName = col.ColName;
                                if (odx.Length > 1)
                                {
                                    if (col.CtrlType == FapColumn.CTRL_TYPE_REFERENCE)
                                    {
                                        colName += "MC";
                                    }
                                    queryOption.OrderBy.AddOrderByCondtion(colName, odx[1]);
                                }
                                else
                                {
                                    queryOption.OrderBy.AddOrderByCondtion(colName, jqGridPostData.Sord);
                                }
                            }
                        }
                    }
                }
                if (qs.OrderByList != null && qs.OrderByList.Count > 0)
                {
                    foreach (var orderby in qs.OrderByList)
                    {
                        queryOption.OrderBy.AddOrderByCondtion(orderby.Field, orderby.Direction);

                    }
                }

                //构造初始化条件,如果没有过滤条件，又设置了初始化条件则设置初始化条件。或者设置了过滤条件且初始化条件为全局条件则同样设置where条件
                if (qs.GlobalWhere.IsPresent())
                {
                    queryOption.Where = qs.GlobalWhere;
                }
                if (jqGridPostData.Filters.IsMissing() && qs.InitWhere.IsPresent())
                {
                    if (queryOption.Where.IsMissing())
                    {
                        queryOption.Where = qs.InitWhere;
                    }
                    else
                    {
                        queryOption.Where += " and " + qs.InitWhere;
                    }
                }

                //页面级条件
                if (jqGridPostData.PageCondition.IsPresent())
                {
                    JsonFilterToSql jfs = new JsonFilterToSql(_dbContext);
                    if (qs.GlobalWhere.IsPresent())
                    {
                        queryOption.AddWhere(jfs.BuilderFilter(queryOption.TableName, jqGridPostData.PageCondition), QuerySymbolEnum.AND);
                    }
                    else
                    {
                        queryOption.Where = jfs.BuilderFilter(queryOption.TableName, jqGridPostData.PageCondition);
                    }
                }
                //构造jqgrid过滤条件
                if (jqGridPostData.Filters.IsPresent())
                {
                    //string strFilter = JqGridHelper.BuilderFilter(queryOption.TableName, model.Filters);
                    //if (!string.IsNullOrWhiteSpace(strFilter))
                    //{
                    //    queryOption.Filter = strFilter;
                    //}
                    FilterCondition filterCondition = JsonFilterToSql.BuildFilterCondition(fapColumns, jqGridPostData.Filters);
                    if (filterCondition != null)
                    {
                        queryOption.FilterCondition = filterCondition;
                    }
                }
                //事件处理
                if (actionSimpleQueryOption != null)
                {
                    actionSimpleQueryOption(queryOption);
                }
                queryOption.PageNumber = jqGridPostData.Page;
                queryOption.PageSize = jqGridPostData.Rows;
                //数据权限
                string dataWhere = _rbacService.GetRoleDataWhere(qs.TableName);
                if (dataWhere.IsPresent())
                {
                    if (queryOption.Where.IsPresent())
                    {
                        queryOption.Where += " and  " + dataWhere;
                    }
                    else
                    {
                        queryOption.Where = dataWhere;
                    }
                }
                //解析条件
                queryOption.Where = AnalysisWhere(queryOption.Where);
                return queryOption;
                string AnalysisWhere(string where)
                {
                    if (where.IsMissing())
                    {
                        return "";
                    }
                    //获得安全sql
                    where = where.FilterDangerSql();
                    //替换部门权限占位符
                    return where.Replace(FapPlatformConstants.DepartmentAuthority, _rbacService.GetUserDeptAuthorityWhere()).ReplaceIgnoreCase("query", "select ");
                }
            }
            PageDataResultView QueryPagedDynamicData(bool hasOperCol = false)
            {
                try
                {
                    PageInfo<dynamic> pi = _dbContext.QueryPage(queryOption);

                    //组装成DataResultView对象
                    PageDataResultView dataResultView = new PageDataResultView();
                    dataResultView.Data = pi.Items.ToFapDynamicObjectList(fapColumns);
                    //当未获取数据的时候才获取默认值
                    //if (!dataObject.Data.Any())
                    //{
                    //wyf表单应用，表格暂时不用取默认值
                    //dataResultView.DefaultData = queryOption.Wraper.GetDefaultData();
                    //}
                    dataResultView.DataJson = JsonConvert.SerializeObject(pi.Items);
                    dataResultView.TotalNum = pi.TotalPages;
                    dataResultView.CurrentPageIndex = pi.PageNumber;
                    dataResultView.OrginData = pi.Items;
                    dataResultView.DataListForJqGrid = pi.Items as IEnumerable<IDictionary<string, object>>;
                    dataResultView.PageCount = pi.PageSize;
                    //统计字段
                    dataResultView.StatFieldData = pi.StatFieldData;
                    dataResultView.StatFieldDataJson = JsonConvert.SerializeObject(pi.StatFieldData);

                    return dataResultView;

                }
                catch (Exception)
                {
                    throw;
                }

            }
        }
        private static object lockSave = new object();
        public async Task<ResponseViewModel> PersistenceAsync(IFormCollection formCollection)
        {
            //响应消息
            string resMessage = string.Empty;
            //操作符 
            OperEnum operEnum = OperEnum.none;
            if (formCollection.TryGetValue(FapWebConstants.OPERATOR, out StringValues oper))
            {
                operEnum = (OperEnum)Enum.Parse(typeof(OperEnum), oper);
            }
            else
            {
                //没有oper的时候 可以根据id的值判断是新增还是编辑
                string id = formCollection["Id"];
                if (id.IsMissing() || id.ToInt() < 1)
                {
                    operEnum = OperEnum.add;
                }
                else
                {
                    operEnum = OperEnum.edit;
                }
            }
            if (operEnum == OperEnum.none)
            {
                return ResponseViewModelUtils.Failure("未知的持久化操作符");
            }
            string tableName = string.Empty;
            if (formCollection.TryGetValue(FapWebConstants.FORM_TABLENAME, out StringValues tn))
            {
                tableName = tn;
            }
            if (_applicationContext.Request.Query.TryGetValue(FapWebConstants.QUERY_TABLENAME, out StringValues value))
            {
                tableName = value;
            }
            if (tableName.IsMissing())
            {
                return ResponseViewModelUtils.Failure("未知的持久化实体");
            }

            #region 防止多次点击重复保存以及CSRF攻击
            //令牌 form生成时赋值
            string avoid_repeat_token = string.Empty;
            if (formCollection.TryGetValue(FapWebConstants.AVOID_REPEAT_TOKEN, out StringValues vs))
            {
                avoid_repeat_token = vs;
            }
            else
            {
                return ResponseViewModelUtils.Failure("不存在防重复提交令牌");
            }
            lock (lockSave)
            {
                string avoid_repeat_tokenKey = tableName.ToLower() + FapWebConstants.AVOID_REPEAT_TOKEN;
                if (oper.Equals("del"))
                {
                    avoid_repeat_tokenKey += "-del";
                }
                if (_applicationContext.Session.GetString(avoid_repeat_tokenKey) != avoid_repeat_token)
                {
                    return ResponseViewModelUtils.Failure("请勿重复提交数据");
                }
                //移除重复提交标记
                _applicationContext.Session.Remove(avoid_repeat_tokenKey);
            }
            //CSRF 令牌验证
            try
            {
                await _antiforgery.ValidateRequestAsync(_applicationContext.HttpContext);
            }
            catch (Exception)
            {
                return ResponseViewModelUtils.Failure("请求非法,校验CSRF异常");
            }
            #endregion

            var (mainData, ChildsData) = BuilderData();
            if (operEnum == OperEnum.del)
            {
                bool islogic = formCollection[FapWebConstants.LOGICDELETE][0].ToBool();
                mainData.IsLogic = islogic;
            }
            try
            {
                return SaveChange(operEnum, mainData, ChildsData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return ResponseViewModelUtils.Failure("发生错误，操作失败");
            }

            (dynamic mainData, Dictionary<string, IEnumerable<dynamic>> childsData) BuilderData()
            {
                var columnList = _dbContext.Columns(tableName);
                //undefined 父文本编辑框控件要去掉,logicdelete逻辑删除,childsData子表格数据
                string[] exclude = new string[]
                {
                    FapWebConstants.OPERATOR,
                    FapWebConstants.QUERY_TABLENAME ,
                    FapWebConstants.FORM_TABLENAME,
                    FapWebConstants.AVOID_REPEAT_TOKEN,
                    FapWebConstants.UNDEFINED,
                    FapWebConstants.LOGICDELETE,
                    FapWebConstants.CHILDS_DATALIST
                };
                dynamic fdo = formCollection.ToDynamicObject(columnList, exclude);
                Dictionary<string, IEnumerable<dynamic>> childDataDic = null;
                if (formCollection.ContainsKey("childsData"))
                {
                    childDataDic = new Dictionary<string, IEnumerable<dynamic>>();
                    JArray arrayGrids = JArray.Parse(formCollection["childsData"]);
                    foreach (JObject item in arrayGrids)
                    {
                        string childTableName = item.GetValue(FapWebConstants.QUERY_TABLENAME).ToString();

                        JArray childDataArray = item.GetValue("data") as JArray;
                        if (childDataArray.Any())
                        {
                            List<FapDynamicObject> childDatas = new List<FapDynamicObject>();
                            foreach (JObject cd in childDataArray)
                            {
                                var cfdo = cd.ToFapDynamicObject(_dbContext.Columns(childTableName), exclude);
                                childDatas.Add(cfdo);
                            }
                            childDataDic.Add(childTableName, childDatas);
                        }
                    }
                }
                return (fdo, childDataDic);
            }

        }

        [Transactional]
        public ResponseViewModel SaveChange(OperEnum oper, FapDynamicObject mainDataKeyValues, Dictionary<string, IEnumerable<dynamic>> childDataList = null)
        {
            ResponseViewModel rvm = new ResponseViewModel();
            dynamic mainData = mainDataKeyValues;
            if (oper == OperEnum.add)
            {
                _dbContext.InsertDynamicData(mainData);
                SaveChildData(mainData, childDataList);
                return ResponseViewModelUtils.Sueecss(mainData, "创建成功");
            }
            else if (oper == OperEnum.edit)
            {
                //返回原因
                bool rv = _dbContext.UpdateDynamicData(mainData);
                SaveChildData(mainData, childDataList);
                rvm.success = rv;
                rvm.msg = rv ? "更新成功" : "更新失败，请重试";
                return rvm;
            }
            else if (oper == OperEnum.del)
            {
                bool rv= _dbContext.DeleteDynamicData(mainDataKeyValues);
                DeleteChildData(mainData);
                rvm.success = rv;
                rvm.msg = rv ? "删除成功" : "删除失败，请重试";
                return rvm;
            }
            return ResponseViewModelUtils.Sueecss();
            void SaveChildData(FapDynamicObject mainData, Dictionary<string, IEnumerable<dynamic>> childDataList)
            {
                if (childDataList != null && childDataList.Any())
                {
                    foreach (var item in childDataList)
                    {
                        //获取外键字段
                        var childColumnList = _dbContext.Columns(item.Key);
                        string foreignKey = childColumnList.First(f => f.RefTable == mainData.TableName).ColName;
                        //先删除后增加
                        _dbContext.DeleteExec(item.Key, $"{foreignKey}='{mainData.Get("Fid")}'");
                        foreach (var data in item.Value)
                        {
                            //赋值外键
                            data.Add(foreignKey, mainData.Get("Fid").ToString());
                            _dbContext.InsertDynamicData(data);
                        }
                    }
                }
            }
            void DeleteChildData(dynamic mainData)
            {
                //检查子表，删除子表数据
                var childtableList = _dbContext.Tables(t => t.ExtTable == mainData.TableName);
                if (childtableList != null && childtableList.Any())
                {
                    foreach (var childTable in childtableList)
                    {
                        var childColumnList = _dbContext.Columns(childTable.TableName);
                        string foreignKey = childColumnList.First(f => f.RefTable == mainData.TableName).ColName;
                        _dbContext.DeleteExec(childTable.TableName, $"{foreignKey} = @Fid", new DynamicParameters(new { Fid = mainData.Fid }));
                    }
                }
            }
        }




    }

    public enum OperEnum
    {
        add, edit, del, none
    }
}

