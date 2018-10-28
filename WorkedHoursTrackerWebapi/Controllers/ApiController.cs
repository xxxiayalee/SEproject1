using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchAThing.Util;
using SearchAThing.EFUtil;
using SearchAThing.PsqlUtil;
using ClosedXML.Excel;
using System.IO;

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
            if (!ctx.Users.Any(w => w.username == "admin"))
            {
                ctx.Users.Add(new User()
                {
                    username = "admin",
                    password = "admin",
                    create_timestamp = DateTime.Now
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

        (bool authValid, bool canEditJobs, bool canEditActivities)
        CheckAuth(string username, string password)
        {
            var qdb = ctx.Users.FirstOrDefault(w => w.username == username);

            var is_valid = qdb != null && qdb.password == password;

            if (!is_valid)
            {
                var q = HttpContext.Request.Headers["X-Real-IP"];
                var url = "";
                if (q.Count > 0) url = q.First();

                logger.LogWarning($"invalid login attempt from [{url}]");
                // todo : autoban
            }

            return (is_valid, username == "admin" || qdb.can_edit_jobs, username == "admin" || qdb.can_edit_activities);
        }
        #endregion

        #region USERS

        [HttpPost]
        public CommonResponse SaveUser(string username, string password, User jUser)
        {
            try
            {
                // disallow non admin
                if (jUser == null || username != "admin" || !CheckAuth(username, password).authValid) return InvalidAuthResponse();

                User user = null;

                if (jUser.id == 0)
                {
                    if (jUser.username == "admin") throw new Exception($"cannot create builtin admin account");

                    user = new User()
                    {
                        create_timestamp = DateTime.UtcNow
                    };

                    ctx.Users.Add(user);
                }
                else
                {
                    user = ctx.Users.FirstOrDefault(w => w.id == jUser.id);
                    if (user == null) throw new Exception($"unable to find [{jUser.id}] entry");

                    user.modify_timestamp = DateTime.UtcNow;
                }
                user.username = jUser.username;
                user.password = jUser.password?.Trim();
                user.cost = jUser.cost;
                if (username == "admin")
                {
                    user.can_edit_jobs = jUser.can_edit_jobs;
                    user.can_edit_activities = jUser.can_edit_activities;
                }

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
                if (username != "admin" || !CheckAuth(username, password).authValid) return InvalidAuthResponse();

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
                if (username != "admin" || !CheckAuth(username, password).authValid) return InvalidAuthResponse();

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
                if (username != "admin" || !CheckAuth(username, password).authValid) return InvalidAuthResponse();

                var response = new UserListResponse();

                response.UserList = ctx.Users.ToList().Where(r => new[] { r.username }.MatchesFilter(filter)).ToList();

                return response;
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        #endregion

        #region ACTIVITIES

        [HttpPost]
        public CommonResponse SaveActivity(string username, string password, Activity jActivity)
        {
            try
            {
                var qa = CheckAuth(username, password);
                if (!qa.authValid || !qa.canEditActivities) return InvalidAuthResponse();

                Activity activity = null;
                if (jActivity.id == 0)
                {
                    activity = new Activity();

                    ctx.Activities.Add(activity);
                }
                else
                {
                    activity = ctx.Activities.FirstOrDefault(w => w.id == jActivity.id);
                    if (activity == null) throw new Exception($"unable to find [{jActivity.id}] entry");
                }
                activity.name = jActivity.name.Trim();
                activity.description = jActivity.description;
                ctx.SaveChanges();

                return SuccessfulResponse();
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        [HttpPost]
        public CommonResponse LoadActivity(string username, string password, int id_activity)
        {
            try
            {
                var qa = CheckAuth(username, password);
                if (!qa.authValid || !qa.canEditActivities) return InvalidAuthResponse();

                var response = new ActivityInfoResponse();

                response.Activity = ctx.Activities.FirstOrDefault(w => w.id == id_activity);

                return response;
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        [HttpPost]
        public CommonResponse DeleteActivity(string username, string password, int id_activity)
        {
            try
            {
                var qa = CheckAuth(username, password);
                if (!qa.authValid || !qa.canEditActivities) return InvalidAuthResponse();

                var q = ctx.Activities.FirstOrDefault(w => w.id == id_activity);

                if (q != null)
                {
                    ctx.Activities.Remove(q);
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
        public CommonResponse ActivityList(string username, string password, string filter)
        {
            try
            {
                if (!CheckAuth(username, password).authValid) return InvalidAuthResponse();

                var response = new ActivityListResponse();

                response.activityList = ctx.Activities.ToList();

                return response;
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        #endregion       

        #region JOBS

        [HttpPost]
        public CommonResponse SaveJob(string username, string password, Job jJob)
        {
            try
            {
                var qa = CheckAuth(username, password);
                if (!qa.authValid || !qa.canEditJobs) return InvalidAuthResponse();

                Job job = null;
                if (jJob.id == 0)
                {
                    job = new Job()
                    {
                        create_timestamp = DateTime.UtcNow
                    };

                    ctx.Jobs.Add(job);
                }
                else
                {
                    job = ctx.Jobs.FirstOrDefault(w => w.id == jJob.id);
                    if (job == null) throw new Exception($"unable to find [{jJob.id}] entry");
                }
                job.name = jJob.name.Trim();
                job.base_cost = jJob.base_cost;
                job.min_cost = jJob.min_cost;
                job.cost_factor = jJob.cost_factor;
                job.minutes_round = jJob.minutes_round;
                ctx.SaveChanges();

                return SuccessfulResponse();
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        [HttpPost]
        public CommonResponse LoadJob(string username, string password, int id_job)
        {
            try
            {
                var qa = CheckAuth(username, password);
                if (!qa.authValid || !qa.canEditJobs) return InvalidAuthResponse();

                var response = new ContactInfoResponse();

                response.Job = ctx.Jobs.FirstOrDefault(w => w.id == id_job);

                return response;
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        [HttpPost]
        public CommonResponse DeleteJob(string username, string password, int id_job)
        {
            try
            {
                var qa = CheckAuth(username, password);
                if (!qa.authValid || !qa.canEditJobs) return InvalidAuthResponse();

                var q = ctx.Jobs.FirstOrDefault(w => w.id == id_job);

                if (q != null)
                {
                    ctx.Jobs.Remove(q);
                    ctx.SaveChanges();
                }

                return SuccessfulResponse();
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        public class tmptype
        {
            public long id_job;
            public double? hours_sum;
        }

        public class tmptype2
        {
            public long id_job;
            public bool is_active;
            public long id_activity;
            public DateTime trigger_timestamp;
        }

        [HttpPost]
        public CommonResponse JobList(string username, string password, string filter)
        {
            try
            {
                if (!CheckAuth(username, password).authValid) return InvalidAuthResponse();

                var response = new JobListResponse();

                var qactivity_desc = ctx.Activities.ToDictionary(k => k.id, v => v.name);

                Func<long, string> GetActivityDesc = (id_activity) =>
                {
                    string res = "";
                    qactivity_desc.TryGetValue(id_activity, out res);
                    return res;
                };

                var user = ctx.Users.First(w => w.username == username);

                var query = $@"
select
	j.id id_job, sum(uj.hours_increment) hours_sum
from
	job j
left join
	userjob uj on j.id = uj.id_job and uj.id_user={user.id}
group by j.id";

                var resTotalHours = ctx.ExecSQL<tmptype>(query).ToDictionary(w => w.id_job, w => w.hours_sum);

                if (resTotalHours.Count > 0)
                {

                    query = $@"
select uj.id_job, sum(uj.hours_increment) hours_sum from ""user"" u
left join userjob uj on u.id = uj.id_user
where uj.id_user={user.id} and uj.trigger_timestamp>{(DateTime.UtcNow - TimeSpan.FromDays(1)).ToPsql()}
group by uj.id_job";

                    var resLast24Hours = ctx.ExecSQL<tmptype>(query).ToDictionary(w => w.id_job, w => w.hours_sum);

                    // build job_ids
                    var job_ids = resTotalHours.Select(w => w.Key).ToArray();
                    var job_ids_str = string.Join(',', job_ids.Select(w => w.ToString()));

                    // retrieve is_active
                    query = $@"
select a.id_job, a.is_active, a.trigger_timestamp, a.id_activity from
(
select
    uj.id_job,
    first(uj.is_active order by uj.trigger_timestamp desc) is_active,
    first(uj.trigger_timestamp order by uj.trigger_timestamp desc) trigger_timestamp,
    first(uj.id_activity order by uj.trigger_timestamp desc) id_activity
from
    userjob uj
where
    uj.id_user={user.id} and uj.id_job in ({job_ids_str})
group by id_job
) a";
                    var resJobNfo = ctx.ExecSQL<tmptype2>(query).ToDictionary(w => w.id_job, v => v);

                    var jobList = ctx.Jobs.AsNoTracking().Where(r => job_ids.Contains(r.id)).ToList();

                    foreach (var job in jobList)
                    {
                        var r = new UserJobNfo()
                        {
                            job = job
                        };

                        tmptype2 jnfo = null;
                        if (resJobNfo.TryGetValue(job.id, out jnfo))
                        {
                            r.is_active = jnfo.is_active;
                            r.trigger_timestamp = jnfo.trigger_timestamp;
                            r.activity = GetActivityDesc(jnfo.id_activity);

                            r.total_hours = resTotalHours[job.id].GetValueOrDefault();

                            double? last24h = null;
                            if (resLast24Hours.TryGetValue(job.id, out last24h))
                                r.last_24_hours = last24h.GetValueOrDefault();

                            if (r.is_active)
                            {
                                var working_increment_h = (DateTime.UtcNow - r.trigger_timestamp).TotalHours;

                                r.total_hours += working_increment_h;
                                r.last_24_hours += working_increment_h;
                            }
                        }

                        response.userJobList.Add(r);
                    }

                    if (username != "admin")
                    {
                        foreach (var x in response.userJobList)
                        {
                            x.job.base_cost = 0;
                            x.job.min_cost = 0;
                            x.job.cost_factor = 0;
                            x.job.minutes_round = 0;
                        }
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        #endregion

        #region USER JOB        

        #region REPORT

        (IXLRange rng_used, int col_cnt, int row_cnt) FinalizeWorksheet(IXLWorksheet ws)
        {
            var rng_used = ws.RangeUsed();
            var col_cnt = rng_used.ColumnCount();
            var row_cnt = rng_used.RowCount();

            (IXLRange rng_used, int row_cnt, int col_cnt) res = (rng_used, row_cnt, col_cnt);

            ws.Range(1, 1, row_cnt, col_cnt).SetAutoFilter();
            for (int c = 1; c <= col_cnt; c++) ws.Column(c).AdjustToContents();

            ws.SheetView.Freeze(1, 0);

            return res;
        }

        [HttpGet]
        public async Task<IActionResult> DownloadReport(string username, string password)
        {
            if (username != "admin" || !CheckAuth(username, password).authValid) return null;

            var pathfilename = System.IO.Path.GetTempFileName() + ".xlsx";

            using (var wb = new XLWorkbook())
            {
                var wspivot = wb.AddWorksheet("summary");
                var ws = wb.AddWorksheet("detail");

                var col = 1;
                var row = 1;

                IXLCell cell = null;

                Action<int, int, object> SetCell = (r, c, v) =>
                {
                    cell = ws.Cell(r, c);
                    cell.Value = v;
                };

                Action<int, int, object> SetCellBold = (r, c, v) =>
                {
                    cell = ws.Cell(r, c);
                    cell.Value = v;
                    cell.Style.Font.SetBold();
                };

                SetCellBold(row, col++, "Activity");
                SetCellBold(row, col++, "Job");
                SetCellBold(row, col++, "Cost(base)");
                SetCellBold(row, col++, "Cost(min)");
                SetCellBold(row, col++, "Cost(factor)");
                SetCellBold(row, col++, "Minutes round");

                SetCellBold(row, col++, "User");
                SetCellBold(row, col++, "cost/h");
                SetCellBold(row, col++, "date");
                SetCellBold(row, col++, "hours");
                SetCellBold(row, col++, "cost");
                SetCellBold(row, col++, "notes");

                var users = ctx.Users.ToList();
                ctx.Jobs.Load();

                foreach (var u in users) // loop over users
                {
                    var quserjobs = ctx.UserJobs
                    .Include("activity")
                    .Where(r => r.user.id == u.id).OrderBy(w => w.trigger_timestamp).ToList();

                    foreach (var uj in quserjobs) // loop over user jobs
                    {
                        if (!uj.is_active)
                        {
                            ++row;
                            col = 1;

                            SetCell(row, col++, uj.activity?.name);
                            SetCell(row, col++, uj.job.name);
                            SetCell(row, col++, uj.job.base_cost);
                            SetCell(row, col++, uj.job.min_cost);
                            SetCell(row, col++, uj.job.cost_factor);
                            SetCell(row, col++, uj.job.minutes_round);

                            SetCell(row, col++, u.username);
                            SetCell(row, col++, u.cost);
                            SetCell(row, col++, uj.trigger_timestamp);
                            SetCell(row, col++, uj.hours_increment);
                            //SetCell(row, col++, uj.job.Cost(uj.hours_increment, u.cost));
                            // Max(base_cost + (hours * 60).MRound(minutes_round) / 60 * hourCost * cost_factor, min_cost);
                            cell = ws.Cell(row, col++);
                            cell.FormulaR1C1 = "=MAX(RC[-8]+MROUND(RC[-1]*60,RC[-5])/60*RC[-3]*RC[-6],RC[-7])";
                            SetCell(row, col++, uj.notes);
                        }
                    }
                }
                FinalizeWorksheet(ws);
                
                // https://github.com/ClosedXML/ClosedXML/wiki/Pivot-Table-example/9c7bf53d9f483ee1b2281e9cd369fe3fd06caa2c
                var pt = wspivot.PivotTables.Add("pivot", wspivot.Cell(1, 1), ws.RangeUsed());

                pt.ReportFilters.Add("Job");
                pt.RowLabels.Add("User");
                pt.ColumnLabels.Add("Activity");
                pt.Values.Add("hours");
                pt.Values.Add("cost");                

                wb.SaveAs(pathfilename);
            }

            var ms = new MemoryStream();
            using (var stream = new FileStream(pathfilename, FileMode.Open))
            {
                await stream.CopyToAsync(ms);
            }
            System.IO.File.Delete(pathfilename);
            ms.Position = 0;
            var dtnow = DateTime.Now;

            return File(ms, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"wht-{dtnow.Year}-{dtnow.Month}-{dtnow.Day}-report.xlsx");
        }

        #endregion

        [HttpPost]
        public CommonResponse TriggerJob(string username, string password, int id_job, string activity)
        {
#if !DEBUG
            try
#endif
            {
                if (!CheckAuth(username, password).authValid) return InvalidAuthResponse();

                var user = ctx.Users.First(w => w.username == username);
                var id_user = user.id;

                var qactivity = ctx.Activities.FirstOrDefault(w => w.name == activity);

                var job = ctx.Jobs.First(w => w.id == id_job);

                var last = ctx.UserJobs
                .Include("activity")
                .Where(r => r.user.id == id_user && r.job.id == id_job)
                .OrderByDescending(w => w.trigger_timestamp).FirstOrDefault();

                UserJob newEntry = null;
                newEntry = new UserJob()
                {
                    user = user,
                    activity = qactivity,
                    job = job,
                    trigger_timestamp = DateTime.UtcNow
                };

                if (last == null || !last.is_active)
                {
                    if (qactivity == null) throw new Exception($"could find activity [{activity}]");
                    newEntry.activity = qactivity;
                }

                if (last == null)
                {
                    newEntry.is_active = true;
                }
                else
                {
                    switch (last.is_active)
                    {
                        case true:
                            {
                                newEntry.is_active = false;
                                newEntry.hours_increment = (newEntry.trigger_timestamp - last.trigger_timestamp).TotalHours;
                                newEntry.activity = last.activity;
                            }
                            break;

                        case false:
                            {
                                newEntry.is_active = true;
                            }
                            break;
                    }
                }
                ctx.UserJobs.Add(newEntry);
                ctx.SaveChanges();

                return SuccessfulResponse();
            }
#if !DEBUG
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
#endif
        }

        [HttpPost]
        public CommonResponse GetJobNotes(string username, string password, int id_job)
        {
            try
            {
                if (!CheckAuth(username, password).authValid) return InvalidAuthResponse();

                var user = ctx.Users.First(w => w.username == username);
                var id_user = user.id;

                var job = ctx.Jobs.First(w => w.id == id_job);

                var last = ctx.UserJobs.Where(r => r.user.id == id_user && r.job.id == id_job).OrderByDescending(w => w.trigger_timestamp).FirstOrDefault();

                if (last == null) throw new Exception($"no worked hours for this entry");


                return new UserJobNotesResponse()
                {
                    Notes = last.notes
                };
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }


        [HttpPost]
        public CommonResponse SaveJobNotes(string username, string password, int id_job, string notes)
        {
            try
            {
                if (!CheckAuth(username, password).authValid) return InvalidAuthResponse();

                var user = ctx.Users.First(w => w.username == username);
                var id_user = user.id;

                var job = ctx.Jobs.First(w => w.id == id_job);

                var last = ctx.UserJobs.Where(r => r.user.id == id_user && r.job.id == id_job).OrderByDescending(w => w.trigger_timestamp).FirstOrDefault();

                last.notes = notes;

                ctx.SaveChanges();

                return SuccessfulResponse();
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
                if (!CheckAuth(username, password).authValid) return InvalidAuthResponse();

                var res = new UserAuthNfoResponse();

                var q = ctx.Users.First(w => w.username == username);
                res.CanEditJobs = q.can_edit_jobs;
                res.CanEditActivities = q.can_edit_activities;

                return res;
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

    }

}
