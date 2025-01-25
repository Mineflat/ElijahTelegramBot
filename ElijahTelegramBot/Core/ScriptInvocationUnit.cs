using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElijahTelegramBot.Core
{
    internal class ScriptInvocationUnit
    {
        public int ScriptID { get; protected set; } = 0;
        public DateTime StartTime { get; protected set; }
        public DateTime? EndTime { get; protected set; }
        public long ReplyChatID { get; protected set; } = 0;
        public long InvokeUID { get; protected set; } = 0;
        public string ComandLine { get; protected set; } = string.Empty;
        public bool IsCancelled { get; protected set; } = false;
        public DateTime? CleanTime { get; protected set; } = null;
        public ScriptInvocationUnit(int scriptID, long replyChatID, long invokeUID, string comandLine)
        {
            ScriptID = scriptID;
            ReplyChatID = replyChatID;
            InvokeUID = invokeUID;
            ComandLine = comandLine;
        }
        public async Task<string> ExecuteScriptAsync(string script)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{script}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return string.IsNullOrWhiteSpace(error) ? output : $"Output: {output}\nError: {error}";
        }
    }
}
