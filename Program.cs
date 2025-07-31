using System.Threading;

namespace WaterTracker;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        const string mutexName = "WaterTrackerSingleInstanceMutex";
        using (Mutex mutex = new Mutex(true, mutexName))
        {
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new Form1());
            }
            else
            {
                MessageBox.Show("WaterTracker进程已存在，无法重复运行。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }    
}