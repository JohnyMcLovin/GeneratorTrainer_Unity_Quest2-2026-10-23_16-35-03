using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace GeneratorTrainer
{
    public sealed class RunLogger
    {
        readonly string path, session;
        readonly float start;

        public RunLogger()
        {
            session = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            start = Time.time;
            path = Path.Combine(Application.persistentDataPath, "run_" + session + ".csv");
            File.WriteAllText(path, "session_id,time_s,step_before,action,result,step_after,message\n");
            Debug.Log("[RunLogger] " + path);
        }

        public void Row(string before, string action, string result, string after, string message)
        {
            string t = (Time.time - start).ToString("F2", CultureInfo.InvariantCulture);
            string m = "\"" + message.Replace("\"", "\"\"") + "\"";
            File.AppendAllText(path, string.Join(",", session, t, before, action, result, after, m) + "\n");
        }
    }
}