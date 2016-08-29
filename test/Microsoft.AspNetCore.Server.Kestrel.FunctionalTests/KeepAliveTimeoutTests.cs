// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Testing;
using Xunit;

namespace Microsoft.AspNetCore.Server.Kestrel.FunctionalTests
{
    public class KeepAliveTimeoutTests
    {
        private static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromSeconds(1);
        private const int LongDelay = 5000; // milliseconds
        private const int ShortDelay = 250; // milliseconds

        [Fact]
        public async Task ConnectionClosedWhenKeepAliveTimeoutExpires()
        {
            using (var host = StartHost())
            {
                using (var connection = new TestConnection(host.GetPort()))
                {
                    await connection.Send(
                        "GET / HTTP/1.1",
                        "",
                        "");
                    await ReceiveResponse(connection);

                    await Task.Delay(LongDelay);

                    await Assert.ThrowsAsync<IOException>(async () =>
                    {
                        await connection.Send(
                            "GET / HTTP/1.1",
                            "",
                            "");
                        await ReceiveResponse(connection);
                    });
                }
            }
        }

        [Fact]
        public async Task ConnectionClosedWhenKeepAliveTimeoutExpiresAfterChunkedRequest()
        {
            using (var host = StartHost())
            {
                using (var connection = new TestConnection(host.GetPort()))
                {
                    await connection.Send(
                            "POST / HTTP/1.1",
                            "Transfer-Encoding: chunked",
                            "",
                            "5", "hello",
                            "6", " world",
                            "0",
                             "",
                             "");
                    await ReceiveResponse(connection);

                    await Task.Delay(LongDelay);

                    await Assert.ThrowsAsync<IOException>(async () =>
                    {
                        await connection.Send(
                            "GET / HTTP/1.1",
                            "",
                            "");
                        await ReceiveResponse(connection);
                    });
                }
            }
        }

        [Fact]
        public async Task KeepAliveTimeoutResetsBetweenContentLengthRequests()
        {
            using (var host = StartHost())
            {
                using (var connection = new TestConnection(host.GetPort()))
                {
                    for (var i = 0; i < 10; i++)
                    {
                        await connection.Send(
                            "GET / HTTP/1.1",
                            "",
                            "");
                        await ReceiveResponse(connection);
                        await Task.Delay(ShortDelay);
                    }
                }
            }
        }

        [Fact]
        public async Task KeepAliveTimeoutResetsBetweenChunkedRequests()
        {
            using (var host = StartHost())
            {
                using (var connection = new TestConnection(host.GetPort()))
                {
                    for (var i = 0; i < 5; i++)
                    {
                        await connection.Send(
                            "POST / HTTP/1.1",
                            "Transfer-Encoding: chunked",
                            "",
                            "5", "hello",
                            "6", " world",
                            "0",
                             "",
                             "");
                        await ReceiveResponse(connection);
                        await Task.Delay(ShortDelay);
                    }
                }
            }
        }

        [Fact]
        public async Task KeepAliveTimeoutNotTriggeredMidContentLengthRequest()
        {
            using (var host = StartHost())
            {
                using (var connection = new TestConnection(host.GetPort()))
                {
                    await connection.Send(
                        "POST / HTTP/1.1",
                        "Content-Length: 8",
                        "",
                        "a");
                    await Task.Delay(LongDelay);
                    await connection.Send("bcdefgh");
                    await ReceiveResponse(connection);
                }
            }
        }

        [Fact]
        public async Task KeepAliveTimeoutNotTriggeredMidChunkedRequest()
        {
            using (var host = StartHost())
            {
                using (var connection = new TestConnection(host.GetPort()))
                {
                    await connection.Send(
                            "POST / HTTP/1.1",
                            "Transfer-Encoding: chunked",
                            "",
                            "5", "hello",
                            "");
                    await Task.Delay(LongDelay);
                    await connection.Send(
                            "6", " world",
                            "0",
                             "",
                             "");
                    await ReceiveResponse(connection);
                }
            }
        }

        [Fact]
        public async Task ConnectionTimesOutWhenOpenedButNoRequestSent()
        {
            using (var host = StartHost())
            {
                using (var connection = new TestConnection(host.GetPort()))
                {
                    await Task.Delay(LongDelay);
                    await Assert.ThrowsAsync<IOException>(async () =>
                    {
                        await connection.Send(
                            "GET / HTTP/1.1",
                            "",
                            "");
                    });
                }
            }
        }

        private static IWebHost StartHost()
        {
            var host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.AddServerHeader = false;
                    options.Limits.KeepAliveTimeout = KeepAliveTimeout;
                })
                .UseUrls("http://127.0.0.1:0/")
                .Configure(app =>
                {
                    app.Run(async context =>
                    {
                        const string response = "hello, world";
                        context.Response.ContentLength = response.Length;
                        await context.Response.WriteAsync(response);
                    });
                })
                .Build();

            host.Start();
            return host;
        }

        private async Task ReceiveResponse(TestConnection connection)
        {
            await connection.Receive(
                "HTTP/1.1 200 OK",
                "");
            await connection.ReceiveStartsWith("Date: ");
            await connection.Receive(
                "Content-Length: 12",
                "",
                "hello, world");
        }
    }
}
