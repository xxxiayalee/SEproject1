using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SearchAThing.Util;

namespace WorkedHoursTrackerWebapi.Controllers
{

    [Route("[controller]/[action]")]
    public class ApiController : Controller
    {

        private readonly IGlobal global;

        private readonly ILogger logger;
        private readonly MyDbContext ctx;

        #region constructor
        public ApiController(MyDbContext ctx, ILogger<ApiController> logger)
        {
            this.logger = logger;
            this.ctx = ctx;
            logger.LogDebug($"ApiController created");
            EnsureAdminAccount();
        }

        void EnsureAdminAccount()
        {
            if (!ctx.Users.Any(w => w.Username == "admin"))
            {
                ctx.Users.Add(new User()
                {
                    Username = "admin",
                    Password = "admin",
                    CreateTimestamp = DateTime.Now
                });
                ctx.SaveChanges();
            }
        }
        #endregion

        #region helpers
        CommonResponse InvalidAuthResponse()
        {
            return new CommonResponse() { ExitCode = CommonResponseExitCodes.InvalidAuth };
        }

        CommonResponse SuccessfulResponse()
        {
            return new CommonResponse() { ExitCode = CommonResponseExitCodes.Successful };
        }

        CommonResponse ErrorResponse(string errMsg)
        {
            return new CommonResponse()
            {
                ExitCode = CommonResponseExitCodes.Error,
                ErrorMsg = errMsg
            };
        }

        bool CheckAuth(string username, string password)
        {
            var qdb = ctx.Users.FirstOrDefault(w => w.Username == username);

            var is_valid = qdb != null && qdb.Password == password;

            if (!is_valid)
            {
                var q = HttpContext.Request.Headers["X-Real-IP"];
                var url = "";
                if (q.Count > 0) url = q.First();

                logger.LogWarning($"invalid login attempt from [{url}]");
                // todo : autoban
            }

            return is_valid;
        }
        #endregion

        #region USERS

        [HttpPost]
        public CommonResponse SaveUser(string username, string password, User jUser)
        {
            try
            {
                // disallow non admin
                if (jUser == null || username != "admin" || !CheckAuth(username, password)) return InvalidAuthResponse();

                User user = null;

                if (jUser.id == 0)
                {
                    if (jUser.Username == "admin") throw new Exception($"cannot create builtin admin account");

                    user = new User()
                    {
                        Username = jUser.Username,
                        CreateTimestamp = DateTime.UtcNow
                    };

                    ctx.Users.Add(user);
                }
                else
                {
                    user = ctx.Users.FirstOrDefault(w => w.id == jUser.id);
                    if (user == null) throw new Exception($"unable to find [{jUser.id}] entry");

                    user.ModifyTimestamp = DateTime.UtcNow;
                }
                user.Password = jUser.Password?.Trim();
                user.Cost = jUser.Cost;

                ctx.SaveChanges();

                return SuccessfulResponse();
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        [HttpPost]
        public CommonResponse LoadUser(string username, string password, int id)
        {
            try
            {
                // disallow non admin
                if (username != "admin" || !CheckAuth(username, password)) return InvalidAuthResponse();

                var response = new UserResponse();

                response.User = ctx.Users.FirstOrDefault(w => w.id == id);

                return response;
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        [HttpPost]
        public CommonResponse DeleteUser(string username, string password, int id)
        {
            try
            {
                // disallow non admin
                if (username != "admin" || !CheckAuth(username, password)) return InvalidAuthResponse();

                var q = ctx.Users.FirstOrDefault(w => w.id == id);
                if (q != null)
                {
                    ctx.Users.Remove(q);
                    ctx.SaveChanges();
                }

                return SuccessfulResponse();
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        [HttpPost]
        public CommonResponse UserList(string username, string password, string filter)
        {
            try
            {
                // disallow non admin
                if (username != "admin" || !CheckAuth(username, password)) return InvalidAuthResponse();

                var response = new UserListResponse();

                response.UserList = ctx.Users.ToList().Where(r => new[] { r.Username }.MatchesFilter(filter)).ToList();

                return response;
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        #endregion

        #region CONTACTS

        [HttpPost]
        public CommonResponse SaveCustomer(string username, string password, Customer jCustomer)
        {
            try
            {
                if (!CheckAuth(username, password)) return InvalidAuthResponse();

                Customer customer = null;
                if (jCustomer.id == 0)
                {
                    customer = new Customer()
                    {
                        CreateTimestamp = DateTime.UtcNow
                    };
                    
                    ctx.Customers.Add(customer);
                }
                else
                {
                    customer = ctx.Customers.FirstOrDefault(w => w.id == jCustomer.id);
                    if (customer == null) throw new Exception($"unable to find [{jCustomer.id}] entry");
                }
                customer.Name = jCustomer.Name.Trim();
                ctx.SaveChanges();

                return SuccessfulResponse();
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        [HttpPost]
        public CommonResponse LoadCustomer(string username, string password, int id)
        {
            try
            {
                if (!CheckAuth(username, password)) return InvalidAuthResponse();

                var response = new ContactInfoResponse();

                response.Customer = ctx.Customers.FirstOrDefault(w => w.id == id);

                return response;
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        [HttpPost]
        public CommonResponse DeleteCustomer(string username, string password, int id)
        {
            try
            {
                if (!CheckAuth(username, password)) return InvalidAuthResponse();

                var q = ctx.Customers.FirstOrDefault(w => w.id == id);

                if (q != null)
                {
                    ctx.Customers.Remove(q);
                    ctx.SaveChanges();
                }

                return SuccessfulResponse();
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        [HttpPost]
        public CommonResponse CustomerList(string username, string password, string filter)
        {
            try
            {
                if (!CheckAuth(username, password)) return InvalidAuthResponse();

                var response = new CustomerListResponse();

                response.customerList = ctx.Customers.ToList().Where(r => new[] { r.Name }.MatchesFilter(filter)).ToList();

                return response;
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        #endregion

        [HttpPost]
        public CommonResponse IsAuthValid(string username, string password)
        {
            try
            {
                if (!CheckAuth(username, password)) return InvalidAuthResponse();
                return SuccessfulResponse();
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

    }

}
