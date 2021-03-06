﻿// Copyright © 2016 Daniel Porrey. All Rights Reserved.
//
// This file is part of the DHT Tiny project.
// 
// DHT Tiny is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// DHT Tiny is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with DHT Tiny. If not, 
// see http://www.gnu.org/licenses/.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DhtTinyMonitor.Common;
using DhtTinyMonitor.Views;
using Porrey.Tiny.Dht.Common;
using Porrey.Uwp.IoT.Sensors;
using Porrey.Uwp.IoT.Sensors.Tiny;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

namespace Porrey.Tiny.Dht.Views
{
    public sealed partial class MainPage : BindablePage
    {
        private DhtTiny _dhtTiny = null;
        private DispatcherTimer _timer = new DispatcherTimer();
        private ManualResetEvent _busy = new ManualResetEvent(false);

        public MainPage()
        {
            this.InitializeComponent();
            InitializeCommands();

            // ***
            // *** Setup the timer.
            // ***
            _timer.Interval = TimeSpan.FromMilliseconds(750);
            _timer.Tick += Timer_Tick;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                // ***
                // *** Find all devices on the bus.
                // ***
                IEnumerable<byte> address = await DhtTiny.FindAllDhtTinyAsync(this.FindAllDhtTinyCallback);

                if (address.Count() > 0)
                {
                    this.Status = string.Format("{0} device(s) found.", address.Count());

                    // ***
                    // *** Setup the DhtTiny instance.
                    // ***
                    _dhtTiny = new DhtTiny(address.First());
                    var result = await _dhtTiny.InitializeAsync();

                    if (result == InitializationResult.Successful)
                    {
						await UpdateStatusBarItems();

                        // ***
                        // *** Start the timer.
                        // ***
                        _timer.Start();

                        this.Status = "Connected/Monitoring.";
                    }
                    else
                    {
                        string message = string.Empty;

                        switch (result)
                        {
                            case InitializationResult.None:
                                message = "Initialization has not been performed.";
                                break;
                            case InitializationResult.NoI2cController:
                                message = "Initialization failed due to lack of an I2C controller.";
                                break;
                            case InitializationResult.DeviceInUse:
                                message = "Initialization failed due to device already in use.";
                                break;
                            case InitializationResult.DeviceNotFound:
                                message = "Initialization failed because a device was not found on the I2C bus.";
                                break;
                        }

                        this.Status = string.Format("Initialization Error: {0}", message);
                    }
                }
                else
                {
                    this.Status = "No DHT Tiny devices were found. Check connections and try again.";
                }
            }
            catch (Exception ex)
            {
                ExceptionEvent.Publish(ex);
            }

            base.OnNavigatedTo(e);
        }

