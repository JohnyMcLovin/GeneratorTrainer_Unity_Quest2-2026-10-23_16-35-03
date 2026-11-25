using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace GeneratorTrainer
{
    // One CSV file per run in Application.persistentDataPath.
    public sealed class RunLogger
    {
        readonly string path, session, fault;
        bool created;

        public RunLogger(string fault)
        {
            this.fault = fault;
            session = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            path = Path.Combine(Application.persistentDataPath, "run_" + session + ".csv");
            // The file is created on the first row, so a computer that never logs (e.g. the second player in
            // multiplayer, which only mirrors the shared state) leaves no empty CSV behind.
        }

        public void Row(string before, string action, string result, string after, string message, TrainingState s)
        {
            if (!created)
            {
                created = true;
                File.WriteAllText(path, "session_id,fault,time_s,step_before,action,result,step_after,wrong_count,penalty_s,total_s,message\n");
                Debug.Log("[RunLogger] " + path);
            }
            var c = CultureInfo.InvariantCulture;
            string m = "\"" + message.Replace("\"", "\"\"") + "\"";
            File.AppendAllText(path, string.Join(",", session, fault, s.elapsed.ToString("F2", c), before, action, result, after,
                s.wrongCount.ToString(c), s.penaltySeconds.ToString("F0", c), s.TotalTime.ToString("F2", c), m) + "\n");
        }
    }
}
