﻿using Fap.Core.DataAccess;
using Fap.Core.DI;
using Fap.Core.Infrastructure.Metadata;
using Fap.Core.Rbac.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace XUnitTestFapCore
{
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
    public class User : IUser
    {
        private IDbContext _dbContext;
        public User(IDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public bool DeleteDynamicLogic()
        {
            var emp = _dbContext.Get("FapUser", 31);
            dynamic empd = new FapDynamicObject(_dbContext.Columns("FapUser"));
            empd.Id = emp.Id;
            empd.Ts = emp.Ts;
            return _dbContext.DeleteDynamicData(empd);
        }

        public bool DeleteDynamicTrace()
        {
            var emp = _dbContext.Get("Employee", 73);
            dynamic empd = new FapDynamicObject(_dbContext.Columns("Employee"));
            empd.Id = emp.Id;
            empd.Ts=emp.Ts;
           return  _dbContext.DeleteDynamicData(empd);
        }

        public bool DeleteLogic()
        {
            return _dbContext.Delete<FapUser>(31);
        }

        public bool DeleteTrace()
        {
            return _dbContext.Delete<Employee>(73);
        }

        [Transactional]
        public bool ModifyEmployee(string pinyin)
        {
            //var emp= _dbContext.Get("Employee", "3534239003521843200");
            //dynamic keyValuePairs = new FapDynamicObject("Employee",emp.Id);         
            //keyValuePairs.EmpPinYin = pinyin;
            //keyValuePairs.Fid = emp.Fid;
            //var b= _dbContext.UpdateDynamicData(keyValuePairs);
            var emp = _dbContext.Get<Employee>(73);

            emp.EmpPinYin = pinyin;
            
            var b = _dbContext.Update(emp);
            return b;
        }
        [Transactional]
        public bool ModifyEmployeeDynamic(string pinyin)
        {
            var emp= _dbContext.Get("Employee", 3094);
          
            dynamic empd = new FapDynamicObject(_dbContext.Columns("Employee"));
            empd.Id = emp.Id;
            empd.Ts = emp.Ts;
            empd.EmpPinYin = pinyin;
            return _dbContext.UpdateDynamicData(empd);
        }

        [Transactional]
        public bool ModifyUser(string pinyin)
        {
            //var emp= _dbContext.Get("Employee", "3534239003521843200");
            //dynamic keyValuePairs = new FapDynamicObject("Employee",emp.Id);         
            //keyValuePairs.EmpPinYin = pinyin;
            //keyValuePairs.Fid = emp.Fid;
            //var b= _dbContext.UpdateDynamicData(keyValuePairs);
            var emp = _dbContext.Get<FapUser>(31);

            emp.UserPhone = pinyin;

            var b = _dbContext.Update(emp);
            return b;
        }

        public bool ModifyUserDynamic(string pinyin)
        {
            var emp = _dbContext.Get("FapUser", 152);
            dynamic empd = new FapDynamicObject(_dbContext.Columns("FapUser"));
            empd.Id = emp.Id;
            empd.Ts = emp.Ts;
            empd.UserPhone = pinyin;
            return _dbContext.UpdateDynamicData(empd);
        }
    }
}
