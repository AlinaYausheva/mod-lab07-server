using System;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace Lab07
{
    class Program
    {
        static void Main()
        {
            int poolNum;
            int clientIntensity;
            int serverIntensity;
            int requestNum;
            double T;
            string[] data = ReadingFile("parameters.txt");
            for (int j = 0; j < data.Length; j++)
            {
                Stopwatch stopwatch = new Stopwatch();
                string[] d = data[j].Split(' ');
                requestNum = int.Parse(d[0]);
                poolNum = int.Parse(d[1]);
                clientIntensity = int.Parse(d[2]);
                serverIntensity = int.Parse(d[3]);

                Server server = new Server(poolNum, serverIntensity);
                Client client = new Client(server);

                stopwatch.Start();
                for (int id = 1; id <= requestNum; id++)
                {
                    client.send(id);
                    Thread.Sleep(50);
                }
                stopwatch.Stop();
                T = (double)stopwatch.ElapsedMilliseconds / 1000;

                WritingFile(j, poolNum, clientIntensity, serverIntensity, requestNum, T, server);

                Console.WriteLine("Всего заявок: {0}", server.requestCount);
                Console.WriteLine("Обработано заявок: {0}", server.processedCount);
                Console.WriteLine("Отклонение заявок: {0}", server.rejectedCount);
            }

        }

        static string[] ReadingFile(string file_name)
        {
            file_name = Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory + @"..\\..\\..\\"), file_name);
            StreamReader file = new StreamReader(file_name);

            string[] initialData = file.ReadToEnd().Split("\r\n");
            file.Close();
            return initialData;
        }

        static public void WritingFile(int id, int poolNum, int clientIntensity, int serverIntensity, int requestNum, double T, Server server)
        {
            string file_name = Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory + @"..\\..\\..\\"), "results.txt");
            StreamWriter file = new StreamWriter(file_name, true);

            double lambda = calculateLambda(server.requestCount, T);
            lambda = server.requestCount / T;
            double nu = calculateNu(server.processedCount, poolNum, T);
            double r = calculateR(clientIntensity, serverIntensity);
            double rReal = calculateRReal(lambda, nu);
            double Pn = calculatePn(poolNum, r);
            double PnReal = calculatePn(poolNum, rReal);
            file.WriteLine("Номер запроса: " + (id + 1));
            file.WriteLine("\nВходные параметры\n");
            file.WriteLine("Всего заявок: " + requestNum);
            file.WriteLine("Интенсивность потока заявок: " + clientIntensity);
            file.WriteLine("Интенсивность потока обслуживания: " + serverIntensity);
            file.WriteLine("Количество потоков сервера: " + poolNum);
            file.WriteLine("\nРезультаты работы\n");
            file.WriteLine("Время работы " + T + " c.");
            file.WriteLine("Обработано заявок: " + server.processedCount);
            file.WriteLine("Отклонено заявок: " + server.rejectedCount);
            file.WriteLine("\nОжидаемые результаты\n");
            file.WriteLine("Вероятность простоя сервера: " + calculateP0(poolNum, r));
            file.WriteLine("Вероятность отказа сервера: " + Pn);
            file.WriteLine("Относительная пропускная способность: " + calculateQ(Pn));
            file.WriteLine("Абсолютная пропускная способность: " + calculateA(lambda, Pn));
            file.WriteLine("Среднее число занятых процессов: " + calculateK(lambda, nu, Pn));
            file.WriteLine("\nПолученные результаты\n");
            file.WriteLine("Вероятность простоя сервера: " + calculateP0(poolNum, rReal));
            file.WriteLine("Вероятность отказа сервера: " + PnReal);
            file.WriteLine("Относительная пропускная способность: " + calculateQ(PnReal));
            file.WriteLine("Абсолютная пропускная способность: " + calculateA(lambda, PnReal));
            file.WriteLine("Среднее число занятых процессов: " + calculateK(lambda, nu, PnReal));
            file.WriteLine("\n");

            file.Close();
        }

        static public double calculateLambda(int requestCount, double T)
        {
            return requestCount / T;
        }

        static public double calculateNu(int processedCount, int poolNum, double T)
        {
            return processedCount / (T * poolNum);
        }

        static public double calculateRReal(double lambda, double nu)
        {
            return lambda / nu;
        }

        static public double calculateR(int clientIntensity, int serverIntensity)
        {
            return clientIntensity / serverIntensity;
        }

        static public double calculateP0(int poolNum, double r)
        {
            double ans = 0;
            for (int i = 0; i <= poolNum; i++)
            {
                ans += Math.Pow(r, i) * Factorial_1(i);
            }
            return Math.Pow(ans, -1);
        }

        static public double calculatePn(int poolNum, double r)
        {
            double ans = (Math.Pow(r, poolNum) * Factorial_1(poolNum)) * calculateP0(poolNum, r);
            return ans;
        }

        static public double calculateQ(double Pn)
        {
            return 1 - Pn;
        }

        static public double calculateA(double lambda, double Pn)
        {
            return (1 - Pn) * lambda;
        }

        static public double calculateK(double lambda, double nu, double Pn)
        {
            return (1 - Pn) * lambda / nu;
        }

        static public double Factorial_1(int num)
        {
            double ans = 1;
            for (int i = 1; i <= num; i++)
            {
                ans *= i;
            }
            return Math.Pow(ans, -1);
        }
    }
    struct PoolRecord
    {
        public Thread thread;
        public bool in_use;
    }

    class Server
    {
        private PoolRecord[] pool;
        private object threadLock = new object();
        public int requestCount = 0;
        public int processedCount = 0;
        public int rejectedCount = 0;
        public int intensity;

        public Server(int poolNum, int serverIntensity)
        {
            pool = new PoolRecord[poolNum];
            intensity = serverIntensity;
        }

        public void proc(object sender, procEventArgs e)
        {
            lock (threadLock)
            {
                requestCount++;
                for (int i = 0; i < pool.Length; i++)
                {
                    if (!pool[i].in_use)
                    {
                        pool[i].in_use = true;
                        pool[i].thread = new Thread(new ParameterizedThreadStart(Answer));
                        pool[i].thread.Start(e.id);
                        processedCount++;
                        return;
                    }
                }
                rejectedCount++;
            }
        }
        public void Answer(object arg)
        {
            int id = (int)arg;
            Thread.Sleep(TimeSpan.FromSeconds(1 / (double)intensity));
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].thread == Thread.CurrentThread)
                    pool[i].in_use = false;
        }
    }
    class Client
    {
        private Server server;
        public Client(Server server)
        {
            this.server = server;
            this.request += server.proc;
        }
        public void send(int id)
        {
            procEventArgs args = new procEventArgs();
            args.id = id;
            OnProc(args);
        }
        protected virtual void OnProc(procEventArgs e)
        {
            EventHandler<procEventArgs> handler = request;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        public event EventHandler<procEventArgs> request;
    }
    public class procEventArgs : EventArgs
    {
        public int id { get; set; }
    }
}