        private async void Timer_Tick(object sender, object e)
        {
            try
            {
                _busy.Reset();

                this.Interval = await _dhtTiny.GetIntervalAsync();
                this.ReadingId = await _dhtTiny.GetReadingIdAsync();
                this.Temperature = string.Format("{0:0.0}°C", await _dhtTiny.GetTemperatureAsync());
                this.Humidity = string.Format("{0:0.0}%", await _dhtTiny.GetHumidityAsync());
                this.UpperThreshold = await _dhtTiny.GetUpperThresholdAsync();
                this.LowerThreshold = await _dhtTiny.GetLowerThresholdAsync();
                this.StartDelay = await _dhtTiny.GetStartDelayAsync();
                byte statusBits = await _dhtTiny.GetStatusAsync();
                byte configurationBits = await _dhtTiny.GetConfigurationAsync();

                this.StatusEnabled = _dhtTiny.GetStatusBit(statusBits, DhtTiny.StatusBit.IsEnabled) ? "1" : "0";
                this.StatusUpperThresholdExceeded = _dhtTiny.GetStatusBit(statusBits, DhtTiny.StatusBit.UpperThresholdExceeded) ? "1" : "0";
                this.StatusLowerThresholdExceeded = _dhtTiny.GetStatusBit(statusBits, DhtTiny.StatusBit.LowerThresholdExceeded) ? "1" : "0";
                this.StatusDhtReadError = _dhtTiny.GetStatusBit(statusBits, DhtTiny.StatusBit.DhtReadError) ? "1" : "0";
                this.StatusReserved2 = _dhtTiny.GetStatusBit(statusBits, DhtTiny.StatusBit.Reserved2) ? "1" : "0";
                this.StatusConfigurationSaved = _dhtTiny.GetStatusBit(statusBits, DhtTiny.StatusBit.ConfigSaved) ? "1" : "0";
                this.StatusReadError = _dhtTiny.GetStatusBit(statusBits, DhtTiny.StatusBit.ReadError) ? "1" : "0";
                this.StatusWriteError = _dhtTiny.GetStatusBit(statusBits, DhtTiny.StatusBit.WriteError) ? "1" : "0";

                this.configEnabled.IsChecked = _dhtTiny.GetConfigurationBit(configurationBits, DhtTiny.ConfigBit.SensorEnabled) ? true : false;
                this.configThresholdEnabled.IsChecked = _dhtTiny.GetConfigurationBit(configurationBits, DhtTiny.ConfigBit.ThresholdEnabled) ? true : false;
                this.configTriggerReading.IsChecked = _dhtTiny.GetConfigurationBit(configurationBits, DhtTiny.ConfigBit.TriggerReading) ? true : false;
                this.reserved1.IsChecked = _dhtTiny.GetConfigurationBit(configurationBits, DhtTiny.ConfigBit.Reserved1) ? true : false;
                this.reserved2.IsChecked = _dhtTiny.GetConfigurationBit(configurationBits, DhtTiny.ConfigBit.Reserved2) ? true : false;
                this.reserved3.IsChecked = _dhtTiny.GetConfigurationBit(configurationBits, DhtTiny.ConfigBit.Reserved3) ? true : false;
                this.writeConfiguration.IsChecked = _dhtTiny.GetConfigurationBit(configurationBits, DhtTiny.ConfigBit.WriteConfig) ? true : false;
                this.resetConfiguration.IsChecked = _dhtTiny.GetConfigurationBit(configurationBits, DhtTiny.ConfigBit.ResetConfig) ? true : false;

                this.EnabledCommand.RaiseCanExecuteChanged();
                this.EnableThresholdsCommand.RaiseCanExecuteChanged();
                this.TriggerReadingCommand.RaiseCanExecuteChanged();
                this.Reserved1Command.RaiseCanExecuteChanged();
                this.Reserved2Command.RaiseCanExecuteChanged();
                this.Reserved3Command.RaiseCanExecuteChanged();
                this.WriteConfigurationCommand.RaiseCanExecuteChanged();
                this.ResetConfigurationCommand.RaiseCanExecuteChanged();
                this.SettingsCommand.RaiseCanExecuteChanged();
            }
            catch(Exception ex)
            {
                ExceptionEvent.Publish(ex);
            }
            finally
            {
                this.EnabledCommand.RaiseCanExecuteChanged();
                this.EnableThresholdsCommand.RaiseCanExecuteChanged();
                this.TriggerReadingCommand.RaiseCanExecuteChanged();
                this.Reserved1Command.RaiseCanExecuteChanged();
                this.Reserved2Command.RaiseCanExecuteChanged();
                this.Reserved3Command.RaiseCanExecuteChanged();
                this.WriteConfigurationCommand.RaiseCanExecuteChanged();
                this.ResetConfigurationCommand.RaiseCanExecuteChanged();
                this.SettingsCommand.RaiseCanExecuteChanged();

                _busy.Set();
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            try
            {
                if (_dhtTiny != null)
                {
                    _dhtTiny.Dispose();
                    _dhtTiny = null;
                }

                _timer.Stop();
            }
            catch (Exception ex)
            {
                ExceptionEvent.Publish(ex);
            }

            base.OnNavigatingFrom(e);
        }

        private async Task FlipConfigurationBit(DhtTiny.ConfigBit bit)
        {
            try
            {
                // ***
                // *** Stop the timer.
                // ***
                _timer.Stop();

                // ***
                // *** Wait for the timer event to finish.
                // ***
                _busy.WaitOne();

                // ***
                // *** Get the current configuration bits.
                // ***
                byte configurationBits = await _dhtTiny.GetConfigurationAsync();

                // ***
                // *** Determine the value of the bit.
                // ***
                bool isEnabled = _dhtTiny.GetConfigurationBit(configurationBits, bit);

                // ***
                // *** Set the new configuration bit.
                // ***
                await _dhtTiny.SetConfigurationAsync((byte)bit, !isEnabled);
            }
            catch (Exception ex)
            {
                ExceptionEvent.Publish(ex);
            }
            finally
            {
                // ***
                // *** Start the timer.
                // ***
                _timer.Start();
            }
        }

        private void FindAllDhtTinyCallback(I2cScanEventArgs e)
        {
            int percentComplete = (int)((double)e.CurrentIndex / (double)e.Total * 100.0d);
            this.Status = string.Format("Locating devices [0x{0:X2}] [{1}%] [Found = {2:##0}]...", e.CurrentAddress, percentComplete, e.Items.Count());
        }

		private async Task UpdateStatusBarItems()
		{
			this.Version = string.Format("Version: {0}", await _dhtTiny.GetFirmwareVersionAsync());
			this.Id = string.Format("ID: 0x{0:X2}", await _dhtTiny.GetDeviceIdAsync());
			this.Address = string.Format("Address: 0x{0:X2}", await _dhtTiny.GetDeviceAddressAsync());
			this.Model = string.Format("DHT{0}", await _dhtTiny.GetDeviceModelAsync());
		}

        #region Values
        private string _status = "Ready.";
        public string Status
        {
            get
            {
                return _status;
            }
            set
            {
                this.SetProperty(ref _status, value);
            }
        }

        private string _version = string.Empty;
        public string Version
        {
            get
            {
                return _version;
            }
            set
            {
                this.SetProperty(ref _version, value);
            }
        }

        private string _address = string.Empty;
        public string Address
        {
            get
            {
                return _address;
            }
            set
            {
                this.SetProperty(ref _address, value);
            }
        }

        private string _id = string.Empty;
        public string Id
        {
            get
            {
                return _id;
            }
            set
            {
                this.SetProperty(ref _id, value);
            }
        }

        private string _model = string.Empty;
        public string Model
        {
            get
            {
                return _model;
            }
            set
            {
                this.SetProperty(ref _model, value);
            }
        }

        private uint _readingId = 0;
        public uint ReadingId
        {
            get
            {
                return _readingId;
            }
            set
            {
                this.SetProperty(ref _readingId, value);
            }
        }

        private double _interval = 0;
        public double Interval
        {
            get
            {
                return _interval;
            }
            set
            {
                this.SetProperty(ref _interval, value);
            }
        }

        private string _temperature = "0.0°C";
        public string Temperature
        {
            get
            {
                return _temperature;
            }
            set
            {
                this.SetProperty(ref _temperature, value);
            }
        }

        private string _humidity = "0.0%";
        public string Humidity
        {
            get
            {
                return _humidity;
            }
            set
            {
                this.SetProperty(ref _humidity, value);
            }
        }

        private double _upperThreshold = 0;
        public double UpperThreshold
        {
            get
            {
                return _upperThreshold;
            }
            set
            {
                this.SetProperty(ref _upperThreshold, value);
            }
        }

        private double _lowerThreshold = 0;
        public double LowerThreshold
        {
            get
            {
                return _lowerThreshold;
            }
            set
            {
                this.SetProperty(ref _lowerThreshold, value);
            }
        }

        private double _startDelay = 0;
        public double StartDelay
        {
            get
            {
                return _startDelay;
            }
            set
            {
                this.SetProperty(ref _startDelay, value);
            }
        }

        private string _statusEnabled = "0";
        public string StatusEnabled
        {
            get
            {
                return _statusEnabled;
            }
            set
            {
                this.SetProperty(ref _statusEnabled, value);
            }
        }

        private string _statusUpperThresholdExceeded = "0";
        public string StatusUpperThresholdExceeded
        {
            get
            {
                return _statusUpperThresholdExceeded;
            }
            set
            {
                this.SetProperty(ref _statusUpperThresholdExceeded, value);
            }
        }

        private string _statusLowerThresholdExceeded = "0";
        public string StatusLowerThresholdExceeded
        {
            get
            {
                return _statusLowerThresholdExceeded;
            }
            set
            {
                this.SetProperty(ref _statusLowerThresholdExceeded, value);
            }
        }

        private string _statusDhtReadError = "0";
        public string StatusDhtReadError
        {
            get
            {
                return _statusDhtReadError;
            }
            set
            {
                this.SetProperty(ref _statusDhtReadError, value);
            }
        }

        private string _statusReserved2 = "0";
        public string StatusReserved2
        {
            get
            {
                return _statusReserved2;
            }
            set
            {
                this.SetProperty(ref _statusReserved2, value);
            }
        }

        private string _statusConfigurationSaved = "0";
        public string StatusConfigurationSaved
        {
            get
            {
                return _statusConfigurationSaved;
            }
            set
            {
                this.SetProperty(ref _statusConfigurationSaved, value);
            }
        }

        private string _statusReadError = "0";
        public string StatusReadError
        {
            get
            {
                return _statusReadError;
            }
            set
            {
                this.SetProperty(ref _statusReadError, value);
            }
        }

        private string _statusWriteError = "0";
        public string StatusWriteError
        {
            get
            {
                return _statusWriteError;
            }
            set
            {
                this.SetProperty(ref _statusWriteError, value);
            }
        }
        #endregion

        #region Commands
        public RelayCommand EnabledCommand { get; set; }
        public RelayCommand EnableThresholdsCommand { get; set; }
        public RelayCommand TriggerReadingCommand { get; set; }
        public RelayCommand Reserved1Command { get; set; }
        public RelayCommand Reserved2Command { get; set; }
        public RelayCommand Reserved3Command { get; set; }
        public RelayCommand WriteConfigurationCommand { get; set; }
        public RelayCommand ResetConfigurationCommand { get; set; }
        public RelayCommand SettingsCommand { get; set; }

        private void InitializeCommands()
        {
            try
            {
                this.EnabledCommand = new RelayCommand(OnEnabledCommand, OnCanEnabledCommand);
                this.EnableThresholdsCommand = new RelayCommand(OnEnableThresholdsCommand, OnCanEnableThresholdsCommand);
                this.TriggerReadingCommand = new RelayCommand(OnTriggerReadingCommand, OnCanTriggerReadingCommand);
                this.Reserved1Command = new RelayCommand(OnReserved1Command, OnCanReserved1Command);
                this.Reserved2Command = new RelayCommand(OnReserved2Command, OnCanReserved2Command);
                this.Reserved3Command = new RelayCommand(OnReserved3Command, OnCanReserved3Command);
                this.WriteConfigurationCommand = new RelayCommand(OnWriteConfigurationCommand, OnCanWriteConfigurationCommand);
                this.ResetConfigurationCommand = new RelayCommand(OnResetConfigurationCommand, OnCanResetConfigurationCommand);
                this.SettingsCommand = new RelayCommand(OnSettingsCommand, OnCanSettingsCommand);
            }
            catch (Exception ex)
            {
                ExceptionEvent.Publish(ex);
            }
        }

        private bool OnCanSettingsCommand()
        {
            return _dhtTiny != null && _dhtTiny.IsInitialized;
        }

        private async void OnSettingsCommand()
        {
            try
            {
                // ***
                // *** Stop the timer.
                // ***
                _timer.Stop();

                // ***
                // *** Wait for the timer event to finish.
                // ***
                _busy.WaitOne();

                // ***
                // *** Create and open the settings dialog.
                // ***
                Settings settings = new Settings(_dhtTiny);
                await settings.ShowAsync();
            }
            catch (Exception ex)
            {
                ExceptionEvent.Publish(ex);
            }
            finally
            {
				// ***
				// *** Update the status bar items.
				// ***
				await UpdateStatusBarItems();

				// ***
				// *** Start the timer.
				// ***
				_timer.Start();
            }
        }

        private bool OnCanEnabledCommand()
        {
            return _dhtTiny != null && _dhtTiny.IsInitialized;
        }

        private async void OnEnabledCommand()
        {
            await FlipConfigurationBit(DhtTiny.ConfigBit.SensorEnabled);
        }

        private bool OnCanEnableThresholdsCommand()
        {
            return _dhtTiny != null && _dhtTiny.IsInitialized;
        }

        private async void OnEnableThresholdsCommand()
        {
            await FlipConfigurationBit(DhtTiny.ConfigBit.ThresholdEnabled);
        }

        private bool OnCanTriggerReadingCommand()
        {
            return _dhtTiny != null && _dhtTiny.IsInitialized;
        }

        private async void OnTriggerReadingCommand()
        {
            await FlipConfigurationBit(DhtTiny.ConfigBit.TriggerReading);
        }

        private bool OnCanReserved1Command()
        {
            return _dhtTiny != null && _dhtTiny.IsInitialized;
        }

        private async void OnReserved1Command()
        {
            await FlipConfigurationBit(DhtTiny.ConfigBit.Reserved1);
        }

        private bool OnCanReserved2Command()
        {
            return _dhtTiny != null && _dhtTiny.IsInitialized;
        }

        private async void OnReserved2Command()
        {
            await FlipConfigurationBit(DhtTiny.ConfigBit.Reserved2);
        }

        private bool OnCanReserved3Command()
        {
            return _dhtTiny != null && _dhtTiny.IsInitialized;
        }

        private async void OnReserved3Command()
        {
            await FlipConfigurationBit(DhtTiny.ConfigBit.Reserved3);
        }

        private bool OnCanWriteConfigurationCommand()
        {
            return _dhtTiny != null && _dhtTiny.IsInitialized;
        }

        private async void OnWriteConfigurationCommand()
        {
            await FlipConfigurationBit(DhtTiny.ConfigBit.WriteConfig);
        }

        private bool OnCanResetConfigurationCommand()
        {
            return _dhtTiny != null && _dhtTiny.IsInitialized;
        }

        private async void OnResetConfigurationCommand()
        {
            await FlipConfigurationBit(DhtTiny.ConfigBit.ResetConfig);
        }
        #endregion
    }
}
