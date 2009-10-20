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

namespace NamedReaderWriterLock
{
    public class NamedReaderWriterLockSlim<T>
    {
        private static readonly Dictionary<T, RefCounter> _locks = new Dictionary<T, RefCounter>();
        private const int TIMEOUT_MILLISECONDS = 5000;
        public IDisposable LockRead(T name)
        {
            return LockRead(name, TIMEOUT_MILLISECONDS);
        }

        public IDisposable LockRead(T name, int timeoutMilliseconds)
        {
            return WithLock(name, refCounter =>
                                      {

                                          if (!refCounter.RWLock.TryEnterReadLock(timeoutMilliseconds))
                                              throw new TimeoutException(String.Format("Timed out after {0}ms waiting to acquire read lock on '{1}' - possible deadlock", timeoutMilliseconds, name));
                                          return 0;
                                      }, refCounter =>
                                             {
                                                 refCounter.RWLock.ExitReadLock();
                                                 return refCounter.Refs;
                                             });
        }

        public IDisposable LockWrite(T name)
        {
            return LockWrite(name, TIMEOUT_MILLISECONDS);
        }

        public IDisposable LockWrite(T name, int timeoutMilliseconds)
        {
            return WithLock(name, refCounter =>
                                      {
                                          if (!refCounter.RWLock.TryEnterWriteLock(timeoutMilliseconds))
                                              throw new TimeoutException(String.Format("Timed out after {0}ms waiting to acquire write lock on '{1}' - possible deadlock", timeoutMilliseconds, name));
                                          return 0;
                                      }, refCounter =>
                                             {
                                                 refCounter.RWLock.ExitWriteLock();
                                                 return refCounter.Refs;
                                             });
        }

        public IDisposable LockUpgradeableRead(T name)
        {
            return LockUpgradeableRead(name, TIMEOUT_MILLISECONDS);
        }

        public IDisposable LockUpgradeableRead(T name, int timeoutMilliseconds)
        {
            return WithLock(name, refCounter =>
                                      {
                                          if (!refCounter.RWLock.TryEnterUpgradeableReadLock(timeoutMilliseconds))
                                              throw new TimeoutException(String.Format("Timed out after {0}ms waiting to acquire upgradeable read lock on '{1}' - possible deadlock", timeoutMilliseconds, name));
                                          return 0;
                                      }, refCounter =>
                                             {
                                                 refCounter.RWLock.ExitUpgradeableReadLock();
                                                 return refCounter.Refs;
                                             });
        }

        private static void WithUnlock(T name, Func<RefCounter, int> unlockAction)
        {
            lock (_locks)
            {
                RefCounter refCounter = null;
                _locks.TryGetValue(name, out refCounter);
                if (refCounter != null)
                {
                    if (0 == unlockAction(refCounter))
                    {
                        _locks.Remove(name);
                    }
                }
            }
        }

        private static IDisposable WithLock(T name, Func<RefCounter, int> lockAction, Func<RefCounter, int> unlockAction)
        {
            Monitor.Enter(_locks);
            RefCounter refCounter = null;
            _locks.TryGetValue(name, out refCounter);
            if (refCounter == null)
            {
                refCounter = new RefCounter();
                lockAction(refCounter);
                _locks.Add(name, refCounter);
                Monitor.Exit(_locks);
            }
            else
            {
                Monitor.Exit(_locks);
                lockAction(refCounter);
            }
            return new Token(() => WithUnlock(name, unlockAction));
        }

        public class RefCounter //public for unit tests - can be internal if the unit tests assembly can access this assembl_y
        {
            public readonly ReaderWriterLockSlim RWLock = new ReaderWriterLockSlim();

            public int Refs
            {
                get
                {
                    return RWLock.CurrentReadCount + RWLock.WaitingReadCount + RWLock.WaitingUpgradeCount + RWLock.WaitingWriteCount;
                }
            }
        }

        class Token : IDisposable
        {
            private readonly Action _fn;

            public Token(Action fn)
            {
                _fn = fn;
            }
            public void Dispose()
            {
                _fn();
            }
        }
    }
}