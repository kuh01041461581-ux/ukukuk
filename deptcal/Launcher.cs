// 부서 일정 공유 달력 - 실행 파일(exe) 런처
// DeptCalendarApp.ps1 을 리소스로 내장하고, 윈도우 PowerShell 엔진으로 같은 프로세스에서 실행합니다.
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Windows.Forms;

static class Program
{
    static void Log(string msg)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeptCalendar");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), DateTime.Now.ToString("s") + " [exe] " + msg + Environment.NewLine, Encoding.UTF8);
        }
        catch { }
    }

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            Environment.SetEnvironmentVariable("DEPT_EXE_PATH", asm.Location);
            string script;
            using (Stream s = asm.GetManifestResourceStream("app.ps1"))
            using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                script = r.ReadToEnd();

            InitialSessionState iss = InitialSessionState.CreateDefault();
            iss.ApartmentState = ApartmentState.STA;
            iss.ThreadOptions = PSThreadOptions.UseCurrentThread;
            try { iss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass; } catch { }

            using (Runspace rs = RunspaceFactory.CreateRunspace(iss))
            {
                rs.Open();
                using (PowerShell ps = PowerShell.Create())
                {
                    ps.Runspace = rs;
                    ps.AddScript(script);
                    ps.Invoke();
                    if (ps.Streams.Error.Count > 0)
                    {
                        foreach (ErrorRecord er in ps.Streams.Error) Log(er.ToString());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            MessageBox.Show("달력을 시작하지 못했습니다.\n" + ex.Message, "부서 일정 공유 달력");
            return 1;
        }
        return 0;
    }
}
