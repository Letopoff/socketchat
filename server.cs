using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace server
{
    class ServerObject
    {
        TcpListener tcpListener = new TcpListener(IPAddress.Any, 8888); // сервер для прослушивания
        List<ClientObject> clients = new List<ClientObject>(); // все подключения
        protected internal void RemoveConnection(string id)
        {
            // получаем по id закрытое подключение
            ClientObject? client = clients.FirstOrDefault(c => c.Id == id);
            // и удаляем его из списка подключений
            if (client != null) clients.Remove(client);
            client?.Close();
        }
        // прослушивание входящих подключений
        protected internal async Task ListenAsync()
        {
            try
            {
                tcpListener.Start();
                Console.WriteLine("Сервер запущен. Ожидание подключений...");

                while (true)
                {
                    TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();

                    ClientObject clientObject = new ClientObject(tcpClient, this);
                    clients.Add(clientObject);
                    Task.Run(clientObject.ProcessAsync);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Disconnect();
            }
        }
        // трансляция сообщения подключенным клиентам
        protected internal async Task BroadcastMessageAsync(string message)
        {
            DateTime startTime = DateTime.Now;
            foreach (var client in clients)
            {
                if (client.Id != null)
                {
                    await client.Writer.WriteLineAsync($"{message}");//передача данных
                    await client.Writer.FlushAsync();
                }
            }
        }
        // отключение всех клиентов
        protected internal void Disconnect()
        {
            foreach (var client in clients)
            {
                client.Close(); //отключение клиента
            }
            tcpListener.Stop(); //остановка сервера
        }
    }
    class ClientObject
    {
        protected internal string Id { get; } = Guid.NewGuid().ToString();
        protected internal StreamWriter Writer { get; }
        protected internal StreamReader Reader { get; }

        TcpClient client;
        ServerObject server; // объект сервера

        public ClientObject(TcpClient tcpClient, ServerObject serverObject)
        {
            client = tcpClient;
            server = serverObject;
            // получаем NetworkStream для взаимодействия с сервером
            var stream = client.GetStream();
            // создаем StreamReader для чтения данных
            Reader = new StreamReader(stream);
            // создаем StreamWriter для отправки данных
            Writer = new StreamWriter(stream);
        }
        public async Task ProcessAsync()
        {
            try
            {
                // получаем имя пользователя
                string? userName = await Reader.ReadLineAsync();
                string? message = $"{userName} вошел в чат";
                // посылаем сообщение о входе в чат всем подключенным пользователям
                await server.BroadcastMessageAsync(message);
                Console.WriteLine(message);
                // в бесконечном цикле получаем сообщения от клиента
                while (true)
                {
                    try
                    {
                        message = await Reader.ReadLineAsync();
                        if (message == null) continue;
                        Console.WriteLine(message);
                        await server.BroadcastMessageAsync(message);
                    }
                    catch
                    {
                        message = $"{userName} покинул чат";
                        Console.WriteLine(message);
                        await server.BroadcastMessageAsync(message);
                        break;
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine($"Возникла ошибка, скорее всего отключение пользователя!"); ;
            }
            finally
            {
                // в случае выхода из цикла закрываем ресурсы
                server.RemoveConnection(Id);
            }
        }
        public DateTime getDateTime() => DateTime.Now;
        // закрытие подключения
        protected internal void Close()
        {
            Writer.Close();
            Reader.Close();
            client.Close();
        }
    }
    internal class Program
    {

        static async Task Main(string[] args)
        {
            ServerObject server = new ServerObject();// создаем сервер
            await server.ListenAsync(); // запускаем сервер
        }
    }
}
