using SaleaeAutomationApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SaleaeLogger
{
    public class LoggingThread
    {
        private EventHandler<LoggingEventArgs> loggingEventHandler;

        private SocketAPI saleae;
        private LoggingIndexWriter indxWriter;

        public int ScanSeconds { get; private set; }
        public int BurstSeconds { get; private set; }

        private CancellationTokenSource canTokSrc;

        public LoggingThread(SocketAPI saleae, int ScanSeconds, int BurstSeconds, EventHandler<LoggingEventArgs> CallerLoggingEventHandler)
        {
            this.saleae = saleae;
            this.ScanSeconds = ScanSeconds;
            this.BurstSeconds = BurstSeconds;
            Register(CallerLoggingEventHandler);
        }

        public void StartScan(int rate)
        {
            StopScan();
            canTokSrc = new CancellationTokenSource();
            Task.Factory.StartNew(() => { LoggingScanThread(canTokSrc.Token, ScanSeconds, rate); }, canTokSrc.Token);
        }

        public void StopScan()
        {
            if (canTokSrc != null)
            {
                canTokSrc.Cancel();
                canTokSrc = null;
            }
        }


        private void LoggingScanThread(CancellationToken canTok, int seconds, int rate)
        {
            Thread.CurrentThread.Name = "LoggingScanThread";

            // **** Turn off all triggers ****
            SaleaeAutomationApi.Trigger[] t = new SaleaeAutomationApi.Trigger[8];
            for (int i = 0; i < t.Length; i++)
            {
                t[i] = SaleaeAutomationApi.Trigger.None;
            }

            saleae.SetNumSamples(BurstSeconds * rate);

            // **** Do the data logging ****
            DateTime started = DateTime.Now;

            string saveFolder = @"C:\_LogicData\" +
                started.Year + "_" + started.Month.ToString("00") + "_" + started.Day.ToString("00") + "-" +
                started.Hour.ToString("00") + "_" + started.Minute.ToString("00") + "_" + started.Second.ToString("00") +
                @"\";
            if (!Directory.Exists(saveFolder))
            {
                Directory.CreateDirectory(saveFolder);
            }

            indxWriter = new LoggingIndexWriter(Register);
            OnLoggingEvent(new LoggingStartedEventArgs(saveFolder));
            while (DateTime.Now.Subtract(started).TotalSeconds < seconds)
            {
                if (canTok.IsCancellationRequested)
                {   // Cancel me!
                    break;
                }

                double remain = seconds - DateTime.Now.Subtract(started).TotalSeconds;
                if (remain < BurstSeconds)
                {   // Remaining time is less than burst time
                    saleae.SetNumSamples((int)(remain * rate));
                }

                TimeSpan scanTime = DateTime.Now.Subtract(started);
                string filename = System.IO.Path.Combine(saveFolder, "data" + scanTime.TotalSeconds.ToString("00000") + ".logicdata");
                saleae.CaptureToFile(filename);
                OnLoggingEvent(new LoggingFileEventArgs(filename, scanTime));
            }
            OnLoggingEvent(new LoggingStoppedEventArgs());
        }

        private void Register(EventHandler<LoggingEventArgs> handler)
        {
            loggingEventHandler -= handler; // Prevents double registration
            loggingEventHandler += handler;
        }

        private void OnLoggingEvent(LoggingEventArgs args)
        {
            if( loggingEventHandler != null )
            {
                loggingEventHandler(this, args);
            }
        }
    }

    //==================================================================
    public class LoggingEventArgs : EventArgs
    {
        public DateTime Timestamp { get; private set; }

        protected LoggingEventArgs()
        {
            Timestamp = DateTime.Now;
        }
    }

    public class LoggingStartedEventArgs : LoggingEventArgs
    {
        public string SavedToFolder { get; private set; }

        public LoggingStartedEventArgs(string saveFolder)
            : base()
        {
            SavedToFolder = saveFolder;
        }
    }

    public class LoggingStoppedEventArgs : LoggingEventArgs
    {
        public LoggingStoppedEventArgs()
            : base()
        {
        }
    }

    public class LoggingFileEventArgs : LoggingEventArgs
    {
        public string File { get; private set; }
        public TimeSpan ScanTime { get; private set; }

        public LoggingFileEventArgs(string file, TimeSpan scanTime)
            : base()
        {
            this.File = file;
            this.ScanTime = scanTime;
        }
    }

}
