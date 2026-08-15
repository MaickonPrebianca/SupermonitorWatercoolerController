using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Security.Principal;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace AIOController
{
    public class AppSettings
    {
        public string SelectedComPort { get; set; } = string.Empty;
        public string CpuTempSensor { get; set; } = string.Empty;
        public string CpuLoadSensor { get; set; } = string.Empty;
        public string PumpSpeedSensor { get; set; } = string.Empty;
        public string FanSpeedSensor { get; set; } = string.Empty;
        public string GpuTempSensor { get; set; } = string.Empty;
        public string GpuLoadSensor { get; set; } = string.Empty;
        public string GpuFanSpeedSensor { get; set; } = string.Empty;
        public bool StartMinimized { get; set; } = false;
        public bool CalculatePumpSpeed { get; set; } = false;
    }

    public partial class MainWindow : Window
    {
        private const string TASK_NAME = "SupermonitorWatercoolerController_AutoStart";
        private readonly Computer _computer;
        private SerialPort? _serialPort;
        private readonly DispatcherTimer _timer;
        private readonly NotifyIcon _notifyIcon;
        private readonly List<ISensor> _availableSensors = new List<ISensor>();
        private readonly string _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        private bool _isInitializing = true;

        public MainWindow()
        {
            InitializeComponent();

            // Garante a autorização permanente via Agendador de Tarefas do Windows
            EnsureTaskSchedulerAutoStart();

            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true
            };

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
            _notifyIcon = new NotifyIcon();

            InitSystemTray();
            InitHardwareMonitor();
            PopulateComPorts();
            PopulateSensorDropdowns();

            bool hasSavedSettings = LoadSettings();
            _isInitializing = false;

            if (hasSavedSettings && ComboComPorts.SelectedItem != null && !string.IsNullOrEmpty(ComboComPorts.SelectedItem.ToString()))
            {
                ConnectSerialPort();
            }

            StartMonitoringLoop();

            if (ChkStartMinimized.IsChecked == true)
            {
                HideWindowToTray();
            }
        }

        private void EnsureTaskSchedulerAutoStart()
        {
            try
            {
                if (!IsUserAdministrator()) return;

                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrEmpty(exePath)) return;

                string arguments = $"/create /tn \"{TASK_NAME}\" /tr \"\\\"{exePath}\\\"\" /sc ONLOGON /rl HIGHEST /f";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (Process? process = Process.Start(psi))
                {
                    process?.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao criar tarefa no Agendador: {ex.Message}");
            }
        }

        private bool IsUserAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void InitSystemTray()
        {
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Text = "Supermonitor Watercooler Controller";
            _notifyIcon.Visible = true;

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Restaurar / Abrir", null, (s, e) => RestoreFromTray());
            contextMenu.Items.Add("Sair", null, (s, e) => ExitApplication());

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        private void InitHardwareMonitor()
        {
            try
            {
                _computer.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erro ao inicializar o monitor de hardware: {ex.Message}\n\n" +
                    "Certifique-se de que o aplicativo foi executado como Administrador para obter acesso aos sensores.",
                    "Erro de Permissão",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        private void PopulateComPorts()
        {
            ComboComPorts.ItemsSource = SerialPort.GetPortNames();
            if (ComboComPorts.Items.Count > 0)
                ComboComPorts.SelectedIndex = 0;
        }

        private void PopulateSensorDropdowns()
        {
            _availableSensors.Clear();

            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                foreach (var subHardware in hardware.SubHardware) subHardware.Update();

                CollectSensors(hardware);
            }

            var sensorNames = _availableSensors.Select(s => $"{s.Hardware.Name} - {s.Name} ({s.SensorType})").ToList();
            sensorNames.Insert(0, "-- Selecionar Sensor --");

            ComboCpuTemp.ItemsSource = sensorNames;
            ComboCpuLoad.ItemsSource = sensorNames;
            ComboPumpSpeed.ItemsSource = sensorNames;
            ComboFanSpeed.ItemsSource = sensorNames;
            ComboGpuTemp.ItemsSource = sensorNames;
            ComboGpuLoad.ItemsSource = sensorNames;
            ComboGpuFanSpeed.ItemsSource = sensorNames;

            AutoSelectSensor(ComboCpuTemp, SensorType.Temperature, "Core");
            AutoSelectSensor(ComboCpuLoad, SensorType.Load, "Total");
            AutoSelectSensor(ComboPumpSpeed, SensorType.Fan, "Pump");
            AutoSelectSensor(ComboFanSpeed, SensorType.Fan, "Fan #1");
            AutoSelectSensor(ComboGpuTemp, SensorType.Temperature, "GPU");
            AutoSelectSensor(ComboGpuLoad, SensorType.Load, "GPU Core");
            AutoSelectSensor(ComboGpuFanSpeed, SensorType.Fan, "GPU Fan");
        }

        private void CollectSensors(IHardware hardware)
        {
            foreach (var sensor in hardware.Sensors)
            {
                _availableSensors.Add(sensor);
            }
            foreach (var subHardware in hardware.SubHardware)
            {
                CollectSensors(subHardware);
            }
        }

        private void AutoSelectSensor(System.Windows.Controls.ComboBox comboBox, SensorType type, string nameKeywords)
        {
            var match = _availableSensors.FirstOrDefault(s => s.SensorType == type && s.Name.Contains(nameKeywords));
            if (match != null)
            {
                comboBox.SelectedIndex = _availableSensors.IndexOf(match) + 1;
            }
        }

        private void StartMonitoringLoop()
        {
            _timer.Tick += UpdateHardwareData;
            _timer.Start();
        }

        private void UpdateHardwareData(object? sender, EventArgs? e)
        {
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                foreach (var subHardware in hardware.SubHardware) subHardware.Update();
            }

            float cpuTemp = GetSelectedSensorValue(ComboCpuTemp);
            float cpuLoad = GetSelectedSensorValue(ComboCpuLoad);
            float fanSpeed = GetSelectedSensorValue(ComboFanSpeed);
            
            float pumpSpeed;
            if (ChkCalculatePumpSpeed.IsChecked == true)
            {
                pumpSpeed = fanSpeed * 1.6f;
            }
            else
            {
                pumpSpeed = GetSelectedSensorValue(ComboPumpSpeed);
            }

            float gpuTemp = GetSelectedSensorValue(ComboGpuTemp);
            float gpuLoad = GetSelectedSensorValue(ComboGpuLoad);
            float gpuFanSpeed = GetSelectedSensorValue(ComboGpuFanSpeed);

            TxtCpuTemp.Text = cpuTemp.ToString("F0");
            TxtCpuLoad.Text = $"{cpuLoad:F0} %";
            ProgressCpuLoad.Value = cpuLoad;

            TxtPumpSpeed.Text = $"{pumpSpeed:F0} RPM";
            ProgressPumpSpeed.Value = Math.Min(pumpSpeed, ProgressPumpSpeed.Maximum);

            TxtFanSpeed.Text = $"{fanSpeed:F0} RPM";
            ProgressFanSpeed.Value = Math.Min(fanSpeed, ProgressFanSpeed.Maximum);

            TxtGpuTemp.Text = gpuTemp.ToString("F0");
            TxtGpuLoad.Text = $"{gpuLoad:F0} %";
            ProgressGpuLoad.Value = gpuLoad;

            TxtGpuFanSpeed.Text = $"{gpuFanSpeed:F0} RPM";
            ProgressGpuFanSpeed.Value = Math.Min(gpuFanSpeed, ProgressGpuFanSpeed.Maximum);

            if (_serialPort != null && _serialPort.IsOpen)
            {
                SendStatusToWatercooler(cpuTemp, (ushort)pumpSpeed, (ushort)fanSpeed);
            }
        }

        private float GetSelectedSensorValue(System.Windows.Controls.ComboBox comboBox)
        {
            if (comboBox != null && comboBox.SelectedIndex > 0 && (comboBox.SelectedIndex - 1) < _availableSensors.Count)
            {
                return _availableSensors[comboBox.SelectedIndex - 1].Value ?? 0.0f;
            }
            return 0.0f;
        }

        private void SendStatusToWatercooler(float cpuTemp, ushort pumpRpm, ushort fanRpm)
        {
            try
            {
                ushort rawTemp = (ushort)(cpuTemp * 10.0f);
                byte[] packet = new byte[]
                {
                    0x01, 0x01,
                    (byte)(rawTemp >> 8), (byte)(rawTemp & 0xFF),
                    (byte)(pumpRpm >> 8), (byte)(pumpRpm & 0xFF),
                    (byte)(fanRpm >> 8),  (byte)(fanRpm & 0xFF)
                };

                _serialPort?.Write(packet, 0, packet.Length);
                StatusText.Content = $"Dados enviados -> Temp: {cpuTemp:F1}°C | Pump: {pumpRpm} RPM | Fan: {fanRpm} RPM";
            }
            catch (Exception ex)
            {
                StatusText.Content = $"Erro de comunicação Serial: {ex.Message}";
            }
        }

        private void ConnectSerialPort()
        {
            if (ComboComPorts.SelectedItem == null) return;
            try
            {
                _serialPort = new SerialPort(ComboComPorts.SelectedItem.ToString()!, 115200, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 200,
                    WriteTimeout = 200
                };
                _serialPort.Open();
                BtnToggleConnect.Content = "Desconectar";
                BtnToggleConnect.Foreground = System.Windows.Media.Brushes.Red;
                StatusText.Content = $"Conectado à porta {ComboComPorts.SelectedItem}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha ao abrir porta serial: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnToggleConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                BtnToggleConnect.Content = "Conectar";
                BtnToggleConnect.Foreground = System.Windows.Media.Brushes.Green;
                StatusText.Content = "Desconectado.";
            }
            else
            {
                ConnectSerialPort();
            }
        }

        private void OnSensorSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing)
            {
                SaveSettings();
                UpdateHardwareData(null, null);
            }
        }

        private void ChkCalculatePumpSpeed_CheckedChanged(object sender, RoutedEventArgs e)
        {
            ComboPumpSpeed.IsEnabled = ChkCalculatePumpSpeed.IsChecked != true;
            OnSettingChanged(sender, e);
        }

        private void OnSettingChanged(object sender, RoutedEventArgs e)
        {
            if (!_isInitializing)
            {
                SaveSettings();
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                HideWindowToTray();
            }
        }

        private void HideWindowToTray()
        {
            Hide();
            _notifyIcon.BalloonTipTitle = "Supermonitor Watercooler Controller";
            _notifyIcon.BalloonTipText = "O aplicativo continua rodando em segundo plano.";
            _notifyIcon.ShowBalloonTip(1000);
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            _notifyIcon.Dispose();
            Application.Current.Shutdown();
        }

        private bool LoadSettings()
        {
            if (!File.Exists(_settingsFilePath)) return false;

            try
            {
                string json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                if (settings == null) return false;

                if (!string.IsNullOrEmpty(settings.SelectedComPort) && ComboComPorts.Items.Contains(settings.SelectedComPort))
                {
                    ComboComPorts.SelectedItem = settings.SelectedComPort;
                }

                SelectComboBoxByText(ComboCpuTemp, settings.CpuTempSensor);
                SelectComboBoxByText(ComboCpuLoad, settings.CpuLoadSensor);
                SelectComboBoxByText(ComboPumpSpeed, settings.PumpSpeedSensor);
                SelectComboBoxByText(ComboFanSpeed, settings.FanSpeedSensor);
                SelectComboBoxByText(ComboGpuTemp, settings.GpuTempSensor);
                SelectComboBoxByText(ComboGpuLoad, settings.GpuLoadSensor);
                SelectComboBoxByText(ComboGpuFanSpeed, settings.GpuFanSpeedSensor);

                ChkStartMinimized.IsChecked = settings.StartMinimized;
                ChkCalculatePumpSpeed.IsChecked = settings.CalculatePumpSpeed;
                ComboPumpSpeed.IsEnabled = !settings.CalculatePumpSpeed;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new AppSettings
                {
                    SelectedComPort = ComboComPorts.SelectedItem?.ToString() ?? string.Empty,
                    CpuTempSensor = ComboCpuTemp.SelectedItem?.ToString() ?? string.Empty,
                    CpuLoadSensor = ComboCpuLoad.SelectedItem?.ToString() ?? string.Empty,
                    PumpSpeedSensor = ComboPumpSpeed.SelectedItem?.ToString() ?? string.Empty,
                    FanSpeedSensor = ComboFanSpeed.SelectedItem?.ToString() ?? string.Empty,
                    GpuTempSensor = ComboGpuTemp.SelectedItem?.ToString() ?? string.Empty,
                    GpuLoadSensor = ComboGpuLoad.SelectedItem?.ToString() ?? string.Empty,
                    GpuFanSpeedSensor = ComboGpuFanSpeed.SelectedItem?.ToString() ?? string.Empty,
                    StartMinimized = ChkStartMinimized.IsChecked ?? false,
                    CalculatePumpSpeed = ChkCalculatePumpSpeed.IsChecked ?? false
                };

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
                // Ignora erros pontuais de I/O na escrita de configurações
            }
        }

        private void SelectComboBoxByText(System.Windows.Controls.ComboBox comboBox, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var item in comboBox.Items)
            {
                if (item.ToString() == text)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            _serialPort?.Close();
            _computer.Close();
            _notifyIcon.Dispose();
            base.OnClosed(e);
        }
    }
}