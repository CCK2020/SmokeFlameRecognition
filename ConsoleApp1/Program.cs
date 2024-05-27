using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using System.Diagnostics;
using System.Reflection;

namespace ConsoleApp1
{
    internal class Program
    {
        static Socket socket;
        static string IP = "192.168.1.103";
        static string Port = "23";
        static bool connect = false;
        static byte[] information = new byte[4096];
        static void Main(string[] args)
        {
            IP = Console.ReadLine();
            Port = Console.ReadLine();
            //TcpServer(555);
            OpenConnect();
            while (true)
            {
                Thread.Sleep(3000);
            }
        }
        public static void OpenConnect()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;
            Task task = new Task(() =>
            {
                Thread.Sleep(5000);
                while (true)
                {
                    while (!connect)
                    {
                        socket.Close();
                        socket.Dispose();
                        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        socket.NoDelay = true;
                        socket.BeginConnect(IPAddress.Parse(IP), int.Parse(Port), AsyncConnectback, null);
                    }
                }
            });
            task.Start();
            socket.BeginConnect(IPAddress.Parse(IP), int.Parse(Port), AsyncConnectback, socket);
        }
        public static void AsyncConnectback(IAsyncResult ar)
        {
            try
            {
                socket.BeginReceive(information, 0, information.Length, SocketFlags.None, AsyncReceiveback, null);
                connect = true;
                while (connect)
                {
                    string sendData = JsonSerializer.Serialize(new IdentifyingEvents() { RecognitionTime = DateTime.Now.Ticks, ImagesPath = new string[] { @"C:\Users\Administrator\Desktop\net6.0\1.jpg", @"C:\Users\Administrator\Desktop\net6.0\2.jpg", @"C:\Users\Administrator\Desktop\net6.0\3.jpg" } });
                    socket.Send(Encoding.UTF8.GetBytes(sendData));
                    CLog.Instance.WriteLog("发送数据：" + sendData);
                    Thread.Sleep(3000);
                }
            }
            catch (Exception ex)
            {

            }
        }
        public static void AsyncReceiveback(IAsyncResult ar)
        {
            try
            {
                int ReceiveIndex = socket.EndReceive(ar);
                string ReceiveMsg = Encoding.UTF8.GetString(information, 0, ReceiveIndex);
                CLog.Instance.WriteLog("接收数据：" + ReceiveMsg);
                socket.BeginReceive(information, 0, information.Length, SocketFlags.None, AsyncReceiveback, socket);
            }
            catch (Exception ex)
            {
                connect = false;
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
}
