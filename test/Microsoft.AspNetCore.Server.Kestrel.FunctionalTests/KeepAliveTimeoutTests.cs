// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
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
            using (var server = CreateServer())
            {
                using (var connection = new TestConnection(server.Port))
                {
                    await connection.Send(
                        "GET / HTTP/1.1",
                        "",
                        "");
                    await ReceiveResponse(connection, server.Context);

                    await Task.Delay(LongDelay);

                    await Assert.ThrowsAsync<IOException>(async () =>
                    {
                        await connection.Send(
                            "GET / HTTP/1.1",
                            "",
                            "");
                        await ReceiveResponse(connection, server.Context);
                    });
                }
            }
        }

        [Fact]
        public async Task ConnectionClosedWhenKeepAliveTimeoutExpiresAfterChunkedRequest()
        {
            using (var server = CreateServer())
            {
                using (var connection = new TestConnection(server.Port))
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
                    await ReceiveResponse(connection, server.Context);

                    await Task.Delay(LongDelay);

                    await Assert.ThrowsAsync<IOException>(async () =>
                    {
                        await connection.Send(
                            "GET / HTTP/1.1",
                            "",
                            "");
                        await ReceiveResponse(connection, server.Context);
                    });
                }
            }
        }

        [Fact]
        public async Task KeepAliveTimeoutResetsBetweenContentLengthRequests()
        {
            using (var server = CreateServer())
            {
                using (var connection = new TestConnection(server.Port))
                {
                    for (var i = 0; i < 10; i++)
                    {
                        await connection.Send(
                            "GET / HTTP/1.1",
                            "",
                            "");
                        await ReceiveResponse(connection, server.Context);
                        await Task.Delay(ShortDelay);
                    }
                }
            }
        }

        [Fact]
        public async Task KeepAliveTimeoutResetsBetweenChunkedRequests()
        {
            using (var server = CreateServer())
            {
                using (var connection = new TestConnection(server.Port))
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
                        await ReceiveResponse(connection, server.Context);
                        await Task.Delay(ShortDelay);
                    }
                }
            }
        }

        [Fact]
        public async Task KeepAliveTimeoutNotTriggeredMidContentLengthRequest()
        {
            using (var server = CreateServer())
            {
                using (var connection = new TestConnection(server.Port))
                {
                    await connection.Send(
                        "POST / HTTP/1.1",
                        "Content-Length: 8",
                        "",
                        "a");
                    await Task.Delay(LongDelay);
                    await connection.Send("bcdefgh");
                    await ReceiveResponse(connection, server.Context);
                }
            }
        }

        [Fact]
        public async Task KeepAliveTimeoutNotTriggeredMidChunkedRequest()
        {
            using (var server = CreateServer())
            {
                using (var connection = new TestConnection(server.Port))
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
                    await ReceiveResponse(connection, server.Context);
                }
            }
        }

        [Fact]
        public async Task ConnectionTimesOutWhenOpenedButNoRequestSent()
        {
            using (var server = CreateServer())
            {
                using (var connection = new TestConnection(server.Port))
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

        private TestServer CreateServer()
        {
            return new TestServer(App, new TestServiceContext
            {
                ServerOptions = new KestrelServerOptions
                {
                    AddServerHeader = false,
                    Limits =
                    {
                        KeepAliveTimeout = KeepAliveTimeout
                    }
                }
            });
        }

        private async Task App(HttpContext httpContext)
        {
            const string response = "hello, world";
            httpContext.Response.ContentLength = response.Length;
            await httpContext.Response.WriteAsync(response);
        }

        private async Task ReceiveResponse(TestConnection connection, TestServiceContext testServiceContext)
        {
            await connection.Receive(
                "HTTP/1.1 200 OK",
                $"Date: {testServiceContext.DateHeaderValue}",
                "Content-Length: 12",
                "",
                "hello, world");
        }
    }
}
