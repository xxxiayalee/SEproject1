using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SearchAThing.Util;

namespace WorkedHoursTrackerWebapi
{

    public class Config
    {

        Global global { get { return Global.Instance; } }

        object lck = new object();

        public Config()
        {
            if (Credentials == null) Credentials = new List<CredInfo>();
        }

        public void SaveCred(CredInfo cred)
        {
            lock (lck)
            {
                if (string.IsNullOrEmpty(cred.GUID))
                {                    
                    if (cred.Username == "admin") throw new Exception($"cannot create builtin admin account");
                    cred.GUID = Guid.NewGuid().ToString();

                    // trim spaces                    
                    cred.Password = cred.Password?.Trim();                    

                    cred.CreateTimestamp = DateTime.UtcNow;

                    Credentials.Add(cred);
                }
                else
                {
                    var q = Credentials.FirstOrDefault(w => w.GUID == cred.GUID);
                    if (q == null) throw new Exception($"unable to find [{cred.GUID}] entry");

                    q.Password = cred.Password?.Trim();                    
                    q.ModifyTimestamp = DateTime.UtcNow;
                }                
            }
            Save();
        }

        public CredInfo LoadCred(string guid)
        {
            CredInfo nfo;
            lock (lck)
            {
                nfo = Credentials.FirstOrDefault(w => w.GUID == guid);
            }

            return nfo;
        }

        public void DeleteCred(string guid)
        {
            CredInfo nfo;
            lock (lck)
            {
                nfo = Credentials.FirstOrDefault(w => w.GUID == guid);
                if (nfo.Username == "admin") throw new Exception($"cannot delete builtin admin account");

                if (nfo != null) Credentials.Remove(nfo);
            }
        }

        public List<CredInfo> GetCredList(string filter)
        {
            List<CredInfo> res;

            if (!string.IsNullOrEmpty(filter)) filter = filter.Trim();

            lock (lck)
            {
                res = Credentials.Where(r => new[] { r.Username }.MatchesFilter(filter))
                .ToList();
            }

            return res;
        }

        public void Save()
        {
            lock (lck)
            {
                try
                {
                    if (File.Exists(Global.AppConfigPathfilename))
                        File.Copy(Global.AppConfigPathfilename, Global.AppConfigPathfilenameBackup, true);
                }
                catch (Exception ex)
                {
                    global.LogError($"unable to backup config file [{Global.AppConfigPathfilename}] to [{Global.AppConfigPathfilenameBackup}] : {ex.Message}");
                }
                File.WriteAllText(Global.AppConfigPathfilename, JsonConvert.SerializeObject(this, Formatting.Indented));
                // save with mode 600
                LinuxHelper.SetFilePermission(Global.AppConfigPathfilename, 384);
            }
        }

        public string AdminPassword { get; set; }

        public List<CredInfo> Credentials { get; set; }

    }

}