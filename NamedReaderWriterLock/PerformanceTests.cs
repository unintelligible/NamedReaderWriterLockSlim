// Copyright (c) 2009, Nick Curry
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//     * Neither the name of the <organization> nor the
//       names of its contributors may be used to endorse or promote products
//       derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY <copyright holder> ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework.Extensions;
using NUnit.Framework;

namespace NamedReaderWriterLock
{
    [TestFixture]
    public class PerformanceTests
    {
        Dictionary<string, NamedReaderWriterLockSlim<string>.RefCounter> _lockDictionary;
        NamedReaderWriterLockSlim<string> _rwLock;
        [SetUp]
        public void SetUp()
        {
            _rwLock = new NamedReaderWriterLockSlim<string>();
            //_lockDictionary = new ReflectionHelper().GetPrivateStaticField<Dictionary<string, NamedReaderWriterLockSlim<string>.RefCounter>>(typeof(NamedReaderWriterLockSlim<string>), "_locks");
            _lockDictionary = (Dictionary<string, NamedReaderWriterLockSlim<string>.RefCounter>)typeof(NamedReaderWriterLockSlim<string>).GetField("_locks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
            //don't just clear this down, that could result in uncleared locks and strange test results
            Assert.AreEqual(0, _lockDictionary.Count, "Expect no locks to be present before the test is run");
        }

        [RowTest]
        [Row(1)]
        [Row(2)]
        [Row(3)]
        public void NameLock_vs_NamedReaderWriterLockSlim_PerformanceTest_Balanced(int run)
        {
            const int threadCount = 50;
            const int lockSleepTime = 100;
            var threads = new Thread[threadCount];
            var startTime = DateTime.Now;
            for (var i = 0; i < threadCount; i++)
            {
                var j = i;
                threads[i] = new Thread(() =>
                                            {
                                                try
                                                {
                                                    switch (j % 3)
                                                    {
                                                        case 0:
                                                            using (_rwLock.LockRead("my lock" + run))
                                                                Thread.Sleep(lockSleepTime);
                                                            break;
                                                        case 1:
                                                            using (_rwLock.LockUpgradeableRead("my lock" + run, 10000))
                                                                Thread.Sleep(lockSleepTime);
                                                            break;
                                                        default:
                                                            using (_rwLock.LockWrite("my lock" + run))
                                                                Thread.Sleep(lockSleepTime);
                                                            break;
                                                    }
                                                }
                                                catch (TimeoutException e)
                                                {
                                                    Console.WriteLine(e.Message);
                                                }
                                            });
                threads[i].Start();
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
            Console.WriteLine("Run {0} - finished NamedRWLock run in {1}ms", run, new TimeSpan(DateTime.Now.Ticks - startTime.Ticks).TotalMilliseconds);
            //NamedLock
            var namedLock = new NamedLock<string>();
            startTime = DateTime.Now;
            for (var i = 0; i < threadCount; i++)
            {
                var j = i;
                threads[i] = new Thread(() =>
                                            {
                                                try
                                                {
                                                    using (namedLock.Lock("my lock" + run, 10000))
                                                        Thread.Sleep(lockSleepTime);
                                                }
                                                catch (TimeoutException e)
                                                {
                                                    Console.WriteLine(e.Message);
                                                }
                                            });
                threads[i].Start();
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
            Console.WriteLine("Run {0} - finished NamedLock run in {1}ms", run, new TimeSpan(DateTime.Now.Ticks - startTime.Ticks).TotalMilliseconds);
            //Monitor
            startTime = DateTime.Now;
            var myMonitorLock = new object();
            for (var i = 0; i < threadCount; i++)
            {
                var j = i;
                threads[i] = new Thread(() =>
                                            {
                                                if (!Monitor.TryEnter(myMonitorLock, 10000))
                                                    Console.WriteLine("Failed to acquire monitor lock");
                                                Thread.Sleep(lockSleepTime);
                                                Monitor.Exit(myMonitorLock);
                                            });
                threads[i].Start();
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
            Console.WriteLine("Run {0} - finished Monitor run in {1}ms", run, new TimeSpan(DateTime.Now.Ticks - startTime.Ticks).TotalMilliseconds);
        }

        [RowTest]
        [Row(1)]
        [Row(2)]
        [Row(3)]
        public void NameLock_vs_NamedReaderWriterLockSlim_PerformanceTest_SkewedRead(int run)
        {
            const int threadCount = 50;
            const int lockReadSleepTime = 50;
            const int lockWriteSleepTime = 500;
            var threads = new Thread[threadCount];
            //NamedRWLock

            var startTime = DateTime.Now;
            for (var i = 0; i < threadCount; i++)
            {
                var j = i;
                threads[i] = new Thread(() =>
                                            {
                                                try
                                                {
                                                    if (j % 4 == 0)
                                                        using (_rwLock.LockWrite("my lock" + run, 10000))
                                                            Thread.Sleep(lockWriteSleepTime);
                                                    else
                                                        using (_rwLock.LockRead("my lock" + run, 10000))
                                                        {
                                                            Thread.Sleep(lockReadSleepTime);
                                                        }
                                                }
                                                catch (TimeoutException e)
                                                {
                                                    Console.WriteLine(e.Message);
                                                }
                                            });
                threads[i].Start();
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
            Console.WriteLine("Run {0} - finished NamedRWLock run in {1}ms", run, new TimeSpan(DateTime.Now.Ticks - startTime.Ticks).TotalMilliseconds);
            //NamedLock
            var namedLock = new NamedLock<string>();
            startTime = DateTime.Now;
            for (var i = 0; i < threadCount; i++)
            {
                var j = i;
                threads[i] = new Thread(() =>
                                            {
                                                try
                                                {
                                                    if (j % 4 == 0)
                                                        using (namedLock.Lock("my lock" + run, 10000))
                                                            Thread.Sleep(lockWriteSleepTime);
                                                    else
                                                        using (namedLock.Lock("my lock" + run, 10000))
                                                            Thread.Sleep(lockReadSleepTime);
                                                }
                                                catch (TimeoutException e)
                                                {
                                                    Console.WriteLine(e.Message);
                                                }
                                            });
                threads[i].Start();
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
            Console.WriteLine("Run {0} - finished NamedLock run in {1}ms", run, new TimeSpan(DateTime.Now.Ticks - startTime.Ticks).TotalMilliseconds);
            //Monitor
            startTime = DateTime.Now;
            var myMonitorLock = new object();
            for (var i = 0; i < threadCount; i++)
            {
                var j = i;
                threads[i] = new Thread(() =>
                                            {
                                                if (!Monitor.TryEnter(myMonitorLock, 10000))
                                                    Console.WriteLine("Failed to acquire monitor lock");
                                                if (j % 4 == 0)
                                                    Thread.Sleep(lockWriteSleepTime);
                                                else
                                                    Thread.Sleep(lockReadSleepTime);
                                                Monitor.Exit(myMonitorLock);
                                            });
                threads[i].Start();
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
            Console.WriteLine("Run {0} - finished Monitor run in {1}ms", run, new TimeSpan(DateTime.Now.Ticks - startTime.Ticks).TotalMilliseconds);
        }
    }
}
