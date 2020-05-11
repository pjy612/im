using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BiliEntity;
using CSRedis;
using NewLife;
using NewLife.Data;
using NewLife.Json;
using NewLife.Log;
using NewLife.Model;
using NewLife.Net;
using NewLife.Net.Handlers;
using NewLife.Security;
using NewLife.Serialization;
using NewLife.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XCode;
using XCode.DataAccessLayer;
using YjMonitorNet;

namespace YjMonitor
{
    [JsonConfigFile("app.json", 10_000)]
    public class YjMonitorConfig : JsonConfig<YjMonitorConfig>
    {
        public string Key { get; set; } = "admin";
        public string Address { get; set; } = "tcp://127.0.0.1:8002";
        public string Redis { get; set; } = "127.0.0.1:6379,poolsize=5";
        public string[] ImServers { get; set; } = new[] {"127.0.0.1:7777"};
        public double Rand { get; set; } = 0.8;
    }

    class Program
    {
        static string data = new
        {
            data = new
            {
                key = "admin"
            }
        }.ToJson();

        public class FixedQueue<T> : ConcurrentQueue<T>
        {
            public int limit { get; set; }

            public FixedQueue(int limit)
            {
                this.limit = limit;
            }

            public new bool Enqueue(T item)
            {
                if (this.Contains(item)) return false;
                base.Enqueue(item);
                if (Count > limit)
                    this.TryDequeue(out var tmp);
                return true;
            }
        }

        private static void PollWestApi()
        {
            XTrace.WriteLine("POST WestApi Start");
            List<StormGiftDTO> stormGiftDtos = WestApi.GetStorm();
            List<TvGiftDTO> tvGiftDtos = WestApi.GetTv();
            List<GuardGiftDTO> guardGiftDtos = WestApi.GetGuard();
            RaffleList.Meta.ProcessWithSplit(RaffleList.Meta.ConnName, $"{RaffleList.Meta.TableName}_west", () =>
            {
                List<RaffleList> list = new List<RaffleList>();
                if (stormGiftDtos.Any())
                {
                    List<long> ids = stormGiftDtos.Select(r => r.Id).ToList();

                    IList<RaffleList> entitys = RaffleList.FindAllByRaffleIDsAndType(ids, "STORM");

                    stormGiftDtos = stormGiftDtos.Where(r => entitys.All(c => c.RaffleID != r.Id)).ToList();
                    if (stormGiftDtos.Any())
                    {
                        var tmp = stormGiftDtos.AsParallel().Select(item =>
                        {
                            var raffle = new RaffleList();
                            raffle.RaffleID     = item.Id;
                            raffle.RaffleIDSort = item.Id / 1000000;
                            raffle.RoomID       = item.RoomId;
                            raffle.RaffleType   = "STORM";
                            raffle.EndTime      = ToLocal(item.Time + 60);
                            raffle.Data         = JsonConvert.SerializeObject(item);
                            raffle.CreateAt     = ToLocal(item.Time);
                            return raffle;
                        }).ToList();
                        list.AddRange(tmp);
                    }
                }
                if (guardGiftDtos.Any())
                {
                    List<long> ids = guardGiftDtos.Select(r => r.Id).ToList();

                    IList<RaffleList> entitys = RaffleList.FindAllByRaffleIDsAndType(ids, "GUARD");
                    guardGiftDtos = guardGiftDtos.Where(r => entitys.All(c => c.RaffleID != r.Id)).ToList();
                    if (guardGiftDtos.Any())
                    {
                        var tmp = guardGiftDtos.AsParallel().Select(item =>
                        {
                            var raffle = new RaffleList();
                            raffle.RaffleID   = raffle.RaffleIDSort = item.Id;
                            raffle.RoomID     = item.RoomId;
                            raffle.RaffleType = "GUARD";
                            raffle.EndTime    = ToLocal(item.EndTime);
                            raffle.GuardType  = $"{item.Type}";
                            raffle.Data       = JsonConvert.SerializeObject(item);
                            raffle.CreateAt   = ToLocal(item.Time);
                            return raffle;
                        }).ToList();
                        list.AddRange(tmp);
                    }
                }
                if (tvGiftDtos.Any())
                {
                    List<long> ids = tvGiftDtos.Select(r => r.Id).ToList();

                    IList<RaffleList> entitys = RaffleList.FindAllByRaffleIDsAndType(ids, "TV");
                    tvGiftDtos = tvGiftDtos.Where(r => entitys.All(c => c.RaffleID != r.Id)).ToList();
                    if (tvGiftDtos.Any())
                    {
                        var tmp = tvGiftDtos.AsParallel().Select(item =>
                        {
                            var raffle = new RaffleList();
                            raffle.RaffleID   = raffle.RaffleIDSort = item.Id;
                            raffle.RoomID     = item.RoomId;
                            raffle.RaffleType = "TV";
                            raffle.EndTime    = ToLocal(item.EndTime);
                            raffle.TvType     = item.GiftTypeForJoin;
                            raffle.Data       = JsonConvert.SerializeObject(item);
                            raffle.CreateAt   = ToLocal(item.Time);
                            return raffle;
                        }).ToList();
                        list.AddRange(tmp);
                    }
                }
                if (list.Any())
                {
                    list.Insert();
                }
                return true;
            });
        }

