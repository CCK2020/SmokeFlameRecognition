using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using Yolov8Net;
using static System.Net.Mime.MediaTypeNames;
using System.Net.Sockets;
using System.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Diagnostics;
using System.Reflection;
using System;
using System.Threading;

namespace SmokeAndFlameRecognition
{
    internal class Program
    {
        //private static Socket sSocket;
        //private static Socket _serverSocket;
        private static TcpListener tcpListener;
        private static readonly byte[] msgBuffer = new byte[2048];

        private static IPredictor? yolo;
        private static ProgramConfig programConfig;

        static void Main(string[] args)
        {
            //while (true)
            //{
            string Path = $"{Directory.GetCurrentDirectory()}\\Config\\ProgramConfig.json";
            programConfig = JsonSerializer.Deserialize<ProgramConfig>(File.ReadAllText(Path));
            yolo = YoloV8Predictor.Create($"{AppDomain.CurrentDomain.BaseDirectory}onnx\\{programConfig.OnnxName}.onnx", programConfig.DetectionType.Split(','), false);
            //Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>($"C:\\Users\\KK\\Desktop\\AI测试\\p1.jpg");
            //var predictions = yolo.Predict(image);
            //    Image<Rgba32> image2 = SixLabors.ImageSharp.Image.Load<Rgba32>($"{AppDomain.CurrentDomain.BaseDirectory}\\2.jpg");
            //    var predictions2 = yolo.Predict(image2);
            //    Image<Rgba32> image3 = SixLabors.ImageSharp.Image.Load<Rgba32>($"{AppDomain.CurrentDomain.BaseDirectory}\\3.jpg");
            //    var predictions3 = yolo.Predict(image3);
            //    Thread.Sleep(3000);
            //}

            string configPath = $"{Directory.GetCurrentDirectory()}\\Config\\ProgramConfig.json";
            if (!File.Exists(configPath))
            {
                CLog.Instance.WriteLog("找不到配置文件!", CLog.LogType.Failure);
                return;
            }
            else
            {
                programConfig = JsonSerializer.Deserialize<ProgramConfig>(File.ReadAllText(configPath));
                yolo = YoloV8Predictor.Create($"{Directory.GetCurrentDirectory()}\\onnx\\{programConfig.OnnxName}.onnx", programConfig.DetectionType.Split(','), false);
            }
            try
            {
                // IPEndPoint ipe = new IPEndPoint(IPAddress.Parse(programConfig.IPAddress), int.Parse(programConfig.Port));
                //sSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //sSocket.Bind(ipe);
                //ssock
                // tcpListener = new TcpListener(IPAddress.Parse(programConfig.IPAddress), 6423);
                //获取本地主机名
                string hostName = Dns.GetHostName();
                // 根据主机名获取IP地址列表
                IPAddress[] addresses = Dns.GetHostAddresses(hostName);
                IPAddress ipAddres = null;
                foreach (IPAddress address in addresses)
                {
                    string str = address.ToString();
                    if (IPAddress.TryParse(str, out IPAddress iPAddress) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        CLog.Instance.WriteLog("IP地址:", CLog.LogType.Failure);
                        ipAddres = iPAddress;
                        break;
                    }
                }
                tcpListener = new TcpListener(ipAddres, 6423);

                CLog.Instance.WriteLog("Connt IP:" + ipAddres);
                Console.WriteLine("Connt IP:" + ipAddres);
                // 开始监听连接请求
                tcpListener.Start();
                CLog.Instance.WriteLog("程序启动");
                while (true)
                {
                    // 接受客户端的连接
                    TcpClient client = tcpListener.AcceptTcpClient();
                    Console.WriteLine($"已连接客户端: {(client.Client.RemoteEndPoint as IPEndPoint).Address}");

                    // 处理连接的客户端（在新线程中处理，以允许同时处理多个客户端）
                    Task.Run(() =>
                    {
                        while (true)
                        {
                            HandleClient(client);
                            Thread.Sleep(1000);
                        }
                    });
                    //  AsynServer();
                }
            }
            catch (Exception ex)
            {
                CLog.Instance.WriteException(ex, "程序执行异常，已退出");
                Console.WriteLine("Exction :"+ex.ToString());
                return;
            }
            //Console.ReadKey();
            //Console.ReadLine(); // 等待用户输入
        }

