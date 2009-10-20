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

using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;

namespace NamedReaderWriterLock
{
    [TestFixture]
    public class NamedLockTests
    {
        [Test]
        public void Lock_ShouldRemoveTheLockWhenNoFurtherReferencesAreHeld()
        {
            var lockDictionary = (Dictionary<string, NamedLock<string>.RefCounter>)typeof(NamedLock<string>).GetField("_locks", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

            //var lockDictionary =
            //    new ReflectionHelper().GetPrivateStaticField
            //        <Dictionary<string, NamedLock<string>.RefCounter>>(
            //        typeof(NamedLock<string>), "_locks");
            Assert.AreEqual(0, lockDictionary.Count, "Expect no locks to be present before the test is run");
            var namedLock = new NamedLock<string>();
            using (namedLock.Lock("lock name"))
            {
                Assert.AreEqual(1, lockDictionary.Count, "Expect the lock to be recorded in the dictionary");
            }
            Assert.AreEqual(0, lockDictionary.Count, "Expect the lock to be removed from the dictionary when no more threads hold the lock");
        }

        [Test]
        public void WithoutLock_TestShouldBeValid()
        {
            var counter = 0;
            const int threadCount = 50;
            var threads = new Thread[threadCount];
            var areEqual = true;
            for (var i = 0; i < threadCount; i++)
            {
                threads[i] = new Thread(() =>
                                            {
                                                int currentCounter = ++counter;
                                                Thread.Sleep(25);
                                                if (currentCounter != counter)
                                                    areEqual = false;
                                            });
                threads[i].Start();
            }
            for (var i = 0; i < threadCount; i++)
            {
                threads[i].Join();
            }
            Assert.IsFalse(areEqual, "The test is not valid if unsynchronised access doesn't yield incorrect results");
        }

        [Test]
        public void Lock_ThreadedCalls_ShouldOnlyAllowASingleThreadToAccessTheProcessAtOnce()
        {
            var counter = 0;
            var namedLock = new NamedLock<string>();
            const int threadCount = 50;
            var areEqual = true;
            var threads = new Thread[threadCount];
            for (var i = 0; i < threadCount; i++)
            {
                threads[i] = new Thread(() =>
                                            {
                                                using (namedLock.Lock("my lock"))
                                                {
                                                    int currentCounter = ++counter;
                                                    Thread.Sleep(25);
                                                    if (currentCounter != counter)
                                                        areEqual = false;
                                                }
                                            });
                threads[i].Start();
            }
            for (var i = 0; i < threadCount; i++)
            {
                threads[i].Join();
            }
            Assert.IsTrue(areEqual, "Only a single thread should be able to modify the shared variable at any time");
        }
    }
}