Usage:

    private static NamedReaderWriterLock<string> locker = new NamedReaderWriterLock<string>();
    public void DoSomethingConcurrent(string lockName)
    {
        using (locker.LockRead(url))
        {
           //Do something concurrent that only requires
           //read access to the resource
        }
        using (locker.LockUpgradeableRead(url))
        {
           //Do something concurrent that only requires
           //read access to the resource, but that may require
           //upgrading to a Write lock later in the code
        }
        using (locker.LockWrite(url))
        {
           //Do something concurrent that requires
           //write access to the resource
        }
    }

See [the home page](http://unintelligible.org/blog/2009/10/20/named-reader-writer-lock-in-c/) for more information.