        static void HandleClient(object obj)
        {
            TcpClient tcpClient = (TcpClient)obj;
            if (tcpClient.Available > 0)
            {
                try
                {
                    // 获取网络流
                    NetworkStream stream = tcpClient.GetStream();

                    // 接收客户端发送的数据
                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"收到来自客户端的消息: {message}");
                    string resultStr = AsynServer(message);
                    // 发送响应给客户端
                    byte[] data = Encoding.UTF8.GetBytes(resultStr);
                    stream.Write(data, 0, data.Length);
                    Console.WriteLine($"已发送响应: {data}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"处理客户端时发生错误: {ex.Message}");
                }
                finally
                {
                    // 关闭连接
                    tcpClient.Close();
                }
            }
        }
        /// <summary>
        /// 异步Socket服务
        /// </summary>
        public static string AsynServer(string receivedData)
        {
            try
            {
                IdentifyingEvents identifying = JsonSerializer.Deserialize<IdentifyingEvents>(receivedData);
                List<IdentifyingResult> identifyingResults = new List<IdentifyingResult>();
                foreach (var item in identifying.ImagesPath)
                {
                    IdentifyingResult identifyingResult = new IdentifyingResult();
                    identifyingResult.TriggerTime = identifying.RecognitionTime;
                    if (!File.Exists(item))
                    {
                        identifyingResult.ErrorID = -1;
                        CLog.Instance.WriteLog("Unable to find detection image!---Path:" + item, CLog.LogType.Failure);
                        Console.WriteLine("文件不存在！");    
                    }
                    else
                    {
                        Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(item);
                        var predictions = yolo.Predict(image);
                        foreach (var pred in predictions)
                        {
                            if (pred.Score > 0.5f && pred.Label.Name == "fire" && pred.Score > identifyingResult.FireConfidenceLevel)
                            {
                                identifyingResult.HasFire = true;
                                identifyingResult.FireConfidenceLevel = (float)Math.Round(pred.Score, 2);
                            }
                            if (pred.Score > 0.5f && pred.Label.Name == "smoke" && pred.Score > identifyingResult.SmokeConfidenceLevel)
                            {
                                identifyingResult.HasSmoke = true;
                                identifyingResult.SmokeConfidenceLevel = (float)Math.Round(pred.Score, 2);
                            }
                        }
                    }
                    identifyingResults.Add(identifyingResult);
                }
                return JsonSerializer.Serialize(identifyingResults);
                //_serverSocket.BeginReceive(msgBuffer, 0, msgBuffer.Length, 0, new AsyncCallback(ReceiveCallback), null);
            }
            catch (Exception ex)
            {
                CLog.Instance.WriteException(ex, "Socket执行异常");
                // _serverSocket = null;
                Console.WriteLine("转换出错： Info \r\n"+ex.ToString());
            }
            return null;
        }
        /// <summary>
        /// 异步接收客户端信息
        /// </summary>
        /// <param name="ar"></param>
        private static void ReceiveCallback(IAsyncResult asyncResult)
        {
            try
            {
                Console.WriteLine("信息输入");
                int rEnd = 0;//_serverSocket.EndReceive(asyncResult);
                if (rEnd > 0)
                {
                    try
                    {
                        string resultStr = Encoding.UTF8.GetString(msgBuffer, 0, rEnd);
                        IdentifyingEvents identifying = JsonSerializer.Deserialize<IdentifyingEvents>(resultStr);
                        List<IdentifyingResult> identifyingResults = new List<IdentifyingResult>();
                        foreach (var item in identifying.ImagesPath)
                        {
                            IdentifyingResult identifyingResult = new IdentifyingResult();
                            identifyingResult.TriggerTime = identifying.RecognitionTime;
                            if (!File.Exists(item))
                            {
                                identifyingResult.ErrorID = -1;
                                CLog.Instance.WriteLog("Unable to find detection image!---Path:" + item, CLog.LogType.Failure);
                                Console.WriteLine("文件不存在！");
                            }
                            else
                            {
                                Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(item);
                                var predictions = yolo.Predict(image);
                                foreach (var pred in predictions)
                                {
                                    if (pred.Score > 0.5f && pred.Label.Name == "fire" && pred.Score > identifyingResult.FireConfidenceLevel)
                                    {
                                        identifyingResult.HasFire = true;
                                        identifyingResult.FireConfidenceLevel = (float)Math.Round(pred.Score, 2);
                                    }
                                    if (pred.Score > 0.5f && pred.Label.Name == "smoke" && pred.Score > identifyingResult.SmokeConfidenceLevel)
                                    {
                                        identifyingResult.HasSmoke = true;
                                        identifyingResult.SmokeConfidenceLevel = (float)Math.Round(pred.Score, 2);
                                    }
                                }
                            }
                            identifyingResults.Add(identifyingResult);
                        }
                        try
                        {
                           // if (_serverSocket != null && _serverSocket.Connected)
                            {
                                string str = JsonSerializer.Serialize(identifyingResults);
                                Console.WriteLine("内容： \r\n" + str);
                                byte[] sendData = Encoding.UTF8.GetBytes(str);
                               // _serverSocket.Send(sendData);
                            }
                        }
                        catch (Exception ex)
                        {
                            CLog.Instance.WriteException(ex, "Socket接收后发送异常");
                        //    _serverSocket = null;
                        //    AsynServer();
                        }
                    }
                    catch (Exception ex)
                    {
                        CLog.Instance.WriteException(ex, "解析异常");
                    }
                 //   _serverSocket.BeginReceive(msgBuffer, 0, msgBuffer.Length, 0, new AsyncCallback(ReceiveCallback), null);
                }
            }
            catch (Exception ex)
            {
              //  _serverSocket = null;
            //    AsynServer();
                CLog.Instance.WriteException(ex, "Socket接收异常");
            }
        }
    }
    public class CLog
    {

