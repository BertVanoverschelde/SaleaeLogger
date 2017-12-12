using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SaleaeAutomationApi;
using System.Windows;
using System.IO;
using System.Threading;

namespace SaleaeLogger
{
    class MainWindowViewModel : PropertyChangedNotifier
    {
        private SocketAPI saleae;
        private string host = "127.0.0.1";
        private int port = 10429;

        private ConnectedDevices[] connDevs;
        private ConnectedDevices dev;

        public int ScanSeconds { get; set; }
        public int sampleMode { get; set; }
        public int sampleRate { get; set; }
        public int BurstSeconds { get; set; }
        public bool HasConnection { get { return (saleae != null);} }

        private LoggingThread logger;
        private EventHandler<LoggingEventArgs> CallerLoggingEventHandler;
        private List<SampleRate> smpRates = new List<SampleRate>();

        public MainWindowViewModel()
        {
            ScanSeconds = 1800;
            BurstSeconds = 5;
        }

        public List<string> SelectMode(int mode)
        {
            List<string> sampleRates = new List<string>();

            if(HasConnection)
            {
                if(mode > 0)
                {
                    saleae.SetActiveChannels(new int[] { 0, 1, 2, 3, 4, 5, 6, 7 }, null);
                }
                else
                {
                    saleae.SetActiveChannels(null, new int[] { 0, 1, 2, 3, 4, 5, 6, 7 });
                }

                smpRates = saleae.GetAvailableSampleRates();

                foreach(SampleRate sr in smpRates)
                {
                    if (mode > 0)
                    {
                        sampleRates.Add(sr.DigitalSampleRate.ToString());
                    }
                    else
                    {
                        sampleRates.Add(sr.AnalogSampleRate.ToString());
                    }
                }
            }

            return sampleRates;
        }

        public void SelectRate(int rate)
        {
            if (HasConnection)
            {
                saleae.SetSampleRate(smpRates[rate]);
                sampleRate = (smpRates[rate].AnalogSampleRate > 0) ? smpRates[rate].AnalogSampleRate : smpRates[rate].DigitalSampleRate;
            }
        }
        
        public void Connect(EventHandler<SaleaeStringEventArgs> saleaeApiMonitor = null,
            EventHandler<LoggingEventArgs> loggingEventHandler = null)
        {
            saleae = new SocketAPI(host, port);

            if (saleaeApiMonitor != null)
            {
                saleae.SaleaeStringEvent += saleaeApiMonitor;
            }
            CallerLoggingEventHandler = loggingEventHandler;

            connDevs = saleae.GetConnectedDevices();
            dev = (from d in connDevs where d.type == "LOGIC_PRO_16_DEVICE" select d).FirstOrDefault();
            if( dev.index == 0 )
            {   // Can't find logic pro 16.  Just pick the first one.
                var dLst = (from d in connDevs where d.index > 0 select d);
                var minIdx = (from d in dLst select d.index).Min();
                dev = (from d in dLst where d.index == minIdx select d ).FirstOrDefault();
            }

            if (dev.index != 0)
            {
                saleae.SelectActiveDevice(dev.index);
            }
            else
            {   // Can't find any device.  Not sure what is wrong.
                saleae = null;
            }

            OnPropertyChanged("HasConnection");
        }
        



        //=============================================================================

        internal void StartScan()
        {
            if( saleae == null )
            {   // Don't do anything if not yet connected!
                return;
            }

            StopScan();

            if (ScanSeconds < 1)
            {
                ScanSeconds = 1;
                OnPropertyChanged("ScanSec");
            }

            if(BurstSeconds < 5)
            {
                BurstSeconds = 5;
                OnPropertyChanged("BurstSeconds");
            }

            logger = new LoggingThread(saleae, ScanSeconds, BurstSeconds, CallerLoggingEventHandler);
            logger.StartScan(sampleRate);
        }


        internal void StopScan()
        {
            if (logger != null)
            {
                logger.StopScan();
                logger = null;
            }
        }

        //=============================================================================

    }
}