        static DateTime ToLocal(long time)
        {
            return new DateTime(1970, 1, 1).ToLocalTime().AddSeconds(time);
        }

        static RaffleList Process(string item)
        {
            try
            {
                JToken data = JObject.Parse(item);
                long room_id = data["data"]["room_id"].Value<int>();
                string raffle_type = data["data"]["raffle_type"].Value<string>();
                long raffle_id = data["data"]["raffle_id"].Value<long>();
                long end_time = data["data"]["end_time"].Value<long>();
                RaffleList raffle = RaffleList.FindByRaffleIDAndType(raffle_id, raffle_type);
                if (raffle != null)
                {
                    return null;
                }

                raffle            = new RaffleList();
                raffle.RaffleID   = raffle.RaffleIDSort = raffle_id;
                raffle.RoomID     = room_id;
                raffle.RaffleType = raffle_type;
                raffle.EndTime    = ToLocal(end_time);
                string key = $"RR:{room_id}";
                TimeSpan exp = raffle.EndTime - DateTime.Now;
                var exps = RedisHelper.Ttl(key);
                if (exps < exp.TotalSeconds)
                {
                    RedisHelper.Set(key, 1, exp.TotalSeconds.ToInt());
                }
                switch (raffle_type)
                {
                    case "TV":
                        raffle.TvType = data["data"]["other_raffle_data"]["type"].Value<string>();
                        break;
                    case "GUARD":
                        raffle.GuardType = data["data"]["other_raffle_data"]["privilege_type"].Value<string>();
                        break;
                    case "PK":
                        break;
                    case "STORM":
                        raffle.RaffleIDSort = raffle_id / 1000000;
                        break;
                }
                raffle.Data     = item;
                raffle.CreateAt = DateTime.Now;
                return raffle;
            }
            catch
            {
                Console.WriteLine($"ErrorData：{item}");
            }
            return null;
        }