        public enum LogType
        {
            All,
            Information,
            Debug,
            Success,
            Failure,
            Warning,
            Error
        }
        public static Logger Instance
        {
            get
            {
                return Logger.Instance;
            }
        }

        public class Logger
        {
            #region Instance
            private static object logLock;

            private static Logger _instance;

            private static string logFileName;
            private Logger() { }
            private static string ShortGUID()
            {
                byte[] buffer = Guid.NewGuid().ToByteArray();
                return BitConverter.ToInt64(buffer, 0).ToString();
            }
            /// <summary>
            /// Logger instance
            /// </summary>
            public static Logger Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        _instance = new Logger();
                        logLock = new object();
                        logFileName = DateTime.Now.ToString("yyyy-MMdd-HHmmss") + "-" + ShortGUID() + ".log";
                    }
                    return _instance;
                }
            }
            #endregion

            /// <summary>
            /// Write log to log file
            /// </summary>
            /// <param name="logContent">log content</param>
            /// <param name="logType">log type</param>
            public void WriteLog(string logContent, LogType logType = LogType.Information, string fileName = null)
            {
                try
                {
                    //Console.WriteLine(logContent);
                    Debug.WriteLine(logContent);
                    string basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    //basePath = @"C:\APILogs";
                    string logPath = Path.Combine(basePath, "log");
                    if (!Directory.Exists(logPath))
                    {
                        Directory.CreateDirectory(logPath);
                    }

                    string dataStringLogPath = Path.Combine(logPath, DateTime.Now.ToString("yyyy-MM-dd"));

                    if (!Directory.Exists(dataStringLogPath))
                    {
                        Directory.CreateDirectory(dataStringLogPath);
                    }

                    string[] logText = new string[] { DateTime.Now.ToString("HH:mm:ss") + ": " + logType.ToString() + ": " + logContent };
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        fileName = fileName + "_" + logFileName;
                    }
                    else
                    {
                        fileName = logFileName;
                    }

                    lock (logLock)
                    {

                        File.AppendAllLines(Path.Combine(dataStringLogPath, fileName), logText);
                    }
                }
                catch (Exception) 
                {

                }
            }

            /// <summary>
            /// Write exception to log file
            /// </summary>
            /// <param name="exception">Exception</param>
            public void WriteException(Exception exception, string specialText = null)
            {
                if (exception != null)
                {
                    Type exceptionType = exception.GetType();
                    string text = string.Empty;
                    if (!string.IsNullOrEmpty(specialText))
                    {
                        text = text + specialText + Environment.NewLine;
                    }
                    text = "Exception: " + exceptionType.Name + Environment.NewLine;
                    text += "               " + "Message: " + exception.Message + Environment.NewLine;
                    text += "               " + "Source: " + exception.Source + Environment.NewLine;
                    text += "               " + "StackTrace: " + exception.StackTrace + Environment.NewLine;
                    WriteLog(text, LogType.Error);
                }
            }
        }

    }

    public class ProgramConfig
    {
        public string IPAddress { get; set; }
        public string Port { get; set; }
        public string OnnxName { get; set; }
        public string DetectionType { get; set; }

    }

    public class IdentifyingEvents
    {
        /// <summary>
        /// 识别时间
        /// </summary>
        public long RecognitionTime { get; set; }
        /// <summary>
        /// 图片地址
        /// </summary>
        public string[] ImagesPath { get; set; }
    }

    public class IdentifyingResult 
    {
        /// <summary>
        /// 事件时间
        /// </summary>
        public long TriggerTime { get; set; }
        /// <summary>
        /// 是否出现火焰
        /// </summary>
        public bool HasFire { get; set; } = false;
        /// <summary>
        /// 是否出现烟雾
        /// </summary>
        public bool HasSmoke { get; set; } = false;
        /// <summary>
        /// 火焰置信值
        /// </summary>
        public float FireConfidenceLevel { get; set; } = 0;
        /// <summary>
        /// 烟雾置信值
        /// </summary>
        public float SmokeConfidenceLevel { get; set; } = 0;
        /// <summary>
        /// 识别错误ID
        /// </summary>
        public int ErrorID { get; set; } = 0;
    }
}