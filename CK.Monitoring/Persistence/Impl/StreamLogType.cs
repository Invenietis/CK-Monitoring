using System;

namespace CK.Monitoring.Impl
{
    [Flags]
    enum StreamLogType : ushort
    {
        EndOfStream = 0,

        TypeMask = 3,

        TypeLine = 1,
        TypeOpenGroup = 2,
        TypeGroupClosed = 3,

        HasTags = 4,
        HasException = 8,
        HasFileName = 16,
        IsTextTheExceptionMessage = 32,
        HasConclusions = 64,

        IsFullEntry = 128,

        HasUniquifier = 256,

        IsPreviousKnown = 512,
        IsPreviousKnownHasUniquifier = 1024,
        IsLFOnly = 2048,

        IsSimpleLogEntry = 4096,

        MaxFlag = 4096
    }
}
