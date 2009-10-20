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
    public class NamedLock<T>
    {
        private static readonly Dictionary<T, RefCounter> _locks = new Dictionary<T, RefCounter>();
        private const int MAX_LOCK_TIMEOUT = 5000;

        public IDisposable Lock(T name)
        {
            return Lock(name, MAX_LOCK_TIMEOUT);
        }
        public IDisposable Lock(T name, int timeoutMilliseconds)
        {

            Monitor.Enter(_locks);
            RefCounter obj = null;
            _locks.TryGetValue(name, out obj);
            if (obj == null)
            {
                obj = new RefCounter();
                Monitor.Enter(obj);
                _locks.Add(name, obj);
                Monitor.Exit(_locks);
            }
            else
            {
                obj.AddRef();
                Monitor.Exit(_locks);
                if (!Monitor.TryEnter(obj, timeoutMilliseconds))
                {
                    throw new TimeoutException(String.Format( "Timeout while waiting for lock on '{0}' - possible deadlock", name));
                }
            }

            return new Token<T>(this, name);
        }

        private static void Unlock(T name)
        {
            lock (_locks)
            {
                RefCounter obj = null;
                _locks.TryGetValue(name, out obj);
                if (obj != null)
                {
                    Monitor.Exit(obj);
                    if (0 == obj.Release())
                    {
                        _locks.Remove(name);
                    }
                }
            }
        }

        public class RefCounter // public for unit tests only
        {
            private int _count = 1;
            public void AddRef()
            {
                Interlocked.Increment(ref _count);
            }

            public int Release()
            {
                return Interlocked.Decrement(ref _count);
            }
        }
        class Token<TK> : IDisposable
        {
            private readonly NamedLock<TK> _lock;
            private readonly TK _name;

            public Token(NamedLock<TK> myLock, TK myName)
            {
                _lock = myLock;
                _name = myName;
            }
            public void Dispose()
            {
                NamedLock<TK>.Unlock(_name);
            }
        }
    }
}