        static void Main(string[] args)
        {
            DAL.AddConnStr("BiliCenter", "Server=127.0.0.1;port=3306;Database=im;Uid=poster;Pwd=;SslMode=none;Convert Zero Datetime=True;Allow Zero Datetime=True;Allow User Variables=True;", null,"mysql");
            XTrace.UseConsole();
            CSRedisClient csRedisClient = new CSRedis.CSRedisClient(JsonConfig<YjMonitorConfig>.Current.Redis);
            RedisHelper.Initialization(csRedisClient);
            ImHelper.Initialization(new ImClientOptions
            {
                Redis   = csRedisClient,
                Servers = JsonConfig<YjMonitorConfig>.Current.ImServers
            });
            BlockingCollection<RaffleList> SaveQueue = new BlockingCollection<RaffleList>();
            ConcurrentQueue<(int, string)> Queue = new ConcurrentQueue<(int, string)>();
            Task.Run(() =>
            {
                foreach (var item in SaveQueue.GetConsumingEnumerable())
                {
                    item.Insert();
                    Thread.Sleep(50);
                }
            });
            Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(2500);
                    HashSet<(int, string)> set = new HashSet<(int, string)>();
                    while (Queue.TryDequeue(out var tuple))
                    {
                        set.Add(tuple);
                    }
                    if (set.Any())
                    {
                        set.ToList().ForEach((item) =>
                        {
                            ImHelper.SendMessageOnline(JsonConvert.SerializeObject(new
                            {
                                code = 0,
                                type = "raffle",
                                data = new
                                {
                                    room_id     = item.Item1,
                                    raffle_type = item.Item2
                                }
                            }));
                        });
                    }
                }
            });
            new TimerX(state =>
            {
                try
                {
                    PollWestApi();
                }
                catch (Exception e)
                {
                    XTrace.WriteException(e);
                }
            }, null, 1000, 10_000);
            YjClient yjClient = new YjClient(JsonConfig<YjMonitorConfig>.Current.Address, JsonConfig<YjMonitorConfig>.Current.Key);
            yjClient.OnReceived = (s, e) =>
            {
                var pk = e.Message as Packet;
                string msg = pk.ToStr();
                string unescape = Regex.Unescape(msg);
                XTrace.WriteLine("收到：{0}", unescape);
                try
                {
                    JObject data = JObject.Parse(unescape);
                    RaffleList raffleList = Process(unescape);
                    SaveQueue.Add(raffleList);
                    if (new Random(Guid.NewGuid().GetHashCode()).NextDouble() < JsonConfig<YjMonitorConfig>.Current.Rand)
                    {
                        //ImHelper.SendMessageOnline(msg);
                        int room_id = data["data"]["room_id"].Value<int>();
                        string type = data["data"]["raffle_type"].Value<string>();
                        switch (type)
                        {
                            case "TV":
                            case "GUARD":
                            case "PK":
                                Queue.Enqueue((room_id, "GIFT"));
                                break;
                            case "STORM":
                                Queue.Enqueue((room_id, "STORM"));
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
                //var socketClient = s as ISocketClient;
                //socketClient.SendMessage(new Packet(new byte[0]));
            };
            yjClient.ReConnect();
            while (true)
            {
                Console.Read();
            }
        }

        public class YjClient
        {
            private NetUri net;
            private string key;
            private TcpSession client;
            private int heartBeat = 30;
            private TimerX timer;
            private TimerX onlineCheck;

            private string keyReqData => new
            {
                data = new
                {
                    key = key
                }
            }.ToJson();

            public YjClient(string uri = "tcp://127.0.0.1:8002", string key = "admin")
            {
                this.net = new NetUri(uri);
                this.key = key;
            }

            public void Connect()
            {
                client               = net.CreateRemote() as TcpSession;
                client.Log           = XTrace.Log;
                client.MaxAsync      = 1;
                client.AutoReconnect = 0;
//                    client.LogSend    = true;
//                    client.LogReceive = true;
                client.Add(new LengthFieldCodec {Size = -4});
                client.Opened         += Client_Opened;
                client.Received       += Client_Received;
                client.Closed         += Client_Closed;
                client.Error          += Client_Error;
                client.ThrowException =  false;
                client.OnDisposed     += Client_OnDisposed;
                try
                {
                    client.Open();
                }
                finally
                {
                    onlineCheck = new TimerX(state =>
                    {
                        //XTrace.WriteLine("自检");
                        ISocketClient socketClient = state as ISocketClient;
                        if (!socketClient.Active) ReConnect();
                    }, client, 1_000, 1_000) {CanExecute = () => !client.Active};
                }
            }

            private void Client_OnDisposed(object sender, EventArgs e)
            {
                //XTrace.WriteLine("销毁...");
            }

            private void Client_Received(object sender, ReceivedEventArgs e)
            {
                var pk = e.Message as Packet;
                string str = pk.ToStr();
                if (str.IsNullOrWhiteSpace()) return;
                JObject data = JObject.Parse(str);
                string data_type = data["type"].Value<string>();
                if (data_type == "entered")
                {
                    XTrace.WriteLine("监控端连接成功！");
                    timer = new TimerX(state =>
                    {
                        XTrace.WriteLine("发送心跳检测...");
                        try
                        {
                            Send("");
                        }
                        catch (Exception exception)
                        {
                            XTrace.WriteException(exception);
                            ReConnect();
                        }
                    }, null, 0, 30_000) {CanExecute = () => client.Active};
                    return;
                }
                else if (data_type == "error")
                {
                    XTrace.WriteLine("监控端异常断开准备重连！");
                    client?.Close("监控端异常断开准备重连！");
                    return;
                }
                OnReceived?.Invoke(sender, e);
            }

            public void ReConnect()
            {
                //again:
                try
                {
                    //client?.Close("ReConnect");
                    client?.Dispose();
                    onlineCheck?.Dispose();
                    timer?.Dispose();
                    Connect();
                }
                catch (Exception e)
                {
                    //goto again;
                }
            }

            private void Client_Error(object sender, ExceptionEventArgs e)
            {
                XTrace.WriteLine("连接发生错误,尝试重连...");
                //ReConnect();
            }

            private void Client_Closed(object sender, EventArgs e)
            {
                XTrace.WriteLine("连接断开,尝试重连...");
                //ReConnect();
            }

            private void Client_Opened(object sender, EventArgs e)
            {
                Send(keyReqData);
            }

            public void Send(string data)
            {
                client.SendMessage(new Packet(Encoding.UTF8.GetBytes(data)));
            }

            public EventHandler<ReceivedEventArgs> OnReceived;
        }

        class YjPythonHandle : MessageCodec<Packet>
        {
            protected override object Encode(IHandlerContext context, Packet msg)
            {
                var lenByte = new Byte[4];
                lenByte.Write((uint) msg.Count, 0, false);
                Packet packet = new Packet(lenByte) {Next = msg};
                return packet;
            }

            protected override IList<Packet> Decode(IHandlerContext context, Packet pk)
            {
                byte[] readBytes = pk.ReadBytes(0, 4);
                if (BitConverter.IsLittleEndian) Array.Reverse(readBytes);
                int int32 = BitConverter.ToInt32(readBytes);
                return base.Decode(context, pk);
            }
        }

        static void NET45ClientStart()
        {
            try
            {
                TimerX heatbeat = null;
                SocketClient client = new SocketClient(8002);
                //绑定当收到服务器发送的消息后的处理事件
                client.HandleRecMsg = new Action<byte[], SocketClient>((bytes, theClient) =>
                {
                    BinaryReader br = new BinaryReader(new MemoryStream(bytes));
                    byte[] readBytes = br.ReadBytes(4);
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(readBytes);
                    }
                    int len = BitConverter.ToInt32(readBytes, 0);
                    byte[] readBytes1 = br.ReadBytes(len);
                    string s = Encoding.UTF8.GetString(readBytes1);
                    string unescape = Regex.Unescape(s);

                    Console.WriteLine($"收到消息:{unescape}");
                });

                //绑定向服务器发送消息后的处理事件
                client.HandleSendMsg = new Action<byte[], SocketClient>((bytes, theClient) =>
                {
                    string msg = Encoding.UTF8.GetString(bytes);
                    Console.WriteLine($"向服务器发送消息:{msg}");
                });

                client.HandleClientStarted = socketClient =>
                {
                    Console.WriteLine("连接成功");
                    client.Send(data);
                    heatbeat = new TimerX(state =>
                    {
                        if (client.IsSocketConnected())
                        {
                            client.Send("");
                        }
                    }, null, 30_000, 30_000);
                };
                client.HandleClientClose = socketClient =>
                {
                    Console.WriteLine("连接断开");
                    heatbeat?.Dispose();
                    Thread.Sleep(3_000);
                    client.StartClient();
                };

                client.HandleException = exception =>
                {
                    Console.WriteLine(exception.Message);
                    heatbeat?.Dispose();
                    Thread.Sleep(3_000);
                    client.StartClient();
                };

                //开始运行客户端
                client.StartClient();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// Socket客户端
        /// </summary>
        public class SocketClient
        {
            #region 构造函数

            /// <summary>
            /// 构造函数,连接服务器IP地址默认为本机127.0.0.1
            /// </summary>
            /// <param name="port">监听的端口</param>
            public SocketClient(int port)
            {
                _ip   = "127.0.0.1";
                _port = port;
            }

            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="ip">监听的IP地址</param>
            /// <param name="port">监听的端口</param>
            public SocketClient(string ip, int port)
            {
                _ip   = ip;
                _port = port;
            }

            #endregion

            #region 内部成员

            private Socket _socket = null;
            private string _ip = "";
            private int _port = 0;
            private bool _isRec = true;

            public bool IsSocketConnected()
            {
                bool part1 = _socket.Poll(1000, SelectMode.SelectRead);
                bool part2 = (_socket.Available == 0);
                if (part1 && part2)
                    return false;
                else
                    return true;
            }

            /// <summary>
            /// 开始接受客户端消息
            /// </summary>
            public void StartRecMsg()
            {
                try
                {
                    byte[] container = new byte[1024 * 1024 * 2];
                    _socket.BeginReceive(container, 0, container.Length, SocketFlags.None, asyncResult =>
                    {
                        try
                        {
                            int length = _socket.EndReceive(asyncResult);

                            //马上进行下一轮接受，增加吞吐量
                            if (length > 0 && _isRec && IsSocketConnected())
                                StartRecMsg();

                            if (length > 0)
                            {
                                byte[] recBytes = new byte[length];
                                Array.Copy(container, 0, recBytes, 0, length);

                                //处理消息
                                HandleRecMsg?.BeginInvoke(recBytes, this, null, null);
                            }
                            else
                                Close();
                        }
                        catch (Exception ex)
                        {
                            HandleException?.BeginInvoke(ex, null, null);
                            Close();
                        }
                    }, null);
                }
                catch (Exception ex)
                {
                    HandleException?.BeginInvoke(ex, null, null);
                    Close();
                }
            }

            #endregion

            #region 外部接口

            /// <summary>
            /// 开始服务，连接服务端
            /// </summary>
            public void StartClient()
            {
                try
                {
                    //实例化 套接字 （ip4寻址协议，流式传输，TCP协议）
                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    //创建 ip对象
                    IPAddress address = IPAddress.Parse(_ip);
                    //创建网络节点对象 包含 ip和port
                    IPEndPoint endpoint = new IPEndPoint(address, _port);
                    //将 监听套接字  绑定到 对应的IP和端口
                    _socket.BeginConnect(endpoint, asyncResult =>
                    {
                        try
                        {
                            _socket.EndConnect(asyncResult);
                            //开始接受服务器消息
                            StartRecMsg();

                            HandleClientStarted?.BeginInvoke(this, null, null);
                        }
                        catch (Exception ex)
                        {
                            HandleException?.BeginInvoke(ex, null, null);
                        }
                    }, null);
                }
                catch (Exception ex)
                {
                    HandleException?.BeginInvoke(ex, null, null);
                }
            }

            /// <summary>
            /// 发送数据
            /// </summary>
            /// <param name="bytes">数据字节</param>
            public void Send(byte[] bytes)
            {
                try
                {
                    _socket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, asyncResult =>
                    {
                        try
                        {
                            int length = _socket.EndSend(asyncResult);
                            HandleSendMsg?.BeginInvoke(bytes, this, null, null);
                        }
                        catch (Exception ex)
                        {
                            HandleException?.BeginInvoke(ex, null, null);
                        }
                    }, null);
                }
                catch (Exception ex)
                {
                    HandleException?.BeginInvoke(ex, null, null);
                }
            }

            /// <summary>
            /// 发送字符串（默认使用UTF-8编码）
            /// </summary>
            /// <param name="msgStr">字符串</param>
            public void Send(string msgStr)
            {
                BinaryWriter bw = new BinaryWriter(new MemoryStream());
                byte[] bytes = Encoding.UTF8.GetBytes(msgStr);
                byte[] bytes1 = BitConverter.GetBytes(bytes.Length);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes1);
                }
                bw.Write(bytes1);
                bw.Write(bytes);
                bw.Flush();
                Send(bw.BaseStream.ToArray());
            }

            /// <summary>
            /// 发送字符串（使用自定义编码）
            /// </summary>
            /// <param name="msgStr">字符串消息</param>
            /// <param name="encoding">使用的编码</param>
            public void Send(string msgStr, Encoding encoding)
            {
                BinaryWriter bw = new BinaryWriter(new MemoryStream());
                byte[] bytes = encoding.GetBytes(msgStr);
                byte[] bytes1 = BitConverter.GetBytes(bytes.Length);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes1);
                }
                bw.Write(bytes1);
                bw.Write(bytes);
                bw.Flush();
                Send(bw.BaseStream.ToArray());
            }

            /// <summary>
            /// 传入自定义属性
            /// </summary>
            public object Property { get; set; }

            /// <summary>
            /// 关闭与服务器的连接
            /// </summary>
            public void Close()
            {
                try
                {
                    _isRec = false;
                    _socket.Disconnect(false);
                    HandleClientClose?.BeginInvoke(this, null, null);
                }
                catch (Exception ex)
                {
                    HandleException?.BeginInvoke(ex, null, null);
                }
                finally
                {
                    _socket.Dispose();
                    GC.Collect();
                }
            }

            #endregion

            #region 事件处理

            /// <summary>
            /// 客户端连接建立后回调
            /// </summary>
            public Action<SocketClient> HandleClientStarted { get; set; }

            /// <summary>
            /// 处理接受消息的委托
            /// </summary>
            public Action<byte[], SocketClient> HandleRecMsg { get; set; }

            /// <summary>
            /// 客户端连接发送消息后回调
            /// </summary>
            public Action<byte[], SocketClient> HandleSendMsg { get; set; }

            /// <summary>
            /// 客户端连接关闭后回调
            /// </summary>
            public Action<SocketClient> HandleClientClose { get; set; }

            /// <summary>
            /// 异常处理程序
            /// </summary>
            public Action<Exception> HandleException { get; set; }

            #endregion
        }
    }
}