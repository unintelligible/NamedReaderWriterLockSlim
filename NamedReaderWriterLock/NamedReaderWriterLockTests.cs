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
using System.Threading;
using NUnit.Framework;

namespace NamedReaderWriterLock
{
    [TestFixture]
    public class NamedReaderWriterLockTests
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

        [Test]
        public void LockRead_ShouldRemoveTheLockWhenNoFurtherReferencesAreHeld()
        {
            using (_rwLock.LockRead("lock name"))
            {
                Assert.AreEqual(1, _lockDictionary.Count, "Expect the lock to be recorded in the dictionary");
            }
            Assert.AreEqual(0, _lockDictionary.Count, "Expect the lock to be removed from the dictionary when no more threads hold the lock");
        }

        [Test]
        public void LockWrite_ShouldRemoveTheLockWhenNoFurtherReferencesAreHeld()
        {
            using (_rwLock.LockWrite("lock name"))
            {
                Assert.AreEqual(1, _lockDictionary.Count, "Expect the lock to be recorded in the dictionary");
            }
            Assert.AreEqual(0, _lockDictionary.Count, "Expect the lock to be removed from the dictionary when no more threads hold the lock");
        }

        [Test]
        public void LockUpgradeableRead_ShouldRemoveTheLockWhenNoFurtherReferencesAreHeld()
        {
            using (_rwLock.LockUpgradeableRead("lock name"))
            {
                Assert.AreEqual(1, _lockDictionary.Count, "Expect the lock to be recorded in the dictionary");
            }
            Assert.AreEqual(0, _lockDictionary.Count, "Expect the lock to be removed from the dictionary when no more threads hold the lock");
        }

        [Test]
        public void Lock_WithThreadedCalls_ShouldRemoveTheLockWhenNoFurtherReferencesAreHeld()
        {
            const int threadCount = 50;
            const int lockSleepTime = 100;
            var threads = new Thread[threadCount];
            var manualResetEvents = new ManualResetEvent[threadCount];
            for (var i = 0; i < threadCount; i++)
            {
                var mre = new ManualResetEvent(false);
                manualResetEvents[i] = mre;
                var j = i;
                threads[i] = new Thread(() =>
                                            {
                                                if (j % 3 == 0)
                                                    using (_rwLock.LockRead("my lock"))
                                                    {

                                                        Thread.Sleep(lockSleepTime);
                                                    }
                                                else if (j % 3 == 1)
                                                    using (_rwLock.LockUpgradeableRead("my lock", 10000))
                                                    {

                                                        Thread.Sleep(lockSleepTime);
                                                    }
                                                else
                                                    using (_rwLock.LockWrite("my lock"))
                                                    {

                                                        Thread.Sleep(lockSleepTime);
                                                    }
                                                //at this point, the lock should be released
                                                mre.Set();
                                                //sleep a little longer before letting the thread die
                                                Thread.Sleep(1000);
                                            });
                threads[i].Start();
            }
            for (var i = 0; i < threadCount; i++)
            {
                if (!manualResetEvents[i].WaitOne(threadCount * lockSleepTime))
                    Assert.Fail("Failed waiting for the manualResetEvent to be set - likely error in the test");
            }
            //check all locks have been released
            Assert.AreEqual(0, _lockDictionary.Count, "Expect the lock to be removed from the dictionary when no more threads hold the lock");
        }
    }
}